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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/StoryTagDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent
//   - KEPT: All other using directives, class/method signatures, and business logic
//   - IJsonDeserializer, JsonValue, JsonMapper resolve via C# parent namespace resolution
//     from Spring.Social.Facebook.Api.Impl.Json → Spring.Social.Facebook.Api.Impl
//     (stubs defined in AbstractFacebookOperations.cs)
//   - StoryTag resolves via grandparent namespace Spring.Social.Facebook.Api
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

using System;
using System.Globalization;
using System.Collections.Generic;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for StoryTag. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class StoryTagDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			StoryTag video = null;
			if ( json != null && !json.IsNull )
			{
				video = new StoryTag();
				video.ID     = json.ContainsName("id"    ) ? json.GetValue<string>("id"    ) : String.Empty;
				video.Name   = json.ContainsName("name"  ) ? json.GetValue<string>("name"  ) : String.Empty;
				video.Offset = json.ContainsName("offset") ? json.GetValue<int   >("offset") : 0;
				video.Length = json.ContainsName("length") ? json.GetValue<int   >("length") : 0;
			}
			return video;
		}
	}
}
