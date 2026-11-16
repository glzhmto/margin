using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Margin.App.Services.Net;
using Margin.Core;
using Newtonsoft.Json.Linq;

namespace Margin.App.Services
{
    /// <summary>
    /// Friends, follows, the activity feed, creators' posts and reviews: the same queries as the web
    /// (preview/js/net.js), so web and iPhone readers can be friends. The lists are cached here and
    /// refreshed by polling (see MarginApp): realtime websockets would need a socket client on iOS,
    /// and a 20-second poll while a social screen is open is enough for reading updates.
    /// </summary>
    public class SocialService
    {
        const string Profiles = "profiles";
        const string PostColumns = "*,profiles(username,name,color)";

        readonly SupabaseClient _sb;
        readonly AccountService _account;
        string _signature;

        public SocialGraph Graph { get; private set; } = SocialGraph.Empty;
        /// <summary>Profiles seen so far, by id.</summary>
        public Dictionary<string, Profile> People { get; } = new Dictionary<string, Profile>();
        /// <summary>Recent activity of friends and followed creators (newest first).</summary>
        public List<Activity> Feed { get; private set; } = new List<Activity>();
        public int Followers { get; private set; }
        public List<Post> MyPosts { get; private set; } = new List<Post>();
        public bool Loaded { get; private set; }

        /// <summary>New friend requests, or requests that were accepted, found by a refresh.</summary>
        public event Action<List<SocialNews>> News;

        /// <summary>Raised after a refresh that found something new (the web's Social.changed()).</summary>
        public event Action Updated;

        public SocialService(SupabaseClient sb, AccountService account)
        {
            _sb = sb;
            _account = account;
        }

        string Me => _account.UserId;

        public void Reset()
        {
            Graph = SocialGraph.Empty;
            People.Clear();
            Feed = new List<Activity>();
            MyPosts = new List<Post>();
            Followers = 0;
            Loaded = false;
            _signature = null;
        }

        /// <summary>Reloads friends, follows and the feed. True if anything changed since the last time.</summary>
        public async Task<bool> Refresh()
        {
            var me = Me;
            if (me == null) return false;
            var friendships = _sb.Select<Friendship>(RestQuery.From("friendships").Select("*").Or($"requester.eq.{me}", $"addressee.eq.{me}"));
            var follows = _sb.Select<FollowRow>(RestQuery.From("follows").Select("followee").Eq("follower", me));
            var followers = _sb.Count(RestQuery.From("follows").Select("follower").Eq("followee", me));
            var feed = _sb.Select<Activity>(RestQuery.From("activity").Select("*").Neq("user_id", me).Order("created_at", false).Limit(60));
            var posts = _account.IsCreator
                ? _sb.Select<Post>(RestQuery.From("posts").Select("*").Eq("author", me).Order("created_at", false).Limit(30))
                : Task.FromResult(Result<List<Post>>.Success(new List<Post>()));
            await Task.WhenAll(friendships, follows, followers, feed, posts);
            if (Me != me || !friendships.Result.Ok || !follows.Result.Ok || !feed.Result.Ok) return false;

            var rows = friendships.Result.Value;
            var followIds = follows.Result.Value.Select(f => f.followee).ToList();
            var activity = feed.Result.Value;
            var ids = rows.Select(r => r.OtherThan(me)).Concat(followIds).Concat(activity.Select(a => a.user_id)).Distinct().ToList();
            if (ids.Count > 0)
            {
                var people = await _sb.Select<Profile>(RestQuery.From(Profiles).Select(AccountService.ProfileColumns).In("id", ids));
                if (!people.Ok || Me != me) return false;
                foreach (var p in people.Value) People[p.id] = p;
            }

            var before = Graph;
            Graph = SocialGraph.Build(me, rows, followIds, id => People.TryGetValue(id, out var p) ? p : null);
            Feed = activity.Where(a => People.ContainsKey(a.user_id)).ToList();
            Followers = followers.Result.Ok ? followers.Result.Value : Followers;
            if (posts.Result.Ok) MyPosts = posts.Result.Value;
            bool firstLoad = !Loaded;
            Loaded = true;

            if (!firstLoad)
            {
                var news = Graph.NewsSince(before);
                if (news.Count > 0) News?.Invoke(news);
            }

            var signature = NetJson.Write(rows) + NetJson.Write(followIds) + NetJson.Write(Feed.Select(a => a.id)) + Followers + MyPosts.Count
                          + NetJson.Write(ids.Select(id => People.TryGetValue(id, out var p) ? p.current_page + p.current_book_id + p.momentum_level + p.name : ""));
            bool changed = signature != _signature;
            _signature = signature;
            if (changed) Updated?.Invoke();
            return changed;
        }

        [Serializable]
        class FollowRow { public string followee; }

        // ---------- Friends ----------

        /// <summary>Sends a friend request (unless there is already a row between us, in either direction).</summary>
        public async Task<string> Request(string userId)
        {
            var me = Me;
            var existing = await _sb.Select<Friendship>(RestQuery.From("friendships").Select("*")
                .Or($"and(requester.eq.{me},addressee.eq.{userId})", $"and(requester.eq.{userId},addressee.eq.{me})"));
            if (!existing.Ok) return existing.Error;
            if (existing.Value.Count == 0)
            {
                var r = await _sb.Insert("friendships", new JObject { ["requester"] = me, ["addressee"] = userId, ["status"] = FriendshipStatus.Pending });
                if (!r.Ok) return r.Error;
            }
            await Refresh();
            return null;
        }

        public Task<string> Accept(Friendship row) =>
            Then(_sb.Update(RestQuery.From("friendships").Eq("id", row.id), new JObject { ["status"] = FriendshipStatus.Accepted }));

        /// <summary>Declines, cancels or unfriends: the row is deleted either way.</summary>
        public Task<string> Remove(Friendship row) =>
            Then(_sb.Delete(RestQuery.From("friendships").Eq("id", row.id)));

        /// <summary>Starts, changes (bookId) or stops (null) reading a book together. Both friends see it.</summary>
        public Task<string> SetSharedBook(Friendship row, string bookId) =>
            Then(_sb.Update(RestQuery.From("friendships").Eq("id", row.id), new JObject { ["shared_book_id"] = bookId }));

        public async Task<string> Follow(string userId)
        {
            var r = await _sb.Insert("follows", new JObject { ["follower"] = Me, ["followee"] = userId });
            if (!r.Ok && r.Code != "23505") return r.Error; // 23505: already following
            await Refresh();
            return null;
        }

        public Task<string> Unfollow(string userId) =>
            Then(_sb.Delete(RestQuery.From("follows").Eq("follower", Me).Eq("followee", userId)));

        async Task<string> Then(Task<Result<bool>> call)
        {
            var r = await call;
            if (!r.Ok) return r.Offline ? Copy.NetFail : r.Error;
            await Refresh();
            return null;
        }

        // ---------- People ----------

        public async Task<Result<Profile>> ByUsername(string username)
        {
            var r = await _sb.SelectOne<Profile>(RestQuery.From(Profiles).Select(AccountService.ProfileColumns).Eq("username", AccountRules.NormalizeUsername(username)));
            if (r.Ok && r.Value != null) People[r.Value.id] = r.Value;
            return r;
        }

        public async Task<Result<Profile>> ById(string id)
        {
            var r = await _sb.SelectOne<Profile>(RestQuery.From(Profiles).Select(AccountService.ProfileColumns).Eq("id", id));
            if (r.Ok && r.Value != null) People[id] = r.Value;
            return r;
        }

        /// <summary>Readers whose @username or name contains the text (not guests, not deleted accounts).</summary>
        public async Task<Result<List<Profile>>> Search(string text)
        {
            var q = AccountRules.CleanQuery(text).TrimStart('@');
            if (q.Length < 2) return Result<List<Profile>>.Success(new List<Profile>());
            return await _sb.Select<Profile>(RestQuery.From(Profiles).Select(AccountService.ProfileColumns)
                .Or($"username.ilike.*{q}*", $"name.ilike.*{q}*")
                .Neq("id", Me).NotLike("username", AccountRules.DeletedPrefix + "*").Eq("is_guest", false).Limit(20));
        }

        /// <summary>People from my university I'm not connected to yet.</summary>
        public async Task<Result<List<Profile>>> SameUniversity()
        {
            var uni = AccountRules.CleanQuery(_account.Me?.university);
            if (uni.Length == 0) return Result<List<Profile>>.Success(new List<Profile>());
            var r = await _sb.Select<Profile>(RestQuery.From(Profiles).Select(AccountService.ProfileColumns)
                .ILike("university", uni).Neq("id", Me).Eq("is_guest", false).Limit(20));
            if (r.Ok) r.Value = r.Value.Where(p => Graph.Resolve(p.id).Kind == RelationKind.None).ToList();
            return r;
        }

        public Task<Result<List<Activity>>> ActivityOf(string userId, int count = 12) =>
            _sb.Select<Activity>(RestQuery.From("activity").Select("*").Eq("user_id", userId).Order("created_at", false).Limit(count));

        public Task<Result<List<Profile>>> Creators() =>
            _sb.Select<Profile>(RestQuery.From(Profiles).Select(AccountService.ProfileColumns).Eq("is_creator", true).Order("updated_at", false).Limit(30));

        // ---------- Posts ----------

        /// <summary>Latest posts; by one author, or book lists that include a book (the web's Social.posts).</summary>
        public Task<Result<List<Post>>> Posts(string author = null, int limit = 12, string bookId = null)
        {
            var q = RestQuery.From("posts").Select(PostColumns).Order("created_at", false).Limit(limit);
            if (author != null) q.Eq("author", author);
            if (bookId != null) q.Eq("kind", PostKind.List).Contains("items", new JArray(new JObject { ["bookId"] = bookId }).ToString(Newtonsoft.Json.Formatting.None));
            return _sb.Select<Post>(q);
        }

        /// <summary>How many readers there are on Margin (deleted accounts don't count).</summary>
        public Task<Result<int>> ReaderCount() =>
            _sb.Count(RestQuery.From(Profiles).Select("id").NotLike("username", AccountRules.DeletedPrefix + "*"));

        /// <summary>How many readers have each book as their current book (real counts from profiles).</summary>
        public async Task<Result<Dictionary<string, int>>> BookCounts()
        {
            var r = await _sb.SelectRaw(RestQuery.From(Profiles).Select("current_book_id").NotNull("current_book_id").Limit(2000));
            if (!r.Ok) return r.As<Dictionary<string, int>>();
            var counts = new Dictionary<string, int>();
            foreach (var row in r.Value)
            {
                var id = (string)row["current_book_id"];
                if (string.IsNullOrEmpty(id)) continue;
                counts.TryGetValue(id, out var n);
                counts[id] = n + 1;
            }
            return Result<Dictionary<string, int>>.Success(counts);
        }

        public Task<Result<Post>> Post(long id) => _sb.SelectOne<Post>(RestQuery.From("posts").Select(PostColumns).Eq("id", id));

        public async Task<Result<Post>> Publish(string kind, string title, string body, string videoUrl, List<PostItem> items)
        {
            var row = new JObject
            {
                ["author"] = Me,
                ["kind"] = kind,
                ["title"] = title.Trim(),
                ["body"] = (body ?? "").Trim(),
                ["video_url"] = kind == PostKind.Video ? PostRules.CanonicalVideoUrl(videoUrl) : null,
                ["items"] = kind == PostKind.List ? JArray.FromObject(PostRules.CleanItems(items)) : new JArray(),
            };
            var r = await _sb.Insert<Post>("posts", row);
            if (!r.Ok) return r.As<Post>();
            var post = r.Value.FirstOrDefault();
            if (post == null) return new Result<Post> { Ok = false, Error = Copy.NetFail };
            _account.Queue(new List<Activity>
            {
                new Activity
                {
                    kind = ActivityKind.Post,
                    book_id = kind == PostKind.List && post.items.Count > 0 ? post.items[0].bookId : null,
                    data = new ActivityData { post_id = post.id, title = post.title, kind = post.kind },
                },
            });
            await _account.Sync();
            await Refresh();
            return Result<Post>.Success(post);
        }

        public async Task<string> DeletePost(long id)
        {
            var r = await _sb.Delete(RestQuery.From("posts").Eq("id", id));
            if (!r.Ok) return r.Offline ? Copy.NetFail : r.Error;
            await _sb.Delete(RestQuery.From("activity").Eq("user_id", Me).Eq("kind", ActivityKind.Post).Contains("data", "{\"post_id\":" + id + "}"));
            await Refresh();
            return null;
        }

        // ---------- Reviews ----------

        public Task<Result<List<Review>>> Reviews(string bookId) =>
            _sb.Select<Review>(RestQuery.From("reviews").Select(PostColumns).Eq("book_id", bookId).Order("created_at", false).Limit(200));

        public async Task<string> SaveReview(string bookId, double rating, string body)
        {
            var row = new JObject { ["user_id"] = Me, ["book_id"] = bookId, ["rating"] = rating, ["body"] = (body ?? "").Trim() };
            var r = await _sb.Upsert("reviews", row, "user_id,book_id");
            if (!r.Ok) return r.Offline ? Copy.NetFail : r.Error;
            _account.Queue(new List<Activity> { new Activity { kind = ActivityKind.Review, book_id = bookId, data = new ActivityData { rating = rating } } });
            await _account.Sync();
            return null;
        }

        public async Task<string> DeleteReview(string bookId)
        {
            var r = await _sb.Delete(RestQuery.From("reviews").Eq("user_id", Me).Eq("book_id", bookId));
            return r.Ok ? null : r.Offline ? Copy.NetFail : r.Error;
        }
    }
}
