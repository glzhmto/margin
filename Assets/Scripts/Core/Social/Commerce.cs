// Buying the printed book: real Gandhi prices (from the book-prices edge function) and search
// links for other Mexican stores. Same stores, URLs and Spanish titles as preview/js/commerce.js.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Margin.Core
{
    public class StoreLink
    {
        public string Name;
        /// <summary>Brand color for the store's letter badge ("#232F3E").</summary>
        public string Color;
        public string Url;
    }

    public static class Commerce
    {
        /// <summary>Spanish titles, used to search Mexican stores (catalog titles are in English).</summary>
        public static readonly IReadOnlyDictionary<string, string> SpanishTitles = new Dictionary<string, string>
        {
            { "fahrenheit-451", "Fahrenheit 451" }, { "the-metamorphosis", "La metamorfosis" }, { "animal-farm", "Rebelión en la granja" },
            { "the-little-prince", "El principito" }, { "chronicle-of-a-death-foretold", "Crónica de una muerte anunciada" }, { "aura", "Aura" },
            { "lazarillo-de-tormes", "Lazarillo de Tormes" }, { "hamlet", "Hamlet" }, { "the-house-of-bernarda-alba", "La casa de Bernarda Alba" },
            { "1984", "1984" }, { "jekyll-and-hyde", "El extraño caso del Dr. Jekyll y Mr. Hyde" }, { "frankenstein", "Frankenstein" },
            { "the-stranger", "El extranjero" }, { "the-underdogs", "Los de abajo" }, { "the-labyrinth-of-solitude", "El laberinto de la soledad" },
            { "the-prince", "El príncipe" },
            { "the-odyssey", "La Odisea" }, { "the-theban-plays", "Tragedias" }, { "meditations", "Meditaciones" },
            { "the-last-days-of-socrates", "Apología de Sócrates" }, { "the-symposium", "El banquete" }, { "confessions", "Confesiones" },
            { "siddhartha", "Siddhartha" }, { "poems-protest-and-a-dream", "Respuesta a Sor Filotea" }, { "pride-and-prejudice", "Orgullo y prejuicio" },
            { "wuthering-heights", "Cumbres borrascosas" }, { "the-picture-of-dorian-gray", "El retrato de Dorian Gray" }, { "dracula", "Drácula" },
            { "heart-of-darkness", "El corazón de las tinieblas" }, { "candide", "Cándido" }, { "the-great-gatsby", "El gran Gatsby" },
            { "things-fall-apart", "Todo se desmorona" }, { "fever-dream", "Distancia de rescate" }, { "hurricane-season", "Temporada de huracanes" },
            { "things-we-lost-in-the-fire", "Las cosas que perdimos en el fuego" }, { "never-let-me-go", "Nunca me abandones" }, { "klara-and-the-sun", "Klara y el Sol" },
            { "the-road", "La carretera" }, { "normal-people", "Gente normal" }, { "convenience-store-woman", "La dependienta" },
            { "pedro-paramo", "Pedro Páramo" }, { "like-water-for-chocolate", "Como agua para chocolate" }, { "the-burning-plain", "El llano en llamas" },
            { "recollections-of-things-to-come", "Los recuerdos del porvenir" }, { "balun-canan", "Balún Canán" }, { "the-death-of-artemio-cruz", "La muerte de Artemio Cruz" },
            { "signs-preceding-the-end-of-the-world", "Señales que precederán al fin del mundo" }, { "faces-in-the-crowd", "Los ingrávidos" }, { "one-hundred-years-of-solitude", "Cien años de soledad" },
            { "ficciones", "Ficciones" }, { "the-house-of-the-spirits", "La casa de los espíritus" }, { "the-tunnel", "El túnel" },
            { "love-in-the-time-of-cholera", "El amor en los tiempos del cólera" }, { "no-one-writes-to-the-colonel", "El coronel no tiene quien le escriba" }, { "the-invention-of-morel", "La invención de Morel" },
            { "brave-new-world", "Un mundo feliz" }, { "the-handmaids-tale", "El cuento de la criada" }, { "lord-of-the-flies", "El señor de las moscas" },
            { "we", "Nosotros" }, { "of-mice-and-men", "De ratones y hombres" }, { "to-kill-a-mockingbird", "Matar a un ruiseñor" },
            { "crime-and-punishment", "Crimen y castigo" }, { "the-catcher-in-the-rye", "El guardián entre el centeno" }, { "the-old-man-and-the-sea", "El viejo y el mar" },
            { "slaughterhouse-five", "Matadero cinco" }, { "macbeth", "Macbeth" }, { "romeo-and-juliet", "Romeo y Julieta" },
            { "a-dolls-house", "Casa de muñecas" }, { "waiting-for-godot", "Esperando a Godot" }, { "death-of-a-salesman", "La muerte de un viajante" },
            { "life-is-a-dream", "La vida es sueño" }, { "blood-wedding", "Bodas de sangre" }, { "nada", "Nada" },
            { "platero-and-i", "Platero y yo" }, { "inferno", "Infierno" }, { "invisible-cities", "Las ciudades invisibles" },
            { "the-leopard", "El gatopardo" }, { "a-room-of-ones-own", "Una habitación propia" }, { "mans-search-for-meaning", "El hombre en busca de sentido" },
            { "letters-to-a-young-poet", "Cartas a un joven poeta" }, { "the-myth-of-sisyphus", "El mito de Sísifo" }, { "the-iliad", "La Ilíada" },
            { "medea", "Medea" }, { "on-the-shortness-of-life", "Sobre la brevedad de la vida" }, { "jane-eyre", "Jane Eyre" },
            { "the-turn-of-the-screw", "Otra vuelta de tuerca" }, { "rebecca", "Rebeca" }, { "kafka-on-the-shore", "Kafka en la orilla" },
            { "the-vegetarian", "La vegetariana" }, { "piranesi", "Piranesi" },
            { "the-count-of-monte-cristo", "El conde de Montecristo" }, { "anna-karenina", "Ana Karenina" }, { "the-brothers-karamazov", "Los hermanos Karamázov" },
            { "notes-from-underground", "Memorias del subsuelo" }, { "the-death-of-ivan-ilyich", "La muerte de Iván Ilich" }, { "madame-bovary", "Madame Bovary" },
            { "great-expectations", "Grandes esperanzas" }, { "a-tale-of-two-cities", "Historia de dos ciudades" }, { "a-christmas-carol", "Cuento de Navidad" },
            { "emma", "Emma" }, { "persuasion", "Persuasión" }, { "middlemarch", "Middlemarch" },
            { "tess-of-the-durbervilles", "Tess de los d'Urberville" }, { "the-scarlet-letter", "La letra escarlata" }, { "moby-dick", "Moby Dick" },
            { "the-adventures-of-huckleberry-finn", "Las aventuras de Huckleberry Finn" }, { "little-women", "Mujercitas" }, { "the-awakening", "El despertar" },
            { "the-call-of-the-wild", "La llamada de lo salvaje" }, { "the-time-machine", "La máquina del tiempo" }, { "the-war-of-the-worlds", "La guerra de los mundos" },
            { "treasure-island", "La isla del tesoro" }, { "robinson-crusoe", "Robinson Crusoe" }, { "gullivers-travels", "Los viajes de Gulliver" },
            { "don-quixote", "Don Quijote de la Mancha" }, { "the-betrothed", "Los novios" }, { "the-aeneid", "La Eneida" },
            { "the-oresteia", "La Orestíada" }, { "the-republic", "La República" }, { "the-art-of-war", "El arte de la guerra" },
            { "the-communist-manifesto", "El manifiesto comunista" }, { "the-epic-of-gilgamesh", "La epopeya de Gilgamesh" }, { "beowulf", "Beowulf" },
            { "the-canterbury-tales", "Los cuentos de Canterbury" }, { "the-trial", "El proceso" }, { "fathers-and-sons", "Padres e hijos" },
            { "the-age-of-innocence", "La edad de la inocencia" }, { "mrs-dalloway", "La señora Dalloway" }, { "the-sorrows-of-young-werther", "Las desventuras del joven Werther" },
            { "dead-souls", "Almas muertas" }, { "the-importance-of-being-earnest", "La importancia de llamarse Ernesto" }, { "king-lear", "El rey Lear" },
            { "walden", "Walden" }, { "narrative-of-the-life-of-frederick-douglass", "Narración de la vida de Frederick Douglass" }, { "the-hound-of-the-baskervilles", "El sabueso de los Baskerville" },
            { "the-fall-of-the-house-of-usher", "La caída de la casa Usher" }, { "kokoro", "Kokoro" }, { "rashomon-and-seventeen-other-stories", "Rashomon y otros cuentos" },
            { "the-master-and-margarita", "El maestro y Margarita" }, { "the-conquest-of-new-spain", "Historia verdadera de la conquista de la Nueva España" },
        };

        /// <summary>"$249 MXN", "$1,234.50 MXN".</summary>
        public static string FormatMxn(decimal amount)
        {
            bool cents = amount % 1 != 0;
            return "$" + amount.ToString(cents ? "#,0.00" : "#,0", CultureInfo.InvariantCulture) + " MXN";
        }

        /// <summary>The cheapest <paramref name="count"/> offers with a price and a link, lowest first.</summary>
        public static List<PriceOffer> TopOffers(IEnumerable<PriceOffer> offers, int count = 3) =>
            (offers ?? Enumerable.Empty<PriceOffer>())
                .Where(o => o != null && o.price > 0 && !string.IsNullOrEmpty(o.url))
                .OrderBy(o => o.price)
                .Take(count)
                .ToList();

        /// <summary>What we search for in the stores: "El principito Antoine de Saint-Exupéry".</summary>
        public static string SearchQuery(Book b)
        {
            string title;
            if (!SpanishTitles.TryGetValue(b.id ?? "", out title)) title = b.title;
            return $"{title} {b.author}".Trim();
        }

        public static List<StoreLink> Stores(Book b)
        {
            var q = SearchQuery(b);
            var e = Uri.EscapeDataString(q);
            return new List<StoreLink>
            {
                new StoreLink { Name = "Amazon México", Color = "#232F3E", Url = $"https://www.amazon.com.mx/s?k={e}&i=stripbooks" },
                new StoreLink { Name = "El Sótano", Color = "#5B3A29", Url = $"https://www.elsotano.com/busqueda/listaLibros.php?tipoBus=full&palabrasBusqueda={e}" },
            };
        }

        /// <summary>The footnote under the prices.</summary>
        public static string PriceFootnote(DateTime? fetchedAtLocal)
        {
            if (!fetchedAtLocal.HasValue) return "Prices from gandhi.com.mx. Printed books only.";
            var d = fetchedAtLocal.Value;
            string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            return $"Real prices from gandhi.com.mx, updated {months[d.Month - 1]} {d.Day}, {d.Hour:00}:{d.Minute:00}. Printed books only. Margin earns nothing from these links.";
        }
    }

    /// <summary>The FIL Guadalajara 2026 banner on Home: closable, and gone once the fair is over.</summary>
    public static class FilBanner
    {
        public static bool IsVisible(DateTime today, bool hiddenByUser, DateTime endsOn) =>
            !hiddenByUser && today.Date < endsOn.Date;
    }
}
