#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.LinkedIn.Api
{
    [Serializable] public class LinkedInFullProfile { public string Id { get; set; } public string FirstName { get; set; } public string LastName { get; set; } public string Headline { get; set; } public string Industry { get; set; } public string Summary { get; set; } public IList<Position> Positions { get; set; } public IList<Education> Educations { get; set; } public IList<ImAccount> ImAccounts { get; set; } public IList<TwitterAccount> TwitterAccounts { get; set; } public IList<LinkedInUrl> Urls { get; set; } public IList<PhoneNumber> PhoneNumbers { get; set; } public IList<Recommendation> Recommendations { get; set; } }
}
