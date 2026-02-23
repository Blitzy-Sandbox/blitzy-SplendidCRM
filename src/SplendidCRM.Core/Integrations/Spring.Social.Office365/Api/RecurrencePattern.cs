#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable] public class RecurrencePattern { public string Type { get; set; } public int Interval { get; set; } public int DayOfMonth { get; set; } public IList<string> DaysOfWeek { get; set; } public string FirstDayOfWeek { get; set; } public string Index { get; set; } public int Month { get; set; } }
}
