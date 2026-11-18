// Port of LibraryAdd in preview/js/screens-main.js.
using System;
using System.Collections.Generic;
using System.Linq;
using Margin.Core;
using UnityEngine.UIElements;
using static Margin.App.Web.Components;
using static Margin.App.Web.H;

namespace Margin.App.Web.Screens
{
    /// <summary>"Add books you've read": search the catalog, then say when (roughly) and how it was.</summary>
    public static class LibraryAdd
    {
        public static void Open()
        {
            var done = new HashSet<string>(Data.Finished().Select(p => p.BookId)) { Data.State.profile.currentBookId };
            Sheet.Open("Add a book you've read", El("div",
                BookSearch.Grid(Data.Books.Where(b => !done.Contains(b.id)), "div.list", b =>
                    Btn("button.item", () => Details(b), Cover(b, 44), El("div.grow", El("div.strong", b.title), El("div.small.muted", b.author))))));
        }

        static void Details(Book b)
        {
            int y = Clock.Today.Year;
            string when = "earlier";
            double rating = 0;
            var whens = new[] { ("this", $"In {y}"), ("last", $"In {y - 1}"), ("earlier", "Before that") };
            VisualElement seg = null;
            seg = El("div.seg.mt8", whens.Select(w => Btn("button" + (w.Item1 == when ? ".on" : ""), () =>
            {
                when = w.Item1;
                int i = 0;
                foreach (var c in seg.Children()) c.EnableInClassList("on", whens[i++].Item1 == when);
            }, w.Item2)));
            var stars = El("div.mt8");
            void Stars(double v) { stars.Clear(); stars.Add(BookUI.RateInput(v, nv => { rating = nv; Stars(nv); })); }
            Stars(0);
            void Add()
            {
                var s = Data.State;
                var p = s.ProgressFor(b.id);
                p.currentPage = s.PagesFor(b);
                p.finished = true;
                Data.SetFinishedOn(b.id, when == "this" ? D.Key(Clock.Today) : when == "last" ? $"{y - 1}-07-01" : $"{y - 3}-07-01");
                if (rating > 0) Data.SetRating(b.id, rating);
                Data.X.want.RemoveAll(q => q == b.id);
                Data.X.dnf.RemoveAll(q => q == b.id);
                Data.Save();
                Sheet.Close();
                Toast($"{b.title} is in your library.");
                Nav.Refresh();
            }
            Sheet.Open(b.title, El("div",
                El("div.row.gap12", Cover(b, 56), El("div.grow", El("div.strong", b.title), El("div.small.muted", b.author))),
                El("div.small.muted.mt20", "When did you read it?"), seg,
                El("div.small.muted.mt20", "How was it? (optional)"), stars,
                Btn("button.btn.accent.mt24", Add, "Add to my library")));
        }
    }
}
