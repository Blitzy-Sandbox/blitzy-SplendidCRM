#nullable disable
using System;
namespace Spring.Social.Office365.Api
{
    [Serializable]
    public class Subscription { public string Id { get; set; } public string Resource { get; set; } public string ChangeType { get; set; } public DateTime? ExpirationDateTime { get; set; } }
}
