#region License

/*
 * Copyright (C) 2012 SplendidCRM Software, Inc. All Rights Reserved. 
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
// is not supported in .NET Core / .NET 10. The [SecurityPermission] attribute was removed
// from GetObjectData() accordingly.

namespace Spring.Social.Salesforce.Api
{
	/// <summary>
	/// The exception that is thrown when a error occurs while consuming Salesforce REST API.
	/// </summary>
	/// <author>Bruno Baia</author>
	/// <author>SplendidCRM (.NET)</author>
	// Migration note: [Serializable] is now unconditional — removed #if !SILVERLIGHT guards
	// as SILVERLIGHT conditional compilation is not applicable in .NET 10.
	[Serializable]
	// Migration note: Base class changed from SocialException (Spring.Social.Core, discontinued)
	// to System.Exception, the standard .NET base class. This is sufficient for a dormant stub.
	public class SalesforceApiException : Exception
	{
		private SalesforceApiError error;

		/// <summary>
		/// Gets the Salesforce error.
		/// </summary>
		public SalesforceApiError Error
		{
			get { return this.error; }
		}

		/// <summary>
		/// Creates a new instance of the <see cref="SalesforceApiException"/> class.
		/// </summary>
		/// <param name="message">A message about the exception.</param>
		/// <param name="error">The Salesforce error.</param>
		public SalesforceApiException(string message, SalesforceApiError error) : base(message)
		{
			this.error = error;
		}

		/// <summary>
		/// Creates a new instance of the <see cref="SalesforceApiException"/> class.
		/// </summary>
		/// <param name="message">A message about the exception.</param>
		/// <param name="innerException">The inner exception that is the cause of the current exception.</param>
		public SalesforceApiException(string message, Exception innerException) : base(message, innerException)
		{
			this.error = SalesforceApiError.Unknown;
		}

		/// <summary>
		/// Creates a new instance of the <see cref="SalesforceApiException"/> class.
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
		// now unconditional in .NET 10 as SILVERLIGHT platform is no longer applicable.
		protected SalesforceApiException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
			if (info != null)
			{
				this.error = (SalesforceApiError)info.GetValue("Error", typeof(SalesforceApiError));
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
		// The GetObjectData method itself is preserved unchanged.
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
