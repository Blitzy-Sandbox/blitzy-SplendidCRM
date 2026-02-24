#region License

/*
 * Copyright 2011-2012 the original author or authors.
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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/FriendTemplate.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json;        — Spring.Json has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Http;        — Spring.Http has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Rest.Client; — Spring.Rest DLL has no .NET 10 NuGet equivalent
//   - ADDED:   using Spring.Social.Facebook.Api; — resolves Reference, FacebookProfile,
//              FamilyMember types from the parent namespace (previously resolved implicitly
//              via Spring.* assemblies in the web application project context).
//   - CHANGED: RemoveFromFriendList() — original restTemplate.Delete(path) replaced with
//              this.Delete(path) using AbstractFacebookOperations.Delete(string objectId).
//              The RestTemplate stub does not expose a Delete(string) method because
//              IRestOperations is a minimal empty-interface stub per AAP §0.7.4. The base
//              class Delete(string objectId) issues an equivalent deletion request against
//              the same compound path; semantics preserved for this dormant stub.
//   - KEPT:    Apache License 2.0 header, namespace Spring.Social.Facebook.Api.Impl,
//              class declaration, FULL_PROFILE_FIELDS constant, #region IFriendOperations
//              Members block, constructor, and all 24 method bodies exactly as in source
//              per AAP §0.8.1 Minimal Change Clause.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and §0.8.1 (Minimal Change Clause).

using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;

using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
	/// <summary>
	/// Implementation of <see cref="IFriendOperations"/> that performs friend-related operations
	/// against the Facebook Graph API. Controls friend lists, memberships, family relationships,
	/// mutual friends, subscribers, and profile retrieval with explicit field projections.
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	public class FriendTemplate : AbstractFacebookOperations, IFriendOperations
	{
		/// <summary>
		/// Comma-separated list of fields used to retrieve full Facebook profile data for friends.
		/// Preserved exactly from source per AAP §0.8.1 Minimal Change Clause.
		/// </summary>
		private string FULL_PROFILE_FIELDS = "id,username,name,first_name,last_name,gender,locale,education,work,email,third_party_id,link,timezone,updated_time,verified,about,bio,birthday,location,hometown,interested_in,religion,political,quotes,relationship_status,significant_other,website";

		/// <summary>
		/// Initializes a new instance of <see cref="FriendTemplate"/> with the given application
		/// namespace, REST template, and authorization state.
		/// </summary>
		/// <param name="applicationNamespace">The Facebook application namespace.</param>
		/// <param name="restTemplate">The REST template stub used for Graph API calls.</param>
		/// <param name="isAuthorized">Whether an OAuth access token has been provided.</param>
		public FriendTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized)
			: base(applicationNamespace, restTemplate, isAuthorized)
		{
		}

		#region IFriendOperations Members

		/// <inheritdoc/>
		public List<Reference> GetFriendLists()
		{
			return GetFriendLists("me");
		}

		/// <inheritdoc/>
		public List<Reference> GetFriendLists(string userId)
		{
			requireAuthorization();
			return this.FetchConnections<Reference>(userId, "friendlists");
		}

		/// <inheritdoc/>
		public Reference GetFriendList(string friendListId)
		{
			requireAuthorization();
			return this.FetchObject<Reference>(friendListId);
		}

		/// <inheritdoc/>
		public List<Reference> GetFriendListMembers(string friendListId)
		{
			requireAuthorization();
			return this.FetchConnections<Reference>(friendListId, "members");
		}

		/// <inheritdoc/>
		public string CreateFriendList(string name)
		{
			return CreateFriendList("me", name);
		}

		/// <inheritdoc/>
		public string CreateFriendList(string userId, string name)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("name", name);
			return this.Publish(userId, "friendlists", parameters);
		}

		/// <inheritdoc/>
		public void DeleteFriendList(string friendListId)
		{
			requireAuthorization();
			this.Delete(friendListId);
		}

		/// <inheritdoc/>
		public void AddToFriendList(string friendListId, string friendId)
		{
			requireAuthorization();
			this.Post(friendListId, "members/" + friendId, new NameValueCollection());
		}

		/// <inheritdoc/>
		public void RemoveFromFriendList(string friendListId, string friendId)
		{
			requireAuthorization();
			// Migration note: Original source called restTemplate.Delete(friendListId + "/members/" + friendId).
			// The RestTemplate stub does not expose a Delete(string) method because IRestOperations is a
			// minimal empty-interface stub per AAP §0.7.4 (Spring.Social Dependency Removal). Replaced with
			// AbstractFacebookOperations.Delete(string objectId) which issues an equivalent deletion request
			// against the same compound path using POST with method=delete. Semantics preserved for this
			// dormant Enterprise Edition stub. Per AAP §0.8.1: minimal migration-required change only.
			this.Delete(friendListId + "/members/" + friendId);
		}

		/// <inheritdoc/>
		public List<Reference> GetFriends()
		{
			return GetFriends("me");
		}

		/// <inheritdoc/>
		public List<string> GetFriendIds()
		{
			return GetFriendIds("me");
		}

		/// <inheritdoc/>
		public List<FacebookProfile> GetFriendProfiles()
		{
			return GetFriendProfiles("me", 0, 100);
		}

		/// <inheritdoc/>
		public List<FacebookProfile> GetFriendProfiles(int offset, int limit)
		{
			return GetFriendProfiles("me", offset, limit);
		}

		/// <inheritdoc/>
		public List<Reference> GetFriends(string userId)
		{
			requireAuthorization();
			return this.FetchConnections<Reference>(userId, "friends");
		}

		/// <inheritdoc/>
		public List<string> GetFriendIds(string userId)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("fields", "id");
			JsonValue response = restTemplate.GetForObject<JsonValue>(this.BuildUrl(userId + "/friends", parameters));

			List<string> idList = new List<string>();
			if ( response != null && !response.IsNull )
			{
				JsonValue entryList = response.GetValue("data");
				if ( entryList != null && !entryList.IsNull )
				{
					foreach ( JsonValue entry in entryList.GetValues() )
					{
						idList.Add(entry.GetValue<string>("id"));
					}
				}
			}
			return idList;
		}

		/// <inheritdoc/>
		public List<FacebookProfile> GetFriendProfiles(string userId)
		{
			return GetFriendProfiles(userId, 0, 100);
		}

		/// <inheritdoc/>
		public List<FacebookProfile> GetFriendProfiles(string userId, int offset, int limit)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("offset", offset.ToString()  );
			parameters.Add("limit" , limit .ToString()  );
			parameters.Add("fields", FULL_PROFILE_FIELDS);
			return this.FetchConnections<FacebookProfile>(userId, "friends", parameters);
		}

		/// <inheritdoc/>
		public List<FamilyMember> GetFamily()
		{
			requireAuthorization();
			return this.FetchConnections<FamilyMember>("me", "family");
		}

		/// <inheritdoc/>
		public List<FamilyMember> GetFamily(string userId)
		{
			requireAuthorization();
			return this.FetchConnections<FamilyMember>(userId, "family");
		}

		/// <inheritdoc/>
		public List<Reference> GetMutualFriends(string userId)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("user", userId);
			return this.FetchConnections<Reference>("me", "mutualfriends", parameters);
		}

		/// <inheritdoc/>
		public List<Reference> GetSubscribedTo()
		{
			return GetSubscribedTo("me");
		}

		/// <inheritdoc/>
		public List<Reference> GetSubscribedTo(string userId)
		{
			requireAuthorization();
			return this.FetchConnections<Reference>(userId, "subscribedTo");
		}

		/// <inheritdoc/>
		public List<Reference> GetSubscribers()
		{
			return GetSubscribers("me");
		}

		/// <inheritdoc/>
		public List<Reference> GetSubscribers(string userId)
		{
			requireAuthorization();
			return this.FetchConnections<Reference>(userId, "subscribers");
		}

		#endregion
	}
}
