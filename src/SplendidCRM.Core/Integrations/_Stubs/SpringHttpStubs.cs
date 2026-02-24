/*******************************************************************************************************************
 * Spring.Http stub — Minimal replacement for Spring.Rest.dll Spring.Http types
 * Created as part of .NET Framework 4.8 → .NET 10 migration (AAP section 0.7.4).
 * These stubs satisfy compilation requirements for dormant Spring.Social.* integration files.
 * They are NOT intended for production execution.
 *******************************************************************************************************************/
#nullable disable
using System;
using System.Net;
using System.Text;

namespace Spring.Http
{
    /// <summary>
    /// Stub replacement for Spring.Http.HttpUtils from Spring.Rest.dll.
    /// Provides URL encoding helpers consumed by dormant Spring.Social integration stubs.
    /// </summary>
    public static class HttpUtils
    {
        public static string UrlEncode(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }

        public static string FormEncode(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }
    }

    /// <summary>
    /// Stub replacement for Spring.Http.HttpMethod from Spring.Rest.dll.
    /// Represents HTTP request methods used in Spring.Social integration stubs.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public enum HttpMethod
    {
        GET,
        POST,
        PUT,
        DELETE,
        HEAD,
        OPTIONS,
        PATCH
    }

    /// <summary>
    /// Stub replacement for Spring.Http.HttpResponseMessage&lt;T&gt; from Spring.Rest.dll.
    /// Represents an HTTP response message with a typed body payload.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class HttpResponseMessage<T>
    {
        /// <summary>Gets or sets the HTTP status code.</summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>Gets or sets the HTTP status description.</summary>
        public string StatusDescription { get; set; }

        /// <summary>Gets or sets the response body.</summary>
        public T Body { get; set; }

        /// <summary>Gets or sets the response headers.</summary>
        public HttpHeaders Headers { get; set; }
    }

    /// <summary>
    /// Stub replacement for Spring.Http.HttpHeaders from Spring.Rest.dll.
    /// Represents HTTP response headers. Dormant stub — not executed at runtime.
    /// </summary>
    public class HttpHeaders
    {
        /// <summary>Gets or sets the Content-Type header parsed as a MediaType.</summary>
        public MediaType ContentType { get; set; }
    }

    /// <summary>
    /// Stub replacement for Spring.Http.MediaType from Spring.Rest.dll.
    /// Represents an HTTP media/content-type value with optional charset.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class MediaType
    {
        /// <summary>Gets or sets the charset encoding for this media type.</summary>
        public Encoding CharSet { get; set; }
    }
}

namespace Spring.Http.Client
{
    /// <summary>
    /// Stub replacement for Spring.Http.Client.IClientHttpRequest from Spring.Rest.dll.
    /// Represents an HTTP request that can be executed. Dormant stub — not executed at runtime.
    /// </summary>
    public interface IClientHttpRequest
    {
    }

    /// <summary>
    /// Stub replacement for Spring.Http.Client.IClientHttpRequestFactoryCreation from Spring.Rest.dll.
    /// Provides URI and factory method for creating HTTP requests. Dormant stub — not executed at runtime.
    /// </summary>
    public interface IClientHttpRequestFactoryCreation
    {
        /// <summary>
        /// Gets the URI for the request being created.
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// Creates the HTTP request.
        /// </summary>
        IClientHttpRequest Create();
    }
}

namespace Spring.Http.Client.Interceptor
{
    /// <summary>
    /// Stub replacement for Spring.Http.Client.Interceptor.IClientHttpRequestFactoryInterceptor from Spring.Rest.dll.
    /// Intercepts HTTP request factory creation. Dormant stub — not executed at runtime.
    /// </summary>
    public interface IClientHttpRequestFactoryInterceptor
    {
        /// <summary>
        /// Intercepts the creation of an HTTP request.
        /// </summary>
        Spring.Http.Client.IClientHttpRequest Create(Spring.Http.Client.IClientHttpRequestFactoryCreation creation);
    }
}

namespace Spring.Rest.Client
{
    /// <summary>
    /// Stub replacement for Spring.Rest.Client.IResponseErrorHandler from Spring.Rest.dll.
    /// Interface for handling HTTP response errors in Spring.Social integration stubs.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public interface IResponseErrorHandler
    {
        /// <summary>
        /// Determines whether the given response has an error.
        /// </summary>
        bool HasError(Uri requestUri, Spring.Http.HttpMethod requestMethod, Spring.Http.HttpResponseMessage<byte[]> response);

        /// <summary>
        /// Handles the error in the given response.
        /// </summary>
        void HandleError(Uri requestUri, Spring.Http.HttpMethod requestMethod, Spring.Http.HttpResponseMessage<byte[]> response);
    }
}

namespace Spring.Rest.Client.Support
{
    /// <summary>
    /// Stub replacement for Spring.Rest.Client.Support.DefaultResponseErrorHandler from Spring.Rest.dll.
    /// Base class for HTTP response error handlers in Spring.Social integration stubs.
    /// TwitterErrorHandler inherits from this to intercept 4xx/5xx errors.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class DefaultResponseErrorHandler : Spring.Rest.Client.IResponseErrorHandler
    {
        /// <summary>
        /// Stub implementation — always returns false. Dormant stub.
        /// </summary>
        public virtual bool HasError(Uri requestUri, Spring.Http.HttpMethod requestMethod, Spring.Http.HttpResponseMessage<byte[]> response)
        {
            return false;
        }

        /// <summary>
        /// Stub implementation — no-op. Dormant stub.
        /// Overridden by TwitterErrorHandler to map HTTP error codes to TwitterApiException.
        /// </summary>
        public virtual void HandleError(Uri requestUri, Spring.Http.HttpMethod requestMethod, Spring.Http.HttpResponseMessage<byte[]> response)
        {
            // Dormant stub — not executed at runtime
        }
    }
}
