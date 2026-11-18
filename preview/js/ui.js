/* UI components, icons and navigation. */
"use strict";

/* h("div.card.block", {onclick}, ...children) */
function h(sel, attrs, ...kids) {
  if (attrs == null || typeof attrs !== "object" || attrs instanceof Node || Array.isArray(attrs)) { kids.unshift(attrs); attrs = {}; }
  const [tag, ...cls] = sel.split(".");
  const e = document.createElement(tag || "div");
  if (cls.length) e.className = cls.join(" ");
  for (const [k, v] of Object.entries(attrs)) {
    if (v == null || v === false) continue;
    if (k.startsWith("on")) e.addEventListener(k.slice(2), v);
    else if (k === "style" && typeof v === "object") Object.assign(e.style, v);
    else if (k === "html") e.innerHTML = v;
    else e.setAttribute(k, v === true ? "" : v);
  }
  for (const k of kids.flat(Infinity)) if (k != null && k !== false) e.append(k instanceof Node ? k : document.createTextNode(String(k)));
  return e;
}

function setKids(el, ...kids) { el.replaceChildren(...kids.flat(Infinity).filter(k => k != null && k !== false)); return el; }

/* Icons: Lucide strokes (ISC License, lucide.dev). */
const ICONS = {
  home: '<path d="M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6h-6v6H4a1 1 0 0 1-1-1z"/>',
  search: '<circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/>',
  plus: '<path d="M5 12h14"/><path d="M12 5v14"/>',
  diary: '<path d="M4 4.5A1.5 1.5 0 0 1 5.5 3H19v15H5.5A1.5 1.5 0 0 0 4 19.5z"/><path d="M4 19.5A1.5 1.5 0 0 0 5.5 21H19v-3"/><path d="M9 7h6"/>',
  user: '<path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
  bell: '<path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/>',
  heart: '<path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"/>',
  comment: '<path d="M7.9 20A9 9 0 1 0 4 16.1L2 22Z"/>',
  back: '<path d="m15 18-6-6 6-6"/>', chev: '<path d="m9 18 6-6-6-6"/>', down: '<path d="m6 9 6 6 6-6"/>',
  x: '<path d="M18 6 6 18"/><path d="m6 6 12 12"/>', check: '<path d="M20 6 9 17l-5-5"/>',
  more: '<circle cx="5" cy="12" r="1.2"/><circle cx="12" cy="12" r="1.2"/><circle cx="19" cy="12" r="1.2"/>',
  gear: '<path d="M12.2 2h-.4a2 2 0 0 0-2 2v.2a2 2 0 0 1-1 1.7l-.4.3a2 2 0 0 1-2 0l-.2-.1a2 2 0 0 0-2.7.7l-.2.4a2 2 0 0 0 .7 2.7l.2.1a2 2 0 0 1 1 1.7v.5a2 2 0 0 1-1 1.7l-.2.1a2 2 0 0 0-.7 2.7l.2.4a2 2 0 0 0 2.7.7l.2-.1a2 2 0 0 1 2 0l.4.3a2 2 0 0 1 1 1.7v.2a2 2 0 0 0 2 2h.4a2 2 0 0 0 2-2v-.2a2 2 0 0 1 1-1.7l.4-.3a2 2 0 0 1 2 0l.2.1a2 2 0 0 0 2.7-.7l.2-.4a2 2 0 0 0-.7-2.7l-.2-.1a2 2 0 0 1-1-1.7v-.5a2 2 0 0 1 1-1.7l.2-.1a2 2 0 0 0 .7-2.7l-.2-.4a2 2 0 0 0-2.7-.7l-.2.1a2 2 0 0 1-2 0l-.4-.3a2 2 0 0 1-1-1.7V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/>',
  play: '<polygon points="7 4 20 12 7 20 7 4"/>', pause: '<path d="M8 5v14M16 5v14"/>',
  share: '<path d="M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8"/><path d="m16 6-4-4-4 4"/><path d="M12 2v13"/>',
  pin: '<path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="3"/>',
  moon: '<path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/>', sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
  lock: '<rect width="18" height="11" x="3" y="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/>',
  eye: '<path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>',
  eyeoff: '<path d="M9.9 4.2A10 10 0 0 1 12 4c7 0 10 8 10 8a13 13 0 0 1-1.7 2.7"/><path d="M6.6 6.6A13.5 13.5 0 0 0 2 12s3 8 10 8a9.7 9.7 0 0 0 5.4-1.6"/><path d="m2 2 20 20"/><path d="M14.1 14.1a3 3 0 1 1-4.2-4.2"/>',
  list: '<path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01"/>',
  bookopen: '<path d="M2 4h6a4 4 0 0 1 4 4v13a3 3 0 0 0-3-3H2z"/><path d="M22 4h-6a4 4 0 0 0-4 4v13a3 3 0 0 1 3-3h7z"/>',
  mic: '<rect x="9" y="2" width="6" height="12" rx="3"/><path d="M19 10v1a7 7 0 0 1-14 0v-1M12 18v4"/>',
  mail: '<rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-10 6L2 7"/>',
  timer: '<circle cx="12" cy="13" r="8"/><path d="M12 9v4l2 2M9 2h6"/>',
  scan: '<path d="M3 7V5a2 2 0 0 1 2-2h2M17 3h2a2 2 0 0 1 2 2v2M21 17v2a2 2 0 0 1-2 2h-2M7 21H5a2 2 0 0 1-2-2v-2M7 12h10"/>',
  shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10"/>',
  download: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>',
  upload: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12"/>',
  trash: '<path d="M3 6h18M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6M10 11v6M14 11v6M9 6V4h6v2"/>',
  text: '<path d="M4 7V4h16v3M9 20h6M12 4v16"/>', star: '<polygon points="12 2 15.1 8.3 22 9.3 17 14.1 18.2 21 12 17.8 5.8 21 7 14.1 2 9.3 8.9 8.3 12 2"/>',
  help: '<circle cx="12" cy="12" r="10"/><path d="M9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3M12 17h.01"/>',
  doc: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6M8 13h8M8 17h5"/>',
  link: '<path d="M10 13a5 5 0 0 0 7.5.5l3-3a5 5 0 0 0-7-7l-1.7 1.7"/><path d="M14 11a5 5 0 0 0-7.5-.5l-3 3a5 5 0 0 0 7 7l1.7-1.7"/>',
  users: '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8"/>',
  logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9"/>',
  calendar: '<rect width="18" height="18" x="3" y="4" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/>',
  quote: '<path d="M3 21c3 0 7-1 7-8V5c0-1.2-.8-2-2-2H4c-1.2 0-2 .8-2 2v6c0 1.2.8 2 2 2h3c0 3-1 5-4 5zM15 21c3 0 7-1 7-8V5c0-1.2-.8-2-2-2h-4c-1.2 0-2 .8-2 2v6c0 1.2.8 2 2 2h3c0 3-1 5-4 5z"/>',
};
function icon(name, cls = "") {
  const s = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  s.setAttribute("viewBox", "0 0 24 24"); s.setAttribute("class", "i " + cls); s.innerHTML = ICONS[name] || "";
  return s;
}

/* ---------- Formatting ---------- */
const MES = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const F = {
  n(x) { return Math.round(x).toLocaleString("en-US"); },
  k(x) { return x >= 10000 ? (x / 1000).toFixed(x >= 100000 ? 0 : 1).replace(".0", "") + "k" : F.n(x); },
  date(k) { const d = typeof k === "string" ? D.parse(k) : k; return `${MES[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()}`; },
  dateShort(k) { const d = typeof k === "string" ? D.parse(k) : k; return `${MES[d.getMonth()]} ${d.getDate()}`; },
  ago(stamp, now) {
    const t = typeof stamp === "string" ? D.parseStamp(stamp) : stamp;
    const min = Math.round((now - t) / 60000);
    if (min < 1) return "now";
    if (min < 60) return `${min} min`;
    const hrs = Math.round(min / 60);
    if (hrs < 24) return `${hrs} h`;
    const days = Math.round(hrs / 24);
    if (days < 7) return `${days} d`;
    return F.dateShort(t);
  },
  pages(n) { return n === 1 ? "1 page" : `${F.n(n)} pages`; },
  books(n) { return n === 1 ? "1 book" : `${F.n(n)} books`; },
  pct(x) { return `${Math.round(x * 100)}%`; },
};

/* ---------- Components ---------- */
const COVER_TINTS = ["#3E5C76", "#8A5A44", "#2F6B47", "#6B4E71", "#7A3B3B", "#4A6B5D", "#B5893B"];
function cover(book, w = 72, opts = {}) {
  const c = h(`div.cover.cover-w${w}`, { onclick: opts.onclick, role: opts.onclick ? "button" : null, "aria-label": book.title });
  if (Data.coverUrl(book.id)) c.append(h("img", { src: Data.coverUrl(book.id), alt: "", loading: "lazy" }));
  else {
    c.classList.add("type"); c.style.background = COVER_TINTS[book.id.length % COVER_TINTS.length];
    c.append(h("div.t", book.title), h("div.a", book.author));
  }
  if (opts.rank) c.append(h("span.rank", String(opts.rank)));
  if (opts.style) Object.assign(c.style, opts.style);
  return c;
}

function stars(value, size = 12) {
  const one = () => { const s = icon("star"); s.setAttribute("width", size); s.setAttribute("height", size); s.style.width = size + "px"; s.style.height = size + "px"; return s; };
  const base = h("span.base", [1, 2, 3, 4, 5].map(one));
  const fill = h("span.fill", [1, 2, 3, 4, 5].map(one));
  fill.style.width = `${(Math.max(0, Math.min(5, value)) / 5) * 100}%`;
  return h("span.stars", { "aria-label": `${value} out of 5 stars` }, base, fill);
}

function avatar(user, size = 36) {
  const name = user.name || user.id || "?";
  const ini = name.split(/\s+/).filter(Boolean).slice(0, 2).map(p => p[0]).join("").toUpperCase();
  // A profile photo (a small JPEG data URL from Settings › Profile) replaces the initials.
  return h(`div.avatar.av${size}`, { style: { background: user.color || "#5B554C" } }, user.avatar ? h("img", { src: user.avatar, alt: "" }) : ini);
}

function sect(title, more, onMore) {
  return h("div.sect", h("span.caps", title), more ? (onMore ? h("button.more", { onclick: onMore }, more) : h("span.more.num", more)) : null);
}

function histogram(hist, mine) {
  const max = Math.max(...hist, 1);
  return h("div.histo", hist.map((v, i) => { const b = h("i", { title: `${(i + 1) / 2} stars: ${F.n(v)}` }); b.style.height = `${Math.max(2, (v / max) * 100)}%`; if (mine && Math.round(mine * 2) - 1 === i) b.className = "mine"; return b; }));
}

function bar(ratio, cls = "") { const b = h(`div.bar${cls ? "." + cls : ""}`, h("i")); b.firstChild.style.width = `${Math.max(0, Math.min(1, ratio)) * 100}%`; return b; }

function switchEl(on, onchange) {
  const s = h("button.switch" + (on ? ".on" : ""), { role: "switch", "aria-checked": on ? "true" : "false" });
  s.addEventListener("click", e => { e.stopPropagation(); const v = !s.classList.contains("on"); s.classList.toggle("on", v); s.setAttribute("aria-checked", v); onchange(v); });
  return s;
}


/* Updates an existing tree to match a freshly built one, in place. Inputs are kept (never recreated),
   so typing, focus, the cursor and the phone keyboard are not interrupted. Used by forms that redraw
   while the user types. */
function morph(oldEl, newEl) {
  if (oldEl.nodeType !== newEl.nodeType || oldEl.nodeName !== newEl.nodeName) { oldEl.replaceWith(newEl); return; }
  if (oldEl.nodeType === 3) { if (oldEl.textContent !== newEl.textContent) oldEl.textContent = newEl.textContent; return; }
  if (oldEl.nodeType !== 1) return;
  if (oldEl.namespaceURI === "http://www.w3.org/2000/svg") { if (oldEl.innerHTML !== newEl.innerHTML) oldEl.replaceWith(newEl); return; }
  const isInput = oldEl.nodeName === "INPUT" || oldEl.nodeName === "TEXTAREA";
  for (const { name } of [...oldEl.attributes]) if (!newEl.hasAttribute(name) && !(isInput && name === "value")) oldEl.removeAttribute(name);
  for (const { name, value } of [...newEl.attributes]) { if (isInput && name === "value") continue; if (oldEl.getAttribute(name) !== value) oldEl.setAttribute(name, value); }
  if (oldEl.nodeName === "BUTTON") oldEl.disabled = newEl.disabled;
  if (isInput) { if (document.activeElement !== oldEl && oldEl.value !== newEl.value) oldEl.value = newEl.value; return; }
  oldEl.style.cssText = newEl.style.cssText;
  const a = [...oldEl.childNodes], b = [...newEl.childNodes];
  for (let i = 0; i < b.length; i++) { if (i < a.length) morph(a[i], b[i]); else oldEl.append(b[i]); }
  for (let i = b.length; i < a.length; i++) a[i].remove();
}

/* ---------- Navigation ---------- */
class Screen {
  constructor() { this.tab = null; }
  build() { return h("div"); }
  onShow() {}
  onHide() {}
}

const Nav = {
  root: null, stackEl: null, stack: [], tab: "home",
  mount(root) { this.root = root; this.stackEl = h("div.stack"); root.append(this.stackEl); },
  render(scr, anim) {
    const el = scr.build(); el.classList.add("screen"); if (anim) el.classList.add(anim);
    scr.el = el;
    // Keep the scroll position on refresh
    const old = this.stackEl.querySelector(".screen"); const sc = old && old.__scr === scr ? old.querySelector(".scroll")?.scrollTop : null;
    el.__scr = scr;
    this.stackEl.replaceChildren(el);
    if (sc != null) { const s = el.querySelector(".scroll"); if (s) s.scrollTop = sc; }
    const navbar = el.querySelector(".navbar"); const s = el.querySelector(".scroll");
    if (navbar && s) s.addEventListener("scroll", () => navbar.classList.toggle("scrolled", s.scrollTop > 4));
    scr.onShow();
  },
  push(scr) { this.current()?.onHide(); this.stack.push(scr); this.render(scr, "push"); },
  replace(scr) { this.stack.pop()?.onHide(); this.stack.push(scr); this.render(scr, "push"); },
  back() { if (this.stack.length < 2) return; this.stack.pop().onHide(); this.render(this.current()); },
  reset(scr, tab) { this.stack.forEach(s => s.onHide()); this.stack = [scr]; if (tab) this.tab = tab; this.render(scr); },
  refresh() { const c = this.current(); if (c) this.render(c, "still"); },
  current() { return this.stack[this.stack.length - 1]; },
};

/* Standard top bar */
function navbar(title, opts = {}) {
  const left = Nav.stack.length > 1 || opts.onBack
    ? h("button.iconbtn", { "aria-label": "Back", onclick: opts.onBack || (() => Nav.back()) }, icon("back"))
    : h("div", { style: { width: "44px" } });
  return h("div.navbar" + (opts.transparent ? ".transparent" : ""), left, h("div.title", title || ""), opts.right || h("div", { style: { width: "44px" } }));
}

function tabbar(active) {
  const t = (id, label, ic, make) => h("button.tab" + (active === id ? ".active" : ""), { onclick: () => Nav.reset(make(), id), "aria-label": label }, icon(ic), label);
  return h("nav.tabbar",
    t("home", "Home", "home", () => new HomeScreen()),
    t("explore", "Explore", "search", () => new ExploreScreen()),
    h("button.tab.plus", { onclick: () => LogSheet.open(), "aria-label": "Log reading" }, h("span.ring", icon("plus")), "Log"),
    t("diary", "Library", "diary", () => new DiaryScreen()),
    t("profile", "Profile", "user", () => new ProfileScreen(Data.me())));
}

/* Bottom sheet */
const Sheet = {
  open(title, body, opts = {}) {
    this.close(true);
    const scrim = h("div.scrim", { onclick: () => !opts.locked && this.close() });
    const left = opts.left || h("button.navbar-btn.textbtn", { style: { color: "var(--ink-2)", height: "44px", padding: "0 10px" }, onclick: () => this.close() }, "Cancel");
    const sheet = h("div.sheet", h("div.grab"), h("div.shead", left, h("div.title", title), opts.right || h("div", { style: { width: "76px" } })), h("div.sbody", body));
    App.root.append(scrim, sheet);
    this.cur = { scrim, sheet, onClose: opts.onClose };
    return sheet;
  },
  close(silent) { if (!this.cur) return; this.cur.scrim.remove(); this.cur.sheet.remove(); const f = this.cur.onClose; this.cur = null; if (!silent && f) f(); },
};

/* iOS-style alert */
function iosAlert(title, msg, actions) {
  const scrim = h("div.scrim");
  const box = h("div.alert", h("div.t", title), msg ? h("div.m", msg) : null,
    h("div.acts", actions.map(a => h("button" + (a.bold ? ".bold" : "") + (a.red ? ".red" : ""), { onclick: () => { scrim.remove(); box.remove(); a.do && a.do(); } }, a.label))));
  App.root.append(scrim, box);
}

/* Real sharing: the system share sheet when the browser has one, otherwise the clipboard. */
async function shareText(text) {
  try { if (navigator.share) { await navigator.share({ text }); return; } } catch (e) { if (e && e.name === "AbortError") return; }
  try { await navigator.clipboard.writeText(text); toast("Copied. Paste it anywhere."); } catch { toast("Couldn't share from this browser."); }
}

let _toastT;
function toast(msg) {
  let t = App.root.querySelector(".toast"); if (!t) { t = h("div.toast"); App.root.append(t); }
  t.textContent = msg; t.classList.add("on"); clearTimeout(_toastT); _toastT = setTimeout(() => t.classList.remove("on"), 2400);
}

function banner(title, body) {
  const b = h("div.banner", h("div.appic", "m"), h("div.grow", h("div.row", h("span.strong.small.grow", "Margin"), h("span.xs", { style: { color: "#888" } }, "now")), h("div.small.strong", title), h("div.small", body)));
  App.root.append(b); setTimeout(() => b.remove(), 4200);
}

/* One short, real Goodreads review for a book (4+ stars and brief when possible), or null. */
function shortReview(bookId) {
  const rv = Data.reviewsFor(bookId).filter(r => r.text && r.text.length <= 260);
  return rv.find(r => r.rating >= 4 * (r.ratingScale || 5) / 5) || rv[0] || null;
}

/* Calm "Finding your match…" interstitial. */
function findingView() {
  return h("div.finding", h("div.dots", h("i"), h("i"), h("i")), h("div.h2", "Finding your match…"), h("p.small.muted", "Looking through the catalog"));
}

/* Spotlight + compact list of matches, shared by the first-book flow and "Find your next book".
   owner.sel is the spotlighted index; tapping a row swaps it in, tapping the spotlight calls onOpen(book).
   opts.why also shows the reason under a review ("Because you rated ..."). opts.onStart(book) adds the main
   call to action, "Start reading", under the spotlight; the spotlight itself still opens the book page. */
function matchList(owner, recs, onOpen, opts = {}) {
  if (!recs.length) return h("p.small.muted.mt12", "No matches left in the catalog. Try other answers.");
  const i = Math.min(owner.sel || 0, recs.length - 1), top = recs[i], b = top.book, rv = shortReview(b.id);
  const spot = h("button.spot", { onclick: () => onOpen(b) },
    cover(b, 128), h("div.h1.mt16", { style: { fontSize: "24px" } }, b.title), h("div.muted.mt4", b.author),
    rv ? h("div.spotq", { role: "button", onclick: e => { e.stopPropagation(); Nav.push(new ReviewsScreen(b.id)); } },
      h("div.serif.italic", `“${rv.text}”`), h("div.xs.faint.mt6", `${rv.author} · See reviews`)) : null,
    !rv || opts.why ? h("div.small.accent.mt12", top.reason) : null);
  const cta = opts.onStart ? [h("button.btn.accent.mt16", { onclick: () => opts.onStart(b) }, "Start reading this"),
    h("button.btn.ghost.mt4", { onclick: () => onOpen(b) }, "See the book")] : null;
  const rest = recs.map((r, k) => [r, k]).filter(([, k]) => k !== i);
  return [spot, cta, h("div.caps.mt24", "More matches"),
    h("div.list", { style: { margin: "8px 0 0" } }, rest.map(([r, k]) => h("button.item", { onclick: () => { owner.sel = k; Nav.refresh(); } },
      cover(r.book, 44), h("div.grow", h("div.strong.clamp2", r.book.title), h("div.small.muted", r.book.author)))))];
}

/* A short burst of confetti over the whole app, for a real decision (starting a book).
   It lives inside #app, so the in-app Reduce motion setting (.reduce-motion) hides it too. */
const CONFETTI = { pieces: 80, ms: 4300 };
function celebrate() {
  const colors = ["var(--accent)", "var(--star)", "var(--ok)", "var(--ink)", "#E9B44C"];
  const box = h("div.confetti", { "aria-hidden": "true" });
  for (let i = 0; i < CONFETTI.pieces; i++) {
    const c = h("i"), r = Math.random;
    c.style.left = `${r() * 100}%`; c.style.background = colors[i % colors.length];
    // Custom properties need setProperty (Object.assign on style ignores them).
    [["--x", `${(r() - .5) * 200}px`], ["--r", `${360 + r() * 720}deg`], ["--d", `${2.2 + r() * 1.2}s`], ["--delay", `${r() * .5}s`]]
      .forEach(([k, v]) => c.style.setProperty(k, v));
    box.append(c);
  }
  App.root.append(box);
  setTimeout(() => box.remove(), CONFETTI.ms);
}

/* Search a book by title or author, filtering in place (no re-render, so the keyboard stays open).
   Elements with data-q in the same parent's next sibling grid/list are shown or hidden. */
const norm = s => (s || "").toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "");
function searchKey(b) { return norm(`${b.title} ${b.author}`); }
function bookSearch(placeholder) {
  const empty = h("p.small.faint.mt12", { style: { display: "none" } }, "No book matches. Try another word.");
  const input = h("input.input", { type: "search", placeholder, autocomplete: "off", "aria-label": placeholder });
  input.addEventListener("input", () => {
    const q = norm(input.value).trim();
    const grid = wrap.nextElementSibling; if (!grid) return;
    let shown = 0;
    grid.querySelectorAll("[data-q]").forEach(el => { const on = !q || el.dataset.q.includes(q); el.style.display = on ? "" : "none"; shown += on; });
    empty.style.display = shown ? "none" : "";
  });
  const wrap = h("div.mt16", input, empty);
  return wrap;
}

/* Profile photo: pick an image, crop it square and shrink it to a 160 px JPEG (small enough to live in the profile row). */
function pickPhoto(onDone) {
  const input = h("input", { type: "file", accept: "image/*", style: { display: "none" } });
  input.addEventListener("change", () => {
    const f = input.files && input.files[0]; input.remove(); if (!f) return;
    const img = new Image(), url = URL.createObjectURL(f);
    img.onload = () => {
      const n = 160, c = document.createElement("canvas"), side = Math.min(img.width, img.height);
      c.width = c.height = n;
      c.getContext("2d").drawImage(img, (img.width - side) / 2, (img.height - side) / 2, side, side, 0, 0, n, n);
      URL.revokeObjectURL(url); onDone(c.toDataURL("image/jpeg", .82));
    };
    img.onerror = () => { URL.revokeObjectURL(url); toast("That file isn't an image we can read."); };
    img.src = url;
  });
  document.body.append(input); input.click();
}

/* Stars to rate a book, in half stars (tap the left half of a star for .5). Tapping the current value clears it. */
function ratingInput(val, onRate) {
  const wrap = h("div.row", { style: { gap: "2px" } });
  for (let i = 1; i <= 5; i++) {
    const b = h("button", { style: { width: "30px", height: "30px", position: "relative" }, "aria-label": `${i} stars` }, stars(Math.max(0, Math.min(1, val - (i - 1))) * 5, 26));
    b.firstChild.style.width = "26px"; b.querySelectorAll(".base svg").forEach((x, k) => { if (k) x.style.display = "none"; }); b.querySelectorAll(".fill svg").forEach((x, k) => { if (k) x.style.display = "none"; });
    b.querySelector(".fill").style.width = `${Math.max(0, Math.min(1, val - (i - 1))) * 100}%`;
    b.addEventListener("click", e => { const half = e.offsetX < 13; onRate(val === i - (half ? .5 : 0) ? 0 : i - (half ? .5 : 0)); });
    wrap.append(b);
  }
  return wrap;
}

/* Password strength as bars plus a word and a tip (V.strength and V.strengthTip in screens-auth.js). */
function passwordMeter(pw, st = V.strength(pw)) {
  const word = !pw ? "" : ["Too weak", "Weak", "Okay", "Good", "Strong"][st];
  return [h("div.strength" + (st <= 1 ? ".weak" : st === 2 ? ".mid" : ""), [0, 1, 2, 3].map(i => h("i" + (i < st ? ".on" : "")))),
    pw ? h("div.strength-label", h("b", word), h("span", V.strengthTip(pw))) : null];
}
