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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/GroupTemplate.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Http;        — Spring.Http has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Rest.Client; — Spring.Rest.Client has no .NET 10 NuGet equivalent
//   - ADDED:   using Spring.Social.Facebook.Api; to resolve Group, GroupMembership,
//              GroupMemberReference, FacebookProfile, ImageType, and IGroupOperations
//              from the parent namespace (previously resolved via Spring.* DLLs).
//   - KEPT:    All class structure, FULL_PROFILE_FIELDS constant, constructor signature,
//              all method implementations, #region blocks, and XML doc comments exactly.
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
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class GroupTemplate : AbstractFacebookOperations, IGroupOperations
	{
		private string[] FULL_PROFILE_FIELDS = {"id", "username", "name", "first_name", "last_name", "gender", "locale", "education", "work", "email", "third_party_id", "link", "timezone", "updated_time", "verified", "about", "bio", "birthday", "location", "hometown", "interested_in", "religion", "political", "quotes", "relationship_status", "significant_other", "website"};

		public GroupTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized)
			: base(applicationNamespace, restTemplate, isAuthorized)
		{
		}

		#region IGroupOperations Members
		public Group GetGroup(string groupId)
		{
			return this.FetchObject<Group>(groupId);
		}
	
		public byte[] GetGroupImage(string groupId)
		{
			return GetGroupImage(groupId, ImageType.NORMAL);
		}
	
		public byte[] GetGroupImage(string groupId, ImageType imageType)
		{
			return this.FetchImage(groupId, "picture", imageType);
		}
	
		public List<GroupMemberReference> GetMembers(string groupId)
		{
			requireAuthorization();
			return this.FetchConnections<GroupMemberReference>(groupId, "members");
		}

		public List<FacebookProfile> GetMemberProfiles(string groupId)
		{
			requireAuthorization();
			return this.FetchConnections<FacebookProfile>(groupId, "members", FULL_PROFILE_FIELDS);
		}
	
		public List<GroupMembership> GetMemberships()
		{
			return GetMemberships("me");
		}
	
		public List<GroupMembership> GetMemberships(string userId)
		{
			requireAuthorization();
			return this.FetchConnections<GroupMembership>(userId, "groups");
		}

		public List<Group> Search(string query)
		{
			return Search(query, 0, 25);
		}
	
		public List<Group> Search(string query, int offset, int limit)
		{
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("q"     , query            );
			parameters.Add("type"  , "group"          );
			parameters.Add("fields", "owner,name,description,privacy,icon,updated_time,email,version");
			parameters.Add("offset", offset.ToString());
			parameters.Add("limit" , limit .ToString());
			return this.FetchConnections<Group>("search", "", parameters);
		}	
		#endregion
	}
}
