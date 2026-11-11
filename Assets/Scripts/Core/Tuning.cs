// The app's tunable values in one place. Loaded from Resources/Data/tuning.json,
// so behavior (e.g. how fast momentum drops or how many reminders there are) can change
// without touching code. If the file is missing or invalid, these defaults are used;
// they are the ones justified in DesignJustification.md.
using System;
using System.Collections.Generic;

namespace Margin.Core
{
    [Serializable]
    public class Tuning
    {
        // Reading momentum (HabitEngine)
        public double momentumLambda = 0.85;   // weight of the past: 0.85 => a missed day drops ~15 %
        public double dailyCap = 1.5;          // daily cap, so binges are not rewarded
        public double levelSteady = 0.25;   // level thresholds: Kindling < Steady < Strong < Unstoppable
        public double levelStrong = 0.60;
        public double levelUnstoppable = 0.90;

        // Reminders (ReminderPlanner)
        public int reminderWeeklyBudget = 4;
        public int reminderIgnoredThreshold = 3; // after N ignored in a row the budget is halved

        // Check-ins and editions (ReadingService, Edition)
        public int maxPagesPerCheckIn = 400;
        public double editionMinRatio = 0.5;
        public double editionMaxRatio = 2.0;

        // Quiz (QuizEngine)
        public double quizPassRatio = 2.0 / 3.0; // 2 of 3 correct = "understood"

        // Recommender
        public double shelfWeight = 2.0;
        public double tagWeight = 1.0;
        public double seedShelfWeight = 0.5;
        public double ratingBoost = 0.5;
        public double lowRatingPenalty = 0.75;
        public double shortBookBonus = 1.0;
        public int shortBookPages = 150;

        static Tuning _current = new Tuning();

        /// <summary>Active settings. The app replaces them at startup with tuning.json.</summary>
        public static Tuning Current => _current;

        /// <summary>Applies settings if valid. Returns the errors (empty = applied).</summary>
        public static List<string> Apply(Tuning t)
        {
            var errors = t == null ? new List<string> { "Empty settings." } : t.Validate();
            if (errors.Count == 0) _current = t;
            return errors;
        }

        /// <summary>Restores the defaults (useful in tests).</summary>
        public static void Reset() => _current = new Tuning();

        public List<string> Validate()
        {
            var e = new List<string>();
            if (momentumLambda <= 0 || momentumLambda >= 1) e.Add("momentumLambda must be between 0 and 1.");
            if (dailyCap < 1) e.Add("dailyCap must be at least 1.");
            if (!(0 < levelSteady && levelSteady < levelStrong && levelStrong < levelUnstoppable))
                e.Add("Level thresholds must be increasing and greater than 0.");
            if (reminderWeeklyBudget < 0 || reminderWeeklyBudget > 7) e.Add("reminderWeeklyBudget must be between 0 and 7.");
            if (reminderIgnoredThreshold < 1) e.Add("reminderIgnoredThreshold must be at least 1.");
            if (maxPagesPerCheckIn < 1) e.Add("maxPagesPerCheckIn must be at least 1.");
            if (!(0 < editionMinRatio && editionMinRatio <= 1 && editionMaxRatio >= 1))
                e.Add("editionMinRatio must be in (0, 1] and editionMaxRatio at least 1.");
            if (quizPassRatio <= 0 || quizPassRatio > 1) e.Add("quizPassRatio must be in (0, 1].");
            if (shortBookPages < 1) e.Add("shortBookPages must be at least 1.");
            return e;
        }
    }
}
