using System;

namespace Margin.Core
{
    /// <summary>
    /// Invite links: <c>https://marginreads.vercel.app/app/?add=ana</c>. Opening one on the web lands on
    /// that reader's profile; in the app, pasting one in Find friends does the same.
    /// </summary>
    public static class InviteLink
    {
        public const string Parameter = "add";

        public static string Build(string baseUrl, string username)
        {
            var b = (baseUrl ?? "").Trim();
            var sep = b.Contains("?") ? "&" : "?";
            return $"{b}{sep}{Parameter}={Uri.EscapeDataString(username ?? "")}";
        }

        /// <summary>Reads the username out of an invite link. False if the text is not one.</summary>
        public static bool TryParse(string text, out string username)
        {
            username = null;
            var t = (text ?? "").Trim();
            int q = t.IndexOf('?');
            if (q < 0 || !t.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return false;
            var query = t.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (var pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0 || pair.Substring(0, eq) != Parameter) continue;
                var value = AccountRules.NormalizeUsername(Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' ')));
                if (AccountRules.ValidateUsername(value) != null) return false;
                username = value;
                return true;
            }
            return false;
        }
    }
}
