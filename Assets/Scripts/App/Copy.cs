using System;
using Margin.Core;

namespace Margin.App
{
    /// <summary>
    /// Shared wording used by more than one screen or service (most screen text lives next to its screen, as in the web).
    /// Tone: a firm manifesto, never guilt-tripping.
    /// </summary>
    public static class Copy
    {
        /// <summary>Each one completes "When I ...".</summary>
        public static readonly string[] IntentionSuggestions =
        {
            "finish dinner", "get on the subway", "get into bed", "have my first coffee",
        };

        /// <summary>
        /// "When I finish dinner". The intention is written to follow "When I", so a leading "I "
        /// typed by the user is dropped to avoid "When I I ...".
        /// </summary>
        public static string WhenI(string intention)
        {
            var s = (intention ?? "").Trim();
            if (s.StartsWith("I ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2).TrimStart();
            return "When I " + s;
        }

        public static string LevelName(MomentumLevel l)
        {
            switch (l)
            {
                case MomentumLevel.Kindling: return "Kindling";
                case MomentumLevel.Steady: return "Steady";
                case MomentumLevel.Strong: return "Strong";
                default: return "Unstoppable";
            }
        }

        public static readonly string[] ReflectionPrompts =
        {
            "What did this chapter leave you with?",
            "Which line would you underline, and why?",
            "Which character would you argue with?",
            "What would you tell a friend about what you read?",
        };

        // Notifications
        public static string ReminderTitle(UserProfile p) =>
            string.IsNullOrWhiteSpace(p.goals.intention) ? "Your book is waiting" : $"{WhenI(p.goals.intention)}…";
        public const string ReminderFallback = "A few pages today. That's all.";

        // Network and accounts
        public const string NetFail = "Couldn't reach Margin. Check your connection and try again.";
        public const string UsernameCheckFailed = "Couldn't check the name right now. Try again.";
        public static string UsernameTaken(string u) => $"@{u} is taken. Try another one.";
        public const string GuestProfileNotUpdated = "Your email is saved, but the profile didn't update. Try again.";

        // Friend notifications
        public static string WantsToBeFriend(string name) => $"{name} wants to be your friend";
        public static string AcceptedYou(string name) => $"{name} accepted your request";

        public static readonly string[] DayLetters = { "S", "M", "T", "W", "T", "F", "S" }; // index = DayOfWeek (0 = Sunday)
    }
}
