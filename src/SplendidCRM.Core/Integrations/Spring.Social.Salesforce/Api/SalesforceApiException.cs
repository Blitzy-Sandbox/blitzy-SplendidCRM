#nullable disable
using System;
namespace Spring.Social.Salesforce.Api
{
    [Serializable]
    public class SalesforceApiException : Exception
    {
        public SalesforceApiException(string message) : base(message) { }
        public SalesforceApiException(string message, Exception innerException) : base(message, innerException) { }
    }
}
