// Reading check-in with error prevention and detection of finished chapters.
using System;
using System.Collections.Generic;

namespace Margin.Core
{
    public enum CheckInError
    {
        None,
        NoBookSelected,
        UnknownBook,
        PageNotAhead,      // the new page does not move forward
        PageBeyondBook,    // the page is past the book's total
        TooManyPagesToday  // more than is humanly reasonable in a day (likely a typo)
    }

    public class CheckInResult
    {
        public CheckInError Error;
        public int PagesAdded;
        public bool FinishedBook;
        public List<Chapter> ChaptersFinished = new List<Chapter>();
        /// <summary>First chapter that (by the page estimate) was finished in this check-in and has a pending quiz (or null).</summary>
        public Chapter PendingQuiz;
        public bool Ok => Error == CheckInError.None;
    }

    public static class ReadingService
    {
        public static int MaxPagesPerCheckIn => Tuning.Current.maxPagesPerCheckIn;

        public static string ErrorMessage(CheckInError e)
        {
            switch (e)
            {
                case CheckInError.NoBookSelected: return "First choose the book you are reading.";
                case CheckInError.UnknownBook: return "We could not find that book in the catalog.";
                case CheckInError.PageNotAhead: return "That page is before where you were. Enter the page where you stopped today.";
                case CheckInError.PageBeyondBook: return "That number is past the book's last page.";
                case CheckInError.TooManyPagesToday: return "That is a lot of pages for one check-in. Can you check the number?";
                default: return null;
            }
        }

        /// <summary>Validates without changing state (for feedback while the user types).</summary>
        public static CheckInError Validate(AppState state, Catalog catalog, int newPage)
        {
            var bookId = state.profile.currentBookId;
            if (string.IsNullOrEmpty(bookId)) return CheckInError.NoBookSelected;
            var book = catalog.FindBook(bookId);
            if (book == null) return CheckInError.UnknownBook;
            int current = state.ProgressFor(bookId).currentPage;
            if (newPage <= current) return CheckInError.PageNotAhead;
            if (newPage > state.PagesFor(book)) return CheckInError.PageBeyondBook;
            if (newPage - current > MaxPagesPerCheckIn) return CheckInError.TooManyPagesToday;
            return CheckInError.None;
        }

        /// <summary>Records that the user reached <paramref name="newPage"/> today.</summary>
        public static CheckInResult CheckIn(AppState state, Catalog catalog, int newPage, DateTime today)
        {
            var result = new CheckInResult { Error = Validate(state, catalog, newPage) };
            if (!result.Ok) return result;

            var book = catalog.FindBook(state.profile.currentBookId);
            var progress = state.ProgressFor(book.id);
            int from = progress.currentPage;
            int pages = Edition.PagesOf(book, progress);

            state.logs.Add(new ReadingLog { date = today.Date, bookId = book.id, fromPage = from, toPage = newPage });
            progress.currentPage = newPage;
            result.PagesAdded = newPage - from;

            // Chapter bounds are scaled to the user's edition. Since this is an estimate,
            // PendingQuiz is a suggestion: the UI asks whether the chapter was really finished.
            foreach (var c in book.chapters)
            {
                int end = Edition.ChapterEnd(book, c, pages);
                if (end > from && end <= newPage)
                {
                    result.ChaptersFinished.Add(c);
                    if (result.PendingQuiz == null && c.HasQuiz && !state.HasQuizResult(book.id, c.number))
                        result.PendingQuiz = c;
                }
            }

            if (newPage >= pages)
            {
                progress.finished = true;
                result.FinishedBook = true;
            }
            return result;
        }

        /// <summary>Saves how many pages the user's edition has. Returns the error or null.</summary>
        public static string SetEditionPages(AppState state, Book book, int pages)
        {
            var err = Edition.ValidatePages(book, pages);
            if (err != null) return err;
            var progress = state.ProgressFor(book.id);
            progress.editionPages = pages == book.pages ? 0 : pages;
            if (progress.currentPage > pages) progress.currentPage = pages;
            return null;
        }

        /// <summary>Changes the current book (resumes at the saved page if already started).</summary>
        public static void SetCurrentBook(AppState state, string bookId)
        {
            state.profile.currentBookId = bookId;
            state.ProgressFor(bookId);
        }
    }
}
