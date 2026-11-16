using System;
using System.Collections.Generic;
using System.Globalization;
using Margin.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Margin.App.Services
{
    /// <summary>
    /// Converts the diary (AppState) to and from the JSON stored in <c>reading_states.state</c>,
    /// in the exact shape the web version reads and writes:
    /// <list type="bullet">
    /// <item>Dates are "yyyy-MM-dd" (with "THH:mm" only when there is a time), like the web's D.key/D.stamp.</item>
    /// <item>Fields only the web knows (lists, favorites, notifications, a finishedOn per book…) are kept:
    /// the app's fields are merged on top of the last JSON we saw instead of replacing it.</item>
    /// </list>
    /// </summary>
    public static class RemoteStateCodec
    {
        /// <summary>Changes only the phone makes for itself; they never make the diary "unsynced".</summary>
        static readonly string[] DeviceOnlyKeys = { "remindersScheduled" };

        static readonly JsonSerializer Writer = JsonSerializer.Create(new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            Converters = { new WebDateConverter() },
        });

        static readonly JsonSerializer Reader = JsonSerializer.Create(new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,       // web writes null for "not set" (e.g. createdOn)
            ObjectCreationHandling = ObjectCreationHandling.Replace, // don't append to default lists (readingDays)
            MissingMemberHandling = MissingMemberHandling.Ignore,
            // One odd value written by another client must not lose the whole diary.
            Error = (sender, args) => args.ErrorContext.Handled = true,
        });

        /// <summary>The diary as remote JSON, merged over <paramref name="previous"/> (may be null).</summary>
        public static JObject ToRemote(AppState state, JObject previous)
        {
            var mine = JObject.FromObject(state, Writer);
            return previous == null ? mine : (JObject)Merge(previous, mine);
        }

        /// <summary>The diary inside remote JSON, or null if there is none yet (new account).</summary>
        public static AppState FromRemote(JObject remote)
        {
            if (remote == null || !(remote["profile"] is JObject)) return null;
            var s = remote.ToObject<AppState>(Reader) ?? new AppState();
            if (s.profile == null) s.profile = new UserProfile();
            if (s.profile.goals == null) s.profile.goals = new ReadingGoals();
            if (s.logs == null) s.logs = new List<ReadingLog>();
            if (s.progress == null) s.progress = new List<BookProgress>();
            if (s.quizResults == null) s.quizResults = new List<QuizResult>();
            if (s.reflections == null) s.reflections = new List<Reflection>();
            if (s.remindersScheduled == null) s.remindersScheduled = new List<DateTime>();
            s.Normalize();
            return s;
        }

        /// <summary>
        /// The server JSON as this app would write it back (its own fields filled in, the web's kept).
        /// Comparing canonical forms means a diary saved by the web doesn't look "changed" here
        /// only because the app adds a few fields of its own. Null if there is no diary.
        /// </summary>
        public static JObject Canonical(JObject remote)
        {
            var diary = FromRemote(remote);
            return diary == null ? null : ToRemote(diary, remote);
        }

        /// <summary>True if both describe the same diary (ignoring key order and device-only fields).</summary>
        public static bool SameDiary(JObject a, JObject b)
        {
            if (a == null || b == null) return a == b;
            return JToken.DeepEquals(WithoutDeviceKeys(a), WithoutDeviceKeys(b));
        }

        /// <summary>Favorite book ids (the web keeps them in the diary; the app shows them on profiles).</summary>
        public static List<string> Favorites(JObject remote)
        {
            var list = new List<string>();
            if (remote?["favorites"] is JArray a)
                foreach (var t in a) if (t.Type == JTokenType.String) list.Add((string)t);
            return list;
        }

        static JObject WithoutDeviceKeys(JObject o)
        {
            var copy = (JObject)o.DeepClone();
            foreach (var k in DeviceOnlyKeys) copy.Remove(k);
            return copy;
        }

        /// <summary>Our values win; keys and list items only the other side has are kept.</summary>
        static JToken Merge(JToken previous, JToken mine)
        {
            if (mine is JObject mo && previous is JObject po)
            {
                var result = (JObject)po.DeepClone();
                foreach (var p in mo.Properties())
                {
                    // The web rates in half stars (3.5); BookProgress.rating holds whole stars and reads 3.5 as 4
                    // (Newtonsoft rounds to even). Keep the web's value when it rounds to ours.
                    if (p.Name == "rating" && IsHalfStarOf(po[p.Name], p.Value)) continue;
                    result[p.Name] = Merge(po[p.Name], p.Value);
                }
                return result;
            }
            if (mine is JArray ma && previous is JArray pa)
            {
                // Lists are append-only on both sides (logs, progress…), so item i is the same item
                // when it refers to the same book. Extra fields the web put on it are kept.
                var result = new JArray();
                for (int i = 0; i < ma.Count; i++)
                    result.Add(i < pa.Count && SameItem(pa[i], ma[i]) ? Merge(pa[i], ma[i]) : ma[i].DeepClone());
                return result;
            }
            return mine == null ? JValue.CreateNull() : mine.DeepClone();
        }

        static bool IsHalfStarOf(JToken previous, JToken mine)
        {
            if (previous == null || previous.Type != JTokenType.Float || mine == null || mine.Type != JTokenType.Integer) return false;
            double prev = (double)previous;
            return Math.Abs(prev % 1) > 1e-9 && (long)Math.Round(prev) == (long)mine;
        }

        static bool SameItem(JToken a, JToken b) =>
            a is JObject oa && b is JObject ob && JToken.DeepEquals(oa["bookId"], ob["bookId"]);

        /// <summary>Writes dates like the web: "2026-11-13", or "2026-11-13T21:30" when there is a time.</summary>
        class WebDateConverter : JsonConverter
        {
            public override bool CanConvert(Type t) => t == typeof(DateTime) || t == typeof(DateTime?);
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (!(value is DateTime d) || d == DateTime.MinValue) { writer.WriteNull(); return; }
                writer.WriteValue(d.TimeOfDay == TimeSpan.Zero
                    ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : d.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture));
            }

            public override object ReadJson(JsonReader reader, Type t, object existing, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }
    }
}
