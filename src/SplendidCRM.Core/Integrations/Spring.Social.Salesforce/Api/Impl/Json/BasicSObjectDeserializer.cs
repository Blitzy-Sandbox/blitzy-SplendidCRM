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

// .NET 10 Migration: Removed 'using Spring.Json;' — the Spring.Json assembly (Spring.REST.dll) is
// discontinued with no .NET Core / .NET 10 equivalent. IJsonDeserializer, JsonValue, and JsonMapper
// are now stub types defined in the parent namespace Spring.Social.Salesforce.Api.Impl
// (see AbstractSalesforceOperations.cs #region Spring Framework Stubs). C# namespace resolution
// from child namespace Spring.Social.Salesforce.Api.Impl.Json walks up to the parent namespace and
// finds them automatically without an explicit using directive.
// This is a dormant integration stub — compiles but is NOT expected to execute at runtime.

using System;
using System.Collections.Generic;

namespace Spring.Social.Salesforce.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Version. 
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class BasicSObjectDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			BasicSObject obj = null;
			if ( json != null && !json.IsNull )
			{
				obj = new BasicSObject();
				obj.ObjectDescribe = mapper.Deserialize<DescribeSObject >(json.GetValue("objectDescribe"));
				obj.RecentItems    = mapper.Deserialize<List<RecentItem>>(json.GetValue("recentItems"   ));
			}
			return obj;
		}
	}
}
