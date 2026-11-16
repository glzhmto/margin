using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.App.Web
{
    /// <summary>
    /// An attribute passed among the children of <see cref="H.El"/>, like the web's attrs object:
    /// H.El("button.btn", H.On(f), H.Disabled(busy), H.Css(s => s.width = 44), "Text").
    /// </summary>
    public sealed class Attr
    {
        internal readonly Action<VisualElement> Apply;
        internal Attr(Action<VisualElement> apply) { Apply = apply; }
    }

    /// <summary>
    /// The web's h(sel, attrs, ...kids) for UI Toolkit, so screens port almost line by line.
    /// <list type="bullet">
    /// <item>sel is "tag.class1.class2" with the class names of app.css (app.uss has the same names).</item>
    /// <item>Kids: strings and numbers become Labels; VisualElements are added; null and false are skipped;
    /// lists and arrays are flattened; <see cref="Attr"/> values set click, disabled and inline styles.</item>
    /// <item>An element whose kids are only text IS a Label (wraps like the web's text).</item>
    /// <item>"scroll", "sbody" and "hscroll" make a ScrollView (kids go to its content).</item>
    /// <item>Non-div tags add a "t-tag" class (t-button, t-span, t-i...) so app.uss can match the web's tag selectors.</item>
    /// <item>HTML inline layout is emulated: text mixed with spans/buttons in a block becomes a wrapping row (class "flow"),
    /// and a lone span/button in a block shrinks to its content (class "inline").</item>
    /// <item>gap, grid, sibling selectors and line clamps are emulated by <see cref="Layout"/>.</item>
    /// </list>
    /// </summary>
    public static class H
    {
        /// <summary>Tags that are inline in HTML: in a block parent they shrink to their content.</summary>
        static readonly HashSet<string> InlineTags = new HashSet<string> { "span", "b", "a", "button", "svg", "strong", "em" };

        /// <summary>Classes that make an element display:flex, inline-flex or grid in app.css. Everything else is a block.</summary>
        internal static readonly HashSet<string> FlexClasses = new HashSet<string>
        {
            "row", "col", "flow", "hscroll", "tabbar", "tab", "ring", "navbar", "iconbtn", "stack", "screen", "sect", "hscroll-content", "stackcov", "facepile",
            "avatar", "rank", "stars", "base", "fill", "rate-input", "histo", "btn", "pillbtn", "split", "seg", "week", "d", "item", "ic", "check",
            "box", "strength", "chip", "stepper", "feeditem", "meta", "foot", "human", "like", "bookhead", "facts", "chapter", "month", "dentry",
            "statgrid", "bars", "b", "counters", "ach", "a", "medal", "acts", "banner", "appic", "sheet", "shead", "type", "grid3", "grid4",
            "mosaic", "player", "switch", "inline-row",
        };

        /// <summary>Classes with text-transform: uppercase in app.css (USS can't transform text).</summary>
        static readonly HashSet<string> UpperClasses = new HashSet<string> { "caps", "grouphead", "k" };

        // ---------- Elements ----------

        /// <summary>h("div.card.block", ...kids).</summary>
        public static VisualElement El(string sel, params object[] kids) => Build(sel, null, kids);

        /// <summary>h("button.item", { onclick }, ...kids): a clickable element (works with touch, inside ScrollViews too).</summary>
        public static VisualElement Btn(string sel, Action onClick, params object[] kids) => Build(sel, onClick, kids);

        /// <summary>h("span.small.muted", "text") as a Label.</summary>
        public static Label Text(string sel, string text)
        {
            var e = Build(sel, null, new object[] { text ?? "" });
            return e as Label ?? new Label(text);
        }

        /// <summary>The web's setKids(el, ...kids): replaces the children (of a ScrollView's content too).</summary>
        public static T SetKids<T>(T el, params object[] kids) where T : VisualElement
        {
            var target = el.contentContainer ?? el;
            target.Clear();
            var flat = new List<object>();
            Flatten(kids, flat);
            AddContent(el, flat.Where(k => !(k is Attr)).ToList());
            Layout.Apply(el);
            return el;
        }

        /// <summary>Appends kids with the same rules as El (handy after inline styles: El("div").Abs(0, 0).With(...)).</summary>
        public static T With<T>(this T el, params object[] kids) where T : VisualElement
        {
            var flat = new List<object>();
            Flatten(kids, flat);
            foreach (var a in flat.OfType<Attr>()) a.Apply(el);
            AddContent(el, flat.Where(k => !(k is Attr)).ToList());
            Layout.Apply(el);
            return el;
        }

        /// <summary>A plain Label with no theme padding (rich text off: user text is shown as typed).</summary>
        public static Label NewLabel(string text)
        {
            var l = new Label(text ?? "");
            l.RemoveFromClassList(Label.ussClassName);
            l.RemoveFromClassList(TextElement.ussClassName);
            l.enableRichText = false;
            return l;
        }

        // ---------- Attributes ----------

        public static Attr On(Action onClick) => new Attr(e => MakeClickable(e, onClick));
        public static Attr Disabled(bool disabled) => new Attr(e => e.SetEnabled(!disabled));
        public static Attr Css(Action<IStyle> set) => new Attr(e => set(e.style));
        public static Attr Do(Action<VisualElement> f) => new Attr(f);
        public static Attr Name(string name) => new Attr(e => e.name = name);
        /// <summary>aria-label: kept as the tooltip (UI Toolkit has no accessibility label in this version).</summary>
        public static Attr Aria(string label) => new Attr(e => e.tooltip = label);

        public static void MakeClickable(VisualElement e, Action onClick)
        {
            if (onClick == null) return;
            e.AddManipulator(new Clickable(onClick));
            e.pickingMode = PickingMode.Position;
            e.AddToClassList("clickable");
        }

        // ---------- Form fields ----------

        /// <summary>
        /// h("input.input", { value, placeholder, oninput }): a TextField with the web look (.input in app.uss).
        /// onInput gets every change; onBlur runs when it loses focus; onEnter on the Return key.
        /// </summary>
        public static TextField Input(string sel, string value, Action<string> onInput, string placeholder = null,
                                      bool password = false, TouchScreenKeyboardType keyboard = TouchScreenKeyboardType.Default,
                                      int maxLength = -1, Action onBlur = null, Action onEnter = null)
        {
            var f = (TextField)Build(string.IsNullOrEmpty(sel) ? "input.input" : sel, null, new object[0]);
            f.SetValueWithoutNotify(value ?? "");
            if (!string.IsNullOrEmpty(placeholder))
            {
                f.textEdition.placeholder = placeholder;
                f.textEdition.hidePlaceholderOnFocus = false;
            }
            f.textEdition.isPassword = password;
            f.keyboardType = keyboard;
            if (maxLength > 0) f.maxLength = maxLength;
            if (onInput != null) f.RegisterValueChangedCallback(e => onInput(e.newValue));
            if (onBlur != null) f.RegisterCallback<FocusOutEvent>(_ => onBlur());
            if (onEnter != null)
                f.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) onEnter(); }, TrickleDown.TrickleDown);
            return f;
        }

        /// <summary>h("textarea.input", ...): multiline, serif 18 px like the web.</summary>
        public static TextField TextArea(string sel, string value, Action<string> onInput, string placeholder = null, int maxLength = -1)
        {
            var f = Input(string.IsNullOrEmpty(sel) ? "textarea.input" : sel, value, onInput, placeholder, maxLength: maxLength);
            f.multiline = true;
            f.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            return f;
        }

        /// <summary>h("img", { src }): a texture that covers the element (object-fit: cover).</summary>
        public static VisualElement Img(Texture texture, string sel = "img")
        {
            var e = Build(sel, null, new object[0]);
            if (texture is Texture2D t2) e.style.backgroundImage = new StyleBackground(t2);
            else if (texture is RenderTexture rt) e.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            e.style.backgroundPositionX = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(UnityEngine.ScaleMode.ScaleAndCrop);
            e.style.backgroundPositionY = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(UnityEngine.ScaleMode.ScaleAndCrop);
            e.style.backgroundRepeat = BackgroundPropertyHelper.ConvertScaleModeToBackgroundRepeat(UnityEngine.ScaleMode.ScaleAndCrop);
            e.style.backgroundSize = BackgroundPropertyHelper.ConvertScaleModeToBackgroundSize(UnityEngine.ScaleMode.ScaleAndCrop);
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        // ---------- Rich inline text ----------

        /// <summary>A styled piece of an <see cref="Inline"/> paragraph.</summary>
        public sealed class Seg
        {
            public string Text;
            public bool Bold;
            public bool Italic;
            /// <summary>Token or color ("ink-3", "var(--accent)", "#888").</summary>
            public string Color;
            public Action OnClick;
        }

        public static Seg S(string text, bool bold = false, string color = null, Action onClick = null, bool italic = false) =>
            new Seg { Text = text, Bold = bold, Color = color, OnClick = onClick, Italic = italic };

        /// <summary>
        /// A paragraph that wraps word by word across styled pieces (rich text), for long inline text such as
        /// "I accept the Terms of Use and the Privacy Notice". Plain strings are shown as typed; tappable pieces
        /// use &lt;link&gt; tags. Bold is synthesized from the paragraph's font.
        /// </summary>
        public static Label Inline(string sel, params object[] parts)
        {
            var l = (Label)Build(sel, null, new object[] { "" });
            l.enableRichText = true;
            var sb = new System.Text.StringBuilder();
            var actions = new List<Action>();
            var flat = new List<object>();
            Flatten(parts, flat);
            foreach (var p in flat)
            {
                if (p is Seg s)
                {
                    var t = "<noparse>" + (s.Text ?? "") + "</noparse>";
                    if (s.Bold) t = "<b>" + t + "</b>";
                    if (s.Italic) t = "<i>" + t + "</i>";
                    if (!string.IsNullOrEmpty(s.Color)) t = $"<color={Theme.Hex(s.Color)}>" + t + "</color>";
                    if (s.OnClick != null) { t = $"<link=\"{actions.Count}\">" + t + "</link>"; actions.Add(s.OnClick); }
                    sb.Append(t);
                }
                else if (p != null && !(p is Attr)) sb.Append("<noparse>" + Convert.ToString(p, System.Globalization.CultureInfo.InvariantCulture) + "</noparse>");
            }
            l.text = sb.ToString();
            if (actions.Count > 0)
            {
                l.RegisterCallback<UnityEngine.UIElements.Experimental.PointerUpLinkTagEvent>(e =>
                {
                    if (int.TryParse(e.linkID, out var i) && i >= 0 && i < actions.Count) { e.StopPropagation(); actions[i](); }
                });
            }
            foreach (var a in flat.OfType<Attr>()) a.Apply(l);
            return l;
        }

        // ---------- Building ----------

        static VisualElement Build(string sel, Action onClick, object[] kids)
        {
            var parts = (sel ?? "div").Split('.');
            var tag = string.IsNullOrEmpty(parts[0]) ? "div" : parts[0];
            var classes = parts.Skip(1).Where(c => c.Length > 0).ToList();

            var flat = new List<object>();
            Flatten(kids, flat);
            var attrs = flat.OfType<Attr>().ToList();
            var content = flat.Where(k => !(k is Attr)).ToList();
            bool textOnly = content.Count > 0 && content.All(IsText);

            VisualElement el;
            if (classes.Contains("hscroll"))
                el = NewScroll(ScrollViewMode.Horizontal);
            else if (classes.Contains("scroll") || classes.Contains("sbody"))
                el = NewScroll(ScrollViewMode.Vertical);
            else if (tag == "input" || tag == "textarea")
                el = NewField(tag == "textarea", textOnly ? string.Concat(content.Select(TextOf)) : "");
            else if (textOnly || (classes.Contains("dotsep") && content.Count == 0))
            {
                var text = classes.Contains("dotsep") && content.Count == 0 ? "·" : string.Concat(content.Select(TextOf));
                if (tag == "small" || classes.Any(UpperClasses.Contains)) text = text.ToUpperInvariant();
                el = NewLabel(text);
                content.Clear();
            }
            else el = new VisualElement();

            if (tag != "div") el.AddToClassList("t-" + tag);
            foreach (var c in classes) el.AddToClassList(c);
            if (classes.Any(c => c == "clamp2" || c == "clamp3" || c == "clamp6")) Layout.Clamp(el);

            foreach (var a in attrs) a.Apply(el);
            if (onClick != null) MakeClickable(el, onClick);

            if (content.Count > 0) AddContent(el, content);
            Layout.Apply(el);
            return el;
        }

        static ScrollView NewScroll(ScrollViewMode mode)
        {
            var s = new ScrollView(mode)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic,
            };
            s.mouseWheelScrollSize = 40;
            if (mode == ScrollViewMode.Horizontal) s.contentContainer.AddToClassList("hscroll-content");
            else s.contentContainer.AddToClassList("scroll-content");
            return s;
        }

        static TextField NewField(bool multiline, string value)
        {
            var f = new TextField { multiline = multiline, value = value ?? "" };
            // The root of a TextField carries the theme's field margins; .input in app.uss draws the box itself.
            f.RemoveFromClassList(TextField.ussClassName);
            f.RemoveFromClassList(TextInputBaseField<string>.ussClassName);
            f.RemoveFromClassList(BaseField<string>.ussClassName);
            f.textEdition.hidePlaceholderOnFocus = false;
            return f;
        }

        static void AddContent(VisualElement el, List<object> content)
        {
            bool block = IsBlock(el);
            bool hasText = content.Any(IsText);
            bool allInline = content.All(k => IsText(k) || (k is VisualElement v && IsInlineElement(v)));
            bool flow = block && hasText && allInline && content.Count > 1;
            if (flow) el.AddToClassList("flow");

            var target = el.contentContainer ?? el;
            foreach (var k in content)
            {
                if (k is VisualElement v)
                {
                    if (block && !flow && IsInlineElement(v))
                    {
                        v.AddToClassList("inline");
                        RelaxGrowKids(v);
                    }
                    target.Add(v);
                    continue;
                }
                var raw = TextOf(k);
                if (flow)
                {
                    // Spaces at the edges of a text piece separate it from its neighbours in the row.
                    var trimmed = raw.Trim();
                    if (trimmed.Length == 0) { target.Add(new VisualElement { pickingMode = PickingMode.Ignore, style = { width = 4 } }); continue; }
                    var l = NewLabel(trimmed);
                    l.AddToClassList("t-text");
                    if (raw.StartsWith(" ")) l.style.marginLeft = 4;
                    if (raw.EndsWith(" ")) l.style.marginRight = 4;
                    target.Add(l);
                }
                else
                {
                    var l = NewLabel(raw);
                    l.AddToClassList("t-text");
                    target.Add(l);
                }
            }
        }

        /// <summary>Row-direction classes of app.css (flex boxes whose main axis is horizontal).</summary>
        static readonly HashSet<string> RowClasses = new HashSet<string>
        {
            "row", "flow", "pillbtn", "check", "chip", "banner", "split", "seg", "player", "inline-row",
        };

        /// <summary>Classes that already have width: 100% in app.uss (they never shrink to their content).</summary>
        static readonly HashSet<string> FullWidthClasses = new HashSet<string>
        {
            "btn", "input", "item", "feeditem", "chapter", "dentry", "option",
        };

        /// <summary>
        /// An inline flex row (e.g. a button.row with no width) shrinks to its content, like a CSS button.
        /// CSS sizes it from the max-content width of its children, so a "flex: 1" text column still gets
        /// the width of its text. Yoga instead measures that column from its flex-basis (0), so the
        /// shrink-to-fit row collapses it to zero width and the text breaks one letter per line.
        /// Giving those columns an automatic basis makes Yoga measure their text first; they still shrink
        /// (min-width 0) and wrap when the row reaches the parent's width. Rows with an explicit width
        /// (the web's width: 100%) keep the zero basis and share the free space exactly like CSS.
        /// </summary>
        static void RelaxGrowKids(VisualElement v)
        {
            if (v.style.width.keyword != StyleKeyword.Null || v.GetClasses().Any(FullWidthClasses.Contains)) return;
            bool row = v.style.flexDirection.keyword != StyleKeyword.Null
                ? v.style.flexDirection.value == FlexDirection.Row
                : v.GetClasses().Any(RowClasses.Contains);
            if (!row) return;
            foreach (var k in (v.contentContainer ?? v).Children())
            {
                bool grow = k.ClassListContains("grow") || (k.style.flexGrow.keyword != StyleKeyword.Null && k.style.flexGrow.value > 0);
                if (!grow) continue;
                k.style.flexBasis = StyleKeyword.Auto;
                k.style.minWidth = 0;
            }
        }

        internal static bool IsBlock(VisualElement el)
        {
            if (el is Label) return false;
            if (el.style.flexDirection.keyword != StyleKeyword.Null) return false;
            foreach (var c in el.GetClasses()) if (FlexClasses.Contains(c)) return false;
            return true;
        }

        static bool IsInlineElement(VisualElement v)
        {
            foreach (var c in v.GetClasses())
                if (c.StartsWith("t-") && InlineTags.Contains(c.Substring(2))) return true;
            return false;
        }

        static bool IsText(object k) => k is string || k is int || k is long || k is double || k is float || k is decimal;

        /// <summary>Like the web's String(x): numbers without thousands separators (use F.N for those).</summary>
        static string TextOf(object k) => Convert.ToString(k, System.Globalization.CultureInfo.InvariantCulture);

        static void Flatten(IEnumerable kids, List<object> into)
        {
            if (kids == null) return;
            foreach (var k in kids)
            {
                if (k == null || (k is bool b && !b)) continue;
                if (k is string || k is VisualElement || k is Attr || k is Seg) { into.Add(k); continue; }
                if (k is IEnumerable list) { Flatten(list, into); continue; }
                into.Add(k);
            }
        }
    }

    /// <summary>
    /// Inline styles in the web's terms: el.W(44).Mt(12).Bg("var(--accent)").Fg("#fff").
    /// Lengths accept numbers (px) or strings ("100%", "44px", "auto"). Colors accept tokens ("ink-3"),
    /// "var(--ink-3)", "#hex" and "rgba(...)", resolved in the current theme.
    /// </summary>
    public static class Css
    {
        public static StyleLength Len(object v)
        {
            switch (v)
            {
                case null: return StyleKeyword.Null;
                case int i: return new StyleLength(i);
                case float f: return new StyleLength(f);
                case double d: return new StyleLength((float)d);
                case string s:
                    s = s.Trim();
                    if (s == "auto") return StyleKeyword.Auto;
                    if (s.EndsWith("%")) return new StyleLength(Length.Percent(float.Parse(s.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture)));
                    if (s.EndsWith("px")) s = s.Substring(0, s.Length - 2);
                    return new StyleLength(float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            }
            return StyleKeyword.Null;
        }

        public static T W<T>(this T e, object v) where T : VisualElement { e.style.width = Len(v); return e; }
        public static T Hgt<T>(this T e, object v) where T : VisualElement { e.style.height = Len(v); return e; }
        public static T Size<T>(this T e, object w, object h) where T : VisualElement { e.style.width = Len(w); e.style.height = Len(h); return e; }
        public static T MinH<T>(this T e, object v) where T : VisualElement { e.style.minHeight = Len(v); return e; }
        public static T MaxW<T>(this T e, object v) where T : VisualElement { e.style.maxWidth = Len(v); return e; }
        public static T Mt<T>(this T e, object v) where T : VisualElement { e.style.marginTop = Len(v); return e; }
        public static T Mb<T>(this T e, object v) where T : VisualElement { e.style.marginBottom = Len(v); return e; }
        public static T Ml<T>(this T e, object v) where T : VisualElement { e.style.marginLeft = Len(v); return e; }
        public static T Mr<T>(this T e, object v) where T : VisualElement { e.style.marginRight = Len(v); return e; }

        /// <summary>CSS margin shorthand: 1, 2, 3 or 4 values (top right bottom left).</summary>
        public static T M<T>(this T e, params object[] v) where T : VisualElement
        {
            var (t, r, b, l) = Box(v);
            e.style.marginTop = Len(t); e.style.marginRight = Len(r); e.style.marginBottom = Len(b); e.style.marginLeft = Len(l);
            return e;
        }

        /// <summary>CSS padding shorthand: 1, 2, 3 or 4 values (top right bottom left).</summary>
        public static T P<T>(this T e, params object[] v) where T : VisualElement
        {
            var (t, r, b, l) = Box(v);
            e.style.paddingTop = Len(t); e.style.paddingRight = Len(r); e.style.paddingBottom = Len(b); e.style.paddingLeft = Len(l);
            return e;
        }

        public static T Pt<T>(this T e, object v) where T : VisualElement { e.style.paddingTop = Len(v); return e; }
        public static T Pb<T>(this T e, object v) where T : VisualElement { e.style.paddingBottom = Len(v); return e; }

        static (object, object, object, object) Box(object[] v)
        {
            switch (v.Length)
            {
                case 1: return (v[0], v[0], v[0], v[0]);
                case 2: return (v[0], v[1], v[0], v[1]);
                case 3: return (v[0], v[1], v[2], v[1]);
                default: return (v[0], v[1], v[2], v[3]);
            }
        }

        public static T Bg<T>(this T e, string color) where T : VisualElement { e.style.backgroundColor = Theme.Color(color); return e; }

        /// <summary>Text color (and the icons inside, since inline styles can't set --fg).</summary>
        public static T Fg<T>(this T e, string color) where T : VisualElement
        {
            var c = Theme.Color(color);
            e.style.color = c;
            Icons.TintTree(e, c);
            return e;
        }

        public static T Radius<T>(this T e, float r) where T : VisualElement
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
            return e;
        }

        public static T Border<T>(this T e, float width, string color) where T : VisualElement
        {
            var c = Theme.Color(color);
            e.style.borderTopWidth = width; e.style.borderRightWidth = width; e.style.borderBottomWidth = width; e.style.borderLeftWidth = width;
            e.style.borderTopColor = c; e.style.borderRightColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c;
            return e;
        }

        public static T BorderTop<T>(this T e, float width = 1, string color = "rule") where T : VisualElement { e.style.borderTopWidth = width; e.style.borderTopColor = Theme.Color(color); return e; }
        public static T BorderBottom<T>(this T e, float width = 1, string color = "rule") where T : VisualElement { e.style.borderBottomWidth = width; e.style.borderBottomColor = Theme.Color(color); return e; }

        /// <summary>CSS flex: 1 (grow and shrink from a zero basis).</summary>
        public static T Flex1<T>(this T e) where T : VisualElement { e.style.flexGrow = 1; e.style.flexShrink = 1; e.style.flexBasis = 0; return e; }
        /// <summary>CSS flex: none.</summary>
        public static T FlexNone<T>(this T e) where T : VisualElement { e.style.flexGrow = 0; e.style.flexShrink = 0; return e; }

        /// <summary>position: absolute with the given edges (null = not set). Inset(0) fills the parent.</summary>
        public static T Abs<T>(this T e, object top = null, object right = null, object bottom = null, object left = null) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            if (top != null) e.style.top = Len(top);
            if (right != null) e.style.right = Len(right);
            if (bottom != null) e.style.bottom = Len(bottom);
            if (left != null) e.style.left = Len(left);
            return e;
        }

        public static T Inset<T>(this T e, object v) where T : VisualElement => e.Abs(v, v, v, v);
        public static T Relative<T>(this T e) where T : VisualElement { e.style.position = Position.Relative; return e; }
        public static T Opacity<T>(this T e, float o) where T : VisualElement { e.style.opacity = o; return e; }
        public static T FontSize<T>(this T e, float px) where T : VisualElement { e.style.fontSize = px; return e; }

        /// <summary>fontFamily + fontWeight (+ italic): Font(serif: true, weight: 600).</summary>
        public static T Font<T>(this T e, bool serif, int weight = 400, bool italic = false) where T : VisualElement => Fonts.Set(e, serif, weight, italic);

        public static T TextAlign<T>(this T e, TextAnchor a) where T : VisualElement { e.style.unityTextAlign = a; return e; }

        /// <summary>text-align: center (also centers inline children, like the web).</summary>
        public static T TextCenter<T>(this T e) where T : VisualElement { e.AddToClassList("center"); return e; }

        /// <summary>text-align: left (buttons are centered by default, as in HTML).</summary>
        public static T TextLeft<T>(this T e) where T : VisualElement { e.AddToClassList("text-left"); return e; }

        public static T Row<T>(this T e) where T : VisualElement { e.style.flexDirection = FlexDirection.Row; return e; }
        public static T Col<T>(this T e) where T : VisualElement { e.style.flexDirection = FlexDirection.Column; return e; }
        public static T AlignItems<T>(this T e, Align a) where T : VisualElement { e.style.alignItems = a; return e; }
        public static T AlignSelf<T>(this T e, Align a) where T : VisualElement { e.style.alignSelf = a; return e; }
        public static T Justify<T>(this T e, Justify j) where T : VisualElement { e.style.justifyContent = j; return e; }

        /// <summary>display: grid; place-items: center.</summary>
        public static T PlaceCenter<T>(this T e) where T : VisualElement { e.style.justifyContent = UnityEngine.UIElements.Justify.Center; e.style.alignItems = Align.Center; return e; }

        public static T Clip<T>(this T e) where T : VisualElement { e.style.overflow = Overflow.Hidden; return e; }
        public static T NoWrap<T>(this T e) where T : VisualElement { e.style.whiteSpace = WhiteSpace.NoWrap; return e; }

        /// <summary>white-space: nowrap; overflow: hidden; text-overflow: ellipsis.</summary>
        public static T Ellipsis<T>(this T e) where T : VisualElement
        {
            e.style.whiteSpace = WhiteSpace.NoWrap; e.style.overflow = Overflow.Hidden; e.style.textOverflow = TextOverflow.Ellipsis;
            return e;
        }

        public static T Cls<T>(this T e, string classes) where T : VisualElement
        {
            foreach (var c in (classes ?? "").Split(' ', '.')) if (c.Length > 0) e.AddToClassList(c);
            return e;
        }

        public static T Toggle<T>(this T e, string cls, bool on) where T : VisualElement { e.EnableInClassList(cls, on); return e; }
        public static T Show<T>(this T e, bool visible) where T : VisualElement { e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; return e; }
    }
}
