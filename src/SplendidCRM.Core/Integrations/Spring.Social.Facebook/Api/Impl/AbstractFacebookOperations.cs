#nullable disable
using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    // Stubs for Spring.Rest.Client types removed during .NET 10 migration
    public class RestTemplate : IRestOperations
    {
        public Uri BaseAddress { get; set; }
        public object ErrorHandler { get; set; }
        public T GetForObject<T>(string url) { return default(T); }
        public T GetForObject<T>(Uri uri) { return default(T); }
        public T PostForObject<T>(string url, object request) { return default(T); }
        public void Delete(string url) { }
        public void Delete(Uri uri) { }
    }

    public static class HttpUtils
    {
        public static string UrlEncode(string value) { return Uri.EscapeDataString(value ?? string.Empty); }
        public static string FormEncode(string value) { return Uri.EscapeDataString(value ?? string.Empty); }
    }

    public class JsonValue
    {
        public bool IsNull { get; }
        public bool IsArray { get; }
        public bool IsString { get; }
        public bool ContainsName(string name) { return false; }
        public JsonValue GetValue(string name) { return null; }
        public JsonValue GetValue(int index) { return null; }
        public T GetValue<T>(string name) { return default(T); }
        public T GetValue<T>() { return default(T); }
        public IList<JsonValue> GetValues() { return new List<JsonValue>(); }
        public static bool TryParse(string json, out JsonValue result) { result = null; return false; }
    }

    public interface IJsonDeserializer
    {
        object Deserialize(JsonValue json, JsonMapper mapper);
    }

    public class JsonMapper
    {
        public void RegisterDeserializer(Type type, IJsonDeserializer deserializer) { }
        public T Deserialize<T>(JsonValue value) { return default(T); }
    }

    public abstract class AbstractFacebookOperations : IGraphApi
    {
        private bool isAuthorized;
        protected RestTemplate restTemplate;
        protected string applicationNamespace;

        public AbstractFacebookOperations(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized)
        {
            this.applicationNamespace = applicationNamespace;
            this.restTemplate = restTemplate;
            this.isAuthorized = isAuthorized;
        }

        public string ApplicationNamespace() { return applicationNamespace; }
        protected void requireAuthorization() { if (!isAuthorized) throw new FacebookApiException("Unauthorized", FacebookApiError.NotAuthorized); }
        public void EnsureIsAuthorized() { requireAuthorization(); }
        
        public string BuildUrl(string path) { return "https://graph.facebook.com/v2.0/" + path; }
        public string BuildUrl(string path, string parameterName, string parameterValue)
        {
            return BuildUrl(path) + "?" + HttpUtils.UrlEncode(parameterName) + "=" + HttpUtils.UrlEncode(parameterValue);
        }
        public string BuildUrl(string path, NameValueCollection parameters)
        {
            StringBuilder sb = new StringBuilder(BuildUrl(path));
            if (parameters != null && parameters.Count > 0)
            {
                sb.Append("?");
                bool first = true;
                foreach (string key in parameters.AllKeys)
                {
                    if (!first) sb.Append("&");
                    sb.Append(HttpUtils.UrlEncode(key)).Append("=").Append(HttpUtils.UrlEncode(parameters[key]));
                    first = false;
                }
            }
            return sb.ToString();
        }

        public T FetchObject<T>(string objectId) where T : class { return restTemplate.GetForObject<T>(BuildUrl(objectId)); }
        public T FetchObject<T>(string objectId, NameValueCollection queryParameters) where T : class { return restTemplate.GetForObject<T>(BuildUrl(objectId, queryParameters)); }
        public List<T> FetchConnections<T>(string objectId, string connectionName, string[] fields) where T : class { return new List<T>(); }
        public List<T> FetchConnections<T>(string objectId, string connectionName, NameValueCollection queryParameters) where T : class { return new List<T>(); }
        public List<T> FetchConnections<T>(string objectId, string connectionType) where T : class { return FetchConnections<T>(objectId, connectionType, (NameValueCollection)null); }
        public byte[] FetchImage(string objectId, string connectionName, ImageType imageType) { return new byte[0]; }
        public string Publish(string objectId, string connectionName, NameValueCollection data) { return string.Empty; }
        public string Publish(string objectId, string connectionName, Dictionary<string, object> data) { return string.Empty; }
        public void Post(string objectId, string connectionName, NameValueCollection data) { }
        public void Delete(string objectId) { }
        public void Delete(string objectId, string connectionName) { }
        protected List<T> FetchConnectionList<T>(string baseUri, int offset, int limit) where T : class { return new List<T>(); }
    }
}
