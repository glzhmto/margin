using System;

namespace Margin.Core
{
    /// <summary>How an activity row reads in a feed: "Ana read 12 pages of Aura · p. 40".</summary>
    public static class ActivityText
    {
        static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        /// <summary>
        /// The sentence after the reader's name, or null if the row can't be shown
        /// (unknown kind, or a book that is not in this catalog).
        /// </summary>
        public static string Describe(Activity a, Catalog catalog)
        {
            if (a == null) return null;
            var d = a.data ?? new ActivityData();
            var book = string.IsNullOrEmpty(a.book_id) ? null : catalog?.FindBook(a.book_id);
            switch (a.kind)
            {
                case ActivityKind.Pages:
                    if (book == null) return null;
                    int pages = Math.Max(0, (d.to ?? 0) - (d.from ?? 0));
                    return $"read {Pages(pages)} of {book.title} · p. {d.to ?? 0}";
                case ActivityKind.Finished:
                    return book == null ? null : $"finished {book.title}";
                case ActivityKind.Started:
                    return book == null ? null : $"started {book.title}";
                case ActivityKind.Quiz:
                    return book == null ? null : $"took the chapter {d.chapter ?? 0} quiz for {book.title}: {d.correct ?? 0} of {d.total ?? 0}";
                case ActivityKind.Review:
                    if (book == null) return null;
                    return d.rating.HasValue && d.rating.Value > 0 ? $"reviewed {book.title} · {Stars(d.rating.Value)}" : $"reviewed {book.title}";
                case ActivityKind.Post:
                    return $"published “{(string.IsNullOrWhiteSpace(d.title) ? "a post" : d.title)}”";
                case ActivityKind.Joined:
                    return "joined Margin";
                default:
                    return null;
            }
        }

        /// <summary>"★★★½" for 3.5 (ratings go from 0.5 to 5).</summary>
        public static string Stars(double rating)
        {
            var r = Math.Max(0, Math.Min(5, Math.Round(rating * 2) / 2));
            int full = (int)Math.Floor(r);
            return new string('★', full) + (r - full >= 0.5 ? "½" : "");
        }

        /// <summary>"now", "5 min", "3 h", "2 d", then "Nov 2". Same steps as the web.</summary>
        public static string TimeAgo(DateTime then, DateTime now)
        {
            int min = (int)Math.Round((now - then).TotalMinutes);
            if (min < 1) return "now";
            if (min < 60) return $"{min} min";
            int hrs = (int)Math.Round(min / 60.0);
            if (hrs < 24) return $"{hrs} h";
            int days = (int)Math.Round(hrs / 24.0);
            if (days < 7) return $"{days} d";
            return $"{Months[then.Month - 1]} {then.Day}";
        }

        static string Pages(int n) => n == 1 ? "1 page" : $"{n} pages";
    }
}
