#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Salesforce.Api
{
    [Serializable] public class Field { public string Name { get; set; } public string Label { get; set; } public string Type { get; set; } public int Length { get; set; } public int Precision { get; set; } public int Scale { get; set; } public bool Nillable { get; set; } public bool Custom { get; set; } public IList<PicklistEntry> PicklistValues { get; set; } public string DefaultValue { get; set; } }
}
