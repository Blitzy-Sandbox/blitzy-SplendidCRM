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
// .NET 10 Migration: SplendidCRM/_code/MassUpdate.cs → src/SplendidCRM.Core/MassUpdate.cs
// Changes applied:
//   - REMOVED: using System.Web.UI; using System.Web.UI.HtmlControls; using System.Web.UI.WebControls;
//              (all WebForms-only namespaces)
//   - REMOVED: using System.Data; using System.Threading; using System.Globalization;
//              (unused in MassUpdate — were framework defaults in original file)
//   - REMOVED: WebForms event model — OnInit(EventArgs e) override and InitializeComponent()
//              with Load event registration (this.Load += new EventHandler(this.Page_Load))
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//   - ADDED:   Constructor with DI parameters (IHttpContextAccessor, IMemoryCache) forwarded
//              to SplendidControl base class — replacing HttpContext.Current static access
//   - ADAPTED: Page_Load(object sender, EventArgs e) → public void Page_Load()
//              Signature changed from private WebForms event handler to public method called
//              explicitly from controller actions or lifecycle managers in .NET 10 pattern
//   - ADAPTED: OnInit(EventArgs e) → public override void OnInit()
//              Calls base.OnInit(); Load event registration removed (not applicable in .NET 10)
//   - PRESERVED: namespace SplendidCRM, class name MassUpdate, business logic in Page_Load,
//                IsMobile check (reads USER_SETTINGS/THEME from session via SplendidControl.IsMobile),
//                Visible assignment (SplendidControl.Visible property, default true)
//   - NOTE:   SplendidControl.Visible is a plain bool property (default true) that replaces
//             the System.Web.UI.Control.Visible property from the original WebForms base class
#nullable disable
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	// =====================================================================================
	// MassUpdate — Mobile-aware mass update control adapter
	//
	// BEFORE: public class MassUpdate : SplendidControl
	//         Inheriting from SplendidControl which inherited from System.Web.UI.UserControl.
	//         WebForms Load event registered via InitializeComponent() in OnInit(EventArgs e).
	//
	// AFTER:  public class MassUpdate : SplendidControl
	//         Inheriting from SplendidControl (migrated WebForms control adapter, no System.Web).
	//         DI constructor forwards IHttpContextAccessor and IMemoryCache to the base class.
	//         Page_Load() called explicitly; OnInit() is a public virtual override.
	//
	// DI Registration: services.AddScoped<MassUpdate>();
	//   (Scoped so that each request gets its own instance bound to the current HttpContext.)
	// =====================================================================================

	/// <summary>
	/// Mass update control adapter for SplendidCRM.
	/// 
	/// Migrated from SplendidCRM/_code/MassUpdate.cs for .NET 10 ASP.NET Core.
	/// Replaces System.Web.UI.UserControl inheritance (via SplendidControl) and all System.Web
	/// dependencies with ASP.NET Core DI-compatible equivalents.
	///
	/// BUSINESS RULE PRESERVED:
	///   MassUpdate is not displayed on a mobile browser (11/15/2007 Paul).
	///   When IsMobile is true, Visible is set to false to suppress rendering.
	///
	/// DESIGN NOTES:
	///   • Inherits from SplendidControl (the migrated WebForms control adapter).
	///   • Constructor forwards IHttpContextAccessor and IMemoryCache to the base class.
	///   • Page_Load() hides the control on mobile browsers (logic preserved from original).
	///   • OnInit() calls base.OnInit() for per-request localization/cache initialization.
	///   • Register as SCOPED so each HTTP request gets its own instance.
	/// </summary>
	public class MassUpdate : SplendidControl
	{
		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs a MassUpdate control with required DI dependencies.
		/// Both parameters are forwarded to the SplendidControl base class constructor.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current for session, request, response, and Items access.
		/// Used by SplendidControl.IsMobile to read USER_SETTINGS/THEME from session.
		/// Forwarded to the SplendidControl base class constructor.
		/// BEFORE: HttpContext.Current (static)
		/// AFTER:  IHttpContextAccessor (constructor-injected)
		/// </param>
		/// <param name="memoryCache">
		/// Replaces HttpApplicationState (Application[]) for cached configuration flags.
		/// Forwarded to the SplendidControl base class constructor.
		/// BEFORE: Application["key"] (static HttpApplicationState)
		/// AFTER:  IMemoryCache (constructor-injected)
		/// </param>
		public MassUpdate(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
			: base(httpContextAccessor, memoryCache)
		{
		}

		// =====================================================================================
		// Page lifecycle
		//
		// BEFORE: private void Page_Load(object sender, System.EventArgs e)
		//         Called automatically by the ASP.NET WebForms pipeline after the Load event.
		//         Registered via this.Load += new System.EventHandler(this.Page_Load) in
		//         InitializeComponent(), which was called from OnInit(EventArgs e).
		//
		// AFTER:  public void Page_Load()
		//         Called explicitly from controller actions, middleware, or lifecycle managers
		//         in the ASP.NET Core pipeline — no WebForms event subscription.
		//
		// BUSINESS LOGIC PRESERVED:
		//   11/15/2007 Paul.  MassUpdate is not displayed on a mobile browser.
		//   IsMobile reads USER_SETTINGS/THEME from session via SplendidControl.IsMobile.
		//   Visible is a plain bool property on SplendidControl (default true), replacing
		//   the System.Web.UI.Control.Visible property from the original WebForms base class.
		// =====================================================================================

		/// <summary>
		/// Handles the Page_Load lifecycle event for MassUpdate.
		/// Hides the MassUpdate control on mobile browsers.
		///
		/// BEFORE: private void Page_Load(object sender, System.EventArgs e)
		///         — called by WebForms pipeline via registered Load event handler
		/// AFTER:  public void Page_Load()
		///         — called explicitly in ASP.NET Core pipeline (no event args required)
		/// </summary>
		public void Page_Load()
		{
			// 11/15/2007 Paul.  MassUpdate is not displayed on a mobile browser. 
			if ( this.IsMobile )
				this.Visible = false;
		}

		// =====================================================================================
		// WebForms Designer generated code — adapted for .NET 10
		//
		// BEFORE: override protected void OnInit(EventArgs e)
		//         {
		//             InitializeComponent();
		//             base.OnInit(e);
		//         }
		//         private void InitializeComponent()
		//         {
		//             this.Load += new System.EventHandler(this.Page_Load);
		//         }
		//
		// AFTER:  public override void OnInit()
		//         — overrides the public virtual OnInit() from SplendidControl
		//         — calls base.OnInit() for per-request initialization (bDebug, L10n, T10n, C10n)
		//         — Load event registration removed (not applicable in ASP.NET Core .NET 10)
		//         — Page_Load() is called explicitly by the caller when needed
		// =====================================================================================

		/// <summary>
		/// Performs initialization for the MassUpdate control.
		/// Calls base.OnInit() to initialize per-request debug flags and localization objects.
		///
		/// BEFORE: override protected void OnInit(EventArgs e) — invoked by WebForms lifecycle
		///         Registered Page_Load handler via Load event (this.Load += new EventHandler(...))
		/// AFTER:  Call explicitly from controller action filters or lifecycle managers.
		///         Load event registration removed — Page_Load() is called explicitly in .NET 10.
		/// </summary>
		public override void OnInit()
		{
			//
			// CODEGEN: This call is required by the ASP.NET Web Form Designer.
			// MIGRATION: InitializeComponent() removed — no Load event in ASP.NET Core.
			// base.OnInit() initializes bDebug, L10n, T10n, C10n for the request.
			//
			base.OnInit();
		}
	}
}
