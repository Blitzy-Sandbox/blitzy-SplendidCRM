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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/ReferenceDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent;
//     JsonValue, JsonMapper, and IJsonDeserializer stubs are defined in the parent
//     namespace Spring.Social.Facebook.Api.Impl (AbstractFacebookOperations.cs) and
//     resolve via C# parent namespace lookup without a using directive.
//   - KEPT: using System; (String.Empty), using System.Globalization; (preserved per AAP 0.8.1)
//   - KEPT: All class/method signatures, field mapping logic, null guards, and XML doc comments
//     preserved exactly per AAP 0.8.1 Minimal Change Clause.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

using System;
using System.Globalization;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Reference. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class ReferenceDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			Reference reference = null;
			if ( json != null && !json.IsNull )
			{
				reference = new Reference();
				reference.ID   = json.ContainsName("id"  ) ? json.GetValue<string>("id"  ) : String.Empty;
				reference.Name = json.ContainsName("name") ? json.GetValue<string>("name") : String.Empty;
			}
			return reference;
		}
	}
}
