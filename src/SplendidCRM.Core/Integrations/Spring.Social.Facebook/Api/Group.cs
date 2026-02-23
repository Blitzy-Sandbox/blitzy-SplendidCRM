#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Group
    {
        public enum enumPrivacy { OPEN, SECRET, CLOSED }
        public Group() { }
        public Group(string id, Reference owner, string name, enumPrivacy privacy, string link, DateTime updatedTime) { ID = id; Owner = owner; Name = name; Privacy = privacy; Link = link; UpdatedTime = updatedTime; }
        public string ID { get; set; }
        public int Version { get; set; }
        public string Icon { get; set; }
        public Reference Owner { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Link { get; set; }
        public enumPrivacy Privacy { get; set; }
        public DateTime? UpdatedTime { get; set; }
    }
}
