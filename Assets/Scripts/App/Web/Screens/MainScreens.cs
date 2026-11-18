// Port of preview/js/screens-main.js (Home, Explore, Library, Profile, Activity, Notifications)
// and preview/js/commerce.js (Shop: Gandhi prices and stores; Fil: the FIL 2026 banner).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Margin.App.Services;
using Margin.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Margin.App.Web.Components;
using static Margin.App.Web.H;

namespace Margin.App.Web.Screens
{
    /// <summary>Helpers of screens-main.js used by other screens too.</summary>
    public static class MainUI
    {
        /// <summary>Letters inside each achievement medal.</summary>
        public static readonly Dictionary<string, string> ACH_MARK = new Dictionary<string, string>
        {
            ["first-book"] = "1", ["ten-chapters"] = "10", ["month"] = "4", ["night-owl"] = "23", ["focus-10"] = "10",
            ["reflections-5"] = "5", ["theater"] = "III", ["mexico"] = "MX", ["paper"] = "5", ["year-12"] = "12",
        };

        /// <summary>bookLink(b): the title in serif, opens the book.</summary>
        public static VisualElement BookLink(Book b) =>
            Btn("button.strong", () => Nav.Push(new BookScreen(b.id)), b.title).Font(serif: true, weight: 600).FontSize(15.4f); // 1.06em of 14.5 px

        /// <summary>userLink(p): the first name, opens the profile.</summary>
        public static VisualElement UserLink(Profile p) => Btn("button.strong", () => Nav.Push(new UserScreen(p)), Pfirst(p));

        /// <summary>activityItem(a, { noUser, user }): one row of real activity. Null when it can't be shown.</summary>
        public static VisualElement ActivityItem(Activity a, bool noUser = false, Profile user = null)
        {
            if (a == null) return null;
            Profile u;
            if (a.user_id == null || !Social.Profiles.TryGetValue(a.user_id, out u) || u == null) u = user;
            if (u == null) return null;
            var b = !string.IsNullOrEmpty(a.book_id) ? Data.Book(a.book_id) : null;
            var d = a.data ?? new ActivityData();
            Action go = b != null ? (Action)(() => Nav.Push(new BookScreen(b.id))) : () => Nav.Push(new UserScreen(u));
            object[] what;
            switch (a.kind)
            {
                case "pages":
                    if (b == null) return null;
                    what = new object[] { "read ", El("span.num", F.Pages(Math.Max(0, (d.to ?? 0) - (d.from ?? 0)))), " of ", BookLink(b), El("span.faint.num", $" · p. {d.to}") };
                    break;
                case "finished":
                    if (b == null) return null;
                    what = new object[] { "finished ", BookLink(b) };
                    break;
                case "quiz":
                    if (b == null) return null;
                    what = new object[] { "took the chapter ", $"{d.chapter}", " quiz for ", BookLink(b), $": {d.correct} of {d.total}" };
                    break;
                case "started":
                    if (b == null) return null;
                    what = new object[] { "started ", BookLink(b) };
                    break;
                case "review":
                    if (b == null) return null;
                    what = new object[] { "reviewed ", BookLink(b), " ", d.rating.HasValue && d.rating.Value > 0 ? Stars(d.rating.Value, 11) : null };
                    break;
                case "post":
                    what = new object[] { "published ", El("span.strong", $"“{(string.IsNullOrEmpty(d.title) ? "a post" : d.title)}”") };
                    var postId = d.post_id ?? 0;
                    go = () => Nav.Push(new PostScreen(postId));
                    break;
                case "joined":
                    what = new object[] { "joined Margin" };
                    break;
                default:
                    return null;
            }
            return Btn("div.feeditem", go,
                Btn("button", () => Nav.Push(new UserScreen(u)), Aria(Pname(u)), Avatar(Pav(u), 36)),
                El("div.grow",
                    El("div", Css(s => s.fontSize = 14.5f), noUser ? null : new object[] { UserLink(u), " " }, what),
                    El("div.xs.faint.mt2", F.Ago(a.created_at, DateTime.Now))),
                b != null ? Cover(b, 44) : null);
        }

        /// <summary>friendsEmpty(text): the "No friends yet" card with the invite button.</summary>
        public static VisualElement FriendsEmpty(string text = null) =>
            El("div.card.block.empty",
                El("div.h3", "No friends yet"),
                El("p.small", text ?? "Invite someone and read together. You'll see their pages here."),
                Btn("button.btn.sm.primary.mt12", () => Nav.Push(new FriendsScreen()), Icon("users"), "Invite a friend").AlignSelf(Align.Center))
            .M(4, 16, 0);

        /// <summary>pactCard(x): "Reading together" card for one shared pact.</summary>
        public static VisualElement PactCard(Connection x)
        {
            var b = Data.Book(x.Row.shared_book_id);
            if (b == null) return null;
            var s = Data.State;
            var mine = s.progress.Find(p => p.bookId == b.id);
            var p = x.Person;
            var myTxt = mine != null && mine.finished ? "Finished" : mine != null && mine.currentPage > 0 ? $"Page {mine.currentPage} of {b.pages}" : "Not started";
            double tr; string tt;
            if ((p.books_read ?? new List<string>()).Contains(b.id)) { tr = 1; tt = "Finished"; }
            else if (p.current_book_id == b.id) { tr = (p.current_page ?? 0) / (double)b.pages; tt = $"Page {p.current_page ?? 0} of {b.pages}"; }
            else { tr = 0; tt = "Not started yet"; }
            VisualElement Line(string who, double ratio, string txt) =>
                El("div.mt8", El("div.row.xs", El("span.grow.strong", who), El("span.faint.num", txt)), El("div.mt4", Bar(ratio)));
            return Btn("button.card.block", () => Nav.Push(new UserScreen(p)),
                    El("div.row.top.gap12", Cover(b, 56),
                        El("div.grow", El("div.h3", b.title), El("div.small.muted", $"with {Pname(p)}"),
                            Line("You", mine != null && mine.finished ? 1 : (mine?.currentPage ?? 0) / (double)b.pages, myTxt),
                            Line(Pfirst(p), tr, tt))))
                .M(0, 16, 10).TextLeft();
        }

        /// <summary>listRow(l): one of the reader's lists.</summary>
        public static VisualElement ListRow(WebList l)
        {
            var owner = Data.Me();
            return Btn("button.pad", () => Nav.Push(new ListScreen(l.id)),
                    El("div.stackcov", l.books.Take(5).Select(id => Cover(Data.Book(id), 72))),
                    El("div.h3.mt8", l.title),
                    El("div.row.gap6.mt4.xs.muted", Avatar(owner, 20), El("span", owner?.Name ?? ""), El("span.dotsep"),
                        El("span.num", F.Books(l.books.Count)), El("span.dotsep"), El("span", "Only you see it")),
                    !string.IsNullOrEmpty(l.description) ? El("div.small.muted.mt4.clamp2", l.description) : null)
                .W("100%").TextLeft().P(10, 16, 16).BorderBottom();
        }
    }

    public class HomeScreen : WebScreen
    {
        public HomeScreen() { Live = true; Tab = "home"; }

        public override VisualElement Build()
        {
            var s = Data.State;
            var today = Clock.Today;
            var g = s.profile.goals;
            var m = HabitEngine.Compute(s, today);
            var weekly = HabitEngine.Weekly(s, today);
            var streak = Data.WeeksMeetingGoal(s, today);
            var book = Data.Book(s.profile.currentBookId);
            var seenAt = Data.X.notificationsSeen ?? "";
            bool unread = Data.Notifications().Any(n => string.CompareOrdinal(n.at ?? "", seenAt) > 0) || Social.Incoming.Count > 0;

            var head = El("div.row.pad", El("div.wordmark.grow", "margin"),
                Clock.IsSimulated ? El("span.xs.accent.strong", $"+{Clock.DayOffset} d") : null,
                Btn("button.iconbtn", () => Nav.Push(new FriendsScreen()), Aria("Friends"), Icon("users"), Social.Incoming.Count > 0 ? El("span.dot") : null),
                Btn("button.iconbtn", () => Nav.Push(new NotificationsScreen()), Aria("Notifications"), Icon("bell"), unread ? El("span.dot") : null))
                .Hgt(48);

            // Reading momentum
            var week = El("div.week", Enumerable.Range(0, 7).Select(i =>
            {
                var d = weekly.WeekStart.AddDays(i);
                int p = s.PagesOn(d);
                bool rest = !g.IsReadingDay(d);
                VisualElement fill = null;
                if (p > 0)
                {
                    fill = El("i");
                    fill.style.height = Length.Percent((float)(Math.Min(1.0, p / (double)Math.Max(1, g.dailyPages)) * 100));
                }
                var mk = El("div.m" + (rest && p == 0 ? ".rest" : "") + (D.Same(d, today) ? ".today" : ""),
                    Aria($"{Fmt.Days[(int)d.DayOfWeek]}: {F.Pages(p)}"), fill);
                return El("div.d", mk, El("span.l", Copy.DayLetters[(int)d.DayOfWeek]));
            }));
            int left = weekly.Target - weekly.PagesRead;
            var mom = El("div.card.momentum",
                El("div.row", El("span.caps.grow", "Reading momentum"), El("span.xs.faint", m.Trend > 0 ? "rising" : m.Trend < 0 ? "falling" : "steady")),
                El("div.row.mt6.gap10", El("span.level", Copy.LevelName(m.Level)),
                    streak.Current > 0 ? El("span.small.muted", streak.Current == 1 ? "You met your goal last week" : $"{streak.Current} weeks in a row meeting your goal") : null)
                    .AlignItems(Align.FlexEnd), // baseline in the web
                week,
                El("div.row.mt12.small", El("span.grow.num", El("b", F.N(weekly.PagesRead)), $" of {F.N(weekly.Target)} pages this week"),
                    El("span.row.gap8", El("span.faint.num", left > 0 ? $"{left} to go" : "goal met"),
                        Btn("button.small.link", () => Nav.Push(new SettingsDetail("reading")), "Change"))),
                Bar(weekly.Target > 0 ? weekly.PagesRead / (double)weekly.Target : 0, "ink").Cls("mt6"));

            // Reading
            VisualElement reading;
            if (book != null)
            {
                var p = s.progress.Find(x => x.bookId == book.id) ?? new BookProgress { bookId = book.id };
                // The reader's edition (Edition), like LogSheet and the book page.
                int pages = s.PagesFor(book);
                var ch = Edition.ChapterAt(book, Math.Max(1, p.currentPage + 1), pages);
                int toEnd = ch != null ? Edition.ChapterEnd(book, ch, pages) - p.currentPage : 0;
                reading = El("div.card.block.mt12",
                    El("div.row.top.gap12", Cover(book, 72, () => Nav.Push(new BookScreen(book.id))),
                        El("div.grow", El("div.caps", "Reading"), El("div.h3.mt4", book.title), El("div.small.muted", book.author),
                            El("div.mt8", Bar(p.currentPage / (double)Math.Max(1, pages))),
                            El("div.xs.muted.mt6.num", $"Page {p.currentPage} of {pages}", ch != null ? El("span.dotsep") : null,
                                ch != null ? $"{toEnd} to finish “{ch.title}”" : null))),
                    El("div.row.gap8.mt12",
                        Btn("button.btn.sm.accent", () => LogSheet.Open(), "Log reading").Flex1(),
                        Btn("button.btn.sm.secondary", () => Nav.Push(new FocusScreen()), Icon("moon"), "Focus mode")))
                    .M(12, 16, 0);
            }
            else
            {
                // No current book. First book: the onboarding taste quiz (saved answers open on the matches).
                // Later books: "Find your next book", which also learns from what they read (the same quiz as in Explore).
                // It goes above the momentum card, so it's the first thing on Home.
                bool first = Data.Finished(s).Count == 0;
                bool matched = Data.X?.tastes != null && !TasteQuiz.IsEmpty(Data.X.tastes);
                reading = El("div.card.block",
                    El("div.row.top.gap12",
                        El("span.iconbtn", Icon("bookopen")).Bg("accent").Fg("#fff").Size(44, 44).Radius(22).FlexNone(),
                        El("div.grow", El("div.h3", first ? "Choose your first book" : "Choose your next book"),
                            El("p.small.muted.mt4", !first ? "Five questions, matched to what you've read."
                                : matched ? "We matched books from the catalog to your answers."
                                : "Answer a few questions and we'll match you with a book."))),
                    El("div.row.gap8.mt12",
                        Btn("button.btn.sm.accent", () => Nav.Push(first ? new TasteQuizScreen() : (WebScreen)new NextBookScreen()), first && matched ? "See my matches" : "Find my book").Flex1(),
                        Btn("button.btn.sm.secondary", () => Nav.Reset(new ExploreScreen(), "explore"), "Browse")))
                    .M(8, 16, 0);
            }

            // Shared pacts
            var pacts = Social.Pacts();
            object together = pacts.Count > 0 ? new object[] { Sect("Reading together"), pacts.Select(MainUI.PactCard).ToList() } : null;

            // Friends reading now (real profiles of accepted friends)
            var friends = Social.Friends.Where(x => Data.Book(x.Person?.current_book_id) != null).ToList();
            object friendsRow = friends.Count > 0 ? new object[]
            {
                Sect("Friends reading now", "All friends", () => Nav.Push(new FriendsScreen())),
                El("div.hscroll", friends.Take(12).Select(x =>
                    Btn("button.col.gap6", () => Nav.Push(new UserScreen(x.Person)),
                        El("div", Cover(Data.Book(x.Person.current_book_id), 72), El("div", Avatar(Pav(x.Person), 28)).Abs(null, -6, -6, null)).Relative(),
                        El("span.xs.muted.clamp2.center", Pfirst(x.Person)).Mt(4),
                        El("span.xs.faint.num", $"p. {x.Person.current_page ?? 0}"))
                    .W(72).AlignItems(Align.Center))),
            } : null;

            // Activity of friends and followed creators
            var feed = Social.Feed.Select(a => MainUI.ActivityItem(a)).Where(e => e != null).Take(6).ToList();
            bool hasPeople = Social.Friends.Count > 0 || Social.FollowingIds.Count > 0;
            var act = new object[]
            {
                Sect("Friends' activity", feed.Count > 0 ? "See all" : null, () => Nav.Push(new ActivityScreen())),
                feed.Count > 0 ? El("div", feed).BorderTop()
                    : !Social.Loaded ? El("p.pad.small.faint", "Loading…")
                    : hasPeople ? El("p.pad.small.muted", "Nothing new yet. When your friends log pages, finish a book or post, it shows up here.")
                    : MainUI.FriendsEmpty(),
            };

            // For you
            // The quiz answers nudge the reading-based picks (TasteQuiz.Boost); without answers this is the plain list.
            var recs = TasteQuiz.Boost(Recommender.Recommend(Data.Catalog, s, m.Level, Data.Books.Count), Data.Catalog, Data.X?.tastes, 6);
            var forYou = new object[]
            {
                Sect("For you"),
                El("div.hscroll", recs.Select(r => Btn("button.col", () => Nav.Push(new BookScreen(r.Book.id)),
                    Cover(r.Book, 96), El("div.xs.strong.mt6.clamp2", r.Book.title), El("div.xs.faint.clamp2", r.Reason)).W(96).TextLeft())),
            };

            var q = Data.QuoteOfDay(today);
            var quote = q != null
                ? El("div.pad.mt32", El("div.serif.italic", $"“{q.text}”"), El("div.xs.faint.mt6", $"{q.author}{(!string.IsNullOrEmpty(q.source) ? ", " + q.source : "")}"))
                : null;

            return El("div",
                El("div.scroll", head, AuthUI.GuestNote(true), book != null ? new object[] { mom, reading } : new object[] { reading, mom }, Fil.Banner(), together, friendsRow, act, forYou, quote, El("div.bottom-space")),
                Tabbar("home"));
        }
    }

    public class ActivityScreen : WebScreen
    {
        public ActivityScreen() { Live = true; }

        public override VisualElement Build()
        {
            var list = Social.Feed.Select(a => MainUI.ActivityItem(a)).Where(e => e != null).ToList();
            return El("div", Navbar("Activity"),
                El("div.scroll.under-nav",
                    list.Count > 0 ? (object)list : MainUI.FriendsEmpty("Add friends or follow creators to see what they read."),
                    list.Count > 0 ? El("p.center.xs.faint", "From your friends and the creators you follow.").P(20) : null)
                .BorderTop());
        }
    }

    public class NotificationsScreen : WebScreen
    {
        string _seen;

        public NotificationsScreen() { Live = true; }

        public override void OnShow()
        {
            if (_seen != null) return;
            _seen = Data.X.notificationsSeen ?? "";
            Data.X.notificationsSeen = D.Stamp(Clock.Now);
            Data.Save();
        }

        public override VisualElement Build()
        {
            var now = Clock.Now;
            var seen = _seen ?? Data.X.notificationsSeen ?? "";
            VisualElement Item(WebNotification n)
            {
                Action go = null;
                if (n.kind == "reminder") go = () => LogSheet.Open();
                else if (n.kind == "weekly") go = () => Nav.Reset(new DiaryScreen { Mode = "goals" }, "diary");
                var row = Btn("button.feeditem", go,
                    El("div.avatar.av36", "m").Bg("accent").Font(serif: true),
                    El("div.grow", El("div", n.text ?? "").FontSize(14.5f), El("div.xs.faint.mt2", F.Ago(n.at, now))));
                if (string.CompareOrdinal(n.at ?? "", seen) > 0) row.Bg("card");
                return row;
            }
            var reqs = Social.Incoming.Select(x => SocialUI.RequestRow(x)).ToList();
            var list = Data.Notifications().Where(n => !string.IsNullOrEmpty(n.text)).OrderByDescending(n => n.at ?? "", StringComparer.Ordinal).ToList();
            return El("div",
                Navbar("Notifications", new NavOpts { Right = Btn("button.iconbtn", () => Nav.Push(new SettingsDetail("reminders")), Aria("Notification settings"), Icon("gear")) }),
                El("div.scroll.under-nav",
                    reqs.Count > 0 ? new object[] { Sect("Friend requests"), reqs } : null,
                    list.Count > 0 ? (object)list.Select(Item).ToList()
                        : reqs.Count > 0 ? null
                        : El("div.empty", El("div.h3", "No notifications"), El("p.small", "Your reminders and friend requests show up here.")))
                .BorderTop());
        }
    }

    public class ExploreScreen : WebScreen
    {
        public string q = "";
        public List<Profile> users;

        Dictionary<string, int> counts;
        int readers;
        List<Profile> creators;
        List<Post> posts;
        VisualElement results;
        int _searchSeq;

        /// <summary>The last data loaded. A new Explore starts from it and refreshes in the background, so switching
        /// tabs doesn't show a half-built page that then jumps as sections arrive. Cleared on sign out.</summary>
        public static (Dictionary<string, int> counts, int readers, List<Profile> creators, List<Post> posts)? Last;

        public ExploreScreen()
        {
            Tab = "explore";
            if (Last is { } l) { counts = l.counts; readers = l.readers; creators = l.creators; posts = l.posts; }
        }

        public override void OnShow() => ScreenLoad(async () =>
        {
            var c = Social.BookCounts(); var r = Social.ReaderCount(); var cr = Social.Creators(); var p = Social.Posts(limit: 8);
            await Task.WhenAll(c, r, cr, p);
            counts = c.Result; readers = r.Result; creators = cr.Result; posts = p.Result;
            Last = (counts, readers, creators, posts);
        });

        public override VisualElement Build()
        {
            var input = H.Input("input.input", q, v => { q = v; RenderResults(); SearchUsers(); }, placeholder: "Books, authors or readers");
            input.tooltip = "Search";
            input.style.paddingLeft = 40;
            input.style.height = 42;
            input.Radius(10);
            var glass = Icon("search").Tint("ink-3").Abs(11, null, null, 12);
            glass.pickingMode = PickingMode.Ignore;
            results = El("div");
            // The web's "Scan barcode" button only simulates a scan; a real ISBN scan needs a barcode decoder
            // that this project doesn't ship, so the button is left out here.
            var searchBar = El("div.row.gap8.pad", El("div.grow", input, glass).Relative()).Pt(8).Pb(8);
            var el = El("div",
                El("div", El("div.pad.h1", "Explore").Pt(6), searchBar).Pt(Safe.Top),
                El("div.scroll.under-nav", results),
                Tabbar("explore"));
            RenderResults();
            return el;
        }

        void SearchUsers()
        {
            var query = q.Trim();
            int seq = ++_searchSeq; // the web's clearTimeout
            var clean = CleanQuery(query);
            if (clean.StartsWith("@")) clean = clean.Substring(1);
            if (clean.Length < 2) { users = null; return; }
            LookUp(query, seq);
        }

        async void LookUp(string query, int seq)
        {
            try
            {
                await UnityEngine.Awaitable.WaitForSecondsAsync(0.25f); // runs on the main loop, also in WebGL (no threads)
                if (seq != _searchSeq || !IsCurrent) return;
                var u = await Social.Search(query);
                if (q.Trim() == query && IsCurrent) { users = u; RenderResults(); }
            }
            catch (Exception) { /* the web ignores search errors */ }
        }

        void RenderResults()
        {
            if (results == null) return;
            if (q.Trim().Length > 0)
            {
                var r = Data.Search(q);
                var us = users ?? new List<Profile>();
                bool none = r.Books.Count == 0 && us.Count == 0 && r.Lists.Count == 0;
                SetKids(results,
                    none ? El("div.empty", El("div.h3", $"Nothing for “{q}”"), El("p.small", "Check the spelling, search by author, or look for a friend by @username.")) : null,
                    r.Books.Count > 0 ? new object[]
                    {
                        Sect("Books"),
                        r.Books.Select(b => Btn("button.row.gap12.pad", () => Nav.Push(new BookScreen(b.id)), Cover(b, 44),
                            El("div.grow", El("div.strong", b.title), El("div.small.muted", $"{b.author} · {(b.year < 0 ? -b.year + " BC" : b.year.ToString(CultureInfo.InvariantCulture))}")))
                            .W("100%").TextLeft().P(8, 16)),
                    } : null,
                    us.Count > 0 ? new object[] { Sect("Readers"), us.Select(p => SocialUI.PersonRow(p)) } : null,
                    r.Lists.Count > 0 ? new object[] { Sect("Your lists"), r.Lists.Select(MainUI.ListRow) } : null);
                return;
            }
            bool d = counts != null && (Loaded ? Err == null : Last != null);
            var trending = d ? Data.Books.Where(b => counts.TryGetValue(b.id, out var n) && n > 0).OrderByDescending(b => counts[b.id]).Take(10).ToList() : new List<Book>();
            var shelves = Data.Shelves.Select(sh =>
            {
                var books = Data.Catalog.BooksInShelf(sh.id);
                return new object[]
                {
                    El("div.sect", El("span.caps", sh.name), El("span.more", F.Books(books.Count))),
                    El("div.hscroll", books.Select(b => Cover(b, 84, () => Nav.Push(new BookScreen(b.id))))),
                };
            }).ToList();
            var crs = d ? (creators ?? new List<Profile>()).Where(p => p.id != Net.Uid).ToList() : new List<Profile>();
            SetKids(results,
                d ? El("p.pad.small.muted.num", $"{F.N(readers)} {(readers == 1 ? "reader" : "readers")} on Margin").Mt(-2) : null,
                El("div.pad.mt8", Btn("button.card.block.row.gap12", () => Nav.Push(new FriendsScreen()),
                    El("span.iconbtn", Icon("users")).Bg("accent").Fg("#fff").Size(40, 40).Radius(20),
                    El("div.grow", El("div.strong", "Find friends"), El("div.small.muted", "Search by @username or share your invite link")),
                    Icon("chev", "chev")).W("100%").TextLeft()),
                El("div.pad.mt8", Btn("button.card.block.row.gap12", () => Nav.Push(new NextBookScreen()),
                    El("span.iconbtn", Icon("bookopen")).Bg("ink").Fg("paper").Size(40, 40).Radius(20),
                    El("div.grow", El("div.strong", "Find your next book"), El("div.small.muted", "Five questions, matched to what you've read")),
                    Icon("chev", "chev")).W("100%").TextLeft()),
                trending.Count > 0 ? new object[]
                {
                    Sect("Being read on Margin"),
                    El("div.hscroll", trending.Select((b, i) => Btn("button.col", () => Nav.Push(new BookScreen(b.id)),
                        Cover(b, 96, null, i + 1), El("div.xs.muted.mt6.num", $"{F.N(counts[b.id])} reading now")).W(96).TextLeft())),
                } : null,
                // Nothing from the server yet: one quiet line instead of empty sections that would move when data arrives.
                !d ? El("p.pad.small.faint.mt16", Err ?? "Loading…") : Sect("Creators"),
                !d ? null
                    : crs.Count > 0 || Data.Creators.Count > 0 ? El("div.hscroll", crs.Select(p => SocialUI.CreatorCard(p)), Data.Creators.Select(c => BookUI.WebCreatorCard(c)))
                    : El("p.pad.small.muted", "No creators yet. Anyone can become one in Settings › Profile and publish lists, notes and videos about books."),
                d && posts != null && posts.Count > 0 ? new object[] { Sect("Latest from creators"), posts.Select(p => SocialUI.PostRow(p)) } : null,
                shelves,
                El("div.bottom-space"));
        }
    }

    public class DiaryScreen : WebScreen
    {
        /// <summary>"library", "diary", "stats" or "goals" (other screens open a tab directly: new DiaryScreen { Mode = "goals" }).</summary>
        public string Mode = "library";
        public int Year;

        public DiaryScreen() { Tab = "diary"; Year = Clock.Today.Year; }

        public override VisualElement Build()
        {
            var modes = new[] { ("library", "Books"), ("diary", "Diary"), ("stats", "Stats"), ("goals", "Goals") };
            var top = El("div",
                El("div.row.pad", El("div.h1.grow", "Library")).Pt(6),
                El("div.pad.mt12", El("div.seg", modes.Select(t => Btn("button" + (Mode == t.Item1 ? ".on" : ""), () => { Mode = t.Item1; Nav.Refresh(); }, t.Item2)))).Pb(10))
                .Pt(Safe.Top);
            object body = Mode == "library" ? LibraryBody() : Mode == "diary" ? DiaryBody() : Mode == "stats" ? Stats() : Goals();
            return El("div", top, El("div.scroll.under-nav", body), Tabbar("diary"));
        }

        /// <summary>
        /// The reader's own library: reading now, want to read, and every book read, including the ones read before
        /// Margin, which they add by hand (they count as finished books, not as logged pages).
        /// </summary>
        object LibraryBody()
        {
            var s = Data.State;
            var cur = Data.Book(s.profile.currentBookId);
            var read = Data.Finished(s);
            VisualElement Shelf(IEnumerable<(Book b, string sub)> list) => El("div.grid3.pad.mt8", list.Where(x => x.b != null).Select(x =>
                Btn("button.col.gap4", () => Nav.Push(new BookScreen(x.b.id)), Cover(x.b, 110).W("100%"), El("div.xs.strong.clamp2", x.b.title), x.sub != null ? El("div.xs.faint", x.sub) : null).TextLeft()));
            string Sub(FinishedBook p) => string.Join(" · ", new[] { p.Rating > 0 ? new string('★', (int)Math.Round(p.Rating)) : null, p.FinishedOn?.Substring(0, 4) }.Where(t => !string.IsNullOrEmpty(t)));
            return new object[]
            {
                El("div.pad.mt4", Btn("button.btn.accent", LibraryAdd.Open, Icon("plus"), "Add books you've read")),
                El("p.pad.xs.faint.mt6", "Books you read before Margin count too. Add them so your library and recommendations know you."),
                cur != null ? new object[] { Sect("Reading now"), Shelf(new[] { (cur, $"Page {s.ProgressFor(cur.id).currentPage} of {s.PagesFor(cur)}") }) } : null,
                Data.X.want.Count > 0 ? new object[] { Sect("Want to read", F.N(Data.X.want.Count)), Shelf(Data.X.want.Select(id => (Data.Book(id), (string)null))) } : null,
                Sect("Read", F.N(read.Count)),
                read.Count > 0 ? (object)Shelf(read.Select(p => (Data.Book(p.BookId), Sub(p))))
                    : El("p.pad.small.muted", "Nothing here yet. Finish a book, or add the ones you've already read."),
                El("div.bottom-space"),
            };
        }

        static DateTime Created(AppState s) => s.profile.createdOn != DateTime.MinValue ? s.profile.createdOn : Clock.Today;

        class Entry { public string Date; public string BookId; public int From; public int To; }

        object DiaryBody()
        {
            var s = Data.State;
            // Group by day and book
            var byKey = new Dictionary<string, Entry>();
            var order = new List<Entry>();
            foreach (var l in s.logs)
            {
                var date = D.Key(l.date);
                var k = date + "|" + l.bookId;
                if (!byKey.TryGetValue(k, out var e))
                {
                    e = new Entry { Date = date, BookId = l.bookId, From = l.fromPage, To = l.toPage };
                    byKey[k] = e;
                    order.Add(e);
                }
                e.From = Math.Min(e.From, l.fromPage);
                e.To = Math.Max(e.To, l.toPage);
            }
            var fins = new Dictionary<string, FinishedBook>();
            foreach (var p in Data.Finished(s)) fins[p.BookId + "|" + p.FinishedOn] = p;
            var entries = order.Where(e => Data.Book(e.BookId) != null).OrderByDescending(e => e.Date, StringComparer.Ordinal).ToList();
            if (entries.Count == 0)
                return El("div.empty", El("div.h3", "Your diary is empty"), El("p.small", "Every time you log pages it shows up here, grouped by month."),
                    Btn("button.btn.sm.accent.mt12", () => LogSheet.Open(), "Log reading").AlignSelf(Align.Center));
            var output = new List<object>();
            string month = null;
            int shown = 0;
            foreach (var e in entries)
            {
                if (shown > 220) break;
                var d = D.Parse(e.Date);
                var mk = e.Date.Substring(0, 7);
                if (mk != month)
                {
                    month = mk;
                    int pages = entries.Where(x => x.Date.StartsWith(mk, StringComparison.Ordinal)).Sum(x => x.To - x.From);
                    output.Add(El("div.month", El("span.h3", $"{Fmt.Months[d.Month - 1]} {d.Year}"), El("span.xs.faint.num", F.Pages(pages))));
                }
                var b = Data.Book(e.BookId);
                fins.TryGetValue(e.BookId + "|" + e.Date, out var fin);
                output.Add(Btn("button.dentry", () => Nav.Push(new BookScreen(b.id)),
                    El("div.day", El("div", d.Day.ToString(CultureInfo.InvariantCulture)), El("small", Fmt.Days[(int)d.DayOfWeek].Substring(0, 3))),
                    Cover(b, 44),
                    El("div.grow", El("div.strong.clamp2", b.title),
                        fin != null
                            ? El("div.row.gap6.mt2.small", El("span.badge.ok", "Finished"), fin.Rating > 0 ? Stars(fin.Rating, 12) : null)
                            : El("div.small.muted.num.mt2", $"pp. {e.From + 1}–{e.To}", El("span.dotsep"), F.Pages(e.To - e.From)))));
                shown++;
            }
            output.Add(El("p.center.xs.faint", $"You've been reading with Margin since {F.Date(Created(s))}.").P(20));
            return output;
        }

        object Stats()
        {
            var s = Data.State;
            int y = Year;
            var today = Clock.Today;
            var years = new List<int>();
            for (int k = today.Year; k >= Created(s).Year; k--) years.Add(k);
            var prefix = y.ToString(CultureInfo.InvariantCulture);
            var logsY = s.logs.Where(l => l.date.Year == y).ToList();
            int pages = logsY.Sum(l => l.toPage - l.fromPage);
            int days = logsY.Select(l => l.date.Date).Distinct().Count();
            var allFin = Data.Finished(s);
            var fin = allFin.Where(p => (p.FinishedOn ?? "").StartsWith(prefix, StringComparison.Ordinal)).ToList();
            var rated = fin.Where(p => p.Rating > 0).ToList();
            double avg = rated.Count > 0 ? rated.Average(p => p.Rating) : 0;
            var quiz = s.quizResults.Where(q => q.date.Year == y).ToList();
            int qc = quiz.Sum(q => q.correct), qt = quiz.Sum(q => q.total);
            var months = Enumerable.Range(1, 12).Select(mo => logsY.Where(l => l.date.Month == mo).Sum(l => l.toPage - l.fromPage)).ToArray();
            int maxM = Math.Max(1, months.Max());
            int curM = y == today.Year ? today.Month - 1 : -1;
            var hist = new int[10];
            foreach (var p in allFin)
                if (p.Rating > 0) hist[Math.Max(0, Math.Min(9, (int)Math.Round(p.Rating * 2, MidpointRounding.AwayFromZero) - 1))]++;
            var shelfCount = new Dictionary<string, int>();
            var shelfOrder = new List<string>();
            foreach (var p in fin)
                foreach (var sh in Data.Book(p.BookId)?.shelves ?? new List<string>())
                {
                    if (!shelfCount.ContainsKey(sh)) { shelfCount[sh] = 0; shelfOrder.Add(sh); }
                    shelfCount[sh]++;
                }
            int maxShelf = shelfCount.Count > 0 ? shelfCount.Values.Max() : 1;

            return new object[]
            {
                El("div.row.gap6.pad.mt4", years.Select(k => Btn("button.pillbtn" + (k == y ? ".on" : ""), () => { Year = k; Nav.Refresh(); }, k.ToString(CultureInfo.InvariantCulture)))),
                El("div.statgrid.mt12",
                    El("div", El("div.v", fin.Count.ToString()), El("div.xs.muted", $"books finished · goal {Data.X.yearGoal}")),
                    El("div", El("div.v", F.N(pages)), El("div.xs.muted", "pages")),
                    El("div", El("div.v", days.ToString()), El("div.xs.muted", "reading days")),
                    El("div", El("div.v", $"{Math.Round(pages * 1.5 / 60, MidpointRounding.AwayFromZero)} h"), El("div.xs.muted", "approx., at 1.5 min per page")),
                    El("div", El("div.v", avg > 0 ? avg.ToString("0.0", CultureInfo.InvariantCulture) : "–"), El("div.xs.muted", "average rating")),
                    El("div", El("div.v", qt > 0 ? F.Pct(qc / (double)qt) : "–"), El("div.xs.muted", $"correct across {quiz.Count} quizzes"))),
                Sect("Pages per month"),
                El("div.pad", El("div.bars", months.Select((v, i) =>
                {
                    var bb = El("i" + (i == curM ? ".now" : ""));
                    bb.style.height = Length.Percent(v / (float)maxM * 100f);
                    return El("div.b", Aria($"{Fmt.Months[i]}: {F.Pages(v)}"), El("span.num", v > 0 ? F.K(v) : ""), bb, El("span", F.MES[i].Substring(0, 1).ToUpperInvariant()));
                }))),
                Sect("Your ratings"),
                El("div.row.gap8.pad", El("span.small", Stars(0.5, 10)), Histogram(hist), El("span.small", Stars(5, 10))),
                El("p.pad.xs.faint.mt6", $"All your rated books: {allFin.Count(p => p.Rating > 0)}."),
                Sect("By shelf"),
                El("div.pad",
                    shelfOrder.OrderByDescending(id => shelfCount[id]).Select(id =>
                    {
                        var fill = El("i");
                        fill.style.width = Length.Percent(shelfCount[id] / (float)maxShelf * 100f);
                        return El("div.row.gap10.mt8.breakdown",
                            El("span.small", Data.Catalog.FindShelf(id)?.name ?? id).W(110),
                            El("div.bar", fill),
                            El("span.xs.faint.num", shelfCount[id].ToString()).W(20).TextAlign(TextAnchor.UpperRight));
                    }),
                    fin.Count == 0 ? El("p.small.faint", "You haven't finished a book this year yet.") : null),
                El("div.bottom-space"),
            };
        }

        object Goals()
        {
            var s = Data.State;
            var today = Clock.Today;
            int y = today.Year;
            int done = Data.BooksInYear(s, y);
            var st = Data.WeeksMeetingGoal(s, today);
            int goal = Math.Max(1, Data.X.yearGoal);
            int weeksLeft = Math.Max(1, (int)Math.Round((new DateTime(y, 12, 31) - today.Date).TotalDays / 7, MidpointRounding.AwayFromZero));
            int toGo = goal - done;
            var g = s.profile.goals;
            return new object[]
            {
                // The daily goal first, with its own button (in user testing, 3 of 6 couldn't find where to change it).
                El("div.card.block",
                    El("div.caps", "Daily goal"),
                    El("div.row.mt6.gap6", El("span.level", F.N(g.dailyPages)).Font(serif: true, weight: 600).FontSize(30), El("span.muted", "pages a day")).AlignItems(Align.FlexEnd),
                    El("p.small.muted.mt4", $"{F.Pages(g.WeeklyTarget)} a week, {Fmt.DayList(g.readingDays)}."),
                    Btn("button.btn.sm.accent.mt12", () => Nav.Push(new SettingsDetail("reading")), "Change daily goal"))
                    .M(4, 16, 0),
                El("div.card.block",
                    El("div.caps", $"{y} goal"),
                    El("div.row.mt6.gap6", El("span.level", done.ToString()).Font(serif: true, weight: 600).FontSize(34), El("span.muted", $"of {F.Books(goal)}"))
                        .AlignItems(Align.FlexEnd), // baseline in the web
                    El("div.mt8", Bar(done / (double)goal, "ink")),
                    El("p.small.muted.mt8", done >= goal ? $"You already met your {y} goal." : $"{(toGo == 1 ? "1 book" : $"{toGo} books")} to go, with {weeksLeft} weeks left in the year."),
                    Btn("button.btn.sm.secondary.mt12", () => Nav.Push(new SettingsDetail("reading")), "Change goal"))
                    .M(12, 16, 0),
                El("div.statgrid.mt12",
                    El("div", El("div.v", st.Current.ToString()), El("div.xs.muted", st.Current == 1 ? "week meeting your goal (last week)" : "weeks in a row meeting your goal")),
                    El("div", El("div.v", st.Best.ToString()), El("div.xs.muted", st.Best == 1 ? "week is your best" : "weeks is your best")),
                    El("div", El("div.v", F.N(Data.ReadingDaysCount(s))), El("div.xs.muted", "reading days in total")),
                    El("div", El("div.v", (Data.X.focusSessions?.Count ?? 0).ToString()), El("div.xs.muted", "Focus mode sessions"))),
                El("p.pad.small.faint.mt12", "Missed weeks don't erase anything. Your best stays, and momentum drops little by little."),
                El("div.bottom-space"),
            };
        }
    }

    /// <summary>Your own profile (other readers open UserScreen).</summary>
    public class ProfileScreen : WebScreen
    {
        ScrollView _scroll;

        public ProfileScreen() { Live = true; Tab = "profile"; }

        /// <summary>Scrolls to the section with this title (the web's jump).</summary>
        void Jump(string title)
        {
            if (_scroll == null) return;
            var label = _scroll.Query<Label>(className: "caps").ToList()
                .FirstOrDefault(l => l.parent != null && l.parent.ClassListContains("sect") && string.Equals(l.text, title, StringComparison.OrdinalIgnoreCase));
            if (label != null) _scroll.ScrollTo(label.parent);
        }

        public override VisualElement Build()
        {
            var u = Data.Me();
            var s = Data.State;
            var acc = Accounts.Current;
            if (u == null || acc == null) return El("div", Navbar(""), El("div.scroll.under-nav"));
            var read = Data.Finished(s);
            var lists = Data.X.lists ?? new List<WebList>();
            int yearCount = Data.BooksInYear(s, Clock.Today.Year);
            var counters = El("div.counters",
                Btn("button", () => Nav.Reset(new DiaryScreen(), "diary"), El("div.v", read.Count.ToString()), El("div.k", "Books")),
                Btn("button", () => Nav.Reset(new DiaryScreen { Mode = "stats" }, "diary"), El("div.v", yearCount.ToString()), El("div.k", "This year")),
                Btn("button", () => Nav.Push(new FriendsScreen()), El("div.v", Social.Friends.Count.ToString()), El("div.k", "Friends")),
                acc.IsCreator
                    ? Btn("button", () => Jump("Your posts"), El("div.v", F.K(Social.Followers)), El("div.k", "Followers"))
                    : Btn("button", () => { if (lists.Count > 0) Jump("Lists"); else Toast("You don't have lists yet. Make one from a book's page."); },
                        El("div.v", lists.Count.ToString()), El("div.k", "Lists")));
            var right = Btn("button.iconbtn", () => Nav.Push(new SettingsScreen()), Aria("Settings"), Icon("gear"));
            var joined = D.Parse(!string.IsNullOrEmpty(u.Joined) ? u.Joined : D.Key(Clock.Today));
            var header = El("div.col.pad.center", Avatar(u, 88), El("div.h2.mt12", u.Name),
                    El("div.small.muted.mt2", $"@{u.Id}", El("span.dotsep"), $"on Margin since {F.MES[joined.Month - 1]} {joined.Year}"),
                    acc.IsCreator ? El("div.mt6", El("span.badge.ok", "Creator")) : null,
                    !string.IsNullOrEmpty(u.Bio) ? El("p.small.mt8", u.Bio).MaxW(300) : null,
                    El("div.row.gap8.mt12",
                        Btn("button.btn.sm.secondary", () => Nav.Push(new SettingsDetail("profile")), "Edit profile"),
                        Btn("button.btn.sm.secondary", () => Nav.Push(new FriendsScreen()), Icon("share"), "Invite")),
                    acc.IsGuest ? Btn("button.btn.sm.accent.mt8", () => Nav.Push(new SaveAccountScreen()), "Save your account") : null)
                .AlignItems(Align.Center);
            var favs = (Data.X.favorites ?? new List<string>()).Select(Data.Book).Where(b => b != null).ToList();
            var cur = Data.Book(s.profile.currentBookId);
            var hist = new int[10];
            foreach (var p in read)
                if (p.Rating > 0) hist[Math.Max(0, Math.Min(9, (int)Math.Round(p.Rating * 2, MidpointRounding.AwayFromZero) - 1))]++;
            object posts = acc.IsCreator ? new object[]
            {
                Sect("Your posts", "New post", () => Nav.Push(new PostEditor())),
                Social.MyPosts.Count > 0
                    ? (object)Social.MyPosts.Select(p => SocialUI.PostRow(WithAuthor(p, acc))).ToList()
                    : El("p.pad.small.muted", "Publish a list of books, a note or a video. Your followers see it in their feed."),
            } : null;
            var achs = Data.Achievements();
            int curPage = cur != null ? (s.progress.Find(x => x.bookId == cur.id)?.currentPage ?? 0) : 0;

            _scroll = (ScrollView)El("div.scroll.under-nav",
                header, counters,
                posts,
                favs.Count > 0 ? new object[] { Sect("Favorite books"), El("div.grid4.pad", favs.Select(b => Cover(b, 84, () => Nav.Push(new BookScreen(b.id))).W("100%"))) } : null,
                cur != null ? new object[]
                {
                    Sect("Reading now"),
                    Btn("button.row.gap12.pad", () => Nav.Push(new BookScreen(cur.id)), Cover(cur, 44),
                        El("div.grow", El("div.strong", cur.title), El("div.mt6", Bar(curPage / (double)cur.pages)), El("div.xs.faint.mt4.num", $"Page {curPage} of {cur.pages}")))
                        .W("100%").TextLeft(),
                } : null,
                Sect("Achievements", $"{achs.Count(a => a.Got)} of {achs.Count}"),
                El("div.ach", achs.Select(a => Btn("button.a" + (a.Got ? ".got" : ""),
                    () => IosAlert(a.Name, a.Detail + (a.Got ? "" : " You don't have it yet."), Act("Close", bold: true)),
                    El("div.medal", MainUI.ACH_MARK.TryGetValue(a.Id, out var mark) ? mark : a.Name.Substring(0, 1)), El("span.clamp2", a.Name)))),
                Sect("Finished", "Library", () => Nav.Reset(new DiaryScreen(), "diary")),
                read.Count > 0
                    ? El("div.hscroll", read.Take(12).Select(p =>
                    {
                        var b = Data.Book(p.BookId);
                        return b == null ? null : Btn("button.col", () => Nav.Push(new BookScreen(b.id)), Cover(b, 72), p.Rating > 0 ? El("div.mt4", Stars(p.Rating, 11)) : null).W(72);
                    }))
                    : El("p.pad.small.faint", "Books you finish show up here."),
                Sect("Ratings", read.Count(p => p.Rating > 0).ToString()),
                El("div.row.gap8.pad", Stars(0.5, 10), Histogram(hist), Stars(5, 10)),
                lists.Count > 0 ? new object[] { Sect("Lists"), lists.Select(MainUI.ListRow) } : null,
                Sect("Friends", Social.Friends.Count > 0 ? F.N(Social.Friends.Count) : null),
                Social.Friends.Count > 0
                    ? El("div.facepile.pad", Social.Friends.Take(12).Select(x => Btn("button", () => Nav.Push(new UserScreen(x.Person)), Aria(Pname(x.Person)), Avatar(Pav(x.Person), 36))))
                    : El("div.pad", Btn("button.btn.sm.secondary", () => Nav.Push(new FriendsScreen()), Icon("users"), "Find friends")),
                El("div.bottom-space"));

            return El("div", Navbar("", new NavOpts { Right = right }), _scroll, Nav.Count == 1 ? Tabbar("profile") : null);
        }

        /// <summary>{ ...p, profiles: { username, name, color } }: my post with my author fields.</summary>
        static Post WithAuthor(Post p, Acc acc) => new Post
        {
            id = p.id, author = p.author, kind = p.kind, title = p.title, body = p.body, video_url = p.video_url,
            items = p.items, created_at = p.created_at,
            profiles = new AuthorRef { username = acc.Username, name = acc.Name, color = acc.Color },
        };
    }

    // ---------------- commerce.js ----------------

    /// <summary>Buying the book on paper: real Gandhi prices (MarginApp.Instance.Prices) and store searches.</summary>
    public static class Shop
    {
        /// <summary>Spanish titles, used to search Mexican stores (Margin.Core.Commerce.SpanishTitles has them too).</summary>
        public static IReadOnlyDictionary<string, string> ES_TITLE => Commerce.SpanishTitles;

        /// <summary>Real prices from the book-prices edge function (cached). Throws NetException.</summary>
        public static async Task<PriceQuote> Prices(string bookId)
        {
            var r = await MarginApp.Instance.Prices.For(bookId);
            if (!r.Ok) throw new NetException(r.Error ?? Copy.NetFail, r.Offline);
            return r.Value;
        }

        /// <summary>"$249 MXN".</summary>
        public static string Money(decimal n) => Commerce.FormatMxn(n);

        /// <summary>The four store searches (name, color, url).</summary>
        public static List<StoreLink> Stores(Book b) => Commerce.Stores(b);

        /// <summary>The "Buy it" block for the Book screen (fills itself in when the prices arrive).</summary>
        public static object[] Section(Book b)
        {
            var box = El("div.list", El("div.item", El("div.grow", El("div.small.muted", "Checking prices at Gandhi…"))));
            Load(b, box);
            return new object[] { Sect("Buy it"), box };
        }

        static async void Load(Book b, VisualElement box)
        {
            PriceQuote data;
            try { data = await Prices(b.id); }
            catch (Exception e)
            {
                Debug.LogWarning("[Margin] prices: " + e.Message);
                data = new PriceQuote();
            }
            Fill(b, box, data ?? new PriceQuote());
        }

        static void Fill(Book b, VisualElement box, PriceQuote data)
        {
            var kids = new List<object>();
            var offers = Commerce.TopOffers(data.offers, 3);
            if (offers.Count > 0)
            {
                for (int i = 0; i < offers.Count; i++)
                {
                    var o = offers[i];
                    kids.Add(Btn("button.item", () => App.OpenUrl(o.url),
                        Badge("G", "#E2001A"),
                        El("div.grow", El("div.clamp2", o.title ?? b.title), El("div.xs.faint", i == 0 ? "Lowest price at Gandhi" : "Gandhi")),
                        El("span.num", Money(o.price)).Font(serif: false, weight: 600).NoWrap(),
                        Icon("chev", "chev")));
                }
            }
            else kids.Add(El("div.item", El("div.grow", El("div.small.muted", "Gandhi has no printed copies in stock right now."))));
            foreach (var st in Stores(b))
            {
                var url = st.Url;
                kids.Add(Btn("button.item", () => App.OpenUrl(url),
                    Badge(st.Name.Substring(0, 1), st.Color),
                    El("div.grow", El("div", st.Name), El("div.xs.faint", "Search this book")),
                    Icon("chev", "chev")));
            }
            var when = data.fetchedAt;
            kids.Add(El("p.groupfoot", when.HasValue
                ? $"Real prices from gandhi.com.mx, updated {F.MES[when.Value.Month - 1]} {when.Value.Day}, {when.Value.Hour:00}:{when.Value.Minute:00}. Printed books only. Margin earns nothing from these links."
                : "Prices from gandhi.com.mx."));
            SetKids(box, kids.ToArray());
        }

        static VisualElement Badge(string letter, string color) =>
            El("span.ic", letter).Bg(color).Fg("#fff").Font(serif: false, weight: 600).FontSize(13); // 700 in the web; 600 is the boldest font shipped
    }

    /// <summary>The FIL Guadalajara 2026 banner on Home (image Resources/FIL/fil-2026, BackendConfig.fil).</summary>
    public static class Fil
    {
        public const string KEY = "margin.fil26.hidden";

        /// <summary>The card for Home, or null after the fair or once the reader hid it.</summary>
        public static VisualElement Banner()
        {
            var cfg = MarginApp.Instance != null ? MarginApp.Instance.Backend?.fil : null;
            cfg = cfg ?? new FilConfig();
            var end = cfg.EndsOn != DateTime.MinValue ? cfg.EndsOn : new DateTime(2026, 12, 7); // the fair ends Dec 6, 2026
            if (!FilBanner.IsVisible(Clock.Today, PlayerPrefs.GetString(KEY, "") == "1", end)) return null;

            VisualElement card = null;
            var close = Btn("button.iconbtn", () =>
                {
                    PlayerPrefs.SetString(KEY, "1");
                    PlayerPrefs.Save();
                    card?.RemoveFromHierarchy();
                }, Aria("Hide"), "×")
                .Abs(6, 6, null, null).Size(28, 28).Radius(14).Bg("rgba(255,255,255,.85)");
            var img = H.Img(Resources.Load<Texture2D>(cfg.image));
            img.tooltip = "FIL Guadalajara 2026, 40th edition. Italy, guest of honor. Nov 28 to Dec 6.";
            img.style.width = Length.Percent(100);
            Layout.AspectRatio(img, 566f / 1080f);
            var url = string.IsNullOrEmpty(cfg.url) ? "https://www.fil.com.mx" : cfg.url;
            card = Btn("div.card", () => App.OpenUrl(url),
                    img,
                    El("div.row.gap10",
                        El("div.grow", El("div.small", "FIL Guadalajara · Nov 28 – Dec 6").Font(serif: false, weight: 600),
                            El("div.xs.faint", "The largest book fair in Spanish turns 40. Italy is the guest of honor.")),
                        El("span.btn.sm.accent", "Get tickets").FlexNone()).P(10, 14),
                    close)
                .M(12, 16, 0).P(0).Clip().Relative();
            return card;
        }
    }
}
