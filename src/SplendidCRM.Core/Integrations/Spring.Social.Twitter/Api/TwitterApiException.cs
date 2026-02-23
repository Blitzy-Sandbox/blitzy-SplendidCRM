#nullable disable
using System;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class TwitterApiException : Exception
    {
        public TwitterApiError Error { get; set; }
        public TwitterApiException(string message, TwitterApiError error) : base(message) { Error = error; }
    }
}
