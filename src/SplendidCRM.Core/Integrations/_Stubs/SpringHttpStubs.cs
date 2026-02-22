/*******************************************************************************************************************
 * Spring.Http stub — Minimal replacement for Spring.Rest.dll Spring.Http.HttpUtils
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
