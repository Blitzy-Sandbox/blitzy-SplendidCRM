#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable] public class Attachment { public string Id { get; set; } public string Name { get; set; } public string ContentType { get; set; } public int Size { get; set; } public bool IsInline { get; set; } public byte[] ContentBytes { get; set; } }
}
