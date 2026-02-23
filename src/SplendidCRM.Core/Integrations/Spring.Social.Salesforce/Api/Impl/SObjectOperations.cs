#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Salesforce.Api;

namespace Spring.Social.Salesforce.Api.Impl
{
    class SObjectOperations : AbstractSalesforceOperations, ISObjectOperations
    {
        public object GetSObjects() { return null; }
        public object DescribeSObject(string name) { return null; }
        public object GetRow(string type, string id, params string[] fields) { return null; }
        public object Create(string type, object fields) { return null; }
        public object Update(string type, string id, object fields) { return null; }
        public void Delete(string type, string id) { }
    }
}
