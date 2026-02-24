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
using System;
using System.Collections.Generic;
// NOTE: .NET 10 Migration — removed 'using Spring.Rest.Client;' (Spring.Rest is discontinued; no .NET 10 equivalent).
// IRestOperations and IApiBinding are defined as local stub interfaces below to satisfy compilation requirements
// and preserve the Enterprise Edition upgrade path per AAP Section 0.7.4.

namespace Spring.Social.Office365.Api
{
	// Stub interface replacing Spring.Rest.Client.IRestOperations (discontinued).
	// Spring.Rest has no .NET 10 equivalent; this empty stub preserves compilation and public interface contracts.
	public interface IRestOperations { }

	// Stub interface replacing Spring.Rest.Client.IApiBinding (discontinued).
	// Spring.Social.Core has no .NET 10 equivalent; this empty stub preserves compilation and public interface contracts.
	public interface IApiBinding { }

	public interface IOffice365 : IApiBinding
	{
		IMailOperations         MailOperations         { get; }
		IContactOperations      ContactOperations      { get; }
		IEventOperations        EventOperations        { get; }
		ICategoryOperations     CategoryOperations     { get; }
		IFolderOperations       FolderOperations       { get; }
		ISubscriptionOperations SubscriptionOperations { get; }
		IMyProfileOperations    MyProfileOperations    { get; }
		IRestOperations         RestOperations         { get; }
	}
}
