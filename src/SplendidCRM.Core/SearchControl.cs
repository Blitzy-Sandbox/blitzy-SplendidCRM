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
// .NET 10 Migration: SplendidCRM/_code/SearchControl.cs → src/SplendidCRM.Core/SearchControl.cs
// Changes applied:
//   - REMOVED: using System.Web; (no System.Web in .NET 10)
//   - REMOVED: using System.Web.UI.WebControls; (WebForms namespace not available in .NET 10)
//   - REMOVED: implicit System.Web.UI.UserControl base class from SplendidControl chain
//   - ADDED:   using Microsoft.AspNetCore.Http; (IHttpContextAccessor replaces HttpContext.Current)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache replaces Application[])
//   - ADDED:   Constructor with IHttpContextAccessor + IMemoryCache DI parameters,
//              passed through to SplendidControl base class constructor
//   - PRESERVED: namespace SplendidCRM
//   - PRESERVED: public class SearchControl : SplendidControl (inherits migrated base)
//   - PRESERVED: public CommandEventHandler Command field
//              (CommandEventHandler delegate defined in SplendidControl.cs, same namespace)
//   - PRESERVED: protected void Page_Command(object sender, CommandEventArgs e)
//              (CommandEventArgs defined in SplendidPage.cs, same namespace SplendidCRM)
//   - PRESERVED: public virtual void ClearForm() — empty body, override in derived classes
//   - PRESERVED: public virtual void SqlSearchClause(IDbCommand cmd) — override in derived classes
//   - PRESERVED: using System.Data; (IDbCommand interface for SqlSearchClause parameter)
#nullable disable
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Base search control for SplendidCRM module search forms.
	///
	/// Migrated from SplendidCRM/_code/SearchControl.cs for .NET 10 ASP.NET Core.
	///
	/// BEFORE (.NET Framework 4.8):
	///   public class SearchControl : SplendidControl
	///   where SplendidControl ultimately extended System.Web.UI.UserControl.
	///   Lifecycle driven by the WebForms Page processing pipeline.
	///   Static access via HttpContext.Current and Application[].
	///
	/// AFTER (.NET 10 ASP.NET Core):
	///   public class SearchControl : SplendidControl
	///   where SplendidControl is a DI-injectable service class (no System.Web base class).
	///   Lifecycle driven by ASP.NET Core DI; context access via IHttpContextAccessor.
	///   Application[] replaced by IMemoryCache injection.
	///
	/// DESIGN NOTES:
	///   • Register as SCOPED so each HTTP request gets its own instance with live Session data.
	///   • Derived classes override ClearForm() to reset search form field values.
	///   • Derived classes override SqlSearchClause(IDbCommand) to append WHERE predicates.
	///   • Page_Command raises the public Command event — derived classes wire it to button controls.
	/// </summary>
	public class SearchControl : SplendidControl
	{
		// =====================================================================================
		// Public event field
		// BEFORE: public CommandEventHandler Command; (System.Web.UI.WebControls.CommandEventHandler)
		// AFTER:  public CommandEventHandler Command; (local delegate defined in SplendidControl.cs)
		//
		// CommandEventHandler delegate: void(object sender, CommandEventArgs e)
		// Defined in: src/SplendidCRM.Core/SplendidControl.cs (same namespace SplendidCRM)
		// =====================================================================================

		/// <summary>
		/// Event raised when a search command button is clicked (e.g., Search, Clear).
		/// Consumers assign a CommandEventHandler to respond to search actions.
		///
		/// Preserved from original WebForms CommandEventHandler pattern.
		/// The CommandEventHandler delegate is defined in SplendidControl.cs (same namespace).
		/// </summary>
		public CommandEventHandler Command;

		// =====================================================================================
		// Constructor
		// BEFORE: No explicit constructor — WebForms instantiated controls via reflection.
		// AFTER:  Explicit constructor required for ASP.NET Core DI container registration.
		//
		// Parameters are passed directly through to the SplendidControl base class.
		// SplendidControl constructor signature (all optional after memoryCache):
		//   SplendidControl(IHttpContextAccessor, IMemoryCache, Security=null,
		//                   SplendidCache=null, SplendidPage=null)
		// =====================================================================================

		/// <summary>
		/// Initializes a new instance of <see cref="SearchControl"/> with required DI dependencies.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces <c>HttpContext.Current</c> static access throughout the SplendidControl base.
		/// BEFORE: HttpContext.Current.Session["USER_ID"]
		/// AFTER:  _httpContextAccessor.HttpContext?.Session.GetString("USER_ID")
		/// </param>
		/// <param name="memoryCache">
		/// Replaces <c>Application[]</c> and <c>HttpRuntime.Cache</c> static access.
		/// BEFORE: Application["Modules.Accounts.ArchiveEnabled"]
		/// AFTER:  _memoryCache.Get&lt;object&gt;("Modules.Accounts.ArchiveEnabled")
		/// </param>
		public SearchControl(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
			: base(httpContextAccessor, memoryCache)
		{
		}

		// =====================================================================================
		// Event dispatch
		// BEFORE: Called automatically by WebForms button control's Command event binding.
		// AFTER:  Called explicitly by derived search control implementations or controllers.
		// =====================================================================================

		/// <summary>
		/// Dispatches a search command to any registered <see cref="Command"/> handler.
		///
		/// BEFORE: Wired via WebForms declarative event syntax — OnCommand="Page_Command".
		/// AFTER:  Called explicitly when search button actions are processed in API context.
		///
		/// CommandEventArgs is defined in SplendidPage.cs (namespace SplendidCRM).
		/// </summary>
		/// <param name="sender">The source of the command event (typically the invoking button).</param>
		/// <param name="e">Command event data including CommandName and CommandArgument.</param>
		protected void Page_Command(object sender, CommandEventArgs e)
		{
			if ( Command != null )
				Command(this, e);
		}

		// =====================================================================================
		// Virtual overridable methods
		// Derived search controls (e.g., Accounts_SearchView) override these to provide
		// module-specific search form logic.
		// =====================================================================================

		/// <summary>
		/// Clears all search form fields to their default/empty state.
		///
		/// Override in derived classes to reset specific control values.
		/// Base implementation is intentionally empty — no shared fields to clear.
		///
		/// BEFORE: Called by "Clear" button handler in WebForms search UserControl.
		/// AFTER:  Called from controller action or API endpoint that processes clear commands.
		/// </summary>
		public virtual void ClearForm()
		{
		}

		/// <summary>
		/// Appends module-specific WHERE clause predicates to the provided SQL command.
		///
		/// Override in derived classes to add search criteria based on the control's current
		/// field values. The base implementation is intentionally empty — SearchControl itself
		/// contributes no predicates; all logic lives in derived module search controls.
		///
		/// BEFORE: Called by module ListView page to build the search SQL.
		/// AFTER:  Called by RestController or module API action to build search SQL.
		/// </summary>
		/// <param name="cmd">
		/// Provider-agnostic SQL command to which WHERE predicates are appended.
		/// Implementations add parameters via cmd.Parameters and append to the CommandText.
		/// At runtime this will be a <see cref="Microsoft.Data.SqlClient.SqlCommand"/> instance.
		/// The <see cref="System.Data.IDbCommand"/> abstraction allows unit-testable derived classes.
		/// </param>
		public virtual void SqlSearchClause(IDbCommand cmd)
		{
		}
	}
}
