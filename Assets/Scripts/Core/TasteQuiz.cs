// Taste quiz: ten quick, skippable questions that connect the catalog to the reader's real life
// (what they study, what is going on, how they want to feel, what they watch, how much they read)
// plus classic reading preferences (pace, length, era, region) and books they already liked.
// Scoring is a transparent weighted sum over the per-book metadata in books.json (moods, pace, fields,
// life, screen, fieldNotes), shelves, tags, pages and year. Every result says why, citing the reader's
// own answers. Ported 1:1 to preview/js/core.js (TasteQuiz).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Margin.Core
{
    /// <summary>One answer of the taste quiz.</summary>
    public class TasteOption
    {
        public string id;
        /// <summary>Text on the button.</summary>
        public string label;
        /// <summary>Small line under the label (examples, page ranges), or null.</summary>
        public string hint;
        /// <summary>Phrase used in a result's reason ("You study Law", "short"), or null when the answer never shows up in reasons.</summary>
        public string reason;

        public TasteOption() { }

        public TasteOption(string id, string label, string reason = null, string hint = null)
        {
            this.id = id; this.label = label; this.reason = reason; this.hint = hint;
        }
    }

    /// <summary>The reader's answers, saved in the synced state as state.tastes. Every field is optional.</summary>
    [Serializable]
    public class Tastes
    {
        /// <summary>Id of TasteQuiz.Fields (what they study or do).</summary>
        public string field;
        /// <summary>Ids of TasteQuiz.Levels (how much they read today).</summary>
        public string level;
        /// <summary>Ids of TasteQuiz.Life (what is going on in their life).</summary>
        public List<string> life = new List<string>();
        /// <summary>Ids of TasteQuiz.Moods (how they want to feel). Saves from the first, three-question quiz used this key for themes; Clean migrates them.</summary>
        public List<string> feel = new List<string>();
        /// <summary>Ids of TasteQuiz.Screens (films and series they like).</summary>
        public List<string> screen = new List<string>();
        /// <summary>Id of TasteQuiz.Paces.</summary>
        public string pace;
        /// <summary>Id of TasteQuiz.Lengths.</summary>
        public string length;
        /// <summary>Id of TasteQuiz.Eras.</summary>
        public string era;
        /// <summary>Id of TasteQuiz.Regions.</summary>
        public string region;
        /// <summary>Catalog ids of books they already read and liked (seeds, never recommended).</summary>
        public List<string> liked = new List<string>();
    }

    public static class TasteQuiz
    {
        // ---------- Vocabularies of the per-book metadata (books.json). Mirrored in core.js. ----------

        /// <summary>Book.moods: how the book tends to make readers feel.</summary>
        public static readonly string[] MoodVocab = { "thoughtful", "moving", "thrilling", "unsettling", "hopeful", "funny" };
        /// <summary>Book.pace.</summary>
        public static readonly string[] PaceVocab = { "fast", "medium", "slow" };
        /// <summary>Book.fields (and fieldNotes keys): study fields whose themes the book speaks to.</summary>
        public static readonly string[] FieldVocab = { "law", "health", "psychology", "tech", "business", "communication", "humanities", "arts", "education" };
        /// <summary>Book.life: life situations at the center of the book.</summary>
        public static readonly string[] LifeVocab = { "love", "family", "meaning", "pressure", "change", "friendship" };
        /// <summary>Book.screen: film and series genres that lead to the book.</summary>
        public static readonly string[] ScreenVocab = { "scifi", "period", "horror", "crime", "epic", "latam" };

        // ---------- Weights ----------

        public const double FieldWeight = 3.0;        // the book speaks to the reader's field
        public const double LifeWeight = 2.0;         // per matching life situation
        public const double MoodWeight = 1.5;         // per matching mood
        public const double ScreenWeight = 1.25;      // per matching film or series genre
        public const double PaceMatch = 1.5;
        public const double PaceNear = 0.5;           // medium pace when fast or slow was asked for
        public const double LengthMatch = 2.0;
        public const double LengthNear = 0.75;        // shorter than asked for (still easy to pick up)
        public const double EraMatch = 2.0;
        public const double RegionMatch = 2.0;
        public const double SeedTagWeight = 1.0;      // per tag shared with a liked book
        public const double SeedShelfWeight = 0.5;    // per shelf shared with a liked book (not "classics")
        public const double SeedMoodWeight = 0.5;     // per mood shared with a liked book
        public const double SeedMax = 4.0;            // one liked book can't outweigh everything else
        public const double BeginnerShort = 2.0;      // reads barely: short books first
        public const double BeginnerFast = 1.0;       // ... and gripping ones
        public const double BeginnerLong = -1.5;      // ... and not the long ones yet
        public const double CasualShort = 1.0;        // about a book a year: the same, gentler
        public const double CasualFast = 0.5;
        public const double CasualLong = -0.75;
        public const double HomeBoost = 0.5;          // share of the quiz score added to Home's "For you"

        public const int ShortPages = 150;
        public const int MediumPages = 300;
        /// <summary>Books from this year on (or on the "modern" shelf) count as modern.</summary>
        public const int ModernYear = 1990;
        public const int MaxPerAuthor = 1;
        public const int MaxPerShelf = 3;
        public const int Questions = 10;

        /// <summary>Shelves and tags too common to say anything about taste.</summary>
        static readonly string[] GenericShelves = { "classics", "short-reads", "modern" };
        static readonly string[] GenericTags = { "modern" };

        // ---------- Questions ----------

        public static readonly TasteOption[] Fields =
        {
            new TasteOption("law", "Law", "You study Law"),
            new TasteOption("health", "Medicine or health", "You're in health", "Medicine, Nursing, Nutrition, Dentistry"),
            new TasteOption("psychology", "Psychology", "You study Psychology"),
            new TasteOption("tech", "Engineering or tech", "You're in engineering and tech", "Engineering, Computer Science, Data"),
            new TasteOption("business", "Business or politics", "You're in business and politics", "Business, Economics, International Relations"),
            new TasteOption("communication", "Communication or marketing", "You study Communication", "Communication, Marketing, Journalism"),
            new TasteOption("humanities", "Humanities", "You study the humanities", "History, Philosophy, Literature, Languages"),
            new TasteOption("arts", "Arts or design", "You're in arts and design", "Architecture, Design, Music, Film"),
            new TasteOption("education", "Education", "You study Education", "Teaching, Pedagogy"),
            new TasteOption("other", "Something else", null, "Or not studying right now"),
        };

        public static readonly TasteOption[] Levels =
        {
            new TasteOption("barely", "I barely read", null, "No problem: we'll start with short, gripping books"),
            new TasteOption("yearly", "About a book a year"),
            new TasteOption("several", "A few books a year"),
            new TasteOption("lots", "A lot"),
        };

        public static readonly TasteOption[] Life =
        {
            new TasteOption("love", "Love and relationships", "Love is on your mind, and it's at the heart of this one"),
            new TasteOption("family", "Family", "Family is on your mind, and it's at the center of this book"),
            new TasteOption("meaning", "Looking for meaning or faith", "You're looking for meaning, and so is this book"),
            new TasteOption("pressure", "Pressure and stress", "Life feels heavy right now: this book knows that weight"),
            new TasteOption("change", "Moving or big changes", "You're going through changes, and so does this story"),
            new TasteOption("friendship", "Friendship", "Friendship matters to you right now: it's at the heart of this one"),
            new TasteOption(None, "Nothing special"),
        };

        public static readonly TasteOption[] Moods =
        {
            new TasteOption("thoughtful", "Thoughtful", "You want a book that makes you think"),
            new TasteOption("moving", "Moved", "You want to be moved"),
            new TasteOption("thrilling", "On the edge of my seat", "You want to be on the edge of your seat"),
            new TasteOption("unsettling", "Unsettled or scared", "You want to be unsettled"),
            new TasteOption("hopeful", "Hopeful", "You want something hopeful"),
            new TasteOption("funny", "Entertained", "You want to be entertained"),
        };

        public static readonly TasteOption[] Screens =
        {
            new TasteOption("scifi", "Sci-fi and dystopian series", "You watch sci-fi and dystopias"),
            new TasteOption("period", "Period dramas and romance", "You watch period dramas"),
            new TasteOption("horror", "Horror", "You watch horror"),
            new TasteOption("crime", "Crime and thrillers", "You watch crime and thrillers"),
            new TasteOption("epic", "Historical epics and adventure", "You watch epics and adventure"),
            new TasteOption("latam", "Latin American films and telenovelas", "You watch Latin American drama"),
        };

        public static readonly TasteOption[] Paces =
        {
            new TasteOption("fast", "Quick and gripping", "quick and gripping", "You want to know what happens next"),
            new TasteOption("slow", "Slow and deep", "slow and deep", "You like to stop and think"),
            new TasteOption(Any, "Either works"),
        };

        public static readonly TasteOption[] Lengths =
        {
            new TasteOption("short", "Short", "short", $"Up to {ShortPages} pages"),
            new TasteOption("medium", "Medium", "medium length", $"{ShortPages + 1} to {MediumPages} pages"),
            new TasteOption("long", "Long", "long", $"More than {MediumPages} pages"),
            new TasteOption(Any, "I don't mind", null, "Any length"),
        };

        public static readonly TasteOption[] Eras =
        {
            new TasteOption("classic", "Classics", "a classic", "From the ancient world to the 1980s"),
            new TasteOption("modern", "Recent books", "recent", $"Published since {ModernYear}"),
            new TasteOption(Both, "Both"),
        };

        public static readonly TasteOption[] Regions =
        {
            new TasteOption("latam", "Mexico and Latin America", "You asked for Mexico and Latin America"),
            new TasteOption("world", "Anywhere in the world"),
        };

        public const string Any = "any";
        public const string Both = "both";
        public const string None = "none";
        public const string Other = "other";
        public const string World = "world";

        public static TasteOption Find(TasteOption[] options, string id) =>
            id == null ? null : options.FirstOrDefault(o => o.id == id);

        // ---------- Answers ----------

        /// <summary>True when no answer changes the ranking (everything skipped or "I don't mind").</summary>
        public static bool IsEmpty(Tastes t)
        {
            t = Clean(t);
            return (t.field == null || t.field == Other)
                && (t.level == null || t.level == "several" || t.level == "lots")
                && t.life.All(x => x == None) && t.feel.Count == 0 && t.screen.Count == 0
                && (t.pace == null || t.pace == Any) && (t.length == null || t.length == Any)
                && (t.era == null || t.era == Both) && (t.region == null || t.region == World)
                && t.liked.Count == 0;
        }

        /// <summary>
        /// Drops unknown or repeated ids and migrates saves from the first quiz ({ feel: [themes], length, era }).
        /// Never returns null.
        /// </summary>
        public static Tastes Clean(Tastes t)
        {
            var r = new Tastes();
            if (t == null) return r;
            r.field = Find(Fields, t.field)?.id;
            r.level = Find(Levels, t.level)?.id;
            r.pace = Find(Paces, t.pace)?.id;
            r.length = Find(Lengths, t.length)?.id;
            r.region = Find(Regions, t.region)?.id;
            r.era = Find(Eras, t.era)?.id ?? LegacyEra(t.era);
            AddAll(r.life, t.life, Life);
            AddAll(r.screen, t.screen, Screens);
            if (t.feel != null)
                foreach (var f in t.feel)
                {
                    if (Find(Moods, f) != null) { if (!r.feel.Contains(f)) r.feel.Add(f); continue; }
                    // Themes of the first quiz, mapped to the closest new answer. "theater" has none.
                    switch (f)
                    {
                        case "ideas": AddAll(r.feel, new List<string> { "thoughtful" }, Moods); break;
                        case "dark": AddAll(r.feel, new List<string> { "unsettling" }, Moods); break;
                        case "love": AddAll(r.life, new List<string> { "love" }, Life); break;
                        case "adventure": AddAll(r.screen, new List<string> { "epic" }, Screens); break;
                        case "power": AddAll(r.screen, new List<string> { "scifi" }, Screens); break;
                        case "mexico": if (r.region == null) r.region = "latam"; break;
                    }
                }
            // "Nothing special" can't go with anything else.
            if (r.life.Contains(None) && r.life.Count > 1) r.life.Remove(None);
            if (t.liked != null)
                foreach (var id in t.liked)
                    if (!string.IsNullOrEmpty(id) && !r.liked.Contains(id)) r.liked.Add(id);
            return r;
        }

        static string LegacyEra(string id)
        {
            switch (id)
            {
                case "ancient": case "early": case "19th": return "classic";
                case "any": return Both;
                default: return null;
            }
        }

        static void AddAll(List<string> into, List<string> from, TasteOption[] options)
        {
            if (from == null) return;
            foreach (var x in from)
                if (Find(options, x) != null && !into.Contains(x)) into.Add(x);
        }

        // ---------- Book facts ----------

        public static bool IsModern(Book b) => (b.shelves != null && b.shelves.Contains("modern")) || b.year >= ModernYear;

        /// <summary>"short", "medium" or "long".</summary>
        public static string LengthOf(Book b) => b.pages <= ShortPages ? "short" : b.pages <= MediumPages ? "medium" : "long";

        internal static int LengthRank(string id) => id == "short" ? 0 : id == "medium" ? 1 : 2;

        static bool Has(List<string> list, string x) => list != null && list.Contains(x);

        internal static List<string> Shared(List<string> a, List<string> b, string[] skip, bool ignoreCase)
        {
            var r = new List<string>();
            if (a == null || b == null) return r;
            var cmp = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            foreach (var x in a)
                if (x != null && !skip.Contains(x, cmp) && b.Contains(x, cmp) && !r.Contains(x, cmp)) r.Add(x);
            return r;
        }

        // ---------- Scoring ----------

        /// <summary>
        /// Score and reason for one book. The reason starts with the strongest matching answer, in the reader's
        /// own terms ("You study Law: the law against your conscience"), followed by up to two short facts that
        /// also matched ("short", "a classic"). Reason is null when nothing matched.
        /// </summary>
        public static (double score, string reason) Score(Book b, Tastes tastes, Catalog catalog = null)
        {
            var t = Clean(tastes);
            double score = 0;
            // Candidate main clauses, in priority order for ties.
            var clauses = new List<(double weight, string text)>();

            // What they study or do.
            var field = Find(Fields, t.field);
            if (field != null && field.reason != null && Has(b.fields, field.id))
            {
                score += FieldWeight;
                string note = null;
                if (b.fieldNotes != null) b.fieldNotes.TryGetValue(field.id, out note);
                clauses.Add((FieldWeight, field.reason + ": " + (string.IsNullOrEmpty(note) ? "it speaks to your field" : note)));
            }

            // What is going on in their life: every match counts, the first one is cited.
            var life = t.life.Where(x => x != None && Has(b.life, x)).ToList();
            if (life.Count > 0)
            {
                score += LifeWeight * life.Count;
                clauses.Add((LifeWeight * life.Count, Find(Life, life[0]).reason));
            }

            // Books they liked: the closest one is the seed.
            double seed = 0;
            string seedText = null;
            if (catalog != null)
                foreach (var id in t.liked)
                {
                    var lb = catalog.FindBook(id);
                    if (lb == null || lb.id == b.id) continue;
                    var tags = Shared(lb.tags, b.tags, GenericTags, true);
                    double s = tags.Count * SeedTagWeight
                        + Shared(lb.shelves, b.shelves, GenericShelves, false).Count * SeedShelfWeight
                        + Shared(lb.moods, b.moods, new string[0], false).Count * SeedMoodWeight;
                    if (!string.IsNullOrEmpty(lb.author) && lb.author == b.author) s += SeedTagWeight;
                    s = Math.Min(SeedMax, s);
                    if (s > seed)
                    {
                        seed = s;
                        seedText = tags.Count >= 2 ? $"You liked {lb.title}: this one also has {tags[0]} and {tags[1]}"
                            : tags.Count == 1 ? $"You liked {lb.title}: this one also has {tags[0]}"
                            : $"You liked {lb.title}: this one has a similar feel";
                    }
                }
            score += seed;
            clauses.Add((seed, seedText));

            // How they want to feel.
            var moods = t.feel.Where(x => Has(b.moods, x)).ToList();
            score += MoodWeight * moods.Count;
            clauses.Add((MoodWeight * moods.Count, moods.Count > 0 ? Find(Moods, moods[0]).reason : null));

            // Films and series.
            var screens = t.screen.Where(x => Has(b.screen, x)).ToList();
            score += ScreenWeight * screens.Count;
            clauses.Add((ScreenWeight * screens.Count, screens.Count > 0 ? Find(Screens, screens[0]).reason : null));

            // Mexico and Latin America.
            if (t.region == "latam" && b.shelves != null && (b.shelves.Contains("mexican") || b.shelves.Contains("latin-american")))
            {
                score += RegionMatch;
                clauses.Add((RegionMatch, Find(Regions, "latam").reason + (b.shelves.Contains("mexican") ? ": this one is Mexican" : ": this one is Latin American")));
            }

            // Short facts that matched.
            var facts = new List<string>();
            var len = LengthOf(b);
            if (t.length != null && t.length != Any)
            {
                if (len == t.length) { score += LengthMatch; facts.Add(Find(Lengths, len).reason); }
                else if (LengthRank(len) < LengthRank(t.length)) score += LengthNear;
            }
            if (t.pace != null && t.pace != Any && b.pace != null)
            {
                if (b.pace == t.pace) { score += PaceMatch; facts.Add(Find(Paces, t.pace).reason); }
                else if (b.pace == "medium") score += PaceNear;
            }
            if (t.era == "classic" || t.era == "modern")
            {
                bool modern = IsModern(b);
                if (modern == (t.era == "modern")) { score += EraMatch; facts.Add(Find(Eras, t.era).reason); }
            }
            // Reading level: beginners get short, gripping books first.
            if (t.level == "barely" || t.level == "yearly")
            {
                bool beginner = t.level == "barely";
                if (len == "short") { score += beginner ? BeginnerShort : CasualShort; if (!facts.Contains("short")) facts.Add("short"); }
                if (b.pace == "fast") { score += beginner ? BeginnerFast : CasualFast; if (!facts.Contains("quick and gripping")) facts.Add("quick and gripping"); }
                if (len == "long") score += beginner ? BeginnerLong : CasualLong;
            }

            // The strongest clause wins; ties go to the earlier one (field, life, liked, mood, screen, region).
            string main = null;
            double best = 0;
            foreach (var (w, text) in clauses)
                if (text != null && w > best) { best = w; main = text; }

            var parts = new List<string>();
            if (main != null) parts.Add(main);
            parts.AddRange(facts.Take(main != null ? 2 : 3));
            if (parts.Count == 0) return (score, null);
            var reason = string.Join(" · ", parts);
            return (score, char.ToUpperInvariant(reason[0]) + reason.Substring(1));
        }

        /// <summary>
        /// The best books for these answers, leaving out excluded ids (finished, current) and the books the reader
        /// said they liked. Sorted by score, then shorter book, then id, so the order is stable. Diversity: at most
        /// MaxPerAuthor book per author and MaxPerShelf per distinctive shelf, unless that leaves fewer than count.
        /// </summary>
        public static List<Recommendation> Recommend(Catalog catalog, Tastes tastes, ICollection<string> exclude = null, int count = 5)
        {
            var t = Clean(tastes);
            bool empty = IsEmpty(t);
            var seen = new HashSet<string>();
            var all = new List<Recommendation>();
            if (catalog?.books == null) return all;
            foreach (var b in catalog.books)
            {
                if (b == null || string.IsNullOrEmpty(b.id) || !seen.Add(b.id)) continue;
                if ((exclude != null && exclude.Contains(b.id)) || t.liked.Contains(b.id)) continue;
                var (score, reason) = Score(b, t, catalog);
                if (reason == null)
                    reason = empty && b.pages <= ShortPages ? "Short: a good one to start with" : score > 0 ? "Close to your answers" : "From the Margin catalog";
                all.Add(new Recommendation { Book = b, Score = score, Reason = reason });
            }
            var sorted = all
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Book.pages)
                .ThenBy(r => r.Book.id, StringComparer.Ordinal)
                .ToList();
            // Books that match come first even when diversity defers some of them.
            var picked = new List<Recommendation>();
            var authors = new Dictionary<string, int>();
            var shelves = new Dictionary<string, int>();
            Diverse(sorted.Where(r => r.Score > 0).ToList(), Math.Max(0, count), picked, authors, shelves);
            Diverse(sorted.Where(r => r.Score <= 0).ToList(), Math.Max(0, count), picked, authors, shelves);
            return picked;
        }

        /// <summary>
        /// Greedy pick in score order that skips a book whose author or shelf is already well represented,
        /// then fills any gap with the skipped ones, in order.
        /// </summary>
        static void Diverse(List<Recommendation> sorted, int count, List<Recommendation> picked, Dictionary<string, int> authors, Dictionary<string, int> shelves)
        {
            var skipped = new List<Recommendation>();
            foreach (var r in sorted)
            {
                if (picked.Count >= count) break;
                var b = r.Book;
                var a = b.author ?? "";
                var own = (b.shelves ?? new List<string>()).Where(s => !GenericShelves.Contains(s)).Distinct().ToList();
                bool tooMany = (authors.TryGetValue(a, out var na) && na >= MaxPerAuthor)
                    || own.Any(s => shelves.TryGetValue(s, out var ns) && ns >= MaxPerShelf);
                if (tooMany) { skipped.Add(r); continue; }
                picked.Add(r);
                authors[a] = (authors.TryGetValue(a, out var x) ? x : 0) + 1;
                foreach (var s in own) shelves[s] = (shelves.TryGetValue(s, out var y) ? y : 0) + 1;
            }
            // Not enough variety: fill with the best of the skipped ones, in order.
            foreach (var r in skipped)
            {
                if (picked.Count >= count) break;
                picked.Add(r);
                authors[r.Book.author ?? ""] = (authors.TryGetValue(r.Book.author ?? "", out var x) ? x : 0) + 1;
            }
        }

        /// <summary>
        /// What the results screen shows: the books that match something (up to five), and never fewer than three.
        /// </summary>
        public static List<Recommendation> Results(Catalog catalog, Tastes tastes, ICollection<string> exclude = null)
        {
            var recs = Recommend(catalog, tastes, exclude, 5);
            var shown = recs.Where(r => r.Score > 0).ToList();
            return shown.Count >= 3 ? shown : recs.Take(3).ToList();
        }

        /// <summary>
        /// Home's "For you": the reading-based recommendations (Recommender) with a share of the quiz score added,
        /// so the answers nudge the list without replacing what the reader actually read. The quiz reason is shown
        /// when the quiz contributes more than the reading history. Liked books are left out.
        /// </summary>
        public static List<Recommendation> Boost(List<Recommendation> recs, Catalog catalog, Tastes tastes, int count)
        {
            var t = Clean(tastes);
            if (recs == null) return new List<Recommendation>();
            if (IsEmpty(t)) return recs.Take(count).ToList();
            var r = new List<Recommendation>();
            foreach (var x in recs)
            {
                if (x?.Book == null || t.liked.Contains(x.Book.id)) continue;
                var (s, reason) = Score(x.Book, t, catalog);
                double add = s * HomeBoost;
                r.Add(new Recommendation { Book = x.Book, Score = x.Score + add, Reason = add > x.Score && reason != null ? reason : x.Reason });
            }
            return r
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Book.pages)
                .ThenBy(x => x.Book.id, StringComparer.Ordinal)
                .Take(Math.Max(0, count))
                .ToList();
        }

        /// <summary>Books the reader shouldn't be offered: finished ones and the current one.</summary>
        public static HashSet<string> ExcludeFor(AppState s)
        {
            var r = new HashSet<string>();
            if (s == null) return r;
            foreach (var p in s.progress) if (p.finished) r.Add(p.bookId);
            if (!string.IsNullOrEmpty(s.profile?.currentBookId)) r.Add(s.profile.currentBookId);
            return r;
        }
    }
}
