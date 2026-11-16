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
            var slug = Uri.EscapeDataString(string.Join("-", q.ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries)));
            return new List<StoreLink>
            {
                new StoreLink { Name = "Amazon México", Color = "#232F3E", Url = $"https://www.amazon.com.mx/s?k={e}&i=stripbooks" },
                new StoreLink { Name = "Mercado Libre", Color = "#2D3277", Url = $"https://listado.mercadolibre.com.mx/{slug}" },
                new StoreLink { Name = "Sanborns", Color = "#7A1F2B", Url = $"https://www.sanborns.com.mx/{e}?_q={e}&map=ft" },
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
