// Stub interfaces replacing the discontinued TweetinCore.dll (BackupBin2012/).
// These dormant stubs preserve compilation compatibility per .NET 10 migration (AAP Section 0.6.1).
// TweetinCore.dll has been REMOVED from BackupBin2012/ as part of the .NET Framework 4.8 → .NET 10
// ASP.NET Core migration. SocialImport.cs and other consumers reference these types using
// fully-qualified names (TweetinCore.Interfaces.ITweet, etc.), so the namespace MUST be
// TweetinCore.Interfaces to maintain zero-change compilation compatibility.
//
// Per AAP Section 0.8.1: "Preserve all public interfaces and class signatures for Enterprise Edition
// upgrade path." These interfaces are dormant stubs — they are NOT expected to be instantiated at
// runtime in the Community Edition. They exist solely for compilation.

using System.Collections.Generic;

namespace TweetinCore.Interfaces
{
    /// <summary>
    /// Represents a Twitter tweet with its text content and associated entities.
    /// Stub interface replacing TweetinCore.dll (BackupBin2012/) per AAP Section 0.6.1.
    /// </summary>
    public interface ITweet
    {
        /// <summary>The full text body of the tweet.</summary>
        string Text { get; }

        /// <summary>Entity metadata extracted from the tweet (hashtags, mentions, URLs).</summary>
        ITweetEntities Entities { get; }
    }

    /// <summary>
    /// Represents the collection of entities embedded in a tweet (hashtags, user mentions, URLs).
    /// Stub interface replacing TweetinCore.dll (BackupBin2012/) per AAP Section 0.6.1.
    /// </summary>
    public interface ITweetEntities
    {
        /// <summary>List of hashtag entities found in the tweet text.</summary>
        IList<IHashTagEntity> Hashtags { get; }

        /// <summary>List of user mention entities found in the tweet text.</summary>
        IList<IUserMentionEntity> UserMentions { get; }

        /// <summary>List of URL entities found in the tweet text.</summary>
        IList<IUrlEntity> Urls { get; }
    }

    /// <summary>
    /// Represents a hashtag entity within a tweet.
    /// Stub interface replacing TweetinCore.dll (BackupBin2012/) per AAP Section 0.6.1.
    /// </summary>
    public interface IHashTagEntity
    {
        /// <summary>The hashtag text (without the leading '#' character).</summary>
        string Text { get; }
    }

    /// <summary>
    /// Represents a user mention entity within a tweet.
    /// Stub interface replacing TweetinCore.dll (BackupBin2012/) per AAP Section 0.6.1.
    /// </summary>
    public interface IUserMentionEntity
    {
        /// <summary>The Twitter screen name of the mentioned user (without the leading '@' character).</summary>
        string ScreenName { get; }
    }

    /// <summary>
    /// Represents a URL entity embedded in a tweet.
    /// Stub interface replacing TweetinCore.dll (BackupBin2012/) per AAP Section 0.6.1.
    /// </summary>
    public interface IUrlEntity
    {
        /// <summary>The URL string as it appeared in the tweet (may be a t.co shortened URL).</summary>
        string Url { get; }
    }
}
