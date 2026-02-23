#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface IQuestionOperations
    {
        string AskQuestion(string questionText);
        string AddOption(string questionId, string optionText);
        Question GetQuestion(string questionId);
        List<Question> GetQuestions();
        List<Question> GetQuestions(string userId);
        void DeleteQuestion(string questionId);
        QuestionOption GetOption(string optionId);
        List<QuestionOption> GetOptions(string questionId);
        void DeleteOption(string optionId);
    }
}
