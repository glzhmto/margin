// Port of bookSearch() and searchKey() in preview/js/ui.js.
using System;
using System.Collections.Generic;
using System.Linq;
using Margin.Core;
using UnityEngine.UIElements;
using static Margin.App.Web.Components;
using static Margin.App.Web.H;

namespace Margin.App.Web.Screens
{
    /// <summary>
    /// bookSearch(): a search field over a list of books that filters in place (no rebuild, so the keyboard stays open).
    /// Used by the onboarding book step and "Add books you've read".
    /// </summary>
    public static class BookSearch
    {
        public static string Key(Book b) => Data.Norm($"{b.title} {b.author}");

        /// <summary>
        /// The search field followed by a container (sel) with one item per book from item(b).
        /// query/onQuery let a screen keep the typed text across a rebuild (the onboarding book step).
        /// </summary>
        public static object[] Grid(IEnumerable<Book> books, string sel, Func<Book, VisualElement> item, string query = "", Action<string> onQuery = null)
        {
            var rows = books.Select(b => (key: Key(b), el: item(b))).ToList();
            var empty = El("p.small.faint.mt12", "No book matches. Try another word.");
            empty.style.display = DisplayStyle.None;
            void Filter(string text)
            {
                var q = Data.Norm(text).Trim();
                int shown = 0;
                foreach (var (key, el) in rows) { bool on = q.Length == 0 || key.Contains(q); el.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; if (on) shown++; }
                empty.style.display = shown > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }
            var field = Input("input.input.mt16", query, q => { onQuery?.Invoke(q); Filter(q); }, "Search by title or author");
            if (!string.IsNullOrEmpty(query)) Filter(query);
            return new object[] { field, empty, El(sel, rows.Select(r => r.el)) };
        }
    }
}
