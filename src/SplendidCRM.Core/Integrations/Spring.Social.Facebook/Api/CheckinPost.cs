#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class CheckinPost : Post
    {
        public CheckinPost() { }
        public CheckinPost(string id, Reference from, DateTime createdTime, DateTime updatedTime) : base(id, from, createdTime, updatedTime) { }
        public Page Place { get; set; }
        public List<Tag> Tags { get; set; }
        public string CheckinId() { return ID != null && ID.Contains("_") ? ID.Split('_')[1] : ID; }
    }
}
