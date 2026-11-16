using System;
using System.Collections.Generic;
using System.Globalization;

namespace Margin.Core
{
    /// <summary>
    /// Where the shared backend lives and the links the app uses (Resources/Data/backend.json).
    /// Only the publishable key goes here: it is safe in a client because every table is protected by RLS.
    /// </summary>
    [Serializable]
    public class BackendConfig
    {
        public string supabaseUrl = "";
        public string publishableKey = "";
        /// <summary>Invite links point here, with <c>?add=username</c> (the web app opens the profile).</summary>
        public string inviteBaseUrl = "https://marginreads.vercel.app/app/";
        /// <summary>Edge function with real Gandhi prices.</summary>
        public string pricesFunction = "book-prices";
        /// <summary>How often friends and the feed are refreshed while a live screen is open.</summary>
        public int pollSeconds = 20;
        public int requestTimeoutSeconds = 15;
        public FilConfig fil = new FilConfig();

        public string RestUrl(string path) => supabaseUrl.TrimEnd('/') + "/rest/v1/" + path;
        public string AuthUrl(string path) => supabaseUrl.TrimEnd('/') + "/auth/v1/" + path;
        public string FunctionUrl(string name) => supabaseUrl.TrimEnd('/') + "/functions/v1/" + name;

        /// <summary>Problems that would stop the app from talking to the backend (empty = fine).</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (!(supabaseUrl ?? "").StartsWith("https://")) errors.Add("supabaseUrl must be an https:// URL.");
            if (string.IsNullOrWhiteSpace(publishableKey)) errors.Add("publishableKey is empty.");
            else if (publishableKey.StartsWith("sb_secret_")) errors.Add("Never ship a secret key in the app: use the publishable key.");
            if (pollSeconds < 5) errors.Add("pollSeconds must be at least 5.");
            if (requestTimeoutSeconds < 3) errors.Add("requestTimeoutSeconds must be at least 3.");
            if (fil == null) errors.Add("fil is missing.");
            else if (!DateTime.TryParseExact(fil.endsOn ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                errors.Add("fil.endsOn must be a date like 2026-12-07.");
            return errors;
        }
    }

    /// <summary>The FIL Guadalajara banner on Home.</summary>
    [Serializable]
    public class FilConfig
    {
        public string url = "https://www.fil.com.mx";
        /// <summary>Resources path of the banner image (no extension).</summary>
        public string image = "FIL/fil-2026";
        /// <summary>First day the banner is no longer shown (the fair ends the day before).</summary>
        public string endsOn = "2026-12-07";

        public DateTime EndsOn =>
            DateTime.TryParseExact(endsOn ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.MinValue;
    }
}
