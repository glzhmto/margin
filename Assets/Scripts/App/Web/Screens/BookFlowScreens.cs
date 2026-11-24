// Port of preview/js/screens-book.js (book page, reviews, lists) and preview/js/screens-flow.js
// (log sheet, quiz, reflection, finished, start a book, end of session, Focus mode, app picker).
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
    /// <summary>What a check-in produced, passed along the flow (the web's outcome: { r, book, before } or { standalone }).</summary>
    public class Outcome
    {
        public CheckInResult R;
        public Book Book;
        public MomentumSnapshot Before;
        /// <summary>Achievements before the check-in, so FinishedScreen can show the ones it unlocked.</summary>
        public List<Achievement> AchBefore;
        /// <summary>Quiz opened from the book page (not after a check-in).</summary>
        public bool Standalone;
    }

    /// <summary>Helpers of screens-book.js used by other screens too.</summary>
    public static class BookUI
    {
        /// <summary>webCreatorCard(c): a real book creator's review from the web: who, a short quote, the book, the link.</summary>
        public static VisualElement WebCreatorCard(WebCreator c, bool full = false)
        {
            var b = Data.Book(c.bookId);
            string link = c.platform == "YouTube" ? "Watch the review ›" : c.platform == "TikTok" || c.platform == "Instagram" ? $"See it on {c.platform} ›" : "Read the review ›";
            var card = Btn("button.card.block", () => App.OpenUrl(c.reviewUrl),
                El("div.row.gap10", Avatar(new AvatarUser(c.name, c.color ?? "#3E5C76"), 36),
                    El("div.grow", El("div.strong.small", c.name), El("div.xs.muted", $"{c.handle} · {c.platform}"))),
                El("div.mt12", $"“{c.quote}”").Font(serif: true).FontSize(17).Fg("ink"),
                !string.IsNullOrEmpty(c.quoteEn) ? El("div.xs.muted.mt8", $"In English: “{c.quoteEn}”") : null,
                El("div.grow"),
                full || b == null ? null : El("div.row.gap8.mt12", Cover(b, 44), El("div.xs.muted.clamp2.grow", $"On {b.title}")),
                El("div.xs.mt12", link).Font(serif: false, weight: 600).Fg("accent")).TextLeft();
            if (full) card.M(8, 16, 0).AlignSelf(Align.Stretch);
            else card.W(272).FlexNone();
            return card;
        }

        public static readonly string[] REVIEWER_COLORS = { "#3E5C76", "#8A5A44", "#2F6B47", "#6B4E71", "#7A3B3B", "#4A6B5D", "#B5893B", "#5B554C" };
        public static readonly Dictionary<string, string> LANG_NAMES = new Dictionary<string, string> { ["es"] = "Spanish", ["en"] = "English" };

        /// <summary>reviewLang(r): the language the reviewer wrote in.</summary>
        public static string ReviewLang(ReaderReview r) => r.originalLang ?? r.lang ?? "en";

        /// <summary>The web's hash(str) (data.js, FNV-1a 32-bit), so reviewer colors match the web.</summary>
        public static uint Hash(string s)
        {
            uint x = 2166136261;
            foreach (var c in s ?? "") { x ^= c; x = unchecked(x * 16777619); }
            return x;
        }

        /// <summary>reviewer(r): avatar of a Goodreads reviewer.</summary>
        static AvatarUser Reviewer(ReaderReview r) => new AvatarUser(r.author, REVIEWER_COLORS[Hash(r.author) % (uint)REVIEWER_COLORS.Length]);

        /// <summary>A server time shown in the phone's time zone (the web's new Date(iso)).</summary>
        internal static DateTime Local(DateTime d) => d.Kind == DateTimeKind.Utc ? d.ToLocalTime() : d;

        internal static int Words(string t) => (t ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;

        /// <summary>reviewCard(r, { full }): a Goodreads review.</summary>
        public static VisualElement ReviewCard(ReaderReview r, bool full = false)
        {
            bool own = r.own;
            var lang = ReviewLang(r);
            string when;
            if (own) when = string.IsNullOrEmpty(r.date) ? "" : F.Date(r.date);
            else if (string.IsNullOrEmpty(r.date)) when = "";
            else { var d = D.Parse(r.date); when = $"{F.MES[d.Month - 1]} {d.Year}"; }
            double rating = r.ratingScale == 5 || r.ratingScale == 0 ? r.rating : r.rating / r.ratingScale * 5;
            var meta = El("div.meta",
                own ? Avatar(Data.Me(), 28) : Avatar(Reviewer(r), 28),
                El("span.strong", own ? "Your review" : r.author ?? "").Fg("ink"),
                r.rating > 0 ? Stars(rating, 11) : null,
                El("span.grow"),
                lang != "en" ? El("span.lang", lang.ToUpperInvariant()) : null);
            var body = El("div.body" + (full ? "" : ".clamp6"), r.text ?? "");
            var foot = El("div.foot", El("span.human", "Written by a human"), El("span.grow", when).Ellipsis());
            return full ? El("div.review", meta, body, foot) : Btn("div.review", () => Nav.Push(new ReviewScreen(r)), meta, body, foot);
        }

        /// <summary>marginReviewCard(r, { own, onEdit }): a review by a Margin reader.</summary>
        public static VisualElement MarginReviewCard(Review r, bool own = false, Action onEdit = null)
        {
            var p = r.profiles ?? new AuthorRef();
            var name = Pname(p);
            return El("div.review",
                El("div.meta",
                    Btn("button", () => { if (own) Nav.Push(new ProfileScreen()); else Nav.Push(new UserScreen(p.username)); }, Aria(name), Avatar(Pav(p), 28)),
                    El("span.strong", own ? "Your review" : name).Fg("ink"),
                    Stars(r.rating ?? 0, 11),
                    El("span.grow"),
                    own ? Btn("button.small.link", onEdit, "Edit") : null),
                string.IsNullOrEmpty(r.body) ? null : El("div.body", r.body),
                El("div.foot", El("span.human", "Written by a human"), El("span.grow", $"Margin, {F.Date(Local(r.created_at))}").Ellipsis()));
        }

        /// <summary>
        /// The book page's rateInput(val): five 26 px stars, tap the left half of a star for a half star,
        /// tap the current value again to clear it. Calls rate(v) with 0..5.
        /// </summary>
        public static VisualElement RateInput(double val, Action<double> rate)
        {
            var wrap = El("div.row");
            for (int i = 1; i <= 5; i++)
            {
                int n = i;
                double frac = Math.Max(0, Math.Min(1, val - (i - 1)));
                var baseRow = new VisualElement { pickingMode = PickingMode.Ignore };
                baseRow.AddToClassList("base");
                baseRow.Add(Icons.Make("star-fill").Size(26));
                var fillRow = new VisualElement { pickingMode = PickingMode.Ignore };
                fillRow.AddToClassList("fill");
                fillRow.Add(Icons.Make("star-fill").Size(26));
                fillRow.style.width = Length.Percent((float)(frac * 100));
                var star = new VisualElement { pickingMode = PickingMode.Ignore };
                star.AddToClassList("stars");
                star.Add(baseRow);
                star.Add(fillRow);
                star.style.width = 26;

                // The web reads e.offsetX on the click; Clickable has no position, so we keep the pointer-down x.
                float downX = 30;
                var b = Btn("button", () =>
                {
                    bool half = downX < 13;
                    double v = n - (half ? 0.5 : 0);
                    rate(Math.Abs(val - v) < 1e-9 ? 0 : v);
                }, Aria($"{n} stars"), star).Size(30, 30).Relative().PlaceCenter();
                b.RegisterCallback<PointerDownEvent>(e => downX = b.WorldToLocal(e.position).x, TrickleDown.TrickleDown);
                if (i > 1) b.style.marginLeft = 2;
                wrap.Add(b);
            }
            return wrap;
        }
    }

    // ---------------- screens-book.js ----------------

    public class BookScreen : WebScreen
    {
        public string id;
        public string revMode = "recent";

        // screenLoad data (the web's this.d)
        List<Review> reviews;
        int readingNow;
        List<Post> lists;

        // Onboarding: a first-time reader choosing a book sees one "Read this" button (onPick) instead of the status split.
        readonly bool onboarding;
        readonly Action onPick;

        public BookScreen(string id) { this.id = id; }
        public BookScreen(string id, bool onboarding, Action onPick) { this.id = id; this.onboarding = onboarding; this.onPick = onPick; }

        public override void OnShow() => ScreenLoad(async () =>
        {
            var rv = Social.BookReviews(id);
            var rd = Social.BookCounts();
            var ls = Social.Posts(limit: 5, bookId: id);
            await Task.WhenAll(rv, rd, ls);
            reviews = rv.Result ?? new List<Review>();
            readingNow = rd.Result != null && rd.Result.TryGetValue(id, out var c) ? c : 0;
            lists = ls.Result ?? new List<Post>();
        });

        void ReloadData() { Reload(); }

        bool HasData => Loaded && Err == null && reviews != null;

        /// <summary>Ratings from Margin reviews (real readers in the database).</summary>
        (int ratings, double average, int[] histogram) Stats()
        {
            var rv = HasData ? reviews : new List<Review>();
            var hist = new int[10];
            foreach (var r in rv) hist[Math.Max(0, Math.Min(9, (int)Math.Round((r.rating ?? 0) * 2, MidpointRounding.AwayFromZero) - 1))]++;
            return (rv.Count, rv.Count > 0 ? rv.Sum(r => r.rating ?? 0) / rv.Count : 0, hist);
        }

        static string Year(Book b) => b.year < 0 ? $"{-b.year} BC" : b.year.ToString(CultureInfo.InvariantCulture);
        static string Parts(Book b) => $"{b.chapters.Count} {(b.chapters.Count == 1 ? "part" : "parts")}";

        public override VisualElement Build()
        {
            var b = Data.Book(id);
            if (b == null) return El("div", Navbar("Book"), El("div.empty", El("div.h3", "This book is not in the catalog")));
            var s = Data.State;
            var st = Stats();
            var p = s.progress.Find(x => x.bookId == b.id);
            bool isCur = s.profile.currentBookId == b.id;
            bool want = Data.X.want.Contains(b.id), dnf = Data.X.dnf.Contains(b.id);
            bool finished = p != null && p.finished;
            // Nothing being read: the main action is to start this one (accent, like every main action).
            bool canStart = string.IsNullOrEmpty(s.profile.currentBookId) && !finished;
            string status = finished ? "Read" : isCur ? $"Reading · p. {(p != null ? p.currentPage : 0)}" : canStart ? "Start reading" : dnf ? "You dropped it" : want ? "On your want-to-read list" : "Want to read";
            double myRating = Data.Rating(b.id);
            int pages = s.PagesFor(b);

            // Hero: the cover blurred behind (filter: blur(26px)) and the shade gradient to the paper color.
            var bg = El("div.bg");
            var tex = Data.CoverTexture(b.id);
            if (tex != null)
            {
                var blurred = Fx.Blurred(tex);
                if (blurred is Texture2D bt) bg.style.backgroundImage = new StyleBackground(bt);
                else if (blurred is RenderTexture rt) bg.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            }
            else bg.Bg("ink-2");
            var paper = Theme.Color("paper");
            var shade = Fx.Gradient(El("div.shade"), (0f, new Color(20 / 255f, 18 / 255f, 15 / 255f, 0.25f)), (0.45f, new Color(20 / 255f, 18 / 255f, 15 / 255f, 0.05f)), (1f, paper));
            var hero = El("div.hero", bg, shade);
            var head = El("div.bookhead", Cover(b, 110), El("div.grow", Css(x => x.paddingBottom = 4),
                El("div.h1", b.title).FontSize(b.title.Length > 28 ? 22 : 27),
                El("div.mt4", b.author),
                El("div.small.muted.mt2.num", Year(b), El("span.dotsep"), $"{pages} pp.", El("span.dotsep"), Parts(b))));

            var actions = onboarding ? El("div.pad.mt16", Btn("button.btn.accent", () => onPick(), "Read this")) : El("div.pad.mt16",
                El("div.split" + (finished ? ".done" : canStart ? ".start" : ""),
                    Btn("button", Primary, finished ? new object[] { Icon("check"), " Read" } : new object[] { status }),
                    Btn("button", StatusSheet, Aria("Change status"), Icon("down"))),
                El("div.row.mt12", Css(x => x.justifyContent = Justify.SpaceBetween),
                    El("div", El("div.xs.faint", myRating > 0 ? "Your rating" : "Rate it"), BookUI.RateInput(myRating, Rate)),
                    El("div.row.gap6",
                        isCur ? Btn("button.btn.sm.accent", () => LogSheet.Open(), "Log reading") : null,
                        Btn("button.iconbtn", AddToList, Aria("Add to a list"), Icon("list")),
                        // The web only shows a toast; here the link really goes to the clipboard.
                        Btn("button.iconbtn", () => SocialUI.CopyText($"I'm reading {b.title} by {b.author} on Margin. https://marginreads.vercel.app", "Copied. Paste it anywhere."), Aria("Share"), Icon("share")))));

            // Real friends (accepted friendships) who finished it or are reading it now.
            var friendsRead = Social.Friends.Where(x => x.Person != null && (x.Person.books_read ?? new List<string>()).Contains(b.id)).Select(x => x.Person).ToList();
            var friendsNow = Social.Friends.Where(x => x.Person != null && x.Person.current_book_id == b.id).Select(x => x.Person).ToList();

            VisualElement ratings;
            if (!HasData) ratings = El("p.pad.small.faint", Err ?? "Loading ratings…");
            else if (st.ratings > 0)
                ratings = Btn("button.pad", Breakdown,
                    El("div.row.gap12",
                        El("div", El("div", st.average.ToString("0.0", CultureInfo.InvariantCulture)).Font(true, 600).FontSize(34), El("div.mt4", Stars(st.average, 12))),
                        Histogram(st.histogram, myRating)),
                    El("div.row.xs.faint.mt6.num", El("span.grow", $"{F.N(st.ratings)} {(st.ratings == 1 ? "rating" : "ratings")} from Margin readers"), El("span", "See breakdown"))).W("100%").TextLeft();
            else ratings = El("p.pad.small.muted", "No Margin ratings yet. Rate it and write a review to be the first.");

            string Names(List<Profile> ps) => ps.Count == 1 ? Pfirst(ps[0]) : $"{Pfirst(ps[0])} and {ps.Count - 1} {(ps.Count == 2 ? "other friend" : "other friends")}";
            var lines = new List<VisualElement>();
            if (HasData && readingNow > 0) lines.Add(El("div", El("b.num", F.N(readingNow)), $" {(readingNow == 1 ? "reader has" : "readers have")} it as their current book"));
            if (friendsNow.Count > 0) lines.Add(El("div.muted", $"{Names(friendsNow)} {(friendsNow.Count == 1 ? "is" : "are")} reading it now"));
            if (friendsRead.Count > 0) lines.Add(El("div.muted", $"{Names(friendsRead)} {(friendsRead.Count == 1 ? "has" : "have")} read it"));
            var faces = friendsNow.Concat(friendsRead).Take(3).ToList();
            var social = lines.Count > 0
                ? El("div.pad.mt16.row.gap10",
                    faces.Count > 0 ? El("div.facepile", faces.Select(f => (object)Btn("button", () => Nav.Push(new UserScreen(f)), Avatar(f, 28)))) : null,
                    El("div.small.grow", lines))
                : null;

            var grReviews = Data.ReviewsFor(b.id).OrderByDescending(r => r.date ?? "", StringComparer.Ordinal).ToList();
            var marginRv = HasData ? reviews : new List<Review>();
            var mineRv = marginRv.FirstOrDefault(r => r.user_id == Net.Uid);
            var others = marginRv.Where(r => r.user_id != Net.Uid).ToList();
            var revSect = new List<object>
            {
                El("div.sect", El("span.caps", "Margin reviews"), El("span.more.num", HasData ? F.N(marginRv.Count) : "")),
                mineRv != null
                    ? BookUI.MarginReviewCard(mineRv, true, () => ReviewSheet.Open(b.id, mineRv, ReloadData))
                    : El("div.pad.mt8", Btn("button.btn.sm.secondary", () => ReviewSheet.Open(b.id, null, ReloadData), "Write your review").W("100%")),
                others.Take(5).Select(r => BookUI.MarginReviewCard(r)),
                HasData && others.Count == 0 ? El("p.pad.small.faint.mt8", "No other Margin reader has reviewed it yet.") : null,
            };
            if (grReviews.Count > 0)
            {
                revSect.Add(El("div.sect", El("span.caps", "Reviews"), El("span.more.num", F.N(grReviews.Count))));
                revSect.Add(grReviews.Take(3).Select(r => BookUI.ReviewCard(r)));
                if (grReviews.Count > 3) revSect.Add(El("div.pad.mt8", Btn("button.btn.sm.secondary", () => Nav.Push(new ReviewsScreen(b.id)), $"See all {grReviews.Count} reviews").W("100%")));
                revSect.Add(El("p.pad.xs.faint.mt8", "Real reader reviews, quoted with their author. Margin doesn't summarize them or rewrite them with AI."));
            }
            var webCreators = Data.CreatorsFor(b.id);
            if (webCreators.Count > 0)
            {
                revSect.Add(El("div.sect", El("span.caps", "From book creators")));
                revSect.Add(webCreators.Select(c => BookUI.WebCreatorCard(c, full: true)));
                revSect.Add(El("p.pad.xs.faint.mt8", "Quoted with a link to the original review."));
            }

            // Chapter bounds follow the reader's edition (Core Edition), the same estimate the check-in uses.
            var chapters = new object[] { Sect("Chapters and quizzes"), El("div.pad", b.chapters.Select(c =>
            {
                var q = s.quizResults.Find(x => x.bookId == b.id && x.chapterNumber == c.number);
                int start = Edition.ChapterStart(b, c, pages), end = Edition.ChapterEnd(b, c, pages);
                bool read = p != null && p.currentPage >= end;
                return (object)Btn("button.chapter", () =>
                    {
                        if (read && c.HasQuiz) QuizFlow.Start(b, c, new Outcome { Standalone = true, Book = b });
                        else Toast(read ? "This chapter has no quiz." : $"The quiz shows up when you reach page {end}.");
                    },
                    El("span.n", c.number.ToString(CultureInfo.InvariantCulture)), El("span.grow" + (read ? "" : ".muted"), c.title),
                    El("span.xs.faint.num", $"{start}–{end}"),
                    q != null ? El("span.badge.ok.num", $"{q.correct}/{q.total}") : read && c.HasQuiz ? El("span.badge.todo", "Quiz") : null);
            })) };

            var inLists = Data.X.lists.Where(l => l.books.Contains(b.id)).ToList();
            var creatorLists = HasData ? lists : new List<Post>();
            object listsSect = inLists.Count > 0 || creatorLists.Count > 0
                ? new object[] { Sect("In lists"), creatorLists.Select(SocialUI.PostRow), inLists.Take(3).Select(MainUI.ListRow) }
                : null;

            var shelves = string.Join(", ", b.shelves.Select(x => Data.Catalog.FindShelf(x)?.name).Where(x => x != null));
            var facts = new object[] { Sect("Details"), El("div.facts.pad",
                El("div", "Author").Fg("ink-3"), El("div", b.author),
                El("div", "Published").Fg("ink-3"), El("div", Year(b)),
                El("div", "Length").Fg("ink-3"), El("div", $"{b.pages} pages, {Parts(b)}"),
                El("div", "Shelves").Fg("ink-3"), El("div", shelves),
                El("div", "Themes").Fg("ink-3"), El("div", string.Join(", ", b.tags ?? new List<string>()))) };

            var paperSect = new object[] { Sect("Get it on paper"), El("div.list",
                Btn("button.item", () => App.OpenUrl("https://www.google.com/maps/search/bookstores+near+me"), El("span.ic", Icon("pin")).Bg("#8A5A44"), El("div.grow", El("div", "Bookstores near you"), El("div.xs.faint", "Opens Maps")), Icon("chev", "chev")),
                Btn("button.item", () => App.OpenUrl("https://www.google.com/maps/search/public+library"), El("span.ic", Icon("bookopen")).Bg("#3E5C76"), El("div.grow", El("div", "Public libraries"), El("div.xs.faint", "Borrow for free")), Icon("chev", "chev")),
                friendsRead.Count > 0 ? Btn("button.item", () => Nav.Push(new UserScreen(friendsRead[0])), El("span.ic", Icon("users")).Bg("#2F6B47"), El("div.grow", El("div", "Borrow it from a friend"), El("div.xs.faint", $"{Pname(friendsRead[0])} has read it")), Icon("chev", "chev")) : null) };

            var scroller = El("div.scroll.under-nav", Css(x => x.paddingTop = 0), hero, head, actions, Sect("Synopsis"), El("p.pad.serif", b.synopsis ?? ""),
                Sect("Ratings"), ratings, social, revSect, chapters, listsSect, facts, EditionSection(b, p, isCur, pages), Shop.Section(b), paperSect, El("div.bottom-space"));
            var nav = El("div.navbar.transparent",
                Btn("button.iconbtn.round", () => { if (Nav.Count > 1) Nav.Back(); else Nav.Reset(new ExploreScreen(), "explore"); }, Aria("Back"), Icon("back")),
                El("div.title", b.title),
                onboarding ? El("div").W(44) : Btn("button.iconbtn.round", StatusSheet, Aria("More options"), Icon("more")));
            if (scroller is ScrollView sv) sv.verticalScroller.valueChanged += v => nav.EnableInClassList("solid", v > 190);
            return El("div", scroller, nav);
        }

        /// <summary>
        /// Pages in your edition (from the earlier Unity app; the web has no editions): every publisher paginates
        /// differently, so chapters and quizzes land where they should. Shown once the book is started.
        /// </summary>
        object EditionSection(Book b, BookProgress p, bool isCur, int pages)
        {
            if (!isCur && (p == null || p.currentPage <= 0)) return null;
            string value = pages.ToString(CultureInfo.InvariantCulture);
            var input = Input("input.input", value, v => value = v, keyboard: TouchScreenKeyboardType.NumberPad).W(120);
            return new object[] { Sect("Your edition"), El("div.pad",
                El("div.row.gap8", input, Btn("button.btn.sm.secondary", () =>
                {
                    int.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n);
                    var err = ReadingService.SetEditionPages(Data.State, b, n);
                    if (err != null) { input.AddToClassList("bad"); Toast(err); return; }
                    Data.Save();
                    Toast("Done. We placed the chapters in your edition.");
                    Nav.Refresh();
                }, "Save")),
                El("p.xs.faint.mt8", "Every publisher paginates differently. This number tells us where each chapter ends in your book.")) };
        }

        void Rate(double v)
        {
            Data.SetRating(id, v);
            Data.Save();
            Nav.Refresh();
            if (v > 0) Toast($"You rated it {Num(v)} {(v == 1 ? "star" : "stars")}");
        }

        internal static string Num(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        void Primary()
        {
            var s = Data.State;
            var p = s.progress.Find(x => x.bookId == id);
            bool finished = p != null && p.finished;
            if (s.profile.currentBookId == id && !finished) LogSheet.Open();
            else if (string.IsNullOrEmpty(s.profile.currentBookId) && !finished) Nav.Push(new StartBookScreen(id));
            else StatusSheet();
        }

        void StatusSheet()
        {
            var s = Data.State;
            var b = Data.Book(id);
            var x = Data.X;
            VisualElement Opt(string label, string sub, Action fn, bool on) =>
                Btn("button.item", () => { Sheet.Close(); fn(); Data.Save(); Nav.Refresh(); },
                    El("div.grow", El("div", label), sub != null ? El("div.xs.faint", sub) : null), on ? Icon("check") : null);
            var p = s.progress.Find(q => q.bookId == id);
            void Clear() { x.want.RemoveAll(q => q == id); x.dnf.RemoveAll(q => q == id); }
            var cur = s.profile.currentBookId;
            Sheet.Open(b.title, El("div.list", Css(st => { st.marginLeft = 0; st.marginRight = 0; st.marginTop = 0; st.marginBottom = 0; }),
                Opt("Want to read", "We save it to your want-to-read list", () => { Clear(); x.want.Add(id); Toast("Added to Want to read"); }, x.want.Contains(id)),
                Opt("Reading now", !string.IsNullOrEmpty(cur) && cur != id ? $"Replaces {Data.Book(cur)?.title} as your current book" : null,
                    () => { if (cur != id) Nav.Push(new StartBookScreen(id)); }, cur == id),
                Opt("Read", "Mark it as finished", () =>
                {
                    Clear();
                    var pp = s.ProgressFor(id);
                    pp.currentPage = s.PagesFor(b);
                    pp.finished = true;
                    Data.SetFinishedOn(id, D.Key(Clock.Today));
                    if (s.profile.currentBookId == id) s.profile.currentBookId = null;
                    Toast("Marked as read");
                }, p != null && p.finished),
                Opt("Dropped it", "Doesn't count in your stats", () => { Clear(); x.dnf.Add(id); if (s.profile.currentBookId == id) s.profile.currentBookId = null; }, x.dnf.Contains(id))));
        }

        void AddToList()
        {
            var x = Data.X;
            Sheet.Open("Add to a list", El("div",
                El("div.list", Css(st => { st.marginLeft = 0; st.marginRight = 0; st.marginTop = 0; st.marginBottom = 0; }), x.lists.Select(l => (object)Btn("button.item", () =>
                    {
                        if (!l.books.Contains(id)) l.books.Add(id);
                        Data.Save();
                        Sheet.Close();
                        Toast($"Added to “{l.title}”");
                    },
                    El("div.grow", El("div", l.title), El("div.xs.faint", F.Books(l.books.Count))), l.books.Contains(id) ? Icon("check") : null))),
                Btn("button.btn.secondary.mt12", () => { Sheet.Close(); ListEditor.Open(id); }, "New list")));
        }

        void Breakdown()
        {
            var st = Stats();
            int tot = Math.Max(1, st.histogram.Sum());
            var rows = new List<object>();
            for (int i = 9; i >= 0; i--)
            {
                var fill = El("i");
                fill.style.width = Length.Percent(st.histogram[i] / (float)tot * 100f);
                rows.Add(El("div.row.gap10.mt6.breakdown",
                    El("span.small.num", ((i + 1) / 2.0).ToString("0.0", CultureInfo.InvariantCulture)).W(26),
                    El("div.bar", fill),
                    El("span.xs.faint.num", $"{F.N(st.histogram[i])} · {Math.Round(st.histogram[i] / (double)tot * 100, MidpointRounding.AwayFromZero)}%").W(64).TextAlign(TextAnchor.MiddleRight)));
            }
            Sheet.Open("Breakdown", El("div",
                El("div.row.gap8", Css(x => x.alignItems = Align.FlexEnd),
                    El("span", st.average.ToString("0.00", CultureInfo.InvariantCulture)).Font(true, 600).FontSize(40),
                    El("span.muted", $"out of 5 · {F.N(st.ratings)} {(st.ratings == 1 ? "rating" : "ratings")}").Mb(8)),
                rows,
                El("p.xs.faint.mt16", "Ratings from Margin readers' reviews. Simple average, not weighted. Each reader counts once.")));
        }
    }

    public class ReviewsScreen : WebScreen
    {
        public string id;
        public string lang = "all";

        public ReviewsScreen(string id) { this.id = id; }

        public override VisualElement Build()
        {
            var all = Data.ReviewsFor(id).OrderByDescending(r => r.date ?? "", StringComparer.Ordinal).ToList();
            var list = all.Where(r => lang == "all" || BookUI.ReviewLang(r) == lang).ToList();
            var pills = new[] { ("all", "All"), ("en", "English"), ("es", "Spanish") };
            return El("div", Navbar(Data.Book(id)?.title ?? ""),
                El("div.row.gap6.pad", Css(x => x.paddingBottom = 8), pills.Select(k => (object)Btn("button.pillbtn" + (lang == k.Item1 ? ".on" : ""), () => { lang = k.Item1; Nav.Refresh(); }, k.Item2))),
                El("div.scroll.under-nav", list.Select(r => BookUI.ReviewCard(r)),
                    El("p.pad.xs.faint", "Tap a review to see where it was first posted.").P(16)).BorderTop(1, "rule"));
        }
    }

    public class ReviewScreen : WebScreen
    {
        public ReaderReview r;

        public ReviewScreen(ReaderReview r) { this.r = r; }

        public override VisualElement Build()
        {
            var b = Data.Book(r.bookId);
            var lang = BookUI.ReviewLang(r);
            return El("div", Navbar("Review"), El("div.scroll.under-nav",
                b != null ? Btn("button.row.gap12.pad", () => Nav.Push(new BookScreen(b.id)), Cover(b, 56), El("div.grow", El("div.h3", b.title), El("div.small.muted", b.author)))
                    .W("100%").TextLeft().Pb(12).BorderBottom(1, "rule") : null,
                BookUI.ReviewCard(r, true),
                r.own ? null : El("div.pad.mt8",
                    Btn("button.btn.secondary", () => App.OpenUrl(r.pageUrl ?? r.url), Icon("link"), "See the original"),
                    lang != "en" ? El("p.xs.faint.mt8", $"Translated from {(BookUI.LANG_NAMES.TryGetValue(lang, out var ln) ? ln : lang.ToUpperInvariant())}. The link above has the reader's words exactly as they wrote them.") : null)));
        }
    }

    /// <summary>Write or edit your review (Social.SaveReview).</summary>
    public static class ReviewSheet
    {
        public static void Open(string bookId, Review existing = null, Action onDone = null)
        {
            var book = Data.Book(bookId);
            if (book == null) return;
            double rating = existing != null ? existing.rating ?? 0 : Data.Rating(bookId);
            string text = existing != null ? existing.body ?? "" : "";
            bool busy = false;
            Label counter = Text("div.help", ""), err = Text("div.help.bad", "");
            var starsBox = El("div");
            VisualElement save = null;

            void Upd()
            {
                int w = BookUI.Words(text);
                counter.text = $"{w} {(w == 1 ? "word" : "words")}";
                save?.SetEnabled(!busy && w >= 3 && rating > 0);
                err.text = rating > 0 ? "" : "Tap the stars to rate it.";
            }
            void DrawStars() => SetKids(starsBox, BookUI.RateInput(rating, v => { rating = v; DrawStars(); Upd(); }));

            var ta = TextArea("textarea.input", text, v => { text = v; Upd(); }, "What did it leave you with? No one else will write this for you.", 4000);
            save = Btn("button.textbtn", () => App.Run(async () =>
            {
                if (busy) return;
                busy = true; Upd();
                try { await Social.SaveReview(bookId, rating, text.Trim()); }
                catch (Exception e) { busy = false; Upd(); NetFail(e); return; }
                Data.SetRating(bookId, rating);
                Data.Save();
                Sheet.Close();
                Toast(existing != null ? "Review updated" : "Review posted");
                if (onDone != null) onDone(); else Nav.Refresh();
            }), existing != null ? "Save" : "Post").Fg("link").Font(false, 600).Hgt(44).P(0, 10);

            Sheet.Open("Your review", El("div",
                El("div.row.gap12", Cover(book, 44), El("div.strong.grow", book.title)),
                El("div.mt12.small.muted", "Your rating"), El("div.mt4", starsBox), err,
                El("div.mt12", ta), counter,
                existing != null ? Btn("button.small.link.mt8", () => App.Run(async () =>
                {
                    try { await Social.DeleteReview(bookId); }
                    catch (Exception e) { NetFail(e); return; }
                    Sheet.Close();
                    Toast("Review deleted");
                    onDone?.Invoke();
                }), "Delete my review").Fg("error") : null,
                El("p.xs.faint.mt12", "No autocomplete and no AI suggestions. It's posted with your name and today's date, and other Margin readers can see it.")),
                new SheetOpts { Right = save });
            DrawStars();
            Upd();
            ta.schedule.Execute(() => ta.Focus()).StartingIn(300);
        }
    }

    /// <summary>New private list.</summary>
    public static class ListEditor
    {
        public static void Open(string firstBook = null)
        {
            string title = "", desc = "";
            var t = Input("input.input", "", v => title = v, "List name", maxLength: 60);
            var d = Input("input.input.mt8", "", v => desc = v, "Description (optional)", maxLength: 140);
            var create = Btn("button.textbtn", () =>
            {
                if (string.IsNullOrWhiteSpace(title)) { t.AddToClassList("bad"); return; }
                var l = new WebList
                {
                    id = "u" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    owner = Accounts.Session(),
                    title = title.Trim(),
                    description = desc.Trim(),
                    books = firstBook != null ? new List<string> { firstBook } : new List<string>(),
                    created = D.Key(Clock.Today),
                };
                Data.X.lists.Add(l);
                Data.Save();
                Sheet.Close();
                Nav.Push(new ListScreen(l.id));
            }, "Create").Fg("link").Font(false, 600).Hgt(44).P(0, 10);
            Sheet.Open("New list", El("div", t, d), new SheetOpts { Right = create });
            t.schedule.Execute(() => t.Focus()).StartingIn(300);
        }
    }

    public class ListScreen : WebScreen
    {
        public string id;

        public ListScreen(string id) { this.id = id; }

        public override VisualElement Build()
        {
            var l = Data.List(id);
            var owner = Data.Me();
            if (l == null) return El("div", Navbar("List"), El("div.empty", El("div.h3", "This list no longer exists")));
            var books = l.books.Select(Data.Book).Where(b => b != null).ToList();
            var acc = Accounts.Current;
            return El("div", Navbar("List"),
                El("div.scroll.under-nav",
                    El("div.pad",
                        El("div.stackcov", books.Take(5).Select(b => Cover(b, 72))),
                        El("div.h1.mt12", l.title),
                        !string.IsNullOrEmpty(l.description) ? El("p.serif.muted.mt6", l.description) : null,
                        El("div.row.gap8.mt12.small", Avatar(owner, 28), El("span", "Your private list")),
                        El("div.row.gap12.mt12.small.muted.num", El("span", F.Books(l.books.Count)), string.IsNullOrEmpty(l.created) ? null : El("span", $"Created {F.Date(l.created)}")),
                        El("div.row.gap8.mt12",
                            Btn("button.btn.sm.secondary", () => IosAlert("Delete the list?", $"“{l.title}” will be deleted for good.",
                                Act("Cancel"),
                                Act("Delete", () => { Data.X.lists.RemoveAll(x => x.id == l.id); Data.Save(); Nav.Back(); Toast("List deleted"); }, red: true)),
                                Icon("trash"), "Delete"),
                            acc != null && acc.IsCreator
                                ? Btn("button.btn.sm.primary", () => Nav.Push(new PostEditor(new PostDraft
                                {
                                    kind = "list", title = l.title, body = l.description ?? "",
                                    items = l.books.Select(b => new PostItem { bookId = b, note = "" }).ToList(),
                                })), "Publish as a post")
                                : null)),
                    El("div.grid3.pad.mt20", books.Select((b, i) => (object)Btn("button.col", () => Nav.Push(new BookScreen(b.id)),
                        Cover(b, 110, null, i + 1).W("100%"), El("div.xs.strong.mt4.clamp2", b.title), El("div.xs.faint", b.author)).TextLeft())),
                    l.books.Count == 0 ? El("div.empty", El("div.h3", "Empty list"), El("p.small", "Add books from their page, with the list button.")) : null,
                    El("div.bottom-space")));
        }
    }

    // ---------------- screens-flow.js ----------------

    /// <summary>The "Log reading" sheet (the + tab).</summary>
    public static class LogSheet
    {
        public static void Open(string bookId = null)
        {
            var s = Data.State;
            if (bookId != null) ReadingService.SetCurrentBook(s, bookId);
            var book = Data.Book(s.profile.currentBookId);
            if (book == null) { Toast("You're not reading a book yet. Pick one to start."); return; }
            var p = s.ProgressFor(book.id);
            int from = p.currentPage;
            int pages = s.PagesFor(book); // the reader's edition (the same total ReadingService checks)
            int page = Math.Min(pages, from + s.profile.goals.dailyPages);
            string when = "today";

            TextField input = null;
            Label delta = Text("div.serif.center.mt12", " "), info = Text("div.small.muted.center.mt4", " "), err = Text("div.help.bad.center", "");
            var save = Btn("button.textbtn", () => Submit(page, when), "Log it").Fg("ink").Font(false, 600).Hgt(44).P(0, 10);

            void Render()
            {
                var e = ReadingService.Validate(s, Data.Catalog, page);
                bool ok = e == CheckInError.None;
                input.EnableInClassList("bad", !ok);
                err.text = ReadingService.ErrorMessage(e) ?? "";
                delta.text = ok ? $"+{F.Pages(page - from)}" : " ";
                var closed = book.chapters.Where(c => { int end = Edition.ChapterEnd(book, c, pages); return end > from && end <= page; }).ToList();
                string txt = " ";
                if (ok)
                {
                    if (page >= pages) txt = "You finish the book";
                    else if (closed.Any(c => c.HasQuiz)) txt = "You finish a chapter. Its quiz is ready.";
                    else txt = $"{F.N(pages - page)} pages left";
                }
                info.text = txt;
                save.SetEnabled(ok);
                save.style.opacity = ok ? 1 : 0.35f;
            }
            void Set(int v)
            {
                page = Math.Max(0, Math.Min(pages, v));
                input.SetValueWithoutNotify(page.ToString(CultureInfo.InvariantCulture));
                Render();
            }

            input = Input("input.bignum", page.ToString(CultureInfo.InvariantCulture), v =>
            {
                page = int.TryParse((v ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
                Render();
            }, keyboard: TouchScreenKeyboardType.NumberPad);
            input.tooltip = "Page you got to";

            var next = Edition.ChapterAt(book, Math.Max(1, from + 1), pages);
            var whenSeg = El("div.seg.mt20");
            foreach (var k in new[] { "today", "yesterday" })
            {
                var key = k;
                whenSeg.Add(Btn("button" + (when == key ? ".on" : ""), () =>
                {
                    when = key;
                    foreach (var c in whenSeg.Children()) c.EnableInClassList("on", (string)c.userData == key);
                }, key == "today" ? "Today" : "Yesterday"));
                whenSeg[whenSeg.childCount - 1].userData = key;
            }

            Sheet.Open("Log reading", El("div",
                El("div.row.gap12", Cover(book, 44), El("div.grow", El("div.strong", book.title), El("div.xs.muted.num", $"You were on page {from} of {pages}.")),
                    Btn("button.btn.sm.secondary", PickBook, "Change")),
                El("div.center.small.muted.mt20", "What page did you get to?"),
                El("div.stepper.mt8", Btn("button.circle", () => Set(page - 1), Aria("One page less"), "−"), input, Btn("button.circle", () => Set(page + 1), Aria("One page more"), "+")),
                delta, info, err,
                El("div.row.wrap.gap6.mt12", Css(x => x.justifyContent = Justify.Center),
                    Btn("button.pillbtn", () => Set(from + 10), "+10"), Btn("button.pillbtn", () => Set(from + 20), "+20"),
                    next != null ? Btn("button.pillbtn", () => Set(Edition.ChapterEnd(book, next, pages)), "Finished the chapter") : null,
                    Btn("button.pillbtn", () => Set(pages), "Finished the book")),
                whenSeg,
                El("p.xs.faint.center.mt12", "Log the page number you see in the book. The app works out the rest.")),
                new SheetOpts { Right = save });
            Render();
        }

        public static void PickBook()
        {
            var s = Data.State;
            var x = Data.X;
            var ids = new List<string> { s.profile.currentBookId };
            ids.AddRange(x.want);
            ids.AddRange(s.progress.Where(p => !p.finished && p.currentPage > 0).Select(p => p.bookId));
            ids = ids.Where(i => !string.IsNullOrEmpty(i) && Data.Book(i) != null).Distinct().ToList();
            Sheet.Open("What are you reading?", El("div",
                El("div.list", Css(st => { st.marginLeft = 0; st.marginRight = 0; st.marginTop = 0; st.marginBottom = 0; }), ids.Select(id =>
                {
                    var b = Data.Book(id);
                    return (object)Btn("button.item", () => { Sheet.Close(); Open(id); },
                        Cover(b, 44), El("div.grow", El("div.strong", b.title), El("div.xs.faint", x.want.Contains(id) ? "In Want to read" : $"p. {s.ProgressFor(id).currentPage}")),
                        id == s.profile.currentBookId ? Icon("check") : null);
                })),
                Btn("button.btn.secondary.mt12", () => { Sheet.Close(); Nav.Reset(new ExploreScreen(), "explore"); }, "Find another book")));
        }

        /// <summary>
        /// Saves the check-in. Activity rows (pages, finished, quiz) for friends are written by Data.Save
        /// (AccountService diffs the diary, like the web's Net.save).
        /// </summary>
        static void Submit(int page, string when)
        {
            var s = Data.State;
            var before = HabitEngine.Compute(s, Clock.Today);
            var book = Data.Book(s.profile.currentBookId);
            var day = when == "yesterday" ? Clock.Today.AddDays(-1) : Clock.Today;
            var achBefore = Data.Achievements(s);
            var r = ReadingService.CheckIn(s, Data.Catalog, page, day);
            if (!r.Ok) return;
            if (Clock.Now.Hour >= 23) Data.X.nightLogs++;
            Data.X.want.RemoveAll(i => i == book.id);
            if (r.FinishedBook) Data.SetFinishedOn(book.id, D.Key(day));
            Data.Save();
            // If they already read today, today's reminder is canceled (as the earlier Unity check-in did).
            MarginApp.Instance.RescheduleReminders();
            Sheet.Close();
            var outcome = new Outcome { R = r, Book = book, Before = before, AchBefore = achBefore };
            if (r.PendingQuiz != null) QuizFlow.Start(book, r.PendingQuiz, outcome);
            else if (r.FinishedBook) Nav.Push(new FinishedScreen(outcome));
            else Nav.Push(new ClosingScreen(outcome));
        }
    }

    public static class QuizFlow
    {
        public static void Start(Book book, Chapter chapter, Outcome outcome) => Nav.Push(new QuizScreen(book, chapter, outcome));

        /// <summary>QuizScreen.finish(): back to the book page, or on to the finished or closing screen.</summary>
        public static void Finish(Outcome o)
        {
            if (o.Standalone) Nav.Back();
            else if (o.R != null && o.R.FinishedBook) Nav.Replace(new FinishedScreen(o));
            else Nav.Replace(new ClosingScreen(o));
        }
    }

    public class QuizScreen : WebScreen
    {
        public Book b;
        public Chapter c;
        public Outcome o;

        string phase;
        int i;
        int sel = -1;
        bool check;
        bool recorded;
        readonly List<int> answers = new List<int>();

        public QuizScreen(Book book, Chapter chapter, Outcome outcome) { b = book; c = chapter; o = outcome; phase = outcome.Standalone ? "q" : "offer"; }

        void Finish() => QuizFlow.Finish(o);

        static VisualElement Bottom(params object[] kids) => El("div.pad", kids).P(12, 16, 40);

        public override VisualElement Build()
        {
            var qs = c.quiz.questions;
            if (phase == "offer")
                return El("div", Navbar("", new NavOpts { OnBack = Finish }), El("div.scroll.under-nav", El("div.pad",
                        Cover(b, 72), El("div.h1.mt16", $"Nice, you finished “{c.title}”"), El("p.muted.mt8", $"{b.title}, pages {c.startPage} to {c.endPage}."),
                        El("p.mt16", $"Want to see what stayed with you? {qs.Count} quick questions, no grades. Recalling what you read helps you remember it. You can skip them, they don't change your momentum."),
                        El("p.xs.faint.mt12", "Questions written with AI and checked by people. The AI asks; you think."))),
                    Bottom(Btn("button.btn.accent", () => { phase = "q"; Nav.Refresh(); }, "Try the questions"), Btn("button.btn.ghost.mt4", Finish, "Skip for now")));

            if (phase == "done")
            {
                var sc = QuizEngine.Score(c.quiz, answers);
                if (!recorded) { QuizEngine.Record(Data.State, b.id, c.number, sc, Clock.Today); Data.Save(); recorded = true; }
                return El("div", Navbar("", new NavOpts { OnBack = Finish }), El("div.scroll.under-nav", El("div.pad",
                        El("div", $"{sc.Correct} of {sc.Total}").Font(true, 600).FontSize(64),
                        El("p.serif-l.mt8", QuizEngine.Verdict(sc)),
                        El("div.mt20", qs.Select((q, k) =>
                        {
                            bool right = k < answers.Count && answers[k] == q.correctIndex;
                            return (object)El("div.row.top.gap10", El("span.badge" + (right ? ".ok" : ".todo"), right ? "Right" : "Review"),
                                El("div.grow.small", El("div", q.prompt), right ? null : El("div.muted.mt4", "Answer: " + q.options[q.correctIndex])))
                                .P(10, 0).BorderTop(1, "rule");
                        })))),
                    Bottom(Btn("button.btn.primary", () => Nav.Replace(new ReflectionScreen(b, c, o)), "Write a reflection"), Btn("button.btn.ghost.mt4", Finish, "Finish")));
            }

            var cur = qs[i];
            bool ok = check && sel == cur.correctIndex;
            var bar = El("i");
            bar.style.width = Length.Percent((i + (check ? 1 : 0)) / (float)qs.Count * 100f);
            var top = El("div.row.gap10.pad",
                Btn("button.iconbtn", () => IosAlert("Leave the quiz?", "Nothing from this quiz will be saved.", Act("Keep going"), Act("Leave", Finish, red: true)), Aria("Leave the quiz"), Icon("x")),
                El("div.qprogress", bar)).Pt(Safe.Top + 6).Pb(8);
            var opts = cur.options.Select((text, k) =>
            {
                var cls = "option";
                if (!check && sel == k) cls += ".sel";
                if (check) cls += k == cur.correctIndex ? ".ok" : k == sel ? ".ko" : ".dim";
                return (object)Btn("button." + cls, () => { if (!check) { sel = k; Nav.Refresh(); } }, text);
            });
            var bottom = check
                ? El("div.verdict" + (ok ? ".ok" : ".ko"),
                    El("div.strong", ok ? "Exactly." : "Not quite.").Fg(ok ? "ok" : "error").FontSize(18),
                    El("p.small.mt4", cur.explanation ?? ""),
                    Btn("button.btn.mt12.primary", () =>
                    {
                        answers.Add(sel); sel = -1; check = false;
                        if (i < qs.Count - 1) i++; else phase = "done";
                        Nav.Refresh();
                    }, i < qs.Count - 1 ? "Next question" : "See result").Mb(34))
                : Bottom(Btn("button.btn.primary", () => { check = true; Nav.Refresh(); }, Disabled(sel < 0), "Check")).BorderTop(1, "rule");
            return El("div", top, El("div.scroll.under-nav", El("div.pad",
                El("div.xs.faint", $"{c.title} · question {i + 1} of {qs.Count}"),
                El("div.serif-l.mt8", cur.prompt).Font(true, 600),
                El("div.mt20", opts))), bottom);
        }
    }

    public class ReflectionScreen : WebScreen
    {
        public Book b;
        public Chapter c;
        public Outcome o;

        string text = "";
        int k;

        public ReflectionScreen(Book book, Chapter chapter, Outcome outcome)
        {
            b = book; c = chapter; o = outcome;
            k = chapter.number % Copy.ReflectionPrompts.Length;
        }

        public override VisualElement Build()
        {
            var count = Text("div.help", "");
            var save = Btn("button.btn.accent", Save, "Save reflection");
            void Upd()
            {
                int w = BookUI.Words(text);
                count.text = $"{w} {(w == 1 ? "word" : "words")}";
                save.SetEnabled(w >= 5);
            }
            var ta = TextArea("textarea.input", text, v => { text = v; Upd(); }, "Write here…").MinH(220);
            Upd();
            return El("div", Navbar(c.title, new NavOpts { OnBack = () => QuizFlow.Finish(o) }), El("div.scroll.under-nav", El("div.pad",
                    El("span.human", "Written by a human"), El("div.h1.mt12", Copy.ReflectionPrompts[k]),
                    Btn("button.small.link.mt6", () => { k = (k + 1) % Copy.ReflectionPrompts.Length; Nav.Refresh(); }, "Another question"),
                    El("div.mt16", ta), count, El("p.xs.faint.mt8", "No autocomplete and no AI suggestions here. Only you see it, unless you post it as a review."))),
                El("div.pad", save, Btn("button.btn.ghost.mt4", () => QuizFlow.Finish(o), "Skip")).P(12, 16, 40).BorderTop(1, "rule"));
        }

        void Save()
        {
            Data.State.reflections.Add(new Reflection { bookId = b.id, chapterNumber = c.number, text = text.Trim(), date = Clock.Today, humanWritten = true });
            Data.Save();
            Toast("Saved. It's yours.");
            QuizFlow.Finish(o);
        }
    }

    /// <summary>
    /// Finishing a book: the achievements it unlocked, a rating, and ways to tell people (a public review, a share).
    /// Friends already see "finished" in their feed unless activity is set to "Only me".
    /// </summary>
    public class FinishedScreen : WebScreen
    {
        public Outcome o;

        public FinishedScreen(Outcome o) { this.o = o; }

        static VisualElement Unlock(string mark, string title, string name, string detail) =>
            El("div.unlock", El("span.medal", mark), El("div.grow", El("div.caps", title), El("div.strong.mt2", name), El("div.xs.muted.mt2", detail)));

        public override VisualElement Build()
        {
            var b = o.Book;
            var s = Data.State;
            int y = Clock.Today.Year, n = Data.BooksInYear(s, y);
            var had = new HashSet<string>((o.AchBefore ?? new List<Achievement>()).Where(a => a.Got).Select(a => a.Id));
            var fresh = Data.Achievements(s).Where(a => a.Got && !had.Contains(a.Id)).Select(a => Unlock(MainUI.ACH_MARK.TryGetValue(a.Id, out var m) ? m : a.Name.Substring(0, 1), "Achievement unlocked", a.Name, a.Detail));
            bool seen = Data.Settings?.ActivityVisibility != "Only me";
            return El("div", El("div.scroll", El("div.pad.col", Css(x => x.alignItems = Align.Center), Cover(b, 128),
                    El("div.h1.mt20", $"You finished {b.title}.").TextCenter(),
                    El("p.muted.mt6.num", $"It's book {n} of {y} for you. Your goal is {Data.X.yearGoal}.").TextCenter(),
                    Unlock(n.ToString(), "Book finished", $"Book {n} of {y}", $"{F.N(s.PagesFor(b))} pages, read by you, on paper."),
                    fresh,
                    El("div.mt24.small.muted", "How would you rate it?"),
                    El("div.mt8", BookUI.RateInput(Data.Rating(b.id), v => { Data.SetRating(b.id, v); Data.Save(); Nav.Refresh(); })),
                    Btn("button.btn.accent.mt24", () => ReviewSheet.Open(b.id), "Post a review"),
                    // The web opens the share sheet; iOS sharing needs a native plugin, so we copy a share line instead.
                    Btn("button.btn.secondary.mt8", () => SocialUI.CopyText($"I just finished {b.title} by {b.author}. Book {n} of my {y} on Margin. https://marginreads.vercel.app", "Copied. Paste it anywhere."), Icon("share"), "Share it"),
                    El("p.xs.faint.mt12", seen ? "Your friends will see that you finished it." : "Your activity is set to Only me, so friends won't see it."))
                    .Pt(30).TextCenter()),
                El("div.pad", Btn("button.btn.secondary", () =>
                {
                    s.profile.currentBookId = null;
                    Data.Save();
                    Nav.Reset(new HomeScreen(), "home");
                }, "Done")).P(12, 16, 40));
        }
    }

    /// <summary>
    /// Starting a book is a real decision, so it gets its own moment: the book, how long it will take at the
    /// reader's pace, and one clear button. Every way of starting a book comes through here.
    /// </summary>
    public class StartBookScreen : WebScreen
    {
        readonly string id;

        public StartBookScreen(string id) { this.id = id; }

        public override VisualElement Build()
        {
            var b = Data.Book(id);
            if (b == null) return El("div", Navbar(""));
            var s = Data.State;
            var g = s.profile.goals;
            int pages = s.PagesFor(b);
            int perWeek = Math.Max(1, g.dailyPages * Math.Max(1, g.readingDays.Count));
            int weeks = Math.Max(1, (pages + perWeek - 1) / perWeek);
            var curId = s.profile.currentBookId;
            var cur = !string.IsNullOrEmpty(curId) && curId != id ? Data.Book(curId) : null;
            VisualElement Line(string k, string v, string cls = "div.row.mt8") => El(cls, El("span.grow.small.muted", k), El("span.strong.num", v));
            return El("div", Navbar(""), El("div.scroll.under-nav", El("div.startbook",
                    El("div.caps", "A new book"), El("div.mt16", Cover(b, 128)),
                    El("div.h1.mt20", b.title).TextCenter(), El("div.muted.mt4", b.author).TextCenter(),
                    El("div.card.block.plan",
                        Line("Length", $"{F.N(pages)} pages", "div.row"),
                        Line("At your pace", $"about {weeks} {(weeks == 1 ? "week" : "weeks")}"),
                        Line("Today", $"{g.dailyPages} pages{(string.IsNullOrEmpty(g.intention) ? "" : ", " + g.intention)}")),
                    El("p.serif-l.mt24", "Every book starts on page one.").TextCenter(),
                    cur != null ? El("p.xs.faint.mt8", $"It replaces {cur.title} as your current book. Your progress there is kept.").TextCenter() : null)),
                El("div.pad", Btn("button.btn.accent", Begin, "Start reading")).P(12, 16, 40));
        }

        /// <summary>Confirming is the moment: confetti, then Home with the book set, where they log their reading.</summary>
        void Begin()
        {
            var b = Data.Book(id);
            if (b == null) return;
            Data.X.want.RemoveAll(q => q == id);
            Data.X.dnf.RemoveAll(q => q == id);
            ReadingService.SetCurrentBook(Data.State, id);
            Data.Save();
            Celebrate();
            App.GoHome();
            Toast($"Now reading {b.title}. Enjoy it.");
        }
    }

    public class ClosingScreen : WebScreen
    {
        public Outcome o;

        public ClosingScreen(Outcome o) { this.o = o; }

        public override VisualElement Build()
        {
            var s = Data.State;
            var after = HabitEngine.Compute(s, Clock.Today);
            var w = HabitEngine.Weekly(s, Clock.Today);
            var b = o.Book;
            var p = s.ProgressFor(b.id);
            int pages = s.PagesFor(b);
            bool changed = after.Level != o.Before.Level;
            int added = o.R?.PagesAdded ?? 0;
            return El("div", El("div.scroll", El("div.pad",
                    El("div", $"+{added}").Font(true, 600).FontSize(56),
                    El("div.serif-l.mt4", added == 1 ? "page" : "pages"),
                    El("div.row.gap12.mt24", Cover(b, 56), El("div.grow", El("div.strong", b.title), El("div.mt6", Bar(p.currentPage / (double)Math.Max(1, pages))), El("div.xs.faint.mt4.num", $"Page {p.currentPage} of {pages}"))),
                    El("div.card.block.mt20", El("div.row", El("span.caps.grow", "Reading momentum"), El("span.strong", Copy.LevelName(after.Level))),
                        changed ? El("p.small.mt6", $"You moved up from {Copy.LevelName(o.Before.Level)} to {Copy.LevelName(after.Level)}.") : null,
                        El("div.row.small.mt8.num", El("span.grow", $"{w.PagesRead} of {w.Target} pages this week")),
                        El("div.mt6", Bar(w.Target > 0 ? w.PagesRead / (double)w.Target : 0, "ink"))),
                    // Under the summary so it is always visible
                    Btn("button.btn.primary.mt20", () => Nav.Reset(new HomeScreen(), "home"), "Done")
)).Pt(40));
        }
    }

    // ---------------- Focus mode ----------------

    public class FocusScreen : WebScreen
    {
        int min;
        DateTime? end;
        double total;
        IVisualElementScheduledItem tick;

        public FocusScreen()
        {
            int m = Accounts.Current?.Settings.FocusMinutes ?? 25;
            min = m > 0 ? m : 25;
        }

        static ScreenTimeService ST => MarginApp.Instance.ScreenTime;

        /// <summary>
        /// The account's settings (synced like the web) are the source of truth; the phone's Screen Time
        /// service follows them so the shield silences the same apps.
        /// </summary>
        internal static void SyncScreenTime()
        {
            var st = Accounts.Current?.Settings;
            if (st == null || ST == null) return;
            if (st.ScreenTime && !ST.IsAuthorized) ST.Authorize();
            var silenced = st.Silenced;
            foreach (var a in ScreenTimeService.Apps)
                if (ST.IsSelected(a.Id) != silenced.Contains(a.Id)) ST.Toggle(a.Id);
        }

        public override void OnHide()
        {
            tick?.Pause();
            if (end != null)
            {
                ST?.SetShield(false);
                Notifications?.CancelFocusEnd(); // the session was left without finishing
            }
            App.ApplySettings(); // drops the dark theme (or keeps it if the reader chose Dark)
        }

        public override VisualElement Build()
        {
            tick?.Pause();
            App.Root?.AddToClassList("theme-dark");
            var a = Accounts.Current;
            var settings = a?.Settings;
            var apps = settings != null ? settings.Silenced : new List<string>();
            bool screenTime = settings != null && settings.ScreenTime;
            var b = Data.Book(Data.State.profile.currentBookId);

            if (end == null)
                return El("div.focus", Navbar("Focus mode"), El("div.scroll.under-nav", El("div.pad",
                        El("div.h1.mt12", "How long will you read?"),
                        El("div.row.wrap.gap8.mt16", new[] { 15, 25, 45, 60 }.Select(m => (object)Btn("button.chip" + (min == m ? ".on" : ""), () => { min = m; Nav.Refresh(); }, $"{m} min"))),
                        El("div.list.mt24", Btn("button.item", () => Nav.Push(new AppPickerScreen()),
                                El("div.grow", El("div", "Silenced apps"), El("div.xs.faint", screenTime ? $"{apps.Count} {(apps.Count == 1 ? "app" : "apps")}" : "No Screen Time permission")), Icon("chev", "chev")),
                            b != null ? El("div.item", Cover(b, 44), El("div.grow", El("div.xs.faint", "Reading"), El("div", b.title))) : null).M(24, 0, 0),
                        El("p.small.faint.mt16", "Put your phone face down. It will vibrate when time's up."))),
                    El("div.pad", Btn("button.btn.accent", Start, "Start")).P(12, 16, 40)).Bg("paper");

            // The ring: the web's SVG circles, drawn with Painter2D (track in --rule, the time left in --accent).
            float frac = 1;
            var rule = Theme.Color("rule");
            var accent = Theme.Color("accent");
            var ring = El("div.fring");
            ring.generateVisualContent += ctx =>
            {
                var r = ring.contentRect;
                float k = Mathf.Min(r.width, r.height) / 260f;
                var center = new Vector2(r.width / 2f, r.height / 2f);
                var p2 = ctx.painter2D;
                p2.lineWidth = 6 * k;
                p2.strokeColor = rule;
                p2.BeginPath();
                p2.Arc(center, 120 * k, Angle.Degrees(0), Angle.Degrees(360));
                p2.Stroke();
                if (frac > 0.0005f)
                {
                    p2.strokeColor = accent;
                    p2.lineCap = LineCap.Round;
                    p2.BeginPath();
                    p2.Arc(center, 120 * k, Angle.Degrees(-90), Angle.Degrees(-90 + 360 * frac));
                    p2.Stroke();
                }
            };
            var time = Text("span", "");
            ring.Add(El("div.timer", time).Inset(0).PlaceCenter().FontSize(64));
            time.style.fontSize = 64;

            void Tick(bool canFinish)
            {
                if (end == null) return;
                double left = Math.Max(0, (end.Value - DateTime.UtcNow).TotalMilliseconds);
                time.text = $"{(int)Math.Floor(left / 60000)}:{((int)Math.Floor(left / 1000) % 60).ToString("00", CultureInfo.InvariantCulture)}";
                frac = total > 0 ? (float)(left / total) : 0;
                ring.MarkDirtyRepaint();
                if (left <= 0 && canFinish) Done(false);
            }
            Tick(false); // the first paint never navigates from inside Build
            tick = ring.schedule.Execute(() => Tick(true)).Every(250);

            return El("div.focus", El("div.scroll", El("div.pad.col", Css(x => x.alignItems = Align.Center), ring,
                        b != null ? El("div.mt24.serif-l", b.title).TextCenter() : null,
                        El("div.small.faint.mt4", screenTime ? $"{apps.Count} {(apps.Count == 1 ? "app" : "apps")} silenced" : "No app blocking").TextCenter())
                    .Pt(Safe.Top + 20).TextCenter()),
                El("div.pad", Btn("button.btn.secondary", () => Done(true), "End early")).P(12, 16, 40)).Bg("paper");
        }

        void Start()
        {
            total = min * 60000.0;
            end = DateTime.UtcNow.AddMilliseconds(total);
            SyncScreenTime();
            ST?.SetShield(true);
            ScheduleEndAlert();
            Nav.Refresh();
        }

        static NotificationService Notifications => MarginApp.Instance != null ? MarginApp.Instance.Notifications : null;

        /// <summary>"25 minutes of Hamlet. Log your page." (the end alert and the in-app banner).</summary>
        static string EndBody(int minutes)
        {
            var b = Data.Book(Data.State.profile.currentBookId);
            return $"{minutes} {(minutes == 1 ? "minute" : "minutes")} of {(b != null ? b.title : "reading")}. Log your page.";
        }

        /// <summary>
        /// The phone is face down: a local notification tells the reader when time's up even if Margin is
        /// closed. On an iPhone it asks for permission first (no prompt if it was already answered).
        /// </summary>
        void ScheduleEndAlert()
        {
            var n = Notifications;
            if (n == null || end == null) return;
            var at = end.Value;
            var body = EndBody(min);
            if (!n.IsSupported) { n.ScheduleFocusEnd(at, "Time's up", body); return; }
            MarginApp.Instance.StartCoroutine(n.RequestPermission(granted =>
            {
                // Still the same session (not ended or restarted while the prompt was open).
                if (granted && end == at) n.ScheduleFocusEnd(at, "Time's up", body);
            }));
        }

        void Done(bool early)
        {
            if (end == null) return;
            tick?.Pause();
            double left = Math.Max(0, (end.Value - DateTime.UtcNow).TotalMilliseconds);
            int mins = (int)Math.Round((total - left) / 60000, MidpointRounding.AwayFromZero);
            Data.X.focusSessions.Add(new FocusSession { date = D.Key(Clock.Today), minutes = mins });
            Data.Save();
            end = null;
            ST?.SetShield(false);
            Notifications?.CancelFocusEnd(); // early: it must not ring later; on time: the app shows its own banner
            App.ApplySettings();
            Nav.Back();
            if (Nav.Current is FocusScreen) Nav.Reset(new HomeScreen(), "home");
            if (early) Toast($"{mins}-min session saved");
            else
            {
                // Time's up with the app open: the phone vibrates and the in-app banner says so (web: banner).
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
                Handheld.Vibrate();
#endif
                Banner("Time's up", EndBody((int)Math.Round(total / 60000, MidpointRounding.AwayFromZero)));
            }
            App.Root?.schedule.Execute(() => LogSheet.Open()).StartingIn(400);
        }
    }

    public class AppPickerScreen : WebScreen
    {
        static readonly List<string> Defaults = new List<string> { "instagram", "tiktok", "youtube" };

        public override VisualElement Build()
        {
            var a = Accounts.Current;
            if (a == null) return El("div", Navbar("Silenced apps"));
            var st = a.Settings;
            var known = st.Silenced.Where(id => ScreenTimeService.Apps.Any(c => c.Id == id)).ToList();
            if (known.Count == 0 && !st.SilencedSet) known = Defaults.ToList();
            st.Silenced = known; // only marks the settings changed when it really differs

            if (!st.ScreenTime)
                return El("div", Navbar("Silenced apps"), El("div.scroll.under-nav", El("div.pad",
                    El("div.h1", "Screen Time"),
                    El("p.muted.mt8", "To silence apps while you read, we need Screen Time permission. You pick the apps; Apple never tells us what you do in them."),
                    Btn("button.btn.primary.mt24", () => IosAlert("“Margin” Would Like to Access Screen Time",
                        "Margin will be able to see your activity, restrict content, and limit the use of apps and websites. You can turn this off in Settings.",
                        Act("Don't Allow"),
                        Act("Continue", () =>
                        {
                            st.ScreenTime = true;
                            MarginApp.Instance.ScreenTime?.Authorize();
                            FocusScreen.SyncScreenTime();
                            Data.Save();
                            Nav.Refresh();
                        }, bold: true)), "Allow"),
                    El("p.xs.faint.mt12", "Simulation: the prototype doesn't really block apps."))));

            return El("div", Navbar("Silenced apps"), El("div.scroll.under-nav",
                El("p.pad.small.muted", "During Focus mode, these apps go quiet: no notifications, and they won't open."),
                El("div.list.mt12", ScreenTimeService.Apps.Select(app => (object)El("div.item",
                    Img(Resources.Load<Texture2D>(app.IconPath)).Size(36, 36).Radius(8).Mr(12).FlexNone(),
                    El("div.grow", El("div", app.Name), El("div.xs.faint", app.Example)),
                    SwitchEl(known.Contains(app.Id), v =>
                    {
                        var list = st.Silenced;
                        if (v) { if (!list.Contains(app.Id)) list.Add(app.Id); }
                        else list.Remove(app.Id);
                        st.Silenced = list;
                        st.SilencedSet = true;
                        known = list;
                        FocusScreen.SyncScreenTime();
                        Data.Save();
                    })))),
                El("p.groupfoot", "Demo version: the final version uses iOS Screen Time (FamilyControls).")));
        }
    }
}
