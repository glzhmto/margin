using System;
using System.Collections.Generic;
using System.Linq;
using Margin.App.Web.Screens;
using Margin.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Margin.App.Web.H;

namespace Margin.App.Web
{
    /// <summary>Who an avatar shows: the web's { name, color } (pav(p), reviewer(r), Data.me()).</summary>
    public class AvatarUser
    {
        public string Name;
        public string Color;
        public string Id;
        /// <summary>Profile photo (JPEG data URL) or null.</summary>
        public string Photo;
        public AvatarUser() { }
        public AvatarUser(string name, string color, string id = null) { Name = name; Color = color; Id = id; }
    }

    /// <summary>One button of an iOS alert (the web's { label, do, bold, red }).</summary>
    public class AlertAction
    {
        public string Label;
        public Action Do;
        public bool Bold;
        public bool Red;
    }

    /// <summary>Options of <see cref="Components.Navbar"/> (the web's opts: right, onBack, transparent).</summary>
    public class NavOpts
    {
        public VisualElement Right;
        public Action OnBack;
        public bool Transparent;
    }

    /// <summary>
    /// The components of preview/js/ui.js, with the same names, plus the small helpers of net.js
    /// (pname, pfirst, pav, cleanQuery, netFail). Use them with
    /// <c>using static Margin.App.Web.Components;</c> and write Cover(b, 72), Sect("For you"), Toast("Saved").
    /// </summary>
    public static class Components
    {
        public static readonly string[] COVER_TINTS = { "#3E5C76", "#8A5A44", "#2F6B47", "#6B4E71", "#7A3B3B", "#4A6B5D", "#B5893B" };
        static readonly int[] AvatarSizes = { 20, 28, 36, 44, 72, 88 };
        static readonly int[] CoverWidths = { 44, 56, 72, 84, 96, 110, 128 };

        // ---------- Icons ----------

        /// <summary>icon(name, cls).</summary>
        public static VisualElement Icon(string name, string cls = "") => Icons.Make(name, cls);

        // ---------- Book cover ----------

        /// <summary>
        /// cover(book, w, { onclick, rank }): the real cover from Resources/Covers, or the colored type cover.
        /// Height follows the width at 2:3, also when the width is a percentage (cover(b, 84).W("100%")).
        /// </summary>
        public static VisualElement Cover(Book book, int w = 72, Action onClick = null, int rank = 0)
        {
            if (book == null) return new VisualElement();
            var c = new VisualElement { name = "cover-" + book.id };
            c.AddToClassList("cover");
            if (CoverWidths.Contains(w)) c.AddToClassList("cover-w" + w);
            else { c.style.width = w; c.style.height = w * 1.5f; }
            var tex = Data.CoverTexture(book.id);
            if (tex != null)
            {
                c.style.backgroundImage = new StyleBackground(tex);
                c.style.backgroundPositionX = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(UnityEngine.ScaleMode.ScaleAndCrop);
                c.style.backgroundPositionY = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(UnityEngine.ScaleMode.ScaleAndCrop);
                c.style.backgroundRepeat = BackgroundPropertyHelper.ConvertScaleModeToBackgroundRepeat(UnityEngine.ScaleMode.ScaleAndCrop);
                c.style.backgroundSize = BackgroundPropertyHelper.ConvertScaleModeToBackgroundSize(UnityEngine.ScaleMode.ScaleAndCrop);
            }
            else
            {
                c.AddToClassList("type");
                c.style.backgroundColor = Theme.Color(COVER_TINTS[book.id.Length % COVER_TINTS.Length]);
                c.Add(El("div.t", book.title));
                c.Add(El("div.a", book.author));
            }
            if (rank > 0) c.Add(El("span.rank", rank.ToString()));
            var edge = new VisualElement { pickingMode = PickingMode.Ignore };
            edge.AddToClassList("cover-edge");
            c.Add(edge);
            if (onClick != null) MakeClickable(c, onClick);
            else c.pickingMode = PickingMode.Ignore;
            Layout.AspectRatio(c, 1.5f);
            return c;
        }

        // ---------- Stars, avatar, section ----------

        /// <summary>stars(value, size): five stars with a partial fill (half stars and averages).</summary>
        public static VisualElement Stars(double value, float size = 12)
        {
            VisualElement Row(string cls)
            {
                var r = new VisualElement { pickingMode = PickingMode.Ignore };
                r.AddToClassList(cls);
                for (int i = 0; i < 5; i++)
                {
                    var s = Icons.Make("star-fill").Size(size);
                    if (i > 0) s.style.marginLeft = 1;
                    r.Add(s);
                }
                return r;
            }
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("stars");
            box.Add(Row("base"));
            var fill = Row("fill");
            fill.style.width = Length.Percent((float)(Math.Max(0, Math.Min(5, value)) / 5 * 100));
            box.Add(fill);
            box.tooltip = $"{value} out of 5 stars";
            return box;
        }

        /// <summary>avatar(user, size): colored circle with the initials.</summary>
        public static VisualElement Avatar(AvatarUser user, int size = 36)
        {
            var name = !string.IsNullOrWhiteSpace(user?.Name) ? user.Name : user?.Id ?? "?";
            var ini = string.Concat(name.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p.Substring(0, 1))).ToUpperInvariant();
            var a = El("div.avatar", ini);
            if (AvatarSizes.Contains(size)) a.AddToClassList("av" + size);
            else { a.style.width = size; a.style.height = size; a.style.fontSize = Mathf.Round(size * 0.36f); }
            a.style.backgroundColor = Theme.Color(string.IsNullOrEmpty(user?.Color) ? "#5B554C" : user.Color);
            a.pickingMode = PickingMode.Ignore;
            var tex = Photo(user?.Photo);
            if (tex != null) { a.Clear(); a.style.backgroundImage = new StyleBackground(tex); a.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop; }
            return a;
        }

        static readonly Dictionary<string, Texture2D> _photos = new Dictionary<string, Texture2D>();

        /// <summary>Decodes a profile photo data URL once and caches it.</summary>
        static Texture2D Photo(string dataUrl)
        {
            const string head = "data:image/jpeg;base64,";
            if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith(head)) return null;
            if (_photos.TryGetValue(dataUrl, out var t)) return t;
            try
            {
                t = new Texture2D(2, 2);
                if (!t.LoadImage(Convert.FromBase64String(dataUrl.Substring(head.Length)))) t = null;
            }
            catch (FormatException) { t = null; }
            return _photos[dataUrl] = t;
        }

        public static VisualElement Avatar(Profile p, int size = 36) => Avatar(Pav(p), size);
        public static VisualElement Avatar(MeInfo me, int size = 36) => Avatar(me == null ? null : new AvatarUser(me.Name, me.Color, me.Id) { Photo = me.Avatar }, size);

        /// <summary>sect(title, more, onMore): small caps title, a rule and an optional "See all".</summary>
        public static VisualElement Sect(string title, string more = null, Action onMore = null) =>
            El("div.sect", El("span.caps", title),
                more == null ? null : onMore != null ? Btn("button.more", onMore, more) : El("span.more.num", more));

        /// <summary>histogram(hist, mine): ten bars (half stars); the reader's own rating in gold.</summary>
        public static VisualElement Histogram(IList<int> hist, double mine = 0)
        {
            int max = Math.Max(1, hist.Count > 0 ? hist.Max() : 1);
            var bars = new List<object>();
            for (int i = 0; i < hist.Count; i++)
            {
                var b = El("i");
                b.style.height = Length.Percent(Math.Max(2f, hist[i] / (float)max * 100f));
                b.tooltip = $"{(i + 1) / 2.0} stars: {F.N(hist[i])}";
                if (mine > 0 && (int)Math.Round(mine * 2) - 1 == i) b.AddToClassList("mine");
                bars.Add(b);
            }
            return El("div.histo", bars);
        }

        /// <summary>bar(ratio, cls): a thin progress bar ("ink" for the dark fill).</summary>
        public static VisualElement Bar(double ratio, string cls = "")
        {
            var fill = El("i");
            fill.style.width = Length.Percent((float)(Math.Max(0, Math.Min(1, ratio)) * 100));
            return El("div.bar" + (string.IsNullOrEmpty(cls) ? "" : "." + cls), fill);
        }

        /// <summary>switchEl(on, onchange): the iOS switch.</summary>
        public static VisualElement SwitchEl(bool on, Action<bool> onChange)
        {
            var knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.AddToClassList("knob");
            var s = new VisualElement();
            s.AddToClassList("switch");
            s.AddToClassList("t-button");
            s.EnableInClassList("on", on);
            s.Add(knob);
            MakeClickable(s, () =>
            {
                bool v = !s.ClassListContains("on");
                s.EnableInClassList("on", v);
                onChange?.Invoke(v);
            });
            return s;
        }

        // ---------- Bars ----------

        /// <summary>navbar(title, { right, onBack, transparent }): back button when there is somewhere to go back to.</summary>
        public static VisualElement Navbar(string title, NavOpts opts = null)
        {
            opts = opts ?? new NavOpts();
            var left = Nav.Count > 1 || opts.OnBack != null
                ? Btn("button.iconbtn", opts.OnBack ?? Nav.Back, Aria("Back"), Icon("back"))
                : El("div", Css(s => s.width = 44));
            return El("div.navbar" + (opts.Transparent ? ".transparent" : ""), left, El("div.title", title ?? ""), opts.Right ?? El("div", Css(s => s.width = 44)));
        }

        /// <summary>tabbar(active): Home, Explore, Log (+), Diary, Profile.</summary>
        public static VisualElement Tabbar(string active)
        {
            VisualElement T(string id, string label, string ic, Func<WebScreen> make) =>
                Btn("button.tab" + (active == id ? ".active" : ""), () => Nav.Reset(make(), id), Aria(label), Icon(ic), El("span.tab-label", label));
            return El("nav.tabbar",
                T("home", "Home", "home", () => new HomeScreen()),
                T("explore", "Explore", "search", () => new ExploreScreen()),
                Btn("button.tab.plus", () => LogSheet.Open(), Aria("Log reading"), El("span.ring", Icon("plus-bold")), El("span.tab-label", "Log")),
                T("diary", "Library", "diary", () => new DiaryScreen()),
                T("profile", "Profile", "user", () => new ProfileScreen()));
        }

        // ---------- Feedback ----------

        /// <summary>iosAlert(title, msg, actions): a centered iOS alert over a scrim.</summary>
        public static void IosAlert(string title, string msg, params AlertAction[] actions)
        {
            var scrim = El("div.scrim");
            VisualElement box = null;
            var buttons = actions.Select(a => (object)Btn("button" + (a.Bold ? ".bold" : "") + (a.Red ? ".red" : ""), () =>
            {
                scrim.RemoveFromHierarchy();
                box.RemoveFromHierarchy();
                a.Do?.Invoke();
            }, a.Label)).ToList();
            box = El("div.alert", El("div.t", title), string.IsNullOrEmpty(msg) ? null : El("div.m", msg), El("div.acts", buttons));
            App.Root.Add(scrim);
            App.Root.Add(box);
            Appear(scrim);
        }

        /// <summary>One alert button: Act("Delete", () => ..., red: true).</summary>
        public static AlertAction Act(string label, Action run = null, bool bold = false, bool red = false) =>
            new AlertAction { Label = label, Do = run, Bold = bold, Red = red };

        static Label _toast;
        static IVisualElementScheduledItem _toastHide;

        /// <summary>toast(msg): a short message at the top that hides itself after 2.4 s.</summary>
        public static void Toast(string msg)
        {
            if (App.Root == null) { Debug.Log("[Margin] " + msg); return; }
            if (_toast == null || _toast.parent != App.Root)
            {
                _toast = H.NewLabel("");
                _toast.AddToClassList("toast");
                _toast.pickingMode = PickingMode.Ignore;
                App.Root.Add(_toast);
            }
            _toast.style.top = Safe.Top + 6;
            _toast.text = msg;
            _toast.BringToFront();
            _toast.AddToClassList("on");
            _toastHide?.Pause();
            _toastHide = _toast.schedule.Execute(() => _toast.RemoveFromClassList("on")).StartingIn(2400);
        }

        /// <summary>
        /// celebrate(): a short burst of confetti over the whole app, for a real decision (starting a book).
        /// The web uses CSS keyframes; here each piece falls on a ~60 fps schedule. Off with reduced motion.
        /// </summary>
        public static void Celebrate()
        {
            if (App.Root == null || App.ReduceMotion) return;
            string[] colors = { "#C23E29", "#B97C10", "#2F6B47", "#1C1A17", "#E9B44C" };
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = 0; box.style.right = 0; box.style.top = 0; box.style.bottom = 0;
            box.style.overflow = Overflow.Hidden;
            float w = App.Root.resolvedStyle.width, hgt = App.Root.resolvedStyle.height;
            var rnd = new System.Random();
            var pieces = new List<(VisualElement el, float x, float dx, float spin, float dur, float delay)>();
            for (int i = 0; i < 80; i++)
            {
                var p = new VisualElement { pickingMode = PickingMode.Ignore };
                p.style.position = Position.Absolute;
                p.style.width = 8; p.style.height = 12;
                p.style.borderTopLeftRadius = p.style.borderTopRightRadius = p.style.borderBottomLeftRadius = p.style.borderBottomRightRadius = 2;
                ColorUtility.TryParseHtmlString(colors[i % colors.Length], out var c);
                p.style.backgroundColor = c;
                box.Add(p);
                pieces.Add((p, (float)rnd.NextDouble() * w, ((float)rnd.NextDouble() - .5f) * 200, 360 + (float)rnd.NextDouble() * 720,
                    2.2f + (float)rnd.NextDouble() * 1.2f, (float)rnd.NextDouble() * .5f));
            }
            App.Root.Add(box);
            box.BringToFront();
            float start = Time.realtimeSinceStartup;
            IVisualElementScheduledItem tick = null;
            tick = box.schedule.Execute(() =>
            {
                float now = Time.realtimeSinceStartup - start;
                foreach (var (el, x, dx, spin, dur, delay) in pieces)
                {
                    float t = Mathf.Clamp01((now - delay) / dur), e = t * t * (3 - 2 * t); // smoothstep: eases in and out
                    el.style.left = x + dx * e;
                    el.style.top = -12 + (hgt * 1.05f + 12) * e;
                    el.style.rotate = new Rotate(spin * e);
                }
                if (now > 4.2f) { tick.Pause(); box.RemoveFromHierarchy(); }
            }).Every(16);
        }

        /// <summary>banner(title, body): the in-app notification banner (as iOS shows it), gone after 4.2 s.</summary>
        public static void Banner(string title, string body)
        {
            var b = El("div.banner", El("div.appic", "m"),
                El("div.grow", El("div.row", El("span.strong.small.grow", "Margin"), El("span.xs", Css(s => s.color = Theme.Color("#888")), "now")),
                    El("div.small.strong", title), El("div.small", body)));
            b.style.top = Safe.Top + 2;
            b.pickingMode = PickingMode.Ignore;
            App.Root.Add(b);
            Appear(b, "banner-from");
            b.schedule.Execute(() => b.RemoveFromHierarchy()).StartingIn(4200);
        }

        /// <summary>Plays an element's entry transition (class "from" state removed on the next frame).</summary>
        internal static void Appear(VisualElement e, string fromClass = "enter-from")
        {
            if (App.ReduceMotion) return;
            e.AddToClassList(fromClass);
            e.schedule.Execute(() => e.RemoveFromClassList(fromClass)).StartingIn(16);
        }

        // ---------- People (net.js helpers) ----------

        /// <summary>pname(p): the name, or "@username".</summary>
        public static string Pname(Profile p) => p == null ? "Reader" : !string.IsNullOrWhiteSpace(p.name) ? p.name.Trim() : "@" + p.username;
        public static string Pname(AuthorRef a) => a == null ? "Reader" : !string.IsNullOrWhiteSpace(a.name) ? a.name.Trim() : "@" + a.username;

        /// <summary>pfirst(p): the first name.</summary>
        public static string Pfirst(Profile p) => Pname(p).TrimStart('@').Split(' ')[0];
        public static string Pfirst(AuthorRef a) => Pname(a).TrimStart('@').Split(' ')[0];

        /// <summary>pav(p): what the avatar needs.</summary>
        public static AvatarUser Pav(Profile p) => new AvatarUser(p?.name ?? p?.username ?? "?", p?.color, p?.id) { Photo = p?.avatar };
        public static AvatarUser Pav(AuthorRef a) => new AvatarUser(a?.name ?? a?.username ?? "?", a?.color);

        /// <summary>cleanQuery(q): text safe to put inside a PostgREST filter.</summary>
        public static string CleanQuery(string q) => AccountRules.CleanQuery(q);

        /// <summary>netFail(e): logs and shows the "couldn't reach Margin" toast.</summary>
        public static void NetFail(Exception e = null)
        {
            if (e != null) Debug.LogWarning("[Margin] " + e.Message);
            Toast(Copy.NetFail);
        }
    }

    /// <summary>Options of <see cref="Sheet.Open"/> (the web's opts: left, right, locked, onClose).</summary>
    public class SheetOpts
    {
        public VisualElement Left;
        public VisualElement Right;
        public bool Locked;
        public Action OnClose;
    }

    /// <summary>The bottom sheet of ui.js: one at a time, over a scrim, with a grab handle.</summary>
    public static class Sheet
    {
        public class Current
        {
            public VisualElement Scrim;
            public VisualElement Sheet;
            /// <summary>The scrolling body (the web's .sbody); replace its content with H.SetKids.</summary>
            public ScrollView Body;
            public Action OnClose;
        }

        /// <summary>The open sheet, or null (the web's Sheet.cur).</summary>
        public static Current Cur { get; private set; }

        public static VisualElement Open(string title, VisualElement body, SheetOpts opts = null)
        {
            opts = opts ?? new SheetOpts();
            Close(true);
            var scrim = Btn("div.scrim", () => { if (!opts.Locked) Close(); });
            var left = opts.Left ?? Btn("button.navbar-btn.textbtn.sheet-cancel", () => Close(), "Cancel");
            var sbody = (ScrollView)El("div.sbody", body);
            sbody.contentContainer.style.paddingBottom = Mathf.Max(34, Safe.Bottom);
            var sheet = El("div.sheet", El("div.grab"),
                El("div.shead", left, El("div.title", title ?? ""), opts.Right ?? El("div", Css(s => s.width = 76))),
                sbody);
            App.Root.Add(scrim);
            App.Root.Add(sheet);
            Components.Appear(scrim);
            Components.Appear(sheet, "sheet-from");
            Cur = new Current { Scrim = scrim, Sheet = sheet, Body = sbody, OnClose = opts.OnClose };
            return sheet;
        }

        public static void Close(bool silent = false)
        {
            if (Cur == null) return;
            Cur.Scrim.RemoveFromHierarchy();
            Cur.Sheet.RemoveFromHierarchy();
            var f = Cur.OnClose;
            Cur = null;
            if (!silent) f?.Invoke();
        }
    }
}
