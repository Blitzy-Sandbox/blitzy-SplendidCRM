#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class TweetEntities
    {
        public IList<UrlEntity>         Urls         { get; set; }
        public IList<HashTagEntity>     HashTags     { get; set; }
        public IList<MentionEntity>     Mentions     { get; set; }
        public IList<MediaEntity>       Media        { get; set; }
        public TweetEntities() { Urls = new List<UrlEntity>(); HashTags = new List<HashTagEntity>(); Mentions = new List<MentionEntity>(); Media = new List<MediaEntity>(); }
    }
}
