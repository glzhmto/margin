// Validates the real content (books.json / quotes.json / tuning.json) against the Margin.Core models.
// Runs only with dotnet (uses System.Text.Json, which Unity does not include).
using System.IO;
using System.Linq;
using System.Text.Json;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    public class DataIntegrityTests
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };

        static T Load<T>(string file) =>
            JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", file)), Options);

        [Test]
        public void Books_ChaptersAreContiguousAndCoverTheBook()
        {
            foreach (var b in Load<Catalog>("books.json").books)
            {
                Assert.IsNotEmpty(b.chapters, b.id);
                Assert.AreEqual(1, b.chapters[0].startPage, $"{b.id} must start on page 1");
                for (int i = 1; i < b.chapters.Count; i++)
                    Assert.AreEqual(b.chapters[i - 1].endPage + 1, b.chapters[i].startPage, $"{b.id} ch. {b.chapters[i].number}");
                Assert.AreEqual(b.pages, b.chapters.Last().endPage, $"{b.id} must end on the last page");
            }
        }

        [Test]
        public void Books_QuizzesAreWellFormed()
        {
            foreach (var b in Load<Catalog>("books.json").books)
            foreach (var c in b.chapters.Where(c => c.HasQuiz))
            foreach (var q in c.quiz.questions)
            {
                var where = $"{b.id} ch. {c.number}: {q.prompt}";
                Assert.IsFalse(string.IsNullOrWhiteSpace(q.prompt), where);
                Assert.GreaterOrEqual(q.options.Count, 2, where);
                Assert.That(q.correctIndex, Is.InRange(0, q.options.Count - 1), where);
                Assert.AreEqual(q.options.Count, q.options.Distinct().Count(), "repeated options: " + where);
                Assert.IsFalse(string.IsNullOrWhiteSpace(q.explanation), where);
            }
        }

        [Test]
        public void Books_ReferenceExistingShelvesAndEveryShelfHasBooks()
        {
            var cat = Load<Catalog>("books.json");
            var ids = cat.shelves.Select(s => s.id).ToHashSet();
            foreach (var b in cat.books)
                foreach (var s in b.shelves) Assert.IsTrue(ids.Contains(s), $"{b.id} → unknown shelf {s}");
            foreach (var s in cat.shelves) Assert.IsNotEmpty(cat.BooksInShelf(s.id), $"empty shelf {s.id}");
            Assert.AreEqual(cat.books.Count, cat.books.Select(b => b.id).Distinct().Count(), "duplicate ids");
        }

        [Test]
        public void Books_QuizMetadataUsesKnownValues()
        {
            foreach (var b in Load<Catalog>("books.json").books)
            {
                Assert.IsNotEmpty(b.moods, b.id + " needs at least one mood");
                CollectionAssert.IsSubsetOf(b.moods, TasteQuiz.MoodVocab, b.id + " moods");
                CollectionAssert.AllItemsAreUnique(b.moods, b.id);
                CollectionAssert.Contains(TasteQuiz.PaceVocab, b.pace, b.id + " pace");
                CollectionAssert.IsSubsetOf(b.fields, TasteQuiz.FieldVocab, b.id + " fields");
                CollectionAssert.AllItemsAreUnique(b.fields, b.id);
                CollectionAssert.IsSubsetOf(b.life, TasteQuiz.LifeVocab, b.id + " life");
                CollectionAssert.AllItemsAreUnique(b.life, b.id);
                CollectionAssert.IsSubsetOf(b.screen, TasteQuiz.ScreenVocab, b.id + " screen");
                CollectionAssert.AllItemsAreUnique(b.screen, b.id);
                foreach (var kv in b.fieldNotes)
                {
                    CollectionAssert.Contains(b.fields, kv.Key, $"{b.id}: a note for a field it doesn't list");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value), b.id);
                }
            }
        }

        [Test]
        public void Quotes_HaveTextAndAuthor()
        {
            var quotes = Load<QuoteCollection>("quotes.json").quotes;
            Assert.IsNotEmpty(quotes);
            Assert.IsTrue(quotes.All(q => !string.IsNullOrWhiteSpace(q.text) && !string.IsNullOrWhiteSpace(q.author)));
        }

        [Test]
        public void EndToEnd_ReadingFirstChapterOfEveryBookOffersItsQuiz()
        {
            var cat = Load<Catalog>("books.json");
            foreach (var b in cat.books)
            {
                var s = new AppState();
                s.profile.createdOn = new System.DateTime(2026, 11, 2);
                ReadingService.SetCurrentBook(s, b.id);
                var r = ReadingService.CheckIn(s, cat, b.chapters[0].endPage, s.profile.createdOn);
                Assert.IsTrue(r.Ok, b.id);
                Assert.AreEqual(b.chapters[0].HasQuiz ? 1 : (int?)null, r.PendingQuiz?.number, b.id);
            }
        }

        [Test]
        public void Tuning_JsonIsValidAndMatchesDefaults()
        {
            var t = Load<Tuning>("tuning.json");
            Assert.IsEmpty(t.Validate());
            var d = new Tuning();
            Assert.AreEqual(d.momentumLambda, t.momentumLambda);
            Assert.AreEqual(d.reminderWeeklyBudget, t.reminderWeeklyBudget);
            Assert.AreEqual(d.quizPassRatio, t.quizPassRatio, 1e-3);
        }

        [Test]
        public void Backend_JsonIsValidAndHasNoSecretKey()
        {
            var c = Load<BackendConfig>("backend.json");
            Assert.IsEmpty(c.Validate());
            StringAssert.StartsWith("sb_publishable_", c.publishableKey);
            StringAssert.StartsWith("https://", c.inviteBaseUrl);
            Assert.AreEqual(new System.DateTime(2026, 12, 7), c.fil.EndsOn);
        }
    }
}
