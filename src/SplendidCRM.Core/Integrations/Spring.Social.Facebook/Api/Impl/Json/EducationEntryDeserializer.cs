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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/EducationEntryDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent.
//     Types IJsonDeserializer, JsonValue, and JsonMapper are resolved via C# parent
//     namespace resolution from Spring.Social.Facebook.Api.Impl.Json to parent namespace
//     Spring.Social.Facebook.Api.Impl (stub definitions in AbstractFacebookOperations.cs).
//     Types EducationEntry and Reference resolve from grandparent namespace
//     Spring.Social.Facebook.Api via the same C# namespace resolution mechanism.
//   - KEPT: Apache License 2.0 header, all using System.* directives, namespace,
//     class declaration, and Deserialize method body exactly as-is.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and §0.8.1 (Minimal Change Clause).

using System;
using System.Globalization;
using System.Collections.Generic;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for EducationEntry. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class EducationEntryDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			EducationEntry entry = null;
			if ( json != null && !json.IsNull )
			{
				entry = new EducationEntry();
				entry.Type          = json.ContainsName("type"  ) ? json.GetValue<string>("type") : String.Empty;

				entry.School        = mapper.Deserialize<Reference      >(json.GetValue("school"       ));
				entry.Year          = mapper.Deserialize<Reference      >(json.GetValue("year"         ));
				entry.Concentration = mapper.Deserialize<List<Reference>>(json.GetValue("concentration"));
			}
			return entry;
		}
	}
}
