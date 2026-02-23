#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class GroupMemberReference : Reference
    {
        public GroupMemberReference() { }
        public GroupMemberReference(string id, string name, bool administrator) : base(id, name) { Administrator = administrator; }
        public bool Administrator { get; set; }
    }
}
