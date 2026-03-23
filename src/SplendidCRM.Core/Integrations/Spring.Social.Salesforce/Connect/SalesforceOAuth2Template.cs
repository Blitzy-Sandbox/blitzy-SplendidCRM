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

// .NET 10 Migration: Spring.Social.OAuth2 and Spring.Rest.Client dependencies replaced with minimal stubs.
// Silverlight conditional compilation directives (#if SILVERLIGHT, #if NET_4_0) removed.
// All three method overrides (Task-based async, synchronous, callback-based async) preserved.
// This is a dormant integration stub — compiles but is not expected to execute.

using System;
using System.Threading.Tasks;
using System.Collections.Specialized;

namespace Spring.Social.Salesforce.Connect
{
	// STUB: Minimal replacement for Spring.Social.OAuth2.AccessGrant for .NET 10 compilation compatibility
	/// <summary>
	/// Represents an OAuth 2.0 access grant containing the access token and related information.
	/// </summary>
	public class AccessGrant
	{
		/// <summary>
		/// Gets the access token.
		/// </summary>
		public string AccessToken { get; }

		/// <summary>
		/// Gets the scope of the access grant.
		/// </summary>
		public string Scope { get; }

		/// <summary>
		/// Gets the refresh token.
		/// </summary>
		public string RefreshToken { get; }

		/// <summary>
		/// Gets the time (in seconds) until the access token expires.
		/// </summary>
		public int? ExpireTime { get; }

		/// <summary>
		/// Creates a new instance of <see cref="AccessGrant"/>.
		/// </summary>
		/// <param name="accessToken">The access token.</param>
		/// <param name="scope">The scope of the access grant.</param>
		/// <param name="refreshToken">The refresh token.</param>
		/// <param name="expireTime">The time (in seconds) until the access token expires.</param>
		public AccessGrant(string accessToken, string scope, string refreshToken, int? expireTime)
		{
			AccessToken   = accessToken;
			Scope         = scope;
			RefreshToken  = refreshToken;
			ExpireTime    = expireTime;
		}
	}

	// STUB: Minimal replacement for Spring.Rest.Client.RestOperationCanceler for .NET 10 compilation compatibility
	/// <summary>
	/// Allows cancellation of an asynchronous REST operation.
	/// </summary>
	public class RestOperationCanceler
	{
	}

	// STUB: Minimal replacement for Spring.Rest.Client.RestOperationCompletedEventArgs&lt;T&gt; for .NET 10 compilation compatibility
	/// <summary>
	/// Provides data for the asynchronous REST operation completed event.
	/// </summary>
	/// <typeparam name="T">The type of the response.</typeparam>
	public class RestOperationCompletedEventArgs<T> : EventArgs
	{
		/// <summary>
		/// Gets the response returned by the REST operation.
		/// </summary>
		public T Response { get; }

		/// <summary>
		/// Gets the error that occurred during the REST operation, if any.
		/// </summary>
		public Exception Error { get; }

		/// <summary>
		/// Gets a value indicating whether the REST operation was cancelled.
		/// </summary>
		public bool Cancelled { get; }

		/// <summary>
		/// Gets the user state associated with the REST operation.
		/// </summary>
		public object UserState { get; }

		/// <summary>
		/// Creates a new instance of <see cref="RestOperationCompletedEventArgs{T}"/>.
		/// </summary>
		/// <param name="response">The response from the REST operation.</param>
		/// <param name="error">The error that occurred, if any.</param>
		/// <param name="cancelled">Whether the operation was cancelled.</param>
		/// <param name="userState">The user state associated with the operation.</param>
		public RestOperationCompletedEventArgs(T response, Exception error, bool cancelled, object userState)
		{
			Response  = response;
			Error     = error;
			Cancelled = cancelled;
			UserState = userState;
		}
	}

	// STUB: Minimal replacement for Spring.Rest.Client.RestTemplate for .NET 10 compilation compatibility
	/// <summary>
	/// Provides methods for making REST API calls.
	/// </summary>
	public class RestTemplate
	{
		/// <summary>
		/// Asynchronously posts a request and returns the response as the specified type.
		/// </summary>
		/// <typeparam name="T">The type to deserialize the response as.</typeparam>
		/// <param name="url">The URL to post to.</param>
		/// <param name="request">The request data.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		public Task<T> PostForObjectAsync<T>(string url, object request)
		{
			throw new NotImplementedException("Dormant stub — Spring.Rest.Client.RestTemplate not available on .NET 10");
		}

		/// <summary>
		/// Posts a request and returns the response as the specified type.
		/// </summary>
		/// <typeparam name="T">The type to deserialize the response as.</typeparam>
		/// <param name="url">The URL to post to.</param>
		/// <param name="request">The request data.</param>
		/// <returns>The deserialized response.</returns>
		public T PostForObject<T>(string url, object request)
		{
			throw new NotImplementedException("Dormant stub — Spring.Rest.Client.RestTemplate not available on .NET 10");
		}

		/// <summary>
		/// Asynchronously posts a request and invokes the callback with the response.
		/// </summary>
		/// <typeparam name="T">The type to deserialize the response as.</typeparam>
		/// <param name="url">The URL to post to.</param>
		/// <param name="request">The request data.</param>
		/// <param name="callback">The callback to invoke when the operation completes.</param>
		/// <returns>A <see cref="RestOperationCanceler"/> that allows cancellation of the operation.</returns>
		public RestOperationCanceler PostForObjectAsync<T>(string url, object request, Action<RestOperationCompletedEventArgs<T>> callback)
		{
			throw new NotImplementedException("Dormant stub — Spring.Rest.Client.RestTemplate not available on .NET 10");
		}
	}

	// STUB: Minimal replacement for Spring.Social.OAuth2.OAuth2Template for .NET 10 compilation compatibility
	/// <summary>
	/// Base OAuth 2.0 template that provides REST client functionality for OAuth operations.
	/// </summary>
	public class OAuth2Template
	{
		/// <summary>
		/// Gets the REST template used for making HTTP requests.
		/// </summary>
		protected RestTemplate RestTemplate { get; }

		/// <summary>
		/// Creates a new instance of <see cref="OAuth2Template"/>.
		/// </summary>
		/// <param name="clientId">The client identifier.</param>
		/// <param name="clientSecret">The client secret.</param>
		/// <param name="authorizeUrl">The authorization URL.</param>
		/// <param name="accessTokenUrl">The access token URL.</param>
		public OAuth2Template(string clientId, string clientSecret, string authorizeUrl, string accessTokenUrl)
		{
			RestTemplate = new RestTemplate();
		}
	}

	/// <summary>
	/// Salesforce-specific extension of OAuth2Template to use a RestTemplate that recognizes form-encoded responses as "text/plain".
	/// <para/>
	/// (The OAuth 2 specification indicates that an access token response should be in JSON format)
	/// </summary>
	/// <remarks>
	/// Salesforce token responses are form-encoded results with a content type of "text/plain", 
	/// which prevents the FormHttpMessageConverter registered by default from parsing the results.
	/// </remarks>
	/// <author>SplendidCRM (.NET)</author>
	public class SalesforceOAuth2Template : OAuth2Template
	{
		/// <summary>
		/// Creates a new instance of <see cref="SalesforceOAuth2Template"/>.
		/// </summary>
		/// <param name="clientId">The client identifier.</param>
		/// <param name="clientSecret">The client secret.</param>
		public SalesforceOAuth2Template(string clientId, string clientSecret)
			: base(clientId, clientSecret, 
				"https://login.salesforce.com/services/oauth2/authorize", 
				"https://login.salesforce.com/services/oauth2/token")
		{
		}

		/// <summary>
		/// Asynchronously posts the request for an access grant to the provider.
		/// </summary>
		/// <remarks>
		/// The default implementation uses RestTemplate to request the access token and expects a JSON response to be bound to a dictionary.
		/// The information in the dictionary will be used to create an <see cref="AccessGrant"/>.
		/// Since the OAuth 2 specification indicates that an access token response should be in JSON format, there's often no need to override this method.
		/// If all you need to do is capture provider-specific data in the response, you should override CreateAccessGrant() instead.
		/// However, in the event of a provider whose access token response is non-JSON, 
		/// you may need to override this method to request that the response be bound to something other than a dictionary.
		/// For example, if the access token response is given as form-encoded, this method should be overridden to call RestTemplate.PostForObject() 
		/// asking for the response to be bound to a NameValueCollection (whose contents can then be used to create an <see cref="AccessGrant"/>).
		/// </remarks>
		/// <param name="accessTokenUrl">The URL of the provider's access token endpoint.</param>
		/// <param name="request">The request data to post to the access token endpoint.</param>
		/// <returns>
		/// A <code>Task&lt;AccessGrant&gt;</code> that represents the asynchronous operation that can return the OAuth2 access token.
		/// </returns>
		protected virtual Task<AccessGrant> PostForAccessGrantAsync(string accessTokenUrl, NameValueCollection request)
		{
			return this.RestTemplate.PostForObjectAsync<NameValueCollection>(accessTokenUrl, request)
				.ContinueWith<AccessGrant>(task =>
				{
					string expires = task.Result["expires"];
					return new AccessGrant(task.Result["access_token"], null, null, expires != null ? new Nullable<int>(Int32.Parse(expires)) : null);
				});
		}

		/// <summary>
		/// Posts the request for an access grant to the provider.
		/// </summary>
		/// <remarks>
		/// The default implementation uses RestTemplate to request the access token and expects a JSON response to be bound to a dictionary.
		/// The information in the dictionary will be used to create an <see cref="AccessGrant"/>.
		/// Since the OAuth 2 specification indicates that an access token response should be in JSON format, there's often no need to override this method.
		/// If all you need to do is capture provider-specific data in the response, you should override CreateAccessGrant() instead.
		/// However, in the event of a provider whose access token response is non-JSON, 
		/// you may need to override this method to request that the response be bound to something other than a dictionary.
		/// For example, if the access token response is given as form-encoded, this method should be overridden to call RestTemplate.PostForObject() 
		/// asking for the response to be bound to a NameValueCollection (whose contents can then be used to create an <see cref="AccessGrant"/>).
		/// </remarks>
		/// <param name="accessTokenUrl">The URL of the provider's access token endpoint.</param>
		/// <param name="request">The request data to post to the access token endpoint.</param>
		/// <returns>The OAuth2 access token.</returns>
		protected virtual AccessGrant PostForAccessGrant(string accessTokenUrl, NameValueCollection request)
		{
			NameValueCollection response = this.RestTemplate.PostForObject<NameValueCollection>(accessTokenUrl, request);
			string expires = response["expires"];
			return new AccessGrant(response["access_token"], null, null, expires != null ? new Nullable<int>(Int32.Parse(expires)) : null);
		}

		/// <summary>
		/// Asynchronously posts the request for an access grant to the provider.
		/// </summary>
		/// <remarks>
		/// The default implementation uses RestTemplate to request the access token and expects a JSON response to be bound to a dictionary.
		/// The information in the dictionary will be used to create an <see cref="AccessGrant"/>.
		/// Since the OAuth 2 specification indicates that an access token response should be in JSON format, there's often no need to override this method.
		/// If all you need to do is capture provider-specific data in the response, you should override CreateAccessGrant() instead.
		/// However, in the event of a provider whose access token response is non-JSON, 
		/// you may need to override this method to request that the response be bound to something other than a dictionary.
		/// For example, if the access token response is given as form-encoded, this method should be overridden to call RestTemplate.PostForObject() 
		/// asking for the response to be bound to a NameValueCollection (whose contents can then be used to create an <see cref="AccessGrant"/>).
		/// </remarks>
		/// <param name="accessTokenUrl">The URL of the provider's access token endpoint.</param>
		/// <param name="request">The request data to post to the access token endpoint.</param>
		/// <param name="operationCompleted">
		/// The <code>Action&lt;T&gt;</code> to perform when the asynchronous request completes. 
		/// Provides the OAuth2 access token.
		/// </param>
		/// <returns>
		/// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
		/// </returns>
		protected virtual RestOperationCanceler PostForAccessGrantAsync(string accessTokenUrl, NameValueCollection request, Action<RestOperationCompletedEventArgs<AccessGrant>> operationCompleted)
		{
			return this.RestTemplate.PostForObjectAsync<NameValueCollection>(accessTokenUrl, request,
				r =>
				{
					if (r.Error == null)
					{
						string expires = r.Response["expires"];
						AccessGrant token = new AccessGrant(r.Response["access_token"], null, null, expires != null ? new Nullable<int>(Int32.Parse(expires)) : null);
						operationCompleted(new RestOperationCompletedEventArgs<AccessGrant>(token, null, false, r.UserState));
					}
					else
					{
						operationCompleted(new RestOperationCompletedEventArgs<AccessGrant>(null, r.Error, r.Cancelled, r.UserState));
					}
				});
		}
	}
}
