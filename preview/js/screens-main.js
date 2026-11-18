/* Main tabs: Home, Explore, Library, Profile; plus Activity and Notifications. Social data is real (js/net.js). */
"use strict";

function bookLink(b) { return h("button.strong", { style: { fontFamily: "var(--serif)", fontSize: "1.06em" }, onclick: e => { e.stopPropagation(); Nav.push(new BookScreen(b.id)); } }, b.title); }
function userLink(p) { return h("button.strong", { onclick: e => { e.stopPropagation(); Nav.push(new UserScreen(p)); } }, pfirst(p)); }

/* One row of real activity (from the activity table). */
function activityItem(a, opts = {}) {
  const u = Social.profiles.get(a.user_id) || opts.user; if (!u) return null;
  const b = a.book_id ? Data.book(a.book_id) : null, d = a.data || {};
  let what, go = b ? () => Nav.push(new BookScreen(b.id)) : () => Nav.push(new UserScreen(u));
  switch (a.kind) {
    case "pages": if (!b) return null; what = ["read ", h("span.num", F.pages(Math.max(0, (d.to || 0) - (d.from || 0)))), " of ", bookLink(b), h("span.faint.num", ` · p. ${d.to}`)]; break;
    case "finished": if (!b) return null; what = ["finished ", bookLink(b)]; break;
    case "quiz": if (!b) return null; what = ["took the chapter ", d.chapter, " quiz for ", bookLink(b), `: ${d.correct} of ${d.total}`]; break;
    case "started": if (!b) return null; what = ["started ", bookLink(b)]; break;
    case "review": if (!b) return null; what = ["reviewed ", bookLink(b), " ", d.rating ? stars(d.rating, 11) : null]; break;
    case "post": what = ["published ", h("span.strong", `“${d.title || "a post"}”`)]; go = () => Nav.push(new PostScreen(d.post_id)); break;
    case "joined": what = ["joined Margin"]; break;
    default: return null;
  }
  return h("div.feeditem", { onclick: go },
    h("button", { onclick: e => { e.stopPropagation(); Nav.push(new UserScreen(u)); }, "aria-label": pname(u) }, avatar(pav(u), 36)),
    h("div.grow", h("div", { style: { fontSize: "14.5px", lineHeight: 1.4 } }, opts.noUser ? null : [userLink(u), " "], ...what), h("div.xs.faint.mt2", F.ago(new Date(a.created_at), new Date()))),
    b ? cover(b, 44) : null);
}

function friendsEmpty(text) {
  return h("div.card.block.empty", { style: { margin: "4px 16px 0" } }, h("div.h3", "No friends yet"), h("p.small", text || "Invite someone and read together. You'll see their pages here."),
    h("button.btn.sm.primary.mt12", { style: { margin: "12px auto 0" }, onclick: () => Nav.push(new FriendsScreen()) }, icon("users"), "Invite a friend"));
}

/* "Reading together" card for one shared pact. */
function pactCard(x) {
  const b = Data.book(x.f.shared_book_id), s = Data.state(), mine = s.progress.find(p => p.bookId === b.id), p = x.p;
  const myTxt = mine?.finished ? "Finished" : mine && mine.currentPage ? `Page ${mine.currentPage} of ${b.pages}` : "Not started";
  const theirs = (p.books_read || []).includes(b.id) ? { r: 1, t: "Finished" } : p.current_book_id === b.id ? { r: (p.current_page || 0) / b.pages, t: `Page ${p.current_page || 0} of ${b.pages}` } : { r: 0, t: "Not started yet" };
  const line = (who, ratio, txt) => h("div.mt8", h("div.row.xs", h("span.grow.strong", who), h("span.faint.num", txt)), h("div.mt4", bar(ratio)));
  return h("button.card.block", { style: { margin: "0 16px 10px", width: "calc(100% - 32px)", textAlign: "left", display: "block" }, onclick: () => Nav.push(new UserScreen(p)) },
    h("div.row.top.gap12", cover(b, 56),
      h("div.grow", h("div.h3", b.title), h("div.small.muted", `with ${pname(p)}`),
        line("You", mine?.finished ? 1 : (mine?.currentPage || 0) / b.pages, myTxt), line(pfirst(p), theirs.r, theirs.t))));
}

class HomeScreen extends Screen {
  constructor() { super(); this.live = true; }
  build() {
    const s = Data.state(), today = Clock.today(), g = s.profile.goals;
    const m = HabitEngine.compute(s, today), weekly = HabitEngine.weekly(s, today), streak = Data.weeksMeetingGoal(s, today);
    const book = Data.book(s.profile.currentBookId), unread = Data.notifications().some(n => n.at > (s.notificationsSeen || "")) || Social.incoming.length > 0;

    const head = h("div.row.pad", { style: { height: "48px" } }, h("div.wordmark.grow", "margin"),
      Clock.isSimulated ? h("span.xs.accent.strong", `+${Clock.dayOffset} d`) : null,
      h("button.iconbtn", { "aria-label": "Friends", onclick: () => Nav.push(new FriendsScreen()) }, icon("users"), Social.incoming.length ? h("span.dot") : null),
      h("button.iconbtn", { "aria-label": "Notifications", onclick: () => Nav.push(new NotificationsScreen()) }, icon("bell"), unread ? h("span.dot") : null));

    // Reading momentum
    const week = h("div.week", Array.from({ length: 7 }, (_, i) => {
      const d = D.add(weekly.weekStart, i), p = S.pagesOn(s, d), rest = !S.isReadingDay(g, d);
      const mk = h("div.m" + (rest && !p ? ".rest" : "") + (D.same(d, today) ? ".today" : ""), { title: `${Fmt.Days[d.getDay()]}: ${F.pages(p)}` });
      if (p) { const f = h("i"); f.style.height = `${Math.min(1, p / g.dailyPages) * 100}%`; mk.append(f); }
      return h("div.d", mk, h("span.l", Copy.DayLetters[d.getDay()]));
    }));
    const left = weekly.target - weekly.pagesRead;
    const mom = h("div.card.momentum",
      h("div.row", h("span.caps.grow", "Reading momentum"), h("span.xs.faint", m.trend > 0 ? "rising" : m.trend < 0 ? "falling" : "steady")),
      h("div.row.mt6", { style: { alignItems: "baseline", gap: "10px" } }, h("span.level", Copy.levelName(m.level)),
        streak.current ? h("span.small.muted", streak.current === 1 ? "You met your goal last week" : `${streak.current} weeks in a row meeting your goal`) : null),
      week,
      h("div.row.mt12.small", h("span.grow.num", h("b", F.n(weekly.pagesRead)), ` of ${F.n(weekly.target)} pages this week`), h("span.faint.num", left > 0 ? `${left} to go` : "goal met")),
      bar(weekly.pagesRead / weekly.target, "ink"));
    mom.querySelector(".bar").classList.add("mt6");

    // Reading
    let reading;
    if (book) {
      const p = S.progressFor(s, book.id), ch = Book.chapterAt(book, Math.max(1, p.currentPage + 1));
      const toEnd = ch ? ch.endPage - p.currentPage : 0;
      reading = h("div.card.block.mt12", { style: { margin: "12px 16px 0" } },
        h("div.row.top.gap12", cover(book, 72, { onclick: () => Nav.push(new BookScreen(book.id)) }),
          h("div.grow", h("div.caps", "Reading"), h("div.h3.mt4", book.title), h("div.small.muted", book.author),
            h("div.mt8", bar(p.currentPage / book.pages)),
            h("div.xs.muted.mt6.num", `Page ${p.currentPage} of ${book.pages}`, ch ? h("span.dotsep") : null, ch ? `${toEnd} to finish “${ch.title}”` : null))),
        h("div.row.gap8.mt12", h("button.btn.sm.accent", { style: { flex: 1 }, onclick: () => LogSheet.open() }, "Log reading"),
          h("button.btn.sm.secondary", { onclick: () => Nav.push(new FocusScreen()) }, icon("moon"), "Focus mode")));
    } else {
      // No current book. First book: the onboarding taste quiz (saved answers open on the matches).
      // Later books: "Find your next book", which also learns from what they read (the same quiz as in Explore).
      const first = !Data.finished(s).length, matched = s.tastes && !TasteQuiz.isEmpty(s.tastes);
      // It goes above the momentum card, so it's the first thing on Home.
      reading = h("div.card.block", { style: { margin: "8px 16px 0" } },
        h("div.row.top.gap12", h("span.iconbtn", { style: { background: "var(--accent)", color: "#fff", width: "44px", height: "44px", borderRadius: "22px", flex: "none" } }, icon("bookopen")),
          h("div.grow", h("div.h3", first ? "Choose your first book" : "Choose your next book"),
            h("p.small.muted.mt4", !first ? "Five questions, matched to what you've read." : matched ? "We matched books from the catalog to your answers." : "Answer a few questions and we'll match you with a book."))),
        h("div.row.gap8.mt12", h("button.btn.sm.accent", { style: { flex: 1 }, onclick: () => Nav.push(first ? new TasteQuizScreen() : new NextBookScreen()) }, first && matched ? "See my matches" : "Find my book"),
          h("button.btn.sm.secondary", { onclick: () => Nav.reset(new ExploreScreen(), "explore") }, "Browse")));
    }

    // Shared pacts
    const pacts = Social.pacts();
    const together = pacts.length ? [sect("Reading together"), pacts.map(pactCard)] : null;

    // Friends reading now (real profiles of accepted friends)
    const friends = Social.friends.filter(x => Data.book(x.p.current_book_id));
    const friendsRow = friends.length ? [sect("Friends reading now", "All friends", () => Nav.push(new FriendsScreen())), h("div.hscroll", friends.slice(0, 12).map(x => h("button.col.gap6", { style: { width: "72px", alignItems: "center" }, onclick: () => Nav.push(new UserScreen(x.p)) },
      h("div", { style: { position: "relative" } }, cover(Data.book(x.p.current_book_id), 72), h("div", { style: { position: "absolute", right: "-6px", bottom: "-6px" } }, avatar(pav(x.p), 28))),
      h("span.xs.muted.clamp2.center", { style: { marginTop: "4px" } }, pfirst(x.p)), h("span.xs.faint.num", `p. ${x.p.current_page || 0}`))))] : null;

    // Activity of friends and followed creators
    const feed = Social.feed.map(a => activityItem(a)).filter(Boolean).slice(0, 6);
    const hasPeople = Social.friends.length || Social.followingIds.size;
    const act = [sect("Friends' activity", feed.length ? "See all" : null, () => Nav.push(new ActivityScreen())),
      feed.length ? h("div", { style: { borderTop: "1px solid var(--rule)" } }, feed)
        : !Social.loaded ? h("p.pad.small.faint", "Loading…")
        : hasPeople ? h("p.pad.small.muted", "Nothing new yet. When your friends log pages, finish a book or post, it shows up here.")
        : friendsEmpty()];

    // For you
    // The quiz answers nudge the reading-based picks (TasteQuiz.boost); without answers this is the plain list.
    const recs = TasteQuiz.boost(Recommender.recommend(s, m.level, Catalog.data.books.length), s.tastes, 6);
    const forYou = [sect("For you"), h("div.hscroll", recs.map(r => h("button.col", { style: { width: "96px", textAlign: "left" }, onclick: () => Nav.push(new BookScreen(r.book.id)) },
      cover(r.book, 96), h("div.xs.strong.mt6.clamp2", r.book.title), h("div.xs.faint.clamp2", r.reason))))];

    const q = Data.quoteOfDay(today);
    const quote = q ? h("div.pad.mt32", h("div.serif.italic", `“${q.text}”`), h("div.xs.faint.mt6", `${q.author}${q.source ? ", " + q.source : ""}`)) : null;

    return h("div", h("div.scroll", head, guestNote(true), book ? [mom, reading] : [reading, mom], Fil.banner(), together, friendsRow, act, forYou, quote, h("div.bottom-space")), tabbar("home"));
  }
}

class ActivityScreen extends Screen {
  constructor() { super(); this.live = true; }
  build() {
    const list = Social.feed.map(a => activityItem(a)).filter(Boolean);
    return h("div", navbar("Activity"),
      h("div.scroll.under-nav", { style: { borderTop: "1px solid var(--rule)" } }, list.length ? list : friendsEmpty("Add friends or follow creators to see what they read."),
        list.length ? h("p.center.xs.faint", { style: { padding: "20px" } }, "From your friends and the creators you follow.") : null));
  }
}

class NotificationsScreen extends Screen {
  constructor() { super(); this.live = true; }
  onShow() { const s = Data.state(); if (!this._seen) { this._seen = s.notificationsSeen || ""; s.notificationsSeen = D.stamp(Clock.now()); Data.save(); } }
  build() {
    const now = Clock.now(), seen = this._seen ?? (Data.state().notificationsSeen || "");
    const item = n => {
      let go = null;
      if (n.kind === "reminder") go = () => LogSheet.open();
      else if (n.kind === "weekly") go = () => { const d = new DiaryScreen(); d.mode = "goals"; Nav.reset(d, "diary"); };
      return h("button.feeditem", { onclick: go, style: n.at > seen ? { background: "var(--card)" } : null },
        h("div.avatar.av36", { style: { background: "var(--accent)", fontFamily: "var(--serif)" } }, "m"),
        h("div.grow", h("div", { style: { fontSize: "14.5px" } }, n.text || ""), h("div.xs.faint.mt2", F.ago(n.at, now))));
    };
    const reqs = Social.incoming.map(x => requestRow(x));
    const list = Data.notifications().filter(n => n.text).slice().sort((a, b) => b.at.localeCompare(a.at));
    return h("div", navbar("Notifications", { right: h("button.iconbtn", { "aria-label": "Notification settings", onclick: () => Nav.push(new SettingsDetail("reminders")) }, icon("gear")) }),
      h("div.scroll.under-nav", { style: { borderTop: "1px solid var(--rule)" } },
        reqs.length ? [sect("Friend requests"), reqs] : null,
        list.length ? list.map(item) : (reqs.length ? null : h("div.empty", h("div.h3", "No notifications"), h("p.small", "Your reminders and friend requests show up here.")))));
  }
}

/* ---------------- Explore ---------------- */
class ExploreScreen extends Screen {
  // Starts from the last data it loaded (ExploreScreen.last) and refreshes in the background, so switching
  // tabs doesn't show a half-built page that then jumps as sections arrive.
  constructor() { super(); this.q = ""; this.users = null; this.d = ExploreScreen.last || null; }
  onShow() {
    screenLoad(this, async () => {
      const [counts, readers, creators, posts] = await Promise.all([Social.bookCounts(), Social.readerCount(), Social.creators(), Social.posts({ limit: 8 })]);
      return (ExploreScreen.last = { counts, readers, creators, posts });
    }, () => this.renderResults());
  }
  build() {
    const input = h("input.input", { value: this.q, placeholder: "Books, authors or readers", "aria-label": "Search", style: { paddingLeft: "40px", height: "42px", borderRadius: "10px" } });
    input.addEventListener("input", () => { this.q = input.value; this.renderResults(); this.searchUsers(); });
    this.results = h("div");
    const searchBar = h("div.row.gap8.pad", { style: { paddingTop: "8px", paddingBottom: "8px" } },
      h("div.grow", { style: { position: "relative" } }, h("span", { style: { position: "absolute", left: "12px", top: "11px", color: "var(--ink-3)" } }, icon("search")), input));
    const el = h("div", h("div", h("div.pad.h1", { style: { paddingTop: "14px" } }, "Explore"), searchBar), h("div.scroll.under-nav", this.results), tabbar("explore"));
    this.renderResults();
    return el;
  }
  searchUsers() {
    const q = this.q.trim(); clearTimeout(this._t);
    if (cleanQuery(q).replace(/^@/, "").length < 2) { this.users = null; return; }
    this._t = setTimeout(() => Social.search(q).then(u => { if (this.q.trim() === q) { this.users = u; this.renderResults(); } }).catch(() => {}), 250);
  }
  renderResults() {
    if (!this.results) return;
    if (this.q.trim()) {
      const r = Data.search(this.q), users = this.users || [];
      const none = !r.books.length && !users.length && !r.lists.length;
      setKids(this.results,
        none ? h("div.empty", h("div.h3", `Nothing for “${this.q}”`), h("p.small", "Check the spelling, search by author, or look for a friend by @username.")) : null,
        r.books.length ? [sect("Books"), r.books.map(b => h("button.row.gap12.pad", { style: { width: "100%", textAlign: "left", padding: "8px 16px" }, onclick: () => Nav.push(new BookScreen(b.id)) }, cover(b, 44), h("div.grow", h("div.strong", b.title), h("div.small.muted", `${b.author} · ${b.year < 0 ? -b.year + " BC" : b.year}`))))] : null,
        users.length ? [sect("Readers"), users.map(p => personRow(p))] : null,
        r.lists.length ? [sect("Your lists"), r.lists.map(l => listRow(l))] : null);
      return;
    }
    const d = this.d;
    const trending = d ? Catalog.data.books.filter(b => d.counts[b.id]).sort((a, b) => d.counts[b.id] - d.counts[a.id]).slice(0, 10) : [];
    const shelves = Catalog.data.shelves.map(sh => {
      const books = Catalog.booksInShelf(sh.id);
      return [h("div.sect", h("span.caps", sh.name), h("span.more", F.books(books.length))),
        h("div.hscroll", books.map(b => cover(b, 84, { onclick: () => Nav.push(new BookScreen(b.id)) })))];
    });
    const creators = d ? d.creators.filter(p => p.id !== Net.uid) : [];
    setKids(this.results,
      d ? h("p.pad.small.muted.num", { style: { marginTop: "-2px" } }, `${F.n(d.readers)} ${d.readers === 1 ? "reader" : "readers"} on Margin`) : null,
      h("div.pad.mt8", h("button.card.block.row.gap12", { style: { width: "100%", textAlign: "left" }, onclick: () => Nav.push(new FriendsScreen()) },
        h("span.iconbtn", { style: { background: "var(--accent)", color: "#fff", width: "40px", height: "40px", borderRadius: "20px" } }, icon("users")),
        h("div.grow", h("div.strong", "Find friends"), h("div.small.muted", "Search by @username or share your invite link")), icon("chev", "chev"))),
      h("div.pad.mt8", h("button.card.block.row.gap12", { style: { width: "100%", textAlign: "left" }, onclick: () => Nav.push(new NextBookScreen()) },
        h("span.iconbtn", { style: { background: "var(--ink)", color: "var(--paper)", width: "40px", height: "40px", borderRadius: "20px" } }, icon("bookopen")),
        h("div.grow", h("div.strong", "Find your next book"), h("div.small.muted", "Five questions, matched to what you've read")), icon("chev", "chev"))),
      trending.length ? [sect("Being read on Margin"), h("div.hscroll", trending.map((b, i) => h("button.col", { style: { width: "96px", textAlign: "left" }, onclick: () => Nav.push(new BookScreen(b.id)) },
        cover(b, 96, { rank: i + 1 }), h("div.xs.muted.mt6.num", `${F.n(d.counts[b.id])} reading now`))))] : null,
      // Nothing from the server yet: one quiet line instead of empty sections that would move when data arrives.
      !d ? h("p.pad.small.faint.mt16", this.err || "Loading…") : sect("Creators"),
      !d ? null
        : creators.length || Data.creators.length ? h("div.hscroll", { style: { alignItems: "stretch" } }, creators.map(p => creatorCard(p)), Data.creators.map(c => webCreatorCard(c)))
        : h("p.pad.small.muted", "No creators yet. Anyone can become one in Settings › Profile and publish lists, notes and videos about books."),
      d && d.posts.length ? [sect("Latest from creators"), d.posts.map(p => postRow(p))] : null,
      shelves,
      h("div.bottom-space"));
  }

}

function listRow(l) {
  const owner = Data.me();
  return h("button.pad", { style: { width: "100%", textAlign: "left", padding: "10px 16px 16px", borderBottom: "1px solid var(--rule)" }, onclick: () => Nav.push(new ListScreen(l.id)) },
    h("div.stackcov", l.books.slice(0, 5).map(id => cover(Data.book(id), 72))),
    h("div.h3.mt8", l.title),
    h("div.row.gap6.mt4.xs.muted", avatar(owner, 20), h("span", owner.name), h("span.dotsep"), h("span.num", F.books(l.books.length)), h("span.dotsep"), h("span", "Only you see it")),
    l.description ? h("div.small.muted.mt4.clamp2", l.description) : null);
}
/* ---------------- Diary ---------------- */
class DiaryScreen extends Screen {
  constructor() { super(); this.mode = "library"; this.year = Clock.today().getFullYear(); }
  build() {
    const top = h("div", h("div.row.pad", { style: { paddingTop: "14px" } }, h("div.h1.grow", "Library")),
      h("div.pad.mt12", { style: { paddingBottom: "10px" } }, h("div.seg", [["library", "Books"], ["diary", "Diary"], ["stats", "Stats"], ["goals", "Goals"]].map(([k, l]) => h("button" + (this.mode === k ? ".on" : ""), { onclick: () => { this.mode = k; Nav.refresh(); } }, l)))));
    const body = this.mode === "library" ? this.library() : this.mode === "diary" ? this.diary() : this.mode === "stats" ? this.stats() : this.goals();
    return h("div", top, h("div.scroll.under-nav", body), tabbar("diary"));
  }
  // The reader's own library: reading now, want to read, and every book read, including the ones read before
  // Margin, which they add by hand (they count as finished books, not as logged pages).
  library() {
    const s = Data.state(), cur = Data.book(s.profile.currentBookId);
    const read = Data.finished(s).slice().sort((a, b) => (b.finishedOn || "").localeCompare(a.finishedOn || ""));
    const shelf = list => h("div.grid3.pad.mt8", list.map(({ b, sub }) => h("button.col.gap4", { style: { textAlign: "left" }, onclick: () => Nav.push(new BookScreen(b.id)) },
      cover(b, 110, { style: { width: "100%" } }), h("div.xs.strong.clamp2", b.title), sub ? h("div.xs.faint", sub) : null)));
    return [
      h("div.pad.mt4", h("button.btn.accent", { onclick: () => LibraryAdd.open() }, icon("plus"), "Add books you've read")),
      h("p.pad.xs.faint.mt6", "Books you read before Margin count too. Add them so your library and recommendations know you."),
      cur ? [sect("Reading now"), shelf([{ b: cur, sub: `Page ${S.progressFor(s, cur.id).currentPage} of ${cur.pages}` }])] : null,
      s.want.length ? [sect("Want to read", F.n(s.want.length)), shelf(s.want.map(id => ({ b: Data.book(id) })).filter(x => x.b))] : null,
      [sect("Read", F.n(read.length)), read.length ? shelf(read.map(p => ({ b: Data.book(p.bookId), sub: [p.rating ? "★".repeat(Math.round(p.rating)) : "", p.finishedOn ? p.finishedOn.slice(0, 4) : ""].filter(Boolean).join(" · ") })).filter(x => x.b))
        : h("p.pad.small.muted", "Nothing here yet. Finish a book, or add the ones you've already read.")],
      h("div.bottom-space")];
  }
  diary() {
    const s = Data.state();
    // Group by day and book
    const byKey = new Map();
    for (const l of s.logs) { const k = l.date + "|" + l.bookId; const e = byKey.get(k) || { date: l.date, bookId: l.bookId, from: l.fromPage, to: l.toPage }; e.from = Math.min(e.from, l.fromPage); e.to = Math.max(e.to, l.toPage); byKey.set(k, e); }
    const fins = new Map(Data.finished(s).map(p => [p.bookId + "|" + p.finishedOn, p]));
    const entries = [...byKey.values()].sort((a, b) => b.date.localeCompare(a.date));
    if (!entries.length) return h("div.empty", h("div.h3", "Your diary is empty"), h("p.small", "Every time you log pages it shows up here, grouped by month."), h("button.btn.sm.accent.mt12", { style: { margin: "12px auto 0" }, onclick: () => LogSheet.open() }, "Log reading"));
    const out = []; let month = null, shown = 0;
    for (const e of entries) {
      if (shown > 220) break;
      const d = D.parse(e.date), mk = e.date.slice(0, 7);
      if (mk !== month) {
        month = mk;
        const pages = entries.filter(x => x.date.startsWith(mk)).reduce((t, x) => t + x.to - x.from, 0);
        out.push(h("div.month", h("span.h3", `${Fmt.Months[d.getMonth()]} ${d.getFullYear()}`), h("span.xs.faint.num", F.pages(pages))));
      }
      const b = Data.book(e.bookId), fin = fins.get(e.bookId + "|" + e.date);
      out.push(h("button.dentry", { onclick: () => Nav.push(new BookScreen(b.id)) },
        h("div.day", d.getDate(), h("small", Fmt.Days[d.getDay()].slice(0, 3))), cover(b, 44),
        h("div.grow", h("div.strong.clamp2", b.title),
          fin ? h("div.row.gap6.mt2.small", h("span.badge.ok", "Finished"), fin.rating ? stars(fin.rating, 12) : null)
              : h("div.small.muted.num.mt2", `pp. ${e.from + 1}–${e.to}`, h("span.dotsep"), F.pages(e.to - e.from)))));
      shown++;
    }
    out.push(h("p.center.xs.faint", { style: { padding: "20px" } }, `You've been reading with Margin since ${F.date(s.profile.createdOn)}.`));
    return out;
  }
  stats() {
    const s = Data.state(), y = this.year, years = [];
    for (let k = Clock.today().getFullYear(); k >= D.parse(s.profile.createdOn).getFullYear(); k--) years.push(k);
    const logsY = s.logs.filter(l => l.date.startsWith(String(y)));
    const pages = logsY.reduce((t, l) => t + l.toPage - l.fromPage, 0), days = new Set(logsY.map(l => l.date)).size;
    const fin = Data.finished(s).filter(p => (p.finishedOn || "").startsWith(String(y)));
    const rated = fin.filter(p => p.rating), avg = rated.length ? rated.reduce((t, p) => t + p.rating, 0) / rated.length : 0;
    const quiz = s.quizResults.filter(q => q.date.startsWith(String(y))), qc = quiz.reduce((t, q) => t + q.correct, 0), qt = quiz.reduce((t, q) => t + q.total, 0);
    const months = Array.from({ length: 12 }, (_, i) => logsY.filter(l => +l.date.slice(5, 7) === i + 1).reduce((t, l) => t + l.toPage - l.fromPage, 0));
    const maxM = Math.max(...months, 1), curM = y === Clock.today().getFullYear() ? Clock.today().getMonth() : -1;
    const hist = Array(10).fill(0); Data.finished(s).forEach(p => { if (p.rating) hist[Math.round(p.rating * 2) - 1]++; });
    const shelfCount = {}; fin.forEach(p => Data.book(p.bookId).shelves.forEach(sh => shelfCount[sh] = (shelfCount[sh] || 0) + 1));
    return [
      h("div.row.gap6.pad.mt4", years.map(k => h("button.pillbtn" + (k === y ? ".on" : ""), { onclick: () => { this.year = k; Nav.refresh(); } }, k))),
      h("div.statgrid.mt12",
        h("div", h("div.v", fin.length), h("div.xs.muted", `books finished · goal ${s.yearGoal}`)),
        h("div", h("div.v", F.n(pages)), h("div.xs.muted", "pages")),
        h("div", h("div.v", days), h("div.xs.muted", "reading days")),
        h("div", h("div.v", `${Math.round(pages * 1.5 / 60)} h`), h("div.xs.muted", "approx., at 1.5 min per page")),
        h("div", h("div.v", avg ? avg.toFixed(1) : "–"), h("div.xs.muted", "average rating")),
        h("div", h("div.v", qt ? F.pct(qc / qt) : "–"), h("div.xs.muted", `correct across ${quiz.length} quizzes`))),
      sect("Pages per month"),
      h("div.pad", h("div.bars", months.map((v, i) => { const bb = h("i" + (i === curM ? ".now" : "")); bb.style.height = `${(v / maxM) * 100}%`; return h("div.b", { title: `${Fmt.Months[i]}: ${F.pages(v)}` }, h("span.num", v ? F.k(v) : ""), bb, h("span", MES[i][0].toUpperCase())); }))),
      sect("Your ratings"),
      h("div.row.gap8.pad", h("span.small", stars(0.5, 10)), histogram(hist), h("span.small", stars(5, 10))),
      h("p.pad.xs.faint.mt6", `All your rated books: ${Data.finished(s).filter(p => p.rating).length}.`),
      sect("By shelf"),
      h("div.pad", Object.entries(shelfCount).sort((a, b) => b[1] - a[1]).map(([id, n]) => h("div.row.gap10.mt8.breakdown", h("span.small", { style: { width: "110px" } }, Catalog.findShelf(id)?.name || id), h("div.bar", h("i", { style: { width: `${(n / Math.max(...Object.values(shelfCount))) * 100}%` } })), h("span.xs.faint.num", { style: { width: "20px", textAlign: "right" } }, n))),
        !fin.length ? h("p.small.faint", "You haven't finished a book this year yet.") : null),
      h("div.bottom-space")];
  }
  goals() {
    const s = Data.state(), today = Clock.today(), y = today.getFullYear();
    const done = Data.booksInYear(s, y), st = Data.weeksMeetingGoal(s, today);
    const dayOfYear = Math.floor((today - new Date(y, 0, 1)) / 864e5) + 1, expected = s.yearGoal * dayOfYear / 365;
    return [
      h("div.card.block", { style: { margin: "4px 16px 0" } }, h("div.caps", `${y} goal`), h("div.row.mt6", { style: { alignItems: "baseline", gap: "6px" } }, h("span.level", { style: { fontFamily: "var(--serif)", fontSize: "34px", fontWeight: 600 } }, done), h("span.muted", `of ${F.books(s.yearGoal)}`)),
        h("div.mt8", bar(done / s.yearGoal, "ink")), h("p.small.muted.mt8", done >= s.yearGoal ? `You already met your ${y} goal.` : `${s.yearGoal - done === 1 ? "1 book" : `${s.yearGoal - done} books`} to go, with ${Math.max(1, Math.round((new Date(y, 11, 31) - today) / 6048e5))} weeks left in the year.`),
        h("button.btn.sm.secondary.mt12", { onclick: () => Nav.push(new SettingsDetail("reading")) }, "Change goal")),
      h("div.statgrid.mt12",
        h("div", h("div.v", st.current), h("div.xs.muted", st.current === 1 ? "week meeting your goal (last week)" : "weeks in a row meeting your goal")),
        h("div", h("div.v", st.best), h("div.xs.muted", st.best === 1 ? "week is your best" : "weeks is your best")),
        h("div", h("div.v", F.n(Data.readingDaysCount(s))), h("div.xs.muted", "reading days in total")),
        h("div", h("div.v", (s.focusSessions || []).length), h("div.xs.muted", "Focus mode sessions"))),
      h("p.pad.small.faint.mt12", "Missed weeks don't erase anything. Your best stays, and momentum drops little by little."),
      h("div.bottom-space")];
  }
}

/* ---------------- Profile (yours; other readers open UserScreen) ---------------- */
const ACH_MARK = { "first-book": "1", "ten-chapters": "10", month: "4", "night-owl": "23", "focus-10": "10", "reflections-5": "5", theater: "III", mexico: "MX", paper: "5", "year-12": "12" };
class ProfileScreen extends Screen {
  constructor() { super(); this.live = true; }
  jump(title) { const el = [...this.el.querySelectorAll(".sect .caps")].find(x => x.textContent.toLowerCase() === title.toLowerCase()); if (el) el.parentNode.scrollIntoView({ behavior: "smooth", block: "start" }); }
  build() {
    const u = Data.me(), s = Data.state(), acc = Accounts.current();
    const read = Data.finished(s), lists = s.lists || [];
    const yearCount = Data.booksInYear(s, Clock.today().getFullYear());
    const counters = h("div.counters",
      h("button", { onclick: () => Nav.reset(new DiaryScreen(), "diary") }, h("div.v", read.length), h("div.k", "Books")),
      h("button", { onclick: () => { const d = new DiaryScreen(); d.mode = "stats"; Nav.reset(d, "diary"); } }, h("div.v", yearCount), h("div.k", "This year")),
      h("button", { onclick: () => Nav.push(new FriendsScreen()) }, h("div.v", Social.friends.length), h("div.k", "Friends")),
      acc.isCreator ? h("button", { onclick: () => this.jump("Your posts") }, h("div.v", F.k(Social.followers)), h("div.k", "Followers"))
        : h("button", { onclick: () => lists.length ? this.jump("Lists") : toast("You don't have lists yet. Make one from a book's page.") }, h("div.v", lists.length), h("div.k", "Lists")));
    const right = h("button.iconbtn", { "aria-label": "Settings", onclick: () => Nav.push(new SettingsScreen()) }, icon("gear"));
    const joined = D.parse(u.joined || D.key(Clock.today()));
    const header = h("div.col.pad", { style: { alignItems: "center", textAlign: "center" } }, avatar(u, 88), h("div.h2.mt12", u.name),
      h("div.small.muted.mt2", `@${u.id}`, h("span.dotsep"), `on Margin since ${MES[joined.getMonth()]} ${joined.getFullYear()}`),
      acc.isCreator ? h("div.mt6", h("span.badge.ok", "Creator")) : null,
      u.bio ? h("p.small.mt8", { style: { maxWidth: "300px" } }, u.bio) : null,
      h("div.row.gap8.mt12", h("button.btn.sm.secondary", { onclick: () => Nav.push(new SettingsDetail("profile")) }, "Edit profile"),
        h("button.btn.sm.secondary", { onclick: () => InviteSheet.open() }, icon("share"), "Invite")),
      acc.isGuest ? h("button.btn.sm.accent.mt8", { onclick: () => Nav.push(new SaveAccountScreen()) }, "Save your account") : null);
    const favs = (s.favorites || []).map(id => Data.book(id)).filter(Boolean);
    const cur = Data.book(s.profile.currentBookId);
    const hist = Array(10).fill(0); read.forEach(p => { if (p.rating) hist[Math.round(p.rating * 2) - 1]++; });
    const posts = acc.isCreator ? [sect("Your posts", "New post", () => Nav.push(new PostEditor())),
      Social.myPosts.length ? Social.myPosts.map(p => postRow({ ...p, profiles: { username: acc.username, name: acc.name, color: acc.color } }))
        : h("p.pad.small.muted", "Publish a list of books, a note or a video. Your followers see it in their feed.")] : null;
    return h("div", navbar("", { right }),
      h("div.scroll.under-nav",
        header, counters,
        posts,
        favs.length ? [sect("Favorite books"), h("div.grid4.pad", favs.map(b => cover(b, 84, { onclick: () => Nav.push(new BookScreen(b.id)), style: { width: "100%" } })))] : null,
        cur ? [sect("Reading now"), h("button.row.gap12.pad", { style: { width: "100%", textAlign: "left" }, onclick: () => Nav.push(new BookScreen(cur.id)) }, cover(cur, 44), h("div.grow", h("div.strong", cur.title), h("div.mt6", bar(S.progressFor(s, cur.id).currentPage / cur.pages)), h("div.xs.faint.mt4.num", `Page ${S.progressFor(s, cur.id).currentPage} of ${cur.pages}`)))] : null,
        [sect("Achievements", `${Data.achievements().filter(a => a.got).length} of ${Data.achievements().length}`), h("div.ach", Data.achievements().map(a => h("button.a" + (a.got ? ".got" : ""), { onclick: () => iosAlert(a.name, a.detail + (a.got ? "" : " You don't have it yet."), [{ label: "Close", bold: true }]) },
          h("div.medal", ACH_MARK[a.id] || a.name[0]), h("span.clamp2", a.name))))],
        sect("Finished", "Library", () => Nav.reset(new DiaryScreen(), "diary")),
        read.length ? h("div.hscroll", read.slice(0, 12).map(p => { const b = Data.book(p.bookId); return h("button.col", { style: { width: "72px" }, onclick: () => Nav.push(new BookScreen(b.id)) }, cover(b, 72), p.rating ? h("div.mt4", stars(p.rating, 11)) : null); }))
          : h("p.pad.small.faint", "Books you finish show up here."),
        sect("Ratings", `${read.filter(p => p.rating).length}`), h("div.row.gap8.pad", stars(0.5, 10), histogram(hist), stars(5, 10)),
        lists.length ? [sect("Lists"), lists.map(listRow)] : null,
        [sect("Friends", Social.friends.length ? F.n(Social.friends.length) : null), Social.friends.length
          ? h("div.facepile.pad", Social.friends.slice(0, 12).map(x => h("button", { onclick: () => Nav.push(new UserScreen(x.p)), "aria-label": pname(x.p) }, avatar(pav(x.p), 36))))
          : h("div.pad", h("button.btn.sm.secondary", { onclick: () => Nav.push(new FriendsScreen()) }, icon("users"), "Find friends"))],
        h("div.bottom-space")),
      Nav.stack.length === 1 ? tabbar("profile") : null);
  }
}

/* A real book creator's review from the web (Data/creators.json): who, a short quote, the book, the link. */
function webCreatorCard(c, opts = {}) {
  const b = Data.book(c.bookId);
  return h("button.card.block", { style: { width: opts.full ? "calc(100% - 32px)" : "272px", margin: opts.full ? "8px 16px 0" : null, flex: "none", textAlign: "left", display: "flex", flexDirection: "column", gap: "10px" },
    onclick: () => window.open(c.reviewUrl, "_blank", "noopener") },
    h("div.row.gap10", avatar({ name: c.name, color: c.color || "#3E5C76" }, 36),
      h("div.grow", { style: { minWidth: 0 } }, h("div.strong.small", c.name), h("div.xs.muted", `${c.handle} · ${c.platform}`))),
    h("div", { style: { fontFamily: "var(--serif)", fontSize: "17px", lineHeight: "1.35", color: "var(--ink)" } }, `“${c.quote}”`),
    c.quoteEn ? h("div.xs.muted", `In English: “${c.quoteEn}”`) : null,
    h("span.grow"),
    opts.full || !b ? null : h("div.row.gap8", cover(b, 44), h("div.xs.muted.clamp2", `On ${b.title}`)),
    h("div.xs", { style: { color: "var(--accent)", fontWeight: 600 } }, c.platform === "YouTube" ? "Watch the review ›" : c.platform === "TikTok" || c.platform === "Instagram" ? `See it on ${c.platform} ›` : "Read the review ›"));
}

/* "Add books you've read": search the catalog, then say when (roughly) and how it was. */
const LibraryAdd = {
  open() {
    const s = Data.state(), done = new Set([...Data.finished(s).map(p => p.bookId), s.profile.currentBookId]);
    const list = h("div.list", { style: { margin: 0 } }, Catalog.data.books.filter(b => !done.has(b.id)).map(b =>
      h("button.item", { "data-q": searchKey(b), onclick: () => this.details(b) }, cover(b, 44), h("div.grow", h("div.strong", b.title), h("div.small.muted", b.author)))));
    Sheet.open("Add a book you've read", h("div", bookSearch("Search by title or author"), list));
  },
  details(b) {
    const s = Data.state(), y = Clock.today().getFullYear();
    let when = "earlier", rating = 0;
    const whens = [["this", `In ${y}`], ["last", `In ${y - 1}`], ["earlier", "Before that"]];
    const seg = h("div.seg.mt8", whens.map(([k, l]) => h("button" + (k === when ? ".on" : ""), { onclick: e => { when = k; seg.querySelectorAll("button").forEach(x => x.classList.toggle("on", x === e.currentTarget)); } }, l)));
    const mk = v => ratingInput(v, nv => { rating = nv; stars.replaceChildren(mk(nv)); });
    const stars = h("div.mt8", mk(0));
    const add = () => {
      const pp = S.progressFor(s, b.id);
      Object.assign(pp, { currentPage: b.pages, finished: true, finishedOn: when === "this" ? D.key(Clock.today()) : when === "last" ? `${y - 1}-07-01` : `${y - 3}-07-01` });
      if (rating) pp.rating = rating;
      s.want = s.want.filter(x => x !== b.id); s.dnf = s.dnf.filter(x => x !== b.id);
      Data.save(); Sheet.close(); toast(`${b.title} is in your library.`); Nav.refresh();
    };
    Sheet.open(b.title, h("div",
      h("div.row.gap12", cover(b, 56), h("div.grow", h("div.strong", b.title), h("div.small.muted", b.author))),
      h("div.small.muted.mt20", "When did you read it?"), seg,
      h("div.small.muted.mt20", "How was it? (optional)"), stars,
      h("button.btn.accent.mt24", { onclick: add }, "Add to my library")));
  },
};
