/* Reading check-in, quiz, reflection, the end of a session, finishing a book, starting one, and Focus mode. */
"use strict";

const LogSheet = {
  open(bookId) {
    const s = Data.state();
    if (bookId) ReadingService.setCurrentBook(s, bookId);
    const book = Data.book(s.profile.currentBookId);
    if (!book) { toast("You're not reading a book yet. Pick one to start."); return; }
    const p = S.progressFor(s, book.id), from = p.currentPage;
    let page = Math.min(book.pages, from + s.profile.goals.dailyPages), when = "today";
    const input = h("input.bignum", { value: page, inputmode: "numeric", "aria-label": "Page you got to" });
    const delta = h("div.serif.center.mt12", " "), info = h("div.small.muted.center.mt4", " "), err = h("div.help.bad.center", "");
    const save = h("button.textbtn", { style: { color: "var(--ink)", fontWeight: 600, height: "44px", padding: "0 10px" }, onclick: () => this.submit(page, when) }, "Log it");
    const render = () => {
      const e = ReadingService.validate(s, page), ok = e === CheckInError.None;
      input.classList.toggle("bad", !ok); err.textContent = ReadingService.errorMessage(e) || "";
      delta.textContent = ok ? `+${F.pages(page - from)}` : " ";
      const closed = book.chapters.filter(c => c.endPage > from && c.endPage <= page);
      info.textContent = !ok ? " " : page >= book.pages ? "You finish the book" : closed.some(c => Book.hasQuiz(c)) ? "You finish a chapter. Its quiz is ready." : `${F.n(book.pages - page)} pages left`;
      save.disabled = !ok; save.style.opacity = ok ? 1 : .35;
    };
    const set = v => { page = Math.max(0, Math.min(book.pages, v)); input.value = page; render(); };
    input.addEventListener("input", () => { page = parseInt(input.value || "0", 10) || 0; render(); });
    const ch = Book.chapterAt(book, Math.max(1, from + 1));
    const whenSeg = h("div.seg.mt20", ["today", "yesterday"].map(k => h("button" + (when === k ? ".on" : ""), { onclick: e => { when = k; e.currentTarget.parentNode.querySelectorAll("button").forEach(b => b.classList.toggle("on", b === e.currentTarget)); } }, k === "today" ? "Today" : "Yesterday")));
    Sheet.open("Log reading", h("div",
      h("div.row.gap12", cover(book, 44), h("div.grow", h("div.strong", book.title), h("div.xs.muted.num", `You were on page ${from} of ${book.pages}.`)),
        h("button.btn.sm.secondary", { onclick: () => this.pickBook() }, "Change")),
      h("div.center.small.muted.mt20", "What page did you get to?"),
      h("div.stepper.mt8", h("button.circle", { "aria-label": "One page less", onclick: () => set(page - 1) }, "−"), input, h("button.circle", { "aria-label": "One page more", onclick: () => set(page + 1) }, "+")),
      delta, info, err,
      h("div.row.wrap.gap6.mt12", { style: { justifyContent: "center" } },
        h("button.pillbtn", { onclick: () => set(from + 10) }, "+10"), h("button.pillbtn", { onclick: () => set(from + 20) }, "+20"),
        ch ? h("button.pillbtn", { onclick: () => set(ch.endPage) }, "Finished the chapter") : null, h("button.pillbtn", { onclick: () => set(book.pages) }, "Finished the book")),
      whenSeg,
      h("p.xs.faint.center.mt12", "Log the page number you see in the book. The app works out the rest.")), { right: save });
    render();
  },
  pickBook() {
    const s = Data.state();
    const ids = [...new Set([s.profile.currentBookId, ...s.want, ...s.progress.filter(p => !p.finished && p.currentPage > 0).map(p => p.bookId)])].filter(id => Data.book(id));
    // A book already started is just picked back up; one never started gets its own moment (StartBookScreen).
    const pick = id => { Sheet.close(); if ((s.progress.find(p => p.bookId === id)?.currentPage || 0) > 0 || id === s.profile.currentBookId) this.open(id); else Nav.push(new StartBookScreen(id)); };
    Sheet.open("What are you reading?", h("div",
      h("div.list", { style: { margin: 0 } }, ids.map(id => { const b = Data.book(id); return h("button.item", { onclick: () => pick(id) }, cover(b, 44), h("div.grow", h("div.strong", b.title), h("div.xs.faint", s.want.includes(id) ? "In Want to read" : `p. ${S.progressFor(s, id).currentPage}`)), id === s.profile.currentBookId ? icon("check") : null); })),
      h("button.btn.secondary.mt12", { onclick: () => { Sheet.close(); Nav.reset(new ExploreScreen(), "explore"); } }, "Find another book")));
  },
  submit(page, when) {
    const s = Data.state(), before = HabitEngine.compute(s, Clock.today()), book = Data.book(s.profile.currentBookId);
    const day = when === "yesterday" ? D.add(Clock.today(), -1) : Clock.today();
    const achBefore = Data.achievements(s), r = ReadingService.checkIn(s, page, day);
    if (!r.ok) return;
    if (Clock.now().getHours() >= 23) s.nightLogs = (s.nightLogs || 0) + 1;
    s.want = s.want.filter(x => x !== book.id);
    if (r.finishedBook) S.progressFor(s, book.id).finishedOn = D.key(day);
    Data.save(); Sheet.close();
    const outcome = { r, book, before, achBefore };
    if (r.pendingQuiz) QuizFlow.start(book, r.pendingQuiz, outcome);
    else if (r.finishedBook) Nav.push(new FinishedScreen(outcome));
    else Nav.push(new ClosingScreen(outcome));
  },
};

/* ---------------- Quiz ---------------- */
const QuizFlow = {
  start(book, chapter, outcome) { Nav.push(new QuizScreen(book, chapter, outcome)); },
  // After the quiz (or its reflection): back to the book page, the finished-book screen, or the session summary.
  finish(o) { if (o.standalone) Nav.back(); else if (o.r.finishedBook) Nav.replace(new FinishedScreen(o)); else Nav.replace(new ClosingScreen(o)); },
};

class QuizScreen extends Screen {
  constructor(book, chapter, outcome) { super(); this.b = book; this.c = chapter; this.o = outcome; this.phase = outcome.standalone ? "q" : "offer"; this.i = 0; this.sel = -1; this.checked = false; this.answers = []; }
  finish() { QuizFlow.finish(this.o); }
  build() {
    const qs = this.c.quiz.questions;
    if (this.phase === "offer") return h("div", navbar("", { onBack: () => this.finish() }), h("div.scroll.under-nav", h("div.pad",
      cover(this.b, 72), h("div.h1.mt16", `You finished “${this.c.title}”`), h("p.muted.mt8", `${this.b.title}, pages ${this.c.startPage} to ${this.c.endPage}.`),
      h("p.mt16", `${qs.length} questions about what you just read. Recalling what you read is the best way not to forget it. It's optional and doesn't affect your momentum.`),
      h("p.xs.faint.mt12", "Questions written with AI and checked by people. The AI asks; you think."))),
      h("div.footbar", h("button.btn.accent", { onclick: () => { this.phase = "q"; Nav.refresh(); } }, "Take the quiz"), h("button.btn.ghost.mt4", { onclick: () => this.finish() }, "Not now")));
    if (this.phase === "done") {
      const sc = QuizEngine.score(this.c.quiz, this.answers);
      if (!this.recorded) { QuizEngine.record(Data.state(), this.b.id, this.c.number, sc, Clock.today()); Data.save(); this.recorded = true; }
      return h("div", navbar("", { onBack: () => this.finish() }), h("div.scroll.under-nav", h("div.pad",
        h("div", { style: { fontFamily: "var(--serif)", fontSize: "64px", fontWeight: 600, lineHeight: 1 } }, `${sc.correct} of ${sc.total}`),
        h("p.serif-l.mt8", QuizEngine.verdict(sc)),
        h("div.mt20", qs.map((q, k) => h("div.row.top.gap10", { style: { padding: "10px 0", borderTop: "1px solid var(--rule)" } },
          h("span.badge" + (this.answers[k] === q.correctIndex ? ".ok" : ".todo"), this.answers[k] === q.correctIndex ? "Right" : "Review"),
          h("div.grow.small", h("div", q.prompt), this.answers[k] !== q.correctIndex ? h("div.muted.mt4", "Answer: ", q.options[q.correctIndex]) : null)))))),
        h("div.footbar", h("button.btn.primary", { onclick: () => Nav.replace(new ReflectionScreen(this.b, this.c, this.o)) }, "Write a reflection"), h("button.btn.ghost.mt4", { onclick: () => this.finish() }, "Finish")));
    }
    const q = qs[this.i], ok = this.checked && this.sel === q.correctIndex;
    const top = h("div.row.gap10.pad", { style: { paddingTop: "14px", paddingBottom: "8px" } },
      h("button.iconbtn", { "aria-label": "Leave the quiz", onclick: () => iosAlert("Leave the quiz?", "Nothing from this quiz will be saved.", [{ label: "Keep going" }, { label: "Leave", red: true, do: () => this.finish() }]) }, icon("x")),
      h("div.qprogress", h("i", { style: { width: `${((this.i + (this.checked ? 1 : 0)) / qs.length) * 100}%` } })));
    const opts = q.options.map((o, k) => {
      let cls = "option"; if (!this.checked && this.sel === k) cls += ".sel";
      if (this.checked) cls += k === q.correctIndex ? ".ok" : k === this.sel ? ".ko" : ".dim";
      return h("button." + cls, { onclick: () => { if (!this.checked) { this.sel = k; Nav.refresh(); } } }, o);
    });
    const bottom = this.checked
      ? h("div.verdict" + (ok ? ".ok" : ".ko"), h("div.strong", { style: { color: ok ? "var(--ok)" : "var(--error)", fontSize: "18px" } }, ok ? "Exactly." : "Not quite."),
          h("p.small.mt4", q.explanation), h("button.btn.mt12" + ".primary", { style: { marginBottom: "34px" }, onclick: () => { this.answers.push(this.sel); this.sel = -1; this.checked = false; if (this.i < qs.length - 1) this.i++; else this.phase = "done"; Nav.refresh(); } }, this.i < qs.length - 1 ? "Next question" : "See result"))
      : h("div.footbar.ruled", h("button.btn.primary", { disabled: this.sel < 0, onclick: () => { this.checked = true; Nav.refresh(); } }, "Check"));
    return h("div", top, h("div.scroll.under-nav", h("div.pad", h("div.xs.faint", `${this.c.title} · question ${this.i + 1} of ${qs.length}`), h("div.serif-l.mt8", { style: { fontWeight: 600 } }, q.prompt), h("div.mt20", opts))), bottom);
  }
}

class ReflectionScreen extends Screen {
  constructor(book, chapter, outcome) { super(); this.b = book; this.c = chapter; this.o = outcome; this.text = ""; this.k = (chapter.number) % Copy.ReflectionPrompts.length; }
  build() {
    const ta = h("textarea.input", { placeholder: "Write here…", style: { minHeight: "220px" } }); ta.value = this.text;
    const count = h("div.help"), save = h("button.btn.accent", { onclick: () => this.save() }, "Save reflection");
    const upd = () => { this.text = ta.value; const w = ta.value.trim().split(/\s+/).filter(Boolean).length; count.textContent = `${w} ${w === 1 ? "word" : "words"}`; save.disabled = w < 5; };
    ta.addEventListener("input", upd); setTimeout(upd);
    return h("div", navbar(this.c.title, { onBack: () => QuizFlow.finish(this.o) }), h("div.scroll.under-nav", h("div.pad",
      h("span.human", "Written by a human"), h("div.h1.mt12", Copy.ReflectionPrompts[this.k]),
      h("button.small.link.mt6", { onclick: () => { this.k = (this.k + 1) % Copy.ReflectionPrompts.length; Nav.refresh(); } }, "Another question"),
      h("div.mt16", ta), count, h("p.xs.faint.mt8", "No autocomplete and no AI suggestions here. Only you see it, unless you post it as a review."))),
      h("div.footbar.ruled", save, h("button.btn.ghost.mt4", { onclick: () => QuizFlow.finish(this.o) }, "Skip")));
  }
  save() {
    const s = Data.state(); s.reflections.push({ bookId: this.b.id, chapterNumber: this.c.number, text: this.text.trim(), date: D.key(Clock.today()), humanWritten: true });
    Data.save(); toast("Saved. It's yours."); QuizFlow.finish(this.o);
  }
}

/* Finishing a book: the achievements it unlocked, a rating, and ways to tell people (a public review, a share).
   Friends already see "finished" in their feed unless activity is set to "Only me". */
class FinishedScreen extends Screen {
  constructor(o) { super(); this.o = o; this.rating = 0; }
  build() {
    const b = this.o.book, s = Data.state(), y = Clock.today().getFullYear(), n = Data.booksInYear(s, y);
    const had = new Set((this.o.achBefore || []).filter(a => a.got).map(a => a.id));
    const fresh = Data.achievements(s).filter(a => a.got && !had.has(a.id));
    const unlock = (mark, title, name, detail) => h("div.unlock", h("span.medal", mark), h("div.grow", h("div.caps", title), h("div.strong.mt2", name), h("div.xs.muted.mt2", detail)));
    const seen = Accounts.current().settings.activityVisibility !== "Only me";
    return h("div", h("div.scroll", h("div.pad.col", { style: { alignItems: "center", textAlign: "center", paddingTop: "30px" } },
      cover(b, 128), h("div.h1.mt20", `You finished ${b.title}.`),
      h("p.muted.mt6.num", `It's book ${n} of ${y} for you. Your goal is ${s.yearGoal}.`),
      unlock(String(n), "Book finished", `Book ${n} of ${y}`, `${F.n(b.pages)} pages, read by you, on paper.`),
      fresh.map(a => unlock(a.name[0], "Achievement unlocked", a.name, a.detail)),
      h("div.mt24.small.muted", "How would you rate it?"),
      h("div.mt8", ratingInput(S.progressFor(s, b.id).rating || 0, v => { S.progressFor(s, b.id).rating = v; Data.save(); this.rating = v; Nav.refresh(); })),
      h("button.btn.accent.mt24", { onclick: () => ReviewSheet.open(b.id) }, "Post a review"),
      h("button.btn.secondary.mt8", { onclick: () => shareText(`I just finished ${b.title} by ${b.author}. Book ${n} of my ${y} on Margin. https://marginreads.vercel.app`) }, icon("share"), "Share it"),
      h("p.xs.faint.mt12", seen ? "Your friends will see that you finished it." : "Your activity is set to Only me, so friends won't see it."))),
      h("div.footbar", h("button.btn.secondary", { onclick: () => { s.profile.currentBookId = null; Data.save(); Nav.reset(new HomeScreen(), "home"); } }, "Done")));
  }
}

/* Starting a book is a real decision, so it gets its own moment: the book, how long it will take at the
   reader's pace, and one clear button. Every way of starting a book comes through here. */
class StartBookScreen extends Screen {
  constructor(id) { super(); this.id = id; }
  build() {
    const b = Data.book(this.id), s = Data.state(), g = s.profile.goals;
    const perWeek = Math.max(1, g.dailyPages * Math.max(1, g.readingDays.length)), weeks = Math.max(1, Math.ceil(b.pages / perWeek));
    const cur = s.profile.currentBookId && s.profile.currentBookId !== b.id ? Data.book(s.profile.currentBookId) : null;
    return h("div", navbar(""), h("div.scroll.under-nav", h("div.startbook",
      h("div.caps", "A new book"), h("div.mt16", cover(b, 128)),
      h("div.h1.mt20", b.title), h("div.muted.mt4", b.author),
      h("div.card.block.plan", h("div.row", h("span.grow.small.muted", "Length"), h("span.strong.num", `${F.n(b.pages)} pages`)),
        h("div.row.mt8", h("span.grow.small.muted", "At your pace"), h("span.strong.num", `about ${weeks} ${weeks === 1 ? "week" : "weeks"}`)),
        h("div.row.mt8", h("span.grow.small.muted", "Today"), h("span.strong.num", `${g.dailyPages} pages${g.intention ? `, ${g.intention}` : ""}`))),
      h("p.serif-l.mt24", "Every book starts on page one."),
      cur ? h("p.xs.faint.mt8", `It replaces ${cur.title} as your current book. Your progress there is kept.`) : null)),
      h("div.footbar", h("button.btn.accent", { onclick: () => this.begin() }, "Start reading")));
  }
  // Confirming is the moment: confetti, then Home with the book set, where they log their reading.
  begin() {
    const s = Data.state(), b = Data.book(this.id);
    s.want = s.want.filter(x => x !== b.id); s.dnf = s.dnf.filter(x => x !== b.id);
    ReadingService.setCurrentBook(s, b.id); Data.save();
    celebrate(); App.goHome(); toast(`Now reading ${b.title}. Enjoy it.`);
  }
}

class ClosingScreen extends Screen {
  constructor(o) { super(); this.o = o; }
  build() {
    const s = Data.state(), after = HabitEngine.compute(s, Clock.today()), w = HabitEngine.weekly(s, Clock.today());
    const b = this.o.book, p = S.progressFor(s, b.id);
    const changed = after.level !== this.o.before.level;
    return h("div", h("div.scroll", h("div.pad", { style: { paddingTop: "40px" } },
      h("div", { style: { fontFamily: "var(--serif)", fontSize: "56px", fontWeight: 600, lineHeight: 1 } }, `+${this.o.r.pagesAdded}`),
      h("div.serif-l.mt4", this.o.r.pagesAdded === 1 ? "page" : "pages"),
      h("div.row.gap12.mt24", cover(b, 56), h("div.grow", h("div.strong", b.title), h("div.mt6", bar(p.currentPage / b.pages)), h("div.xs.faint.mt4.num", `Page ${p.currentPage} of ${b.pages}`))),
      h("div.card.block.mt20", h("div.row", h("span.caps.grow", "Reading momentum"), h("span.strong", Copy.levelName(after.level))),
        changed ? h("p.small.mt6", `You moved up from ${Copy.levelName(this.o.before.level)} to ${Copy.levelName(after.level)}.`) : null,
        h("div.row.small.mt8.num", h("span.grow", `${w.pagesRead} of ${w.target} pages this week`)), h("div.mt6", bar(w.pagesRead / w.target, "ink"))),
      )),
      h("div.footbar", h("button.btn.primary", { onclick: () => Nav.reset(new HomeScreen(), "home") }, "Done")));
  }
}

/* ---------------- Focus mode ---------------- */
class FocusScreen extends Screen {
  constructor() { super(); const a = Accounts.current(); this.min = a.settings.focusMinutes || 25; this.end = null; this.t = null; }
  onHide() { clearInterval(this.t); App.root.classList.remove("theme-dark"); }
  build() {
    App.root.classList.add("theme-dark");
    const a = Accounts.current(), apps = a.settings.silenced || FOCUS.silenced, b = Data.book(Data.state().profile.currentBookId);
    if (!this.end) return h("div.focus", { style: { background: "var(--paper)" } }, navbar("Focus mode"), h("div.scroll.under-nav", h("div.pad",
      h("div.h1.mt12", "How long will you read?"), h("div.row.wrap.gap8.mt16", FOCUS.minutes.map(m => h("button.chip" + (this.min === m ? ".on" : ""), { onclick: () => { this.min = m; Nav.refresh(); } }, `${m} min`))),
      h("div.list.mt24", { style: { margin: "24px 0 0" } }, h("button.item", { onclick: () => Nav.push(new AppPickerScreen()) }, h("div.grow", h("div", "Silenced apps"), h("div.xs.faint", a.settings.screenTime ? `${apps.length} ${apps.length === 1 ? "app" : "apps"}` : "No Screen Time permission")), icon("chev", "chev")),
        b ? h("div.item", cover(b, 44), h("div.grow", h("div.xs.faint", "Reading"), h("div", b.title))) : null),
      h("p.small.faint.mt16", "Put your phone face down. It will vibrate when time's up."))),
      h("div.footbar", h("button.btn.accent", { onclick: () => { this.end = Date.now() + this.min * 60000; this.total = this.min * 60000; Nav.refresh(); } }, "Start")));
    const C = 2 * Math.PI * 120;
    const ring = h("div.fring", { html: `<svg viewBox="0 0 260 260"><circle cx="130" cy="130" r="120" fill="none" stroke="var(--rule)" stroke-width="6"/><circle id="fr" cx="130" cy="130" r="120" fill="none" stroke="var(--accent)" stroke-width="6" stroke-linecap="round" stroke-dasharray="${C}" stroke-dashoffset="0"/></svg>` },
      h("div.timer", { style: { position: "absolute", inset: 0, display: "grid", placeItems: "center", fontSize: "64px" } }, h("span", { id: "ft" }, "")));
    const tick = () => { const left = Math.max(0, this.end - Date.now()); const t = document.getElementById("ft"), r = document.getElementById("fr");
      if (t) t.textContent = `${Math.floor(left / 60000)}:${String(Math.floor(left / 1000) % 60).padStart(2, "0")}`; if (r) r.setAttribute("stroke-dashoffset", String(C * (1 - left / this.total)));
      if (!left) this.done(false); };
    clearInterval(this.t); this.t = setInterval(tick, 250); setTimeout(tick);
    return h("div.focus", { style: { background: "var(--paper)" } }, h("div.scroll", h("div.pad.col", { style: { alignItems: "center", paddingTop: "70px", textAlign: "center" } }, ring,
      b ? h("div.mt24.serif-l", b.title) : null, h("div.small.faint.mt4", a.settings.screenTime ? `${apps.length} ${apps.length === 1 ? "app" : "apps"} silenced` : "No app blocking"))),
      h("div.footbar", h("button.btn.secondary", { onclick: () => this.done(true) }, "End early")));
  }
  done(early) {
    clearInterval(this.t); const mins = Math.round((this.total - Math.max(0, this.end - Date.now())) / 60000);
    const s = Data.state(); (s.focusSessions = s.focusSessions || []).push({ date: D.key(Clock.today()), minutes: mins }); Data.save();
    this.end = null; App.root.classList.remove("theme-dark"); Nav.back(); if (Nav.current() instanceof FocusScreen) Nav.reset(new HomeScreen(), "home");
    if (early) toast(`${mins}-min session saved`);
    else {
      // Time's up: the phone vibrates (where the browser can) and the banner says so, like the iOS alert.
      const b = Data.book(s.profile.currentBookId), planned = Math.round(this.total / 60000);
      if (navigator.vibrate) navigator.vibrate([200, 100, 200]);
      banner("Time's up", `${planned} ${planned === 1 ? "minute" : "minutes"} of ${b ? b.title : "reading"}. Log your page.`);
    }
    setTimeout(() => LogSheet.open(), 400);
  }
}

class AppPickerScreen extends Screen {
  build() {
    const a = Accounts.current(); const cats = [["instagram", "Instagram", "Reels, stories and feed"], ["tiktok", "TikTok", "For You feed"], ["youtube", "YouTube", "Shorts and videos"], ["whatsapp", "WhatsApp", "Chats and groups"], ["x", "X", "Timeline and trends"], ["snapchat", "Snapchat", "Snaps and stories"], ["netflix", "Netflix", "Shows and autoplay"], ["discord", "Discord", "Servers and DMs"]];
    a.settings.silenced = (a.settings.silenced || FOCUS.silenced).filter(id => cats.some(c => c[0] === id));
    if (!a.settings.silenced.length && !a.settings.silencedSet) a.settings.silenced = FOCUS.silenced;
    if (!a.settings.screenTime) return h("div", navbar("Silenced apps"), h("div.scroll.under-nav", h("div.pad",
      h("div.h1", "Screen Time"), h("p.muted.mt8", "To silence apps while you read, we need Screen Time permission. You pick the apps; Apple never tells us what you do in them."),
      h("button.btn.primary.mt24", { onclick: () => iosAlert("“Margin” Would Like to Access Screen Time", "Margin will be able to see your activity, restrict content, and limit the use of apps and websites. You can turn this off in Settings.", [{ label: "Don't Allow" }, { label: "Continue", bold: true, do: () => { a.settings.screenTime = true; Data.save(); Nav.refresh(); } }]) }, "Allow"),
      h("p.xs.faint.mt12", "Simulation: the prototype doesn't really block apps."))));
    return h("div", navbar("Silenced apps"), h("div.scroll.under-nav", h("p.pad.small.muted", "During Focus mode, these apps go quiet: no notifications, and they won't open."),
      h("div.list.mt12", cats.map(([id, n, d]) => h("div.item", h("img", { src: `../Assets/Resources/AppIcons/${id}.png`, alt: "", style: { width: "36px", height: "36px", borderRadius: "8px", marginRight: "12px", flex: "none" } }), h("div.grow", h("div", n), h("div.xs.faint", d)), switchEl(a.settings.silenced.includes(id), v => { const L = a.settings.silenced; v ? L.push(id) : L.splice(L.indexOf(id), 1); a.settings.silencedSet = true; Data.save(); })))),
      h("p.groupfoot", "Demo version: the final version uses iOS Screen Time (FamilyControls).")));
  }
}
