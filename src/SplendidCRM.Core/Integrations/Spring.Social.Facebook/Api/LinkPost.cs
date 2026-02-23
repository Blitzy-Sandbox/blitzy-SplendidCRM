#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class LinkPost : Post
    {
        public LinkPost() { }
        public LinkPost(string id, Reference from, DateTime createdTime, DateTime updatedTime) : base(id, from, createdTime, updatedTime) { }
        public string ObjectId { get; set; }
    }
}
