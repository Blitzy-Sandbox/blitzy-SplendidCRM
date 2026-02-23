#nullable disable
using System;
using System.Net;
using System.Text;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class DefaultResponseErrorHandler
    {
        public virtual void HandleError(Uri requestUri, HttpMethod requestMethod, HttpResponseMessage<byte[]> response) { }
    }

    public enum HttpMethod { GET, POST, PUT, DELETE, HEAD, OPTIONS, PATCH }

    public class HttpResponseMessage<T>
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public T Body { get; set; }
        public HttpHeaders Headers { get; set; }
    }

    public class HttpHeaders { public MediaType ContentType { get; set; } }
    public class MediaType { public Encoding CharSet { get; set; } }

    public class FacebookErrorHandler : DefaultResponseErrorHandler
    {
        private static readonly Encoding DEFAULT_CHARSET = new UTF8Encoding(false);

        public override void HandleError(Uri requestUri, HttpMethod requestMethod, HttpResponseMessage<byte[]> response)
        {
            HandleClientErrors(response);
            HandleServerErrors(response.StatusCode, ExtractErrorDetailsFromResponse(response));
        }

        private void HandleClientErrors(HttpResponseMessage<byte[]> response)
        {
            string errorDetails = ExtractErrorDetailsFromResponse(response);
            if (string.IsNullOrEmpty(errorDetails)) return;
            JsonValue json;
            if (!JsonValue.TryParse(errorDetails, out json)) return;
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new FacebookApiException(errorDetails, FacebookApiError.NotAuthorized);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new FacebookApiException(errorDetails, FacebookApiError.OperationNotPermitted);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new FacebookApiException(errorDetails, FacebookApiError.ResourceNotFound);
        }

        private void HandleServerErrors(HttpStatusCode statusCode, string errorDetails)
        {
            if (statusCode == HttpStatusCode.InternalServerError)
                throw new FacebookApiException(errorDetails ?? "Internal Server Error", FacebookApiError.Server);
            if (statusCode == HttpStatusCode.BadGateway)
                throw new FacebookApiException(errorDetails ?? "Server Down", FacebookApiError.ServerDown);
            if (statusCode == HttpStatusCode.ServiceUnavailable)
                throw new FacebookApiException(errorDetails ?? "Server Overloaded", FacebookApiError.ServerOverloaded);
        }

        private string ExtractErrorDetailsFromResponse(HttpResponseMessage<byte[]> response)
        {
            if (response.Body == null) return null;
            Encoding charset = (response.Headers?.ContentType?.CharSet) ?? DEFAULT_CHARSET;
            return charset.GetString(response.Body);
        }
    }
}
