#nullable disable
using System;
using System.Runtime.Serialization;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class SocialException : Exception
    {
        public SocialException() { }
        public SocialException(string message) : base(message) { }
        public SocialException(string message, Exception innerException) : base(message, innerException) { }
        protected SocialException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class FacebookApiException : SocialException
    {
        private FacebookApiError error;
        public FacebookApiError Error { get { return error; } }
        public FacebookApiException(string message, FacebookApiError error) : base(message) { this.error = error; }
        public FacebookApiException(string message, Exception innerException) : base(message, innerException) { this.error = FacebookApiError.Unknown; }
        protected FacebookApiException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public override void GetObjectData(SerializationInfo info, StreamingContext context) { base.GetObjectData(info, context); }
    }
}
