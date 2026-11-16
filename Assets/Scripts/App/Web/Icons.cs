using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.App.Web
{
    /// <summary>
    /// Lucide icons (ISC License) rendered to white PNGs in Resources/Icons (one per entry of ICONS in
    /// preview/js/ui.js, plus star-fill, heart-fill, plus-bold and check-bold).
    /// The PNG is tinted with -unity-background-image-tint-color. USS has no currentColor, so every
    /// color rule in app.uss also sets --fg, and .i uses var(--fg): the icon follows the text color
    /// around it, like the web's stroke: currentColor.
    /// </summary>
    public static class Icons
    {
        public static readonly string[] Names =
        {
            "home", "search", "plus", "diary", "user", "bell", "heart", "comment", "back", "chev", "down", "x", "check", "more",
            "gear", "play", "pause", "share", "pin", "moon", "sun", "lock", "eye", "eyeoff", "list", "bookopen", "mic", "mail",
            "timer", "scan", "shield", "download", "upload", "trash", "text", "star", "help", "doc", "link", "users", "logout",
            "calendar", "quote", "star-fill", "heart-fill", "plus-bold", "check-bold",
        };

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Texture(string name)
        {
            if (!Cache.TryGetValue(name, out var t) || t == null)
            {
                t = Resources.Load<Texture2D>("Icons/" + name);
                if (t == null) Debug.LogWarning("[Margin] Missing icon Resources/Icons/" + name);
                Cache[name] = t;
            }
            return t;
        }

        /// <summary>
        /// The web's icon(name, cls): an 18 px icon (class "i" plus cls) in the current text color.
        /// Size comes from USS (.iconbtn .i is 22 px, .tab .i 24 px...) or from <see cref="Size"/>.
        /// </summary>
        public static VisualElement Make(string name, string cls = "")
        {
            var e = new VisualElement { name = "icon-" + name, pickingMode = PickingMode.Ignore };
            e.AddToClassList("i");
            e.AddToClassList("t-svg");
            foreach (var c in (cls ?? "").Split(' ', '.'))
                if (c.Length > 0) e.AddToClassList(c);
            e.style.backgroundImage = new StyleBackground(Texture(name));
            return e;
        }

        /// <summary>Forces a size in px (the web's svg width/height attributes).</summary>
        public static VisualElement Size(this VisualElement icon, float px)
        {
            icon.style.width = px;
            icon.style.height = px;
            return icon;
        }

        /// <summary>Forces a color (when the color comes from an inline style, which can't set --fg).</summary>
        public static VisualElement Tint(this VisualElement icon, string color)
        {
            icon.style.unityBackgroundImageTintColor = Theme.Color(color);
            return icon;
        }

        /// <summary>Tints every icon inside <paramref name="root"/> that has no tint of its own.</summary>
        internal static void TintTree(VisualElement root, Color color)
        {
            root.Query<VisualElement>(className: "i").ForEach(i =>
            {
                if (i.style.unityBackgroundImageTintColor.keyword != StyleKeyword.Null) return;
                i.style.unityBackgroundImageTintColor = color;
            });
        }
    }
}
