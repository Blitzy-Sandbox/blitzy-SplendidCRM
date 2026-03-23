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

// .NET 10 Migration: Conditional compilation directives (#if NET_4_0, #if SILVERLIGHT) removed.
// Silverlight callback-based async method removed; Task-based async and synchronous methods retained.
// Spring.Rest.Client and Spring.Social.OAuth2 stubs defined below for compilation compatibility.
// This is a dormant Enterprise Edition integration stub — compiles but is not expected to execute.

using System;
using System.Threading.Tasks;
using System.Collections.Specialized;

using Spring.Rest.Client;
using Spring.Social.OAuth2;

namespace Spring.Rest.Client
{
	// STUB: Minimal replacement for Spring.Rest.Client.RestTemplate for .NET 10 compilation compatibility.
	// Dormant stub — not executed at runtime (AAP section 0.7.4).
	/// <summary>
	/// Provides methods for making REST API calls.
	/// Stub replacement for the discontinued Spring.Rest.dll RestTemplate.
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
	}
}

namespace Spring.Social.OAuth2
{
	// STUB: Minimal replacement for Spring.Social.OAuth2.AccessGrant for .NET 10 compilation compatibility.
	// Dormant stub — not executed at runtime (AAP section 0.7.4).
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
			AccessToken  = accessToken;
			Scope        = scope;
			RefreshToken = refreshToken;
			ExpireTime   = expireTime;
		}
	}

	// STUB: Minimal replacement for Spring.Social.OAuth2.OAuth2Operations interface for .NET 10 compilation compatibility.
	/// <summary>
	/// Interface for OAuth 2.0 operations. Implemented by <see cref="OAuth2Template"/>.
	/// </summary>
	public interface OAuth2Operations
	{
	}

	// STUB: Minimal replacement for Spring.Social.OAuth2.OAuth2Template for .NET 10 compilation compatibility.
	// Dormant stub — not executed at runtime (AAP section 0.7.4).
	/// <summary>
	/// Base OAuth 2.0 template that provides REST client functionality for OAuth operations.
	/// Stub replacement for the discontinued Spring.Social.Core.dll OAuth2Template.
	/// </summary>
	public class OAuth2Template : OAuth2Operations
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

		/// <summary>
		/// Asynchronously posts the request for an access grant to the provider.
		/// </summary>
		/// <param name="accessTokenUrl">The URL of the provider's access token endpoint.</param>
		/// <param name="request">The request data to post to the access token endpoint.</param>
		/// <returns>A task that represents the asynchronous operation returning the OAuth2 access token.</returns>
		protected virtual Task<AccessGrant> PostForAccessGrantAsync(string accessTokenUrl, NameValueCollection request)
		{
			throw new NotImplementedException("Dormant stub — Spring.Social.OAuth2.OAuth2Template not available on .NET 10");
		}

		/// <summary>
		/// Posts the request for an access grant to the provider.
		/// </summary>
		/// <param name="accessTokenUrl">The URL of the provider's access token endpoint.</param>
		/// <param name="request">The request data to post to the access token endpoint.</param>
		/// <returns>The OAuth2 access token.</returns>
		protected virtual AccessGrant PostForAccessGrant(string accessTokenUrl, NameValueCollection request)
		{
			throw new NotImplementedException("Dormant stub — Spring.Social.OAuth2.OAuth2Template not available on .NET 10");
		}
	}

	// STUB: Minimal replacement for Spring.Social.OAuth2.AbstractOAuth2ServiceProvider<T> for .NET 10 compilation compatibility.
	// Dormant stub — not executed at runtime (AAP section 0.7.4).
	/// <summary>
	/// Abstract base class for OAuth 2.0 service providers.
	/// Stub replacement for the discontinued Spring.Social.Core.dll AbstractOAuth2ServiceProvider.
	/// </summary>
	/// <typeparam name="T">The API binding type.</typeparam>
	public abstract class AbstractOAuth2ServiceProvider<T>
	{
		/// <summary>
		/// Creates a new instance of <see cref="AbstractOAuth2ServiceProvider{T}"/>.
		/// </summary>
		/// <param name="oauth2Operations">The OAuth 2.0 operations template.</param>
		protected AbstractOAuth2ServiceProvider(OAuth2Operations oauth2Operations)
		{
		}

		/// <summary>
		/// Returns an API interface allowing the client application to access protected resources on behalf of a user.
		/// </summary>
		/// <param name="accessToken">The API access token.</param>
		/// <returns>A binding to the service provider's API.</returns>
		public abstract T GetApi(string accessToken);
	}
}

namespace Spring.Social.Facebook.Connect
{
	/// <summary>
	/// Facebook-specific extension of OAuth2Template to use a RestTemplate that recognizes form-encoded responses as "text/plain".
	/// <para/>
	/// (The OAuth 2 specification indicates that an access token response should be in JSON format)
	/// </summary>
	/// <remarks>
	/// Facebook token responses are form-encoded results with a content type of "text/plain", 
	/// which prevents the FormHttpMessageConverter registered by default from parsing the results.
	/// </remarks>
	/// <author>Craig Walls</author>
	/// <author>Bruno Baia (.NET)</author>
	public class FacebookOAuth2Template : OAuth2Template
	{
		/// <summary>
		/// Creates a new instance of <see cref="FacebookOAuth2Template"/>.
		/// </summary>
		/// <param name="clientId">The client identifier.</param>
		/// <param name="clientSecret">The client secret.</param>
		public FacebookOAuth2Template(string clientId, string clientSecret)
			: base(clientId, clientSecret, 
				"https://graph.facebook.com/oauth/authorize", 
				"https://graph.facebook.com/oauth/access_token")
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
		protected override Task<AccessGrant> PostForAccessGrantAsync(string accessTokenUrl, NameValueCollection request)
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
		protected override AccessGrant PostForAccessGrant(string accessTokenUrl, NameValueCollection request)
		{
			NameValueCollection response = this.RestTemplate.PostForObject<NameValueCollection>(accessTokenUrl, request);
			string expires = response["expires"];
			return new AccessGrant(response["access_token"], null, null, expires != null ? new Nullable<int>(Int32.Parse(expires)) : null);
		}
	}
}
