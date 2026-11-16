// Plans reminders with a weekly budget that shrinks if the user ignores them.
// The opposite of the "Duolingo" pattern: few, at the time the user chose, never if they already read today.
using System;
using System.Collections.Generic;

namespace Margin.Core
{
    public static class ReminderPlanner
    {
        public static int WeeklyBudget => Tuning.Current.reminderWeeklyBudget;
        public static int IgnoredThreshold => Tuning.Current.reminderIgnoredThreshold;

        /// <summary>
        /// Ignored reminders in a row: past days with a scheduled reminder and no logged reading,
        /// counting back from the most recent.
        /// </summary>
        public static int IgnoredStreak(AppState state, DateTime today)
        {
            var past = state.remindersScheduled.FindAll(d => d.Date < today.Date);
            past.Sort();
            int streak = 0;
            for (int i = past.Count - 1; i >= 0; i--)
            {
                if (state.PagesOn(past[i]) > 0) break;
                streak++;
            }
            return streak;
        }

        public static int BudgetFor(int ignoredStreak) =>
            ignoredStreak >= IgnoredThreshold * 2 ? 1 :
            ignoredStreak >= IgnoredThreshold ? WeeklyBudget / 2 :
            WeeklyBudget;

        /// <summary>
        /// Reminder dates/times for the next 7 days (starting today if the time has not passed and they have not read).
        /// Spreads the budget across reading days, spacing them out.
        /// </summary>
        public static List<DateTime> PlanNext7Days(AppState state, DateTime now)
        {
            var goals = state.profile.goals;
            var result = new List<DateTime>();
            if (!goals.remindersEnabled || goals.readingDays.Count == 0) return result;

            var candidates = new List<DateTime>();
            for (int i = 0; i < 7; i++)
            {
                var day = now.Date.AddDays(i);
                if (!goals.IsReadingDay(day)) continue;
                var at = day.AddHours(goals.reminderHour).AddMinutes(goals.reminderMinute);
                if (at <= now) continue;
                if (i == 0 && state.PagesOn(day) > 0) continue; // already read today: stay quiet
                candidates.Add(at);
            }

            int budget = Math.Min(BudgetFor(IgnoredStreak(state, now)), candidates.Count);
            if (budget <= 0) return result;

            // Even spacing across candidates.
            double step = (double)candidates.Count / budget;
            for (int k = 0; k < budget; k++)
                result.Add(candidates[(int)Math.Floor(k * step)]);
            return result;
        }
    }
}
