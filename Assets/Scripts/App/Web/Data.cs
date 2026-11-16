using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Margin.App.Services;
using Margin.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace Margin.App.Web
{
    /// <summary>A reader review from Goodreads (Resources/Data/reviews.json), quoted with author and link.</summary>
    [Serializable]
    public class GoodreadsReview
    {
        public string bookId;
        public string text;
        public string author;
        public string source;
        public string url;
        public string pageUrl;
        public string lang;
        public string originalLang;
        public double rating;
        public int ratingScale;
        public string date;
        public string kind;
        /// <summary>True for "your review" cards built from the reader's own data (the web's r.own).</summary>
        [JsonIgnore] public bool own;
    }

    [Serializable]
    class ReviewFile { public List<GoodreadsReview> reviews = new List<GoodreadsReview>(); }

    /// <summary>data.js Data.me(): the signed-in reader as profile screens show it.</summary>
    public class MeInfo
    {
        /// <summary>The username (the web uses it as the id).</summary>
        public string Id;
        /// <summary>The auth user id.</summary>
        public string Uid;
        public string Name;
        public string Bio;
        public string Color;
        /// <summary>"yyyy-MM-dd".</summary>
        public string Joined;
        public bool IsMe = true;
        public List<string> Read = new List<string>();
        public List<string> Favorites = new List<string>();
    }

    /// <summary>A finished book with its date (Data.finished()).</summary>
    public class FinishedBook
    {
        public string BookId;
        /// <summary>"yyyy-MM-dd" (or the last log date for books finished before the date was kept).</summary>
        public string FinishedOn;
        /// <summary>0..5 in half stars (0 = not rated).</summary>
        public double Rating;
        public BookProgress Progress;
    }

    public class Achievement
    {
        public string Id;
        public string Name;
        public string Detail;
        public bool Got;
    }

    public class SearchResult
    {
        public List<Book> Books = new List<Book>();
        public List<WebList> Lists = new List<WebList>();
    }

    /// <summary>
    /// The web's Data object (preview/js/data.js) over the Unity services: the catalog, quotes and
    /// Goodreads reviews, the signed-in diary (<see cref="State"/> is the Core AppState; the web-only
    /// fields are in <see cref="X"/>), and everything derived from it.
    /// <para>Change the state or X in place, then call <see cref="Save"/> (like the web's Data.save()).</para>
    /// </summary>
    public static class Data
    {
        public const string APP_VERSION = "5.3.1";

        public static readonly Achievement[] ACHIEVEMENTS =
        {
            new Achievement { Id = "first-book", Name = "First book", Detail = "You finished your first book on Margin." },
            new Achievement { Id = "ten-chapters", Name = "Ten chapters", Detail = "You answered the quiz for 10 chapters." },
            new Achievement { Id = "month", Name = "One month", Detail = "You met your weekly goal 4 weeks in a row." },
            new Achievement { Id = "night-owl", Name = "Night reading", Detail = "You logged reading after 23:00 ten times." },
            new Achievement { Id = "focus-10", Name = "Ten focus sessions", Detail = "You completed 10 Focus mode sessions." },
            new Achievement { Id = "reflections-5", Name = "Five reflections", Detail = "You wrote five reflections in your own words." },
            new Achievement { Id = "theater", Name = "Curtain up", Detail = "You read a whole play." },
            new Achievement { Id = "mexico", Name = "Made in Mexico", Detail = "You read three books by Mexican authors." },
            new Achievement { Id = "paper", Name = "On paper", Detail = "You marked 5 books as read in print." },
            new Achievement { Id = "year-12", Name = "Twelve in a year", Detail = "You finished 12 books in one calendar year." },
        };

        static MarginApp Host => MarginApp.Instance;
        static List<GoodreadsReview> _reviews;
        static readonly Dictionary<string, Texture2D> _covers = new Dictionary<string, Texture2D>();
        static readonly HashSet<string> _noCover = new HashSet<string>();

        // ---------- Catalog ----------

        public static Catalog Catalog => Host.Catalog;
        public static Book Book(string id) => string.IsNullOrEmpty(id) ? null : Catalog.FindBook(id);
        public static List<Book> Books => Catalog.books;
        public static List<Shelf> Shelves => Catalog.shelves;

        /// <summary>Resources path of the real cover (Covers/{id}), or null when the book has none (type cover).</summary>
        public static string CoverUrl(string id) => CoverTexture(id) != null ? "Covers/" + id : null;

        public static Texture2D CoverTexture(string id)
        {
            if (string.IsNullOrEmpty(id) || _noCover.Contains(id)) return null;
            if (_covers.TryGetValue(id, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("Covers/" + id);
            if (t == null) _noCover.Add(id); else _covers[id] = t;
            return t;
        }

        /// <summary>Real reviews from Goodreads readers for a book.</summary>
        public static List<GoodreadsReview> ReviewsFor(string id)
        {
            if (_reviews == null)
            {
                var asset = Resources.Load<TextAsset>("Data/reviews");
                try { _reviews = asset != null ? JsonConvert.DeserializeObject<ReviewFile>(asset.text)?.reviews : null; }
                catch (Exception e) { Debug.LogWarning("[Margin] reviews.json: " + e.Message); }
                if (_reviews == null) _reviews = new List<GoodreadsReview>();
            }
            return _reviews.Where(r => r.bookId == id).ToList();
        }

        public static Quote QuoteOfDay(DateTime d) => Host.Content.QuoteOfTheDay(d);

        // ---------- Session ----------

        /// <summary>The signed-in diary (Data.state()).</summary>
        public static AppState State => Host.State;

        /// <summary>The web-only fields of the diary: lists, want, dnf, focusSessions, favorites, yearGoal, notifications...</summary>
        public static WebExtras X => Host.Account.Extras;

        /// <summary>The account's settings (Accounts.current().settings).</summary>
        public static WebSettings Settings => Host.Account.Settings;

        public static WebList List(string id) => X.lists.FirstOrDefault(l => l.id == id);

        /// <summary>Data.me(), or null when nobody is signed in.</summary>
        public static MeInfo Me()
        {
            var a = Accounts.Current;
            if (a == null) return null;
            var s = State;
            return new MeInfo
            {
                Id = a.Username, Uid = a.Id, Name = !string.IsNullOrWhiteSpace(a.Name) ? a.Name : a.Username, Bio = a.Bio, Color = a.Color,
                Joined = s.profile.createdOn != DateTime.MinValue ? D.Key(s.profile.createdOn) : a.Created,
                Read = s.progress.Where(p => p.finished).Select(p => p.bookId).ToList(),
                Favorites = X.favorites.ToList(),
            };
        }

        /// <summary>Data.save(): saves on the phone and queues the sync (state, web fields and settings).</summary>
        public static void Save()
        {
            if (Host.Account.SignedIn) Host.Account.CommitExtras();
            Host.Save();
        }

        // ---------- Ratings and finish dates (half stars live in X) ----------

        /// <summary>The reader's rating of a book, 0..5 in half stars (0 = not rated).</summary>
        public static double Rating(string bookId)
        {
            if (X.progress.TryGetValue(bookId, out var e) && e.rating.HasValue) return e.rating.Value;
            var p = State.progress.Find(x => x.bookId == bookId);
            return p != null ? p.rating : 0;
        }

        /// <summary>Sets the rating (half stars allowed). Call Save() after.</summary>
        public static void SetRating(string bookId, double rating)
        {
            var p = State.ProgressFor(bookId);
            p.rating = (int)Math.Round(rating); // rounds half to even, like the JSON reader does
            X.For(bookId).rating = rating;
        }

        public static string FinishedOn(string bookId) => X.progress.TryGetValue(bookId, out var e) ? e.finishedOn : null;

        /// <summary>Stores the day a book was finished ("yyyy-MM-dd"). Call Save() after.</summary>
        public static void SetFinishedOn(string bookId, string dayKey)
        {
            State.ProgressFor(bookId);
            X.For(bookId).finishedOn = dayKey;
        }

        // ---------- Derived ----------

        public static List<FinishedBook> Finished(AppState state = null)
        {
            state = state ?? State;
            return state.progress.Where(p => p.finished)
                .Select(p => new FinishedBook { BookId = p.bookId, FinishedOn = FinishedOn(p.bookId) ?? LastLogDate(state, p.bookId), Rating = Rating(p.bookId), Progress = p })
                .OrderByDescending(f => f.FinishedOn ?? "", StringComparer.Ordinal).ToList();
        }

        public static string LastLogDate(AppState state, string bookId)
        {
            DateTime? k = null;
            foreach (var l in state.logs) if (l.bookId == bookId && (k == null || l.date > k)) k = l.date;
            return k.HasValue ? D.Key(k.Value) : null;
        }

        public static int PagesInYear(AppState state, int y) => state.logs.Where(l => l.date.Year == y).Sum(l => l.toPage - l.fromPage);

        public static int BooksInYear(AppState state, int y)
        {
            var prefix = y.ToString(CultureInfo.InvariantCulture);
            return Finished(state).Count(p => (p.FinishedOn ?? "").StartsWith(prefix, StringComparison.Ordinal));
        }

        /// <summary>Weeks in a row (before this one) meeting the weekly goal, and the best run.</summary>
        public static (int Current, int Best) WeeksMeetingGoal(AppState state, DateTime today)
        {
            int target = state.profile.goals.WeeklyTarget;
            int run = 0, best = 0, cur = 0;
            var start = HabitEngine.WeekStart(today);
            var first = state.profile.createdOn != DateTime.MinValue ? state.profile.createdOn.Date : today.Date;
            bool counting = true;
            for (var w = start.AddDays(-7); w >= first; w = w.AddDays(-7))
            {
                int p = 0;
                for (int i = 0; i < 7; i++) p += state.PagesOn(w.AddDays(i));
                if (p >= target) { run++; if (counting) cur++; } else { counting = false; run = 0; }
                best = Math.Max(best, run);
            }
            return (cur, Math.Max(best, cur));
        }

        public static int ReadingDaysCount(AppState state) => state.logs.Select(l => l.date.Date).Distinct().Count();

        public static List<Achievement> Achievements(AppState state = null)
        {
            state = state ?? State;
            var fin = Finished(state).Select(p => p.BookId).ToList();
            int mx = fin.Count(id => Book(id)?.shelves.Contains("mexican") == true);
            int wk = WeeksMeetingGoal(state, Clock.Today).Best;
            int y = Clock.Today.Year;
            var got = new Dictionary<string, bool>
            {
                ["first-book"] = fin.Count >= 1,
                ["ten-chapters"] = state.quizResults.Count >= 10,
                ["month"] = wk >= 4,
                ["night-owl"] = X.nightLogs >= 10,
                ["focus-10"] = X.focusSessions.Count >= 10,
                ["reflections-5"] = state.reflections.Count >= 5,
                ["theater"] = fin.Any(id => Book(id)?.shelves.Contains("drama") == true),
                ["mexico"] = mx >= 3,
                ["paper"] = fin.Count >= 5,
                ["year-12"] = new[] { y, y - 1, y - 2, y - 3 }.Any(yy => BooksInYear(state, yy) >= 12),
            };
            return ACHIEVEMENTS.Select(a => new Achievement { Id = a.Id, Name = a.Name, Detail = a.Detail, Got = got.TryGetValue(a.Id, out var g) && g }).ToList();
        }

        /// <summary>Books by title or author (accents ignored) and the reader's lists by title.</summary>
        public static SearchResult Search(string q)
        {
            q = (q ?? "").Trim().ToLowerInvariant();
            var r = new SearchResult();
            if (q.Length == 0) return r;
            var nq = Norm(q);
            bool Has(string s) => Norm(s).Contains(nq);
            r.Books = Catalog.books.Where(b => Has(b.title) || Has(b.author)).ToList();
            r.Lists = X.lists.Where(l => Has(l.title)).ToList();
            return r;
        }

        static string Norm(string s)
        {
            var d = (s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var c in d) if (c < '̀' || c > 'ͯ') sb.Append(c);
            return sb.ToString();
        }

        public static List<WebNotification> Notifications() => X.notifications;
    }

    /// <summary>The signed-in account as the web's Accounts.current() shows it (live view over AccountService).</summary>
    public class Acc
    {
        AccountService S => MarginApp.Instance.Account;
        public string Id => S.UserId;
        public string Username => S.Me?.username ?? "";
        public string Name => S.Me?.name ?? "";
        public string Email => S.Email;
        public string Bio => S.Me?.bio ?? "";
        public string Color => S.Me?.color;
        public string University => S.Me?.university ?? "";
        public bool IsCreator => S.IsCreator;
        public bool IsGuest => S.IsGuest;
        public string PendingEmail => S.PendingEmail;
        /// <summary>"yyyy-MM-dd" the account was created.</summary>
        public string Created => S.Me != null && S.Me.created_at != default ? D.Key(S.Me.created_at) : "";
        public AppState State => MarginApp.Instance.State;
        public WebSettings Settings => S.Settings;
        public Profile Profile => S.Me;
    }

    /// <summary>data.js Accounts.</summary>
    public static class Accounts
    {
        static readonly Acc _acc = new Acc();

        /// <summary>The signed-in account, or null.</summary>
        public static Acc Current => MarginApp.Instance != null && MarginApp.Instance.Account.SignedIn ? _acc : null;

        /// <summary>The username, or null.</summary>
        public static string Session() => Current?.Username;
    }

    /// <summary>The app clock (core.js Clock): real date plus the class demo's "simulate next day".</summary>
    public static class Clock
    {
        static Services.Clock C => MarginApp.Instance.Clock;
        public static DateTime Now => C.Now;
        public static DateTime Today => C.Today;
        public static bool IsSimulated => C.IsSimulated;
        public static int DayOffset => C.DayOffset;
        public static void AdvanceDay() => C.AdvanceDay();
        public static void ResetSimulation() => C.ResetSimulation();
    }
}
