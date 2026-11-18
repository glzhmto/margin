// Tests for Margin.Core.TasteQuiz: the ten-question taste quiz that matches the reader with books from the catalog.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    public class TasteQuizTests
    {
        static Catalog real;

        /// <summary>The real catalog (books.json), loaded once.</summary>
        static Catalog Real() => real ??= JsonSerializer.Deserialize<Catalog>(
            File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "books.json")),
            new JsonSerializerOptions { IncludeFields = true });

        static List<Recommendation> Recs(Tastes t, ICollection<string> ex = null, int n = 5) => TasteQuiz.Recommend(Real(), t, ex, n);

        static List<string> Ids(Tastes t, ICollection<string> ex = null, int n = 5) => Recs(t, ex, n).Select(r => r.Book.id).ToList();

        static List<string> L(params string[] xs) => xs.ToList();

        [Test]
        public void Field_MapsToBooksAboutThatField()
        {
            var law = Recs(new Tastes { field = "law" });
            Assert.IsTrue(law.Take(3).All(r => r.Book.fields.Contains("law")), string.Join(", ", law.Select(r => r.Book.id)));
            // With a bigger catalog many law books tie; the field note still becomes the reason.
            var theban = TasteQuiz.Score(Real().books.First(b => b.id == "the-theban-plays"), new Tastes { field = "law" }, Real());
            Assert.Greater(theban.score, 0);
            Assert.AreEqual("You study Law: the law against your conscience", theban.reason);

            var tech = Recs(new Tastes { field = "tech" });
            Assert.IsTrue(tech.All(r => r.Book.fields.Contains("tech")), string.Join(", ", tech.Select(r => r.Book.id)));
            CollectionAssert.IsSubsetOf(new[] { "klara-and-the-sun", "1984" }, Ids(new Tastes { field = "tech" }, null, 20));
            var psych = Recs(new Tastes { field = "psychology" }, null, 10);
            Assert.IsTrue(psych.All(r => r.Book.fields.Contains("psychology")), string.Join(", ", psych.Select(r => r.Book.id)));
            var psychIds = Ids(new Tastes { field = "psychology" }, null, 30);
            Assert.IsTrue(psychIds.Contains("convenience-store-woman") && psychIds.Contains("jekyll-and-hyde"), string.Join(", ", psychIds));
        }

        [Test]
        public void Field_WithoutANoteStillSaysWhy()
        {
            var b = new Book { id = "x", title = "X", author = "A", pages = 100, year = 1900, fields = L("law") };
            Assert.AreEqual("You study Law: it speaks to your field", TasteQuiz.Score(b, new Tastes { field = "law" }).reason);
            Assert.AreEqual(0, TasteQuiz.Score(b, new Tastes { field = "other" }).score);
        }

        [Test]
        public void Beginner_GetsShortFastBooks()
        {
            var top = Recs(new Tastes { level = "barely" });
            Assert.IsTrue(top.All(r => r.Book.pages <= TasteQuiz.ShortPages && r.Book.pace == "fast"), string.Join(", ", top.Select(r => r.Book.id)));
            // The same interests without the level can bring long books; the beginner's list has none.
            var deep = Recs(new Tastes { field = "humanities", feel = L("thoughtful") }, null, 20);
            var easy = Recs(new Tastes { field = "humanities", feel = L("thoughtful"), level = "barely" }, null, 20);
            Assert.Greater(deep.Average(r => r.Book.pages), easy.Average(r => r.Book.pages));
            Assert.IsFalse(easy.Any(r => r.Book.pages > TasteQuiz.MediumPages));
        }

        [Test]
        public void ModernPreference_PicksModernBooks()
        {
            var modern = Recs(new Tastes { era = "modern" });
            Assert.IsTrue(modern.All(r => TasteQuiz.IsModern(r.Book)));
            var classic = Recs(new Tastes { era = "classic", feel = L("thrilling") });
            Assert.IsTrue(classic.All(r => !TasteQuiz.IsModern(r.Book)));
            StringAssert.Contains("recent", modern[0].Reason.ToLowerInvariant());
        }

        [Test]
        public void LikedBooks_AreExcludedAndUsedAsSeeds()
        {
            var t = new Tastes { liked = L("1984") };
            var recs = Recs(t);
            Assert.IsFalse(recs.Any(r => r.Book.id == "1984"));
            // The closest books share 1984's tags, shelves and moods.
            CollectionAssert.Contains(new[] { "animal-farm", "fahrenheit-451" }, recs[0].Book.id);
            StringAssert.StartsWith("You liked 1984: this one also has", recs[0].Reason);
            Assert.IsFalse(TasteQuiz.IsEmpty(t));
        }

        [Test]
        public void FinishedAndCurrentBooks_AreExcluded()
        {
            var s = new AppState();
            s.profile.currentBookId = "the-theban-plays";
            s.progress.Add(new BookProgress { bookId = "the-last-days-of-socrates", finished = true });
            var ids = Ids(new Tastes { field = "law" }, TasteQuiz.ExcludeFor(s), 40);
            Assert.IsFalse(ids.Contains("the-theban-plays") || ids.Contains("the-last-days-of-socrates"));
        }

        [Test]
        public void Diversity_OneBookPerAuthorAndFewPerShelf()
        {
            // Five books by one author outscore everything else.
            var books = Enumerable.Range(1, 5).Select(i => new Book { id = "x" + i, title = "X" + i, author = "Same", pages = 100 + i, year = 1900, moods = L("unsettling", "thrilling"), shelves = L("romance-gothic") }).ToList();
            books.Add(new Book { id = "other1", title = "O1", author = "B", pages = 200, year = 1900, moods = L("unsettling"), shelves = L("romance-gothic") });
            books.Add(new Book { id = "other2", title = "O2", author = "C", pages = 200, year = 1900, moods = L("unsettling"), shelves = L("dystopias") });
            var c = new Catalog { books = books };
            var ids = TasteQuiz.Recommend(c, new Tastes { feel = L("unsettling", "thrilling") }, null, 3).Select(r => r.Book.id).ToList();
            CollectionAssert.AreEqual(new[] { "x1", "other1", "other2" }, ids);
            // When the catalog can't give variety, the list is still filled.
            Assert.AreEqual(5, TasteQuiz.Recommend(c, new Tastes { feel = L("unsettling") }, null, 5).Count);

            // Real catalog, many answer sets: never the same author twice, never more than three from one shelf.
            var sets = new[]
            {
                new Tastes { feel = L("unsettling") }, new Tastes { feel = L("thoughtful") },
                new Tastes { field = "tech", era = "modern" }, new Tastes { liked = L("never-let-me-go") }, new Tastes { screen = L("period"), life = L("love") },
            };
            foreach (var t in sets)
            {
                var r = Recs(t);
                Assert.AreEqual(r.Count, r.Select(x => x.Book.author).Distinct().Count());
                foreach (var shelf in new[] { "latin-american", "mexican", "romance-gothic", "dystopias", "ancient", "essays", "drama" })
                    Assert.LessOrEqual(r.Count(x => x.Book.shelves.Contains(shelf)), TasteQuiz.MaxPerShelf, shelf);
            }
            // When few books match, matches beat variety: asking for Latin America fills the list with Latin American books.
            var latam = Recs(new Tastes { region = "latam" });
            Assert.IsTrue(latam.All(x => x.Book.shelves.Contains("latin-american") || x.Book.shelves.Contains("mexican")));
            Assert.AreEqual(latam.Count, latam.Select(x => x.Book.author).Distinct().Count());
        }

        [Test]
        public void EmptyAnswers_GiveShortBooksToStartWith()
        {
            Assert.IsTrue(TasteQuiz.IsEmpty(null));
            Assert.IsTrue(TasteQuiz.IsEmpty(new Tastes()));
            Assert.IsTrue(TasteQuiz.IsEmpty(new Tastes { field = "other", life = L("none"), pace = "any", length = "any", era = "both", region = "world", level = "lots" }));
            var r = TasteQuiz.Results(Real(), null);
            Assert.AreEqual(3, r.Count);
            Assert.IsTrue(r.All(x => x.Book.pages <= TasteQuiz.ShortPages && x.Reason == "Short: a good one to start with"));
            Assert.AreEqual(0, TasteQuiz.Recommend(null, null).Count);
        }

        [Test]
        public void Results_ShowThreeToFive()
        {
            var r = TasteQuiz.Results(Real(), new Tastes { field = "law", feel = L("thoughtful"), era = "classic" });
            Assert.That(r.Count, Is.InRange(3, 5));
            Assert.IsTrue(r.All(x => x.Score > 0));
        }

        [Test]
        public void Reason_CitesTheReadersOwnAnswers()
        {
            var t = new Tastes { field = "law", feel = L("thoughtful"), era = "classic", length = "short" };
            var top = Recs(t)[0];
            StringAssert.StartsWith("You study Law: ", top.Reason);
            StringAssert.Contains(" · ", top.Reason);
            var b = Real().FindBook("normal-people");
            Assert.AreEqual("Love is on your mind, and it's at the heart of this one · recent",
                TasteQuiz.Score(b, new Tastes { life = L("love"), era = "modern" }).reason);
        }

        [Test]
        public void Ranking_IsDeterministic()
        {
            var t = new Tastes { field = "business", screen = L("crime", "epic"), pace = "fast" };
            CollectionAssert.AreEqual(Ids(t, null, 40), Ids(t, null, 40));
        }

        [Test]
        public void EveryVocabularyValue_MatchesAtLeastOneBook()
        {
            var books = Real().books;
            foreach (var v in TasteQuiz.MoodVocab) Assert.IsTrue(books.Any(b => b.moods.Contains(v)), "mood " + v);
            foreach (var v in TasteQuiz.PaceVocab) Assert.IsTrue(books.Any(b => b.pace == v), "pace " + v);
            foreach (var v in TasteQuiz.FieldVocab) Assert.IsTrue(books.Any(b => b.fields.Contains(v)), "field " + v);
            foreach (var v in TasteQuiz.LifeVocab) Assert.IsTrue(books.Any(b => b.life.Contains(v)), "life " + v);
            foreach (var v in TasteQuiz.ScreenVocab) Assert.IsTrue(books.Any(b => b.screen.Contains(v)), "screen " + v);

            // And every answer that means something brings a matching book to the top, with a reason.
            var answers = new List<Tastes>();
            answers.AddRange(TasteQuiz.Fields.Where(o => o.id != TasteQuiz.Other).Select(o => new Tastes { field = o.id }));
            answers.AddRange(TasteQuiz.Life.Where(o => o.id != TasteQuiz.None).Select(o => new Tastes { life = L(o.id) }));
            answers.AddRange(TasteQuiz.Moods.Select(o => new Tastes { feel = L(o.id) }));
            answers.AddRange(TasteQuiz.Screens.Select(o => new Tastes { screen = L(o.id) }));
            foreach (var t in answers)
            {
                var top = Recs(t, null, 1)[0];
                Assert.Greater(top.Score, 0);
                Assert.AreNotEqual("From the Margin catalog", top.Reason);
            }
            // Every option id in the questions belongs to its vocabulary (or is a "no preference" answer).
            CollectionAssert.IsSubsetOf(TasteQuiz.Fields.Select(o => o.id).Where(x => x != TasteQuiz.Other), TasteQuiz.FieldVocab);
            CollectionAssert.IsSubsetOf(TasteQuiz.Life.Select(o => o.id).Where(x => x != TasteQuiz.None), TasteQuiz.LifeVocab);
            CollectionAssert.AreEquivalent(TasteQuiz.Moods.Select(o => o.id), TasteQuiz.MoodVocab);
            CollectionAssert.AreEquivalent(TasteQuiz.Screens.Select(o => o.id), TasteQuiz.ScreenVocab);
        }

        [Test]
        public void Clean_DropsUnknownIdsAndReadsTheFirstQuizSaves()
        {
            var c = TasteQuiz.Clean(new Tastes { field = "astrology", feel = L("nope", "moving", "moving"), life = L("none", "family"), length = "huge", liked = L("1984", "1984", "") });
            Assert.IsNull(c.field);
            CollectionAssert.AreEqual(new[] { "moving" }, c.feel);
            CollectionAssert.AreEqual(new[] { "family" }, c.life);
            Assert.IsNull(c.length);
            CollectionAssert.AreEqual(new[] { "1984" }, c.liked);

            // { feel: [themes], length, era } from the three-question quiz.
            var old = TasteQuiz.Clean(new Tastes { feel = L("ideas", "dark", "love", "adventure", "power", "mexico", "theater"), length = "short", era = "19th" });
            CollectionAssert.AreEqual(new[] { "thoughtful", "unsettling" }, old.feel);
            CollectionAssert.AreEqual(new[] { "love" }, old.life);
            CollectionAssert.AreEqual(new[] { "epic", "scifi" }, old.screen);
            Assert.AreEqual("latam", old.region);
            Assert.AreEqual("short", old.length);
            Assert.AreEqual("classic", old.era);
            Assert.AreEqual("modern", TasteQuiz.Clean(new Tastes { era = "modern" }).era);
            Assert.AreEqual("both", TasteQuiz.Clean(new Tastes { era = "any" }).era);
        }

        [Test]
        public void Boost_NudgesHomeForYou()
        {
            var s = new AppState();
            var plain = Recommender.Recommend(Real(), s, MomentumLevel.Steady, 100);
            var boosted = TasteQuiz.Boost(plain, Real(), new Tastes { field = "law", liked = L("the-stranger") }, 6);
            Assert.AreEqual(6, boosted.Count);
            Assert.IsTrue(boosted[0].Book.fields.Contains("law"));
            Assert.IsFalse(boosted.Any(r => r.Book.id == "the-stranger"));
            CollectionAssert.AreEqual(plain.Take(6).Select(r => r.Book.id), TasteQuiz.Boost(plain, Real(), null, 6).Select(r => r.Book.id));
        }
    }
}
