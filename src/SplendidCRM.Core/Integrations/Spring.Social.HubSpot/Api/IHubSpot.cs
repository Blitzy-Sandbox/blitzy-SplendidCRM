/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>. 
 * 
 * You can contact SplendidCRM Software, Inc. at email address support@splendidcrm.com. 
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2011 SplendidCRM Software, Inc. All rights reserved."
 *********************************************************************************************************************/
// .NET 10 Migration: Removed discontinued Spring.Rest.Client and Spring.Http using directives.
// Replaced with System.Net.Http per AAP Section 0.7.4 — Spring.Rest.dll and Spring.Social.Core.dll
// have no .NET Core / .NET 10 equivalent. Stub interfaces IApiBinding and IRestOperations are
// defined locally to satisfy compile-time type references while preserving the public API surface
// for Enterprise Edition upgrade path. This is a dormant integration stub — must compile but
// is NOT expected to execute.
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Spring.Social.HubSpot.Api
{
	// Stub interface replacing Spring.Social.Api.IApiBinding — Spring.Social is discontinued.
	// Preserved for compilation compatibility per .NET 10 migration (AAP Section 0.7.4).
	public interface IApiBinding
	{
		bool IsAuthorized { get; }
	}

	// Stub interface replacing Spring.Rest.Client.IRestOperations — Spring.Rest is discontinued.
	// Preserved for compilation compatibility per .NET 10 migration (AAP Section 0.7.4).
	public interface IRestOperations
	{
	}

	public class IContactOperations
	{
		public IList<Contact> GetAll(string search)
		{
			throw(new Exception("not implemented"));
		}
	}

	public interface IHubSpot : IApiBinding
	{
		IContactOperations ContactOperations { get; }
		IRestOperations    RestOperations    { get; }
	}
}
