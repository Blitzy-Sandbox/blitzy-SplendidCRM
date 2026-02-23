#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface IGroupOperations
    {
        Group GetGroup(string groupId);
        byte[] GetGroupImage(string groupId);
        byte[] GetGroupImage(string groupId, ImageType imageType);
        List<GroupMemberReference> GetMembers(string groupId);
        List<FacebookProfile> GetMemberProfiles(string groupId);
        List<GroupMembership> GetMemberships();
        List<GroupMembership> GetMemberships(string userId);
        List<Group> Search(string query);
        List<Group> Search(string query, int offset, int limit);
    }
}
