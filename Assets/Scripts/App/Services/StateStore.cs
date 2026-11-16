using System;
using System.IO;
using Margin.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace Margin.App.Services
{
    /// <summary>Local storage of the user's state as JSON. Everything lives on the device; there is no server.</summary>
    public class StateStore
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss",
            DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
            NullValueHandling = NullValueHandling.Include,
            // Without this, Newtonsoft APPENDS saved items to lists that already have default values
            // (e.g. readingDays = {1..5}) and the days got duplicated on every launch.
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        readonly string _path;

        public StateStore(string fileName = "margin-state.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public AppState Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var state = JsonConvert.DeserializeObject<AppState>(File.ReadAllText(_path), Settings) ?? new AppState();
                    state.Normalize(); // fixes states saved by the previous version
                    return state;
                }
            }
            catch (Exception e)
            {
                // Corrupt file: back it up and start fresh instead of blocking the app.
                Debug.LogWarning($"[Margin] Could not read the state: {e.Message}");
                TryBackupCorrupt();
            }
            return new AppState();
        }

        /// <summary>Atomic write: first to a temp file, then replace.</summary>
        public void Save(AppState state)
        {
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(state, Settings));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }

        public void Delete()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        void TryBackupCorrupt()
        {
            try { File.Copy(_path, _path + ".corrupt", overwrite: true); } catch { /* no backup */ }
        }
    }
}
