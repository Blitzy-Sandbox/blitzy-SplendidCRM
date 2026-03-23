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

// .NET 10 Migration: Removed Spring.Json, Spring.Http, Spring.Rest.Client using directives.
// These Spring.NET Framework assemblies (discontinued) are unavailable on .NET 10.
// RestTemplate stub is defined in AbstractSalesforceOperations (same namespace).
// SalesforceVersion and SalesforceResources are resolved via the enclosing parent namespace
// (Spring.Social.Salesforce.Api) without explicit using directives per C# namespace lookup rules.
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
	class VersionTemplate : AbstractSalesforceOperations, IVersionOperations
	{
		public VersionTemplate(RestTemplate restTemplate, bool isAuthorized) : base(restTemplate, isAuthorized)
		{
		}

		#region IMetadataOperations Members
		public List<SalesforceVersion> GetVersions()
		{
			return FetchConnections<SalesforceVersion>("/services/data/", null);
		}

		public SalesforceResources GetResourcesByVersion(string version)
		{
			return FetchObject<SalesforceResources>("/services/data/v" + version + "/");
		}
		#endregion
	}
}
