#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Post
    {
        public enum enumPostType { POST, CHECKIN, LINK, NOTE, PHOTO, STATUS, VIDEO, SWF, MUSIC }
        public Post() { }
        public Post(string id, Reference from, DateTime createdTime, DateTime updatedTime) { ID = id; From = from; CreatedTime = createdTime; UpdatedTime = updatedTime; }
        public string ID { get; set; }
        public Reference From { get; set; }
        public List<Reference> To { get; set; }
        public string Caption { get; set; }
        public string Message { get; set; }
        public string Picture { get; set; }
        public string Link { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public DateTime? CreatedTime { get; set; }
        public DateTime? UpdatedTime { get; set; }
        public Reference Application { get; set; }
        public enumPostType? Type { get; set; }
        public List<Reference> Likes { get; set; }
        public int LikeCount { get; set; }
        public int SharesCount { get; set; }
        public List<Comment> Comments { get; set; }
        public string Story { get; set; }
        public Dictionary<int, List<StoryTag>> StoryTags { get; set; }
        public int CommentCount { get; set; }
    }
}
