using System.Linq;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    public class RecommenderTests
    {
        [Test]
        public void Recommend_ExcludesCurrentAndFinishedBooks()
        {
            var s = TestData.State();
            s.ProgressFor("aura").finished = true;
            var recs = Recommender.Recommend(TestData.Catalog(), s, MomentumLevel.Strong, 10);
            Assert.IsFalse(recs.Any(r => r.Book.id == "f451" || r.Book.id == "aura"));
        }

        [Test]
        public void Recommend_PrefersSharedTagsWithCurrentBook()
        {
            var s = TestData.State();
            var top = Recommender.Recommend(TestData.Catalog(), s, MomentumLevel.Strong, 1)[0];
            Assert.AreEqual("1984", top.Book.id); // shares "dystopia" and a shelf
        }

        [Test]
        public void Recommend_FavoriteShelfWinsAndExplainsWhy()
        {
            var s = TestData.State(currentBook: null);
            s.profile.goals.favoriteShelfId = "mexican";
            var top = Recommender.Recommend(TestData.Catalog(), s, MomentumLevel.Strong, 1)[0];
            Assert.That(top.Book.shelves, Does.Contain("mexican"));
            StringAssert.Contains("Mexican", top.Reason);
        }

        [Test]
        public void Recommend_EveryResultHasAReason()
        {
            var recs = Recommender.Recommend(TestData.Catalog(), TestData.State(), MomentumLevel.Kindling, 10);
            Assert.IsTrue(recs.All(r => !string.IsNullOrEmpty(r.Reason)));
        }
    }

    public class ReminderPlannerTests
    {
        [Test]
        public void Plan_RespectsWeeklyBudgetAndReadingDays()
        {
            var s = TestData.State();
            var plan = ReminderPlanner.PlanNext7Days(s, TestData.Monday.AddHours(8));
            Assert.AreEqual(ReminderPlanner.WeeklyBudget, plan.Count);
            Assert.IsTrue(plan.All(d => s.profile.goals.IsReadingDay(d)));
            Assert.IsTrue(plan.All(d => d.Hour == s.profile.goals.reminderHour));
        }

        [Test]
        public void Plan_SkipsTodayIfAlreadyRead()
        {
            var s = TestData.State();
            TestData.Read(s, TestData.Monday, 10);
            var plan = ReminderPlanner.PlanNext7Days(s, TestData.Monday.AddHours(8));
            Assert.IsFalse(plan.Any(d => d.Date == TestData.Monday));
        }

        [Test]
        public void Plan_IgnoredRemindersHalveBudget()
        {
            var s = TestData.State();
            for (int i = 1; i <= 3; i++) s.remindersScheduled.Add(TestData.Monday.AddDays(-i).AddHours(21));
            Assert.AreEqual(3, ReminderPlanner.IgnoredStreak(s, TestData.Monday));
            var plan = ReminderPlanner.PlanNext7Days(s, TestData.Monday.AddHours(8));
            Assert.AreEqual(ReminderPlanner.WeeklyBudget / 2, plan.Count);
        }

        [Test]
        public void Plan_DisabledReminders_ReturnsNothing()
        {
            var s = TestData.State();
            s.profile.goals.remindersEnabled = false;
            Assert.IsEmpty(ReminderPlanner.PlanNext7Days(s, TestData.Monday));
        }
    }
}
