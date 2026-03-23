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

// .NET 10 Migration: Removed "using Spring.Json;" (discontinued Spring.NET Framework assembly,
// unavailable on .NET 10). The IJsonDeserializer, JsonValue, and JsonMapper stub types are
// defined in the parent namespace Spring.Social.Salesforce.Api.Impl (AbstractSalesforceOperations.cs)
// and resolve automatically via C# namespace resolution from this child namespace
// Spring.Social.Salesforce.Api.Impl.Json — no explicit using directive required.
// This is a dormant integration stub — compiles on .NET 10 but is NOT expected to execute at runtime.

using System;
using System.Collections.Generic;

namespace Spring.Social.Salesforce.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Version. 
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class QueryResultDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			QueryResult query = null;
			if ( json != null && !json.IsNull )
			{
				query = new QueryResult();
				query.Done           = json.ContainsName("done"          ) ? json.GetValue<bool  >("done"          ) : false;
				query.TotalSize      = json.ContainsName("totalSize"     ) ? json.GetValue<int   >("totalSize"     ) : 0;
				query.NextRecordsUrl = json.ContainsName("nextRecordsUrl") ? json.GetValue<string>("nextRecordsUrl") : String.Empty;
				query.Records        = mapper.Deserialize<List<SObject>>(json.GetValue("records"));
			}
			return query;
		}
	}
}
