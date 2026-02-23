#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Event
    {
        public enum enumPrivacy { OPEN, SECRET, CLOSED }
        public Event() { }
        public Event(string id, string name, Reference owner, enumPrivacy privacy, DateTime startTime, DateTime endTime, DateTime updatedTime) { ID = id; Name = name; Owner = owner; Privacy = privacy; StartTime = startTime; EndTime = endTime; UpdatedTime = updatedTime; }
        public string ID { get; set; }
        public Reference Owner { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Location { get; set; }
        public Location Venue { get; set; }
        public enumPrivacy Privacy { get; set; }
        public DateTime? UpdatedTime { get; set; }
    }
}
