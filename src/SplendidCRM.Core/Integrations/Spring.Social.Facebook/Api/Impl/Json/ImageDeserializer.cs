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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/ImageDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json;  — Spring.Json has no .NET 10 NuGet equivalent.
//     IJsonDeserializer, JsonValue, and JsonMapper are stub types defined in
//     AbstractFacebookOperations.cs (Spring.Social.Facebook.Api.Impl namespace)
//     and resolve via C# parent-namespace resolution from this child namespace
//     (Spring.Social.Facebook.Api.Impl.Json).
//   - KEPT: using System; and using System.Globalization; exactly as in original source.
//   - ALL other content (license header, namespace, class declaration, method body,
//     XML doc comments, author tags) preserved unchanged per AAP §0.8.1 Minimal Change Clause.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and §0.8.1 (Minimal Change Clause).

using System;
using System.Globalization;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Image. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class ImageDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			Photo.Image image = null;
			if ( json != null && !json.IsNull )
			{
				image = new Photo.Image();
				image.Source = json.ContainsName("source") ? json.GetValue<string>("source") : String.Empty;
				image.Width  = json.ContainsName("width" ) ? json.GetValue<int   >("width" ) : 0;
				image.Height = json.ContainsName("height") ? json.GetValue<int   >("height") : 0;
			}
			return image;
		}
	}
}
