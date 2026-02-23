#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class PhotoPost : Post
    {
        public PhotoPost() { }
        public PhotoPost(string id, Reference from, DateTime createdTime, DateTime updatedTime) : base(id, from, createdTime, updatedTime) { }
        public string PhotoId { get; set; }
        public List<Tag> Tags { get; set; }
    }
}
