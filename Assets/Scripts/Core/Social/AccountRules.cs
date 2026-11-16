// Rules for accounts: what a valid username, email and password look like, how a reader's
// name is shown, and how server errors are turned into plain sentences. Same rules as the web.
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Margin.Core
{
    public static class AccountRules
    {
        public const int UsernameMin = 3;
        public const int UsernameMax = 16;
        public const int PasswordMin = 8;
        /// <summary>Usernames of deleted accounts start with this; nobody can pick one.</summary>
        public const string DeletedPrefix = "deleted_";

        static readonly Regex UsernameChars = new Regex("^[a-z0-9._]+$");
        static readonly Regex EmailShape = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$");

        /// <summary>What the user typed, as a username: lowercase, no spaces, no leading @.</summary>
        public static string NormalizeUsername(string input)
        {
            var s = (input ?? "").Trim().ToLowerInvariant();
            if (s.StartsWith("@")) s = s.Substring(1);
            var sb = new StringBuilder(s.Length);
            foreach (var c in s) if (!char.IsWhiteSpace(c)) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>Null if the username is valid, otherwise the reason in plain words.</summary>
        public static string ValidateUsername(string u)
        {
            u = u ?? "";
            if (u.Length < UsernameMin) return $"At least {UsernameMin} characters.";
            if (u.Length > UsernameMax) return $"{UsernameMax} characters at most.";
            if (!UsernameChars.IsMatch(u)) return "Only lowercase letters, numbers, dots and underscores.";
            if (u.StartsWith(DeletedPrefix)) return "Pick another name.";
            return null;
        }

        /// <summary>A suggestion when a username is taken: "ana" → "ana26".</summary>
        public static string SuggestAlternative(string taken, DateTime today)
        {
            var suffix = (today.Year % 100).ToString("00");
            var stem = taken ?? "";
            if (stem.Length + suffix.Length > UsernameMax) stem = stem.Substring(0, UsernameMax - suffix.Length);
            return stem + suffix;
        }

        public static string ValidateEmail(string email)
        {
            var e = (email ?? "").Trim();
            if (EmailShape.IsMatch(e)) return null;
            return e.Contains("@") ? "Check the email domain." : "The email is missing an @.";
        }

        public static string ValidatePassword(string p)
        {
            p = p ?? "";
            if (p.Length < PasswordMin) return $"At least {PasswordMin} characters.";
            foreach (var c in p) if (char.IsDigit(c)) return null;
            return "Add at least one number.";
        }

        /// <summary>0–4: length, digits, mixed case, symbols or a long password.</summary>
        public static int PasswordStrength(string p)
        {
            p = p ?? "";
            bool digit = false, upper = false, lower = false, symbol = false;
            foreach (var c in p)
            {
                if (char.IsDigit(c)) digit = true;
                else if (char.IsUpper(c)) upper = true;
                else if (char.IsLower(c)) lower = true;
                else symbol = true;
            }
            int s = 0;
            if (p.Length >= PasswordMin) s++;
            if (digit) s++;
            if (upper && lower) s++;
            if (symbol || p.Length >= 12) s++;
            return s;
        }

        public static string ValidateName(string name) =>
            (name ?? "").Trim().Length < 2 ? "Enter your name." : null;

        /// <summary>"Ana López", or "@ana" when there is no name.</summary>
        public static string DisplayName(Profile p)
        {
            if (p == null) return "Reader";
            var n = (p.name ?? "").Trim();
            return n.Length > 0 ? n : "@" + p.username;
        }

        /// <summary>"Ana" (first word of the display name, without @).</summary>
        public static string FirstName(Profile p)
        {
            var n = DisplayName(p).TrimStart('@');
            int space = n.IndexOf(' ');
            return space > 0 ? n.Substring(0, space) : n;
        }

        /// <summary>One letter for the avatar.</summary>
        public static string Initial(Profile p)
        {
            var n = DisplayName(p).TrimStart('@');
            return n.Length > 0 ? n.Substring(0, 1).ToUpperInvariant() : "?";
        }

        /// <summary>
        /// Removes the characters that have a meaning in PostgREST filters (and quotes),
        /// so user text can be put inside an <c>ilike</c> or <c>or=(...)</c> safely. Max 40 characters.
        /// </summary>
        public static string CleanQuery(string q)
        {
            var sb = new StringBuilder();
            foreach (var c in q ?? "") sb.Append("%,()*\\:\"'".IndexOf(c) >= 0 ? ' ' : c);
            var s = sb.ToString().Trim();
            return s.Length > 40 ? s.Substring(0, 40) : s;
        }

        /// <summary>A stable avatar color for a new username (same palette and hash as the web).</summary>
        public static string ColorFor(string username)
        {
            string[] tints = { "#3E5C76", "#8A5A44", "#2F6B47", "#6B4E71", "#7A3B3B", "#4A6B5D", "#B5893B" };
            uint x = 2166136261; // FNV-1a, like hash() in preview/js/data.js
            foreach (var c in username ?? "") { x ^= c; x = unchecked(x * 16777619); }
            return tints[(int)(x % (uint)tints.Length)];
        }

        /// <summary>Turns an auth error from the server into something a person can act on.</summary>
        public static string FriendlyAuthError(string errorCode, string message)
        {
            var code = (errorCode ?? "").ToLowerInvariant();
            var m = message ?? "";
            bool Has(string s) => m.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
            if (code == "user_already_exists" || code == "email_exists" || Has("already registered") || Has("already exists"))
                return "There is already an account with that email. Log in instead.";
            if (code == "invalid_credentials" || Has("invalid login credentials"))
                return "Wrong email or password. Check capital letters and try again.";
            if (code == "email_not_confirmed" || Has("email not confirmed"))
                return "Confirm your email first: open the link we sent you, then log in.";
            if (code == "anonymous_provider_disabled" || Has("anonymous"))
                return "Guest mode is switched off right now. Sign up with email instead, it takes a minute.";
            if (code.StartsWith("over_") || Has("rate limit") || Has("too many"))
                return "Too many attempts for now. Wait a few minutes and try again.";
            if (code == "weak_password" || Has("password")) return m.Length > 0 ? m : "Choose a stronger password.";
            return m.Length > 0 ? m : "Something went wrong. Try again.";
        }
    }
}
