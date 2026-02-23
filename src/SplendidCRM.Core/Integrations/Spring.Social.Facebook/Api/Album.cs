#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Album
    {
        public enum enumType { PROFILE, MOBILE, WALL, NORMAL, ALBUM }
        public enum enumPrivacy { EVERYONE, FRIENDS_OF_FRIENDS, FRIENDS, CUSTOM }
        public Album() { }
        public Album(string id, Reference from, string name, enumType type, string link, int count, enumPrivacy privacy, DateTime createdTime) { ID = id; From = from; Name = name; Type = type; Link = link; Count = count; Privacy = privacy; CreatedTime = createdTime; }
        public string ID { get; set; }
        public Reference From { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string Link { get; set; }
        public string CoverPhotoId { get; set; }
        public enumPrivacy Privacy { get; set; }
        public int Count { get; set; }
        public enumType Type { get; set; }
        public DateTime? CreatedTime { get; set; }
        public DateTime? UpdatedTime { get; set; }
        public bool CanUpload { get; set; }
    }
}
