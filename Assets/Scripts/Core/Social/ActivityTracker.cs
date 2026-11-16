// Turns what changed in the reading state into activity rows for friends and followers
// (started a book, logged pages, finished, took a quiz, joined). The reading code stays
// unaware of the social layer: the app only compares the state before and after a save.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Margin.Core
{
    /// <summary>The parts of the state that produce activity, captured at one moment.</summary>
    public class ActivitySnapshot
    {
        public int LogCount;
        public string CurrentBookId;
        public HashSet<string> Finished = new HashSet<string>();
        public HashSet<string> Quizzes = new HashSet<string>();
        public bool Onboarded;
    }

    public static class ActivityTracker
    {
        public static ActivitySnapshot Snapshot(AppState s)
        {
            return new ActivitySnapshot
            {
                LogCount = s.logs.Count,
                CurrentBookId = s.profile.currentBookId,
                Finished = new HashSet<string>(s.progress.Where(p => p.finished).Select(p => p.bookId)),
                Quizzes = new HashSet<string>(s.quizResults.Select(QuizKey)),
                Onboarded = s.profile.onboardingComplete,
            };
        }

        /// <summary>
        /// Rows for everything new in <paramref name="now"/> compared with <paramref name="before"/>.
        /// <c>user_id</c> is left empty; the caller fills it in.
        /// </summary>
        public static List<Activity> Diff(ActivitySnapshot before, AppState now)
        {
            var rows = new List<Activity>();
            if (before == null) return rows;
            var after = Snapshot(now);

            if (after.Onboarded && !before.Onboarded)
                rows.Add(new Activity { kind = ActivityKind.Joined });

            if (!string.IsNullOrEmpty(after.CurrentBookId) && after.CurrentBookId != before.CurrentBookId && !after.Finished.Contains(after.CurrentBookId))
                rows.Add(new Activity { kind = ActivityKind.Started, book_id = after.CurrentBookId });

            for (int i = before.LogCount; i < now.logs.Count; i++)
            {
                var l = now.logs[i];
                rows.Add(new Activity { kind = ActivityKind.Pages, book_id = l.bookId, data = new ActivityData { from = l.fromPage, to = l.toPage } });
            }

            foreach (var p in now.progress)
                if (p.finished && !before.Finished.Contains(p.bookId))
                    rows.Add(new Activity { kind = ActivityKind.Finished, book_id = p.bookId });

            foreach (var q in now.quizResults)
                if (!before.Quizzes.Contains(QuizKey(q)))
                    rows.Add(new Activity { kind = ActivityKind.Quiz, book_id = q.bookId, data = new ActivityData { chapter = q.chapterNumber, correct = q.correct, total = q.total } });

            return rows;
        }

        /// <summary>Same key as the web: book | chapter | day | score.</summary>
        static string QuizKey(QuizResult q) => q.bookId + "|" + q.chapterNumber + "|" + q.date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + q.correct;
    }
}
