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

// Migration Note: .NET 10 ASP.NET Core replatforming — Spring.Rest.Client, Spring.Social.OAuth2,
// Spring.Http.Converters, Spring.Json removed (no .NET 10 equivalents). This is a dormant
// Enterprise Edition integration stub; it must compile but NOT execute.
// SalesforceOAuth2ApiBinding moved to Spring.Social.OAuth2 namespace (preserving original namespace)
// after .NET 10 replatforming; added using directive to resolve the base class reference.
using System;

using Spring.Social.OAuth2;
using Spring.Social.Salesforce.Api.Impl.Json;

namespace Spring.Social.Salesforce.Api.Impl
{
	/// <summary>
	/// This is the central class for interacting with Salesforce.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Most (not all) Salesforce operations require OAuth authentication. 
	/// To perform such operations, <see cref="SalesforceTemplate"/> must be constructed 
	/// with the minimal amount of information required to sign requests to Salesforce's API 
	/// with an OAuth <code>Authorization</code> header.
	/// </para>
	/// <para>
	/// There are some operations, such as searching, that do not require OAuth authentication. 
	/// In those cases, you may use a <see cref="SalesforceTemplate"/> that is created through 
	/// the default constructor and without any OAuth details.
	/// Attempts to perform secured operations through such an instance, however, 
	/// will result in <see cref="SalesforceApiException"/> being thrown.
	/// </para>
	/// </remarks>
	/// <author>SplendidCRM (.NET)</author>
	public class SalesforceTemplate : SalesforceOAuth2ApiBinding, ISalesforce
	{
		private IVersionOperations  versionOperations;
		private IMetadataOperations metadataOperations;
		private ISObjectOperations  sobjectOperations;
		private ISearchOperations   searchOperations;
		private IUserOperations     userOperations;

		/// <summary>
		/// Create a new instance of <see cref="SalesforceTemplate"/>.
		/// </summary>
		/// <param name="instanceUrl">The Salesforce instance URL.</param>
		/// <param name="accessToken">An access token acquired through OAuth authentication with Salesforce.</param>
		public SalesforceTemplate(string instanceUrl, string accessToken) : base(instanceUrl, accessToken)
		{
			this.InitSubApis();
		}

		#region ISalesforce Members

		/// <summary>Gets the version operations sub-API.</summary>
		public IVersionOperations VersionOperations { get { return this.versionOperations; } }

		/// <summary>Gets the metadata operations sub-API.</summary>
		public IMetadataOperations MetadataOperations { get { return this.metadataOperations; } }

		/// <summary>Gets the SObject operations sub-API.</summary>
		public ISObjectOperations SObjectOperations { get { return this.sobjectOperations; } }

		/// <summary>Gets the search operations sub-API.</summary>
		public ISearchOperations SearchOperations { get { return this.searchOperations; } }

		/// <summary>Gets the user operations sub-API.</summary>
		public IUserOperations UserOperations { get { return this.userOperations; } }

		/// <summary>
		/// Gets the underlying <see cref="IRestOperations"/> object allowing for consumption of Salesforce endpoints 
		/// that may not be otherwise covered by the API binding. 
		/// </summary>
		/// <remarks>
		/// The <see cref="IRestOperations"/> object returned is configured to include an OAuth "Authorization" header on all requests.
		/// </remarks>
		public IRestOperations RestOperations
		{
			// Migration note: Spring.Rest.Client.RestTemplate no longer implements Spring.Rest.Client.IRestOperations.
			// Returning null for this dormant integration stub; this property is not expected to be invoked at runtime.
			get { return null; }
		}

		#endregion

		private void InitSubApis()
		{
			// Migration note: RestTemplate stub is passed for sub-API construction.
			// Sub-templates use it for HTTP operations but are never called at runtime in this dormant stub.
			RestTemplate rt = new RestTemplate();
			bool authorized = this.IsAuthorized;
			this.versionOperations  = new VersionTemplate  (rt, authorized);
			this.metadataOperations = new MetadataTemplate (rt, authorized);
			this.sobjectOperations  = new SObjectTemplate  (rt, authorized);
			this.searchOperations   = new SearchTemplate   (rt, authorized);
			this.userOperations     = new UserTemplate     (rt, authorized);
		}
	}
}
