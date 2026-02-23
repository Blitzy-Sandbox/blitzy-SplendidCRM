#nullable disable
using System;
namespace Spring.Social.Office365
{
    /// <summary>Utility methods for Office 365 integration.</summary>
    public static class Office365Utils
    {
        public static string FormatDateTimeOffset(DateTimeOffset dto) { return dto.ToString("o"); }
    }
}
