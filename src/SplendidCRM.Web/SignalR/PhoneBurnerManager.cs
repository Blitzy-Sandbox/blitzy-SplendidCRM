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
using System.Data;
using System.Data.Common;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Summary description for PhoneBurnerManager.
	/// PhoneBurner SignalR business logic service — migrated from .NET Framework 4.8 OWIN SignalR.
	/// The WebForms-specific RegisterScripts method has been removed (no ASP.NET Core equivalent for ScriptManager).
	/// This class preserves the public API surface as a DI-registered service for future Enterprise Edition
	/// PhoneBurner integration activation.
	/// </summary>
	public class PhoneBurnerManager
	{
		private readonly IHttpContextAccessor        _httpContextAccessor;
		private readonly IMemoryCache                _memoryCache        ;
		private readonly IHubContext<PhoneBurnerHub>  _hubContext         ;

		/// <summary>
		/// Constructor for dependency injection. Replaces the legacy static singleton pattern
		/// and OWIN GlobalHost.ConnectionManager.GetHubContext pattern from .NET Framework 4.8.
		/// </summary>
		/// <param name="httpContextAccessor">Provides access to HttpContext, replacing HttpContext.Current static access.</param>
		/// <param name="memoryCache">In-memory cache replacing HttpRuntime.Cache and Application[] state.</param>
		/// <param name="hubContext">SignalR hub context for PhoneBurnerHub, replacing OWIN GlobalHost.ConnectionManager.</param>
		public PhoneBurnerManager(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IHubContext<PhoneBurnerHub> hubContext)
		{
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
			_memoryCache         = memoryCache         ?? throw new ArgumentNullException(nameof(memoryCache));
			_hubContext          = hubContext           ?? throw new ArgumentNullException(nameof(hubContext));
		}
	}
}
