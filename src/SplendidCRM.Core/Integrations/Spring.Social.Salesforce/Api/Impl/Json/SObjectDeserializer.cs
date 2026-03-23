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

// .NET 10 Migration: Removed "using Spring.Json;" (line 24 in original source).
// Spring.Json (from discontinued Spring.Rest.dll) is unavailable on .NET 10.
// The stub types IJsonDeserializer, JsonValue, and JsonMapper are defined within the parent
// namespace Spring.Social.Salesforce.Api.Impl (AbstractSalesforceOperations.cs) and are
// accessible here without an explicit using directive via C# namespace hierarchy resolution.
// SObject and Attributes reside in Spring.Social.Salesforce.Api (grandparent namespace) and
// are similarly accessible without an explicit using directive.
// This is a DORMANT STUB — compiles on .NET 10 but is NOT expected to execute at runtime.

using System;
using System.Collections.Generic;

namespace Spring.Social.Salesforce.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Version. 
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class SObjectDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			SObject obj = null;
			if ( json != null && !json.IsNull )
			{
				obj = new SObject();
				obj.Attributes = mapper.Deserialize<Attributes>(json.GetValue("attributes"));
				obj.Fields     = new Dictionary<string, object>();
				foreach ( string sField in json.GetNames() )
				{
					JsonValue jsonValue = json.GetValue(sField);
					if ( jsonValue.IsBoolean )
					{
						obj.Fields.Add(sField, json.GetValue<bool>(sField));
					}
					else if ( jsonValue.IsString )
					{
						obj.Fields.Add(sField, json.GetValue<string>(sField));
					}
					else if ( jsonValue.IsNumber )
					{
						try
						{
							obj.Fields.Add(sField, json.GetValue<int>(sField));
						}
						catch
						{
							obj.Fields.Add(sField, json.GetValue<float>(sField));
						}
					}
					else if ( jsonValue.IsNull )
					{
						obj.Fields.Add(sField, null);
					}
					else if ( jsonValue.IsObject && sField != "attributes" )
					{
						obj.Fields.Add(sField, jsonValue);
					}
				}
			}
			return obj;
		}
	}
}
