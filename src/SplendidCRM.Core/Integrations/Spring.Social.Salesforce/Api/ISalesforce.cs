#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Salesforce.Api
{
    public interface ISalesforce
    {
        bool IsAuthorized { get; }
        IApiOperations ApiOperations { get; }
        IQueryOperations QueryOperations { get; }
        IRecentOperations RecentOperations { get; }
        ISearchOperations SearchOperations { get; }
        ISObjectOperations SObjectOperations { get; }
    }
}
