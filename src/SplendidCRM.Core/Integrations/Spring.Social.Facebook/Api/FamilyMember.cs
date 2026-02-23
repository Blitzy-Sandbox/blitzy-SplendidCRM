#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class FamilyMember : Reference
    {
        public FamilyMember() { }
        public FamilyMember(string id, string name, string relationship) : base(id, name) { Relationship = relationship; }
        public string Relationship { get; set; }
    }
}
