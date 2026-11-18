using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Margin.App.Services;
using Margin.Core;
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
            try { r = await Net.SignInGuest("Demo reader"); }
            catch (Exception) { r = new Services.AuthOutcome { Error = "Couldn't reach Margin. Check your connection and try again." }; }
            _guestBusy = false;
            if (scr is WelcomeScreen w2) w2.Busy = false;
            if (r.Error != null)
            {
                if (scr != null && Nav.Current == scr) { scr.Err = r.Error; Nav.Refresh(); }
                else Toast(r.Error);
                return;
            }
            SeedDemo();
            AfterSignIn();
            Start();
            Toast("You're in a demo account. Everything works, try anything");
        }

        static bool _guestBusy;

        /// <summary>
        /// The demo account: the guest session gets six weeks of reading (Core DemoData) plus the web-only parts
        /// (want to read, a list, Focus sessions), like DemoData.seed and DemoData.extras in preview/js/core.js.
        /// </summary>
        static void SeedDemo()
        {
            var acc = Accounts.Current;
            if (acc == null) return;
            var s = Data.State; var x = Data.X; var today = Clock.Today;
            s.profile.name = "Demo reader";
            var finished = DemoData.Seed(s, Data.Catalog, today);
            foreach (var kv in finished) x.For(kv.Key).finishedOn = D.Key(kv.Value);
            x.yearGoal = 12;
            x.want = new List<string> { "klara-and-the-sun", "hurricane-season", "the-great-gatsby" };
            x.lists = new List<WebList>
            {
                new WebList { id = "demo-short", owner = acc.Username, title = "Short books for exam weeks", description = "Under 150 pages, one sitting each.",
                    books = new List<string> { "the-little-prince", "animal-farm", "the-metamorphosis", "aura" }, created = D.Key(today.AddDays(-20)) },
            };
            x.focusSessions = s.logs.Where((l, i) => i % 2 == 0).Select(l => new FocusSession { date = D.Key(l.date), minutes = 25 }).ToList();
            if (x.focusSessions.Count > 9) x.focusSessions = x.focusSessions.Skip(x.focusSessions.Count - 9).ToList();
            x.notifications = new List<WebNotification>
            {
                new WebNotification { at = D.Stamp(Clock.Now), kind = "welcome", text = "This is a demo account with six weeks of reading. Log a page, take a quiz or start Focus mode." },
            };
            Net.Log("joined");
            Data.Save();
        }

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
