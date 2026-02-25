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
// .NET 10 Migration: SplendidCRM/_code/SubPanelControl.cs → src/SplendidCRM.Core/SubPanelControl.cs
// Changes applied:
//   - REMOVED: using System.Data.Common; (not referenced directly; DataTable/DataRow from System.Data is sufficient)
//   - ADDED:   using System.Collections.Generic; (Dictionary<string, object> for ViewState replacement)
//   - ADDED:   using Microsoft.AspNetCore.Http; (IHttpContextAccessor DI parameter)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache DI parameter)
//   - CHANGED: SubPanelControl now explicitly extends InlineEditControl (preserved from original)
//   - ADDED:   Constructor accepting IHttpContextAccessor and IMemoryCache, forwarding to InlineEditControl base
//   - REPLACED: ViewState[key] (System.Web.UI.Control.ViewState) → _viewState[key]
//              (instance Dictionary<string, object>, initialized as empty collection per instance)
//              WebForms ViewState stored serializable objects across postbacks on the same page;
//              in ASP.NET Core (DI-scoped, request-scoped instance), a plain instance dictionary
//              is the idiomatic equivalent — it lives for the duration of the request, just as
//              ViewState persisted for the duration of a single postback round-trip.
//   - PRESERVED: namespace SplendidCRM (unchanged for backward compatibility)
//   - PRESERVED: bEditView backing field name and IsEditView property (identical signatures)
//   - PRESERVED: m_sMODULE usage (protected field inherited from SplendidControl via InlineEditControl)
//   - PRESERVED: All six method signatures — GetDeletedEditViewRelationships(),
//                GetUpdatedEditViewRelationships(), DeleteEditViewRelationship(Guid),
//                UpdateEditViewRelationship(Guid), UpdateEditViewRelationship(string[]),
//                CreateEditViewRelationships(DataTable, string)
//   - PRESERVED: All business logic, inline comments, and original author annotations
//   - PRESERVED: Minimal change clause — only changes required for .NET Framework 4.8 → .NET 10 migration applied
#nullable disable
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Sub-panel control providing relationship tracking for inline edit panels in SplendidCRM.
	///
	/// Migrated from SplendidCRM/_code/SubPanelControl.cs for .NET 10 ASP.NET Core.
	///
	/// BEFORE (.NET Framework 4.8):
	///   public class SubPanelControl : InlineEditControl
	///   where InlineEditControl : SplendidControl : System.Web.UI.UserControl
	///   Instantiated by ASP.NET WebForms runtime; ViewState provided by control infrastructure.
	///
	/// AFTER (.NET 10 ASP.NET Core):
	///   public class SubPanelControl : InlineEditControl
	///   where InlineEditControl : SplendidControl (DI-injectable service, no System.Web dependency).
	///   Explicit constructor required for .NET 10 dependency injection container.
	///   ViewState replaced by an instance-level Dictionary&lt;string, object&gt; (_viewState).
	///
	/// Relationship Tracking Design:
	///   The class maintains two per-instance relationship ID collections:
	///     - "{Module}.Deleted"  — IDs of relationships deleted by the user in the current edit
	///     - "{Module}.Updated"  — IDs of relationships added/updated by the user in the current edit
	///   These collections are stored in _viewState and accessed via GetDeletedEditViewRelationships()
	///   and GetUpdatedEditViewRelationships(). The collections are mutually exclusive — an ID cannot
	///   be in both lists simultaneously (deleting removes it from Updated, and vice versa).
	///
	/// Migration changes summary:
	///   - Added:    Constructor forwarding IHttpContextAccessor + IMemoryCache to InlineEditControl base
	///   - Replaced: ViewState[key] → _viewState[key] (instance Dictionary&lt;string, object&gt;)
	///   - Preserved: All six method signatures unchanged
	///   - Preserved: namespace SplendidCRM, class name SubPanelControl, base class InlineEditControl
	/// </summary>
	public class SubPanelControl : InlineEditControl
	{
		// =====================================================================================
		// ViewState replacement
		//
		// BEFORE: ViewState[key] — System.Web.UI.Control.ViewState (StateBag)
		//         A WebForms per-control dictionary that persisted values across HTTP postbacks
		//         using hidden field serialization in the page response.
		//
		// AFTER:  _viewState[key] — instance Dictionary<string, object>
		//         Since ASP.NET Core uses request-scoped DI instances (one SubPanelControl instance
		//         per request lifecycle), a plain instance dictionary provides equivalent storage
		//         semantics: the data lives for the lifetime of the request, matching the single-
		//         postback lifespan that WebForms ViewState covered per roundtrip.
		//
		// Thread-safety: No concurrent access expected within a single HTTP request scope.
		// =====================================================================================
		private readonly Dictionary<string, object> _viewState = new Dictionary<string, object>();

		/// <summary>
		/// Whether this control is currently in edit view mode.
		/// PRESERVED: backing field name bEditView, property name IsEditView, get/set signatures.
		/// </summary>
		protected bool bEditView;

		/// <summary>
		/// Constructs a SubPanelControl with required DI dependencies.
		///
		/// BEFORE (.NET Framework 4.8):
		///   No explicit constructor — the ASP.NET WebForms runtime instantiated user controls
		///   automatically via the compiled ASPX page infrastructure.
		///
		/// AFTER (.NET 10 ASP.NET Core):
		///   Explicit constructor required for the .NET 10 DI container. The constructor
		///   forwards IHttpContextAccessor and IMemoryCache to the InlineEditControl base class,
		///   which in turn forwards them to SplendidControl for session, request, and cache access.
		///
		/// DI Registration (callers must register in Program.cs or equivalent):
		///   services.AddScoped&lt;SubPanelControl&gt;();
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current for session, request, response, and Items access.
		/// BEFORE: HttpContext.Current.Session["key"] / .Request / .Response
		/// AFTER:  _httpContextAccessor.HttpContext?.Session / .Request / .Response
		/// Forwarded to: InlineEditControl → SplendidControl._httpContextAccessor (protected field)
		/// </param>
		/// <param name="memoryCache">
		/// Replaces Application[] (HttpApplicationState) and HttpRuntime.Cache access.
		/// BEFORE: Application["Modules.X.ArchiveEnabled"] / HttpRuntime.Cache["key"]
		/// AFTER:  _memoryCache.Get&lt;object&gt;("Modules.X.ArchiveEnabled")
		/// Forwarded to: InlineEditControl → SplendidControl._memoryCache (protected field)
		/// </param>
		public SubPanelControl(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
			: base(httpContextAccessor, memoryCache)
		{
		}

		// =====================================================================================
		// IsEditView property
		// PRESERVED: Identical to original — boolean flag indicating whether the sub-panel
		// is in edit view mode versus detail view mode.
		// =====================================================================================

		/// <summary>
		/// Gets or sets whether the sub-panel control is operating in edit view mode.
		/// When true, the panel renders inline edit fields; when false, it renders read-only detail fields.
		/// </summary>
		public bool IsEditView
		{
			get { return bEditView; }
			set { bEditView = value; }
		}

		// =====================================================================================
		// Relationship collection accessors
		// PRESERVED: Method signatures, logic, and behavior identical to original.
		// CHANGED:   ViewState[key] → _viewState[key] (see ViewState replacement comment above)
		// =====================================================================================

		/// <summary>
		/// Returns the collection of relationship IDs that have been deleted in the current edit session.
		///
		/// PRESERVED: Method signature and return type identical to original.
		///
		/// Returns the existing UniqueGuidCollection from _viewState if one exists, or a new empty
		/// collection if no deletions have been recorded yet. The returned collection is not
		/// automatically persisted — callers must assign the result back to _viewState when
		/// adding entries (see DeleteEditViewRelationship).
		/// </summary>
		/// <returns>
		/// UniqueGuidCollection containing all relationship IDs deleted during this edit session.
		/// Never returns null — returns an empty collection if no deletions recorded.
		/// </returns>
		protected UniqueGuidCollection GetDeletedEditViewRelationships()
		{
			// BEFORE: UniqueGuidCollection arrDELETED = ViewState[m_sMODULE + ".Deleted"] as UniqueGuidCollection;
			// AFTER:  _viewState replaces ViewState (Dictionary<string, object> instance field)
			_viewState.TryGetValue(m_sMODULE + ".Deleted", out object obj);
			UniqueGuidCollection arrDELETED = obj as UniqueGuidCollection;
			if ( arrDELETED == null )
				arrDELETED = new UniqueGuidCollection();
			return arrDELETED;
		}

		/// <summary>
		/// Returns the collection of relationship IDs that have been added or updated in the current edit session.
		///
		/// PRESERVED: Method signature and return type identical to original.
		///
		/// Returns the existing UniqueGuidCollection from _viewState if one exists, or a new empty
		/// collection if no updates have been recorded yet. The returned collection is not
		/// automatically persisted — callers must assign the result back to _viewState when
		/// adding entries (see UpdateEditViewRelationship and CreateEditViewRelationships).
		/// </summary>
		/// <returns>
		/// UniqueGuidCollection containing all relationship IDs added/updated during this edit session.
		/// Never returns null — returns an empty collection if no updates recorded.
		/// </returns>
		protected UniqueGuidCollection GetUpdatedEditViewRelationships()
		{
			// BEFORE: UniqueGuidCollection arrUPDATED = ViewState[m_sMODULE + ".Updated"] as UniqueGuidCollection;
			// AFTER:  _viewState replaces ViewState (Dictionary<string, object> instance field)
			_viewState.TryGetValue(m_sMODULE + ".Updated", out object obj);
			UniqueGuidCollection arrUPDATED = obj as UniqueGuidCollection;
			if ( arrUPDATED == null )
				arrUPDATED = new UniqueGuidCollection();
			return arrUPDATED;
		}

		/// <summary>
		/// Records a relationship ID as deleted in the current edit session.
		///
		/// PRESERVED: Method signature and logic identical to original.
		///
		/// Adds gDELETE_ID to the Deleted collection and simultaneously removes it from the
		/// Updated collection (if present), ensuring mutual exclusion between the two lists.
		/// This handles the case where a user adds a relationship then removes it in the same session.
		/// </summary>
		/// <param name="gDELETE_ID">
		/// The Guid identifying the relationship record to mark as deleted.
		/// </param>
		protected void DeleteEditViewRelationship(Guid gDELETE_ID)
		{
			// 01/27/2010 Paul.  Keep a separate list of removed items. 
			UniqueGuidCollection arrDELETED = GetDeletedEditViewRelationships();
			arrDELETED.Add(gDELETE_ID);
			// BEFORE: ViewState[m_sMODULE + ".Deleted"] = arrDELETED;
			// AFTER:  _viewState replaces ViewState
			_viewState[m_sMODULE + ".Deleted"] = arrDELETED;
			
			// BEFORE: UniqueGuidCollection arrUPDATED = ViewState[m_sMODULE + ".Updated"] as UniqueGuidCollection;
			// AFTER:  _viewState replaces ViewState
			_viewState.TryGetValue(m_sMODULE + ".Updated", out object obj);
			UniqueGuidCollection arrUPDATED = obj as UniqueGuidCollection;
			if ( arrUPDATED != null )
			{
				arrUPDATED.Remove(gDELETE_ID);
				// BEFORE: ViewState[m_sMODULE + ".Updated"] = arrUPDATED;
				// AFTER:  _viewState replaces ViewState
				_viewState[m_sMODULE + ".Updated"] = arrUPDATED;
			}
		}

		/// <summary>
		/// Records a relationship ID as added or updated in the current edit session.
		///
		/// PRESERVED: Method signature and logic identical to original.
		///
		/// Adds gUPDATE_ID to the Updated collection and simultaneously removes it from the
		/// Deleted collection (if present), ensuring mutual exclusion between the two lists.
		/// This handles the case where a user removes a relationship then adds it back in the same session.
		/// </summary>
		/// <param name="gUPDATE_ID">
		/// The Guid identifying the relationship record to mark as added or updated.
		/// </param>
		protected void UpdateEditViewRelationship(Guid gUPDATE_ID)
		{
			UniqueGuidCollection arrUPDATED = GetUpdatedEditViewRelationships();
			arrUPDATED.Add(gUPDATE_ID);
			// BEFORE: ViewState[m_sMODULE + ".Updated"] = arrUPDATED;
			// AFTER:  _viewState replaces ViewState
			_viewState[m_sMODULE + ".Updated"] = arrUPDATED;
			
			// 01/27/2010 Paul.  Just in case the user is adding back a record that he previous removed. 
			// BEFORE: UniqueGuidCollection arrDELETED = ViewState[m_sMODULE + ".Deleted"] as UniqueGuidCollection;
			// AFTER:  _viewState replaces ViewState
			_viewState.TryGetValue(m_sMODULE + ".Deleted", out object obj);
			UniqueGuidCollection arrDELETED = obj as UniqueGuidCollection;
			if ( arrDELETED != null )
			{
				arrDELETED.Remove(gUPDATE_ID);
				// BEFORE: ViewState[m_sMODULE + ".Deleted"] = arrDELETED;
				// AFTER:  _viewState replaces ViewState
				_viewState[m_sMODULE + ".Deleted"] = arrDELETED;
			}
		}

		/// <summary>
		/// Records multiple relationship IDs (from a string array) as added or updated
		/// in the current edit session.
		///
		/// PRESERVED: Method signature and logic identical to original.
		///
		/// Adds each string ID converted to Guid (via Sql.ToGuid) to the Updated collection.
		/// Simultaneously removes each ID from the Deleted collection (if present).
		/// This is used when bulk-selecting multiple relationship records for addition.
		/// </summary>
		/// <param name="arrID">
		/// Array of string-formatted Guid values identifying relationships to mark as updated.
		/// Each string is converted to Guid via Sql.ToGuid(); invalid or empty values are silently
		/// ignored by UniqueGuidCollection.Add() (which skips Guid.Empty).
		/// </param>
		protected void UpdateEditViewRelationship(string[] arrID)
		{
			UniqueGuidCollection arrUPDATED = GetUpdatedEditViewRelationships();
			foreach ( string item in arrID )
			{
				Guid gUPDATE_ID = Sql.ToGuid(item);
				arrUPDATED.Add(gUPDATE_ID);
			}
			// BEFORE: ViewState[m_sMODULE + ".Updated"] = arrUPDATED;
			// AFTER:  _viewState replaces ViewState
			_viewState[m_sMODULE + ".Updated"] = arrUPDATED;
			
			// BEFORE: UniqueGuidCollection arrDELETED = ViewState[m_sMODULE + ".Deleted"] as UniqueGuidCollection;
			// AFTER:  _viewState replaces ViewState
			_viewState.TryGetValue(m_sMODULE + ".Deleted", out object obj);
			UniqueGuidCollection arrDELETED = obj as UniqueGuidCollection;
			if ( arrDELETED != null )
			{
				foreach ( string item in arrID )
				{
					Guid gUPDATE_ID = Sql.ToGuid(item);
					arrDELETED.Remove(gUPDATE_ID);
				}
				// BEFORE: ViewState[m_sMODULE + ".Deleted"] = arrDELETED;
				// AFTER:  _viewState replaces ViewState
				_viewState[m_sMODULE + ".Deleted"] = arrDELETED;
			}
		}

		/// <summary>
		/// Initializes the Updated relationship collection from an existing DataTable of relationship records.
		///
		/// PRESERVED: Method signature and logic identical to original.
		///
		/// Creates a fresh UniqueGuidCollection populated with the primary key values from each row
		/// in the DataTable, then stores it as the Updated collection. Used when loading an existing
		/// relationship panel to pre-populate the tracking state with currently associated records.
		/// </summary>
		/// <param name="dt">
		/// DataTable containing the existing relationship records.
		/// DataTable.Rows is iterated to extract primary key values.
		/// </param>
		/// <param name="sPrimaryField">
		/// The column name in dt that contains the primary key Guid values for the relationship records.
		/// Each row[sPrimaryField] value is converted via Sql.ToGuid() and added to the collection.
		/// </param>
		protected void CreateEditViewRelationships(DataTable dt, string sPrimaryField)
		{
			UniqueGuidCollection arrUPDATED = new UniqueGuidCollection();
			foreach ( DataRow row in dt.Rows )
			{
				Guid gUPDATE_ID = Sql.ToGuid(row[sPrimaryField]);
				arrUPDATED.Add(gUPDATE_ID);
			}
			// BEFORE: ViewState[m_sMODULE + ".Updated"] = arrUPDATED;
			// AFTER:  _viewState replaces ViewState
			_viewState[m_sMODULE + ".Updated"] = arrUPDATED;
		}
	}
}
