using System;
using System.Collections.Generic;

namespace Margin.Core
{
    /// <summary>
    /// The demo account ("Try a demo account" on Welcome): six weeks of a believable reader, so someone who
    /// scans the QR at the pitch sees a full diary right away. Two finished books with ratings, 1984 in
    /// progress, quizzes after chapters, rest days and two missed days. Same plan as DemoData in preview/js/core.js.
    /// </summary>
    public static class DemoData
    {
        public const int Days = 42;

        /// <summary>The books in order: id, the day (counting back from today) it was started, rating when finished (0 = still reading).</summary>
        public static readonly (string id, int startDay, int rating)[] Plan =
        {
            ("the-little-prince", 42, 5),
            ("animal-farm", 33, 4),
            ("1984", 19, 0),
        };

        /// <summary>Days (counting back from today) the demo reader skipped, so momentum shows a dip.</summary>
        static readonly HashSet<int> Missed = new HashSet<int> { 26, 9 };

        /// <summary>Fills the diary. Returns the day each finished book was finished (for the app's finishedOn).</summary>
        public static Dictionary<string, DateTime> Seed(AppState state, Catalog catalog, DateTime today)
        {
            var finished = new Dictionary<string, DateTime>();
            var goals = state.profile.goals;
            goals.dailyPages = 15;
            goals.readingDays = new List<int> { 1, 2, 3, 4, 5, 6 };
            goals.intention = "after dinner";
            goals.remindersEnabled = false;
            state.logs.Clear();
            state.quizResults.Clear();
            state.progress.Clear();
            state.profile.createdOn = today.AddDays(-Days - 1);
            state.profile.onboardingComplete = true;

            int seed = 451; // small deterministic generator, the same numbers as the web
            int Rnd(int a, int b) { seed = (int)(seed * 48271L % 2147483647L); return a + seed % (b - a); } // Park-Miller: exact in JS too

            for (int k = 0; k < Plan.Length; k++)
            {
                var (id, start, rating) = Plan[k];
                var book = catalog.FindBook(id);
                if (book == null) continue;
                int end = k + 1 < Plan.Length ? Plan[k + 1].startDay + 1 : 1; // the next book starts the day after
                ReadingService.SetCurrentBook(state, id);
                for (int i = start; i >= end; i--)
                {
                    var day = today.AddDays(-i);
                    if (!goals.IsReadingDay(day) || Missed.Contains(i)) continue;
                    var p = state.ProgressFor(id);
                    bool last = rating > 0 && i == end;
                    int target = last ? book.pages : Math.Min(rating > 0 ? book.pages : book.pages - 1, p.currentPage + Rnd(9, 19));
                    if (target <= p.currentPage) continue;
                    var r = ReadingService.CheckIn(state, catalog, target, day);
                    if (r.PendingQuiz != null)
                        QuizEngine.Record(state, id, r.PendingQuiz.number,
                            new QuizScore { Correct = Rnd(2, 4), Total = r.PendingQuiz.quiz.questions.Count }, day);
                    if (r.FinishedBook) { finished[id] = day; break; }
                }
                if (rating > 0)
                {
                    var p = state.ProgressFor(id);
                    p.currentPage = book.pages; p.finished = true; p.rating = rating;
                    if (!finished.ContainsKey(id)) finished[id] = today.AddDays(-end);
                }
            }
            ReadingService.SetCurrentBook(state, Plan[Plan.Length - 1].id);
            return finished;
        }
    }
}
