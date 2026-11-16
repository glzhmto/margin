// The public part of a reader's diary: what friends see on their profile
// (current book and page, momentum, pages this week, books finished, favorites).
// The full diary stays private in reading_states; only this summary goes to profiles.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Margin.Core
{
    /// <summary>Columns of <c>profiles</c> that come from the reading state.</summary>
    [Serializable]
    public class ProfileSummary
    {
        public string current_book_id;
        public int? current_page;
        public string momentum_level;
        public int pages_this_week;
        public List<string> books_read = new List<string>();
        public List<string> favorites = new List<string>();

        /// <param name="showCurrentBook">The reader's privacy choice ("Show what I'm reading").</param>
        /// <param name="favorites">Favorite book ids (kept by the web version of the diary).</param>
        /// <param name="levelName">Turns the momentum level into the word shown to friends.</param>
        public static ProfileSummary From(AppState s, DateTime today, bool showCurrentBook, IEnumerable<string> favorites, Func<MomentumLevel, string> levelName)
        {
            var current = showCurrentBook ? s.profile.currentBookId : null;
            if (string.IsNullOrEmpty(current)) current = null;
            var progress = current == null ? null : s.progress.Find(p => p.bookId == current);
            return new ProfileSummary
            {
                current_book_id = current,
                current_page = current == null ? (int?)null : progress?.currentPage ?? 0,
                momentum_level = s.profile.onboardingComplete ? levelName(HabitEngine.Compute(s, today).Level) : null,
                pages_this_week = HabitEngine.Weekly(s, today).PagesRead,
                books_read = s.progress.Where(p => p.finished).Select(p => p.bookId).Distinct().ToList(),
                favorites = (favorites ?? Enumerable.Empty<string>()).Distinct().ToList(),
            };
        }
    }

    /// <summary>One side of a "Reading together" card: how far someone is in the shared book.</summary>
    public struct PactProgress
    {
        public double Ratio;
        public string Text;

        public static PactProgress Mine(AppState s, Book b)
        {
            var p = s.progress.Find(x => x.bookId == b.id);
            if (p != null && p.finished) return new PactProgress { Ratio = 1, Text = "Finished" };
            if (p == null || p.currentPage <= 0) return new PactProgress { Ratio = 0, Text = "Not started" };
            return new PactProgress { Ratio = Clamp((double)p.currentPage / b.pages), Text = $"Page {p.currentPage} of {b.pages}" };
        }

        public static PactProgress Theirs(Profile p, Book b)
        {
            if (p.books_read != null && p.books_read.Contains(b.id)) return new PactProgress { Ratio = 1, Text = "Finished" };
            if (p.current_book_id == b.id)
            {
                int page = p.current_page ?? 0;
                return new PactProgress { Ratio = Clamp((double)page / b.pages), Text = $"Page {page} of {b.pages}" };
            }
            return new PactProgress { Ratio = 0, Text = "Not started yet" };
        }

        static double Clamp(double r) => double.IsNaN(r) || r < 0 ? 0 : r > 1 ? 1 : r;
    }
}
