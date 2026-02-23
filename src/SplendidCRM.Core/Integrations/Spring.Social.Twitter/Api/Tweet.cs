#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class Tweet
    {
        public Tweet() { }
        public long ID { get; set; }
        public string IdStr { get; set; }
        public string Text { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string FromUser { get; set; }
        public long? FromUserId { get; set; }
        public string ProfileImageUrl { get; set; }
        public long? ToUserId { get; set; }
        public long? InReplyToStatusId { get; set; }
        public long? InReplyToUserId { get; set; }
        public string InReplyToScreenName { get; set; }
        public string LanguageCode { get; set; }
        public string Source { get; set; }
        public int RetweetCount { get; set; }
        public bool Retweeted { get; set; }
        public Tweet RetweetedStatus { get; set; }
        public bool Favorited { get; set; }
        public int FavoriteCount { get; set; }
        public TwitterProfile User { get; set; }
        public Entities Entities { get; set; }
    }
}
