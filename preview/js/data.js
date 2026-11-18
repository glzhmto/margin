/* Data: catalog, quotes and real reader reviews (static JSON), plus the signed-in account.
   Accounts, friends, creators and activity are real and live in Supabase (js/net.js). */
"use strict";

const APP_VERSION = "5.3.1";

/* Defaults and choices that screens share, in one place (Unity reads the same values from Resources/Data/tuning.json). */
const PLAN = {
  days: [1, 2, 3, 4, 5],            // Monday to Friday
  daily: 15, skipDaily: 10,         // pages a day: suggested in setup, and when setup is left for later
  hour: 22, minute: 0,              // reminder time
  year: 12,                         // books this year
  dailyOptions: [5, 10, 15, 20, 30, 50],
  yearOptions: [4, 6, 12, 24, 52],
};
const FOCUS = { minutes: [15, 25, 45, 60], silenced: ["instagram", "tiktok", "youtube"] };
const TIMING = { findingMatchMs: 1500 };

function hash(str) { let x = 2166136261; for (const c of str) { x ^= c.charCodeAt(0); x = Math.imul(x, 16777619); } return x >>> 0; }

/* Personal milestones, computed from the reader's own diary. */
const ACHIEVEMENTS = [
  { id: "first-book", name: "First book", detail: "You finished your first book on Margin." },
  { id: "ten-chapters", name: "Ten chapters", detail: "You answered the quiz for 10 chapters." },
  { id: "month", name: "One month", detail: "You met your weekly goal 4 weeks in a row." },
  { id: "night-owl", name: "Night reading", detail: "You logged reading after 23:00 ten times." },
  { id: "focus-10", name: "Ten focus sessions", detail: "You completed 10 Focus mode sessions." },
  { id: "reflections-5", name: "Five reflections", detail: "You wrote five reflections in your own words." },
  { id: "theater", name: "Curtain up", detail: "You read a whole play." },
  { id: "mexico", name: "Made in Mexico", detail: "You read three books by Mexican authors." },
  { id: "paper", name: "On paper", detail: "You marked 5 books as read in print." },
  { id: "year-12", name: "Twelve in a year", detail: "You finished 12 books in one calendar year." },
];

const Data = {
  quotes: [], reviews: [], creators: [], covers: new Set(),

  async load() {
    const get = p => fetch(p).then(r => { if (!r.ok) throw new Error(p); return r.json(); });
    const base = "../Assets/Resources/";
    const [books, quotes, reviews, covers, creators] = await Promise.all([
      get(base + "Data/books.json"), get(base + "Data/quotes.json"), get(base + "Data/reviews.json"),
      get(base + "Covers/SOURCES.json").catch(() => ({})), get(base + "Data/creators.json").catch(() => ({ creators: [] })),
    ]);
    Catalog.data = books; this.quotes = quotes.quotes; this.reviews = reviews.reviews;
    this.creators = (creators.creators || []).filter(c => Catalog.findBook(c.bookId));
    Object.keys(covers).forEach(k => this.covers.add(k));
  },
  coverUrl(id) { return this.covers.has(id) ? `../Assets/Resources/Covers/${id}.jpg` : null; },
  book(id) { return Catalog.findBook(id); },
  list(id) { return (this.state().lists || []).find(l => l.id === id); },
  // Real reviews from Goodreads readers (quoted with author and link).
  reviewsFor(id) { return this.reviews.filter(r => r.bookId === id); },
  // Real book creators (BookTube, blogs) and what they said about a catalog book, with a link to the source.
  creatorsFor(id) { return this.creators.filter(c => c.bookId === id); },
  quoteOfDay(d) { const q = this.quotes; return q.length ? q[(d.getFullYear() * 400 + Math.floor((d - new Date(d.getFullYear(), 0, 0)) / 864e5)) % q.length] : null; },

  /* ---- Session ---- */
  state() { return Accounts.current()?.state; },
  me() {
    const a = Accounts.current();
    if (!a) return null;
    const s = a.state;
    return { id: a.username, uid: a.id, name: a.name || a.username, bio: a.bio, color: a.color, avatar: a.avatar, joined: s.profile.createdOn || a.created, isMe: true,
      read: s.progress.filter(p => p.finished).map(p => p.bookId), favorites: s.favorites || [] };
  },
  save() { Net.save(); },

  /* ---- Derived ---- */
  finished(state = this.state()) {
    return state.progress.filter(p => p.finished).map(p => ({ ...p, finishedOn: p.finishedOn || this.lastLogDate(state, p.bookId) }))
      .sort((a, b) => (b.finishedOn || "").localeCompare(a.finishedOn || ""));
  },
  lastLogDate(state, bookId) { let k = null; for (const l of state.logs) if (l.bookId === bookId && (!k || l.date > k)) k = l.date; return k; },
  booksInYear(state, y) { return this.finished(state).filter(p => (p.finishedOn || "").startsWith(String(y))).length; },
  weeksMeetingGoal(state, today) {
    const target = S.weeklyTarget(state.profile.goals);
    let run = 0, best = 0, cur = 0, start = HabitEngine.weekStart(today);
    const first = D.parse(state.profile.createdOn || D.key(today));
    let counting = true;
    for (let w = D.add(start, -7); w >= first; w = D.add(w, -7)) {
      let p = 0; for (let i = 0; i < 7; i++) p += S.pagesOn(state, D.add(w, i));
      if (p >= target) { run++; if (counting) cur++; } else { counting = false; run = 0; }
      best = Math.max(best, run);
    }
    return { current: cur, best: Math.max(best, cur) };
  },
  readingDaysCount(state) { return new Set(state.logs.map(l => l.date)).size; },

  achievements(state = this.state()) {
    const fin = this.finished(state).map(p => p.bookId);
    const mx = fin.filter(id => this.book(id)?.shelves.includes("mexican")).length;
    const quizCount = state.quizResults.length;
    const wk = this.weeksMeetingGoal(state, Clock.today()).best;
    const y = Clock.today().getFullYear();
    const got = {
      "first-book": fin.length >= 1, "ten-chapters": quizCount >= 10, "month": wk >= 4,
      "night-owl": (state.nightLogs || 0) >= 10, "focus-10": (state.focusSessions || []).length >= 10,
      "reflections-5": state.reflections.length >= 5, "theater": fin.some(id => this.book(id)?.shelves.includes("drama")),
      "mexico": mx >= 3, "paper": fin.length >= 5, "year-12": [y, y - 1, y - 2, y - 3].some(yy => this.booksInYear(state, yy) >= 12),
    };
    return ACHIEVEMENTS.map(a => ({ ...a, got: !!got[a.id] }));
  },

  search(q) {
    q = q.trim().toLowerCase(); if (!q) return { books: [], lists: [] };
    const norm = s => (s || "").toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");
    const has = s => norm(s).includes(norm(q));
    return {
      books: Catalog.data.books.filter(b => has(b.title) || has(b.author)),
      lists: (this.state().lists || []).filter(l => has(l.title)),
    };
  },
  notifications(state = this.state()) { return state.notifications || []; },
};

/* ---------------- Account: a thin view over the Supabase session (js/net.js) ---------------- */
const Accounts = {
  current() { return typeof Net !== "undefined" ? Net.acc : null; },
  session() { return this.current()?.username || null; },
};

/* Fills in the fields the app expects, for a new account or an older saved state. */
function normalizeState(s, name) {
  if (!s || !s.profile) { s = newState(); s.profile.name = (name || "").split(" ")[0]; }
  const def = { lists: [], social: { likedLists: [], likedReviews: [] }, want: [], dnf: [], focusSessions: [], reviews: [], favorites: [],
    yearGoal: 12, notifications: [], nightLogs: 0,
    tastes: null }; // taste quiz answers (TasteQuiz.clean: field, level, life, feel, screen, pace, length, era, region, liked); null until the quiz is taken
  for (const k in def) if (s[k] == null) s[k] = JSON.parse(JSON.stringify(def[k]));
  for (const k of ["logs", "progress", "quizResults", "reflections", "remindersScheduled"]) if (!Array.isArray(s[k])) s[k] = [];
  if (!s.profile.goals) s.profile.goals = newGoals();
  return s;
}

function defaultSettings() {
  return { publicProfile: true, activityVisibility: "Friends and followers", showCurrent: true, quotesInReminders: true, weeklySummary: true,
    focusMinutes: 25, theme: "System", textLarge: false, reduceMotion: false, countRereads: true, member: false, spoilers: true };
}
