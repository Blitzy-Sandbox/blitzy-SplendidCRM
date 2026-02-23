#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface IEventOperations
    {
        List<Invitation> GetInvitations();
        List<Invitation> GetInvitations(int offset, int limit);
        List<Invitation> GetInvitations(string userId);
        List<Invitation> GetInvitations(string userId, int offset, int limit);
        Event GetEvent(string eventId);
        byte[] GetEventImage(string eventId);
        byte[] GetEventImage(string eventId, ImageType imageType);
        string CreateEvent(string name, DateTime startTime, DateTime endTime);
        void DeleteEvent(string eventId);
        List<EventInvitee> GetInvited(string eventId);
        List<EventInvitee> GetAttending(string eventId);
        List<EventInvitee> GetMaybeAttending(string eventId);
        List<EventInvitee> GetNoReplies(string eventId);
        List<EventInvitee> GetDeclined(string eventId);
        void AcceptInvitation(string eventId);
        void MaybeInvitation(string eventId);
        void DeclineInvitation(string eventId);
        List<Event> Search(string query);
        List<Event> Search(string query, int offset, int limit);
    }
}
