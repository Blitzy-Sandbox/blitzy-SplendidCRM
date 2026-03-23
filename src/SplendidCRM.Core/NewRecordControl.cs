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
// .NET 10 Migration: SplendidCRM/_code/NewRecordControl.cs → src/SplendidCRM.Core/NewRecordControl.cs
// Changes applied:
//   - REMOVED: using System.Web.UI.WebControls; (WebForms-only namespace — Unit, CommandEventHandler defined there)
//   - REMOVED: Implicit System.Web.UI.UserControl base class chain
//   - ADDED:   using Microsoft.AspNetCore.Http; (IHttpContextAccessor — replaces HttpContext.Current static access)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache — replaces Application[] static access)
//   - ADDED:   Constructor accepting IHttpContextAccessor and IMemoryCache, forwarding to InlineEditControl base
//   - REPLACED: Unit uWidth (System.Web.UI.WebControls.Unit, WebForms-only) → string uWidth
//               preserving the default CSS measurement "100%" as a string literal
//   - REPLACED: ViewState["PARENT_ID"] / ViewState["PARENT_TYPE"] backing store
//               → private fields _parentId (Guid) and _parentType (string)
//               ViewState was WebForms-specific per-control postback state; no equivalent in ASP.NET Core.
//               A simple private backing field preserves the identical API contract (get/set pair).
//   - REPLACED: Request["PARENT_ID"] fallback
//               → _httpContextAccessor.HttpContext?.Request.Query["PARENT_ID"].ToString()
//               The WebForms Request indexer accessed GET/POST parameters. In ASP.NET Core,
//               Request.Query provides the equivalent GET parameter access.
//   - PRESERVED: CommandEventHandler delegate type — now defined locally in SplendidControl.cs
//               (namespace SplendidCRM) replacing System.Web.UI.WebControls.CommandEventHandler
//   - PRESERVED: EventHandler delegate (System standard library, unchanged)
//   - PRESERVED: namespace SplendidCRM (unchanged for backward compatibility)
//   - PRESERVED: All public property signatures: PARENT_ID, PARENT_TYPE, EditView, Width,
//               ShowTopButtons, ShowBottomButtons, ShowHeader, ShowInlineHeader, ShowCancel, ShowFullForm
//   - PRESERVED: All public event fields: Command (CommandEventHandler), EditViewLoad (EventHandler)
//   - PRESERVED: InlineEditControl base class inheritance (migrated separately to .NET 10)
//   - PRESERVED: Minimal change clause — only changes required for .NET Framework 4.8 → .NET 10 migration
#nullable disable
using System;
using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Base control for creating new CRM records inline within a parent detail or list view.
	///
	/// Migrated from SplendidCRM/_code/NewRecordControl.cs for .NET 10 ASP.NET Core.
	///
	/// BEFORE (.NET Framework 4.8):
	///   public class NewRecordControl : InlineEditControl
	///   where InlineEditControl : SplendidControl : System.Web.UI.UserControl
	///   WebForms runtime managed lifecycle; ViewState provided postback-surviving per-control state;
	///   Request indexed over both QueryString and Form values.
	///
	/// AFTER (.NET 10 ASP.NET Core):
	///   public class NewRecordControl : InlineEditControl
	///   where InlineEditControl : SplendidControl (DI-injectable, no System.Web dependency)
	///   Constructor injection replaces static HttpContext.Current / Application[] patterns.
	///   Private backing fields replace ViewState per-control postback state.
	///   IHttpContextAccessor.HttpContext.Request.Query replaces Request[] indexer.
	///
	/// Migration changes summary:
	///   - Removed:  using System.Web.UI.WebControls (Unit, CommandEventHandler)
	///   - Replaced: Unit → string for Width property (CSS measurement preserved as "100%")
	///   - Replaced: ViewState["PARENT_ID"] → private Guid _parentId backing field
	///   - Replaced: ViewState["PARENT_TYPE"] → private string _parentType backing field
	///   - Replaced: Request["PARENT_ID"] → _httpContextAccessor.HttpContext?.Request.Query["PARENT_ID"]
	///   - Added:    Constructor forwarding IHttpContextAccessor + IMemoryCache to base
	///   - Preserved: All public property and event field signatures
	///   - Preserved: namespace SplendidCRM, class name NewRecordControl
	/// </summary>
	public class NewRecordControl : InlineEditControl
	{
		// =====================================================================================
		// Protected configuration fields — preserved from original source with identical names.
		// CHANGE: uWidth type changed from Unit (System.Web.UI.WebControls) to string.
		//         Unit was a WebForms measurement struct wrapping CSS values like "100%" or "300px".
		//         string is the minimal equivalent for .NET 10 (no semantic loss for a layout hint).
		// =====================================================================================

		/// <summary>The edit view layout name. Default: "NewRecord".</summary>
		protected string sEditView          = "NewRecord";

		/// <summary>
		/// CSS width value for the control.
		/// BEFORE: protected Unit uWidth = new Unit("100%"); (System.Web.UI.WebControls.Unit)
		/// AFTER:  protected string uWidth = "100%";
		/// Unit is WebForms-only. Replaced with string to preserve the CSS width measurement.
		/// </summary>
		protected string uWidth             = "100%";

		/// <summary>Whether to render action buttons above the inline form. Default: false.</summary>
		protected bool   bShowTopButtons    = false;

		/// <summary>Whether to render action buttons below the inline form. Default: true.</summary>
		protected bool   bShowBottomButtons = true ;

		/// <summary>Whether to render the module header section. Default: true.</summary>
		protected bool   bShowHeader        = true ;

		/// <summary>Whether to render a compact inline header instead of the full header. Default: false.</summary>
		protected bool   bShowInlineHeader  = false;

		/// <summary>Whether to render the full expanded form (vs. inline collapsed). Default: false.</summary>
		protected bool   bShowFullForm      = false;

		/// <summary>Whether to render a Cancel button in the inline form. Default: false.</summary>
		protected bool   bShowCancel        = false;

		// =====================================================================================
		// ViewState replacement backing fields
		//
		// BEFORE (.NET Framework 4.8):
		//   ViewState["PARENT_ID"]   — ASP.NET WebForms per-control hidden-field postback store
		//   ViewState["PARENT_TYPE"] — persisted across postbacks automatically by WebForms
		//
		// AFTER (.NET 10 ASP.NET Core):
		//   Private instance fields — provide identical get/set semantics without WebForms.
		//   State lifetime: scoped to the current request/component instance (same as a
		//   single WebForms postback cycle in non-postback NewRecord scenarios).
		// =====================================================================================

		/// <summary>
		/// Backing field replacing ViewState["PARENT_ID"].
		/// Guid.Empty is the default — same as Sql.ToGuid(null) returned from an absent ViewState entry.
		/// </summary>
		private Guid   _parentId   = Guid.Empty;

		/// <summary>
		/// Backing field replacing ViewState["PARENT_TYPE"].
		/// null is the default — Sql.ToString(null) returns string.Empty, matching original behaviour.
		/// </summary>
		private string _parentType = null;

		// =====================================================================================
		// Public event fields
		// PRESERVED: Identical to original source with identical types and names.
		//
		// CommandEventHandler: Originally System.Web.UI.WebControls.CommandEventHandler.
		//   AFTER: Defined locally in SplendidControl.cs (same SplendidCRM namespace) as:
		//     public delegate void CommandEventHandler(object sender, CommandEventArgs e);
		//   No import required — same namespace resolution.
		//
		// EventHandler: System.EventHandler — standard library, unchanged between .NET Framework and .NET 10.
		// =====================================================================================

		// 05/06/2010 Paul.  We need a common way to attach a command from the Toolbar. 
		/// <summary>
		/// Toolbar command event.
		/// BEFORE: System.Web.UI.WebControls.CommandEventHandler (WebForms)
		/// AFTER:  SplendidCRM.CommandEventHandler (defined in SplendidControl.cs, same namespace)
		/// </summary>
		public CommandEventHandler Command     ;

		// 06/04/2010 Paul.  Generate a load event so that the fields can be populated. 
		/// <summary>
		/// Fires after the inline edit view is loaded and fields are initialized.
		/// System.EventHandler — unchanged from .NET Framework to .NET 10.
		/// </summary>
		public EventHandler        EditViewLoad;

		// =====================================================================================
		// Constructor
		//
		// BEFORE (.NET Framework 4.8):
		//   No explicit constructor — the ASP.NET WebForms runtime instantiated user controls
		//   automatically via compiled ASPX page infrastructure. Dependencies resolved via static
		//   HttpContext.Current and Application[] access patterns inherited through SplendidControl.
		//
		// AFTER (.NET 10 ASP.NET Core):
		//   Explicit constructor required for .NET 10 DI container. Forwards IHttpContextAccessor
		//   and IMemoryCache to InlineEditControl base, which in turn forwards to SplendidControl.
		//   This satisfies the SplendidControl(IHttpContextAccessor, IMemoryCache, ...) constructor chain.
		// =====================================================================================

		/// <summary>
		/// Constructs a NewRecordControl with required DI dependencies.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current for request, session, and response access.
		/// BEFORE: HttpContext.Current.Request["PARENT_ID"] (WebForms Request indexer)
		/// AFTER:  _httpContextAccessor.HttpContext?.Request.Query["PARENT_ID"] (ASP.NET Core)
		/// Forwarded through: NewRecordControl → InlineEditControl → SplendidControl._httpContextAccessor
		/// </param>
		/// <param name="memoryCache">
		/// Replaces Application[] (HttpApplicationState) for cached application state access.
		/// BEFORE: Application["key"]
		/// AFTER:  _memoryCache.Get&lt;object&gt;("key")
		/// Forwarded through: NewRecordControl → InlineEditControl → SplendidControl._memoryCache
		/// </param>
		public NewRecordControl(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
			: base(httpContextAccessor, memoryCache)
		{
		}

		// =====================================================================================
		// Public properties
		// PRESERVED: All property names, access modifiers, and getter/setter patterns.
		// CHANGED:   PARENT_ID/PARENT_TYPE — ViewState replaced by backing fields + Request.Query
		//            Width — Unit type replaced by string type
		// =====================================================================================

		// 05/05/2010 Paul.  We need a common way to access the parent from the Toolbar. 
		/// <summary>
		/// Gets or sets the parent record identifier for this inline new-record control.
		///
		/// BEFORE (.NET Framework 4.8):
		///   get — Reads Sql.ToGuid(ViewState["PARENT_ID"]) with Request["PARENT_ID"] fallback.
		///         ViewState provided per-control postback-surviving hidden-field storage.
		///         Request accessed both QueryString and Form POST values via a unified indexer.
		///   set — Writes value to ViewState["PARENT_ID"] for postback roundtrip persistence.
		///
		/// AFTER (.NET 10 ASP.NET Core):
		///   get — Reads private _parentId field (replaces ViewState["PARENT_ID"]).
		///         Falls back to _httpContextAccessor.HttpContext?.Request.Query["PARENT_ID"]
		///         (replaces Request["PARENT_ID"] — Query string is the primary parameter source
		///         for NewRecord scenarios; POST body is handled at controller level).
		///   set — Writes to _parentId backing field (replaces ViewState["PARENT_ID"]).
		///
		/// Comment preserved from original:
		///   02/21/2010 Paul.  An EditView Inline will use the ViewState, and a NewRecord Inline will use the Request.
		/// </summary>
		public Guid PARENT_ID
		{
			get
			{
				// 02/21/2010 Paul.  An EditView Inline will use the ViewState, and a NewRecord Inline will use the Request. 
				// BEFORE: Guid gPARENT_ID = Sql.ToGuid(ViewState["PARENT_ID"]);
				// AFTER:  Read from private backing field (replaces ViewState["PARENT_ID"])
				Guid gPARENT_ID = _parentId;
				if ( Sql.IsEmptyGuid(gPARENT_ID) )
				{
					// BEFORE: gPARENT_ID = Sql.ToGuid(Request["PARENT_ID"]);
					// AFTER:  Request.Query["PARENT_ID"] replaces the WebForms Request[] indexer.
					//         StringValues.ToString() returns string.Empty for missing keys,
					//         equivalent to null returned by the WebForms Request indexer.
					gPARENT_ID = Sql.ToGuid(_httpContextAccessor?.HttpContext?.Request?.Query["PARENT_ID"].ToString());
				}
				return gPARENT_ID;
			}
			set
			{
				// BEFORE: ViewState["PARENT_ID"] = value;
				// AFTER:  Write to private backing field (replaces ViewState["PARENT_ID"])
				_parentId = value;
			}
		}

		/// <summary>
		/// Gets or sets the parent module type name for this inline new-record control
		/// (e.g., "Accounts", "Contacts", "Opportunities").
		///
		/// BEFORE (.NET Framework 4.8):
		///   get — Returns Sql.ToString(ViewState["PARENT_TYPE"]).
		///   set — Writes to ViewState["PARENT_TYPE"].
		///
		/// AFTER (.NET 10 ASP.NET Core):
		///   get — Returns Sql.ToString(_parentType).
		///         Sql.ToString(null) returns string.Empty — identical to original behaviour
		///         when ViewState["PARENT_TYPE"] was absent (returned null).
		///   set — Writes to _parentType backing field (replaces ViewState["PARENT_TYPE"]).
		/// </summary>
		public string PARENT_TYPE
		{
			get
			{
				// BEFORE: return Sql.ToString(ViewState["PARENT_TYPE"]);
				// AFTER:  Return Sql.ToString(_parentType) — identical null→empty conversion
				return Sql.ToString(_parentType);
			}
			set
			{
				// BEFORE: ViewState["PARENT_TYPE"] = value;
				// AFTER:  Write to private backing field
				_parentType = value;
			}
		}

		// 04/19/2010 Paul.  Allow the EditView to be redefined. 
		/// <summary>
		/// Gets or sets the edit view layout name. Default: "NewRecord".
		/// Identifies which EditView metadata record to load from SplendidCache for field layout.
		/// </summary>
		public string EditView
		{
			get { return sEditView; }
			set { sEditView = value; }
		}

		/// <summary>
		/// Gets or sets the CSS width value for the inline control container.
		///
		/// BEFORE (.NET Framework 4.8):
		///   public Unit Width — System.Web.UI.WebControls.Unit wrapping "100%".
		///   Unit is a WebForms-specific CSS measurement struct (Width, Height properties on controls).
		///
		/// AFTER (.NET 10 ASP.NET Core):
		///   public string Width — CSS measurement string (e.g., "100%", "300px").
		///   string is the minimal equivalent for .NET 10. The caller rendered "100%" as a CSS value
		///   in WebForms; the same string value is used directly in ASP.NET Core / React rendering.
		/// </summary>
		public string Width
		{
			get { return uWidth; }
			set { uWidth = value; }
		}

		/// <summary>
		/// Gets or sets whether action buttons are rendered above the inline form.
		/// Default: false — bottom buttons only by default.
		/// </summary>
		public bool ShowTopButtons
		{
			get { return bShowTopButtons; }
			set { bShowTopButtons = value; }
		}

		/// <summary>
		/// Gets or sets whether action buttons are rendered below the inline form.
		/// Default: true — bottom buttons are shown by default.
		/// </summary>
		public bool ShowBottomButtons
		{
			get { return bShowBottomButtons; }
			set { bShowBottomButtons = value; }
		}

		/// <summary>
		/// Gets or sets whether the module title header is rendered above the inline form.
		/// Default: true — header is shown by default.
		/// </summary>
		public bool ShowHeader
		{
			get { return bShowHeader; }
			set { bShowHeader = value; }
		}

		/// <summary>
		/// Gets or sets whether a compact inline header is rendered instead of the full panel header.
		/// Default: false — full header is used by default.
		/// </summary>
		public bool ShowInlineHeader
		{
			get { return bShowInlineHeader; }
			set { bShowInlineHeader = value; }
		}

		/// <summary>
		/// Gets or sets whether a Cancel button is rendered within the inline form.
		/// Default: false — no Cancel button by default (parent dismisses the form).
		/// </summary>
		public bool ShowCancel
		{
			get { return bShowCancel; }
			set { bShowCancel = value; }
		}

		/// <summary>
		/// Gets or sets whether the full expanded form is rendered (true) or the inline
		/// collapsed quick-entry form (false).
		/// Default: false — inline quick-entry form by default.
		/// </summary>
		public bool ShowFullForm
		{
			get { return bShowFullForm; }
			set { bShowFullForm = value; }
		}
	}
}
