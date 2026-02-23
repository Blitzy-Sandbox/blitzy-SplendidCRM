#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class EventTemplate : AbstractFacebookOperations, IEventOperations
    {
        public EventTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public List<Invitation> GetInvitations() { return GetInvitations("me", 0, 25); }
        public List<Invitation> GetInvitations(int offset, int limit) { return GetInvitations("me", offset, limit); }
        public List<Invitation> GetInvitations(string userId) { return GetInvitations(userId, 0, 25); }
        public List<Invitation> GetInvitations(string userId, int offset, int limit) { requireAuthorization(); return new List<Invitation>(); }
        public Event GetEvent(string eventId) { return FetchObject<Event>(eventId); }
        public byte[] GetEventImage(string eventId) { return GetEventImage(eventId, ImageType.NORMAL); }
        public byte[] GetEventImage(string eventId, ImageType imageType) { return FetchImage(eventId, "picture", imageType); }
        public string CreateEvent(string name, DateTime startTime, DateTime endTime) { requireAuthorization(); return string.Empty; }
        public void DeleteEvent(string eventId) { requireAuthorization(); Delete(eventId); }
        public List<EventInvitee> GetInvited(string eventId) { return new List<EventInvitee>(); }
        public List<EventInvitee> GetAttending(string eventId) { return new List<EventInvitee>(); }
        public List<EventInvitee> GetMaybeAttending(string eventId) { return new List<EventInvitee>(); }
        public List<EventInvitee> GetNoReplies(string eventId) { return new List<EventInvitee>(); }
        public List<EventInvitee> GetDeclined(string eventId) { return new List<EventInvitee>(); }
        public void AcceptInvitation(string eventId) { requireAuthorization(); }
        public void MaybeInvitation(string eventId) { requireAuthorization(); }
        public void DeclineInvitation(string eventId) { requireAuthorization(); }
        public List<Event> Search(string query) { return Search(query, 0, 25); }
        public List<Event> Search(string query, int offset, int limit) { return new List<Event>(); }
    }
}
