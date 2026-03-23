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

// .NET 10 Migration: Removed 'using Spring.Json;' (line 24 of original source).
// Spring.Json.IJsonDeserializer, Spring.Json.JsonValue, and Spring.Json.JsonMapper are
// discontinued .NET Framework-only assemblies with no .NET 10 NuGet equivalent.
// Stub replacements for IJsonDeserializer, JsonValue, and JsonMapper are defined in
// AbstractSalesforceOperations.cs under the Spring.Social.Salesforce.Api.Impl namespace
// and resolve here via C# parent-namespace resolution without an explicit using directive.
// ChildRelationship resolves from the grandparent Spring.Social.Salesforce.Api namespace,
// also via C# namespace resolution. This is a dormant integration stub — compiles on
// .NET 10 but is NOT expected to execute at runtime (AAP section 0.7.4).

using System;
using System.Collections.Generic;

namespace Spring.Social.Salesforce.Api.Impl.Json
{
	/// <summary>
	/// JSON deserializer for <see cref="Spring.Social.Salesforce.Api.ChildRelationship"/>.
	/// Populates CascadeDelete, ChildSObject, DeprecatedAndHidden, Field, and RelationshipName
	/// properties from the Salesforce API JSON metadata response.
	/// </summary>
	/// <remarks>
	/// .NET 10 Migration: <c>using Spring.Json;</c> removed. IJsonDeserializer, JsonValue, and
	/// JsonMapper stub types are resolved from the parent namespace
	/// <c>Spring.Social.Salesforce.Api.Impl</c> (defined in AbstractSalesforceOperations.cs).
	/// Dormant stub — compiles but is NOT executed at runtime.
	/// </remarks>
	/// <author>SplendidCRM (.NET)</author>
	class ChildRelationshipDeserializer : IJsonDeserializer
	{
		/// <summary>
		/// Deserializes a Salesforce child relationship JSON value into a
		/// <see cref="Spring.Social.Salesforce.Api.ChildRelationship"/> instance.
		/// </summary>
		/// <param name="json">
		/// The JSON value node representing the child relationship object.
		/// If <c>null</c> or a JSON null value, returns <c>null</c>.
		/// </param>
		/// <param name="mapper">
		/// The JSON mapper providing type-dispatched deserialization for nested objects
		/// (unused in this leaf deserializer).
		/// </param>
		/// <returns>
		/// A populated <see cref="Spring.Social.Salesforce.Api.ChildRelationship"/> instance,
		/// or <c>null</c> if <paramref name="json"/> is <c>null</c> or a JSON null value.
		/// </returns>
		public object Deserialize(JsonValue json, JsonMapper mapper)
		{
			ChildRelationship info = null;
			if ( json != null && !json.IsNull )
			{
				info = new ChildRelationship();
				info.CascadeDelete       = json.ContainsName("cascadeDelete"      ) ? json.GetValue<bool  >("cascadeDelete"      ) : false;
				info.ChildSObject        = json.ContainsName("childSObject"       ) ? json.GetValue<string>("childSObject"       ) : String.Empty;
				info.DeprecatedAndHidden = json.ContainsName("deprecatedAndHidden") ? json.GetValue<bool  >("deprecatedAndHidden") : false;
				info.Field               = json.ContainsName("field"              ) ? json.GetValue<string>("field"              ) : String.Empty;
				info.RelationshipName    = json.ContainsName("relationshipName"   ) ? json.GetValue<string>("relationshipName"   ) : String.Empty;
			}
			return info;
		}
	}
}
