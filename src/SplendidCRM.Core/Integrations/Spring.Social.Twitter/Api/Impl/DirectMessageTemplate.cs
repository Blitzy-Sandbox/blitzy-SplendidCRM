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

// .NET 10 Migration: DirectMessageTemplate migrated from .NET Framework 4.8 to .NET 10 ASP.NET Core.
// 
// Changes from original source (SplendidCRM/_code/Spring.Social.Twitter/Api/Impl/DirectMessageTemplate.cs):
//
// 1. Conditional compilation blocks removed:
//    - #if NET_4_0 || SILVERLIGHT_5 / #else / #endif blocks eliminated.
//    - On .NET 10, neither NET_4_0 nor SILVERLIGHT_5 is defined, so the #else branch would have been
//      active. However, that branch depends on RestTemplate.GetForObjectAsync with callback overloads
//      which are not available in the Spring stub (Spring.Rest.dll is discontinued with no .NET Core
//      equivalent). Per AAP §0.7.4 and §0.8.1 minimal change clause, the conditional blocks are removed.
//    - #if SILVERLIGHT / #else / #endif (for NameValueCollection namespace) resolved to the #else
//      branch unconditionally — System.Collections.Specialized is used directly.
//
// 2. Task-based async methods are now unconditional (previously under #if NET_4_0 || SILVERLIGHT_5):
//    - Task<T> is the standard async pattern for .NET 10. System.Threading.Tasks import added
//      unconditionally (was previously inside the conditional block).
//
// 3. Callback-based async methods removed (previously in the #else branch):
//    - Methods using Action<RestOperationCompletedEventArgs<T>> callbacks and returning
//      RestOperationCanceler have been removed.
//    - The Spring stub provides RestOperationCanceler and RestOperationCompletedEventArgs<T> but
//      the RestTemplate stub lacks GetForObjectAsync callback overloads needed by these methods.
//    - The migrated IDirectMessageOperations interface no longer declares callback-based methods.
//    - Per AAP §0.8.1 minimal change clause.
//
// 4. Spring.Rest.Client.RestTemplate field and constructor preserved for structural fidelity:
//    - The private restTemplate field and constructor taking RestTemplate are retained to preserve
//      the original class structure and support future Enterprise Edition activation.
//    - Method bodies stub via Task.FromResult / null returns — RestTemplate.GetForObjectAsync and
//      RestTemplate.GetForObject are not available in the .NET 10 stub.
//
// 5. using Spring.Rest.Client retained unconditionally (was already unconditional in source).
//
// This is a dormant Enterprise Edition integration stub — compile only, not expected to execute.
// Per AAP §0.3.1 and §0.7.4.

#nullable disable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;

using Spring.Rest.Client;

namespace Spring.Social.Twitter.Api.Impl
{
    /// <summary>
    /// Implementation of <see cref="IDirectMessageOperations"/>, providing a binding to Twitter's
    /// direct message-oriented REST resources.
    /// </summary>
    /// <author>Craig Walls</author>
    /// <author>Bruno Baia (.NET)</author>
    class DirectMessageTemplate : AbstractTwitterOperations, IDirectMessageOperations
    {
        private RestTemplate restTemplate;

        /// <summary>
        /// Creates a new <see cref="DirectMessageTemplate"/> with the given REST template.
        /// </summary>
        /// <param name="restTemplate">
        /// The <see cref="RestTemplate"/> used to make REST API calls. Not null.
        /// </param>
        public DirectMessageTemplate(RestTemplate restTemplate)
        {
            this.restTemplate = restTemplate;
        }

        #region IDirectMessageOperations Members

        // =====================================================================
        // Synchronous methods
        // =====================================================================

        /// <summary>
        /// Retrieves the 20 most recently received direct messages for the authenticating user.
        /// The most recently received messages are listed first.
        /// </summary>
        /// <returns>
        /// A collection of <see cref="DirectMessage"/> with the authenticating user as the recipient.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<DirectMessage> GetDirectMessagesReceived()
        {
            return this.GetDirectMessagesReceived(1, 20, 0, 0);
        }

        /// <summary>
        /// Retrieves received direct messages for the authenticating user.
        /// The most recently received messages are listed first.
        /// </summary>
        /// <param name="page">The page to return.</param>
        /// <param name="pageSize">
        /// The number of <see cref="DirectMessage"/>s per page. Should be less than or equal to 200.
        /// (Will return at most 200 entries, even if pageSize is greater than 200.)
        /// </param>
        /// <returns>
        /// A collection of <see cref="DirectMessage"/> with the authenticating user as the recipient.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<DirectMessage> GetDirectMessagesReceived(int page, int pageSize)
        {
            return this.GetDirectMessagesReceived(page, pageSize, 0, 0);
        }

        /// <summary>
        /// Retrieves received direct messages for the authenticating user.
        /// The most recently received messages are listed first.
        /// </summary>
        /// <param name="page">The page to return.</param>
        /// <param name="pageSize">
        /// The number of <see cref="DirectMessage"/>s per page. Should be less than or equal to 200.
        /// (Will return at most 200 entries, even if pageSize is greater than 200.)
        /// </param>
        /// <param name="sinceId">The minimum <see cref="DirectMessage"/> ID to return in the results.</param>
        /// <param name="maxId">The maximum <see cref="DirectMessage"/> ID to return in the results.</param>
        /// <returns>
        /// A collection of <see cref="DirectMessage"/> with the authenticating user as the recipient.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<DirectMessage> GetDirectMessagesReceived(int page, int pageSize, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithPageCount(page, pageSize, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<IList<DirectMessage>>(this.BuildUrl("direct_messages.json", parameters));
            // Suppress unused variables — preserved for Enterprise Edition activation.
            _ = parameters;
            return new List<DirectMessage>();
        }

        /// <summary>
        /// Retrieves the 20 most recent direct messages sent by the authenticating user.
        /// The most recently sent messages are listed first.
        /// </summary>
        /// <returns>
        /// A collection of <see cref="DirectMessage"/> with the authenticating user as the sender.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<DirectMessage> GetDirectMessagesSent()
        {
            return this.GetDirectMessagesSent(1, 20, 0, 0);
        }

        /// <summary>
        /// Retrieves direct messages sent by the authenticating user.
        /// The most recently sent messages are listed first.
        /// </summary>
        /// <param name="page">The page to return.</param>
        /// <param name="pageSize">
        /// The number of <see cref="DirectMessage"/>s per page. Should be less than or equal to 200.
        /// (Will return at most 200 entries, even if pageSize is greater than 200.)
        /// </param>
        /// <returns>
        /// A collection of <see cref="DirectMessage"/> with the authenticating user as the sender.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<DirectMessage> GetDirectMessagesSent(int page, int pageSize)
        {
            return this.GetDirectMessagesSent(page, pageSize, 0, 0);
        }

        /// <summary>
        /// Retrieves direct messages sent by the authenticating user.
        /// The most recently sent messages are listed first.
        /// </summary>
        /// <param name="page">The page to return.</param>
        /// <param name="pageSize">
        /// The number of <see cref="DirectMessage"/>s per page. Should be less than or equal to 200.
        /// (Will return at most 200 entries, even if pageSize is greater than 200.)
        /// </param>
        /// <param name="sinceId">The minimum <see cref="DirectMessage"/> ID to return in the results.</param>
        /// <param name="maxId">The maximum <see cref="DirectMessage"/> ID to return in the results.</param>
        /// <returns>
        /// A collection of <see cref="DirectMessage"/> with the authenticating user as the sender.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<DirectMessage> GetDirectMessagesSent(int page, int pageSize, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithPageCount(page, pageSize, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<IList<DirectMessage>>(this.BuildUrl("direct_messages/sent.json", parameters));
            _ = parameters;
            return new List<DirectMessage>();
        }

        /// <summary>
        /// Retrieves a direct message by its ID. The message must be readable by the authenticating user.
        /// </summary>
        /// <param name="id">The message ID.</param>
        /// <returns>The <see cref="DirectMessage"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public DirectMessage GetDirectMessage(long id)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<DirectMessage>(this.BuildUrl("direct_messages/show.json", "id", id.ToString()));
            _ = id;
            return null;
        }

        /// <summary>
        /// Sends a direct message to another Twitter user.
        /// </summary>
        /// <param name="toScreenName">The screen name of the recipient of the messages.</param>
        /// <param name="text">The message text.</param>
        /// <returns>The <see cref="DirectMessage"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public DirectMessage SendDirectMessage(string toScreenName, string text)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("screen_name", toScreenName);
            request.Add("text", text);
            // MIGRATION STUB: RestTemplate.PostForObject does not match the Spring original signature.
            // Original: return this.restTemplate.PostForObject<DirectMessage>("direct_messages/new.json", request);
            _ = request;
            return null;
        }

        /// <summary>
        /// Sends a direct message to another Twitter user.
        /// </summary>
        /// <param name="toUserId">The Twitter user ID of the recipient of the messages.</param>
        /// <param name="text">The message text.</param>
        /// <returns>The <see cref="DirectMessage"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public DirectMessage SendDirectMessage(long toUserId, string text)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("user_id", toUserId.ToString());
            request.Add("text", text);
            // MIGRATION STUB: RestTemplate.PostForObject does not match the Spring original signature.
            // Original: return this.restTemplate.PostForObject<DirectMessage>("direct_messages/new.json", request);
            _ = request;
            return null;
        }

        /// <summary>
        /// Deletes a direct message for the authenticated user.
        /// </summary>
        /// <param name="messageId">The ID of the message to be removed.</param>
        /// <returns>The deleted <see cref="DirectMessage"/>, if successful.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public DirectMessage DeleteDirectMessage(long messageId)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("id", messageId.ToString());
            // MIGRATION STUB: RestTemplate.PostForObject does not match the Spring original signature.
            // Original: return this.restTemplate.PostForObject<DirectMessage>("direct_messages/destroy.json", request);
            _ = request;
            return null;
        }

        // =====================================================================
        // Task-based asynchronous methods (.NET 10 — standard async pattern)
        // Previously guarded by #if NET_4_0 || SILVERLIGHT_5, now unconditional.
        // =====================================================================

        /// <summary>
        /// Asynchronously retrieves the 20 most recently received direct messages for the authenticating user.
        /// The most recently received messages are listed first.
        /// </summary>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// a collection of <see cref="DirectMessage"/> with the authenticating user as the recipient.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<IList<DirectMessage>> GetDirectMessagesReceivedAsync()
        {
            return this.GetDirectMessagesReceivedAsync(1, 20, 0, 0);
        }

        /// <summary>
        /// Asynchronously retrieves received direct messages for the authenticating user.
        /// The most recently received messages are listed first.
        /// </summary>
        /// <param name="page">The page to return.</param>
        /// <param name="pageSize">
        /// The number of <see cref="DirectMessage"/>s per page. Should be less than or equal to 200.
        /// (Will return at most 200 entries, even if pageSize is greater than 200.)
        /// </param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// a collection of <see cref="DirectMessage"/> with the authenticating user as the recipient.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<IList<DirectMessage>> GetDirectMessagesReceivedAsync(int page, int pageSize)
        {
            return this.GetDirectMessagesReceivedAsync(page, pageSize, 0, 0);
        }

        /// <summary>
        /// Asynchronously retrieves received direct messages for the authenticating user.
        /// The most recently received messages are listed first.
        /// </summary>
        /// <param name="page">The page to return.</param>
        /// <param name="pageSize">
        /// The number of <see cref="DirectMessage"/>s per page. Should be less than or equal to 200.
        /// (Will return at most 200 entries, even if pageSize is greater than 200.)
        /// </param>
        /// <param name="sinceId">The minimum <see cref="DirectMessage"/> ID to return in the results.</param>
        /// <param name="maxId">The maximum <see cref="DirectMessage"/> ID to return in the results.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// a collection of <see cref="DirectMessage"/> with the authenticating user as the recipient.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<IList<DirectMessage>> GetDirectMessagesReceivedAsync(int page, int pageSize, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithPageCount(page, pageSize, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<IList<DirectMessage>>(this.BuildUrl("direct_messages.json", parameters));
            _ = parameters;
            return Task.FromResult<IList<DirectMessage>>(new List<DirectMessage>());
        }

        /// <summary>
        /// Asynchronously retrieves the 20 most recent direct messages sent by the authenticating user.
        /// The most recently sent messages are listed first.
        /// </summary>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// a collection of <see cref="DirectMessage"/> with the authenticating user as the sender.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<IList<DirectMessage>> GetDirectMessagesSentAsync()
        {
            return this.GetDirectMessagesSentAsync(1, 20, 0, 0);
        }

        /// <summary>
        /// Asynchronously retrieves direct messages sent by the authenticating user.
        /// The most recently sent messages are listed first.
        /// </summary>
        /// <param name="page">The page to return.</param>
        /// <param name="pageSize">
        /// The number of <see cref="DirectMessage"/>s per page. Should be less than or equal to 200.
        /// (Will return at most 200 entries, even if pageSize is greater than 200.)
        /// </param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// a collection of <see cref="DirectMessage"/> with the authenticating user as the sender.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<IList<DirectMessage>> GetDirectMessagesSentAsync(int page, int pageSize)
        {
            return this.GetDirectMessagesSentAsync(page, pageSize, 0, 0);
        }

        /// <summary>
        /// Asynchronously retrieves direct messages sent by the authenticating user.
        /// The most recently sent messages are listed first.
        /// </summary>
        /// <param name="page">The page to return.</param>
        /// <param name="pageSize">
        /// The number of <see cref="DirectMessage"/>s per page. Should be less than or equal to 200.
        /// (Will return at most 200 entries, even if pageSize is greater than 200.)
        /// </param>
        /// <param name="sinceId">The minimum <see cref="DirectMessage"/> ID to return in the results.</param>
        /// <param name="maxId">The maximum <see cref="DirectMessage"/> ID to return in the results.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// a collection of <see cref="DirectMessage"/> with the authenticating user as the sender.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<IList<DirectMessage>> GetDirectMessagesSentAsync(int page, int pageSize, long sinceId, long maxId)
        {
            NameValueCollection parameters = PagingUtils.BuildPagingParametersWithPageCount(page, pageSize, sinceId, maxId);
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<IList<DirectMessage>>(this.BuildUrl("direct_messages/sent.json", parameters));
            _ = parameters;
            return Task.FromResult<IList<DirectMessage>>(new List<DirectMessage>());
        }

        /// <summary>
        /// Asynchronously retrieves a direct message by its ID. The message must be readable by the authenticating user.
        /// </summary>
        /// <param name="id">The message ID.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// the <see cref="DirectMessage"/>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<DirectMessage> GetDirectMessageAsync(long id)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<DirectMessage>(this.BuildUrl("direct_messages/show.json", "id", id.ToString()));
            _ = id;
            return Task.FromResult<DirectMessage>(null);
        }

        /// <summary>
        /// Asynchronously sends a direct message to another Twitter user.
        /// </summary>
        /// <param name="toScreenName">The screen name of the recipient of the messages.</param>
        /// <param name="text">The message text.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// the <see cref="DirectMessage"/>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<DirectMessage> SendDirectMessageAsync(string toScreenName, string text)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("screen_name", toScreenName);
            request.Add("text", text);
            // MIGRATION STUB: RestTemplate.PostForObjectAsync does not match the Spring Task-returning signature.
            // Original: return this.restTemplate.PostForObjectAsync<DirectMessage>("direct_messages/new.json", request);
            _ = request;
            return Task.FromResult<DirectMessage>(null);
        }

        /// <summary>
        /// Asynchronously sends a direct message to another Twitter user.
        /// </summary>
        /// <param name="toUserId">The Twitter user ID of the recipient of the messages.</param>
        /// <param name="text">The message text.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// the <see cref="DirectMessage"/>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<DirectMessage> SendDirectMessageAsync(long toUserId, string text)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("user_id", toUserId.ToString());
            request.Add("text", text);
            // MIGRATION STUB: RestTemplate.PostForObjectAsync does not match the Spring Task-returning signature.
            // Original: return this.restTemplate.PostForObjectAsync<DirectMessage>("direct_messages/new.json", request);
            _ = request;
            return Task.FromResult<DirectMessage>(null);
        }

        /// <summary>
        /// Asynchronously deletes a direct message for the authenticated user.
        /// </summary>
        /// <param name="messageId">The ID of the message to be removed.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return
        /// the deleted <see cref="DirectMessage"/>, if successful.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public Task<DirectMessage> DeleteDirectMessageAsync(long messageId)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("id", messageId.ToString());
            // MIGRATION STUB: RestTemplate.PostForObjectAsync does not match the Spring Task-returning signature.
            // Original: return this.restTemplate.PostForObjectAsync<DirectMessage>("direct_messages/destroy.json", request);
            _ = request;
            return Task.FromResult<DirectMessage>(null);
        }

        #endregion
    }
}
