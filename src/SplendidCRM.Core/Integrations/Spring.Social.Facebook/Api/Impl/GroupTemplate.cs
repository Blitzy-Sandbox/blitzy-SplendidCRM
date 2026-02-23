#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class GroupTemplate : AbstractFacebookOperations, IGroupOperations
    {
        public GroupTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public Group GetGroup(string groupId) { return FetchObject<Group>(groupId); }
        public byte[] GetGroupImage(string groupId) { return GetGroupImage(groupId, ImageType.NORMAL); }
        public byte[] GetGroupImage(string groupId, ImageType imageType) { return FetchImage(groupId, "picture", imageType); }
        public List<GroupMemberReference> GetMembers(string groupId) { requireAuthorization(); return new List<GroupMemberReference>(); }
        public List<FacebookProfile> GetMemberProfiles(string groupId) { requireAuthorization(); return new List<FacebookProfile>(); }
        public List<GroupMembership> GetMemberships() { return GetMemberships("me"); }
        public List<GroupMembership> GetMemberships(string userId) { requireAuthorization(); return new List<GroupMembership>(); }
        public List<Group> Search(string query) { return Search(query, 0, 25); }
        public List<Group> Search(string query, int offset, int limit) { return new List<Group>(); }
    }
}
