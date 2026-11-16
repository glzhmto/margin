using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.App.Web
{
    /// <summary>
    /// A screen of the web app (ui.js Screen). Build() returns a fresh tree every time it is shown or
    /// refreshed, reading the current state, exactly like the web.
    /// </summary>
    public abstract class WebScreen
    {
        /// <summary>The element built last (the web's scr.el).</summary>
        public VisualElement El;
        /// <summary>The tab it belongs to, if any (the web's this.tab).</summary>
        public string Tab;
        /// <summary>The web's this.live: rebuilt when friends' data changes (polling) and polled while on top.</summary>
        public bool Live;

        // screenLoad state (the web's _loaded, _loading and err)
        public bool Loaded;
        public bool Loading;
        public string Err;

        public bool IsLive => Live;

        /// <summary>
        /// True if the diary may be swapped for the server's copy while this screen is open
        /// (the app pulls on returning to the foreground). By default only live screens allow it.
        /// </summary>
        public virtual bool CanReplaceState => Live;

        /// <summary>Still the screen on top? Check it after every await before touching the UI.</summary>
        public bool IsCurrent => Nav.Current == this;

        public virtual VisualElement Build() => new VisualElement();
        public virtual void OnShow() { }
        public virtual void OnHide() { }

        /// <summary>
        /// The web's screenLoad(scr, fn): runs the load once (Loaded/Loading guard it), stores errors in Err,
        /// then rebuilds the screen if it is still on top. The function assigns its results to fields:
        /// <code>public override void OnShow() => ScreenLoad(async () => { counts = await Social.BookCounts(); });</code>
        /// Call Reload() to load again (the web's this._loaded = false; this.onShow()).
        /// </summary>
        protected async void ScreenLoad(Func<Task> load)
        {
            if (Loaded || Loading) return;
            Loading = true;
            try
            {
                await load();
                Err = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Margin] " + e.Message);
                Err = "Couldn't load this. Check your connection.";
            }
            Loading = false;
            Loaded = true;
            if (Nav.Current == this) Nav.Refresh();
        }

        public void Reload()
        {
            Loaded = false;
            OnShow();
        }
    }

    /// <summary>
    /// Stack navigation of ui.js: Push, Replace, Back, Reset, Refresh. Screens live in a host element
    /// inside the app root; overlays (sheets, alerts, toasts, banners) are siblings after it.
    /// </summary>
    public static class Nav
    {
        static VisualElement _host;
        static readonly List<WebScreen> _stack = new List<WebScreen>();

        /// <summary>The active tab ("home", "explore", "diary", "profile" or null).</summary>
        public static string Tab = "home";

        public static IReadOnlyList<WebScreen> Stack => _stack;
        public static int Count => _stack.Count;
        public static WebScreen Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        /// <summary>Raised after a screen is rendered (MarginApp uses it to start polling on live screens).</summary>
        public static event Action<WebScreen> Rendered;

        public static void Mount(VisualElement root)
        {
            _stack.Clear();
            _host = new VisualElement { name = "stack" };
            _host.AddToClassList("stack");
            root.Add(_host);
        }

        public static void Push(WebScreen scr)
        {
            Current?.OnHide();
            _stack.Add(scr);
            Render(scr, "push");
        }

        public static void Replace(WebScreen scr)
        {
            if (_stack.Count > 0) { var top = _stack[_stack.Count - 1]; _stack.RemoveAt(_stack.Count - 1); top.OnHide(); }
            _stack.Add(scr);
            Render(scr, "push");
        }

        public static void Back()
        {
            if (_stack.Count < 2) return;
            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            top.OnHide();
            Render(Current, "enter");
        }

        public static void Reset(WebScreen scr, string tab = null)
        {
            foreach (var s in _stack) s.OnHide();
            _stack.Clear();
            _stack.Add(scr);
            if (tab != null) Tab = tab;
            Render(scr, "enter");
        }

        /// <summary>Rebuilds the current screen, keeping its scroll position (no animation).</summary>
        public static void Refresh()
        {
            var c = Current;
            if (c != null) Render(c, null);
        }

        static void Render(WebScreen scr, string anim)
        {
            if (_host == null) return;
            // Keep the scroll position when the same screen is rebuilt.
            Vector2? keep = null;
            var old = _host.childCount > 0 ? _host[0] : null;
            if (old != null && old.userData == scr)
            {
                var oldScroll = old.Q<ScrollView>(className: "scroll");
                if (oldScroll != null) keep = oldScroll.scrollOffset;
            }

            VisualElement el;
            try { el = scr.Build() ?? new VisualElement(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                el = H.El("div", Components.Navbar("Margin"), H.El("p.pad.small.muted.mt12", "Something went wrong on this screen."));
            }
            el.AddToClassList("screen");
            el.userData = scr;
            scr.El = el;

            _host.Clear();
            _host.Add(el);
            Safe.Apply(el);
            WireNavbar(el);

            if (keep.HasValue)
            {
                var s = el.Q<ScrollView>(className: "scroll");
                if (s != null) RestoreScroll(s, keep.Value);
            }

            if (anim != null && !App.ReduceMotion)
            {
                // Start from the "from" state, then drop it on the next frame so the USS transition plays.
                el.AddToClassList(anim == "push" ? "push-from" : "enter-from");
                el.AddToClassList(anim == "push" ? "push" : "enter");
                el.schedule.Execute(() =>
                {
                    el.RemoveFromClassList("push-from");
                    el.RemoveFromClassList("enter-from");
                }).StartingIn(16);
            }

            App.UpdateStatusBar(el);
            Rendered?.Invoke(scr);
            try { scr.OnShow(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>
        /// Puts the old scroll offset back once the new content has a size, clamped to what the new content
        /// can scroll (a browser clamps scrollTop the same way). Without the clamp, a rebuild that makes the
        /// content shorter (e.g. Setup going from the tall book grid to the short year-goal step) kept the old
        /// offset: the ScrollView's scroller accepts the value before its range is updated, so the content
        /// was moved above the viewport and the step looked empty.
        /// </summary>
        static void RestoreScroll(ScrollView s, Vector2 offset)
        {
            EventCallback<GeometryChangedEvent> once = null;
            once = _ =>
            {
                var content = s.contentContainer.layout;
                var view = s.contentViewport.layout;
                if (float.IsNaN(content.height) || float.IsNaN(view.height) || view.height <= 0) return;
                float maxY = Mathf.Max(0, content.height - view.height);
                float maxX = Mathf.Max(0, content.width - view.width);
                s.scrollOffset = new Vector2(Mathf.Clamp(offset.x, 0, maxX), Mathf.Clamp(offset.y, 0, maxY));
                if (content.height > 0) s.contentContainer.UnregisterCallback(once);
            };
            s.contentContainer.RegisterCallback(once);
        }

        /// <summary>The navbar gets a hairline once the content scrolls under it (.navbar.scrolled).</summary>
        static void WireNavbar(VisualElement el)
        {
            var navbar = el.Q<VisualElement>(className: "navbar");
            var s = el.Q<ScrollView>(className: "scroll");
            if (navbar == null || s == null) return;
            s.verticalScroller.valueChanged += v => navbar.EnableInClassList("scrolled", v > 4);
        }
    }
}
