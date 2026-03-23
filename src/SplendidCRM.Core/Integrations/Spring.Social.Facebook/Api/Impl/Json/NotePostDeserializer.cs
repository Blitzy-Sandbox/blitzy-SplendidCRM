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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/NotePostDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent
//   - KEPT: using System; and using System.Globalization; (per AAP §0.8.1 Minimal Change Clause)
//   - ALL other code preserved exactly: Apache License header, namespace, class, XML doc comments,
//     Deserialize method body, all field assignments, null guards, and JsonUtils/mapper calls
// IJsonDeserializer, JsonValue, and JsonMapper resolve via C# parent namespace resolution from
// Spring.Social.Facebook.Api.Impl.Json to Spring.Social.Facebook.Api.Impl (defined in
// AbstractFacebookOperations.cs as single-definition stubs replacing removed Spring.Json types).
// NotePost and Reference resolve via grandparent namespace resolution to Spring.Social.Facebook.Api.
// JsonUtils resolves directly within the same namespace Spring.Social.Facebook.Api.Impl.Json.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

using System;
using System.Globalization;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for NotePost. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class NotePostDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			NotePost post = null;
			if ( json != null && !json.IsNull )
			{
				post = new NotePost();
				post.ID          = json.ContainsName("id"          ) ? json.GetValue<string>("id"     ) : String.Empty;
				post.Subject     = json.ContainsName("subject"     ) ? json.GetValue<string>("subject") : String.Empty;
				post.CreatedTime = json.ContainsName("created_time") ? JsonUtils.ToDateTime(json.GetValue<string>("created_time"), "yyyy-MM-ddTHH:mm:ss") : DateTime.MinValue;
				post.UpdatedTime = json.ContainsName("updated_time") ? JsonUtils.ToDateTime(json.GetValue<string>("updated_time"), "yyyy-MM-ddTHH:mm:ss") : DateTime.MinValue;
				
				post.From        = mapper.Deserialize<Reference>(json.GetValue("from"));
			}
			return post;
		}
	}
}
