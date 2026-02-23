#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class OpenGraphTemplate : AbstractFacebookOperations, IOpenGraphOperations
    {
        public OpenGraphTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public string PublishAction(string action, string objectType, string objectUrl) { requireAuthorization(); return string.Empty; }
    }
}
