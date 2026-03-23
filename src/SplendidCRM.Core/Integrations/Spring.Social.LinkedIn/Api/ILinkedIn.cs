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

// .NET 10 Migration: Removed discontinued Spring.Rest.Client using directive.
// Spring.Rest.dll and Spring.Social.Core.dll have no .NET Core / .NET 10 equivalent
// per AAP Section 0.7.4 — Spring.Social Dependency Removal.
// Stub interfaces IApiBinding and IRestOperations are defined inline in this namespace
// to satisfy compile-time type references while preserving the public API surface
// for the Enterprise Edition upgrade path.
// This is a dormant Enterprise Edition integration stub — compile only, not activated.

using System;

namespace Spring.Social.LinkedIn.Api
{
    // Stub interface replacing Spring.Social.Api.IApiBinding — Spring.Social is discontinued.
    // Preserved for compilation compatibility per .NET 10 migration (AAP Section 0.7.4).
    public interface IApiBinding
    {
        /// <summary>
        /// Returns true if this API binding has been authorized.
        /// </summary>
        bool IsAuthorized { get; }
    }

    // Stub interface replacing Spring.Rest.Client.IRestOperations — Spring.Rest is discontinued.
    // Preserved for compilation compatibility per .NET 10 migration (AAP Section 0.7.4).
    public interface IRestOperations
    {
    }

    /// <summary>
    /// Interface specifying a basic set of operations for interacting with LinkedIn.
    /// </summary>
    /// <author>Craig Walls</author>
    /// <author>Robert Drysdale</author>
    /// <author>Bruno Baia (.NET)</author>
    public interface ILinkedIn : IApiBinding
    {
        /// <summary>
        /// Gets the portion of the LinkedIn API sending messages and connection requests.
        /// </summary>
        ICommunicationOperations CommunicationOperations { get; }

        /// <summary>
        /// Gets the portion of the LinkedIn API retrieving connections.
        /// </summary>
        IConnectionOperations ConnectionOperations { get; }

        /// <summary>
        /// Gets the portion of the LinkedIn API retrieving and performing operations on profiles.
        /// </summary>
        IProfileOperations ProfileOperations { get; }

        /// <summary>
        /// Gets the underlying <see cref="IRestOperations"/> object allowing for consumption of LinkedIn endpoints 
        /// that may not be otherwise covered by the API binding. 
        /// </summary>
        /// <remarks>
        /// The <see cref="IRestOperations"/> object returned is configured to include an OAuth "Authorization" header on all requests.
        /// </remarks>
        IRestOperations RestOperations { get; }
    }
}
