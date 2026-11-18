// Margin data models. Plain C# (no UnityEngine) so they can be tested outside Unity.
// Note: Unity 6 compiles C# 9 without IsExternalInit, so we avoid records and init-only.
using System;
using System.Collections.Generic;

namespace Margin.Core
{
    // ---------- Content (loaded from Resources/Data/books.json) ----------

    [Serializable]
    public class Shelf
    {
        public string id;
        public string name;
        public string description;
    }

    [Serializable]
    public class QuizQuestion
    {
        public string prompt;
        public List<string> options = new List<string>();
        public int correctIndex;
        public string explanation;
    }

    [Serializable]
    public class Quiz
    {
        public List<QuizQuestion> questions = new List<QuizQuestion>();
    }

    [Serializable]
    public class Chapter
    {
        public int number;
        public string title;
        public int startPage;
        public int endPage;
        public Quiz quiz;

        public bool HasQuiz => quiz != null && quiz.questions != null && quiz.questions.Count > 0;
    }

    [Serializable]
    public class Book
    {
        public string id;
        public string title;
        public string author;
        public int year;
        public int pages;
        public List<string> shelves = new List<string>();
        public List<string> tags = new List<string>();

        // Taste quiz metadata. Values come from the fixed vocabularies in TasteQuiz (MoodVocab, PaceVocab,
        // FieldVocab, LifeVocab, ScreenVocab); DataIntegrityTests rejects anything else.
        /// <summary>How the book tends to make readers feel.</summary>
        public List<string> moods = new List<string>();
        /// <summary>"fast", "medium" or "slow".</summary>
        public string pace;
        /// <summary>Study fields whose themes the book speaks to.</summary>
        public List<string> fields = new List<string>();
        /// <summary>Life situations at the center of the book.</summary>
        public List<string> life = new List<string>();
        /// <summary>Film and series genres a viewer could come from.</summary>
        public List<string> screen = new List<string>();
        /// <summary>Short phrase per field explaining the link ("the law against your conscience"). Only where accurate.</summary>
        public Dictionary<string, string> fieldNotes = new Dictionary<string, string>();

        public string synopsis;
        public List<Chapter> chapters = new List<Chapter>();

        /// <summary>Chapter that contains the given page (or null if out of range).</summary>
        public Chapter ChapterAt(int page)
        {
            foreach (var c in chapters)
                if (page >= c.startPage && page <= c.endPage) return c;
            return null;
        }

        public Chapter GetChapter(int number) => chapters.Find(c => c.number == number);
    }

    [Serializable]
    public class Catalog
    {
        public List<Shelf> shelves = new List<Shelf>();
        public List<Book> books = new List<Book>();

        public Book FindBook(string id) => books.Find(b => b.id == id);
        public Shelf FindShelf(string id) => shelves.Find(s => s.id == id);
        public List<Book> BooksInShelf(string shelfId) => books.FindAll(b => b.shelves.Contains(shelfId));
    }

    [Serializable]
    public class Quote
    {
        public string text;
        public string author;
        public string source;
    }

    [Serializable]
    public class QuoteCollection
    {
        public List<Quote> quotes = new List<Quote>();
    }

    // ---------- User state (saved as local JSON) ----------

    [Serializable]
    public class ReadingGoals
    {
        /// <summary>Pages per reading day.</summary>
        public int dailyPages = 20;

        /// <summary>Reading days (System.DayOfWeek as int: 0 = Sunday … 6 = Saturday). The rest are planned rest.</summary>
        public List<int> readingDays = new List<int> { 1, 2, 3, 4, 5 };

        public int reminderHour = 21;
        public int reminderMinute = 0;
        public bool remindersEnabled = true;
        public bool focusModeEnabled = true;
        public string favoriteShelfId;

        /// <summary>Implementation intention (Gollwitzer): "When ___, I will read ___".</summary>
        public string intention;

        public bool IsReadingDay(DateTime date) => readingDays.Contains((int)date.DayOfWeek);
        public int WeeklyTarget => dailyPages * readingDays.Count;
    }

    [Serializable]
    public class UserProfile
    {
        public string name;
        public ReadingGoals goals = new ReadingGoals();
        public string currentBookId;
        public bool onboardingComplete;
        public DateTime createdOn;
    }

    [Serializable]
    public class ReadingLog
    {
        public DateTime date;
        public string bookId;
        public int fromPage;
        public int toPage;
        public int Pages => Math.Max(0, toPage - fromPage);
    }

    [Serializable]
    public class BookProgress
    {
        public string bookId;
        public int currentPage;
        /// <summary>Pages in the user's edition (0 = the catalog's reference edition). See Edition.</summary>
        public int editionPages;
        public bool finished;
        /// <summary>User rating 1–5 (0 = not rated).</summary>
        public int rating;
    }

    [Serializable]
    public class QuizResult
    {
        public string bookId;
        public int chapterNumber;
        public int correct;
        public int total;
        public DateTime date;
    }

    [Serializable]
    public class Reflection
    {
        public string bookId;
        public int chapterNumber;
        public string text;
        public DateTime date;
        /// <summary>Always true: the app never generates user text. Shown as the "Written by a human" stamp.</summary>
        public bool humanWritten = true;
    }

    [Serializable]
    public class AppState
    {
        public int schemaVersion = 1;
        public UserProfile profile = new UserProfile();
        public List<ReadingLog> logs = new List<ReadingLog>();
        public List<BookProgress> progress = new List<BookProgress>();
        public List<QuizResult> quizResults = new List<QuizResult>();
        public List<Reflection> reflections = new List<Reflection>();
        /// <summary>Dates when a reminder was scheduled (to measure ignored reminders).</summary>
        public List<DateTime> remindersScheduled = new List<DateTime>();

        public BookProgress ProgressFor(string bookId)
        {
            var p = progress.Find(x => x.bookId == bookId);
            if (p == null)
            {
                p = new BookProgress { bookId = bookId };
                progress.Add(p);
            }
            return p;
        }

        /// <summary>Fixes inconsistent data on load (repeated or out-of-range days, duplicate progress).</summary>
        public void Normalize()
        {
            var g = profile.goals;
            var days = new SortedSet<int>();
            foreach (var d in g.readingDays) if (d >= 0 && d <= 6) days.Add(d);
            g.readingDays = new List<int>(days);
            var seen = new HashSet<string>();
            progress.RemoveAll(p => !seen.Add(p.bookId));
        }

        /// <summary>Pages in the user's edition of this book (does not create progress).</summary>
        public int PagesFor(Book book) => Edition.PagesOf(book, progress.Find(x => x.bookId == book.id));

        public bool HasQuizResult(string bookId, int chapter) =>
            quizResults.Exists(q => q.bookId == bookId && q.chapterNumber == chapter);

        public int PagesOn(DateTime date)
        {
            var d = date.Date;
            int total = 0;
            foreach (var l in logs)
                if (l.date.Date == d) total += l.Pages;
            return total;
        }
    }
}
