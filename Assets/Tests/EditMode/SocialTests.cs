using System;
using System.Collections.Generic;
using System.Linq;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    public class SocialGraphTests
    {
        const string Me = "me";

        static readonly Dictionary<string, Profile> People = new Dictionary<string, Profile>
        {
            { "ana", new Profile { id = "ana", username = "ana", name = "Ana", current_book_id = "f451", current_page = 40 } },
            { "beto", new Profile { id = "beto", username = "beto", name = "Beto" } },
            { "caro", new Profile { id = "caro", username = "caro", name = "Caro" } },
            { "dani", new Profile { id = "dani", username = "dani", name = "Dani", is_creator = true } },
        };

        static Profile Lookup(string id) => People.TryGetValue(id, out var p) ? p : null;

        static List<Friendship> Rows() => new List<Friendship>
        {
            new Friendship { id = 1, requester = Me, addressee = "ana", status = FriendshipStatus.Accepted, shared_book_id = "f451" },
            new Friendship { id = 2, requester = "beto", addressee = Me, status = FriendshipStatus.Pending },
            new Friendship { id = 3, requester = Me, addressee = "caro", status = FriendshipStatus.Pending },
            new Friendship { id = 4, requester = Me, addressee = "ghost", status = FriendshipStatus.Accepted }, // no profile: skipped
        };

        [Test]
        public void Resolve_EveryKindOfRelation()
        {
            var g = SocialGraph.Build(Me, Rows(), new[] { "dani" }, Lookup);
            Assert.AreEqual(RelationKind.Self, g.Resolve(Me).Kind);
            Assert.AreEqual(RelationKind.Friend, g.Resolve("ana").Kind);
            Assert.AreEqual(RelationKind.Incoming, g.Resolve("beto").Kind);
            Assert.AreEqual(RelationKind.Outgoing, g.Resolve("caro").Kind);
            Assert.AreEqual(RelationKind.None, g.Resolve("dani").Kind);
            Assert.AreEqual(2L, g.Resolve("beto").Row.id);
            Assert.AreEqual(1, g.Friends.Count);
            Assert.IsTrue(g.CanSeeActivityOf("dani"));   // followed creator
            Assert.IsTrue(g.CanSeeActivityOf("ana"));    // friend
            Assert.IsFalse(g.CanSeeActivityOf("caro"));  // request not accepted yet
        }

        [Test]
        public void Pacts_And_ReadingNow_OnlyUseCatalogBooks()
        {
            var cat = TestData.Catalog();
            var rows = Rows();
            rows.Add(new Friendship { id = 5, requester = "dani", addressee = Me, status = FriendshipStatus.Accepted, shared_book_id = "not-in-catalog" });
            var g = SocialGraph.Build(Me, rows, null, Lookup);
            Assert.AreEqual(new[] { "ana" }, g.Pacts(cat).Select(c => c.Person.id).ToArray());
            Assert.AreEqual(new[] { "ana" }, g.ReadingNow(cat).Select(c => c.Person.id).ToArray());
        }

        [Test]
        public void Friends_AreSortedByName()
        {
            var rows = new List<Friendship>
            {
                new Friendship { requester = Me, addressee = "caro", status = FriendshipStatus.Accepted },
                new Friendship { requester = "ana", addressee = Me, status = FriendshipStatus.Accepted },
            };
            var g = SocialGraph.Build(Me, rows, null, Lookup);
            Assert.AreEqual(new[] { "ana", "caro" }, g.Friends.Select(c => c.Person.id).ToArray());
        }

        [Test]
        public void News_NewRequestsAndAcceptedOnes()
        {
            var before = SocialGraph.Build(Me, Rows(), null, Lookup);
            var rows = Rows();
            rows[2].status = FriendshipStatus.Accepted; // caro accepted
            rows.Add(new Friendship { id = 6, requester = "dani", addressee = Me, status = FriendshipStatus.Pending });
            var after = SocialGraph.Build(Me, rows, null, Lookup);
            var news = after.NewsSince(before);
            Assert.AreEqual(2, news.Count);
            Assert.IsTrue(news.Any(n => n.NewRequest && n.Person.id == "dani"));
            Assert.IsTrue(news.Any(n => !n.NewRequest && n.Person.id == "caro"));
            Assert.IsEmpty(after.NewsSince(SocialGraph.Empty), "a first load is not news");
        }
    }

    public class ActivityTests
    {
        static AppState Reading()
        {
            var s = new AppState();
            s.profile.onboardingComplete = true;
            ReadingService.SetCurrentBook(s, "f451");
            return s;
        }

        [Test]
        public void Diff_CheckInGivesPagesRow()
        {
            var cat = TestData.Catalog();
            var s = Reading();
            var before = ActivityTracker.Snapshot(s);
            Assert.IsTrue(ReadingService.CheckIn(s, cat, 25, TestData.Monday).Ok);
            var rows = ActivityTracker.Diff(before, s);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(ActivityKind.Pages, rows[0].kind);
            Assert.AreEqual("f451", rows[0].book_id);
            Assert.AreEqual(0, rows[0].data.from);
            Assert.AreEqual(25, rows[0].data.to);
        }

        [Test]
        public void Diff_StartedFinishedQuizAndJoined()
        {
            var s = new AppState();
            var before = ActivityTracker.Snapshot(s);
            s.profile.onboardingComplete = true;
            ReadingService.SetCurrentBook(s, "f451");
            s.ProgressFor("aura").finished = true;
            s.quizResults.Add(new QuizResult { bookId = "f451", chapterNumber = 1, correct = 2, total = 3, date = TestData.Monday });
            var kinds = ActivityTracker.Diff(before, s).Select(r => r.kind).ToList();
            CollectionAssert.AreEquivalent(new[] { ActivityKind.Joined, ActivityKind.Started, ActivityKind.Finished, ActivityKind.Quiz }, kinds);

            // Nothing changed since: no rows.
            Assert.IsEmpty(ActivityTracker.Diff(ActivityTracker.Snapshot(s), s));
        }

        [Test]
        public void Diff_PickingAFinishedBookIsNotStarted()
        {
            var s = Reading();
            s.ProgressFor("aura").finished = true;
            var before = ActivityTracker.Snapshot(s);
            ReadingService.SetCurrentBook(s, "aura");
            Assert.IsEmpty(ActivityTracker.Diff(before, s));
        }

        [Test]
        public void Describe_ReadsLikeTheWeb()
        {
            var cat = TestData.Catalog();
            Activity A(string kind, string book, ActivityData d = null) => new Activity { kind = kind, book_id = book, data = d ?? new ActivityData() };
            Assert.AreEqual("read 12 pages of Fahrenheit 451 · p. 40", ActivityText.Describe(A("pages", "f451", new ActivityData { from = 28, to = 40 }), cat));
            Assert.AreEqual("read 1 page of Fahrenheit 451 · p. 2", ActivityText.Describe(A("pages", "f451", new ActivityData { from = 1, to = 2 }), cat));
            Assert.AreEqual("finished Fahrenheit 451", ActivityText.Describe(A("finished", "f451"), cat));
            Assert.AreEqual("took the chapter 2 quiz for Fahrenheit 451: 3 of 4",
                ActivityText.Describe(A("quiz", "f451", new ActivityData { chapter = 2, correct = 3, total = 4 }), cat));
            Assert.AreEqual("reviewed Fahrenheit 451 · ★★★½", ActivityText.Describe(A("review", "f451", new ActivityData { rating = 3.5 }), cat));
            Assert.AreEqual("published “Short books”", ActivityText.Describe(A("post", null, new ActivityData { title = "Short books" }), cat));
            Assert.AreEqual("joined Margin", ActivityText.Describe(A("joined", null), cat));
            Assert.IsNull(ActivityText.Describe(A("pages", "unknown-book"), cat));
            Assert.IsNull(ActivityText.Describe(A("liked", "f451"), cat));
        }

        [Test]
        public void TimeAgo_Steps()
        {
            var now = new DateTime(2026, 11, 13, 12, 0, 0);
            Assert.AreEqual("now", ActivityText.TimeAgo(now.AddSeconds(-20), now));
            Assert.AreEqual("5 min", ActivityText.TimeAgo(now.AddMinutes(-5), now));
            Assert.AreEqual("3 h", ActivityText.TimeAgo(now.AddHours(-3), now));
            Assert.AreEqual("2 d", ActivityText.TimeAgo(now.AddDays(-2), now));
            Assert.AreEqual("Nov 2", ActivityText.TimeAgo(new DateTime(2026, 11, 2, 9, 0, 0), now));
        }
    }

    public class SummaryTests
    {
        [Test]
        public void Summary_PublicFieldsFromTheDiary()
        {
            var cat = TestData.Catalog();
            var s = new AppState();
            s.profile.onboardingComplete = true;
            ReadingService.SetCurrentBook(s, "f451");
            ReadingService.CheckIn(s, cat, 30, TestData.Monday);
            s.ProgressFor("aura").finished = true;

            var sum = ProfileSummary.From(s, TestData.Monday, true, new[] { "aura", "aura" }, l => l.ToString());
            Assert.AreEqual("f451", sum.current_book_id);
            Assert.AreEqual(30, sum.current_page);
            Assert.AreEqual(30, sum.pages_this_week);
            Assert.IsNotNull(sum.momentum_level);
            CollectionAssert.AreEqual(new[] { "aura" }, sum.books_read);
            CollectionAssert.AreEqual(new[] { "aura" }, sum.favorites);

            var hidden = ProfileSummary.From(s, TestData.Monday, false, null, l => l.ToString());
            Assert.IsNull(hidden.current_book_id);
            Assert.IsNull(hidden.current_page);
        }

        [Test]
        public void Summary_NoMomentumBeforeOnboarding()
        {
            var sum = ProfileSummary.From(new AppState(), TestData.Monday, true, null, l => l.ToString());
            Assert.IsNull(sum.momentum_level);
            Assert.IsNull(sum.current_book_id);
        }

        [Test]
        public void PactProgress_BothSides()
        {
            var book = new Book { id = "f451", pages = 200 };
            var s = new AppState();
            Assert.AreEqual("Not started", PactProgress.Mine(s, book).Text);
            s.ProgressFor("f451").currentPage = 50;
            Assert.AreEqual("Page 50 of 200", PactProgress.Mine(s, book).Text);
            Assert.AreEqual(0.25, PactProgress.Mine(s, book).Ratio, 1e-9);

            Assert.AreEqual("Not started yet", PactProgress.Theirs(new Profile { current_book_id = "aura" }, book).Text);
            Assert.AreEqual("Page 120 of 200", PactProgress.Theirs(new Profile { current_book_id = "f451", current_page = 120 }, book).Text);
            var done = PactProgress.Theirs(new Profile { books_read = new List<string> { "f451" } }, book);
            Assert.AreEqual("Finished", done.Text);
            Assert.AreEqual(1.0, done.Ratio);
        }
    }

    public class CommerceAndPostTests
    {
        [Test]
        public void Money_InPesos()
        {
            Assert.AreEqual("$249 MXN", Commerce.FormatMxn(249m));
            Assert.AreEqual("$1,234.50 MXN", Commerce.FormatMxn(1234.5m));
        }

        [Test]
        public void TopOffers_CheapestThreeWithALink()
        {
            var offers = new List<PriceOffer>
            {
                new PriceOffer { title = "a", price = 300, url = "u1" },
                new PriceOffer { title = "b", price = 85, url = "u2" },
                new PriceOffer { title = "c", price = 161, url = "u3" },
                new PriceOffer { title = "d", price = 0, url = "u4" },     // no price
                new PriceOffer { title = "e", price = 120, url = "" },     // no link
                new PriceOffer { title = "f", price = 500, url = "u6" },
            };
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, Commerce.TopOffers(offers).Select(o => o.title).ToArray());
            Assert.IsEmpty(Commerce.TopOffers(null));
        }

        [Test]
        public void Stores_SearchTheSpanishTitle()
        {
            var b = new Book { id = "the-little-prince", title = "The Little Prince", author = "Antoine de Saint-Exupéry" };
            Assert.AreEqual("El principito Antoine de Saint-Exupéry", Commerce.SearchQuery(b));
            var stores = Commerce.Stores(b);
            CollectionAssert.AreEqual(new[] { "Amazon México", "El Sótano" }, stores.Select(s => s.Name).ToArray());
            StringAssert.StartsWith("https://www.amazon.com.mx/s?k=El%20principito%20", stores[0].Url);
            StringAssert.EndsWith("&i=stripbooks", stores[0].Url);
            StringAssert.Contains("palabrasBusqueda=El%20principito%20", stores[1].Url);
            // Books without a Spanish title use the catalog title.
            Assert.AreEqual("Unknown X", Commerce.SearchQuery(new Book { id = "zzz", title = "Unknown", author = "X" }));
        }

        [Test]
        public void PriceFootnote_SaysPrintedOnly()
        {
            StringAssert.Contains("Printed books only", Commerce.PriceFootnote(new DateTime(2026, 11, 13, 9, 5, 0)));
            StringAssert.Contains("updated Nov 13, 09:05", Commerce.PriceFootnote(new DateTime(2026, 11, 13, 9, 5, 0)));
            StringAssert.Contains("Printed books only", Commerce.PriceFootnote(null));
        }

        [Test]
        public void FilBanner_HidesAfterTheFairOrWhenClosed()
        {
            var ends = new DateTime(2026, 12, 7);
            Assert.IsTrue(FilBanner.IsVisible(new DateTime(2026, 11, 13), false, ends));
            Assert.IsTrue(FilBanner.IsVisible(new DateTime(2026, 12, 6, 23, 0, 0), false, ends));
            Assert.IsFalse(FilBanner.IsVisible(new DateTime(2026, 12, 7), false, ends));
            Assert.IsFalse(FilBanner.IsVisible(new DateTime(2026, 11, 13), true, ends));
        }

        [TestCase("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("https://m.youtube.com/watch?feature=x&v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("https://youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("http://youtu.be/dQw4w9WgXcQ", null)]
        [TestCase("https://vimeo.com/12345", null)]
        public void YouTubeLinks(string url, string id)
        {
            Assert.AreEqual(id, PostRules.YouTubeId(url));
        }

        [Test]
        public void VideoUrl_IsStoredInTheShapeTheDatabaseAccepts()
        {
            Assert.AreEqual("https://www.youtube.com/watch?v=dQw4w9WgXcQ", PostRules.CanonicalVideoUrl("https://m.youtube.com/watch?v=dQw4w9WgXcQ"));
            Assert.IsNull(PostRules.CanonicalVideoUrl("nope"));
        }

        [Test]
        public void Post_Validation()
        {
            var one = new List<PostItem> { new PostItem { bookId = "f451" } };
            Assert.AreEqual("Give it a title.", PostRules.Validate(PostKind.List, " ", "", null, one));
            Assert.AreEqual("Add at least one book.", PostRules.Validate(PostKind.List, "T", "", null, new List<PostItem>()));
            Assert.IsNull(PostRules.Validate(PostKind.List, "T", "", null, one));
            Assert.AreEqual("Write a few more words.", PostRules.Validate(PostKind.Text, "T", "too short", null, null));
            Assert.IsNull(PostRules.Validate(PostKind.Text, "T", "one two three four five", null, null));
            Assert.AreEqual("Paste a YouTube link (youtube.com or youtu.be).", PostRules.Validate(PostKind.Video, "T", "", "https://vimeo.com/1", null));
            Assert.IsNull(PostRules.Validate(PostKind.Video, "T", "", "https://youtu.be/dQw4w9WgXcQ", null));
        }

        [Test]
        public void Post_CleanItems_TrimsAndDropsRepeats()
        {
            var items = PostRules.CleanItems(new List<PostItem>
            {
                new PostItem { bookId = "f451", note = "  why  " },
                new PostItem { bookId = "f451", note = "again" },
                new PostItem { bookId = "aura", note = new string('x', 300) },
                null,
            });
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("why", items[0].note);
            Assert.AreEqual(PostRules.NoteMax, items[1].note.Length);
        }

        [Test]
        public void BackendConfig_Validation()
        {
            var ok = new BackendConfig { supabaseUrl = "https://x.supabase.co", publishableKey = "sb_publishable_x" };
            Assert.IsEmpty(ok.Validate());
            Assert.AreEqual("https://x.supabase.co/rest/v1/profiles", ok.RestUrl("profiles"));
            Assert.AreEqual(new DateTime(2026, 12, 7), ok.fil.EndsOn);
            var secret = new BackendConfig { supabaseUrl = "https://x.supabase.co", publishableKey = "sb_secret_x" };
            Assert.IsNotEmpty(secret.Validate());
            Assert.IsNotEmpty(new BackendConfig().Validate());
        }
    }

    public class RestQueryTests
    {
        [Test]
        public void BuildsEscapedPostgrestPaths()
        {
            var q = RestQuery.From("profiles").Select("id,username").Eq("username", "ana").Neq("id", "me").Limit(1).ToString();
            Assert.AreEqual("profiles?select=id%2Cusername&username=eq.ana&id=neq.me&limit=1", q);
        }

        [Test]
        public void OrInAndOrder()
        {
            var q = RestQuery.From("friendships").Or("requester.eq.a", "addressee.eq.a").ToString();
            Assert.AreEqual("friendships?or=%28requester.eq.a%2Caddressee.eq.a%29", q);
            Assert.AreEqual("profiles?id=in.%28a%2Cb%29", RestQuery.From("profiles").In("id", new[] { "a", "b" }).ToString());
            Assert.AreEqual("activity?order=created_at.desc", RestQuery.From("activity").Order("created_at", false).ToString());
            Assert.AreEqual("follows?follower=eq.me&is_guest=eq.false", RestQuery.From("follows").Eq("follower", "me").Eq("is_guest", false).ToString());
            Assert.AreEqual("reviews?on_conflict=user_id%2Cbook_id", RestQuery.From("reviews").OnConflict("user_id,book_id").ToString());
            Assert.AreEqual("posts", RestQuery.From("posts").ToString());
        }
    }
}
