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

// .NET 10 Migration: Removed discontinued Spring.Social.OAuth1 using directive.
// Spring.Social.OAuth1 has no .NET Core / .NET 10 NuGet equivalent per AAP Section 0.7.4.
// OAuth1Template and AbstractOAuth1ServiceProvider<T> are defined as local stub types in the
// Spring.Social.OAuth1 namespace block below to satisfy compile-time type references while
// preserving the public API surface for the Enterprise Edition upgrade path.
// This is a dormant Enterprise Edition integration stub — compile only, not activated.

#nullable disable
using System;

using Spring.Social.OAuth1;
using Spring.Social.LinkedIn.Api;
using Spring.Social.LinkedIn.Api.Impl;

// ---------------------------------------------------------------------------
// Stub type definitions replacing discontinued Spring.Social.OAuth1 library types.
// Spring.Social.Core.dll has no .NET Core / .NET 10 equivalent per AAP Section 0.7.4.
// These stubs satisfy compile-time type references for dormant integration code.
// Dormant stubs — not executed at runtime.
// ---------------------------------------------------------------------------

namespace Spring.Social.OAuth1
{
    /// <summary>
    /// Stub replacement for Spring.Social.OAuth1.OAuth1Template from the discontinued
    /// Spring.Social.Core.dll library.
    /// <para/>
    /// Encapsulates the OAuth 1.0a provider endpoints (request token, authorize, authenticate,
    /// access token) used during the OAuth authorization flow. Used as a constructor parameter
    /// for <see cref="AbstractOAuth1ServiceProvider{T}"/> implementations.
    /// Dormant stub — preserves constructor signature for Enterprise Edition upgrade path.
    /// </summary>
    public class OAuth1Template
    {
        /// <summary>
        /// Initializes a new instance of <see cref="OAuth1Template"/> with the OAuth 1.0a
        /// provider endpoint URLs and application credentials.
        /// Dormant stub — constructor is a no-op; parameters are stored for signature preservation only.
        /// </summary>
        /// <param name="consumerKey">The application's OAuth consumer key.</param>
        /// <param name="consumerSecret">The application's OAuth consumer secret.</param>
        /// <param name="requestTokenUrl">The URL for obtaining an unauthorized request token.</param>
        /// <param name="authorizeUrl">The URL for the user to authorize the request token.</param>
        /// <param name="authenticateUrl">The URL for the user to authenticate and authorize the request token.</param>
        /// <param name="accessTokenUrl">The URL for exchanging the authorized request token for an access token.</param>
        public OAuth1Template(string consumerKey, string consumerSecret,
            string requestTokenUrl, string authorizeUrl,
            string authenticateUrl, string accessTokenUrl)
        {
        }
    }

    /// <summary>
    /// Stub replacement for Spring.Social.OAuth1.AbstractOAuth1ServiceProvider&lt;T&gt; from the
    /// discontinued Spring.Social.Core.dll library.
    /// <para/>
    /// Base class for OAuth 1.0a service providers. Stores the consumer key and consumer secret,
    /// and defines the abstract <see cref="GetApi"/> factory method for creating API binding instances.
    /// Dormant stub — preserves inheritance contract and property access for Enterprise Edition upgrade path.
    /// </summary>
    /// <typeparam name="T">The API binding interface type returned by <see cref="GetApi"/>.</typeparam>
    public abstract class AbstractOAuth1ServiceProvider<T>
    {
        /// <summary>
        /// Gets the OAuth consumer key identifying the client application.
        /// </summary>
        public string ConsumerKey { get; }

        /// <summary>
        /// Gets the OAuth consumer secret used to sign requests.
        /// </summary>
        public string ConsumerSecret { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="AbstractOAuth1ServiceProvider{T}"/> with
        /// the application credentials and OAuth 1.0a template configuration.
        /// Dormant stub — stores <paramref name="consumerKey"/> and <paramref name="consumerSecret"/>
        /// for use by <see cref="GetApi"/>; <paramref name="oAuth1Template"/> is accepted but unused.
        /// </summary>
        /// <param name="consumerKey">The application's OAuth consumer key.</param>
        /// <param name="consumerSecret">The application's OAuth consumer secret.</param>
        /// <param name="oAuth1Template">The OAuth 1.0a template with provider endpoint configuration.</param>
        protected AbstractOAuth1ServiceProvider(string consumerKey, string consumerSecret, OAuth1Template oAuth1Template)
        {
            ConsumerKey = consumerKey;
            ConsumerSecret = consumerSecret;
        }

        /// <summary>
        /// Returns an API interface allowing the client application to access protected resources
        /// on behalf of a user who has granted authorization.
        /// </summary>
        /// <param name="accessToken">The OAuth access token acquired through the OAuth authorization flow.</param>
        /// <param name="secret">The OAuth access token secret acquired through the OAuth authorization flow.</param>
        /// <returns>A binding to the service provider's API.</returns>
        public abstract T GetApi(string accessToken, string secret);
    }
}

namespace Spring.Social.LinkedIn.Connect
{
    /// <summary>
    /// LinkedIn <see cref="IServiceProvider"/> implementation.
    /// </summary>
    /// <author>Keith Donald</author>
    /// <author>Bruno Baia (.NET)</author>
    public class LinkedInServiceProvider : AbstractOAuth1ServiceProvider<ILinkedIn>
    {
        /// <summary>
        /// Creates a new instance of <see cref="LinkedInServiceProvider"/>.
        /// </summary>
        /// <param name="consumerKey">The application's API key.</param>
        /// <param name="consumerSecret">The application's API secret.</param>
        public LinkedInServiceProvider(string consumerKey, string consumerSecret)
            : base(consumerKey, consumerSecret, new OAuth1Template(consumerKey, consumerSecret,
                "https://api.linkedin.com/uas/oauth/requestToken", 
                "https://www.linkedin.com/uas/oauth/authorize", 
                "https://www.linkedin.com/uas/oauth/authenticate", 
                "https://api.linkedin.com/uas/oauth/accessToken"))
        {
        }

        /// <summary>
        /// Returns an API interface allowing the client application to access protected resources on behalf of a user.
        /// </summary>
        /// <param name="accessToken">The API access token.</param>
        /// <param name="secret">The access token secret.</param>
        /// <returns>A binding to the service provider's API.</returns>
        public override ILinkedIn GetApi(string accessToken, string secret)
        {
            return new LinkedInTemplate(this.ConsumerKey, this.ConsumerSecret, accessToken, secret);
        }
    }
}
