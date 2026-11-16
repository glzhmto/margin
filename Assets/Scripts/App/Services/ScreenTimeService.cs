using System.Collections.Generic;
using UnityEngine;

namespace Margin.App.Services
{
    /// <summary>An app the user can silence in Focus mode. Its icon lives in Resources/AppIcons/{Id}.png.</summary>
    public class BlockedApp
    {
        public string Id;
        public string Name;
        public string Example;
        public string IconPath => "AppIcons/" + Id;
    }

    /// <summary>
    /// Silence apps during Focus mode — DEMO VERSION (simulated).
    /// The production version would use iOS Screen Time (FamilyControls + ManagedSettings),
    /// which needs an entitlement from Apple. Here the permission, picker and blocking are simulated
    /// so the full experience can be shown and tested.
    /// </summary>
    public class ScreenTimeService
    {
        const string AuthKey = "margin.st.authorized";
        const string SelKey = "margin.st.selection";

        public static readonly BlockedApp[] Apps =
        {
            new BlockedApp { Id = "instagram", Name = "Instagram", Example = "Reels, stories and feed" },
            new BlockedApp { Id = "tiktok", Name = "TikTok", Example = "For You feed" },
            new BlockedApp { Id = "youtube", Name = "YouTube", Example = "Shorts and videos" },
            new BlockedApp { Id = "whatsapp", Name = "WhatsApp", Example = "Chats and groups" },
            new BlockedApp { Id = "x", Name = "X", Example = "Timeline and trends" },
            new BlockedApp { Id = "snapchat", Name = "Snapchat", Example = "Snaps and stories" },
            new BlockedApp { Id = "netflix", Name = "Netflix", Example = "Shows and autoplay" },
            new BlockedApp { Id = "discord", Name = "Discord", Example = "Servers and DMs" },
        };

        readonly HashSet<string> _selected = new HashSet<string>();

        public ScreenTimeService()
        {
            var saved = PlayerPrefs.GetString(SelKey, "instagram,tiktok,youtube");
            foreach (var id in saved.Split(','))
                if (System.Array.Exists(Apps, a => a.Id == id)) _selected.Add(id); // ignores ids saved by older versions
        }

        public bool IsAuthorized => PlayerPrefs.GetInt(AuthKey, 0) == 1;
        public bool ShieldActive { get; private set; }
        public int SelectedCount => _selected.Count;
        public bool IsSelected(string id) => _selected.Contains(id);

        /// <summary>Simulates the Screen Time permission.</summary>
        public void Authorize()
        {
            PlayerPrefs.SetInt(AuthKey, 1);
            PlayerPrefs.Save();
        }

        public void Toggle(string id)
        {
            if (!_selected.Remove(id)) _selected.Add(id);
            PlayerPrefs.SetString(SelKey, string.Join(",", _selected));
            PlayerPrefs.Save();
        }

        public void SetShield(bool on)
        {
            ShieldActive = on && IsAuthorized && _selected.Count > 0;
            if (ShieldActive) Debug.Log($"[Margin] (Simulated) {_selected.Count} apps silenced");
        }

        public string Summary() =>
            SelectedCount == 0 ? "No apps chosen" :
            SelectedCount == 1 ? "1 app silenced" : $"{SelectedCount} apps silenced";
    }
}
