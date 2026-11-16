using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.App.Web
{
    /// <summary>
    /// The design tokens of preview/css/app.css, readable from C#.
    /// app.uss has the same values as custom properties; this copy is for the few places where a color
    /// must be set from code (inline styles, rich text, generated textures). Screens are rebuilt when the
    /// theme changes, so reading the current palette at build time is enough.
    /// </summary>
    public static class Theme
    {
        public static readonly Dictionary<string, string> Light = new Dictionary<string, string>
        {
            ["paper"] = "#F4EFE6", ["paper-2"] = "#ECE5D8", ["card"] = "#FBF8F2", ["ink"] = "#1C1A17", ["ink-2"] = "#5B554C", ["ink-3"] = "#716C63",
            ["rule"] = "#DDD4C4", ["accent"] = "#C23E29", ["accent-soft"] = "#F3DCD3", ["on-accent"] = "#FFFFFF", ["star"] = "#B97C10", ["ok"] = "#2F6B47", ["ok-soft"] = "#DCEBDF",
            ["error"] = "#9E2A1A", ["error-soft"] = "#F6DDD6", ["link"] = "#2E4A6B",
        };

        public static readonly Dictionary<string, string> Dark = new Dictionary<string, string>
        {
            ["paper"] = "#14120F", ["paper-2"] = "#2B2722", ["card"] = "#1D1A16", ["ink"] = "#EDE6D8", ["ink-2"] = "#B7AE9F", ["ink-3"] = "#8C8376",
            ["rule"] = "#34302A", ["accent"] = "#E0674F", ["accent-soft"] = "#3A221B", ["on-accent"] = "#14120F", ["star"] = "#E0A83A", ["ok"] = "#7FC29A", ["ok-soft"] = "#1E3326",
            ["error"] = "#F08A75", ["error-soft"] = "#3B231C", ["link"] = "#9DB7D5",
        };

        /// <summary>True while the app shows the dark palette (set by App.ApplySettings and Focus mode).</summary>
        public static bool IsDark => App.Root != null && App.Root.ClassListContains("theme-dark");

        public static Dictionary<string, string> Current => IsDark ? Dark : Light;

        /// <summary>
        /// "#C23E29", "rgba(20,18,15,.45)", "var(--accent)" or a token name ("accent") as a Color.
        /// Unknown values give magenta so mistakes are easy to spot.
        /// </summary>
        public static Color Color(string value)
        {
            var v = (value ?? "").Trim();
            if (v.StartsWith("var(--") && v.EndsWith(")")) v = v.Substring(6, v.Length - 7);
            if (Current.TryGetValue(v, out var hex)) v = hex;
            if (v == "transparent") return new Color(0, 0, 0, 0);
            if (v.StartsWith("rgba(") || v.StartsWith("rgb("))
            {
                var parts = v.Substring(v.IndexOf('(') + 1).TrimEnd(')').Split(',');
                float P(int i) => float.Parse(parts[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                return new Color(P(0) / 255f, P(1) / 255f, P(2) / 255f, parts.Length > 3 ? P(3) : 1f);
            }
            return ColorUtility.TryParseHtmlString(v, out var c) ? c : UnityEngine.Color.magenta;
        }

        /// <summary>"#RRGGBB" of a token in the current theme (for rich text tags).</summary>
        public static string Hex(string token)
        {
            var c = Color(token);
            return "#" + ColorUtility.ToHtmlStringRGBA(c);
        }
    }

    /// <summary>
    /// The six font files (Resources/Fonts). USS picks them through custom properties
    /// (--f400, --f500, --f600, --fi); these are for inline styles set from code.
    /// </summary>
    public static class Fonts
    {
        static readonly Dictionary<string, Font> Cache = new Dictionary<string, Font>();

        public static Font Get(string file)
        {
            if (!Cache.TryGetValue(file, out var f) || f == null)
            {
                f = Resources.Load<Font>("Fonts/" + file);
                if (f == null) Debug.LogWarning("[Margin] Missing font Resources/Fonts/" + file);
                Cache[file] = f;
            }
            return f;
        }

        public static Font Sans400 => Get("inter-400-normal");
        public static Font Sans500 => Get("inter-500-normal");
        public static Font Sans600 => Get("inter-600-normal");
        public static Font Serif400 => Get("eb-garamond-400-normal");
        public static Font SerifItalic => Get("eb-garamond-400-italic");
        public static Font Serif600 => Get("eb-garamond-600-normal");

        /// <summary>Font for a family and weight, like CSS font-family + font-weight (500 serif falls back to 400, as browsers do).</summary>
        public static Font For(bool serif, int weight = 400, bool italic = false)
        {
            if (serif) return italic ? SerifItalic : weight >= 600 ? Serif600 : Serif400;
            return weight >= 600 ? Sans600 : weight >= 500 ? Sans500 : Sans400;
        }

        /// <summary>
        /// Works around Unity bug UUM-121371 ("Font kerning breaks at runtime when multiple UI Documents use
        /// different fonts", fixed in 6000.0.61f1). In 6000.0.23f1, when glyphs are added to a dynamic font
        /// asset, its kerning is read later from a queue (FontAsset.UpdateFontFeaturesForFontAssetsInQueue)
        /// without loading that asset's own font face first, so it gets the pairs of whichever font the
        /// FontEngine loaded last (we use six files: Inter and EB Garamond). EB Garamond then receives Inter's
        /// kerning looked up with EB Garamond glyph numbers: for example the space (glyph 205) followed by
        /// "y" reads Inter's pair oslashacute + Ydieresis (-213 units), which swallows the whole space.
        /// That is why "What are you reading?" showed as "What areyoureading?".
        /// Turning off the kern feature (TextHandle.m_ActiveFontFeatures, internal) stops the corrupted
        /// pairs from being applied; text keeps the plain advances of each font. Remove after upgrading
        /// Unity to 6000.0.61f1 or newer.
        /// </summary>
        public static void DisableBrokenKerning()
        {
            if (!HasKerningBug(Application.unityVersion)) return;
            try
            {
                var type = System.Type.GetType("UnityEngine.TextCore.Text.TextHandle, UnityEngine.TextCoreTextEngineModule");
                var field = type?.GetField("m_ActiveFontFeatures",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (field?.GetValue(null) is System.Collections.IList features)
                {
                    features.Clear();
                    return;
                }
                Debug.LogWarning("[Margin] Could not turn off kerning (TextHandle.m_ActiveFontFeatures not found); spaces may be missing in serif text.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Margin] Could not turn off kerning: " + e.Message);
            }
        }

        /// <summary>True for Unity versions with UUM-121371 (2023.2 up to 6000.0.60).</summary>
        static bool HasKerningBug(string version)
        {
            // "6000.0.23f1" -> 6000, 0, 23
            var parts = (version ?? "").Split('.');
            if (parts.Length < 3 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor)) return true;
            int patch = 0;
            foreach (var ch in parts[2]) { if (ch < '0' || ch > '9') break; patch = patch * 10 + (ch - '0'); }
            if (major == 6000 && minor == 0) return patch < 61;
            if (major == 6000 && minor == 1) return true;
            if (major == 6000 && minor == 2) return true;
            return major < 6000 || (major == 6000 && minor == 3 && patch == 0);
        }

        /// <summary>Sets the font on one element (the web's inline fontFamily/fontWeight).</summary>
        public static T Set<T>(T el, bool serif, int weight = 400, bool italic = false) where T : VisualElement
        {
            el.style.unityFontDefinition = FontDefinition.FromFont(For(serif, weight, italic));
            // Inter has no italic file: the browser slants it, so we ask TextCore to do the same.
            el.style.unityFontStyleAndWeight = italic && !serif ? FontStyle.Italic : FontStyle.Normal;
            return el;
        }
    }

    /// <summary>
    /// Small generated textures for what USS can't draw: gradients, the striped rest day and the
    /// blurred hero behind a book. They are cached per color so rebuilding a screen is cheap.
    /// </summary>
    public static class Fx
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// A vertical gradient (top to bottom), like CSS linear-gradient(to bottom, ...).
        /// Stops are (position 0..1, color). Use it as a background image stretched over the element.
        /// </summary>
        public static Texture2D VerticalGradient(params (float at, Color color)[] stops)
        {
            var key = "v";
            foreach (var s in stops) key += "|" + s.at + ColorUtility.ToHtmlStringRGBA(s.color);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int h = 128;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                float t = 1f - (y + 0.5f) / h; // texture rows go bottom to top
                tex.SetPixel(0, y, Sample(stops, t));
            }
            tex.Apply();
            Cache[key] = tex;
            return tex;
        }

        static Color Sample((float at, Color color)[] stops, float t)
        {
            if (stops.Length == 0) return new Color(0, 0, 0, 0);
            if (t <= stops[0].at) return stops[0].color;
            for (int i = 1; i < stops.Length; i++)
                if (t <= stops[i].at)
                {
                    float k = Mathf.InverseLerp(stops[i - 1].at, stops[i].at, t);
                    return UnityEngine.Color.Lerp(stops[i - 1].color, stops[i].color, k);
                }
            return stops[stops.Length - 1].color;
        }

        /// <summary>Puts a gradient texture on an element, stretched to fill it.</summary>
        public static T Gradient<T>(T el, params (float at, Color color)[] stops) where T : VisualElement
        {
            el.style.backgroundImage = new StyleBackground(VerticalGradient(stops));
            el.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            return el;
        }

        /// <summary>Diagonal stripes (the rest day in the momentum week): 4 px of color, 4 px clear, at 135 degrees.</summary>
        public static Texture2D Stripes(Color color)
        {
            var key = "s" + ColorUtility.ToHtmlStringRGBA(color);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int n = 32; // tiles every 8 px at 4x
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var clear = new Color(color.r, color.g, color.b, 0);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, ((x + y) % 16) < 8 ? color : clear);
            tex.Apply();
            Cache[key] = tex;
            return tex;
        }

        /// <summary>
        /// A very small copy of a texture, drawn back at full size with bilinear filtering: it reads as a
        /// strong blur (the web's filter: blur(26px) behind the book cover). Works on non-readable textures.
        /// </summary>
        public static Texture Blurred(Texture source, int width = 10)
        {
            if (source == null) return null;
            var key = "b" + source.GetInstanceID() + "x" + width;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int height = Mathf.Max(1, Mathf.RoundToInt(width * (float)source.height / Mathf.Max(1, source.width)));
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Cache[key] = tex;
            return tex;
        }
    }
}
