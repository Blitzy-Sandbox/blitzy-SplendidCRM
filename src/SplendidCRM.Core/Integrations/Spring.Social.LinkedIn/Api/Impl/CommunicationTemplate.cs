#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.LinkedIn.Api;

namespace Spring.Social.LinkedIn.Api.Impl
{
    class CommunicationTemplate : AbstractLinkedInOperations, ICommunicationOperations
    {
        public void SendMessage(string subject, string body, params string[] recipientIds) { }
    }
}
