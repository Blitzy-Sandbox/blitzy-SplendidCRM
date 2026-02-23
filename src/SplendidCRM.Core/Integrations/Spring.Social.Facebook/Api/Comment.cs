#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Comment
    {
        public Comment() { }
        public Comment(string id, Reference from, string message, DateTime createdTime) { ID = id; From = from; Message = message; CreatedTime = createdTime; }
        public string ID { get; set; }
        public Reference From { get; set; }
        public string Message { get; set; }
        public DateTime? CreatedTime { get; set; }
        public int LikesCount { get; set; }
        public List<Reference> Likes { get; set; }
    }
}
