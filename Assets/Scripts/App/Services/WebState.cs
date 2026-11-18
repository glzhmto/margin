using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Margin.Core;
using Newtonsoft.Json.Linq;

namespace Margin.App.Services
{
    // Fields of reading_states.state that only the web wrote until now (preview/js/data.js normalizeState),
    // and the per-account settings of reading_states.settings (data.js defaultSettings).
    // AppState (Core) keeps the diary; these classes keep everything else so both clients share one shape.

    /// <summary>A private list of books (state.lists).</summary>
    [Serializable]
    public class WebList
    {
        public string id;
        public string owner;
        public string title;
        public string description;
        public List<string> books = new List<string>();
        public int likes;
        public int comments;
        /// <summary>"yyyy-MM-dd".</summary>
        public string created;
    }

    /// <summary>An entry of the in-app notifications list (state.notifications).</summary>
    [Serializable]
    public class WebNotification
    {
        /// <summary>"yyyy-MM-ddTHH:mm".</summary>
        public string at;
        /// <summary>"welcome", "reminder", "weekly"...</summary>
        public string kind;
        public string text;
    }

    /// <summary>A finished Focus mode session (state.focusSessions).</summary>
    [Serializable]
    public class FocusSession
    {
        public string date;
        public int minutes;
    }

    [Serializable]
    public class SocialLikes
    {
        public List<string> likedLists = new List<string>();
        public List<string> likedReviews = new List<string>();
    }

    /// <summary>What the web keeps per book on top of the app's BookProgress.</summary>
    public class ProgressExtra
    {
        /// <summary>"yyyy-MM-dd" the book was finished, or null.</summary>
        public string finishedOn;
        /// <summary>The rating with half stars (BookProgress.rating only holds whole stars).</summary>
        public double? rating;
    }

    /// <summary>
    /// The web-only part of the diary, as typed lists you can change in place (like the web mutates
    /// state.lists and then calls Data.save()). <see cref="AccountService.CommitExtras"/> writes it back
    /// into the JSON that is synced, so nothing the web wrote is lost.
    /// </summary>
    public class WebExtras
    {
        public List<WebList> lists = new List<WebList>();
        public SocialLikes social = new SocialLikes();
        public List<string> want = new List<string>();
        public List<string> dnf = new List<string>();
        public List<FocusSession> focusSessions = new List<FocusSession>();
        /// <summary>Kept as raw JSON (the web doesn't use it yet).</summary>
        public JArray reviews = new JArray();
        public List<string> favorites = new List<string>();
        public int yearGoal = 12;
        public List<WebNotification> notifications = new List<WebNotification>();
        public int nightLogs;
        /// <summary>Stamp of the last time the notifications were opened ("" = never).</summary>
        public string notificationsSeen;
        /// <summary>Taste quiz answers (state.tastes); null until the quiz is taken. Old accounts have none.</summary>
        public Tastes tastes;
        /// <summary>finishedOn and half-star ratings, by book id.</summary>
        public Dictionary<string, ProgressExtra> progress = new Dictionary<string, ProgressExtra>();

        public ProgressExtra For(string bookId)
        {
            if (!progress.TryGetValue(bookId, out var p)) progress[bookId] = p = new ProgressExtra();
            return p;
        }

        /// <summary>Reads the fields from the state JSON, with the web's defaults for missing ones.</summary>
        public static WebExtras From(JObject state)
        {
            var x = new WebExtras();
            if (state == null) return x;
            x.lists = Read(state["lists"], x.lists);
            x.social = Read(state["social"], x.social) ?? new SocialLikes();
            if (x.social.likedLists == null) x.social.likedLists = new List<string>();
            if (x.social.likedReviews == null) x.social.likedReviews = new List<string>();
            x.want = Read(state["want"], x.want);
            x.dnf = Read(state["dnf"], x.dnf);
            x.focusSessions = Read(state["focusSessions"], x.focusSessions);
            x.reviews = state["reviews"] as JArray ?? new JArray();
            x.favorites = Read(state["favorites"], x.favorites);
            x.yearGoal = state["yearGoal"] != null && state["yearGoal"].Type == JTokenType.Integer ? (int)state["yearGoal"] : 12;
            x.notifications = Read(state["notifications"], x.notifications);
            x.nightLogs = state["nightLogs"] != null && state["nightLogs"].Type == JTokenType.Integer ? (int)state["nightLogs"] : 0;
            x.notificationsSeen = state["notificationsSeen"]?.Type == JTokenType.String ? (string)state["notificationsSeen"] : null;
            x.tastes = state["tastes"] is JObject tj ? TasteQuiz.Clean(Read<Tastes>(tj, null)) : null;
            if (state["progress"] is JArray pa)
                foreach (var t in pa.OfType<JObject>())
                {
                    var id = (string)t["bookId"];
                    if (string.IsNullOrEmpty(id)) continue;
                    var e = new ProgressExtra { finishedOn = t["finishedOn"]?.Type == JTokenType.String ? (string)t["finishedOn"] : null };
                    if (t["rating"] != null && (t["rating"].Type == JTokenType.Float || t["rating"].Type == JTokenType.Integer)) e.rating = (double)t["rating"];
                    x.progress[id] = e;
                }
            return x;
        }

        /// <summary>Writes the fields into the state JSON (its progress items must already exist).</summary>
        // Null fields are left out, like the web's JSON (a list without a description has no "description" key).
        static readonly Newtonsoft.Json.JsonSerializer NoNulls = Newtonsoft.Json.JsonSerializer.Create(
            new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });

        public void WriteTo(JObject state)
        {
            state["lists"] = JArray.FromObject(lists ?? new List<WebList>(), NoNulls);
            state["social"] = JObject.FromObject(social ?? new SocialLikes(), NoNulls);
            state["want"] = JArray.FromObject(want ?? new List<string>());
            state["dnf"] = JArray.FromObject(dnf ?? new List<string>());
            state["focusSessions"] = JArray.FromObject(focusSessions ?? new List<FocusSession>(), NoNulls);
            state["reviews"] = reviews ?? new JArray();
            state["favorites"] = JArray.FromObject(favorites ?? new List<string>());
            state["yearGoal"] = yearGoal;
            state["notifications"] = JArray.FromObject(notifications ?? new List<WebNotification>(), NoNulls);
            state["nightLogs"] = nightLogs;
            if (notificationsSeen != null) state["notificationsSeen"] = notificationsSeen;
            // Same shape as the web (TasteQuiz.clean): field, level, life, feel, screen, pace, length, era, region, liked (null fields included, like JSON.stringify).
            if (tastes != null) state["tastes"] = JObject.FromObject(TasteQuiz.Clean(tastes));
            if (state["progress"] is JArray pa)
                foreach (var t in pa.OfType<JObject>())
                {
                    var id = (string)t["bookId"];
                    if (id == null || !progress.TryGetValue(id, out var e)) continue;
                    if (e.finishedOn != null) t["finishedOn"] = e.finishedOn;
                    if (e.rating.HasValue)
                        t["rating"] = Math.Abs(e.rating.Value % 1) > 1e-9 ? new JValue(e.rating.Value) : new JValue((long)Math.Round(e.rating.Value));
                }
        }

        static T Read<T>(JToken t, T fallback) where T : class
        {
            if (t == null || t.Type == JTokenType.Null) return fallback;
            try { return t.ToObject<T>() ?? fallback; }
            catch (Exception) { return fallback; }
        }
    }

    /// <summary>
    /// reading_states.settings, typed. Every setter marks the settings unsent; they go to the server
    /// with the next sync, exactly as the web's Net.flush sends { state, settings }.
    /// Keys the app doesn't know are kept.
    /// </summary>
    public class WebSettings
    {
        readonly JObject _o;
        readonly Action _changed;

        public WebSettings(JObject o, Action changed)
        {
            _o = o ?? Defaults();
            _changed = changed;
        }

        /// <summary>The raw JSON (sent as is).</summary>
        public JObject Json => _o;

        /// <summary>data.js defaultSettings().</summary>
        public static JObject Defaults() => new JObject
        {
            ["publicProfile"] = true,
            ["activityVisibility"] = "Friends and followers",
            ["showCurrent"] = true,
            ["quotesInReminders"] = true,
            ["weeklySummary"] = true,
            ["focusMinutes"] = 25,
            ["theme"] = "System",
            ["textLarge"] = false,
            ["reduceMotion"] = false,
            ["countRereads"] = true,
            ["member"] = false,
            ["spoilers"] = true,
        };

        /// <summary>{ ...defaultSettings(), ...saved }.</summary>
        public static JObject Normalize(JObject saved)
        {
            var o = Defaults();
            if (saved != null) foreach (var p in saved.Properties()) o[p.Name] = p.Value.DeepClone();
            return o;
        }

        public bool GetBool(string key, bool def = false) => _o[key] != null && _o[key].Type == JTokenType.Boolean ? (bool)_o[key] : def;
        public string GetString(string key, string def = null) => _o[key] != null && _o[key].Type == JTokenType.String ? (string)_o[key] : def;
        public int GetInt(string key, int def = 0) =>
            _o[key] != null && (_o[key].Type == JTokenType.Integer || _o[key].Type == JTokenType.Float) ? (int)Math.Round((double)_o[key], MidpointRounding.AwayFromZero) : def;

        public void Set(string key, JToken value)
        {
            if (JToken.DeepEquals(_o[key], value)) return;
            _o[key] = value;
            _changed?.Invoke();
        }

        public bool PublicProfile { get => GetBool("publicProfile", true); set => Set("publicProfile", value); }
        /// <summary>"Friends and followers" or "Only me" (no activity rows are written).</summary>
        public string ActivityVisibility { get => GetString("activityVisibility", "Friends and followers"); set => Set("activityVisibility", value); }
        public bool ShowCurrent { get => GetBool("showCurrent", true); set => Set("showCurrent", value); }
        public bool QuotesInReminders { get => GetBool("quotesInReminders", true); set => Set("quotesInReminders", value); }
        public bool WeeklySummary { get => GetBool("weeklySummary", true); set => Set("weeklySummary", value); }
        public int FocusMinutes { get => GetInt("focusMinutes", 25); set => Set("focusMinutes", value); }
        /// <summary>"System", "Light" or "Dark".</summary>
        public string Theme { get => GetString("theme", "System"); set => Set("theme", value); }
        public bool TextLarge { get => GetBool("textLarge"); set => Set("textLarge", value); }
        public bool ReduceMotion { get => GetBool("reduceMotion"); set => Set("reduceMotion", value); }
        public bool CountRereads { get => GetBool("countRereads", true); set => Set("countRereads", value); }
        public bool Member { get => GetBool("member"); set => Set("member", value); }
        /// <summary>"annual" or "monthly".</summary>
        public string MemberPlan { get => GetString("memberPlan"); set => Set("memberPlan", value); }
        public bool Spoilers { get => GetBool("spoilers", true); set => Set("spoilers", value); }
        /// <summary>Screen Time permission granted (simulated, like the web).</summary>
        public bool ScreenTime { get => GetBool("screenTime"); set => Set("screenTime", value); }
        public bool SilencedSet { get => GetBool("silencedSet"); set => Set("silencedSet", value); }

        /// <summary>Apps silenced in Focus mode (ids of ScreenTimeService.Apps). Assign a new list to save.</summary>
        public List<string> Silenced
        {
            get => _o["silenced"] is JArray a ? a.Select(t => (string)t).Where(s => s != null).ToList() : new List<string> { "instagram", "tiktok", "youtube" };
            set => Set("silenced", JArray.FromObject(value ?? new List<string>()));
        }

        public override string ToString() => _o.ToString(Newtonsoft.Json.Formatting.None);

        internal static string Stamp(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
    }
}
