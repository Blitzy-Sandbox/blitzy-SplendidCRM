#nullable disable
using System;
namespace Spring.Social.Office365.Api
{
    [Serializable]
    public class Folder { public string Id { get; set; } public string DisplayName { get; set; } public int TotalItemCount { get; set; } public int UnreadItemCount { get; set; } }
}
