#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.LinkedIn.Api
{
    [Serializable] public class Position { public string Id { get; set; } public string Title { get; set; } public string Summary { get; set; } public Company Company { get; set; } public bool IsCurrent { get; set; } public LinkedInDate StartDate { get; set; } public LinkedInDate EndDate { get; set; } }
}
