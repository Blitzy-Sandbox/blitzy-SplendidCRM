#nullable disable
using System;
using System.Collections.Generic;

namespace Spring.Social.Twitter.Api
{
    /// <summary>
    /// Legacy compatibility class representing tweet entity metadata.
    /// Note: The migrated Twitter API uses <see cref="TweetEntities"/> instead.
    /// This class is retained as a compile-time compatibility stub.
    /// </summary>
    [Serializable]
    public class Entities
    {
        /// <summary>
        /// Gets or sets the URLs extracted from the Tweet text.
        /// </summary>
        public List<UrlEntity> Urls { get; set; }

        /// <summary>
        /// Gets or sets the hashtags extracted from the Tweet text.
        /// </summary>
        public List<HashtagEntity> Hashtags { get; set; }

        /// <summary>
        /// Gets or sets the user mentions extracted from the Tweet text.
        /// </summary>
        public List<UserMentionEntity> Mentions { get; set; }

        /// <summary>
        /// Gets or sets the media extracted from the Tweet text.
        /// </summary>
        public List<MediaEntity> Media { get; set; }
    }
}
