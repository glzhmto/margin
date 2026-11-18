// Port of preview/js/screens-settings.js (Settings and subscreens) and preview/js/screens-social.js
// (friends, invite link and QR, reader profiles, shared pacts, creators and posts).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Margin.App.Services;
using Margin.Core;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;
using static Margin.App.Web.Components;
using static Margin.App.Web.H;

namespace Margin.App.Web.Screens
{
    /// <summary>Helpers of screens-settings.js (row, group, exportCsv, importGoodreads).</summary>
    public static class SettingsUI
    {
        /// <summary>row(label, { ic, color, sub, value, control, onclick, danger, noChev }).</summary>
        public static VisualElement Row(string label, string ic = null, string color = null, string sub = null, string value = null,
                                        VisualElement control = null, Action onClick = null, bool danger = false, bool noChev = false)
        {
            var kids = new object[]
            {
                ic != null ? El("span.ic", Icon(ic)).Bg(color ?? "#5B554C") : null,
                El("div.grow", El("div", label), sub != null ? El("div.xs.faint", sub) : null),
                value != null ? El("span.val", value) : null,
                control,
                onClick != null && !noChev ? Icon("chev", "chev") : null,
            };
            var sel = (onClick != null ? "button.item" : "div.item") + (danger ? ".danger" : "");
            return onClick != null ? Btn(sel, onClick, kids) : El(sel, kids);
        }

        /// <summary>group(title, rows, foot).</summary>
        public static object[] Group(string title, IEnumerable<VisualElement> rows, string foot = null) =>
            new object[]
            {
                !string.IsNullOrEmpty(title) ? El("div.grouphead", title) : El("div").Hgt(18),
                El("div.list", rows.Where(r => r != null).ToList()),
                foot != null ? El("div.groupfoot", foot) : null,
            };

        /// <summary>
        /// exportCsv(): the web downloads margin-user-date.csv. On the phone there is no Downloads folder, so the
        /// CSV is written to the app's documents folder (Application.persistentDataPath) and the alert says where;
        /// it can also be copied to the clipboard from the same alert.
        /// </summary>
        public static void ExportCsv()
        {
            var s = Data.State;
            var rows = new List<object[]> { new object[] { "date", "book", "author", "from", "to", "pages" } };
            foreach (var l in s.logs)
            {
                var b = Data.Book(l.bookId);
                rows.Add(new object[] { D.Key(l.date), b?.title, b?.author, l.fromPage, l.toPage, l.toPage - l.fromPage });
            }
            rows.Add(new object[0]);
            rows.Add(new object[] { "book", "finished", "rating" });
            foreach (var p in Data.Finished(s))
                rows.Add(new object[] { Data.Book(p.BookId)?.title, p.FinishedOn, p.Rating > 0 ? (object)p.Rating : "" });

            string Cell(object x) => "\"" + Convert.ToString(x ?? "", System.Globalization.CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\"";
            var csv = string.Join("\n", rows.Select(r => string.Join(",", r.Select(Cell))));
            var name = $"margin-{Accounts.Session()}-{D.Key(Clock.Today)}.csv";
            try
            {
                var path = Path.Combine(UnityEngine.Application.persistentDataPath, name);
                File.WriteAllText(path, "﻿" + csv, new UTF8Encoding(false));
                IosAlert($"Downloaded: {F.N(s.logs.Count)} check-ins", $"Saved as {name} in Margin's documents folder:\n{path}",
                    Act("Copy CSV", () => SocialUI.CopyText(csv, "CSV copied")), Act("OK", bold: true));
            }
            catch (Exception e)
            {
                // Writing failed (no space, no permission): the clipboard still gets the data.
                UnityEngine.Debug.LogWarning("[Margin] " + e.Message);
                SocialUI.CopyText(csv, $"Copied: {F.N(s.logs.Count)} check-ins (CSV)");
            }
        }

        /// <summary>parseCsv(text): rows of cells (quoted cells, doubled quotes, CRLF); blank rows are dropped.</summary>
        public static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool q = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (q)
                {
                    if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else q = false; }
                    else cell.Append(c);
                }
                else if (c == '"') q = true;
                else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); rows.Add(row); row = new List<string>(); cell.Clear();
                }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows.Where(r => r.Any(x => x.Trim().Length > 0)).ToList();
        }

        /// <summary>What importGoodreadsText found: data rows, books matched in the catalog, and whether the file isn't a Goodreads export.</summary>
        public class ImportResult { public int Rows; public int Matched; public bool Bad; }

        static string NormTitle(string x)
        {
            var d = (x ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
            d = Regex.Replace(d, "[̀-ͯ]", "");
            d = Regex.Replace(d, @"\(.*?\)", "");
            d = Regex.Replace(d, "[^a-z0-9 ]", " ");
            return Regex.Replace(d, @"\s+", " ").Trim();
        }

        static string LastWord(string x) { var n = NormTitle(x).Split(' '); return n[n.Length - 1]; }

        /// <summary>
        /// importGoodreadsText(text): matches each row to the catalog by normalized title and the author's last name.
        /// "read" marks the book finished (with the Goodreads rating and date read if Margin has none);
        /// "to-read" adds it to the want list. Saves when something matched.
        /// </summary>
        public static ImportResult ImportGoodreadsText(string text)
        {
            var rows = ParseCsv((text ?? "").TrimStart('﻿'));
            if (rows.Count < 2) return new ImportResult();
            var head = rows[0].Select(x => x.Trim().ToLowerInvariant()).ToList();
            int iT = head.IndexOf("title"), iA = head.IndexOf("author"), iS = head.IndexOf("exclusive shelf"), iR = head.IndexOf("my rating"), iD = head.IndexOf("date read");
            if (iT < 0) return new ImportResult { Bad = true };
            string At(List<string> r, int i) => i >= 0 && i < r.Count ? r[i] : null;
            var s = Data.State;
            var want = Data.X.want;
            int matched = 0;
            foreach (var r in rows.Skip(1))
            {
                var t = NormTitle(At(r, iT));
                var au = iA >= 0 ? LastWord(At(r, iA)) : "";
                var b = Data.Books.FirstOrDefault(bk =>
                {
                    var bt = NormTitle(bk.title);
                    return (t == bt || t.StartsWith(bt + " ", StringComparison.Ordinal)) && (au.Length == 0 || LastWord(bk.author) == au);
                });
                if (b == null) continue;
                matched++;
                var shelf = iS >= 0 ? (At(r, iS) ?? "").Trim() : "read";
                if (shelf == "read")
                {
                    var p = s.ProgressFor(b.id);
                    if (!p.finished) { p.finished = true; p.currentPage = Math.Max(p.currentPage, b.pages); }
                    var m = Regex.Match((At(r, iR) ?? "").Trim(), @"^-?\d+"); // parseInt
                    if (m.Success && int.TryParse(m.Value, out var stars) && stars > 0 && Data.Rating(b.id) == 0) Data.SetRating(b.id, stars);
                    var dr = (At(r, iD) ?? "").Trim();
                    var d = iD >= 0 && Regex.IsMatch(dr, @"^\d{4}/\d{2}/\d{2}$") ? dr.Replace('/', '-') : null;
                    if (d != null && string.IsNullOrEmpty(Data.FinishedOn(b.id))) Data.SetFinishedOn(b.id, d);
                    want.RemoveAll(id => id == b.id);
                }
                else if (shelf == "to-read")
                {
                    if (!want.Contains(b.id) && !s.ProgressFor(b.id).finished) want.Add(b.id);
                }
            }
            if (matched > 0) Data.Save();
            return new ImportResult { Rows = rows.Count - 1, Matched = matched };
        }

        /// <summary>Runs the import and shows the web's toasts.</summary>
        static void ImportText(string text)
        {
            var r = ImportGoodreadsText(text);
            if (r.Bad) { Toast("That doesn't look like a Goodreads export."); return; }
            if (r.Rows == 0) { Toast("The file is empty."); return; }
            Toast(r.Matched > 0 ? $"We read {F.Books(r.Rows)}. {F.Books(r.Matched)} from our catalog were added." : $"We read {F.Books(r.Rows)}, but none are in our catalog yet.");
            Nav.Refresh();
        }

        /// <summary>
        /// importGoodreads(): the web opens a file picker. iOS has no built-in file picker for Unity, so the CSV is
        /// pasted from the clipboard (copied from the Files app). In the editor a file path can be typed instead.
        /// </summary>
        public static void ImportGoodreads()
        {
            var acts = new List<AlertAction> { Act("Cancel") };
            if (UnityEngine.Application.isEditor) acts.Add(Act("Choose file", ImportFromPath));
            acts.Add(Act("Paste", () => ImportText(UnityEngine.GUIUtility.systemCopyBuffer ?? ""), bold: true));
            IosAlert("Import from Goodreads",
                "In Goodreads, go to My Books › Import and export › Export Library. Open the export in Files, copy all, then tap Paste.",
                acts.ToArray());
        }

        /// <summary>Editor only: type the path of the exported CSV.</summary>
        static void ImportFromPath()
        {
            string path = "";
            void Go()
            {
                string text;
                try { text = File.ReadAllText(path.Trim().Trim('"')); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[Margin] " + e.Message); Toast("Couldn't read the file."); return; }
                Sheet.Close(true);
                ImportText(text);
            }
            Sheet.Open("Import from Goodreads", El("div",
                El("p.small.muted", "Path of goodreads_library_export.csv on this computer."),
                Input("input.input.mt12", "", v => path = v, placeholder: "/Users/you/Downloads/goodreads_library_export.csv", onEnter: Go),
                Btn("button.btn.primary.mt12", Go, "Import")));
        }
    }

    /// <summary>Helpers of screens-social.js used by other screens too.</summary>
    public static class SocialUI
    {
        public static readonly Dictionary<string, string> POST_KIND = new Dictionary<string, string> { ["list"] = "List", ["text"] = "Note", ["video"] = "Video" };

        /// <summary>inviteLink(username): the web link that opens the profile (InviteLink.Build).</summary>
        public static string InviteLink(string username = null) =>
            Core.InviteLink.Build(MarginApp.Instance.Backend.inviteBaseUrl, username ?? Accounts.Session());

        /// <summary>copyText(t, msg): GUIUtility.systemCopyBuffer, then a toast.</summary>
        public static void CopyText(string text, string msg = null)
        {
            UnityEngine.GUIUtility.systemCopyBuffer = text;
            Toast(msg ?? "Link copied");
        }

        /// <summary>qrImage(text, size): the QR code (level M, like the web's qrcode(0, "M")) with a 4-module margin.</summary>
        public static VisualElement QrImage(string text, int size = 184)
        {
            try
            {
                var tex = Qr.Cached(text);
                int cells = tex.width, cell = Math.Max(2, size / cells);
                var img = Img(tex).Size(cell * cells, cell * cells).Bg("#fff").Radius(10);
                img.style.backgroundPositionX = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(UnityEngine.ScaleMode.StretchToFill);
                img.style.backgroundPositionY = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(UnityEngine.ScaleMode.StretchToFill);
                img.style.backgroundRepeat = BackgroundPropertyHelper.ConvertScaleModeToBackgroundRepeat(UnityEngine.ScaleMode.StretchToFill);
                img.style.backgroundSize = BackgroundPropertyHelper.ConvertScaleModeToBackgroundSize(UnityEngine.ScaleMode.StretchToFill);
                img.tooltip = "QR code with your invite link";
                return img;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Margin] " + e.Message);
                return El("p.small.faint", "Couldn't draw the QR code.");
            }
        }

        /// <summary>A button that disables itself, runs f, then redraws screens that aren't live (the web's run()).</summary>
        static VisualElement RunBtn(string sel, Func<Task> f, params object[] kids)
        {
            VisualElement b = null;
            b = Btn(sel, () => App.Run(async () =>
            {
                b.SetEnabled(false);
                await f();
                // Live screens redraw from Social.Changed(); the others (search results) need an explicit redraw.
                var c = Nav.Current;
                if (c != null && !c.Live)
                {
                    if (c is FindFriendsScreen ff) ff.Draw();
                    else Nav.Refresh();
                }
            }), kids);
            return b;
        }

        public static VisualElement RelButton(Profile p, bool compact = false)
        {
            var r = Social.Rel(p.id);
            const string sm = "button.btn.sm";
            switch (r.Kind)
            {
                case RelationKind.Self: return null;
                case RelationKind.Friend:
                    return RunBtn(sm + ".secondary", () =>
                    {
                        IosAlert($"Remove {Pname(p)} as a friend?", "You'll stop seeing each other's reading. They won't be notified.",
                            Act("Cancel", () => Nav.Refresh()), Act("Remove", () => App.Run(Social.Remove(r.Row)), red: true));
                        return Task.CompletedTask;
                    }, Icon("check"), "Friends");
                case RelationKind.Incoming:
                    return El("div.row.gap6", RunBtn(sm + ".primary", () => Social.Accept(r.Row), "Accept"),
                        compact ? null : RunBtn(sm + ".secondary", () => Social.Remove(r.Row), "Decline"));
                case RelationKind.Outgoing:
                    return RunBtn(sm + ".secondary", () => Social.Remove(r.Row), "Requested");
                default:
                    return RunBtn(sm + ".primary", async () => { await Social.Request(p.id); Toast($"Request sent to {Pfirst(p)}"); }, "Add friend");
            }
        }

        public static VisualElement FollowButton(Profile p)
        {
            if (p == null || p.id == Net.Uid || !p.is_creator) return null;
            bool on = Social.FollowingIds.Contains(p.id);
            VisualElement b = null;
            b = Btn("button.btn.sm" + (on ? ".secondary" : ".primary"), () => App.Run(async () =>
            {
                b.SetEnabled(false);
                await (on ? Social.Unfollow(p.id) : Social.Follow(p.id));
                Nav.Refresh();
            }), on ? "Following" : "Follow");
            return b;
        }

        public static VisualElement PersonRow(Profile p, bool uni = false, bool noButton = false)
        {
            var b = Data.Book(p.current_book_id);
            return Btn("div.feeditem", () => Nav.Push(new UserScreen(p)), Avatar(Pav(p), 44),
                El("div.grow", El("div.strong", Pname(p)), El("div.small.muted", $"@{p.username}", p.is_creator ? new object[] { El("span.dotsep"), "Creator" } : null),
                    b != null ? El("div.xs.faint.mt2.clamp2", $"Reading {b.title}") : !string.IsNullOrEmpty(p.university) && uni ? El("div.xs.faint.mt2", p.university) : null),
                noButton ? null : RelButton(p, compact: true)).AlignItems(Align.Center);
        }

        public static VisualElement RequestRow(Connection x)
        {
            VisualElement accept = null;
            accept = Btn("button.btn.sm.primary", () => App.Run(async () =>
            {
                accept.SetEnabled(false);
                await Social.Accept(x.Row);
                Toast($"You and {Pfirst(x.Person)} are friends now");
            }), "Accept");
            return Btn("div.feeditem", () => Nav.Push(new UserScreen(x.Person)), Avatar(Pav(x.Person), 44),
                El("div.grow", El("div.strong", Pname(x.Person)), El("div.small.muted", $"@{x.Person?.username} wants to be your friend")),
                El("div.row.gap6", accept, Btn("button.iconbtn", () => App.Run(Social.Remove(x.Row)), Aria("Decline"), Icon("x")))).AlignItems(Align.Center);
        }

        public static VisualElement CreatorCard(Profile p) =>
            El("div.card.col", Btn("button", () => Nav.Push(new UserScreen(p)), Avatar(Pav(p), 44)),
                El("div.small.strong.mt6.clamp2", Pname(p)), El("div.xs.faint", $"@{p.username}"),
                El("div.mt8", FollowButton(p)).W("100%"))
            .W(140).P(14, 10).AlignItems(Align.Center).TextCenter();

        public static VisualElement PostRow(Post p)
        {
            var a = p.profiles ?? new AuthorRef();
            var books = p.kind == "list" ? (p.items ?? new List<PostItem>()).Select(i => Data.Book(i.bookId)).Where(b => b != null).ToList() : new List<Book>();
            return Btn("button.pad", () => Nav.Push(new PostScreen(p.id)),
                books.Count > 0 ? El("div.stackcov", books.Take(5).Select(b => Cover(b, 72))).Mb(8) : null,
                El("div.row.gap6.xs.faint", El("span.badge" + (p.kind == "video" ? ".todo" : ".ok"), POST_KIND.TryGetValue(p.kind ?? "", out var k) ? k : "Note"),
                    El("span", F.Ago(Local(p.created_at), DateTime.Now))),
                El("div.h3.mt6", p.title ?? ""),
                El("div.row.gap6.mt4.xs.muted", Avatar(Pav(a), 20), El("span", Pname(a)), books.Count > 0 ? new object[] { El("span.dotsep"), El("span.num", F.Books(books.Count)) } : null),
                !string.IsNullOrEmpty(p.body) ? El("div.small.muted.mt4.clamp2", p.body) : null)
            .W("100%").TextLeft().P(12, 16, 14).BorderBottom();
        }

        /// <summary>youtubeId(url): the 11-character id, or null.</summary>
        public static string YoutubeId(string url) => PostRules.YouTubeId(url);

        /// <summary>Server stamps come in UTC; the web's new Date() shows them in local time.</summary>
        internal static DateTime Local(DateTime d) => d.Kind == DateTimeKind.Utc ? d.ToLocalTime() : d;

        /// <summary>word-break: break-all for links: zero-width spaces let TextCore wrap anywhere.</summary>
        internal static string BreakAll(string s) => string.Join("​", (s ?? "").ToCharArray());

        /// <summary>Inline font of a TextField's text (the web sets fontSize/fontFamily on the input itself).</summary>
        internal static T InputFont<T>(T f, float size, bool sans = false) where T : TextField
        {
            var inner = f.Q(className: "unity-base-text-field__input");
            if (inner != null)
            {
                inner.style.fontSize = size;
                if (sans) Fonts.Set(inner, false, 400, false);
            }
            return f;
        }
    }

    // ---------------- screens-settings.js ----------------

    public class SettingsScreen : WebScreen
    {
        static VisualElement Row(string label, string ic = null, string color = null, string sub = null, string value = null,
                                 VisualElement control = null, Action onClick = null, bool danger = false, bool noChev = false) =>
            SettingsUI.Row(label, ic, color, sub, value, control, onClick, danger, noChev);

        public override VisualElement Build()
        {
            var a = Accounts.Current;
            if (a == null) return El("div", Navbar("Settings"));
            var s = a.State; var g = s.profile.goals; var st = a.Settings;
            return El("div", Navbar("Settings"), El("div.scroll.under-nav",
                Btn("button.row.gap12.list", () => Nav.Push(new SettingsDetail("account")),
                    Avatar(Data.Me(), 44), El("div.grow", El("div.strong", a.Name), El("div.small.muted", !string.IsNullOrEmpty(a.Email) ? $"@{a.Username} · {a.Email}" : $"@{a.Username} · guest")), Icon("chev", "chev"))
                    .P(12, 14).TextLeft().Mt(8).AlignSelf(Align.Stretch),
                a.IsGuest ? SettingsUI.Group("", new[] { Row("Save your account", ic: "user", color: "#C8402A", sub: "Add an email and password to keep your reading", onClick: () => Nav.Push(new SaveAccountScreen())) },
                    "You're trying Margin as a guest. If you log out before saving, this reading is lost.") : null,
                SettingsUI.Group("", new[]
                {
                    Row("Profile", ic: "user", color: "#3E5C76", onClick: () => Nav.Push(new SettingsDetail("profile"))),
                    Row("Privacy", ic: "lock", color: "#5B554C", value: st.PublicProfile ? "Public" : "Private", onClick: () => Nav.Push(new SettingsDetail("privacy"))),
                }),
                SettingsUI.Group("Reading", new[]
                {
                    Row("Goals", ic: "bookopen", color: "#C8402A", value: $"{g.dailyPages} pages a day", onClick: () => Nav.Push(new SettingsDetail("reading"))),
                    Row("Reminders", ic: "bell", color: "#B5893B", value: g.remindersEnabled ? Fmt.Time(g.reminderHour, g.reminderMinute) : "Off", onClick: () => Nav.Push(new SettingsDetail("reminders"))),
                    Row("Focus mode", ic: "moon", color: "#6B4E71", value: $"{st.FocusMinutes} min", onClick: () => Nav.Push(new SettingsDetail("focus"))),
                }),
                SettingsUI.Group("App", new[]
                {
                    Row("Appearance", ic: "sun", color: "#2F6B47", value: st.Theme, onClick: () => Nav.Push(new SettingsDetail("appearance"))),
                    Row("Friends", ic: "users", color: "#8A5A44", value: Social.Incoming.Count > 0 ? $"{Social.Incoming.Count} new" : (Social.Friends.Count > 0 ? Social.Friends.Count.ToString() : null), onClick: () => Nav.Push(new FriendsScreen())),
                }),
                SettingsUI.Group("Your data", new[]
                {
                    Row("Export my data", ic: "download", color: "#4A6B5D", sub: "CSV with your diary, ratings and reviews", onClick: SettingsUI.ExportCsv),
                    Row("Import from Goodreads", ic: "upload", color: "#4A6B5D", onClick: SettingsUI.ImportGoodreads),
                }),
                SettingsUI.Group("Help", new[]
                {
                    Row("Help center", ic: "help", color: "#5B554C", onClick: () => Nav.Push(new HelpScreen())),
                    Row("Write to us", ic: "mail", color: "#5B554C", value: "0262939@up.edu.mx", noChev: true, onClick: () => App.OpenUrl("mailto:0262939@up.edu.mx?subject=Margin%20feedback")),
                    Row("Terms and privacy", ic: "doc", color: "#5B554C", onClick: () => Nav.Push(new LegalScreen())),
                }),
                SettingsUI.Group("Presentation", new[]
                {
                    Row("Simulate next day", value: Clock.IsSimulated ? $"+{Clock.DayOffset} d" : null, noChev: true, onClick: () => { Clock.AdvanceDay(); Toast($"It's now {Fmt.DateLong(Clock.Today)}."); Nav.Refresh(); }),
                    Clock.IsSimulated ? Row("Back to the real clock", noChev: true, onClick: () => { Clock.ResetSimulation(); Nav.Refresh(); }) : null,
                    Row("Show a reminder", noChev: true, onClick: App.DemoBanner),
                }, "Tools for the class demo. They don't exist in the released app."),
                El("div").Hgt(18),
                El("div.list", Row("Log out", danger: true, noChev: true, onClick: () => IosAlert("Log out?",
                    a.IsGuest ? "You're a guest. If you log out now, this reading is gone. Save your account first to keep it." : "Your diary stays saved in your account.",
                    Act("Cancel"), Act("Log out", () => App.Run(async () => { await Net.SignOut(); App.Start(); }), red: true)))),
                SettingsUI.Group("", new[] { Row("Delete account", danger: true, noChev: true, onClick: () => Nav.Push(new DeleteAccountScreen())) },
                    "Deletes your account and everything in it, so you can start again from zero. This can't be undone."),
                El("p.center.xs.faint", $"Margin {Data.APP_VERSION} (2026.11) · Made in Mexico City").P(20, 0, 34)));
        }
    }

    public class SettingsDetail : WebScreen
    {
        /// <summary>"account", "profile", "privacy", "reading", "reminders", "focus" or "appearance".</summary>
        public string key;

        public SettingsDetail(string key) { this.key = key; }

        static readonly Dictionary<string, string> T = new Dictionary<string, string>
        {
            ["account"] = "Account", ["profile"] = "Profile", ["privacy"] = "Privacy", ["reading"] = "Reading goals",
            ["reminders"] = "Reminders", ["focus"] = "Focus mode", ["appearance"] = "Appearance",
        };

        static VisualElement Row(string label, string ic = null, string color = null, string sub = null, string value = null,
                                 VisualElement control = null, Action onClick = null, bool danger = false, bool noChev = false) =>
            SettingsUI.Row(label, ic, color, sub, value, control, onClick, danger, noChev);

        static Task<string> Done(string error = null) => Task.FromResult(error);

        public override VisualElement Build()
        {
            var a = Accounts.Current;
            if (a == null) return El("div", Navbar(T.TryGetValue(key, out var t0) ? t0 : ""));
            var s = a.State; var g = s.profile.goals; var st = a.Settings;
            void Save() { Data.Save(); Nav.Refresh(); }
            object body = null;
            switch (key)
            {
                case "account":
                    body = new object[]
                    {
                        SettingsUI.Group("", new[]
                        {
                            Row("Name", value: a.Name, onClick: () => Nav.Push(new EditField("Name", a.Name, v => v.Trim().Length < 2 ? "Type your name." : null, async v =>
                            {
                                var e = await Net.UpdateProfile(new JObject { ["name"] = v.Trim() });
                                if (e != null) return "Couldn't save. Try again.";
                                s.profile.name = v.Trim().Split(' ')[0];
                                return null;
                            }))),
                            Row("Username", value: "@" + a.Username, onClick: () => Nav.Push(new EditField("Username", a.Username, v => v == a.Username ? null : V.Username(v),
                                v => v == a.Username ? Done() : Net.ChangeUsername(v), "Friends find you by this name. Your old invite link stops working."))),
                            a.IsGuest ? Row("Email and password", value: "Not set", onClick: () => Nav.Push(new SaveAccountScreen())) : null,
                            a.IsGuest ? null : Row("Email", value: a.Email.Length > 22 ? a.Email.Substring(0, 20) + "…" : a.Email, onClick: () => Nav.Push(new EditField("Email", a.Email, V.Email, async v =>
                            {
                                if (v == a.Email) return null;
                                var e = await Net.SetEmail(v);
                                if (e != null) return e;
                                Toast("We sent a link to the new address. The change applies when you open it.");
                                return null;
                            }))),
                            a.IsGuest ? null : Row("Password", value: "••••••••", onClick: () => Nav.Push(new PasswordScreen())),
                            Row("University", value: !string.IsNullOrEmpty(a.University) ? a.University : "Not set", onClick: () => Nav.Push(new EditField("University", a.University ?? "", _ => null, async v =>
                            {
                                var u = v.Trim();
                                var e = await Net.UpdateProfile(new JObject { ["university"] = u.Length > 80 ? u.Substring(0, 80) : u });
                                return e != null ? "Couldn't save. Try again." : null;
                            }, "Optional. We suggest friends from the same place."))),
                        }),
                        SettingsUI.Group("", new[] { Row("Delete account", danger: true, noChev: true, onClick: () => Nav.Push(new DeleteAccountScreen())) },
                            "Your diary, friends, posts and reviews are deleted. This can't be undone."),
                    };
                    break;

                case "profile":
                {
                    var bioText = a.Bio ?? "";
                    var cnt = Text("div.help", $"{bioText.Length}/160");
                    var bio = SocialUI.InputFont(TextArea("textarea.input", bioText, v => { bioText = v; cnt.text = $"{v.Length}/160"; }, maxLength: 160), 15, sans: true);
                    bio.style.minHeight = 90;
                    var fins = Data.Finished(s);
                    var favs = Data.X.favorites;
                    body = new object[]
                    {
                        El("div.col.pad.mt12", Avatar(Data.Me(), 88)).AlignItems(Align.Center),
                        El("div.pad.mt16", El("span.small.muted", "Bio"), El("div.mt6", bio), cnt,
                            Btn("button.btn.sm.primary.mt8", () => App.Run(async () => { if (await Net.UpdateProfile(new JObject { ["bio"] = bioText.Trim() }) == null) Toast("Bio saved"); }), "Save")),
                        SettingsUI.Group("Creator", new[]
                        {
                            Row("Creator account", sub: "Publish book lists, notes and videos. Readers can follow you.", control: SwitchEl(a.IsCreator, v => App.Run(async () =>
                            {
                                var e = await Net.UpdateProfile(new JObject { ["is_creator"] = v });
                                if (e == null) { Toast(v ? "You're a creator now. Publish from your profile." : "Creator account turned off"); App.Run(Social.Refresh()); }
                            }))),
                        }, "Your posts are public to every Margin reader. Followers see them in their feed. No like counts, no autoplay."),
                        El("div.grouphead", "Favorite books (up to 4)"),
                        El("div.grid4.pad", fins.Select(p =>
                        {
                            var b = Data.Book(p.BookId);
                            if (b == null) return null;
                            bool on = favs.Contains(b.id);
                            var c = Cover(b, 84).W("100%").Opacity(on ? 1f : 0.45f);
                            // outline: 2px solid var(--accent); USS has no outline, so the cover gets a border.
                            if (on) c.Border(2, "accent");
                            return Btn("button", () =>
                            {
                                if (on) favs.Remove(b.id);
                                else if (favs.Count < 4) favs.Add(b.id);
                                else { Toast("You already have 4. Remove one first."); return; }
                                Save();
                            }, c);
                        })),
                        fins.Count == 0 ? El("p.pad.small.faint", "Books you finish show up here.") : null,
                    };
                    break;
                }

                case "privacy":
                    body = new object[]
                    {
                        SettingsUI.Group("", new[]
                        {
                            Row("Show what I'm reading", sub: "Your current book and page, for your friends", control: SwitchEl(st.ShowCurrent, v => { st.ShowCurrent = v; Data.Save(); })),
                            Row("Hide spoilers in reviews", control: SwitchEl(st.Spoilers, v => { st.Spoilers = v; Data.Save(); })),
                        }),
                        El("div.grouphead", "Who sees your activity"),
                        El("div.list", new[] { "Friends and followers", "Only me" }.Select(o =>
                            Row(o, noChev: true, control: (st.ActivityVisibility == "Only me") == (o == "Only me") ? Icon("check") : null, onClick: () => { st.ActivityVisibility = o; Save(); }))),
                        El("p.groupfoot", "Your diary stays private. Friends see your current book, page, momentum level, finished books and favorites. Your private notes and reflections are never shared. Margin doesn't sell your data or use your writing to train AI models."),
                    };
                    break;

                case "reading":
                    body = new object[]
                    {
                        El("div.grouphead", "Pages a day"),
                        El("div.row.wrap.gap8.pad", new[] { 5, 10, 15, 20, 30, 50 }.Select(n => Btn("button.chip" + (g.dailyPages == n ? ".on" : ""), () => { g.dailyPages = n; Save(); }, n))),
                        El("div.grouphead", "Reading days"),
                        El("div.row.gap6.pad", new[] { 1, 2, 3, 4, 5, 6, 0 }.Select(d => Btn("button.chip.day" + (g.readingDays.Contains(d) ? ".on" : ""), () =>
                        {
                            if (g.readingDays.Contains(d))
                            {
                                if (g.readingDays.Count == 1) { Toast("You need at least one reading day."); return; }
                                g.readingDays.Remove(d);
                            }
                            else g.readingDays.Add(d);
                            Save();
                        }, Aria(Fmt.Days[d]), Copy.DayLetters[d]))),
                        El("p.groupfoot", $"{F.Pages(g.WeeklyTarget)} a week, {Fmt.DayList(g.readingDays)}."),
                        El("div.grouphead", $"Book goal for {Clock.Today.Year}"),
                        El("div.row.wrap.gap8.pad", new[] { 4, 6, 12, 24, 52 }.Select(n => Btn("button.chip" + (Data.X.yearGoal == n ? ".on" : ""), () => { Data.X.yearGoal = n; Save(); }, n))),
                        SettingsUI.Group("", new[]
                        {
                            Row("Count rereads", control: SwitchEl(st.CountRereads, v => { st.CountRereads = v; Data.Save(); })),
                            Row("Favorite shelf", value: Data.Catalog.FindShelf(g.favoriteShelfId)?.name ?? "None", onClick: () => Nav.Push(new ChoiceScreen("Favorite shelf",
                                new[] { ("", "None") }.Concat(Data.Shelves.Select(x => (x.id, x.name))).ToList(),
                                () => g.favoriteShelfId ?? "", v => { g.favoriteShelfId = string.IsNullOrEmpty(v) ? null : v; }, "It gets priority in your recommendations."))),
                        }),
                    };
                    break;

                case "reminders":
                {
                    void Shift(int minutes)
                    {
                        int tm = (g.reminderHour * 60 + g.reminderMinute + minutes) % 1440;
                        g.reminderHour = tm / 60; g.reminderMinute = tm % 60;
                        Save();
                    }
                    body = new object[]
                    {
                        SettingsUI.Group("", new[] { Row("Reminders", control: SwitchEl(g.remindersEnabled, v => { g.remindersEnabled = v; Save(); })) }),
                        g.remindersEnabled ? new object[]
                        {
                            El("div.grouphead", "Time"),
                            El("div.stepper.pad",
                                Btn("button.circle", () => Shift(1410), Aria("Half an hour earlier"), "−"),
                                El("div.bignum", Fmt.Time(g.reminderHour, g.reminderMinute)).FontSize(40),
                                Btn("button.circle", () => Shift(30), Aria("Half an hour later"), "+")),
                            SettingsUI.Group("", new[]
                            {
                                Row("When I…", value: !string.IsNullOrEmpty(g.intention) ? g.intention : "Not set", onClick: () => Nav.Push(new EditField("When I…", g.intention ?? "", _ => null,
                                    v => { g.intention = string.IsNullOrWhiteSpace(v) ? null : v.Trim(); return Done(); }, "It shows as the reminder title: “When I finish dinner…”."))),
                                Row("Include a quote from the book", control: SwitchEl(st.QuotesInReminders, v => { st.QuotesInReminders = v; Data.Save(); })),
                                Row("Weekly summary on Sundays", control: SwitchEl(st.WeeklySummary, v => { st.WeeklySummary = v; Data.Save(); })),
                            }, $"Up to {ReminderPlanner.WeeklyBudget} a week. If you ignore 3 in a row, we drop to {ReminderPlanner.WeeklyBudget / 2}. They never come if you already logged that day."),
                            SettingsUI.Group("", new[] { Row("Try a reminder", noChev: true, onClick: App.DemoBanner) }),
                        } : null,
                    };
                    break;
                }

                case "focus":
                    body = new object[]
                    {
                        El("div.grouphead", "Default length"),
                        El("div.list", new[] { 15, 25, 45, 60 }.Select(m => Row($"{m} minutes", noChev: true, control: st.FocusMinutes == m ? Icon("check") : null, onClick: () => { st.FocusMinutes = m; Save(); }))),
                        SettingsUI.Group("", new[] { Row("Silenced apps", value: st.ScreenTime ? st.Silenced.Count.ToString() : "No permission", onClick: () => Nav.Push(new AppPickerScreen())) }),
                    };
                    break;

                case "appearance":
                    body = new object[]
                    {
                        El("div.grouphead", "Theme"),
                        El("div.list", new[] { "System", "Light", "Dark" }.Select(o => Row(o, noChev: true, control: st.Theme == o ? Icon("check") : null,
                            onClick: () => { st.Theme = o; Data.Save(); App.ApplySettings(); Nav.Refresh(); }))),
                        SettingsUI.Group("Accessibility", new[]
                        {
                            Row("Larger text", control: SwitchEl(st.TextLarge, v => { st.TextLarge = v; Data.Save(); App.ApplySettings(); })),
                            Row("Reduce motion", control: SwitchEl(st.ReduceMotion, v => { st.ReduceMotion = v; Data.Save(); App.ApplySettings(); })),
                        }),
                    };
                    break;
            }
            return El("div", Navbar(T.TryGetValue(key, out var title) ? title : ""), El("div.scroll.under-nav", body, El("div.bottom-space")));
        }
    }

    public class ChoiceScreen : WebScreen
    {
        public string title, foot;
        public List<(string value, string label)> options;
        public Func<string> get;
        public Action<string> set;

        public ChoiceScreen(string title, List<(string value, string label)> options, Func<string> get, Action<string> set, string foot = null)
        {
            this.title = title; this.options = options; this.get = get; this.set = set; this.foot = foot;
        }

        public override VisualElement Build() =>
            El("div", Navbar(title), El("div.scroll.under-nav", SettingsUI.Group("", options.Select(o =>
                SettingsUI.Row(o.label, noChev: true, control: get() == o.value ? Icon("check") : null, onClick: () => { set(o.value); Data.Save(); Nav.Back(); })), foot)));
    }

    public class EditField : WebScreen
    {
        public string title, value, foot;
        /// <summary>Returns an error message or null.</summary>
        public Func<string, string> validate;
        /// <summary>Saves the value; returns an error message or null.</summary>
        public Func<string, Task<string>> apply;
        string err;

        public EditField(string title, string value, Func<string, string> validate, Func<string, Task<string>> apply, string foot = null)
        {
            this.title = title; this.value = value ?? ""; this.validate = validate; this.apply = apply; this.foot = foot;
        }

        async void SaveNow()
        {
            err = validate(value.Trim());
            if (err != null) { Nav.Refresh(); return; }
            string r;
            try { r = await apply(value.Trim()); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[Margin] " + e.Message); r = "Couldn't save. Try again."; }
            if (r != null) { err = r; if (IsCurrent) Nav.Refresh(); return; }
            Data.Save();
            if (IsCurrent) Nav.Back();
        }

        public override VisualElement Build()
        {
            var i = Input("input.input" + (err != null ? ".bad" : ""), value, v => value = v, onEnter: SaveNow);
            i.schedule.Execute(() => i.Focus()).StartingIn(200);
            return El("div", Navbar(title, new NavOpts { Right = Btn("button.textbtn", SaveNow, "Save").Font(false, 600) }),
                El("div.scroll.under-nav", El("div.pad.mt12", i, El("div.help.bad", err ?? ""), foot != null ? El("p.small.faint.mt8", foot) : null)));
        }
    }

    public class PasswordScreen : WebScreen
    {
        string cur = "", n1 = "", n2 = "", err;

        async void Change()
        {
            if (n1.Length < 8 || !n1.Any(char.IsDigit)) err = "The new one needs 8 characters and at least one number.";
            else if (n1 != n2) err = "The two new passwords don't match.";
            else if (!await Net.CheckPassword(cur)) err = "The current password doesn't match.";
            else
            {
                err = await Net.SetPassword(n1);
                if (err == null) { if (IsCurrent) Nav.Back(); Toast("Password updated"); return; }
            }
            if (IsCurrent) Nav.Refresh();
        }

        public override VisualElement Build()
        {
            var a = Accounts.Current;
            TextField Mk(string v, Action<string> set, string ph) => Input("input.input.mt8", v, set, placeholder: ph, password: true);
            return El("div", Navbar("Password"), El("div.scroll.under-nav", El("div.pad.mt12",
                Mk(cur, v => cur = v, "Current password"), Mk(n1, v => n1 = v, "New password"), Mk(n2, v => n2 = v, "Repeat the new password"),
                El("div.help.bad", err ?? ""),
                Btn("button.btn.primary.mt12", Change, "Change password"),
                Btn("button.btn.ghost.mt4", () => App.Run(async () => { var e = await Net.ResetPassword(a?.Email); Toast(e ?? $"We sent a link to {a?.Email}"); }), "I don't remember my current one"))));
        }
    }

    /// <summary>Deletes the account for good with Net.DeleteAccount() (server function delete_my_account).</summary>
    public class DeleteAccountScreen : WebScreen
    {
        public override VisualElement Build()
        {
            var a = Accounts.Current;
            if (a == null) return El("div", Navbar("Delete account"));
            var s = a.State;
            var username = a.Username;
            Label btn = null;
            btn = (Label)Btn("button.btn.mt16", () => App.Run(async () =>
            {
                btn.SetEnabled(false); btn.text = "Deleting…";
                var email = a.Email;
                bool ok = await Net.DeleteAccount();
                if (!ok) { btn.SetEnabled(true); btn.text = "Delete my account"; return; }
                App.Start();
                IosAlert("Your account was deleted", string.IsNullOrEmpty(email)
                    ? "Your guest diary, friends, posts, reviews and activity are gone."
                    : $"Your account ({email}), diary, friends, posts, reviews and activity are gone. You can sign up again with the same email whenever you want.",
                    Act("OK", bold: true));
            }), "Delete my account").Bg("error").Fg("#fff");
            btn.SetEnabled(false);
            var i = Input("input.input.mt8", "", v => btn.SetEnabled(v.Trim() == username), placeholder: username);
            return El("div", Navbar("Delete account"), El("div.scroll.under-nav", El("div.pad.mt8",
                El("div.h2", "This deletes everything"),
                El("p.mt8", $"{F.N(s.logs.Count)} check-ins, {F.Books(Data.Finished(s).Count)} finished, {Data.X.lists.Count} lists, your friends, your posts and your reviews will be deleted. This can't be undone."),
                El("p.small.muted.mt8", "Your login (email and password) is deleted too. You won't be able to log in with it again."),
                Btn("button.btn.secondary.mt16", SettingsUI.ExportCsv, Icon("download"), "Download my data first"),
                El("p.small.muted.mt24", "To confirm, type your username: ", El("b", username)), i, btn)));
        }
    }

    public class HelpScreen : WebScreen
    {
        static readonly (string q, string a)[] QA =
        {
            ("Why do I log the page and not how many pages I read?", "Because you see the page number in the book and don't have to subtract. The app works out how far you got."),
            ("What is reading momentum?", "An average of your last few days. It goes up when you meet your daily goal and drops little by little if you skip a reading day. Planned rest days don't count."),
            ("Who writes the quizzes?", "An AI writes them from the chapter text, and a person on the team checks them before they go out. Reviews, on the other hand, are written by readers."),
            ("Can I use Margin with ebooks?", "Yes, though the app is made for paper. You log the page the same way."),
            ("What if I don't read for a week?", "Nothing breaks. Your best stays and your momentum drops. When you come back, it goes up again."),
        };

        public override VisualElement Build() =>
            El("div", Navbar("Help center"), El("div.scroll.under-nav", QA.Select(x =>
            {
                // <details>/<summary>: the question toggles the answer.
                var ans = El("p.small.muted.mt8", x.a).Show(false);
                return El("div.pad", Btn("button.strong", () => ans.Show(ans.style.display == DisplayStyle.None), x.q).TextLeft(), ans).P(14, 16).BorderBottom();
            })));
    }

    public class LegalScreen : WebScreen
    {
        public override VisualElement Build() =>
            El("div", Navbar("Terms and privacy"), El("div.scroll.under-nav", El("div.pad",
                El("div.h3.mt8", "Privacy notice"), El("p.small.mt6", "Margin stores your diary, your ratings and your writing to show them to you and, if your profile is public, so other readers can see them. We don't sell personal data or use your writing to train artificial intelligence models. You can download or delete your data from Settings."),
                El("div.h3.mt20", "Terms of use"), El("p.small.mt6", "What you write is yours. When you post it, you give us permission to show it in the app. Don't post reviews written by other people or generated by AI as if they were yours."),
                El("div.h3.mt20", "Licenses"), El("p.small.mt6", "EB Garamond and Inter: SIL Open Font License 1.1. Icons: Lucide (ISC). Covers: Open Library Covers API. Reviews quoted from Goodreads with their author and link."),
                El("p.xs.faint.mt20", "Academic prototype. Universidad Panamericana, Human Computer Interaction, 2026."))));
    }

    // ---------------- screens-social.js ----------------

    public class FriendsScreen : WebScreen
    {
        public FriendsScreen() { Live = true; }

        public override VisualElement Build()
        {
            var acc = Accounts.Current;
            var link = SocialUI.InviteLink();
            var invite = El("div.card.block",
                El("div.row.gap12", El("div.grow", El("div.strong", "Invite a friend"), El("div.small.muted.mt2", "Share your link. When they open it and sign up, they land on your profile."),
                    El("div.xs.faint.mt6", SocialUI.BreakAll(link))),
                    Btn("button", InviteSheet.Open, Aria("Show QR code"), SocialUI.QrImage(link, 72))),
                El("div.row.gap8.mt12", Btn("button.btn.sm.primary", InviteSheet.Share, Icon("share"), "Share link").Flex1(), Btn("button.btn.sm.secondary", InviteSheet.Open, "QR code")))
                .M(8, 16, 0);
            return El("div", Navbar("Friends", new NavOpts { Right = Btn("button.iconbtn", () => Nav.Push(new FindFriendsScreen()), Aria("Find friends"), Icon("search")) }),
                El("div.scroll.under-nav",
                    El("div.pad.mt4", Btn("button.input.row.gap8", () => Nav.Push(new FindFriendsScreen()), Icon("search"), El("span", "Search by @username or name"))
                        .W("100%").TextLeft().Fg("ink-3").Hgt(42).Radius(10).AlignItems(Align.Center)),
                    Social.Incoming.Count > 0 ? new object[] { Sect("Requests", F.N(Social.Incoming.Count)), Social.Incoming.Select(SocialUI.RequestRow) } : null,
                    Sect("Your friends", Social.Friends.Count > 0 ? F.N(Social.Friends.Count) : null),
                    !Social.Loaded ? El("p.pad.small.faint", "Loading…")
                        : Social.Friends.Count > 0 ? (object)Social.Friends.Select(x => SocialUI.PersonRow(x.Person, noButton: true)).ToList() : null,
                    invite,
                    Social.Outgoing.Count > 0 ? new object[] { Sect("Sent"), Social.Outgoing.Select(x => SocialUI.PersonRow(x.Person)) } : null,
                    acc != null && !string.IsNullOrEmpty(acc.University) ? El("div.pad.mt16", Btn("button.btn.secondary", () => Nav.Push(new FindFriendsScreen()), $"People from {acc.University}")) : null,
                    El("div.bottom-space")));
        }
    }

    public class FindFriendsScreen : WebScreen
    {
        public string q = "";
        List<Profile> res, uni;
        bool busy;
        int seq;
        VisualElement box;

        public override void OnShow() => ScreenLoad(async () => { uni = await Social.SameUniversity(); });

        public override VisualElement Build()
        {
            var input = Input("input.input", q, v => { q = v; Search(); }, placeholder: "@username or name");
            input.tooltip = "Search readers";
            input.style.paddingLeft = 40; input.style.height = 42; input.Radius(10);
            input.schedule.Execute(() => input.Focus()).StartingIn(250);
            box = El("div");
            // The icon goes after the input: in USS later siblings draw on top (the web uses position: absolute).
            var ic = El("span", Icon("search")).Abs(11, null, null, 28).Fg("ink-3");
            ic.pickingMode = PickingMode.Ignore;
            var el = El("div", Navbar("Find friends"), El("div.scroll.under-nav",
                El("div.pad.mt4", input, ic).Relative(),
                box, El("div.bottom-space")));
            Draw();
            return el;
        }

        async void Search()
        {
            var query = q.Trim();
            int my = ++seq; // clearTimeout
            if (CleanQuery(query).TrimStart('@').Length < 2) { res = null; busy = false; Draw(); return; }
            busy = true; Draw();
            await UnityEngine.Awaitable.WaitForSecondsAsync(0.25f); // runs on the main loop, also in WebGL (no threads)
            if (my != seq) return;
            try
            {
                var r = await Social.Search(query);
                if (my == seq && q.Trim() == query) { res = r; busy = false; Draw(); }
            }
            catch (Exception e) { busy = false; NetFail(e); }
        }

        /// <summary>Re-renders the results in place (keeps the input focused).</summary>
        public void Draw()
        {
            if (box == null) return;
            var acc = Accounts.Current;
            var u = acc?.University;
            var list = uni ?? new List<Profile>();
            SetKids(box,
                res != null ? new object[]
                {
                    Sect("Results"),
                    res.Count > 0 ? (object)res.Select(p => SocialUI.PersonRow(p)).ToList()
                        : El("div.empty", El("div.h3", $"No reader called “{q.Trim()}”"), El("p.small", "Check the spelling, or send them your invite link."),
                            Btn("button.btn.sm.primary.mt12", InviteSheet.Share, "Share invite link").AlignSelf(Align.Center)),
                } : busy ? El("p.pad.small.faint", "Searching…") : null,
                res == null ? new object[]
                {
                    Sect(!string.IsNullOrEmpty(u) ? $"From {u}" : "Suggestions"),
                    string.IsNullOrEmpty(u) ? El("p.pad.small.muted", "Add your university in Settings › Account and we'll suggest people from there.")
                        : !Loaded ? El("p.pad.small.faint", "Loading…")
                        : list.Count > 0 ? (object)list.Select(p => SocialUI.PersonRow(p, uni: true)).ToList()
                        : El("p.pad.small.muted", "Nobody else from there yet. Share your invite link with classmates."),
                } : null);
        }

        public override void OnHide() { seq++; }
    }

    /// <summary>The invite sheet with the QR code, and the share action.</summary>
    public static class InviteSheet
    {
        public static void Open()
        {
            var link = SocialUI.InviteLink();
            var u = Accounts.Session();
            Sheet.Open("Invite", El("div.col",
                El("div.small.muted", "Scan to add"), El("div.h3.mt2", $"@{u}"), El("div.mt12", SocialUI.QrImage(link, 220)),
                El("div.xs.faint.mt12", SocialUI.BreakAll(link)),
                El("div.row.gap8.mt16", Btn("button.btn.primary", Share, Icon("share"), "Share").Flex1(), Btn("button.btn.secondary", () => SocialUI.CopyText(link), Icon("link"), "Copy").Flex1()).W("100%"))
                .AlignItems(Align.Center).TextCenter());
        }

        /// <summary>navigator.share needs a native plugin on iOS; like the web without it, the link is copied.</summary>
        public static void Share() => SocialUI.CopyText(SocialUI.InviteLink(), "Invite link copied");
    }

    /// <summary>Another reader's profile. Accepts a profile row or a username (invite links, reviews).</summary>
    public class UserScreen : WebScreen
    {
        public string username;
        public string uid;

        bool found;
        Profile prof;
        List<Activity> act = new List<Activity>();
        List<Post> posts = new List<Post>();

        public UserScreen(Profile p) { Live = true; username = p?.username; uid = p?.id; }
        public UserScreen(string username) { Live = true; this.username = username; }

        public override void OnShow() => ScreenLoad(async () =>
        {
            Profile p;
            if (uid != null)
            {
                var x = await Social.ById(uid);
                p = x != null && !string.IsNullOrEmpty(x.username) ? await Social.ByUsername(x.username) : x;
            }
            else p = await Social.ByUsername(username);
            if (p == null) { found = false; return; }
            uid = p.id;
            var r = Social.Rel(p.id);
            bool canSee = r.Kind == RelationKind.Friend || Social.FollowingIds.Contains(p.id);
            var ta = canSee ? Social.ActivityOf(p.id, 12) : Task.FromResult(new List<Activity>());
            var tp = p.is_creator ? Social.Posts(author: p.id, limit: 20) : Task.FromResult(new List<Post>());
            await Task.WhenAll(ta, tp);
            prof = p; act = ta.Result ?? new List<Activity>(); posts = tp.Result ?? new List<Post>(); found = true;
        });

        public override VisualElement Build()
        {
            if (!Loaded || Err != null) return El("div", Navbar(!string.IsNullOrEmpty(username) ? "@" + username : ""), El("p.pad.small.faint.mt12", Err ?? "Loading…"));
            if (!found)
                return El("div", Navbar(""), El("div.empty", El("div.h3", $"We couldn't find @{username}"), El("p.small", "The link may be old, or they changed their username."),
                    Btn("button.btn.sm.primary.mt12", () => Nav.Replace(new FindFriendsScreen()), "Search readers").AlignSelf(Align.Center)).Pt(120));

            var p = Social.Profiles.TryGetValue(prof.id, out var fresh) && fresh != null ? fresh : prof;
            var r = Social.Rel(p.id);
            bool friend = r.Kind == RelationKind.Friend;
            if (r.Kind == RelationKind.Self) return new ProfileScreen().Build();
            var cur = Data.Book(p.current_book_id);
            var favs = (p.favorites ?? new List<string>()).Select(Data.Book).Where(b => b != null).ToList();
            var read = (p.books_read ?? new List<string>()).Select(Data.Book).Where(b => b != null).ToList();
            var joined = SocialUI.Local(p.created_at);

            var header = El("div.col.pad", Avatar(Pav(p), 88), El("div.h2.mt12", Pname(p)),
                El("div.small.muted.mt2", $"@{p.username}", El("span.dotsep"), $"on Margin since {F.MES[joined.Month - 1]} {joined.Year}"),
                !string.IsNullOrEmpty(p.university) ? El("div.xs.faint.mt2", p.university) : null,
                p.is_creator ? El("div.mt6", El("span.badge.ok", "Creator")) : null,
                !string.IsNullOrEmpty(p.bio) ? El("p.small.mt8", p.bio).MaxW(300) : null,
                El("div.row.gap8.mt12", SocialUI.RelButton(p), SocialUI.FollowButton(p)))
                .AlignItems(Align.Center).TextCenter();

            var pact = friend ? Social.Friends.FirstOrDefault(x => x.Person?.id == p.id) : null;
            object pactSect = friend ? new object[]
            {
                Sect("Reading together"),
                pact != null && Data.Book(pact.Row.shared_book_id) != null
                    ? (object)new object[]
                    {
                        MainUI.PactCard(pact),
                        El("div.row.gap8.pad", Btn("button.btn.sm.secondary", () => PactSheet.Open(pact), "Change book"),
                            Btn("button.btn.sm.ghost", () => IosAlert("Stop reading together?", $"{Pfirst(p)} will see the shared book removed too.",
                                Act("Cancel"), Act("Stop", () => App.Run(Social.SetPact(pact.Row, null)), red: true)), "Stop")),
                    }
                    : El("div.pad", El("p.small.muted", "Pick a book you'll both read. You'll each see the other's page."),
                        Btn("button.btn.sm.primary.mt8", () => { if (pact != null) PactSheet.Open(pact); }, Icon("bookopen"), "Read a book together")),
            } : null;

            object status = friend ? new object[]
            {
                Sect("Now"),
                El("div.statgrid",
                    El("div", El("div.v", !string.IsNullOrEmpty(p.momentum_level) ? p.momentum_level : "–"), El("div.xs.muted", "reading momentum")),
                    El("div", El("div.v", read.Count), El("div.xs.muted", read.Count == 1 ? "book finished" : "books finished"))),
                cur != null
                    ? Btn("button.row.gap12.pad.mt12", () => Nav.Push(new BookScreen(cur.id)), Cover(cur, 44),
                        El("div.grow", El("div.xs.faint", "Reading"), El("div.strong", cur.title), El("div.mt6", Bar((p.current_page ?? 0) / (double)Math.Max(1, cur.pages))),
                            El("div.xs.faint.mt4.num", $"Page {p.current_page ?? 0} of {cur.pages}"))).W("100%").TextLeft()
                    : El("p.pad.small.faint.mt8", "Not reading anything right now."),
            }
            : r.Kind == RelationKind.None || r.Kind == RelationKind.Outgoing
                ? El("div.card.block.empty", El("div.h3", r.Kind == RelationKind.Outgoing ? "Request sent" : "Read together"),
                    El("p.small", r.Kind == RelationKind.Outgoing ? $"When {Pfirst(p)} accepts, you'll see each other's current book and page." : $"Add {Pfirst(p)} as a friend to see what they're reading and share a book."))
                    .M(8, 16, 0)
                : null;

            var acts = act.Select(x => MainUI.ActivityItem(x, noUser: true, user: p)).Where(x => x != null).ToList();
            return El("div", Navbar(Pfirst(p)),
                El("div.scroll.under-nav", header,
                    status, pactSect,
                    p.is_creator ? new object[] { Sect("Posts", posts.Count > 0 ? F.N(posts.Count) : null), posts.Count > 0 ? (object)posts.Select(SocialUI.PostRow).ToList() : El("p.pad.small.muted", "No posts yet.") } : null,
                    friend && favs.Count > 0 ? new object[] { Sect("Favorite books"), El("div.grid4.pad", favs.Select(b => Cover(b, 84, () => Nav.Push(new BookScreen(b.id))).W("100%"))) } : null,
                    friend && read.Count > 0 ? new object[] { Sect("Finished"), El("div.hscroll", read.Take(16).Select(b => Cover(b, 72, () => Nav.Push(new BookScreen(b.id))))) } : null,
                    act.Count > 0 ? new object[] { Sect("Recent activity"), El("div", acts).BorderTop() } : null,
                    El("div.bottom-space")));
        }
    }

    /// <summary>Pick the book to read together with a friend.</summary>
    public static class PactSheet
    {
        public static void Open(Connection pact)
        {
            if (pact == null) return;
            var cur = pact.Row.shared_book_id;
            var mine = Data.State.profile.currentBookId;
            var theirs = pact.Person?.current_book_id;
            // Stable sort: my book first, then theirs, then the catalog order.
            var books = Data.Books.Select((b, i) => (b, i))
                .OrderByDescending(x => x.b.id == mine).ThenByDescending(x => x.b.id == theirs).ThenBy(x => x.i).Select(x => x.b).ToList();
            Sheet.Open("Read together", El("div",
                El("p.small.muted", $"Pick the book you and {Pfirst(pact.Person)} will read. Each of you logs pages as usual."),
                El("div.grid3.mt12", books.Select(b =>
                {
                    var c = Cover(b, 110).W("100%");
                    // outline: 3px solid var(--accent)
                    if (cur == b.id) c.Border(3, "accent");
                    return Btn("button.col.gap4", () => App.Run(async () =>
                        {
                            Sheet.Close();
                            await Social.SetPact(pact.Row, b.id);
                            Toast($"Reading {b.title} with {Pfirst(pact.Person)}");
                        }),
                        c,
                        El("div.xs.clamp2" + (cur == b.id ? ".strong" : ".muted"), b.title),
                        b.id == mine ? El("div.xs.accent", "You're reading it") : b.id == theirs ? El("div.xs.accent", $"{Pfirst(pact.Person)} is reading it") : null)
                        .TextLeft();
                }))));
        }
    }

    public class PostScreen : WebScreen
    {
        public long id;
        Post p;

        public PostScreen(long id) { this.id = id; }

        public override void OnShow() => ScreenLoad(async () => { p = await Social.Post(id); });

        public override VisualElement Build()
        {
            if (p == null)
                return El("div", Navbar(""), Loaded && Err == null
                    ? El("div.empty", El("div.h3", "This post is gone"), El("p.small", "The creator may have deleted it.")).Pt(120)
                    : El("p.pad.small.faint.mt12", Err ?? "Loading…"));
            var a = p.profiles ?? new AuthorRef();
            bool mine = p.author == Net.Uid;
            var vid = p.kind == "video" ? SocialUI.YoutubeId(p.video_url) : null;
            var authorP = Social.Profiles.TryGetValue(p.author ?? "", out var known) && known != null ? known
                : new Profile { id = p.author, username = a.username, name = a.name, color = a.color, is_creator = true };
            var post = p;
            return El("div", Navbar(SocialUI.POST_KIND.TryGetValue(p.kind ?? "", out var kind) ? kind : "", new NavOpts
            {
                Right = mine ? Btn("button.iconbtn", () => IosAlert("Delete this post?", "Your followers won't see it anymore.", Act("Cancel"), Act("Delete", async () =>
                {
                    try { await Social.DeletePost(post.id); Nav.Back(); Toast("Post deleted"); }
                    catch (Exception e) { NetFail(e); }
                }, red: true)), Aria("Delete post"), Icon("trash")) : null,
            }),
                El("div.scroll.under-nav", El("div.pad",
                    El("div.h1", p.title ?? ""),
                    El("div.row.gap8.mt12", Btn("button.row.gap8.grow", () => { if (mine) Nav.Push(new ProfileScreen()); else Nav.Push(new UserScreen(authorP)); }, Avatar(Pav(a), 32),
                        El("div", El("div.small.strong", Pname(a)), El("div.xs.faint", $"@{a.username} · {F.Date(SocialUI.Local(p.created_at))}"))).TextLeft(), mine ? null : SocialUI.FollowButton(authorP)),
                    vid != null ? Video(vid) : null,
                    p.kind == "video" && vid == null ? El("p.small.faint.mt12", "This video link can't be shown.") : null,
                    !string.IsNullOrEmpty(p.body) ? El("p.serif.mt16", p.body) : null),
                    p.kind == "list" ? El("div.mt12", (p.items ?? new List<PostItem>()).Select((it, i) =>
                    {
                        var b = Data.Book(it.bookId);
                        if (b == null) return null;
                        return Btn("button.row.top.gap12.pad", () => Nav.Push(new BookScreen(b.id)), Cover(b, 56, rank: i + 1),
                            El("div.grow", El("div.strong", b.title), El("div.xs.muted", b.author), !string.IsNullOrEmpty(it.note) ? El("p.small.mt6", it.note) : null))
                            .W("100%").TextLeft().P(10, 16).BorderTop();
                    })) : null,
                    El("p.pad.xs.faint.mt12", "Written by a person on Margin. No like counts, no autoplay: read it and go read your book."),
                    El("div.bottom-space")));
        }

        /// <summary>
        /// The web embeds the video in an iframe; UI Toolkit can't, so a 16:9 card with the video's thumbnail
        /// opens it in YouTube (still never on its own).
        /// </summary>
        static VisualElement Video(string vid)
        {
            var box = Btn("div.mt16", () => App.OpenUrl("https://www.youtube.com/watch?v=" + vid),
                El("div.col", Icon("play").Size(44).Tint("#fff"), El("div.small.mt6", "Watch on YouTube").Fg("#fff")).Inset(0).PlaceCenter())
                .Relative().Radius(12).Clip().Bg("#000");
            box.tooltip = "Play video";
            Layout.AspectRatio(box, 0.5625f);
            LoadThumb(box, vid);
            return box;
        }

        static async void LoadThumb(VisualElement el, string vid)
        {
            try
            {
                using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture($"https://i.ytimg.com/vi/{vid}/hqdefault.jpg"))
                {
                    var op = req.SendWebRequest();
                    while (!op.isDone) await Task.Yield();
                    if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) return;
                    var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                    el.style.backgroundImage = new StyleBackground(tex);
                    el.style.backgroundPositionX = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(UnityEngine.ScaleMode.ScaleAndCrop);
                    el.style.backgroundPositionY = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(UnityEngine.ScaleMode.ScaleAndCrop);
                    el.style.backgroundRepeat = BackgroundPropertyHelper.ConvertScaleModeToBackgroundRepeat(UnityEngine.ScaleMode.ScaleAndCrop);
                    el.style.backgroundSize = BackgroundPropertyHelper.ConvertScaleModeToBackgroundSize(UnityEngine.ScaleMode.ScaleAndCrop);
                }
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[Margin] " + e.Message); }
        }
    }

    /// <summary>What PostEditor can start with (the web's prefill: { kind, title, body, items }).</summary>
    public class PostDraft
    {
        public string kind = "list", title = "", body = "", video = "";
        public List<PostItem> items = new List<PostItem>();
    }

    public class PostEditor : WebScreen
    {
        public PostDraft v;
        string err;
        bool busy;

        public PostEditor(PostDraft prefill = null) { v = prefill ?? new PostDraft(); v.video = ""; v.items = v.items ?? new List<PostItem>(); }

        public override VisualElement Build()
        {
            if (Accounts.Current == null || !Accounts.Current.IsCreator)
                return El("div", Navbar("New post"), El("div.empty", El("div.h3", "Creators only"), El("p.small", "Turn on Creator account in Settings › Profile to publish."),
                    Btn("button.btn.sm.primary.mt12", () => Nav.Replace(new SettingsDetail("profile")), "Open Settings").AlignSelf(Align.Center)).Pt(120));

            var items = v.items.Select((it, i) =>
            {
                var b = Data.Book(it.bookId);
                if (b == null) return null;
                var note = SocialUI.InputFont(Input("input.input.mt6", it.note ?? "", x => it.note = x, placeholder: "A short note: why this one? (optional)", maxLength: 200), 14);
                note.style.height = 38; note.style.fontSize = 14;
                return El("div.row.top.gap10", Cover(b, 44),
                    El("div.grow", El("div.small.strong", $"{i + 1}. {b.title}"), note),
                    Btn("button.iconbtn", () => { v.items.Remove(it); Nav.Refresh(); }, Aria($"Remove {b.title}"), Icon("x")))
                    .P(10, 0).BorderTop();
            }).ToList();

            var title = Input("input.input", v.title, x => v.title = x, maxLength: 120,
                placeholder: v.kind == "list" ? "Short books for a long semester" : v.kind == "video" ? "What I read in October" : "Why I reread The Little Prince");
            var body = TextArea("textarea.input", v.body, x => v.body = x, placeholder: v.kind == "text" ? "Write it in your own words." : "", maxLength: 8000);
            body.style.minHeight = v.kind == "text" ? 200 : 90;

            return El("div", Navbar("New post", new NavOpts
            {
                Right = Btn("button.textbtn", Publish, Disabled(busy), busy ? "…" : "Publish").Fg("link").Font(false, 600).Hgt(44).P(0, 10),
            }),
                El("div.scroll.under-nav", El("div.pad.mt4",
                    El("div.seg", new[] { "list", "text", "video" }.Select(k => Btn("button" + (v.kind == k ? ".on" : ""), () => { v.kind = k; err = null; Nav.Refresh(); }, SocialUI.POST_KIND[k]))),
                    AuthUI.Field("Title", title),
                    v.kind == "video" ? AuthUI.Field("YouTube link", Input("input.input", v.video, x => v.video = x, placeholder: "https://youtu.be/…", keyboard: UnityEngine.TouchScreenKeyboardType.URL),
                        El("div.help", "Only YouTube links. It plays inside the post, never on its own.")) : null,
                    AuthUI.Field(v.kind == "list" ? "Intro (optional)" : v.kind == "video" ? "Text (optional)" : "Text", body),
                    v.kind == "list" ? new object[]
                    {
                        El("div.row.mt20", El("span.caps.grow", $"Books ({v.items.Count})"), Btn("button.btn.sm.secondary", PickBooks, Icon("plus"), "Add books")),
                        El("div.mt8", items),
                        v.items.Count == 0 ? El("p.small.faint.mt8", "Pick books from the catalog. Add a note to each one if you like.") : null,
                    } : null,
                    El("div.help.bad.mt12", err ?? ""),
                    El("p.xs.faint.mt12", "Public to every Margin reader. Your followers see it in their feed. Write it yourself: no AI text."),
                    El("div.bottom-space"))));
        }

        void PickBooks()
        {
            var sel = new HashSet<string>(v.items.Select(i => i.bookId));
            VisualElement Grid() => El("div.grid3.mt8", Data.Books.Select(b =>
            {
                var c = Cover(b, 110).W("100%");
                if (sel.Contains(b.id)) c.Border(3, "accent"); // outline: 3px solid var(--accent)
                return Btn("button.col.gap4", () =>
                {
                    if (sel.Contains(b.id)) { sel.Remove(b.id); v.items = v.items.Where(i => i.bookId != b.id).ToList(); }
                    else if (v.items.Count >= 20) { Toast("20 books at most."); return; }
                    else { sel.Add(b.id); v.items.Add(new PostItem { bookId = b.id, note = "" }); }
                    if (Sheet.Cur != null) SetKids(Sheet.Cur.Body, Grid());
                }, c, El("div.xs.clamp2" + (sel.Contains(b.id) ? ".strong" : ".muted"), b.title)).TextLeft();
            }));
            Sheet.Open("Add books", Grid(), new SheetOpts
            {
                Right = Btn("button.textbtn", () => Sheet.Close(), "Done").Fg("link").Font(false, 600).Hgt(44).P(0, 10),
                OnClose = Nav.Refresh,
            });
        }

        async void Publish()
        {
            var title = v.title.Trim();
            var body = v.body.Trim();
            err = title.Length == 0 ? "Give it a title."
                : v.kind == "text" && body.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length < 5 ? "Write a few more words."
                : v.kind == "video" && SocialUI.YoutubeId(v.video) == null ? "Paste a YouTube link (youtube.com or youtu.be)."
                : v.kind == "list" && v.items.Count == 0 ? "Add at least one book."
                : null;
            if (err != null) { Nav.Refresh(); return; }
            var items = v.kind == "list" ? v.items.Select(i => { var n = (i.note ?? "").Trim(); return new PostItem { bookId = i.bookId, note = n.Length > 200 ? n.Substring(0, 200) : n }; }).ToList() : new List<PostItem>();
            busy = true; Nav.Refresh();
            try
            {
                var p = await Social.Publish(v.kind, title, body, v.kind == "video" ? v.video.Trim() : null, items);
                Toast("Published");
                if (IsCurrent) Nav.Replace(new PostScreen(p.id));
            }
            catch (Exception e)
            {
                busy = false;
                err = Regex.IsMatch(e.Message ?? "", "row-level security", RegexOptions.IgnoreCase) ? "Turn on Creator account first (Settings › Profile)." : "Couldn't publish. Check your connection.";
                if (IsCurrent) Nav.Refresh();
            }
        }
    }
}
