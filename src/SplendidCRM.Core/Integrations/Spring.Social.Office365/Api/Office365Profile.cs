#nullable disable
using System;
namespace Spring.Social.Office365.Api
{
    [Serializable]
    public class Office365Profile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string EmailAddress { get; set; }
        public string GivenName { get; set; }
        public string Surname { get; set; }
        public string UserPrincipalName { get; set; }
    }
}
