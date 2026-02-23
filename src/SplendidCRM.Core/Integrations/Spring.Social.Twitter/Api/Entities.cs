#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class Entities
    {
        public List<UrlEntity> Urls { get; set; }
        public List<HashTagEntity> HashTags { get; set; }
        public List<MentionEntity> Mentions { get; set; }
        public List<MediaEntity> Media { get; set; }
    }

    [Serializable]
    public class UrlEntity
    {
        public string Url { get; set; }
        public string DisplayUrl { get; set; }
        public string ExpandedUrl { get; set; }
        public List<int> Indices { get; set; }
    }

    [Serializable]
    public class HashTagEntity
    {
        public string Text { get; set; }
        public List<int> Indices { get; set; }
    }

    [Serializable]
    public class MentionEntity
    {
        public long ID { get; set; }
        public string ScreenName { get; set; }
        public string Name { get; set; }
        public List<int> Indices { get; set; }
    }

    [Serializable]
    public class MediaEntity
    {
        public long ID { get; set; }
        public string MediaUrl { get; set; }
        public string MediaUrlHttps { get; set; }
        public string Url { get; set; }
        public string DisplayUrl { get; set; }
        public string ExpandedUrl { get; set; }
        public string Type { get; set; }
        public List<int> Indices { get; set; }
    }
}
