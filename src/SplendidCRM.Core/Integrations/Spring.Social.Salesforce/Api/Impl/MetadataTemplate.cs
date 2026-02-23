#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Salesforce.Api;

namespace Spring.Social.Salesforce.Api.Impl
{
    class MetadataTemplate : AbstractSalesforceOperations, IMetadataOperations
    {
        public DescribeGlobal DescribeGlobal() { return null; }
        public DescribeSObject DescribeSObject(string name) { return null; }
    }
}
