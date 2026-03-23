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
// .NET 10 Migration: Dormant integration stub for HubSpot API template implementation.
// Spring.Social is discontinued. This class preserves the public API surface for
// Enterprise Edition upgrade path per AAP Section 0.8.1. Must compile but is NOT
// expected to execute.
using Spring.Social.HubSpot.Api;

namespace Spring.Social.HubSpot.Api.Impl
{
	/// <summary>
	/// Dormant stub implementation of the HubSpot API template.
	/// Implements IHubSpot to satisfy the compile-time interface contract for Enterprise Edition upgrade path.
	/// </summary>
	public class HubSpotTemplate : IHubSpot
	{
		public HubSpotTemplate(string accessToken)
		{
			IsAuthorized = true;
		}

		public bool IsAuthorized { get; private set; }

		public IContactOperations ContactOperations { get; } = new IContactOperations();

		public IRestOperations RestOperations { get; } = null;
	}
}
