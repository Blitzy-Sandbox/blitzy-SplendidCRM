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

// Migration Note: .NET 10 ASP.NET Core replatforming.
// Removed: using Spring.Rest.Client; using Spring.Http.Client; using Spring.Http.Converters;
// These Spring.NET libraries have no .NET 10 equivalent.
//
// RestTemplate stub is reused from Spring.Social.Salesforce.Api.Impl namespace
// (defined in AbstractSalesforceOperations.cs) to avoid namespace conflict with
// FacebookOAuth2Template.cs's Spring.Rest.Client.RestTemplate stub which also
// contributes to the Spring.Social.OAuth2 namespace.
//
// IHttpMessageConverter is likewise reused from Spring.Social.Salesforce.Api.Impl.
// StringHttpMessageConverter and FormHttpMessageConverter are defined below in
// Spring.Social.OAuth2 namespace, implementing Spring.Social.Salesforce.Api.Impl.IHttpMessageConverter.
//
// Removed: #if !SILVERLIGHT / #endif conditional — kept inner line unconditionally.
// This file is a DORMANT STUB — must compile on .NET 10 but NOT execute.

using System;
using System.Collections.Generic;

// IApiBinding stub is defined in the parent Api/ folder under Spring.Social.Salesforce.Api namespace.
using Spring.Social.Salesforce.Api;

// RestTemplate and IHttpMessageConverter stubs are defined in AbstractSalesforceOperations.cs
// in this namespace; importing here to avoid redefining RestTemplate in Spring.Social.OAuth2
// (which would conflict with FacebookOAuth2Template.cs's usage of the type in that namespace).
using Spring.Social.Salesforce.Api.Impl;

namespace Spring.Social.OAuth2
{
	// ---------------------------------------------------------------------------
	// Stub replacements for removed Spring.NET library types
	// (Spring.Http.Client, Spring.Http.Converters, Spring.Social.OAuth2 types)
	//
	// NOTE: RestTemplate is intentionally NOT defined here to avoid namespace conflict.
	// RestTemplate is defined in Spring.Social.Salesforce.Api.Impl (AbstractSalesforceOperations.cs)
	// and is accessible via the 'using Spring.Social.Salesforce.Api.Impl;' directive above.
	//
	// IHttpMessageConverter is likewise sourced from Spring.Social.Salesforce.Api.Impl.
	// All stubs are minimal .NET 10-compatible replacements preserving the public contract
	// required by dormant Enterprise Edition integration code.
	// ---------------------------------------------------------------------------

	/// <summary>
	/// Stub enum replacing Spring.Social.OAuth2.OAuth2Version.
	/// Defines the version of OAuth2 used when forming authorization headers.
	/// Preserved in Spring.Social.OAuth2 namespace matching original Spring.Social library location.
	/// </summary>
	public enum OAuth2Version
	{
		/// <summary>Bearer token scheme (RFC 6750).</summary>
		Bearer,
		/// <summary>Draft 10 token scheme (legacy).</summary>
		Draft10
	}

	/// <summary>
	/// Stub class replacing Spring.Http.Client.WebClientHttpRequestFactory.
	/// Creates HTTP request instances using the underlying WebClient infrastructure.
	/// Used in the SalesforceOAuth2ApiBinding constructor to disable Expect-100-Continue behavior.
	/// </summary>
	public class WebClientHttpRequestFactory
	{
		/// <summary>
		/// Gets or sets whether the Expect: 100-Continue header is sent with HTTP requests.
		/// Preserved from original .NET Framework stub; set to false in SalesforceOAuth2ApiBinding constructor.
		/// </summary>
		public bool Expect100Continue { get; set; }
	}

	/// <summary>
	/// Stub class replacing Spring.Social.OAuth2.OAuth2RequestInterceptor.
	/// Adds OAuth2 authorization headers to outgoing HTTP requests.
	/// Preserved in Spring.Social.OAuth2 namespace matching original Spring.Social library location.
	/// </summary>
	public class OAuth2RequestInterceptor
	{
		/// <summary>
		/// Initializes a new instance of <see cref="OAuth2RequestInterceptor"/> with the given access token
		/// and OAuth2 version. Preserved from original Spring.Social implementation.
		/// </summary>
		/// <param name="accessToken">The OAuth2 access token to include in Authorization headers.</param>
		/// <param name="version">The OAuth2 version that determines the authorization header format.</param>
		public OAuth2RequestInterceptor(string accessToken, OAuth2Version version)
		{
			// Dormant stub — no-op implementation preserved for compilation compatibility.
		}
	}

	/// <summary>
	/// Stub class replacing Spring.Http.Converters.StringHttpMessageConverter.
	/// Reads and writes strings from/to HTTP messages.
	/// Implements <see cref="IHttpMessageConverter"/> (from Spring.Social.Salesforce.Api.Impl)
	/// so it can be added to the RestTemplate.MessageConverters collection.
	/// </summary>
	public class StringHttpMessageConverter : IHttpMessageConverter
	{
		// Dormant stub — no-op implementation preserved for compilation compatibility.
	}

	/// <summary>
	/// Stub class replacing Spring.Http.Converters.FormHttpMessageConverter.
	/// Reads and writes form data (application/x-www-form-urlencoded) from/to HTTP messages.
	/// Implements <see cref="IHttpMessageConverter"/> (from Spring.Social.Salesforce.Api.Impl)
	/// so it can be added to the RestTemplate.MessageConverters collection.
	/// </summary>
	public class FormHttpMessageConverter : IHttpMessageConverter
	{
		// Dormant stub — no-op implementation preserved for compilation compatibility.
	}

	// ---------------------------------------------------------------------------
	// Main class: SalesforceOAuth2ApiBinding
	// ---------------------------------------------------------------------------

	/// <summary>
	/// Base class for OAuth2-based provider API bindings.
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>Bruno Baia (.NET)</author>
	public abstract class SalesforceOAuth2ApiBinding : IApiBinding
	{
		private string instanceURL;
		private string accessToken;
		private RestTemplate restTemplate;

		/// <summary>
		/// Gets a reference to the REST client backing this API binding and used to perform API calls. 
		/// </summary>
		/// <remarks>
		/// Callers may use the RestTemplate to invoke other API operations not yet modeled by the binding interface. 
		/// Callers may also modify the configuration of the RestTemplate to support unit testing the API binding with a mock server in a test environment. 
		/// During construction, subclasses may apply customizations to the RestTemplate needed to invoke a specific API.
		/// </remarks>
		public RestTemplate RestTemplate
		{
			get { return this.restTemplate; }
		}

		/// <summary>
		/// Constructs the API template with OAuth credentials necessary to perform operations on behalf of a user.
		/// </summary>
		/// <param name="instanceURL">The Salesforce instance URL (e.g., https://na1.salesforce.com).</param>
		/// <param name="accessToken">The OAuth2 access token.</param>
		protected SalesforceOAuth2ApiBinding(string instanceURL, string accessToken)
		{
			this.instanceURL = instanceURL;
			this.accessToken = accessToken;
			this.restTemplate = new RestTemplate();
			restTemplate.BaseAddress = new Uri(instanceURL);
			// Migration Note: #if !SILVERLIGHT removed — line kept unconditionally per .NET 10 migration rules.
			// RequestFactory is initialized as WebClientHttpRequestFactory; the cast is preserved exactly
			// from the original source. This is a dormant stub — the constructor is not expected to execute.
			restTemplate.RequestFactory = new WebClientHttpRequestFactory();
			((WebClientHttpRequestFactory)restTemplate.RequestFactory).Expect100Continue = false;
			this.restTemplate.RequestInterceptors.Add(new OAuth2RequestInterceptor(accessToken, this.GetOAuth2Version()));
			this.restTemplate.RequestInterceptors.Add(new PrettyPrintInterceptor());
			this.restTemplate.MessageConverters = this.GetMessageConverters();
			this.ConfigureRestTemplate(this.restTemplate);
		}

		#region IApiBinding Members

		/// <summary>
		/// Returns true if this API binding has been authorized on behalf of a specific user.
		/// </summary>
		/// <remarks>
		/// If so, calls to the API are signed with the user's authorization credentials, indicating an application is invoking the API on a user's behalf. 
		/// If not, API calls do not contain any user authorization information. 
		/// Callers can use this status flag to determine if API operations requiring authorization can be invoked.
		/// </remarks>
		public bool IsAuthorized
		{
			get { return this.accessToken != null; }
		}

		#endregion

		/// <summary>
		/// Returns the version of OAuth2 the API implements. 
		/// </summary>
		/// <remarks>
		/// Subclasses may override to return another version.
		/// </remarks>
		/// <returns>
		/// By default, returns OAuth2Version.Bearer indicating versions of OAuth2 that apply the bearer token scheme.
		/// </returns>
		/// <see cref="OAuth2Version"/>
		protected virtual OAuth2Version GetOAuth2Version()
		{
			return OAuth2Version.Bearer;
		}

		/// <summary>
		/// Returns a list of <see cref="IHttpMessageConverter"/>s to be used by the internal <see cref="RestTemplate"/>.
		/// </summary>
		/// <remarks>
		/// Override this method to add additional message converters or to replace the default list of message converters. 
		/// By default, this includes a <see cref="StringHttpMessageConverter"/> and a <see cref="FormHttpMessageConverter"/>.
		/// </remarks>
		/// <returns>
		/// The list of <see cref="IHttpMessageConverter"/>s to be used by the internal <see cref="RestTemplate"/>.
		/// </returns>
		protected virtual IList<IHttpMessageConverter> GetMessageConverters()
		{
			IList<IHttpMessageConverter> messageConverters = new List<IHttpMessageConverter>();
			messageConverters.Add(new StringHttpMessageConverter());
			messageConverters.Add(new FormHttpMessageConverter());
			return messageConverters;
		}

		/// <summary>
		/// Enables customization of the RestTemplate used to consume provider API resources.
		/// </summary>
		/// <remarks>
		/// An example use case might be to configure a custom error handler. 
		/// Note that this method is called after the RestTemplate has been configured with the message converters returned from GetMessageConverters().
		/// </remarks>
		/// <param name="restTemplate">The RestTemplate to configure.</param>
		protected virtual void ConfigureRestTemplate(RestTemplate restTemplate)
		{
		}
	}
}
