#nullable disable
using System;
using Spring.Social.Salesforce.Api;
namespace Spring.Social.Salesforce.Api.Impl
{
    public class SalesforceTemplate : ISalesforce
    {
        public SalesforceTemplate(string instanceUrl, string accessToken) { IsAuthorized = true; }
        public bool IsAuthorized { get; private set; }
        public IApiOperations ApiOperations { get; private set; }
        public IQueryOperations QueryOperations { get; private set; }
        public IRecentOperations RecentOperations { get; private set; }
        public ISearchOperations SearchOperations { get; private set; }
        public ISObjectOperations SObjectOperations { get; private set; }
    }
}
