#nullable disable
using System;
namespace Spring.Social.Salesforce.Api
{
    [Serializable]
    public class SalesforceProfile
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string DisplayName { get; set; }
        public string Username { get; set; }
        public string OrganizationId { get; set; }
    }
}
