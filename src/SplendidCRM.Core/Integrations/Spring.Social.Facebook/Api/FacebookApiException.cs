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
using System.Security.Permissions;

namespace Spring.Social.Facebook.Api
{
	// Stub for Spring.Social.Core.SocialException — replaced during .NET 10 migration.
	// Spring.Social.Core (Spring.Social.Core.dll, Spring.NET framework) is a discontinued
	// library with no .NET 10 NuGet equivalent. This local stub satisfies the compilation
	// requirements for the dormant Spring.Social.Facebook integration.
	// Per AAP §0.7.4: dormant stub, MUST compile on .NET 10, NOT expected to execute.
	// Per AAP §0.8.1: all public class signatures preserved for Enterprise Edition upgrade path.
	// Migration note: [Serializable] applied unconditionally — removed #if !SILVERLIGHT guards
	// as SILVERLIGHT conditional compilation is not applicable on .NET 10.
	[Serializable]
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
		/// with a specified error message and a reference to the inner exception that is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The inner exception that is the cause of the current exception.</param>
		public SocialException(string message, Exception innerException) : base(message, innerException) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="SocialException"/> class with serialized data.
		/// </summary>
		/// <param name="info">
		/// The <see cref="System.Runtime.Serialization.SerializationInfo"/> that holds the serialized
		/// object data about the exception being thrown.
		/// </param>
		/// <param name="context">
		/// The <see cref="System.Runtime.Serialization.StreamingContext"/> that contains contextual
		/// information about the source or destination.
		/// </param>
		protected SocialException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}

	/// <summary>
	/// The exception that is thrown when a error occurs while consuming Facebook REST API.
	/// </summary>
	/// <author>Bruno Baia</author>
	// Migration note: [Serializable] applied unconditionally — removed #if !SILVERLIGHT
	// preprocessor guard as SILVERLIGHT conditional compilation is not applicable on .NET 10
	// (AAP §0.8.1 minimal change clause: only migration-required changes applied).
	[Serializable]
	public class FacebookApiException : SocialException
	{
		private FacebookApiError error;

		/// <summary>
		/// Gets the Facebook error.
		/// </summary>
		public FacebookApiError Error
		{
			get { return this.error; }
		}

		/// <summary>
		/// Creates a new instance of the <see cref="FacebookApiException"/> class.
		/// </summary>
		/// <param name="message">A message about the exception.</param>
		/// <param name="error">The Facebook error.</param>
		public FacebookApiException(string message, FacebookApiError error)
			: base(message)
		{
			this.error = error;
		}

		/// <summary>
		/// Creates a new instance of the <see cref="FacebookApiException"/> class.
		/// </summary>
		/// <param name="message">A message about the exception.</param>
		/// <param name="innerException">The inner exception that is the cause of the current exception.</param>
		public FacebookApiException(string message, Exception innerException)
			: base(message, innerException)
		{
			this.error = FacebookApiError.Unknown;
		}

		/// <summary>
		/// Creates a new instance of the <see cref="FacebookApiException"/> class.
		/// </summary>
		/// <param name="info">
		/// The <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// that holds the serialized object data about the exception being thrown.
		/// </param>
		/// <param name="context">
		/// The <see cref="System.Runtime.Serialization.StreamingContext"/>
		/// that contains contextual information about the source or destination.
		/// </param>
		// Migration note: Serialization constructor previously guarded by #if !SILVERLIGHT —
		// now unconditional in .NET 10 as SILVERLIGHT platform is not applicable (AAP §0.8.1).
		protected FacebookApiException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			if (info != null)
			{
				this.error = (FacebookApiError)info.GetValue("Error", typeof(FacebookApiError));
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
		// Migration note: GetObjectData previously guarded by #if !SILVERLIGHT — now unconditional
		// in .NET 10 as SILVERLIGHT platform is not applicable (AAP §0.8.1 minimal change clause).
		// [SecurityPermission] preserved from original source for binary serialization compatibility;
		// requires System.Security.Permissions NuGet package (10.0.0). CAS is not enforced at
		// runtime on .NET 10 but attribute is retained per schema requirements.
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			if (info != null)
			{
				info.AddValue("Error", this.error);
			}
		}
	}
}
