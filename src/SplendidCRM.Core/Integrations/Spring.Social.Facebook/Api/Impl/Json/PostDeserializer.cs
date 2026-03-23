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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/PostDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json;  — Spring.Json has no .NET 10 NuGet equivalent;
//              IJsonDeserializer, JsonValue, and JsonMapper stubs are defined in
//              AbstractFacebookOperations.cs and resolve via C# parent namespace lookup.
//   - KEPT: All other using directives (System, System.Globalization, System.Collections.Generic)
//   - KEPT: Entire Deserialize method body, all field mappings, shares nested parsing,
//           TypeDeserializer private static method, Enum.Parse with try/catch fallback.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

#nullable disable

using System;
using System.Globalization;
using System.Collections.Generic;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Post. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class PostDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			Post post = null;
			if ( json != null && !json.IsNull )
			{
				post = new Post();
				post.ID          = json.ContainsName("id"          ) ? json.GetValue<string>("id"         ) : String.Empty;
				post.Message     = json.ContainsName("message"     ) ? json.GetValue<string>("message"    ) : String.Empty;
				post.Caption     = json.ContainsName("caption"     ) ? json.GetValue<string>("caption"    ) : String.Empty;
				post.Picture     = json.ContainsName("picture"     ) ? json.GetValue<string>("picture"    ) : String.Empty;
				post.Link        = json.ContainsName("link"        ) ? json.GetValue<string>("link"       ) : String.Empty;
				post.Name        = json.ContainsName("name"        ) ? json.GetValue<string>("name"       ) : String.Empty;
				post.Description = json.ContainsName("description" ) ? json.GetValue<string>("description") : String.Empty;
				post.Icon        = json.ContainsName("icon"        ) ? json.GetValue<string>("icon"       ) : String.Empty;
				post.Story       = json.ContainsName("story"       ) ? json.GetValue<string>("story"      ) : String.Empty;
				post.CreatedTime = json.ContainsName("created_time") ? JsonUtils.ToDateTime(json.GetValue<string>("created_time"), "yyyy-MM-ddTHH:mm:ss") : DateTime.MinValue;
				post.UpdatedTime = json.ContainsName("updated_time") ? JsonUtils.ToDateTime(json.GetValue<string>("updated_time"), "yyyy-MM-ddTHH:mm:ss") : DateTime.MinValue;
				
				// 04/15/2012 Paul.  Shares is not a simple integer.  It contains a count node. 
				if ( json.ContainsName("shares") )
				{
					JsonValue jsonShares = json.GetValue("shares");
					post.SharesCount = jsonShares.ContainsName("count") ? jsonShares.GetValue<int>("count") : 0;
				}
				
				post.Type        = TypeDeserializer(json.GetValue("type"));
				post.From        = mapper.Deserialize<Reference                      >(json.GetValue("from"       ));
				post.Application = mapper.Deserialize<Reference                      >(json.GetValue("application"));
				post.To          = mapper.Deserialize<List<Reference>                >(json.GetValue("to"         ));
				post.Likes       = mapper.Deserialize<List<Reference>                >(json.GetValue("likes"      ));
				post.Comments    = mapper.Deserialize<List<Comment  >                >(json.GetValue("comments"   ));
				post.StoryTags   = mapper.Deserialize<Dictionary<int, List<StoryTag>>>(json.GetValue("story_tags" ));
				
				post.LikeCount    = (post.Likes    != null ) ? post.Likes.Count    : 0;
				post.CommentCount = (post.Comments != null ) ? post.Comments.Count : 0;
			}
			return post;
		}

		private static Post.enumPostType TypeDeserializer(JsonValue json)
		{
			Post.enumPostType value = Post.enumPostType.POST;
			if ( json != null && !json.IsNull )
			{
				try
				{
					string code = json.GetValue<string>();
					code = code.ToUpper();
					value = (Post.enumPostType) Enum.Parse(typeof(Post.enumPostType), code);
				}
				catch
				{
				}
			}
			return value;
		}
	}
}
