// Transparent rule-based recommender (not a black box): every recommendation says why.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Margin.Core
{
    public class Recommendation
    {
        public Book Book;
        public double Score;
        public string Reason;
    }

    public static class Recommender
    {
        // Weights live in Tuning (tuning.json).
        static double ShelfWeight => Tuning.Current.shelfWeight;
        static double TagWeight => Tuning.Current.tagWeight;
        static double SeedShelfWeight => Tuning.Current.seedShelfWeight;   // shares a shelf with something you already read
        static double RatingBoost => Tuning.Current.ratingBoost;           // per star above 3 on the source book
        static double LowRatingPenalty => Tuning.Current.lowRatingPenalty; // if you gave it 1 or 2 stars
        static double ShortBookBonus => Tuning.Current.shortBookBonus;     // if the habit is just starting, suggest short books
        static int ShortBookPages => Tuning.Current.shortBookPages;

        public static List<Recommendation> Recommend(Catalog catalog, AppState state, MomentumLevel level, int count = 3)
        {
            var goals = state.profile.goals;
            var read = new HashSet<string>(state.progress.Where(p => p.finished).Select(p => p.bookId));

            // "Seed" books: finished ones or the current one, weighted by rating.
            var seeds = new List<(Book book, double weight)>();
            foreach (var p in state.progress)
            {
                var b = catalog.FindBook(p.bookId);
                if (b == null || !p.finished || p.bookId == state.profile.currentBookId) continue;
                double w = 1.0 + (p.rating > 3 ? (p.rating - 3) * RatingBoost : 0) - (p.rating > 0 && p.rating < 3 ? LowRatingPenalty : 0);
                seeds.Add((b, w));
            }
            var current = catalog.FindBook(state.profile.currentBookId ?? "");
            if (current != null) seeds.Add((current, 1.0));

            var results = new List<Recommendation>();
            foreach (var b in catalog.books)
            {
                if (read.Contains(b.id) || b.id == state.profile.currentBookId) continue;

                double score = 0;
                string reason = null;

                if (!string.IsNullOrEmpty(goals.favoriteShelfId) && b.shelves.Contains(goals.favoriteShelfId))
                {
                    score += ShelfWeight;
                    var shelf = catalog.FindShelf(goals.favoriteShelfId);
                    reason = $"From your favorite shelf: {shelf?.name ?? goals.favoriteShelfId}";
                }

                double bestSeed = 0;
                Book bestSeedBook = null;
                foreach (var (seed, weight) in seeds)
                {
                    double overlap = (seed.tags.Intersect(b.tags, StringComparer.OrdinalIgnoreCase).Count() * TagWeight
                                      + seed.shelves.Intersect(b.shelves).Count() * SeedShelfWeight) * weight;
                    if (overlap > bestSeed) { bestSeed = overlap; bestSeedBook = seed; }
                }
                if (bestSeedBook != null)
                {
                    score += bestSeed;
                    if (bestSeed >= ShelfWeight || reason == null)
                        reason = $"Shares themes with {bestSeedBook.title}";
                }

                if (level == MomentumLevel.Kindling && b.pages <= ShortBookPages)
                {
                    score += ShortBookBonus;
                    reason ??= "Short: a good one to start with";
                }

                results.Add(new Recommendation { Book = b, Score = score, Reason = reason ?? "A classic to discover" });
            }

            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Book.pages)
                .ThenBy(r => r.Book.id, StringComparer.Ordinal)
                .Take(count)
                .ToList();
        }
    }
}
