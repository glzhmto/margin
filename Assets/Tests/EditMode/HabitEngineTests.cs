using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    public class HabitEngineTests
    {
        [Test]
        public void DailyRatio_IsCappedToAvoidRewardingBinges()
        {
            Assert.AreEqual(1.5, HabitEngine.DailyRatio(500, 20), 1e-9);
            Assert.AreEqual(0.5, HabitEngine.DailyRatio(10, 20), 1e-9);
        }

        [Test]
        public void Step_RestDayWithoutReading_KeepsMomentum()
        {
            Assert.AreEqual(0.7, HabitEngine.Step(0.7, 0, 20, isReadingDay: false, dayIsOver: true), 1e-9);
        }

        [Test]
        public void Step_TodayNotOver_DoesNotPunish()
        {
            Assert.AreEqual(0.7, HabitEngine.Step(0.7, 0, 20, isReadingDay: true, dayIsOver: false), 1e-9);
        }

        [Test]
        public void Step_MissedReadingDay_DecaysGraduallyNotToZero()
        {
            double m = HabitEngine.Step(0.8, 0, 20, isReadingDay: true, dayIsOver: true);
            Assert.AreEqual(0.8 * HabitEngine.Lambda, m, 1e-9);
            Assert.Greater(m, 0.6);
        }

        [Test]
        public void Compute_TwoConsistentWeeks_ReachesFirmOrBetter()
        {
            var s = TestData.State();
            var today = TestData.Monday.AddDays(13);
            for (var d = TestData.Monday; d <= today; d = d.AddDays(1))
                if (s.profile.goals.IsReadingDay(d)) TestData.Read(s, d, 20);

            var snap = HabitEngine.Compute(s, today);
            Assert.That(snap.Level, Is.EqualTo(MomentumLevel.Strong).Or.EqualTo(MomentumLevel.Unstoppable));
        }

        [Test]
        public void Compute_WeekendsDoNotLowerMomentum()
        {
            var s = TestData.State();
            for (int i = 0; i < 5; i++) TestData.Read(s, TestData.Monday.AddDays(i), 20);
            double friday = HabitEngine.Compute(s, TestData.Monday.AddDays(4)).Value;
            double sunday = HabitEngine.Compute(s, TestData.Monday.AddDays(6)).Value;
            Assert.AreEqual(friday, sunday, 1e-9);
        }

        [Test]
        public void Compute_ReadingTodayShowsUpwardTrend()
        {
            var s = TestData.State();
            TestData.Read(s, TestData.Monday, 20);
            Assert.AreEqual(1, HabitEngine.Compute(s, TestData.Monday).Trend);
        }

        [Test]
        public void Weekly_SumsMondayToSunday()
        {
            var s = TestData.State(dailyPages: 10);
            TestData.Read(s, TestData.Monday, 15);
            TestData.Read(s, TestData.Monday.AddDays(6), 5);
            TestData.Read(s, TestData.Monday.AddDays(7), 99); // next week
            var w = HabitEngine.Weekly(s, TestData.Monday.AddDays(3));
            Assert.AreEqual(20, w.PagesRead);
            Assert.AreEqual(50, w.Target);
            Assert.AreEqual(TestData.Monday, w.WeekStart);
        }

        [Test]
        public void WeekStart_SundayBelongsToPreviousMonday()
        {
            Assert.AreEqual(TestData.Monday, HabitEngine.WeekStart(TestData.Monday.AddDays(6)));
        }
    }
}
