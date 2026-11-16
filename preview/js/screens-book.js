/* Book page, reviews (Margin readers from the database + real Goodreads quotes) and your lists. */
"use strict";

const REVIEWER_COLORS = ["#3E5C76", "#8A5A44", "#2F6B47", "#6B4E71", "#7A3B3B", "#4A6B5D", "#B5893B", "#5B554C"];
// Language the reviewer wrote in. Reviews translated for the English app keep it in originalLang.
function reviewLang(r) { return r.originalLang || r.lang || "en"; }
const LANG_NAMES = { es: "Spanish", en: "English" };
function reviewer(r) { return { name: r.author, color: REVIEWER_COLORS[hash(r.author) % REVIEWER_COLORS.length] }; }

function reviewCard(r, opts = {}) {
  const own = !!r.own;
  const body = h("div.body" + (opts.full ? "" : ".clamp6"), r.text);
  const el = h("div.review", { onclick: opts.full ? null : () => Nav.push(new ReviewScreen(r)) },
    h("div.meta", own ? avatar(Data.me(), 28) : avatar(reviewer(r), 28), h("span.strong", { style: { color: "var(--ink)" } }, own ? "Your review" : r.author),
      r.rating ? stars(r.ratingScale === 5 || !r.ratingScale ? r.rating : r.rating / r.ratingScale * 5, 11) : null,
      h("span.grow"), reviewLang(r) !== "en" ? h("span.lang", reviewLang(r).toUpperCase()) : null),
    body,
    h("div.foot", h("span.human", "Written by a human"),
      h("span.grow", { style: { whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" } }, own ? F.date(r.date) : `${r.source}, ${MES[D.parse(r.date).getMonth()]} ${D.parse(r.date).getFullYear()}`),
      null));
  return el;
}

class BookScreen extends Screen {
  constructor(id) { super(); this.id = id; this.revMode = "recent"; }
  onShow() {
    screenLoad(this, async () => {
      const [reviews, readers, lists] = await Promise.all([Social.bookReviews(this.id), Social.bookCounts(), Social.posts({ bookId: this.id, limit: 5 })]);
      return { reviews, readingNow: readers[this.id] || 0, lists };
    });
  }
  reload() { this._loaded = false; this.onShow(); }
  // Ratings from Margin reviews (real readers in the database).
  stats() {
    const rv = this.d ? this.d.reviews : [], hist = Array(10).fill(0);
    rv.forEach(r => { hist[Math.max(0, Math.min(9, Math.round(r.rating * 2) - 1))]++; });
    return { ratings: rv.length, average: rv.length ? rv.reduce((t, r) => t + Number(r.rating), 0) / rv.length : 0, histogram: hist };
  }
  build() {
    const b = Data.book(this.id), s = Data.state(), st = this.stats();
    const p = s.progress.find(x => x.bookId === b.id), isCur = s.profile.currentBookId === b.id;
    const want = s.want.includes(b.id), dnf = s.dnf.includes(b.id);
    const status = p?.finished ? "Read" : isCur ? `Reading · p. ${p ? p.currentPage : 0}` : dnf ? "You dropped it" : want ? "On your want-to-read list" : "Want to read";
    const myRating = p?.rating || 0;

    const hero = h("div.hero", h("div.bg", { style: Data.coverUrl(b.id) ? { backgroundImage: `url(${Data.coverUrl(b.id)})` } : { backgroundColor: "var(--ink-2)" } }), h("div.shade"));
    const head = h("div.bookhead", cover(b, 110), h("div.grow", { style: { paddingBottom: "4px" } },
      h("div.h1", { style: { fontSize: b.title.length > 28 ? "22px" : "27px" } }, b.title), h("div.mt4", b.author), h("div.small.muted.mt2.num", `${b.year < 0 ? -b.year + " BC" : b.year}`, h("span.dotsep"), `${b.pages} pp.`, h("span.dotsep"), `${b.chapters.length} ${b.chapters.length === 1 ? "part" : "parts"}`)));

    const actions = h("div.pad.mt16",
      h("div.split" + (p?.finished ? ".done" : ""), h("button", { onclick: () => this.primary() }, p?.finished ? [icon("check"), " Read"] : status), h("button", { "aria-label": "Change status", onclick: () => this.statusSheet() }, icon("down"))),
      h("div.row.mt12", { style: { justifyContent: "space-between" } },
        h("div", h("div.xs.faint", myRating ? "Your rating" : "Rate it"), this.rateInput(myRating)),
        h("div.row.gap6", isCur ? h("button.btn.sm.accent", { onclick: () => LogSheet.open() }, "Log reading") : null,
          h("button.iconbtn", { "aria-label": "Add to a list", onclick: () => this.addToList() }, icon("list")),
          h("button.iconbtn", { "aria-label": "Share", onclick: () => shareText(`I'm reading ${b.title} by ${b.author} on Margin. https://marginreads.vercel.app`) }, icon("share")))));

    // Real friends (accepted friendships) who finished it or are reading it now.
    const friendsRead = Social.friends.filter(x => (x.p.books_read || []).includes(b.id)).map(x => x.p);
    const friendsNow = Social.friends.filter(x => x.p.current_book_id === b.id).map(x => x.p);
    const ratings = !this.d ? h("p.pad.small.faint", this.err || "Loading ratings…")
      : st.ratings ? h("button.pad", { style: { width: "100%", textAlign: "left" }, onclick: () => this.breakdown() },
        h("div.row.gap12", h("div", h("div", { style: { fontFamily: "var(--serif)", fontSize: "34px", fontWeight: 600, lineHeight: 1 } }, st.average.toFixed(1)), h("div.mt4", stars(st.average, 12))),
          histogram(st.histogram, myRating)),
        h("div.row.xs.faint.mt6.num", h("span.grow", `${F.n(st.ratings)} ${st.ratings === 1 ? "rating" : "ratings"} from Margin readers`), h("span", "See breakdown")))
      : h("p.pad.small.muted", "No Margin ratings yet. Rate it and write a review to be the first.");
    const names = ps => ps.length === 1 ? pfirst(ps[0]) : `${pfirst(ps[0])} and ${ps.length - 1} ${ps.length === 2 ? "other friend" : "other friends"}`;
    const lines = [
      this.d && this.d.readingNow ? [h("b.num", F.n(this.d.readingNow)), ` ${this.d.readingNow === 1 ? "reader has" : "readers have"} it as their current book`] : null,
      friendsNow.length ? h("span.muted", `${names(friendsNow)} ${friendsNow.length === 1 ? "is" : "are"} reading it now`) : null,
      friendsRead.length ? h("span.muted", `${names(friendsRead)} ${friendsRead.length === 1 ? "has" : "have"} read it`) : null,
    ].filter(Boolean);
    const faces = [...friendsNow, ...friendsRead].slice(0, 3);
    const social = lines.length ? h("div.pad.mt16.row.gap10", faces.length ? h("div.facepile", faces.map(p => h("button", { onclick: () => Nav.push(new UserScreen(p)) }, avatar(pav(p), 28)))) : null,
      h("div.small", lines.map((l, i) => [i ? h("br") : null, l]))) : null;

    const reviews = Data.reviewsFor(b.id).slice().sort((a, c) => (c.date || "").localeCompare(a.date || ""));
    const marginRv = this.d ? this.d.reviews : [], mineRv = marginRv.find(r => r.user_id === Net.uid);
    const others = marginRv.filter(r => r.user_id !== Net.uid);
    const revSect = [h("div.sect", h("span.caps", "Margin reviews"), h("span.more.num", this.d ? F.n(marginRv.length) : "")),
      mineRv ? marginReviewCard(mineRv, { own: true, onEdit: () => ReviewSheet.open(b.id, mineRv, () => this.reload()) })
        : h("div.pad.mt8", h("button.btn.sm.secondary", { style: { width: "100%" }, onclick: () => ReviewSheet.open(b.id, null, () => this.reload()) }, "Write your review")),
      others.slice(0, 5).map(r => marginReviewCard(r)),
      this.d && !others.length ? h("p.pad.small.faint.mt8", "No other Margin reader has reviewed it yet.") : null,
      reviews.length ? [h("div.sect", h("span.caps", "From Goodreads"), h("span.more.num", F.n(reviews.length))),
        reviews.slice(0, 3).map(r => reviewCard(r)),
        reviews.length > 3 ? h("div.pad.mt8", h("button.btn.sm.secondary", { style: { width: "100%" }, onclick: () => Nav.push(new ReviewsScreen(b.id)) }, `See all ${reviews.length} Goodreads reviews`)) : null,
        h("p.pad.xs.faint.mt8", "Reader reviews posted on Goodreads, quoted with their author. Margin doesn't summarize them or rewrite them with AI.")] : null];

    const chapters = [sect("Chapters and quizzes"), h("div.pad", b.chapters.map(c => {
      const q = s.quizResults.find(x => x.bookId === b.id && x.chapterNumber === c.number);
      const read = p && p.currentPage >= c.endPage;
      return h("button.chapter", { onclick: () => read && Book.hasQuiz(c) ? QuizFlow.start(b, c, { standalone: true }) : toast(read ? "This chapter has no quiz." : `The quiz shows up when you reach page ${c.endPage}.`) },
        h("span.n", c.number), h("span.grow" + (read ? "" : ".muted"), c.title), h("span.xs.faint.num", `${c.startPage}–${c.endPage}`),
        q ? h("span.badge.ok.num", `${q.correct}/${q.total}`) : read && Book.hasQuiz(c) ? h("span.badge.todo", "Quiz") : null);
    }))];

    const inLists = (s.lists || []).filter(l => l.books.includes(b.id)), creatorLists = this.d ? this.d.lists : [];
    const lists = inLists.length || creatorLists.length ? [sect("In lists"), creatorLists.map(p => postRow(p)), inLists.slice(0, 3).map(listRow)] : null;

    const facts = [sect("Details"), h("div.facts.pad",
      h("div", "Author"), h("div", b.author), h("div", "Published"), h("div", b.year < 0 ? `${-b.year} BC` : String(b.year)),
      h("div", "Length"), h("div", `${b.pages} pages, ${b.chapters.length} ${b.chapters.length === 1 ? "part" : "parts"}`),
      h("div", "Shelves"), h("div", b.shelves.map(id => Catalog.findShelf(id)?.name).filter(Boolean).join(", ")),
      h("div", "Themes"), h("div", (b.tags || []).join(", ")))];

    const paper = [sect("Get it on paper"), h("div.list",
      h("button.item", { onclick: () => window.open("https://www.google.com/maps/search/bookstores+near+me", "_blank") }, h("span.ic", { style: { background: "#8A5A44" } }, icon("pin")), h("div.grow", h("div", "Bookstores near you"), h("div.xs.faint", "Opens Maps")), icon("chev", "chev")),
      h("button.item", { onclick: () => window.open("https://www.google.com/maps/search/public+library", "_blank") }, h("span.ic", { style: { background: "#3E5C76" } }, icon("bookopen")), h("div.grow", h("div", "Public libraries"), h("div.xs.faint", "Borrow for free")), icon("chev", "chev")),
      friendsRead.length ? h("button.item", { onclick: () => Nav.push(new UserScreen(friendsRead[0])) }, h("span.ic", { style: { background: "#2F6B47" } }, icon("users")), h("div.grow", h("div", "Borrow it from a friend"), h("div.xs.faint", `${pname(friendsRead[0])} has read it`)), icon("chev", "chev")) : null)];

    const scroller = h("div.scroll.under-nav", { style: { paddingTop: 0 } }, hero, head, actions, sect("Synopsis"), h("p.pad.serif", b.synopsis),
      sect("Ratings"), ratings, social, revSect, chapters, lists, facts, Shop.section(b), paper, h("div.bottom-space"));
    const nav = h("div.navbar.transparent", h("button.iconbtn.round", { "aria-label": "Back", onclick: () => Nav.stack.length > 1 ? Nav.back() : Nav.reset(new ExploreScreen(), "explore") }, icon("back")), h("div.title", b.title),
      h("button.iconbtn.round", { "aria-label": "More options", onclick: () => this.statusSheet() }, icon("more")));
    scroller.addEventListener("scroll", () => { const solid = scroller.scrollTop > 190; nav.classList.toggle("solid", solid); App.status.classList.toggle("on-dark", !solid); });
    return h("div", scroller, nav);
  }
  rateInput(val) {
    const wrap = h("div.row", { style: { gap: "2px" } });
    for (let i = 1; i <= 5; i++) {
      const b = h("button", { style: { width: "30px", height: "30px", position: "relative" }, "aria-label": `${i} stars` }, stars(Math.max(0, Math.min(1, val - (i - 1))) * 5, 26));
      b.firstChild.style.width = "26px"; b.querySelectorAll(".base svg").forEach((x, k) => { if (k) x.style.display = "none"; }); b.querySelectorAll(".fill svg").forEach((x, k) => { if (k) x.style.display = "none"; });
      b.querySelector(".fill").style.width = `${Math.max(0, Math.min(1, val - (i - 1))) * 100}%`;
      b.addEventListener("click", e => { const half = e.offsetX < 13; this.rate(val === i - (half ? .5 : 0) ? 0 : i - (half ? .5 : 0)); });
      wrap.append(b);
    }
    return wrap;
  }
  rate(v) { const s = Data.state(), p = S.progressFor(s, this.id); p.rating = v; Data.save(); Nav.refresh(); if (v) toast(`You rated it ${v} ${v === 1 ? "star" : "stars"}`); }
  primary() { const s = Data.state(), p = s.progress.find(x => x.bookId === this.id); if (s.profile.currentBookId === this.id && !p?.finished) LogSheet.open(); else this.statusSheet(); }
  statusSheet() {
    const s = Data.state(), id = this.id, b = Data.book(id);
    const opt = (label, sub, fn, on) => h("button.item", { onclick: () => { Sheet.close(); fn(); Data.save(); Nav.refresh(); } }, h("div.grow", h("div", label), sub ? h("div.xs.faint", sub) : null), on ? icon("check") : null);
    const p = s.progress.find(x => x.bookId === id);
    const clear = () => { s.want = s.want.filter(x => x !== id); s.dnf = s.dnf.filter(x => x !== id); };
    Sheet.open(b.title, h("div.list", { style: { margin: 0 } },
      opt("Want to read", "We save it to your want-to-read list", () => { clear(); s.want.push(id); toast("Added to Want to read"); }, s.want.includes(id)),
      opt("Reading now", s.profile.currentBookId && s.profile.currentBookId !== id ? `Replaces ${Data.book(s.profile.currentBookId).title} as your current book` : null, () => { clear(); ReadingService.setCurrentBook(s, id); toast(`Now reading ${b.title}.`); }, s.profile.currentBookId === id),
      opt("Read", "Mark it as finished", () => { clear(); const pp = S.progressFor(s, id); pp.currentPage = b.pages; pp.finished = true; pp.finishedOn = D.key(Clock.today()); if (s.profile.currentBookId === id) s.profile.currentBookId = null; toast("Marked as read"); }, p?.finished),
      opt("Dropped it", "Doesn't count in your stats", () => { clear(); s.dnf.push(id); if (s.profile.currentBookId === id) s.profile.currentBookId = null; }, s.dnf.includes(id))));
  }
  addToList() {
    const s = Data.state(), id = this.id;
    Sheet.open("Add to a list", h("div",
      h("div.list", { style: { margin: 0 } }, s.lists.map(l => h("button.item", { onclick: () => { if (!l.books.includes(id)) l.books.push(id); Data.save(); Sheet.close(); toast(`Added to “${l.title}”`); } },
        h("div.grow", h("div", l.title), h("div.xs.faint", F.books(l.books.length))), l.books.includes(id) ? icon("check") : null))),
      h("button.btn.secondary.mt12", { onclick: () => { Sheet.close(); ListEditor.open(id); } }, "New list")));
  }
  breakdown() {
    const st = this.stats(), tot = Math.max(1, st.histogram.reduce((a, c) => a + c, 0));
    const rows = []; for (let i = 9; i >= 0; i--) rows.push(h("div.row.gap10.mt6.breakdown", h("span.small.num", { style: { width: "26px" } }, ((i + 1) / 2).toFixed(1)), h("div.bar", h("i", { style: { width: `${(st.histogram[i] / tot) * 100}%` } })), h("span.xs.faint.num", { style: { width: "64px", textAlign: "right" } }, `${F.n(st.histogram[i])} · ${Math.round(st.histogram[i] / tot * 100)}%`)));
    Sheet.open("Breakdown", h("div", h("div.row", { style: { alignItems: "baseline", gap: "8px" } }, h("span", { style: { fontFamily: "var(--serif)", fontSize: "40px", fontWeight: 600 } }, st.average.toFixed(2)), h("span.muted", `out of 5 · ${F.n(st.ratings)} ${st.ratings === 1 ? "rating" : "ratings"}`)), rows,
      h("p.xs.faint.mt16", "Ratings from Margin readers' reviews. Simple average, not weighted. Each reader counts once.")));
  }
}

class ReviewsScreen extends Screen {
  constructor(id) { super(); this.id = id; this.lang = "all"; }
  build() {
    const all = Data.reviewsFor(this.id).slice().sort((a, b) => (b.date || "").localeCompare(a.date || ""));
    const list = all.filter(r => this.lang === "all" || reviewLang(r) === this.lang);
    return h("div", navbar(Data.book(this.id).title), h("div.row.gap6.pad", { style: { paddingBottom: "8px" } }, [["all", "All"], ["en", "English"], ["es", "Spanish"]].map(([k, l]) => h("button.pillbtn" + (this.lang === k ? ".on" : ""), { onclick: () => { this.lang = k; Nav.refresh(); } }, l))),
      h("div.scroll.under-nav", { style: { borderTop: "1px solid var(--rule)" } }, list.map(r => reviewCard(r)), h("p.pad.xs.faint", { style: { padding: "16px" } }, "Source: public Goodreads pages. Tap a review to see the original link.")));
  }
}

class ReviewScreen extends Screen {
  constructor(r) { super(); this.r = r; }
  build() {
    const r = this.r, b = Data.book(r.bookId);
    return h("div", navbar("Review"), h("div.scroll.under-nav",
      h("button.row.gap12.pad", { style: { width: "100%", textAlign: "left", paddingBottom: "12px", borderBottom: "1px solid var(--rule)" }, onclick: () => Nav.push(new BookScreen(b.id)) }, cover(b, 56), h("div.grow", h("div.h3", b.title), h("div.small.muted", b.author))),
      reviewCard(r, { full: true }),
      r.own ? null : h("div.pad.mt8", h("button.btn.secondary", { onclick: () => window.open(r.pageUrl || r.url, "_blank") }, icon("link"), "See original on Goodreads"),
        reviewLang(r) !== "en" ? h("p.xs.faint.mt8", `Translated from ${LANG_NAMES[reviewLang(r)] || reviewLang(r).toUpperCase()}. The link above has the reader's words exactly as they wrote them.`) : null)));
  }
}

/* A review by a Margin reader (stored in the reviews table). */
function marginReviewCard(r, opts = {}) {
  const p = r.profiles || {}, who = { username: p.username, name: p.name, color: p.color, id: r.user_id };
  return h("div.review",
    h("div.meta", h("button", { onclick: () => opts.own ? Nav.push(new ProfileScreen()) : Nav.push(new UserScreen(p.username)), "aria-label": pname(who) }, avatar(pav(who), 28)),
      h("span.strong", { style: { color: "var(--ink)" } }, opts.own ? "Your review" : pname(who)), stars(Number(r.rating), 11), h("span.grow"),
      opts.own ? h("button.small.link", { onclick: opts.onEdit }, "Edit") : null),
    r.body ? h("div.body", r.body) : null,
    h("div.foot", h("span.human", "Written by a human"), h("span.grow", { style: { whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" } }, `Margin, ${F.date(new Date(r.created_at))}`)));
}

const ReviewSheet = {
  open(bookId, existing, onDone) {
    const s = Data.state(), book = Data.book(bookId);
    let rating = existing ? Number(existing.rating) : (S.progressFor(s, bookId).rating || 0);
    const ta = h("textarea.input", { placeholder: "What did it leave you with? No one else will write this for you.", maxlength: 4000 });
    ta.value = existing ? existing.body || "" : "";
    const counter = h("div.help"), err = h("div.help.bad");
    const starsBox = h("div");
    const drawStars = () => starsBox.replaceChildren(BookScreen.prototype.rateInput.call({ id: bookId, rate: v => { rating = v; drawStars(); upd(); } }, rating));
    const upd = () => { const w = ta.value.trim().split(/\s+/).filter(Boolean).length; counter.textContent = `${w} ${w === 1 ? "word" : "words"}`; save.disabled = w < 3 || !rating; err.textContent = rating ? "" : "Tap the stars to rate it."; };
    ta.addEventListener("input", upd);
    const save = h("button.textbtn", { style: { color: "var(--link)", fontWeight: 600, height: "44px", padding: "0 10px" }, onclick: async () => {
      save.disabled = true;
      try { await Social.saveReview(bookId, rating, ta.value.trim()); } catch (e) { save.disabled = false; return netFail(e); }
      S.progressFor(s, bookId).rating = rating; Data.save();
      Sheet.close(); toast(existing ? "Review updated" : "Review posted"); if (onDone) onDone(); else Nav.refresh(); } }, existing ? "Save" : "Post");
    Sheet.open("Your review", h("div", h("div.row.gap12", cover(book, 44), h("div.strong.grow", book.title)),
      h("div.mt12.small.muted", "Your rating"), h("div.mt4", starsBox), err,
      h("div.mt12", ta), counter,
      existing ? h("button.small.link.mt8", { style: { color: "var(--error)" }, onclick: async () => { try { await Social.deleteReview(bookId); } catch (e) { return netFail(e); } Sheet.close(); toast("Review deleted"); if (onDone) onDone(); } }, "Delete my review") : null,
      h("p.xs.faint.mt12", "No autocomplete and no AI suggestions. It's posted with your name and today's date, and other Margin readers can see it.")), { right: save });
    drawStars(); upd();
    setTimeout(() => ta.focus(), 300);
  },
};

const ListEditor = {
  open(firstBook) {
    const s = Data.state(); const t = h("input.input", { placeholder: "List name", maxlength: 60 }); const d = h("input.input.mt8", { placeholder: "Description (optional)", maxlength: 140 });
    const create = h("button.textbtn", { style: { color: "var(--link)", fontWeight: 600, height: "44px", padding: "0 10px" }, onclick: () => {
      if (!t.value.trim()) { t.classList.add("bad"); return; }
      const l = { id: "u" + Date.now(), owner: Accounts.session(), title: t.value.trim(), description: d.value.trim(), books: firstBook ? [firstBook] : [], likes: 0, comments: 0, created: D.key(Clock.today()) };
      s.lists.push(l); Data.save(); Sheet.close(); Nav.push(new ListScreen(l.id)); } }, "Create");
    Sheet.open("New list", h("div", t, d), { right: create }); setTimeout(() => t.focus(), 300);
  },
};

class ListScreen extends Screen {
  constructor(id) { super(); this.id = id; }
  build() {
    const l = Data.list(this.id), s = Data.state(), owner = Data.me();
    if (!l) return h("div", navbar("List"), h("div.empty", h("div.h3", "This list no longer exists")));
    return h("div", navbar("List"),
      h("div.scroll.under-nav",
        h("div.pad", h("div.stackcov", l.books.slice(0, 5).map(id => cover(Data.book(id), 72))),
          h("div.h1.mt12", l.title), l.description ? h("p.serif.muted.mt6", l.description) : null,
          h("div.row.gap8.mt12.small", avatar(owner, 28), h("span", "Your private list")),
          h("div.row.gap12.mt12.small.muted.num", h("span", F.books(l.books.length)), h("span", `Created ${F.date(l.created)}`)),
          h("div.row.gap8.mt12",
            h("button.btn.sm.secondary", { onclick: () => iosAlert("Delete the list?", `“${l.title}” will be deleted for good.`, [{ label: "Cancel" }, { label: "Delete", red: true, do: () => { s.lists = s.lists.filter(x => x.id !== l.id); Data.save(); Nav.back(); toast("List deleted"); } }]) }, icon("trash"), "Delete"),
            Accounts.current().isCreator ? h("button.btn.sm.primary", { onclick: () => Nav.push(new PostEditor({ kind: "list", title: l.title, body: l.description, items: l.books.map(bookId => ({ bookId, note: "" })) })) }, "Publish as a post") : null)),
        h("div.grid3.pad.mt20", l.books.map((id, i) => { const b = Data.book(id); return h("button.col", { style: { textAlign: "left" }, onclick: () => Nav.push(new BookScreen(id)) }, cover(b, 110, { rank: i + 1, style: { width: "100%" } }), h("div.xs.strong.mt4.clamp2", b.title), h("div.xs.faint", b.author)); })),
        !l.books.length ? h("div.empty", h("div.h3", "Empty list"), h("p.small", "Add books from their page, with the list button.")) : null,
        h("div.bottom-space")));
  }
}
