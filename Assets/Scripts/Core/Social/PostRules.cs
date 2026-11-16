using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Margin.Core
{
    /// <summary>What a creator's post needs before it can be published (same checks as the web and the database).</summary>
    public static class PostRules
    {
        public const int TitleMax = 120;
        public const int BodyMax = 8000;
        public const int NoteMax = 200;
        public const int ListMax = 20;
        public const int TextMinWords = 5;

        static readonly Regex YouTube = new Regex(
            @"^https://(?:www\.|m\.)?(?:youtube\.com/(?:watch\?(?:.*&)?v=|shorts/|embed/|live/)|youtu\.be/)([A-Za-z0-9_-]{11})");

        /// <summary>The 11-character video id of a YouTube link, or null.</summary>
        public static string YouTubeId(string url)
        {
            var m = YouTube.Match((url ?? "").Trim());
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>The database only accepts https://youtube.com/… or https://youtu.be/… (without "m.").</summary>
        public static string CanonicalVideoUrl(string url)
        {
            var id = YouTubeId(url);
            return id == null ? null : "https://www.youtube.com/watch?v=" + id;
        }

        public static string Label(string kind)
        {
            switch (kind)
            {
                case PostKind.List: return "List";
                case PostKind.Video: return "Video";
                default: return "Note";
            }
        }

        /// <summary>Null if the post can be published, otherwise what is missing.</summary>
        public static string Validate(string kind, string title, string body, string videoUrl, IList<PostItem> items)
        {
            var t = (title ?? "").Trim();
            var b = (body ?? "").Trim();
            if (t.Length == 0) return "Give it a title.";
            if (t.Length > TitleMax) return $"Keep the title under {TitleMax} characters.";
            if (b.Length > BodyMax) return "That text is too long.";
            switch (kind)
            {
                case PostKind.Text:
                    if (b.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length < TextMinWords) return "Write a few more words.";
                    return null;
                case PostKind.Video:
                    return YouTubeId(videoUrl) == null ? "Paste a YouTube link (youtube.com or youtu.be)." : null;
                case PostKind.List:
                    if (items == null || items.Count == 0) return "Add at least one book.";
                    if (items.Count > ListMax) return $"{ListMax} books at most.";
                    return null;
                default:
                    return "Pick a kind of post.";
            }
        }

        /// <summary>Trims the notes and drops repeated books, ready to be saved.</summary>
        public static List<PostItem> CleanItems(IEnumerable<PostItem> items)
        {
            var seen = new HashSet<string>();
            return (items ?? Enumerable.Empty<PostItem>())
                .Where(i => i != null && !string.IsNullOrEmpty(i.bookId) && seen.Add(i.bookId))
                .Select(i =>
                {
                    var note = (i.note ?? "").Trim();
                    return new PostItem { bookId = i.bookId, note = note.Length > NoteMax ? note.Substring(0, NoteMax) : note };
                })
                .ToList();
        }
    }
}
