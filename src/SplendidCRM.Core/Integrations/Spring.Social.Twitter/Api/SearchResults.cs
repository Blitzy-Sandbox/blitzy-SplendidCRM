#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class SearchResults
    {
        public SearchResults() { Tweets = new List<Tweet>(); }
        public List<Tweet> Tweets { get; set; }
        public SearchMetadata SearchMetadata { get; set; }
    }

    [Serializable]
    public class SearchMetadata
    {
        public long MaxId { get; set; }
        public long SinceId { get; set; }
    }
}
