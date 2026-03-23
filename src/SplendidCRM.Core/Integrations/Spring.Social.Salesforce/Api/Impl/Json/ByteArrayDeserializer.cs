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

// .NET 10 Migration: Removed "using Spring.Json;" — Spring.Json assembly (Spring.Rest.dll) is
// discontinued with no .NET Core / .NET 10 equivalent. The IJsonDeserializer, JsonValue, and
// JsonMapper stub types are defined in the parent namespace Spring.Social.Salesforce.Api.Impl
// (in AbstractSalesforceOperations.cs) and resolve via C# namespace resolution without an
// explicit using directive. This is a DORMANT STUB — compiles on .NET 10 but NOT executed at runtime.

namespace Spring.Social.Salesforce.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Version. 
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class ByteArrayDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			byte[] value = null;
			if ( json != null && !json.IsNull )
			{
				string sValue = json.GetValue<string>();
				try
				{
					value = Convert.FromBase64String(sValue);
				}
				catch
				{
				}
			}
			return value;
		}
	}
}
