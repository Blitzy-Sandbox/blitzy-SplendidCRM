#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.LinkedIn.Api
{
    [Serializable] public class SearchParameters { public string Keywords { get; set; } public string FirstName { get; set; } public string LastName { get; set; } public string CompanyName { get; set; } public int Start { get; set; } public int Count { get; set; } }
}
