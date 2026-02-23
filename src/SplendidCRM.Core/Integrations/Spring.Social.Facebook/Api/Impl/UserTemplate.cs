#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class UserTemplate : AbstractFacebookOperations, IUserOperations
    {
        public UserTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public FacebookProfile GetUserProfile() { requireAuthorization(); return default(FacebookProfile); }
        public FacebookProfile GetUserProfile(string facebookId) { requireAuthorization(); return default(FacebookProfile); }
        public byte[] GetUserProfileImage() { requireAuthorization(); return new byte[0]; }
        public byte[] GetUserProfileImage(string userId) { requireAuthorization(); return new byte[0]; }
        public byte[] GetUserProfileImage(ImageType imageType) { requireAuthorization(); return new byte[0]; }
        public byte[] GetUserProfileImage(string userId, ImageType imageType) { requireAuthorization(); return new byte[0]; }
        public List<string> GetUserPermissions() { requireAuthorization(); return new List<string>(); }
        public List<Reference> Search(string query) { requireAuthorization(); return new List<Reference>(); }
    }
}
