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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/ListDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent
//   - KEPT: using System.Collections.Generic; — standard library, unchanged
//   - IJsonDeserializer, JsonValue, and JsonMapper now resolve from the parent namespace
//     Spring.Social.Facebook.Api.Impl (stubs defined in AbstractFacebookOperations.cs)
//     via C# parent namespace lookup — no additional using directive required.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and §0.8.1 (Minimal Change Clause).

using System.Collections.Generic;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer a generic list.
	/// </summary>
	/// <author>Bruno Baia</author>
	/// <author>SplendidCRM (.NET)</author>
	class ListDeserializer<T> : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			IList<T> entries = null;
			if ( json != null && !json.IsNull )
			{
				JsonValue dataNode = json.GetValue("data");
				if ( dataNode != null )
				{
					entries = new List<T>();
					foreach ( JsonValue itemValue in dataNode.GetValues() )
					{
						entries.Add(mapper.Deserialize<T>(itemValue));
					}
				}
			}
			return entries;
		}
	}
}
