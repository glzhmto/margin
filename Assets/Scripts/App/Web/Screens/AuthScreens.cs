// Port of preview/js/screens-auth.js: welcome, sign up, log in and first-time setup.
// Owner: porter 1. Accounts are real (Supabase Auth through Net / AccountService).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Margin.App.Services;
using Margin.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Margin.App.Web.Components;
using static Margin.App.Web.H;

namespace Margin.App.Web.Screens
{
    public partial class WelcomeScreen : WebScreen
    {
        /// <summary>True while the guest session is being created (the web's this.busy).</summary>
        public bool Busy;

        public override VisualElement Build()
        {
            var ids = Data.Books.Select(b => b.id).Where(id => Data.CoverUrl(id) != null).ToList();
            int[] order = { 5, 0, 9, 2, 12, 7, 14, 3, 10, 1, 15, 6, 4, 11, 8, 13 };
            var tiles = ids.Count == 0 ? new string[0] : order.Select(i => ids[i % ids.Count]).ToArray();
            var mosaic = Mosaic(tiles);

            var paper = Theme.Color("paper");
            Color A(float a) => new Color(paper.r, paper.g, paper.b, a);
            // .welcome-fade: linear-gradient(to bottom, paper/0 8%, paper/.85 44%, paper 54%)
            var fade = Fx.Gradient(El("div.welcome-fade"), (0.08f, A(0)), (0.44f, A(0.85f)), (0.54f, A(1)));
            // linear-gradient(var(--paper) 30%, transparent) over the top 64 px
            var topFade = Fx.Gradient(El("div").Abs(0, 0, null, 0).Hgt(64), (0.3f, A(1)), (1f, A(0)));
            topFade.pickingMode = PickingMode.Ignore;

            var add = App.PendingAdd;
            return El("div", Css(s => s.overflow = Overflow.Hidden),
                mosaic, fade, topFade,
                El("div", Css(s => { s.position = Position.Absolute; s.left = 0; s.right = 0; s.bottom = 0; }))
                    .P(0, 24, Mathf.Max(44, Safe.Bottom + 10), 24)
                    .With(
                        El("div.wordmark", "margin").FontSize(44),
                        El("div.serif-l.mt8", "Your reading diary, for paper books."),
                        El("div.small.muted.mt8", !string.IsNullOrEmpty(add) ? $"@{add} invited you. Sign up or log in to add them as a friend." : "Read with your friends, at your own pace."),
                        El("div.col.gap10.mt24",
                            Btn("button.btn.primary", () => Nav.Push(new SignupScreen()), Disabled(Busy), "Sign up with email"),
                            Btn("button.btn.secondary", () => App.TryGuest(this), Disabled(Busy), Busy ? "Opening Margin…" : "Try it without an account"),
                            Btn("button.btn.ghost", () => Nav.Push(new LoginScreen()), Disabled(Busy), "I already have an account")),
                        Err != null ? El("p.small.center.mt8", Err).Fg("error") : null,
                        El("p.xs.faint.center.mt12", "By continuing you accept the Terms of Use and the Privacy Notice.")));
        }
    }

    public partial class WelcomeScreen
    {
        /// <summary>
        /// .mosaic: a 4-column grid (gap 8) of covers, 30 px wider than the screen on each side, rotated -8deg.
        /// Built as four equal flex columns instead of a wrapping grid: the wrapping grid sized its cells after
        /// layout (Layout.Fit) and, in the Device Simulator, wrapped to three columns, leaving the right side of
        /// the wall empty. Equal columns can't wrap, so the wall always matches the web's composition.
        /// </summary>
        static VisualElement Mosaic(string[] tiles)
        {
            const int cols = 4;
            var mosaic = new VisualElement { name = "mosaic", pickingMode = PickingMode.Ignore };
            mosaic.AddToClassList("mosaic");
            mosaic.style.flexDirection = FlexDirection.Row;
            mosaic.style.alignItems = Align.FlexStart;
            for (int c = 0; c < cols; c++)
            {
                var col = new VisualElement { pickingMode = PickingMode.Ignore };
                col.style.flexGrow = 1; col.style.flexShrink = 1; col.style.flexBasis = 0; col.style.minWidth = 0;
                if (c > 0) col.style.marginLeft = 8;
                // Row-major like the CSS grid: tile i sits in row i / 4, column i % 4.
                for (int i = c, r = 0; i < tiles.Length; i += cols, r++)
                {
                    var cover = Cover(Data.Book(tiles[i]), 72).W("100%");
                    if (r > 0) cover.style.marginTop = 8;
                    col.Add(cover);
                }
                mosaic.Add(col);
            }
            Drift(mosaic);
            return mosaic;
        }

        /// <summary>
        /// The slow drift of the cover wall (app.css: .mosaic { animation: drift 40s ease-in-out infinite alternate },
        /// translate 0 to (-18px, -60px)). It follows the clock, so a rebuild of the screen doesn't restart it,
        /// and it stays still with Reduce motion. The scheduled item stops when the screen leaves the panel.
        /// </summary>
        static void Drift(VisualElement mosaic)
        {
            if (App.ReduceMotion) return;
            const float period = 40f; // seconds one way
            void Step()
            {
                float t = Time.realtimeSinceStartup / period;
                // ease-in-out back and forth: 0 -> 1 in 40 s, 1 -> 0 in the next 40 s
                float k = (1f - Mathf.Cos(t * Mathf.PI)) / 2f;
                mosaic.style.translate = new Translate(-18f * k, -60f * k);
            }
            Step();
            mosaic.schedule.Execute(Step).Every(33);
        }
    }

    /// <summary>Form validation of screens-auth.js (V).</summary>
    public static class V
    {
        static readonly Regex EmailRx = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$");
        static readonly Regex UserRx = new Regex(@"^[a-z0-9._]+$");

        public static string Email(string v) =>
            EmailRx.IsMatch((v ?? "").Trim()) ? null : (v ?? "").Contains("@") ? "Check the email domain." : "The email is missing an @.";

        public static string Username(string v)
        {
            v = v ?? "";
            if (v.Length < 3) return "At least 3 characters.";
            if (v.Length > 16) return "16 characters at most.";
            if (!UserRx.IsMatch(v)) return "Only lowercase letters, numbers, dots and underscores.";
            if (v.StartsWith("deleted_")) return "Pick another name.";
            return null;
        }

        public static int Strength(string p)
        {
            p = p ?? "";
            int s = 0;
            if (p.Length >= 8) s++;
            if (p.Any(IsDigit)) s++;
            if (p.Any(c => c >= 'A' && c <= 'Z') && p.Any(c => c >= 'a' && c <= 'z')) s++;
            if (p.Any(c => !(IsDigit(c) || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))) || p.Length >= 12) s++; // /[^A-Za-z0-9]/
            return s;
        }

        /// <summary>The web's \d (ASCII digits only).</summary>
        static bool IsDigit(char c) => c >= '0' && c <= '9';

        public static string Password(string p) =>
            (p ?? "").Length < 8 ? "At least 8 characters." : !(p ?? "").Any(IsDigit) ? "Add at least one number." : null;
    }

    /// <summary>Small helpers of screens-auth.js shared with other screens.</summary>
    public static class AuthUI
    {
        /// <summary>field(label, input, help).</summary>
        public static VisualElement Field(string label, VisualElement input, VisualElement help = null) =>
            El("label.field.mt16", El("span.lab", label), input, help);

        /// <summary>guestNote(compact): the "save your account" card on Home, Settings and Profile while the reader is a guest.</summary>
        public static VisualElement GuestNote(bool compact)
        {
            var a = Accounts.Current;
            if (a == null || !a.IsGuest) return null;
            if (!string.IsNullOrEmpty(a.PendingEmail))
                return El("div.card.block.guestnote", El("p.small", $"Open the link we sent to {a.PendingEmail} to finish saving your account."))
                    .M(compact ? 8 : 12, 16, 0, 16);
            return El("div.card.block.guestnote" + (compact ? "" : ".mt12"),
                    El("p.small", "You're trying Margin as a guest. Save your account to keep your reading."),
                    Btn("button.btn.sm.primary.mt8", () => Nav.Push(new SaveAccountScreen()), "Save your account"))
                .M(compact ? 8 : 12, 16, 0, 16);
        }
    }

    // ---------------- Sign up ----------------

    /// <summary>
    /// The sign-up form. The web rebuilds the screen on every keystroke and puts the focus back; in Unity a
    /// rebuild would close the iOS keyboard, so the parts that change (input borders, help lines, strength
    /// meter, button) are updated in place by UpdateForm(), the web's update().
    /// </summary>
    public class SignupScreen : WebScreen
    {
        public SignupValues v = new SignupValues();
        public bool terms, show, busy, sent;

        protected readonly HashSet<string> touched = new HashSet<string>();
        /// <summary>Username checks: true free, false taken, null couldn't check (missing = still checking).</summary>
        protected readonly Dictionary<string, bool?> avail = new Dictionary<string, bool?>();
        /// <summary>Refreshers of the parts that change while typing (cleared on every Build).</summary>
        protected readonly List<Action> live = new List<Action>();
        int _checkSeq;

        // ---------- Form state (shared with SaveAccountScreen) ----------

        protected virtual string TakenMessage(string u) => $"@{u} is taken. Try @{u}{DateTime.Now.Year % 100}.";
        protected virtual bool NeedsTerms => true;

        protected string Get(string k)
        {
            switch (k)
            {
                case "name": return v.name;
                case "email": return v.email;
                case "username": return v.username;
                case "password": return v.password;
                default: return v.university;
            }
        }

        protected void Set(string k, string val)
        {
            switch (k)
            {
                case "name": v.name = val; break;
                case "email": v.email = val; break;
                case "username": v.username = val; break;
                case "password": v.password = val; break;
                default: v.university = val; break;
            }
        }

        /// <summary>this.avail[v.username]: has = false while the check hasn't answered.</summary>
        protected bool? Avail(out bool has)
        {
            has = avail.TryGetValue(v.username ?? "", out var a);
            return a;
        }

        protected string UserErr()
        {
            var a = Avail(out var has);
            return V.Username(v.username) ?? (has && a == false ? TakenMessage(v.username) : null);
        }

        protected Dictionary<string, string> Errs() => new Dictionary<string, string>
        {
            ["name"] = (v.name ?? "").Trim().Length < 2 ? "Enter your name." : null,
            ["email"] = V.Email(v.email),
            ["username"] = UserErr(),
            ["password"] = V.Password(v.password),
        };

        protected bool FormOk()
        {
            var a = Avail(out var has);
            return !Errs().Values.Any(e => e != null) && (!NeedsTerms || terms) && has && a == true && !busy;
        }

        /// <summary>checkName(): asks the server 300 ms after the last keystroke (only valid, unchecked names).</summary>
        protected void CheckName()
        {
            var u = v.username;
            if (V.Username(u) != null || avail.ContainsKey(u)) return;
            var seq = ++_checkSeq;
            App.Root?.schedule.Execute(() =>
            {
                if (seq != _checkSeq) return; // clearTimeout: only the last keystroke asks
                App.Run(async () =>
                {
                    var ok = await Net.UsernameAvailable(u);
                    avail[u] = ok;
                    if (IsCurrent) UpdateForm();
                });
            }).StartingIn(300);
        }

        /// <summary>The web's update(): refreshes what changed without rebuilding the inputs.</summary>
        protected void UpdateForm()
        {
            foreach (var f in live) f();
        }

        // ---------- Pieces ----------

        /// <summary>inp(k, attrs): an input bound to v[k], red or green once touched.</summary>
        protected TextField Inp(string k, string placeholder, TouchScreenKeyboardType keyboard = TouchScreenKeyboardType.Default,
                                bool password = false, int maxLength = -1)
        {
            TextField f = null;
            f = Input("input.input", Get(k), val =>
            {
                if (k == "username")
                {
                    // oninput: lowercase, no spaces
                    var clean = Regex.Replace((val ?? "").ToLowerInvariant(), @"\s", "");
                    if (clean != val) { f.SetValueWithoutNotify(clean); val = clean; }
                }
                Set(k, val);
                if (k == "username") CheckName();
                UpdateForm();
            }, placeholder, password, keyboard, maxLength, onBlur: () => { touched.Add(k); UpdateForm(); });
            live.Add(() =>
            {
                Errs().TryGetValue(k, out var e);
                bool t = touched.Contains(k);
                f.EnableInClassList("bad", t && e != null);
                f.EnableInClassList("good", t && e == null);
            });
            return f;
        }

        /// <summary>helpEl(k, def): the error once touched, otherwise the hint.</summary>
        protected Label HelpEl(string k, string def = null)
        {
            var l = Text("div.help", def ?? "");
            live.Add(() =>
            {
                Errs().TryGetValue(k, out var e);
                bool bad = touched.Contains(k) && e != null;
                l.EnableInClassList("bad", bad);
                l.text = bad ? e : def ?? "";
            });
            return l;
        }

        /// <summary>userHelp(): hint, error, "Checking…", available or couldn't check.</summary>
        protected Label UserHelp()
        {
            var l = Text("div.help", "");
            live.Add(() =>
            {
                var u = v.username ?? "";
                var a = Avail(out var has);
                var userErr = UserErr();
                string text; string cls = null;
                if (u.Length == 0) text = "Friends find you by this name.";
                else if (userErr != null && (touched.Contains("username") || (has && a == false))) { text = userErr; cls = "bad"; }
                else if (userErr != null) text = "";
                else if (has && a == true) { text = $"@{u} is available."; cls = "good"; }
                else if (has && a == null) { text = "Couldn't check right now. Try again in a moment."; cls = "bad"; }
                else text = "Checking…";
                l.text = text;
                l.EnableInClassList("bad", cls == "bad");
                l.EnableInClassList("good", cls == "good");
            });
            return l;
        }

        /// <summary>The password input with the show/hide eye (and, on sign up, the strength meter).</summary>
        protected VisualElement PasswordField(bool meter)
        {
            var pw = Inp("password", "", password: !show);
            VisualElement eye = null;
            eye = Btn("button.iconbtn.trail", () =>
            {
                show = !show;
                pw.isPasswordField = !show;
                eye.tooltip = show ? "Hide password" : "Show password";
                SetKids(eye, Icon(show ? "eyeoff" : "eye"));
            }, Aria(show ? "Hide password" : "Show password"), Icon(show ? "eyeoff" : "eye"));

            VisualElement strength = null;
            if (meter)
            {
                var bars = Enumerable.Range(0, 4).Select(_ => El("i")).ToList();
                strength = El("div.strength", bars);
                live.Add(() =>
                {
                    int st = V.Strength(v.password);
                    strength.EnableInClassList("weak", st <= 1);
                    strength.EnableInClassList("mid", st == 2);
                    for (int i = 0; i < 4; i++) bars[i].EnableInClassList("on", i < st);
                });
            }
            return El("label.field.mt16", El("span.lab", "Password"), El("div.inwrap", pw, eye), strength, HelpEl("password", "At least 8 characters, with at least one number."));
        }

        /// <summary>The red line under the form (this.err).</summary>
        protected Label ErrorLine()
        {
            var l = Text("div.help.bad.mt12", "");
            live.Add(() => { l.text = Err ?? ""; l.Show(Err != null); });
            return l;
        }

        /// <summary>The accent button: disabled until the form is valid, with the busy label.</summary>
        protected VisualElement SubmitButton(string label, string busyLabel)
        {
            var b = Btn("button.btn.accent.mt24", () => { if (FormOk()) Submit(); }, label);
            live.Add(() =>
            {
                b.SetEnabled(FormOk());
                if (b is Label l) l.text = busy ? busyLabel : label;
            });
            return b;
        }

        // ---------- Screen ----------

        public override VisualElement Build()
        {
            live.Clear();
            if (sent)
                return El("div", Navbar(""), El("div.scroll.under-nav", El("div.pad",
                    El("div.h1", "Check your email"),
                    El("p.muted.mt8", $"We sent a confirmation link to {v.email}. Open it on this device, then come back and log in."),
                    El("p.small.faint.mt12", "Didn't get it? Check spam or promotions."),
                    Btn("button.btn.primary.mt24", () => Nav.Replace(new LoginScreen(v.email)), "Go to log in"))));

            VisualElement box = El("span.box", terms ? Icon("check") : null);
            VisualElement check = null;
            check = Btn("button.check.mt20" + (terms ? ".on" : ""), () =>
                {
                    terms = !terms;
                    check.EnableInClassList("on", terms);
                    SetKids(box, terms ? Icon("check") : null);
                    UpdateForm();
                },
                box,
                Inline("span", "I accept the ", S("Terms of Use", color: "link"), " and the ", S("Privacy Notice", color: "link"), ". I am 13 or older.").Flex1());
            check.style.alignSelf = Align.Stretch; // display: flex in the web, so it spans the form (not an inline button)

            var body = El("div.pad",
                El("div.h1", "Create account"),
                El("p.muted.mt6", "Your diary is saved to your account, so it follows you to any device."),
                AuthUI.Field("Name", Inp("name", "How you want to be seen"), HelpEl("name")),
                AuthUI.Field("Email", Inp("email", "you@email.com", TouchScreenKeyboardType.EmailAddress), HelpEl("email")),
                AuthUI.Field("Username", El("div.inwrap", Inp("username", "no spaces")), UserHelp()),
                PasswordField(true),
                AuthUI.Field("University (optional)", Inp("university", "Universidad Panamericana", maxLength: 80), El("div.help", "We suggest friends from the same place.")),
                check,
                ErrorLine(),
                SubmitButton("Create account", "Creating…"),
                El("div.bottom-space"));
            var root = El("div", Navbar(""), El("div.scroll.under-nav", body));
            UpdateForm();
            return root;
        }

        protected virtual async void Submit()
        {
            if (busy) return;
            busy = true; Err = null; UpdateForm();
            AuthOutcome r;
            try { r = await Net.SignUp(v); }
            catch (Exception) { r = new AuthOutcome { Error = "Couldn't reach Margin. Check your connection." }; }
            busy = false;
            if (r.Error != null) { Err = r.Error; if (IsCurrent) UpdateForm(); return; }
            if (r.Confirm) { sent = true; if (IsCurrent) Nav.Refresh(); return; }
            App.ApplySettings();
            App.AfterSignIn();
            var acc = Accounts.Current;
            if (acc == null) { App.Start(); return; }
            Nav.Reset(new SetupScreen(acc));
        }
    }

    // ---------------- Log in ----------------

    public class LoginScreen : WebScreen
    {
        public string id, pw = "";
        bool show, busy;

        public LoginScreen(string email = null) { id = email ?? ""; }

        public override VisualElement Build()
        {
            var bad = Err != null ? ".bad" : "";
            var idI = Input("input.input" + bad, id, x => id = x, "you@email.com", keyboard: TouchScreenKeyboardType.EmailAddress);
            var pwI = Input("input.input" + bad, pw, x => pw = x, "password", password: !show, onEnter: Submit);
            VisualElement eye = null;
            // The web rebuilds the screen here; toggling in place keeps the keyboard open.
            eye = Btn("button.iconbtn.trail", () => { show = !show; pwI.isPasswordField = !show; SetKids(eye, Icon(show ? "eyeoff" : "eye")); },
                Aria("Show password"), Icon(show ? "eyeoff" : "eye"));
            return El("div", Navbar(""), El("div.scroll.under-nav", El("div.pad",
                El("div.h1", "Log in"),
                AuthUI.Field("Email", idI),
                AuthUI.Field("Password", El("div.inwrap", pwI, eye)),
                El("div.help.bad", Err ?? ""),
                Btn("button.btn.primary.mt12", Submit, Disabled(busy), busy ? "Logging in…" : "Log in"),
                Btn("button.btn.ghost.mt4", () => Nav.Push(new ForgotScreen(id)), "I forgot my password"))));
        }

        async void Submit()
        {
            if (busy) return;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrEmpty(pw)) { Err = "Enter your email and password."; Nav.Refresh(); return; }
            if (V.Email(id) != null) { Err = "Log in with the email you signed up with."; Nav.Refresh(); return; }
            busy = true; Err = null; Nav.Refresh();
            AuthOutcome r;
            try { r = await Net.SignIn(id, pw); }
            catch (Exception) { r = new AuthOutcome { Error = "Couldn't reach Margin. Check your connection." }; }
            busy = false;
            if (r.Error != null) { Err = r.Error; if (IsCurrent) Nav.Refresh(); return; }
            App.AfterSignIn();
            App.Start();
        }
    }

    // ---------------- Forgot password ----------------

    /// <summary>Sends the real Supabase recovery email (Net.ResetPassword).</summary>
    public class ForgotScreen : WebScreen
    {
        public string email;
        bool sent, sending;

        public ForgotScreen(string prefill = null) { email = prefill != null && prefill.Contains("@") ? prefill : ""; }

        public override VisualElement Build()
        {
            if (sent)
                return El("div", Navbar(""), El("div.scroll.under-nav", El("div.pad",
                    El("div.h1", "Check your email"),
                    El("p.muted.mt8", $"If {email} has an account, you will get a link to set a new password. Open it on this device."),
                    El("p.small.faint.mt12", "Didn't get it? Check spam or promotions."),
                    Btn("button.btn.secondary.mt24", Nav.Back, "Back to log in"))));
            var e = Input("input.input" + (Err != null ? ".bad" : ""), email, x => email = x, "you@email.com", keyboard: TouchScreenKeyboardType.EmailAddress);
            return El("div", Navbar(""), El("div.scroll.under-nav", El("div.pad",
                El("div.h1", "Reset password"),
                El("p.muted.mt8", "We will send a link to your account's email."),
                AuthUI.Field("Email", e), El("div.help.bad", Err ?? ""),
                Btn("button.btn.primary.mt12", Send, "Send link"))));
        }

        async void Send()
        {
            if (sending) return; // the web has no busy state; this only stops a double send
            Err = V.Email(email);
            if (Err == null)
            {
                sending = true;
                try { Err = await Net.ResetPassword(email); }
                catch (Exception) { Err = "Couldn't reach Margin. Check your connection."; }
                sending = false;
                if (Err == null) sent = true;
            }
            if (IsCurrent) Nav.Refresh();
        }
    }

    // ---------------- New password ----------------

    /// <summary>
    /// Sets a new password for the signed-in session. On the web it opens from the reset link in the email
    /// (Supabase's PASSWORD_RECOVERY event). The recovery email links to the web app, so in Unity the screen
    /// is reached by pushing it while the app holds a session (Nav.Push(new NewPasswordScreen())): for
    /// example, once a margin:// recovery deep link has been turned into a session by MarginApp.
    /// </summary>
    public class NewPasswordScreen : WebScreen
    {
        string p = "";
        bool saving;

        public override VisualElement Build()
        {
            var e = Input("input.input", p, x => p = x, "New password", password: true);
            return El("div", Navbar("New password"), El("div.scroll.under-nav", El("div.pad.mt12",
                El("p.muted", "Choose a new password for your account."), El("div.mt12", e), El("div.help.bad", Err ?? ""),
                Btn("button.btn.primary.mt12", Save, "Save password"))));
        }

        async void Save()
        {
            if (saving) return;
            Err = V.Password(p);
            if (Err == null)
            {
                saving = true;
                try { Err = await Net.SetPassword(p); }
                catch (Exception) { Err = "Couldn't reach Margin. Check your connection."; }
                saving = false;
            }
            if (Err != null) { if (IsCurrent) Nav.Refresh(); return; }
            Toast("Password updated");
            App.Start();
        }
    }

    // ---------------- First-time setup ----------------

    /// <summary>First-time setup after creating the account (or entering as a guest).</summary>
    public class SetupScreen : WebScreen
    {
        public Acc acc;
        public int step;

        /// <summary>The plan being chosen (this.g).</summary>
        class Plan
        {
            public int daily = 15;
            public List<int> days = new List<int> { 1, 2, 3, 4, 5 };
            public int hour = 22, min = 0;
            public string intention = "";
            public string book;
            public int year = 12;
            public bool? notif;
        }

        readonly Plan g = new Plan();
        readonly int[] order;

        bool IsGuest => acc != null && acc.IsGuest;

        public SetupScreen(Acc acc)
        {
            this.acc = acc;
            // Guests see three screens (pages a day, book, summary) with the rest filled in; all of it changes later in Settings.
            order = IsGuest ? new[] { 0, 3, 6 } : new[] { 0, 1, 2, 3, 4, 5, 6 };
            if (IsGuest) { g.book = "the-little-prince"; g.daily = 10; g.notif = false; }
        }

        static int Round(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);

        public override VisualElement Build()
        {
            int N = order.Length - 1;
            var steps = El("div.row.gap4.pad.mt8", Enumerable.Range(0, N).Select(i => El("div.grow").Hgt(3).Radius(2).Bg(i <= step ? "ink" : "rule")));
            object[] content;
            string cta = "Next";
            Action ctaDo = Next;
            switch (order[step])
            {
                case 0:
                    content = new object[]
                    {
                        El("div.h1", "How many pages a day?"), El("p.muted.mt8", "You can change it any time. Ten pages take about fifteen minutes."),
                        El("div.row.wrap.gap8.mt20", new[] { 5, 10, 15, 20, 30, 50 }.Select(n => Btn("button.chip" + (g.daily == n ? ".on" : ""), () => { g.daily = n; Nav.Refresh(); }, n))),
                        El("p.small.faint.mt12", $"{g.daily} pages, about {Round(g.daily * 1.5)} minutes."),
                    };
                    break;
                case 1:
                    content = new object[]
                    {
                        El("div.h1", "Which days will you read?"), El("p.muted.mt8", "The days you leave out are planned rest. They don't count against you."),
                        El("div.row.gap6.mt20", new[] { 1, 2, 3, 4, 5, 6, 0 }.Select(d => Btn("button.chip.day" + (g.days.Contains(d) ? ".on" : ""), () =>
                        {
                            if (g.days.Contains(d)) g.days.Remove(d); else g.days.Add(d);
                            Err = null; Nav.Refresh();
                        }, Aria(Fmt.Days[d]), Copy.DayLetters[d]))),
                        El("p.small.faint.mt12", g.days.Count > 0 ? $"{F.Pages(g.daily * g.days.Count)} a week, {Fmt.DayList(g.days)}." : "Pick at least one day."),
                    };
                    break;
                case 2:
                {
                    var intent = Input("input.input", g.intention, x => g.intention = x, "finish dinner", maxLength: 50);
                    content = new object[]
                    {
                        El("div.h1", "When will you read?"), El("p.muted.mt8", "We send a reminder at that time, only on the days you picked, and never if you already logged."),
                        El("div.stepper.mt20",
                            Btn("button.circle", () => { g.hour = (g.hour + 23) % 24; Nav.Refresh(); }, Aria("One hour earlier"), "−"),
                            El("div.bignum", Fmt.Time(g.hour, g.min)),
                            Btn("button.circle", () => { g.hour = (g.hour + 1) % 24; Nav.Refresh(); }, Aria("One hour later"), "+")),
                        El("div.row.gap8.mt12", new[] { 0, 15, 30, 45 }.Select(m => Btn("button.chip" + (g.min == m ? ".on" : ""), () => { g.min = m; Nav.Refresh(); }, ":" + m.ToString("00"))))
                            .Justify(Justify.Center),
                        El("label.field.mt24", El("span.lab", "When I... (optional)"), intent),
                        El("div.row.wrap.gap6.mt8", Copy.IntentionSuggestions.Select(s => Btn("button.pillbtn" + (g.intention == s ? ".on" : ""), () => { g.intention = s; Nav.Refresh(); }, s))),
                    };
                    break;
                }
                case 3:
                    content = new object[]
                    {
                        El("div.h1", "What are you reading?"), El("p.muted.mt8", "Pick one. If you already started it, you can enter the page later."),
                        El("div.grid3.mt16", Data.Books.Select(b => El("div.col.gap4", On(() => { g.book = b.id; Err = null; Nav.Refresh(); }),
                            PickCover(b, g.book == b.id),
                            El("div.xs.clamp2" + (g.book == b.id ? ".strong" : ".muted"), b.title)))),
                    };
                    break;
                case 4:
                {
                    var today = Clock.Today;
                    content = new object[]
                    {
                        El("div.h1", "How many books this year?"), El("p.muted.mt8", $"{13 - today.Month} months of {today.Year} left. The goal only shows in your diary."),
                        El("div.row.wrap.gap8.mt20", new[] { 4, 6, 12, 24, 52 }.Select(n => Btn("button.chip" + (g.year == n ? ".on" : ""), () => { g.year = n; Nav.Refresh(); }, n))),
                        El("p.small.faint.mt12", g.year >= 24 ? $"{g.year} books is about {Round(52.0 / g.year * 7)} days per book." : $"One book every {Round(52.0 / g.year)} weeks."),
                    };
                    break;
                }
                case 5:
                    content = new object[]
                    {
                        El("div.h1", "Reminders"), El("p.muted.mt8", "Four a week at most, with a line from the book you are reading. If you ignore them, we write less."),
                        El("div.card.block.mt20", El("div.row.gap10", El("div.appic", "m").Font(true, 600),
                            El("div.grow", El("div.small.strong", !string.IsNullOrEmpty(g.intention) ? Copy.WhenI(g.intention) : "Your book is waiting"),
                                El("div.small.muted", "“Let others boast of the pages they have written; I am proud of those I have read.” Borges")))),
                        g.notif == false ? El("p.small.mt12", "Without permission we can't remind you. You can turn it on later in iPhone Settings.").Fg("error") : null,
                    };
                    cta = g.notif == null ? "Turn on reminders" : "Next";
                    ctaDo = () => { if (g.notif == null) AskNotifications(); else Next(); };
                    break;
                default:
                {
                    var b = Data.Book(g.book);
                    var first = (acc?.Name ?? "").Split(' ')[0];
                    content = new object[]
                    {
                        El("div.h1", IsGuest ? "All set" : $"All set, {first}"), El("p.muted.mt8", "Here is your plan. You can change all of it in Settings."),
                        El("div.list.mt20",
                            El("div.item", El("span.grow", "Daily goal"), El("span.val", F.Pages(g.daily))),
                            El("div.item", El("span.grow", "Days"), El("span.val", Fmt.DayList(g.days))),
                            El("div.item", El("span.grow", "Reminder"), El("span.val", g.notif == true ? Fmt.Time(g.hour, g.min) : "Off")),
                            El("div.item", El("span.grow", "Yearly goal"), El("span.val", F.Books(g.year))),
                            b != null ? El("div.item", Cover(b, 44), El("div.grow", El("div.small.faint", "Reading"), El("div.strong", b.title))) : null)
                            .M(20, 0, 0),
                    };
                    cta = "Start"; ctaDo = () => Finish(false);
                    break;
                }
            }

            return El("div",
                Navbar("", new NavOpts { OnBack = Back }),
                steps,
                El("div.scroll.under-nav", El("div.pad.mt20", content)),
                El("div.pad", El("div.help.bad", Err ?? ""), Btn("button.btn.accent", ctaDo, cta))
                    .P(12, 16, Mathf.Max(40, Safe.Bottom + 6), 16).BorderTop(1, "rule"));
        }

        /// <summary>cover(b, 110) at full width with the 3 px accent outline (outline-offset 2 px) when picked.</summary>
        static VisualElement PickCover(Book b, bool on)
        {
            var wrap = El("div", Cover(b, 110).W("100%")).Relative();
            wrap.pickingMode = PickingMode.Ignore;
            if (on)
            {
                // USS has no outline: a ring drawn over the cover, outside its edges.
                var ring = El("div").Abs(-5, -5, -5, -5).Border(3, "accent").Radius(6);
                ring.pickingMode = PickingMode.Ignore;
                wrap.Add(ring);
            }
            return wrap;
        }

        void Back()
        {
            if (step == 0)
                IosAlert("Leave setup?", IsGuest ? "We'll use these settings. You can change them later." : "Your account is already created. You can finish this later.",
                    Act("Stay here"), Act("Leave", () => Finish(true)));
            else { step--; Nav.Refresh(); }
        }

        void Next()
        {
            int k = order[step];
            if (k == 1 && g.days.Count == 0) { Err = "Pick at least one reading day."; Nav.Refresh(); return; }
            if (k == 3 && g.book == null) { Err = "Pick a book. You can change it any time."; Nav.Refresh(); return; }
            Err = null;
            step++; Nav.Refresh();
        }

        /// <summary>
        /// "Turn on reminders": on an iPhone this is the real system permission prompt. In the editor there is
        /// no system prompt, so it shows the web's copy of the iOS alert instead.
        /// </summary>
        void AskNotifications()
        {
            var host = MarginApp.Instance;
            if (host != null && host.Notifications.IsSupported)
            {
                host.StartCoroutine(host.Notifications.RequestPermission(granted =>
                {
                    g.notif = granted;
                    if (!IsCurrent) return;
                    if (granted) Next(); else Nav.Refresh();
                }));
                return;
            }
            IosAlert("“Margin” Would Like to Send You Notifications", "Notifications may include alerts and sounds. You can change this in Settings.",
                Act("Don’t Allow", () => { g.notif = false; Nav.Refresh(); }),
                Act("Allow", () => { g.notif = true; Next(); }, bold: true));
        }

        void Finish(bool skip)
        {
            var s = Data.State;
            s.profile.createdOn = Clock.Today;
            s.profile.onboardingComplete = true;
            var goals = s.profile.goals;
            goals.dailyPages = g.daily;
            goals.readingDays = g.days.Count > 0 ? new List<int>(g.days) : new List<int> { 1, 2, 3, 4, 5 };
            goals.reminderHour = g.hour;
            goals.reminderMinute = g.min;
            goals.remindersEnabled = g.notif == true;
            goals.intention = string.IsNullOrEmpty(g.intention) ? null : g.intention;
            Data.X.yearGoal = g.year;
            ReadingService.SetCurrentBook(s, g.book ?? "the-little-prince");
            Data.X.notifications = new List<WebNotification>
            {
                new WebNotification { at = D.Stamp(Clock.Now), kind = "welcome", text = "Your diary starts today. Log your first reading with the + button." },
            };
            Data.Save();
            Net.Log("joined"); // also flushes the diary to the server (Net.flush)
            MarginApp.Instance?.RescheduleReminders(); // the real local reminders for the new plan
            App.Start();
            if (!skip) Toast(IsGuest ? "You're in as a guest" : "Account created");
        }
    }

    // ---------------- Save a guest account ----------------

    /// <summary>Guests keep everything they logged: this adds an email and password to the same account.</summary>
    public class SaveAccountScreen : SignupScreen
    {
        public SaveAccountScreen()
        {
            var a = Accounts.Current;
            v.name = a != null && !string.IsNullOrEmpty(a.Name) && a.Name != "Guest" ? a.Name : "";
        }

        protected override string TakenMessage(string u) => $"@{u} is taken. Try another one.";
        protected override bool NeedsTerms => false;

        public override VisualElement Build()
        {
            live.Clear();
            var s = Data.State;
            int logged = s != null ? s.logs.Sum(l => Math.Max(0, l.toPage - l.fromPage)) : 0;
            var body = El("div.pad",
                El("div.h1", "Save your account"),
                El("p.muted.mt6", logged > 0 ? $"You keep the {F.Pages(logged)} you logged, your book and your friends." : "You keep your book, your goals and your friends."),
                AuthUI.Field("Name", Inp("name", "How you want to be seen"), HelpEl("name")),
                AuthUI.Field("Email", Inp("email", "you@email.com", TouchScreenKeyboardType.EmailAddress), HelpEl("email")),
                AuthUI.Field("Username", El("div.inwrap", Inp("username", "no spaces")), UserHelp()),
                PasswordField(false),
                ErrorLine(),
                SubmitButton("Save account", "Saving…"),
                El("div.bottom-space"));
            var root = El("div", Navbar(""), El("div.scroll.under-nav", body));
            UpdateForm();
            return root;
        }

        protected override async void Submit()
        {
            if (busy) return;
            busy = true; Err = null; UpdateForm();
            string e, pending;
            try { (e, pending) = await Net.SaveGuest(v); }
            catch (Exception) { e = "Couldn't reach Margin. Check your connection."; pending = null; }
            busy = false;
            if (pending != null)
            {
                if (IsCurrent) UpdateForm();
                IosAlert("Check your email", $"We sent a link to {pending}. Open it on this device to finish saving your account. Until then, don't log out.",
                    Act("Close", () => { if (IsCurrent) Nav.Back(); }, bold: true));
                return;
            }
            if (e != null) { Err = e; if (IsCurrent) UpdateForm(); return; }
            Toast($"Saved. You're @{v.username} now.");
            if (IsCurrent) Nav.Back();
        }
    }
}
