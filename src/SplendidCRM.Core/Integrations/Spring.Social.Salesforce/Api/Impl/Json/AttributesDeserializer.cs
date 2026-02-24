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

using System;
using System.Collections.Generic;

// .NET 10 Migration: Removed "using Spring.Json;" (line 24 in source).
// Spring.Json.IJsonDeserializer, Spring.Json.JsonValue, and Spring.Json.JsonMapper
// are replaced by stub types defined in the Spring.Social.Salesforce.Api.Impl namespace
// (AbstractSalesforceOperations.cs). These stubs are accessible from this child namespace
// (Spring.Social.Salesforce.Api.Impl.Json) via C# namespace resolution without explicit
// using directives. The Attributes DTO (Spring.Social.Salesforce.Api.Attributes) is
// similarly accessible from the grandparent namespace without a using directive.
// This is a dormant integration stub — compiles but is NOT expected to execute at runtime.

namespace Spring.Social.Salesforce.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Version. 
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class AttributesDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			Attributes obj = null;
			if ( json != null && !json.IsNull )
			{
				obj = new Attributes();
				obj.Type = json.ContainsName("type") ? json.GetValue<string>("type") : String.Empty;
				obj.Url  = json.ContainsName("url" ) ? json.GetValue<string>("url" ) : String.Empty;
			}
			return obj;
		}
	}
}
