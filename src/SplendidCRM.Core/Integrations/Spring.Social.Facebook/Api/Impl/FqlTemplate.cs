#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class FqlTemplate : AbstractFacebookOperations, IFqlOperations
    {
        public FqlTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public T QueryFQL<T>(string fql) where T : class { requireAuthorization(); return default(T); }
    }
}
