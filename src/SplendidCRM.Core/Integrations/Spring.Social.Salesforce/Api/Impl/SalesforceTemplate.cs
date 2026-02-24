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

// Migration Note: .NET 10 ASP.NET Core replatforming — all Spring.* using directives removed.
//   Removed: using Spring.Json;
//   Removed: using Spring.Rest.Client;
//   Removed: using Spring.Social.OAuth2;        (replaced by using directive below — namespace preserved)
//   Removed: using Spring.Http.Converters;
//   Removed: using Spring.Http.Converters.Json;
//
// The Spring.Social.OAuth2 namespace is now a stub namespace defined in SalesforceOAuth2ApiBinding.cs
// (in the same assembly). The using directive below resolves OAuth2Version, which SalesforceTemplate
// overrides via GetOAuth2Version(), and the stub converter types defined in this file.
//
// SpringJsonHttpMessageConverter and ByteArrayHttpMessageConverter are defined in this file
// as .NET 10-compatible stub replacements for the discontinued Spring.Http.Converters package.
// They implement IHttpMessageConverter (defined in AbstractSalesforceOperations.cs, same namespace).
//
// RestOperations returns null in this dormant stub: the stub RestTemplate class does not implement
// IRestOperations because modifying AbstractSalesforceOperations.cs is outside this file's scope.
// This is acceptable — the Salesforce integration is a dormant Enterprise Edition stub that
// must compile on .NET 10 but is NOT expected to execute at runtime.
//
// Kept: using Spring.Social.Salesforce.Api.Impl.Json;  (internal namespace — all deserializers)
// This file is a DORMANT STUB — must compile on .NET 10 but NOT execute.

using System;
using System.Collections.Generic;

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
		private IVersionOperations   versionOperations ;
		private IMetadataOperations  metadataOperations;
		private ISObjectOperations   sobjectOperations ;
		private ISearchOperations    searchOperations  ;
		private IUserOperations      userOperations    ;

		/// <summary>
		/// Create a new instance of <see cref="SalesforceTemplate"/>.
		/// </summary>
		/// <param name="instanceURL">The Salesforce instance URL (e.g., https://na1.salesforce.com).</param>
		/// <param name="accessToken">An access token acquired through OAuth authentication with Salesforce.</param>
		public SalesforceTemplate(string instanceURL, string accessToken) : base(instanceURL, accessToken)
		{
			this.InitSubApis();
		}

		#region ISalesforce Members

		/// <summary>Gets the version operations sub-API.</summary>
		public IVersionOperations  VersionOperations  { get { return this.versionOperations ; } }

		/// <summary>Gets the metadata operations sub-API.</summary>
		public IMetadataOperations MetadataOperations { get { return this.metadataOperations; } }

		/// <summary>Gets the SObject operations sub-API.</summary>
		public ISObjectOperations  SObjectOperations  { get { return this.sobjectOperations ; } }

		/// <summary>Gets the search operations sub-API.</summary>
		public ISearchOperations   SearchOperations   { get { return this.searchOperations  ; } }

		/// <summary>Gets the user operations sub-API.</summary>
		public IUserOperations     UserOperations     { get { return this.userOperations    ; } }

		/// <summary>
		/// Gets the underlying <see cref="IRestOperations"/> object allowing for consumption of Twitter endpoints 
		/// that may not be otherwise covered by the API binding. 
		/// </summary>
		/// <remarks>
		/// The <see cref="IRestOperations"/> object returned is configured to include an OAuth "Authorization" header on all requests.
		/// </remarks>
		public IRestOperations RestOperations
		{
			// Migration note: Spring.Rest.Client.RestTemplate no longer implements Spring.Rest.Client.IRestOperations.
			// The stub RestTemplate class (defined in AbstractSalesforceOperations.cs) does not implement IRestOperations.
			// Returning null for this dormant integration stub; this property is not expected to be invoked at runtime.
			// Original: return this.RestTemplate;
			get { return null; }
		}

		#endregion

		/// <summary>
		/// Enables customization of the <see cref="RestTemplate"/> used to consume provider API resources.
		/// </summary>
		/// <remarks>
		/// An example use case might be to configure a custom error handler. 
		/// Note that this method is called after the RestTemplate has been configured with the message converters returned from GetMessageConverters().
		/// </remarks>
		/// <param name="restTemplate">The RestTemplate to configure.</param>
		protected override void ConfigureRestTemplate(RestTemplate restTemplate)
		{
			restTemplate.ErrorHandler = new SalesforceErrorHandler();
		}

		/// <summary>
		/// Returns the OAuth2 version used for Salesforce authentication.
		/// Overrides the base class default (<see cref="OAuth2Version.Bearer"/>) to use
		/// <see cref="OAuth2Version.Draft10"/> for Salesforce compatibility.
		/// </summary>
		/// <returns><see cref="OAuth2Version.Draft10"/>.</returns>
		protected override OAuth2Version GetOAuth2Version()
		{
			return OAuth2Version.Draft10;
		}

		/// <summary>
		/// Returns a list of <see cref="IHttpMessageConverter"/>s to be used by the internal <see cref="RestTemplate"/>.
		/// </summary>
		/// <remarks>
		/// This implementation adds <see cref="SpringJsonHttpMessageConverter"/> and <see cref="ByteArrayHttpMessageConverter"/> to the default list.
		/// </remarks>
		/// <returns>
		/// The list of <see cref="IHttpMessageConverter"/>s to be used by the internal <see cref="RestTemplate"/>.
		/// </returns>
		protected override IList<IHttpMessageConverter> GetMessageConverters()
		{
			IList<IHttpMessageConverter> converters = base.GetMessageConverters();
			converters.Add(new ByteArrayHttpMessageConverter());
			converters.Add(this.GetJsonMessageConverter());
			return converters;
		}

		/// <summary>
		/// Returns a <see cref="SpringJsonHttpMessageConverter"/> to be used by the internal <see cref="RestTemplate"/>.
		/// <para/>
		/// Override to customize the message converter (for example, to set a custom object mapper or supported media types).
		/// </summary>
		/// <returns>The configured <see cref="SpringJsonHttpMessageConverter"/>.</returns>
		protected virtual SpringJsonHttpMessageConverter GetJsonMessageConverter()
		{
			JsonMapper jsonMapper = new JsonMapper();
			jsonMapper.RegisterDeserializer(typeof(SalesforceVersion          ), new SalesforceVersionDeserializer          ());
			jsonMapper.RegisterDeserializer(typeof(List<SalesforceVersion>    ), new ListDeserializer<SalesforceVersion>    ());
			jsonMapper.RegisterDeserializer(typeof(SalesforceResources        ), new SalesforceResourcesDeserializer        ());
			jsonMapper.RegisterDeserializer(typeof(DescribeGlobal             ), new DescribeGlobalDeserializer             ());
			jsonMapper.RegisterDeserializer(typeof(DescribeGlobalSObject      ), new DescribeGlobalSObjectDeserializer      ());
			jsonMapper.RegisterDeserializer(typeof(List<DescribeGlobalSObject>), new ListDeserializer<DescribeGlobalSObject>());
			jsonMapper.RegisterDeserializer(typeof(DescribeSObject            ), new DescribeSObjectDeserializer            ());
			jsonMapper.RegisterDeserializer(typeof(Field                      ), new FieldDeserializer                      ());
			jsonMapper.RegisterDeserializer(typeof(List<Field>                ), new ListDeserializer<Field>                ());
			jsonMapper.RegisterDeserializer(typeof(RecordTypeInfo             ), new RecordTypeInfoDeserializer             ());
			jsonMapper.RegisterDeserializer(typeof(List<RecordTypeInfo>       ), new ListDeserializer<RecordTypeInfo>       ());
			jsonMapper.RegisterDeserializer(typeof(ChildRelationship          ), new ChildRelationshipDeserializer          ());
			jsonMapper.RegisterDeserializer(typeof(List<ChildRelationship>    ), new ListDeserializer<ChildRelationship>    ());
			jsonMapper.RegisterDeserializer(typeof(PicklistEntry              ), new PicklistEntryDeserializer              ());
			jsonMapper.RegisterDeserializer(typeof(List<PicklistEntry>        ), new ListDeserializer<PicklistEntry>        ());
			jsonMapper.RegisterDeserializer(typeof(String                     ), new StringDeserializer                     ());
			jsonMapper.RegisterDeserializer(typeof(List<String>               ), new ListDeserializer<String>               ());
			jsonMapper.RegisterDeserializer(typeof(byte[]                     ), new ByteArrayDeserializer                  ());
			jsonMapper.RegisterDeserializer(typeof(BasicSObject               ), new BasicSObjectDeserializer               ());
			jsonMapper.RegisterDeserializer(typeof(List<BasicSObject>         ), new ListDeserializer<BasicSObject>         ());
			jsonMapper.RegisterDeserializer(typeof(RecentItem                 ), new RecentItemDeserializer                 ());
			jsonMapper.RegisterDeserializer(typeof(List<RecentItem>           ), new ListDeserializer<RecentItem>           ());
			jsonMapper.RegisterDeserializer(typeof(SObject                    ), new SObjectDeserializer                    ());
			jsonMapper.RegisterDeserializer(typeof(List<SObject>              ), new ListDeserializer<SObject>              ());
			jsonMapper.RegisterDeserializer(typeof(Attributes                 ), new AttributesDeserializer                 ());
			jsonMapper.RegisterDeserializer(typeof(QueryResult                ), new QueryResultDeserializer                ());
			return new SpringJsonHttpMessageConverter(jsonMapper);
		}

		private void InitSubApis()
		{
			this.versionOperations  = new VersionTemplate (this.RestTemplate, this.IsAuthorized);
			this.metadataOperations = new MetadataTemplate(this.RestTemplate, this.IsAuthorized);
			this.sobjectOperations  = new SObjectTemplate (this.RestTemplate, this.IsAuthorized);
			this.searchOperations   = new SearchTemplate  (this.RestTemplate, this.IsAuthorized);
			this.userOperations     = new UserTemplate    (this.RestTemplate, this.IsAuthorized);
		}
	}

	#region Spring Framework Stubs — HTTP Message Converter Types

	// .NET 10 Migration: The following stub types replace discontinued Spring.NET Framework assemblies:
	//   - Spring.Http.Converters.SpringJsonHttpMessageConverter (from Spring.Http.Converters.Json)
	//   - Spring.Http.Converters.ByteArrayHttpMessageConverter  (from Spring.Http.Converters)
	//
	// These stubs are defined within the Spring.Social.Salesforce.Api.Impl namespace alongside
	// SalesforceTemplate so that GetMessageConverters() and GetJsonMessageConverter() can reference
	// them without additional using directives. Both implement IHttpMessageConverter (defined in
	// AbstractSalesforceOperations.cs in the same namespace).
	//
	// The stubs satisfy compilation requirements but are NOT executed at runtime (dormant stub
	// pattern, AAP section 0.7.4). In a production integration, these would wrap the actual
	// Spring JSON serializer and binary response converters respectively.

	/// <summary>
	/// Stub replacement for <c>Spring.Http.Converters.Json.SpringJsonHttpMessageConverter</c>
	/// (from the discontinued Spring.Http.Converters.Json assembly).
	/// Wraps a <see cref="JsonMapper"/> instance to serialize/deserialize JSON API responses.
	/// Added to the RestTemplate message converters list by
	/// <see cref="SalesforceTemplate.GetMessageConverters()"/>.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class SpringJsonHttpMessageConverter : IHttpMessageConverter
	{
		/// <summary>
		/// Creates a new instance of <see cref="SpringJsonHttpMessageConverter"/> with the
		/// specified <see cref="JsonMapper"/> for JSON type dispatch.
		/// </summary>
		/// <param name="jsonMapper">
		/// The configured <see cref="JsonMapper"/> with all registered deserializers
		/// for Salesforce API response types.
		/// </param>
		public SpringJsonHttpMessageConverter(JsonMapper jsonMapper)
		{
			// Dormant stub — no-op. At runtime this would store the JsonMapper for use
			// during HTTP response deserialization.
		}
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Http.Converters.ByteArrayHttpMessageConverter</c>
	/// (from the discontinued Spring.Http.Converters assembly).
	/// Reads HTTP response bodies as raw byte arrays without transformation.
	/// Added to the RestTemplate message converters list by
	/// <see cref="SalesforceTemplate.GetMessageConverters()"/> before the JSON converter
	/// to handle binary response payloads (e.g., blob field data, document attachments).
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class ByteArrayHttpMessageConverter : IHttpMessageConverter
	{
		// Dormant stub — no-op implementation preserved for compilation compatibility.
		// At runtime, this would support reading byte[] responses with media type application/octet-stream.
	}

	#endregion
}
