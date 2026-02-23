#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.LinkedIn.Api
{
    [Serializable] public class LinkedInProfiles { public int Total { get; set; } public IList<LinkedInProfile> Values { get; set; } }
}
