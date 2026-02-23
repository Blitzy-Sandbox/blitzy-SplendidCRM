#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    public interface ITimelineOperations
    {
        List<Tweet> GetHomeTimeline();
        List<Tweet> GetHomeTimeline(int count);
        List<Tweet> GetHomeTimeline(int count, long sinceId, long maxId);
        List<Tweet> GetUserTimeline();
        List<Tweet> GetUserTimeline(int count);
        List<Tweet> GetUserTimeline(string screenName);
        List<Tweet> GetUserTimeline(string screenName, int count);
        List<Tweet> GetUserTimeline(string screenName, int count, long sinceId, long maxId);
        List<Tweet> GetUserTimeline(long userId);
        List<Tweet> GetUserTimeline(long userId, int count);
        List<Tweet> GetUserTimeline(long userId, int count, long sinceId, long maxId);
        List<Tweet> GetMentions();
        List<Tweet> GetMentions(int count);
        List<Tweet> GetMentions(int count, long sinceId, long maxId);
        List<Tweet> GetRetweetsOfMe();
        List<Tweet> GetRetweetsOfMe(int count);
        List<Tweet> GetRetweetsOfMe(int count, long sinceId, long maxId);
        Tweet GetStatus(long tweetId);
        Tweet UpdateStatus(string message);
        Tweet UpdateStatus(string message, StatusDetails details);
        void DeleteStatus(long tweetId);
        void Retweet(long tweetId);
        List<Tweet> GetRetweets(long tweetId);
        List<Tweet> GetRetweets(long tweetId, int count);
        List<Tweet> GetFavorites();
        List<Tweet> GetFavorites(int count);
        void AddToFavorites(long tweetId);
        void RemoveFromFavorites(long tweetId);
    }

    public class StatusDetails
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool DisplayCoordinates { get; set; }
        public long InReplyToStatusId { get; set; }
        public bool WrapLinks { get; set; }
    }
}
