using System;
using System.Threading.Tasks;
using Margin.App.Web.Screens;
using UnityEngine;
using UnityEngine.UIElements;
using static Margin.App.Web.Components;

namespace Margin.App.Web
{
    /// <summary>
    /// The web's App object (preview/js/app.js): the root element, the first screen after launch or sign-in,
    /// and the theme, text size and motion settings. MarginApp (the MonoBehaviour) mounts it.
    /// </summary>
    public static class App
    {
        /// <summary>The .app element: screens, sheets, alerts and toasts all live in it.</summary>
        public static VisualElement Root { get; private set; }

        /// <summary>An @username from an invite link, kept until the reader is signed in.</summary>
        public static string PendingAdd;

        /// <summary>Settings › Appearance › Reduce motion (no push, sheet or toast animations).</summary>
        public static bool ReduceMotion { get; private set; }

        /// <summary>Called by MarginApp with the .app element.</summary>
        public static void Mount(VisualElement root)
        {
            Root = root;
            Nav.Mount(root);
        }

        /// <summary>app.js start(): Welcome, first-time Setup, or Home (then the pending invite).</summary>
        public static void Start()
        {
            Sheet.Close(true);
            var acc = Accounts.Current;
            ApplySettings();
            if (acc == null) { Nav.Reset(new WelcomeScreen(), null); return; }
            if (!acc.State.profile.onboardingComplete) { Nav.Reset(new SetupScreen(acc)); return; }
            GoHome();
            var add = PendingAdd;
            if (!string.IsNullOrEmpty(add))
            {
                PendingAdd = null;
                if (add != acc.Username) Nav.Push(new UserScreen(add));
            }
        }

        public static void GoHome() => Nav.Reset(new HomeScreen(), "home");

        /// <summary>
        /// app.js applySettings(): theme (System follows the phone; Unity has no runtime dark-mode query, so
        /// "System" means light until the phone reports otherwise), larger text and reduce motion.
        /// </summary>
        public static void ApplySettings()
        {
            if (Root == null) return;
            var st = Accounts.Current?.Settings;
            var theme = st?.Theme ?? "System";
            bool dark = theme == "Dark" || (theme == "System" && SystemPrefersDark());
            Root.EnableInClassList("theme-dark", dark);
            Root.EnableInClassList("text-large", st != null && st.TextLarge);
            ReduceMotion = st != null && st.ReduceMotion;
            Root.EnableInClassList("reduce-motion", ReduceMotion);
        }

        /// <summary>
        /// The phone's appearance. iOS exposes it to native code only; MarginApp can set this from a plugin.
        /// Until then "System" shows the light theme, like a browser without prefers-color-scheme.
        /// </summary>
        public static Func<bool> SystemPrefersDark = () => false;

        /// <summary>"Try it without an account": a guest session, then start().</summary>
        public static async void TryGuest(WebScreen scr)
        {
            if (_guestBusy) return;
            _guestBusy = true;
            if (scr is WelcomeScreen w) { w.Busy = true; w.Err = null; Nav.Refresh(); }
            Services.AuthOutcome r;
            try { r = await Net.SignInGuest(); }
            catch (Exception) { r = new Services.AuthOutcome { Error = "Couldn't reach Margin. Check your connection and try again." }; }
            _guestBusy = false;
            if (scr is WelcomeScreen w2) w2.Busy = false;
            if (r.Error != null)
            {
                if (scr != null && Nav.Current == scr) { scr.Err = r.Error; Nav.Refresh(); }
                else Toast(r.Error);
                return;
            }
            AfterSignIn();
            Start();
        }

        static bool _guestBusy;

        /// <summary>Starts the social refresh after any sign in (the web's Social.start()).</summary>
        public static void AfterSignIn()
        {
            Run(Social.Refresh());
        }

        /// <summary>The in-app reminder banner (Settings › Presentation › Show a reminder).</summary>
        public static void DemoBanner()
        {
            var s = Accounts.Current?.State;
            if (s == null) { Toast("Log in first."); return; }
            var b = Data.Book(s.profile.currentBookId);
            var g = s.profile.goals;
            var q = Data.QuoteOfDay(Clock.Today);
            var qt = q?.text ?? "";
            var title = !string.IsNullOrWhiteSpace(g.intention) ? Copy.WhenI(g.intention) + "…" : "Your book is waiting";
            Banner(title, b != null ? $"{F.Pages(g.dailyPages)} of {b.title}. “{(qt.Length > 70 ? qt.Substring(0, 70) + "…" : qt)}”" : qt);
        }

        /// <summary>The web tints its fake status bar over the book hero; iOS draws the real one, so nothing to do.</summary>
        public static void UpdateStatusBar(VisualElement screen) { }

        /// <summary>Opens a link in Safari (the web's window.open).</summary>
        public static void OpenUrl(string url)
        {
            if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }

        /// <summary>Fire-and-forget for async work from a tap: errors are logged and shown as the network toast.</summary>
        public static async void Run(Task task)
        {
            if (task == null) return;
            try { await task; }
            catch (Exception e)
            {
                Debug.LogException(e);
                NetFail();
            }
        }

        public static void Run(Func<Task> work)
        {
            Task t;
            try { t = work(); }
            catch (Exception e) { t = Task.FromException(e); }
            Run(t);
        }
    }
}
