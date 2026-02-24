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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/EventInviteeDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent.
//     IJsonDeserializer, JsonValue, and JsonMapper stub definitions are provided
//     by AbstractFacebookOperations.cs in the Spring.Social.Facebook.Api.Impl
//     parent namespace and are accessible here without an explicit using directive.
//   - KEPT: using System; and using System.Globalization; preserved exactly.
//   - KEPT: All class/method signatures, fields, constructor, business logic,
//           XML doc comments, and exception handling preserved exactly.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

using System;
using System.Globalization;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for EventInvitee. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class EventInviteeDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			EventInvitee album = null;
			if ( json != null && !json.IsNull )
			{
				album = new EventInvitee();
				album.ID         = json.ContainsName("id"  ) ? json.GetValue<string>("id"  ) : String.Empty;
				album.Name       = json.ContainsName("name") ? json.GetValue<string>("name") : String.Empty;
				album.RsvpStatus = RsvpStatusDeserializer(json.GetValue("rsvp_status"));
			}
			return album;
		}

		private static RsvpStatus RsvpStatusDeserializer(JsonValue json)
		{
			RsvpStatus value = RsvpStatus.UNSURE;
			if ( json != null && !json.IsNull )
			{
				try
				{
					string code = json.GetValue<string>();
					code = code.ToUpper();
					value = (RsvpStatus) Enum.Parse(typeof(RsvpStatus), code);
				}
				catch
				{
				}
			}
			return value;
		}
	}
}
