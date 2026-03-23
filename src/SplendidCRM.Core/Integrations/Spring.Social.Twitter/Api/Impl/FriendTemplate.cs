#nullable disable
// .NET 10 Migration: Spring.Rest.Client, Spring.Http, and conditional compilation blocks removed.
// All method implementations are stub no-ops returning default values — this is a dormant
// Enterprise Edition integration stub, compile only, not expected to execute.
// Method signatures updated to match the corrected IFriendOperations interface per AAP §0.8.1.
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    /// <summary>
    /// Implementation of <see cref="IFriendOperations"/>, providing a binding to Twitter's
    /// friends and followers-oriented REST resources.
    /// Dormant Enterprise Edition integration stub — compile only, not expected to execute.
    /// </summary>
    class FriendTemplate : AbstractTwitterOperations, IFriendOperations
    {
        // =====================================================================
        // GetFriends overloads
        // =====================================================================

        public CursoredList<TwitterProfile> GetFriends() { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFriendsInCursor(long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFriends(long userId) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFriendsInCursor(long userId, long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFriends(string screenName) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFriendsInCursor(string screenName, long cursor) { return new CursoredList<TwitterProfile>(); }

        // =====================================================================
        // GetFriendIds overloads
        // =====================================================================

        public CursoredList<long> GetFriendIds() { return new CursoredList<long>(); }
        public CursoredList<long> GetFriendIdsInCursor(long cursor) { return new CursoredList<long>(); }
        public CursoredList<long> GetFriendIds(long userId) { return new CursoredList<long>(); }
        public CursoredList<long> GetFriendIdsInCursor(long userId, long cursor) { return new CursoredList<long>(); }
        public CursoredList<long> GetFriendIds(string screenName) { return new CursoredList<long>(); }
        public CursoredList<long> GetFriendIdsInCursor(string screenName, long cursor) { return new CursoredList<long>(); }

        // =====================================================================
        // GetFollowers overloads
        // =====================================================================

        public CursoredList<TwitterProfile> GetFollowers() { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFollowersInCursor(long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFollowers(long userId) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFollowersInCursor(long userId, long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFollowers(string screenName) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetFollowersInCursor(string screenName, long cursor) { return new CursoredList<TwitterProfile>(); }

        // =====================================================================
        // GetFollowerIds overloads
        // =====================================================================

        public CursoredList<long> GetFollowerIds() { return new CursoredList<long>(); }
        public CursoredList<long> GetFollowerIdsInCursor(long cursor) { return new CursoredList<long>(); }
        public CursoredList<long> GetFollowerIds(long userId) { return new CursoredList<long>(); }
        public CursoredList<long> GetFollowerIdsInCursor(long userId, long cursor) { return new CursoredList<long>(); }
        public CursoredList<long> GetFollowerIds(string screenName) { return new CursoredList<long>(); }
        public CursoredList<long> GetFollowerIdsInCursor(string screenName, long cursor) { return new CursoredList<long>(); }

        // =====================================================================
        // Follow / Unfollow
        // =====================================================================

        public TwitterProfile Follow(long userId) { return null; }
        public TwitterProfile Follow(string screenName) { return null; }
        public TwitterProfile Unfollow(long userId) { return null; }
        public TwitterProfile Unfollow(string screenName) { return null; }

        // =====================================================================
        // Enable / Disable Notifications
        // =====================================================================

        public void EnableNotifications(long userId) { }
        public void EnableNotifications(string screenName) { }
        public void DisableNotifications(long userId) { }
        public void DisableNotifications(string screenName) { }

        // =====================================================================
        // Friendships (incoming/outgoing)
        // =====================================================================

        public CursoredList<long> GetIncomingFriendships() { return new CursoredList<long>(); }
        public CursoredList<long> GetIncomingFriendships(long cursor) { return new CursoredList<long>(); }
        public CursoredList<long> GetOutgoingFriendships() { return new CursoredList<long>(); }
        public CursoredList<long> GetOutgoingFriendships(long cursor) { return new CursoredList<long>(); }
    }
}
