/* Boot, session, theme and side tools. */
"use strict";

const App = {
  root: null, pendingAdd: null,
  async boot() {
    this.root = document.getElementById("app");
    this.status = h("div.statusbar", h("span.num", { id: "clock" }), h("div.island"), h("div.sys", this.sysIcons()));
    this.root.append(this.status, h("div.homebar"));
    Nav.mount(this.root);
    // Invite links look like /app/?add=username. Keep the name until the reader is logged in.
    // /app/?guest=1 (the website's "Try it without an account") opens a guest session.
    const params = new URLSearchParams(location.search), add = (params.get("add") || "").toLowerCase().replace(/[^a-z0-9._]/g, "");
    const guest = params.get("guest") === "1";
    if (add || guest) { if (add) { try { sessionStorage.setItem("margin.add", add); } catch {} } params.delete("add"); params.delete("guest"); history.replaceState(null, "", location.pathname + (params.toString() ? "?" + params : "") + location.hash); }
    try { this.pendingAdd = sessionStorage.getItem("margin.add"); } catch { this.pendingAdd = add || null; }
    try { await Data.load(); }
    catch (e) { this.root.append(h("div.empty", { style: { paddingTop: "200px" } }, h("div.h3", "Could not load the catalog"), h("p.small", "Open the preview with a local server from the project root: python3 -m http.server 8765"))); return; }
    Clock.resetSimulation();
    try { await Net.init(); } catch (e) { console.warn("[margin] init", e); }
    this.tickClock(); setInterval(() => this.tickClock(), 15000);
    this.wireSide();
    this.start();
    if (guest && !Accounts.current()) this.tryGuest(Nav.current());
  },
  async tryGuest(scr) {
    if (this._guestBusy) return;
    this._guestBusy = true;
    if (scr) { scr.busy = true; scr.err = null; Nav.refresh(); }
    let r;
    try { r = await Net.signInGuest(); } catch (e) { r = { error: "Couldn't reach Margin. Check your connection and try again." }; }
    this._guestBusy = false;
    if (scr) scr.busy = false;
    if (r.error) { if (scr && Nav.current() === scr) { scr.err = r.error; Nav.refresh(); } else toast(r.error); return; }
    this.start();
  },
  sysIcons() {
    const s = document.createElementNS("http://www.w3.org/2000/svg", "svg"); s.setAttribute("width", "70"); s.setAttribute("height", "13"); s.setAttribute("viewBox", "0 0 70 13");
    s.innerHTML = '<g fill="currentColor"><rect x="0" y="8" width="3" height="4" rx="1"/><rect x="5" y="6" width="3" height="6" rx="1"/><rect x="10" y="3.5" width="3" height="8.5" rx="1"/><rect x="15" y="1" width="3" height="11" rx="1"/><path d="M30 3.2a8.5 8.5 0 0 1 11 0l-1.3 1.4a6.6 6.6 0 0 0-8.4 0zM32.3 5.8a5 5 0 0 1 6.4 0l-1.4 1.4a3 3 0 0 0-3.6 0zM35.5 8.6l1.4 1.5-1.4 1.4-1.4-1.4z"/><rect x="46" y="1" width="21" height="11" rx="3.2" fill="none" stroke="currentColor" opacity=".45"/><rect x="48" y="3" width="15" height="7" rx="1.6"/><rect x="68" y="4.5" width="1.6" height="4" rx=".8" opacity=".45"/></g>';
    return s;
  },
  tickClock() { const n = new Date(); const el = this.status.querySelector("#clock"); if (el) el.textContent = `${n.getHours()}:${String(n.getMinutes()).padStart(2, "0")}`; },
  updateStatusBar(screenEl) { this.status.classList.toggle("on-dark", !!screenEl.querySelector(".hero")); },
  start() {
    Sheet.close(true);
    const acc = Accounts.current();
    this.applySettings();
    if (!acc) return Nav.reset(new WelcomeScreen(), null);
    if (!acc.state.profile.onboardingComplete) return Nav.reset(new SetupScreen(acc));
    this.goHome();
    const add = this.pendingAdd;
    if (add) { this.pendingAdd = null; try { sessionStorage.removeItem("margin.add"); } catch {} if (add !== acc.username) Nav.push(new UserScreen(add)); }
  },
  goHome() { Nav.reset(new HomeScreen(), "home"); },
  applySettings() {
    const st = Accounts.current()?.settings || defaultSettings();
    const dark = st.theme === "Dark" || (st.theme === "System" && matchMedia("(prefers-color-scheme: dark)").matches);
    document.documentElement.dataset.theme = dark ? "dark" : "light";
    this.root.classList.toggle("text-large", !!st.textLarge);
    this.root.classList.toggle("reduce-motion", !!st.reduceMotion);
  },
  demoBanner() {
    const s = Data.state(); if (!s) return toast("Log in first.");
    const b = Data.book(s.profile.currentBookId), g = s.profile.goals, q = Data.quoteOfDay(Clock.today());
    banner(g.intention ? `${Copy.whenI(g.intention)}…` : "Your book is waiting", b ? `${F.pages(g.dailyPages)} of ${b.title}. “${q.text.slice(0, 70)}${q.text.length > 70 ? "…" : ""}”` : q.text);
  },
  wireSide() {
    const on = (id, f) => document.getElementById(id)?.addEventListener("click", f);
    on("t-day", () => { Clock.advanceDay(); toast(`It is now ${Fmt.dateLong(Clock.today())}`); Nav.refresh(); });
    on("t-notif", () => this.demoBanner());
    on("t-theme", () => { const a = Accounts.current(); if (!a) return; a.settings.theme = document.documentElement.dataset.theme === "dark" ? "Light" : "Dark"; Data.save(); this.applySettings(); Nav.refresh(); });
    on("t-clock", () => { Clock.resetSimulation(); toast("Back to the real date"); Nav.refresh(); });
  },
};

window.addEventListener("DOMContentLoaded", () => App.boot());
