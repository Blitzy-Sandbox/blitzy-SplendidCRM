#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    public interface IListOperations
    {
        List<UserList> GetLists();
        List<UserList> GetLists(string screenName);
        List<UserList> GetLists(long userId);
        UserList GetList(long listId);
        UserList GetList(string screenName, string listSlug);
        List<Tweet> GetListStatuses(long listId);
        List<Tweet> GetListStatuses(long listId, int count);
        List<Tweet> GetListStatuses(string screenName, string listSlug);
        List<Tweet> GetListStatuses(string screenName, string listSlug, int count);
        UserList CreateList(string name, string description, bool isPublic);
        UserList UpdateList(long listId, string name, string description, bool isPublic);
        void DeleteList(long listId);
        List<TwitterProfile> GetListMembers(long listId);
        UserList AddToList(long listId, params long[] newMemberIds);
        void RemoveFromList(long listId, long memberId);
        List<TwitterProfile> GetListSubscribers(long listId);
        UserList Subscribe(long listId);
        UserList Unsubscribe(long listId);
        List<UserList> GetMemberships(string screenName);
        List<UserList> GetSubscriptions(string screenName);
    }
}
