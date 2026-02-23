#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Salesforce.Api
{
    [Serializable] public class QueryResult { public int TotalSize { get; set; } public bool Done { get; set; } public string NextRecordsUrl { get; set; } public IList<Dictionary<string, object>> Records { get; set; } }
}
