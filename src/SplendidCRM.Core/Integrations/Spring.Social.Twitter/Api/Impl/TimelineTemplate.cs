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

#nullable disable
using System;
using System.Collections.Generic;
#if NET_4_0 || SILVERLIGHT_5
using System.Threading.Tasks;
#else
using Spring.Rest.Client;
using Spring.Http;
#endif
using Spring.IO;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    // Dormant integration stub — compile-only, not executed.
    // Migrated from .NET Framework 4.8 to .NET 10 per AAP §0.8.1 (zero code changes
    // beyond framework compatibility). All methods are stub implementations returning
    // default/null values; this class is not activated in production.
    class TimelineTemplate : AbstractTwitterOperations, ITimelineOperations
    {
#if NET_4_0 || SILVERLIGHT_5
        public Task<IList<Tweet>> GetHomeTimelineAsync() { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetHomeTimelineAsync(int count) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetHomeTimelineAsync(int count, long sinceId, long maxId) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetUserTimelineAsync() { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetUserTimelineAsync(int count) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetUserTimelineAsync(int count, long sinceId, long maxId) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetUserTimelineAsync(string screenName) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetUserTimelineAsync(string screenName, int count) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetUserTimelineAsync(string screenName, int count, long sinceId, long maxId) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetUserTimelineAsync(long userId) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetUserTimelineAsync(long userId, int count) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetUserTimelineAsync(long userId, int count, long sinceId, long maxId) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetMentionsAsync() { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetMentionsAsync(int count) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetMentionsAsync(int count, long sinceId, long maxId) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetRetweetsOfMeAsync() { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetRetweetsOfMeAsync(int count) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetRetweetsOfMeAsync(int count, long sinceId, long maxId) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<Tweet> GetStatusAsync(long tweetId) { return Task.FromResult<Tweet>(null); }
        public Task<Tweet> UpdateStatusAsync(string status) { return Task.FromResult<Tweet>(null); }
        public Task<Tweet> UpdateStatusAsync(string status, IResource photo) { return Task.FromResult<Tweet>(null); }
        public Task<Tweet> UpdateStatusAsync(string status, StatusDetails details) { return Task.FromResult<Tweet>(null); }
        public Task<Tweet> UpdateStatusAsync(string status, IResource photo, StatusDetails details) { return Task.FromResult<Tweet>(null); }
        public Task<Tweet> DeleteStatusAsync(long tweetId) { return Task.FromResult<Tweet>(null); }
        public Task RetweetAsync(long tweetId) { return Task.CompletedTask; }
        public Task<IList<Tweet>> GetRetweetsAsync(long tweetId) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetRetweetsAsync(long tweetId, int count) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetFavoritesAsync() { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task<IList<Tweet>> GetFavoritesAsync(int count) { return Task.FromResult<IList<Tweet>>(new List<Tweet>()); }
        public Task AddToFavoritesAsync(long tweetId) { return Task.CompletedTask; }
        public Task RemoveFromFavoritesAsync(long tweetId) { return Task.CompletedTask; }
#else
#if !SILVERLIGHT
        public IList<Tweet> GetHomeTimeline() { return new List<Tweet>(); }
        public IList<Tweet> GetHomeTimeline(int count) { return new List<Tweet>(); }
        public IList<Tweet> GetHomeTimeline(int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public IList<Tweet> GetUserTimeline() { return new List<Tweet>(); }
        public IList<Tweet> GetUserTimeline(int count) { return new List<Tweet>(); }
        public IList<Tweet> GetUserTimeline(int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public IList<Tweet> GetUserTimeline(string screenName) { return new List<Tweet>(); }
        public IList<Tweet> GetUserTimeline(string screenName, int count) { return new List<Tweet>(); }
        public IList<Tweet> GetUserTimeline(string screenName, int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public IList<Tweet> GetUserTimeline(long userId) { return new List<Tweet>(); }
        public IList<Tweet> GetUserTimeline(long userId, int count) { return new List<Tweet>(); }
        public IList<Tweet> GetUserTimeline(long userId, int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public IList<Tweet> GetMentions() { return new List<Tweet>(); }
        public IList<Tweet> GetMentions(int count) { return new List<Tweet>(); }
        public IList<Tweet> GetMentions(int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public IList<Tweet> GetRetweetsOfMe() { return new List<Tweet>(); }
        public IList<Tweet> GetRetweetsOfMe(int count) { return new List<Tweet>(); }
        public IList<Tweet> GetRetweetsOfMe(int count, long sinceId, long maxId) { return new List<Tweet>(); }
        public Tweet GetStatus(long tweetId) { return null; }
        public Tweet UpdateStatus(string status) { return null; }
        public Tweet UpdateStatus(string status, IResource photo) { return null; }
        public Tweet UpdateStatus(string status, StatusDetails details) { return null; }
        public Tweet UpdateStatus(string status, IResource photo, StatusDetails details) { return null; }
        public Tweet DeleteStatus(long tweetId) { return null; }
        public void Retweet(long tweetId) { }
        public IList<Tweet> GetRetweets(long tweetId) { return new List<Tweet>(); }
        public IList<Tweet> GetRetweets(long tweetId, int count) { return new List<Tweet>(); }
        public IList<Tweet> GetFavorites() { return new List<Tweet>(); }
        public IList<Tweet> GetFavorites(int count) { return new List<Tweet>(); }
        public void AddToFavorites(long tweetId) { }
        public void RemoveFromFavorites(long tweetId) { }
#endif

        public RestOperationCanceler GetHomeTimelineAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetHomeTimelineAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetHomeTimelineAsync(int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetUserTimelineAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetUserTimelineAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetUserTimelineAsync(int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetUserTimelineAsync(string screenName, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetUserTimelineAsync(string screenName, int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetUserTimelineAsync(string screenName, int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetUserTimelineAsync(long userId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetUserTimelineAsync(long userId, int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetUserTimelineAsync(long userId, int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetMentionsAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetMentionsAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetMentionsAsync(int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetRetweetsOfMeAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetRetweetsOfMeAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetRetweetsOfMeAsync(int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetStatusAsync(long tweetId, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted) { return null; }
        public RestOperationCanceler UpdateStatusAsync(string status, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted) { return null; }
        public RestOperationCanceler UpdateStatusAsync(string status, IResource photo, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted) { return null; }
        public RestOperationCanceler UpdateStatusAsync(string status, StatusDetails details, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted) { return null; }
        public RestOperationCanceler UpdateStatusAsync(string status, IResource photo, StatusDetails details, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted) { return null; }
        public RestOperationCanceler DeleteStatusAsync(long tweetId, Action<RestOperationCompletedEventArgs<Tweet>> operationCompleted) { return null; }
        public RestOperationCanceler RetweetAsync(long tweetId, Action<RestOperationCompletedEventArgs<Spring.Http.HttpResponseMessage>> operationCompleted) { return null; }
        public RestOperationCanceler GetRetweetsAsync(long tweetId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetRetweetsAsync(long tweetId, int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetFavoritesAsync(Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler GetFavoritesAsync(int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted) { return null; }
        public RestOperationCanceler AddToFavoritesAsync(long tweetId, Action<RestOperationCompletedEventArgs<Spring.Http.HttpResponseMessage>> operationCompleted) { return null; }
        public RestOperationCanceler RemoveFromFavoritesAsync(long tweetId, Action<RestOperationCompletedEventArgs<Spring.Http.HttpResponseMessage>> operationCompleted) { return null; }
#endif
    }
}
