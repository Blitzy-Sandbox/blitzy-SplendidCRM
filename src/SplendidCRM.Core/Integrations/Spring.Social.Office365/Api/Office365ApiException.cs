#nullable disable
using System;
namespace Spring.Social.Office365.Api
{
    [Serializable]
    public class Office365ApiException : Exception
    {
        public Office365ApiException(string message) : base(message) { }
        public Office365ApiException(string message, Exception inner) : base(message, inner) { }
    }
}
