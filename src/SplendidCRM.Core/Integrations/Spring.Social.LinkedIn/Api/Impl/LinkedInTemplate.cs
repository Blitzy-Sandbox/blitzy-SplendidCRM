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

// .NET 10 Migration: Conditional compilation directives removed.
//   - #if !WINDOWS_PHONE / #endif guards around RequestInterceptors.Add removed;
//     line is now unconditional (WINDOWS_PHONE target does not apply to .NET 10).
//   - #if NET_3_0 || SILVERLIGHT block adding XElementHttpMessageConverter,
//     DataContractHttpMessageConverter, DataContractJsonHttpMessageConverter removed;
//     those targets are not applicable to .NET 10.
//   - using Spring.Http.Converters.Xml removed (only used in the NET_3_0 block).
// Inline stub type definitions added below for types from discontinued Spring.* libraries:
//   - Spring.Http.Converters.IHttpMessageConverter
//   - Spring.Http.Converters.ByteArrayHttpMessageConverter
//   - Spring.Http.Converters.Json.SpringJsonHttpMessageConverter
//   - Spring.Social.OAuth1.AbstractOAuth1ApiBinding
// A local Spring.Social.LinkedIn.Api.Impl.RestTemplate derived class is defined to
// add BaseAddress, RequestInterceptors, and IRestOperations implementation on top of
// the Spring.Rest.Client.RestTemplate stub, enabling ConfigureRestTemplate and
// RestOperations to work correctly with the available stub infrastructure.
// Per AAP Section 0.7.4 — Spring.Social Dependency Removal.
// This is a dormant Enterprise Edition integration stub — compile only, not activated.

#nullable disable
using System;
using System.Collections.Generic;

using Spring.Json;
using Spring.Rest.Client;
using Spring.Social.OAuth1;
using Spring.Http.Converters;
using Spring.Http.Converters.Json;

using Spring.Social.LinkedIn.Api.Impl.Json;

// ---------------------------------------------------------------------------
// Stub type definitions replacing discontinued Spring.* library types.
// These stubs satisfy compile-time type references for dormant integration code.
// Per AAP Section 0.7.4 — Spring.Social Dependency Removal.
// ---------------------------------------------------------------------------

namespace Spring.Http.Converters
{
    /// <summary>
    /// Stub replacement for Spring.Http.Converters.IHttpMessageConverter from Spring.Rest.dll.
    /// Marker interface for HTTP message converters used in dormant Spring.Social.LinkedIn stubs.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public interface IHttpMessageConverter { }

    /// <summary>
    /// Stub replacement for Spring.Http.Converters.ByteArrayHttpMessageConverter from Spring.Rest.dll.
    /// Converts HTTP messages to/from byte array representations.
    /// Used in Spring.Social.LinkedIn.Api.Impl.LinkedInTemplate.GetMessageConverters().
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class ByteArrayHttpMessageConverter : IHttpMessageConverter { }
}

namespace Spring.Http.Converters.Json
{
    /// <summary>
    /// Stub replacement for Spring.Http.Converters.Json.SpringJsonHttpMessageConverter from Spring.Rest.dll.
    /// Converts HTTP messages using the Spring.Json.JsonMapper for type-safe JSON deserialization.
    /// Used in Spring.Social.LinkedIn.Api.Impl.LinkedInTemplate.GetJsonMessageConverter().
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class SpringJsonHttpMessageConverter : Spring.Http.Converters.IHttpMessageConverter
    {
        /// <summary>
        /// Initializes a new instance using the provided JsonMapper for deserialization dispatch.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        /// <param name="jsonMapper">The JsonMapper with registered type deserializers/serializers.</param>
        public SpringJsonHttpMessageConverter(Spring.Json.JsonMapper jsonMapper) { }
    }
}

namespace Spring.Social.LinkedIn.Api.Impl
{
    /// <summary>
    /// LinkedIn-specific RestTemplate extending <see cref="Spring.Rest.Client.RestTemplate"/> with
    /// the BaseAddress and RequestInterceptors properties required by ConfigureRestTemplate, and
    /// implementing <see cref="Spring.Social.LinkedIn.Api.IRestOperations"/> so that the
    /// <see cref="LinkedInTemplate.RestOperations"/> property can return it as the correct type.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public sealed class LinkedInRestTemplate : Spring.Rest.Client.RestTemplate, Spring.Social.LinkedIn.Api.IRestOperations
    {
        /// <summary>Gets or sets the base address for all LinkedIn REST API requests.</summary>
        public Uri BaseAddress { get; set; }

        /// <summary>
        /// Gets the list of request interceptors applied to outgoing HTTP requests.
        /// Provides the hook used to register <see cref="LinkedInRequestFactoryInterceptor"/>
        /// for preserving escaped dots and slashes in public profile URLs.
        /// </summary>
        public IList<object> RequestInterceptors { get; } = new List<object>();
    }
}

namespace Spring.Social.OAuth1
{
    /// <summary>
    /// Stub replacement for Spring.Social.OAuth1.AbstractOAuth1ApiBinding from the discontinued
    /// Spring.Social.Core.dll library.
    /// <para/>
    /// Provides the base infrastructure for OAuth 1.0a API bindings: IsAuthorized flag,
    /// RestTemplate access, ConfigureRestTemplate hook, and GetMessageConverters override point.
    /// Used as the base class for <see cref="Spring.Social.LinkedIn.Api.Impl.LinkedInTemplate"/>.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public abstract class AbstractOAuth1ApiBinding
    {
        /// <summary>
        /// Gets a value indicating whether this API binding has been authorized with a valid access token.
        /// </summary>
        public bool IsAuthorized { get; protected set; }

        /// <summary>
        /// Gets the <see cref="Spring.Social.LinkedIn.Api.Impl.LinkedInRestTemplate"/> used to
        /// issue REST API calls with OAuth 1.0a Authorization headers.
        /// </summary>
        public Spring.Social.LinkedIn.Api.Impl.LinkedInRestTemplate RestTemplate { get; protected set; }

        /// <summary>
        /// Initializes a new instance of <see cref="AbstractOAuth1ApiBinding"/>.
        /// Sets <see cref="IsAuthorized"/> based on the presence of a non-empty access token,
        /// creates a new <see cref="Spring.Social.LinkedIn.Api.Impl.LinkedInRestTemplate"/>,
        /// invokes <see cref="ConfigureRestTemplate"/> for subclass customization,
        /// and calls <see cref="GetMessageConverters"/> to populate message converters.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        /// <param name="consumerKey">The OAuth consumer key for the application.</param>
        /// <param name="consumerSecret">The OAuth consumer secret for the application.</param>
        /// <param name="accessToken">The OAuth access token acquired through the OAuth flow.</param>
        /// <param name="accessTokenSecret">The OAuth access token secret acquired through the OAuth flow.</param>
        protected AbstractOAuth1ApiBinding(string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret)
        {
            IsAuthorized = !string.IsNullOrEmpty(accessToken);
            RestTemplate = new Spring.Social.LinkedIn.Api.Impl.LinkedInRestTemplate();
            ConfigureRestTemplate(RestTemplate);
            // GetMessageConverters is invoked but converters list is dormant — not used at runtime
            _ = GetMessageConverters();
        }

        /// <summary>
        /// Enables subclass customization of the <see cref="RestTemplate"/> used for REST calls.
        /// Called after the RestTemplate is created and before InitSubApis.
        /// Dormant stub — override in derived class to set BaseAddress and add interceptors.
        /// </summary>
        /// <param name="restTemplate">The RestTemplate to configure.</param>
        protected virtual void ConfigureRestTemplate(Spring.Social.LinkedIn.Api.Impl.LinkedInRestTemplate restTemplate) { }

        /// <summary>
        /// Returns the list of HTTP message converters registered on the RestTemplate.
        /// Dormant stub — override to add type-specific serializers and deserializers.
        /// </summary>
        /// <returns>
        /// A mutable list of <see cref="Spring.Http.Converters.IHttpMessageConverter"/> instances.
        /// </returns>
        protected virtual IList<Spring.Http.Converters.IHttpMessageConverter> GetMessageConverters()
        {
            return new List<Spring.Http.Converters.IHttpMessageConverter>();
        }
    }
}

namespace Spring.Social.LinkedIn.Api.Impl
{
    /// <summary>
    /// This is the central class for interacting with LinkedIn.
    /// </summary>
    /// <author>Bruno Baia</author>
    public class LinkedInTemplate : AbstractOAuth1ApiBinding, ILinkedIn 
    {
        private static readonly Uri API_URI_BASE = new Uri("https://api.linkedin.com/v1/");

        private ICommunicationOperations communicationOperations;
        private IConnectionOperations connectionOperations;
        private IProfileOperations profileOperations;

        /// <summary>
        /// Create a new instance of <see cref="LinkedInTemplate"/>.
        /// </summary>
        /// <param name="consumerKey">The application's API key.</param>
        /// <param name="consumerSecret">The application's API secret.</param>
        /// <param name="accessToken">An access token acquired through OAuth authentication with LinkedIn.</param>
        /// <param name="accessTokenSecret">An access token secret acquired through OAuth authentication with LinkedIn.</param>
        public LinkedInTemplate(string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret) 
            : base(consumerKey, consumerSecret, accessToken, accessTokenSecret)
        {
            this.InitSubApis();
	    }

        #region ILinkedIn Members

        /// <summary>
        /// Gets the portion of the LinkedIn API sending messages and connection requests.
        /// </summary>
        public ICommunicationOperations CommunicationOperations 
        {
            get { return this.communicationOperations; }
        }

        /// <summary>
        /// Gets the portion of the LinkedIn API retrieving connections.
        /// </summary>
        public IConnectionOperations ConnectionOperations 
        { 
            get { return this.connectionOperations; }
        }

        /// <summary>
        /// Gets the portion of the LinkedIn API retrieving and performing operations on profiles.
        /// </summary>
        public IProfileOperations ProfileOperations 
        {
            get { return this.profileOperations; }
        }

        /// <summary>
        /// Gets the underlying <see cref="IRestOperations"/> object allowing for consumption of LinkedIn endpoints 
        /// that may not be otherwise covered by the API binding. 
        /// </summary>
        /// <remarks>
        /// The <see cref="IRestOperations"/> object returned is configured to include an OAuth "Authorization" header on all requests.
        /// </remarks>
        public IRestOperations RestOperations
        {
            get { return this.RestTemplate; }
        }

        #endregion

        /// <summary>
        /// Enables customization of the <see cref="RestTemplate"/> used to consume provider API resources.
        /// </summary>
        /// <remarks>
        /// An example use case might be to configure a custom error handler. 
        /// Note that this method is called after the RestTemplate has been configured with the message converters returned from GetMessageConverters().
        /// </remarks>
        /// <param name="restTemplate">The RestTemplate to configure.</param>
        protected override void ConfigureRestTemplate(LinkedInRestTemplate restTemplate)
        {
            restTemplate.BaseAddress = API_URI_BASE;
            // .NET 10 Migration: #if !WINDOWS_PHONE / #endif removed — line is now unconditional.
            // WINDOWS_PHONE conditional compilation does not apply to .NET 10.
            restTemplate.RequestInterceptors.Add(new LinkedInRequestFactoryInterceptor());
        }

        /// <summary>
        /// Returns a list of <see cref="IHttpMessageConverter"/>s to be used by the internal <see cref="LinkedInRestTemplate"/>.
        /// </summary>
        /// <remarks>
        /// This implementation adds <see cref="SpringJsonHttpMessageConverter"/> and <see cref="ByteArrayHttpMessageConverter"/> to the default list.
        /// </remarks>
        /// <returns>
        /// The list of <see cref="IHttpMessageConverter"/>s to be used by the internal <see cref="LinkedInRestTemplate"/>.
        /// </returns>
        protected override IList<IHttpMessageConverter> GetMessageConverters()
        {
            IList<IHttpMessageConverter> converters = base.GetMessageConverters();
            converters.Add(new ByteArrayHttpMessageConverter());
            converters.Add(this.GetJsonMessageConverter());
            // .NET 10 Migration: #if NET_3_0 || SILVERLIGHT block removed entirely.
            // XElementHttpMessageConverter, DataContractHttpMessageConverter, and
            // DataContractJsonHttpMessageConverter are not applicable to .NET 10.
            return converters;
        }

        /// <summary>
        /// Returns a <see cref="SpringJsonHttpMessageConverter"/> to be used by the internal <see cref="LinkedInRestTemplate"/>.
        /// <para/>
        /// Override to customize the message converter (for example, to set a custom object mapper or supported media types).
        /// </summary>
        /// <returns>The configured <see cref="SpringJsonHttpMessageConverter"/>.</returns>
        protected virtual SpringJsonHttpMessageConverter GetJsonMessageConverter()
        {
            JsonMapper jsonMapper = new JsonMapper();
            jsonMapper.RegisterDeserializer(typeof(LinkedInProfile), new LinkedInProfileDeserializer());
            jsonMapper.RegisterDeserializer(typeof(LinkedInFullProfile), new LinkedInFullProfileDeserializer());
            jsonMapper.RegisterDeserializer(typeof(IList<LinkedInProfile>), new LinkedInProfileListDeserializer<LinkedInProfile>());
            jsonMapper.RegisterDeserializer(typeof(IList<LinkedInFullProfile>), new LinkedInProfileListDeserializer<LinkedInFullProfile>());
            jsonMapper.RegisterDeserializer(typeof(NetworkStatistics), new NetworkStatisticsDeserializer());
            jsonMapper.RegisterSerializer(typeof(Message), new MessageSerializer());
            jsonMapper.RegisterSerializer(typeof(Invitation), new InvitationSerializer());
            jsonMapper.RegisterDeserializer(typeof(LinkedInProfiles), new LinkedInProfilesDeserializer());
            // 04/10/2012 Paul.  We need a new deserializer for the full profile. Not sure why there is both FullProfileList and FullProfiles. 
            jsonMapper.RegisterDeserializer(typeof(IList<LinkedInFullProfile>), new LinkedInFullProfileListDeserializer<LinkedInFullProfile>());
            jsonMapper.RegisterDeserializer(typeof(LinkedInFullProfiles), new LinkedInFullProfilesDeserializer());

            return new SpringJsonHttpMessageConverter(jsonMapper);
        }

        private void InitSubApis()
        {
            this.communicationOperations = new CommunicationTemplate(this.RestTemplate);
            this.connectionOperations = new ConnectionTemplate(this.RestTemplate);
            this.profileOperations = new ProfileTemplate(this.RestTemplate);
        }
    }
}
