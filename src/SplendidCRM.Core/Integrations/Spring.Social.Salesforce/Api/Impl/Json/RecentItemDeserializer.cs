#nullable disable
#region License

/*
 * Copyright (C) 2012 SplendidCRM Software, Inc. All Rights Reserved. 
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

// .NET 10 Migration: Removed 'using Spring.Json;' — Spring.Json assembly is discontinued with no
// .NET Core / .NET 10 equivalent NuGet package. The IJsonDeserializer, JsonValue, and JsonMapper
// stub types are defined in the Spring.Social.Salesforce.Api.Impl namespace
// (AbstractSalesforceOperations.cs) and resolve here via C# parent-namespace lookup without an
// explicit using directive. RecentItem and Attributes resolve from the grandparent namespace
// Spring.Social.Salesforce.Api by the same mechanism. This is a DORMANT STUB — compiles on
// .NET 10 but is NOT executed at runtime (AAP section 0.7.4).

using System;
using System.Collections.Generic;

namespace Spring.Social.Salesforce.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Version. 
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class RecentItemDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			RecentItem item = null;
			if ( json != null && !json.IsNull )
			{
				item = new RecentItem();
				item.Id         = json.ContainsName("Id"  ) ? json.GetValue<string>("Id"  ) : String.Empty;
				item.Name       = json.ContainsName("Name") ? json.GetValue<string>("Name") : String.Empty;
				item.Attributes = mapper.Deserialize<Attributes>(json.GetValue("attributes"));
			}
			return item;
		}
	}
}
