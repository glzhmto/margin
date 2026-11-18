using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>Editions with different pagination: chapters scale to the user's total.</summary>
    public class EditionTests
    {
        static Book F451() => TestData.Catalog().FindBook("f451"); // 200 pages; chapters end at 70, 130, 200

        [Test]
        public void SameEdition_KeepsCatalogPages()
        {
            var b = F451();
            Assert.AreEqual(70, Edition.ChapterEnd(b, b.GetChapter(1), 200));
        }

        [Test]
        public void LongerEdition_ScalesChapterEnds()
        {
            var b = F451();
            Assert.AreEqual(105, Edition.ChapterEnd(b, b.GetChapter(1), 300));
            Assert.AreEqual(195, Edition.ChapterEnd(b, b.GetChapter(2), 300));
            Assert.AreEqual(300, Edition.ChapterEnd(b, b.GetChapter(3), 300), "The last chapter closes the book.");
            Assert.AreEqual(106, Edition.ChapterStart(b, b.GetChapter(2), 300));
            Assert.AreEqual(2, Edition.ChapterAt(b, 150, 300).number);
        }

        [Test]
        public void LongerEdition_QuizIsNotOfferedMidChapter()
        {
            var s = TestData.State();
            Assert.IsNull(ReadingService.SetEditionPages(s, F451(), 300));
            // Page 75: in the reference edition it already ended chapter 1; in this 300-page one it does not yet.
            var r = ReadingService.CheckIn(s, TestData.Catalog(), 75, TestData.Monday);
            Assert.IsTrue(r.Ok);
            Assert.IsNull(r.PendingQuiz);
            r = ReadingService.CheckIn(s, TestData.Catalog(), 110, TestData.Monday);
            Assert.AreEqual(1, r.PendingQuiz.number);
        }

        [Test]
        public void LongerEdition_AllowsPagesBeyondReferenceAndFinishesAtItsOwnEnd()
        {
            var s = TestData.State();
            ReadingService.SetEditionPages(s, F451(), 300);
            s.ProgressFor("f451").currentPage = 250;
            var r = ReadingService.CheckIn(s, TestData.Catalog(), 300, TestData.Monday);
            Assert.IsTrue(r.Ok);
            Assert.IsTrue(r.FinishedBook);
            Assert.AreEqual(CheckInError.PageBeyondBook, ReadingService.Validate(s, TestData.Catalog(), 301));
        }

        [TestCase(99)]
        [TestCase(401)]
        public void UnreasonableEditionPages_AreRejected(int pages)
        {
            var s = TestData.State();
            Assert.IsNotNull(ReadingService.SetEditionPages(s, F451(), pages));
            Assert.AreEqual(0, s.ProgressFor("f451").editionPages);
        }

        [Test]
        public void ShorterEdition_ClampsCurrentPage()
        {
            var s = TestData.State();
            s.ProgressFor("f451").currentPage = 180;
            ReadingService.SetEditionPages(s, F451(), 150);
            Assert.AreEqual(150, s.ProgressFor("f451").currentPage);
            Assert.AreEqual(150, s.PagesFor(F451()));
        }
    }
}
