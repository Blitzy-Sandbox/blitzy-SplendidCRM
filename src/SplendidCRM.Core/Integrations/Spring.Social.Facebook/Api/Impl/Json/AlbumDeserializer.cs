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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/Json/AlbumDeserializer.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Json; — Spring.Json has no .NET 10 NuGet equivalent.
//     IJsonDeserializer, JsonValue, and JsonMapper are stub types defined in the
//     parent namespace Spring.Social.Facebook.Api.Impl (AbstractFacebookOperations.cs)
//     and resolve automatically via C# namespace resolution.
//   - KEPT: All other using directives, namespace, class declaration, method bodies,
//     field mappings, null guards, enum parsing helpers, and ToDateTime calls preserved exactly.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and AAP §0.8.1 (Minimal Change Clause).

using System;
using System.Globalization;

namespace Spring.Social.Facebook.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for Album. 
	/// </summary>
	/// <author>Craig Walls</author>
	/// <author>SplendidCRM (.NET)</author>
	class AlbumDeserializer : IJsonDeserializer
	{
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			Album album = null;
			if ( json != null && !json.IsNull )
			{
				album = new Album();
				album.ID           = json.ContainsName("id"          ) ? json.GetValue<string>("id"         ) : String.Empty;
				album.Name         = json.ContainsName("name"        ) ? json.GetValue<string>("name"       ) : String.Empty;
				album.Description  = json.ContainsName("description" ) ? json.GetValue<string>("description") : String.Empty;
				album.Location     = json.ContainsName("location"    ) ? json.GetValue<string>("location"   ) : String.Empty;
				album.Link         = json.ContainsName("link"        ) ? json.GetValue<string>("link"       ) : String.Empty;
				album.CoverPhotoId = json.ContainsName("cover_photo" ) ? json.GetValue<string>("cover_photo") : String.Empty;
				album.Count        = json.ContainsName("count"       ) ? json.GetValue<int   >("count"      ) : 0;
				album.CanUpload    = json.ContainsName("can_upload"  ) ? json.GetValue<bool  >("can_upload" ) : false;
				album.CreatedTime  = json.ContainsName("created_time") ? JsonUtils.ToDateTime(json.GetValue<string>("created_time"), "yyyy-MM-ddTHH:mm:ss") : DateTime.MinValue;
				album.UpdatedTime  = json.ContainsName("updated_time") ? JsonUtils.ToDateTime(json.GetValue<string>("updated_time"), "yyyy-MM-ddTHH:mm:ss") : DateTime.MinValue;

				album.From         = mapper.Deserialize<Reference>(json.GetValue("from"));
				album.Type         = TypeDeserializer   (json.GetValue("type"   ));
				album.Privacy      = PrivacyDeserializer(json.GetValue("privacy"));
			}
			return album;
		}

		private static Album.enumType TypeDeserializer(JsonValue json)
		{
			Album.enumType value = Album.enumType.UNKNOWN;
			if ( json != null && !json.IsNull )
			{
				try
				{
					string code = json.GetValue<string>();
					code = code.ToUpper();
					value = (Album.enumType) Enum.Parse(typeof(Album.enumType), code);
				}
				catch
				{
				}
			}
			return value;
		}

		private static Album.enumPrivacy PrivacyDeserializer(JsonValue json)
		{
			Album.enumPrivacy value = Album.enumPrivacy.CUSTOM;
			if ( json != null && !json.IsNull )
			{
				try
				{
					string code = json.GetValue<string>();
					code = code.ToUpper();
					code = code.Replace("-", "_");
					value = (Album.enumPrivacy) Enum.Parse(typeof(Album.enumPrivacy), code);
				}
				catch
				{
				}
			}
			return value;
		}
	}
}
