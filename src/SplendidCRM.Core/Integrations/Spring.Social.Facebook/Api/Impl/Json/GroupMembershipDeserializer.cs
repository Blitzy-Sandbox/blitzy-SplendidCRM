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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/GroupMembershipDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent
//   - KEPT: using System; and using System.Globalization; as-is
//   - All class/method signatures, field mappings, XML doc comments, and business logic preserved exactly.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and §0.8.1 (Minimal Change Clause).
// IJsonDeserializer, JsonValue, JsonMapper resolve via C# parent namespace resolution from
// Spring.Social.Facebook.Api.Impl (defined in AbstractFacebookOperations.cs).
// GroupMembership resolves via C# grandparent namespace resolution from Spring.Social.Facebook.Api.

using System;
using System.Globalization;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for GroupMembership. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class GroupMembershipDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			GroupMembership group = null;
			if ( json != null && !json.IsNull )
			{
				group = new GroupMembership();
				group.ID            = json.ContainsName("id"            ) ? json.GetValue<string>("id"            ) : String.Empty;
				group.Name          = json.ContainsName("name"          ) ? json.GetValue<string>("name"          ) : String.Empty;
				group.Version       = json.ContainsName("version"       ) ? json.GetValue<int   >("version"       ) : 0;
				group.BookmarkOrder = json.ContainsName("bookmark_order") ? json.GetValue<int   >("bookmark_order") : 0;
				group.Administrator = json.ContainsName("administrator" ) ? json.GetValue<bool  >("administrator" ) : false;
				group.Unread        = json.ContainsName("unread"        ) ? json.GetValue<int   >("unread"        ) : 0;
			}
			return group;
		}
	}
}
