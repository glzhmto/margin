/* Friends, invite link + QR, reader profiles, shared pacts, creators and posts. All real data (js/net.js). */
"use strict";

function inviteLink(username) { return `${location.origin}/app/?add=${encodeURIComponent(username || Accounts.session())}`; }
async function copyText(t, msg) {
  try { await navigator.clipboard.writeText(t); toast(msg || "Link copied"); }
  catch { iosAlert("Copy this link", t, [{ label: "OK", bold: true }]); }
}
function qrImage(text, size = 184) {
  try {
    const qr = qrcode(0, "M"); qr.addData(text); qr.make();
    const cells = qr.getModuleCount() + 8, cell = Math.max(2, Math.floor(size / cells));
    return h("img", { src: qr.createDataURL(cell, cell * 4), alt: "QR code with your invite link", width: cell * cells, height: cell * cells, style: { borderRadius: "10px", background: "#fff", imageRendering: "pixelated" } });
  } catch (e) { console.warn(e); return h("p.small.faint", "Couldn't draw the QR code."); }
}

/* ---- Small rows used across screens ---- */
function relButton(p, opts = {}) {
  const r = Social.rel(p.id), sm = "button.btn.sm";
  // Live screens redraw from Social.changed(); the others (search results) need an explicit redraw.
  const run = (f) => async e => { e.stopPropagation(); e.currentTarget.disabled = true; await f(); const c = Nav.current(); if (c && !c.live) { if (c.draw) c.draw(); else Nav.refresh(); } };
  switch (r.kind) {
    case "self": return null;
    case "friend": return h(sm + ".secondary", { onclick: run(async () => iosAlert(`Remove ${pname(p)} as a friend?`, "You'll stop seeing each other's reading. They won't be notified.", [{ label: "Cancel", do: () => Nav.refresh() }, { label: "Remove", red: true, do: () => Social.remove(r.row) }])) }, icon("check"), "Friends");
    case "incoming": return h("div.row.gap6", h(sm + ".primary", { onclick: run(() => Social.accept(r.row)) }, "Accept"), opts.compact ? null : h(sm + ".secondary", { onclick: run(() => Social.remove(r.row)) }, "Decline"));
    case "outgoing": return h(sm + ".secondary", { onclick: run(() => Social.remove(r.row)) }, "Requested");
    default: return h(sm + ".primary", { onclick: run(() => Social.request(p.id).then(() => toast(`Request sent to ${pfirst(p)}`))) }, "Add friend");
  }
}
function followButton(p) {
  if (p.id === Net.uid || !p.is_creator) return null;
  const on = Social.followingIds.has(p.id);
  return h("button.btn.sm" + (on ? ".secondary" : ".primary"), { onclick: async e => { e.stopPropagation(); e.currentTarget.disabled = true; await (on ? Social.unfollow(p.id) : Social.follow(p.id)); Nav.refresh(); } }, on ? "Following" : "Follow");
}
function personRow(p, opts = {}) {
  const b = Data.book(p.current_book_id);
  return h("div.feeditem", { onclick: () => Nav.push(new UserScreen(p)), style: { alignItems: "center" } }, avatar(pav(p), 44),
    h("div.grow", h("div.strong", pname(p)), h("div.small.muted", `@${p.username}`, p.is_creator ? [h("span.dotsep"), "Creator"] : null),
      b ? h("div.xs.faint.mt2.clamp2", `Reading ${b.title}`) : p.university && opts.uni ? h("div.xs.faint.mt2", p.university) : null),
    opts.noButton ? null : relButton(p, { compact: true }));
}
function requestRow(x) {
  return h("div.feeditem", { onclick: () => Nav.push(new UserScreen(x.p)), style: { alignItems: "center" } }, avatar(pav(x.p), 44),
    h("div.grow", h("div.strong", pname(x.p)), h("div.small.muted", `@${x.p.username} wants to be your friend`)),
    h("div.row.gap6", h("button.btn.sm.primary", { onclick: async e => { e.stopPropagation(); e.currentTarget.disabled = true; await Social.accept(x.f); toast(`You and ${pfirst(x.p)} are friends now`); } }, "Accept"),
      h("button.iconbtn", { "aria-label": "Decline", onclick: async e => { e.stopPropagation(); await Social.remove(x.f); } }, icon("x"))));
}
function creatorCard(p) {
  return h("div.card.col", { style: { width: "140px", padding: "14px 10px", alignItems: "center", textAlign: "center" } },
    h("button", { onclick: () => Nav.push(new UserScreen(p)) }, avatar(pav(p), 44)), h("div.small.strong.mt6.clamp2", pname(p)), h("div.xs.faint", `@${p.username}`),
    h("div.mt8", { style: { width: "100%" } }, followButton(p)));
}
const POST_KIND = { list: "List", text: "Note", video: "Video" };
function postRow(p) {
  const a = p.profiles || {}, books = p.kind === "list" ? (p.items || []).map(i => Data.book(i.bookId)).filter(Boolean) : [];
  return h("button.pad", { style: { width: "100%", textAlign: "left", padding: "12px 16px 14px", borderBottom: "1px solid var(--rule)", display: "block" }, onclick: () => Nav.push(new PostScreen(p.id)) },
    books.length ? h("div.stackcov", { style: { marginBottom: "8px" } }, books.slice(0, 5).map(b => cover(b, 72))) : null,
    h("div.row.gap6.xs.faint", h("span.badge" + (p.kind === "video" ? ".todo" : ".ok"), POST_KIND[p.kind]), h("span", F.ago(new Date(p.created_at), new Date()))),
    h("div.h3.mt6", p.title),
    h("div.row.gap6.mt4.xs.muted", avatar(pav(a), 20), h("span", pname(a)), books.length ? [h("span.dotsep"), h("span.num", F.books(books.length))] : null),
    p.body ? h("div.small.muted.mt4.clamp2", p.body) : null);
}

/* ---------------- Friends hub ---------------- */
class FriendsScreen extends Screen {
  constructor() { super(); this.live = true; }
  build() {
    const acc = Accounts.current();
    const invite = h("div.card.block", { style: { margin: "8px 16px 0" } },
      h("div.row.gap12", h("div.grow", h("div.strong", "Invite a friend"), h("div.small.muted.mt2", "Share your link. When they open it and sign up, they land on your profile."),
        h("div.xs.faint.mt6", { style: { wordBreak: "break-all" } }, inviteLink())),
        h("button", { onclick: () => InviteSheet.open(), "aria-label": "Show QR code" }, qrImage(inviteLink(), 72))),
      h("div.row.gap8.mt12", h("button.btn.sm.primary", { style: { flex: 1 }, onclick: () => InviteSheet.share() }, icon("share"), "Share link"), h("button.btn.sm.secondary", { onclick: () => InviteSheet.open() }, "QR code")));
    return h("div", navbar("Friends", { right: h("button.iconbtn", { "aria-label": "Find friends", onclick: () => Nav.push(new FindFriendsScreen()) }, icon("search")) }),
      h("div.scroll.under-nav",
        h("div.pad.mt4", h("button.input.row.gap8", { style: { width: "100%", textAlign: "left", color: "var(--ink-3)", height: "42px", borderRadius: "10px" }, onclick: () => Nav.push(new FindFriendsScreen()) }, icon("search"), "Search by @username or name")),
        Social.incoming.length ? [sect("Requests", F.n(Social.incoming.length)), Social.incoming.map(requestRow)] : null,
        sect("Your friends", Social.friends.length ? F.n(Social.friends.length) : null),
        !Social.loaded ? h("p.pad.small.faint", "Loading…")
          : Social.friends.length ? Social.friends.map(x => personRow(x.p, { noButton: true })) : null,
        invite,
        Social.outgoing.length ? [sect("Sent"), Social.outgoing.map(x => personRow(x.p))] : null,
        acc.university ? h("div.pad.mt16", h("button.btn.secondary", { onclick: () => Nav.push(new FindFriendsScreen()) }, `People from ${acc.university}`)) : null,
        h("div.bottom-space")));
  }
}

class FindFriendsScreen extends Screen {
  constructor() { super(); this.q = ""; this.res = null; this.busy = false; }
  onShow() { screenLoad(this, () => Social.sameUniversity()); }
  build() {
    const input = h("input.input", { value: this.q, placeholder: "@username or name", "aria-label": "Search readers", autocapitalize: "none", style: { paddingLeft: "40px", height: "42px", borderRadius: "10px" } });
    input.addEventListener("input", () => { this.q = input.value; this.search(); });
    this.box = h("div");
    setTimeout(() => input.focus(), 250);
    const el = h("div", navbar("Find friends"), h("div.scroll.under-nav",
      h("div.pad.mt4", { style: { position: "relative" } }, h("span", { style: { position: "absolute", left: "28px", top: "11px", color: "var(--ink-3)" } }, icon("search")), input),
      this.box, h("div.bottom-space")));
    this.draw();
    return el;
  }
  search() {
    const q = this.q.trim(); clearTimeout(this._t);
    if (cleanQuery(q).replace(/^@/, "").length < 2) { this.res = null; return this.draw(); }
    this.busy = true; this.draw();
    this._t = setTimeout(() => Social.search(q).then(r => { if (this.q.trim() === q) { this.res = r; this.busy = false; this.draw(); } }).catch(e => { this.busy = false; netFail(e); }), 250);
  }
  draw() {
    if (!this.box) return;
    const acc = Accounts.current(), uni = this.d || [];
    setKids(this.box,
      this.res ? [sect("Results"), this.res.length ? this.res.map(p => personRow(p))
        : h("div.empty", h("div.h3", `No reader called “${this.q.trim()}”`), h("p.small", "Check the spelling, or send them your invite link."), h("button.btn.sm.primary.mt12", { style: { margin: "12px auto 0" }, onclick: () => InviteSheet.share() }, "Share invite link"))]
        : this.busy ? h("p.pad.small.faint", "Searching…") : null,
      !this.res ? [sect(acc.university ? `From ${acc.university}` : "Suggestions"),
        !acc.university ? h("p.pad.small.muted", "Add your university in Settings › Account and we'll suggest people from there.")
          : !this._loaded ? h("p.pad.small.faint", "Loading…")
          : uni.length ? uni.map(p => personRow(p, { uni: true }))
          : h("p.pad.small.muted", "Nobody else from there yet. Share your invite link with classmates.")] : null);
  }
  // Re-render the list in place (keeps the input focused) when the screen refreshes for live data.
  onHide() { clearTimeout(this._t); }
}

const InviteSheet = {
  open() {
    const link = inviteLink(), u = Accounts.session();
    Sheet.open("Invite", h("div.col", { style: { alignItems: "center", textAlign: "center" } },
      h("div.small.muted", "Scan to add"), h("div.h3.mt2", `@${u}`), h("div.mt12", qrImage(link, 220)),
      h("div.xs.faint.mt12", { style: { wordBreak: "break-all" } }, link),
      h("div.row.gap8.mt16", { style: { width: "100%" } }, h("button.btn.primary", { style: { flex: 1 }, onclick: () => this.share() }, icon("share"), "Share"), h("button.btn.secondary", { style: { flex: 1 }, onclick: () => copyText(link) }, icon("link"), "Copy"))));
  },
  async share() {
    const link = inviteLink(), text = "I'm reading on Margin. Add me so we can keep each other reading.";
    if (navigator.share) { try { await navigator.share({ title: "Margin", text, url: link }); return; } catch (e) { if (e && e.name === "AbortError") return; } }
    copyText(link, "Invite link copied");
  },
};

/* ---------------- Another reader's profile ---------------- */
class UserScreen extends Screen {
  // Accepts a profile row or a username (from invite links and reviews).
  constructor(p) { super(); this.live = true; this.username = typeof p === "string" ? p : p && p.username; this.uid = typeof p === "object" && p ? p.id : null; }
  onShow() {
    screenLoad(this, async () => {
      const p = this.uid ? await Social.byId(this.uid).then(x => x && x.username ? Social.byUsername(x.username) : x) : await Social.byUsername(this.username);
      if (!p) return { p: null };
      this.uid = p.id;
      const r = Social.rel(p.id), canSee = r.kind === "friend" || Social.followingIds.has(p.id);
      const [act, posts] = await Promise.all([canSee ? Social.activityOf(p.id, 12) : [], p.is_creator ? Social.posts({ author: p.id, limit: 20 }) : []]);
      return { p, act, posts };
    });
  }
  build() {
    const d = this.d;
    if (!d) return h("div", navbar(this.username ? "@" + this.username : ""), h("p.pad.small.faint.mt12", this.err || "Loading…"));
    if (!d.p) return h("div", navbar(""), h("div.empty", { style: { paddingTop: "120px" } }, h("div.h3", `We couldn't find @${this.username}`), h("p.small", "The link may be old, or they changed their username."),
      h("button.btn.sm.primary.mt12", { style: { margin: "12px auto 0" }, onclick: () => Nav.replace(new FindFriendsScreen()) }, "Search readers")));
    const p = Social.profiles.get(d.p.id) || d.p, r = Social.rel(p.id), friend = r.kind === "friend";
    if (r.kind === "self") return new ProfileScreen().build();
    const cur = Data.book(p.current_book_id), favs = (p.favorites || []).map(id => Data.book(id)).filter(Boolean), read = (p.books_read || []).map(id => Data.book(id)).filter(Boolean);
    const joined = new Date(p.created_at);
    const header = h("div.col.pad", { style: { alignItems: "center", textAlign: "center" } }, avatar(pav(p), 88), h("div.h2.mt12", pname(p)),
      h("div.small.muted.mt2", `@${p.username}`, h("span.dotsep"), `on Margin since ${MES[joined.getMonth()]} ${joined.getFullYear()}`),
      p.university ? h("div.xs.faint.mt2", p.university) : null,
      p.is_creator ? h("div.mt6", h("span.badge.ok", "Creator")) : null,
      p.bio ? h("p.small.mt8", { style: { maxWidth: "300px" } }, p.bio) : null,
      h("div.row.gap8.mt12", relButton(p), followButton(p)));
    const pact = friend ? Social.friends.find(x => x.p.id === p.id) : null;
    const pactSect = friend ? [sect("Reading together"),
      pact && Data.book(pact.f.shared_book_id) ? [pactCard(pact), h("div.row.gap8.pad", h("button.btn.sm.secondary", { onclick: () => PactSheet.open(pact) }, "Change book"),
        h("button.btn.sm.ghost", { onclick: () => iosAlert("Stop reading together?", `${pfirst(p)} will see the shared book removed too.`, [{ label: "Cancel" }, { label: "Stop", red: true, do: () => Social.setPact(pact.f, null) }]) }, "Stop"))]
        : h("div.pad", h("p.small.muted", `Pick a book you'll both read. You'll each see the other's page.`), h("button.btn.sm.primary.mt8", { onclick: () => PactSheet.open(pact) }, icon("bookopen"), "Read a book together"))] : null;
    const status = friend ? [sect("Now"),
      h("div.statgrid", h("div", h("div.v", p.momentum_level || "–"), h("div.xs.muted", "reading momentum")), h("div", h("div.v", read.length), h("div.xs.muted", read.length === 1 ? "book finished" : "books finished"))),
      cur ? h("button.row.gap12.pad.mt12", { style: { width: "100%", textAlign: "left" }, onclick: () => Nav.push(new BookScreen(cur.id)) }, cover(cur, 44),
        h("div.grow", h("div.xs.faint", "Reading"), h("div.strong", cur.title), h("div.mt6", bar((p.current_page || 0) / cur.pages)), h("div.xs.faint.mt4.num", `Page ${p.current_page || 0} of ${cur.pages}`))) : h("p.pad.small.faint.mt8", "Not reading anything right now.")]
      : r.kind === "none" || r.kind === "outgoing" ? h("div.card.block.empty", { style: { margin: "8px 16px 0" } }, h("div.h3", r.kind === "outgoing" ? "Request sent" : "Read together"),
        h("p.small", r.kind === "outgoing" ? `When ${pfirst(p)} accepts, you'll see each other's current book and page.` : `Add ${pfirst(p)} as a friend to see what they're reading and share a book.`)) : null;
    return h("div", navbar(pfirst(p)),
      h("div.scroll.under-nav", header,
        status, pactSect,
        p.is_creator ? [sect("Posts", d.posts.length ? F.n(d.posts.length) : null), d.posts.length ? d.posts.map(postRow) : h("p.pad.small.muted", "No posts yet.")] : null,
        friend && favs.length ? [sect("Favorite books"), h("div.grid4.pad", favs.map(b => cover(b, 84, { onclick: () => Nav.push(new BookScreen(b.id)), style: { width: "100%" } })))] : null,
        friend && read.length ? [sect("Finished"), h("div.hscroll", read.slice(0, 16).map(b => cover(b, 72, { onclick: () => Nav.push(new BookScreen(b.id)) })))] : null,
        d.act.length ? [sect("Recent activity"), h("div", { style: { borderTop: "1px solid var(--rule)" } }, d.act.map(a => activityItem(a, { noUser: true, user: p })))] : null,
        h("div.bottom-space")));
  }
}

const PactSheet = {
  open(pact) {
    const cur = pact.f.shared_book_id, mine = Data.state().profile.currentBookId;
    const books = Catalog.data.books.slice().sort((a, b) => (b.id === mine) - (a.id === mine) || (b.id === pact.p.current_book_id) - (a.id === pact.p.current_book_id));
    Sheet.open("Read together", h("div",
      h("p.small.muted", `Pick the book you and ${pfirst(pact.p)} will read. Each of you logs pages as usual.`),
      h("div.grid3.mt12", books.map(b => h("button.col.gap4", { style: { textAlign: "left" }, onclick: async () => { Sheet.close(); await Social.setPact(pact.f, b.id); toast(`Reading ${b.title} with ${pfirst(pact.p)}`); } },
        cover(b, 110, { style: { width: "100%", outline: cur === b.id ? "3px solid var(--accent)" : "none", outlineOffset: "2px" } }),
        h("div.xs.clamp2" + (cur === b.id ? ".strong" : ".muted"), b.title),
        b.id === mine ? h("div.xs.accent", "You're reading it") : b.id === pact.p.current_book_id ? h("div.xs.accent", `${pfirst(pact.p)} is reading it`) : null)))));
  },
};

/* ---------------- Creator posts ---------------- */
function youtubeId(url) {
  const m = (url || "").trim().match(/^https:\/\/(?:www\.|m\.)?(?:youtube\.com\/(?:watch\?(?:.*&)?v=|shorts\/|embed\/|live\/)|youtu\.be\/)([A-Za-z0-9_-]{11})/);
  return m ? m[1] : null;
}

class PostScreen extends Screen {
  constructor(id) { super(); this.id = id; }
  onShow() { screenLoad(this, () => Social.post(this.id)); }
  build() {
    const p = this.d;
    if (!p) return h("div", navbar(""), this._loaded ? h("div.empty", { style: { paddingTop: "120px" } }, h("div.h3", "This post is gone"), h("p.small", "The creator may have deleted it.")) : h("p.pad.small.faint.mt12", this.err || "Loading…"));
    const a = p.profiles || {}, mine = p.author === Net.uid, vid = p.kind === "video" ? youtubeId(p.video_url) : null;
    const authorP = Social.profiles.get(p.author) || { id: p.author, username: a.username, name: a.name, color: a.color, is_creator: true };
    return h("div", navbar(POST_KIND[p.kind], { right: mine ? h("button.iconbtn", { "aria-label": "Delete post", onclick: () => iosAlert("Delete this post?", "Your followers won't see it anymore.", [{ label: "Cancel" }, { label: "Delete", red: true, do: async () => { try { await Social.deletePost(p.id); Nav.back(); toast("Post deleted"); } catch (e) { netFail(e); } } }]) }, icon("trash")) : null }),
      h("div.scroll.under-nav", h("div.pad",
        h("div.h1", p.title),
        h("div.row.gap8.mt12", h("button.row.gap8.grow", { style: { textAlign: "left" }, onclick: () => mine ? Nav.push(new ProfileScreen()) : Nav.push(new UserScreen(authorP)) }, avatar(pav(a), 36),
          h("div", h("div.small.strong", pname(a)), h("div.xs.faint", `@${a.username} · ${F.date(new Date(p.created_at))}`))), mine ? null : followButton(authorP)),
        vid ? h("div.mt16", { style: { position: "relative", paddingTop: "56.25%", borderRadius: "12px", overflow: "hidden", background: "#000" } },
          h("iframe", { src: `https://www.youtube-nocookie.com/embed/${vid}?rel=0`, title: p.title, allow: "encrypted-media; picture-in-picture", allowfullscreen: true, loading: "lazy", referrerpolicy: "strict-origin-when-cross-origin",
            style: { position: "absolute", inset: 0, width: "100%", height: "100%", border: 0 } })) : null,
        p.kind === "video" && !vid ? h("p.small.faint.mt12", "This video link can't be shown.") : null,
        p.body ? h("p.serif.mt16", { style: { whiteSpace: "pre-wrap" } }, p.body) : null),
        p.kind === "list" ? h("div.mt12", (p.items || []).map((it, i) => { const b = Data.book(it.bookId); if (!b) return null;
          return h("button.row.top.gap12.pad", { style: { width: "100%", textAlign: "left", padding: "10px 16px", borderTop: "1px solid var(--rule)" }, onclick: () => Nav.push(new BookScreen(b.id)) },
            cover(b, 56, { rank: i + 1 }), h("div.grow", h("div.strong", b.title), h("div.xs.muted", b.author), it.note ? h("p.small.mt6", it.note) : null)); })) : null,
        h("p.pad.xs.faint.mt12", "Written by a person on Margin. No like counts, no autoplay: read it and go read your book."),
        h("div.bottom-space")));
  }
}

class PostEditor extends Screen {
  constructor(prefill = {}) { super(); this.v = { kind: prefill.kind || "list", title: prefill.title || "", body: prefill.body || "", video: "", items: prefill.items || [] }; this.err = null; this.busy = false; }
  build() {
    const v = this.v;
    if (!Accounts.current().isCreator) return h("div", navbar("New post"), h("div.empty", { style: { paddingTop: "120px" } }, h("div.h3", "Creators only"), h("p.small", "Turn on Creator account in Settings › Profile to publish."),
      h("button.btn.sm.primary.mt12", { style: { margin: "12px auto 0" }, onclick: () => Nav.replace(new SettingsDetail("profile")) }, "Open Settings")));
    const inp = (k, attrs, tag = "input.input") => { const e = h(tag, attrs); e.value = v[k]; e.addEventListener("input", () => { v[k] = e.value; }); return e; };
    const items = v.items.map((it, i) => {
      const b = Data.book(it.bookId), note = h("input.input.mt6", { placeholder: "A short note: why this one? (optional)", maxlength: 200, style: { height: "38px", fontSize: "14px" } });
      note.value = it.note || ""; note.addEventListener("input", () => { it.note = note.value; });
      return h("div.row.top.gap10", { style: { padding: "10px 0", borderTop: "1px solid var(--rule)" } }, cover(b, 44),
        h("div.grow", h("div.small.strong", `${i + 1}. ${b.title}`), note),
        h("button.iconbtn", { "aria-label": `Remove ${b.title}`, onclick: () => { v.items.splice(i, 1); Nav.refresh(); } }, icon("x")));
    });
    return h("div", navbar("New post", { right: h("button.textbtn", { style: { color: "var(--link)", fontWeight: 600, height: "44px", padding: "0 10px" }, disabled: this.busy, onclick: () => this.publish() }, this.busy ? "…" : "Publish") }),
      h("div.scroll.under-nav", h("div.pad.mt4",
        h("div.seg", ["list", "text", "video"].map(k => h("button" + (v.kind === k ? ".on" : ""), { onclick: () => { v.kind = k; this.err = null; Nav.refresh(); } }, POST_KIND[k]))),
        field("Title", inp("title", { maxlength: 120, placeholder: v.kind === "list" ? "Short books for a long semester" : v.kind === "video" ? "What I read in October" : "Why I reread The Little Prince" })),
        v.kind === "video" ? field("YouTube link", inp("video", { type: "url", placeholder: "https://youtu.be/…", autocapitalize: "none" }), h("div.help", "Only YouTube links. It plays inside the post, never on its own.")) : null,
        field(v.kind === "list" ? "Intro (optional)" : v.kind === "video" ? "Text (optional)" : "Text", inp("body", { maxlength: 8000, placeholder: v.kind === "text" ? "Write it in your own words." : "", style: { minHeight: v.kind === "text" ? "200px" : "90px" } }, "textarea.input")),
        v.kind === "list" ? [h("div.row.mt20", h("span.caps.grow", `Books (${v.items.length})`), h("button.btn.sm.secondary", { onclick: () => this.pickBooks() }, icon("plus"), "Add books")), h("div.mt8", items),
          !v.items.length ? h("p.small.faint.mt8", "Pick books from the catalog. Add a note to each one if you like.") : null] : null,
        h("div.help.bad.mt12", this.err || ""),
        h("p.xs.faint.mt12", "Public to every Margin reader. Your followers see it in their feed. Write it yourself: no AI text."),
        h("div.bottom-space"))));
  }
  pickBooks() {
    const v = this.v, sel = new Set(v.items.map(i => i.bookId));
    const grid = () => h("div.grid3.mt8", Catalog.data.books.map(b => h("button.col.gap4", { style: { textAlign: "left" }, onclick: e => {
      if (sel.has(b.id)) { sel.delete(b.id); v.items = v.items.filter(i => i.bookId !== b.id); } else if (v.items.length >= 20) return toast("20 books at most."); else { sel.add(b.id); v.items.push({ bookId: b.id, note: "" }); }
      Sheet.cur.sheet.querySelector(".sbody").replaceChildren(grid()); } },
      cover(b, 110, { style: { width: "100%", outline: sel.has(b.id) ? "3px solid var(--accent)" : "none", outlineOffset: "2px" } }), h("div.xs.clamp2" + (sel.has(b.id) ? ".strong" : ".muted"), b.title))));
    Sheet.open("Add books", grid(), { right: h("button.textbtn", { style: { color: "var(--link)", fontWeight: 600, height: "44px", padding: "0 10px" }, onclick: () => Sheet.close() }, "Done"), onClose: () => Nav.refresh() });
  }
  async publish() {
    const v = this.v, title = v.title.trim(), body = v.body.trim();
    this.err = !title ? "Give it a title." : v.kind === "text" && body.split(/\s+/).filter(Boolean).length < 5 ? "Write a few more words." : v.kind === "video" && !youtubeId(v.video) ? "Paste a YouTube link (youtube.com or youtu.be)." : v.kind === "list" && !v.items.length ? "Add at least one book." : null;
    if (this.err) return Nav.refresh();
    const post = { kind: v.kind, title, body, video_url: v.kind === "video" ? v.video.trim() : null, items: v.kind === "list" ? v.items.map(i => ({ bookId: i.bookId, note: (i.note || "").trim().slice(0, 200) })) : [] };
    this.busy = true; Nav.refresh();
    try { const p = await Social.publish(post); toast("Published"); Nav.replace(new PostScreen(p.id)); }
    catch (e) { this.busy = false; this.err = /row-level security/i.test(e.message || "") ? "Turn on Creator account first (Settings › Profile)." : "Couldn't publish. Check your connection."; Nav.refresh(); }
  }
}
