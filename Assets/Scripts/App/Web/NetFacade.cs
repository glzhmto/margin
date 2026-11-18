using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Margin.App.Services;
using Margin.App.Services.Net;
using Margin.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Margin.App.Web
{
    /// <summary>
    /// Thrown by the facade where the web's supabase call would throw (Social.search, bookReviews, publish...).
    /// Catch it like the web does (try/catch, or let ScreenLoad turn it into Err).
    /// </summary>
    public class NetException : Exception
    {
        public bool Offline;
        public NetException(string message, bool offline = false) : base(message) { Offline = offline; }
    }

    /// <summary>The sign-up form (the web's SignupScreen.v).</summary>
    public class SignupValues
    {
        public string name = "", email = "", username = "", password = "", university = "";
    }

    /// <summary>
    /// The web's Net object (preview/js/net.js): accounts, saving the private state, and the profile.
    /// Everything goes through AccountService (the real Supabase backend, same tables as the web).
    /// </summary>
    public static class Net
    {
        static AccountService A => MarginApp.Instance.Account;

        /// <summary>The auth user id (Net.uid), or null.</summary>
        public static string Uid => A.SignedIn ? A.UserId : null;
        public static Acc Acc => Accounts.Current;

        public static Task<bool?> UsernameAvailable(string u) => A.UsernameAvailable(u);

        public static Task<AuthOutcome> SignUp(SignupValues v) => A.SignUp(v.name, v.email, v.username, v.password, v.university);
        public static Task<AuthOutcome> SignIn(string email, string password) => A.LogIn(email, password);
        public static Task<AuthOutcome> SignInGuest(string name = "Guest") => A.ContinueAsGuest(name);

        /// <summary>Saves a guest account. Returns (error, pendingEmail) like the web's string | { pending }.</summary>
        public static Task<(string error, string pending)> SaveGuest(SignupValues v) => A.SaveGuest(v.name, v.email, v.username, v.password);

        public static async Task SignOut()
        {
            try { await A.SignOut(); }
            catch (Exception e) { Debug.LogWarning("[Margin] sign out: " + e.Message); A.ForgetLocalAccount(); }
            Social.Reset();
            Screens.ExploreScreen.Last = null;
        }

        public static Task<string> ResetPassword(string email) => A.ResetPassword(email);
        public static Task<bool> CheckPassword(string password) => A.CheckPassword(password);
        public static Task<string> SetPassword(string password) => A.SetPassword(password);
        public static Task<string> SetEmail(string email) => A.SetEmail(email);

        /// <summary>Net.updateProfile({ name, bio, university, is_creator... }): null, or the error (also toasted).</summary>
        public static async Task<string> UpdateProfile(JObject fields)
        {
            var e = await A.UpdateProfile(fields);
            if (e != null) Components.NetFail(new Exception(e));
            return e;
        }

        public static Task<string> ChangeUsername(string username) => A.ChangeUsername(username);

        /// <summary>
        /// Deletes the account for good (server function delete_my_account: the login and everything that
        /// hangs from it), forgets it on this phone and goes back to Welcome. False if it failed (toasted).
        /// </summary>
        public static async Task<bool> DeleteAccount()
        {
            var e = await A.DeleteAccount();
            if (e != null) { Components.Toast(e); return false; }
            MarginApp.Instance.ForgetDevice();
            Social.Reset();
            return true;
        }

        /// <summary>Net.log(rows): an activity row for friends and followers (skipped when activity is "Only me").</summary>
        public static void Log(string kind, string bookId = null, ActivityData data = null)
        {
            A.Queue(new List<Activity> { new Activity { kind = kind, book_id = bookId, data = data ?? new ActivityData() } });
            Flush();
        }

        /// <summary>Net.save(): same as Data.Save().</summary>
        public static void Save() => Data.Save();

        /// <summary>Net.flush(): sends what's pending now.</summary>
        public static Task Flush() => A.SignedIn ? A.Sync() : Task.CompletedTask;

        public static void FlushNow() => App.Run(Flush());
    }

    /// <summary>
    /// The web's Social object: friends, follows, the feed and creators' posts (cached, refreshed by polling).
    /// Same queries as net.js, through SocialService.
    /// </summary>
    public static class Social
    {
        static SocialService S => MarginApp.Instance.Social;

        public static Dictionary<string, Profile> Profiles => S.People;
        /// <summary>Accepted friends: x.Row is the friendship (the web's x.f), x.Person the profile (x.p).</summary>
        public static List<Connection> Friends => S.Graph.Friends;
        public static List<Connection> Incoming => S.Graph.Incoming;
        public static List<Connection> Outgoing => S.Graph.Outgoing;
        public static HashSet<string> FollowingIds => S.Graph.Following;
        public static List<Activity> Feed => S.Feed;
        public static int Followers => S.Followers;
        public static List<Post> MyPosts => S.MyPosts;
        public static bool Loaded => S.Loaded;

        public static void Reset() => S.Reset();

        /// <summary>Reloads everything; live screens rebuild if something changed.</summary>
        public static Task Refresh() => S.Refresh();

        /// <summary>Social.changed(): rebuilds the current screen if it is live and no sheet is open.</summary>
        public static void Changed()
        {
            var c = Nav.Current;
            if (c != null && c.Live && Sheet.Cur == null) { c.Loaded = false; Nav.Refresh(); }
        }

        static float _soonAt;

        /// <summary>Social.soon(): a refresh in a moment (several calls make one).</summary>
        public static void Soon()
        {
            _soonAt = Time.unscaledTime + 0.35f;
            var at = _soonAt;
            App.Root?.schedule.Execute(() => { if (Mathf.Approximately(at, _soonAt)) App.Run(Refresh()); }).StartingIn(350);
        }

        /// <summary>Social.rel(uid): self, friend, incoming, outgoing or none (with the friendship row).</summary>
        public static Relation Rel(string uid) => S.Graph.Resolve(uid);
        public static bool IsFriend(string uid) => S.Graph.IsFriend(uid);

        /// <summary>Friends with a shared book that is in the catalog.</summary>
        public static List<Connection> Pacts() => Friends.Where(x => !string.IsNullOrEmpty(x.Row.shared_book_id) && Data.Book(x.Row.shared_book_id) != null).ToList();

        // These toast on failure and refresh after, like the web.
        public static Task Request(string uid) => Toasting(S.Request(uid));
        public static Task Accept(Friendship row) => Toasting(S.Accept(row));
        public static Task Remove(Friendship row) => Toasting(S.Remove(row));
        public static Task SetPact(Friendship row, string bookId) => Toasting(S.SetSharedBook(row, bookId));
        public static Task Follow(string uid) => Toasting(S.Follow(uid));
        public static Task Unfollow(string uid) => Toasting(S.Unfollow(uid));

        static async Task Toasting(Task<string> call)
        {
            var e = await call;
            if (e != null) Components.NetFail(new Exception(e));
        }

        // ---------- Queries (throw NetException on failure, like the web) ----------

        public static async Task<Profile> ByUsername(string u) => Value(await S.ByUsername(u));

        public static async Task<Profile> ById(string id)
        {
            if (Profiles.TryGetValue(id, out var p)) return p;
            return Value(await S.ById(id));
        }

        public static async Task<List<Profile>> Search(string q) => Value(await S.Search(q));
        public static async Task<List<Profile>> SameUniversity() => Value(await S.SameUniversity());
        public static async Task<List<Activity>> ActivityOf(string uid, int n = 10) => Value(await S.ActivityOf(uid, n));
        public static async Task<int> ReaderCount() => Value(await S.ReaderCount());
        public static async Task<List<Profile>> Creators() => Value(await S.Creators());
        public static async Task<List<Post>> Posts(string author = null, int limit = 12, string bookId = null) => Value(await S.Posts(author, limit, bookId));
        public static async Task<Post> Post(long id) => Value(await S.Post(id));
        public static async Task<Dictionary<string, int>> BookCounts() => Value(await S.BookCounts());
        public static async Task<List<Review>> BookReviews(string bookId) => Value(await S.Reviews(bookId));

        /// <summary>Publishes a creator post; logs the activity and refreshes. Returns the new post.</summary>
        public static async Task<Post> Publish(string kind, string title, string body, string videoUrl, List<PostItem> items) =>
            Value(await S.Publish(kind, title, body, videoUrl, items));

        public static async Task DeletePost(long id) => Check(await S.DeletePost(id));
        public static async Task SaveReview(string bookId, double rating, string body) => Check(await S.SaveReview(bookId, rating, body));
        public static async Task DeleteReview(string bookId) => Check(await S.DeleteReview(bookId));

        static T Value<T>(Result<T> r)
        {
            if (!r.Ok) throw new NetException(r.Offline ? Copy.NetFail : r.Error ?? Copy.NetFail, r.Offline);
            return r.Value;
        }

        static void Check(string error)
        {
            if (error != null) throw new NetException(error);
        }
    }
}
