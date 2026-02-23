#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    public interface IFriendOperations
    {
        CursoredList<TwitterProfile> GetFriends();
        CursoredList<TwitterProfile> GetFriends(long cursor);
        CursoredList<TwitterProfile> GetFriendsInCursor(long cursor);
        CursoredList<TwitterProfile> GetFriends(string screenName);
        CursoredList<TwitterProfile> GetFriends(long userId, long cursor);
        CursoredList<long> GetFriendIds();
        CursoredList<long> GetFriendIds(long cursor);
        CursoredList<TwitterProfile> GetFollowers();
        CursoredList<TwitterProfile> GetFollowers(long cursor);
        CursoredList<TwitterProfile> GetFollowersInCursor(long cursor);
        CursoredList<TwitterProfile> GetFollowers(string screenName);
        CursoredList<TwitterProfile> GetFollowers(long userId, long cursor);
        CursoredList<long> GetFollowerIds();
        CursoredList<long> GetFollowerIds(long cursor);
        string Follow(string screenName);
        string Follow(long userId);
        string Unfollow(string screenName);
        string Unfollow(long userId);
        bool FriendshipExists(string screenNameA, string screenNameB);
        TwitterProfile EnableNotifications(string screenName);
        TwitterProfile EnableNotifications(long userId);
        TwitterProfile DisableNotifications(string screenName);
        TwitterProfile DisableNotifications(long userId);
        CursoredList<long> GetIncomingFriendships();
        CursoredList<long> GetIncomingFriendships(long cursor);
        CursoredList<long> GetOutgoingFriendships();
        CursoredList<long> GetOutgoingFriendships(long cursor);
    }
}
