// The demo account (Core DemoData) against the real catalog. Runs only with dotnet (System.Text.Json).
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    public class DemoAccountTests
    {
        static Catalog Real() => JsonSerializer.Deserialize<Catalog>(
            File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "books.json")), new JsonSerializerOptions { IncludeFields = true });

        static readonly DateTime Today = new DateTime(2026, 11, 17); // a Tuesday

        [Test]
        public void Seed_FinishesTwoBooksAndLeaves1984InProgress()
        {
            var s = new AppState(); var cat = Real();
            var finished = DemoData.Seed(s, cat, Today);

            CollectionAssert.AreEquivalent(new[] { "the-little-prince", "animal-farm" }, finished.Keys);
            Assert.AreEqual("1984", s.profile.currentBookId);
            var p = s.ProgressFor("1984");
            Assert.IsFalse(p.finished);
            Assert.That(p.currentPage, Is.InRange(100, 351));
            Assert.AreEqual(5, s.ProgressFor("the-little-prince").rating);
            Assert.IsTrue(s.profile.onboardingComplete);
        }

        [Test]
        public void Seed_LooksLikeARealReader()
        {
            var s = new AppState(); var cat = Real();
            DemoData.Seed(s, cat, Today);

            Assert.IsTrue(s.logs.TrueForAll(l => l.date < Today), "Today stays empty so the person can log live.");
            Assert.IsFalse(s.logs.Any(l => l.date.DayOfWeek == DayOfWeek.Sunday), "Sunday is a rest day.");
            Assert.IsFalse(s.logs.Any(l => l.date == Today.AddDays(-9)), "Day 9 is a missed day.");
            Assert.Greater(s.quizResults.Count, 5);
            Assert.That(HabitEngine.Compute(s, Today).Level, Is.Not.EqualTo(MomentumLevel.Kindling));
        }

        [Test]
        public void Seed_IsDeterministic()
        {
            var a = new AppState(); var b = new AppState();
            DemoData.Seed(a, Real(), Today);
            DemoData.Seed(b, Real(), Today);
            Assert.AreEqual(a.ProgressFor("1984").currentPage, b.ProgressFor("1984").currentPage);
            Assert.AreEqual(a.logs.Count, b.logs.Count);
        }
    }
}
