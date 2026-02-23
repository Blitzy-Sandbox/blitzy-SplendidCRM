#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Video
    {
        public Video() { }
        public Video(string id, Reference from, string picture, string embedHtml, string icon, string source, DateTime createdTime, DateTime updatedTime) { ID = id; From = from; Picture = picture; EmbedHtml = embedHtml; Icon = icon; Source = source; CreatedTime = createdTime; UpdatedTime = updatedTime; }
        public string ID { get; set; }
        public Reference From { get; set; }
        public List<Tag> Tags { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Picture { get; set; }
        public string EmbedHtml { get; set; }
        public string Icon { get; set; }
        public string Source { get; set; }
        public DateTime? CreatedTime { get; set; }
        public DateTime? UpdatedTime { get; set; }
        public List<Comment> Comments { get; set; }
    }
}
