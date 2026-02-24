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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/FacebookErrorHandler.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json;              — Spring.Json has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Http;              — Spring.Http has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Rest.Client;       — Spring.Rest.Client has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Rest.Client.Support; — Spring.Rest.Client.Support has no .NET 10 NuGet equivalent
//   - ADDED:   using Spring.Social.Facebook.Api; to resolve FacebookApiException, FacebookApiError
//              from the parent namespace (previously resolved via Spring.* DLLs).
//   - ADDED:   Minimal stubs for DefaultResponseErrorHandler, HttpMethod, HttpResponseMessage<T>,
//              HttpHeaders, and MediaType (Spring.Rest.Client.Support / Spring.Http types) as the
//              single definition point in this file. JsonValue stub is NOT duplicated here — it is
//              already defined in AbstractFacebookOperations.cs in the same namespace.
//   - KEPT:    All class/method signatures, fields, business logic, XML doc comments, and
//              commented-out code blocks preserved exactly per AAP §0.8.1 Minimal Change Clause.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and §0.8.1 (Minimal Change Clause).

using System;
using System.Net;
using System.Text;

using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
	// ---------------------------------------------------------------------------
	// Stubs for Spring.Rest.Client.Support and Spring.Http types removed during
	// .NET 10 migration.  Spring.Rest.dll (v1.1) and Spring.Social.Core.dll (v1.0)
	// are discontinued .NET Framework-only libraries with no .NET 10 NuGet equivalents.
	// These minimal stubs satisfy compilation requirements for this dormant
	// Enterprise Edition integration stub.  JsonValue is defined in
	// AbstractFacebookOperations.cs and is NOT redefined here.
	// Per AAP §0.7.4 and §0.8.1: stubs only — no real implementation required.
	// ---------------------------------------------------------------------------

	/// <summary>
	/// Stub replacing Spring.Rest.Client.Support.DefaultResponseErrorHandler for .NET 10 compatibility.
	/// Base class for <see cref="FacebookErrorHandler"/>. Provides a default do-nothing implementation
	/// of HandleError that FacebookErrorHandler overrides to add Facebook-specific error handling.
	/// </summary>
	public class DefaultResponseErrorHandler
	{
		/// <summary>
		/// Default error handling — no-op stub. Overridden by <see cref="FacebookErrorHandler"/>.
		/// </summary>
		/// <param name="requestUri">The request URI.</param>
		/// <param name="requestMethod">The request method.</param>
		/// <param name="response">The response message with the error.</param>
		public virtual void HandleError(Uri requestUri, HttpMethod requestMethod, HttpResponseMessage<byte[]> response) { }
	}

	/// <summary>
	/// Stub replacing Spring.Http.HttpMethod for .NET 10 compatibility.
	/// Enumerates the HTTP methods used in Spring.Http request/response types.
	/// </summary>
	public enum HttpMethod { GET, POST, PUT, DELETE, HEAD, OPTIONS, PATCH }

	/// <summary>
	/// Stub replacing Spring.Http.HttpResponseMessage&lt;T&gt; for .NET 10 compatibility.
	/// Represents an HTTP response with a typed body, used throughout FacebookErrorHandler
	/// for status code inspection and body decoding.
	/// </summary>
	/// <typeparam name="T">The type of the response body — typically <c>byte[]</c>.</typeparam>
	public class HttpResponseMessage<T>
	{
		/// <summary>Gets or sets the HTTP status code of the response.</summary>
		public System.Net.HttpStatusCode StatusCode { get; set; }

		/// <summary>Gets or sets the HTTP status description (reason phrase) of the response.</summary>
		public string StatusDescription { get; set; }

		/// <summary>Gets or sets the typed response body.</summary>
		public T Body { get; set; }

		/// <summary>Gets or sets the HTTP response headers.</summary>
		public HttpHeaders Headers { get; set; }
	}

	/// <summary>
	/// Stub replacing Spring.Http.HttpHeaders for .NET 10 compatibility.
	/// Exposes the Content-Type header used in <see cref="FacebookErrorHandler.ExtractErrorDetailsFromResponse"/>
	/// for charset detection during error body decoding.
	/// </summary>
	public class HttpHeaders
	{
		/// <summary>Gets or sets the Content-Type media type of the response.</summary>
		public MediaType ContentType { get; set; }
	}

	/// <summary>
	/// Stub replacing Spring.Http.MediaType for .NET 10 compatibility.
	/// Provides the character set encoding declared in the Content-Type header,
	/// used by <see cref="FacebookErrorHandler.ExtractErrorDetailsFromResponse"/> for charset-aware decoding.
	/// </summary>
	public class MediaType
	{
		/// <summary>Gets or sets the character set encoding declared in the media type.</summary>
		public System.Text.Encoding CharSet { get; set; }
	}

	// ---------------------------------------------------------------------------
	// End of Spring stub definitions
	// ---------------------------------------------------------------------------

	/// <summary>
	/// Implementation of the <see cref="IResponseErrorHandler"/> that handles errors from Facebook's REST API, 
	/// interpreting them into appropriate exceptions.
	/// </summary>
	/// <author>Bruno Baia (.NET)</author>
	class FacebookErrorHandler : DefaultResponseErrorHandler
	{
		// Default encoding for JSON
		private static readonly Encoding DEFAULT_CHARSET = new UTF8Encoding(false); // Remove byte Order Mask (BOM)

		/// <summary>
		/// Handles the error in the given response. 
		/// <para/>
		/// This method is only called when HasError() method has returned <see langword="true"/>.
		/// </summary>
		/// <remarks>
		/// This implementation throws appropriate exception if the response status code 
		/// is a client code error (4xx) or a server code error (5xx). 
		/// </remarks>
		/// <param name="requestUri">The request URI.</param>
		/// <param name="requestMethod">The request method.</param>
		/// <param name="response">The response message with the error.</param>
		public override void HandleError(Uri requestUri, HttpMethod requestMethod, HttpResponseMessage<byte[]> response)
		{
			int type = (int)response.StatusCode / 100;
			if (type == 4)
			{
				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					string path = requestUri.AbsolutePath;
					if ( path.EndsWith("blocks/exists.json"         ) ||
						 path.EndsWith("lists/members/show.json"    ) ||
						 path.EndsWith("lists/subscribers/show.json") )
					{
						// Special cases: API binding will handle this
						return;
					}
				}
				this.HandleClientErrors(response);
			}
			else if (type == 5)
			{
				string errorDetails = DEFAULT_CHARSET.GetString(response.Body, 0, response.Body.Length);
				this.HandleServerErrors(response.StatusCode, errorDetails);
			}

			// if not otherwise handled, do default handling and wrap with FacebookApiException
			try
			{
				base.HandleError(requestUri, requestMethod, response);
			}
			catch (Exception ex)
			{
				throw new FacebookApiException("Error consuming Facebook REST API.", ex);
			}
		}

		private void HandleClientErrors(HttpResponseMessage<byte[]> response) 
		{
			JsonValue errorValue = this.ExtractErrorDetailsFromResponse(response);
			if (errorValue == null) 
			{
				return; // unexpected error body, can't be handled here
			}

			string errorText = null;
			if ( errorValue.ContainsName("error") )
			{
				// 04/14/2012 Paul.  The text is in a message property. 
				JsonValue errorValue2 = errorValue.GetValue("error");
				errorText = errorValue2.GetValue<string>("message");
			}
			else if ( errorValue.ContainsName("errors") )
			{
				JsonValue errorsValue = errorValue.GetValue("errors");
				if (errorsValue.IsArray) 
				{
					errorText = errorsValue.GetValue(0).GetValue<string>("message");
				}
				else if (errorsValue.IsString) 
				{
					errorText = errorsValue.GetValue<string>();
				}
			}

			if ( response.StatusCode == HttpStatusCode.Unauthorized )
			{
				if ( errorText == "Could not authenticate you." )
				{
					throw new FacebookApiException("Authorization is required for the operation, but the API binding was created without authorization.", FacebookApiError.NotAuthorized);
				}
				else if ( errorText == "Could not authenticate with OAuth." )
				{
					throw new FacebookApiException("The authorization has been revoked.", FacebookApiError.NotAuthorized);
				}
				else
				{
					throw new FacebookApiException(errorText ?? response.StatusDescription, FacebookApiError.NotAuthorized);
				}
			}
			else if ( response.StatusCode == HttpStatusCode.BadRequest )
			{
				throw new FacebookApiException(errorText, FacebookApiError.OperationNotPermitted);
			}
			else if ( response.StatusCode == HttpStatusCode.Forbidden )
			{
				throw new FacebookApiException(errorText, FacebookApiError.OperationNotPermitted);
			}
			else if ( response.StatusCode == HttpStatusCode.NotFound )
			{
				throw new FacebookApiException(errorText, FacebookApiError.ResourceNotFound);
			}
			else if ( response.StatusCode == (HttpStatusCode)420 )
			{
				throw new FacebookApiException("The rate limit has been exceeded.", FacebookApiError.RateLimitExceeded);
			}
		}

		private void HandleServerErrors(HttpStatusCode statusCode, string errorDetails)
		{
			if ( statusCode == HttpStatusCode.InternalServerError )
			{
				JsonValue errorValue = null;
				JsonValue.TryParse(errorDetails, out errorValue);
				if ( errorValue != null && !errorValue.IsNull && errorValue.ContainsName("error") )
				{
					// 04/14/2012 Paul.  The text is in a message property. 
					JsonValue errorValue2 = errorValue.GetValue("error");
					string errorText = errorValue2.GetValue<string>("message");
					throw new FacebookApiException(errorText, FacebookApiError.Server);
				}
				else if ( errorValue != null && !errorValue.IsNull && errorValue.ContainsName("error_msg") )
				{
					string errorText = errorValue.GetValue<string>("error_msg");
					throw new FacebookApiException(errorText, FacebookApiError.Server);
				}
				else
				{
					//throw new FacebookApiException("Something is broken at Facebook. Please see http://developer.facebook.com/ to report the issue.", FacebookApiError.Server);
				}
			}
			else if ( statusCode == HttpStatusCode.BadGateway )
			{
				throw new FacebookApiException("Facebook is down or is being upgraded.", FacebookApiError.ServerDown);
			}
			else if ( statusCode == HttpStatusCode.ServiceUnavailable )
			{
				throw new FacebookApiException("Facebook is overloaded with requests. Try again later.", FacebookApiError.ServerOverloaded);
			}
		}

		private JsonValue ExtractErrorDetailsFromResponse(HttpResponseMessage<byte[]> response)
		{
			if ( response.Body == null )
			{
				return null;
			}
			MediaType contentType = response.Headers.ContentType;
			Encoding charset = (contentType != null && contentType.CharSet != null) ? contentType.CharSet : DEFAULT_CHARSET;
			string errorDetails = charset.GetString(response.Body, 0, response.Body.Length);

			JsonValue result;
			return JsonValue.TryParse(errorDetails, out result) ? result : null;
		}
	}
}
