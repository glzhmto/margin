using System;
using System.Collections.Generic;
using Margin.Core;

namespace Margin.Tests
{
    /// <summary>Minimal, deterministic data for the tests.</summary>
    internal static class TestData
    {
        // Monday, November 2, 2026.
        public static readonly DateTime Monday = new DateTime(2026, 11, 2);

        public static Quiz Quiz3() => new Quiz
        {
            questions = new List<QuizQuestion>
            {
                new QuizQuestion { prompt = "a", options = new List<string> { "x", "y" }, correctIndex = 0 },
                new QuizQuestion { prompt = "b", options = new List<string> { "x", "y" }, correctIndex = 1 },
                new QuizQuestion { prompt = "c", options = new List<string> { "x", "y" }, correctIndex = 1 },
            }
        };

        public static Catalog Catalog()
        {
            return new Catalog
            {
                shelves = new List<Shelf>
                {
                    new Shelf { id = "classics", name = "Classics" },
                    new Shelf { id = "mexican", name = "Mexican" },
                },
                books = new List<Book>
                {
                    new Book
                    {
                        id = "f451", title = "Fahrenheit 451", pages = 200,
                        shelves = new List<string> { "classics" }, tags = new List<string> { "dystopia", "censorship" },
                        chapters = new List<Chapter>
                        {
                            new Chapter { number = 1, startPage = 1, endPage = 70, quiz = Quiz3() },
                            new Chapter { number = 2, startPage = 71, endPage = 130, quiz = Quiz3() },
                            new Chapter { number = 3, startPage = 131, endPage = 200 },
                        }
                    },
                    new Book { id = "1984", title = "1984", pages = 350, shelves = new List<string> { "classics" }, tags = new List<string> { "dystopia", "surveillance" } },
                    new Book { id = "aura", title = "Aura", pages = 80, shelves = new List<string> { "mexican" }, tags = new List<string> { "fantasy" } },
                    new Book { id = "cronica", title = "Chronicle", pages = 120, shelves = new List<string> { "mexican" }, tags = new List<string> { "censorship" } },
                }
            };
        }

        public static AppState State(int dailyPages = 20, string currentBook = "f451")
        {
            var s = new AppState();
            s.profile.createdOn = Monday;
            s.profile.goals.dailyPages = dailyPages;
            s.profile.goals.readingDays = new List<int> { 1, 2, 3, 4, 5 }; // Mon–Fri
            s.profile.currentBookId = currentBook;
            return s;
        }

        public static void Read(AppState s, DateTime day, int pages)
        {
            s.logs.Add(new ReadingLog { date = day, bookId = "f451", fromPage = 0, toPage = pages });
        }
    }
}
