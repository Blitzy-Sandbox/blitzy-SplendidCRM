#nullable disable
using System;
using Spring.Social.Facebook.Api;
using Spring.Social.Facebook.Api.Impl;

namespace Spring.Social.Facebook.Connect
{
    public class FacebookServiceProvider
    {
        private string appId;
        private string appSecret;
        public FacebookServiceProvider(string appId, string appSecret) { this.appId = appId; this.appSecret = appSecret; }
        public IFacebook GetApi(string accessToken) { return new FacebookTemplate(accessToken); }
    }
}
