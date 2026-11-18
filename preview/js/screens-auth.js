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
          h("button.btn.secondary", { disabled: this.busy, onclick: () => App.tryGuest(this) }, this.busy ? "Opening the demo…" : "Try a demo account"),
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
  // What would make a password stronger, in words (the bars alone weren't clear in user testing, T5).
  strengthTip(p) {
    if (!p) return "";
    if (p.length < 8) return "Use at least 8 characters.";
    if (!/\d/.test(p)) return "Add a number.";
    if (!(/[A-Z]/.test(p) && /[a-z]/.test(p))) return "Mix uppercase and lowercase to make it stronger.";
    if (!(/[^A-Za-z0-9]/.test(p) || p.length >= 12)) return "Add a symbol or make it longer to make it stronger.";
    return "Good to go.";
  },
  password(p) { return p.length < 8 ? "At least 8 characters." : !/\d/.test(p) ? "Add at least one number." : null; },
};

function field(label, input, help) { return h("label.field.mt16", h("span.lab", label), input, help); }

/* What the sign up form still needs, as one short sentence for a toast. */
function missingText(v, errs, avail, terms) {
  const m = [];
  if (errs.name) m.push("your name");
  if (errs.email) m.push("a valid email");
  if (!v.username || V.username(v.username)) m.push("a username");
  else if (avail === false) m.push("a different username");
  if (errs.password) m.push("a password (8+ characters, one number)");
  if (terms === false) m.push("accepting the terms");
  if (!m.length) return avail === undefined ? "Still checking your username. Try again in a second." : null;
  const list = m.length === 1 ? m[0] : m.slice(0, -1).join(", ") + " and " + m[m.length - 1];
  return "Still missing: " + list + ".";
}

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
        passwordMeter(v.password, st),
        helpEl("password", "At least 8 characters, with at least one number.")),
      field("University (optional)", inp("university", { autocomplete: "organization", placeholder: "Universidad Panamericana", maxlength: 80 }), h("div.help", "We suggest friends from the same place.")),
      h("button.check.mt20" + (this.terms ? ".on" : ""), { onclick: () => { this.terms = !this.terms; this.update(); } },
        h("span.box", this.terms ? icon("check") : null), h("span", "I accept the ", h("span.link", "Terms of Use"), " and the ", h("span.link", "Privacy Notice"), ". I am 13 or older.")),
      this.err ? h("div.help.bad.mt12", this.err) : null,
      h("button.btn.accent.mt24" + (ok ? "" : ".soft"), { disabled: this.busy, onclick: () => this.trySubmit() }, this.busy ? "Creating…" : "Create account"),
      h("div.bottom-space"));
    return h("div", navbar(""), h("div.scroll.under-nav", body));
  }
  update() { if (Nav.current() !== this || !this.el) return; const fresh = this.build(); const a = [...this.el.childNodes], b = [...fresh.childNodes]; b.forEach((n, j) => j < a.length ? morph(a[j], n) : this.el.append(n)); a.slice(b.length).forEach(n => n.remove()); }
  // Read at tap time: the button element is kept between updates, so it must not rely on values from build().
  trySubmit() {
    if (this.busy) return;
    const v = this.v, a = this.avail[v.username], needTerms = !(this instanceof SaveAccountScreen);
    const errs = { name: v.name.trim().length < 2 ? "x" : null, email: V.email(v.email), username: V.username(v.username) || (a === false ? "x" : null), password: V.password(v.password) };
    if (!Object.values(errs).some(Boolean) && (!needTerms || this.terms) && a === true) return this.submit();
    // Not complete yet: mark every field and say what is left, without blocking.
    ["name", "email", "username", "password"].forEach(k => { this.touched[k] = true; });
    this.update();
    const t = missingText(v, errs, a, needTerms ? this.terms : null); if (t) toast(t);
  }
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
const SETUP_STEPS = 7; // pages a day, days, time, book, year goal, reminders, all set
class SetupScreen extends Screen {
  constructor(acc) { super(); this.acc = acc; this.step = 0; this.g = { daily: PLAN.daily, days: [...PLAN.days], hour: PLAN.hour, min: PLAN.minute, intention: "", book: null, noBook: false, year: PLAN.year, notif: null }; this.err = null; }
  build() {
    const g = this.g, N = SETUP_STEPS - 1;
    const steps = h("div.row.gap4.pad.mt8", Array.from({ length: N }, (_, i) => h("div.grow", { style: { height: "3px", borderRadius: "2px", background: i <= this.step ? "var(--ink)" : "var(--rule)" } })));
    // later: the text button under the main one. On optional steps it skips with the defaults (laterDo).
    let content, cta = "Next", ctaDo = () => this.next(), later = "Skip for now", laterDo = null;
    switch (this.step) {
      case 0:
        content = [h("div.h1", "How many pages a day?"), h("p.muted.mt8", "You can change it any time. Ten pages take about fifteen minutes."),
          h("div.row.wrap.gap8.mt20", PLAN.dailyOptions.map(n => h("button.chip" + (g.daily === n ? ".on" : ""), { onclick: () => { g.daily = n; Nav.refresh(); } }, n))),
          h("p.small.faint.mt12", `${g.daily} pages, about ${Math.round(g.daily * 1.5)} minutes.`)];
        later = "Set up later"; laterDo = () => this.later();
        break;
      case 1:
        content = [h("div.h1", "Which days will you read?"), h("p.muted.mt8", "The days you leave out are planned rest. They don't count against you."),
          h("div.row.gap6.mt20", [1, 2, 3, 4, 5, 6, 0].map(d => h("button.chip.day" + (g.days.includes(d) ? ".on" : ""), { "aria-label": Fmt.Days[d], onclick: () => { g.days.includes(d) ? g.days.splice(g.days.indexOf(d), 1) : g.days.push(d); this.err = null; Nav.refresh(); } }, Copy.DayLetters[d]))),
          h("p.small.faint.mt12", g.days.length ? `${F.pages(g.daily * g.days.length)} a week, ${Fmt.dayList(g.days)}.` : "Pick at least one day.")];
        laterDo = () => { g.days = [...PLAN.days]; this.skip(); };
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
        laterDo = () => { g.hour = 22; g.min = 0; g.intention = ""; this.skip(); };
        break;
      }
      case 3:
        content = [h("div.h1", "What are you reading?"), h("p.muted.mt8", "Pick one. If you already started it, you can enter the page later."),
          h("button.check.mt16" + (g.noBook ? ".on" : ""), { onclick: () => { g.noBook = !g.noBook; if (g.noBook) g.book = null; this.err = null; Nav.refresh(); } },
            h("span.box", g.noBook ? icon("check") : null), h("span", "I'm not reading anything right now")),
          g.noBook ? h("p.small.muted.mt8", "No problem. You can choose your first book from Home: a few quick questions match you with one from the catalog.") : null,
          bookSearch("Search by title or author"),
          h("div.grid3.mt16", Catalog.data.books.map(b => h("div.col.gap4", { "data-q": searchKey(b), onclick: e => this.pickBook(b.id, e.currentTarget) },
            cover(b, 110, { style: { width: "100%", outline: g.book === b.id ? "3px solid var(--accent)" : "none", outlineOffset: "2px" } }),
            h("div.xs.clamp2" + (g.book === b.id ? ".strong" : ".muted"), b.title))))];
        later = "Choose later"; laterDo = () => this.chooseLater();
        break;
      case 4:
        content = [h("div.h1", "How many books this year?"), h("p.muted.mt8", `${12 - Clock.today().getMonth()} months of ${Clock.today().getFullYear()} left. The goal only shows in your diary.`),
          h("div.row.wrap.gap8.mt20", PLAN.yearOptions.map(n => h("button.chip" + (g.year === n ? ".on" : ""), { onclick: () => { g.year = n; Nav.refresh(); } }, n))),
          h("p.small.faint.mt12", g.year >= 24 ? `${g.year} books is about ${Math.round(52 / g.year * 7)} days per book.` : `One book every ${Math.round(52 / g.year)} weeks.`)];
        laterDo = () => { g.year = 12; this.skip(); };
        break;
      case 5:
        content = [h("div.h1", "Reminders"), h("p.muted.mt8", "Four a week at most, with a line from the book you are reading. If you ignore them, we write less."),
          h("div.card.block.mt20", h("div.row.gap10", h("div.appic", { style: { width: "38px", height: "38px", borderRadius: "9px", background: "#C8402A", color: "#fff", display: "grid", placeItems: "center", fontFamily: "var(--serif)", fontWeight: 600, fontSize: "22px" } }, "m"),
            h("div.grow", h("div.small.strong", g.intention ? Copy.whenI(g.intention) : "Your book is waiting"), h("div.small.muted", "“Let others boast of the pages they have written; I am proud of those I have read.” Borges")))),
          g.notif === false ? h("p.small.mt12", { style: { color: "var(--error)" } }, "Without permission we can't remind you. You can turn it on later in iPhone Settings.") : null];
        cta = g.notif == null ? "Turn on reminders" : "Next";
        laterDo = g.notif == null ? () => { g.notif = false; this.skip(); } : null;
        ctaDo = () => g.notif == null ? iosAlert("“Margin” Would Like to Send You Notifications", "Notifications may include alerts and sounds. You can change this in Settings.", [
          { label: "Don’t Allow", do: () => { g.notif = false; Nav.refresh(); } }, { label: "Allow", bold: true, do: () => { g.notif = true; this.next(); } }]) : this.next();
        break;
      default: {
        const b = Data.book(g.book);
        content = [h("div.h1", `All set, ${this.acc.name.split(" ")[0]}`), h("p.muted.mt8", "Here is your plan. You can change all of it in Settings."),
          h("div.list.mt20", { style: { margin: "20px 0 0" } },
            h("div.item", h("span.grow", "Daily goal"), h("span.val", F.pages(g.daily))),
            h("div.item", h("span.grow", "Days"), h("span.val", Fmt.dayList(g.days))),
            h("div.item", h("span.grow", "Reminder"), h("span.val", g.notif ? Fmt.time(g.hour, g.min) : "Off")),
            h("div.item", h("span.grow", "Yearly goal"), h("span.val", F.books(g.year))),
            b ? h("div.item", cover(b, 44), h("div.grow", h("div.small.faint", "Reading"), h("div.strong", b.title)))
              : h("div.item", h("span.grow", "Reading"), h("span.val", "Choose it on Home")))];
        cta = "Start"; ctaDo = () => this.finish();
      }
    }
    return h("div", navbar("", { onBack: () => { if (this.step === 0) { iosAlert("Leave setup?", "Your account is already created. You can finish this later.", [{ label: "Stay here" }, { label: "Leave", do: () => { this.g.book = null; this.g.noBook = true; this.finish(true); } }]); } else { this.step--; Nav.refresh(); } } }),
      steps, h("div.scroll.under-nav", h("div.pad.mt20", content)),
      h("div.footbar.ruled", h("div.help.bad", this.err || ""), h("button.btn.accent", { onclick: ctaDo }, cta),
        laterDo ? h("button.btn.ghost.mt4", { onclick: laterDo }, later) : null));
  }
  // Optional step left for later: its defaults are already set, move on.
  skip() { this.err = null; this.step++; Nav.refresh(); }
  // "Set up later" on the first step: the same defaults as the guest flow, no book yet, straight to Home.
  later() {
    const g = this.g;
    Object.assign(g, { daily: PLAN.skipDaily, days: [...PLAN.days], hour: PLAN.hour, min: PLAN.minute, intention: "", year: PLAN.year, notif: false, book: null, noBook: true });
    this.finish(true);
    toast("You can set your goals any time in Settings");
  }
  // Book step: marks the picked cover in place (no rebuild), so a typed search and the scroll stay.
  pickBook(id, el) {
    const g = this.g, rebuild = g.noBook || this.err;
    g.book = id; g.noBook = false; this.err = null;
    if (rebuild) return Nav.refresh();
    el.parentNode.querySelectorAll("[data-q]").forEach(x => {
      const on = x === el;
      x.firstChild.style.outline = on ? "3px solid var(--accent)" : "none";
      x.lastChild.classList.toggle("strong", on); x.lastChild.classList.toggle("muted", !on);
    });
  }
  // "Choose later" on the book step: no book, the defaults for whatever is left, straight to Home.
  chooseLater() {
    const g = this.g;
    g.book = null; g.noBook = true;
    this.finish(true);
    toast("Choose your first book from Home whenever you're ready");
  }
  next() {
    const g = this.g;
    const k = this.step;
    if (k === 1 && !g.days.length) { this.err = "Pick at least one reading day."; return Nav.refresh(); }
    if (k === 3 && !g.book && !g.noBook) { this.err = "Pick a book, or tap Choose later."; return Nav.refresh(); }
    this.step++; Nav.refresh();
  }
  finish(skip) {
    const s = this.acc.state, g = this.g;
    s.profile.createdOn = D.key(Clock.today()); s.profile.onboardingComplete = true;
    Object.assign(s.profile.goals, { dailyPages: g.daily, readingDays: g.days.length ? g.days : [...PLAN.days], reminderHour: g.hour, reminderMinute: g.min, remindersEnabled: !!g.notif, intention: g.intention || null });
    s.yearGoal = g.year;
    if (g.book) ReadingService.setCurrentBook(s, g.book); // no book: Home shows "Choose your first book"
    s.notifications = [{ at: D.stamp(Clock.now()), kind: "welcome", text: "Your diary starts today. Log your first reading with the + button." }];
    Net.log({ kind: "joined" }); Data.save(); Net.flush(); App.start(); if (!skip) toast("Account created");
  }
}

/* ---- Taste quiz: ten quick questions, one per screen, then books from the catalog (TasteQuiz in core.js) ----
   Opened from Home while there is no current book ("Choose your first book") and from Explore. Every question
   can be skipped, and saved answers open straight on the results, where
   "Change my answers" edits them. */
const QUIZ_QUESTIONS = [
  { key: "field", kind: "one", title: "What do you study or do?", sub: "We'll look for books that speak to your field.", options: () => TasteQuiz.Fields },
  { key: "level", kind: "one", title: "How much do you read these days?", sub: "Be honest: it helps us pick the right first book.", options: () => TasteQuiz.Levels },
  { key: "life", kind: "many", title: "What's going on in your life right now?", sub: "Pick any that fit. A book can keep you company.", options: () => TasteQuiz.Life },
  { key: "feel", kind: "many", title: "How do you want to feel when you read?", sub: "Pick as many as you like.", options: () => TasteQuiz.Moods },
  { key: "screen", kind: "many", title: "What do you like to watch?", sub: "Films and series are a good bridge to books.", options: () => TasteQuiz.Screens },
  { key: "pace", kind: "one", title: "Quick and gripping, or slow and deep?", sub: "How you like a story to move.", options: () => TasteQuiz.Paces },
  { key: "length", kind: "one", title: "How long a book?", sub: "Ten pages take about fifteen minutes.", options: () => TasteQuiz.Lengths },
  { key: "era", kind: "one", title: "Classics or recent books?", sub: "The catalog has both, from Homer to 2021.", options: () => TasteQuiz.Eras },
  { key: "region", kind: "one", title: "Stories from Mexico and Latin America?", sub: "Mexican and Latin American classics and new voices.", options: () => TasteQuiz.Regions },
  { key: "liked", kind: "books", title: "Already read any of these and liked it?", sub: "We'll look for books like them and won't suggest them again." },
];

class TasteQuizScreen extends Screen {
  constructor(opts = {}) {
    super(); this.opts = opts;
    const s = Data.state(), saved = s && s.tastes;
    this.t = TasteQuiz.clean(saved);
    // Saved answers open on the results; a first visit (or answers that were all skipped) starts at question 1.
    this.step = saved && !TasteQuiz.isEmpty(this.t) && !opts.edit ? QUIZ_QUESTIONS.length : 0;
  }
  build() {
    const t = this.t, last = QUIZ_QUESTIONS.length, q = QUIZ_QUESTIONS[this.step];
    let content, foot;
    if (this.finding) return h("div", navbar("", { onBack: () => { this.finding = false; this.step = last - 1; Nav.refresh(); } }), findingView());
    if (q) {
      const answered = q.kind === "one" ? t[q.key] != null : t[q.key].length > 0;
      let body;
      if (q.kind === "one") {
        body = h("div.list", { style: { margin: "20px 0 0" } }, q.options().map(o => h("button.item", { onclick: () => { t[q.key] = o.id; this.go(this.step + 1); } },
          h("div.grow", h("div", o.label), o.hint ? h("div.xs.faint", o.hint) : null), t[q.key] === o.id ? icon("check") : icon("chev", "chev"))));
      } else if (q.kind === "many") {
        body = h("div.row.wrap.gap8.mt20", q.options().map(o => h("button.chip" + (t[q.key].includes(o.id) ? ".on" : ""), { onclick: () => this.toggle(q.key, o.id) }, o.label)));
      } else {
        body = h("div.grid3.mt20", Catalog.data.books.map(b => {
          const on = t.liked.includes(b.id);
          return h("div.col.gap4", { onclick: () => this.toggle("liked", b.id) },
            cover(b, 110, { style: { width: "100%", outline: on ? "3px solid var(--accent)" : "none", outlineOffset: "2px" } }),
            h("div.xs.clamp2" + (on ? ".strong" : ".muted"), b.title));
        }));
      }
      content = [h("div.caps", `Question ${this.step + 1} of ${last}`), h("div.h1.mt6", q.title), h("p.muted.mt8", q.sub), body];
      const next = this.step === last - 1 ? "See my books" : "Next";
      foot = answered ? [h("button.btn.accent", { onclick: () => this.go(this.step + 1) }, next)]
        : [h("button.btn.secondary", { onclick: () => { if (q.kind === "one") t[q.key] = null; this.go(this.step + 1); } }, "Skip this question")];
    } else {
      const shown = TasteQuiz.results(t, TasteQuiz.excludeFor(Data.state()));
      content = [h("div.caps", "Your match"), matchList(this, shown, b => this.open(b), { onStart: b => this.start(b.id) })];
      foot = [h("button.btn.secondary", { onclick: () => { this.step = 0; Nav.refresh(); } }, "Change my answers")];
    }
    const progress = q ? h("div.pad.mt8", bar((this.step + 1) / last, "ink")) : null;
    return h("div", navbar("", { onBack: () => { if (this.step > 0 && q) { this.step--; Nav.refresh(); } else Nav.back(); } }),
      progress, h("div.scroll.under-nav", h("div.pad.mt20", content, h("div.bottom-space"))),
      h("div.footbar.ruled", foot));
  }
  // Multiple choice: "Nothing special" clears the other life answers, and any other answer clears it.
  toggle(key, id) {
    const list = this.t[key];
    if (list.includes(id)) list.splice(list.indexOf(id), 1);
    else {
      if (key === "life" && id === TasteQuiz.None) list.length = 0;
      if (key === "life" && id !== TasteQuiz.None && list.includes(TasteQuiz.None)) list.splice(list.indexOf(TasteQuiz.None), 1);
      list.push(id);
    }
    Nav.refresh();
  }
  // The first book (nothing finished yet) opens the book page in onboarding mode: just "Read this".
  open(b) {
    const first = !Data.finished(Data.state()).length;
    Nav.push(new BookScreen(b.id, first ? { onboarding: true, onPick: () => this.start(b.id) } : {}));
  }
  go(n) {
    this.step = n;
    // The answers are saved with the diary (state.tastes) once the reader sees the results.
    if (n === QUIZ_QUESTIONS.length) {
      const s = Data.state(); if (s) { s.tastes = TasteQuiz.clean(this.t); Data.save(); }
      this.sel = 0; this.finding = true;
      setTimeout(() => { if (!this.finding) return; this.finding = false; if (Nav.current() === this) Nav.refresh(); }, TIMING.findingMatchMs);
    }
    Nav.refresh();
  }
  start(id) { Nav.push(new StartBookScreen(id)); }
}

/* ---- "Find your next book": five new questions for readers with a history (NextBook in core.js).
   The ranking also learns from what they finished, rated, dropped and want to read. Results reuse matchList. ---- */
class NextBookScreen extends Screen {
  constructor() { super(); this.a = {}; this.step = 0; this.sel = 0; }
  build() {
    if (this.finding) return h("div", navbar("", { onBack: () => { this.finding = false; this.step--; Nav.refresh(); } }), findingView());
    const Q = NextBook.Questions, q = Q[this.step];
    let content, foot = null;
    if (q) {
      const pick = id => { this.a[q.key] = id; this.step++; if (this.step === Q.length) this.think(); Nav.refresh(); };
      content = [h("div.caps", `Question ${this.step + 1} of ${Q.length}`), h("div.h1.mt6", q.title), h("p.muted.mt8", q.sub),
        h("div.list", { style: { margin: "20px 0 0" } }, q.options().map(o => h("button.item", { onclick: () => pick(o.id) },
          h("div.grow", h("div", o.label), o.hint ? h("div.xs.faint", o.hint) : null), icon("chev", "chev"))))];
      foot = h("div.footbar.ruled", h("button.btn.secondary", { onclick: () => pick(null) }, "Skip this question"));
    } else {
      const recs = NextBook.recommend(Data.state(), this.a, Catalog.data);
      content = [h("div.caps", "Your next book"), matchList(this, recs, b => Nav.push(new BookScreen(b.id)), { why: true, onStart: b => Nav.push(new StartBookScreen(b.id)) })];
      foot = h("div.footbar.ruled", h("button.btn.secondary", { onclick: () => { this.step = 0; this.sel = 0; Nav.refresh(); } }, "Change my answers"));
    }
    return h("div", navbar("", { onBack: () => { if (this.step > 0) { this.step--; this.sel = 0; Nav.refresh(); } else Nav.back(); } }),
      q ? h("div.pad.mt8", bar((this.step + 1) / Q.length, "ink")) : null,
      h("div.scroll.under-nav", h("div.pad.mt20", content, h("div.bottom-space"))), foot);
  }
  // After the last answer: "Finding your match…" for a moment, then the results (same as the first-book quiz).
  think() {
    this.sel = 0; this.finding = true;
    setTimeout(() => { if (!this.finding) return; this.finding = false; if (Nav.current() === this) Nav.refresh(); }, TIMING.findingMatchMs);
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
      h("button.btn.accent.mt24" + (ok ? "" : ".soft"), { disabled: this.busy, onclick: () => this.trySubmit() }, this.busy ? "Saving…" : "Save account"),
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
    h("p.small", "This is a demo account with six weeks of sample reading. Save it to keep it, or log out and sign up to start fresh."),
    h("button.btn.sm.primary.mt8", { onclick: () => Nav.push(new SaveAccountScreen()) }, "Save your account"));
}
