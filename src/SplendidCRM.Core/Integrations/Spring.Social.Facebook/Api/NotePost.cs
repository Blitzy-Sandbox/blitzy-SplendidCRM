#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class NotePost : Post
    {
        public NotePost() { }
        public NotePost(string id, Reference from, DateTime createdTime, DateTime updatedTime) : base(id, from, createdTime, updatedTime) { }
        public string Subject { get; set; }
    }
}
