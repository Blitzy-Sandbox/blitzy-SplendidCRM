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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/FeedTemplate.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json;       — Spring.Json has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Http;       — Spring.Http has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Rest.Client; — Spring.Rest DLL has no .NET 10 NuGet equivalent
//   - KEPT: all using System.* imports unchanged per AAP 0.8.1 Minimal Change Clause
//   - CHANGED: class access modifier from implicit internal to public (required for class library
//             export where base AbstractFacebookOperations is public — per AAP §0.8.1 minimal change)
//   - CHANGED: this.restTemplate.Delete(id) → this.Delete(id) in DeletePost() because the
//             RestTemplate stub (defined in AbstractFacebookOperations.cs) provides GetForObject<T>()
//             and PostForObject<T>() only; the inherited AbstractFacebookOperations.Delete(string)
//             method performs the equivalent operation via PostForObject using method=delete semantics.
//   - KEPT: all constructor and method implementations exactly as in source
//   - JsonValue type resolves from the stub defined in AbstractFacebookOperations.cs
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP 0.7.4 (Spring.Social Dependency Removal) and AAP 0.8.1 (Minimal Change Clause).

using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Spring.Social.Facebook.Api.Impl
{
	/// <summary>
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	public class FeedTemplate : AbstractFacebookOperations, IFeedOperations
	{
		public FeedTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized)
			: base(applicationNamespace, restTemplate, isAuthorized)
		{
		}

		#region IFeedOperations Members
		public List<Post> GetFeed()
		{
			return GetFeed("me", 0, 25);
		}

		public List<Post> GetFeed(int offset, int limit)
		{
			return GetFeed("me", offset, limit);
		}

		public List<Post> GetFeed(string ownerId)
		{
			return GetFeed(ownerId, 0, 25);
		}
		
		public List<Post> GetFeed(string ownerId, int offset, int limit)
		{
			requireAuthorization();
			return FetchConnectionList<List<Post>>(ownerId + "/feed", offset, limit);
			//return DeserializeList<Post>(responseNode, String.Empty);
		}

		public List<Post> GetHomeFeed()
		{
			return GetHomeFeed(0, 25);
		}
	
		public List<Post> GetHomeFeed(int offset, int limit)
		{
			requireAuthorization();
			return FetchConnectionList<List<Post>>("me/home", offset, limit);
			//return DeserializeList<Post>(responseNode, String.Empty);
		}

		public List<StatusPost> GetStatuses()
		{
			return GetStatuses("me", 0, 25);
		}
	
		public List<StatusPost> GetStatuses(int offset, int limit)
		{
			return GetStatuses("me", offset, limit);
		}

		public List<StatusPost> GetStatuses(string userId)
		{
			return GetStatuses(userId, 0, 25);
		}
	
		public List<StatusPost> GetStatuses(string userId, int offset, int limit)
		{
			requireAuthorization();
			return FetchConnectionList<List<StatusPost>>(userId + "/statuses", offset, limit);
			//return DeserializeList<StatusPost>(responseNode, "status");
		}

		public List<LinkPost> GetLinks()
		{
			return GetLinks("me", 0, 25);
		}

		public List<LinkPost> GetLinks(int offset, int limit)
		{
			return GetLinks("me", offset, limit);
		}

		public List<LinkPost> GetLinks(string ownerId)
		{
			return GetLinks(ownerId, 0, 25);
		}
	
		public List<LinkPost> GetLinks(string ownerId, int offset, int limit)
		{
			requireAuthorization();
			return FetchConnectionList<List<LinkPost>>(ownerId + "/links", offset, limit);
			//return DeserializeList<LinkPost>(responseNode, "link");
		}

		public List<NotePost> GetNotes()
		{
			return GetNotes("me", 0, 25);
		}

		public List<NotePost> GetNotes(int offset, int limit)
		{
			return GetNotes("me", offset, limit);
		}

		public List<NotePost> GetNotes(string ownerId)
		{
			return GetNotes(ownerId, 0, 25);
		}
	
		public List<NotePost> GetNotes(string ownerId, int offset, int limit)
		{
			requireAuthorization();
			return FetchConnectionList<List<NotePost>>(ownerId + "/notes", offset, limit);
			//return DeserializeList<NotePost>(responseNode, "note");
		}
	
		public List<Post> GetPosts()
		{
			return GetPosts("me", 0, 25);
		}

		public List<Post> GetPosts(int offset, int limit)
		{
			return GetPosts("me", offset, limit);
		}

		public List<Post> GetPosts(string ownerId)
		{
			return GetPosts(ownerId, 0, 25);
		}
	
		public List<Post> GetPosts(string ownerId, int offset, int limit)
		{
			requireAuthorization();
			return FetchConnectionList<List<Post>>(ownerId + "/posts", offset, limit);
			//return DeserializeList<Post>(responseNode, null);
		}
	
		public Post GetPost(string entryId)
		{
			requireAuthorization();
			return restTemplate.GetForObject<Post>(entryId);
			//return DeserializePost<Post>(null, responseNode);
		}

		public string UpdateStatus(string message)
		{
			return Post("me", message);
		}

		public string PostLink(string message, FacebookLink link)
		{
			return PostLink("me", message, link);
		}
	
		public string PostLink(string ownerId, string message, FacebookLink link)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("link"       , link.Link       );
			parameters.Add("name"       , link.Name       );
			parameters.Add("caption"    , link.Caption    );
			parameters.Add("description", link.Description);
			parameters.Add("message"    , message         );
			return this.Publish(ownerId, "feed", parameters);
		}
	
		public string Post(string ownerId, string message)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("message", message);
			return this.Publish(ownerId, "feed", parameters);
		}

		public void DeletePost(string id)
		{
			requireAuthorization();
			// MIGRATION NOTE: Original source called this.restTemplate.Delete(id) but the
			// RestTemplate stub (AbstractFacebookOperations.cs) provides only GetForObject<T>()
			// and PostForObject<T>(). The inherited Delete(string) method on AbstractFacebookOperations
			// provides equivalent semantics via PostForObject with method=delete parameter.
			this.Delete(id);
		}

		public List<Post> SearchPublicFeed(string query)
		{
			return SearchPublicFeed(query, 0, 25);
		}
	
		public List<Post> SearchPublicFeed(string query, int offset, int limit)
		{
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("q"     , query            );
			parameters.Add("type"  , "post"           );
			parameters.Add("offset", offset.ToString());
			parameters.Add("limit" , limit .ToString());
			return restTemplate.GetForObject<List<Post>>(this.BuildUrl("feed", parameters));
			//return DeserializeList<Post>(responseNode, null);
		}
	
		public List<Post> SearchHomeFeed(string query)
		{
			return SearchHomeFeed(query, 0, 25);
		}
	
		public List<Post> SearchHomeFeed(string query, int offset, int limit)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("q"     , query            );
			parameters.Add("type"  , "post"           );
			parameters.Add("offset", offset.ToString());
			parameters.Add("limit" , limit .ToString());
			return restTemplate.GetForObject<List<Post>>(this.BuildUrl("me/home", parameters));
			//return DeserializeList<Post>(responseNode, null);
		}
	
		public List<Post> SearchUserFeed(string query)
		{
			return SearchUserFeed("me", query, 0, 25);
		}

		public List<Post> SearchUserFeed(string query, int offset, int limit)
		{
			return SearchUserFeed("me", query, offset, limit);
		}

		public List<Post> SearchUserFeed(string userId, string query)
		{
			return SearchUserFeed(userId, query, 0, 25);
		}
	
		public List<Post> SearchUserFeed(string userId, string query, int offset, int limit)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("q"     , query            );
			parameters.Add("offset", offset.ToString());
			parameters.Add("limit" , limit .ToString());
			return restTemplate.GetForObject<List<Post>>(this.BuildUrl(userId + "/feed", parameters));
			//return DeserializeList<Post>(responseNode, null);
		}
		#endregion
	}
}
