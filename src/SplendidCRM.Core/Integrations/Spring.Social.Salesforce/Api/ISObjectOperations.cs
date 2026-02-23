#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Salesforce.Api
{
    public interface ISObjectOperations
    {
        object GetSObjects();
        object DescribeSObject(string name);
        object GetRow(string type, string id, params string[] fields);
        object Create(string type, object fields);
        object Update(string type, string id, object fields);
        void Delete(string type, string id);
    }
}
