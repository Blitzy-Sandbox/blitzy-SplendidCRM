#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class VideoPost : Post
    {
        public VideoPost() { }
        public VideoPost(string id, Reference from, DateTime createdTime, DateTime updatedTime) : base(id, from, createdTime, updatedTime) { }
        public string Source { get; set; }
        public string VideoId { get; set; }
        public List<Tag> Tags { get; set; }
    }
}
