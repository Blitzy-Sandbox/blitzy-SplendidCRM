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

// .NET 10 Migration: Removed "using Spring.Json;" — Spring.Json.IJsonDeserializer, Spring.Json.JsonValue,
// and Spring.Json.JsonMapper are discontinued .NET Framework assemblies with no .NET 10 NuGet equivalent.
// Stub replacements for all three types are defined in the Spring.Social.Salesforce.Api.Impl namespace
// (AbstractSalesforceOperations.cs) and resolve automatically via C# enclosing-namespace lookup.
// RecordTypeInfo resolves from the Spring.Social.Salesforce.Api enclosing namespace.
// This is a dormant integration stub — compiles but is NOT expected to execute at runtime (AAP §0.7.4).

using System;
using System.Collections.Generic;

namespace Spring.Social.Salesforce.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Version. 
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class RecordTypeInfoDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			RecordTypeInfo info = null;
			if ( json != null && !json.IsNull )
			{
				info = new RecordTypeInfo();
				info.Available                = json.ContainsName("available"               ) ? json.GetValue<bool  >("available"               ) : false;
				info.DefaultRecordTypeMapping = json.ContainsName("defaultRecordTypeMapping") ? json.GetValue<bool  >("defaultRecordTypeMapping") : false;
				info.Name                     = json.ContainsName("name"                    ) ? json.GetValue<string>("name"                    ) : String.Empty;
				info.RecordTypeId             = json.ContainsName("recordTypeId"            ) ? json.GetValue<string>("recordTypeId"            ) : String.Empty;
			}
			return info;
		}
	}
}
