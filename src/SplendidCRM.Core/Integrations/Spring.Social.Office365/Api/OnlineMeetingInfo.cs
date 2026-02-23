#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable] public class OnlineMeetingInfo { public string JoinUrl { get; set; } public string ConferenceId { get; set; } public string QuickDial { get; set; } public IList<Phone> Phones { get; set; } }
}
