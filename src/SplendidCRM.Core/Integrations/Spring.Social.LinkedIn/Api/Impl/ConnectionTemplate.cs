#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.LinkedIn.Api;

namespace Spring.Social.LinkedIn.Api.Impl
{
    class ConnectionTemplate : AbstractLinkedInOperations, IConnectionOperations
    {
        public List<LinkedInProfile> GetConnections() { return new List<LinkedInProfile>(); }
    }
}
