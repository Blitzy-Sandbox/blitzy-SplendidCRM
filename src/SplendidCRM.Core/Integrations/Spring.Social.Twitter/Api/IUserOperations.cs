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

// .NET 10 Migration: Conditional #if NET_4_0 || SILVERLIGHT_5 / #else / #if !SILVERLIGHT blocks removed.
// Task-based async methods (previously under #if NET_4_0 || SILVERLIGHT_5) are now unconditional —
// Task<T> is the standard async pattern for .NET 10.
// Callback-based async methods using Spring.Rest.Client.RestOperationCanceler (previously in #else
// branch) have been removed: Spring.Rest.Client and Spring.Http are discontinued libraries with no
// .NET Core / .NET 10 equivalent.  The Spring.* using directives are removed accordingly.
// System.Threading.Tasks import added unconditionally (was previously inside the conditional block).
// All synchronous and Task-based async method signatures are preserved per AAP §0.8.1 minimal
// change clause.  GetRateLimitStatus() return type kept as RateLimitStatus (single object) for
// consistency with the dormant UserTemplate stub implementation.
// This is a dormant Enterprise Edition integration stub — compile only, not expected to execute.

using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spring.Social.Twitter.Api
{
    /// <summary>
    /// Interface defining the operations for searching Twitter and retrieving user data.
    /// </summary>
    /// <author>Craig Walls</author>
    /// <author>Bruno Baia (.NET)</author>
    public interface IUserOperations
    {
        // =====================================================================
        // Synchronous methods
        // =====================================================================

        /// <summary>
        /// Retrieves the authenticated user's Twitter profile details.
        /// </summary>
        /// <returns>A <see cref="TwitterProfile"/> object representing the user's profile.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile GetUserProfile();

        /// <summary>
        /// Retrieves a specific user's Twitter profile details.
        /// Note that this method does not require authentication.
        /// </summary>
        /// <param name="screenName">The screen name for the user whose details are to be retrieved.</param>
        /// <returns>A <see cref="TwitterProfile"/> object representing the user's profile.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile GetUserProfile(string screenName);

        /// <summary>
        /// Retrieves a specific user's Twitter profile details.
        /// Note that this method does not require authentication.
        /// </summary>
        /// <param name="userId">The user ID for the user whose details are to be retrieved.</param>
        /// <returns>A <see cref="TwitterProfile"/> object representing the user's profile.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile GetUserProfile(long userId);

        /// <summary>
        /// Retrieves a list of Twitter profiles for the given list of user IDs.
        /// </summary>
        /// <param name="userIds">The list of user IDs.</param>
        /// <returns>A list of <see cref="TwitterProfile">user's profiles</see>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        IList<TwitterProfile> GetUsers(params long[] userIds);

        /// <summary>
        /// Retrieves a list of Twitter profiles for the given list of screen names.
        /// </summary>
        /// <param name="screenNames">The list of screen names.</param>
        /// <returns>A list of <see cref="TwitterProfile">user's profiles</see>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        IList<TwitterProfile> GetUsers(params string[] screenNames);

        /// <summary>
        /// Searches for up to 20 users that match a given query.
        /// </summary>
        /// <param name="query">The search query string.</param>
        /// <returns>A list of <see cref="TwitterProfile">user's profiles</see>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        IList<TwitterProfile> SearchForUsers(string query);

        /// <summary>
        /// Searches for users that match a given query.
        /// </summary>
        /// <param name="query">The search query string.</param>
        /// <param name="page">The page of search results to return.</param>
        /// <param name="pageSize">The number of <see cref="TwitterProfile"/>s per page. Maximum of 20 per page.</param>
        /// <returns>A list of <see cref="TwitterProfile">user's profiles</see>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        IList<TwitterProfile> SearchForUsers(string query, int page, int pageSize);

        /// <summary>
        /// Retrieves a list of categories from which suggested users to follow may be found.
        /// </summary>
        /// <returns>A list of suggestion categories.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        IList<SuggestionCategory> GetSuggestionCategories();

        /// <summary>
        /// Retrieves a list of suggestions of users to follow for a given category.
        /// </summary>
        /// <param name="slug">The category's slug.</param>
        /// <returns>A list of <see cref="TwitterProfile">user's profiles</see>.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        IList<TwitterProfile> GetSuggestions(string slug);

        /// <summary>
        /// Retrieves the current rate limit status for the authenticated user.
        /// </summary>
        /// <returns>The <see cref="RateLimitStatus"/> representing the current rate limit state.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        RateLimitStatus GetRateLimitStatus();

        // =====================================================================
        // Task-based asynchronous methods (.NET 10 — replaces Spring callback pattern)
        // =====================================================================

        /// <summary>
        /// Asynchronously retrieves the authenticated user's Twitter profile details.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{T}"/> that represents the asynchronous operation that can return
        /// a <see cref="TwitterProfile"/> object representing the user's profile.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        Task<TwitterProfile> GetUserProfileAsync();

        /// <summary>
        /// Asynchronously retrieves a list of Twitter profiles for the given list of user IDs.
        /// </summary>
        /// <param name="userIds">The list of user IDs.</param>
        /// <returns>
        /// A <see cref="Task{T}"/> that represents the asynchronous operation that can return
        /// a list of <see cref="TwitterProfile">user's profiles</see>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        Task<IList<TwitterProfile>> GetUsersAsync(params long[] userIds);

        /// <summary>
        /// Asynchronously searches for users that match a given query.
        /// </summary>
        /// <param name="query">The search query string.</param>
        /// <returns>
        /// A <see cref="Task{T}"/> that represents the asynchronous operation that can return
        /// a list of <see cref="TwitterProfile">user's profiles</see>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        Task<IList<TwitterProfile>> SearchForUsersAsync(string query);

        /// <summary>
        /// Asynchronously retrieves a list of categories from which suggested users to follow may be found.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{T}"/> that represents the asynchronous operation that can return
        /// a list of suggestion categories.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        Task<IList<SuggestionCategory>> GetSuggestionCategoriesAsync();

        /// <summary>
        /// Asynchronously retrieves a list of suggestions of users to follow for a given category.
        /// </summary>
        /// <param name="slug">The category's slug.</param>
        /// <returns>
        /// A <see cref="Task{T}"/> that represents the asynchronous operation that can return
        /// a list of <see cref="TwitterProfile">user's profiles</see>.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        Task<IList<TwitterProfile>> GetSuggestionsAsync(string slug);

        /// <summary>
        /// Asynchronously retrieves the current rate limit status for the authenticated user.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{T}"/> that represents the asynchronous operation that can return
        /// the <see cref="RateLimitStatus"/> representing the current rate limit state.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        Task<RateLimitStatus> GetRateLimitStatusAsync();
    }
}
