using System.Collections.Generic;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>Settings change behavior without touching code, and reject absurd values.</summary>
    public class TuningTests
    {
        [TearDown] public void Restore() => Tuning.Reset();

        [Test]
        public void Defaults_AreValid() => Assert.IsEmpty(new Tuning().Validate());

        [Test]
        public void InvalidTuning_IsRejectedAndDefaultsStay()
        {
            var errors = Tuning.Apply(new Tuning { momentumLambda = 1.2, levelStrong = 0.1 });
            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual(0.85, HabitEngine.Lambda);
        }

        [Test]
        public void Lambda_ChangesHowFastMomentumDrops()
        {
            Assert.IsEmpty(Tuning.Apply(new Tuning { momentumLambda = 0.5 }));
            // One missed reading day with momentum 1.0: 0.5·1.0 + 0.5·0 = 0.5
            Assert.AreEqual(0.5, HabitEngine.Step(1.0, 0, 20, isReadingDay: true, dayIsOver: true), 1e-9);
        }

        [Test]
        public void WeeklyBudget_LimitsReminders()
        {
            Tuning.Apply(new Tuning { reminderWeeklyBudget = 2 });
            var s = TestData.State();
            var plan = ReminderPlanner.PlanNext7Days(s, TestData.Monday.AddHours(8));
            Assert.AreEqual(2, plan.Count);
        }

        [Test]
        public void MaxPagesPerCheckIn_IsConfigurable()
        {
            Tuning.Apply(new Tuning { maxPagesPerCheckIn = 50 });
            Assert.AreEqual(CheckInError.TooManyPagesToday, ReadingService.Validate(TestData.State(), TestData.Catalog(), 60));
        }

        [TestCase(0.6667, 2, 3, true)]   // 2/3 written as in the JSON
        [TestCase(0.6667, 1, 3, false)]
        [TestCase(1.0, 2, 3, false)]     // strict mode: all correct
        public void QuizPassRatio_DecidesComprehension(double ratio, int correct, int total, bool expected)
        {
            Tuning.Apply(new Tuning { quizPassRatio = ratio });
            Assert.AreEqual(expected, new QuizScore { Correct = correct, Total = total }.Comprehended);
        }
    }
}
