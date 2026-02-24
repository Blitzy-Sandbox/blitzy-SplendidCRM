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

// .NET 10 Migration: Removed 'using Spring.Json;' (line 24 in source file).
// Spring.Json (from the discontinued Spring.Rest.dll) has no .NET 10 equivalent.
// IJsonDeserializer, JsonValue, and JsonMapper are provided as stub types defined in
// AbstractSalesforceOperations.cs under Spring.Social.Salesforce.Api.Impl namespace.
// C# namespace resolution makes them accessible from this child namespace
// (Spring.Social.Salesforce.Api.Impl.Json) without an explicit using directive.
// This is a DORMANT STUB — compiles on .NET 10 but is NOT executed at runtime.

using System;
using System.Collections.Generic;

namespace Spring.Social.Salesforce.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Version. 
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class StringDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			String value = String.Empty;
			if ( json != null && !json.IsNull )
			{
				value = json.GetValue<string>();
			}
			return value;
		}
	}
}
