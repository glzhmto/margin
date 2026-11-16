/* Buying the book on paper (real Gandhi prices through the book-prices edge function) and the FIL 2026 banner. */
"use strict";

// Spanish titles, used to search Mexican stores.
const ES_TITLE = {
  "fahrenheit-451": "Fahrenheit 451", "the-metamorphosis": "La metamorfosis", "animal-farm": "Rebelión en la granja",
  "the-little-prince": "El principito", "chronicle-of-a-death-foretold": "Crónica de una muerte anunciada", "aura": "Aura",
  "lazarillo-de-tormes": "Lazarillo de Tormes", "hamlet": "Hamlet", "the-house-of-bernarda-alba": "La casa de Bernarda Alba",
  "1984": "1984", "jekyll-and-hyde": "El extraño caso del Dr. Jekyll y Mr. Hyde", "frankenstein": "Frankenstein",
  "the-stranger": "El extranjero", "the-underdogs": "Los de abajo", "the-labyrinth-of-solitude": "El laberinto de la soledad",
  "the-prince": "El príncipe",
};

const Shop = {
  cache: {},
  async prices(bookId) {
    if (this.cache[bookId]) return this.cache[bookId];
    const r = await fetch(`${SB_URL}/functions/v1/book-prices?book=${encodeURIComponent(bookId)}`, { headers: { apikey: SB_KEY } });
    if (!r.ok) throw new Error("prices " + r.status);
    return (this.cache[bookId] = await r.json());
  },
  money(n) { return "$" + Number(n).toLocaleString("en-US", { minimumFractionDigits: n % 1 ? 2 : 0, maximumFractionDigits: 2 }) + " MXN"; },
  stores(b) {
    const q = `${ES_TITLE[b.id] || b.title} ${b.author}`, e = encodeURIComponent(q);
    return [
      ["Amazon México", "#232F3E", `https://www.amazon.com.mx/s?k=${e}&i=stripbooks`],
      ["Mercado Libre", "#2D3277", `https://listado.mercadolibre.com.mx/${encodeURIComponent(q.toLowerCase().replace(/\s+/g, "-"))}`],
      ["Sanborns", "#7A1F2B", `https://www.sanborns.com.mx/${e}?_q=${e}&map=ft`],
      ["El Sótano", "#5B3A29", `https://www.elsotano.com/busqueda/listaLibros.php?tipoBus=full&palabrasBusqueda=${e}`],
    ];
  },
  // The "Buy it" block for the Book screen. Fills itself in when the prices arrive.
  section(b) {
    const box = h("div.list", h("div.item", h("div.grow", h("div.small.muted", "Checking prices at Gandhi…"))));
    const fill = data => {
      box.innerHTML = "";
      const offers = (data.offers || []).slice(0, 3);
      if (offers.length) {
        offers.forEach((o, i) => box.append(h("button.item", { onclick: () => window.open(o.url, "_blank", "noopener") },
          h("span.ic", { style: { background: "#E2001A", color: "#fff", fontWeight: 700, fontSize: "13px" } }, "G"),
          h("div.grow", h("div.clamp2", o.title), h("div.xs.faint", i === 0 ? "Lowest price at Gandhi" : "Gandhi")),
          h("span.num", { style: { fontWeight: 600, whiteSpace: "nowrap" } }, this.money(o.price)), icon("chev", "chev"))));
      } else box.append(h("div.item", h("div.grow", h("div.small.muted", "Gandhi has no printed copies in stock right now."))));
      this.stores(b).forEach(([name, color, url]) => box.append(h("button.item", { onclick: () => window.open(url, "_blank", "noopener") },
        h("span.ic", { style: { background: color, color: "#fff", fontWeight: 700, fontSize: "13px" } }, name[0]),
        h("div.grow", h("div", name), h("div.xs.faint", "Search this book")), icon("chev", "chev"))));
      const when = data.fetchedAt ? new Date(data.fetchedAt) : null;
      box.append(h("p.groupfoot", when ? `Real prices from gandhi.com.mx, updated ${Fmt.Months ? Fmt.Months[when.getMonth()].slice(0, 3) : ""} ${when.getDate()}, ${String(when.getHours()).padStart(2, "0")}:${String(when.getMinutes()).padStart(2, "0")}. Printed books only. Margin earns nothing from these links.` : "Prices from gandhi.com.mx."));
    };
    this.prices(b.id).then(fill).catch(() => fill({ offers: [] }));
    return [sect("Buy it"), box];
  },
};

const Fil = {
  END: new Date(2026, 11, 7), // the fair ends Dec 6, 2026
  KEY: "margin.fil26.hidden",
  // Small card for Home. It can be closed, and it disappears after the fair.
  banner() {
    if (Clock.today() >= this.END || store.get(this.KEY) === "1") return null;
    const close = h("button.iconbtn", { "aria-label": "Hide", style: { position: "absolute", top: "6px", right: "6px", background: "rgba(255,255,255,.85)", borderRadius: "50%", width: "28px", height: "28px" },
      onclick: e => { e.stopPropagation(); store.set(this.KEY, "1"); card.remove(); } }, "×");
    const card = h("div.card", { role: "link", tabindex: "0", style: { margin: "12px 16px 0", padding: 0, overflow: "hidden", position: "relative", cursor: "pointer" },
      onclick: () => window.open("https://www.fil.com.mx", "_blank", "noopener") },
      h("img", { src: "assets/fil/fil-2026.jpg", alt: "FIL Guadalajara 2026, 40th edition. Italy, guest of honor. Nov 28 to Dec 6.", style: { width: "100%", display: "block", aspectRatio: "1080 / 566", objectFit: "cover" } }),
      h("div.row", { style: { padding: "10px 14px", gap: "10px" } },
        h("div.grow", h("div.small", { style: { fontWeight: 600 } }, "FIL Guadalajara · Nov 28 – Dec 6"), h("div.xs.faint", "The largest book fair in Spanish turns 40. Italy is the guest of honor.")),
        h("span.btn.sm.accent", { style: { flex: "none" } }, "Get tickets")), close);
    card.addEventListener("keydown", e => { if (e.key === "Enter") card.click(); });
    return card;
  },
};
