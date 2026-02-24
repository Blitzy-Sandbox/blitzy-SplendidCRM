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

// .NET 10 Migration: BlockTemplate migrated from .NET Framework 4.8 to .NET 10 ASP.NET Core.
//
// Changes from original source (SplendidCRM/_code/Spring.Social.Twitter/Api/Impl/BlockTemplate.cs):
//
// 1. Conditional compilation blocks removed:
//    - #if NET_4_0 || SILVERLIGHT_5 / #else / #endif blocks eliminated.
//    - On .NET 10, neither NET_4_0 nor SILVERLIGHT_5 is defined, so the #else branch would have been
//      active. However, that branch depends on RestTemplate.PostForObject/GetForObject and
//      RestTemplate.PostForObjectAsync/GetForObjectAsync callback overloads which are not available
//      in the Spring stub (Spring.Rest.dll is discontinued with no .NET Core / .NET 10 equivalent).
//      Per AAP §0.7.4 and §0.8.1 minimal change clause, the conditional blocks are removed.
//    - #if SILVERLIGHT / #else / #endif (for NameValueCollection namespace) resolved to the #else
//      branch unconditionally — System.Collections.Specialized is used directly (but not needed
//      since no RestTemplate calls are made in the sync-only stub).
//
// 2. Task-based async methods (previously under #if NET_4_0 || SILVERLIGHT_5) removed:
//    - The migrated IBlockOperations interface does not declare Task-based async methods.
//    - Per AAP §0.8.1 minimal change clause: only what the interface requires is implemented.
//
// 3. Callback-based async methods removed (previously in the #else branch):
//    - Methods using Action<RestOperationCompletedEventArgs<T>> callbacks and returning
//      RestOperationCanceler have been removed.
//    - The migrated IBlockOperations interface no longer declares callback-based methods.
//    - Spring.Rest.Client and Spring.Http are discontinued libraries with no .NET Core / .NET 10
//      equivalent. Per AAP §0.7.4 and §0.8.1 minimal change clause.
//
// 4. Spring.Rest.Client using directive and RestTemplate field removed:
//    - BlockTemplate implements only the synchronous IBlockOperations contract.
//    - No RestTemplate dependency is needed for the dormant sync-only stub implementation.
//    - All method bodies stub via null / new CursoredList<T>() returns.
//
// 5. All synchronous method signatures are preserved per AAP §0.8.1 minimal change clause.
//
// This is a dormant Enterprise Edition integration stub — compile only, not expected to execute.
// Per AAP §0.3.1 and §0.7.4.

#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    /// <summary>
    /// Implementation of <see cref="IBlockOperations"/>, providing a binding to Twitter's
    /// block/unblock REST resources.
    /// Dormant Enterprise Edition integration stub — compile only, not expected to execute.
    /// </summary>
    /// <author>Craig Walls</author>
    /// <author>Bruno Baia (.NET)</author>
    class BlockTemplate : AbstractTwitterOperations, IBlockOperations
    {
        // =====================================================================
        // Block / Unblock
        // =====================================================================

        /// <summary>
        /// Blocks a user. If a friendship exists with the user, it will be destroyed.
        /// </summary>
        /// <param name="userId">The ID of the user to block.</param>
        /// <returns>The <see cref="TwitterProfile"/> of the blocked user.</returns>
        public TwitterProfile Block(long userId) { return null; }

        /// <summary>
        /// Blocks a user. If a friendship exists with the user, it will be destroyed.
        /// </summary>
        /// <param name="screenName">The screen name of the user to block.</param>
        /// <returns>The <see cref="TwitterProfile"/> of the blocked user.</returns>
        public TwitterProfile Block(string screenName) { return null; }

        /// <summary>
        /// Unblocks a user.
        /// </summary>
        /// <param name="userId">The ID of the user to unblock.</param>
        /// <returns>The <see cref="TwitterProfile"/> of the unblocked user.</returns>
        public TwitterProfile Unblock(long userId) { return null; }

        /// <summary>
        /// Unblocks a user.
        /// </summary>
        /// <param name="screenName">The screen name of the user to unblock.</param>
        /// <returns>The <see cref="TwitterProfile"/> of the unblocked user.</returns>
        public TwitterProfile Unblock(string screenName) { return null; }

        // =====================================================================
        // GetBlockedUsers
        // =====================================================================

        /// <summary>
        /// Retrieves the first cursored list of users that the authenticating user has blocked.
        /// </summary>
        /// <returns>
        /// A cursored list of <see cref="TwitterProfile"/>s for the users that are blocked.
        /// </returns>
        public CursoredList<TwitterProfile> GetBlockedUsers() { return new CursoredList<TwitterProfile>(); }

        /// <summary>
        /// Retrieves a cursored list of users that the authenticating user has blocked.
        /// </summary>
        /// <param name="cursor">
        /// The cursor to retrieve results from. -1 will retrieve the first cursored page of results.
        /// </param>
        /// <returns>
        /// A cursored list of <see cref="TwitterProfile"/>s for the users that are blocked.
        /// </returns>
        public CursoredList<TwitterProfile> GetBlockedUsers(long cursor) { return new CursoredList<TwitterProfile>(); }

        // =====================================================================
        // GetBlockedUserIds
        // =====================================================================

        /// <summary>
        /// Retrieves the first cursored list of user IDs for the users that the authenticating user has blocked.
        /// </summary>
        /// <returns>A cursored list of user IDs for the users that are blocked.</returns>
        public CursoredList<long> GetBlockedUserIds() { return new CursoredList<long>(); }

        /// <summary>
        /// Retrieves a cursored list of user IDs for the users that the authenticating user has blocked.
        /// </summary>
        /// <param name="cursor">
        /// The cursor to retrieve results from. -1 will retrieve the first cursored page of results.
        /// </param>
        /// <returns>A cursored list of user IDs for the users that are blocked.</returns>
        public CursoredList<long> GetBlockedUserIds(long cursor) { return new CursoredList<long>(); }
    }
}
