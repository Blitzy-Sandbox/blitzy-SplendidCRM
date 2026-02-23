#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class FriendTemplate : AbstractFacebookOperations, IFriendOperations
    {
        public FriendTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public List<Reference> GetFriendLists() { return GetFriendLists("me"); }
        public List<Reference> GetFriendLists(string userId) { requireAuthorization(); return new List<Reference>(); }
        public Reference GetFriendList(string friendListId) { return FetchObject<Reference>(friendListId); }
        public List<Reference> GetFriendListMembers(string friendListId) { requireAuthorization(); return new List<Reference>(); }
        public string CreateFriendList(string name) { return CreateFriendList("me", name); }
        public string CreateFriendList(string userId, string name) { requireAuthorization(); return string.Empty; }
        public void DeleteFriendList(string friendListId) { requireAuthorization(); Delete(friendListId); }
        public void AddToFriendList(string friendListId, string friendId) { requireAuthorization(); }
        public void RemoveFromFriendList(string friendListId, string friendId) { requireAuthorization(); }
        public List<Reference> GetFriends() { return GetFriends("me"); }
        public List<string> GetFriendIds() { return GetFriendIds("me"); }
        public List<FacebookProfile> GetFriendProfiles() { return GetFriendProfiles("me", 0, 100); }
        public List<FacebookProfile> GetFriendProfiles(int offset, int limit) { return GetFriendProfiles("me", offset, limit); }
        public List<Reference> GetFriends(string userId) { requireAuthorization(); return new List<Reference>(); }
        public List<string> GetFriendIds(string userId) { requireAuthorization(); return new List<string>(); }
        public List<FacebookProfile> GetFriendProfiles(string userId) { return GetFriendProfiles(userId, 0, 100); }
        public List<FacebookProfile> GetFriendProfiles(string userId, int offset, int limit) { requireAuthorization(); return new List<FacebookProfile>(); }
        public List<FamilyMember> GetFamily() { return GetFamily("me"); }
        public List<FamilyMember> GetFamily(string userId) { requireAuthorization(); return new List<FamilyMember>(); }
        public List<Reference> GetMutualFriends(string userId) { requireAuthorization(); return new List<Reference>(); }
        public List<Reference> GetSubscribedTo() { return GetSubscribedTo("me"); }
        public List<Reference> GetSubscribedTo(string userId) { requireAuthorization(); return new List<Reference>(); }
        public List<Reference> GetSubscribers() { return GetSubscribers("me"); }
        public List<Reference> GetSubscribers(string userId) { requireAuthorization(); return new List<Reference>(); }
    }
}
