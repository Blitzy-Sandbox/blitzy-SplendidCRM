#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable] public class MailFolder { public string Id { get; set; } public string DisplayName { get; set; } public string ParentFolderId { get; set; } public int ChildFolderCount { get; set; } public int TotalItemCount { get; set; } public int UnreadItemCount { get; set; } }
}
