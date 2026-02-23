#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class QuestionTemplate : AbstractFacebookOperations, IQuestionOperations
    {
        public QuestionTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public string AskQuestion(string questionText) { requireAuthorization(); return string.Empty; }
        public string AddOption(string questionId, string optionText) { requireAuthorization(); return string.Empty; }
        public Question GetQuestion(string questionId) { requireAuthorization(); return default(Question); }
        public List<Question> GetQuestions() { requireAuthorization(); return new List<Question>(); }
        public List<Question> GetQuestions(string userId) { requireAuthorization(); return new List<Question>(); }
        public void DeleteQuestion(string questionId) { requireAuthorization(); }
        public QuestionOption GetOption(string optionId) { requireAuthorization(); return default(QuestionOption); }
        public List<QuestionOption> GetOptions(string questionId) { requireAuthorization(); return new List<QuestionOption>(); }
        public void DeleteOption(string optionId) { requireAuthorization(); }
    }
}
