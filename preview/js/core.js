/* Margin: core logic (1:1 port of Assets/Scripts/Core). Tested in v1 of the replica. */
"use strict";
/* =========================================================================
   DATES (same as DateTime.Date; keys are "YYYY-MM-DD")
   ========================================================================= */
const D = {
  key(d) { return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; },
  parse(k) { const [y, m, d] = k.slice(0, 10).split("-").map(Number); return new Date(y, m - 1, d); },
  date(d) { return new Date(d.getFullYear(), d.getMonth(), d.getDate()); },
  add(d, n) { const r = new Date(d); r.setDate(r.getDate() + n); return r; },
  same(a, b) { return D.key(a) === D.key(b); },
  stamp(d) { return `${D.key(d)}T${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`; },
  parseStamp(s) { const d = D.parse(s); const [h, m] = (s.slice(11) || "00:00").split(":").map(Number); d.setHours(h || 0, m || 0, 0, 0); return d; },
};

/* =========================================================================
   CORE: port of Assets/Scripts/Core
   ========================================================================= */
const Level = { Kindling: 0, Steady: 1, Strong: 2, Unstoppable: 3 };

function newGoals() {
  return { dailyPages: 20, readingDays: [1, 2, 3, 4, 5], reminderHour: 21, reminderMinute: 0,
           remindersEnabled: true, focusModeEnabled: true, favoriteShelfId: null, intention: null };
}
function newState() {
  return { schemaVersion: 1,
           profile: { name: "", goals: newGoals(), currentBookId: null, onboardingComplete: false, createdOn: null },
           logs: [], progress: [], quizResults: [], reflections: [], remindersScheduled: [] };
}

const S = { // AppState / ReadingGoals methods
  isReadingDay(goals, d) { return goals.readingDays.includes(d.getDay()); },
  weeklyTarget(goals) { return goals.dailyPages * goals.readingDays.length; },
  progressFor(state, bookId) {
    let p = state.progress.find(x => x.bookId === bookId);
    if (!p) { p = { bookId, currentPage: 0, finished: false, rating: 0 }; state.progress.push(p); }
    return p;
  },
  hasQuizResult(state, bookId, ch) { return state.quizResults.some(q => q.bookId === bookId && q.chapterNumber === ch); },
  pagesOn(state, d) {
    const k = D.key(d);
    return state.logs.reduce((t, l) => t + (l.date === k ? Math.max(0, l.toPage - l.fromPage) : 0), 0);
  },
};

const Book = {
  chapterAt(b, page) { return b.chapters.find(c => page >= c.startPage && page <= c.endPage) || null; },
  getChapter(b, n) { return b.chapters.find(c => c.number === n) || null; },
  hasQuiz(c) { return !!(c && c.quiz && c.quiz.questions && c.quiz.questions.length); },
};

const Catalog = {
  data: { shelves: [], books: [] },
  findBook(id) { return this.data.books.find(b => b.id === id) || null; },
  findShelf(id) { return this.data.shelves.find(s => s.id === id) || null; },
  booksInShelf(id) { return this.data.books.filter(b => b.shelves.includes(id)); },
};

const HabitEngine = {
  Lambda: 0.85, DailyCap: 1.5,
  dailyRatio(pages, goal) { return goal <= 0 ? (pages > 0 ? 1 : 0) : Math.min(pages / goal, this.DailyCap); },
  step(prev, pages, goal, isReadingDay, dayIsOver) {
    if (pages <= 0 && (!isReadingDay || !dayIsOver)) return prev;
    return this.Lambda * prev + (1 - this.Lambda) * this.dailyRatio(pages, goal);
  },
  levelFor(v) { return v < 0.25 ? Level.Kindling : v < 0.6 ? Level.Steady : v < 0.9 ? Level.Strong : Level.Unstoppable; },
  compute(state, today) {
    const goals = state.profile.goals;
    let start = state.profile.createdOn ? D.parse(state.profile.createdOn) : D.date(today);
    for (const l of state.logs) { const d = D.parse(l.date); if (d < start) start = d; }
    let m = 0, yesterday = 0;
    for (let d = start; d <= today; d = D.add(d, 1)) {
      const isToday = D.same(d, today);
      if (isToday) yesterday = m;
      m = this.step(m, S.pagesOn(state, d), goals.dailyPages, S.isReadingDay(goals, d), !isToday);
    }
    const trend = m > yesterday + 1e-6 ? 1 : m < yesterday - 1e-6 ? -1 : 0;
    return { value: m, yesterday, level: this.levelFor(m), trend, normalized: Math.min(1, m) };
  },
  weekStart(d) { return D.add(D.date(d), -((d.getDay() + 6) % 7)); },
  weekly(state, today) {
    const start = this.weekStart(today);
    let pages = 0;
    for (let i = 0; i < 7; i++) pages += S.pagesOn(state, D.add(start, i));
    return { weekStart: start, pagesRead: pages, target: S.weeklyTarget(state.profile.goals) };
  },
  headline(s, readToday, isReadingDay) {
    if (readToday) return "You already read today. The rest can wait.";
    if (!isReadingDay) return "Today is your rest day. If a book calls you, go ahead.";
    switch (s.level) {
      case Level.Kindling: return "Every reader started with one page.";
      case Level.Steady: return "You are building a rhythm. A few pages today keep it going.";
      case Level.Strong: return "Reading has become part of your days.";
      default: return "You read like you breathe.";
    }
  },
};

const CheckInError = { None: 0, NoBookSelected: 1, UnknownBook: 2, PageNotAhead: 3, PageBeyondBook: 4, TooManyPagesToday: 5 };

const ReadingService = {
  MaxPagesPerCheckIn: 400,
  errorMessage(e) {
    switch (e) {
      case CheckInError.NoBookSelected: return "First choose the book you are reading.";
      case CheckInError.UnknownBook: return "We could not find that book in the catalog.";
      case CheckInError.PageNotAhead: return "That page is before where you were. Enter the page where you stopped today.";
      case CheckInError.PageBeyondBook: return "That number is past the book's last page.";
      case CheckInError.TooManyPagesToday: return "That is a lot of pages for one check-in. Can you check the number?";
      default: return null;
    }
  },
  validate(state, newPage) {
    const id = state.profile.currentBookId;
    if (!id) return CheckInError.NoBookSelected;
    const book = Catalog.findBook(id);
    if (!book) return CheckInError.UnknownBook;
    const current = S.progressFor(state, id).currentPage;
    if (newPage <= current) return CheckInError.PageNotAhead;
    if (newPage > book.pages) return CheckInError.PageBeyondBook;
    if (newPage - current > this.MaxPagesPerCheckIn) return CheckInError.TooManyPagesToday;
    return CheckInError.None;
  },
  checkIn(state, newPage, today) {
    const r = { error: this.validate(state, newPage), pagesAdded: 0, finishedBook: false, chaptersFinished: [], pendingQuiz: null };
    r.ok = r.error === CheckInError.None;
    if (!r.ok) return r;
    const book = Catalog.findBook(state.profile.currentBookId);
    const p = S.progressFor(state, book.id);
    const from = p.currentPage;
    state.logs.push({ date: D.key(today), bookId: book.id, fromPage: from, toPage: newPage });
    p.currentPage = newPage;
    r.pagesAdded = newPage - from;
    for (const c of book.chapters) {
      if (c.endPage > from && c.endPage <= newPage) {
        r.chaptersFinished.push(c);
        if (!r.pendingQuiz && Book.hasQuiz(c) && !S.hasQuizResult(state, book.id, c.number)) r.pendingQuiz = c;
      }
    }
    if (newPage >= book.pages) { p.finished = true; r.finishedBook = true; }
    return r;
  },
  setCurrentBook(state, id) { state.profile.currentBookId = id; S.progressFor(state, id); },
};

const QuizEngine = {
  isCorrect(q, a) { return a === q.correctIndex; },
  score(quiz, answers) {
    let correct = 0;
    quiz.questions.forEach((q, i) => { if (this.isCorrect(q, answers[i])) correct++; });
    const total = quiz.questions.length;
    return { correct, total, comprehended: total > 0 && correct * 3 >= total * 2 };
  },
  record(state, bookId, chapter, score, today) {
    state.quizResults = state.quizResults.filter(q => !(q.bookId === bookId && q.chapterNumber === chapter));
    state.quizResults.push({ bookId, chapterNumber: chapter, correct: score.correct, total: score.total, date: D.key(today) });
  },
  verdict(s) {
    if (s.correct === s.total) return "All correct.";
    if (s.comprehended) return "Good. The one you missed is below.";
    return "Those pages are worth another look before you go on.";
  },
};

const Recommender = {
  ShelfWeight: 2.0, TagWeight: 1.0, SeedShelfWeight: 0.5, RatingBoost: 0.5, ShortBookBonus: 1.0, ShortBookPages: 150,
  intersect(a, b, ci) {
    const norm = x => ci ? x.toLowerCase() : x;
    const set = new Set(b.map(norm));
    return new Set(a.map(norm).filter(x => set.has(x))).size;
  },
  recommend(state, level, count = 3) {
    const goals = state.profile.goals, cur = state.profile.currentBookId;
    const read = new Set(state.progress.filter(p => p.finished).map(p => p.bookId));
    const seeds = [];
    for (const p of state.progress) {
      const b = Catalog.findBook(p.bookId);
      if (!b || !p.finished || p.bookId === cur) continue;
      const w = 1 + (p.rating > 3 ? (p.rating - 3) * this.RatingBoost : 0) - (p.rating > 0 && p.rating < 3 ? 0.75 : 0);
      seeds.push({ book: b, weight: w });
    }
    const current = Catalog.findBook(cur || "");
    if (current) seeds.push({ book: current, weight: 1 });

    const results = [];
    for (const b of Catalog.data.books) {
      if (read.has(b.id) || b.id === cur) continue;
      let score = 0, reason = null;
      if (goals.favoriteShelfId && b.shelves.includes(goals.favoriteShelfId)) {
        score += this.ShelfWeight;
        const sh = Catalog.findShelf(goals.favoriteShelfId);
        reason = `From your favorite shelf: ${sh ? sh.name : goals.favoriteShelfId}`;
      }
      let best = 0, bestBook = null;
      for (const s of seeds) {
        const o = (this.intersect(s.book.tags, b.tags, true) * this.TagWeight + this.intersect(s.book.shelves, b.shelves, false) * this.SeedShelfWeight) * s.weight;
        if (o > best) { best = o; bestBook = s.book; }
      }
      if (bestBook) {
        score += best;
        if (best >= this.ShelfWeight || reason == null) reason = `Shares themes with ${bestBook.title}`;
      }
      if (level === Level.Kindling && b.pages <= this.ShortBookPages) {
        score += this.ShortBookBonus;
        if (reason == null) reason = "Short: a good one to start with";
      }
      results.push({ book: b, score, reason: reason || "A classic to discover" });
    }
    results.sort((x, y) => y.score - x.score || x.book.pages - y.book.pages || (x.book.id < y.book.id ? -1 : x.book.id > y.book.id ? 1 : 0));
    return results.slice(0, count);
  },
};

const ReminderPlanner = {
  WeeklyBudget: 4, IgnoredThreshold: 3,
  ignoredStreak(state, today) {
    const past = state.remindersScheduled.map(D.parseStamp).filter(d => D.date(d) < D.date(today)).sort((a, b) => a - b);
    let streak = 0;
    for (let i = past.length - 1; i >= 0; i--) { if (S.pagesOn(state, past[i]) > 0) break; streak++; }
    return streak;
  },
  budgetFor(s) { return s >= this.IgnoredThreshold * 2 ? 1 : s >= this.IgnoredThreshold ? Math.floor(this.WeeklyBudget / 2) : this.WeeklyBudget; },
  planNext7Days(state, now) {
    const g = state.profile.goals, result = [];
    if (!g.remindersEnabled || g.readingDays.length === 0) return result;
    const cands = [];
    for (let i = 0; i < 7; i++) {
      const day = D.add(D.date(now), i);
      if (!S.isReadingDay(g, day)) continue;
      const at = new Date(day); at.setHours(g.reminderHour, g.reminderMinute, 0, 0);
      if (at <= now) continue;
      if (i === 0 && S.pagesOn(state, day) > 0) continue;
      cands.push(at);
    }
    const budget = Math.min(this.budgetFor(this.ignoredStreak(state, now)), cands.length);
    if (budget <= 0) return result;
    const step = cands.length / budget;
    for (let k = 0; k < budget; k++) result.push(cands[Math.floor(k * step)]);
    return result;
  },
};

const DemoData = {
  seed(state, today, days = 14) {
    const goals = state.profile.goals;
    const bookId = state.profile.currentBookId || (Catalog.data.books[0] && Catalog.data.books[0].id);
    if (!bookId) return;
    state.logs = []; state.quizResults = []; state.remindersScheduled = [];
    state.progress = state.progress.filter(p => p.bookId !== bookId);
    state.profile.createdOn = D.key(D.add(today, -days));
    ReadingService.setCurrentBook(state, bookId);
    let seed = 451; // deterministic RNG (not the same as C# System.Random)
    const rnd = (a, b) => { seed = (seed * 1103515245 + 12345) % 2147483648; return a + (seed % (b - a)); };
    for (let i = days; i >= 1; i--) {
      const day = D.add(today, -i);
      if (!S.isReadingDay(goals, day) || i === 6) continue;
      const pages = Math.max(3, goals.dailyPages + rnd(-5, 8));
      const target = Math.min(Catalog.findBook(bookId).pages - 1, S.progressFor(state, bookId).currentPage + pages);
      const r = ReadingService.checkIn(state, target, day);
      if (r.pendingQuiz) QuizEngine.record(state, bookId, r.pendingQuiz.number,
        { correct: rnd(2, 4), total: r.pendingQuiz.quiz.questions.length }, day);
    }
  },
};
const store = {
  mem: {},
  get(k) { try { const v = localStorage.getItem(k); return v === null ? this.mem[k] ?? null : v; } catch { return this.mem[k] ?? null; } },
  set(k, v) { this.mem[k] = v; try { localStorage.setItem(k, v); } catch { /* memory only */ } },
  del(k) { delete this.mem[k]; try { localStorage.removeItem(k); } catch { /* memory only */ } },
};

const Clock = {
  get dayOffset() { return parseInt(store.get("margin.demo.dayOffset") || "0", 10); },
  set dayOffset(v) { store.set("margin.demo.dayOffset", String(v)); },
  now() { return D.add(new Date(), this.dayOffset); },
  today() { return D.date(this.now()); },
  get isSimulated() { return this.dayOffset !== 0; },
  advanceDay() { this.dayOffset = this.dayOffset + 1; },
  resetSimulation() { this.dayOffset = 0; },
};

/* Pages-per-day index: avoids scanning every log on each lookup (years of history). */
const _pagesIdx = new WeakMap();
S.pagesOn = function (state, d) {
  let c = _pagesIdx.get(state);
  if (!c || c.n !== state.logs.length || c.logs !== state.logs) {
    const m = new Map();
    for (const l of state.logs) m.set(l.date, (m.get(l.date) || 0) + Math.max(0, l.toPage - l.fromPage));
    c = { n: state.logs.length, logs: state.logs, m }; _pagesIdx.set(state, c);
  }
  return c.m.get(D.key(d)) || 0;
};

/* =========================================================================
   FMT: port of UI/Fmt.cs
   ========================================================================= */
const Fmt = {
  Days: ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
  Months: ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"],
  dateLong(d) { return `${this.Days[d.getDay()]}, ${this.Months[d.getMonth()]} ${d.getDate()}`; },
  time(h, m) { return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`; },
  pages(n) { return n === 1 ? "1 page" : `${F.n(n)} pages`; },
  plural(day) { return day + "s"; },
  dayList(days) {
    const set = [...new Set(days)].sort((a, b) => ((a + 6) % 7) - ((b + 6) % 7));
    if (set.length === 7) return "every day";
    if (set.length === 5 && !set.includes(0) && !set.includes(6)) return "Monday to Friday";
    if (set.length === 2 && set.includes(0) && set.includes(6)) return "on weekends";
    const names = set.map(d => this.Days[d]);
    if (names.length === 1) return "on " + this.plural(names[0]);
    return "on " + names.slice(0, -1).map(n => this.plural(n)).join(", ") + " and " + this.plural(names[names.length - 1]);
  },
};

/* Shared copy (same wording as Assets/Scripts/App/Copy.cs; reviewed against docs/research/human-craft-guide.md). */
const Copy = {
  /* Each suggestion completes "When I ...". */
  IntentionSuggestions: ["finish dinner", "get on the subway", "get into bed", "have my first coffee"],
  /* "When I finish dinner". A leading "I " typed by the user is dropped to avoid "When I I ...". */
  whenI(intention) { let s = (intention || "").trim(); if (/^i\s/i.test(s)) s = s.slice(2).trimStart(); return "When I " + s; },
  levelName(l) { return ["Kindling", "Steady", "Strong", "Unstoppable"][l]; },
  ReflectionPrompts: ["What did this chapter leave you with?", "Which line would you underline, and why?", "Which character would you argue with?", "What would you tell a friend about what you read?"],
  DayLetters: ["S", "M", "T", "W", "T", "F", "S"],
};
