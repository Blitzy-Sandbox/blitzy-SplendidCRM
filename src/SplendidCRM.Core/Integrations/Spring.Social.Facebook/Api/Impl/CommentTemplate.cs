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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/CommentTemplate.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Http;        — Spring.Http has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Rest.Client; — Spring.Rest DLL has no .NET 10 NuGet equivalent
//   - The RestTemplate type in the constructor parameter resolves from the stub defined
//     in AbstractFacebookOperations.cs within the same Spring.Social.Facebook.Api.Impl namespace.
//   - Access modifier changed from implicit internal to public — consistent with base class
//     AbstractFacebookOperations migration (see AbstractFacebookOperations.cs migration note).
//   - All method bodies, #region blocks, and business logic preserved exactly as in source.
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
	public class CommentTemplate : AbstractFacebookOperations, ICommentOperations
	{
		public CommentTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized)
			: base(applicationNamespace, restTemplate, isAuthorized)
		{
		}

		#region ICommentOperations Members
		public List<Comment> GetComments(string objectId)
		{
			return GetComments(objectId, 0, 25);
		}

		public List<Comment> GetComments(string objectId, int offset, int limit)
		{
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("offset", offset.ToString());
			parameters.Add("limit" , limit .ToString());
			return this.FetchConnections<Comment>(objectId, "comments", parameters);
		}
	
		public Comment GetComment(string commentId)
		{
			return this.FetchObject<Comment>(commentId);
		}

		public string AddComment(string objectId, string message)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("message", message);
			return this.Publish(objectId, "comments", parameters);
		}

		public void DeleteComment(string objectId)
		{
			requireAuthorization();
			this.Delete(objectId);
		}

		public List<Reference> GetLikes(string objectId)
		{
			return this.FetchConnections<Reference>(objectId, "likes");
		}
		#endregion
	}
}
