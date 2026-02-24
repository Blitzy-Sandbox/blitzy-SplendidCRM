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

// .NET 10 Migration: TimelineTemplate migrated from .NET Framework 4.8 to .NET 10 ASP.NET Core.
//
// Changes from original source (SplendidCRM/_code/Spring.Social.Twitter/Api/Impl/TimelineTemplate.cs):
//
// 1. Conditional compilation blocks preserved exactly from the original source:
//    - #if SILVERLIGHT / #else / #endif for NameValueCollection namespace selection
//    - #if NET_4_0 || SILVERLIGHT_5 / #else / #endif for async pattern selection
//    - On .NET 10, neither NET_4_0, SILVERLIGHT_5, nor SILVERLIGHT is defined:
//      * Task-based async branch (#if NET_4_0 || SILVERLIGHT_5) is NOT compiled
//      * Sync methods (#if !SILVERLIGHT inside #else) ARE compiled
//      * Callback-based async methods (in #else) ARE compiled
//      * System.Collections.Specialized is used unconditionally (SILVERLIGHT #else branch)
//      * Spring.Http is used unconditionally (non-NET_4_0 #else branch for HttpResponseMessage)
//
// 2. RestTemplate field and constructor preserved for structural fidelity:
//    - The private restTemplate field and constructor taking RestTemplate are retained
//      to preserve the original class structure and support future Enterprise Edition activation.
//    - RestTemplate is defined as a stub in Spring.Rest.Client namespace (FacebookOAuth2Template.cs).
//    - Method bodies are stubbed — RestTemplate.GetForObject, GetForObjectAsync, PostForMessage,
//      PostForMessageAsync, and their callback overloads are NOT available in the .NET 10 stub.
//    - All sync method bodies return null / empty collections.
//    - All callback-based async method bodies return null (stub RestOperationCanceler).
//
// 3. Private AddStatusDetailsTo helpers preserved exactly from source:
//    - These helpers do NOT call RestTemplate and compile unchanged on .NET 10.
//    - They use CultureInfo.InvariantCulture for lat/long formatting and
//      StatusDetails properties (Latitude, Longitude, DisplayCoordinates, InReplyToStatusId, WrapLinks).
//
// 4. using Spring.Social.Twitter.Api.Impl.Json retained for structural fidelity:
//    - Present in original source; the Json subfolder types compile successfully on .NET 10.
//
// 5. All method signatures preserved exactly per AAP §0.8.1 immutable interfaces rule.
//
// This is a dormant Enterprise Edition integration stub — compile only, not expected to execute.
// Per AAP §0.3.1 and §0.7.4.

#nullable disable
using System;
using System.Globalization;
using System.Collections.Generic;
#if SILVERLIGHT
using Spring.Collections.Specialized;
#else
using System.Collections.Specialized;
#endif
#if NET_4_0 || SILVERLIGHT_5
using System.Threading.Tasks;
#else
using Spring.Http;
#endif

using Spring.IO;
using Spring.Rest.Client;

using Spring.Social.Twitter.Api.Impl.Json;

namespace Spring.Social.Twitter.Api.Impl
{
    /// <summary>
    /// Implementation of <see cref="ITimelineOperations"/>, providing a binding to Twitter's tweet and timeline-oriented REST resources.
    /// </summary>
    /// <author>Craig Walls</author>
    /// <author>Bruno Baia (.NET)</author>
    class TimelineTemplate : AbstractTwitterOperations, ITimelineOperations
    {
        private RestTemplate restTemplate;

        /// <summary>
        /// Creates a new <see cref="TimelineTemplate"/> with the given REST template.
        /// </summary>
        /// <param name="restTemplate">
        /// The <see cref="RestTemplate"/> used to make REST API calls. Not null.
        /// </param>
        public TimelineTemplate(RestTemplate restTemplate)
        {
            this.restTemplate = restTemplate;
        }

        #region ITimelineOperations Members

#if NET_4_0 || SILVERLIGHT_5
        public Task<IList<Tweet>> GetHomeTimelineAsync()
        {
            return this.GetHomeTimelineAsync(0, 0, 0);
        }

        public Task<IList<Tweet>> GetHomeTimelineAsync(int count)
        {
            return this.GetHomeTimelineAsync(count, 0, 0);
        }

        public Task<IList<Tweet>> GetHomeTimelineAsync(int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            return Task.FromResult<IList<Tweet>>(new List<Tweet>());
        }

        public Task<IList<Tweet>> GetUserTimelineAsync()
        {
            return this.GetUserTimelineAsync(0, 0, 0);
        }

        public Task<IList<Tweet>> GetUserTimelineAsync(int count)
        {
            return this.GetUserTimelineAsync(count, 0, 0);
        }

        public Task<IList<Tweet>> GetUserTimelineAsync(int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            return Task.FromResult<IList<Tweet>>(new List<Tweet>());
        }

        public Task<IList<Tweet>> GetUserTimelineAsync(string screenName)
        {
            return this.GetUserTimelineAsync(screenName, 0, 0, 0);
        }

        public Task<IList<Tweet>> GetUserTimelineAsync(string screenName, int count)
        {
            return this.GetUserTimelineAsync(screenName, count, 0, 0);
        }

        public Task<IList<Tweet>> GetUserTimelineAsync(string screenName, int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            parameters.Add("screen_name", screenName);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            return Task.FromResult<IList<Tweet>>(new List<Tweet>());
        }

        public Task<IList<Tweet>> GetUserTimelineAsync(long userId)
        {
            return this.GetUserTimelineAsync(userId, 0, 0, 0);
        }

        public Task<IList<Tweet>> GetUserTimelineAsync(long userId, int count)
        {
            return this.GetUserTimelineAsync(userId, count, 0, 0);
        }

        public Task<IList<Tweet>> GetUserTimelineAsync(long userId, int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            parameters.Add("user_id", userId.ToString());
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            return Task.FromResult<IList<Tweet>>(new List<Tweet>());
        }

        public Task<IList<Tweet>> GetMentionsAsync()
        {
            return this.GetMentionsAsync(0, 0, 0);
        }

        public Task<IList<Tweet>> GetMentionsAsync(int count)
        {
            return this.GetMentionsAsync(count, 0, 0);
        }

        public Task<IList<Tweet>> GetMentionsAsync(int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            return Task.FromResult<IList<Tweet>>(new List<Tweet>());
        }

        public Task<IList<Tweet>> GetRetweetsOfMeAsync()
        {
            return this.GetRetweetsOfMeAsync(0, 0, 0);
        }

        public Task<IList<Tweet>> GetRetweetsOfMeAsync(int count)
        {
            return this.GetRetweetsOfMeAsync(count, 0, 0);
        }

        public Task<IList<Tweet>> GetRetweetsOfMeAsync(int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            return Task.FromResult<IList<Tweet>>(new List<Tweet>());
        }

        public Task<Tweet> GetStatusAsync(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            return Task.FromResult<Tweet>(null);
        }

        public Task<Tweet> UpdateStatusAsync(string status)
        {
            return this.UpdateStatusAsync(status, new StatusDetails());
        }

        public Task<Tweet> UpdateStatusAsync(string status, IResource photo)
        {
            return this.UpdateStatusAsync(status, photo, new StatusDetails());
        }

        public Task<Tweet> UpdateStatusAsync(string status, StatusDetails details)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("status", status);
            AddStatusDetailsTo(request, details);
            // MIGRATION STUB: RestTemplate.PostForObjectAsync signature mismatch on .NET 10 Spring stub.
            return Task.FromResult<Tweet>(null);
        }

        public Task<Tweet> UpdateStatusAsync(string status, IResource photo, StatusDetails details)
        {
            IDictionary<string, object> request = new Dictionary<string, object>();
            request.Add("status", status);
            AddStatusDetailsTo(request, details);
            request.Add("media", photo);
            // MIGRATION STUB: RestTemplate.PostForObjectAsync signature mismatch on .NET 10 Spring stub.
            return Task.FromResult<Tweet>(null);
        }

        public Task<Tweet> DeleteStatusAsync(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync signature mismatch on .NET 10 Spring stub.
            return Task.FromResult<Tweet>(null);
        }

        public Task RetweetAsync(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.PostForMessageAsync not available on .NET 10 Spring stub.
            return Task.CompletedTask;
        }

        public Task<IList<Tweet>> GetRetweetsAsync(long tweetId)
        {
            return this.GetRetweetsAsync(tweetId, 100);
        }

        public Task<IList<Tweet>> GetRetweetsAsync(long tweetId, int count)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            return Task.FromResult<IList<Tweet>>(new List<Tweet>());
        }

        public Task<IList<Tweet>> GetFavoritesAsync()
        {
            return this.GetFavoritesAsync(0);
        }

        public Task<IList<Tweet>> GetFavoritesAsync(int count)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, 0, 0);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            return Task.FromResult<IList<Tweet>>(new List<Tweet>());
        }

        public Task AddToFavoritesAsync(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.PostForMessageAsync not available on .NET 10 Spring stub.
            return Task.CompletedTask;
        }

        public Task RemoveFromFavoritesAsync(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.PostForMessageAsync not available on .NET 10 Spring stub.
            return Task.CompletedTask;
        }
#else
#if !SILVERLIGHT
        public IList<Tweet> GetHomeTimeline()
        {
            return this.GetHomeTimeline(0, 0, 0);
        }

        public IList<Tweet> GetHomeTimeline(int count)
        {
            return this.GetHomeTimeline(count, 0, 0);
        }

        public IList<Tweet> GetHomeTimeline(int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            return null;
        }

        public IList<Tweet> GetUserTimeline()
        {
            return this.GetUserTimeline(0, 0, 0);
        }

        public IList<Tweet> GetUserTimeline(int count)
        {
            return this.GetUserTimeline(count, 0, 0);
        }

        public IList<Tweet> GetUserTimeline(int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            return null;
        }

        public IList<Tweet> GetUserTimeline(string screenName)
        {
            return this.GetUserTimeline(screenName, 0, 0, 0);
        }

        public IList<Tweet> GetUserTimeline(string screenName, int count)
        {
            return this.GetUserTimeline(screenName, count, 0, 0);
        }

        public IList<Tweet> GetUserTimeline(string screenName, int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            parameters.Add("screen_name", screenName);
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            return null;
        }

        public IList<Tweet> GetUserTimeline(long userId)
        {
            return this.GetUserTimeline(userId, 0, 0, 0);
        }

        public IList<Tweet> GetUserTimeline(long userId, int count)
        {
            return this.GetUserTimeline(userId, count, 0, 0);
        }

        public IList<Tweet> GetUserTimeline(long userId, int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            parameters.Add("user_id", userId.ToString());
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            return null;
        }

        public IList<Tweet> GetMentions()
        {
            return this.GetMentions(0, 0, 0);
        }

        public IList<Tweet> GetMentions(int count)
        {
            return this.GetMentions(count, 0, 0);
        }

        public IList<Tweet> GetMentions(int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            return null;
        }

        public IList<Tweet> GetRetweetsOfMe()
        {
            return this.GetRetweetsOfMe(0, 0, 0);
        }

        public IList<Tweet> GetRetweetsOfMe(int count)
        {
            return this.GetRetweetsOfMe(count, 0, 0);
        }

        public IList<Tweet> GetRetweetsOfMe(int count, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            return null;
        }

        public Tweet GetStatus(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            return null;
        }

        public Tweet UpdateStatus(string status)
        {
            return this.UpdateStatus(status, new StatusDetails());
        }

        public Tweet UpdateStatus(string status, IResource photo)
        {
            return this.UpdateStatus(status, photo, new StatusDetails());
        }

        public Tweet UpdateStatus(string status, StatusDetails details)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("status", status);
            AddStatusDetailsTo(request, details);
            // MIGRATION STUB: RestTemplate.PostForObject signature mismatch on .NET 10 Spring stub.
            return null;
        }

        public Tweet UpdateStatus(string status, IResource photo, StatusDetails details)
        {
            IDictionary<string, object> request = new Dictionary<string, object>();
            request.Add("status", status);
            AddStatusDetailsTo(request, details);
            request.Add("media", photo);
            // MIGRATION STUB: RestTemplate.PostForObject signature mismatch on .NET 10 Spring stub.
            return null;
        }

        public Tweet DeleteStatus(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.PostForObject signature mismatch on .NET 10 Spring stub.
            return null;
        }

        public void Retweet(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.PostForMessage not available on .NET 10 Spring stub.
        }

        public IList<Tweet> GetRetweets(long tweetId)
        {
            return this.GetRetweets(tweetId, 100);
        }

        public IList<Tweet> GetRetweets(long tweetId, int count)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            return null;
        }

        public IList<Tweet> GetFavorites()
        {
            return this.GetFavorites(0);
        }

        public IList<Tweet> GetFavorites(int count)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, 0, 0);
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            return null;
        }

        public void AddToFavorites(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.PostForMessage not available on .NET 10 Spring stub.
        }

        public void RemoveFromFavorites(long tweetId)
        {
            // MIGRATION STUB: RestTemplate.PostForMessage not available on .NET 10 Spring stub.
        }
#endif

        public RestOperationCanceler GetHomeTimelineAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetHomeTimelineAsync(0, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetHomeTimelineAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetHomeTimelineAsync(count, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetHomeTimelineAsync(int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler GetUserTimelineAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetUserTimelineAsync(1, 20, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetUserTimelineAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetUserTimelineAsync(count, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetUserTimelineAsync(int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler GetUserTimelineAsync(string screenName, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetUserTimelineAsync(screenName, 0, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetUserTimelineAsync(string screenName, int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetUserTimelineAsync(screenName, count, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetUserTimelineAsync(string screenName, int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            parameters.Add("screen_name", screenName);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler GetUserTimelineAsync(long userId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetUserTimelineAsync(userId, 0, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetUserTimelineAsync(long userId, int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetUserTimelineAsync(userId, count, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetUserTimelineAsync(long userId, int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            parameters.Add("user_id", userId.ToString());
            // MIGRATION STUB: RestTemplate.GetForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler GetMentionsAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetMentionsAsync(0, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetMentionsAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetMentionsAsync(count, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetMentionsAsync(int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler GetRetweetsOfMeAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetRetweetsOfMeAsync(0, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetRetweetsOfMeAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetRetweetsOfMeAsync(count, 0, 0, operationCompleted);
        }

        public RestOperationCanceler GetRetweetsOfMeAsync(int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler GetStatusAsync(long tweetId, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler UpdateStatusAsync(string status, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted)
        {
            return this.UpdateStatusAsync(status, new StatusDetails(), operationCompleted);
        }

        public RestOperationCanceler UpdateStatusAsync(string status, IResource photo, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted)
        {
            return this.UpdateStatusAsync(status, photo, new StatusDetails(), operationCompleted);
        }

        public RestOperationCanceler UpdateStatusAsync(string status, StatusDetails details, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("status", status);
            AddStatusDetailsTo(request, details);
            // MIGRATION STUB: RestTemplate.PostForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler UpdateStatusAsync(string status, IResource photo, StatusDetails details, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted)
        {
            IDictionary<string, object> request = new Dictionary<string, object>();
            request.Add("status", status);
            AddStatusDetailsTo(request, details);
            request.Add("media", photo);
            // MIGRATION STUB: RestTemplate.PostForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler DeleteStatusAsync(long tweetId, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler RetweetAsync(long tweetId, Action<RestOperationCompletedEventArgs<Spring.Http.HttpResponseMessage>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForMessageAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler GetRetweetsAsync(long tweetId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetRetweetsAsync(tweetId, 100, operationCompleted);
        }

        public RestOperationCanceler GetRetweetsAsync(long tweetId, int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler GetFavoritesAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetFavoritesAsync(0, operationCompleted);
        }

        public RestOperationCanceler GetFavoritesAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, 0, 0);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler AddToFavoritesAsync(long tweetId, Action<RestOperationCompletedEventArgs<Spring.Http.HttpResponseMessage>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForMessageAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }

        public RestOperationCanceler RemoveFromFavoritesAsync(long tweetId, Action<RestOperationCompletedEventArgs<Spring.Http.HttpResponseMessage>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForMessageAsync (callback overload) not available on .NET 10 Spring stub.
            return null;
        }
#endif

        #endregion

        #region Private Methods

        private static void AddStatusDetailsTo(NameValueCollection parameters, StatusDetails details)
        {
            if (details.Latitude.HasValue && details.Longitude.HasValue)
            {
                parameters.Add("lat", details.Latitude.Value.ToString(CultureInfo.InvariantCulture));
                parameters.Add("long", details.Longitude.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (details.DisplayCoordinates)
            {
                parameters.Add("display_coordinates", "true");
            }
            if (details.InReplyToStatusId.HasValue)
            {
                parameters.Add("in_reply_to_status_id", details.InReplyToStatusId.Value.ToString());
            }
            if (details.WrapLinks)
            {
                parameters.Add("wrap_links", "true");
            }
        }

        private static void AddStatusDetailsTo(IDictionary<string, object> parameters, StatusDetails details)
        {
            if (details.Latitude.HasValue && details.Longitude.HasValue)
            {
                parameters.Add("lat", details.Latitude.Value.ToString(CultureInfo.InvariantCulture));
                parameters.Add("long", details.Longitude.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (details.DisplayCoordinates)
            {
                parameters.Add("display_coordinates", "true");
            }
            if (details.InReplyToStatusId.HasValue)
            {
                parameters.Add("in_reply_to_status_id", details.InReplyToStatusId.Value.ToString());
            }
            if (details.WrapLinks)
            {
                parameters.Add("wrap_links", "true");
            }
        }

        #endregion
    }
}
