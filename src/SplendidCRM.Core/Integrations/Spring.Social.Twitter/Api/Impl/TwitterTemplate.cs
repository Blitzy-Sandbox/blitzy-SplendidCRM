#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    public class TwitterTemplate : ITwitter
    {
        public TwitterTemplate(string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret) { IsAuthorized = true; }
        public ITimelineOperations TimelineOperations { get; private set; }
        public ITweetOperations TweetOperations { get; private set; }
        public IUserOperations UserOperations { get; private set; }
        public IFriendOperations FriendOperations { get; private set; }
        public IBlockOperations BlockOperations { get; private set; }
        public IDirectMessageOperations DirectMessageOperations { get; private set; }
        public ISearchOperations SearchOperations { get; private set; }
        public IListOperations ListOperations { get; private set; }
        public IGeoOperations GeoOperations { get; private set; }
        public bool IsAuthorized { get; private set; }
    }
}
