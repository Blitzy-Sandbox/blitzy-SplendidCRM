#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Salesforce.Api;

namespace Spring.Social.Salesforce.Api.Impl
{
    class UserTemplate : AbstractSalesforceOperations, IUserOperations
    {
        public bool GetPasswordExpiration(string version, string userId) { return false; }
        public void SetPassword(string version, string userId, string password) { }
        public string ResetPassword(string version, string userId) { return string.Empty; }
    }
}
