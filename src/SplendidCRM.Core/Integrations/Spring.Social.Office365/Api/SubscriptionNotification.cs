#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable] public class SubscriptionNotification { public string SubscriptionId { get; set; } public string ChangeType { get; set; } public string Resource { get; set; } public ResourceData ResourceData { get; set; } public string ClientState { get; set; } }
}
