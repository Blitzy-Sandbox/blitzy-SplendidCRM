// .NET 10 Migration: TwitterTemplate updated to implement the revised ITwitter interface
// (ITwitter : IApiBinding with IRestOperations RestOperations property).
// IApiBinding and IRestOperations are defined as stubs in Spring.Rest.Client namespace
// via src/SplendidCRM.Core/Integrations/_Stubs/SpringHttpStubs.cs.
// IsAuthorized satisfies IApiBinding.IsAuthorized; RestOperations returns null (dormant stub).
// ITweetOperations TweetOperations is retained as a non-interface extra property (harmless).
// This is a dormant Enterprise Edition integration stub — compile only, not expected to execute.
#nullable disable
using System;
using System.Collections.Generic;
using Spring.Rest.Client;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    /// <summary>
    /// Central class for interacting with Twitter (dormant stub implementation).
    /// Implements <see cref="ITwitter"/> which aggregates all Twitter API sub-interfaces.
    /// Dormant Enterprise Edition stub — compile only, not executed at runtime.
    /// </summary>
    public class TwitterTemplate : ITwitter
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TwitterTemplate"/> with OAuth credentials.
        /// </summary>
        /// <param name="consumerKey">The application's API key.</param>
        /// <param name="consumerSecret">The application's API secret.</param>
        /// <param name="accessToken">An access token acquired through OAuth authentication with Twitter.</param>
        /// <param name="accessTokenSecret">An access token secret acquired through OAuth authentication with Twitter.</param>
        public TwitterTemplate(string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret)
        {
            IsAuthorized = true;
        }

        /// <inheritdoc/>
        public IBlockOperations BlockOperations { get; private set; }

        /// <inheritdoc/>
        public IDirectMessageOperations DirectMessageOperations { get; private set; }

        /// <inheritdoc/>
        public IFriendOperations FriendOperations { get; private set; }

        /// <inheritdoc/>
        public IGeoOperations GeoOperations { get; private set; }

        /// <inheritdoc/>
        public IListOperations ListOperations { get; private set; }

        /// <inheritdoc/>
        public ISearchOperations SearchOperations { get; private set; }

        /// <inheritdoc/>
        public ITimelineOperations TimelineOperations { get; private set; }

        /// <inheritdoc/>
        public IUserOperations UserOperations { get; private set; }

        /// <inheritdoc/>
        /// <remarks>Returns null in this dormant stub implementation.</remarks>
        public IRestOperations RestOperations { get; } = null;

        /// <summary>
        /// Gets a value indicating whether this template is configured with OAuth credentials.
        /// Satisfies <see cref="Spring.Rest.Client.IApiBinding.IsAuthorized"/>.
        /// </summary>
        public bool IsAuthorized { get; private set; }

        // Retained as a non-interface extra property for forward compatibility.
        // ITweetOperations was present in a prior stub version of ITwitter but is not part
        // of the migrated ITwitter interface. Preserved here to avoid breaking any
        // Enterprise Edition code paths that may reference it on a concrete TwitterTemplate.
        /// <summary>Gets tweet operations (extra non-interface property — dormant stub).</summary>
        public ITweetOperations TweetOperations { get; private set; }
    }
}
