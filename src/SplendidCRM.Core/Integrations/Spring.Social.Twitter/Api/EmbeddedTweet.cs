#nullable disable
using System;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class EmbeddedTweet
    {
        public string Html { get; set; }
        public string AuthorName { get; set; }
        public string AuthorUrl { get; set; }
        public string Url { get; set; }
        public string ProviderUrl { get; set; }
        public string ProviderName { get; set; }
        public string Type { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Version { get; set; }
        public long CacheAge { get; set; }
    }
}
