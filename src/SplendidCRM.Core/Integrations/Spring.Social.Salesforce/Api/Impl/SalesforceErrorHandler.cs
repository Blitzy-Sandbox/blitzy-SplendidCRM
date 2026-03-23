#nullable disable
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

// .NET 10 Migration: Removed Spring.Json, Spring.Http, Spring.Rest.Client,
// and Spring.Rest.Client.Support using directives. These Spring.NET Framework assemblies
// (discontinued) are unavailable on .NET 10.
// DefaultResponseErrorHandler, HttpResponseMessage<T>, HttpHeaders, and MediaType stub types
// are defined below within the Spring.Social.Salesforce.Api.Impl namespace to replace:
//   - Spring.Rest.Client.Support.DefaultResponseErrorHandler
//   - Spring.Http.HttpResponseMessage<T>
//   - Spring.Http.HttpHeaders
//   - Spring.Http.MediaType
// JsonValue and HttpMethod stubs are sourced from AbstractSalesforceOperations.cs (same namespace).
// This is a dormant integration stub — compiles but is NOT expected to execute at runtime.

using System;
using System.Net;
using System.Text;

namespace Spring.Social.Salesforce.Api.Impl
{
	/// <summary>
	/// Implementation of the <see cref="IResponseErrorHandler"/> that handles errors from Salesforce's REST API, 
	/// interpreting them into appropriate exceptions.
	/// </summary>
	/// <author>Bruno Baia (.NET)</author>
	/// <author>SplendidCRM (.NET)</author>
	class SalesforceErrorHandler : DefaultResponseErrorHandler
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
				/*
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
				*/
				this.HandleClientErrors(response);
			}
			else if (type == 5)
			{
				string errorDetails = DEFAULT_CHARSET.GetString(response.Body, 0, response.Body.Length);
				this.HandleServerErrors(response.StatusCode, errorDetails);
			}

			// if not otherwise handled, do default handling and wrap with SalesforceApiException
			try
			{
				base.HandleError(requestUri, requestMethod, response);
			}
			catch (Exception ex)
			{
				throw new SalesforceApiException("Error consuming Salesforce REST API.", ex);
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
			if ( errorValue.IsArray )
			{
				errorText = errorValue.GetValue(0).GetValue<string>("message");
			}
			else if ( errorValue.IsObject && errorValue.ContainsName("message") )
			{
				errorText = errorValue.GetValue<string>("message");
			}
			else if ( errorValue.IsString )
			{
				errorText = errorValue.GetValue<string>();
			}

			if ( response.StatusCode == HttpStatusCode.Unauthorized )
			{
				if ( errorText == "Could not authenticate you." )
				{
					throw new SalesforceApiException("Authorization is required for the operation, but the API binding was created without authorization.", SalesforceApiError.NotAuthorized);
				}
				else if ( errorText == "Could not authenticate with OAuth." )
				{
					throw new SalesforceApiException("The authorization has been revoked.", SalesforceApiError.NotAuthorized);
				}
				else
				{
					throw new SalesforceApiException(errorText ?? response.StatusDescription, SalesforceApiError.NotAuthorized);
				}
			}
			else if ( response.StatusCode == HttpStatusCode.BadRequest )
			{
				throw new SalesforceApiException(errorText, SalesforceApiError.OperationNotPermitted);
			}
			else if ( response.StatusCode == HttpStatusCode.Forbidden )
			{
				throw new SalesforceApiException(errorText, SalesforceApiError.OperationNotPermitted);
			}
			else if ( response.StatusCode == HttpStatusCode.NotFound )
			{
				throw new SalesforceApiException(errorText, SalesforceApiError.ResourceNotFound);
			}
			else if ( response.StatusCode == (HttpStatusCode)420 )
			{
				throw new SalesforceApiException("The rate limit has been exceeded.", SalesforceApiError.RateLimitExceeded);
			}
		}

		private void HandleServerErrors(HttpStatusCode statusCode, string errorDetails)
		{
			if ( statusCode == HttpStatusCode.InternalServerError )
			{
				JsonValue errorValue = null;
				JsonValue.TryParse(errorDetails, out errorValue);
				if ( errorValue != null && !errorValue.IsNull && errorValue.ContainsName("errorCode") )
				{
					string errorText = errorValue.GetValue<string>("message");
					throw new SalesforceApiException(errorText, SalesforceApiError.Server);
				}
				else
				{
					//throw new SalesforceApiException("Something is broken at Salesforce. Please see http://developer.Salesforce.com/ to report the issue.", SalesforceApiError.Server);
				}
			}
			else if ( statusCode == HttpStatusCode.BadGateway )
			{
				throw new SalesforceApiException("Salesforce is down or is being upgraded.", SalesforceApiError.ServerDown);
			}
			else if ( statusCode == HttpStatusCode.ServiceUnavailable )
			{
				throw new SalesforceApiException("Salesforce is overloaded with requests. Try again later.", SalesforceApiError.ServerOverloaded);
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

	#region Spring Framework Stubs — Response Error Handler Types

	// .NET 10 Migration: The following stub types replace discontinued Spring.NET Framework assemblies:
	//   - Spring.Rest.Client.Support.DefaultResponseErrorHandler
	//   - Spring.Http.HttpResponseMessage<T>
	//   - Spring.Http.HttpHeaders
	//   - Spring.Http.MediaType
	//
	// These stubs are defined within the Spring.Social.Salesforce.Api.Impl namespace so that
	// SalesforceErrorHandler and all other Impl/ files (SalesforceTemplate, etc.) can resolve them
	// without explicit using directives. The stubs satisfy compilation requirements but are NOT
	// executed at runtime (dormant stub pattern, AAP section 0.7.4).

	/// <summary>
	/// Stub replacement for <c>Spring.Rest.Client.Support.DefaultResponseErrorHandler</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Base class for HTTP response error handlers. Provides a virtual <see cref="HandleError"/>
	/// method that <see cref="SalesforceErrorHandler"/> overrides to map HTTP error status
	/// codes to strongly-typed <see cref="SalesforceApiException"/> instances.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class DefaultResponseErrorHandler
	{
		/// <summary>
		/// Handles the error response by inspecting the HTTP status code and throwing an
		/// appropriate exception. Override in subclasses to provide API-specific error mapping.
		/// Dormant stub — base implementation is a no-op; not executed at runtime.
		/// </summary>
		/// <param name="requestUri">The URI of the request that produced the error response.</param>
		/// <param name="requestMethod">The HTTP method used for the request.</param>
		/// <param name="response">The HTTP response message containing the error status and body.</param>
		public virtual void HandleError(Uri requestUri, HttpMethod requestMethod, HttpResponseMessage<byte[]> response)
		{
			// Dormant stub — no-op. At runtime this would inspect response.StatusCode and throw
			// a corresponding HttpServerErrorException or HttpClientErrorException.
		}
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Http.HttpResponseMessage{T}</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Represents an HTTP response message with a typed body, HTTP status code,
	/// status description, and response headers.
	/// Used by <see cref="SalesforceErrorHandler"/> to inspect Salesforce REST API error responses.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	/// <typeparam name="T">The type of the deserialized response body.</typeparam>
	public class HttpResponseMessage<T>
	{
		/// <summary>
		/// Gets or sets the HTTP status code of the response.
		/// Defaults to <see cref="HttpStatusCode.OK"/> in stub.
		/// </summary>
		public HttpStatusCode StatusCode { get; set; }

		/// <summary>
		/// Gets or sets the HTTP status description (reason phrase) of the response,
		/// e.g. "Unauthorized" for a 401 response. Used in
		/// <see cref="SalesforceErrorHandler"/> when constructing <see cref="SalesforceApiException"/>
		/// messages for unrecognized 401 error codes.
		/// Defaults to an empty string in stub.
		/// </summary>
		public string StatusDescription { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the deserialized response body.
		/// When <typeparamref name="T"/> is <c>byte[]</c>, contains the raw response body bytes
		/// used by <see cref="SalesforceErrorHandler"/> for JSON error payload extraction.
		/// Defaults to <c>default(T)</c> in stub.
		/// </summary>
		public T Body { get; set; }

		/// <summary>
		/// Gets or sets the HTTP response headers. Used by
		/// <see cref="SalesforceErrorHandler.ExtractErrorDetailsFromResponse"/> to read the
		/// <c>Content-Type</c> header for charset-aware JSON decoding.
		/// Defaults to a new <see cref="HttpHeaders"/> instance in stub.
		/// </summary>
		public HttpHeaders Headers { get; set; } = new HttpHeaders();
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Http.HttpHeaders</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Provides access to HTTP response header values. Used by
	/// <see cref="SalesforceErrorHandler.ExtractErrorDetailsFromResponse"/> to
	/// retrieve the <c>Content-Type</c> header for charset detection when decoding
	/// JSON error response bodies.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class HttpHeaders
	{
		/// <summary>
		/// Gets or sets the <c>Content-Type</c> header value parsed as a <see cref="MediaType"/>.
		/// Returns <c>null</c> in stub, signalling that the caller should use the default charset.
		/// </summary>
		public MediaType ContentType { get; set; }
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Http.MediaType</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Represents an HTTP media type (MIME type) including an optional character set encoding.
	/// Used by <see cref="SalesforceErrorHandler.ExtractErrorDetailsFromResponse"/> to
	/// determine the character encoding for JSON error response body decoding
	/// (falling back to UTF-8 if <see cref="CharSet"/> is <c>null</c>).
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class MediaType
	{
		/// <summary>
		/// Gets or sets the character set encoding specified by the <c>Content-Type</c> header
		/// (e.g. <c>charset=UTF-8</c>).
		/// Returns <c>null</c> in stub, signalling use of the default charset in the caller.
		/// </summary>
		public Encoding CharSet { get; set; }
	}

	#endregion
}
