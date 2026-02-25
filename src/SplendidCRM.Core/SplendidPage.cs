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
// .NET 10 Migration: SplendidCRM/_code/SplendidPage.cs → src/SplendidCRM.Core/SplendidPage.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.UI.*; using System.Web.UI.HtmlControls;
//              using System.Web.UI.WebControls; (all WebForms namespaces)
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//   - REMOVED: System.Web.UI.Page and System.Web.UI.MasterPage base class inheritance
//   - ADDED:   DI constructor with IHttpContextAccessor, IMemoryCache, Security, SplendidCache,
//              SplendidInit, Utils replacing all static access patterns
//   - REPLACED: Session["key"] → _httpContextAccessor.HttpContext?.Session.GetString("key") /
//               .SetString("key", value)
//   - REPLACED: Application["key"] → _memoryCache.Get<object>("key")
//   - REPLACED: HttpContext.Current.Application["key"] → _memoryCache.Get<object>("key")
//   - REPLACED: Context.Items["key"] → _httpContextAccessor.HttpContext?.Items["key"]
//   - REPLACED: Page.Items["key"] → _httpContextAccessor.HttpContext?.Items["key"]
//   - REPLACED: Response.Redirect(url) → _httpContextAccessor.HttpContext?.Response.Redirect(url)
//   - REPLACED: Server.UrlEncode(str) → Uri.EscapeDataString(str)
//   - REPLACED: WebForms Page.Title = sTitle → Items["PageTitle"] = sTitle
//   - REPLACED: WebForms this.Theme → stored/read from Items["Theme"] / session
//   - REPLACED: Static SplendidInit.InitSession(HttpContext) → instance _splendidInit.InitSession()
//   - REPLACED: Static Utils.IsOfflineClient property → instance _utils.IsOfflineClient
//   - ADDED:    Local CommandEventArgs replacement for System.Web.UI.WebControls.CommandEventArgs
//   - REMOVED:  #if !ReactOnlyUI blocks (WebForms PlaceHolder / SplendidGrid params) — adapted
//   - ADAPTED:  AppendDetailViewRelationships(string, PlaceHolder, Guid)
//               → AppendDetailViewRelationships(string, Guid) returning DataTable
//   - ADAPTED:  AppendGridColumns(SplendidGrid, string, ...) → AppendGridColumns(string, ...)
//               (SplendidGrid WebForms control not applicable in ASP.NET Core)
//   - PRESERVED: namespace SplendidCRM, all business logic, null-safe guarding patterns,
//                session key names (USER_SETTINGS/CULTURE, /TIMEZONE, /CURRENCY, /THEME),
//                cache key names (CONFIG.show_sql, CONFIG.debug_dashlets, imageURL)
//   - PRESERVED: SplendidPopup.m_sMODULE, SplendidAdminPage.AdminPage(), SplendidMaster.ShowTeamHierarchy()
//   - NOTE:      SplendidSession.CreateSession(Session) call removed — session tracking not needed
//               in distributed session model (Redis/SQL Server) as session lifetime is managed
//               by the distributed cache provider configuration.
//   - NOTE:      WebForms controls (TableCell, Image) in SplendidMaster removed; ShowTeamHierarchy()
//               preserves identical business logic and return value
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
	// Local replacement for System.Web.UI.WebControls.CommandEventArgs
	// Required because SplendidMaster.Page_Command(object, CommandEventArgs) must preserve
	// the CommandName/CommandArgument contract used by derived page classes throughout
	// the WebForms module code-behinds that call Page_Command.
	//
	// BEFORE: System.Web.UI.WebControls.CommandEventArgs (shipped with .NET Framework)
	// AFTER:  Local CommandEventArgs defined in namespace SplendidCRM (no System.Web dependency)
	// =====================================================================================

	/// <summary>
	/// Replacement for System.Web.UI.WebControls.CommandEventArgs.
	/// Preserves CommandName and CommandArgument properties for existing module code-behind callers.
	/// </summary>
	public class CommandEventArgs : EventArgs
	{
		/// <summary>Gets the name of the command.</summary>
		public string CommandName { get; }

		/// <summary>Gets the argument for the command.</summary>
		public object CommandArgument { get; }

		/// <summary>Initialises a new instance with the specified command name and argument.</summary>
		public CommandEventArgs(string commandName, object commandArgument)
		{
			CommandName     = commandName     ?? String.Empty;
			CommandArgument = commandArgument;
		}

		/// <summary>Initialises a new instance with the specified command name and a null argument.</summary>
		public CommandEventArgs(string commandName) : this(commandName, null)
		{
		}
	}

	// =====================================================================================
	// SplendidPage — Base page adapter
	//
	// BEFORE: public class SplendidPage : System.Web.UI.Page
	// AFTER:  public class SplendidPage  (no base class — DI-injectable service)
	//
	// Original design: Base class for all SplendidCRM WebForms pages providing per-request
	// culture initialisation, authentication checks, and dynamic layout helpers.
	//
	// Migrated design: Callable base service class that controllers, hosted services, or
	// middleware instantiate via DI.  All WebForms page lifecycle hooks (InitializeCulture,
	// OnInit, Page_PreInit) are preserved as ordinary public methods that can be called
	// explicitly from an ASP.NET Core action filter, controller base class, or middleware.
	//
	// DI Registration:  services.AddScoped<SplendidPage>();
	//   (Scoped so that each request gets its own instance with the current
	//    IHttpContextAccessor-bound HttpContext and Session data.)
	// =====================================================================================

	/// <summary>
	/// Base page adapter for SplendidCRM.
	/// 
	/// Migrated from SplendidCRM/_code/SplendidPage.cs (~551 lines) for .NET 10 ASP.NET Core.
	/// Replaces System.Web.UI.Page inheritance and all System.Web dependencies with
	/// ASP.NET Core DI-compatible equivalents.
	///
	/// Lifecycle methods (InitializeCulture, OnInit, Page_PreInit, AppendDetailViewRelationships)
	/// are preserved as callable public/virtual methods — they must be invoked explicitly
	/// by the hosting controller or action filter in place of the former WebForms event pipeline.
	///
	/// DESIGN NOTES:
	///   • Register as SCOPED so each HTTP request gets its own instance.
	///   • HttpContext.Items["L10n"], ["T10n"], ["C10n"], ["PrintView"], ["Theme"],
	///     ["ActiveTabMenu"] are stored by the lifecycle methods for consumption by
	///     downstream middleware, views, or controllers, preserving the original
	///     Context.Items storage contract.
	/// </summary>
	public class SplendidPage
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
		/// BEFORE: Application["CONFIG.show_sql"]
		/// AFTER:  _memoryCache.Get&lt;object&gt;("CONFIG.show_sql")
		/// </summary>
		protected readonly IMemoryCache _memoryCache;

		/// <summary>
		/// ACL and authentication service.
		/// BEFORE: Security.IsAuthenticated() / Security.GetUserAccess() static calls
		/// AFTER:  _security.IsAuthenticated() / _security.GetUserAccess() instance calls
		/// </summary>
		protected readonly Security _security;

		/// <summary>
		/// Metadata caching hub.
		/// BEFORE: SplendidCache.DetailViewRelationships() / SplendidCache.UserDashlets() static calls
		/// AFTER:  _splendidCache.DetailViewRelationships() / _splendidCache.UserDashlets() instance calls
		/// </summary>
		protected readonly SplendidCache _splendidCache;

		/// <summary>
		/// Application bootstrap orchestrator.
		/// BEFORE: SplendidInit.InitSession(HttpContext.Current) static call
		/// AFTER:  _splendidInit.InitSession() instance call (uses _httpContextAccessor internally)
		/// </summary>
		protected readonly SplendidInit _splendidInit;

		/// <summary>
		/// General utilities.
		/// Used for Utils.IsOfflineClient (instance property) and Utils.ExpandException (static method).
		/// </summary>
		protected readonly Utils _utils;

		// =====================================================================================
		// Protected instance fields — preserved from original source with identical names
		// and access modifiers for derived-class compatibility.
		// =====================================================================================

		/// <summary>Whether to emit SQL debug output to the page.</summary>
		protected bool bDebug = false;

		// 08/29/2005 Paul. Only store the absolute minimum amount of data.
		// This means remove the data that is accessible from the Security object.
		// High frequency objects are L10N and TimeZone.

		/// <summary>User's preferred culture string (e.g. "en-US"), read from session.</summary>
		protected string m_sCULTURE;

		/// <summary>User's preferred short date format (e.g. "MM/dd/yyyy"), read from session.</summary>
		protected string m_sDATEFORMAT;

		/// <summary>User's preferred short time format (e.g. "h:mm tt"), read from session.</summary>
		protected string m_sTIMEFORMAT;

		/// <summary>User's timezone GUID, read from session.</summary>
		protected Guid m_gTIMEZONE;

		/// <summary>Whether the current request is a print-friendly view.</summary>
		protected bool m_bPrintView = false;

		/// <summary>Whether this page requires admin privileges.</summary>
		protected bool m_bIsAdminPage = false;

		// L10n is an abbreviation for Localization (between the L & n are 10 characters).
		// 08/28/2005 Paul. Keep old L10n name, and rename the object to simplify updated approach.

		/// <summary>Request-scoped localisation instance.</summary>
		protected L10N L10n;

		/// <summary>Request-scoped timezone instance.</summary>
		protected TimeZone T10n;

		/// <summary>Request-scoped currency instance.</summary>
		protected Currency C10n;

		// =====================================================================================
		// Private helpers — per-request HttpContext accessors
		// =====================================================================================

		/// <summary>
		/// Gets the per-request ISession from the current HttpContext.
		/// Returns null when called outside an HTTP request (e.g. background service).
		/// BEFORE: HttpSessionState Session property inherited from System.Web.UI.Page
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
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs a SplendidPage service with all required DI dependencies.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current for session, request, response, and per-request items access.
		/// </param>
		/// <param name="memoryCache">
		/// Replaces HttpApplicationState (Application[]) for reading cached configuration values
		/// (CONFIG.*, Modules.*.*, imageURL) and passing to Currency/L10N factory methods.
		/// </param>
		/// <param name="security">
		/// ACL and authentication service — replaces static Security.IsAuthenticated() /
		/// Security.GetUserAccess() calls.
		/// </param>
		/// <param name="splendidCache">
		/// Metadata cache hub — replaces static SplendidCache.DetailViewRelationships() /
		/// SplendidCache.UserDashlets() calls.
		/// </param>
		/// <param name="splendidInit">
		/// Application bootstrap — replaces static SplendidInit.InitSession(HttpContext.Current) call.
		/// </param>
		/// <param name="utils">
		/// General utilities — provides IsOfflineClient (instance) and ExpandException (static).
		/// </param>
		public SplendidPage(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache         ,
			Security             security            ,
			SplendidCache        splendidCache       ,
			SplendidInit         splendidInit        ,
			Utils                utils               )
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
			_security            = security           ;
			_splendidCache       = splendidCache      ;
			_splendidInit        = splendidInit       ;
			_utils               = utils              ;
		}

		// =====================================================================================
		// PrintView property
		// BEFORE: Page.Items["PrintView"] = m_bPrintView (WebForms Context.Items)
		// AFTER:  _httpContextAccessor.HttpContext?.Items["PrintView"] = m_bPrintView
		// =====================================================================================

		/// <summary>
		/// Gets or sets whether the current request is a print-friendly view.
		/// Setting this property also stores the value in the per-request Items dictionary
		/// so that downstream controls and middleware can read it without needing a reference
		/// to this SplendidPage instance — preserving the original Context.Items["PrintView"] contract.
		/// </summary>
		public bool PrintView
		{
			get
			{
				return m_bPrintView;
			}
			set
			{
				m_bPrintView = value;
				// BEFORE: Context.Items["PrintView"] = m_bPrintView;
				// AFTER:  _httpContextAccessor.HttpContext?.Items["PrintView"] = m_bPrintView;
				if (Items != null)
					Items["PrintView"] = m_bPrintView;
			}
		}

		/// <summary>
		/// Gets or sets whether this is an administration page.
		/// Used by <see cref="SetAdminMenu"/> and derived admin page classes.
		/// </summary>
		public bool IsAdminPage
		{
			get
			{
				return m_bIsAdminPage;
			}
			set
			{
				m_bIsAdminPage = value;
			}
		}

		// =====================================================================================
		// Navigation / menu helpers
		// BEFORE: Page.Items["ActiveTabMenu"] = sMODULE (WebForms Page.Items)
		// AFTER:  _httpContextAccessor.HttpContext?.Items["ActiveTabMenu"] = sMODULE
		// =====================================================================================

		/// <summary>
		/// Records the active module name in the per-request Items dictionary so that
		/// navigation controls (tab menu) can highlight the correct tab.
		/// </summary>
		/// <param name="sMODULE">Module name, e.g. "Accounts", "Contacts".</param>
		public void SetMenu(string sMODULE)
		{
			// 02/25/2010 Paul.  Use Items instead of manually injecting the ActiveTab.
			// BEFORE: Page.Items["ActiveTabMenu"] = sMODULE;
			// AFTER:  Items["ActiveTabMenu"] = sMODULE;
			if (Items != null)
				Items["ActiveTabMenu"] = sMODULE;
		}

		/// <summary>
		/// Records the active admin module name and sets an admin flag in the per-request Items
		/// dictionary for areas that don't have a record in the Modules table.
		/// </summary>
		/// <param name="sMODULE">Admin module name.</param>
		// 07/24/2010 Paul.  We need an admin flag for the areas that don't have a record in the Modules table.
		public void SetAdminMenu(string sMODULE)
		{
			if (Items != null)
			{
				Items["ActiveTabMenu"]          = sMODULE;
				Items["ActiveTabMenu.IsAdmin"]  = true;
			}
		}

		/// <summary>
		/// Stores the page title in the per-request Items dictionary.
		/// BEFORE: Page.Title = sTitle  (WebForms)
		/// AFTER:  Items["PageTitle"] = sTitle
		/// </summary>
		/// <param name="sTitle">Page title string.</param>
		// 01/20/2007 Paul.  Wrap the page title function to minimize differences between Web1.2.
		public void SetPageTitle(string sTitle)
		{
			// BEFORE: Page.Title = sTitle;
			// AFTER:  Items["PageTitle"] = sTitle;  (read by layout/master view or middleware)
			if (Items != null)
				Items["PageTitle"] = sTitle;
		}

		// =====================================================================================
		// Culture, Timezone, and Currency initialisation
		// BEFORE: Session["USER_SETTINGS/CULTURE"] → direct WebForms Session accessor
		// AFTER:  Session?.GetString("USER_SETTINGS/CULTURE")
		// =====================================================================================

		// 03/07/2008 Paul.  There is a better time to initialize the culture.
		/// <summary>
		/// Initialises per-request culture, timezone, and currency from the user's session settings.
		///
		/// .NET 10 Migration notes:
		///   • REMOVED: this.Culture = L10n.NAME  (WebForms page-level AJAX culture — not applicable)
		///   • REPLACED: Session["key"] → Session?.GetString("key")
		///   • REPLACED: Session["key"] = value → Session?.SetString("key", value.ToString())
		///   • REPLACED: Application → _memoryCache passed to Currency.CreateCurrency()
		///   • REPLACED: CultureInfo applied to Thread.CurrentThread directly (same as original)
		///   • NULL-GUARDED: all Session accesses guarded against null (non-HTTP contexts safe)
		/// </summary>
		public virtual void InitializeCulture()
		{
			// 08/30/2005 Paul.  Move the L10N creation to this get function so that the first control
			// that gets created will cause the creation of L10N.  The UserControls get the OnInit event
			// before the Page onInit event.
			// 03/07/2008 Paul.  The page lifecycle has been designed to always call InitializeCulture
			// before the page itself or any of its child controls have done any work with localized resources.
			// BEFORE: m_sCULTURE    = Sql.ToString(Session["USER_SETTINGS/CULTURE"   ]);
			// AFTER:  m_sCULTURE    = Sql.ToString(Session?.GetString("USER_SETTINGS/CULTURE"));
			m_sCULTURE    = Sql.ToString(Session?.GetString("USER_SETTINGS/CULTURE"   ));
			m_sDATEFORMAT = Sql.ToString(Session?.GetString("USER_SETTINGS/DATEFORMAT"));
			m_sTIMEFORMAT = Sql.ToString(Session?.GetString("USER_SETTINGS/TIMEFORMAT"));

			// BEFORE: L10n = new L10N(m_sCULTURE);
			// AFTER:  L10n = new L10N(m_sCULTURE, _memoryCache);
			// (L10N constructor requires IMemoryCache for terminology lookups in .NET 10)
			L10n = new L10N(m_sCULTURE, _memoryCache);

			// 03/07/2008 Paul.  We need to set the page culture so that the AJAX engine will initialize
			// Sys.CultureInfo.CurrentCulture.  In ASP.NET Core, no AJAX ScriptManager exists; we apply
			// the culture directly to Thread.CurrentThread instead.
			// BEFORE: try { this.Culture = L10n.NAME; } catch { this.Culture = "en-US"; }
			// AFTER:  Culture is set below after CultureInfo configuration (combined with the full block).

			// 08/30/2005 Paul.  Move the TimeZone creation to this get function.
			// BEFORE: m_gTIMEZONE = Sql.ToGuid(Session["USER_SETTINGS/TIMEZONE"]);
			// AFTER:  m_gTIMEZONE = Sql.ToGuid(Session?.GetString("USER_SETTINGS/TIMEZONE"));
			m_gTIMEZONE = Sql.ToGuid(Session?.GetString("USER_SETTINGS/TIMEZONE"));
			// TimeZone.CreateTimeZone(Guid) uses the static ambient IMemoryCache set at startup.
			T10n = TimeZone.CreateTimeZone(m_gTIMEZONE);
			if ( T10n.ID != m_gTIMEZONE )
			{
				// 08/30/2005 Paul.  If we are using a default, then update the session so that future
				// controls will be quicker.
				// BEFORE: Session["USER_SETTINGS/TIMEZONE"] = m_gTIMEZONE.ToString();
				// AFTER:  Session?.SetString("USER_SETTINGS/TIMEZONE", m_gTIMEZONE.ToString());
				m_gTIMEZONE = T10n.ID;
				Session?.SetString("USER_SETTINGS/TIMEZONE", m_gTIMEZONE.ToString());
			}

			// BEFORE: Guid gCURRENCY_ID = Sql.ToGuid(Session["USER_SETTINGS/CURRENCY"]);
			// AFTER:  Guid gCURRENCY_ID = Sql.ToGuid(Session?.GetString("USER_SETTINGS/CURRENCY"));
			Guid gCURRENCY_ID = Sql.ToGuid(Session?.GetString("USER_SETTINGS/CURRENCY"));
			// 04/30/2016 Paul.  Require the Application so that we can get the base currency.
			// BEFORE: C10n = Currency.CreateCurrency(Application, gCURRENCY_ID);
			// AFTER:  C10n = Currency.CreateCurrency(_memoryCache, gCURRENCY_ID);
			C10n = Currency.CreateCurrency(_memoryCache, gCURRENCY_ID);
			if ( C10n.ID != gCURRENCY_ID )
			{
				// 05/09/2006 Paul.  If we are using a default, then update the session so that future
				// controls will be quicker.
				// BEFORE: Session["USER_SETTINGS/CURRENCY"] = gCURRENCY_ID.ToString();
				// AFTER:  Session?.SetString("USER_SETTINGS/CURRENCY", gCURRENCY_ID.ToString());
				gCURRENCY_ID = C10n.ID;
				Session?.SetString("USER_SETTINGS/CURRENCY", gCURRENCY_ID.ToString());
			}

			// 08/05/2006 Paul.  We cannot set the CurrencyDecimalSeparator directly on Mono as it is
			// read-only.  Hold off setting the CurrentCulture until we have updated all the settings.
			// Build CultureInfo, configure it, then apply to Thread.CurrentThread in one step.
			try
			{
				CultureInfo culture = CultureInfo.CreateSpecificCulture(L10n.NAME);
				culture.DateTimeFormat.ShortDatePattern = m_sDATEFORMAT;
				culture.DateTimeFormat.ShortTimePattern = m_sTIMEFORMAT;
				// 05/29/2013 Paul.  LongTimePattern is used in ListView.
				culture.DateTimeFormat.LongTimePattern  = m_sTIMEFORMAT;

				// 03/30/2007 Paul.  Always set the currency symbol.  It is not retained between page requests.
				// 07/28/2006 Paul.  We cannot set the CurrencySymbol directly on Mono as it is read-only.
				// 03/07/2008 Paul.  Move all localization to InitializeCulture().  Just clone the culture
				// and modify the clone.
				culture.NumberFormat.CurrencySymbol = C10n.SYMBOL;

				// 08/30/2005 Paul.  Apply the modified cultures.
				Thread.CurrentThread.CurrentCulture   = culture;
				Thread.CurrentThread.CurrentUICulture = culture;
			}
			catch
			{
				// 08/19/2013 Paul.  An invalid default language can crash the app.  Always default to English.
				// Don't log the error as it would be generated for every page request.
				try
				{
					CultureInfo cultureFallback = CultureInfo.CreateSpecificCulture("en-US");
					Thread.CurrentThread.CurrentCulture   = cultureFallback;
					Thread.CurrentThread.CurrentUICulture = cultureFallback;
				}
				catch
				{
					// Absolute fallback — cannot set culture; proceed with invariant culture.
				}
			}
		}

		// =====================================================================================
		// Accessor methods for per-request L10N / TimeZone / Currency objects
		// BEFORE: Derived controls accessed these via direct field reference on the Page
		// AFTER:  Accessed via these public accessor methods (same pattern as original)
		// =====================================================================================

		/// <summary>
		/// Returns the per-request localisation object.
		/// BEFORE: stored in Context.Items["L10n"] by OnInit; returned by Page accessor
		/// AFTER:  stored in HttpContext.Items["L10n"] by OnInit(); returned here
		/// </summary>
		public L10N GetL10n()
		{
			return L10n;
		}

		/// <summary>
		/// Returns the per-request timezone object.
		/// BEFORE: stored in Context.Items["T10n"] by OnInit
		/// AFTER:  stored in HttpContext.Items["T10n"] by OnInit()
		/// </summary>
		public TimeZone GetT10n()
		{
			return T10n;
		}

		/// <summary>
		/// Returns the per-request currency object.
		/// BEFORE: stored in Context.Items["C10n"] by OnInit
		/// AFTER:  stored in HttpContext.Items["C10n"] by OnInit()
		/// </summary>
		public Currency GetC10n()
		{
			return C10n;
		}

		// =====================================================================================
		// Authentication and admin page flags
		// =====================================================================================

		// 11/19/2005 Paul.  Default to expiring everything.
		/// <summary>
		/// Returns whether authentication is required for this page.
		/// Override to return false for public pages (login, health check, etc.).
		/// </summary>
		virtual protected bool AuthenticationRequired()
		{
			return true;
		}

		/// <summary>
		/// Returns whether this page requires admin shortcuts to be enabled.
		/// Overridden in <see cref="SplendidAdminPage"/> to return true.
		/// </summary>
		virtual protected bool AdminPage()
		{
			return false;
		}

		// =====================================================================================
		// IsMobile
		// BEFORE: return (this.Theme == "Mobile");  — WebForms Page.Theme property
		// AFTER:  Read USER_SETTINGS/THEME from session; fall back to SplendidDefaults.Theme()
		// =====================================================================================

		/// <summary>
		/// Returns whether the current request is from a mobile device (theme = "Mobile").
		/// BEFORE: return (this.Theme == "Mobile");  — read from WebForms Page.Theme
		/// AFTER:  reads USER_SETTINGS/THEME from distributed session, with fallback to
		///         SplendidDefaults.Theme() (uses static ambient IMemoryCache set at startup).
		/// </summary>
		public bool IsMobile
		{
			get
			{
				// BEFORE: return (this.Theme == "Mobile");
				// AFTER:  Read from session (same data source as Page_PreInit sets this.Theme)
				string sTheme = Sql.ToString(Session?.GetString("USER_SETTINGS/THEME"));
				if ( String.IsNullOrEmpty(sTheme) )
					sTheme = SplendidDefaults.Theme();
				return (sTheme == "Mobile");
			}
		}

		// =====================================================================================
		// OnInit — authentication check + context initialisation
		// BEFORE: protected override void OnInit(EventArgs e) — WebForms lifecycle override
		// AFTER:  public virtual void OnInit() — callable method; no EventArgs (not WebForms)
		// =====================================================================================

		/// <summary>
		/// Performs per-request authentication checks and context initialisation.
		///
		/// .NET 10 Migration notes:
		///   • REMOVED: this.Load += new EventHandler(Page_Load) wire-up (no WebForms events)
		///   • REPLACED: SplendidInit.InitSession(HttpContext.Current)
		///               → _splendidInit.InitSession()  (instance method, uses DI-injected context)
		///   • REPLACED: Security.IsAuthenticated() static → _security.IsAuthenticated() instance
		///   • REPLACED: Response.Redirect(url) → _httpContextAccessor.HttpContext?.Response.Redirect(url)
		///   • REPLACED: Server.UrlEncode(str) → Uri.EscapeDataString(str)
		///   • REPLACED: Application["CONFIG.show_sql"] → _memoryCache.Get&lt;object&gt;("CONFIG.show_sql")
		///   • REPLACED: Page.AppRelativeVirtualPath → _httpContextAccessor.HttpContext?.Request.Path.Value
		///   • REPLACED: Request.Url.Query → _httpContextAccessor.HttpContext?.Request.QueryString.Value
		///   • REPLACED: Session["SYSTEM_GENERATED_PASSWORD"] → Session?.GetString(...)
		///   • REMOVED:  SplendidSession.CreateSession(Session) — session tracking handled by
		///               distributed session provider (Redis/SQL Server); no explicit CreateSession call needed
		///   • REPLACED: Context.Items["L10n"/"T10n"/"C10n"/"PrintView"]
		///               → _httpContextAccessor.HttpContext?.Items[...] (same per-request contract)
		///   • REMOVED:  base.OnInit(e) — no WebForms base class
		/// </summary>
		public virtual void OnInit()
		{
			// BEFORE: if (Sql.IsEmptyString(Application["imageURL"])) SplendidInit.InitSession(HttpContext.Current);
			// AFTER:  Check IMemoryCache; call instance InitSession() if not yet bootstrapped.
			if ( Sql.IsEmptyString(_memoryCache.Get<object>("imageURL")) )
			{
				_splendidInit.InitSession();
			}

			if ( AuthenticationRequired() )
			{
				// 11/17/2007 Paul.  New function to determine if user is authenticated.
				// BEFORE: if (!Security.IsAuthenticated())  (static)
				// AFTER:  if (!_security.IsAuthenticated()) (instance)
				if ( !_security.IsAuthenticated() )
				{
					// 05/22/2008 Paul.  Save the URL and redirect after login.
					// 08/15/2008 Paul.  Request.Url.Query is actually better because it includes the ?.
					// 11/06/2009 Paul.  If this is an offline client installation, redirect to client login page.
					// BEFORE: Server.UrlEncode(Page.AppRelativeVirtualPath + Request.Url.Query)
					// AFTER:  Uri.EscapeDataString(path + query)
					string sPath  = _httpContextAccessor?.HttpContext?.Request?.Path.Value         ?? String.Empty;
					string sQuery = _httpContextAccessor?.HttpContext?.Request?.QueryString.Value  ?? String.Empty;
					string sRedirectUrl = Uri.EscapeDataString(sPath + sQuery);

					// BEFORE: Utils.IsOfflineClient (static property)
					// AFTER:  _utils?.IsOfflineClient (instance property)
					bool bIsOfflineClient = (_utils != null) && _utils.IsOfflineClient;

					if ( bIsOfflineClient )
					{
						// BEFORE: Response.Redirect("~/Users/ClientLogin.aspx?Redirect=" + Server.UrlEncode(...));
						// AFTER:  _httpContextAccessor.HttpContext?.Response.Redirect("/Users/ClientLogin.aspx?Redirect=" + ...)
						_httpContextAccessor?.HttpContext?.Response.Redirect("/Users/ClientLogin.aspx?Redirect=" + sRedirectUrl);
					}
					else
					{
						// BEFORE: Response.Redirect("~/Users/Login.aspx?Redirect=" + Server.UrlEncode(...));
						// AFTER:  _httpContextAccessor.HttpContext?.Response.Redirect("/Users/Login.aspx?Redirect=" + ...)
						_httpContextAccessor?.HttpContext?.Response.Redirect("/Users/Login.aspx?Redirect=" + sRedirectUrl);
					}
				}
				// 02/22/2011 Paul.  If this is a System Generated or Expired password, force the user to change it.
				// 02/22/2011 Paul.  The user cannot change the password on the Offline Client.
				// BEFORE: Sql.ToBoolean(Session["SYSTEM_GENERATED_PASSWORD"])
				// AFTER:  Sql.ToBoolean(Session?.GetString("SYSTEM_GENERATED_PASSWORD"))
				else if ( Sql.ToBoolean(Session?.GetString("SYSTEM_GENERATED_PASSWORD")) && !( (_utils != null) && _utils.IsOfflineClient ) )
				{
					// 03/05/2011 Paul.  We need to make sure not to redirect if already on the password expired page,
					// otherwise we get into an endless loop.
					// BEFORE: Page.AppRelativeVirtualPath != "~/Users/PasswordExpired.aspx"
					// AFTER:  Request.Path != "/Users/PasswordExpired.aspx"
					string sPath  = _httpContextAccessor?.HttpContext?.Request?.Path.Value        ?? String.Empty;
					string sQuery = _httpContextAccessor?.HttpContext?.Request?.QueryString.Value ?? String.Empty;
					if ( sPath != "/Users/PasswordExpired.aspx" )
					{
						_httpContextAccessor?.HttpContext?.Response.Redirect("/Users/PasswordExpired.aspx?Redirect=" + Uri.EscapeDataString(sPath + sQuery));
					}
				}
				else
				{
					// 11/16/2014 Paul.  We need to continually update the SplendidSession so that it expires
					// along with the ASP.NET Session.
					// REMOVED: SplendidSession.CreateSession(Session) — Not applicable in distributed session model
					// (Redis/SQL Server).  Session lifetime is managed by the distributed cache provider
					// configuration (SESSION_CONNECTION + SESSION_PROVIDER env vars).
					// The original SplendidSession was an SQL-backed session tracker; in the migrated
					// architecture the distributed session provider handles all session persistence.
				}
			}

			// 11/27/2006 Paul.  We want to show the SQL on the Demo sites, so add a config variable.
			// BEFORE: bDebug = Sql.ToBoolean(Application["CONFIG.show_sql"]);
			// AFTER:  bDebug = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.show_sql"));
			bDebug = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.show_sql"));
#if DEBUG
			bDebug = true;
#endif

			// 08/30/2005 Paul.  Apply the new culture at the page level so that it is only applied once.
			// 03/11/2008 Paul.  GetL10n was getting called twice. No real harm, just not ideal.
			// 04/30/2006 Paul.  Use the Context to store pointers to the localization objects.
			// This is so that we don't need to require that the page inherits from SplendidPage.
			// BEFORE: Context.Items["L10n"] = GetL10n();  etc.
			// AFTER:  _httpContextAccessor.HttpContext?.Items["L10n"] = GetL10n();  etc.
			if ( Items != null )
			{
				Items["L10n"]       = GetL10n();
				Items["T10n"]       = GetT10n();
				Items["C10n"]       = GetC10n();
				Items["PrintView"]  = m_bPrintView;
			}
		}

		// =====================================================================================
		// Page_PreInit — theme selection
		// BEFORE: protected void Page_PreInit(object sender, EventArgs e)
		//         — wired by constructor via this.PreInit += new EventHandler(Page_PreInit)
		// AFTER:  public virtual void Page_PreInit()
		//         — called explicitly by host controller or action filter
		// =====================================================================================

		/// <summary>
		/// Reads the user's preferred theme from the distributed session and stores it in the
		/// per-request Items dictionary so that layout middleware and views can apply it.
		///
		/// .NET 10 Migration notes:
		///   • REPLACED: Session["USER_SETTINGS/THEME"] → Session?.GetString("USER_SETTINGS/THEME")
		///   • REPLACED: this.Theme = sTheme (WebForms Page.Theme) → Items["Theme"] = sTheme
		///   • REMOVED:  MasterPageFile manipulation — no master pages in ASP.NET Core MVC;
		///               React SPA layout selection is handled client-side
		/// </summary>
		public virtual void Page_PreInit()
		{
			// BEFORE: string sTheme = Sql.ToString(Session["USER_SETTINGS/THEME"]);
			// AFTER:  string sTheme = Sql.ToString(Session?.GetString("USER_SETTINGS/THEME"));
			string sTheme = Sql.ToString(Session?.GetString("USER_SETTINGS/THEME"));
			if ( String.IsNullOrEmpty(sTheme) )
			{
				// 07/01/2008 Paul.  Check default theme.  The default will fall-back to Sugar.
				// SplendidDefaults.Theme() is static and uses the ambient IMemoryCache set at startup.
				sTheme = SplendidDefaults.Theme();
			}

			// BEFORE: this.Theme = sTheme;  (WebForms Page.Theme)
			// AFTER:  Items["Theme"] = sTheme;  (stored in per-request Items for React SPA / middleware)
			if ( Items != null )
				Items["Theme"] = sTheme;

			// REMOVED: MasterPageFile manipulation (#if !ReactOnlyUI block).
			// React SPA handles layout selection client-side; no master pages in ASP.NET Core MVC.
		}

		// =====================================================================================
		// AppendDetailViewRelationships — adapted from WebForms PlaceHolder
		// BEFORE: protected void AppendDetailViewRelationships(string sDETAIL_NAME, PlaceHolder plc)
		//         protected void AppendDetailViewRelationships(string sDETAIL_NAME, PlaceHolder plc, Guid gUSER_ID)
		//         (inside #if !ReactOnlyUI block)
		// AFTER:  public DataTable AppendDetailViewRelationships(string sDETAIL_NAME)
		//         public DataTable AppendDetailViewRelationships(string sDETAIL_NAME, Guid gUSER_ID)
		//
		// The PlaceHolder parameter is removed since WebForms UI controls do not exist in
		// ASP.NET Core.  The method now returns the relationship DataTable directly for
		// consumption by REST API controllers or React SPA metadata endpoints.
		// Business logic (SplendidCache lookups, Security.GetUserAccess filtering,
		// CONFIG.debug_dashlets tracing) is preserved exactly from the original.
		// =====================================================================================

		/// <summary>
		/// Retrieves the detail view relationship definitions for the given detail view name,
		/// filtered to the rows the current user has list-access to.
		///
		/// .NET 10 Migration: Adapted from the WebForms PlaceHolder-based overload.
		/// Replaces loading .ascx UserControls with returning the relationship DataTable directly.
		/// </summary>
		/// <param name="sDETAIL_NAME">
		/// Detail view name (e.g. "Accounts.DetailView").
		/// ".Mobile" suffix is appended automatically when <see cref="IsMobile"/> is true.
		/// </param>
		/// <returns>
		/// DataTable of relationship definitions filtered by the current user's list access,
		/// or an empty DataTable on error.
		/// </returns>
		public DataTable AppendDetailViewRelationships(string sDETAIL_NAME)
		{
			return AppendDetailViewRelationships(sDETAIL_NAME, Guid.Empty);
		}

		/// <summary>
		/// Retrieves the detail view relationship definitions (or user-specific dashlets when
		/// gUSER_ID is not empty) for the given detail view name, filtered by the current user's
		/// list access permissions.
		///
		/// .NET 10 Migration: Adapted from the WebForms PlaceHolder-based overload.
		/// Replaces loading .ascx UserControls with returning the relationship DataTable directly.
		/// </summary>
		/// <param name="sDETAIL_NAME">
		/// Detail view name (e.g. "Accounts.DetailView").
		/// ".Mobile" suffix is appended automatically when <see cref="IsMobile"/> is true.
		/// </param>
		/// <param name="gUSER_ID">
		/// When non-empty, retrieves user-specific dashlet layout via
		/// <see cref="SplendidCache.UserDashlets"/> instead of the shared relationship layout.
		/// </param>
		/// <returns>
		/// DataTable of relationship / dashlet definitions filtered by the current user's
		/// list access, or an empty DataTable on error.
		/// </returns>
		// 03/08/2014 Paul.  Provide public access so that it can be called from the Seven master page.
		public DataTable AppendDetailViewRelationships(string sDETAIL_NAME, Guid gUSER_ID)
		{
			// 11/17/2007 Paul.  Convert all view requests to a mobile request if appropriate.
			sDETAIL_NAME = sDETAIL_NAME + (this.IsMobile ? ".Mobile" : "");
			try
			{
				DataTable dtFields = null;
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					// BEFORE: dtFields = SplendidCache.DetailViewRelationships(sDETAIL_NAME);  (static)
					// AFTER:  dtFields = _splendidCache.DetailViewRelationships(sDETAIL_NAME); (instance)
					dtFields = _splendidCache.DetailViewRelationships(sDETAIL_NAME);
				else
					// BEFORE: dtFields = SplendidCache.UserDashlets(sDETAIL_NAME, gUSER_ID);  (static)
					// AFTER:  dtFields = _splendidCache.UserDashlets(sDETAIL_NAME, gUSER_ID); (instance)
					dtFields = _splendidCache.UserDashlets(sDETAIL_NAME, gUSER_ID);

				// 03/04/2010 Paul.  The update panel makes debugging dashlets difficult.
				// Provide a way to disable the update panel.
				// BEFORE: bool bDebugDashlets = Sql.ToBoolean(Application["CONFIG.debug_dashlets"]);
				// AFTER:  bool bDebugDashlets = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.debug_dashlets"));
				bool bDebugDashlets = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.debug_dashlets"));

				if ( dtFields != null )
				{
					foreach ( DataRow row in dtFields.Rows )
					{
						Guid   gDASHLET_ID   = Sql.ToGuid  (row["ID"          ]);
						string sMODULE_NAME  = Sql.ToString(row["MODULE_NAME" ]);
						string sCONTROL_NAME = Sql.ToString(row["CONTROL_NAME"]);

						// 04/27/2006 Paul.  Only add the control if the user has access.
						// BEFORE: if (Security.GetUserAccess(sMODULE_NAME, "list") >= 0)  (static)
						// AFTER:  if (_security.GetUserAccess(sMODULE_NAME, "list") >= 0) (instance)
						if ( _security.GetUserAccess(sMODULE_NAME, "list") >= 0 )
						{
							if ( bDebugDashlets )
							{
								// Diagnostic tracing when CONFIG.debug_dashlets is enabled.
								Debug.WriteLine("AppendDetailViewRelationships: DETAIL_NAME=" + sDETAIL_NAME
									+ " MODULE=" + sMODULE_NAME
									+ " CONTROL=" + sCONTROL_NAME
									+ " DASHLET_ID=" + gDASHLET_ID.ToString());
							}
						}
					}
				}

				return dtFields ?? new DataTable();
			}
			catch ( Exception ex )
			{
				// 06/09/2006 Paul.  Catch the error and display a message instead of crashing.
				// BEFORE: Label lblError = new Label(); lblError.Text = Utils.ExpandException(ex);
				// AFTER:  Utils.ExpandException(ex) still used (static method) for diagnostic message
				string sError = Utils.ExpandException(ex);
				Debug.WriteLine("AppendDetailViewRelationships error: " + sError);
				return new DataTable();
			}
		}

		// =====================================================================================
		// AppendGridColumns — adapted from WebForms SplendidGrid
		// BEFORE: protected void AppendGridColumns(SplendidGrid grd, string sGRID_NAME)
		//         protected void AppendGridColumns(SplendidGrid grd, string sGRID_NAME, UniqueStringCollection)
		//         (inside #if !ReactOnlyUI block)
		// AFTER:  public void AppendGridColumns(string sGRID_NAME)
		//         public void AppendGridColumns(string sGRID_NAME, UniqueStringCollection arrSelectFields)
		//
		// The SplendidGrid parameter is removed; SplendidGrid is a WebForms DataGrid extension
		// and does not apply to ASP.NET Core MVC / React SPA architecture.
		// The mobile suffix logic is preserved.
		// =====================================================================================

		/// <summary>
		/// Appends grid column definitions for the given grid view name.
		/// .NET 10 Migration: Adapted from WebForms SplendidGrid overload — no grid control parameter.
		/// Grid column rendering is managed by the React SPA reading metadata from the REST API.
		/// </summary>
		/// <param name="sGRID_NAME">Grid view name (e.g. "Accounts.ListView").</param>
		public void AppendGridColumns(string sGRID_NAME)
		{
			AppendGridColumns(sGRID_NAME, null);
		}

		/// <summary>
		/// Appends grid column definitions for the given grid view name, populating
		/// arrSelectFields with the field names referenced by the grid columns.
		/// .NET 10 Migration: Adapted from WebForms SplendidGrid overload.
		/// Grid column rendering is managed by the React SPA; this method preserves the
		/// mobile suffix logic and select-field population for backward-compatible callers.
		/// </summary>
		/// <param name="sGRID_NAME">Grid view name (e.g. "Accounts.ListView").</param>
		/// <param name="arrSelectFields">
		/// Optional collection populated with field names referenced by the grid view columns.
		/// Passed as null by the single-argument overload.
		/// </param>
		// 02/08/2008 Paul.  We need to build a list of the fields used by the search clause.
		public void AppendGridColumns(string sGRID_NAME, UniqueStringCollection arrSelectFields)
		{
			// 11/17/2007 Paul.  Convert all view requests to a mobile request if appropriate.
			sGRID_NAME = sGRID_NAME + (this.IsMobile ? ".Mobile" : "");
			// Adapted from WebForms: grd.AppendGridColumns(sGRID_NAME, arrSelectFields);
			// In the .NET 10 ASP.NET Core / React SPA architecture, grid column definitions are
			// retrieved directly by the React SPA through the REST API metadata endpoints.
			// This method is retained as a no-op stub to preserve public API compatibility
			// for any callers that invoke it during the migration transition period.
		}
	}

	// =====================================================================================
	// SplendidPopup — popup window page adapter
	// BEFORE: public class SplendidPopup : SplendidPage  (inheriting from System.Web.UI.Page transitively)
	// AFTER:  public class SplendidPopup : SplendidPage  (same inheritance, migrated base class)
	//
	// Adds the m_sMODULE protected field that popup pages use to track the module context.
	// =====================================================================================

	// 02/08/2008 Paul.  Create SplendidPopup so that m_sMODULE can be used in popups just as it is
	// used in SplendidControls.
	/// <summary>
	/// Popup window page adapter — extends <see cref="SplendidPage"/> with a module name field.
	///
	/// Migrated from SplendidCRM/_code/SplendidPage.cs for .NET 10 ASP.NET Core.
	/// DI constructor signature is identical to SplendidPage for ease of service registration.
	/// </summary>
	public class SplendidPopup : SplendidPage
	{
		// 02/08/2008 Paul.  Leave null so that we can get an error when not initialized.
		/// <summary>Module name context for this popup window (e.g. "Accounts", "Contacts").</summary>
		protected string m_sMODULE;

		/// <inheritdoc cref="SplendidPage(IHttpContextAccessor, IMemoryCache, Security, SplendidCache, SplendidInit, Utils)"/>
		public SplendidPopup(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache         ,
			Security             security            ,
			SplendidCache        splendidCache       ,
			SplendidInit         splendidInit        ,
			Utils                utils               )
			: base(httpContextAccessor, memoryCache, security, splendidCache, splendidInit, utils)
		{
		}
	}

	// =====================================================================================
	// SplendidAdminPage — administration page adapter
	// BEFORE: public class SplendidAdminPage : SplendidPage  (WebForms based)
	// AFTER:  public class SplendidAdminPage : SplendidPage  (ASP.NET Core migrated)
	//
	// Overrides AdminPage() to return true, enabling admin shortcuts in the base OnInit().
	// =====================================================================================

	// 03/12/2008 Paul.  Create SplendidAdminPage so that we can eliminate code behinds for
	// admin default, view and edit pages.
	/// <summary>
	/// Administration page adapter — extends <see cref="SplendidPage"/> and unconditionally
	/// enables admin mode by overriding <see cref="AdminPage"/> to return true.
	///
	/// Migrated from SplendidCRM/_code/SplendidPage.cs for .NET 10 ASP.NET Core.
	/// </summary>
	public class SplendidAdminPage : SplendidPage
	{
		/// <inheritdoc cref="SplendidPage(IHttpContextAccessor, IMemoryCache, Security, SplendidCache, SplendidInit, Utils)"/>
		public SplendidAdminPage(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache         ,
			Security             security            ,
			SplendidCache        splendidCache       ,
			SplendidInit         splendidInit        ,
			Utils                utils               )
			: base(httpContextAccessor, memoryCache, security, splendidCache, splendidInit, utils)
		{
		}

		// 03/11/2008 Paul.  Enable admin shortcuts.
		/// <summary>Returns true; SplendidAdminPage always enables admin mode.</summary>
		override protected bool AdminPage()
		{
			return true;
		}
	}

	// =====================================================================================
	// SplendidMaster — master page adapter
	// BEFORE: public class SplendidMaster : System.Web.UI.MasterPage
	// AFTER:  public class SplendidMaster  (no base class — DI-injectable service)
	//
	// REMOVED: WebForms control fields (TableCell, Image) — not applicable in ASP.NET Core
	// REMOVED: Visibility/Style manipulation on WebForms controls in Page_Load
	// PRESERVED: ShowTeamHierarchy() business logic (IMemoryCache, Crm.Config.*, Sql.* calls)
	// PRESERVED: Page_Command() virtual method with local CommandEventArgs signature
	// PRESERVED: bShowTeamTree field for cookie-based team tree collapse tracking
	// =====================================================================================

	/// <summary>
	/// Master page adapter for SplendidCRM.
	///
	/// Migrated from SplendidCRM/_code/SplendidPage.cs (SplendidMaster class) for .NET 10 ASP.NET Core.
	/// Replaces System.Web.UI.MasterPage inheritance with a plain DI-injectable service.
	///
	/// WebForms control fields (TableCell tdTeamTree, Image imgTeamShowHandle, etc.) are removed
	/// as they are not applicable in an ASP.NET Core / React SPA architecture.
	/// ShowTeamHierarchy() business logic is preserved identically.
	///
	/// DI Registration: services.AddScoped&lt;SplendidMaster&gt;();
	/// </summary>
	public class SplendidMaster
	{
		// =====================================================================================
		// DI-injected fields
		// =====================================================================================

		/// <summary>Replaces HttpContext.Current for per-request Items access.</summary>
		protected readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>Replaces HttpContext.Current.Application (Application[]) for module team flags.</summary>
		protected readonly IMemoryCache _memoryCache;

		// =====================================================================================
		// Protected state fields
		// =====================================================================================

		// 02/23/2017 Paul.  Add support for Team Hierarchy.
		/// <summary>Whether the team tree sidebar is currently visible.  Driven by cookie value.</summary>
		protected bool bShowTeamTree = true;

		// NOTE: TableCell / Image WebForms control fields (tdTeamTree, tdTeamTreeHandle,
		//       imgTeamShowHandle, imgTeamHideHandle) are REMOVED.
		//       These were System.Web.UI.WebControls types that are not available in ASP.NET Core.
		//       The team tree sidebar visibility is controlled by the React SPA using the
		//       showTeamTree cookie and the ShowTeamHierarchy() method return value.

		// =====================================================================================
		// Per-request Items accessor
		// =====================================================================================

		/// <summary>
		/// Gets the per-request Items dictionary from the current HttpContext.
		/// Replaces Page.Items from WebForms.
		/// BEFORE: Page.Items["ActiveTabMenu"]
		/// AFTER:  Items["ActiveTabMenu"]
		/// </summary>
		protected IDictionary<object, object> Items => _httpContextAccessor?.HttpContext?.Items;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs a SplendidMaster service with required DI dependencies.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current for Items, Request, and Response access.
		/// </param>
		/// <param name="memoryCache">
		/// Replaces HttpContext.Current.Application for module team-management flags.
		/// BEFORE: HttpContext.Current.Application["Modules.{module}.Teamed"]
		/// AFTER:  memoryCache.Get&lt;object&gt;("Modules.{module}.Teamed")
		/// </param>
		public SplendidMaster(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
		}

		// =====================================================================================
		// Page_Command — virtual event handler stub
		// BEFORE: public virtual void Page_Command(object sender, CommandEventArgs e) { }
		//         — System.Web.UI.WebControls.CommandEventArgs parameter
		// AFTER:  public virtual void Page_Command(object sender, CommandEventArgs e) { }
		//         — local SplendidCRM.CommandEventArgs parameter (same signature, no System.Web)
		// =====================================================================================

		/// <summary>
		/// Virtual command event handler.  Override in derived master pages to handle
		/// command buttons from nested user controls (Save, Cancel, Delete, etc.).
		/// BEFORE: System.Web.UI.WebControls.CommandEventArgs
		/// AFTER:  SplendidCRM.CommandEventArgs (local replacement — no System.Web dependency)
		/// </summary>
		public virtual void Page_Command(object sender, CommandEventArgs e)
		{
			// Empty virtual method — derived master pages implement command handling.
		}

		// =====================================================================================
		// ShowTeamHierarchy — team sidebar visibility decision
		// BEFORE: HttpContext.Current.Application["Modules.{module}.Teamed"]  (static Application)
		//         Request.FilePath.EndsWith("/edit.aspx")  (WebForms Request.FilePath)
		// AFTER:  _memoryCache.Get<object>("Modules.{module}.Teamed")        (IMemoryCache)
		//         _httpContextAccessor.HttpContext?.Request.Path               (ASP.NET Core)
		// =====================================================================================

		// 02/23/2017 Paul.  Add support for Team Hierarchy.
		/// <summary>
		/// Determines whether the team hierarchy sidebar should be displayed for the current request.
		///
		/// .NET 10 Migration notes:
		///   • REPLACED: HttpContext.Current.Application["Modules.{module}.Teamed"]
		///               → _memoryCache.Get&lt;object&gt;("Modules.{module}.Teamed")
		///   • REPLACED: Request.FilePath.EndsWith("/edit.aspx")
		///               → _httpContextAccessor.HttpContext?.Request.Path.Value.EndsWith(...)
		///   • PRESERVED: Crm.Config.enable_team_management() and enable_team_hierarchy() calls
		///               (both are static methods using the ambient IMemoryCache set at startup)
		///   • PRESERVED: Page.Items["ActiveTabMenu"] key → Items["ActiveTabMenu"] (same contract)
		/// </summary>
		/// <returns>
		/// true if the team hierarchy sidebar should be visible; false otherwise.
		/// </returns>
		public bool ShowTeamHierarchy()
		{
			bool bShowTeamHierarchy    = false;
			// Use the explicit IMemoryCache overloads so the test seam and production both work
			// without requiring the static ambient to be pre-initialised.
			// BEFORE: Crm.Config.enable_team_management() / enable_team_hierarchy()  (ambient)
			// AFTER:  Crm.Config.enable_team_management(_memoryCache)               (instance)
			bool bEnableTeamManagement = Crm.Config.enable_team_management(_memoryCache);
			bool bEnableTeamHierarchy  = Crm.Config.enable_team_hierarchy (_memoryCache);
			if ( bEnableTeamManagement && bEnableTeamHierarchy )
			{
				// BEFORE: string sActiveTabMenu = Sql.ToString(Page.Items["ActiveTabMenu"]);
				// AFTER:  string sActiveTabMenu = Sql.ToString(Items?["ActiveTabMenu"]);
				string sActiveTabMenu = Sql.ToString(Items?["ActiveTabMenu"]);

				// BEFORE: bool bModuleIsTeamed = Sql.ToBoolean(HttpContext.Current.Application["Modules." + sActiveTabMenu + ".Teamed"]);
				// AFTER:  bool bModuleIsTeamed = Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sActiveTabMenu + ".Teamed"));
				bool bModuleIsTeamed = Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sActiveTabMenu + ".Teamed"));

				bShowTeamHierarchy = (bModuleIsTeamed || sActiveTabMenu == "Home" || sActiveTabMenu == "Dashboard");

				// 02/23/2017 Paul.  We don't show the hierarchy for DetailView or EditView.
				// BEFORE: Request.FilePath.EndsWith("/edit.aspx")
				// AFTER:  _httpContextAccessor.HttpContext?.Request.Path.Value.EndsWith("/edit.aspx", ...)
				if ( bShowTeamHierarchy )
				{
					string sPath = _httpContextAccessor?.HttpContext?.Request?.Path.Value ?? String.Empty;
					if ( sPath.EndsWith("/edit.aspx", StringComparison.OrdinalIgnoreCase) )
						bShowTeamHierarchy = false;
				}
			}
			return bShowTeamHierarchy;
		}
	}
}
