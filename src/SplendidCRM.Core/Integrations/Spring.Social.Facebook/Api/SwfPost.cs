#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class SwfPost : Post
    {
        public SwfPost() { }
        public SwfPost(string id, Reference from, DateTime createdTime, DateTime updatedTime) : base(id, from, createdTime, updatedTime) { }
        public string Source { get; set; }
    }
}
