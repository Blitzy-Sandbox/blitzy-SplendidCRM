#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class DirectMessageTemplate : AbstractTwitterOperations, IDirectMessageOperations
    {
        public List<DirectMessage> GetDirectMessagesReceived() { return new List<DirectMessage>(); }
        public List<DirectMessage> GetDirectMessagesReceived(int count) { return new List<DirectMessage>(); }
        public List<DirectMessage> GetDirectMessagesReceived(int count, long sinceId, long maxId) { return new List<DirectMessage>(); }
        public List<DirectMessage> GetDirectMessagesSent() { return new List<DirectMessage>(); }
        public List<DirectMessage> GetDirectMessagesSent(int count) { return new List<DirectMessage>(); }
        public List<DirectMessage> GetDirectMessagesSent(int count, long sinceId, long maxId) { return new List<DirectMessage>(); }
        public DirectMessage GetDirectMessage(long id) { return null; }
        public DirectMessage SendDirectMessage(string toScreenName, string text) { return null; }
        public DirectMessage SendDirectMessage(long toUserId, string text) { return null; }
        public void DeleteDirectMessage(long id) { }
    }
}
