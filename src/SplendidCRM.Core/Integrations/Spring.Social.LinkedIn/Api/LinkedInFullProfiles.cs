#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.LinkedIn.Api
{
    [Serializable] public class LinkedInFullProfiles { public int Total { get; set; } public IList<LinkedInFullProfile> Values { get; set; } }
}
