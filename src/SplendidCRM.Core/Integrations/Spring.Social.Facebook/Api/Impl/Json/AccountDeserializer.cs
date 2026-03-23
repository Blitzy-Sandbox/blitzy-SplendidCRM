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

using System;
using System.Globalization;

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/AccountDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json;  — Spring.Json has no .NET 10 NuGet equivalent;
//              IJsonDeserializer, JsonValue, and JsonMapper stubs are defined in
//              AbstractFacebookOperations.cs (Spring.Social.Facebook.Api.Impl namespace)
//              and resolve automatically via C# parent namespace resolution.
//   - KEPT: using System; — provides String.Empty used as fallback for missing JSON fields.
//   - KEPT: using System.Globalization; — preserved per Minimal Change Clause (AAP 0.8.1).
//   - ALL other content is preserved EXACTLY — no business logic changes.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP 0.7.4 (Spring.Social Dependency Removal) and AAP 0.8.1 (Minimal Change Clause).

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Account. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class AccountDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			Account account = null;
			if ( json != null && !json.IsNull )
			{
				account = new Account();
				account.ID          = json.ContainsName("id"          ) ? json.GetValue<string>("id"          ) : String.Empty;
				account.Name        = json.ContainsName("name"        ) ? json.GetValue<string>("name"        ) : String.Empty;
				account.Category    = json.ContainsName("category"    ) ? json.GetValue<string>("category"    ) : String.Empty;
				account.AccessToken = json.ContainsName("access_token") ? json.GetValue<string>("access_token") : String.Empty;
			}
			return account;
		}
	}
}
