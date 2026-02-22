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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Data.Common;
using System.Collections;
using System.Net;
using System.Net.Mail;
using System.Diagnostics;
using System.Collections.Generic;
// .NET 10 Migration: Replaced System.Web (HttpApplicationState) with Microsoft.Extensions.Caching.Memory (IMemoryCache)
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Workflow engine initialization stub. This is a dormant Community Edition placeholder
	/// preserved for Enterprise Edition upgrade path compatibility.
	/// Migrated from .NET Framework 4.8 to .NET 10: HttpApplicationState replaced with IMemoryCache.
	/// </summary>
	public class WorkflowInit
	{
		/// <summary>
		/// Starts the workflow runtime engine.
		/// Community Edition stub — no-op implementation preserved per minimal change clause.
		/// </summary>
		/// <param name="Application">IMemoryCache instance replacing HttpApplicationState for application-level state.</param>
		public static void StartRuntime(IMemoryCache Application)
		{
		}

		/// <summary>
		/// Stops the workflow runtime engine.
		/// Community Edition stub — no-op implementation preserved per minimal change clause.
		/// </summary>
		/// <param name="Application">IMemoryCache instance replacing HttpApplicationState for application-level state.</param>
		public static void StopRuntime(IMemoryCache Application)
		{
		}
	}

	/// <summary>
	/// Splendid persistence service for workflow timer-based operations.
	/// Community Edition stub — no-op implementation preserved per minimal change clause.
	/// </summary>
	public class SplendidPersistenceService 
	{
		/// <summary>
		/// Timer callback for persistence service operations.
		/// Community Edition stub — no-op implementation preserved per minimal change clause.
		/// </summary>
		/// <param name="sender">The timer event source.</param>
		public void OnTimer(Object sender)
		{
		}
	}
}
