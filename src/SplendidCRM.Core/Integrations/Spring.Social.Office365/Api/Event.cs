#nullable disable
using System;
namespace Spring.Social.Office365.Api
{
    [Serializable]
    public class Event { public string Id { get; set; } public string Subject { get; set; } public DateTime? Start { get; set; } public DateTime? End { get; set; } public string Location { get; set; } }
}
