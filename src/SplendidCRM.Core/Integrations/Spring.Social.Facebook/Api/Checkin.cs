#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Checkin
    {
        public Checkin() { }
        public Checkin(string id, Page place, Reference from, Reference application, DateTime createdTime) { ID = id; Place = place; From = from; Application = application; CreatedTime = createdTime; }
        public string ID { get; set; }
        public Reference From { get; set; }
        public List<Reference> Tags { get; set; }
        public Page Place { get; set; }
        public Reference Application { get; set; }
        public DateTime? CreatedTime { get; set; }
        public List<Reference> Likes { get; set; }
        public string Message { get; set; }
        public List<Comment> Comments { get; set; }
    }
}
