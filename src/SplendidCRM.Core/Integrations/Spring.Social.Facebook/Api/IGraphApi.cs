#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface IGraphApi
    {
        T FetchObject<T>(string objectId) where T : class;
        T FetchObject<T>(string objectId, System.Collections.Specialized.NameValueCollection queryParameters) where T : class;
        List<T> FetchConnections<T>(string objectId, string connectionName, string[] fields) where T : class;
        List<T> FetchConnections<T>(string objectId, string connectionName, System.Collections.Specialized.NameValueCollection queryParameters) where T : class;
        byte[] FetchImage(string objectId, string connectionName, ImageType imageType);
        string Publish(string objectId, string connectionName, System.Collections.Specialized.NameValueCollection data);
        void Post(string objectId, string connectionName, System.Collections.Specialized.NameValueCollection data);
        void Delete(string objectId);
        void Delete(string objectId, string connectionName);
        string ApplicationNamespace();
    }
}
