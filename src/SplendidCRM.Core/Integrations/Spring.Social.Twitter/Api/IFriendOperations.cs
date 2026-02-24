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
// Synchronous methods (previously under #if !SILVERLIGHT inside #else branch) are now unconditional —
// this is the standard pattern for dormant Spring.Social.Twitter integration stubs on .NET 10.
// Task-based async methods (previously under #if NET_4_0 || SILVERLIGHT_5) removed — not needed
// for dormant stub compilation.
// Callback-based async methods using Spring.Rest.Client.RestOperationCanceler (previously in #else
// branch) have been removed: Spring.Rest.Client and Spring.Http are discontinued libraries with no
// .NET Core / .NET 10 equivalent. The Spring.* using directives are removed accordingly.
// All synchronous method signatures are preserved per AAP §0.8.1 minimal change clause.
// This is a dormant Enterprise Edition integration stub — compile only, not expected to execute.

using System;
using System.IO;
using System.Collections.Generic;

namespace Spring.Social.Twitter.Api
{
    /// <summary>
    /// Interface defining the operations for working with a user's friends and followers.
    /// </summary>
    /// <author>Craig Walls</author>
    /// <author>Bruno Baia (.NET)</author>
    public interface IFriendOperations
    {
        // =====================================================================
        // Synchronous methods
        // =====================================================================

        /// <summary>
        /// Retrieves a list of up to 20 users that the authenticated user follows.
        /// <para/>
        /// Call GetFriendsInCursor() with a cursor value to get the next/previous page of entries.
        /// <para/>
        /// If all you need is the friend IDs, consider calling GetFriendIds() instead.
        /// </summary>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFriends();

        /// <summary>
        /// Retrieves a list of up to 20 users that the authenticated user follows.
        /// <para/>
        /// If all you need is the friend IDs, consider calling GetFriendIds() instead.
        /// </summary>
        /// <param name="cursor">The cursor used to fetch the friend IDs.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFriendsInCursor(long cursor);

        /// <summary>
        /// Retrieves a list of up to 20 users that the given user follows.
        /// <para/>
        /// Call GetFriendsInCursor() with a cursor value to get the next/previous page of entries.
        /// <para/>
        /// If all you need is the friend IDs, consider calling GetFriendIds() instead.
        /// </summary>
        /// <param name="userId">The user's Twitter ID.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFriends(long userId);

        /// <summary>
        /// Retrieves a list of up to 20 users that the given user follows.
        /// <para/>
        /// If all you need is the friend IDs, consider calling GetFriendIds() instead.
        /// </summary>
        /// <param name="userId">The user's Twitter ID.</param>
        /// <param name="cursor">The cursor used to fetch the friend IDs.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFriendsInCursor(long userId, long cursor);

        /// <summary>
        /// Retrieves a list of up to 20 users that the given user follows.
        /// <para/>
        /// Call GetFriendsInCursor() with a cursor value to get the next/previous page of entries.
        /// <para/>
        /// If all you need is the friend IDs, consider calling GetFriendIds() instead.
        /// </summary>
        /// <param name="screenName">The user's Twitter screen name.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFriends(string screenName);

        /// <summary>
        /// Retrieves a list of up to 20 users that the given user follows.
        /// <para/>
        /// If all you need is the friend IDs, consider calling GetFriendIds() instead.
        /// </summary>
        /// <param name="screenName">The user's Twitter screen name.</param>
        /// <param name="cursor">The cursor used to fetch the friend IDs.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFriendsInCursor(string screenName, long cursor);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that the authenticated user follows.
        /// </summary>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFriendIds();

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that the authenticated user follows.
        /// </summary>
        /// <param name="cursor">
        /// The cursor value to fetch a specific page of entries. Use -1 for the first page of entries.
        /// </param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFriendIdsInCursor(long cursor);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that the given user follows.
        /// </summary>
        /// <param name="userId">The user's Twitter ID.</param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFriendIds(long userId);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that the given user follows.
        /// </summary>
        /// <param name="userId">The user's Twitter ID.</param>
        /// <param name="cursor">The cursor value to fetch a specific page of entries. Use -1 for the first page of entries.</param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFriendIdsInCursor(long userId, long cursor);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that the given user follows.
        /// </summary>
        /// <param name="screenName">The user's Twitter screen name.</param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFriendIds(string screenName);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that the given user follows.
        /// </summary>
        /// <param name="screenName">The user's Twitter screen name.</param>
        /// <param name="cursor">The cursor value to fetch a specific page of entries. Use -1 for the first page of entries.</param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFriendIdsInCursor(string screenName, long cursor);

        /// <summary>
        /// Retrieves a list of up to 20 users that the authenticated user is being followed by.
        /// <para/>
        /// Call GetFollowersInCursor() with a cursor value to get the next/previous page of entries.
        /// <para/>
        /// If all you need is the follower IDs, consider calling GetFollowerIds() instead.
        /// </summary>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFollowers();

        /// <summary>
        /// Retrieves a list of up to 20 users that the authenticated user is being followed by.
        /// <para/>
        /// If all you need is the follower IDs, consider calling GetFollowerIds() instead.
        /// </summary>
        /// <param name="cursor">The cursor used to fetch the follower IDs.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFollowersInCursor(long cursor);

        /// <summary>
        /// Retrieves a list of up to 20 users that the given user is being followed by.
        /// <para/>
        /// Call GetFollowersInCursor() with a cursor value to get the next/previous page of entries.
        /// <para/>
        /// If all you need is the follower IDs, consider calling GetFollowerIds() instead.
        /// </summary>
        /// <param name="userId">The user's Twitter ID.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFollowers(long userId);

        /// <summary>
        /// Retrieves a list of up to 20 users that the given user is being followed by.
        /// <para/>
        /// If all you need is the follower IDs, consider calling GetFollowerIds() instead.
        /// </summary>
        /// <param name="userId">The user's Twitter ID.</param>
        /// <param name="cursor">The cursor used to fetch the follower IDs.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFollowersInCursor(long userId, long cursor);

        /// <summary>
        /// Retrieves a list of up to 20 users that the given user is being followed by.
        /// <para/>
        /// Call GetFollowersInCursor() with a cursor value to get the next/previous page of entries.
        /// <para/>
        /// If all you need is the follower IDs, consider calling GetFollowerIds() instead.
        /// </summary>
        /// <param name="screenName">The user's Twitter screen name.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFollowers(string screenName);

        /// <summary>
        /// Retrieves a list of up to 20 users that the given user is being followed by.
        /// <para/>
        /// If all you need is the follower IDs, consider calling GetFollowerIds() instead.
        /// </summary>
        /// <param name="screenName">The user's Twitter screen name.</param>
        /// <param name="cursor">The cursor used to fetch the follower IDs.</param>
        /// <returns>A cursored list of <see cref="TwitterProfile"/>s.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetFollowersInCursor(string screenName, long cursor);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that follow the authenticated user.
        /// </summary>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFollowerIds();

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that follow the authenticated user.
        /// </summary>
        /// <param name="cursor">The cursor value to fetch a specific page of entries. Use -1 for the first page of entries.</param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFollowerIdsInCursor(long cursor);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that follow the given user.
        /// </summary>
        /// <param name="userId">The user's Twitter ID.</param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFollowerIds(long userId);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that follow the given user.
        /// </summary>
        /// <param name="userId">The user's Twitter ID.</param>
        /// <param name="cursor">The cursor value to fetch a specific page of entries. Use -1 for the first page of entries.</param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFollowerIdsInCursor(long userId, long cursor);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that follow the given user.
        /// </summary>
        /// <param name="screenName">The user's Twitter screen name.</param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFollowerIds(string screenName);

        /// <summary>
        /// Retrieves a list of up to 5000 IDs for the Twitter users that follow the given user.
        /// </summary>
        /// <param name="screenName">The user's Twitter screen name.</param>
        /// <param name="cursor">The cursor value to fetch a specific page of entries. Use -1 for the first page of entries.</param>
        /// <returns>A cursored list of user IDs.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetFollowerIdsInCursor(string screenName, long cursor);

        /// <summary>
        /// Allows the authenticated user to follow (create a friendship) with another user.
        /// </summary>
        /// <param name="userId">The Twitter ID of the user to follow.</param>
        /// <returns>The <see cref="TwitterProfile"/> of the followed user if successful.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile Follow(long userId);

        /// <summary>
        /// Allows the authenticated user to follow (create a friendship) with another user.
        /// </summary>
        /// <param name="screenName">The screen name of the user to follow.</param>
        /// <returns>The <see cref="TwitterProfile"/> of the followed user if successful</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile Follow(string screenName);

        /// <summary>
        /// Allows the authenticated user to follow (create a friendship) with another user.
        /// </summary>
        /// <param name="userId">The Twitter ID of the user to unfollow.</param>
        /// <returns>The <see cref="TwitterProfile"/> of the unfollowed user if successful.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile Unfollow(long userId);

        /// <summary>
        /// Allows the authenticated use to unfollow (destroy a friendship) with another user.
        /// </summary>
        /// <param name="screenName">The screen name of the user to unfollow.</param>
        /// <returns>The <see cref="TwitterProfile"/> of the unfollowed user if successful.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile Unfollow(string screenName);

        /// <summary>
        /// Enables mobile device notifications from Twitter for the specified user.
        /// </summary>
        /// <param name="userId">The Twitter ID of the user to receive notifications for.</param>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        void EnableNotifications(long userId);

        /// <summary>
        /// Enables mobile device notifications from Twitter for the specified user.
        /// </summary>
        /// <param name="screenName">The Twitter screen name of the user to receive notifications for.</param>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        void EnableNotifications(string screenName);

        /// <summary>
        /// Disable mobile device notifications from Twitter for the specified user.
        /// </summary>
        /// <param name="userId">The Twitter ID of the user to stop notifications for.</param>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        void DisableNotifications(long userId);

        /// <summary>
        /// Disable mobile device notifications from Twitter for the specified user.
        /// </summary>
        /// <param name="screenName">The Twitter screen name of the user to stop notifications for.</param>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        void DisableNotifications(string screenName);

        /// <summary>
        /// Returns an array of numeric IDs for every user who has a pending request to follow the authenticating user.
        /// </summary>
        /// <returns>A cursored list of user ids.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetIncomingFriendships();

        /// <summary>
        /// Returns an array of numeric IDs for every user who has a pending request to follow the authenticating user.
        /// </summary>
        /// <param name="cursor">The cursor of the page to retrieve.</param>
        /// <returns>A cursored list of user ids.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetIncomingFriendships(long cursor);

        /// <summary>
        /// Returns an array of numeric IDs for every protected user for whom the authenticating user has a pending follow request.
        /// </summary>
        /// <returns>A cursored list of user ids.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetOutgoingFriendships();

        /// <summary>
        /// Returns an array of numeric IDs for every protected user for whom the authenticating user has a pending follow request.
        /// </summary>
        /// <param name="cursor">The cursor of the page to retrieve.</param>
        /// <returns>A cursored list of user ids.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetOutgoingFriendships(long cursor);
    }
}
