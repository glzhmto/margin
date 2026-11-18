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
  "the-odyssey": "La Odisea", "the-theban-plays": "Tragedias", "meditations": "Meditaciones",
  "the-last-days-of-socrates": "Apología de Sócrates", "the-symposium": "El banquete", "confessions": "Confesiones",
  "siddhartha": "Siddhartha", "poems-protest-and-a-dream": "Respuesta a Sor Filotea", "pride-and-prejudice": "Orgullo y prejuicio",
  "wuthering-heights": "Cumbres borrascosas", "the-picture-of-dorian-gray": "El retrato de Dorian Gray", "dracula": "Drácula",
  "heart-of-darkness": "El corazón de las tinieblas", "candide": "Cándido", "the-great-gatsby": "El gran Gatsby",
  "things-fall-apart": "Todo se desmorona", "fever-dream": "Distancia de rescate", "hurricane-season": "Temporada de huracanes",
  "things-we-lost-in-the-fire": "Las cosas que perdimos en el fuego", "never-let-me-go": "Nunca me abandones", "klara-and-the-sun": "Klara y el Sol",
  "the-road": "La carretera", "normal-people": "Gente normal", "convenience-store-woman": "La dependienta",
  "pedro-paramo": "Pedro Páramo", "like-water-for-chocolate": "Como agua para chocolate", "the-burning-plain": "El llano en llamas",
  "recollections-of-things-to-come": "Los recuerdos del porvenir", "balun-canan": "Balún Canán", "the-death-of-artemio-cruz": "La muerte de Artemio Cruz",
  "signs-preceding-the-end-of-the-world": "Señales que precederán al fin del mundo", "faces-in-the-crowd": "Los ingrávidos", "one-hundred-years-of-solitude": "Cien años de soledad",
  "ficciones": "Ficciones", "the-house-of-the-spirits": "La casa de los espíritus", "the-tunnel": "El túnel",
  "love-in-the-time-of-cholera": "El amor en los tiempos del cólera", "no-one-writes-to-the-colonel": "El coronel no tiene quien le escriba", "the-invention-of-morel": "La invención de Morel",
  "brave-new-world": "Un mundo feliz", "the-handmaids-tale": "El cuento de la criada", "lord-of-the-flies": "El señor de las moscas",
  "we": "Nosotros", "of-mice-and-men": "De ratones y hombres", "to-kill-a-mockingbird": "Matar a un ruiseñor",
  "crime-and-punishment": "Crimen y castigo", "the-catcher-in-the-rye": "El guardián entre el centeno", "the-old-man-and-the-sea": "El viejo y el mar",
  "slaughterhouse-five": "Matadero cinco", "macbeth": "Macbeth", "romeo-and-juliet": "Romeo y Julieta",
  "a-dolls-house": "Casa de muñecas", "waiting-for-godot": "Esperando a Godot", "death-of-a-salesman": "La muerte de un viajante",
  "life-is-a-dream": "La vida es sueño", "blood-wedding": "Bodas de sangre", "nada": "Nada",
  "platero-and-i": "Platero y yo", "inferno": "Infierno", "invisible-cities": "Las ciudades invisibles",
  "the-leopard": "El gatopardo", "a-room-of-ones-own": "Una habitación propia", "mans-search-for-meaning": "El hombre en busca de sentido",
  "letters-to-a-young-poet": "Cartas a un joven poeta", "the-myth-of-sisyphus": "El mito de Sísifo", "the-iliad": "La Ilíada",
  "medea": "Medea", "on-the-shortness-of-life": "Sobre la brevedad de la vida", "jane-eyre": "Jane Eyre",
  "the-turn-of-the-screw": "Otra vuelta de tuerca", "rebecca": "Rebeca", "kafka-on-the-shore": "Kafka en la orilla",
  "the-vegetarian": "La vegetariana", "piranesi": "Piranesi",
  "the-count-of-monte-cristo": "El conde de Montecristo", "anna-karenina": "Ana Karenina", "the-brothers-karamazov": "Los hermanos Karamázov",
  "notes-from-underground": "Memorias del subsuelo", "the-death-of-ivan-ilyich": "La muerte de Iván Ilich", "madame-bovary": "Madame Bovary",
  "great-expectations": "Grandes esperanzas", "a-tale-of-two-cities": "Historia de dos ciudades", "a-christmas-carol": "Cuento de Navidad",
  "emma": "Emma", "persuasion": "Persuasión", "middlemarch": "Middlemarch",
  "tess-of-the-durbervilles": "Tess de los d'Urberville", "the-scarlet-letter": "La letra escarlata", "moby-dick": "Moby Dick",
  "the-adventures-of-huckleberry-finn": "Las aventuras de Huckleberry Finn", "little-women": "Mujercitas", "the-awakening": "El despertar",
  "the-call-of-the-wild": "La llamada de lo salvaje", "the-time-machine": "La máquina del tiempo", "the-war-of-the-worlds": "La guerra de los mundos",
  "treasure-island": "La isla del tesoro", "robinson-crusoe": "Robinson Crusoe", "gullivers-travels": "Los viajes de Gulliver",
  "don-quixote": "Don Quijote de la Mancha", "the-betrothed": "Los novios", "the-aeneid": "La Eneida",
  "the-oresteia": "La Orestíada", "the-republic": "La República", "the-art-of-war": "El arte de la guerra",
  "the-communist-manifesto": "El manifiesto comunista", "the-epic-of-gilgamesh": "La epopeya de Gilgamesh", "beowulf": "Beowulf",
  "the-canterbury-tales": "Los cuentos de Canterbury", "the-trial": "El proceso", "fathers-and-sons": "Padres e hijos",
  "the-age-of-innocence": "La edad de la inocencia", "mrs-dalloway": "La señora Dalloway", "the-sorrows-of-young-werther": "Las desventuras del joven Werther",
  "dead-souls": "Almas muertas", "the-importance-of-being-earnest": "La importancia de llamarse Ernesto", "king-lear": "El rey Lear",
  "walden": "Walden", "narrative-of-the-life-of-frederick-douglass": "Narración de la vida de Frederick Douglass", "the-hound-of-the-baskervilles": "El sabueso de los Baskerville",
  "the-fall-of-the-house-of-usher": "La caída de la casa Usher", "kokoro": "Kokoro", "rashomon-and-seventeen-other-stories": "Rashomon y otros cuentos",
  "the-master-and-margarita": "El maestro y Margarita", "the-conquest-of-new-spain": "Historia verdadera de la conquista de la Nueva España",
  "where-the-crawdads-sing": "La chica salvaje", "the-seven-husbands-of-evelyn-hugo": "Los siete maridos de Evelyn Hugo", "the-midnight-library": "La biblioteca de la medianoche",
  "lessons-in-chemistry": "Lecciones de química", "tomorrow-and-tomorrow-and-tomorrow": "Mañana, y mañana, y mañana", "circe": "Circe",
  "hamnet": "Hamnet", "the-vanishing-half": "La mitad evanescente", "little-fires-everywhere": "Pequeños fuegos por todas partes",
  "anxious-people": "Gente ansiosa", "eleanor-oliphant-is-completely-fine": "Eleanor Oliphant está perfectamente", "demon-copperhead": "Demon Copperhead",
  "pachinko": "Pachinko", "the-invisible-life-of-addie-larue": "La vida invisible de Addie LaRue", "small-things-like-these": "Cosas pequeñas como esas",
  "james": "James", "remarkably-bright-creatures": "Criaturas extraordinariamente brillantes", "the-house-in-the-cerulean-sea": "La casa en el mar más azul",
  "the-nickel-boys": "Los chicos de la Nickel", "homegoing": "Volver a casa", "lincoln-in-the-bardo": "Lincoln en el bardo",
  "exit-west": "Bienvenidos a Occidente", "yellowface": "Yellowface", "the-testaments": "Los testamentos",
  "before-the-coffee-gets-cold": "Antes de que se enfríe el café", "almond": "Almendra", "the-memory-police": "La policía de la memoria",
  "welcome-to-the-hyunam-dong-bookshop": "Bienvenidos a la librería Hyunam-dong", "project-hail-mary": "Proyecto Hail Mary", "the-silent-patient": "La paciente silenciosa",
  "the-housemaid": "La asistenta", "the-thursday-murder-club": "El club del crimen de los jueves", "fourth-wing": "Alas de sangre",
  "it-ends-with-us": "Romper el círculo", "book-lovers": "Book Lovers", "red-white-and-royal-blue": "Rojo, blanco y sangre azul",
  "the-ballad-of-songbirds-and-snakes": "Balada de pájaros cantores y serpientes", "children-of-blood-and-bone": "Hijos de sangre y hueso", "babel": "Babel",
  "the-hate-u-give": "El odio que das", "a-good-girls-guide-to-murder": "Asesinato para principiantes", "they-both-die-at-the-end": "Al final mueren los dos",
  "mexican-gothic": "Gótico", "sea-of-tranquility": "El mar de la tranquilidad", "exhalation": "Exhalación",
  "our-share-of-night": "Nuestra parte de noche", "papyrus": "El infinito en un junco", "homeland": "Patria",
  "still-born": "La hija única", "lost-children-archive": "Desierto sonoro", "educated": "Una educación",
  "atomic-habits": "Hábitos atómicos", "born-a-crime": "Prohibido nacer", "when-breath-becomes-air": "Recuerda que vas a morir. Vive",
  "the-psychology-of-money": "La psicología del dinero", "why-we-sleep": "Por qué dormimos", "crying-in-h-mart": "Llorando en H Mart",
  "the-anxious-generation": "La generación ansiosa", "four-thousand-weeks": "Cuatro mil semanas", "homo-deus": "Homo Deus",
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
      box.append(h("p.groupfoot", when ? `Real prices from gandhi.com.mx, updated ${MES[when.getMonth()]} ${when.getDate()}, ${String(when.getHours()).padStart(2, "0")}:${String(when.getMinutes()).padStart(2, "0")}. Printed books only. Margin earns nothing from these links.` : "Prices from gandhi.com.mx."));
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
