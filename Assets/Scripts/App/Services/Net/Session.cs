using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Margin.App.Services.Net
{
    /// <summary>
    /// The signed-in user's tokens, kept in PlayerPrefs so the app opens signed in.
    /// The access token lasts about an hour; <see cref="SupabaseClient"/> refreshes it before it expires.
    /// </summary>
    public class Session
    {
        const string Key = "margin.session";

        public string AccessToken;
        public string RefreshToken;
        /// <summary>Unix seconds when the access token expires.</summary>
        public long ExpiresAt;
        public string UserId;
        public string Email;
        public bool IsAnonymous;

        public bool ExpiresWithin(int seconds) => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + seconds >= ExpiresAt;

        /// <summary>Reads the session out of a GoTrue token response, or null if it has none.</summary>
        public static Session FromAuthResponse(JObject o)
        {
            var token = (string)o?["access_token"];
            if (string.IsNullOrEmpty(token)) return null;
            var user = o["user"] as JObject;
            long expiresAt = o["expires_at"] != null && o["expires_at"].Type == JTokenType.Integer
                ? (long)o["expires_at"]
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ((long?)o["expires_in"] ?? 3600);
            return new Session
            {
                AccessToken = token,
                RefreshToken = (string)o["refresh_token"],
                ExpiresAt = expiresAt,
                UserId = (string)user?["id"],
                Email = (string)user?["email"] ?? "",
                IsAnonymous = (bool?)user?["is_anonymous"] ?? false,
            };
        }

        public void Save()
        {
            var o = new JObject
            {
                ["access_token"] = AccessToken,
                ["refresh_token"] = RefreshToken,
                ["expires_at"] = ExpiresAt,
                ["user_id"] = UserId,
                ["email"] = Email,
                ["is_anonymous"] = IsAnonymous,
            };
            PlayerPrefs.SetString(Key, o.ToString(Newtonsoft.Json.Formatting.None));
            PlayerPrefs.Save();
        }

        public static Session Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var o = JObject.Parse(json);
                var s = new Session
                {
                    AccessToken = (string)o["access_token"],
                    RefreshToken = (string)o["refresh_token"],
                    ExpiresAt = (long?)o["expires_at"] ?? 0,
                    UserId = (string)o["user_id"],
                    Email = (string)o["email"] ?? "",
                    IsAnonymous = (bool?)o["is_anonymous"] ?? false,
                };
                return string.IsNullOrEmpty(s.RefreshToken) || string.IsNullOrEmpty(s.UserId) ? null : s;
            }
            catch (Exception) { return null; }
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
