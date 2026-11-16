using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Margin.App.Services.Net
{
    /// <summary>
    /// JSON for the backend. Newtonsoft is already used by the app (StateStore, ContentRepository),
    /// so the network code uses it too: it handles nullable fields, nested rows and raw JSON
    /// (JObject) that JsonUtility can't. Field names in the row classes are the column names.
    /// </summary>
    public static class NetJson
    {
        /// <summary>For rows sent and received (timestamps become local time; nulls are not sent).</summary>
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Local,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        /// <summary>For raw JSON kept as-is (the private diary): dates stay strings, exactly as the web wrote them.</summary>
        public static readonly JsonSerializerSettings Raw = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
        };

        public static string Write(object value) => JsonConvert.SerializeObject(value, Settings);

        /// <summary>Parses a row (or rows). Returns default instead of throwing on bad input.</summary>
        public static T Read<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default(T);
            try { return JsonConvert.DeserializeObject<T>(json, Settings); }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Margin] Unexpected JSON from the server: {e.Message}");
                return default(T);
            }
        }

        /// <summary>Parses without touching date strings.</summary>
        public static JToken ReadRaw(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonConvert.DeserializeObject<JToken>(json, Raw); }
            catch (Exception) { return null; }
        }

        /// <summary>A row as a JObject, ready to send (nulls dropped, like <see cref="Write"/>).</summary>
        public static JObject ToObject(object value) => JObject.FromObject(value, JsonSerializer.Create(Settings));

        /// <summary>A row as a JObject that keeps null fields (to clear a column, e.g. no current book).</summary>
        public static JObject ToObjectWithNulls(object value) =>
            JObject.FromObject(value, JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include }));
    }
}
