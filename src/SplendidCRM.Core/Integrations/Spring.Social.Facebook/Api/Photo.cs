#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Photo
    {
        [Serializable]
        public class Image
        {
            public string Source { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }
        public Photo() { }
        public Photo(string id, Reference from, string link, string icon, DateTime createdTime, List<Image> images) { ID = id; From = from; Link = link; Icon = icon; CreatedTime = createdTime; Images = images; }
        public string ID { get; set; }
        public Reference From { get; set; }
        public List<Tag> Tags { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Picture { get; set; }
        public string Source { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public List<Image> Images { get; set; }
        public Image OversizedImage { get; set; }
        public Image SourceImage { get; set; }
        public Image SmallImage { get; set; }
        public Image AlbumImage { get; set; }
        public Image TinyImage { get; set; }
        public string Link { get; set; }
        public Page Place { get; set; }
        public DateTime? CreatedTime { get; set; }
        public DateTime? UpdatedTime { get; set; }
        public int Position { get; set; }
    }
}
