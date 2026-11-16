using System;

namespace Margin.Core
{
    /// <summary>Builds a believable two-week history for demos and user tests.</summary>
    public static class DemoData
    {
        public static void Seed(AppState state, Catalog catalog, DateTime today, int days = 14)
        {
            var goals = state.profile.goals;
            var bookId = state.profile.currentBookId ?? (catalog.books.Count > 0 ? catalog.books[0].id : null);
            if (bookId == null) return;

            state.logs.Clear();
            state.quizResults.Clear();
            state.remindersScheduled.Clear();
            state.progress.RemoveAll(p => p.bookId == bookId);
            state.profile.createdOn = today.AddDays(-days);
            ReadingService.SetCurrentBook(state, bookId);

            // Realistic pattern: most reading days, one slip midweek, varying pace.
            var rng = new Random(451);
            for (int i = days; i >= 1; i--)
            {
                var day = today.AddDays(-i);
                if (!goals.IsReadingDay(day) || i == 6) continue;
                int pages = Math.Max(3, goals.dailyPages + rng.Next(-5, 8));
                var target = Math.Min(catalog.FindBook(bookId).pages - 1, state.ProgressFor(bookId).currentPage + pages);
                var r = ReadingService.CheckIn(state, catalog, target, day);
                if (r.PendingQuiz != null)
                    QuizEngine.Record(state, bookId, r.PendingQuiz.number,
                        new QuizScore { Correct = rng.Next(2, 4), Total = r.PendingQuiz.quiz.questions.Count }, day);
            }
        }
    }
}
