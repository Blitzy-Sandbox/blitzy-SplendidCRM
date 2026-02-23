#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable] public class FollowupFlag { public DateTimeTimeZone CompletedDateTime { get; set; } public DateTimeTimeZone DueDateTime { get; set; } public DateTimeTimeZone StartDateTime { get; set; } public string FlagStatus { get; set; } }
}
