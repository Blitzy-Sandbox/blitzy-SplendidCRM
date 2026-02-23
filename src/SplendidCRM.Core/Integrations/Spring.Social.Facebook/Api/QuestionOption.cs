#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class QuestionOption
    {
        public QuestionOption() { }
        public QuestionOption(string id, string name, Reference from, int votes, DateTime createdTime) { ID = id; Name = name; From = from; Votes = votes; CreatedTime = createdTime; }
        public string ID { get; set; }
        public Reference From { get; set; }
        public string Name { get; set; }
        public int Votes { get; set; }
        public Page Object { get; set; }
        public DateTime? CreatedTime { get; set; }
    }
}
