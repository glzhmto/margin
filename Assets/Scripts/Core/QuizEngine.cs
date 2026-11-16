// Per-chapter quiz scoring. The quiz is retrieval practice (testing effect),
// not an exam: it is optional and always shows the explanation.
using System;
using System.Collections.Generic;

namespace Margin.Core
{
    public struct QuizScore
    {
        public int Correct;
        public int Total;
        /// <summary>At least the Tuning.quizPassRatio share (2/3 by default).</summary>
        public bool Comprehended => Total > 0 && Correct >= Math.Ceiling(Total * Tuning.Current.quizPassRatio - 1e-3); // tolerance for JSON rounding (0.6667)
    }

    public static class QuizEngine
    {
        public static bool IsCorrect(QuizQuestion q, int answerIndex) => answerIndex == q.correctIndex;

        public static QuizScore Score(Quiz quiz, IList<int> answers)
        {
            if (quiz == null) throw new ArgumentNullException(nameof(quiz));
            if (answers == null || answers.Count != quiz.questions.Count)
                throw new ArgumentException("There must be one answer per question.", nameof(answers));

            int correct = 0;
            for (int i = 0; i < quiz.questions.Count; i++)
                if (IsCorrect(quiz.questions[i], answers[i])) correct++;
            return new QuizScore { Correct = correct, Total = quiz.questions.Count };
        }

        public static QuizResult Record(AppState state, string bookId, int chapter, QuizScore score, DateTime today)
        {
            var r = new QuizResult { bookId = bookId, chapterNumber = chapter, correct = score.Correct, total = score.Total, date = today.Date };
            state.quizResults.RemoveAll(q => q.bookId == bookId && q.chapterNumber == chapter);
            state.quizResults.Add(r);
            return r;
        }

        public static string Verdict(QuizScore s)
        {
            if (s.Correct == s.Total) return "You read it closely.";
            if (s.Comprehended) return "You got the essentials.";
            return "Those pages are worth another look. Rereading is reading too.";
        }
    }
}
