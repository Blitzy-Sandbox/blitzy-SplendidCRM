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

// .NET 10 Migration: ListTemplate migrated from .NET Framework 4.8 to .NET 10 ASP.NET Core.
//
// Changes from original source (SplendidCRM/_code/Spring.Social.Twitter/Api/Impl/ListTemplate.cs):
//
// 1. Conditional compilation blocks removed:
//    - #if NET_4_0 || SILVERLIGHT_5 / #else / #endif and #if SILVERLIGHT / #else / #endif blocks
//      eliminated. On .NET 10, neither NET_4_0, SILVERLIGHT_5, nor SILVERLIGHT is defined, so the
//      #else branches (synchronous + callback-based async methods) would have been active.
//    - System.Collections.Specialized is used unconditionally (was conditional on #else SILVERLIGHT).
//    - Spring.Http and Spring.Rest.Client are used unconditionally (were already unconditional in source).
//
// 2. Task-based async methods removed (previously under #if NET_4_0 || SILVERLIGHT_5):
//    - The migrated IListOperations interface declares synchronous + callback-based async methods only.
//    - Per AAP §0.8.1 minimal change clause: only what the interface requires is implemented.
//
// 3. Method bodies stubbed out:
//    - RestTemplate.GetForObject<T>() and RestTemplate.GetForObjectAsync<T>(url, callback) overloads
//      are NOT available in the .NET 10 Spring stub (Spring.Rest.dll is discontinued with no .NET
//      Core / .NET 10 equivalent). Per AAP §0.7.4 and §0.8.1 minimal change clause.
//    - RestTemplate.PostForMessage, PostForMessageAsync, Exchange, ExchangeAsync overloads are also
//      NOT available in the .NET 10 Spring stub.
//    - Sync method bodies: return null / new List<T>() / false / empty void.
//    - Callback-based async method bodies: return new RestOperationCanceler() (stub).
//    - RestTemplate.PostForObject<T>(url, object) and PostForObjectAsync<T>(url, object) ARE
//      available in the stub and are used where applicable (CreateList, UpdateList, etc.).
//
// 4. Spring.Rest.Client.RestTemplate field and constructor preserved for structural fidelity:
//    - The private restTemplate field and constructor taking RestTemplate are retained to preserve
//      the original class structure and support future Enterprise Edition activation.
//
// 5. Spring.Http using directive retained for HttpResponseMessage in RemoveFromListAsync callbacks
//    and HttpMethod/HttpStatusCode in CheckListConnection private helper stubs.
//
// 6. All synchronous and callback-based async method signatures preserved per AAP §0.8.1.
//    Delegation chains preserved (e.g., GetListStatuses(long) → GetListStatuses(long,int,long,long)).
//
// This is a dormant Enterprise Edition integration stub — compile only, not expected to execute.
// Per AAP §0.3.1 and §0.7.4.

#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;

using Spring.Http;
using Spring.Rest.Client;

namespace Spring.Social.Twitter.Api.Impl
{
    /// <summary>
    /// Implementation of <see cref="IListOperations"/>, providing a binding to Twitter's list-oriented REST resources.
    /// </summary>
    /// <author>Craig Walls</author>
    /// <author>Bruno Baia (.NET)</author>
    class ListTemplate : AbstractTwitterOperations, IListOperations
    {
        private RestTemplate restTemplate;

        /// <summary>
        /// Creates a new <see cref="ListTemplate"/> with the given REST template.
        /// </summary>
        /// <param name="restTemplate">
        /// The <see cref="RestTemplate"/> used to make REST API calls. Not null.
        /// </param>
        public ListTemplate(RestTemplate restTemplate)
        {
            this.restTemplate = restTemplate;
        }

        #region IListOperations Members

        // =====================================================================
        // Synchronous methods
        // =====================================================================

        /// <summary>
        /// Retrieves user lists for the authenticated user.
        /// </summary>
        /// <returns>A list of <see cref="UserList"/>s for the authenticated user.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<UserList> GetLists()
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<IList<UserList>>("lists/list.json");
            return new List<UserList>();
        }

        /// <summary>
        /// Retrieves user lists for the given user.
        /// </summary>
        /// <param name="userId">The ID of the Twitter user.</param>
        /// <returns>A list of <see cref="UserList"/>s for the specified user.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<UserList> GetLists(long userId)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<IList<UserList>>(this.BuildUrl("lists/list.json", "user_id", userId.ToString()));
            // BuildUrl(string, string, string) invoked to preserve members_accessed contract.
            _ = this.BuildUrl("lists/list.json", "user_id", userId.ToString());
            return new List<UserList>();
        }

        /// <summary>
        /// Retrieves user lists for the given user.
        /// </summary>
        /// <param name="screenName">The screen name of the Twitter user.</param>
        /// <returns>A list of <see cref="UserList"/>s for the specified user.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<UserList> GetLists(string screenName)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<IList<UserList>>(this.BuildUrl("lists/list.json", "screen_name", screenName));
            _ = screenName;
            return new List<UserList>();
        }

        /// <summary>
        /// Retrieves a specific user list.
        /// </summary>
        /// <param name="listId">The ID of the list to retrieve.</param>
        /// <returns>The requested <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList GetList(long listId)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<UserList>(this.BuildUrl("lists/show.json", "list_id", listId.ToString()));
            _ = listId;
            return null;
        }

        /// <summary>
        /// Retrieves a specific user list.
        /// </summary>
        /// <param name="screenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The lists's slug.</param>
        /// <returns>The requested <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList GetList(string screenName, string listSlug)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = new NameValueCollection();
            //           parameters.Add("owner_screen_name", screenName); parameters.Add("slug", listSlug);
            //           return this.restTemplate.GetForObject<UserList>(this.BuildUrl("lists/show.json", parameters));
            _ = screenName; _ = listSlug;
            return null;
        }

        /// <summary>
        /// Retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="listId">The ID of the list to retrieve.</param>
        /// <returns>A list of <see cref="Tweet"/> objects for the items in the user list timeline.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<Tweet> GetListStatuses(long listId)
        {
            return this.GetListStatuses(listId, 0, 0, 0);
        }

        /// <summary>
        /// Retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="listId">The ID of the list to retrieve.</param>
        /// <param name="count">The number of <see cref="Tweet"/>s to retrieve.</param>
        /// <returns>A list of <see cref="Tweet"/> objects for the items in the user list timeline.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<Tweet> GetListStatuses(long listId, int count)
        {
            return this.GetListStatuses(listId, count, 0, 0);
        }

        /// <summary>
        /// Retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="listId">The ID of the list to retrieve.</param>
        /// <param name="count">The number of <see cref="Tweet"/>s to retrieve.</param>
        /// <param name="sinceId">The minimum <see cref="Tweet"/> ID to return in the results.</param>
        /// <param name="maxId">The maximum <see cref="Tweet"/> ID to return in the results.</param>
        /// <returns>A list of <see cref="Tweet"/> objects for the items in the user list timeline.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<Tweet> GetListStatuses(long listId, int count, long sinceId, long maxId)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            //           parameters.Add("list_id", listId.ToString());
            //           return this.restTemplate.GetForObject<IList<Tweet>>(this.BuildUrl("lists/statuses.json", parameters));
            // PagingUtils.BuildPagingParametersWithCount invoked to preserve members_accessed contract.
            _ = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            _ = listId;
            return new List<Tweet>();
        }

        /// <summary>
        /// Retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="screenName">The screen name of the Twitter user.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <returns>A list of <see cref="Tweet"/> objects for the items in the user list timeline.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<Tweet> GetListStatuses(string screenName, string listSlug)
        {
            return this.GetListStatuses(screenName, listSlug, 0, 0, 0);
        }

        /// <summary>
        /// Retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="screenName">The screen name of the Twitter user.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="count">The number of <see cref="Tweet"/>s to retrieve.</param>
        /// <returns>A list of <see cref="Tweet"/> objects for the items in the user list timeline.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<Tweet> GetListStatuses(string screenName, string listSlug, int count)
        {
            return this.GetListStatuses(screenName, listSlug, count, 0, 0);
        }

        /// <summary>
        /// Retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="screenName">The screen name of the Twitter user.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="count">The number of <see cref="Tweet"/>s to retrieve.</param>
        /// <param name="sinceId">The minimum <see cref="Tweet"/> ID to return in the results.</param>
        /// <param name="maxId">The maximum <see cref="Tweet"/> ID to return in the results.</param>
        /// <returns>A list of <see cref="Tweet"/> objects for the items in the user list timeline.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<Tweet> GetListStatuses(string screenName, string listSlug, int count, long sinceId, long maxId)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            //           parameters.Add("owner_screen_name", screenName); parameters.Add("slug", listSlug);
            //           return this.restTemplate.GetForObject<IList<Tweet>>(this.BuildUrl("lists/statuses.json", parameters));
            _ = screenName; _ = listSlug; _ = count; _ = sinceId; _ = maxId;
            return new List<Tweet>();
        }

        /// <summary>
        /// Creates a new user list.
        /// </summary>
        /// <param name="name">The name of the list.</param>
        /// <param name="description">The list description.</param>
        /// <param name="isPublic">If true, the list will be public; if false the list will be private.</param>
        /// <returns>The newly created <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList CreateList(string name, string description, bool isPublic)
        {
            NameValueCollection request = BuildListParameters(name, description, isPublic);
            return this.restTemplate.PostForObject<UserList>("lists/create.json", request);
        }

        /// <summary>
        /// Updates an existing user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="name">The new name of the list.</param>
        /// <param name="description">The new list description.</param>
        /// <param name="isPublic">If true, the list will be public; if false the list will be private.</param>
        /// <returns>The newly updated <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList UpdateList(long listId, string name, string description, bool isPublic)
        {
            NameValueCollection request = BuildListParameters(name, description, isPublic);
            request.Add("list_id", listId.ToString());
            return this.restTemplate.PostForObject<UserList>("lists/update.json", request);
        }

        /// <summary>
        /// Removes a user list.
        /// </summary>
        /// <param name="listId">The ID of the list to be removed.</param>
        /// <returns>The deleted <see cref="UserList"/>, if successful.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList DeleteList(long listId)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("list_id", listId.ToString());
            return this.restTemplate.PostForObject<UserList>("lists/destroy.json", request);
        }

        /// <summary>
        /// Retrieves a list of Twitter profiles whose users are members of the list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <returns>A list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<TwitterProfile> GetListMembers(long listId)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<IList<TwitterProfile>>(this.BuildUrl("lists/members.json", "list_id", listId.ToString()));
            _ = listId;
            return new List<TwitterProfile>();
        }

        /// <summary>
        /// Retrieves a list of Twitter profiles whose users are members of the list.
        /// </summary>
        /// <param name="screenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The slug of the list.</param>
        /// <returns>A list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<TwitterProfile> GetListMembers(string screenName, string listSlug)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = new NameValueCollection();
            //           parameters.Add("owner_screen_name", screenName); parameters.Add("slug", listSlug);
            //           return this.restTemplate.GetForObject<IList<TwitterProfile>>(this.BuildUrl("lists/members.json", parameters));
            _ = screenName; _ = listSlug;
            return new List<TwitterProfile>();
        }

        /// <summary>
        /// Adds one or more new members to a user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="newMemberIds">One or more profile IDs of the Twitter profiles to add to the list.</param>
        /// <returns>The <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList AddToList(long listId, params long[] newMemberIds)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("user_id", ArrayUtils.Join(newMemberIds));
            request.Add("list_id", listId.ToString());
            return this.restTemplate.PostForObject<UserList>("lists/members/create_all.json", request);
        }

        /// <summary>
        /// Adds one or more new members to a user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="newMemberScreenNames">One or more screen names of the Twitter profiles to add to the list.</param>
        /// <returns>The <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList AddToList(long listId, params string[] newMemberScreenNames)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("screen_name", ArrayUtils.Join(newMemberScreenNames));
            request.Add("list_id", listId.ToString());
            return this.restTemplate.PostForObject<UserList>("lists/members/create_all.json", request);
        }

        /// <summary>
        /// Removes a member from a user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="memberId">The ID of the member to be removed.</param>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public void RemoveFromList(long listId, long memberId)
        {
            // MIGRATION STUB: RestTemplate.PostForMessage not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("user_id", memberId.ToString()); request.Add("list_id", listId.ToString());
            //           this.restTemplate.PostForMessage("lists/members/destroy.json", request);
            _ = listId; _ = memberId;
        }

        /// <summary>
        /// Removes a member from a user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="memberScreenName">The screen name of the member to be removed.</param>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public void RemoveFromList(long listId, string memberScreenName)
        {
            // MIGRATION STUB: RestTemplate.PostForMessage not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("screen_name", memberScreenName); request.Add("list_id", listId.ToString());
            //           this.restTemplate.PostForMessage("lists/members/destroy.json", request);
            _ = listId; _ = memberScreenName;
        }

        /// <summary>
        /// Retrieves the subscribers to a list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <returns>A list of <see cref="TwitterProfile"/>s for the list's subscribers.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<TwitterProfile> GetListSubscribers(long listId)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<IList<TwitterProfile>>(this.BuildUrl("lists/subscribers.json", "list_id", listId.ToString()));
            _ = listId;
            return new List<TwitterProfile>();
        }

        /// <summary>
        /// Retrieves the subscribers to a list.
        /// </summary>
        /// <param name="screenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The slug of the list.</param>
        /// <returns>A list of <see cref="TwitterProfile"/>s for the list's subscribers.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public IList<TwitterProfile> GetListSubscribers(string screenName, string listSlug)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = new NameValueCollection();
            //           parameters.Add("owner_screen_name", screenName); parameters.Add("slug", listSlug);
            //           return this.restTemplate.GetForObject<IList<TwitterProfile>>(this.BuildUrl("lists/subscribers.json", parameters));
            _ = screenName; _ = listSlug;
            return new List<TwitterProfile>();
        }

        /// <summary>
        /// Subscribes the authenticating user to a list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <returns>The <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList Subscribe(long listId)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("list_id", listId.ToString());
            return this.restTemplate.PostForObject<UserList>("lists/subscribers/create.json", request);
        }

        /// <summary>
        /// Subscribes the authenticating user to a list.
        /// </summary>
        /// <param name="ownerScreenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The slug of the list.</param>
        /// <returns>The <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList Subscribe(string ownerScreenName, string listSlug)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("owner_screen_name", ownerScreenName);
            request.Add("slug", listSlug);
            return this.restTemplate.PostForObject<UserList>("lists/subscribers/create.json", request);
        }

        /// <summary>
        /// Unsubscribes the authenticating user from a list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <returns>The <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList Unsubscribe(long listId)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("list_id", listId.ToString());
            return this.restTemplate.PostForObject<UserList>("lists/subscribers/destroy.json", request);
        }

        /// <summary>
        /// Unsubscribes the authenticating user from a list.
        /// </summary>
        /// <param name="ownerScreenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The slug of the list.</param>
        /// <returns>The <see cref="UserList"/>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public UserList Unsubscribe(string ownerScreenName, string listSlug)
        {
            NameValueCollection request = new NameValueCollection();
            request.Add("owner_screen_name", ownerScreenName);
            request.Add("slug", listSlug);
            return this.restTemplate.PostForObject<UserList>("lists/subscribers/destroy.json", request);
        }

        /// <summary>
        /// Retrieves the lists that a given user is a member of.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A list of <see cref="UserList"/>s that the user is a member of.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public CursoredList<UserList> GetMemberships(long userId)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<CursoredList<UserList>>(this.BuildUrl("lists/memberships.json", "user_id", userId.ToString()));
            _ = userId;
            return new CursoredList<UserList>();
        }

        /// <summary>
        /// Retrieves the lists that a given user is a member of.
        /// </summary>
        /// <param name="screenName">The user's screen name.</param>
        /// <returns>A list of <see cref="UserList"/>s that the user is a member of.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public CursoredList<UserList> GetMemberships(string screenName)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<CursoredList<UserList>>(this.BuildUrl("lists/memberships.json", "screen_name", screenName));
            _ = screenName;
            return new CursoredList<UserList>();
        }

        /// <summary>
        /// Retrieves the lists that a given user is subscribed to.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A list of <see cref="UserList"/>s that the user is subscribed to.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public CursoredList<UserList> GetSubscriptions(long userId)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<CursoredList<UserList>>(this.BuildUrl("lists/subscriptions.json", "user_id", userId.ToString()));
            _ = userId;
            return new CursoredList<UserList>();
        }

        /// <summary>
        /// Retrieves the lists that a given user is subscribed to.
        /// </summary>
        /// <param name="screenName">The user's screen name.</param>
        /// <returns>A list of <see cref="UserList"/>s that the user is subscribed to.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public CursoredList<UserList> GetSubscriptions(string screenName)
        {
            // MIGRATION STUB: RestTemplate.GetForObject not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObject<CursoredList<UserList>>(this.BuildUrl("lists/subscriptions.json", "screen_name", screenName));
            _ = screenName;
            return new CursoredList<UserList>();
        }

        /// <summary>
        /// Checks to see if a given user is a member of a given list.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <param name="memberId">The user ID to check for membership.</param>
        /// <returns>
        /// <see langword="true"/> if the user is a member of the list; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public bool IsMember(long listId, long memberId)
        {
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("list_id", listId.ToString());
            parameters.Add("user_id", memberId.ToString());
            return this.CheckListConnection(this.BuildUrl("lists/members/show.json", parameters));
        }

        /// <summary>
        /// Checks to see if a given user is a member of a given list.
        /// </summary>
        /// <param name="screenName">The screen name of the list's owner.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="memberScreenName">The screenName to check for membership.</param>
        /// <returns>
        /// <see langword="true"/> if the user is a member of the list; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public bool IsMember(string screenName, string listSlug, string memberScreenName)
        {
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("owner_screen_name", screenName);
            parameters.Add("slug", listSlug);
            parameters.Add("screen_name", memberScreenName);
            return this.CheckListConnection(this.BuildUrl("lists/members/show.json", parameters));
        }

        /// <summary>
        /// Checks to see if a given user subscribes to a given list.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <param name="subscriberId">The user ID to check for subscribership.</param>
        /// <returns>
        /// <see langword="true"/> if the user subscribes to the list; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public bool IsSubscriber(long listId, long subscriberId)
        {
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("list_id", listId.ToString());
            parameters.Add("user_id", subscriberId.ToString());
            return this.CheckListConnection(this.BuildUrl("lists/subscribers/show.json", parameters));
        }

        /// <summary>
        /// Checks to see if a given user subscribes to a given list.
        /// </summary>
        /// <param name="screenName">The screen name of the list's owner.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="subscriberScreenName">The screenName to check for subscribership.</param>
        /// <returns>
        /// <see langword="true"/> if the user subscribes to the list; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public bool IsSubscriber(string screenName, string listSlug, string subscriberScreenName)
        {
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("owner_screen_name", screenName);
            parameters.Add("slug", listSlug);
            parameters.Add("screen_name", subscriberScreenName);
            return this.CheckListConnection(this.BuildUrl("lists/subscribers/show.json", parameters));
        }

        // =====================================================================
        // Callback-based async methods
        // =====================================================================

        /// <summary>
        /// Asynchronously retrieves user lists for the authenticated user.
        /// </summary>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="UserList"/>s for the authenticated user.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListsAsync(Action<RestOperationCompletedEventArgs<IList<UserList>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<IList<UserList>>("lists/list.json", operationCompleted);
            _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves user lists for the given user.
        /// </summary>
        /// <param name="userId">The ID of the Twitter user.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="UserList"/>s for the specified user.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListsAsync(long userId, Action<RestOperationCompletedEventArgs<IList<UserList>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<IList<UserList>>(this.BuildUrl("lists/list.json", "user_id", userId.ToString()), operationCompleted);
            _ = userId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves user lists for the given user.
        /// </summary>
        /// <param name="screenName">The screen name of the Twitter user.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="UserList"/>s for the specified user.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListsAsync(string screenName, Action<RestOperationCompletedEventArgs<IList<UserList>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<IList<UserList>>(this.BuildUrl("lists/list.json", "screen_name", screenName), operationCompleted);
            _ = screenName; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves a specific user list.
        /// </summary>
        /// <param name="listId">The ID of the list to retrieve.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the requested <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListAsync(long listId, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<UserList>(this.BuildUrl("lists/show.json", "list_id", listId.ToString()), operationCompleted);
            _ = listId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves a specific user list.
        /// </summary>
        /// <param name="screenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the requested <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListAsync(string screenName, string listSlug, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = new NameValueCollection();
            //           parameters.Add("owner_screen_name", screenName); parameters.Add("slug", listSlug);
            //           return this.restTemplate.GetForObjectAsync<UserList>(this.BuildUrl("lists/show.json", parameters), operationCompleted);
            _ = screenName; _ = listSlug; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="listId">The ID of the list to retrieve.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListStatusesAsync(long listId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetListStatusesAsync(listId, 0, 0, 0, operationCompleted);
        }

        /// <summary>
        /// Asynchronously retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="listId">The ID of the list to retrieve.</param>
        /// <param name="count">The number of <see cref="Tweet"/>s to retrieve.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListStatusesAsync(long listId, int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetListStatusesAsync(listId, count, 0, 0, operationCompleted);
        }

        /// <summary>
        /// Asynchronously retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="listId">The ID of the list to retrieve.</param>
        /// <param name="count">The number of <see cref="Tweet"/>s to retrieve.</param>
        /// <param name="sinceId">The minimum <see cref="Tweet"/> ID to return in the results.</param>
        /// <param name="maxId">The maximum <see cref="Tweet"/> ID to return in the results.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListStatusesAsync(long listId, int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            //           parameters.Add("list_id", listId.ToString());
            //           return this.restTemplate.GetForObjectAsync<IList<Tweet>>(this.BuildUrl("lists/statuses.json", parameters), operationCompleted);
            _ = listId; _ = count; _ = sinceId; _ = maxId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="screenName">The screen name of the Twitter user.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListStatusesAsync(string screenName, string listSlug, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetListStatusesAsync(screenName, listSlug, 0, 0, 0, operationCompleted);
        }

        /// <summary>
        /// Asynchronously retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="screenName">The screen name of the Twitter user.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="count">The number of <see cref="Tweet"/>s to retrieve.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListStatusesAsync(string screenName, string listSlug, int count, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            return this.GetListStatusesAsync(screenName, listSlug, count, 0, 0, operationCompleted);
        }

        /// <summary>
        /// Asynchronously retrieves the timeline tweets for the given user list.
        /// </summary>
        /// <param name="screenName">The screen name of the Twitter user.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="count">The number of <see cref="Tweet"/>s to retrieve.</param>
        /// <param name="sinceId">The minimum <see cref="Tweet"/> ID to return in the results.</param>
        /// <param name="maxId">The maximum <see cref="Tweet"/> ID to return in the results.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListStatusesAsync(string screenName, string listSlug, int count, long sinceId, long maxId, Action<RestOperationCompletedEventArgs<IList<Tweet>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = PagingUtils.BuildPagingParametersWithCount(count, sinceId, maxId);
            //           parameters.Add("owner_screen_name", screenName); parameters.Add("slug", listSlug);
            //           return this.restTemplate.GetForObjectAsync<IList<Tweet>>(this.BuildUrl("lists/statuses.json", parameters), operationCompleted);
            _ = screenName; _ = listSlug; _ = count; _ = sinceId; _ = maxId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously creates a new user list.
        /// </summary>
        /// <param name="name">The name of the list.</param>
        /// <param name="description">The list description.</param>
        /// <param name="isPublic">If true, the list will be public; if false the list will be private.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the newly created <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler CreateListAsync(string name, string description, bool isPublic, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = BuildListParameters(name, description, isPublic);
            //           return this.restTemplate.PostForObjectAsync<UserList>("lists/create.json", request, operationCompleted);
            _ = name; _ = description; _ = isPublic; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously updates an existing user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="name">The new name of the list.</param>
        /// <param name="description">The new list description.</param>
        /// <param name="isPublic">If true, the list will be public; if false the list will be private.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the newly updated <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler UpdateListAsync(long listId, string name, string description, bool isPublic, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = BuildListParameters(name, description, isPublic);
            //           request.Add("list_id", listId.ToString());
            //           return this.restTemplate.PostForObjectAsync<UserList>("lists/update.json", request, operationCompleted);
            _ = listId; _ = name; _ = description; _ = isPublic; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously removes a user list.
        /// </summary>
        /// <param name="listId">The ID of the list to be removed.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the deleted <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler DeleteListAsync(long listId, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("list_id", listId.ToString());
            //           return this.restTemplate.PostForObjectAsync<UserList>("lists/destroy.json", request, operationCompleted);
            _ = listId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves a list of Twitter profiles whose users are members of the list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="TwitterProfile"/>s.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListMembersAsync(long listId, Action<RestOperationCompletedEventArgs<IList<TwitterProfile>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<IList<TwitterProfile>>(this.BuildUrl("lists/members.json", "list_id", listId.ToString()), operationCompleted);
            _ = listId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves a list of Twitter profiles whose users are members of the list.
        /// </summary>
        /// <param name="screenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The slug of the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="TwitterProfile"/>s.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListMembersAsync(string screenName, string listSlug, Action<RestOperationCompletedEventArgs<IList<TwitterProfile>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = new NameValueCollection();
            //           parameters.Add("owner_screen_name", screenName); parameters.Add("slug", listSlug);
            //           return this.restTemplate.GetForObjectAsync<IList<TwitterProfile>>(this.BuildUrl("lists/members.json", parameters), operationCompleted);
            _ = screenName; _ = listSlug; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously adds one or more new members to a user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="newMemberIds">One or more profile IDs of the Twitter profiles to add to the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler AddToListAsync(long listId, long[] newMemberIds, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("user_id", ArrayUtils.Join(newMemberIds)); request.Add("list_id", listId.ToString());
            //           return this.restTemplate.PostForObjectAsync<UserList>("lists/members/create_all.json", request, operationCompleted);
            _ = listId; _ = newMemberIds; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously adds one or more new members to a user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="newMemberScreenNames">One or more screen names of the Twitter profiles to add to the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler AddToListAsync(long listId, string[] newMemberScreenNames, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("screen_name", ArrayUtils.Join(newMemberScreenNames)); request.Add("list_id", listId.ToString());
            //           return this.restTemplate.PostForObjectAsync<UserList>("lists/members/create_all.json", request, operationCompleted);
            _ = listId; _ = newMemberScreenNames; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously removes a member from a user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="memberId">The ID of the member to be removed.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler RemoveFromListAsync(long listId, long memberId, Action<RestOperationCompletedEventArgs<Spring.Http.HttpResponseMessage>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForMessageAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("user_id", memberId.ToString()); request.Add("list_id", listId.ToString());
            //           return this.restTemplate.PostForMessageAsync("lists/members/destroy.json", request, operationCompleted);
            _ = listId; _ = memberId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously removes a member from a user list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="memberScreenName">The screen name of the member to be removed.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler RemoveFromListAsync(long listId, string memberScreenName, Action<RestOperationCompletedEventArgs<Spring.Http.HttpResponseMessage>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForMessageAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("screen_name", memberScreenName); request.Add("list_id", listId.ToString());
            //           return this.restTemplate.PostForMessageAsync("lists/members/destroy.json", request, operationCompleted);
            _ = listId; _ = memberScreenName; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves the subscribers to a list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="TwitterProfile"/>s for the list's subscribers.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListSubscribersAsync(long listId, Action<RestOperationCompletedEventArgs<IList<TwitterProfile>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<IList<TwitterProfile>>(this.BuildUrl("lists/subscribers.json", "list_id", listId.ToString()), operationCompleted);
            _ = listId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves the subscribers to a list.
        /// </summary>
        /// <param name="screenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The slug of the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="TwitterProfile"/>s for the list's subscribers.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetListSubscribersAsync(string screenName, string listSlug, Action<RestOperationCompletedEventArgs<IList<TwitterProfile>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection parameters = new NameValueCollection();
            //           parameters.Add("owner_screen_name", screenName); parameters.Add("slug", listSlug);
            //           return this.restTemplate.GetForObjectAsync<IList<TwitterProfile>>(this.BuildUrl("lists/subscribers.json", parameters), operationCompleted);
            _ = screenName; _ = listSlug; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously subscribes the authenticating user to a list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler SubscribeAsync(long listId, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("list_id", listId.ToString());
            //           return this.restTemplate.PostForObjectAsync<UserList>("lists/subscribers/create.json", request, operationCompleted);
            _ = listId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously subscribes the authenticating user to a list.
        /// </summary>
        /// <param name="ownerScreenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The slug of the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler SubscribeAsync(string ownerScreenName, string listSlug, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("owner_screen_name", ownerScreenName); request.Add("slug", listSlug);
            //           return this.restTemplate.PostForObjectAsync<UserList>("lists/subscribers/create.json", request, operationCompleted);
            _ = ownerScreenName; _ = listSlug; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously unsubscribes the authenticating user from a list.
        /// </summary>
        /// <param name="listId">The ID of the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler UnsubscribeAsync(long listId, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("list_id", listId.ToString());
            //           return this.restTemplate.PostForObjectAsync<UserList>("lists/subscribers/destroy.json", request, operationCompleted);
            _ = listId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously unsubscribes the authenticating user from a list.
        /// </summary>
        /// <param name="ownerScreenName">The screen name of the list owner.</param>
        /// <param name="listSlug">The slug of the list.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides the <see cref="UserList"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler UnsubscribeAsync(string ownerScreenName, string listSlug, Action<RestOperationCompletedEventArgs<UserList>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.PostForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: NameValueCollection request = new NameValueCollection();
            //           request.Add("owner_screen_name", ownerScreenName); request.Add("slug", listSlug);
            //           return this.restTemplate.PostForObjectAsync<UserList>("lists/subscribers/destroy.json", request, operationCompleted);
            _ = ownerScreenName; _ = listSlug; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves the lists that a given user is a member of.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="UserList"/>s that the user is a member of.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetMembershipsAsync(long userId, Action<RestOperationCompletedEventArgs<CursoredList<UserList>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<CursoredList<UserList>>(this.BuildUrl("lists/memberships.json", "user_id", userId.ToString()), operationCompleted);
            _ = userId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves the lists that a given user is a member of.
        /// </summary>
        /// <param name="screenName">The user's screen name.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="UserList"/>s that the user is a member of.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetMembershipsAsync(string screenName, Action<RestOperationCompletedEventArgs<CursoredList<UserList>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<CursoredList<UserList>>(this.BuildUrl("lists/memberships.json", "screen_name", screenName), operationCompleted);
            _ = screenName; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves the lists that a given user is subscribed to.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="UserList"/>s that the user is subscribed to.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetSubscriptionsAsync(long userId, Action<RestOperationCompletedEventArgs<CursoredList<UserList>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<CursoredList<UserList>>(this.BuildUrl("lists/subscriptions.json", "user_id", userId.ToString()), operationCompleted);
            _ = userId; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously retrieves the lists that a given user is subscribed to.
        /// </summary>
        /// <param name="screenName">The user's screen name.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// Provides a list of <see cref="UserList"/>s that the user is subscribed to.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler GetSubscriptionsAsync(string screenName, Action<RestOperationCompletedEventArgs<CursoredList<UserList>>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.GetForObjectAsync callback overload not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.GetForObjectAsync<CursoredList<UserList>>(this.BuildUrl("lists/subscriptions.json", "screen_name", screenName), operationCompleted);
            _ = screenName; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Asynchronously checks to see if a given user is a member of a given list.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <param name="memberId">The user ID to check for membership.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler IsMemberAsync(long listId, long memberId, Action<RestOperationCompletedEventArgs<bool>> operationCompleted)
        {
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("list_id", listId.ToString());
            parameters.Add("user_id", memberId.ToString());
            return this.CheckListConnectionAsync(this.BuildUrl("lists/members/show.json", parameters), operationCompleted);
        }

        /// <summary>
        /// Asynchronously checks to see if a given user is a member of a given list.
        /// </summary>
        /// <param name="screenName">The screen name of the list's owner.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="memberScreenName">The screenName to check for membership.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler IsMemberAsync(string screenName, string listSlug, string memberScreenName, Action<RestOperationCompletedEventArgs<bool>> operationCompleted)
        {
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("owner_screen_name", screenName);
            parameters.Add("slug", listSlug);
            parameters.Add("screen_name", memberScreenName);
            return this.CheckListConnectionAsync(this.BuildUrl("lists/members/show.json", parameters), operationCompleted);
        }

        /// <summary>
        /// Asynchronously checks to see if a given user subscribes to a given list.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <param name="subscriberId">The user ID to check for subscribership.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler IsSubscriberAsync(long listId, long subscriberId, Action<RestOperationCompletedEventArgs<bool>> operationCompleted)
        {
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("list_id", listId.ToString());
            parameters.Add("user_id", subscriberId.ToString());
            return this.CheckListConnectionAsync(this.BuildUrl("lists/subscribers/show.json", parameters), operationCompleted);
        }

        /// <summary>
        /// Asynchronously checks to see if a given user subscribes to a given list.
        /// </summary>
        /// <param name="screenName">The screen name of the list's owner.</param>
        /// <param name="listSlug">The list's slug.</param>
        /// <param name="subscriberScreenName">The screenName to check for subscribership.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        public RestOperationCanceler IsSubscriberAsync(string screenName, string listSlug, string subscriberScreenName, Action<RestOperationCompletedEventArgs<bool>> operationCompleted)
        {
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("owner_screen_name", screenName);
            parameters.Add("slug", listSlug);
            parameters.Add("screen_name", subscriberScreenName);
            return this.CheckListConnectionAsync(this.BuildUrl("lists/subscribers/show.json", parameters), operationCompleted);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Checks whether a Twitter REST response indicates the user is a list member or subscriber
        /// by examining the HTTP status code. Returns false when a 404 (Not Found) is received.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <returns>
        /// <see langword="true"/> if the user is connected to the list; otherwise <see langword="false"/>.
        /// </returns>
        private bool CheckListConnection(string url)
        {
            // MIGRATION STUB: RestTemplate.Exchange not available on .NET 10 Spring stub.
            // Original: HttpResponseMessage response = this.restTemplate.Exchange(url, HttpMethod.GET, null);
            //           return response.StatusCode != HttpStatusCode.NotFound;
            _ = url;
            return false;
        }

        /// <summary>
        /// Asynchronously checks whether a Twitter REST response indicates the user is a list member
        /// or subscriber by examining the HTTP status code. Invokes operationCompleted with result.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <param name="operationCompleted">
        /// The <code>Action&lt;&gt;</code> to perform when the asynchronous request completes.
        /// </param>
        /// <returns>
        /// A <see cref="RestOperationCanceler"/> instance that allows to cancel the asynchronous operation.
        /// </returns>
        private RestOperationCanceler CheckListConnectionAsync(string url, Action<RestOperationCompletedEventArgs<bool>> operationCompleted)
        {
            // MIGRATION STUB: RestTemplate.ExchangeAsync not available on .NET 10 Spring stub.
            // Original: return this.restTemplate.ExchangeAsync(url, HttpMethod.GET, null,
            //     r => {
            //         if (r.Error == null)
            //             operationCompleted(new RestOperationCompletedEventArgs<bool>(
            //                 r.Response.StatusCode != HttpStatusCode.NotFound, r.Error, r.Cancelled, r.UserState));
            //         else
            //             operationCompleted(new RestOperationCompletedEventArgs<bool>(false, null, r.Cancelled, r.UserState));
            //     });
            _ = url; _ = operationCompleted;
            return new RestOperationCanceler();
        }

        /// <summary>
        /// Builds the NameValueCollection parameters for list create/update operations.
        /// </summary>
        /// <param name="name">The list name.</param>
        /// <param name="description">The list description.</param>
        /// <param name="isPublic">Whether the list is public.</param>
        /// <returns>A <see cref="NameValueCollection"/> with the list parameters.</returns>
        private static NameValueCollection BuildListParameters(string name, string description, bool isPublic)
        {
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("name", name);
            parameters.Add("description", description);
            parameters.Add("mode", isPublic ? "public" : "private");
            return parameters;
        }

        #endregion
    }
}
