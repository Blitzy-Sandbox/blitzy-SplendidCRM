#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.LinkedIn.Api
{
    [Serializable] public class Education { public string SchoolName { get; set; } public string FieldOfStudy { get; set; } public string Degree { get; set; } public LinkedInDate StartDate { get; set; } public LinkedInDate EndDate { get; set; } }
}
