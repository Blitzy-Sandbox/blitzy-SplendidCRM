#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.LinkedIn.Api;

namespace Spring.Social.LinkedIn.Api.Impl
{
    class ProfileTemplate : AbstractLinkedInOperations, IProfileOperations
    {
        public LinkedInProfile GetUserProfile() { return null; }
        public LinkedInProfile GetUserProfileById(string id) { return null; }
        public LinkedInProfile GetUserProfileByPublicUrl(string publicProfileUrl) { return null; }
        public List<LinkedInProfile> Search(string query) { return new List<LinkedInProfile>(); }
    }
}
