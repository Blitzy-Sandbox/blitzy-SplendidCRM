#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class PageTemplate : AbstractFacebookOperations, IPageOperations
    {
        public PageTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public Page GetPage(string pageId) { requireAuthorization(); return default(Page); }
        public bool IsPageAdmin(string pageId) { requireAuthorization(); return false; }
        public List<Account> GetAccounts() { requireAuthorization(); return new List<Account>(); }
        public string Post(string pageId, string message) { requireAuthorization(); return string.Empty; }
        public string Post(string pageId, string message, FacebookLink link) { requireAuthorization(); return string.Empty; }
        public string PostPhoto(string pageId, string albumId, Resource photo) { requireAuthorization(); return string.Empty; }
        public string PostPhoto(string pageId, string albumId, Resource photo, string caption) { requireAuthorization(); return string.Empty; }
    }
}
