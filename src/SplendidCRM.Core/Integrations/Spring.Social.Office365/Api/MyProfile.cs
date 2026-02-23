#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable]
    public class MyProfile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Mail { get; set; }
        public string UserPrincipalName { get; set; }
        public string JobTitle { get; set; }
        public IList<string> BusinessPhones { get; set; }
    }
}
