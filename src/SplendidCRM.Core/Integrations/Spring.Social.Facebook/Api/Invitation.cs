#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Invitation
    {
        public Invitation() { }
        public Invitation(string eventId, string name, DateTime startTime, DateTime endTime, RsvpStatus rsvpStatus) { EventId = eventId; Name = name; StartTime = startTime; EndTime = endTime; RsvpStatus = rsvpStatus; }
        public Invitation(string eventId, string name, DateTime startTime, DateTime endTime, RsvpStatus rsvpStatus, string location) : this(eventId, name, startTime, endTime, rsvpStatus) { Location = location; }
        public string EventId { get; set; }
        public string Name { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Location { get; set; }
        public RsvpStatus RsvpStatus { get; set; }
    }
}
