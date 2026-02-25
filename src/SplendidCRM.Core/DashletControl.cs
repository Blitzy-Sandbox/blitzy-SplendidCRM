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
// .NET 10 Migration: SplendidCRM/_code/DashletControl.cs → src/SplendidCRM.Core/DashletControl.cs
// Changes applied:
//   - REMOVED: using System.Web;                      (.NET Framework WebForms namespace — not available in .NET 10)
//   - REMOVED: using System.Web.UI.WebControls;       (.NET Framework WebForms controls — not available in .NET 10)
//   - REMOVED: SplendidControl WebForms base class inheritance (System.Web.UI.UserControl)
//              DashletControl now inherits from the migrated SplendidControl standalone service class.
//   - ADDED:   using Microsoft.AspNetCore.Http;        (provides IHttpContextAccessor — replaces HttpContext.Current)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (provides IMemoryCache — replaces Application[] / HttpRuntime.Cache)
//   - ADDED:   Constructor with IHttpContextAccessor and IMemoryCache DI parameters, forwarded to
//              base(httpContextAccessor, memoryCache) to initialise the inherited SplendidControl fields.
//   - PRESERVED: namespace SplendidCRM
//   - PRESERVED: DashletID (Guid) property — identical public interface
//   - PRESERVED: DetailView (string) property — identical public interface
//   - PRESERVED: All protected backing fields (gDashletID, sDetailView) with identical names and types
//   - NOTE: Minimal change clause applied — only framework migration changes were made.
#nullable disable
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Base class for SplendidCRM dashlet controls.
	///
	/// Migrated from SplendidCRM/_code/DashletControl.cs for .NET 10 ASP.NET Core.
	///
	/// BEFORE: public class DashletControl : SplendidControl
	///         where SplendidControl : System.Web.UI.UserControl
	///         System.Web.UI.WebControls types available on base class.
	///
	/// AFTER:  public class DashletControl : SplendidControl
	///         where SplendidControl is a standalone DI-injectable service class.
	///         IHttpContextAccessor replaces HttpContext.Current.
	///         IMemoryCache replaces Application[] / HttpRuntime.Cache.
	///
	/// DI Registration: services.AddScoped&lt;DashletControl&gt;();
	///   (Scoped so that each request gets its own instance bound to the current HTTP context.)
	/// </summary>
	public class DashletControl : SplendidControl
	{
		// =====================================================================================
		// Protected backing fields — preserved from original source with identical names
		// and types for compatibility with any derived dashlet implementations.
		// =====================================================================================

		/// <summary>
		/// Backing field for the dashlet instance identifier.
		/// Preserved from original SplendidCRM/_code/DashletControl.cs with identical name and type.
		/// </summary>
		protected Guid   gDashletID ;

		/// <summary>
		/// Backing field for the detail view layout name used by this dashlet.
		/// Preserved from original SplendidCRM/_code/DashletControl.cs with identical name and type.
		/// </summary>
		protected string sDetailView;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs a DashletControl with required DI dependencies.
		/// Forwards IHttpContextAccessor and IMemoryCache to the base SplendidControl constructor.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current for session, request, response, and Items access.
		/// Forwarded to base SplendidControl via base(httpContextAccessor, memoryCache).
		/// BEFORE: System.Web.HttpContext.Current (static global)
		/// AFTER:  IHttpContextAccessor (constructor-injected)
		/// </param>
		/// <param name="memoryCache">
		/// Replaces HttpApplicationState (Application[]) and HttpRuntime.Cache for in-memory caching.
		/// Forwarded to base SplendidControl via base(httpContextAccessor, memoryCache).
		/// BEFORE: Application["key"] / HttpRuntime.Cache["key"]
		/// AFTER:  IMemoryCache.Get&lt;T&gt;("key")
		/// </param>
		public DashletControl(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
			: base(httpContextAccessor, memoryCache)
		{
		}

		// =====================================================================================
		// Public properties — preserved from original source with identical names, types,
		// accessors, and visibility for full API compatibility.
		// =====================================================================================

		/// <summary>
		/// Gets or sets the unique identifier for this dashlet instance.
		/// Preserved from original SplendidCRM/_code/DashletControl.cs with identical signature.
		/// </summary>
		public Guid DashletID
		{
			get { return gDashletID; }
			set { gDashletID = value; }
		}

		/// <summary>
		/// Gets or sets the detail view layout name used by this dashlet.
		/// Preserved from original SplendidCRM/_code/DashletControl.cs with identical signature.
		/// </summary>
		public string DetailView
		{
			get { return sDetailView; }
			set { sDetailView = value; }
		}
	}
}
