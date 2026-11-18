// Tests for Margin.Core.NextBook: "Find your next book", which learns from the reader's history plus five answers.
using System.Collections.Generic;
using System.Linq;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    public class NextBookTests
    {
        static List<string> L(params string[] xs) => xs.ToList();

        static Book B(string id, int pages, string[] tags, string[] moods = null, string[] shelves = null, string author = null) => new Book
        {
            id = id, title = id.ToUpper(), author = author ?? "Author " + id, pages = pages, year = 1950,
            tags = L(tags), moods = L(moods ?? new string[0]), shelves = L(shelves ?? new string[0]),
        };

        /// <summary>Small catalog: two dystopias, two love stories, an essay and a play.</summary>
        static Catalog Cat() => new Catalog
        {
            books = new List<Book>
            {
                B("seed", 200, new[] { "dystopia", "power" }, new[] { "thrilling" }),
                B("dys-short", 120, new[] { "dystopia", "power" }, new[] { "thrilling" }),
                B("dys-long", 400, new[] { "dystopia", "power" }, new[] { "thoughtful" }),
                B("love-short", 100, new[] { "love", "marriage" }, new[] { "moving" }),
                B("love-long", 500, new[] { "love", "marriage" }, new[] { "moving" }),
                B("essay", 180, new[] { "culture" }, new[] { "thoughtful" }, new[] { "essays" }),
                B("play", 130, new[] { "tragedy" }, new[] { "moving" }, new[] { "drama" }),
                B("mex", 160, new[] { "mexico" }, new[] { "thoughtful" }, new[] { "mexican" }),
            },
        };

        static AppState State(string current = null, params (string id, bool finished, int rating)[] progress)
        {
            var s = new AppState();
            s.profile.currentBookId = current;
            foreach (var (id, finished, rating) in progress) s.progress.Add(new BookProgress { bookId = id, finished = finished, rating = rating });
            return s;
        }

        static List<string> Ids(AppState s, NextBookAnswers a = null, ICollection<string> want = null, ICollection<string> dnf = null) =>
            NextBook.Recommend(Cat(), s, a, want, dnf, 10).Select(r => r.Book.id).ToList();

        [Test]
        public void EverythingRead_ReturnsEmpty_NotACrash()
        {
            // The results screen shows a "no matches left" line for this; it must not index into an empty list.
            var cat = Cat();
            var s = State(null, cat.books.Select(b => (b.id, true, 4)).ToArray());
            Assert.IsEmpty(NextBook.Recommend(cat, s, new NextBookAnswers()));
        }

        [Test]
        public void ExcludesReadCurrentAndDroppedBooks()
        {
            var ids = Ids(State("dys-long", ("seed", true, 5), ("love-short", true, 4)), null, null, L("play"));
            CollectionAssert.DoesNotContain(ids, "seed");
            CollectionAssert.DoesNotContain(ids, "love-short");
            CollectionAssert.DoesNotContain(ids, "dys-long");
            CollectionAssert.DoesNotContain(ids, "play");
        }

        [Test]
        public void HighlyRatedTagsRankHigher()
        {
            var ids = Ids(State(null, ("seed", true, 5)));
            Assert.Less(ids.IndexOf("dys-short"), ids.IndexOf("love-short"));
            Assert.Less(ids.IndexOf("dys-long"), ids.IndexOf("love-long"));
        }

        [Test]
        public void LowRatingAndDroppedBooksPushSimilarOnesDown()
        {
            var hated = Ids(State(null, ("seed", true, 1)));
            Assert.Greater(hated.IndexOf("dys-short"), hated.IndexOf("love-short"));
            var dropped = Ids(State(), null, null, L("seed"));
            Assert.Greater(dropped.IndexOf("dys-short"), dropped.IndexOf("love-short"));
        }

        [Test]
        public void WantToReadGetsABoost()
        {
            var plain = Ids(State());
            var wanted = Ids(State(), null, L("love-long"));
            Assert.Less(wanted.IndexOf("love-long"), plain.IndexOf("love-long"));
        }

        [Test]
        public void QuizAnswersShiftTheRanking()
        {
            var s = State(null, ("seed", true, 4));
            // The history favors dystopias; asking for a short, moving book brings the love story ahead.
            var asked = Ids(s, new NextBookAnswers { mood = "moving", length = "short" });
            Assert.AreEqual("love-short", asked[0]);
            Assert.AreEqual("essay", Ids(s, new NextBookAnswers { kind = "ideas" })[0]);
            Assert.AreEqual("play", Ids(s, new NextBookAnswers { kind = "play" })[0]);
            Assert.AreEqual("mex", Ids(s, new NextBookAnswers { region = "mexican" })[0]);
        }

        [Test]
        public void NewMode_PrefersBooksUnlikeTheHistory()
        {
            var s = State(null, ("seed", true, 5));
            Assert.IsTrue(Ids(s, new NextBookAnswers { novelty = "same" })[0].StartsWith("dys"));
            Assert.IsFalse(Ids(s, new NextBookAnswers { novelty = "new" })[0].StartsWith("dys"));
        }

        [Test]
        public void EveryResultSaysWhy()
        {
            var s = State(null, ("seed", true, 5));
            var recs = NextBook.Recommend(Cat(), s, new NextBookAnswers { length = "short" }, null, null, 10);
            Assert.AreEqual("Because you rated SEED 5★ and wanted something short", recs.First(r => r.Book.id == "dys-short").Reason);
            Assert.AreEqual("Because you wanted something short", recs.First(r => r.Book.id == "love-short").Reason);
            Assert.AreEqual("A classic to discover", NextBook.Recommend(Cat(), State(), null, null, null, 10)[0].Reason);
        }

        [Test]
        public void RealCatalog_Works_WithAnEmptyHistory()
        {
            var catalog = System.Text.Json.JsonSerializer.Deserialize<Catalog>(
                System.IO.File.ReadAllText(System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "books.json")),
                new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
            var recs = NextBook.Recommend(catalog, State(), new NextBookAnswers { mood = "thrilling", length = "short", region = "mexican" }, null, null, 5);
            Assert.AreEqual(5, recs.Count);
            Assert.IsTrue(recs[0].Book.shelves.Contains("mexican"));
        }
    }
}
