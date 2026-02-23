#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Salesforce.Api
{
    public interface IMetadataOperations { DescribeGlobal DescribeGlobal(); DescribeSObject DescribeSObject(string name); }
}
