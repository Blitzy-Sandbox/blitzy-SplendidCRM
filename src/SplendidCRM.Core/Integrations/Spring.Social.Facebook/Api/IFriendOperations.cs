#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface IFriendOperations
    {
        List<Reference> GetFriendLists();
        List<Reference> GetFriendLists(string userId);
        Reference GetFriendList(string friendListId);
        List<Reference> GetFriendListMembers(string friendListId);
        string CreateFriendList(string name);
        string CreateFriendList(string userId, string name);
        void DeleteFriendList(string friendListId);
        void AddToFriendList(string friendListId, string friendId);
        void RemoveFromFriendList(string friendListId, string friendId);
        List<Reference> GetFriends();
        List<string> GetFriendIds();
        List<FacebookProfile> GetFriendProfiles();
        List<FacebookProfile> GetFriendProfiles(int offset, int limit);
        List<Reference> GetFriends(string userId);
        List<string> GetFriendIds(string userId);
        List<FacebookProfile> GetFriendProfiles(string userId);
        List<FacebookProfile> GetFriendProfiles(string userId, int offset, int limit);
        List<FamilyMember> GetFamily();
        List<FamilyMember> GetFamily(string userId);
        List<Reference> GetMutualFriends(string userId);
        List<Reference> GetSubscribedTo();
        List<Reference> GetSubscribedTo(string userId);
        List<Reference> GetSubscribers();
        List<Reference> GetSubscribers(string userId);
    }
}
