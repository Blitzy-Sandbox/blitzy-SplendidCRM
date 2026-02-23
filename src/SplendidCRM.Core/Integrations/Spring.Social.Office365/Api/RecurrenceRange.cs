#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable]
    public class RecurrenceRange
    {
        public string Type { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public int NumberOfOccurrences { get; set; }
        public string RecurrenceTimeZone { get; set; }
    }
}
