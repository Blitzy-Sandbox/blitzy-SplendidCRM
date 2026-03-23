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

// .NET 10 Migration: Conditional compilation directives (#if NET_4_0, #if SILVERLIGHT_5) removed.
// Only Task-based async methods retained; synchronous and callback-based async overloads removed.
// Spring.Rest.Client.RestTemplate field and constructor preserved for structural fidelity.
// Method bodies use Task.CompletedTask — dormant stub, not executed at runtime (AAP section 0.7.4).

#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Spring.Http;
using Spring.Rest.Client;

namespace Spring.Social.LinkedIn.Api.Impl
{
    /// <summary>
    /// Implementation of <see cref="ICommunicationOperations"/>, providing a binding to LinkedIn's communications-oriented REST resources.
    /// </summary>
    /// <author>Robert Drysdale</author>
    /// <author>Bruno Baia</author>
    class CommunicationTemplate : ICommunicationOperations
    {
        private RestTemplate restTemplate;

        public CommunicationTemplate(RestTemplate restTemplate)
        {
            this.restTemplate = restTemplate;
        }

        #region ICommunicationOperations Members

        /// <summary>
        /// Asynchronously sends a textual message to a recipient specified by its ID.
        /// </summary>
        /// <param name="subject">The subject of the message.</param>
        /// <param name="body">The body or text of the message (does not support html).</param>
        /// <param name="recipientId">The recipient <see cref="LinkedInProfile"/> ID.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task SendMessageAsync(string subject, string body, string recipientId)
        {
            // STUB: Dormant — Spring.Rest.Client.RestTemplate.PostForMessageAsync not available on .NET 10.
            // Original: return this.restTemplate.PostForMessageAsync("people/~/mailbox", new Message(subject, body, new string[] { recipientId }));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Asynchronously sends a textual message to a list of recipients specified by their ID.
        /// </summary>
        /// <param name="subject">The subject of the message.</param>
        /// <param name="body">The body or text of the message (does not support html).</param>
        /// <param name="recipientIds">The list of recipient <see cref="LinkedInProfile"/> IDs. At least one.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task SendMessageAsync(string subject, string body, string[] recipientIds)
        {
            // STUB: Dormant — Spring.Rest.Client.RestTemplate.PostForMessageAsync not available on .NET 10.
            // Original: return this.restTemplate.PostForMessageAsync("people/~/mailbox", new Message(subject, body, recipientIds));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Asynchronously sends a connect invitation message to recipient ID.
        /// </summary>
        /// <param name="subject">The subject of the invitation message.</param>
        /// <param name="body">The body or text of the invitation message (does not support html).</param>
        /// <param name="recipientId">The recipient ID.</param>
        /// <param name="recipientAuthToken">The recipient additional authorization token returned when performing a search.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task ConnectToAsync(string subject, string body, string recipientId, string recipientAuthToken)
        {
            // STUB: Dormant — Spring.Rest.Client.RestTemplate.PostForMessageAsync not available on .NET 10.
            // Original: return this.restTemplate.PostForMessageAsync("people/~/mailbox", new Invitation(subject, body, new InvitationRecipient(recipientId, recipientAuthToken)));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Asynchronously sends a connect invitation message to an email (for users not on LinkedIn).
        /// </summary>
        /// <param name="subject">The subject of the invitation message.</param>
        /// <param name="body">The body or text of the invitation message (does not support html).</param>
        /// <param name="email">The email address of the recipient.</param>
        /// <param name="firstName">The first name of the recipient.</param>
        /// <param name="lastName">The last name of the recipient.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task ConnectToAsync(string subject, string body, string email, string firstName, string lastName)
        {
            // STUB: Dormant — Spring.Rest.Client.RestTemplate.PostForMessageAsync not available on .NET 10.
            // Original: return this.restTemplate.PostForMessageAsync("people/~/mailbox", new Invitation(subject, body, new InvitationRecipient(email, firstName, lastName)));
            return Task.CompletedTask;
        }

        #endregion
    }
}
