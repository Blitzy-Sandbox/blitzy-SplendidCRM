#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class TimelineTemplate : AbstractTwitterOperations, ITimelineOperations
    {
        public List<Tweet> GetHomeTimeline() { return new List<Tweet>(); }
        public List<Tweet> GetHomeTimeline(int count) { return new List<Tweet>(); }
        public List<Tweet> GetHomeTimeline(int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public List<Tweet> GetUserTimeline() { return new List<Tweet>(); }
        public List<Tweet> GetUserTimeline(int count) { return new List<Tweet>(); }
        public List<Tweet> GetUserTimeline(string screenName) { return new List<Tweet>(); }
        public List<Tweet> GetUserTimeline(string screenName, int count) { return new List<Tweet>(); }
        public List<Tweet> GetUserTimeline(string screenName, int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public List<Tweet> GetUserTimeline(long userId) { return new List<Tweet>(); }
        public List<Tweet> GetUserTimeline(long userId, int count) { return new List<Tweet>(); }
        public List<Tweet> GetUserTimeline(long userId, int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public List<Tweet> GetMentions() { return new List<Tweet>(); }
        public List<Tweet> GetMentions(int count) { return new List<Tweet>(); }
        public List<Tweet> GetMentions(int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public List<Tweet> GetRetweetsOfMe() { return new List<Tweet>(); }
        public List<Tweet> GetRetweetsOfMe(int count) { return new List<Tweet>(); }
        public List<Tweet> GetRetweetsOfMe(int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public Tweet GetStatus(long tweetId) { return null; }
        public Tweet UpdateStatus(string message) { return null; }
        public Tweet UpdateStatus(string message, StatusDetails details) { return null; }
        public void DeleteStatus(long tweetId) { }
        public void Retweet(long tweetId) { }
        public List<Tweet> GetRetweets(long tweetId) { return new List<Tweet>(); }
        public List<Tweet> GetRetweets(long tweetId, int count) { return new List<Tweet>(); }
        public List<Tweet> GetFavorites() { return new List<Tweet>(); }
        public List<Tweet> GetFavorites(int count) { return new List<Tweet>(); }
        public void AddToFavorites(long tweetId) { }
        public void RemoveFromFavorites(long tweetId) { }
    }
}
