using System;
using Margin.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace Margin.App.Services
{
    /// <summary>Catalog of books, chapters, quizzes, quotes and settings (Resources/Data/*.json).</summary>
    public class ContentRepository
    {
        public Catalog Catalog { get; }
        public QuoteCollection Quotes { get; }
        /// <summary>Backend URL, publishable key and links (Data/backend.json).</summary>
        public BackendConfig Backend { get; }

        public ContentRepository()
        {
            Catalog = LoadJson<Catalog>("Data/books") ?? new Catalog();
            Quotes = LoadJson<QuoteCollection>("Data/quotes") ?? new QuoteCollection();

            Backend = LoadJson<BackendConfig>("Data/backend") ?? new BackendConfig();
            var backendErrors = Backend.Validate();
            if (backendErrors.Count > 0)
                Debug.LogError($"[Margin] backend.json: {string.Join(" ", backendErrors)} Accounts and friends won't work.");

            // Tunable values (momentum, reminders, quiz…). If anything is invalid, the defaults stay.
            var tuning = LoadJson<Tuning>("Data/tuning");
            if (tuning != null)
            {
                var errors = Tuning.Apply(tuning);
                if (errors.Count > 0)
                    Debug.LogWarning($"[Margin] tuning.json was not applied: {string.Join(" ", errors)}");
            }
        }

        /// <summary>Quote of the day: fixed per date, so it doesn't change every time you open the app.</summary>
        public Quote QuoteOfTheDay(DateTime date)
        {
            if (Quotes.quotes.Count == 0) return null;
            int index = (date.Year * 400 + date.DayOfYear) % Quotes.quotes.Count;
            return Quotes.quotes[index];
        }

        static T LoadJson<T>(string resourcePath) where T : class
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError($"[Margin] Missing Resources/{resourcePath}.json");
                return null;
            }
            try { return JsonConvert.DeserializeObject<T>(asset.text); }
            catch (Exception e)
            {
                Debug.LogError($"[Margin] Invalid JSON in {resourcePath}: {e.Message}");
                return null;
            }
        }
    }
}
