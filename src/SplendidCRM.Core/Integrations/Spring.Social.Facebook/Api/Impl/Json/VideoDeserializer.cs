#nullable disable
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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/VideoDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent; stub types
//              IJsonDeserializer, JsonValue, and JsonMapper are defined in AbstractFacebookOperations.cs
//              within the Spring.Social.Facebook.Api.Impl namespace, accessible here via C# enclosing
//              namespace resolution from Spring.Social.Facebook.Api.Impl.Json.
//   - KEPT: using System; — provides String.Empty and DateTime.MinValue defaults
//   - KEPT: using System.Globalization; — preserved per AAP 0.8.1 Minimal Change Clause
//   - KEPT: using System.Collections.Generic; — provides List<T> for Tags and Comments deserialization
//   - KEPT: All class/method signatures and full Deserialize method body
//   - CHANGED: class → public class — required for class library visibility
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

using System;
using System.Globalization;
using System.Collections.Generic;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Video. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	public class VideoDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			Video video = null;
			if ( json != null && !json.IsNull )
			{
				video = new Video();
				video.ID          = json.ContainsName("id"          ) ? json.GetValue<string>("id"         ) : String.Empty;
				video.Name        = json.ContainsName("name"        ) ? json.GetValue<string>("name"       ) : String.Empty;
				video.Description = json.ContainsName("description" ) ? json.GetValue<string>("description") : String.Empty;
				video.Picture     = json.ContainsName("picture"     ) ? json.GetValue<string>("picture"    ) : String.Empty;
				video.EmbedHtml   = json.ContainsName("embed_html"  ) ? json.GetValue<string>("embed_html" ) : String.Empty;
				video.Icon        = json.ContainsName("icon"        ) ? json.GetValue<string>("icon"       ) : String.Empty;
				video.Source      = json.ContainsName("source"      ) ? json.GetValue<string>("source"     ) : String.Empty;
				video.CreatedTime = json.ContainsName("created_time") ? JsonUtils.ToDateTime(json.GetValue<string>("created_time"), "yyyy-MM-ddTHH:mm:ss") : DateTime.MinValue;
				video.UpdatedTime = json.ContainsName("updated_time") ? JsonUtils.ToDateTime(json.GetValue<string>("updated_time"), "yyyy-MM-ddTHH:mm:ss") : DateTime.MinValue;
				
				video.From        = mapper.Deserialize<Reference    >(json.GetValue("from"    ));
				video.Tags        = mapper.Deserialize<List<Tag    >>(json.GetValue("tags"    ));
				video.Comments    = mapper.Deserialize<List<Comment>>(json.GetValue("comments"));
			}
			return video;
		}
	}
}
