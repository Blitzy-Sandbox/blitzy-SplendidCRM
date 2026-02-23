#nullable disable
using System;
namespace Spring.Social.LinkedIn.Api
{
    [Serializable]
    public class LinkedInApiException : Exception
    {
        public LinkedInApiException(string message) : base(message) { }
        public LinkedInApiException(string message, Exception inner) : base(message, inner) { }
    }
}
