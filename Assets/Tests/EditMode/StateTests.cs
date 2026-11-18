using System.Collections.Generic;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>Saved state is repaired on load (reading days used to be duplicated on every startup).</summary>
    public class StateTests
    {
        [Test]
        public void Normalize_RemovesDuplicatedAndInvalidReadingDays()
        {
            var s = TestData.State();
            s.profile.goals.readingDays = new List<int> { 1, 2, 3, 4, 5, 0, 6, 1, 2, 9 };
            s.Normalize();
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6 }, s.profile.goals.readingDays);
            Assert.AreEqual(7 * 20, s.profile.goals.WeeklyTarget);
        }

        [Test]
        public void Normalize_KeepsOneProgressPerBook()
        {
            var s = TestData.State();
            s.progress.Add(new BookProgress { bookId = "f451", currentPage = 30 });
            s.progress.Add(new BookProgress { bookId = "f451", currentPage = 10 });
            s.Normalize();
            Assert.AreEqual(1, s.progress.Count);
            Assert.AreEqual(30, s.ProgressFor("f451").currentPage);
        }
    }
}
