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
// .NET 10 Migration: SplendidCRM/_code/SplendidControl.cs → src/SplendidCRM.Core/SplendidControl.cs
// Changes applied:
//   - REMOVED: using System.Web.UI; using System.Web.UI.HtmlControls; using System.Web.UI.WebControls;
//              using AjaxControlToolkit; (all WebForms-only namespaces)
//   - REMOVED: System.Web.UI.UserControl base class inheritance
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//   - REPLACED: HttpContext.Current.Application["key"] → _memoryCache.Get<object>("key")
//   - REPLACED: Session["key"] → _httpContextAccessor.HttpContext?.Session.GetString("key")
//   - REPLACED: Context.Items["key"] / Page.Items["key"] → _httpContextAccessor.HttpContext?.Items["key"]
//   - REPLACED: Request["key"] → _httpContextAccessor.HttpContext?.Request.Query["key"]
//   - REPLACED: Response.Redirect("~/path") → httpContext.Response.Redirect("/path")
//   - REPLACED: Application["key"] → _memoryCache.Get<object>("key") / _memoryCache.TryGetValue()
//   - REPLACED: Currency.CreateCurrency(Application, id) → Currency.CreateCurrency(_memoryCache, id)
//   - REPLACED: new L10N(sCULTURE) → new L10N(sCULTURE, _memoryCache)
//   - REPLACED: TimeZone.CreateTimeZone(gTIMEZONE) → same (uses static ambient IMemoryCache)
//   - REPLACED: WF4ApprovalActivity.ApplyEditViewPostLoadEventRules(Application,...) → (_memoryCache,...)
//   - REPLACED: Page.Theme → Session?.GetString("USER_SETTINGS/THEME")
//   - REPLACED: Page as SplendidPage → injected SplendidPage _splendidPage (optional)
//   - REMOVED: HiddenField, PlaceHolder, HtmlTable, UpdatePanel, AjaxControlToolkit fields
//              (WebForms controls not applicable in ASP.NET Core ReactOnlyUI migration)
//   - REMOVED: #if !ReactOnlyUI blocks (WebForms dynamic control loading — not applicable)
//   - ADAPTED: AppendDetailViewRelationships(PlaceHolder, ...) → returns DataTable (no PlaceHolder)
//   - ADAPTED: AppendEditViewRelationships(PlaceHolder, ...) → returns int row count (no PlaceHolder)
//   - ADAPTED: AppendGridColumns(SplendidGrid, ...) → no-op (WebForms SplendidGrid not applicable)
//   - ADAPTED: SplendidDynamic.ApplyEditViewRules / ApplyDetailViewRules → no-ops in ReactOnlyUI
//   - PRESERVED: namespace SplendidCRM, all business logic, field names, session key names,
//                cache key names (Modules.*.ArchiveEnabled, Modules.*.StreamEnabled, CONFIG.*, etc.)
//   - ADDED: CommandEventHandler delegate type (was System.Web.UI.WebControls.CommandEventHandler)
//   - NOTE: CommandEventArgs class is defined in SplendidPage.cs (same namespace) — not redefined here
//   - NOTE: OnInit() changed from override void → public virtual void (no WebForms lifecycle)
//   - NOTE: RegisterClientScriptBlock() preserved as no-op for API compatibility
//   - NOTE: LoginRedirect() adapted for ASP.NET Core (~/path → /path, ReactOnlyUI → /React/Home)
//   - NOTE: IsOfflineClient check removed from LoginRedirect() — offline client not applicable in Core
#nullable disable
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	// =====================================================================================
	// CommandEventHandler delegate
	//
	// BEFORE: System.Web.UI.WebControls.CommandEventHandler (shipped with .NET Framework)
	// AFTER:  Local delegate defined in namespace SplendidCRM (no System.Web dependency)
	//
	// CommandEventArgs class is defined in SplendidPage.cs (same namespace SplendidCRM).
	// =====================================================================================

	/// <summary>
	/// Replacement for System.Web.UI.WebControls.CommandEventHandler.
	/// Preserves the event handler pattern for existing module code-behind callers.
	/// </summary>
	public delegate void CommandEventHandler(object sender, CommandEventArgs e);

	// =====================================================================================
	// SplendidControl — Base control adapter
	//
	// BEFORE: public class SplendidControl : System.Web.UI.UserControl
	// AFTER:  public class SplendidControl  (no base class — DI-injectable service)
	//
	// Original design: Base class for all SplendidCRM WebForms user controls providing
	// per-request localization, archive/stream flags, ACL helpers, and dynamic layout rendering.
	//
	// Migrated design: Standalone base service class providing the same API contract.
	// All WebForms lifecycle hooks (OnInit) are preserved as public virtual methods that can
	// be called explicitly from controller actions or service methods.
	//
	// DI Registration: services.AddScoped<SplendidControl>();
	//   (Scoped so that each request gets its own instance with the current
	//    IHttpContextAccessor-bound HttpContext and Session data.)
	// =====================================================================================

	/// <summary>
	/// Base control adapter for SplendidCRM.
	/// 
	/// Migrated from SplendidCRM/_code/SplendidControl.cs (~500 lines) for .NET 10 ASP.NET Core.
	/// Replaces System.Web.UI.UserControl inheritance and all System.Web dependencies with
	/// ASP.NET Core DI-compatible equivalents.
	///
	/// DESIGN NOTES:
	///   • Register as SCOPED so each HTTP request gets its own instance.
	///   • Session keys USER_SETTINGS/CULTURE, /TIMEZONE, /CURRENCY, /THEME are preserved.
	///   • Cache keys Modules.*.ArchiveEnabled, CONFIG.*, etc. are preserved identically.
	///   • SplendidPage is an optional dependency — can be null in non-page contexts.
	/// </summary>
	public class SplendidControl
	{
		// =====================================================================================
		// DI-injected fields
		// =====================================================================================

		/// <summary>
		/// Replaces HttpContext.Current throughout.
		/// BEFORE: HttpContext.Current.Session / .Request / .Response / .Items
		/// AFTER:  _httpContextAccessor.HttpContext?.Session / .Request / .Response / .Items
		/// </summary>
		protected readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Replaces HttpApplicationState (Application[]) throughout.
		/// BEFORE: Application["Modules.X.ArchiveEnabled"]
		/// AFTER:  _memoryCache.Get&lt;object&gt;("Modules.X.ArchiveEnabled")
		/// </summary>
		protected readonly IMemoryCache _memoryCache;

		/// <summary>
		/// ACL and authentication service.
		/// Used for IS_ADMIN check in LoginRedirect() and GetUserAccess() in relationship loading.
		/// </summary>
		protected readonly Security _security;

		/// <summary>
		/// Metadata caching hub.
		/// Used for ArchiveViewExists(), DetailViewRelationships(), UserDashlets(),
		/// EditViewRelationships(), TabGroups(), ModuleGroups().
		/// </summary>
		protected readonly SplendidCache _splendidCache;

		/// <summary>
		/// Base page adapter — optional.
		/// Used to delegate PrintView, SetMenu, SetAdminMenu to the parent page context.
		/// BEFORE: Page as SplendidPage
		/// AFTER:  injected _splendidPage (null when not in a page context)
		/// </summary>
		protected readonly SplendidPage _splendidPage;

		// =====================================================================================
		// Private helpers — per-request HttpContext accessors
		// =====================================================================================

		/// <summary>
		/// Gets the per-request ISession from the current HttpContext.
		/// Returns null when called outside an HTTP request.
		/// BEFORE: HttpSessionState Session property inherited from System.Web.UI.UserControl
		/// AFTER:  _httpContextAccessor.HttpContext?.Session
		/// </summary>
		protected ISession Session => _httpContextAccessor?.HttpContext?.Session;

		/// <summary>
		/// Gets the per-request Items dictionary from the current HttpContext.
		/// Replaces both Page.Items and Context.Items from WebForms.
		/// BEFORE: Context.Items["key"] / Page.Items["key"]
		/// AFTER:  _httpContextAccessor.HttpContext?.Items["key"]
		/// </summary>
		protected IDictionary<object, object> Items => _httpContextAccessor?.HttpContext?.Items;

		// =====================================================================================
		// Protected instance fields — preserved from original source with identical names
		// and access modifiers for derived-class compatibility.
		// =====================================================================================

		/// <summary>Whether to emit SQL debug output.</summary>
		protected bool     bDebug = false;

		/// <summary>Request-scoped localization instance.</summary>
		protected L10N     L10n;

		/// <summary>Request-scoped timezone instance.</summary>
		protected TimeZone T10n;

		/// <summary>Request-scoped currency instance.</summary>
		protected Currency C10n;

		/// <summary>The current module name — set by derived classes.</summary>
		protected string   m_sMODULE;  // 04/27/2006 Paul. Leave null so that we can get an error when not initialized.

		/// <summary>
		/// Override flag to force non-postback behavior regardless of HTTP method.
		/// BEFORE: Used to override WebForms IsPostBack.
		/// AFTER:  Preserved as a configurable flag for derived class use.
		/// </summary>
		protected bool     m_bNotPostBack = false;  // 05/06/2010 Paul. Use a special Page flag to override the default IsPostBack behavior.

		// 11/10/2010 Paul.  The RulesRedirectURL is used by the Rules Engine to allow a redirect after an event.
		/// <summary>Redirect URL set by the Rules Engine after an event fires.</summary>
		protected string m_sRulesRedirectURL ;
		/// <summary>Error message accumulated by Rules Engine validation failures.</summary>
		protected string m_sRulesErrorMessage;
		/// <summary>Whether all currently applied rules are valid.</summary>
		protected bool   m_bRulesIsValid      = true;

		// 11/11/2010 Paul.  We need to access the layout views from a hidden field so that it can be accessed inside OnInit.
		// MIGRATION: HiddenField removed — WebForms ViewState not applicable in ASP.NET Core.
		// Layout view names are backed by plain string fields only.
		/// <summary>Backing field for the list view layout name.</summary>
		protected string m_sLayoutListView  ;
		/// <summary>Backing field for the edit view layout name.</summary>
		protected string m_sLayoutEditView  ;
		/// <summary>Backing field for the detail view layout name.</summary>
		protected string m_sLayoutDetailView;

		// 10/10/2015 Paul.  We need a quick way to return stream enabled status.
		/// <summary>Nullable backing field for the StreamEnabled flag (lazy-loaded from cache).</summary>
		protected bool?  m_bStreamEnabled;
		// 09/26/2017 Paul.  Add Archive access right.
		/// <summary>Nullable backing field for whether the archive view is active on this request.</summary>
		protected bool?  m_bArchiveView      ;
		/// <summary>Nullable backing field for whether archiving is enabled for the current module.</summary>
		protected bool?  m_bArchiveEnabled   ;
		/// <summary>
		/// The view name used by ArchiveViewExists() to look up the archive view in SplendidCache.
		/// Set by derived classes, typically the DetailView layout name (e.g. "Accounts.DetailView").
		/// </summary>
		protected string m_sVIEW_NAME        ;
		/// <summary>Nullable backing field for whether the archive view exists in SplendidCache.</summary>
		protected bool?  m_bArchiveViewExists;
		// 11/01/2017 Paul.  Use a module-based flag so that Record Level Security is only enabled when needed.
		/// <summary>Nullable backing field for whether Record Level Security is enabled for this module.</summary>
		protected bool?  m_bRecordLevelSecurity;
		// 11/23/2017 Paul.  Provide a way to globally disable favorites and following.
		/// <summary>Nullable backing field for the CONFIG.disable_favorites flag.</summary>
		protected bool?  m_bDisableFavorites;
		/// <summary>Nullable backing field for the CONFIG.disable_Following flag.</summary>
		protected bool?  m_bDisableFollowing;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs a SplendidControl service with required DI dependencies.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current for session, request, response, and Items access.
		/// </param>
		/// <param name="memoryCache">
		/// Replaces HttpApplicationState (Application[]) for cached configuration flags.
		/// </param>
		/// <param name="security">
		/// ACL service — used for IS_ADMIN check in LoginRedirect() and
		/// GetUserAccess() in relationship relationship loading.
		/// </param>
		/// <param name="splendidCache">
		/// Metadata cache hub — used for ArchiveViewExists(), DetailViewRelationships(), etc.
		/// </param>
		/// <param name="splendidPage">
		/// Optional parent page adapter — used for PrintView, SetMenu, SetAdminMenu delegation.
		/// Pass null when SplendidControl is used in non-page contexts.
		/// </param>
		public SplendidControl(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache         ,
			Security             security     = null ,
			SplendidCache        splendidCache = null,
			SplendidPage         splendidPage  = null)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
			_security            = security           ;
			_splendidCache       = splendidCache      ;
			_splendidPage        = splendidPage       ;
		}

		// =====================================================================================
		// Archive / stream / ACL flag methods
		// BEFORE: Application["Modules." + m_sMODULE + ".ArchiveEnabled"] → Application[]
		// AFTER:  _memoryCache.Get<object>("Modules." + m_sMODULE + ".ArchiveEnabled")
		// =====================================================================================

		/// <summary>
		/// Returns true when the current request includes ArchiveView=true query parameter.
		/// BEFORE: Sql.ToBoolean(Request["ArchiveView"])
		/// AFTER:  Sql.ToBoolean(httpContext?.Request.Query["ArchiveView"])
		/// </summary>
		public bool ArchiveView()
		{
			if ( !m_bArchiveView.HasValue )
			{
				// BEFORE: this.m_bArchiveView = Sql.ToBoolean(Request["ArchiveView"]);
				// AFTER:  Read from Query string via IHttpContextAccessor
				string sValue = _httpContextAccessor?.HttpContext?.Request?.Query["ArchiveView"].ToString();
				this.m_bArchiveView = Sql.ToBoolean(sValue);
			}
			return m_bArchiveView.Value;
		}

		/// <summary>
		/// Returns true when archiving is enabled for the current module.
		/// BEFORE: Sql.ToBoolean(Application["Modules." + m_sMODULE + ".ArchiveEnabled"])
		/// AFTER:  Sql.ToBoolean(_memoryCache.Get&lt;object&gt;("Modules." + m_sMODULE + ".ArchiveEnabled"))
		/// </summary>
		public bool ArchiveEnabled()
		{
			if ( !m_bArchiveEnabled.HasValue )
			{
				// BEFORE: this.m_bArchiveEnabled = Sql.ToBoolean(Application["Modules." + m_sMODULE + ".ArchiveEnabled"]);
				// AFTER:  _memoryCache replaces Application
				this.m_bArchiveEnabled = Sql.ToBoolean(_memoryCache?.Get<object>("Modules." + m_sMODULE + ".ArchiveEnabled"));
			}
			return m_bArchiveEnabled.Value;
		}

		/// <summary>
		/// Returns true when both ArchiveView and ArchiveEnabled are true.
		/// Convenience method combining ArchiveView() and ArchiveEnabled().
		/// </summary>
		public bool ArchiveViewEnabled()
		{
			return ArchiveView() && ArchiveEnabled();
		}

		/// <summary>
		/// Returns true when the archive view exists in the metadata cache for m_sVIEW_NAME.
		/// BEFORE: SplendidCache.ArchiveViewExists(m_sVIEW_NAME) [static]
		/// AFTER:  _splendidCache.ArchiveViewExists(m_sVIEW_NAME) [instance]
		/// </summary>
		public bool ArchiveViewExists()
		{
			if ( Sql.IsEmptyString(m_sVIEW_NAME) )
				return false;
			if ( !m_bArchiveViewExists.HasValue )
			{
				// BEFORE: this.m_bArchiveViewExists = ArchiveView() && SplendidCache.ArchiveViewExists(m_sVIEW_NAME);
				// AFTER:  Use injected _splendidCache instance
				this.m_bArchiveViewExists = ArchiveView() && (_splendidCache?.ArchiveViewExists(m_sVIEW_NAME) ?? false);
			}
			return m_bArchiveViewExists.Value;
		}

		/// <summary>
		/// Returns true when activity streams are enabled for the current module.
		/// BEFORE: Sql.ToBoolean(Application["Modules." + m_sMODULE + ".StreamEnabled"]) and Application["CONFIG.enable_activity_streams"]
		/// AFTER:  _memoryCache replaces Application
		/// </summary>
		public bool StreamEnabled()
		{
			if ( !m_bStreamEnabled.HasValue )
			{
				// BEFORE: this.m_bStreamEnabled = Sql.ToBoolean(Application["Modules." + m_sMODULE + ".StreamEnabled"])
				//                                  && Sql.ToBoolean(Application["CONFIG.enable_activity_streams"]);
				// AFTER:  _memoryCache replaces Application for both keys
				this.m_bStreamEnabled = Sql.ToBoolean(_memoryCache?.Get<object>("Modules." + m_sMODULE + ".StreamEnabled"))
				                     && Sql.ToBoolean(_memoryCache?.Get<object>("CONFIG.enable_activity_streams"));
			}
			return m_bStreamEnabled.Value;
		}

		/// <summary>
		/// Returns true when Record Level Security is enabled for the current module.
		/// BEFORE: Sql.ToBoolean(Application["Modules." + m_sMODULE + ".RecordLevelSecurity"])
		/// AFTER:  _memoryCache replaces Application
		/// </summary>
		// 11/01/2017 Paul.  Use a module-based flag so that Record Level Security is only enabled when needed.
		public bool RecordLevelSecurity()
		{
			if ( !m_bRecordLevelSecurity.HasValue )
			{
				// BEFORE: this.m_bRecordLevelSecurity = Sql.ToBoolean(Application["Modules." + m_sMODULE + ".RecordLevelSecurity"]);
				// AFTER:  _memoryCache replaces Application
				this.m_bRecordLevelSecurity = Sql.ToBoolean(_memoryCache?.Get<object>("Modules." + m_sMODULE + ".RecordLevelSecurity"));
			}
			return m_bRecordLevelSecurity.Value;
		}

		/// <summary>
		/// Returns true when favorites are globally disabled.
		/// BEFORE: Sql.ToBoolean(Application["CONFIG.disable_favorites"])
		/// AFTER:  _memoryCache replaces Application
		/// </summary>
		// 11/23/2017 Paul.  Provide a way to globally disable favorites and following.
		public bool DisableFavorites()
		{
			if ( !m_bDisableFavorites.HasValue )
			{
				// BEFORE: this.m_bDisableFavorites = Sql.ToBoolean(Application["CONFIG.disable_favorites"]);
				// AFTER:  _memoryCache replaces Application
				this.m_bDisableFavorites = Sql.ToBoolean(_memoryCache?.Get<object>("CONFIG.disable_favorites"));
			}
			return m_bDisableFavorites.Value;
		}

		/// <summary>
		/// Returns true when following (activity stream subscriptions) is globally disabled.
		/// BEFORE: Sql.ToBoolean(Application["CONFIG.disable_Following"])
		/// AFTER:  _memoryCache replaces Application
		/// </summary>
		public bool DisableFollowing()
		{
			if ( !m_bDisableFollowing.HasValue )
			{
				// BEFORE: this.m_bDisableFollowing = Sql.ToBoolean(Application["CONFIG.disable_Following"]);
				// AFTER:  _memoryCache replaces Application
				this.m_bDisableFollowing = Sql.ToBoolean(_memoryCache?.Get<object>("CONFIG.disable_Following"));
			}
			return m_bDisableFollowing.Value;
		}

		// =====================================================================================
		// Rules Engine properties
		// Preserved from original source without change — these are plain C# properties.
		// =====================================================================================

		/// <summary>Gets or sets the redirect URL set by the Rules Engine after an event.</summary>
		// 11/10/2010 Paul.  The RulesRedirectURL is used by the Rules Engine to allow a redirect after an event.
		public string RulesRedirectURL
		{
			get { return m_sRulesRedirectURL; }
			set { m_sRulesRedirectURL = value; }
		}

		/// <summary>
		/// Gets or sets the Rules Engine error message.
		/// Setting this property appends to any existing error message and sets RulesIsValid = false.
		/// </summary>
		public string RulesErrorMessage
		{
			get
			{
				return m_sRulesErrorMessage;
			}
			set
			{
				// 04/28/2011 Paul.  Allow multiple error messages by concatenating.
				m_sRulesErrorMessage += value;
				m_bRulesIsValid = false;
			}
		}

		/// <summary>Gets or sets whether all currently applied Rules Engine rules are valid.</summary>
		public bool RulesIsValid
		{
			get { return m_bRulesIsValid; }
			set { m_bRulesIsValid = value; }
		}

		// =====================================================================================
		// Layout view properties
		// BEFORE: LayoutListView read from HiddenField.Value (with IsTrackingViewState check)
		//         or from Request[LAYOUT_LIST_VIEW.UniqueID]
		// AFTER:  Simple string property backed by m_sLayoutListView.
		//         HiddenField and ViewState patterns removed — not applicable in ASP.NET Core.
		// =====================================================================================

		/// <summary>
		/// Gets or sets the list view layout name.
		/// Defaults to "ListView" when not explicitly set.
		/// BEFORE: Read from HiddenField / Request (WebForms ViewState support)
		/// AFTER:  Simple string field with "ListView" default
		/// </summary>
		public string LayoutListView
		{
			get
			{
				if ( String.IsNullOrEmpty(m_sLayoutListView) )
					m_sLayoutListView = "ListView";
				return m_sLayoutListView;
			}
			set
			{
				m_sLayoutListView = value;
			}
		}

		/// <summary>
		/// Gets or sets the edit view layout name.
		/// Defaults to "EditView" when not explicitly set.
		/// BEFORE: Read from HiddenField / Request (WebForms ViewState support)
		/// AFTER:  Simple string field with "EditView" default
		/// </summary>
		public string LayoutEditView
		{
			get
			{
				if ( String.IsNullOrEmpty(m_sLayoutEditView) )
					m_sLayoutEditView = "EditView";
				return m_sLayoutEditView;
			}
			set
			{
				m_sLayoutEditView = value;
			}
		}

		/// <summary>
		/// Gets or sets the detail view layout name.
		/// Defaults to "DetailView" when not explicitly set.
		/// BEFORE: Read from HiddenField / Request (WebForms ViewState support)
		/// AFTER:  Simple string field with "DetailView" default
		/// </summary>
		public string LayoutDetailView
		{
			get
			{
				if ( String.IsNullOrEmpty(m_sLayoutDetailView) )
					m_sLayoutDetailView = "DetailView";
				return m_sLayoutDetailView;
			}
			set
			{
				m_sLayoutDetailView = value;
			}
		}

		// =====================================================================================
		// Menu and page state helpers
		// =====================================================================================

		/// <summary>
		/// Gets whether the current request uses the mobile theme.
		/// BEFORE: return (Page.Theme == "Mobile");  — read from WebForms Page.Theme
		/// AFTER:  Read USER_SETTINGS/THEME from session (same data as SplendidPage.IsMobile)
		/// </summary>
		public bool IsMobile
		{
			get
			{
				// BEFORE: return (Page.Theme == "Mobile");
				// AFTER:  Read from USER_SETTINGS/THEME session key
				string sTheme = Sql.ToString(Session?.GetString("USER_SETTINGS/THEME"));
				return sTheme == "Mobile";
			}
		}

		/// <summary>
		/// Gets or sets whether the current request is a print-friendly view.
		/// Delegates to the injected SplendidPage instance when available.
		/// Falls back to HttpContext.Items["PrintView"] when SplendidPage is not injected.
		/// BEFORE: delegated to (Page as SplendidPage).PrintView
		/// AFTER:  delegates to _splendidPage?.PrintView OR Items["PrintView"]
		/// </summary>
		public bool PrintView
		{
			get
			{
				if ( _splendidPage != null )
					return _splendidPage.PrintView;
				// BEFORE: return false;
				// AFTER:  Check Items["PrintView"] as fallback when no SplendidPage is injected
				return Sql.ToBoolean(Items?["PrintView"]);
			}
			set
			{
				if ( _splendidPage != null )
					_splendidPage.PrintView = value;
				// Also write to Items so downstream middleware can read PrintView without SplendidPage
				if ( Items != null )
					Items["PrintView"] = value;
			}
		}

		/// <summary>
		/// Gets or sets the override flag to force non-postback behavior.
		/// BEFORE: Used to override WebForms IsPostBack behavior.
		/// AFTER:  Preserved as configurable flag for derived class logic.
		/// </summary>
		public bool NotPostBack
		{
			get { return m_bNotPostBack; }
			set { m_bNotPostBack = value; }
		}

		/// <summary>
		/// Gets or sets the active tab index for the current view, stored in session.
		/// Session key: Items["DETAIL_NAME"] + ".ActiveTabIndex"
		/// BEFORE: Session[sDETAIL_NAME + ".ActiveTabIndex"] in tc_ActiveTabChanged / AppendDetailViewRelationships
		/// AFTER:  Same session key pattern via ISession.GetString / SetString
		/// </summary>
		public int ActiveTabIndex
		{
			get
			{
				// Use the DETAIL_NAME stored in Items (set during AppendDetailViewRelationships)
				string sDETAIL_NAME = Sql.ToString(Items?["DETAIL_NAME"]);
				if ( Sql.IsEmptyString(sDETAIL_NAME) )
					return 0;
				// BEFORE: Sql.ToInteger(Session[sDETAIL_NAME + ".ActiveTabIndex"])
				// AFTER:  Sql.ToInteger(Session?.GetString(sDETAIL_NAME + ".ActiveTabIndex"))
				return Sql.ToInteger(Session?.GetString(sDETAIL_NAME + ".ActiveTabIndex"));
			}
			set
			{
				string sDETAIL_NAME = Sql.ToString(Items?["DETAIL_NAME"]);
				if ( !Sql.IsEmptyString(sDETAIL_NAME) )
				{
					// BEFORE: Session[sDETAIL_NAME + ".ActiveTabIndex"] = value.ToString();
					// AFTER:  Session?.SetString(...)
					Session?.SetString(sDETAIL_NAME + ".ActiveTabIndex", value.ToString());
				}
			}
		}

		/// <summary>
		/// Records the active module name in the per-request Items dictionary.
		/// Delegates to the injected SplendidPage instance when available.
		/// BEFORE: (Page as SplendidPage).SetMenu(sMODULE)
		/// AFTER:  _splendidPage?.SetMenu(sMODULE) OR Items["ActiveTabMenu"] = sMODULE
		/// </summary>
		// 01/20/2007 Paul.  Move code to SplendidPage.
		protected void SetMenu(string sMODULE)
		{
			if ( _splendidPage != null )
				_splendidPage.SetMenu(sMODULE);
			else if ( Items != null )
				Items["ActiveTabMenu"] = sMODULE;
		}

		/// <summary>
		/// Records the active admin module name and sets an admin flag.
		/// BEFORE: (Page as SplendidPage).SetAdminMenu(sMODULE)
		/// AFTER:  _splendidPage?.SetAdminMenu(sMODULE) OR Items["ActiveTabMenu"]/["ActiveTabMenu.IsAdmin"]
		/// </summary>
		// 07/24/2010 Paul.  We need an admin flag for the areas that don't have a record in the Modules table.
		protected void SetAdminMenu(string sMODULE)
		{
			if ( _splendidPage != null )
				_splendidPage.SetAdminMenu(sMODULE);
			else if ( Items != null )
			{
				Items["ActiveTabMenu"]         = sMODULE;
				Items["ActiveTabMenu.IsAdmin"] = true;
			}
		}

		// =====================================================================================
		// Localization, timezone, and currency initialization
		// BEFORE: Context.Items["L10n/T10n/C10n"] → stored/read via WebForms Context.Items
		//         Session["USER_SETTINGS/CULTURE/TIMEZONE/CURRENCY"] → WebForms Session
		//         Application["..."] → WebForms HttpApplicationState
		// AFTER:  Items?["L10n/T10n/C10n"] via IHttpContextAccessor
		//         Session?.GetString("USER_SETTINGS/CULTURE/TIMEZONE/CURRENCY")
		//         _memoryCache passed to factory methods
		// =====================================================================================

		/// <summary>
		/// Returns the per-request timezone instance, creating it from session if not yet initialized.
		/// BEFORE: Context.Items["T10n"] as TimeZone → Session["USER_SETTINGS/TIMEZONE"] → TimeZone.CreateTimeZone(gTIMEZONE)
		/// AFTER:  Items?["T10n"] as TimeZone → Session?.GetString("USER_SETTINGS/TIMEZONE") → TimeZone.CreateTimeZone(gTIMEZONE) [uses static ambient]
		/// </summary>
		public TimeZone GetT10n()
		{
			// 08/30/2005 Paul.  Attempt to get the T10n object from the parent page.
			// If that fails, then just create it because it is required.
			if ( T10n == null )
			{
				// 04/30/2006 Paul.  Use the Context to store pointers to the localization objects.
				// BEFORE: T10n = Context.Items["T10n"] as TimeZone;
				// AFTER:  T10n = Items?["T10n"] as TimeZone;
				T10n = Items?["T10n"] as TimeZone;
				if ( T10n == null )
				{
					// BEFORE: Guid gTIMEZONE = Sql.ToGuid(Session["USER_SETTINGS/TIMEZONE"]);
					// AFTER:  Guid gTIMEZONE = Sql.ToGuid(Session?.GetString("USER_SETTINGS/TIMEZONE"));
					Guid gTIMEZONE = Sql.ToGuid(Session?.GetString("USER_SETTINGS/TIMEZONE"));
					// TimeZone.CreateTimeZone(Guid) uses static ambient IMemoryCache registered at startup.
					T10n = TimeZone.CreateTimeZone(gTIMEZONE);
				}
			}
			return T10n;
		}

		/// <summary>
		/// Returns the per-request localization instance, creating it from session if not yet initialized.
		/// BEFORE: Context.Items["L10n"] as L10N → Session["USER_SETTINGS/CULTURE"] → new L10N(sCULTURE)
		/// AFTER:  Items?["L10n"] as L10N → Session?.GetString("USER_SETTINGS/CULTURE") → new L10N(sCULTURE, _memoryCache)
		/// </summary>
		public L10N GetL10n()
		{
			// 08/30/2005 Paul.  Attempt to get the L10n object from the parent page.
			// If that fails, then just create it because it is required.
			if ( L10n == null )
			{
				// BEFORE: L10n = Context.Items["L10n"] as L10N;
				// AFTER:  L10n = Items?["L10n"] as L10N;
				L10n = Items?["L10n"] as L10N;
				if ( L10n == null )
				{
					// BEFORE: string sCULTURE = Sql.ToString(Session["USER_SETTINGS/CULTURE"]);
					//         L10n = new L10N(sCULTURE);
					// AFTER:  L10N constructor requires IMemoryCache parameter in .NET 10
					string sCULTURE = Sql.ToString(Session?.GetString("USER_SETTINGS/CULTURE"));
					L10n = new L10N(sCULTURE, _memoryCache);
				}
			}
			return L10n;
		}

		/// <summary>
		/// Returns the per-request currency instance, creating it from session if not yet initialized.
		/// BEFORE: Context.Items["C10n"] as Currency → Session["USER_SETTINGS/CURRENCY"] → Currency.CreateCurrency(Application, gCURRENCY_ID)
		/// AFTER:  Items?["C10n"] as Currency → Session?.GetString("USER_SETTINGS/CURRENCY") → Currency.CreateCurrency(_memoryCache, gCURRENCY_ID)
		/// </summary>
		public Currency GetC10n()
		{
			// 05/09/2006 Paul.  Attempt to get the C10n object from the parent page.
			// If that fails, then just create it because it is required.
			if ( C10n == null )
			{
				// BEFORE: C10n = Context.Items["C10n"] as Currency;
				// AFTER:  C10n = Items?["C10n"] as Currency;
				C10n = Items?["C10n"] as Currency;
				if ( C10n == null )
				{
					// BEFORE: Guid gCURRENCY_ID = Sql.ToGuid(Session["USER_SETTINGS/CURRENCY"]);
					//         C10n = Currency.CreateCurrency(Application, gCURRENCY_ID);
					// AFTER:  IMemoryCache replaces Application
					Guid gCURRENCY_ID = Sql.ToGuid(Session?.GetString("USER_SETTINGS/CURRENCY"));
					// 04/30/2016 Paul.  Require the Application so that we can get the base currency.
					C10n = Currency.CreateCurrency(_memoryCache, gCURRENCY_ID);
				}
			}
			return C10n;
		}

		/// <summary>
		/// Sets the currency for the current request to the specified currency ID.
		/// Also updates Thread.CurrentCulture.NumberFormat.CurrencySymbol.
		/// BEFORE: Currency.CreateCurrency(Application, gCURRENCY_ID)
		/// AFTER:  Currency.CreateCurrency(_memoryCache, gCURRENCY_ID)
		/// </summary>
		protected void SetC10n(Guid gCURRENCY_ID)
		{
			// BEFORE: C10n = Currency.CreateCurrency(Application, gCURRENCY_ID);
			// AFTER:  IMemoryCache replaces Application
			// 04/30/2016 Paul.  Require the Application so that we can get the base currency.
			C10n = Currency.CreateCurrency(_memoryCache, gCURRENCY_ID);
			// 07/28/2006 Paul.  We cannot set the CurrencySymbol directly on Mono as it is read-only.
			// Just clone the culture and modify the clone.
			CultureInfo culture = Thread.CurrentThread.CurrentCulture.Clone() as CultureInfo;
			culture.NumberFormat.CurrencySymbol   = C10n.SYMBOL;
			Thread.CurrentThread.CurrentCulture   = culture;
			Thread.CurrentThread.CurrentUICulture = culture;
		}

		/// <summary>
		/// Sets the currency for the current request to the specified currency ID and conversion rate override.
		/// BEFORE: Currency.CreateCurrency(Application, gCURRENCY_ID, fCONVERSION_RATE)
		/// AFTER:  Currency.CreateCurrency(_memoryCache, gCURRENCY_ID, fCONVERSION_RATE)
		/// </summary>
		protected void SetC10n(Guid gCURRENCY_ID, float fCONVERSION_RATE)
		{
			// BEFORE: C10n = Currency.CreateCurrency(Application, gCURRENCY_ID, fCONVERSION_RATE);
			// AFTER:  IMemoryCache replaces Application
			// 04/30/2016 Paul.  Require the Application so that we can get the base currency.
			C10n = Currency.CreateCurrency(_memoryCache, gCURRENCY_ID, fCONVERSION_RATE);
			CultureInfo culture = Thread.CurrentThread.CurrentCulture.Clone() as CultureInfo;
			culture.NumberFormat.CurrencySymbol   = C10n.SYMBOL;
			Thread.CurrentThread.CurrentCulture   = culture;
			Thread.CurrentThread.CurrentUICulture = culture;
		}

		// =====================================================================================
		// Lifecycle / initialization
		// BEFORE: protected override void OnInit(EventArgs e) — called by WebForms page lifecycle
		// AFTER:  public virtual void OnInit()               — called explicitly by controller/filter
		// =====================================================================================

		/// <summary>
		/// Performs per-request initialization: sets bDebug flag and initializes L10n/T10n/C10n.
		/// BEFORE: protected override void OnInit(EventArgs e) — invoked by WebForms lifecycle
		/// AFTER:  Call explicitly from controller action filters or base class constructors
		/// </summary>
		public virtual void OnInit()
		{
			// BEFORE: bDebug = Sql.ToBoolean(Application["CONFIG.show_sql"]);
			// AFTER:  _memoryCache replaces Application
			// 11/27/2006 Paul.  We want to show the SQL on the Demo sites, so add a config variable to allow it.
			bDebug = Sql.ToBoolean(_memoryCache?.Get<object>("CONFIG.show_sql"));
#if DEBUG
			bDebug = true;
#endif
			GetL10n();
			GetT10n();
			GetC10n();
		}

		/// <summary>
		/// No-op — preserves API compatibility.
		/// BEFORE: Page.ClientScript.RegisterClientScriptBlock(Type, key, script)  (WebForms)
		/// AFTER:  No-op — client script injection is handled by the React SPA
		/// </summary>
		public void RegisterClientScriptBlock(string key, string script)
		{
			// No-op: Page.ClientScript not available in ASP.NET Core.
			// Script injection is handled by the React SPA in the ReactOnlyUI migration.
		}

		/// <summary>
		/// Redirects to the appropriate page after successful login.
		/// BEFORE: Used Response.Redirect("~/path") with Application[] for default module,
		///         IsOfflineClient check, and conditional ReactOnlyUI blocks.
		/// AFTER:  Uses httpContext.Response.Redirect("/path") with _memoryCache for config,
		///         IsOfflineClient check removed (not applicable in ASP.NET Core),
		///         ReactOnlyUI: always redirects to /React/Home
		/// </summary>
		// 02/22/2011 Paul.  The login redirect is also needed after the change password.
		protected void LoginRedirect()
		{
			var httpContext = _httpContextAccessor?.HttpContext;
			if ( httpContext == null ) return;

			// BEFORE: string sDefaultModule = Sql.ToString(Application["CONFIG.default_module"]);
			// AFTER:  _memoryCache replaces Application
			string sDefaultModule = Sql.ToString(_memoryCache?.Get<object>("CONFIG.default_module"));

			// BEFORE: string sRedirect = Sql.ToString(Request["Redirect"]);
			// AFTER:  Read from Query string via IHttpContextAccessor
			// 05/22/2008 Paul.  Check for redirect.
			string sRedirect = Sql.ToString(httpContext.Request.Query["Redirect"].ToString());

			// 05/22/2008 Paul.  Only allow virtual relative paths.
			if ( sRedirect.StartsWith("~/") )
			{
				// BEFORE: Response.Redirect(sRedirect)
				// AFTER:  Remove ~/ prefix — ASP.NET Core uses /path not ~/path
				httpContext.Response.Redirect(sRedirect.Substring(1));
			}
			// 07/07/2010 Paul.  Redirect to the AdminWizard.
			// NOTE: IsOfflineClient check removed — offline client not applicable in ASP.NET Core
			else if ( _security != null && _security.IS_ADMIN
			       && Sql.IsEmptyString(Sql.ToString(_memoryCache?.Get<object>("CONFIG.Configurator.LastRun"))) )
			{
				// BEFORE: Context.Response.Redirect("~/Administration/Configurator/")
				// AFTER:  Remove ~/ prefix
				httpContext.Response.Redirect("/Administration/Configurator/");
			}
			// ReactOnlyUI: always redirect to React home
			// BEFORE: Response.Redirect("~/React/Home") [inside #else ReactOnlyUI block]
			// AFTER:  /React/Home (remove ~/ prefix)
			else
			{
				httpContext.Response.Redirect("/React/Home");
			}
		}

		/// <summary>
		/// Returns the value of the named cookie, or empty string if not present.
		/// BEFORE: Request.Cookies[sName].Value  (WebForms HttpRequest)
		/// AFTER:  httpContext.Request.Cookies[sName]  (ASP.NET Core)
		/// </summary>
		// 02/27/2012 Paul.  We need a safe way to get a cookie value.
		protected string CookieValue(string sName)
		{
			// BEFORE: if ( Request.Cookies[sName] != null ) sValue = Request.Cookies[sName].Value;
			// AFTER:  httpContext.Request.Cookies[sName] returns null when cookie is absent
			var httpContext = _httpContextAccessor?.HttpContext;
			if ( httpContext == null ) return String.Empty;
			return httpContext.Request.Cookies[sName] ?? String.Empty;
		}

		// =====================================================================================
		// Relationship and dynamic layout loading (adapted for ReactOnlyUI migration)
		//
		// BEFORE: These methods dynamically added WebForms UserControl instances to a PlaceHolder
		//         or HtmlTable container using LoadControl(), UpdatePanel, TabContainer, etc.
		//         The #if !ReactOnlyUI blocks wrapped all WebForms rendering logic.
		//
		// AFTER:  Methods return DataTable of relationship metadata for callers that need to
		//         iterate relationship definitions. WebForms container parameters removed.
		//         SplendidDynamic methods removed — not in depends_on_files, not applicable
		//         in ReactOnlyUI migration.
		// =====================================================================================

		/// <summary>
		/// Returns the relationship panel definitions for the given detail view layout.
		/// BEFORE: Dynamically appended WebForms UserControl instances to a PlaceHolder container
		/// AFTER:  Returns DataTable of relationship metadata from SplendidCache
		/// </summary>
		/// <param name="sDETAIL_NAME">Detail view layout name (e.g. "Accounts.DetailView").</param>
		/// <returns>DataTable of relationship definitions, or null when SplendidCache is not injected.</returns>
		public virtual DataTable AppendDetailViewRelationships(string sDETAIL_NAME)
		{
			// 11/17/2007 Paul.  Convert all view requests to a mobile request if appropriate.
			sDETAIL_NAME = sDETAIL_NAME + (this.IsMobile ? ".Mobile" : "");
			// Store DETAIL_NAME in Items for use by ActiveTabIndex property
			if ( Items != null )
				Items["DETAIL_NAME"] = sDETAIL_NAME;
			return _splendidCache?.DetailViewRelationships(sDETAIL_NAME);
		}

		/// <summary>
		/// Returns the user-specific dashlet definitions for the given detail view layout.
		/// BEFORE: Dynamically appended WebForms UserControl instances to a PlaceHolder container
		/// AFTER:  Returns DataTable of dashlet/relationship definitions from SplendidCache
		/// </summary>
		/// <param name="sDETAIL_NAME">Detail view layout name.</param>
		/// <param name="gUSER_ID">User ID for user-specific dashlet definitions.</param>
		/// <returns>DataTable of relationship/dashlet definitions, or null when SplendidCache is not injected.</returns>
		// 07/10/2009 Paul.  We are now allowing relationships to be user-specific.
		public virtual DataTable AppendDetailViewRelationships(string sDETAIL_NAME, Guid gUSER_ID)
		{
			// 11/17/2007 Paul.  Convert all view requests to a mobile request if appropriate.
			sDETAIL_NAME = sDETAIL_NAME + (this.IsMobile ? ".Mobile" : "");
			if ( Items != null )
				Items["DETAIL_NAME"] = sDETAIL_NAME;
			if ( Sql.IsEmptyGuid(gUSER_ID) )
				return _splendidCache?.DetailViewRelationships(sDETAIL_NAME);
			else
				return _splendidCache?.UserDashlets(sDETAIL_NAME, gUSER_ID);
		}

		/// <summary>
		/// Returns the relationship panel definitions for the given edit view layout.
		/// BEFORE: Dynamically added NewRecordControl instances to a PlaceHolder container
		/// AFTER:  Returns count of relationships from SplendidCache metadata
		/// </summary>
		/// <param name="sEDIT_NAME">Edit view layout name (e.g. "Accounts.EditView").</param>
		/// <param name="bNewRecord">True for new record context; false for existing record.</param>
		/// <returns>Row count of relationship definitions available for this edit view.</returns>
		// 04/19/2010 Paul.  New approach to EditView Relationships will distinguish between New Record and Existing Record.
		// 08/14/2020 Paul.  Hide Quick Create hover if nothing to display.
		public virtual int AppendEditViewRelationships(string sEDIT_NAME, bool bNewRecord)
		{
			sEDIT_NAME = sEDIT_NAME + (this.IsMobile ? ".Mobile" : "");
			// BEFORE: DataTable dtFields = SplendidCache.EditViewRelationships(sEDIT_NAME, bNewRecord); [static]
			// AFTER:  DataTable dtFields = _splendidCache?.EditViewRelationships(sEDIT_NAME, bNewRecord); [instance]
			DataTable dtFields = _splendidCache?.EditViewRelationships(sEDIT_NAME, bNewRecord);
			return dtFields?.Rows.Count ?? 0;
		}

		/// <summary>
		/// No-op — preserved for API compatibility.
		/// BEFORE: Dynamically appended SplendidGrid columns using AppendGridColumns WebForms method
		/// AFTER:  No-op — SplendidGrid WebForms control not applicable in ASP.NET Core ReactOnlyUI
		/// </summary>
		// 11/03/2021 Paul.  ASP.Net components are not needed.
		public virtual void AppendGridColumns(string sGRID_NAME)
		{
			// No-op: SplendidGrid is a WebForms control not available in ASP.NET Core.
			// Grid columns are defined by the React SPA using metadata from the REST API.
		}

		// =====================================================================================
		// Apply Rules Engine event methods (adapted for ReactOnlyUI migration)
		//
		// BEFORE: Called SplendidDynamic.ApplyEditViewRules / ApplyDetailViewRules (WebForms)
		//         with WebForms control tree parameters.
		//
		// AFTER:  SplendidDynamic.ApplyEditViewRules / ApplyDetailViewRules are NOT available
		//         in ReactOnlyUI migration (not in depends_on_files).
		//         WF4ApprovalActivity calls are preserved (dormant stub compiles for Enterprise upgrade).
		//         Application parameter → _memoryCache
		// =====================================================================================

		/// <summary>
		/// Applies the DetailView pre-load business rules for the given layout and data row.
		/// BEFORE: SplendidDynamic.ApplyDetailViewRules(sDETAIL_NAME, this, "PRE_LOAD_EVENT_XOML", row) [WebForms]
		/// AFTER:  No-op — SplendidDynamic not available in ReactOnlyUI; rules applied by React client
		/// </summary>
		// 11/11/2010 Paul.  Change to Pre Load and Post Load.
		public virtual void ApplyDetailViewPreLoadEventRules(string sDETAIL_NAME, DataRow row)
		{
			sDETAIL_NAME = sDETAIL_NAME + (this.IsMobile ? ".Mobile" : "");
			// No-op: SplendidDynamic.ApplyDetailViewRules not available in ReactOnlyUI migration.
			// Business rules are applied by the React client via dedicated API endpoints.
			// 04/28/2011 Paul.  We don't want to throw an exception here because it would prevent other core logic.
		}

		/// <summary>
		/// Applies the DetailView post-load business rules for the given layout and data row.
		/// BEFORE: SplendidDynamic.ApplyDetailViewRules(sDETAIL_NAME, this, "POST_LOAD_EVENT_XOML", row) [WebForms]
		/// AFTER:  No-op — SplendidDynamic not available in ReactOnlyUI
		/// </summary>
		public virtual void ApplyDetailViewPostLoadEventRules(string sDETAIL_NAME, DataRow row)
		{
			sDETAIL_NAME = sDETAIL_NAME + (this.IsMobile ? ".Mobile" : "");
			// No-op: SplendidDynamic.ApplyDetailViewRules not available in ReactOnlyUI migration.
			// 04/28/2011 Paul.  If there was a rules error, then make sure to display it.
			if ( !this.RulesIsValid )
				throw new Exception(this.RulesErrorMessage);
		}

		/// <summary>
		/// Applies the DetailView new-record business rules for the given layout.
		/// Preserved for API compatibility; no-op in ReactOnlyUI migration.
		/// </summary>
		public virtual void ApplyDetailViewNewEventRules(string sDETAIL_NAME)
		{
			sDETAIL_NAME = sDETAIL_NAME + (this.IsMobile ? ".Mobile" : "");
			// No-op: SplendidDynamic.ApplyDetailViewRules not available in ReactOnlyUI migration.
		}

		/// <summary>
		/// Applies the EditView pre-load business rules for the given layout and data row.
		/// BEFORE: SplendidDynamic.ApplyEditViewRules(sEDIT_NAME, this, "PRE_LOAD_EVENT_XOML", row) [WebForms]
		/// AFTER:  No-op — SplendidDynamic not available in ReactOnlyUI
		/// </summary>
		// 11/11/2010 Paul.  Change to Pre Load and Post Load.
		public virtual void ApplyEditViewPreLoadEventRules(string sEDIT_NAME, DataRow row)
		{
			sEDIT_NAME = sEDIT_NAME + (this.IsMobile ? ".Mobile" : "");
			// No-op: SplendidDynamic.ApplyEditViewRules not available in ReactOnlyUI migration.
			// 04/28/2011 Paul.  We don't want to throw an exception here because it would prevent other core logic.
		}

		/// <summary>
		/// Applies the EditView post-load business rules and WF4 approval rules for the given layout and data row.
		/// BEFORE: SplendidDynamic.ApplyEditViewRules(..., "POST_LOAD_EVENT_XOML", row)
		///         WF4ApprovalActivity.ApplyEditViewPostLoadEventRules(Application, L10n, sEDIT_NAME, this, row)
		/// AFTER:  SplendidDynamic removed; WF4ApprovalActivity call preserved with _memoryCache replacing Application
		/// </summary>
		// 08/08/2016 Paul.  Apply Business Process Rules.
		public virtual void ApplyEditViewPostLoadEventRules(string sEDIT_NAME, DataRow row)
		{
			sEDIT_NAME = sEDIT_NAME + (this.IsMobile ? ".Mobile" : "");
			// No-op: SplendidDynamic.ApplyEditViewRules not available in ReactOnlyUI migration.
			// 08/08/2016 Paul.  Apply Business Process Rules.
			// BEFORE: WF4ApprovalActivity.ApplyEditViewPostLoadEventRules(Application, L10n, sEDIT_NAME, this, row);
			// AFTER:  _memoryCache replaces Application; GetL10n() ensures L10n is initialized
			WF4ApprovalActivity.ApplyEditViewPostLoadEventRules(_memoryCache, GetL10n(), sEDIT_NAME, this, row);
			// 04/28/2011 Paul.  If there was a rules error, then make sure to display it.
			if ( !this.RulesIsValid )
				throw new Exception(this.RulesErrorMessage);
		}

		/// <summary>
		/// Applies the EditView new-record business rules for the given layout.
		/// BEFORE: SplendidDynamic.ApplyEditViewRules(..., "NEW_EVENT_XOML", null) [WebForms]
		/// AFTER:  No-op — SplendidDynamic not available in ReactOnlyUI
		/// </summary>
		// 11/10/2010 Paul.  Apply Business Rules.
		public virtual void ApplyEditViewNewEventRules(string sEDIT_NAME)
		{
			sEDIT_NAME = sEDIT_NAME + (this.IsMobile ? ".Mobile" : "");
			// No-op: SplendidDynamic.ApplyEditViewRules not available in ReactOnlyUI migration.
			// 04/28/2011 Paul.  If there was a rules error, then make sure to display it.
			if ( !this.RulesIsValid )
				throw new Exception(this.RulesErrorMessage);
		}

		/// <summary>
		/// Applies the EditView pre-save business rules and WF4 approval rules for the given layout and data row.
		/// BEFORE: SplendidDynamic.ApplyEditViewRules(..., "PRE_SAVE_EVENT_XOML", row)
		///         WF4ApprovalActivity.ApplyEditViewPreSaveEventRules(Application, L10n, sEDIT_NAME, this, row)
		/// AFTER:  SplendidDynamic removed; WF4ApprovalActivity call preserved with _memoryCache replacing Application
		/// </summary>
		// 08/08/2016 Paul.  Apply Business Process Rules.
		public virtual void ApplyEditViewPreSaveEventRules(string sEDIT_NAME, DataRow row)
		{
			sEDIT_NAME = sEDIT_NAME + (this.IsMobile ? ".Mobile" : "");
			// No-op: SplendidDynamic.ApplyEditViewRules not available in ReactOnlyUI migration.
			// 08/08/2016 Paul.  Apply Business Process Rules.
			// BEFORE: WF4ApprovalActivity.ApplyEditViewPreSaveEventRules(Application, L10n, sEDIT_NAME, this, row);
			// AFTER:  _memoryCache replaces Application; GetL10n() ensures L10n is initialized
			WF4ApprovalActivity.ApplyEditViewPreSaveEventRules(_memoryCache, GetL10n(), sEDIT_NAME, this, row);
			// 04/28/2011 Paul.  If there was a rules error, then make sure to display it.
			if ( !this.RulesIsValid )
				throw new Exception(this.RulesErrorMessage);
		}

		/// <summary>
		/// Applies the EditView post-save business rules for the given layout and data row.
		/// BEFORE: SplendidDynamic.ApplyEditViewRules(..., "POST_SAVE_EVENT_XOML", row) [WebForms]
		/// AFTER:  No-op — SplendidDynamic not available in ReactOnlyUI
		/// </summary>
		// 12/10/2012 Paul.  Provide access to the item data.
		public virtual void ApplyEditViewPostSaveEventRules(string sEDIT_NAME, DataRow row)
		{
			sEDIT_NAME = sEDIT_NAME + (this.IsMobile ? ".Mobile" : "");
			// No-op: SplendidDynamic.ApplyEditViewRules not available in ReactOnlyUI migration.
			// 04/28/2011 Paul.  Throwing an exception here does not make sense as it would block the redirect after a successful save.
		}

		/// <summary>
		/// Applies the EditView validation rules for the given layout.
		/// BEFORE: SplendidDynamic.ApplyEditViewRules(..., "VALIDATION_EVENT_XOML", null) [WebForms]
		/// AFTER:  No-op — SplendidDynamic not available in ReactOnlyUI; validation performed by React client
		/// </summary>
		public virtual void ApplyEditViewValidationRules(string sEDIT_NAME)
		{
			sEDIT_NAME = sEDIT_NAME + (this.IsMobile ? ".Mobile" : "");
			// No-op: SplendidDynamic.ApplyEditViewRules not available in ReactOnlyUI migration.
			// Validation is performed by the React client using the REST validation API.
			// 04/28/2011 Paul.  If there was a rules error, then make sure to display it.
			if ( !this.RulesIsValid )
				throw new Exception(this.RulesErrorMessage);
		}
	}
}
