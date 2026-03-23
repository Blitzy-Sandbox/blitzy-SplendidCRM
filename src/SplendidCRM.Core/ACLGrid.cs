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

namespace SplendidCRM
{
	/// <summary>
	/// ACL access level constants used throughout the CRM for module, field, and record-level
	/// access control enforcement. These integer constants map to the ACL_ACCESS values stored
	/// in the database and are consumed by Security.cs, RestUtil.cs, and authorization handlers.
	/// </summary>
	/// <remarks>
	/// Migrated from SplendidCRM/_code/ACLGrid.cs (.NET Framework 4.8 → .NET 10).
	/// The WebForms UI classes (CreateHeaderTemplateACL, CreateItemTemplateACL, ACLGrid)
	/// that were conditionally compiled under #if !ReactOnlyUI have been removed as they
	/// depend on System.Web.UI which is not available in ASP.NET Core. Only the ACL_ACCESS
	/// constants class is retained — it has no framework dependencies and is used by the
	/// 4-tier ACL model (Module → Team → Field → Record).
	/// </remarks>
	public class ACL_ACCESS
	{
		// 09/26/2017 Paul.  Add Archive access right. 
		public const int FULL_ACCESS = 100;
		public const int ARCHIVE     =  91;
		public const int VIEW        =  90;
		public const int ALL         =  90;
		public const int ENABLED     =  89;
		public const int OWNER       =  75;
		public const int DISABLED    = -98;
		public const int NONE        = -99;
	}
}
