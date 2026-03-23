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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/StoryTagMapDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent
//   - KEPT: All other content preserved exactly (Apache license, namespace, class, method body)
// IJsonDeserializer, JsonValue, and JsonMapper resolve via C# parent namespace resolution
// from Spring.Social.Facebook.Api.Impl.Json → Spring.Social.Facebook.Api.Impl
// (stub definitions in AbstractFacebookOperations.cs).
// StoryTag resolves via grandparent namespace Spring.Social.Facebook.Api (StoryTag.cs).
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

using System.Collections.Generic;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for list of story tags.
	/// </summary>
	/// <author>Bruno Baia</author>
	/// <author>SplendidCRM (.NET)</author>
	class StoryTagMapDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			Dictionary<int, List<StoryTag>> tags = null;
			if ( json != null && !json.IsNull )
			{
				tags = new Dictionary<int, List<StoryTag>>();
				int i = 0;
				foreach ( JsonValue itemValue in json.GetValues() )
				{
					List<StoryTag> tagList = mapper.Deserialize<List<StoryTag>>(itemValue);
					tags.Add(i, tagList);
					i++;
				}
			}
			return tags;
		}
	}
}
