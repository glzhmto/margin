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

/* Taste quiz (port of Core/TasteQuiz.cs): ten quick, skippable questions that connect the catalog to the
   reader's real life (field, life now, mood, films and series, reading habit) plus pace, length, era, region
   and books they liked. A transparent weighted sum over the per-book metadata in books.json (moods, pace,
   fields, life, screen, fieldNotes); every result says why, citing the reader's own answers. */
const TasteQuiz = {
  // Vocabularies of the per-book metadata (books.json). Same lists as Core/TasteQuiz.cs.
  MoodVocab: ["thoughtful", "moving", "thrilling", "unsettling", "hopeful", "funny"],
  PaceVocab: ["fast", "medium", "slow"],
  FieldVocab: ["law", "health", "psychology", "tech", "business", "communication", "humanities", "arts", "education"],
  LifeVocab: ["love", "family", "meaning", "pressure", "change", "friendship"],
  ScreenVocab: ["scifi", "period", "horror", "crime", "epic", "latam"],

  FieldWeight: 3.0, LifeWeight: 2.0, MoodWeight: 1.5, ScreenWeight: 1.25, PaceMatch: 1.5, PaceNear: 0.5,
  LengthMatch: 2.0, LengthNear: 0.75, EraMatch: 2.0, RegionMatch: 2.0,
  SeedTagWeight: 1.0, SeedShelfWeight: 0.5, SeedMoodWeight: 0.5, SeedMax: 4.0,
  BeginnerShort: 2.0, BeginnerFast: 1.0, BeginnerLong: -1.5, CasualShort: 1.0, CasualFast: 0.5, CasualLong: -0.75, HomeBoost: 0.5,
  ShortPages: 150, MediumPages: 300, ModernYear: 1990, MaxPerAuthor: 1, MaxPerShelf: 3, Questions: 10,
  Any: "any", Both: "both", None: "none", Other: "other", World: "world",
  GenericShelves: ["classics", "short-reads", "modern", "most-read"], GenericTags: ["modern"],

  Fields: [
    { id: "law", label: "Law", reason: "You study Law" },
    { id: "health", label: "Medicine or health", reason: "You're in health", hint: "Medicine, Nursing, Nutrition, Dentistry" },
    { id: "psychology", label: "Psychology", reason: "You study Psychology" },
    { id: "tech", label: "Engineering or tech", reason: "You're in engineering and tech", hint: "Engineering, Computer Science, Data" },
    { id: "business", label: "Business or politics", reason: "You're in business and politics", hint: "Business, Economics, International Relations" },
    { id: "communication", label: "Communication or marketing", reason: "You study Communication", hint: "Communication, Marketing, Journalism" },
    { id: "humanities", label: "Humanities", reason: "You study the humanities", hint: "History, Philosophy, Literature, Languages" },
    { id: "arts", label: "Arts or design", reason: "You're in arts and design", hint: "Architecture, Design, Music, Film" },
    { id: "education", label: "Education", reason: "You study Education", hint: "Teaching, Pedagogy" },
    { id: "other", label: "Something else", reason: null, hint: "Or not studying right now" },
  ],
  Levels: [
    { id: "barely", label: "I barely read", reason: null, hint: "No problem: we'll start with short, gripping books" },
    { id: "yearly", label: "About a book a year", reason: null },
    { id: "several", label: "A few books a year", reason: null },
    { id: "lots", label: "A lot", reason: null },
  ],
  Life: [
    { id: "love", label: "Love and relationships", reason: "Love is on your mind, and it's at the heart of this one" },
    { id: "family", label: "Family", reason: "Family is on your mind, and it's at the center of this book" },
    { id: "meaning", label: "Looking for meaning or faith", reason: "You're looking for meaning, and so is this book" },
    { id: "pressure", label: "Pressure and stress", reason: "Life feels heavy right now: this book knows that weight" },
    { id: "change", label: "Moving or big changes", reason: "You're going through changes, and so does this story" },
    { id: "friendship", label: "Friendship", reason: "Friendship matters to you right now: it's at the heart of this one" },
    { id: "none", label: "Nothing special", reason: null },
  ],
  Moods: [
    { id: "thoughtful", label: "Thoughtful", reason: "You want a book that makes you think" },
    { id: "moving", label: "Moved", reason: "You want to be moved" },
    { id: "thrilling", label: "On the edge of my seat", reason: "You want to be on the edge of your seat" },
    { id: "unsettling", label: "Unsettled or scared", reason: "You want to be unsettled" },
    { id: "hopeful", label: "Hopeful", reason: "You want something hopeful" },
    { id: "funny", label: "Entertained", reason: "You want to be entertained" },
  ],
  Screens: [
    { id: "scifi", label: "Sci-fi and dystopian series", reason: "You watch sci-fi and dystopias" },
    { id: "period", label: "Period dramas and romance", reason: "You watch period dramas" },
    { id: "horror", label: "Horror", reason: "You watch horror" },
    { id: "crime", label: "Crime and thrillers", reason: "You watch crime and thrillers" },
    { id: "epic", label: "Historical epics and adventure", reason: "You watch epics and adventure" },
    { id: "latam", label: "Latin American films and telenovelas", reason: "You watch Latin American drama" },
  ],
  Paces: [
    { id: "fast", label: "Quick and gripping", reason: "quick and gripping", hint: "You want to know what happens next" },
    { id: "slow", label: "Slow and deep", reason: "slow and deep", hint: "You like to stop and think" },
    { id: "any", label: "Either works", reason: null },
  ],
  Lengths: [
    { id: "short", label: "Short", reason: "short", hint: "Up to 150 pages" },
    { id: "medium", label: "Medium", reason: "medium length", hint: "151 to 300 pages" },
    { id: "long", label: "Long", reason: "long", hint: "More than 300 pages" },
    { id: "any", label: "I don't mind", reason: null, hint: "Any length" },
  ],
  Eras: [
    { id: "classic", label: "Classics", reason: "a classic", hint: "From the ancient world to the 1980s" },
    { id: "modern", label: "Recent books", reason: "recent", hint: "Published since 1990" },
    { id: "both", label: "Both", reason: null },
  ],
  Regions: [
    { id: "latam", label: "Mexico and Latin America", reason: "You asked for Mexico and Latin America" },
    { id: "world", label: "Anywhere in the world", reason: null },
  ],

  find(options, id) { return id == null ? null : options.find(o => o.id === id) || null; },

  // True when no answer changes the ranking (everything skipped or "I don't mind").
  isEmpty(tastes) {
    const t = this.clean(tastes);
    return (t.field == null || t.field === this.Other)
      && (t.level == null || t.level === "several" || t.level === "lots")
      && t.life.every(x => x === this.None) && !t.feel.length && !t.screen.length
      && (t.pace == null || t.pace === this.Any) && (t.length == null || t.length === this.Any)
      && (t.era == null || t.era === this.Both) && (t.region == null || t.region === this.World)
      && !t.liked.length;
  },
  // Drops unknown or repeated ids and migrates saves from the first quiz ({ feel: [themes], length, era }).
  clean(t) {
    const r = { field: null, level: null, life: [], feel: [], screen: [], pace: null, length: null, era: null, region: null, liked: [] };
    if (!t) return r;
    const id = (opts, x) => this.find(opts, x)?.id ?? null;
    const addAll = (into, from, opts) => { for (const x of from || []) if (this.find(opts, x) && !into.includes(x)) into.push(x); };
    r.field = id(this.Fields, t.field); r.level = id(this.Levels, t.level); r.pace = id(this.Paces, t.pace);
    r.length = id(this.Lengths, t.length); r.region = id(this.Regions, t.region);
    r.era = id(this.Eras, t.era) ?? ({ ancient: "classic", early: "classic", "19th": "classic", any: this.Both }[t.era] || null);
    addAll(r.life, t.life, this.Life); addAll(r.screen, t.screen, this.Screens);
    for (const f of t.feel || []) {
      if (this.find(this.Moods, f)) { if (!r.feel.includes(f)) r.feel.push(f); continue; }
      // Themes of the first quiz, mapped to the closest new answer. "theater" has none.
      if (f === "ideas") addAll(r.feel, ["thoughtful"], this.Moods);
      else if (f === "dark") addAll(r.feel, ["unsettling"], this.Moods);
      else if (f === "love") addAll(r.life, ["love"], this.Life);
      else if (f === "adventure") addAll(r.screen, ["epic"], this.Screens);
      else if (f === "power") addAll(r.screen, ["scifi"], this.Screens);
      else if (f === "mexico" && r.region == null) r.region = "latam";
    }
    // "Nothing special" can't go with anything else.
    if (r.life.includes(this.None) && r.life.length > 1) r.life.splice(r.life.indexOf(this.None), 1);
    for (const x of t.liked || []) if (x && !r.liked.includes(x)) r.liked.push(x);
    return r;
  },

  isModern(b) { return (b.shelves || []).includes("modern") || b.year >= this.ModernYear; },
  lengthOf(b) { return b.pages <= this.ShortPages ? "short" : b.pages <= this.MediumPages ? "medium" : "long"; },
  lengthRank(id) { return id === "short" ? 0 : id === "medium" ? 1 : 2; },
  shared(a, b, skip, ci) {
    const r = [];
    if (!a || !b) return r;
    const n = x => ci ? x.toLowerCase() : x, bs = b.map(n), sk = skip.map(n);
    for (const x of a) if (x != null && !sk.includes(n(x)) && bs.includes(n(x)) && !r.some(y => n(y) === n(x))) r.push(x);
    return r;
  },

  /* Score and reason for one book: the strongest matching answer in the reader's own terms, then up to two
     short facts that also matched. Reason is null when nothing matched. */
  score(b, tastes, catalog = Catalog.data) {
    const t = this.clean(tastes), has = (list, x) => !!list && list.includes(x);
    let score = 0;
    const clauses = []; // [weight, text], in priority order for ties

    const field = this.find(this.Fields, t.field);
    if (field && field.reason && has(b.fields, field.id)) {
      score += this.FieldWeight;
      const note = b.fieldNotes && b.fieldNotes[field.id];
      clauses.push([this.FieldWeight, field.reason + ": " + (note || "it speaks to your field")]);
    }

    const life = t.life.filter(x => x !== this.None && has(b.life, x));
    if (life.length) { score += this.LifeWeight * life.length; clauses.push([this.LifeWeight * life.length, this.find(this.Life, life[0]).reason]); }

    let seed = 0, seedText = null;
    if (catalog) for (const id of t.liked) {
      const lb = (catalog.books || []).find(x => x.id === id);
      if (!lb || lb.id === b.id) continue;
      const tags = this.shared(lb.tags, b.tags, this.GenericTags, true);
      let s = tags.length * this.SeedTagWeight + this.shared(lb.shelves, b.shelves, this.GenericShelves, false).length * this.SeedShelfWeight
        + this.shared(lb.moods, b.moods, [], false).length * this.SeedMoodWeight;
      if (lb.author && lb.author === b.author) s += this.SeedTagWeight;
      s = Math.min(this.SeedMax, s);
      if (s > seed) {
        seed = s;
        seedText = tags.length >= 2 ? `You liked ${lb.title}: this one also has ${tags[0]} and ${tags[1]}`
          : tags.length === 1 ? `You liked ${lb.title}: this one also has ${tags[0]}` : `You liked ${lb.title}: this one has a similar feel`;
      }
    }
    score += seed; clauses.push([seed, seedText]);

    const moods = t.feel.filter(x => has(b.moods, x));
    score += this.MoodWeight * moods.length; clauses.push([this.MoodWeight * moods.length, moods.length ? this.find(this.Moods, moods[0]).reason : null]);

    const screens = t.screen.filter(x => has(b.screen, x));
    score += this.ScreenWeight * screens.length; clauses.push([this.ScreenWeight * screens.length, screens.length ? this.find(this.Screens, screens[0]).reason : null]);

    if (t.region === "latam" && (has(b.shelves, "mexican") || has(b.shelves, "latin-american"))) {
      score += this.RegionMatch;
      clauses.push([this.RegionMatch, this.find(this.Regions, "latam").reason + (has(b.shelves, "mexican") ? ": this one is Mexican" : ": this one is Latin American")]);
    }

    const facts = [], len = this.lengthOf(b);
    if (t.length != null && t.length !== this.Any) {
      if (len === t.length) { score += this.LengthMatch; facts.push(this.find(this.Lengths, len).reason); }
      else if (this.lengthRank(len) < this.lengthRank(t.length)) score += this.LengthNear;
    }
    if (t.pace != null && t.pace !== this.Any && b.pace != null) {
      if (b.pace === t.pace) { score += this.PaceMatch; facts.push(this.find(this.Paces, t.pace).reason); }
      else if (b.pace === "medium") score += this.PaceNear;
    }
    if (t.era === "classic" || t.era === "modern") {
      if (this.isModern(b) === (t.era === "modern")) { score += this.EraMatch; facts.push(this.find(this.Eras, t.era).reason); }
    }
    // Reading level: beginners get short, gripping books first.
    if (t.level === "barely" || t.level === "yearly") {
      const beginner = t.level === "barely";
      if (len === "short") { score += beginner ? this.BeginnerShort : this.CasualShort; if (!facts.includes("short")) facts.push("short"); }
      if (b.pace === "fast") { score += beginner ? this.BeginnerFast : this.CasualFast; if (!facts.includes("quick and gripping")) facts.push("quick and gripping"); }
      if (len === "long") score += beginner ? this.BeginnerLong : this.CasualLong;
    }

    // The strongest clause wins; ties go to the earlier one (field, life, liked, mood, screen, region).
    let main = null, best = 0;
    for (const [w, text] of clauses) if (text != null && w > best) { best = w; main = text; }
    const parts = main != null ? [main, ...facts.slice(0, 2)] : facts.slice(0, 3);
    if (!parts.length) return { score, reason: null };
    const reason = parts.join(" · ");
    return { score, reason: reason[0].toUpperCase() + reason.slice(1) };
  },
  order(x, y) { return y.score - x.score || x.book.pages - y.book.pages || (x.book.id < y.book.id ? -1 : x.book.id > y.book.id ? 1 : 0); },

  /* The best books for these answers, leaving out excluded ids (finished, current) and liked books. Sorted by
     score, then shorter book, then id. Diversity: at most MaxPerAuthor per author and MaxPerShelf per distinctive
     shelf, unless that leaves fewer than count; books that match come first. */
  recommend(tastes, exclude, count = 5, catalog = Catalog.data) {
    const t = this.clean(tastes), empty = this.isEmpty(t), seen = new Set(), ex = new Set(exclude || []), all = [];
    if (!catalog || !catalog.books) return all;
    for (const b of catalog.books) {
      if (!b || !b.id || seen.has(b.id)) continue;
      seen.add(b.id);
      if (ex.has(b.id) || t.liked.includes(b.id)) continue;
      let { score, reason } = this.score(b, t, catalog);
      if (reason == null) reason = empty && b.pages <= this.ShortPages ? "Short: a good one to start with" : score > 0 ? "Close to your answers" : "From the Margin catalog";
      all.push({ book: b, score, reason });
    }
    all.sort((x, y) => this.order(x, y));
    const picked = [], authors = new Map(), shelves = new Map(), n = Math.max(0, count);
    this.diverse(all.filter(r => r.score > 0), n, picked, authors, shelves);
    this.diverse(all.filter(r => r.score <= 0), n, picked, authors, shelves);
    return picked;
  },
  diverse(sorted, count, picked, authors, shelves) {
    const skipped = [];
    for (const r of sorted) {
      if (picked.length >= count) break;
      const a = r.book.author || "", own = [...new Set((r.book.shelves || []).filter(s => !this.GenericShelves.includes(s)))];
      if ((authors.get(a) || 0) >= this.MaxPerAuthor || own.some(s => (shelves.get(s) || 0) >= this.MaxPerShelf)) { skipped.push(r); continue; }
      picked.push(r);
      authors.set(a, (authors.get(a) || 0) + 1);
      for (const s of own) shelves.set(s, (shelves.get(s) || 0) + 1);
    }
    // Not enough variety: fill with the best of the skipped ones, in order.
    for (const r of skipped) {
      if (picked.length >= count) break;
      picked.push(r);
      const a = r.book.author || ""; authors.set(a, (authors.get(a) || 0) + 1);
    }
  },
  // What the results screen shows: the books that match something (up to five), and never fewer than three.
  results(tastes, exclude, catalog = Catalog.data) {
    const recs = this.recommend(tastes, exclude, 5, catalog), shown = recs.filter(r => r.score > 0);
    return shown.length >= 3 ? shown : recs.slice(0, 3);
  },
  // Home's "For you": reading-based recommendations with a share of the quiz score added (liked books left out).
  boost(recs, tastes, count, catalog = Catalog.data) {
    const t = this.clean(tastes);
    if (!recs) return [];
    if (this.isEmpty(t)) return recs.slice(0, count);
    const r = [];
    for (const x of recs) {
      if (!x || !x.book || t.liked.includes(x.book.id)) continue;
      const { score, reason } = this.score(x.book, t, catalog), add = score * this.HomeBoost;
      r.push({ book: x.book, score: x.score + add, reason: add > x.score && reason != null ? reason : x.reason });
    }
    r.sort((x, y) => this.order(x, y));
    return r.slice(0, Math.max(0, count));
  },
  // Books the reader shouldn't be offered: finished ones and the current one.
  excludeFor(s) {
    const r = new Set();
    if (!s) return r;
    for (const p of s.progress) if (p.finished) r.add(p.bookId);
    if (s.profile && s.profile.currentBookId) r.add(s.profile.currentBookId);
    return r;
  },
};

/* "Find your next book" (port of Core/NextBook.cs): five questions for readers who already have a history.
   Pure function: history (finished books and their ratings, current book, want-to-read, dropped books) becomes
   an affinity per tag, shelf, mood and author; the answers add to that. Every weight is in NextBook.W. */
const NextBook = {
  W: {
    RatingNeutral: 3, RatingSpan: 2,          // rating 5 -> +1, 3 -> 0, 1 -> -1
    Unrated: 0.25, Current: 0.3, Want: 0.4, Dropped: -1,   // seed weights without a rating
    Tag: 1.0, Shelf: 0.5, Mood: 0.5, Author: 1.0, AffinityCap: 4.0,
    SameMult: 1.5, NewMult: 0.2, NewBonus: 1.5, // "like what I loved" / "something new" scale the history
    WantBoost: 0.75, ReasonMin: 0.5,
    MoodMatch: 2.5, LengthMatch: 2.0, LengthNear: 0.75, KindMatch: 2.5, RegionMatch: 3.0,
  },
  get GenericShelves() { return TasteQuiz.GenericShelves; }, // the same "too broad to mean much" shelves
  Moods: [
    { id: "thoughtful", label: "Something to think about", phrase: "thought-provoking" },
    { id: "moving", label: "Something that moves me", phrase: "moving" },
    { id: "thrilling", label: "Something gripping", phrase: "gripping" },
    { id: "unsettling", label: "Something dark and unsettling", phrase: "unsettling" },
    { id: "hopeful", label: "Something hopeful", phrase: "hopeful" },
    { id: "funny", label: "Something funny", phrase: "funny" },
  ],
  Lengths: [
    { id: "short", label: "A few evenings", hint: "Up to 150 pages", phrase: "short" },
    { id: "medium", label: "A couple of weeks", hint: "151 to 300 pages", phrase: "mid-length" },
    { id: "long", label: "I'm in for the long haul", hint: "More than 300 pages", phrase: "long" },
    { id: "any", label: "No rush", hint: "Any length" },
  ],
  Novelties: [
    { id: "same", label: "More like what I've loved" },
    { id: "new", label: "Something new to me", phrase: "something new" },
    { id: "either", label: "Either is fine" },
  ],
  Kinds: [
    { id: "story", label: "A novel or a story", phrase: "a story" },
    { id: "ideas", label: "Essays and ideas", phrase: "essays" },
    { id: "play", label: "A play", phrase: "a play" },
    { id: "any", label: "Surprise me" },
  ],
  Regions: [
    { id: "mexican", label: "Mexican authors", phrase: "a Mexican author" },
    { id: "latam", label: "Latin American voices", hint: "Mexico included", phrase: "Latin American voices" },
    { id: "any", label: "Anywhere in the world" },
  ],
  Questions: [
    { key: "mood", title: "What are you in the mood for?", sub: "Pick the feeling you want right now.", options: () => NextBook.Moods },
    { key: "length", title: "How much time do you have?", sub: "Ten pages take about fifteen minutes.", options: () => NextBook.Lengths },
    { key: "novelty", title: "Familiar or new?", sub: "We'll look at the books you've rated.", options: () => NextBook.Novelties },
    { key: "kind", title: "What kind of read?", sub: "Stories, ideas or theater.", options: () => NextBook.Kinds },
    { key: "region", title: "Where should the author be from?", sub: "Mexican and Latin American voices are in the catalog.", options: () => NextBook.Regions },
  ],

  has(list, x) { return !!list && list.includes(x); },
  kindOf(b) { return this.has(b.shelves, "essays") ? "ideas" : this.has(b.shelves, "drama") ? "play" : "story"; },
  isEmpty(a) { return !a || Object.values(a).every(v => v == null || ["any", "either"].includes(v)); },
  joinList(xs) { return xs.length < 2 ? xs.join("") : xs.slice(0, -1).join(", ") + " and " + xs[xs.length - 1]; },

  // What each book in the reader's history says about their taste: { book, weight, how } with weight -1..1.
  seeds(state, catalog) {
    const W = this.W, find = id => (catalog.books || []).find(b => b.id === id), cur = state.profile.currentBookId, out = [], seen = new Set();
    const add = (id, weight, how, rating = 0) => { const book = find(id); if (book && !seen.has(id)) { seen.add(id); out.push({ book, weight, how, rating }); } };
    for (const id of state.dnf || []) add(id, W.Dropped, "dropped");
    for (const p of state.progress || []) {
      if (!p.finished || p.bookId === cur) continue;
      add(p.bookId, p.rating > 0 ? (p.rating - W.RatingNeutral) / W.RatingSpan : W.Unrated, "finished", p.rating);
    }
    add(cur, W.Current, "reading");
    for (const id of state.want || []) add(id, W.Want, "want");
    return out;
  },
  // How much a seed's tags, shelves, moods and author overlap a candidate.
  overlap(seed, b) {
    const W = this.W, T = TasteQuiz;
    return T.shared(seed.tags, b.tags, [], true).length * W.Tag + T.shared(seed.shelves, b.shelves, this.GenericShelves, false).length * W.Shelf
      + T.shared(seed.moods, b.moods, [], false).length * W.Mood + (seed.author && seed.author === b.author ? W.Author : 0);
  },
  historyPhrase(s) {
    const t = s.book.title;
    return s.how === "finished" ? (s.rating > 0 ? `you rated ${t} ${s.rating}★` : `you finished ${t}`) : s.how === "reading" ? `you're reading ${t}` : `${t} is on your want-to-read list`;
  },

  /* Ranked books for this reader, best first: [{ book, score, reason }]. Finished, current and dropped books
     are left out. answers: { mood, length, novelty, kind, region } (ids above, null = skipped). */
  recommend(state, answers, catalog = Catalog.data, count = 5) {
    const W = this.W, a = answers || {}, seeds = this.seeds(state, catalog), want = new Set(state.want || []);
    const ex = new Set([...(state.dnf || []), state.profile.currentBookId, ...(state.progress || []).filter(p => p.finished).map(p => p.bookId)]);
    const results = [];
    for (const b of catalog.books || []) {
      if (ex.has(b.id)) continue;
      let affinity = 0, best = null, bestVal = 0;
      for (const s of seeds) {
        if (s.book.id === b.id) continue;
        const v = s.weight * this.overlap(s.book, b);
        affinity += v;
        if (v > bestVal) { bestVal = v; best = s; }
      }
      affinity = Math.max(-W.AffinityCap, Math.min(W.AffinityCap, affinity));
      let score = affinity * (a.novelty === "same" ? W.SameMult : a.novelty === "new" ? W.NewMult : 1);
      if (a.novelty === "new" && bestVal <= 0) score += W.NewBonus;
      if (want.has(b.id)) score += W.WantBoost;

      const adjectives = [], extras = [], len = TasteQuiz.lengthOf(b);
      const mood = this.Moods.find(m => m.id === a.mood);
      if (mood && this.has(b.moods, mood.id)) { score += W.MoodMatch; adjectives.push(mood.phrase); }
      const length = this.Lengths.find(l => l.id === a.length);
      if (length && length.phrase) {
        if (len === length.id) { score += W.LengthMatch; adjectives.push(length.phrase); }
        else if (TasteQuiz.lengthRank(len) < TasteQuiz.lengthRank(length.id)) score += W.LengthNear;
      }
      const kind = this.Kinds.find(k => k.id === a.kind);
      if (kind && kind.phrase && this.kindOf(b) === kind.id) { score += W.KindMatch; if (kind.id !== "story") extras.push(kind.phrase); }
      const mex = this.has(b.shelves, "mexican"), latam = mex || this.has(b.shelves, "latin-american");
      if ((a.region === "mexican" && mex) || (a.region === "latam" && latam)) { score += W.RegionMatch; extras.push(this.Regions.find(r => r.id === a.region).phrase); }
      const novel = a.novelty === "new" && bestVal <= 0;
      if (novel) extras.push("something new");

      const wanted = [adjectives.length ? "something " + this.joinList(adjectives) : null, ...extras].filter(Boolean);
      const parts = [];
      if (best && bestVal >= W.ReasonMin) parts.push(this.historyPhrase(best));
      if (wanted.length) parts.push((parts.length ? "" : "you ") + "wanted " + this.joinList(wanted));
      const reason = parts.length ? "Because " + this.joinList(parts) : "A classic to discover";
      results.push({ book: b, score, reason });
    }
    results.sort((x, y) => TasteQuiz.order(x, y));
    return results.slice(0, Math.max(0, count));
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

/* The demo account ("Try a demo account" on Welcome): six weeks of a believable reader, so someone who scans the
   QR at the pitch sees a full diary right away. Same plan and same numbers as Core/DemoData.cs. */
const DemoData = {
  DAYS: 42,
  PLAN: [["the-little-prince", 42, 5], ["animal-farm", 33, 4], ["1984", 19, 0]], // id, day it was started (counting back), rating when finished
  MISSED: [26, 9],
  // Fills the diary; returns { bookId: "YYYY-MM-DD" } for the books it finished.
  seed(state, today) {
    const finished = {}, goals = state.profile.goals;
    Object.assign(goals, { dailyPages: 15, readingDays: [1, 2, 3, 4, 5, 6], intention: "after dinner", remindersEnabled: false });
    state.logs = []; state.quizResults = []; state.progress = [];
    state.profile.createdOn = D.key(D.add(today, -this.DAYS - 1));
    state.profile.onboardingComplete = true;
    let seed = 451; // Park-Miller: exact in JS and C#
    const rnd = (a, b) => { seed = (seed * 48271) % 2147483647; return a + seed % (b - a); };
    this.PLAN.forEach(([id, start, rating], k) => {
      const book = Catalog.findBook(id); if (!book) return;
      const end = k + 1 < this.PLAN.length ? this.PLAN[k + 1][1] + 1 : 1;
      ReadingService.setCurrentBook(state, id);
      for (let i = start; i >= end; i--) {
        const day = D.add(today, -i);
        if (!S.isReadingDay(goals, day) || this.MISSED.includes(i)) continue;
        const p = S.progressFor(state, id);
        const last = rating > 0 && i === end;
        const target = last ? book.pages : Math.min(rating > 0 ? book.pages : book.pages - 1, p.currentPage + rnd(9, 19));
        if (target <= p.currentPage) continue;
        const r = ReadingService.checkIn(state, target, day);
        if (r.pendingQuiz) QuizEngine.record(state, id, r.pendingQuiz.number, { correct: rnd(2, 4), total: r.pendingQuiz.quiz.questions.length }, day);
        if (r.finishedBook) { finished[id] = D.key(day); break; }
      }
      if (rating > 0) {
        const p = S.progressFor(state, id);
        Object.assign(p, { currentPage: book.pages, finished: true, rating });
        if (!finished[id]) finished[id] = D.key(D.add(today, -end));
        p.finishedOn = finished[id];
      }
    });
    ReadingService.setCurrentBook(state, this.PLAN[this.PLAN.length - 1][0]);
    return finished;
  },
  // The parts of the demo that live outside the core diary: want to read, a list, Focus sessions, the year goal.
  extras(state, today, username) {
    state.yearGoal = 12;
    state.want = ["klara-and-the-sun", "hurricane-season", "the-great-gatsby"];
    state.lists = [{ id: "demo-short", owner: username, title: "Short books for exam weeks", description: "Under 150 pages, one sitting each.",
      books: ["the-little-prince", "animal-farm", "the-metamorphosis", "aura"], likes: 0, comments: 0, created: D.key(D.add(today, -20)) }];
    state.focusSessions = state.logs.filter((l, i) => i % 2 === 0).slice(-9).map(l => ({ date: l.date, minutes: 25 }));
    state.notifications = [{ at: D.stamp(Clock.now()), kind: "welcome", text: "This is a demo account with six weeks of reading. Log a page, take a quiz or start Focus mode." }];
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
