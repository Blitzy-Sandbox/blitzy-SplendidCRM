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
// Method bodies preserved from original source; FetchObject<T> is defined on AbstractSalesforceOperations.
// This is a dormant integration stub — compiles but is NOT expected to execute at runtime.

using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Spring.Social.Salesforce.Api.Impl
{
	/// <summary>
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class MetadataTemplate : AbstractSalesforceOperations, IMetadataOperations
	{
		public MetadataTemplate(RestTemplate restTemplate, bool isAuthorized) : base(restTemplate, isAuthorized)
		{
		}

		#region IMetadataOperations Members
		/// <summary>
		/// Lists the available objects and their metadata for your organization's data.
		/// </summary>
		/// <param name="version">Version number.</param>
		/// <returns>Globals object.</returns>
		public DescribeGlobal DescribeGlobal(string version)
		{
			requireAuthorization();
			return FetchObject<DescribeGlobal>("/services/data/v" + version + "/sobjects/");
		}

		/// <summary>
		/// Completely describes the individual metadata at all levels for the specified object.
		/// </summary>
		/// <param name="version">Version number.</param>
		/// <param name="name">SObject name.</param>
		/// <returns>Globals object.</returns>
		public DescribeSObject DescribeSObject(string version, string name)
		{
			requireAuthorization();
			return FetchObject<DescribeSObject>("/services/data/v" + version + "/sobjects/" + name + "/describe/");
		}
		#endregion
	}
}
