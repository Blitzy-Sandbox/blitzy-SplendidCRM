#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Salesforce.Api
{
    [Serializable] public class DescribeGlobal { public string Encoding { get; set; } public int MaxBatchSize { get; set; } public IList<DescribeGlobalSObject> SObjects { get; set; } }
}
