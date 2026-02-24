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

// .NET 10 Migration: Removed Spring.Http, Spring.Rest.Client using directives.
// Stub types (RestTemplate, HttpUtils, HttpMethod, etc.) are defined within
// AbstractSalesforceOperations.cs in the Spring.Social.Salesforce.Api.Impl namespace
// and are directly accessible without explicit using directives.
// This is a dormant integration stub — compiles but is NOT expected to execute at runtime.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Spring.Social.Salesforce.Api.Impl
{
	/// <summary>
	/// Implementation of <see cref="ISearchOperations"/> that wraps Salesforce SOQL/SOSL
	/// query and search operations via the REST API.
	/// Dormant stub — compiles on .NET 10 but is NOT executed at runtime.
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class SearchTemplate : AbstractSalesforceOperations, ISearchOperations
	{
		/// <summary>
		/// Creates a new instance of <see cref="SearchTemplate"/>.
		/// </summary>
		/// <param name="restTemplate">The REST template used for HTTP requests.</param>
		/// <param name="isAuthorized">Whether valid OAuth 2.0 credentials were provided.</param>
		public SearchTemplate(RestTemplate restTemplate, bool isAuthorized) : base(restTemplate, isAuthorized)
		{
		}

		#region ISearchOperations Members

		/// <summary>
		/// Executes a SOQL query against the Salesforce REST API and returns paginated results.
		/// </summary>
		/// <param name="version">The Salesforce API version (e.g. "28.0").</param>
		/// <param name="queryString">The SOQL query string.</param>
		/// <returns>A <see cref="QueryResult"/> containing the matching records.</returns>
		public QueryResult Query(string version, string queryString)
		{
			requireAuthorization();
			return restTemplate.GetForObject<QueryResult>("/services/data/v" + version + "/query/?q=" + HttpUtils.FormEncode(queryString));
		}

		/// <summary>
		/// Executes a SOQL queryAll (including deleted/archived records) against the Salesforce
		/// REST API, automatically paginating through all result pages.
		/// </summary>
		/// <param name="version">The Salesforce API version (e.g. "28.0").</param>
		/// <param name="queryString">The SOQL query string.</param>
		/// <returns>
		/// A <see cref="QueryResult"/> with <see cref="QueryResult.Done"/> set to <c>true</c>
		/// and all records aggregated from all pages.
		/// </returns>
		public QueryResult QueryAll(string version, string queryString)
		{
			requireAuthorization();
			QueryResult query = restTemplate.GetForObject<QueryResult>("/services/data/v" + version + "/query/?q=" + HttpUtils.FormEncode(queryString));
			if ( query != null && !query.Done && !String.IsNullOrEmpty(query.NextRecordsUrl) )
			{
				QueryResult next = null;
				do
				{
					next = this.QueryMore(query.NextRecordsUrl);
					if ( next != null && next.Records != null )
					{
						foreach ( SObject record in next.Records )
						{
							query.Records.Add(record);
						}
					}
				}
				while ( next != null && !next.Done && !String.IsNullOrEmpty(next.NextRecordsUrl) );
				query.Done           = true;
				query.NextRecordsUrl = String.Empty;
			}
			return query;
		}

		/// <summary>
		/// Retrieves the next page of results from a previously executed SOQL query.
		/// The <paramref name="queryLocator"/> is the URL returned in
		/// <see cref="QueryResult.NextRecordsUrl"/>.
		/// </summary>
		/// <param name="queryLocator">
		/// The next-page URL returned by a prior <see cref="Query"/> or <see cref="QueryAll"/> call.
		/// </param>
		/// <returns>A <see cref="QueryResult"/> containing the next page of records.</returns>
		public QueryResult QueryMore(string queryLocator)
		{
			requireAuthorization();
			return restTemplate.GetForObject<QueryResult>(queryLocator);
		}

		/// <summary>
		/// Executes a SOSL search query against the Salesforce REST API and returns matching records.
		/// </summary>
		/// <param name="version">The Salesforce API version (e.g. "28.0").</param>
		/// <param name="searchString">
		/// The SOSL search string. If it does not start with "FIND ", the prefix is prepended.
		/// </param>
		/// <returns>A list of <see cref="SObject"/> records matching the SOSL query.</returns>
		public List<SObject> Search(string version, string searchString)
		{
			requireAuthorization();
			// http://www.salesforce.com/us/developer/docs/api/Content/sforce_api_calls_sosl_find.htm
			if ( !searchString.StartsWith("FIND ") )
				searchString = "FIND " + searchString;
			return restTemplate.GetForObject<List<SObject>>("/services/data/v" + version + "/search/?q=" + HttpUtils.FormEncode(searchString));
		}

		#endregion
	}
}
