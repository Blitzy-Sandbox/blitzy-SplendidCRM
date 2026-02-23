#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Salesforce.Api
{
    [Serializable] public class SObject : BasicSObject { public Dictionary<string, object> Fields { get; set; } public SObject() { Fields = new Dictionary<string, object>(); } }
}
