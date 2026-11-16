using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.App.Web
{
    /// <summary>
    /// What app.css does that USS can't, done in code right after an element is built (H.El and SetKids call
    /// <see cref="Apply"/>):
    /// <list type="bullet">
    /// <item>gap (gapN classes and components with a gap in app.css) as margins between children.</item>
    /// <item>display: grid (grid3, grid4, week, statgrid, counters, ach, facts; the Welcome mosaic builds its own columns) as wrapping rows
    /// whose children get their width from the container's width.</item>
    /// <item>Sibling selectors: list separators (.item + .item::before), stacked covers, face piles,
    /// the split button and the alert's action borders.</item>
    /// <item>-webkit-line-clamp as a max height, aspect-ratio as a height that follows the width.</item>
    /// </list>
    /// </summary>
    public static class Layout
    {
        struct Gap { public float Size; public bool Row; public Gap(float s, bool row) { Size = s; Row = row; } }

        /// <summary>Components of app.css that use gap, by their own class (direction of their flex box).</summary>
        static readonly Dictionary<string, Gap> ComponentGaps = new Dictionary<string, Gap>
        {
            ["tab"] = new Gap(3, false), ["btn"] = new Gap(8, true), ["pillbtn"] = new Gap(5, true), ["item"] = new Gap(12, true),
            ["check"] = new Gap(10, true), ["strength"] = new Gap(4, true), ["stepper"] = new Gap(14, true), ["feeditem"] = new Gap(10, true),
            ["meta"] = new Gap(8, true), ["foot"] = new Gap(14, true), ["human"] = new Gap(4, true), ["like"] = new Gap(4, true),
            ["bookhead"] = new Gap(16, true), ["chapter"] = new Gap(10, true), ["dentry"] = new Gap(12, true), ["bars"] = new Gap(6, true),
            ["b"] = new Gap(4, false), ["d"] = new Gap(4, false), ["a"] = new Gap(6, false), ["histo"] = new Gap(2, true),
            ["base"] = new Gap(1, true), ["fill"] = new Gap(1, true), ["rate-input"] = new Gap(6, true), ["hscroll"] = new Gap(10, true),
            ["banner"] = new Gap(10, true), ["player"] = new Gap(10, true),
        };

        static readonly Dictionary<string, float> GapClasses = new Dictionary<string, float>
        {
            ["gap4"] = 4, ["gap6"] = 6, ["gap8"] = 8, ["gap10"] = 10, ["gap12"] = 12, ["gap16"] = 16,
        };

        struct Grid { public int Cols; public float ColGap, RowGap; public float FirstCol; }

        static readonly Dictionary<string, Grid> Grids = new Dictionary<string, Grid>
        {
            ["grid3"] = new Grid { Cols = 3, ColGap = 10, RowGap = 10 },
            ["grid4"] = new Grid { Cols = 4, ColGap = 8, RowGap = 8 },
            ["week"] = new Grid { Cols = 7, ColGap = 6, RowGap = 6 },
            ["statgrid"] = new Grid { Cols = 2, ColGap = 1, RowGap = 1 },
            ["counters"] = new Grid { Cols = 4 },
            ["ach"] = new Grid { Cols = 4, ColGap = 8, RowGap = 12 },
            ["facts"] = new Grid { Cols = 2, FirstCol = 116 },
        };

        /// <summary>Children that are positioned absolutely: they take no part in gaps or grids.</summary>
        static readonly HashSet<string> Overlay = new HashSet<string> { "item-sep", "cover-edge", "rank", "dot", "knob", "fill", "trail" };

        public static void Apply(VisualElement el)
        {
            var kids = Children(el);

            foreach (var c in el.GetClasses().ToList())
            {
                if (Grids.TryGetValue(c, out var g)) { MakeGrid(el, g, kids); return; }
            }

            Gap? gap = null;
            foreach (var c in el.GetClasses())
            {
                if (GapClasses.TryGetValue(c, out var size)) gap = new Gap(size, !IsColumn(el));
                else if (ComponentGaps.TryGetValue(c, out var cg) && gap == null) gap = cg;
            }
            if (gap.HasValue && kids.Count > 1) ApplyGap(el, kids, gap.Value);

            if (el.ClassListContains("list")) ListSeparators(kids);
            if (el.ClassListContains("stackcov"))
                for (int i = 1; i < kids.Count; i++) kids[i].style.marginLeft = -36;
            if (el.ClassListContains("facepile"))
                for (int i = 1; i < kids.Count; i++)
                    if (kids[i].ClassListContains("avatar") && kids[i - 1].ClassListContains("avatar")) kids[i].style.marginLeft = -7;
            if (el.ClassListContains("split") && kids.Count > 0)
            {
                kids[0].AddToClassList("split-main");
                if (kids.Count > 1) kids[kids.Count - 1].AddToClassList("split-more");
            }
            if (el.ClassListContains("acts"))
                for (int i = 1; i < kids.Count; i++) kids[i].AddToClassList("sep-left");
            if (el.ClassListContains("m") && el.ClassListContains("rest"))
            {
                // .week .m.rest: repeating-linear-gradient(135deg, paper-2 0 4px, transparent 4px 8px)
                el.style.backgroundImage = new StyleBackground(Fx.Stripes(Theme.Color("paper-2")));
                el.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
                el.style.backgroundSize = new BackgroundSize(8, 8);
            }
        }

        static List<VisualElement> Children(VisualElement el)
        {
            var target = el.contentContainer ?? el;
            var list = new List<VisualElement>();
            foreach (var k in target.Children())
            {
                if (k.style.position.value == Position.Absolute && k.style.position.keyword != StyleKeyword.Null) continue;
                if (k.GetClasses().Any(Overlay.Contains)) continue;
                if (k.style.display.keyword != StyleKeyword.Null && k.style.display.value == DisplayStyle.None) continue;
                list.Add(k);
            }
            return list;
        }

        static bool IsColumn(VisualElement el)
        {
            if (el.style.flexDirection.keyword != StyleKeyword.Null) return el.style.flexDirection.value == FlexDirection.Column;
            if (el.ClassListContains("row") || el.ClassListContains("flow")) return false;
            return el.ClassListContains("col");
        }

        static void ApplyGap(VisualElement el, List<VisualElement> kids, Gap g)
        {
            bool wrap = el.ClassListContains("wrap") || el.ClassListContains("meta");
            if (!g.Row)
            {
                for (int i = 1; i < kids.Count; i++) kids[i].style.marginTop = g.Size;
                return;
            }
            if (!wrap)
            {
                for (int i = 1; i < kids.Count; i++)
                    if (kids[i].style.marginLeft.keyword == StyleKeyword.Null) kids[i].style.marginLeft = g.Size;
                return;
            }
            // Wrapping rows: space after each item and below each line; the container takes the last line's space back.
            for (int i = 0; i < kids.Count; i++)
            {
                if (i < kids.Count - 1) kids[i].style.marginRight = g.Size;
                kids[i].style.marginBottom = g.Size;
            }
            if (el.style.marginBottom.keyword == StyleKeyword.Null) el.style.marginBottom = -g.Size;
        }

        static void ListSeparators(List<VisualElement> kids)
        {
            bool prevItem = false;
            foreach (var k in kids)
            {
                bool item = k.ClassListContains("item");
                if (item && prevItem && k.Q(className: "item-sep") == null)
                {
                    var sep = new VisualElement { pickingMode = PickingMode.Ignore };
                    sep.AddToClassList("item-sep");
                    k.Add(sep);
                }
                prevItem = item;
            }
        }

        static void MakeGrid(VisualElement el, Grid g, List<VisualElement> kids)
        {
            el.style.flexDirection = FlexDirection.Row;
            el.style.flexWrap = Wrap.Wrap;
            el.style.alignItems = Align.FlexStart;
            bool facts = g.FirstCol > 0;
            for (int i = 0; i < kids.Count; i++)
            {
                var k = kids[i];
                int col = i % g.Cols;
                k.style.flexShrink = 0;
                k.style.flexGrow = 0;
                k.style.marginRight = col < g.Cols - 1 ? g.ColGap : 0;
                k.style.marginTop = i >= g.Cols ? g.RowGap : 0;
                if (facts)
                {
                    if (col == 0) { k.style.width = g.FirstCol; k.AddToClassList("faint"); }
                    else k.style.width = Length.Percent(60);
                }
                // A first guess until the container has a width (percentages can't subtract the gaps).
                else k.style.width = Length.Percent(100f / g.Cols - 3f);
            }
            if (el.userData is GridWired) { Fit(el, g); return; }
            el.userData = new GridWired();
            el.RegisterCallback<GeometryChangedEvent>(_ => Fit(el, g));
        }

        class GridWired { public float Width = -1; }

        static void Fit(VisualElement el, Grid g)
        {
            float w = el.contentRect.width;
            if (w <= 0 || float.IsNaN(w)) return;
            var kids = Children(el);
            if (g.FirstCol > 0)
            {
                for (int i = 1; i < kids.Count; i += 2) kids[i].style.width = Mathf.Max(0, w - g.FirstCol);
                return;
            }
            float cell = Mathf.Floor((w - g.ColGap * (g.Cols - 1)) / g.Cols * 100f) / 100f;
            foreach (var k in kids) k.style.width = cell;
        }

        // ---------- Text ----------

        /// <summary>
        /// -webkit-line-clamp: n. USS has no line clamp or line-height, so the text is cut at about n lines
        /// (1.3 x font size each) once the element knows its font size.
        /// </summary>
        public static void Clamp(VisualElement el)
        {
            int lines = el.ClassListContains("clamp2") ? 2 : el.ClassListContains("clamp3") ? 3 : 6;
            el.style.overflow = Overflow.Hidden;
            el.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float max = Mathf.Ceil(el.resolvedStyle.fontSize * 1.3f * lines);
                if (Mathf.Abs(el.resolvedStyle.maxHeight.value - max) > 0.5f) el.style.maxHeight = max;
            });
        }

        /// <summary>CSS aspect-ratio (height = width x ratio) for elements whose width is a percentage.</summary>
        public static void AspectRatio(VisualElement el, float heightOverWidth)
        {
            el.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float w = el.layout.width;
                if (w <= 0 || float.IsNaN(w)) return;
                float h = Mathf.Round(w * heightOverWidth * 100f) / 100f;
                if (Mathf.Abs(el.layout.height - h) > 0.5f) el.style.height = h;
            });
        }
    }

    /// <summary>
    /// The iPhone safe area, in panel units (= web CSS px). The web draws a fake 50 px status bar and a
    /// 34 px home bar; here the real insets are used instead (Screen.safeArea), so a screen fits under
    /// the notch or Dynamic Island. Use Safe.Top where the web hard-codes "50px" (e.g. paddingTop 56px
    /// becomes Safe.Top + 6).
    /// </summary>
    public static class Safe
    {
        public static float Top { get; private set; }
        public static float Bottom { get; private set; }

        /// <summary>Called by MarginApp when the screen size or safe area changes. True if it changed.</summary>
        public static bool Update(float top, float bottom)
        {
            top = Mathf.Round(top);
            bottom = Mathf.Round(bottom);
            if (Mathf.Approximately(top, Top) && Mathf.Approximately(bottom, Bottom)) return false;
            Top = top;
            Bottom = bottom;
            return true;
        }

        /// <summary>
        /// Applies the insets to a screen, where app.css had the fake status bar and home bar:
        /// navbar padding-top 50 (height 94), .scroll padding-top 50 (not .under-nav), tab bar padding-bottom 30.
        /// </summary>
        public static void Apply(VisualElement screen)
        {
            screen.Query<VisualElement>(className: "navbar").ForEach(n =>
            {
                if (InSheet(n)) return;
                n.style.paddingTop = Top;
                n.style.height = 44 + Top;
            });
            screen.Query<ScrollView>(className: "scroll").ForEach(s =>
            {
                if (InSheet(s) || s.ClassListContains("under-nav")) return;
                if (s.contentContainer.style.paddingTop.keyword == StyleKeyword.Null) s.contentContainer.style.paddingTop = Top;
            });
            screen.Query<VisualElement>(className: "tabbar").ForEach(t =>
            {
                t.style.paddingBottom = Bottom;
                t.style.height = 54 + Bottom;
            });
        }

        static bool InSheet(VisualElement e)
        {
            for (var p = e.parent; p != null; p = p.parent) if (p.ClassListContains("sheet")) return true;
            return false;
        }
    }
}
