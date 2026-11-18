/* Boot, session, theme and side tools. */
"use strict";

const App = {
  root: null, pendingAdd: null,
  async boot() {
    this.root = document.getElementById("app");
    Nav.mount(this.root);
    // Invite links look like /app/?add=username. Keep the name until the reader is logged in.
    // /app/?guest=1 or ?demo=1 opens the demo account (a guest session with six weeks of reading).
    const params = new URLSearchParams(location.search), add = (params.get("add") || "").toLowerCase().replace(/[^a-z0-9._]/g, "");
    const guest = params.get("guest") === "1" || params.get("demo") === "1";
    if (add || guest) { if (add) { try { sessionStorage.setItem("margin.add", add); } catch {} } params.delete("add"); params.delete("guest"); params.delete("demo"); history.replaceState(null, "", location.pathname + (params.toString() ? "?" + params : "") + location.hash); }
    try { this.pendingAdd = sessionStorage.getItem("margin.add"); } catch { this.pendingAdd = add || null; }
    try { await Data.load(); }
    catch (e) { this.root.append(h("div.empty", { style: { paddingTop: "200px" } }, h("div.h3", "Could not load the catalog"), h("p.small", "Open the preview with a local server from the project root: python3 -m http.server 8765"))); return; }
    Clock.resetSimulation();
    try { await Net.init(); } catch (e) { console.warn("[margin] init", e); }
    this.wireSide();
    this.start();
    if (guest && !Accounts.current()) this.tryGuest(Nav.current());
  },
  async tryGuest(scr) {
    if (this._guestBusy) return;
    this._guestBusy = true;
    if (scr) { scr.busy = true; scr.err = null; Nav.refresh(); }
    let r;
    try { r = await Net.signInGuest("Demo reader"); } catch (e) { r = { error: "Couldn't reach Margin. Check your connection and try again." }; }
    this._guestBusy = false;
    if (scr) scr.busy = false;
    if (r.error) { if (scr && Nav.current() === scr) { scr.err = r.error; Nav.refresh(); } else toast(r.error); return; }
    // The demo account: a guest session that already has six weeks of reading (DemoData in core.js).
    const acc = Accounts.current(), s = acc.state, today = Clock.today();
    s.profile.name = "Demo reader";
    DemoData.seed(s, today);
    DemoData.extras(s, today, acc.username);
    Net.log({ kind: "joined" }); Data.save(); Net.flush();
    this.start();
    toast("You're in a demo account. Everything works, try anything");
  },
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
  },
};

window.addEventListener("DOMContentLoaded", () => App.boot());
