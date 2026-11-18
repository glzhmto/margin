// "Find your next book" (port of preview/js/core.js NextBook): five questions for readers who already have a
// history. A pure function: finished books and their ratings, the current book, want-to-read and dropped books
// become an affinity per tag, shelf, mood and author; the answers add to that. Every weight is in this file.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Margin.Core
{
    /// <summary>Answers to the five questions (ids of NextBook options). Null = skipped.</summary>
    [Serializable]
    public class NextBookAnswers
    {
        public string mood, length, novelty, kind, region;
    }

    public static class NextBook
    {
        // ---------- Weights ----------
        public const double RatingNeutral = 3, RatingSpan = 2;   // rating 5 -> +1, 3 -> 0, 1 -> -1
        public const double Unrated = 0.25, Current = 0.3, Want = 0.4, Dropped = -1;   // seed weights without a rating
        public const double TagWeight = 1.0, ShelfWeight = 0.5, MoodAffinity = 0.5, AuthorWeight = 1.0, AffinityCap = 4.0;
        public const double SameMult = 1.5, NewMult = 0.2, NewBonus = 1.5;   // "like what I loved" / "something new" scale the history
        public const double WantBoost = 0.75, ReasonMin = 0.5;
        public const double MoodMatch = 2.5, LengthMatch = 2.0, LengthNear = 0.75, KindMatch = 2.5, RegionMatch = 3.0;

        static readonly string[] GenericShelves = { "classics", "short-reads", "modern", "most-read" };

        // Option "reason" = the phrase used in the result's why ("something gripping").
        public static readonly TasteOption[] Moods =
        {
            new TasteOption("thoughtful", "Something to think about", "thought-provoking"),
            new TasteOption("moving", "Something that moves me", "moving"),
            new TasteOption("thrilling", "Something gripping", "gripping"),
            new TasteOption("unsettling", "Something dark and unsettling", "unsettling"),
            new TasteOption("hopeful", "Something hopeful", "hopeful"),
            new TasteOption("funny", "Something funny", "funny"),
        };
        public static readonly TasteOption[] Lengths =
        {
            new TasteOption("short", "A few evenings", "short", "Up to 150 pages"),
            new TasteOption("medium", "A couple of weeks", "mid-length", "151 to 300 pages"),
            new TasteOption("long", "I'm in for the long haul", "long", "More than 300 pages"),
            new TasteOption("any", "No rush", null, "Any length"),
        };
        public static readonly TasteOption[] Novelties =
        {
            new TasteOption("same", "More like what I've loved"),
            new TasteOption("new", "Something new to me", "something new"),
            new TasteOption("either", "Either is fine"),
        };
        public static readonly TasteOption[] Kinds =
        {
            new TasteOption("story", "A novel or a story", "a story"),
            new TasteOption("ideas", "Essays and ideas", "essays"),
            new TasteOption("play", "A play", "a play"),
            new TasteOption("any", "Surprise me"),
        };
        public static readonly TasteOption[] Regions =
        {
            new TasteOption("mexican", "Mexican authors", "a Mexican author"),
            new TasteOption("latam", "Latin American voices", "Latin American voices", "Mexico included"),
            new TasteOption("any", "Anywhere in the world"),
        };

        static bool Has(List<string> list, string x) => list != null && list.Contains(x);

        /// <summary>"story", "ideas" (essays shelf) or "play" (drama shelf).</summary>
        public static string KindOf(Book b) => Has(b.shelves, "essays") ? "ideas" : Has(b.shelves, "drama") ? "play" : "story";

        /// <summary>True when no answer changes the ranking.</summary>
        public static bool IsEmpty(NextBookAnswers a) =>
            a == null || new[] { a.mood, a.length, a.novelty, a.kind, a.region }.All(v => v == null || v == "any" || v == "either");

        static string JoinList(List<string> xs) =>
            xs.Count < 2 ? string.Concat(xs) : string.Join(", ", xs.Take(xs.Count - 1)) + " and " + xs[xs.Count - 1];

        class Seed
        {
            public Book book;
            public double weight;
            public string how;   // finished, reading, want, dropped
            public int rating;
        }

        /// <summary>What each book in the reader's history says about their taste (weight -1..1).</summary>
        static List<Seed> Seeds(Catalog catalog, AppState state, ICollection<string> want, ICollection<string> dnf)
        {
            var cur = state.profile.currentBookId;
            var seen = new HashSet<string>();
            var list = new List<Seed>();
            void Add(string id, double weight, string how, int rating = 0)
            {
                var book = id == null ? null : catalog.FindBook(id);
                if (book != null && seen.Add(id)) list.Add(new Seed { book = book, weight = weight, how = how, rating = rating });
            }
            foreach (var id in dnf ?? new string[0]) Add(id, Dropped, "dropped");
            foreach (var p in state.progress)
            {
                if (!p.finished || p.bookId == cur) continue;
                Add(p.bookId, p.rating > 0 ? (p.rating - RatingNeutral) / RatingSpan : Unrated, "finished", p.rating);
            }
            Add(cur, Current, "reading");
            foreach (var id in want ?? new string[0]) Add(id, Want, "want");
            return list;
        }

        static double Overlap(Book seed, Book b) =>
            TasteQuiz.Shared(seed.tags, b.tags, new string[0], true).Count * TagWeight
            + TasteQuiz.Shared(seed.shelves, b.shelves, GenericShelves, false).Count * ShelfWeight
            + TasteQuiz.Shared(seed.moods, b.moods, new string[0], false).Count * MoodAffinity
            + (!string.IsNullOrEmpty(seed.author) && seed.author == b.author ? AuthorWeight : 0);

        static string HistoryPhrase(Seed s) =>
            s.how == "finished" ? (s.rating > 0 ? $"you rated {s.book.title} {s.rating}★" : $"you finished {s.book.title}")
            : s.how == "reading" ? $"you're reading {s.book.title}"
            : $"{s.book.title} is on your want-to-read list";

        /// <summary>
        /// Ranked books for this reader, best first. Finished, current and dropped books are left out. Each result
        /// says why: "Because you rated Fahrenheit 451 5★ and wanted something short".
        /// </summary>
        public static List<Recommendation> Recommend(Catalog catalog, AppState state, NextBookAnswers answers,
            ICollection<string> want = null, ICollection<string> dnf = null, int count = 5)
        {
            var a = answers ?? new NextBookAnswers();
            var seeds = Seeds(catalog, state, want, dnf);
            var wantSet = new HashSet<string>(want ?? new string[0]);
            var ex = new HashSet<string>(dnf ?? new string[0]);
            foreach (var p in state.progress) if (p.finished) ex.Add(p.bookId);
            if (!string.IsNullOrEmpty(state.profile.currentBookId)) ex.Add(state.profile.currentBookId);

            var results = new List<Recommendation>();
            foreach (var b in catalog.books)
            {
                if (ex.Contains(b.id)) continue;
                double affinity = 0, bestVal = 0;
                Seed best = null;
                foreach (var s in seeds)
                {
                    if (s.book.id == b.id) continue;
                    double v = s.weight * Overlap(s.book, b);
                    affinity += v;
                    if (v > bestVal) { bestVal = v; best = s; }
                }
                affinity = Math.Max(-AffinityCap, Math.Min(AffinityCap, affinity));
                double score = affinity * (a.novelty == "same" ? SameMult : a.novelty == "new" ? NewMult : 1);
                bool novel = a.novelty == "new" && bestVal <= 0;
                if (novel) score += NewBonus;
                if (wantSet.Contains(b.id)) score += WantBoost;

                var adjectives = new List<string>();
                var extras = new List<string>();
                var len = TasteQuiz.LengthOf(b);
                var mood = Moods.FirstOrDefault(m => m.id == a.mood);
                if (mood != null && Has(b.moods, mood.id)) { score += MoodMatch; adjectives.Add(mood.reason); }
                var length = Lengths.FirstOrDefault(l => l.id == a.length);
                if (length != null && length.reason != null)
                {
                    if (len == length.id) { score += LengthMatch; adjectives.Add(length.reason); }
                    else if (TasteQuiz.LengthRank(len) < TasteQuiz.LengthRank(length.id)) score += LengthNear;
                }
                var kind = Kinds.FirstOrDefault(k => k.id == a.kind);
                if (kind != null && kind.reason != null && KindOf(b) == kind.id)
                {
                    score += KindMatch;
                    if (kind.id != "story") extras.Add(kind.reason);
                }
                bool mex = Has(b.shelves, "mexican"), latam = mex || Has(b.shelves, "latin-american");
                if ((a.region == "mexican" && mex) || (a.region == "latam" && latam))
                {
                    score += RegionMatch;
                    extras.Add(Regions.First(r => r.id == a.region).reason);
                }
                if (novel) extras.Add("something new");

                var wanted = new List<string>();
                if (adjectives.Count > 0) wanted.Add("something " + JoinList(adjectives));
                wanted.AddRange(extras);
                var parts = new List<string>();
                if (best != null && bestVal >= ReasonMin) parts.Add(HistoryPhrase(best));
                if (wanted.Count > 0) parts.Add((parts.Count > 0 ? "" : "you ") + "wanted " + JoinList(wanted));
                string reason = parts.Count > 0 ? "Because " + JoinList(parts) : "A classic to discover";
                results.Add(new Recommendation { Book = b, Score = score, Reason = reason });
            }
            results.Sort((x, y) =>
            {
                int c = y.Score.CompareTo(x.Score);
                if (c != 0) return c;
                c = x.Book.pages.CompareTo(y.Book.pages);
                return c != 0 ? c : string.CompareOrdinal(x.Book.id, y.Book.id);
            });
            return results.Take(Math.Max(0, count)).ToList();
        }
    }
}
