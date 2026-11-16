// "Reading momentum" algorithm: an exponential moving average of daily goal completion.
// Designed NOT to punish: planned rest days do not subtract, a missed day lowers momentum
// gradually (it does not wipe it out like a streak), and the cap avoids rewarding binges.
using System;

namespace Margin.Core
{
    public enum MomentumLevel
    {
        Kindling,
        Steady,
        Strong,
        Unstoppable
    }

    public struct MomentumSnapshot
    {
        public double Value;      // 0 .. 1.5
        public double Yesterday;  // value at the end of yesterday, to show the trend
        public MomentumLevel Level;

        public int Trend => Value > Yesterday + 1e-6 ? 1 : (Value < Yesterday - 1e-6 ? -1 : 0);
        /// <summary>Normalized 0..1 value for drawing the gauge.</summary>
        public double Normalized => Math.Min(1.0, Value);
    }

    public struct WeeklyProgress
    {
        public DateTime WeekStart; // Monday
        public int PagesRead;
        public int Target;
        public double Ratio => Target <= 0 ? 0 : Math.Min(1.0, (double)PagesRead / Target);
    }

    public static class HabitEngine
    {
        // Values live in Tuning (tuning.json); exposed here so the code reads like the formula.
        public static double Lambda => Tuning.Current.momentumLambda;
        public static double DailyCap => Tuning.Current.dailyCap;

        public static double DailyRatio(int pages, int dailyGoal)
        {
            if (dailyGoal <= 0) return pages > 0 ? 1.0 : 0.0;
            return Math.Min((double)pages / dailyGoal, DailyCap);
        }

        /// <summary>
        /// One step of the algorithm. M_t = λ·M_{t-1} + (1-λ)·r_t.
        /// With no reading on a planned rest day (or a day not over yet), momentum is kept.
        /// </summary>
        public static double Step(double previous, int pages, int dailyGoal, bool isReadingDay, bool dayIsOver)
        {
            if (pages <= 0 && (!isReadingDay || !dayIsOver)) return previous;
            return Lambda * previous + (1 - Lambda) * DailyRatio(pages, dailyGoal);
        }

        public static MomentumLevel LevelFor(double value)
        {
            var t = Tuning.Current;
            if (value < t.levelSteady) return MomentumLevel.Kindling;
            if (value < t.levelStrong) return MomentumLevel.Steady;
            if (value < t.levelUnstoppable) return MomentumLevel.Strong;
            return MomentumLevel.Unstoppable;
        }

        /// <summary>Recomputes momentum from the sign-up date through today (inclusive). Deterministic, no hidden state.</summary>
        public static MomentumSnapshot Compute(AppState state, DateTime today)
        {
            var goals = state.profile.goals;
            var start = state.profile.createdOn.Date;
            foreach (var l in state.logs)
                if (l.date.Date < start) start = l.date.Date;

            double m = 0, yesterday = 0;
            for (var d = start; d <= today.Date; d = d.AddDays(1))
            {
                bool isToday = d == today.Date;
                if (isToday) yesterday = m;
                m = Step(m, state.PagesOn(d), goals.dailyPages, goals.IsReadingDay(d), dayIsOver: !isToday);
            }

            return new MomentumSnapshot { Value = m, Yesterday = yesterday, Level = LevelFor(m) };
        }

        public static DateTime WeekStart(DateTime date)
        {
            int offset = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
            return date.Date.AddDays(-offset);
        }

        public static WeeklyProgress Weekly(AppState state, DateTime today)
        {
            var start = WeekStart(today);
            int pages = 0;
            for (int i = 0; i < 7; i++) pages += state.PagesOn(start.AddDays(i));
            return new WeeklyProgress { WeekStart = start, PagesRead = pages, Target = state.profile.goals.WeeklyTarget };
        }

        /// <summary>Guilt-free status message. Used on Home.</summary>
        public static string Headline(MomentumSnapshot s, bool readToday, bool isReadingDay)
        {
            if (readToday) return "You already read today. The rest can wait.";
            if (!isReadingDay) return "Today is your rest day. If a book calls you, go ahead.";
            switch (s.Level)
            {
                case MomentumLevel.Kindling: return "Every reader started with one page.";
                case MomentumLevel.Steady: return "You are building a rhythm. A few pages today keep it going.";
                case MomentumLevel.Strong: return "Reading has become part of your days.";
                default: return "You read like you breathe.";
            }
        }
    }
}
