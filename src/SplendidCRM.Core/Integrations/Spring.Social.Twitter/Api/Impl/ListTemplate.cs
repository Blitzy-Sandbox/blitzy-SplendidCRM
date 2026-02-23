#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class ListTemplate : AbstractTwitterOperations, IListOperations
    {
        public List<UserList> GetLists() { return new List<UserList>(); }
        public List<UserList> GetLists(string screenName) { return new List<UserList>(); }
        public List<UserList> GetLists(long userId) { return new List<UserList>(); }
        public UserList GetList(long listId) { return null; }
        public UserList GetList(string screenName, string listSlug) { return null; }
        public List<Tweet> GetListStatuses(long listId) { return new List<Tweet>(); }
        public List<Tweet> GetListStatuses(long listId, int count) { return new List<Tweet>(); }
        public List<Tweet> GetListStatuses(string screenName, string listSlug) { return new List<Tweet>(); }
        public List<Tweet> GetListStatuses(string screenName, string listSlug, int count) { return new List<Tweet>(); }
        public UserList CreateList(string name, string description, bool isPublic) { return null; }
        public UserList UpdateList(long listId, string name, string description, bool isPublic) { return null; }
        public void DeleteList(long listId) { }
        public List<TwitterProfile> GetListMembers(long listId) { return new List<TwitterProfile>(); }
        public UserList AddToList(long listId, params long[] newMemberIds) { return null; }
        public void RemoveFromList(long listId, long memberId) { }
        public List<TwitterProfile> GetListSubscribers(long listId) { return new List<TwitterProfile>(); }
        public UserList Subscribe(long listId) { return null; }
        public UserList Unsubscribe(long listId) { return null; }
        public List<UserList> GetMemberships(string screenName) { return new List<UserList>(); }
        public List<UserList> GetSubscriptions(string screenName) { return new List<UserList>(); }
    }
}
