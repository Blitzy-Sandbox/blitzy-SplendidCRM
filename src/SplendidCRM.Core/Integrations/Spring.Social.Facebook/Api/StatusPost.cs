#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class StatusPost : Post
    {
        public StatusPost() { }
        public StatusPost(string id, Reference from, DateTime createdTime, DateTime updatedTime) : base(id, from, createdTime, updatedTime) { }

    }
}
