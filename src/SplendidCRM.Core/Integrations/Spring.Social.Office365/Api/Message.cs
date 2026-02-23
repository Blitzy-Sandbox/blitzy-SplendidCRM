#nullable disable
using System;
namespace Spring.Social.Office365.Api
{
    [Serializable]
    public class Message { public string Id { get; set; } public string Subject { get; set; } public string Body { get; set; } public bool IsRead { get; set; } }
}
