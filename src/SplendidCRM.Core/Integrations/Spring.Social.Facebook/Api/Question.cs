#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Question
    {
        public Question() { }
        public Question(string id, string text, Reference from, DateTime createdTime, DateTime updatedTime) { ID = id; Text = text; From = from; CreatedTime = createdTime; UpdatedTime = updatedTime; }
        public string ID { get; set; }
        public Reference From { get; set; }
        public string Text { get; set; }
        public DateTime? CreatedTime { get; set; }
        public DateTime? UpdatedTime { get; set; }
        public List<QuestionOption> Options { get; set; }
    }
}
