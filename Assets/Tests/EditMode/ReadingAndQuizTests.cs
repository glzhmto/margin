using System.Collections.Generic;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    public class ReadingServiceTests
    {
        [Test]
        public void CheckIn_ValidPage_LogsPagesAndAdvances()
        {
            var s = TestData.State();
            var r = ReadingService.CheckIn(s, TestData.Catalog(), 30, TestData.Monday);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(30, r.PagesAdded);
            Assert.AreEqual(30, s.ProgressFor("f451").currentPage);
            Assert.AreEqual(30, s.PagesOn(TestData.Monday));
        }

        [TestCase(0, CheckInError.PageNotAhead)]
        [TestCase(201, CheckInError.PageBeyondBook)]
        public void CheckIn_InvalidPage_IsRejectedWithoutChangingState(int page, CheckInError expected)
        {
            var s = TestData.State();
            var r = ReadingService.CheckIn(s, TestData.Catalog(), page, TestData.Monday);
            Assert.AreEqual(expected, r.Error);
            Assert.IsEmpty(s.logs);
            Assert.IsNotNull(ReadingService.ErrorMessage(expected));
        }

        [Test]
        public void CheckIn_NoBook_ReturnsError()
        {
            var s = TestData.State(currentBook: null);
            Assert.AreEqual(CheckInError.NoBookSelected, ReadingService.CheckIn(s, TestData.Catalog(), 10, TestData.Monday).Error);
        }

        [Test]
        public void CheckIn_CrossingChapterEnd_OffersThatChaptersQuiz()
        {
            var s = TestData.State();
            var r = ReadingService.CheckIn(s, TestData.Catalog(), 75, TestData.Monday);
            Assert.AreEqual(1, r.ChaptersFinished.Count);
            Assert.AreEqual(1, r.PendingQuiz.number);
        }

        [Test]
        public void CheckIn_QuizAlreadyTaken_IsNotOfferedAgain()
        {
            var s = TestData.State();
            QuizEngine.Record(s, "f451", 1, new QuizScore { Correct = 3, Total = 3 }, TestData.Monday);
            var r = ReadingService.CheckIn(s, TestData.Catalog(), 75, TestData.Monday);
            Assert.IsNull(r.PendingQuiz);
        }

        [Test]
        public void CheckIn_LastPage_FinishesBook()
        {
            var s = TestData.State();
            s.ProgressFor("f451").currentPage = 190;
            var r = ReadingService.CheckIn(s, TestData.Catalog(), 200, TestData.Monday);
            Assert.IsTrue(r.FinishedBook);
            Assert.IsTrue(s.ProgressFor("f451").finished);
            Assert.IsNull(r.PendingQuiz, "Chapter 3 has no quiz.");
        }
    }

    public class QuizEngineTests
    {
        [Test]
        public void Score_CountsCorrectAnswers()
        {
            var score = QuizEngine.Score(TestData.Quiz3(), new List<int> { 0, 1, 0 });
            Assert.AreEqual(2, score.Correct);
            Assert.IsTrue(score.Comprehended);
        }

        [Test]
        public void Score_OneOfThree_IsNotComprehended()
        {
            Assert.IsFalse(QuizEngine.Score(TestData.Quiz3(), new List<int> { 0, 0, 0 }).Comprehended);
        }

        [Test]
        public void Score_WrongAnswerCount_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => QuizEngine.Score(TestData.Quiz3(), new List<int> { 0 }));
        }

        [Test]
        public void Record_RetakeReplacesPreviousResult()
        {
            var s = TestData.State();
            QuizEngine.Record(s, "f451", 1, new QuizScore { Correct = 1, Total = 3 }, TestData.Monday);
            QuizEngine.Record(s, "f451", 1, new QuizScore { Correct = 3, Total = 3 }, TestData.Monday);
            Assert.AreEqual(1, s.quizResults.Count);
            Assert.AreEqual(3, s.quizResults[0].correct);
        }
    }
}
