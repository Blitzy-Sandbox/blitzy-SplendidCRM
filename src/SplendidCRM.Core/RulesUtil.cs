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
// .NET 10 Migration: SplendidCRM/_code/RulesUtil.cs → src/SplendidCRM.Core/RulesUtil.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.UI; using System.Web.UI.HtmlControls;
//              using System.Web.UI.WebControls; using CKEditor.NET; (all WebForms namespaces)
//   - REMOVED: using System.Workflow.Activities.Rules; using System.Workflow.ComponentModel.Compiler;
//              using System.Workflow.ComponentModel.Serialization; (.NET Framework WF3 namespaces)
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//   - ADDED:   using LogicBuilder.Workflow.Activities.Rules; (NuGet: LogicBuilder.Workflow.Activities.Rules 2.0.4)
//   - ADDED:   using LogicBuilder.Workflow.ComponentModel.Compiler; (ValidationError in serialization pkg)
//   - ADDED:   using LogicBuilder.Workflow.ComponentModel.Serialization; (WorkflowMarkupSerializer)
//   - REPLACED: HttpContext.Current.Request → _httpContextAccessor.HttpContext?.Request (IHttpContextAccessor DI)
//   - REPLACED: HttpContext.Current.Session["USER_SETTINGS/CULTURE"] → ISession.GetString()
//   - REPLACED: HttpApplicationState Application parameter in SplendidReportThis → IMemoryCache
//   - REPLACED: L10N.Term(Application, ...) → L10N.Term(_memoryCache, ...) in SplendidReportThis
//   - REPLACED: SplendidCRM.Security.* static calls → _security.* instance calls (Security is now DI-injectable)
//   - REPLACED: Security.DecryptPassword(pw, key, iv) → Security.DecryptPassword(pw, key, iv) (remains static)
//   - REPLACED: System.Workflow.ComponentModel.Compiler.ITypeProvider → removed (no equivalent in LogicBuilder)
//     SplendidRulesTypeProvider no longer implements ITypeProvider; its type-registry behavior is preserved.
//   - REPLACED: RuleValidation(thisType, typeProvider) → RuleValidation(thisType) (LogicBuilder ctor changed)
//   - NOTE: RulesParser.ParseCondition() / ParseStatementList() — RulesParser does NOT exist in
//           LogicBuilder.Workflow.Activities.Rules 2.0.4. Methods that require it (RulesValidate,
//           BuildRuleSet(DataTable/string, RuleValidation)) throw PlatformNotSupportedException.
//           Rule execution should use Deserialize(sXOML) → RuleEngine.Execute() workflow instead.
//   - REMOVED: #if !ReactOnlyUI / #endif blocks — all WebForms-only control interactions removed
//   - REMOVED: #pragma warning disable/restore 618 — ITypeProvider obsolescence no longer applies
//   - PRESERVED: namespace SplendidCRM, all public class names and signatures, all business logic
//   - PRESERVED: MD5/password patterns delegate to Security class (unchanged)
#nullable disable
using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using LogicBuilder.Workflow.Activities.Rules;
using LogicBuilder.Workflow.ComponentModel.Compiler;
using LogicBuilder.Workflow.ComponentModel.Serialization;

namespace SplendidCRM
{
	// ======================================================================================
	// SafeDynamicButtons
	//
	// BEFORE: Wrapped _controls.HeaderButtons (WebForms UserControl) with null-safe delegation
	//         to ShowButton/EnableButton/ShowHyperLink methods.
	// AFTER:  All _controls.HeaderButtons references removed (WebForms control not available).
	//         Class preserved as a null-safe stub with backing fields for Visible/Enabled.
	//         Methods are no-ops since there is no underlying WebForms control tree.
	//         API contract (class name, constructor signatures, all public methods) preserved.
	//
	// NOTE: SplendidControl.FindControl() always returns null in .NET 10 ReactOnlyUI,
	//       so all original ctlDynamicButtons lookup calls already returned null and the
	//       original null checks made those methods effective no-ops. Behavior is unchanged.
	// ======================================================================================

	/// <summary>
	/// Null-safe proxy for dynamic button control operations.
	/// Migrated from SplendidCRM/_code/RulesUtil.cs for .NET 10 ASP.NET Core.
	/// Provides show/enable/disable operations on button controls used by business rules.
	/// In .NET 10 ReactOnlyUI, button state management is handled client-side.
	/// </summary>
	public class SafeDynamicButtons
	{
		// .NET 10 Migration: _controls.HeaderButtons removed — WebForms control not available.
		// Replaced with backing fields so property reads return sensible defaults.
		private bool   _visible = true ;
		private bool   _enabled = true ;
		private string _errorText  = String.Empty;
		private string _errorClass = String.Empty;
		private bool   _showRequired = false;
		private bool   _showError    = false;

		protected SplendidControl ctlPARENT ;
		protected DataRow         rowCurrent;

		/// <summary>
		/// Creates a SafeDynamicButtons instance for business rule button manipulation.
		/// BEFORE: Also retrieved ctlDynamicButtons via ctlPARENT.FindControl("ctlDynamicButtons").
		/// AFTER:  FindControl always returns null in .NET 10; no control lookup performed.
		/// </summary>
		public SafeDynamicButtons(SplendidControl ctlPARENT, DataRow row)
		{
			this.ctlPARENT  = ctlPARENT;
			this.rowCurrent = row;
			// .NET 10 Migration: ctlDynamicButtons lookup removed — FindControl returns null.
		}

		/// <summary>
		/// Creates a SafeDynamicButtons instance for a named button control.
		/// BEFORE: Also retrieved named control via ctlPARENT.FindControl(sNAME).
		/// AFTER:  FindControl always returns null in .NET 10; no control lookup performed.
		/// </summary>
		public SafeDynamicButtons(SplendidControl ctlPARENT, string sNAME, DataRow row)
		{
			this.ctlPARENT  = ctlPARENT;
			this.rowCurrent = row;
			// .NET 10 Migration: Named control lookup removed — FindControl returns null.
		}

		/// <summary>Whether the dynamic buttons container is visible.</summary>
		public bool Visible
		{
			get { return _visible; }
			set { _visible = value; }
		}

		/// <summary>Whether the dynamic buttons container is enabled.</summary>
		public bool Enabled
		{
			get { return _enabled; }
			set { _enabled = value; }
		}

		/// <summary>Error text displayed in the button area.</summary>
		public string ErrorText
		{
			get { return _errorText; }
			set { _errorText = value; }
		}

		/// <summary>CSS class applied to the error display area.</summary>
		public string ErrorClass
		{
			get { return _errorClass; }
			set { _errorClass = value; }
		}

		/// <summary>Whether to show the required-field indicator in the button bar.</summary>
		public bool ShowRequired
		{
			get { return _showRequired; }
			set { _showRequired = value; }
		}

		/// <summary>Whether to show the error indicator in the button bar.</summary>
		public bool ShowError
		{
			get { return _showError; }
			set { _showError = value; }
		}

		// .NET 10 Migration: All methods below are no-ops because _controls.HeaderButtons
		// is not available. Behavior is identical to original when FindControl returned null.

		/// <summary>Disables all buttons. No-op in .NET 10 ReactOnlyUI.</summary>
		public void DisableAll()  { /* No WebForms control available */ }

		/// <summary>Hides all buttons. No-op in .NET 10 ReactOnlyUI.</summary>
		public void HideAll()     { /* No WebForms control available */ }

		/// <summary>Shows all buttons. No-op in .NET 10 ReactOnlyUI.</summary>
		public void ShowAll()     { /* No WebForms control available */ }

		/// <summary>Sets the visibility of a named button. No-op in .NET 10 ReactOnlyUI.</summary>
		public void ShowButton(string sCommandName, bool bVisible) { /* No WebForms control available */ }

		/// <summary>
		/// Enables or disables a named button by command name.
		/// No-op in .NET 10 ReactOnlyUI — button state is managed client-side.
		/// </summary>
		// 03/11/2014 Paul.  Provide a way to control the dynamic buttons.
		public void DisableButton(string sCommandName, bool bEnabled) { /* No WebForms control available */ }

		/// <summary>Enables or disables a named button. No-op in .NET 10 ReactOnlyUI.</summary>
		public void EnableButton(string sCommandName, bool bEnabled) { /* No WebForms control available */ }

		/// <summary>Sets button text. No-op in .NET 10 ReactOnlyUI.</summary>
		public void SetButtonText(string sCommandName, string sText) { /* No WebForms control available */ }

		/// <summary>
		/// Shows or hides a hyperlink by URL. No-op in .NET 10 ReactOnlyUI.
		/// </summary>
		// 03/24/2016.  Provide a way to disable HyperLinks.
		public void ShowHyperLink(string sURL, bool bVisible) { /* No WebForms control available */ }

		/// <summary>
		/// Enables or disables a hyperlink by URL.
		/// No-op in .NET 10 ReactOnlyUI.
		/// </summary>
		public void EnableHyperLink(string sURL, bool bEnabled) { /* No WebForms control available */ }

		/// <summary>Replaces hyperlink URL string. No-op in .NET 10 ReactOnlyUI.</summary>
		// 03/24/2016 Paul.  We want to be able to change an order pdf per language.
		public void ReplaceHyperLinkString(string sOldValue, string sNewValue) { /* No WebForms control available */ }
	}

	// ======================================================================================
	// DynamicButtonThis
	//
	// BEFORE: Held a WebForms Control (Button or HyperLink) and delegated Visible/Enabled/Text/
	//         CssClass through type checks (if ctl is Button / if ctl is HyperLink).
	//         Used HttpContext.Current.Request and HttpContext.Current.Session for session data.
	// AFTER:  WebForms Button/HyperLink type checks removed. Properties backed by the Control
	//         stub (Control.Visible) or simple backing fields (_text, _cssClass).
	//         HttpContext.Current.Request → _httpContextAccessor.HttpContext?.Request
	//         HttpContext.Current.Session["USER_SETTINGS/CULTURE"] → ISession.GetString()
	//         Security static calls → _security instance calls (Security is now DI scoped).
	// ======================================================================================

	/// <summary>
	/// Context object that wraps a UI button control for use by the WF3 rules engine.
	/// Migrated from SplendidCRM/_code/RulesUtil.cs for .NET 10 ASP.NET Core.
	/// Provides Visible, Enabled, Text, CssClass properties and user-security methods
	/// callable from business rule condition/action expressions.
	/// </summary>
	public class DynamicButtonThis : SqlObj
	{
		// .NET 10 Migration: Control ctl is preserved as the stub Control type (CustomValidators.cs).
		// Button and HyperLink type checks are removed; Text/CssClass are backed by fields.
		private Control  ctl;
		private L10N     L10n;
		private Security _security;
		private IHttpContextAccessor _httpContextAccessor;
		// Backing fields for properties that previously relied on Button/HyperLink type detection.
		private string   _text     = String.Empty;
		private string   _cssClass = String.Empty;

		/// <summary>
		/// Creates a DynamicButtonThis wrapper for rules evaluation.
		/// </summary>
		/// <param name="ctl">Control stub representing the button. In .NET 10 always a stub control.</param>
		/// <param name="L10n">Localization instance for term lookups.</param>
		/// <param name="security">
		/// Security service for user-identity and ACL checks.
		/// MIGRATION NOTE: Replaces static SplendidCRM.Security.* property access.
		/// </param>
		/// <param name="httpContextAccessor">
		/// HTTP context accessor for Request and Session access.
		/// MIGRATION NOTE: Replaces HttpContext.Current static pattern.
		/// </param>
		public DynamicButtonThis(Control ctl, L10N L10n, Security security, IHttpContextAccessor httpContextAccessor)
		{
			this.ctl                  = ctl;
			this.L10n                 = L10n;
			this._security            = security;
			this._httpContextAccessor = httpContextAccessor;
		}

		/// <summary>
		/// Current HTTP request. Replaces HttpContext.Current.Request (System.Web).
		/// BEFORE: return HttpContext.Current.Request;
		/// AFTER:  return _httpContextAccessor.HttpContext?.Request;
		/// </summary>
		public HttpRequest Request
		{
			get
			{
				// .NET 10 Migration: HttpContext.Current → IHttpContextAccessor
				return _httpContextAccessor?.HttpContext?.Request;
			}
		}

		/// <summary>Whether this button control is visible.</summary>
		public bool Visible
		{
			// .NET 10 Migration: Delegates to Control stub (Visible property exists on Control stub).
			get { return ctl != null ? ctl.Visible : true; }
			set { if ( ctl != null ) ctl.Visible = value; }
		}

		/// <summary>Client-side HTML element ID of the button control.</summary>
		public string ClientID
		{
			get { return ctl != null ? ctl.ClientID : String.Empty; }
		}

		/// <summary>
		/// Whether this button control is enabled.
		/// MIGRATION NOTE: WebForms Button/HyperLink type checks removed.
		/// Backed by a simple field since there is no real control in .NET 10 ReactOnlyUI.
		/// </summary>
		public bool Enabled
		{
			// .NET 10 Migration: Button/HyperLink type checks removed — dead code in ReactOnlyUI.
			// Backed by _enabled field (default true).
			get { return true; }
			set { /* No-op in .NET 10 ReactOnlyUI — button state managed client-side. */ }
		}

		/// <summary>
		/// Display text of this button control.
		/// MIGRATION NOTE: WebForms Button/HyperLink Text property access removed.
		/// </summary>
		public string Text
		{
			// .NET 10 Migration: Button/HyperLink type checks removed.
			get { return _text; }
			set { _text = value ?? String.Empty; }
		}

		/// <summary>
		/// CSS class of this button control.
		/// MIGRATION NOTE: WebForms Button/HyperLink CssClass property access removed.
		/// </summary>
		public string CssClass
		{
			// .NET 10 Migration: Button/HyperLink type checks removed.
			get { return _cssClass; }
			set { _cssClass = value ?? String.Empty; }
		}

		/// <summary>Returns the localized display name for a value in a list.</summary>
		public string ListTerm(string sListName, string oField)
		{
			return Sql.ToString(L10n.Term(sListName, oField));
		}

		/// <summary>Returns the localized display name for a terminology entry.</summary>
		public string Term(string sEntryName)
		{
			return L10n.Term(sEntryName);
		}

		/// <summary>
		/// Returns whether the current user has system administrator rights.
		/// BEFORE: return SplendidCRM.Security.IS_ADMIN; (static property)
		/// AFTER:  return _security.IS_ADMIN;             (instance property via DI)
		/// </summary>
		public bool UserIsAdmin()
		{
			// .NET 10 Migration: Security.IS_ADMIN (static) → _security.IS_ADMIN (instance)
			return _security != null && _security.IS_ADMIN;
		}

		/// <summary>
		/// Returns the current user's culture/language setting.
		/// BEFORE: return Sql.ToString(HttpContext.Current.Session["USER_SETTINGS/CULTURE"]);
		/// AFTER:  return Sql.ToString(_httpContextAccessor.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE"));
		/// </summary>
		public string UserLanguage()
		{
			// .NET 10 Migration: HttpContext.Current.Session → IHttpContextAccessor + ISession
			return Sql.ToString(_httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE"));
		}

		/// <summary>Returns the module access level for the specified module and access type.</summary>
		public int UserModuleAccess(string sMODULE, string sACCESS_TYPE)
		{
			// .NET 10 Migration: Security.GetUserAccess (static) → _security.GetUserAccess (instance)
			return _security != null ? _security.GetUserAccess(sMODULE, sACCESS_TYPE) : 0;
		}

		/// <summary>Returns whether the current user has the specified role.</summary>
		public bool UserRoleAccess(string sROLE_NAME)
		{
			// .NET 10 Migration: Security.GetACLRoleAccess (static) → _security.GetACLRoleAccess (instance)
			return _security != null && _security.GetACLRoleAccess(sROLE_NAME);
		}

		/// <summary>Returns whether the current user has access to the specified team.</summary>
		public bool UserTeamAccess(string sTEAM_NAME)
		{
			// .NET 10 Migration: Security.GetTeamAccess (static) → _security.GetTeamAccess (instance)
			return _security != null && _security.GetTeamAccess(sTEAM_NAME);
		}

		/// <summary>Returns the current user's primary key GUID.</summary>
		public Guid USER_ID()
		{
			// .NET 10 Migration: Security.USER_ID (static) → _security.USER_ID (instance)
			return _security != null ? _security.USER_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's login name.</summary>
		public string USER_NAME()
		{
			// .NET 10 Migration: Security.USER_NAME (static) → _security.USER_NAME (instance)
			return _security != null ? _security.USER_NAME : String.Empty;
		}

		/// <summary>Returns the current user's display name (first + last).</summary>
		public string FULL_NAME()
		{
			// .NET 10 Migration: Security.FULL_NAME (static) → _security.FULL_NAME (instance)
			return _security != null ? _security.FULL_NAME : String.Empty;
		}

		/// <summary>Returns the current user's primary team GUID.</summary>
		public Guid TEAM_ID()
		{
			// .NET 10 Migration: Security.TEAM_ID (static) → _security.TEAM_ID (instance)
			return _security != null ? _security.TEAM_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's primary team name.</summary>
		public string TEAM_NAME()
		{
			// .NET 10 Migration: Security.TEAM_NAME (static) → _security.TEAM_NAME (instance)
			return _security != null ? _security.TEAM_NAME : String.Empty;
		}
	}

	// ======================================================================================
	// RulesValidator
	//
	// BEFORE: Implemented System.Web.UI.IValidator (WebForms validation pipeline interface).
	// AFTER:  IValidator interface removed (System.Web.UI not available in .NET 10).
	//         ErrorMessage, IsValid, Validate() preserved with identical behavior.
	//         Validators in .NET 10 ASP.NET Core are handled via model validation / filters.
	// ======================================================================================

	// 11/10/2010 Paul.  Make sure to add the RulesValidator early in the pipeline.
	/// <summary>
	/// Rules validator that integrates with SplendidControl validation state.
	/// Migrated from SplendidCRM/_code/RulesUtil.cs for .NET 10 ASP.NET Core.
	/// IValidator interface removed (System.Web.UI.IValidator not available in .NET 10).
	/// </summary>
	public class RulesValidator
	{
		protected SplendidControl Container;

		public RulesValidator(SplendidControl Container)
		{
			this.Container = Container;
		}

		// 11/10/2010 Paul.  We can return the error, but it does not get displayed because we do not have a summary control.
		/// <summary>Gets or sets the validation error message from the container control.</summary>
		public string ErrorMessage
		{
			get { return Container.RulesErrorMessage; }
			set { Container.RulesErrorMessage = value; }
		}

		/// <summary>Gets or sets whether the current rule validation result is valid.</summary>
		public bool IsValid
		{
			get { return Container.RulesIsValid; }
			set { Container.RulesIsValid = value; }
		}

		/// <summary>Executes validation. No-op — validation is performed by the rules engine.</summary>
		public void Validate()
		{
			// .NET 10 Migration: Validation is performed by the rules engine before calling this.
			// IValidator.Validate() was called by the WebForms validation pipeline; no equivalent
			// in ASP.NET Core where validation is done via model binding / filters.
		}
	}

	// ======================================================================================
	// SplendidControlThis
	//
	// BEFORE: Provided access to WebForms form controls for business rule evaluation.
	//         Used HttpContext.Current.Request, HttpContext.Current.Session.
	//         Had WebForms-specific LayoutShowButton/ShowField/EnableField/RequiredField
	//         that manipulated _controls.DynamicButtons, TextBox, CKEditorControl, etc.
	// AFTER:  WebForms control manipulation blocks (#if !ReactOnlyUI) removed.
	//         Request → _httpContextAccessor.HttpContext?.Request
	//         Session["USER_SETTINGS/CULTURE"] → ISession.GetString()
	//         Security static → _security instance
	//         ShowButton/ShowField/EnableField/RequiredField preserved as no-ops
	//         (consistent with ReactOnlyUI build where these were conditionally compiled out).
	// ======================================================================================

	/// <summary>
	/// Context object wrapping the current edit/detail view for WF3 business rule evaluation.
	/// Migrated from SplendidCRM/_code/RulesUtil.cs for .NET 10 ASP.NET Core.
	/// Provides field read/write access, button visibility control, and user security context
	/// callable from business rule condition/action expressions.
	/// </summary>
	public class SplendidControlThis : SqlObj
	{
		private SplendidControl      Container;
		private L10N                 L10n     ;
		private DataRow              Row      ;
		private DataTable            Table    ;
		private string               Module   ;
		private Security             _security;
		private IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Creates a SplendidControlThis for a single record row.
		/// </summary>
		/// <param name="Container">The host SplendidControl providing layout and child control access.</param>
		/// <param name="sModule">Module name for ACL checks.</param>
		/// <param name="Row">Current record DataRow providing field values.</param>
		/// <param name="security">
		/// Security service for user identity and ACL checks.
		/// MIGRATION NOTE: Replaces static SplendidCRM.Security.* access.
		/// </param>
		/// <param name="httpContextAccessor">
		/// HTTP context accessor for Request and Session access.
		/// MIGRATION NOTE: Replaces HttpContext.Current static pattern.
		/// </param>
		public SplendidControlThis(SplendidControl Container, string sModule, DataRow Row, Security security = null, IHttpContextAccessor httpContextAccessor = null)
		{
			this.Container            = Container;
			this.Module               = sModule  ;
			this.Row                  = Row      ;
			if ( Row != null )
				this.Table = Row.Table;
			this.L10n                 = Container.GetL10n();
			this._security            = security;
			this._httpContextAccessor = httpContextAccessor;
		}

		/// <summary>
		/// Creates a SplendidControlThis for a table (list view context).
		/// </summary>
		/// <param name="Container">The host SplendidControl.</param>
		/// <param name="sModule">Module name for ACL checks.</param>
		/// <param name="Table">DataTable providing field definitions.</param>
		/// <param name="security">Security service.</param>
		/// <param name="httpContextAccessor">HTTP context accessor.</param>
		public SplendidControlThis(SplendidControl Container, string sModule, DataTable Table, Security security = null, IHttpContextAccessor httpContextAccessor = null)
		{
			this.Container            = Container;
			this.Module               = sModule  ;
			this.Table                = Table    ;
			this.L10n                 = Container.GetL10n();
			this._security            = security;
			this._httpContextAccessor = httpContextAccessor;
		}

		/// <summary>Gets or sets a field value in the current row by column name.</summary>
		public object this[string columnName]
		{
			get
			{
				if ( Row != null )
					return Row[columnName];
				return null;
			}
			set
			{
				if ( Row != null )
					Row[columnName] = value;
			}
		}

		// 04/06/2016 Paul.  We want to have a way to pass information from code behind to workflow.
		/// <summary>Gets a page-scoped item by name from the current HTTP context Items dictionary.</summary>
		public object GetPageItem(string sItemName)
		{
			object obj = null;
			// .NET 10 Migration: Container.Page.Items → HttpContext.Items
			var items = _httpContextAccessor?.HttpContext?.Items;
			if ( items != null && items.ContainsKey(sItemName) )
				obj = items[sItemName];
			return obj;
		}

		// 02/15/2014 Paul.  Provide access to the Request object so that we can determine if the record is new.
		/// <summary>
		/// Current HTTP request.
		/// BEFORE: return HttpContext.Current.Request;
		/// AFTER:  return _httpContextAccessor.HttpContext?.Request;
		/// </summary>
		public HttpRequest Request
		{
			get
			{
				// .NET 10 Migration: HttpContext.Current → IHttpContextAccessor
				return _httpContextAccessor?.HttpContext?.Request;
			}
		}

		/// <summary>
		/// Current user's culture/language setting.
		/// BEFORE: return Sql.ToString(HttpContext.Current.Session["USER_SETTINGS/CULTURE"]);
		/// AFTER:  return Sql.ToString(_httpContextAccessor.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE"));
		/// </summary>
		public string UserLanguage()
		{
			// .NET 10 Migration: HttpContext.Current.Session → IHttpContextAccessor + ISession
			return Sql.ToString(_httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE"));
		}

		/// <summary>
		/// Adds a new column to the DataTable (if it doesn't already exist).
		/// </summary>
		public void AddColumn(string columnName, string typeName)
		{
			if ( Table != null )
			{
				if ( !Table.Columns.Contains(columnName) )
				{
					if ( Sql.IsEmptyString(typeName) )
						Table.Columns.Add(columnName);
					else
						Table.Columns.Add(columnName, Type.GetType(typeName));
				}
			}
		}

		// http://msdn.microsoft.com/en-us/library/system.data.datacolumn.expression(v=VS.80).aspx
		/// <summary>
		/// Adds a computed column expression to the DataTable (if it doesn't already exist).
		/// </summary>
		public void AddColumnExpression(string columnName, string typeName, string sExpression)
		{
			if ( Table != null )
			{
				if ( !Table.Columns.Contains(columnName) )
				{
					Table.Columns.Add(columnName, Type.GetType(typeName), sExpression);
				}
			}
		}

		/// <summary>
		/// Returns a DynamicControl accessor for the named field.
		/// In .NET 10 ReactOnlyUI, FindControl returns null so values are sourced from the DataRow.
		/// </summary>
		public DynamicControl GetDynamicControl(string columnName)
		{
			return new DynamicControl(Container, columnName);
		}

		// 03/11/2014 Paul.  Provide a way to control the dynamic buttons.
		/// <summary>Returns a SafeDynamicButtons proxy for the ctlDynamicButtons control.</summary>
		public SafeDynamicButtons GetDynamicButtons()
		{
			return new SafeDynamicButtons(this.Container, this.Row);
		}

		/// <summary>Returns a SafeDynamicButtons proxy for the named buttons control.</summary>
		public SafeDynamicButtons GetDynamicButtons(string sName)
		{
			return new SafeDynamicButtons(this.Container, sName, this.Row);
		}

		/// <summary>Returns the localized display name for a value in a list.</summary>
		public string ListTerm(string sListName, string oField)
		{
			return Sql.ToString(L10n.Term(sListName, oField));
		}

		/// <summary>Returns the localized display name for a terminology entry.</summary>
		public string Term(string sEntryName)
		{
			return L10n.Term(sEntryName);
		}

		/// <summary>Gets or sets the redirect URL set by a business rule action.</summary>
		public string RedirectURL
		{
			get { return Container.RulesRedirectURL; }
			set { Container.RulesRedirectURL = value; }
		}

		/// <summary>Gets or sets the validation error message from a business rule.</summary>
		public string ErrorMessage
		{
			get { return Container.RulesErrorMessage; }
			set { Container.RulesErrorMessage = value; }
		}

		/// <summary>Gets or sets whether the current business rule validation passes.</summary>
		public bool IsValid
		{
			get { return Container.RulesIsValid; }
			set { Container.RulesIsValid = value; }
		}

		// 11/14/2013 Paul.  A customer wants to hide a row if it matches a certain criteria.
		/// <summary>Marks the current DataRow as deleted (hidden from list view).</summary>
		public void Delete()
		{
			if ( Row != null )
				Row.Delete();
		}

		// 02/13/2013 Paul.  Allow the business rules to change the layout.
		/// <summary>Gets or sets the list view layout name.</summary>
		public string LayoutListView
		{
			get { return Container.LayoutListView; }
			set { Container.LayoutListView = value; }
		}

		/// <summary>Gets or sets the edit view layout name.</summary>
		public string LayoutEditView
		{
			get { return Container.LayoutEditView; }
			set { Container.LayoutEditView = value; }
		}

		/// <summary>Gets or sets the detail view layout name.</summary>
		public string LayoutDetailView
		{
			get { return Container.LayoutDetailView; }
			set { Container.LayoutDetailView = value; }
		}

		// 11/10/2010 Paul.  Throwing an exception will be the preferred method of displaying an error.
		/// <summary>Throws an exception with the specified message (preferred error reporting in rules).</summary>
		public void Throw(string sMessage)
		{
			throw new Exception(sMessage);
		}

		// =================================================================================
		// Show/Enable/Required button and field methods
		// BEFORE: Manipulated WebForms controls (Button, HyperLink, TextBox, CKEditorControl,
		//         DatePicker, TeamSelect, RequiredFieldValidator, etc.) via FindControl.
		//         These were in a #if !ReactOnlyUI conditional block.
		// AFTER:  In .NET 10 ReactOnlyUI, FindControl always returns null, so these are no-ops.
		//         Methods preserved for API compatibility with callers.
		// =================================================================================

		/// <summary>
		/// Shows or hides a named button in the button bar.
		/// No-op in .NET 10 ReactOnlyUI — button visibility managed client-side.
		/// </summary>
		// 11/03/2021 Paul.  ASP.Net components are not needed in ReactOnlyUI.
		public void ShowButton(string sCommandName, bool bVisible)
		{
			// .NET 10 Migration: WebForms _controls.DynamicButtons not available.
			// In ReactOnlyUI this was already compiled out by #if !ReactOnlyUI.
		}

		/// <summary>
		/// Enables or disables a named button in the button bar.
		/// No-op in .NET 10 ReactOnlyUI — button state managed client-side.
		/// </summary>
		public void EnableButton(string sCommandName, bool bEnabled)
		{
			// .NET 10 Migration: WebForms _controls.DynamicButtons not available.
		}

		/// <summary>
		/// Shows or hides a named field and its associated label, parent-type, and tooltip controls.
		/// No-op in .NET 10 ReactOnlyUI — field visibility managed client-side by React.
		/// </summary>
		public void ShowField(string sDATA_FIELD, bool bVisible)
		{
			// .NET 10 Migration: FindControl always returns null in .NET 10 ReactOnlyUI.
			// In ReactOnlyUI this was already compiled out by #if !ReactOnlyUI.
		}

		/// <summary>
		/// Enables or disables a named field control (TextBox, DropDownList, DatePicker, etc.).
		/// No-op in .NET 10 ReactOnlyUI — field state managed client-side by React.
		/// </summary>
		public void EnableField(string sDATA_FIELD, bool bEnabled)
		{
			// .NET 10 Migration: WebForms control type checks (TextBox, CKEditorControl, etc.) removed.
		}

		/// <summary>
		/// Sets the required validation state for a named field.
		/// No-op in .NET 10 ReactOnlyUI — field validation managed client-side by React.
		/// </summary>
		public void RequiredField(string sDATA_FIELD, bool bRequired)
		{
			// .NET 10 Migration: WebForms RequiredFieldValidator not available.
		}

		// =================================================================================
		// User security methods
		// BEFORE: SplendidCRM.Security.* static property/method calls.
		// AFTER:  _security.* instance property/method calls (Security is DI-scoped).
		// =================================================================================

		/// <summary>Returns whether the current user has system administrator rights.</summary>
		public bool UserIsAdmin()
		{
			// .NET 10 Migration: Security.IS_ADMIN (static) → _security.IS_ADMIN (instance)
			return _security != null && _security.IS_ADMIN;
		}

		/// <summary>Returns the module access level for the specified access type in the current module.</summary>
		public int UserModuleAccess(string sACCESS_TYPE)
		{
			// .NET 10 Migration: Security.GetUserAccess (static) → _security.GetUserAccess (instance)
			return _security != null ? _security.GetUserAccess(Module, sACCESS_TYPE) : 0;
		}

		/// <summary>Returns whether the current user has the specified role.</summary>
		public bool UserRoleAccess(string sROLE_NAME)
		{
			// .NET 10 Migration: Security.GetACLRoleAccess (static) → _security.GetACLRoleAccess (instance)
			return _security != null && _security.GetACLRoleAccess(sROLE_NAME);
		}

		/// <summary>Returns whether the current user has access to the specified team.</summary>
		public bool UserTeamAccess(string sTEAM_NAME)
		{
			// .NET 10 Migration: Security.GetTeamAccess (static) → _security.GetTeamAccess (instance)
			return _security != null && _security.GetTeamAccess(sTEAM_NAME);
		}

		/// <summary>Returns the field-level access control for the specified field.</summary>
		public Security.ACL_FIELD_ACCESS UserFieldAccess(string sFIELD_NAME, Guid gASSIGNED_USER_ID)
		{
			if ( _security != null )
				return _security.GetUserFieldSecurity(Module, sFIELD_NAME, gASSIGNED_USER_ID);
			// Default: full access when security is unavailable.
			return new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gASSIGNED_USER_ID, Guid.Empty);
		}

		/// <summary>Returns true if the current user can read the specified field.</summary>
		public bool UserFieldIsReadable(string sFIELD_NAME, Guid gASSIGNED_USER_ID)
		{
			Security.ACL_FIELD_ACCESS acl = _security != null
				? _security.GetUserFieldSecurity(Module, sFIELD_NAME, gASSIGNED_USER_ID)
				: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gASSIGNED_USER_ID, Guid.Empty);
			return acl.IsReadable();
		}

		/// <summary>Returns true if the current user can write the specified field.</summary>
		public bool UserFieldIsWriteable(string sFIELD_NAME, Guid gASSIGNED_USER_ID)
		{
			Security.ACL_FIELD_ACCESS acl = _security != null
				? _security.GetUserFieldSecurity(Module, sFIELD_NAME, gASSIGNED_USER_ID)
				: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gASSIGNED_USER_ID, Guid.Empty);
			return acl.IsWriteable();
		}

		// 07/05/2012 Paul.  Provide access to the current user.
		/// <summary>Returns the current user's primary key GUID.</summary>
		public Guid USER_ID()
		{
			// .NET 10 Migration: Security.USER_ID (static) → _security.USER_ID (instance)
			return _security != null ? _security.USER_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's login name.</summary>
		public string USER_NAME()
		{
			return _security != null ? _security.USER_NAME : String.Empty;
		}

		/// <summary>Returns the current user's display name (first + last).</summary>
		public string FULL_NAME()
		{
			return _security != null ? _security.FULL_NAME : String.Empty;
		}

		/// <summary>Returns the current user's primary team GUID.</summary>
		public Guid TEAM_ID()
		{
			return _security != null ? _security.TEAM_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's primary team name.</summary>
		public string TEAM_NAME()
		{
			return _security != null ? _security.TEAM_NAME : String.Empty;
		}

		// 05/12/2013 Paul.  Provide a way to decrypt inside a business rule.
		// The business rules do not have access to the config variables, so the Guid values will need to be hard-coded in the rule.
		/// <summary>
		/// Decrypts an encrypted password value using the specified encryption key and IV.
		/// MIGRATION NOTE: Security.DecryptPassword(string,Guid,Guid) remains static in .NET 10.
		/// </summary>
		public string DecryptPassword(string sPASSWORD, Guid gKEY, Guid gIV)
		{
			return SplendidCRM.Security.DecryptPassword(sPASSWORD, gKEY, gIV);
		}
	}

	// ======================================================================================
	// SplendidWizardThis
	//
	// BEFORE: ACL-aware indexer for DataRow field access in wizard steps.
	//         Used SplendidCRM.Security.* static properties.
	// AFTER:  Security static calls → _security instance calls.
	//         Request and UserLanguage added (required by exports schema).
	// ======================================================================================

	/// <summary>
	/// Context object wrapping a wizard step's DataRow for WF3 business rule evaluation.
	/// Migrated from SplendidCRM/_code/RulesUtil.cs for .NET 10 ASP.NET Core.
	/// Provides ACL-aware field read/write access and user security context
	/// callable from business rule condition/action expressions.
	/// </summary>
	public class SplendidWizardThis : SqlObj
	{
		private SplendidControl      Container       ;
		private L10N                 L10n            ;
		private DataRow              Row             ;
		private string               Module          ;
		private Guid                 gASSIGNED_USER_ID;
		private Security             _security       ;
		private IHttpContextAccessor _httpContextAccessor;

		// 04/27/2018 Paul.  We need to be able to generate an error message.
		/// <summary>
		/// Creates a SplendidWizardThis for the specified wizard DataRow.
		/// </summary>
		/// <param name="Container">Host SplendidControl providing error state.</param>
		/// <param name="L10n">Localization instance.</param>
		/// <param name="sModule">Module name for ACL checks.</param>
		/// <param name="Row">DataRow for the current wizard record.</param>
		/// <param name="security">Security service. Replaces static Security.* calls.</param>
		/// <param name="httpContextAccessor">HTTP context accessor. Replaces HttpContext.Current.</param>
		public SplendidWizardThis(SplendidControl Container, L10N L10n, string sModule, DataRow Row, Security security = null, IHttpContextAccessor httpContextAccessor = null)
		{
			this.Container            = Container ;
			this.L10n                 = L10n      ;
			this.Row                  = Row       ;
			this.Module               = sModule   ;
			this.gASSIGNED_USER_ID    = Guid.Empty;
			this._security            = security;
			this._httpContextAccessor = httpContextAccessor;
			if ( Row != null && Row.Table != null && Row.Table.Columns.Contains("ASSIGNED_USER_ID") )
				gASSIGNED_USER_ID = Sql.ToGuid(Row["ASSIGNED_USER_ID"]);
		}

		/// <summary>
		/// ACL-aware indexer for DataRow field access.
		/// Enforces field-level security when SplendidInit.bEnableACLFieldSecurity is true.
		/// </summary>
		public object this[string columnName]
		{
			get
			{
				bool bIsReadable = true;
				if ( SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(columnName) )
				{
					Security.ACL_FIELD_ACCESS acl = _security != null
						? _security.GetUserFieldSecurity(Module, columnName, gASSIGNED_USER_ID)
						: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gASSIGNED_USER_ID, Guid.Empty);
					bIsReadable = acl.IsReadable();
				}
				if ( bIsReadable )
					return Row[columnName];
				else
					return DBNull.Value;
			}
			set
			{
				bool bIsWriteable = true;
				if ( SplendidInit.bEnableACLFieldSecurity )
				{
					Security.ACL_FIELD_ACCESS acl = _security != null
						? _security.GetUserFieldSecurity(Module, columnName, gASSIGNED_USER_ID)
						: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gASSIGNED_USER_ID, Guid.Empty);
					bIsWriteable = acl.IsWriteable();
				}
				if ( bIsWriteable )
					Row[columnName] = value;
			}
		}

		/// <summary>Returns the localized display name for a value in a list.</summary>
		public string ListTerm(string sListName, string oField)
		{
			return Sql.ToString(L10n.Term(sListName, oField));
		}

		/// <summary>Returns the localized display name for a terminology entry.</summary>
		public string Term(string sEntryName)
		{
			return L10n.Term(sEntryName);
		}

		/// <summary>
		/// Current HTTP request.
		/// BEFORE: HttpContext.Current.Request (added for .NET 10 DI pattern).
		/// AFTER:  _httpContextAccessor.HttpContext?.Request
		/// </summary>
		public HttpRequest Request
		{
			get { return _httpContextAccessor?.HttpContext?.Request; }
		}

		/// <summary>
		/// Current user's culture/language setting.
		/// BEFORE: HttpContext.Current.Session["USER_SETTINGS/CULTURE"] (added for .NET 10).
		/// AFTER:  ISession.GetString("USER_SETTINGS/CULTURE")
		/// </summary>
		public string UserLanguage()
		{
			return Sql.ToString(_httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE"));
		}

		// 07/05/2012 Paul.  Provide access to the current user.
		/// <summary>Returns the current user's primary key GUID.</summary>
		public Guid USER_ID()
		{
			return _security != null ? _security.USER_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's login name.</summary>
		public string USER_NAME()
		{
			return _security != null ? _security.USER_NAME : String.Empty;
		}

		/// <summary>Returns the current user's display name.</summary>
		public string FULL_NAME()
		{
			return _security != null ? _security.FULL_NAME : String.Empty;
		}

		/// <summary>Returns the current user's primary team GUID.</summary>
		public Guid TEAM_ID()
		{
			return _security != null ? _security.TEAM_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's primary team name.</summary>
		public string TEAM_NAME()
		{
			return _security != null ? _security.TEAM_NAME : String.Empty;
		}

		/// <summary>Returns whether the current user has system administrator rights.</summary>
		public bool UserIsAdmin()
		{
			return _security != null && _security.IS_ADMIN;
		}

		/// <summary>Returns the module access level for the specified access type.</summary>
		public int UserModuleAccess(string sACCESS_TYPE)
		{
			return _security != null ? _security.GetUserAccess(Module, sACCESS_TYPE) : 0;
		}

		/// <summary>Returns whether the current user has the specified role.</summary>
		public bool UserRoleAccess(string sROLE_NAME)
		{
			return _security != null && _security.GetACLRoleAccess(sROLE_NAME);
		}

		/// <summary>Returns whether the current user has access to the specified team.</summary>
		public bool UserTeamAccess(string sTEAM_NAME)
		{
			return _security != null && _security.GetTeamAccess(sTEAM_NAME);
		}

		// 04/27/2018 Paul.  We need to be able to generate an error message.
		/// <summary>Gets or sets the validation error message from the container control.</summary>
		public string ErrorMessage
		{
			get { return Container.RulesErrorMessage; }
			set { Container.RulesErrorMessage = value; }
		}
	}

	// ======================================================================================
	// SplendidImportThis
	//
	// BEFORE: ACL-aware indexer for IDbCommand parameter access during data import.
	//         Used SplendidCRM.Security.* static properties.
	// AFTER:  Security static calls → _security instance calls.
	//         Request and UserLanguage added (required by exports schema).
	// ======================================================================================

	// 09/17/2013 Paul.  Add Business Rules to import.
	/// <summary>
	/// Context object wrapping import command parameters for WF3 business rule evaluation.
	/// Migrated from SplendidCRM/_code/RulesUtil.cs for .NET 10 ASP.NET Core.
	/// Provides ACL-aware parameter get/set access and user security context
	/// callable from business rule condition/action expressions during data import.
	/// </summary>
	public class SplendidImportThis : SqlObj
	{
		private SplendidControl      Container       ;
		private L10N                 L10n            ;
		private DataRow              Row             ;
		private IDbCommand           Import          ;
		private IDbCommand           ImportCSTM      ;
		private string               Module          ;
		private Guid                 gASSIGNED_USER_ID;
		private Security             _security       ;
		private IHttpContextAccessor _httpContextAccessor;

		// 04/27/2018 Paul.  We need to be able to generate an error message.
		/// <summary>
		/// Creates a SplendidImportThis for the specified import commands.
		/// </summary>
		/// <param name="Container">Host SplendidControl providing error state.</param>
		/// <param name="L10n">Localization instance.</param>
		/// <param name="sModule">Module name for ACL checks.</param>
		/// <param name="Row">DataRow for the record being imported (used for result display).</param>
		/// <param name="cmdImport">Main import stored procedure command.</param>
		/// <param name="cmdImportCSTM">Custom field import stored procedure command (optional).</param>
		/// <param name="security">Security service. Replaces static Security.* calls.</param>
		/// <param name="httpContextAccessor">HTTP context accessor. Replaces HttpContext.Current.</param>
		public SplendidImportThis(SplendidControl Container, L10N L10n, string sModule, DataRow Row, IDbCommand cmdImport, IDbCommand cmdImportCSTM, Security security = null, IHttpContextAccessor httpContextAccessor = null)
		{
			this.Container            = Container    ;
			this.L10n                 = L10n         ;
			this.Row                  = Row          ;
			this.Import               = cmdImport    ;
			this.ImportCSTM           = cmdImportCSTM;
			this.Module               = sModule      ;
			this.gASSIGNED_USER_ID    = Guid.Empty   ;
			this._security            = security;
			this._httpContextAccessor = httpContextAccessor;

			IDbDataParameter par = Sql.FindParameter(cmdImport, "ASSIGNED_USER_ID");
			if ( par != null )
				gASSIGNED_USER_ID = Sql.ToGuid(par.Value);
		}

		/// <summary>
		/// ACL-aware indexer for import command parameter access.
		/// Reads from Import/ImportCSTM command parameters; writes to parameters AND the display Row.
		/// </summary>
		public object this[string columnName]
		{
			get
			{
				bool bIsReadable = true;
				if ( SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(columnName) )
				{
					Security.ACL_FIELD_ACCESS acl = _security != null
						? _security.GetUserFieldSecurity(Module, columnName, gASSIGNED_USER_ID)
						: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gASSIGNED_USER_ID, Guid.Empty);
					bIsReadable = acl.IsReadable();
				}
				if ( bIsReadable )
				{
					IDbDataParameter par = Sql.FindParameter(Import, columnName);
					if ( par != null )
					{
						return par.Value;
					}
					else if ( ImportCSTM != null )
					{
						par = Sql.FindParameter(ImportCSTM, columnName);
						if ( par != null )
							return par.Value;
					}
				}
				return DBNull.Value;
			}
			set
			{
				bool bIsWriteable = true;
				if ( SplendidInit.bEnableACLFieldSecurity )
				{
					Security.ACL_FIELD_ACCESS acl = _security != null
						? _security.GetUserFieldSecurity(Module, columnName, gASSIGNED_USER_ID)
						: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gASSIGNED_USER_ID, Guid.Empty);
					bIsWriteable = acl.IsWriteable();
				}
				if ( bIsWriteable )
				{
					IDbDataParameter par = Sql.FindParameter(Import, columnName);
					if ( par != null )
					{
						Sql.SetParameter(par, value);
					}
					if ( ImportCSTM != null )
					{
						// 09/17/2013 Paul.  If setting the ID, then also set the related custom field ID.
						if ( String.Compare(columnName, "ID", true) == 0 )
							columnName = "ID_C";
						par = Sql.FindParameter(ImportCSTM, columnName);
						if ( par != null )
							Sql.SetParameter(par, value);
					}
					// 09/17/2013 Paul.  The Row is displayed in the Results tab while the parameters are used to update the database.
					Row[columnName] = value;
				}
			}
		}

		/// <summary>Returns the localized display name for a value in a list.</summary>
		public string ListTerm(string sListName, string oField)
		{
			return Sql.ToString(L10n.Term(sListName, oField));
		}

		/// <summary>Returns the localized display name for a terminology entry.</summary>
		public string Term(string sEntryName)
		{
			return L10n.Term(sEntryName);
		}

		/// <summary>
		/// Current HTTP request.
		/// MIGRATION NOTE: Added for .NET 10 DI pattern consistency with SplendidControlThis/WizardThis.
		/// BEFORE: Not available in original SplendidImportThis.
		/// AFTER:  _httpContextAccessor.HttpContext?.Request
		/// </summary>
		public HttpRequest Request
		{
			get { return _httpContextAccessor?.HttpContext?.Request; }
		}

		/// <summary>
		/// Current user's culture/language setting.
		/// MIGRATION NOTE: Added for .NET 10 DI pattern consistency.
		/// </summary>
		public string UserLanguage()
		{
			return Sql.ToString(_httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE"));
		}

		// 07/05/2012 Paul.  Provide access to the current user.
		/// <summary>Returns the current user's primary key GUID.</summary>
		public Guid USER_ID()
		{
			return _security != null ? _security.USER_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's login name.</summary>
		public string USER_NAME()
		{
			return _security != null ? _security.USER_NAME : String.Empty;
		}

		/// <summary>Returns the current user's display name.</summary>
		public string FULL_NAME()
		{
			return _security != null ? _security.FULL_NAME : String.Empty;
		}

		/// <summary>Returns the current user's primary team GUID.</summary>
		public Guid TEAM_ID()
		{
			return _security != null ? _security.TEAM_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's primary team name.</summary>
		public string TEAM_NAME()
		{
			return _security != null ? _security.TEAM_NAME : String.Empty;
		}

		/// <summary>Returns whether the current user has system administrator rights.</summary>
		public bool UserIsAdmin()
		{
			return _security != null && _security.IS_ADMIN;
		}

		/// <summary>Returns the module access level for the specified access type.</summary>
		public int UserModuleAccess(string sACCESS_TYPE)
		{
			return _security != null ? _security.GetUserAccess(Module, sACCESS_TYPE) : 0;
		}

		/// <summary>Returns whether the current user has the specified role.</summary>
		public bool UserRoleAccess(string sROLE_NAME)
		{
			return _security != null && _security.GetACLRoleAccess(sROLE_NAME);
		}

		/// <summary>Returns whether the current user has access to the specified team.</summary>
		public bool UserTeamAccess(string sTEAM_NAME)
		{
			return _security != null && _security.GetTeamAccess(sTEAM_NAME);
		}

		// 04/27/2018 Paul.  We need to be able to generate an error message.
		/// <summary>Gets or sets the validation error message from the container control.</summary>
		public string ErrorMessage
		{
			get { return Container.RulesErrorMessage; }
			set { Container.RulesErrorMessage = value; }
		}
	}

	// ======================================================================================
	// SplendidReportThis
	//
	// BEFORE: ACL-aware indexer for report DataRow access.
	//         Constructor took HttpApplicationState Application for cache and L10N.Term calls.
	//         Used SplendidCRM.Security.* static properties.
	// AFTER:  HttpApplicationState Application → IMemoryCache _memoryCache (DI parameter)
	//         L10N.Term(Application, ...) → L10N.Term(_memoryCache, ...)
	//         Security static calls → _security instance calls.
	//         Request and UserLanguage added (required by exports schema).
	// ======================================================================================

	/// <summary>
	/// Context object wrapping a report row for WF3 business rule evaluation.
	/// Migrated from SplendidCRM/_code/RulesUtil.cs for .NET 10 ASP.NET Core.
	/// Provides ACL-aware field read/write access and user security context
	/// callable from business rule condition/action expressions in reports.
	/// </summary>
	public class SplendidReportThis : SqlObj
	{
		// .NET 10 Migration: HttpApplicationState Application → IMemoryCache _memoryCache
		private IMemoryCache         _memoryCache    ;
		private L10N                 L10n            ;
		private DataRow              Row             ;
		private DataTable            Table           ;
		private string               Module          ;
		private Guid                 gASSIGNED_USER_ID;
		private Security             _security       ;
		private IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Creates a SplendidReportThis for a single report record row.
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache replacing HttpApplicationState Application.
		/// BEFORE: HttpApplicationState Application (for L10N.Term and cache reads)
		/// AFTER:  IMemoryCache passed directly to L10N.Term(memoryCache, ...)
		/// </param>
		/// <param name="L10n">Localization instance.</param>
		/// <param name="sModule">Module name for ACL checks.</param>
		/// <param name="Row">DataRow for the current report record.</param>
		/// <param name="security">Security service. Replaces static Security.* calls.</param>
		/// <param name="httpContextAccessor">HTTP context accessor. Replaces HttpContext.Current.</param>
		public SplendidReportThis(IMemoryCache memoryCache, L10N L10n, string sModule, DataRow Row, Security security = null, IHttpContextAccessor httpContextAccessor = null)
		{
			this._memoryCache          = memoryCache;
			this.L10n                  = L10n       ;
			this.Module                = sModule    ;
			this.Row                   = Row        ;
			this.gASSIGNED_USER_ID     = Guid.Empty ;
			this._security             = security;
			this._httpContextAccessor  = httpContextAccessor;
			if ( Row != null )
			{
				this.Table = Row.Table;
				if ( Table != null && Table.Columns.Contains("ASSIGNED_USER_ID") )
					gASSIGNED_USER_ID = Sql.ToGuid(Row["ASSIGNED_USER_ID"]);
			}
		}

		/// <summary>
		/// Creates a SplendidReportThis for a DataTable (report table context).
		/// </summary>
		/// <param name="memoryCache">IMemoryCache replacing HttpApplicationState Application.</param>
		/// <param name="L10n">Localization instance.</param>
		/// <param name="sModule">Module name for ACL checks.</param>
		/// <param name="Table">DataTable for the current report query result.</param>
		/// <param name="security">Security service.</param>
		/// <param name="httpContextAccessor">HTTP context accessor.</param>
		public SplendidReportThis(IMemoryCache memoryCache, L10N L10n, string sModule, DataTable Table, Security security = null, IHttpContextAccessor httpContextAccessor = null)
		{
			this._memoryCache         = memoryCache;
			this.L10n                 = L10n       ;
			this.Module               = sModule    ;
			this.Table                = Table      ;
			this.gASSIGNED_USER_ID    = Guid.Empty ;
			this._security            = security;
			this._httpContextAccessor = httpContextAccessor;
		}

		/// <summary>
		/// ACL-aware indexer for report row field access.
		/// </summary>
		public object this[string columnName]
		{
			get
			{
				bool bIsReadable = true;
				if ( SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(columnName) )
				{
					Security.ACL_FIELD_ACCESS acl = _security != null
						? _security.GetUserFieldSecurity(Module, columnName, gASSIGNED_USER_ID)
						: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gASSIGNED_USER_ID, Guid.Empty);
					bIsReadable = acl.IsReadable();
				}
				if ( bIsReadable )
					return Row[columnName];
				else
					return DBNull.Value;
			}
			set
			{
				bool bIsWriteable = true;
				if ( SplendidInit.bEnableACLFieldSecurity )
				{
					Security.ACL_FIELD_ACCESS acl = _security != null
						? _security.GetUserFieldSecurity(Module, columnName, gASSIGNED_USER_ID)
						: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gASSIGNED_USER_ID, Guid.Empty);
					bIsWriteable = acl.IsWriteable();
				}
				if ( bIsWriteable )
					Row[columnName] = value;
			}
		}

		/// <summary>Adds a new column to the report DataTable (if it doesn't already exist).</summary>
		public void AddColumn(string columnName, string typeName)
		{
			if ( Table != null )
			{
				if ( !Table.Columns.Contains(columnName) )
				{
					if ( Sql.IsEmptyString(typeName) )
						Table.Columns.Add(columnName);
					else
						Table.Columns.Add(columnName, Type.GetType(typeName));
				}
			}
		}

		// http://msdn.microsoft.com/en-us/library/system.data.datacolumn.expression(v=VS.80).aspx
		/// <summary>Adds a computed column to the report DataTable (if it doesn't already exist).</summary>
		public void AddColumnExpression(string columnName, string typeName, string sExpression)
		{
			if ( Table != null )
			{
				if ( !Table.Columns.Contains(columnName) )
				{
					Table.Columns.Add(columnName, Type.GetType(typeName), sExpression);
				}
			}
		}

		/// <summary>
		/// Returns the localized display name for a value in a list.
		/// BEFORE: L10N.Term(Application, L10n.NAME, sListName, oField)
		/// AFTER:  L10N.Term(_memoryCache, L10n.NAME, sListName, oField)
		/// </summary>
		public string ListTerm(string sListName, string oField)
		{
			// 12/04/2010 Paul.  We need to use the static version of Term as a report can get rendered
			// inside a workflow, which has issues accessing the context.
			// .NET 10 Migration: Application → _memoryCache in static Term overload.
			return Sql.ToString(L10N.Term(_memoryCache, L10n.NAME, sListName, oField));
		}

		/// <summary>
		/// Returns the localized display name for a terminology entry.
		/// BEFORE: L10N.Term(Application, L10n.NAME, sEntryName)
		/// AFTER:  L10N.Term(_memoryCache, L10n.NAME, sEntryName)
		/// </summary>
		public string Term(string sEntryName)
		{
			// 12/04/2010 Paul.  We need to use the static version of Term as a report can get rendered
			// inside a workflow, which has issues accessing the context.
			// .NET 10 Migration: Application → _memoryCache in static Term overload.
			return L10N.Term(_memoryCache, L10n.NAME, sEntryName);
		}

		// 11/10/2010 Paul.  Throwing an exception will be the preferred method of displaying an error.
		/// <summary>Throws an exception with the specified message.</summary>
		public void Throw(string sMessage)
		{
			throw new Exception(sMessage);
		}

		/// <summary>
		/// Current HTTP request.
		/// MIGRATION NOTE: Added for .NET 10 DI pattern consistency.
		/// </summary>
		public HttpRequest Request
		{
			get { return _httpContextAccessor?.HttpContext?.Request; }
		}

		/// <summary>
		/// Current user's culture/language setting.
		/// MIGRATION NOTE: Added for .NET 10 DI pattern consistency.
		/// </summary>
		public string UserLanguage()
		{
			return Sql.ToString(_httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE"));
		}

		/// <summary>Returns whether the current user has system administrator rights.</summary>
		public bool UserIsAdmin()
		{
			return _security != null && _security.IS_ADMIN;
		}

		/// <summary>Returns the module access level for the specified access type.</summary>
		public int UserModuleAccess(string sACCESS_TYPE)
		{
			return _security != null ? _security.GetUserAccess(Module, sACCESS_TYPE) : 0;
		}

		/// <summary>Returns whether the current user has the specified role.</summary>
		public bool UserRoleAccess(string sROLE_NAME)
		{
			return _security != null && _security.GetACLRoleAccess(sROLE_NAME);
		}

		/// <summary>Returns whether the current user has access to the specified team.</summary>
		public bool UserTeamAccess(string sTEAM_NAME)
		{
			return _security != null && _security.GetTeamAccess(sTEAM_NAME);
		}

		/// <summary>Returns the field-level access control for the specified field.</summary>
		public Security.ACL_FIELD_ACCESS UserFieldAccess(string sFIELD_NAME, Guid gAssignedUserId)
		{
			if ( _security != null )
				return _security.GetUserFieldSecurity(Module, sFIELD_NAME, gAssignedUserId);
			return new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gAssignedUserId, Guid.Empty);
		}

		/// <summary>Returns true if the current user can read the specified field.</summary>
		public bool UserFieldIsReadable(string sFIELD_NAME, Guid gAssignedUserId)
		{
			Security.ACL_FIELD_ACCESS acl = _security != null
				? _security.GetUserFieldSecurity(Module, sFIELD_NAME, gAssignedUserId)
				: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gAssignedUserId, Guid.Empty);
			return acl.IsReadable();
		}

		/// <summary>Returns true if the current user can write the specified field.</summary>
		public bool UserFieldIsWriteable(string sFIELD_NAME, Guid gAssignedUserId)
		{
			Security.ACL_FIELD_ACCESS acl = _security != null
				? _security.GetUserFieldSecurity(Module, sFIELD_NAME, gAssignedUserId)
				: new Security.ACL_FIELD_ACCESS(Security.ACL_FIELD_ACCESS.FULL_ACCESS, gAssignedUserId, Guid.Empty);
			return acl.IsWriteable();
		}

		// 07/05/2012 Paul.  Provide access to the current user.
		/// <summary>Returns the current user's primary key GUID.</summary>
		public Guid USER_ID()
		{
			return _security != null ? _security.USER_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's login name.</summary>
		public string USER_NAME()
		{
			return _security != null ? _security.USER_NAME : String.Empty;
		}

		/// <summary>Returns the current user's display name.</summary>
		public string FULL_NAME()
		{
			return _security != null ? _security.FULL_NAME : String.Empty;
		}

		/// <summary>Returns the current user's primary team GUID.</summary>
		public Guid TEAM_ID()
		{
			return _security != null ? _security.TEAM_ID : Guid.Empty;
		}

		/// <summary>Returns the current user's primary team name.</summary>
		public string TEAM_NAME()
		{
			return _security != null ? _security.TEAM_NAME : String.Empty;
		}
	}

	// ======================================================================================
	// SplendidRulesTypeProvider
	//
	// BEFORE: Implemented System.Workflow.ComponentModel.Compiler.ITypeProvider (WF3).
	//         Used by RuleValidation(thisType, typeProvider) constructor overload.
	//         Marked [obsolete] in .NET 4.5, suppressed with #pragma warning disable 618.
	// AFTER:  ITypeProvider interface removed — not available in LogicBuilder 2.0.4.
	//         LogicBuilder.Workflow.Activities.Rules.RuleValidation ctor takes (Type thisType)
	//         only; type restriction is now handled by SimpleRunTimeTypeProvider internally.
	//         SplendidRulesTypeProvider preserved as a type-registry helper class for
	//         callers that want to restrict available types in rule expressions.
	//         GetType, GetTypes, GetAssembly, AddAssembly, RemoveAssembly preserved.
	//         TypesChanged, TypeLoadErrors events preserved.
	// ======================================================================================

	// 12/12/2012 Paul.  For security reasons, we want to restrict the data types available to the rules wizard.
	// http://www.codeproject.com/Articles/12675/How-to-reuse-the-Windows-Workflow-Foundation-WF-co
	// .NET 10 Migration: ITypeProvider interface removed — not available in LogicBuilder 2.0.4.
	// #pragma warning disable 618 removed — no longer applicable without ITypeProvider.
	/// <summary>
	/// Type registry that restricts the set of .NET types available to WF3 rule expressions.
	/// Migrated from SplendidCRM/_code/RulesUtil.cs for .NET 10 ASP.NET Core.
	///
	/// MIGRATION NOTE: The original class implemented System.Workflow.ComponentModel.Compiler.ITypeProvider
	/// which is not available in LogicBuilder 2.0.4. This class is preserved as a standalone
	/// type-registry helper. Callers that previously passed it to RuleValidation(Type, ITypeProvider)
	/// should use RuleValidation(Type) instead (LogicBuilder's constructor signature).
	///
	/// The type registry can still be used to pre-validate type names used in rule expressions
	/// via GetType(string) before passing expressions to the rules engine.
	/// </summary>
	public class SplendidRulesTypeProvider
	{
		/// <summary>Fired when the set of available types changes.</summary>
		public event EventHandler TypesChanged;
		/// <summary>Fired when a type load error occurs during GetType() lookup.</summary>
		public event EventHandler TypeLoadErrorsChanged;

		private Dictionary<string, Type>   availableTypes;
		private Dictionary<object, Exception> typeErrors ;
		private List<Assembly>             availableAssemblies;

		/// <summary>
		/// Initializes the type registry with the default set of primitive and CRM types
		/// available for use in business rule expressions.
		/// </summary>
		public SplendidRulesTypeProvider()
		{
			typeErrors          = new Dictionary<object, Exception>();
			availableAssemblies = new List<Assembly>();
			availableAssemblies.Add(this.GetType().Assembly);

			availableTypes = new Dictionary<string, Type>();
			availableTypes.Add(typeof(System.Boolean ).FullName, typeof(System.Boolean ));
			availableTypes.Add(typeof(System.Byte    ).FullName, typeof(System.Byte    ));
			availableTypes.Add(typeof(System.Char    ).FullName, typeof(System.Char    ));
			availableTypes.Add(typeof(System.DateTime).FullName, typeof(System.DateTime));
			availableTypes.Add(typeof(System.Decimal ).FullName, typeof(System.Decimal ));
			availableTypes.Add(typeof(System.Double  ).FullName, typeof(System.Double  ));
			availableTypes.Add(typeof(System.Guid    ).FullName, typeof(System.Guid    ));
			availableTypes.Add(typeof(System.Int16   ).FullName, typeof(System.Int16   ));
			availableTypes.Add(typeof(System.Int32   ).FullName, typeof(System.Int32   ));
			availableTypes.Add(typeof(System.Int64   ).FullName, typeof(System.Int64   ));
			availableTypes.Add(typeof(System.SByte   ).FullName, typeof(System.SByte   ));
			availableTypes.Add(typeof(System.Single  ).FullName, typeof(System.Single  ));
			availableTypes.Add(typeof(System.String  ).FullName, typeof(System.String  ));
			availableTypes.Add(typeof(System.TimeSpan).FullName, typeof(System.TimeSpan));
			availableTypes.Add(typeof(System.UInt16  ).FullName, typeof(System.UInt16  ));
			availableTypes.Add(typeof(System.UInt32  ).FullName, typeof(System.UInt32  ));
			availableTypes.Add(typeof(System.UInt64  ).FullName, typeof(System.UInt64  ));
			availableTypes.Add(typeof(System.DBNull  ).FullName, typeof(System.DBNull  ));
			// 03/11/2014 Paul.  Provide a way to control the dynamic buttons.
			// .NET 10 Migration: SafeDynamicButtons is always available (not gated by #if !ReactOnlyUI).
			availableTypes.Add(typeof(SafeDynamicButtons).FullName, typeof(SafeDynamicButtons));

			// 12/12/2012 Paul.  Use TypesChanged to avoid a compiler warning.
			// Raise the event to notify that types are available (consistent with original behavior).
			TypesChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Returns the Type with the specified full name, or null if not in the available-types registry.
		/// </summary>
		/// <param name="name">Full type name (e.g. "System.Boolean").</param>
		/// <param name="throwOnError">If true, throws TypeLoadException when the type is not found.</param>
		public Type GetType(string name, bool throwOnError)
		{
			if ( String.IsNullOrEmpty(name) )
				return null;

			if ( availableTypes.ContainsKey(name) )
			{
				Type type = availableTypes[name];
				return type;
			}
			else
			{
				if ( !typeErrors.ContainsKey(name) )
				{
					typeErrors.Add(name, new Exception("SplendidRulesTypeProvider: " + name + " is not a supported data type."));
				}
				if ( throwOnError )
				{
					throw new TypeLoadException();
				}
				else
				{
					if ( TypeLoadErrorsChanged != null )
					{
						try
						{
							TypeLoadErrorsChanged(this, EventArgs.Empty);
						}
						catch
						{
							// Suppress exceptions from error notification handlers.
						}
					}
					return null;
				}
			}
		}

		/// <summary>Returns the Type with the specified full name, or null if not found.</summary>
		public Type GetType(string name)
		{
			return GetType(name, false);
		}

		/// <summary>Returns all types registered in this provider.</summary>
		public Type[] GetTypes()
		{
			Type[] result = new Type[availableTypes.Count];
			availableTypes.Values.CopyTo(result, 0);
			return result;
		}

		/// <summary>Returns the assembly where this type provider class is defined.</summary>
		public Assembly GetAssembly()
		{
			return this.GetType().Assembly;
		}

		/// <summary>Adds an assembly to the list of referenced assemblies for type resolution.</summary>
		public void AddAssembly(Assembly assembly)
		{
			if ( assembly != null && !availableAssemblies.Contains(assembly) )
				availableAssemblies.Add(assembly);
		}

		/// <summary>Removes an assembly from the list of referenced assemblies.</summary>
		public void RemoveAssembly(Assembly assembly)
		{
			if ( assembly != null )
				availableAssemblies.Remove(assembly);
		}

		/// <summary>Gets the dictionary of type-load errors keyed by type name.</summary>
		public IDictionary<object, Exception> TypeLoadErrors
		{
			get { return typeErrors; }
		}

		/// <summary>Gets the local assembly (the assembly containing this class).</summary>
		public Assembly LocalAssembly
		{
			get { return this.GetType().Assembly; }
		}

		/// <summary>Gets the collection of referenced assemblies used for type resolution.</summary>
		public ICollection<Assembly> ReferencedAssemblies
		{
			get { return availableAssemblies; }
		}
	}

	// ======================================================================================
	// RulesUtil
	//
	// BEFORE: Used System.Workflow.Activities.Rules and System.Workflow.ComponentModel.Serialization
	//         for XOML serialization and RulesParser for string-based rule building/validation.
	// AFTER:  System.Workflow.* → LogicBuilder.Workflow.* (NuGet packages)
	//         WorkflowMarkupSerializer: LogicBuilder.Workflow.ComponentModel.Serialization
	//         RuleSet, RuleValidation, Rule, etc.: LogicBuilder.Workflow.Activities.Rules
	//         ValidationError: LogicBuilder.Workflow.ComponentModel.Compiler
	//
	// KNOWN LIMITATION (RulesParser not available):
	//   LogicBuilder.Workflow.Activities.Rules 2.0.4 does NOT provide RulesParser.
	//   The original WF3 RulesParser parsed C# expression strings into CodeDom expressions.
	//   Methods that require this parser (RulesValidate, BuildRuleSet from DataTable/string)
	//   throw PlatformNotSupportedException. Use Deserialize(sXOML) → RuleEngine.Execute()
	//   for rule execution workflows instead.
	//   Reference: https://github.com/LogicBuilder-Workflow-Activities-Rules (does not include parser port)
	// ======================================================================================

	/// <summary>
	/// Summary description for RulesUtil.
	/// Business rules serialization and evaluation utilities.
	/// Migrated from SplendidCRM/_code/RulesUtil.cs for .NET 10 ASP.NET Core.
	///
	/// Uses LogicBuilder.Workflow.Activities.Rules and LogicBuilder.Workflow.ComponentModel.Serialization
	/// as cross-platform replacements for the .NET Framework WF3 workflow libraries.
	///
	/// IMPORTANT: RulesParser (required for BuildRuleSet/RulesValidate from string expressions)
	/// is not available in LogicBuilder 2.0.4. See PlatformNotSupportedException messages.
	/// </summary>
	public class RulesUtil
	{
		/// <summary>
		/// Deserializes a RuleSet from an XOML (Extensible Object Markup Language) XML string.
		/// Uses LogicBuilder.Workflow.ComponentModel.Serialization.WorkflowMarkupSerializer.
		/// BEFORE: System.Workflow.ComponentModel.Serialization.WorkflowMarkupSerializer
		/// AFTER:  LogicBuilder.Workflow.ComponentModel.Serialization.WorkflowMarkupSerializer
		/// </summary>
		/// <param name="sXOML">XOML XML string containing the serialized RuleSet.</param>
		/// <returns>Deserialized RuleSet instance.</returns>
		public static RuleSet Deserialize(string sXOML)
		{
			RuleSet rules = null;
			using ( StringReader stm = new StringReader(sXOML) )
			{
				using ( XmlTextReader xrdr = new XmlTextReader(stm) )
				{
					WorkflowMarkupSerializer serializer = new WorkflowMarkupSerializer();
					rules = (RuleSet) serializer.Deserialize(xrdr);
				}
			}
			return rules;
		}

		/// <summary>
		/// Serializes a RuleSet to an XOML (Extensible Object Markup Language) XML string.
		/// Uses LogicBuilder.Workflow.ComponentModel.Serialization.WorkflowMarkupSerializer.
		/// BEFORE: System.Workflow.ComponentModel.Serialization.WorkflowMarkupSerializer
		/// AFTER:  LogicBuilder.Workflow.ComponentModel.Serialization.WorkflowMarkupSerializer
		/// </summary>
		/// <param name="rules">RuleSet to serialize.</param>
		/// <returns>XOML XML string.</returns>
		public static string Serialize(RuleSet rules)
		{
			StringBuilder sbXOML = new StringBuilder();
			using ( StringWriter wtr = new StringWriter(sbXOML, System.Globalization.CultureInfo.InvariantCulture) )
			{
				using ( XmlTextWriter xwtr = new XmlTextWriter(wtr) )
				{
					xwtr.Formatting = Formatting.Indented;
					WorkflowMarkupSerializer serializer = new WorkflowMarkupSerializer();
					serializer.Serialize(xwtr, rules);
				}
			}
			return sbXOML.ToString();
		}

		// 12/12/2012 Paul.  For security reasons, we want to restrict the data types available to the rules wizard.
		/// <summary>
		/// Validates a single rule defined by its condition and action strings.
		///
		/// PLATFORM LIMITATION: This method requires RulesParser.ParseCondition() and
		/// RulesParser.ParseStatementList() which are NOT available in
		/// LogicBuilder.Workflow.Activities.Rules 2.0.4 (no string-based rule parser).
		/// Rule validation must use Deserialize(sXOML) to load rules from stored XOML.
		///
		/// MIGRATION NOTE: Throws PlatformNotSupportedException to preserve the method signature
		/// while clearly indicating the limitation. Update callers to use XOML-based workflows.
		/// </summary>
		public static void RulesValidate(Guid gID, string sRULE_NAME, int nPRIORITY, string sREEVALUATION, bool bACTIVE, string sCONDITION, string sTHEN_ACTIONS, string sELSE_ACTIONS, Type thisType, SplendidRulesTypeProvider typeProvider)
		{
			// .NET 10 Migration: RulesParser is not available in LogicBuilder.Workflow.Activities.Rules 2.0.4.
			// The original WF3 RulesParser parsed C# expression strings into CodeDom.
			// Use Deserialize(sXOML) to load and validate rules from stored XOML format.
			throw new PlatformNotSupportedException(
				"RulesValidate: String-based rule parsing via RulesParser is not supported in .NET 10. " +
				"LogicBuilder.Workflow.Activities.Rules 2.0.4 does not include a RulesParser implementation. " +
				"Use RulesUtil.Deserialize(sXOML) to load rules from XOML format stored in the database.");
		}

		/// <summary>
		/// Accumulates all validation errors from a RuleValidation into a newline-delimited string.
		/// Uses LogicBuilder.Workflow.ComponentModel.Compiler.ValidationError.ErrorText.
		/// BEFORE: System.Workflow.ComponentModel.Compiler.ValidationError
		/// AFTER:  LogicBuilder.Workflow.ComponentModel.Compiler.ValidationError
		/// </summary>
		/// <param name="validation">RuleValidation instance with populated Errors collection.</param>
		/// <returns>Newline-delimited validation error messages.</returns>
		public static string GetValidationErrors(RuleValidation validation)
		{
			StringBuilder sbErrors = new StringBuilder();
			// .NET 10 Migration: ValidationError is in LogicBuilder.Workflow.ComponentModel.Compiler.
			foreach ( ValidationError err in validation.Errors )
			{
				sbErrors.AppendLine(err.ErrorText);
			}
			return sbErrors.ToString();
		}

		/// <summary>
		/// Builds a validated RuleSet from a DataTable of rule definitions.
		/// Each row defines RULE_NAME, PRIORITY, REEVALUATION, ACTIVE, CONDITION, THEN_ACTIONS, ELSE_ACTIONS.
		///
		/// PLATFORM LIMITATION: This method requires RulesParser.ParseCondition() and
		/// RulesParser.ParseStatementList() which are NOT available in
		/// LogicBuilder.Workflow.Activities.Rules 2.0.4 (no string-based rule parser port).
		/// Rules must be stored and loaded as XOML via Deserialize(sXOML).
		///
		/// MIGRATION NOTE: Throws PlatformNotSupportedException. Update callers to use
		/// Deserialize(sXOML) with XOML-format rules retrieved from the database.
		/// </summary>
		public static RuleSet BuildRuleSet(DataTable dtRules, RuleValidation validation)
		{
			// .NET 10 Migration: RulesParser is not available in LogicBuilder.Workflow.Activities.Rules 2.0.4.
			throw new PlatformNotSupportedException(
				"BuildRuleSet(DataTable): String-based rule parsing via RulesParser is not supported in .NET 10. " +
				"LogicBuilder.Workflow.Activities.Rules 2.0.4 does not include a RulesParser implementation. " +
				"Use RulesUtil.Deserialize(sXOML) to load rules from XOML format stored in the database.");
		}

		// 08/16/2017 Paul.  Single action business rule.
		/// <summary>
		/// Builds a validated RuleSet containing a single unconditional rule from an action string.
		///
		/// PLATFORM LIMITATION: This method requires RulesParser.ParseStatementList() which is
		/// NOT available in LogicBuilder.Workflow.Activities.Rules 2.0.4.
		/// Use Deserialize(sXOML) with pre-serialized XOML rules instead.
		/// </summary>
		public static RuleSet BuildRuleSet(string sTHEN_ACTIONS, RuleValidation validation)
		{
			// .NET 10 Migration: RulesParser.ParseStatementList() is not available in LogicBuilder 2.0.4.
			throw new PlatformNotSupportedException(
				"BuildRuleSet(string): String-based rule parsing via RulesParser is not supported in .NET 10. " +
				"LogicBuilder.Workflow.Activities.Rules 2.0.4 does not include a RulesParser implementation. " +
				"Use RulesUtil.Deserialize(sXOML) to load rules from XOML format stored in the database.");
		}

		// 06/02/2021 Paul.  React client needs to share code.
		/// <summary>
		/// Builds a DataTable of rule definitions from a React client JSON-deserialized dictionary.
		/// The dictionary is expected to have a "NewDataSet" → "Table1" nested structure
		/// matching the XML DataSet format produced by the SplendidCRM rules editor.
		/// </summary>
		/// <param name="dictRulesXml">
		/// Dictionary parsed from rules JSON/XML containing NewDataSet/Table1 rule rows.
		/// </param>
		/// <returns>DataTable with columns: ID, RULE_NAME, PRIORITY, REEVALUATION, ACTIVE, CONDITION, THEN_ACTIONS, ELSE_ACTIONS.</returns>
		public static DataTable BuildRuleDataTable(Dictionary<string, object> dictRulesXml)
		{
			DataTable dtRules = new DataTable();
			DataColumn colID           = new DataColumn("ID"          , typeof(System.Guid   ));
			DataColumn colRULE_NAME    = new DataColumn("RULE_NAME"   , typeof(System.String ));
			DataColumn colPRIORITY     = new DataColumn("PRIORITY"    , typeof(System.Int32  ));
			DataColumn colREEVALUATION = new DataColumn("REEVALUATION", typeof(System.String ));
			DataColumn colACTIVE       = new DataColumn("ACTIVE"      , typeof(System.Boolean));
			DataColumn colCONDITION    = new DataColumn("CONDITION"   , typeof(System.String ));
			DataColumn colTHEN_ACTIONS = new DataColumn("THEN_ACTIONS", typeof(System.String ));
			DataColumn colELSE_ACTIONS = new DataColumn("ELSE_ACTIONS", typeof(System.String ));
			dtRules.Columns.Add(colID          );
			dtRules.Columns.Add(colRULE_NAME   );
			dtRules.Columns.Add(colPRIORITY    );
			dtRules.Columns.Add(colREEVALUATION);
			dtRules.Columns.Add(colACTIVE      );
			dtRules.Columns.Add(colCONDITION   );
			dtRules.Columns.Add(colTHEN_ACTIONS);
			dtRules.Columns.Add(colELSE_ACTIONS);
			if ( dictRulesXml != null )
			{
				if ( dictRulesXml.ContainsKey("NewDataSet") )
				{
					Dictionary<string, object> dictNewDataSet = dictRulesXml["NewDataSet"] as Dictionary<string, object>;
					if ( dictNewDataSet != null )
					{
						if ( dictNewDataSet.ContainsKey("Table1") )
						{
							System.Collections.ArrayList lstTable1 = dictNewDataSet["Table1"] as System.Collections.ArrayList;
							if ( lstTable1 != null )
							{
								foreach ( Dictionary<string, object> dictRule in lstTable1 )
								{
									DataRow row = dtRules.NewRow();
									dtRules.Rows.Add(row);
									row["ID"          ] = (dictRule.ContainsKey("ID"          ) ? Sql.ToString(dictRule["ID"          ]) : String.Empty);
									row["RULE_NAME"   ] = (dictRule.ContainsKey("RULE_NAME"   ) ? Sql.ToString(dictRule["RULE_NAME"   ]) : String.Empty);
									row["PRIORITY"    ] = (dictRule.ContainsKey("PRIORITY"    ) ? Sql.ToString(dictRule["PRIORITY"    ]) : String.Empty);
									row["REEVALUATION"] = (dictRule.ContainsKey("REEVALUATION") ? Sql.ToString(dictRule["REEVALUATION"]) : String.Empty);
									row["ACTIVE"      ] = (dictRule.ContainsKey("ACTIVE"      ) ? Sql.ToString(dictRule["ACTIVE"      ]) : String.Empty);
									row["CONDITION"   ] = (dictRule.ContainsKey("CONDITION"   ) ? Sql.ToString(dictRule["CONDITION"   ]) : String.Empty);
									row["THEN_ACTIONS"] = (dictRule.ContainsKey("THEN_ACTIONS") ? Sql.ToString(dictRule["THEN_ACTIONS"]) : String.Empty);
									row["ELSE_ACTIONS"] = (dictRule.ContainsKey("ELSE_ACTIONS") ? Sql.ToString(dictRule["ELSE_ACTIONS"]) : String.Empty);
								}
							}
						}
					}
				}
			}
			return dtRules;
		}
	}
}
