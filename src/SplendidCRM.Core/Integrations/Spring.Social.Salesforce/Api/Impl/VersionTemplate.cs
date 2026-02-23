#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Salesforce.Api;

namespace Spring.Social.Salesforce.Api.Impl
{
    class VersionTemplate : AbstractSalesforceOperations, IVersionOperations
    {
        public IList<SalesforceVersion> GetVersions() { return new List<SalesforceVersion>(); }
    }
}
