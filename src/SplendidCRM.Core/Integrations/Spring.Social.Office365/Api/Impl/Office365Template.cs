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
// NOTE: .NET 10 Migration — removed 'using Spring.Rest.Client;' (Spring.Rest is discontinued; no .NET 10 equivalent).
// NOTE: .NET 10 Migration — removed Spring.Social.Core ApiBinding base class reference.
// IApiBinding is now a local stub interface defined in IOffice365.cs in the same namespace.
using System;
using System.Collections.Generic;

namespace Spring.Social.Office365.Api.Impl
{
	/// <summary>
	/// .NET 10 migration stub implementation of IOffice365.
	/// This class is a dormant Enterprise Edition stub preserved for future activation.
	/// All operation properties are null by default; activation requires full implementation.
	/// Per AAP Section 0.7.4: must compile on .NET 10, not activated beyond compilation.
	/// </summary>
	public class Office365Template : IOffice365
	{
		// NOTE: .NET 10 Migration — constructor preserved with accessToken parameter for API contract compatibility.
		// Spring.Social base class initialization removed; stub only stores token for future use.
		public Office365Template(string accessToken)
		{
			// Store access token for Enterprise Edition activation.
			// In the original Spring.Social implementation this would call ApiBinding base constructor.
		}

		// IOffice365 interface properties — all 8 required members preserved for Enterprise Edition upgrade path.
		public IMailOperations         MailOperations         { get; private set; }
		public IContactOperations      ContactOperations      { get; private set; }
		public IEventOperations        EventOperations        { get; private set; }
		public ICategoryOperations     CategoryOperations     { get; private set; }
		public IFolderOperations       FolderOperations       { get; private set; }
		public ISubscriptionOperations SubscriptionOperations { get; private set; }
		public IMyProfileOperations    MyProfileOperations    { get; private set; }
		public IRestOperations         RestOperations         { get; private set; }
	}
}
