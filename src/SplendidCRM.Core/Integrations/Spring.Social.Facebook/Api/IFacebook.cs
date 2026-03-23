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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/IFacebook.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Rest.Client; — Spring.Rest DLL has no .NET 10 equivalent
//   - ADDED: Minimal stub interfaces IApiBinding and IRestOperations replacing Spring.Rest.Client types
//     These stubs are defined locally within this namespace to satisfy compilation requirements.
//   - KEPT: all property declarations, XML documentation, and namespace preserved exactly
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP 0.7.4 (Spring.Social Dependency Removal) and AAP 0.8.1 (Minimal Change Clause).

using System;

namespace Spring.Social.Facebook.Api
{
	// Stub interfaces replacing Spring.Rest.Client types removed during .NET 10 migration.
	// Spring.Rest.dll has no .NET 10 equivalent NuGet package; these minimal stubs satisfy
	// compilation requirements for this dormant Enterprise Edition integration stub.
	// Do not activate or extend without proper OAuth2 / HTTP client implementation.

	/// <summary>
	/// Stub interface replacing Spring.Social.Core IApiBinding for .NET 10 compatibility.
	/// Original: Spring.Social.Core.IApiBinding from Spring.Social.Core.dll (v1.0, discontinued).
	/// </summary>
	public interface IApiBinding
	{
		/// <summary>
		/// Returns true if this API binding has been authorized on behalf of a specific user.
		/// </summary>
		bool IsAuthorized { get; }
	}

	/// <summary>
	/// Stub interface replacing Spring.Rest.Client IRestOperations for .NET 10 compatibility.
	/// Original: Spring.Rest.Client.IRestOperations from Spring.Rest.dll (v1.1, discontinued).
	/// Consumers should use System.Net.Http.HttpClient directly for REST operations.
	/// </summary>
	public interface IRestOperations
	{
	}

	/// <summary>
	/// Interface specifying a basic set of operations for interacting with Facebook.
	/// </summary>
	/// <author>Keith Donald</author>
	/// <author>SplendidCRM (.NET)</author>
	public interface IFacebook : IApiBinding
	{
		/// <summary>
		/// Returns the portion of the Facebook API containing the user operations.
		/// </summary>
		IUserOperations UserOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the places/check-in operations.
		/// </summary>
		IPlacesOperations PlacesOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the like operations.
		/// </summary>
		ILikeOperations LikeOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the friend operations.
		/// </summary>
		IFriendOperations FriendOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the feed operations.
		/// </summary>
		IFeedOperations FeedOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the group operations.
		/// </summary>
		IGroupOperations GroupOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the comment operations.
		/// </summary>
		ICommentOperations CommentOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the event operations.
		/// </summary>
		IEventOperations EventOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the media (album/photo/video) operations.
		/// </summary>
		IMediaOperations MediaOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the page operations.
		/// </summary>
		IPageOperations PageOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the FQL query operations.
		/// </summary>
		IFqlOperations FqlOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the question operations.
		/// </summary>
		IQuestionOperations QuestionOperations { get; }

		/// <summary>
		/// Returns the portion of the Facebook API containing the Open Graph operations.
		/// </summary>
		IOpenGraphOperations OpenGraphOperations { get; }

		/// <summary>
		/// Gets the underlying <see cref="IRestOperations"/> object allowing for consumption of Facebook endpoints 
		/// that may not be otherwise covered by the API binding. 
		/// </summary>
		/// <remarks>
		/// The <see cref="IRestOperations"/> object returned is configured to include an OAuth "Authorization" header on all requests.
		/// </remarks>
		IRestOperations RestOperations { get; }
	}
}
