/* Settings (iOS-style grouped list) and subscreens. */
"use strict";

function row(label, opts = {}) {
  const tag = opts.onclick ? "button.item" : "div.item";
  return h(tag + (opts.danger ? ".danger" : ""), { onclick: opts.onclick },
    opts.ic ? h("span.ic", { style: { background: opts.color || "#5B554C" } }, icon(opts.ic)) : null,
    h("div.grow", h("div", label), opts.sub ? h("div.xs.faint", opts.sub) : null),
    opts.value != null ? h("span.val", opts.value) : null, opts.control || null, opts.onclick && !opts.noChev ? icon("chev", "chev") : null);
}
function group(title, rows, foot) { return [title ? h("div.grouphead", title) : h("div", { style: { height: "18px" } }), h("div.list", rows.filter(Boolean)), foot ? h("div.groupfoot", foot) : null]; }

class SettingsScreen extends Screen {
  build() {
    const a = Accounts.current(), s = a.state, g = s.profile.goals, st = a.settings;
    return h("div", navbar("Settings"), h("div.scroll.under-nav",
      h("button.row.gap12.list", { style: { padding: "12px 14px", width: "calc(100% - 32px)", textAlign: "left", marginTop: "8px" }, onclick: () => Nav.push(new SettingsDetail("account")) },
        avatar(Data.me(), 44), h("div.grow", h("div.strong", a.name), h("div.small.muted", a.email ? `@${a.username} · ${a.email}` : `@${a.username} · guest`)), icon("chev", "chev")),
      a.isGuest ? group("", [row("Save your account", { ic: "user", color: "#C8402A", sub: "Add an email and password to keep your reading", onclick: () => Nav.push(new SaveAccountScreen()) })],
        "You're trying Margin as a guest. If you log out before saving, this reading is lost.") : null,
      group("", [
        row("Profile", { ic: "user", color: "#3E5C76", onclick: () => Nav.push(new SettingsDetail("profile")) }),
        row("Privacy", { ic: "lock", color: "#5B554C", value: st.publicProfile ? "Public" : "Private", onclick: () => Nav.push(new SettingsDetail("privacy")) }),
      ]),
      group("Reading", [
        row("Goals", { ic: "bookopen", color: "#C8402A", value: `${g.dailyPages} pages a day`, onclick: () => Nav.push(new SettingsDetail("reading")) }),
        row("Reminders", { ic: "bell", color: "#B5893B", value: g.remindersEnabled ? Fmt.time(g.reminderHour, g.reminderMinute) : "Off", onclick: () => Nav.push(new SettingsDetail("reminders")) }),
        row("Focus mode", { ic: "moon", color: "#6B4E71", value: `${st.focusMinutes} min`, onclick: () => Nav.push(new SettingsDetail("focus")) }),
      ]),
      group("App", [
        row("Appearance", { ic: "sun", color: "#2F6B47", value: st.theme, onclick: () => Nav.push(new SettingsDetail("appearance")) }),
        row("Friends", { ic: "users", color: "#8A5A44", value: Social.incoming.length ? `${Social.incoming.length} new` : (Social.friends.length ? String(Social.friends.length) : null), onclick: () => Nav.push(new FriendsScreen()) }),
      ]),
      group("Your data", [
        row("Export my data", { ic: "download", color: "#4A6B5D", sub: "CSV with your diary, ratings and reviews", onclick: () => exportCsv() }),
        row("Import from Goodreads", { ic: "upload", color: "#4A6B5D", onclick: () => importGoodreads() }),
      ]),
      group("Help", [
        row("Help center", { ic: "help", color: "#5B554C", onclick: () => Nav.push(new HelpScreen()) }),
        row("Write to us", { ic: "mail", color: "#5B554C", value: "0262939@up.edu.mx", noChev: true, onclick: () => { location.href = "mailto:0262939@up.edu.mx?subject=" + encodeURIComponent("Margin feedback"); } }),
        row("Terms and privacy", { ic: "doc", color: "#5B554C", onclick: () => Nav.push(new LegalScreen()) }),
      ]),
      group("Presentation", [
        row("Simulate next day", { value: Clock.isSimulated ? `+${Clock.dayOffset} d` : null, noChev: true, onclick: () => { Clock.advanceDay(); toast(`It's now ${Fmt.dateLong(Clock.today())}.`); Nav.refresh(); } }),
        Clock.isSimulated ? row("Back to the real clock", { noChev: true, onclick: () => { Clock.resetSimulation(); Nav.refresh(); } }) : null,
        row("Show a reminder", { noChev: true, onclick: () => App.demoBanner() }),
      ], "Tools for the class demo. They don't exist in the released app."),
      h("div", { style: { height: "18px" } }), h("div.list", row("Log out", { danger: true, noChev: true, onclick: () => iosAlert("Log out?", a.isGuest ? "You're a guest. If you log out now, this reading is gone. Save your account first to keep it." : "Your diary stays saved in your account.", [{ label: "Cancel" }, { label: "Log out", red: true, do: async () => { await Net.signOut(); App.start(); } }]) })),
      group("", [row("Delete account", { danger: true, noChev: true, onclick: () => Nav.push(new DeleteAccountScreen()) })], "Deletes your account and everything in it, so you can start again from zero. This can't be undone."),
      h("p.center.xs.faint", { style: { padding: "20px 0 34px" } }, `Margin ${APP_VERSION} (2026.11) · Made in Mexico City`)));
  }
}

class SettingsDetail extends Screen {
  constructor(key) { super(); this.key = key; }
  build() {
    const a = Accounts.current(), s = a.state, g = s.profile.goals, st = a.settings, save = () => { Data.save(); Nav.refresh(); };
    const T = { account: "Account", profile: "Profile", privacy: "Privacy", reading: "Reading goals", reminders: "Reminders", focus: "Focus mode", appearance: "Appearance" };
    let body;
    switch (this.key) {
      case "account":
        body = [group("", [
          row("Name", { value: a.name, onclick: () => Nav.push(new EditField("Name", a.name, v => v.trim().length < 2 ? "Type your name." : null, async v => { const e = await Net.updateProfile({ name: v.trim() }); if (e) return "Couldn't save. Try again."; s.profile.name = v.trim().split(" ")[0]; })) }),
          row("Username", { value: "@" + a.username, onclick: () => Nav.push(new EditField("Username", a.username, v => v === a.username ? null : V.username(v), async v => v === a.username ? null : Net.changeUsername(v), "Friends find you by this name. Your old invite link stops working.")) }),
          a.isGuest ? row("Email and password", { value: "Not set", onclick: () => Nav.push(new SaveAccountScreen()) }) : null,
          a.isGuest ? null : row("Email", { value: a.email.length > 22 ? a.email.slice(0, 20) + "…" : a.email, onclick: () => Nav.push(new EditField("Email", a.email, V.email, async v => { if (v === a.email) return; const e = await Net.setEmail(v); if (e) return e; toast("We sent a link to the new address. The change applies when you open it."); })) }),
          a.isGuest ? null : row("Password", { value: "••••••••", onclick: () => Nav.push(new PasswordScreen()) }),
          row("University", { value: a.university || "Not set", onclick: () => Nav.push(new EditField("University", a.university || "", () => null, async v => { const e = await Net.updateProfile({ university: v.trim().slice(0, 80) }); if (e) return "Couldn't save. Try again."; }, "Optional. We suggest friends from the same place.")) }),
        ]),
        group("", [row("Delete account", { danger: true, noChev: true, onclick: () => Nav.push(new DeleteAccountScreen()) })], "Your diary, friends, posts and reviews are deleted. This can't be undone.")];
        break;
      case "profile": {
        const bio = h("textarea.input", { maxlength: 160, style: { minHeight: "90px", fontFamily: "var(--sans)", fontSize: "15px" } }); bio.value = a.bio || "";
        const cnt = h("div.help", `${(a.bio || "").length}/160`); bio.addEventListener("input", () => { cnt.textContent = `${bio.value.length}/160`; });
        const fins = Data.finished(s);
        body = [h("div.col.pad.mt12", { style: { alignItems: "center" } }, avatar(Data.me(), 88)),
          h("div.pad.mt16", h("span.small.muted", "Bio"), h("div.mt6", bio), cnt, h("button.btn.sm.primary.mt8", { onclick: async () => { if (!(await Net.updateProfile({ bio: bio.value.trim() }))) toast("Bio saved"); } }, "Save")),
          group("Creator", [row("Creator account", { sub: "Publish book lists, notes and videos. Readers can follow you.", control: switchEl(!!a.isCreator, async v => { const e = await Net.updateProfile({ is_creator: v }); if (!e) { toast(v ? "You're a creator now. Publish from your profile." : "Creator account turned off"); Social.refresh(); } }) })],
            "Your posts are public to every Margin reader. Followers see them in their feed. No like counts, no autoplay."),
          h("div.grouphead", "Favorite books (up to 4)"),
          h("div.grid4.pad", fins.map(p => { const b = Data.book(p.bookId), on = s.favorites.includes(b.id); return h("button", { onclick: () => { if (on) s.favorites = s.favorites.filter(x => x !== b.id); else if (s.favorites.length < 4) s.favorites.push(b.id); else return toast("You already have 4. Remove one first."); save(); } }, cover(b, 84, { style: { width: "100%", opacity: on ? 1 : .45, outline: on ? "2px solid var(--accent)" : "none", outlineOffset: "2px" } })); })),
          !fins.length ? h("p.pad.small.faint", "Books you finish show up here.") : null];
        break;
      }
      case "privacy":
        body = [group("", [
          row("Show what I'm reading", { sub: "Your current book and page, for your friends", control: switchEl(st.showCurrent, v => { st.showCurrent = v; Data.save(); }) }),
          row("Hide spoilers in reviews", { control: switchEl(st.spoilers, v => { st.spoilers = v; Data.save(); }) })]),
          h("div.grouphead", "Who sees your activity"),
          h("div.list", [["Friends and followers", "Friends and followers"], ["Only me", "Only me"]].map(([o, l]) => row(l, { noChev: true, control: (st.activityVisibility === "Only me") === (o === "Only me") ? icon("check") : null, onclick: () => { st.activityVisibility = o; save(); } }))),
          h("p.groupfoot", "Your diary stays private. Friends see your current book, page, momentum level, finished books and favorites. Your private notes and reflections are never shared. Margin doesn't sell your data or use your writing to train AI models.")];
        break;
      case "reading":
        body = [h("div.grouphead", "Pages a day"), h("div.row.wrap.gap8.pad", [5, 10, 15, 20, 30, 50].map(n => h("button.chip" + (g.dailyPages === n ? ".on" : ""), { onclick: () => { g.dailyPages = n; save(); } }, n))),
          h("div.grouphead", "Reading days"), h("div.row.gap6.pad", [1, 2, 3, 4, 5, 6, 0].map(d => h("button.chip.day" + (g.readingDays.includes(d) ? ".on" : ""), { "aria-label": Fmt.Days[d], onclick: () => { if (g.readingDays.includes(d)) { if (g.readingDays.length === 1) return toast("You need at least one reading day."); g.readingDays.splice(g.readingDays.indexOf(d), 1); } else g.readingDays.push(d); save(); } }, Copy.DayLetters[d]))),
          h("p.groupfoot", `${F.pages(S.weeklyTarget(g))} a week, ${Fmt.dayList(g.readingDays)}.`),
          h("div.grouphead", `Book goal for ${Clock.today().getFullYear()}`), h("div.row.wrap.gap8.pad", [4, 6, 12, 24, 52].map(n => h("button.chip" + (s.yearGoal === n ? ".on" : ""), { onclick: () => { s.yearGoal = n; save(); } }, n))),
          group("", [row("Count rereads", { control: switchEl(st.countRereads, v => { st.countRereads = v; Data.save(); }) }),
            row("Favorite shelf", { value: Catalog.findShelf(g.favoriteShelfId)?.name || "None", onclick: () => Nav.push(new ChoiceScreen("Favorite shelf", [["", "None"], ...Catalog.data.shelves.map(x => [x.id, x.name])], () => g.favoriteShelfId || "", v => { g.favoriteShelfId = v || null; }, "It gets priority in your recommendations.")) })])];
        break;
      case "reminders":
        body = [group("", [row("Reminders", { control: switchEl(g.remindersEnabled, v => { g.remindersEnabled = v; save(); }) })]),
          g.remindersEnabled ? [h("div.grouphead", "Time"),
            h("div.stepper.pad", h("button.circle", { "aria-label": "Half an hour earlier", onclick: () => { const t = (g.reminderHour * 60 + g.reminderMinute + 1410) % 1440; g.reminderHour = Math.floor(t / 60); g.reminderMinute = t % 60; save(); } }, "−"),
              h("div.bignum", { style: { lineHeight: "76px", fontSize: "40px" } }, Fmt.time(g.reminderHour, g.reminderMinute)),
              h("button.circle", { "aria-label": "Half an hour later", onclick: () => { const t = (g.reminderHour * 60 + g.reminderMinute + 30) % 1440; g.reminderHour = Math.floor(t / 60); g.reminderMinute = t % 60; save(); } }, "+")),
            group("", [row("When I…", { value: g.intention || "Not set", onclick: () => Nav.push(new EditField("When I…", g.intention || "", () => null, v => { g.intention = v.trim() || null; }, "It shows as the reminder title: “When I finish dinner…”.")) }),
              row("Include a quote from the book", { control: switchEl(st.quotesInReminders, v => { st.quotesInReminders = v; Data.save(); }) }),
              row("Weekly summary on Sundays", { control: switchEl(st.weeklySummary, v => { st.weeklySummary = v; Data.save(); }) })],
              `Up to ${ReminderPlanner.WeeklyBudget} a week. If you ignore 3 in a row, we drop to ${ReminderPlanner.WeeklyBudget / 2}. They never come if you already logged that day.`),
            group("", [row("Try a reminder", { noChev: true, onclick: () => App.demoBanner() })])] : null];
        break;
      case "focus":
        body = [h("div.grouphead", "Default length"), h("div.list", [15, 25, 45, 60].map(m => row(`${m} minutes`, { noChev: true, control: st.focusMinutes === m ? icon("check") : null, onclick: () => { st.focusMinutes = m; save(); } }))),
          group("", [row("Silenced apps", { value: st.screenTime ? `${(st.silenced || ["instagram", "tiktok", "youtube"]).length}` : "No permission", onclick: () => Nav.push(new AppPickerScreen()) })])];
        break;
      case "appearance":
        body = [h("div.grouphead", "Theme"), h("div.list", ["System", "Light", "Dark"].map(o => row(o, { noChev: true, control: st.theme === o ? icon("check") : null, onclick: () => { st.theme = o; Data.save(); App.applySettings(); Nav.refresh(); } }))),
          group("Accessibility", [row("Larger text", { control: switchEl(st.textLarge, v => { st.textLarge = v; Data.save(); App.applySettings(); }) }),
            row("Reduce motion", { control: switchEl(st.reduceMotion, v => { st.reduceMotion = v; Data.save(); App.applySettings(); }) })])];
        break;
    }
    return h("div", navbar(T[this.key]), h("div.scroll.under-nav", body, h("div.bottom-space")));
  }
}

class ChoiceScreen extends Screen {
  constructor(title, options, get, set, foot) { super(); Object.assign(this, { title, options, get, set, foot }); }
  build() { return h("div", navbar(this.title), h("div.scroll.under-nav", group("", this.options.map(([v, l]) => row(l, { noChev: true, control: this.get() === v ? icon("check") : null, onclick: () => { this.set(v); Data.save(); Nav.back(); } })), this.foot))); }
}

class EditField extends Screen {
  constructor(title, value, validate, apply, foot) { super(); Object.assign(this, { title, value, validate, apply, foot }); this.err = null; }
  build() {
    const i = h("input.input" + (this.err ? ".bad" : ""), { value: this.value }); i.addEventListener("input", () => { this.value = i.value; });
    setTimeout(() => i.focus(), 200);
    return h("div", navbar(this.title, { right: h("button.textbtn", { style: { fontWeight: 600 }, onclick: async () => { this.err = this.validate(this.value.trim()); if (this.err) return Nav.refresh(); const r = await this.apply(this.value.trim()); if (typeof r === "string") { this.err = r; return Nav.refresh(); } Data.save(); Nav.back(); } }, "Save") }),
      h("div.scroll.under-nav", h("div.pad.mt12", i, h("div.help.bad", this.err || ""), this.foot ? h("p.small.faint.mt8", this.foot) : null)));
  }
}

class PasswordScreen extends Screen {
  constructor() { super(); this.v = { cur: "", n1: "", n2: "" }; this.err = null; }
  build() {
    const a = Accounts.current(), mk = (k, ph) => { const e = h("input.input.mt8", { type: "password", placeholder: ph, value: this.v[k] }); e.addEventListener("input", () => { this.v[k] = e.value; }); return e; };
    return h("div", navbar("Password"), h("div.scroll.under-nav", h("div.pad.mt12",
      mk("cur", "Current password"), mk("n1", "New password"), mk("n2", "Repeat the new password"), h("div.help.bad", this.err || ""),
      h("button.btn.primary.mt12", { onclick: async () => {
        if (this.v.n1.length < 8 || !/\d/.test(this.v.n1)) this.err = "The new one needs 8 characters and at least one number.";
        else if (this.v.n1 !== this.v.n2) this.err = "The two new passwords don't match.";
        else if (!(await Net.checkPassword(this.v.cur))) this.err = "The current password doesn't match.";
        else { this.err = await Net.setPassword(this.v.n1); if (!this.err) { Nav.back(); return toast("Password updated"); } }
        Nav.refresh(); } }, "Change password"),
      h("button.btn.ghost.mt4", { onclick: async () => { const e = await Net.resetPassword(a.email); toast(e || `We sent a link to ${a.email}`); } }, "I don't remember my current one"))));
  }
}

class DeleteAccountScreen extends Screen {
  constructor() { super(); this.v = ""; this.busy = false; }
  build() {
    const a = Accounts.current(), s = a.state;
    const i = h("input.input.mt8", { placeholder: a.username, autocapitalize: "none" });
    const btn = h("button.btn.mt16", { style: { background: "var(--error)", color: "#fff" }, disabled: true, onclick: async () => {
      btn.disabled = true; btn.textContent = "Deleting…";
      const email = a.email, ok = await Net.deleteAccount();
      if (!ok) { btn.disabled = false; btn.textContent = "Delete my account"; return; }
      App.start();
      iosAlert("Your account was deleted", !email ? "Your guest diary, friends, posts, reviews and activity are gone." : `Your account (${email}), diary, friends, posts, reviews and activity are gone. You can sign up again with the same email whenever you want.`, [{ label: "OK", bold: true }]);
    } }, "Delete my account");
    i.addEventListener("input", () => { btn.disabled = i.value.trim() !== a.username; });
    return h("div", navbar("Delete account"), h("div.scroll.under-nav", h("div.pad.mt8",
      h("div.h2", "This deletes everything"), h("p.mt8", `${F.n(s.logs.length)} check-ins, ${F.books(Data.finished(s).length)} finished, ${s.lists.length} lists, your friends, your posts and your reviews will be deleted. This can't be undone.`),
      h("p.small.muted.mt8", "Your login (email and password) is deleted too. You won't be able to log in with it again."),
      h("button.btn.secondary.mt16", { onclick: () => exportCsv() }, icon("download"), "Download my data first"),
      h("p.small.muted.mt24", `To confirm, type your username: `, h("b", a.username)), i, btn)));
  }
}

class HelpScreen extends Screen {
  build() {
    const qa = [
      ["Why do I log the page and not how many pages I read?", "Because you see the page number in the book and don't have to subtract. The app works out how far you got."],
      ["What is reading momentum?", "An average of your last few days. It goes up when you meet your daily goal and drops little by little if you skip a reading day. Planned rest days don't count."],
      ["Who writes the quizzes?", "An AI writes them from the chapter text, and a person on the team checks them before they go out. Reviews, on the other hand, are written by readers."],
      ["Can I use Margin with ebooks?", "Yes, though the app is made for paper. You log the page the same way."],
      ["What if I don't read for a week?", "Nothing breaks. Your best stays and your momentum drops. When you come back, it goes up again."],
    ];
    return h("div", navbar("Help center"), h("div.scroll.under-nav", qa.map(([q, a]) => h("details.pad", { style: { padding: "14px 16px", borderBottom: "1px solid var(--rule)" } }, h("summary.strong", { style: { cursor: "pointer" } }, q), h("p.small.muted.mt8", a)))));
  }
}

class LegalScreen extends Screen {
  build() {
    return h("div", navbar("Terms and privacy"), h("div.scroll.under-nav", h("div.pad",
      h("div.h3.mt8", "Privacy notice"), h("p.small.mt6", "Margin stores your diary, your ratings and your writing to show them to you and, if your profile is public, so other readers can see them. We don't sell personal data or use your writing to train artificial intelligence models. You can download or delete your data from Settings."),
      h("div.h3.mt20", "Terms of use"), h("p.small.mt6", "What you write is yours. When you post it, you give us permission to show it in the app. Don't post reviews written by other people or generated by AI as if they were yours."),
      h("div.h3.mt20", "Licenses"), h("p.small.mt6", "EB Garamond and Inter: SIL Open Font License 1.1. Icons: Lucide (ISC). Covers: Open Library Covers API. Reviews quoted from Goodreads with their author and link."),
      h("p.xs.faint.mt20", "Academic prototype. Universidad Panamericana, Human Computer Interaction, 2026."))));
  }
}

function exportCsv() {
  const s = Data.state(), rows = [["date", "book", "author", "from", "to", "pages"]];
  for (const l of s.logs) { const b = Data.book(l.bookId); rows.push([l.date, b.title, b.author, l.fromPage, l.toPage, l.toPage - l.fromPage]); }
  rows.push([], ["book", "finished", "rating"]); for (const p of Data.finished(s)) rows.push([Data.book(p.bookId).title, p.finishedOn, p.rating || ""]);
  const csv = rows.map(r => r.map(x => `"${String(x ?? "").replace(/"/g, '""')}"`).join(",")).join("\n");
  const a = h("a", { href: URL.createObjectURL(new Blob(["﻿" + csv], { type: "text/csv" })), download: `margin-${Accounts.session()}-${D.key(Clock.today())}.csv` });
  document.body.append(a); a.click(); a.remove(); toast(`Downloaded: ${F.n(s.logs.length)} check-ins`);
}

/* Goodreads export (My Books > Import and export > Export Library). Books in our catalog are matched by title
   and author: "read" marks them finished with your rating, "to-read" adds them to Want to read. */
function parseCsv(text) {
  const rows = []; let row = [], cell = "", q = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (q) { if (c === '"') { if (text[i + 1] === '"') { cell += '"'; i++; } else q = false; } else cell += c; }
    else if (c === '"') q = true;
    else if (c === ",") { row.push(cell); cell = ""; }
    else if (c === "\n" || c === "\r") { if (c === "\r" && text[i + 1] === "\n") i++; row.push(cell); rows.push(row); row = []; cell = ""; }
    else cell += c;
  }
  if (cell || row.length) { row.push(cell); rows.push(row); }
  return rows.filter(r => r.some(x => x.trim()));
}
function importGoodreadsText(text) {
  const rows = parseCsv(text.replace(/^\uFEFF/, ""));
  if (rows.length < 2) return { rows: 0, matched: 0 };
  const head = rows[0].map(x => x.trim().toLowerCase()), col = n => head.indexOf(n);
  const iT = col("title"), iA = col("author"), iS = col("exclusive shelf"), iR = col("my rating"), iD = col("date read");
  if (iT < 0) return { rows: 0, matched: 0, bad: true };
  const norm = x => (x || "").toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/\(.*?\)/g, "").replace(/[^a-z0-9 ]/g, " ").replace(/\s+/g, " ").trim();
  const last = x => norm(x).split(" ").pop();
  const s = Data.state(); let matched = 0;
  for (const r of rows.slice(1)) {
    const t = norm(r[iT]), au = iA >= 0 ? last(r[iA]) : "";
    const b = Catalog.data.books.find(bk => { const bt = norm(bk.title); return (t === bt || t.startsWith(bt + " ")) && (!au || last(bk.author) === au); });
    if (!b) continue;
    matched++;
    const shelf = iS >= 0 ? (r[iS] || "").trim() : "read";
    if (shelf === "read") {
      const p = S.progressFor(s, b.id);
      if (!p.finished) { p.finished = true; p.currentPage = Math.max(p.currentPage, b.pages); }
      const stars = parseInt(r[iR], 10); if (stars > 0 && !p.rating) p.rating = stars;
      const d = iD >= 0 && /^\d{4}\/\d{2}\/\d{2}$/.test((r[iD] || "").trim()) ? r[iD].trim().replace(/\//g, "-") : null;
      if (d && !p.finishedOn) p.finishedOn = d;
      s.want = s.want.filter(id => id !== b.id);
    } else if (shelf === "to-read") { if (!s.want.includes(b.id) && !S.progressFor(s, b.id).finished) s.want.push(b.id); }
  }
  if (matched) Data.save();
  return { rows: rows.length - 1, matched };
}
function importGoodreads() {
  const inp = h("input", { type: "file", accept: ".csv,text/csv" });
  inp.addEventListener("change", () => { const f = inp.files[0]; if (!f) return; f.text().then(t => {
    const r = importGoodreadsText(t);
    if (r.bad) return toast("That doesn't look like a Goodreads export.");
    if (!r.rows) return toast("The file is empty.");
    toast(r.matched ? `We read ${F.books(r.rows)}. ${F.books(r.matched)} from our catalog were added.` : `We read ${F.books(r.rows)}, but none are in our catalog yet.`);
    Nav.refresh();
  }); });
  iosAlert("Import from Goodreads", "In Goodreads, go to My Books › Import and export › Export Library, then pick the CSV file here.", [{ label: "Cancel" }, { label: "Choose file", bold: true, do: () => inp.click() }]);
}
