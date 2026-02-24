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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/WorkEntryDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json;   — Spring.Json has no .NET 10 NuGet equivalent;
//              IJsonDeserializer, JsonValue, and JsonMapper are stub types defined in
//              AbstractFacebookOperations.cs (Spring.Social.Facebook.Api.Impl namespace)
//              and resolve automatically via C# parent namespace lookup.
//   - KEPT: All using System.* imports, Apache License header, class/method bodies,
//           XML doc comments, and access modifier (internal/default) unchanged.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

using System;
using System.Globalization;
using System.Collections.Generic;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for WorkEntry. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class WorkEntryDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			WorkEntry entry = null;
			if ( json != null && !json.IsNull )
			{
				entry = new WorkEntry();
				entry.StartDate = json.ContainsName("start_date") ? json.GetValue<string>("start_date") : String.Empty;
				entry.EndDate   = json.ContainsName("end_date"  ) ? json.GetValue<string>("end_date"  ) : String.Empty;
				
				entry.Employer  = mapper.Deserialize<Reference>(json.GetValue("employer"));
			}
			return entry;
		}
	}
}
