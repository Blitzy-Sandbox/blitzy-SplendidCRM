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

// .NET 10 Migration: Removed Spring.Json, Spring.Http, Spring.Rest.Client using directives.
// These Spring.NET Framework assemblies (discontinued) are unavailable on .NET 10.
// Equivalent stub RestTemplate is defined in AbstractSalesforceOperations.
// This is a dormant integration stub — compiles but is NOT expected to execute at runtime.

using System;
using System.Collections.Specialized;

namespace Spring.Social.Salesforce.Api.Impl
{
	/// <summary>
	/// Implementation of ISObjectOperations for Salesforce SObject CRUD operations.
	/// Dormant Enterprise Edition stub — compiles on .NET 10 but not activated.
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class SObjectOperations : AbstractSalesforceOperations, ISObjectOperations
	{
		#region ISObjectOperations Members

		/// <summary>
		/// Describes the individual metadata for the specified object.
		/// </summary>
		/// <param name="version">Version number.</param>
		/// <param name="name">SObject name.</param>
		/// <returns>BasicSObject describing the SObject metadata.</returns>
		public BasicSObject GetBasicSObject(string version, string name)
		{
			requireAuthorization();
			return FetchObject<BasicSObject>("/services/data/v" + version + "/sobjects/" + name + "/");
		}

		/// <summary>
		/// Accesses records based on the specified object ID.
		/// </summary>
		/// <param name="version">Version number.</param>
		/// <param name="name">SObject name.</param>
		/// <param name="id">SObject ID.</param>
		/// <returns>SObject representing the Salesforce record.</returns>
		public SObject GetSObject(string version, string name, string id)
		{
			requireAuthorization();
			return FetchObject<SObject>("/services/data/v" + version + "/sobjects/" + name + "/" + id);
		}

		/// <summary>
		/// Accesses records based on the specified object ID with field selection.
		/// </summary>
		/// <param name="version">Version number.</param>
		/// <param name="name">SObject name.</param>
		/// <param name="id">SObject ID.</param>
		/// <param name="fields">Fields to retrieve.</param>
		/// <returns>SObject representing the Salesforce record with only selected fields.</returns>
		public SObject GetSObject(string version, string name, string id, string[] fields)
		{
			requireAuthorization();
			NameValueCollection queryParameters = new NameValueCollection();
			if ( fields != null )
				queryParameters.Add("fields", String.Join(",", fields));
			return FetchObject<SObject>("/services/data/v" + version + "/sobjects/" + name + "/" + id, queryParameters);
		}

		/// <summary>
		/// Retrieves the specified blob field from an individual record.
		/// </summary>
		/// <param name="version">Version number.</param>
		/// <param name="name">SObject name.</param>
		/// <param name="id">SObject ID.</param>
		/// <param name="field">Field to retrieve.</param>
		/// <returns>Byte array containing the blob field data.</returns>
		public byte[] GetSObjectBlob(string version, string name, string id, string field)
		{
			requireAuthorization();
			return FetchObject<byte[]>("/services/data/v" + version + "/sobjects/" + name + "/" + id + "/" + field);
		}

		/// <summary>
		/// Deletes the record identified by the specified object ID.
		/// </summary>
		/// <param name="version">Version number.</param>
		/// <param name="name">SObject name.</param>
		/// <param name="id">SObject ID.</param>
		public void DeleteSObject(string version, string name, string id)
		{
			requireAuthorization();
			this.restTemplate.Delete("/services/data/v" + version + "/sobjects/" + name + "/" + id);
		}

		#endregion
	}
}
