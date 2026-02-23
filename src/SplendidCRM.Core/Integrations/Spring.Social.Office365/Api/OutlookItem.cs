#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable] public class OutlookItem { public string Id { get; set; } public string ChangeKey { get; set; } public IList<string> Categories { get; set; } public DateTimeOffset CreatedDateTime { get; set; } public DateTimeOffset LastModifiedDateTime { get; set; } }
}
