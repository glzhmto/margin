/* Supabase backend: real accounts, the private reading state, and the social layer
   (friends, shared pacts, creators, follows, activity, reviews). Nothing here is simulated. */
"use strict";

const SB_URL = "https://jrstqzmsyscnhxjbqtrm.supabase.co";
const SB_KEY = "sb_publishable_4vUUUoG4se5Da4PxVQBNMQ_XboKzRSK"; // publishable key: safe in the browser, RLS protects every table

let _keepalive = false; // set while the page is closing so the last save still reaches the server
const sb = supabase.createClient(SB_URL, SB_KEY, {
  auth: { persistSession: true, autoRefreshToken: true, detectSessionInUrl: true, storageKey: "margin5.auth" },
  global: { fetch: (u, o = {}) => fetch(u, _keepalive && !(typeof o.body === "string" && o.body.length > 60000) ? { ...o, keepalive: true } : o) },
});

// What a post or review needs to show its author.
const AUTHOR_COLS = "username,name,color,avatar";
const PROFILE_COLS = "id,username,name,bio,university,color,is_creator,current_book_id,current_page,momentum_level,pages_this_week,books_read,favorites,created_at,avatar";
const pname = p => (p && (p.name || "").trim()) || (p ? "@" + p.username : "Reader");
const pfirst = p => pname(p).replace(/^@/, "").split(" ")[0];
const pav = p => ({ name: (p && p.name) || (p && p.username) || "?", color: p && p.color, avatar: p && p.avatar });
function cleanQuery(q) { return (q || "").replace(/[%,()*\\:"']/g, " ").trim().slice(0, 40); }
function netFail(e) { console.warn("[margin]", e && (e.message || e)); toast("Couldn't reach Margin. Check your connection and try again."); }
function authMessage(e) {
  const m = (e && e.message) || "";
  if (/already registered|already exists/i.test(m)) return "There is already an account with that email. Log in instead.";
  if (/invalid login credentials/i.test(m)) return "Wrong email or password. Check capital letters and try again.";
  if (/email not confirmed/i.test(m)) return "Confirm your email first: open the link we sent you, then log in.";
  if (/rate limit|too many/i.test(m)) return "Too many attempts for now. Wait a few minutes and try again.";
  if (/password/i.test(m)) return m;
  return m || "Something went wrong. Try again.";
}

const Net = {
  acc: null, uid: null, _timer: null, _dirty: false, _seen: null, _signingOut: false,

  async init() {
    sb.auth.onAuthStateChange((ev) => {
      if (ev === "PASSWORD_RECOVERY") setTimeout(() => Nav.push(new NewPasswordScreen()), 0);
      if (ev === "SIGNED_OUT" && this.acc && !this._signingOut) setTimeout(() => { this.clear(); App.start(); }, 0);
    });
    const { data } = await sb.auth.getSession();
    if (data.session) { try { await this.loadAccount(data.session.user); } catch (e) { console.warn("[margin] session", e); } }
    addEventListener("pagehide", () => this.flushNow());
    document.addEventListener("visibilitychange", () => { if (document.visibilityState === "hidden") this.flushNow(); });
  },

  /* ---- Auth ---- */
  async usernameAvailable(u) {
    if (this.uid) { const { data, error } = await sb.from("profiles").select("id").eq("username", u).neq("id", this.uid).limit(1); return error ? null : !data.length; }
    const { data, error } = await sb.rpc("username_available", { u });
    return error ? null : data;
  },
  async signUp(v) {
    const { data, error } = await sb.auth.signUp({ email: v.email.trim(), password: v.password, options: {
      emailRedirectTo: location.origin + location.pathname,
      data: { username: v.username, name: v.name.trim(), university: (v.university || "").trim(), color: COVER_TINTS[hash(v.username) % COVER_TINTS.length] } } });
    if (error) return { error: authMessage(error) };
    if (!data.session) return { confirm: true };
    await this.loadAccount(data.user);
    return { ok: true };
  },
  async signIn(email, password) {
    const { data, error } = await sb.auth.signInWithPassword({ email: email.trim(), password });
    if (error) return { error: authMessage(error) };
    await this.loadAccount(data.user);
    return { ok: true };
  },
  // Guest mode: an anonymous Supabase user with a real profile (username guestXXXXXX, made by the DB trigger).
  async signInGuest(name = "Guest") {
    const { data, error } = await sb.auth.signInAnonymously({ options: { data: { name } } });
    if (error) {
      console.warn("[margin] guest", error.message);
      if (/anonymous/i.test(error.message || "")) return { error: "Guest mode is switched off right now. Sign up with email instead, it takes a minute.", disabled: true };
      return { error: authMessage(error) };
    }
    const { error: e2 } = await sb.from("profiles").update({ is_guest: true }).eq("id", data.user.id);
    if (e2) console.warn("[margin] guest flag", e2.message);
    await this.loadAccount(data.user);
    return { ok: true };
  },
  // Turns a guest into a normal account: email + password on the same auth user, then the public profile.
  async saveGuest(v) {
    const email = v.email.trim(), username = v.username, name = v.name.trim();
    const free = await this.usernameAvailable(username);
    if (free === false) return `@${username} is taken. Try another one.`;
    if (free === null) return "Couldn't check the name right now. Try again.";
    let pending = false;
    let { data: d1, error } = await sb.auth.updateUser({ email, password: v.password }, { emailRedirectTo: location.origin + location.pathname });
    if (error && !/already registered|already exists|rate limit|invalid/i.test(error.message || "")) {
      // Some projects only accept a password once the email is confirmed: link the email first.
      const r1 = await sb.auth.updateUser({ email }, { emailRedirectTo: location.origin + location.pathname });
      error = r1.error; d1 = r1.data;
      if (!error) { const r2 = await sb.auth.updateUser({ password: v.password }); if (r2.error) pending = true; }
    }
    if (error) return authMessage(error);
    const u = (d1 && d1.user) || {};
    if (!u.email && u.new_email) pending = true; // the email still needs the confirmation link
    const { error: e2 } = await sb.from("profiles").update({ username, name, is_guest: pending, updated_at: new Date().toISOString() }).eq("id", this.uid);
    if (e2) return /duplicate|unique/i.test(e2.message) ? `@${username} is taken. Try another one.` : "Your email is saved, but the profile didn't update. Try again.";
    Object.assign(this.acc, { username, name, isGuest: pending, pendingEmail: pending ? email : null, email: u.email || this.acc.email || "" });
    this.acc.state.profile.name = name.split(" ")[0];
    this.save();
    return pending ? { pending: email } : null;
  },
  async signOut() {
    this._signingOut = true;
    try { await this.flush(); Social.stop(); await sb.auth.signOut(); } catch (e) { console.warn(e); }
    this.clear(); this._signingOut = false;
  },
  async resetPassword(email) {
    const { error } = await sb.auth.resetPasswordForEmail(email.trim(), { redirectTo: location.origin + location.pathname });
    return error ? authMessage(error) : null;
  },
  async checkPassword(pw) { const { error } = await sb.auth.signInWithPassword({ email: this.acc.email, password: pw }); return !error; },
  async setPassword(pw) { const { error } = await sb.auth.updateUser({ password: pw }); return error ? authMessage(error) : null; },
  async setEmail(email) { const { error } = await sb.auth.updateUser({ email: email.trim() }, { emailRedirectTo: location.origin + location.pathname }); return error ? authMessage(error) : null; },
  clear() { this.acc = null; this.uid = null; this._seen = null; this._dirty = false; clearTimeout(this._timer); Social.reset(); if (typeof ExploreScreen !== "undefined") ExploreScreen.last = null; },

  async loadAccount(user) {
    const [p, rs] = await Promise.all([
      sb.from("profiles").select(PROFILE_COLS + ",is_guest").eq("id", user.id).single(),
      sb.from("reading_states").select("state,settings").eq("user_id", user.id).maybeSingle(),
    ]);
    if (p.error) throw p.error;
    const pr = p.data;
    if (pr.is_guest && user.email && !user.is_anonymous) { pr.is_guest = false; sb.from("profiles").update({ is_guest: false }).eq("id", user.id).then(() => {}); }
    const state = normalizeState(rs.data && rs.data.state, pr.name);
    const settings = { ...defaultSettings(), ...((rs.data && rs.data.settings) || {}) };
    this.uid = user.id;
    this.acc = { id: user.id, username: pr.username, name: pr.name, email: user.email || "", bio: pr.bio || "", color: pr.color, avatar: pr.avatar || null, university: pr.university || "",
      isCreator: !!pr.is_creator, isGuest: !!(pr.is_guest || (user.is_anonymous && !user.email && !user.new_email)), created: (pr.created_at || "").slice(0, 10), state, settings };
    this._seen = this.snapshot(state);
    Social.start();
  },

  /* ---- Private reading state: debounced save to reading_states + public summary in profiles ---- */
  snapshot(s) {
    return { logs: s.logs.length, cur: s.profile.currentBookId, fin: new Set(s.progress.filter(p => p.finished).map(p => p.bookId)),
      quiz: new Set(s.quizResults.map(q => `${q.bookId}|${q.chapterNumber}|${q.date}|${q.correct}`)) };
  },
  save() {
    if (!this.acc) return;
    this.diffActivity();
    this._dirty = true; clearTimeout(this._timer); this._timer = setTimeout(() => this.flush(), 1000);
  },
  // Turns what changed in the state into activity rows (started, pages, finished, quiz) for friends and followers.
  diffActivity() {
    const s = this.acc.state, prev = this._seen || this.snapshot(s), now = this.snapshot(s), rows = [];
    if (now.cur && now.cur !== prev.cur && !now.fin.has(now.cur)) rows.push({ kind: "started", book_id: now.cur });
    if (s.logs.length > prev.logs) for (const l of s.logs.slice(prev.logs)) rows.push({ kind: "pages", book_id: l.bookId, data: { from: l.fromPage, to: l.toPage } });
    for (const id of now.fin) if (!prev.fin.has(id)) rows.push({ kind: "finished", book_id: id });
    for (const q of s.quizResults) if (!prev.quiz.has(`${q.bookId}|${q.chapterNumber}|${q.date}|${q.correct}`)) rows.push({ kind: "quiz", book_id: q.bookId, data: { chapter: q.chapterNumber, correct: q.correct, total: q.total } });
    this._seen = now;
    if (rows.length) this.log(rows);
  },
  log(rows) {
    if (!this.acc || this.acc.settings.activityVisibility === "Only me") return Promise.resolve();
    rows = (Array.isArray(rows) ? rows : [rows]).map(r => ({ user_id: this.uid, data: {}, book_id: null, ...r }));
    return sb.from("activity").insert(rows).then(({ error }) => { if (error) console.warn("[margin] activity", error.message); });
  },
  summary() {
    const a = this.acc, s = a.state, today = Clock.today();
    const m = HabitEngine.compute(s, today), w = HabitEngine.weekly(s, today);
    const cur = a.settings.showCurrent === false ? null : s.profile.currentBookId;
    const cp = cur ? s.progress.find(p => p.bookId === cur) : null;
    return { name: a.name, bio: a.bio || "", current_book_id: cur || null, current_page: cur ? (cp ? cp.currentPage : 0) : null,
      momentum_level: s.profile.onboardingComplete ? Copy.levelName(m.level) : null, pages_this_week: w.pagesRead,
      books_read: Data.finished(s).map(p => p.bookId), favorites: s.favorites || [], updated_at: new Date().toISOString() };
  },
  async flush() {
    clearTimeout(this._timer);
    if (!this.acc || !this._dirty) return;
    this._dirty = false;
    const a = this.acc;
    const [r1, r2] = await Promise.all([
      sb.from("reading_states").upsert({ user_id: this.uid, state: a.state, settings: a.settings, updated_at: new Date().toISOString() }),
      sb.from("profiles").update(this.summary()).eq("id", this.uid),
    ]);
    const err = r1.error || r2.error;
    if (err) { console.warn("[margin] save", err.message); if (this.acc === a) { this._dirty = true; this._timer = setTimeout(() => this.flush(), 5000); } }
  },
  flushNow() { if (this._dirty) { _keepalive = true; this.flush().finally(() => { _keepalive = false; }); } },

  async updateProfile(fields) {
    const { error } = await sb.from("profiles").update({ ...fields, updated_at: new Date().toISOString() }).eq("id", this.uid);
    if (error) { netFail(error); return error.message; }
    const map = { name: "name", bio: "bio", username: "username", university: "university", is_creator: "isCreator", color: "color", avatar: "avatar" };
    for (const k in fields) if (map[k]) this.acc[map[k]] = fields[k];
    return null;
  },
  async changeUsername(u) {
    const free = await this.usernameAvailable(u);
    if (free === false) return `@${u} is taken. Try another one.`;
    if (free === null) return "Couldn't check the name right now. Try again.";
    return this.updateProfile({ username: u });
  },

  // Deletes everything the client is allowed to delete. The login itself (auth user) can only be removed by the team.
  async deleteAccount() {
    // Deletes the auth user on the server; profile, diary, friends, follows, activity, posts and reviews go with it.
    await this.flush();
    const { error } = await sb.rpc("delete_my_account");
    if (error) { netFail(error); return false; }
    this._dirty = false;
    this._signingOut = true;
    try { Social.stop(); await sb.auth.signOut({ scope: "local" }); } catch (e) { console.warn(e); }
    this.clear(); this._signingOut = false;
    return true;
  },
};

/* ---------------- Social: friends, follows, feed (cached, refreshed live) ---------------- */
const Social = {
  profiles: new Map(), friends: [], incoming: [], outgoing: [], followingIds: new Set(), feed: [], followers: 0, myPosts: [],
  loaded: false, channel: null, _t: null, _announce: null,

  start() { this.refresh(); this.subscribe(); },
  stop() { if (this.channel) { sb.removeChannel(this.channel); this.channel = null; } },
  reset() { this.stop(); Object.assign(this, { profiles: new Map(), friends: [], incoming: [], outgoing: [], followingIds: new Set(), feed: [], followers: 0, myPosts: [], loaded: false }); },
  other(r) { return r.requester === Net.uid ? r.addressee : r.requester; },

  async refresh() {
    const me = Net.uid; if (!me) return;
    try {
      const [fr, fo, fc, act, mp] = await Promise.all([
        sb.from("friendships").select("*").or(`requester.eq.${me},addressee.eq.${me}`),
        sb.from("follows").select("followee").eq("follower", me),
        sb.from("follows").select("follower", { count: "exact", head: true }).eq("followee", me),
        sb.from("activity").select("*").neq("user_id", me).order("created_at", { ascending: false }).limit(60),
        Net.acc.isCreator ? sb.from("posts").select("*").eq("author", me).order("created_at", { ascending: false }).limit(30) : Promise.resolve({ data: [] }),
      ]);
      if (Net.uid !== me) return;
      const err = fr.error || fo.error || act.error; if (err) throw err;
      const rows = fr.data, follows = fo.data.map(x => x.followee);
      const ids = [...new Set([...rows.map(r => this.other(r)), ...follows, ...act.data.map(a => a.user_id)])];
      if (ids.length) { const { data, error } = await sb.from("profiles").select(PROFILE_COLS).in("id", ids); if (error) throw error; data.forEach(p => this.profiles.set(p.id, p)); }
      const withP = r => ({ f: r, p: this.profiles.get(this.other(r)) });
      this.friends = rows.filter(r => r.status === "accepted").map(withP).filter(x => x.p).sort((a, b) => pname(a.p).localeCompare(pname(b.p)));
      this.incoming = rows.filter(r => r.status === "pending" && r.addressee === me).map(withP).filter(x => x.p);
      this.outgoing = rows.filter(r => r.status === "pending" && r.requester === me).map(withP).filter(x => x.p);
      this.followingIds = new Set(follows);
      this.followers = fc.count || 0;
      this.myPosts = mp.data || [];
      this.feed = act.data.filter(a => this.profiles.has(a.user_id));
      this.loaded = true;
      if (this._announce) { const x = this.incoming.find(i => i.f.requester === this._announce); if (x) toast(`${pname(x.p)} wants to be your friend`); this._announce = null; }
      this.changed();
    } catch (e) { console.warn("[margin] social", e.message || e); }
  },
  changed() { const c = Nav.current(); if (c && c.live && !Sheet.cur) { c._loaded = false; Nav.refresh(); } },
  soon() { clearTimeout(this._t); this._t = setTimeout(() => this.refresh(), 350); },
  subscribe() {
    this.stop();
    const me = Net.uid;
    this.channel = sb.channel("margin-live-" + me)
      .on("postgres_changes", { event: "*", schema: "public", table: "friendships" }, p => {
        if (p.eventType === "INSERT" && p.new.addressee === me) this._announce = p.new.requester;
        if (p.eventType === "UPDATE" && p.new.requester === me && p.new.status === "accepted" && p.old && p.old.status === "pending") setTimeout(() => { const pr = this.profiles.get(p.new.addressee); if (pr) toast(`${pname(pr)} accepted your request`); }, 800);
        this.soon(); })
      .on("postgres_changes", { event: "INSERT", schema: "public", table: "activity" }, p => { if (p.new.user_id !== me) this.soon(); })
      .on("postgres_changes", { event: "INSERT", schema: "public", table: "posts" }, p => { if (p.new.author !== me) this.soon(); })
      .subscribe();
  },

  /* ---- Relationship with another reader ---- */
  rel(uid) {
    if (uid === Net.uid) return { kind: "self" };
    const pick = (list, kind) => { const x = list.find(i => i.p.id === uid); return x ? { kind, row: x.f } : null; };
    return pick(this.friends, "friend") || pick(this.incoming, "incoming") || pick(this.outgoing, "outgoing") || { kind: "none" };
  },
  async request(uid) {
    const me = Net.uid;
    const ex = await sb.from("friendships").select("*").or(`and(requester.eq.${me},addressee.eq.${uid}),and(requester.eq.${uid},addressee.eq.${me})`);
    if (ex.error) return netFail(ex.error);
    if (!ex.data.length) { const { error } = await sb.from("friendships").insert({ requester: me, addressee: uid, status: "pending" }); if (error) return netFail(error); }
    await this.refresh();
  },
  async accept(row) { const { error } = await sb.from("friendships").update({ status: "accepted" }).eq("id", row.id); if (error) return netFail(error); await this.refresh(); },
  async remove(row) { const { error } = await sb.from("friendships").delete().eq("id", row.id); if (error) return netFail(error); await this.refresh(); },
  async setPact(row, bookId) { const { error } = await sb.from("friendships").update({ shared_book_id: bookId }).eq("id", row.id); if (error) return netFail(error); await this.refresh(); },
  async follow(uid) { const { error } = await sb.from("follows").insert({ follower: Net.uid, followee: uid }); if (error && error.code !== "23505") return netFail(error); await this.refresh(); },
  async unfollow(uid) { const { error } = await sb.from("follows").delete().eq("follower", Net.uid).eq("followee", uid); if (error) return netFail(error); await this.refresh(); },
  pacts() { return this.friends.filter(x => x.f.shared_book_id && Data.book(x.f.shared_book_id)); },

  /* ---- Queries ---- */
  async byUsername(u) { const { data, error } = await sb.from("profiles").select(PROFILE_COLS).eq("username", (u || "").toLowerCase()).maybeSingle(); if (error) throw error; if (data) this.profiles.set(data.id, data); return data; },
  async byId(id) { if (this.profiles.has(id)) return this.profiles.get(id); const { data, error } = await sb.from("profiles").select(PROFILE_COLS).eq("id", id).maybeSingle(); if (error) throw error; if (data) this.profiles.set(id, data); return data; },
  async search(q) {
    q = cleanQuery(q).replace(/^@/, ""); if (q.length < 2) return [];
    const { data, error } = await sb.from("profiles").select(PROFILE_COLS).or(`username.ilike.%${q}%,name.ilike.%${q}%`).neq("id", Net.uid).not("username", "like", "deleted_*").eq("is_guest", false).limit(20);
    if (error) throw error; return data;
  },
  async sameUniversity() {
    const uni = cleanQuery(Net.acc.university); if (!uni) return [];
    const { data, error } = await sb.from("profiles").select(PROFILE_COLS).ilike("university", uni).neq("id", Net.uid).eq("is_guest", false).limit(20);
    if (error) throw error; return data.filter(p => this.rel(p.id).kind === "none");
  },
  async activityOf(uid, n = 10) { const { data, error } = await sb.from("activity").select("*").eq("user_id", uid).order("created_at", { ascending: false }).limit(n); if (error) throw error; return data; },
  async readerCount() { const { count, error } = await sb.from("profiles").select("id", { count: "exact", head: true }).not("username", "like", "deleted_*"); if (error) throw error; return count || 0; },
  async creators() { const { data, error } = await sb.from("profiles").select(PROFILE_COLS).eq("is_creator", true).order("updated_at", { ascending: false }).limit(30); if (error) throw error; return data; },
  async posts({ author, limit = 12, bookId } = {}) {
    let q = sb.from("posts").select(`*, profiles(${AUTHOR_COLS})`).order("created_at", { ascending: false }).limit(limit);
    if (author) q = q.eq("author", author);
    if (bookId) q = q.eq("kind", "list").contains("items", JSON.stringify([{ bookId }]));
    const { data, error } = await q; if (error) throw error; return data;
  },
  async post(id) { const { data, error } = await sb.from("posts").select(`*, profiles(${AUTHOR_COLS})`).eq("id", id).maybeSingle(); if (error) throw error; return data; },
  async publish(post) {
    const { data, error } = await sb.from("posts").insert({ author: Net.uid, ...post }).select().single();
    if (error) throw error;
    await Net.log({ kind: "post", book_id: post.kind === "list" && post.items[0] ? post.items[0].bookId : null, data: { post_id: data.id, title: data.title, kind: data.kind } });
    this.refresh();
    return data;
  },
  async deletePost(id) { const { error } = await sb.from("posts").delete().eq("id", id); if (error) throw error; await sb.from("activity").delete().eq("user_id", Net.uid).eq("kind", "post").contains("data", { post_id: id }); this.refresh(); },
  // How many readers have each book as their current book (real counts from profiles).
  async bookCounts() {
    const { data, error } = await sb.from("profiles").select("current_book_id").not("current_book_id", "is", null).limit(2000);
    if (error) throw error; const c = {}; data.forEach(r => { c[r.current_book_id] = (c[r.current_book_id] || 0) + 1; }); return c;
  },
  async bookReviews(bookId) { const { data, error } = await sb.from("reviews").select(`*, profiles(${AUTHOR_COLS})`).eq("book_id", bookId).order("created_at", { ascending: false }).limit(200); if (error) throw error; return data; },
  async saveReview(bookId, rating, body) {
    const { error } = await sb.from("reviews").upsert({ user_id: Net.uid, book_id: bookId, rating, body }, { onConflict: "user_id,book_id" });
    if (error) throw error;
    await Net.log({ kind: "review", book_id: bookId, data: { rating } });
  },
  async deleteReview(bookId) { const { error } = await sb.from("reviews").delete().eq("user_id", Net.uid).eq("book_id", bookId); if (error) throw error; },
};

/* Async data for a screen: loads once, then re-renders the screen if it is still on top. */
/* Loads a screen's data once (scr.d). When it arrives the screen is redrawn, or onDone() runs instead
   (Explore redraws only its results so the search field keeps focus). */
function screenLoad(scr, fn, onDone) {
  if (scr._loaded || scr._loading) return;
  scr._loading = true;
  fn().then(d => { scr.d = d; scr.err = null; }).catch(e => { console.warn("[margin]", e && (e.message || e)); scr.err = "Couldn't load this. Check your connection."; })
    .finally(() => { scr._loading = false; scr._loaded = true; if (Nav.current() === scr) onDone ? onDone() : Nav.refresh(); });
}
