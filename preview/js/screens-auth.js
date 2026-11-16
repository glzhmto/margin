/* Welcome, sign up, log in and first-time setup. Accounts are real (Supabase Auth, js/net.js). */
"use strict";

class WelcomeScreen extends Screen {
  build() {
    const ids = Catalog.data.books.map(b => b.id).filter(id => Data.coverUrl(id));
    const order = [5, 0, 9, 2, 12, 7, 14, 3, 10, 1, 15, 6, 4, 11, 8, 13];
    const tiles = order.map(i => ids[i % ids.length]);
    const mosaic = h("div.mosaic", tiles.map(id => cover(Data.book(id), 72, { style: { width: "100%" } })));
    const add = App.pendingAdd;
    return h("div", { style: { overflow: "hidden" } },
      mosaic, h("div.welcome-fade"), h("div", { style: { position: "absolute", top: 0, left: 0, right: 0, height: "64px", background: "linear-gradient(var(--paper) 30%, transparent)" } }),
      h("div", { style: { position: "absolute", left: 0, right: 0, bottom: 0, padding: "0 24px 44px" } },
        h("div.wordmark", { style: { fontSize: "44px" } }, "margin"),
        h("div.serif-l.mt8", "Your reading diary, for paper books."),
        h("div.small.muted.mt8", add ? `@${add} invited you. Sign up or log in to add them as a friend.` : "Read with your friends, at your own pace."),
        h("div.col.gap10.mt24",
          h("button.btn.primary", { disabled: this.busy, onclick: () => Nav.push(new SignupScreen()) }, "Sign up with email"),
          h("button.btn.secondary", { disabled: this.busy, onclick: () => App.tryGuest(this) }, this.busy ? "Opening Margin…" : "Try it without an account"),
          h("button.btn.ghost", { disabled: this.busy, onclick: () => Nav.push(new LoginScreen()) }, "I already have an account")),
        this.err ? h("p.small.center.mt8", { role: "alert", style: { color: "var(--error)" } }, this.err) : null,
        h("p.xs.faint.center.mt12", "By continuing you accept the Terms of Use and the Privacy Notice.")));
  }
}

/* ---- Validation ---- */
const V = {
  email(v) { return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(v.trim()) ? null : (v.includes("@") ? "Check the email domain." : "The email is missing an @."); },
  username(v) {
    if (v.length < 3) return "At least 3 characters.";
    if (v.length > 16) return "16 characters at most.";
    if (!/^[a-z0-9._]+$/.test(v)) return "Only lowercase letters, numbers, dots and underscores.";
    if (/^deleted_/.test(v)) return "Pick another name.";
    return null;
  },
  strength(p) { let s = 0; if (p.length >= 8) s++; if (/\d/.test(p)) s++; if (/[A-Z]/.test(p) && /[a-z]/.test(p)) s++; if (/[^A-Za-z0-9]/.test(p) || p.length >= 12) s++; return s; },
  password(p) { return p.length < 8 ? "At least 8 characters." : !/\d/.test(p) ? "Add at least one number." : null; },
};

function field(label, input, help) { return h("label.field.mt16", h("span.lab", label), input, help); }

class SignupScreen extends Screen {
  constructor() { super(); this.v = { name: "", email: "", username: "", password: "", university: "" }; this.terms = false; this.show = false; this.touched = {}; this.avail = {}; this.busy = false; this.err = null; this.sent = false; }
  checkName() {
    const u = this.v.username; if (V.username(u) || u in this.avail) return;
    clearTimeout(this._t);
    this._t = setTimeout(() => Net.usernameAvailable(u).then(ok => { this.avail[u] = ok; if (Nav.current() === this) this.update(); }), 300);
  }
  build() {
    const v = this.v;
    if (this.sent) return h("div", navbar(""), h("div.scroll.under-nav", h("div.pad",
      h("div.h1", "Check your email"),
      h("p.muted.mt8", `We sent a confirmation link to ${v.email}. Open it on this device, then come back and log in.`),
      h("p.small.faint.mt12", "Didn't get it? Check spam or promotions."),
      h("button.btn.primary.mt24", { onclick: () => Nav.replace(new LoginScreen(v.email)) }, "Go to log in"))));
    const a = this.avail[v.username];
    const userErr = V.username(v.username) || (a === false ? `@${v.username} is taken. Try @${v.username}${new Date().getFullYear() % 100}.` : null);
    const errs = { name: v.name.trim().length < 2 ? "Enter your name." : null, email: V.email(v.email), username: userErr, password: V.password(v.password) };
    const ok = !Object.values(errs).some(Boolean) && this.terms && a === true && !this.busy;
    const inp = (k, attrs) => {
      const e = h("input.input" + (this.touched[k] && errs[k] ? ".bad" : this.touched[k] && !errs[k] ? ".good" : ""), { value: v[k], ...attrs });
      e.addEventListener("input", () => { v[k] = e.value; if (k === "username") this.checkName(); this.update(); });
      e.addEventListener("blur", () => { this.touched[k] = true; this.update(); });
      return e;
    };
    const userHelp = () => {
      if (!v.username) return h("div.help", "Friends find you by this name.");
      if (userErr && (this.touched.username || a === false)) return h("div.help.bad", userErr);
      if (userErr) return h("div.help", "");
      if (a === true) return h("div.help.good", `@${v.username} is available.`);
      if (a === null) return h("div.help.bad", "Couldn't check right now. Try again in a moment.");
      return h("div.help", "Checking…");
    };
    const helpEl = (k, def) => h("div.help" + (this.touched[k] && errs[k] ? ".bad" : ""), this.touched[k] && errs[k] ? errs[k] : def || "");
    const st = V.strength(v.password);
    const pw = inp("password", { type: this.show ? "text" : "password", autocomplete: "new-password", placeholder: "" });
    const body = h("div.pad",
      h("div.h1", "Create account"),
      h("p.muted.mt6", "Your diary is saved to your account, so it follows you to any device."),
      field("Name", inp("name", { autocomplete: "name", placeholder: "How you want to be seen" }), helpEl("name")),
      field("Email", inp("email", { type: "email", autocomplete: "email", inputmode: "email", placeholder: "you@email.com" }), helpEl("email")),
      field("Username", h("div.inwrap", inp("username", { autocapitalize: "none", placeholder: "no spaces", oninput: e => { e.target.value = e.target.value.toLowerCase().replace(/\s/g, ""); } })), userHelp()),
      field("Password", h("div.inwrap", pw, h("button.iconbtn.trail", { type: "button", "aria-label": this.show ? "Hide password" : "Show password", onclick: () => { this.show = !this.show; this.update(); } }, icon(this.show ? "eyeoff" : "eye"))),
        h("div.strength" + (st <= 1 ? ".weak" : st === 2 ? ".mid" : ""), [0, 1, 2, 3].map(i => h("i" + (i < st ? ".on" : "")))),
        helpEl("password", "At least 8 characters, with at least one number.")),
      field("University (optional)", inp("university", { autocomplete: "organization", placeholder: "Universidad Panamericana", maxlength: 80 }), h("div.help", "We suggest friends from the same place.")),
      h("button.check.mt20" + (this.terms ? ".on" : ""), { onclick: () => { this.terms = !this.terms; this.update(); } },
        h("span.box", this.terms ? icon("check") : null), h("span", "I accept the ", h("span.link", "Terms of Use"), " and the ", h("span.link", "Privacy Notice"), ". I am 13 or older.")),
      this.err ? h("div.help.bad.mt12", this.err) : null,
      h("button.btn.accent.mt24", { disabled: !ok, onclick: () => this.submit() }, this.busy ? "Creating…" : "Create account"),
      h("div.bottom-space"));
    return h("div", navbar(""), h("div.scroll.under-nav", body));
  }
  update() { const a = document.activeElement; const ph = a && a.getAttribute("placeholder"); const pos = a && a.selectionStart; Nav.refresh(); if (ph != null) { const e = [...Nav.current().el.querySelectorAll("input")].find(i => i.getAttribute("placeholder") === ph); if (e) { e.focus(); try { e.setSelectionRange(pos, pos); } catch {} } } }
  async submit() {
    this.busy = true; this.err = null; this.update();
    let r;
    try { r = await Net.signUp(this.v); } catch (e) { r = { error: "Couldn't reach Margin. Check your connection." }; }
    this.busy = false;
    if (r.error) { this.err = r.error; return this.update(); }
    if (r.confirm) { this.sent = true; return Nav.refresh(); }
    App.applySettings();
    Nav.reset(new SetupScreen(Accounts.current()));
  }
}

class LoginScreen extends Screen {
  constructor(email) { super(); this.id = email || ""; this.pw = ""; this.err = null; this.show = false; this.busy = false; }
  build() {
    const idI = h("input.input" + (this.err ? ".bad" : ""), { value: this.id, type: "email", autocapitalize: "none", autocomplete: "email", inputmode: "email", placeholder: "you@email.com" });
    idI.addEventListener("input", () => { this.id = idI.value; });
    const pwI = h("input.input" + (this.err ? ".bad" : ""), { value: this.pw, type: this.show ? "text" : "password", autocomplete: "current-password", placeholder: "password" });
    pwI.addEventListener("input", () => { this.pw = pwI.value; });
    pwI.addEventListener("keydown", e => { if (e.key === "Enter") this.submit(); });
    return h("div", navbar(""), h("div.scroll.under-nav", h("div.pad",
      h("div.h1", "Log in"),
      field("Email", idI),
      field("Password", h("div.inwrap", pwI, h("button.iconbtn.trail", { type: "button", "aria-label": "Show password", onclick: () => { this.show = !this.show; Nav.refresh(); } }, icon(this.show ? "eyeoff" : "eye")))),
      h("div.help.bad", this.err || ""),
      h("button.btn.primary.mt12", { disabled: this.busy, onclick: () => this.submit() }, this.busy ? "Logging in…" : "Log in"),
      h("button.btn.ghost.mt4", { onclick: () => Nav.push(new ForgotScreen(this.id)) }, "I forgot my password"))));
  }
  async submit() {
    if (!this.id.trim() || !this.pw) { this.err = "Enter your email and password."; return Nav.refresh(); }
    if (V.email(this.id)) { this.err = "Log in with the email you signed up with."; return Nav.refresh(); }
    this.busy = true; this.err = null; Nav.refresh();
    let r;
    try { r = await Net.signIn(this.id, this.pw); } catch (e) { r = { error: "Couldn't reach Margin. Check your connection." }; }
    this.busy = false;
    if (r.error) { this.err = r.error; return Nav.refresh(); }
    App.start();
  }
}

class ForgotScreen extends Screen {
  constructor(prefill) { super(); this.email = prefill && prefill.includes("@") ? prefill : ""; this.sent = false; this.err = null; }
  build() {
    if (this.sent) return h("div", navbar(""), h("div.scroll.under-nav", h("div.pad",
      h("div.h1", "Check your email"),
      h("p.muted.mt8", `If ${this.email} has an account, you will get a link to set a new password. Open it on this device.`),
      h("p.small.faint.mt12", "Didn't get it? Check spam or promotions."),
      h("button.btn.secondary.mt24", { onclick: () => Nav.back() }, "Back to log in"))));
    const e = h("input.input" + (this.err ? ".bad" : ""), { value: this.email, type: "email", placeholder: "you@email.com" });
    e.addEventListener("input", () => { this.email = e.value; });
    return h("div", navbar(""), h("div.scroll.under-nav", h("div.pad",
      h("div.h1", "Reset password"),
      h("p.muted.mt8", "We will send a link to your account's email."),
      field("Email", e), h("div.help.bad", this.err || ""),
      h("button.btn.primary.mt12", { onclick: async () => { this.err = V.email(this.email); if (!this.err) { this.err = await Net.resetPassword(this.email); if (!this.err) this.sent = true; } Nav.refresh(); } }, "Send link"))));
  }
}

/* Opened from the reset link in the email. */
class NewPasswordScreen extends Screen {
  constructor() { super(); this.p = ""; this.err = null; }
  build() {
    const e = h("input.input", { type: "password", placeholder: "New password", autocomplete: "new-password", value: this.p });
    e.addEventListener("input", () => { this.p = e.value; });
    return h("div", navbar("New password"), h("div.scroll.under-nav", h("div.pad.mt12",
      h("p.muted", "Choose a new password for your account."), h("div.mt12", e), h("div.help.bad", this.err || ""),
      h("button.btn.primary.mt12", { onclick: async () => { this.err = V.password(this.p) || await Net.setPassword(this.p); if (this.err) return Nav.refresh(); toast("Password updated"); App.start(); } }, "Save password"))));
  }
}

/* ---- First-time setup (after creating the account) ---- */
class SetupScreen extends Screen {
  constructor(acc) { super(); this.acc = acc; this.step = 0; this.g = { daily: 15, days: [1, 2, 3, 4, 5], hour: 22, min: 0, intention: "", book: null, year: 12, notif: null }; this.err = null;
    // Guests see three screens (pages a day, book, summary) with the rest filled in; all of it changes later in Settings.
    this.order = acc.isGuest ? [0, 3, 6] : [0, 1, 2, 3, 4, 5, 6];
    if (acc.isGuest) { this.g.book = "the-little-prince"; this.g.daily = 10; this.g.notif = false; } }
  build() {
    const g = this.g, N = this.order.length - 1;
    const steps = h("div.row.gap4.pad.mt8", Array.from({ length: N }, (_, i) => h("div.grow", { style: { height: "3px", borderRadius: "2px", background: i <= this.step ? "var(--ink)" : "var(--rule)" } })));
    let content, cta = "Next", ctaDo = () => this.next();
    switch (this.order[this.step]) {
      case 0:
        content = [h("div.h1", "How many pages a day?"), h("p.muted.mt8", "You can change it any time. Ten pages take about fifteen minutes."),
          h("div.row.wrap.gap8.mt20", [5, 10, 15, 20, 30, 50].map(n => h("button.chip" + (g.daily === n ? ".on" : ""), { onclick: () => { g.daily = n; Nav.refresh(); } }, n))),
          h("p.small.faint.mt12", `${g.daily} pages, about ${Math.round(g.daily * 1.5)} minutes.`)];
        break;
      case 1:
        content = [h("div.h1", "Which days will you read?"), h("p.muted.mt8", "The days you leave out are planned rest. They don't count against you."),
          h("div.row.gap6.mt20", [1, 2, 3, 4, 5, 6, 0].map(d => h("button.chip.day" + (g.days.includes(d) ? ".on" : ""), { "aria-label": Fmt.Days[d], onclick: () => { g.days.includes(d) ? g.days.splice(g.days.indexOf(d), 1) : g.days.push(d); this.err = null; Nav.refresh(); } }, Copy.DayLetters[d]))),
          h("p.small.faint.mt12", g.days.length ? `${F.pages(g.daily * g.days.length)} a week, ${Fmt.dayList(g.days)}.` : "Pick at least one day.")];
        break;
      case 2: {
        const intent = h("input.input", { value: g.intention, placeholder: "finish dinner", maxlength: 50 }); intent.addEventListener("input", () => { g.intention = intent.value; });
        content = [h("div.h1", "When will you read?"), h("p.muted.mt8", "We send a reminder at that time, only on the days you picked, and never if you already logged."),
          h("div.stepper.mt20", h("button.circle", { "aria-label": "One hour earlier", onclick: () => { g.hour = (g.hour + 23) % 24; Nav.refresh(); } }, "−"),
            h("div.bignum", { style: { lineHeight: "76px" } }, Fmt.time(g.hour, g.min)),
            h("button.circle", { "aria-label": "One hour later", onclick: () => { g.hour = (g.hour + 1) % 24; Nav.refresh(); } }, "+")),
          h("div.row.gap8.mt12", { style: { justifyContent: "center" } }, [0, 15, 30, 45].map(m => h("button.chip" + (g.min === m ? ".on" : ""), { onclick: () => { g.min = m; Nav.refresh(); } }, ":" + String(m).padStart(2, "0")))),
          h("label.field.mt24", h("span.lab", "When I... (optional)"), intent),
          h("div.row.wrap.gap6.mt8", Copy.IntentionSuggestions.map(s => h("button.pillbtn" + (g.intention === s ? ".on" : ""), { onclick: () => { g.intention = s; Nav.refresh(); } }, s)))];
        break;
      }
      case 3:
        content = [h("div.h1", "What are you reading?"), h("p.muted.mt8", "Pick one. If you already started it, you can enter the page later."),
          h("div.grid3.mt16", Catalog.data.books.map(b => h("div.col.gap4", { onclick: () => { g.book = b.id; this.err = null; Nav.refresh(); } },
            cover(b, 110, { style: { width: "100%", outline: g.book === b.id ? "3px solid var(--accent)" : "none", outlineOffset: "2px" } }),
            h("div.xs.clamp2" + (g.book === b.id ? ".strong" : ".muted"), b.title))))];
        break;
      case 4:
        content = [h("div.h1", "How many books this year?"), h("p.muted.mt8", `${12 - Clock.today().getMonth()} months of ${Clock.today().getFullYear()} left. The goal only shows in your diary.`),
          h("div.row.wrap.gap8.mt20", [4, 6, 12, 24, 52].map(n => h("button.chip" + (g.year === n ? ".on" : ""), { onclick: () => { g.year = n; Nav.refresh(); } }, n))),
          h("p.small.faint.mt12", g.year >= 24 ? `${g.year} books is about ${Math.round(52 / g.year * 7)} days per book.` : `One book every ${Math.round(52 / g.year)} weeks.`)];
        break;
      case 5:
        content = [h("div.h1", "Reminders"), h("p.muted.mt8", "Four a week at most, with a line from the book you are reading. If you ignore them, we write less."),
          h("div.card.block.mt20", h("div.row.gap10", h("div.appic", { style: { width: "38px", height: "38px", borderRadius: "9px", background: "#C8402A", color: "#fff", display: "grid", placeItems: "center", fontFamily: "var(--serif)", fontWeight: 600, fontSize: "22px" } }, "m"),
            h("div.grow", h("div.small.strong", g.intention ? Copy.whenI(g.intention) : "Your book is waiting"), h("div.small.muted", "“Let others boast of the pages they have written; I am proud of those I have read.” Borges")))),
          g.notif === false ? h("p.small.mt12", { style: { color: "var(--error)" } }, "Without permission we can't remind you. You can turn it on later in iPhone Settings.") : null];
        cta = g.notif == null ? "Turn on reminders" : "Next";
        ctaDo = () => g.notif == null ? iosAlert("“Margin” Would Like to Send You Notifications", "Notifications may include alerts and sounds. You can change this in Settings.", [
          { label: "Don’t Allow", do: () => { g.notif = false; Nav.refresh(); } }, { label: "Allow", bold: true, do: () => { g.notif = true; this.next(); } }]) : this.next();
        break;
      default: {
        const b = Data.book(g.book);
        content = [h("div.h1", this.acc.isGuest ? "All set" : `All set, ${this.acc.name.split(" ")[0]}`), h("p.muted.mt8", "Here is your plan. You can change all of it in Settings."),
          h("div.list.mt20", { style: { margin: "20px 0 0" } },
            h("div.item", h("span.grow", "Daily goal"), h("span.val", F.pages(g.daily))),
            h("div.item", h("span.grow", "Days"), h("span.val", Fmt.dayList(g.days))),
            h("div.item", h("span.grow", "Reminder"), h("span.val", g.notif ? Fmt.time(g.hour, g.min) : "Off")),
            h("div.item", h("span.grow", "Yearly goal"), h("span.val", F.books(g.year))),
            h("div.item", cover(b, 44), h("div.grow", h("div.small.faint", "Reading"), h("div.strong", b.title))))];
        cta = "Start"; ctaDo = () => this.finish();
      }
    }
    return h("div", navbar("", { onBack: () => { if (this.step === 0) { iosAlert("Leave setup?", this.acc.isGuest ? "We'll use these settings. You can change them later." : "Your account is already created. You can finish this later.", [{ label: "Stay here" }, { label: "Leave", do: () => this.finish(true) }]); } else { this.step--; Nav.refresh(); } } }),
      steps, h("div.scroll.under-nav", h("div.pad.mt20", content)),
      h("div.pad", { style: { padding: "12px 16px 40px", borderTop: "1px solid var(--rule)" } }, h("div.help.bad", this.err || ""), h("button.btn.accent", { onclick: ctaDo }, cta)));
  }
  next() {
    const g = this.g;
    const k = this.order[this.step];
    if (k === 1 && !g.days.length) { this.err = "Pick at least one reading day."; return Nav.refresh(); }
    if (k === 3 && !g.book) { this.err = "Pick a book. You can change it any time."; return Nav.refresh(); }
    this.step++; Nav.refresh();
  }
  finish(skip) {
    const s = this.acc.state, g = this.g;
    s.profile.createdOn = D.key(Clock.today()); s.profile.onboardingComplete = true;
    Object.assign(s.profile.goals, { dailyPages: g.daily, readingDays: g.days.length ? g.days : [1, 2, 3, 4, 5], reminderHour: g.hour, reminderMinute: g.min, remindersEnabled: !!g.notif, intention: g.intention || null });
    s.yearGoal = g.year;
    ReadingService.setCurrentBook(s, g.book || "the-little-prince");
    s.notifications = [{ at: D.stamp(Clock.now()), kind: "welcome", text: "Your diary starts today. Log your first reading with the + button." }];
    Net.log({ kind: "joined" }); Data.save(); Net.flush(); App.start(); if (!skip) toast(this.acc.isGuest ? "You're in as a guest" : "Account created");
  }
}

/* Guests keep everything they logged: this adds an email and password to the same account. */
class SaveAccountScreen extends SignupScreen {
  constructor() { super(); const a = Accounts.current(); this.v.name = a && a.name && a.name !== "Guest" ? a.name : ""; }
  build() {
    const v = this.v;
    const a = this.avail[v.username];
    const userErr = V.username(v.username) || (a === false ? `@${v.username} is taken. Try another one.` : null);
    const errs = { name: v.name.trim().length < 2 ? "Enter your name." : null, email: V.email(v.email), username: userErr, password: V.password(v.password) };
    const ok = !Object.values(errs).some(Boolean) && a === true && !this.busy;
    const inp = (k, attrs) => {
      const e = h("input.input" + (this.touched[k] && errs[k] ? ".bad" : this.touched[k] && !errs[k] ? ".good" : ""), { value: v[k], ...attrs });
      e.addEventListener("input", () => { v[k] = e.value; if (k === "username") this.checkName(); this.update(); });
      e.addEventListener("blur", () => { this.touched[k] = true; this.update(); });
      return e;
    };
    const helpEl = (k, def) => h("div.help" + (this.touched[k] && errs[k] ? ".bad" : ""), this.touched[k] && errs[k] ? errs[k] : def || "");
    const userHelp = !v.username ? h("div.help", "Friends find you by this name.")
      : userErr && (this.touched.username || a === false) ? h("div.help.bad", userErr)
      : userErr ? h("div.help", "") : a === true ? h("div.help.good", `@${v.username} is available.`)
      : a === null ? h("div.help.bad", "Couldn't check right now. Try again in a moment.") : h("div.help", "Checking…");
    const s = Data.state(), logged = s ? s.logs.reduce((n, l) => n + Math.max(0, l.toPage - l.fromPage), 0) : 0;
    return h("div", navbar(""), h("div.scroll.under-nav", h("div.pad",
      h("div.h1", "Save your account"),
      h("p.muted.mt6", logged ? `You keep the ${F.pages(logged)} you logged, your book and your friends.` : "You keep your book, your goals and your friends."),
      field("Name", inp("name", { autocomplete: "name", placeholder: "How you want to be seen" }), helpEl("name")),
      field("Email", inp("email", { type: "email", autocomplete: "email", inputmode: "email", placeholder: "you@email.com" }), helpEl("email")),
      field("Username", h("div.inwrap", inp("username", { autocapitalize: "none", placeholder: "no spaces", oninput: e => { e.target.value = e.target.value.toLowerCase().replace(/\s/g, ""); } })), userHelp),
      field("Password", h("div.inwrap", inp("password", { type: this.show ? "text" : "password", autocomplete: "new-password", placeholder: "" }),
        h("button.iconbtn.trail", { type: "button", "aria-label": this.show ? "Hide password" : "Show password", onclick: () => { this.show = !this.show; this.update(); } }, icon(this.show ? "eyeoff" : "eye"))),
        helpEl("password", "At least 8 characters, with at least one number.")),
      this.err ? h("div.help.bad.mt12", { role: "alert" }, this.err) : null,
      h("button.btn.accent.mt24", { disabled: !ok, onclick: () => this.submit() }, this.busy ? "Saving…" : "Save account"),
      h("div.bottom-space"))));
  }
  async submit() {
    this.busy = true; this.err = null; this.update();
    let e;
    try { e = await Net.saveGuest(this.v); } catch (x) { e = "Couldn't reach Margin. Check your connection."; }
    this.busy = false;
    if (e && e.pending) { this.update(); return iosAlert("Check your email", `We sent a link to ${e.pending}. Open it on this device to finish saving your account. Until then, don't log out.`, [{ label: "Close", bold: true, do: () => Nav.back() }]); }
    if (e) { this.err = e; return this.update(); }
    toast(`Saved. You're @${this.v.username} now.`);
    Nav.back();
  }
}

/* A gentle reminder on Home, Settings and Profile while the reader is a guest. */
function guestNote(compact) {
  const a = Accounts.current(); if (!a || !a.isGuest) return null;
  if (a.pendingEmail) return h("div.card.block.guestnote", { style: { margin: compact ? "8px 16px 0" : "12px 16px 0" } },
    h("p.small", `Open the link we sent to ${a.pendingEmail} to finish saving your account.`));
  return h("div.card.block.guestnote" + (compact ? "" : ".mt12"), { style: { margin: compact ? "8px 16px 0" : "12px 16px 0" } },
    h("p.small", "You're trying Margin as a guest. Save your account to keep your reading."),
    h("button.btn.sm.primary.mt8", { onclick: () => Nav.push(new SaveAccountScreen()) }, "Save your account"));
}
