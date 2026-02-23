#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable] public class Location { public string DisplayName { get; set; } public string LocationEmailAddress { get; set; } public PhysicalAddress Address { get; set; } public OutlookGeoCoordinates Coordinates { get; set; } }
}
