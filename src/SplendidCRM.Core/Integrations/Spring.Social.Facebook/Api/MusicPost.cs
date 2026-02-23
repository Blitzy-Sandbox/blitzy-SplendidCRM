#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class MusicPost : Post
    {
        public MusicPost() { }
        public MusicPost(string id, Reference from, DateTime createdTime, DateTime updatedTime) : base(id, from, createdTime, updatedTime) { }
        public string Source { get; set; }
    }
}
