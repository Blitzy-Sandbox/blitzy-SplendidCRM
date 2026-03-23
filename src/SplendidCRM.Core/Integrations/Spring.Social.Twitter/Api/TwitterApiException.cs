#region License

/*
 * Copyright 2002-2012 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;
using System.Runtime.Serialization;
// Migration note: Removed 'using System.Security.Permissions;' — Code Access Security (CAS)
// is not supported in .NET Core / .NET 10 without the System.Security.Permissions NuGet package.
// The [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)] attribute
// has been removed from GetObjectData() accordingly.
// Preserved per AAP §0.8.1 minimal change clause (only migration-required changes applied).

namespace Spring.Social.Twitter.Api
{
    // Migration note: SocialException base class stub — replaces Spring.Social.Core.SocialException
    // from the discontinued Spring.Social.Core library (Spring.Social.Core.dll, Spring.NET framework).
    // This local stub satisfies compilation requirements for the dormant Spring.Social.Twitter
    // integration. It is NOT intended for production execution.
    // Pattern: consistent with Spring.Social.Facebook.Api.SocialException local stub approach.
#if !SILVERLIGHT
    [Serializable]
#endif
    public class SocialException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SocialException"/> class.
        /// </summary>
        public SocialException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SocialException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialException"/> class
        /// with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception that is the cause of the current exception.</param>
        public SocialException(string message, Exception innerException) : base(message, innerException) { }

#if !SILVERLIGHT
        /// <summary>
        /// Initializes a new instance of the <see cref="SocialException"/> class
        /// with serialized data.
        /// </summary>
        /// <param name="info">The serialization info that holds the serialized object data.</param>
        /// <param name="context">The streaming context that contains contextual information.</param>
        protected SocialException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

    /// <summary>
    /// The exception that is thrown when a error occurs while consuming Twitter REST API.
    /// </summary>
    /// <author>Bruno Baia</author>
#if !SILVERLIGHT
    [Serializable]
#endif
    public class TwitterApiException : SocialException
    {
        private TwitterApiError error;

        /// <summary>
        /// Gets the Twitter error.
        /// </summary>
        public TwitterApiError Error
        {
            get { return this.error; }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="TwitterApiException"/> class.
        /// </summary>
        /// <param name="message">A message about the exception.</param>
        /// <param name="error">The Twitter error.</param>
        public TwitterApiException(string message, TwitterApiError error)
            : base(message)
        {
            this.error = error;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="TwitterApiException"/> class.
        /// </summary>
        /// <param name="message">A message about the exception.</param>
        /// <param name="innerException">The inner exception that is the cause of the current exception.</param>
        public TwitterApiException(string message, Exception innerException)
            : base(message, innerException)
        {
            this.error = TwitterApiError.Unknown;
        }

#if !SILVERLIGHT
        /// <summary>
        /// Creates a new instance of the <see cref="TwitterApiException"/> class.
        /// </summary>
        /// <param name="info">
        /// The <see cref="System.Runtime.Serialization.SerializationInfo"/>
        /// that holds the serialized object data about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="System.Runtime.Serialization.StreamingContext"/>
        /// that contains contextual information about the source or destination.
        /// </param>
        protected TwitterApiException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info != null)
            {
                this.error = (TwitterApiError)info.GetValue("Error", typeof(TwitterApiError));
            }
        }

        /// <summary>
        /// Populates the <see cref="System.Runtime.Serialization.SerializationInfo"/> with 
        /// information about the exception.
        /// </summary>
        /// <param name="info">
        /// The <see cref="System.Runtime.Serialization.SerializationInfo"/> that holds 
        /// the serialized object data about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="System.Runtime.Serialization.StreamingContext"/> that contains contextual 
        /// information about the source or destination.
        /// </param>
        // Migration note: [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        // removed — Code Access Security (CAS) is not supported in .NET Core / .NET 10.
        // The GetObjectData method itself is preserved unchanged from the original source.
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            if (info != null)
            {
                info.AddValue("Error", this.error);
            }
        }
#endif
    }
}
