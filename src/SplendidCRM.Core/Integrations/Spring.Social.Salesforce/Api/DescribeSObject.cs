#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Salesforce.Api
{
    [Serializable] public class DescribeSObject { public string Name { get; set; } public string Label { get; set; } public string LabelPlural { get; set; } public string KeyPrefix { get; set; } public IList<Field> Fields { get; set; } public IList<ChildRelationship> ChildRelationships { get; set; } }
}
