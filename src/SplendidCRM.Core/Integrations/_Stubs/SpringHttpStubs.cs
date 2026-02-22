/*******************************************************************************************************************
 * Spring.Http stub — Minimal replacement for Spring.Rest.dll Spring.Http types
 * Created as part of .NET Framework 4.8 → .NET 10 migration (AAP section 0.7.4).
 * These stubs satisfy compilation requirements for dormant Spring.Social.* integration files.
 * They are NOT intended for production execution.
 *******************************************************************************************************************/
using System;

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
