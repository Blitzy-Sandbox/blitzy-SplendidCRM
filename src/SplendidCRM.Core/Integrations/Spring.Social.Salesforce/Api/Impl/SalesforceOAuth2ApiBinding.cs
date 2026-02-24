#nullable disable
using System;
using Spring.Social.Facebook.Api.Impl;

namespace Spring.Social.Salesforce.Api.Impl
{
    public class SalesforceOAuth2ApiBinding : AbstractOAuth2ApiBinding
    {
        public SalesforceOAuth2ApiBinding(string accessToken) : base(accessToken) { }
    }
}
