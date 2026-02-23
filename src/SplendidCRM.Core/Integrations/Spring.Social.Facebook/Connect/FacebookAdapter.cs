#nullable disable
using System;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Connect
{
    public class FacebookAdapter
    {
        public bool Test(IFacebook api) { return api != null && api.IsAuthorized; }
        public void SetConnectionValues(IFacebook api, object values) { }
        public object FetchUserProfile(IFacebook api) { return api?.UserOperations?.GetUserProfile(); }
    }
}
