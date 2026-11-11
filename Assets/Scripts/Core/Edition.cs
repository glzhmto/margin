// Editions: each publisher paginates differently. The catalog stores chapters with the pages
// of a reference edition (book.pages). If the user says their edition has a different total,
// each chapter's bounds are scaled in proportion. It is an estimate, so the app asks
// "Did you finish the chapter?" before opening a quiz instead of assuming it.
using System;

namespace Margin.Core
{
    public static class Edition
    {
        /// <summary>Accepted range relative to the reference edition (by default, half to double).</summary>
        public static double MinRatio => Tuning.Current.editionMinRatio;
        public static double MaxRatio => Tuning.Current.editionMaxRatio;

        /// <summary>Total pages of the user's edition (or the reference one if not set).</summary>
        public static int PagesOf(Book book, BookProgress progress) =>
            progress != null && progress.editionPages > 0 ? progress.editionPages : book.pages;

        /// <summary>Page where the chapter ends in an edition of <paramref name="pages"/> pages.</summary>
        public static int ChapterEnd(Book book, Chapter chapter, int pages)
        {
            if (pages == book.pages || book.pages <= 0) return chapter.endPage;
            if (chapter.endPage >= book.pages) return pages; // the last chapter closes the book
            return Math.Max(1, (int)Math.Round(chapter.endPage * (double)pages / book.pages));
        }

        public static int ChapterStart(Book book, Chapter chapter, int pages)
        {
            if (pages == book.pages || book.pages <= 0) return chapter.startPage;
            if (chapter.startPage <= 1) return 1;
            return Math.Max(1, (int)Math.Round((chapter.startPage - 1) * (double)pages / book.pages) + 1);
        }

        /// <summary>Chapter that contains the given page in the user's edition (or null).</summary>
        public static Chapter ChapterAt(Book book, int page, int pages)
        {
            foreach (var c in book.chapters)
                if (page >= ChapterStart(book, c, pages) && page <= ChapterEnd(book, c, pages)) return c;
            return null;
        }

        /// <summary>Null if the total is reasonable; otherwise the reason (shown before saving).</summary>
        public static string ValidatePages(Book book, int pages)
        {
            int min = (int)Math.Ceiling(book.pages * MinRatio), max = (int)Math.Floor(book.pages * MaxRatio);
            if (pages < min || pages > max)
                return $"Check the number: editions of this book usually have between {min} and {max} pages.";
            return null;
        }
    }
}
