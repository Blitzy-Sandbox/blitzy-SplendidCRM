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
    /// Interface defining the operations for blocking and unblocking users
    /// </summary>
    /// <author>Craig Walls</author>
    /// <author>Bruno Baia (.NET)</author>
    public interface IBlockOperations
    {
        /// <summary>
        /// Blocks a user. If a friendship exists with the user, it will be destroyed.
        /// </summary>
        /// <param name="userId">The ID of the user to block.</param>
        /// <returns>
        /// The <see cref="TwitterProfile"/> of the blocked user.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile Block(long userId);

        /// <summary>
        /// Blocks a user. If a friendship exists with the user, it will be destroyed.
        /// </summary>
        /// <param name="screenName">The screen name of the user to block.</param>
        /// <returns>
        /// The <see cref="TwitterProfile"/> of the blocked user.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile Block(string screenName);

        /// <summary>
        /// Unblocks a user.
        /// </summary>
        /// <param name="userId">The ID of the user to unblock.</param>
        /// <returns>
        /// The <see cref="TwitterProfile"/> of the unblocked user.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile Unblock(long userId);

        /// <summary>
        /// Unblocks a user.
        /// </summary>
        /// <param name="screenName">The screen name of the user to unblock.</param>
        /// <returns>
        /// The <see cref="TwitterProfile"/> of the unblocked user.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        TwitterProfile Unblock(string screenName);

        /// <summary>
        /// Retrieves the first cursored list of users that the authenticating user has blocked.
        /// </summary>
        /// <returns>
        /// A cursored list of <see cref="TwitterProfile"/>s for the users that are blocked.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetBlockedUsers();

        /// <summary>
        /// Retrieves a cursored list of users that the authenticating user has blocked.
        /// </summary>
        /// <param name="cursor">
        /// The cursor to retrieve results from. -1 will retrieve the first cursored page of results.
        /// </param>
        /// <returns>
        /// A cursored list of <see cref="TwitterProfile"/>s for the users that are blocked.
        /// </returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<TwitterProfile> GetBlockedUsers(long cursor);

        /// <summary>
        /// Retrieves the first cursored list of user IDs for the users that the authenticating user has blocked.
        /// </summary>
        /// <returns>A cursored list of user IDs for the users that are blocked.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetBlockedUserIds();

        /// <summary>
        /// Retrieves a cursored list of user IDs for the users that the authenticating user has blocked.
        /// </summary>
        /// <param name="cursor">
        /// The cursor to retrieve results from. -1 will retrieve the first cursored page of results.
        /// </param>
        /// <returns>A cursored list of user IDs for the users that are blocked.</returns>
        /// <exception cref="TwitterApiException">If there is an error while communicating with Twitter.</exception>
        CursoredList<long> GetBlockedUserIds(long cursor);
    }
}
