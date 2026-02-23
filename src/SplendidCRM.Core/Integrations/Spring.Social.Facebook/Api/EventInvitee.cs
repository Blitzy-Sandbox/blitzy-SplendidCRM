#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class EventInvitee
    {
        public EventInvitee() { }
        public EventInvitee(string id, string name, RsvpStatus rsvpStatus) { ID = id; Name = name; RsvpStatus = rsvpStatus; }
        public string ID { get; set; }
        public string Name { get; set; }
        public RsvpStatus RsvpStatus { get; set; }
    }
}
