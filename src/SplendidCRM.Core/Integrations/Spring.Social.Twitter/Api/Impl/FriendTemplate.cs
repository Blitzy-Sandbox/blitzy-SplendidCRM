#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class FriendTemplate : AbstractTwitterOperations, IFriendOperations
    {
        public CursoredList<TwitterProfile> GetFriends() { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFriends(long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFriendsInCursor(long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFriends(string screenName) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFriends(long userId, long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<long> GetFriendIds() { return new CursoredList<long>(); }
        public CursoredList<long> GetFriendIds(long cursor) { return new CursoredList<long>(); }
        public CursoredList<TwitterProfile> GetFollowers() { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFollowers(long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFollowersInCursor(long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFollowers(string screenName) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFollowers(long userId, long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<long> GetFollowerIds() { return new CursoredList<long>(); }
        public CursoredList<long> GetFollowerIds(long cursor) { return new CursoredList<long>(); }
        public string Follow(string screenName) { return string.Empty; }
        public string Follow(long userId) { return string.Empty; }
        public string Unfollow(string screenName) { return string.Empty; }
        public string Unfollow(long userId) { return string.Empty; }
        public bool FriendshipExists(string screenNameA, string screenNameB) { return false; }
        public TwitterProfile EnableNotifications(string screenName) { return null; }
        public TwitterProfile EnableNotifications(long userId) { return null; }
        public TwitterProfile DisableNotifications(string screenName) { return null; }
        public TwitterProfile DisableNotifications(long userId) { return null; }
        public CursoredList<long> GetIncomingFriendships() { return new CursoredList<long>(); }
        public CursoredList<long> GetIncomingFriendships(long cursor) { return new CursoredList<long>(); }
        public CursoredList<long> GetOutgoingFriendships() { return new CursoredList<long>(); }
        public CursoredList<long> GetOutgoingFriendships(long cursor) { return new CursoredList<long>(); }
    }
}
