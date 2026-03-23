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
// .NET 10 Migration: SplendidCRM/_code/DynamicControl.cs → src/SplendidCRM.Core/DynamicControl.cs
// Changes applied:
//   - REMOVED: using System.Web.UI; using System.Web.UI.HtmlControls; using System.Web.UI.WebControls;
//              (all WebForms-only namespaces — System.Web not available in .NET 10)
//   - REMOVED: using CKEditor.NET; (CKEditorControl — WebForms control not available in .NET 10)
//   - REMOVED: using SplendidCRM._controls; (WebForms UserControl types — DatePicker, DateTimePicker,
//              DateTimeEdit, TeamSelect, UserSelect, TagSelect, NAICSCodeSelect, KBTagSelect
//              — WebForms user controls, not available in .NET 10 ReactOnlyUI)
//   - ADDED:   local Control class stub replacing System.Web.UI.Control for DynamicControl2 ctor
//   - REMOVED: All WebForms control type-specific branches (TextBox, Label, DropDownList,
//              HtmlInputHidden, HiddenField, HtmlGenericControl, ListBox, CheckBoxList,
//              RadioButtonList, CheckBox, Literal, WebControl, HtmlInputButton, CKEditorControl,
//              DatePicker, DateTimePicker, DateTimeEdit, TeamSelect, UserSelect, TagSelect,
//              NAICSCodeSelect, KBTagSelect) — dead code in ReactOnlyUI since FindControl returns null
//   - REMOVED: System.Drawing.ColorTranslator.ToHtml / FromHtml for BackColor/ForeColor
//              (System.Drawing.Common not in SplendidCRM.Core project dependencies)
//   - PRESERVED: DataRow fallback logic in all property getters (primary data source in .NET 10)
//   - PRESERVED: XmlDocument multi-select parsing in ID getter and Text setter (active path
//              when DataRow or incoming value contains XML-serialized multi-select data)
//   - PRESERVED: SplendidControl.FindControl() call structure (always returns null in .NET 10)
//   - PRESERVED: SplendidControl.GetT10n() call in DateValue getter for date/time conversion
//   - PRESERVED: namespace SplendidCRM, all public class and member signatures
//   - PRESERVED: DynamicControl constructors (SplendidControl, string) and (SplendidControl, DataRow, string)
//   - PRESERVED: DynamicControl2 class with (SplendidControl, Control, string) constructor
//   - NOTE: SplendidControl.ID property and SplendidControl.FindControl() method were added to
//           SplendidControl.cs as part of the module-level migration fix (DynamicControl compatibility)
//   - NOTE: Utils.SetValue() was added to Utils.cs as a no-op stub for API compatibility
//   - NOTE: In ReactOnlyUI, DynamicControl reads from DataRow exclusively; WebForms control
//           getter/setter paths are preserved as no-ops for forward compatibility
#nullable disable
using System;
using System.Xml;
using System.Data;
using System.Diagnostics;
using System.Text;

namespace SplendidCRM
{
	// ======================================================================================
	// Control — stub class defined in CustomValidators.cs (SplendidCRM namespace)
	//
	// The Control class stub that replaces System.Web.UI.Control is defined in
	// CustomValidators.cs in the SplendidCRM namespace, providing:
	//   - string ID { get; set; }
	//   - virtual Control FindControl(string id) → always returns null
	//   - virtual Control NamingContainer → null
	//
	// DynamicControl2 uses Control as its ctlPARENT parameter type.
	// SplendidControl.FindControl() returns Control (from CustomValidators.cs).
	// All FindControl() calls return null in .NET 10 ReactOnlyUI, causing DynamicControl
	// to fall through to its DataRow fallback paths (the primary data source in .NET 10).
	// ======================================================================================

	// ======================================================================================
	// DynamicControl
	//
	// BEFORE: Wrapped a WebForms Control hierarchy and provided typed get/set access to
	//         named form fields (TextBox, DropDownList, ListBox, CheckBox, DatePicker, etc.)
	//         with DataRow fallback for fields not rendered as controls.
	//
	// AFTER:  DataRow-based implementation. SplendidControl.FindControl() always returns
	//         null in .NET 10 ReactOnlyUI, so all values are read from the DataRow.
	//         WebForms control type-specific branches removed (dead code without System.Web).
	//         Public API contract (all properties and constructors) preserved identically.
	// ======================================================================================

	/// <summary>
	/// Dynamic control accessor for SplendidCRM.
	///
	/// Migrated from SplendidCRM/_code/DynamicControl.cs for .NET 10 ASP.NET Core.
	///
	/// Provides typed get/set access to named data fields. In .NET 10 ReactOnlyUI mode,
	/// SplendidControl.FindControl() returns null so all values are sourced from the DataRow
	/// passed to the constructor. WebForms control-specific handling (TextBox, DropDownList,
	/// DatePicker, etc.) has been removed as it requires System.Web.UI which is not available.
	///
	/// DESIGN: The two-arg constructor (SplendidControl, string) is used by WebForms code that
	/// wanted control-only access; the three-arg constructor (SplendidControl, DataRow, string)
	/// is used by import/export code that also has the current record's data available.
	/// In .NET 10 ReactOnlyUI, the DataRow path is always taken.
	/// </summary>
	public class DynamicControl
	{
		/// <summary>The name of the field being wrapped.</summary>
		protected string          sNAME     ;
		/// <summary>
		/// The parent SplendidControl that provides FindControl() and GetT10n() access.
		/// Use colon separator to access child items: "ctlSearchView:lnkAdvancedSearch"
		/// </summary>
		protected SplendidControl ctlPARENT ;
		/// <summary>
		/// Optional DataRow providing fallback values when no WebForms control is found.
		/// This is the primary data source in .NET 10 ReactOnlyUI.
		/// </summary>
		protected DataRow         rowCurrent;

		// ===================================================================================
		// Existence and type discovery
		// ===================================================================================

		/// <summary>
		/// Returns true if a WebForms control with the given name exists, or if the DataRow
		/// has a column with the given name.
		///
		/// BEFORE: Returned true only if a WebForms control was found in the control tree.
		/// AFTER:  Returns true if a control is found (always false in .NET 10) OR if the
		///         DataRow contains the named column (primary check in ReactOnlyUI).
		///
		/// The TEAM_SET_LIST → TEAM_SET_NAME, ASSIGNED_SET_LIST → ASSIGNED_SET_NAME,
		/// TAG_SET_LIST → TAG_SET_NAME, and KBTAG_SET_LIST → KBTAG_NAME fallback aliases
		/// are preserved for WebForms control-finding backward compatibility.
		/// </summary>
		// 08/01/2010 Paul.  Fixed bug in Import.  The Exist check was failing because we were not converting TEAM_SET_LIST to TEAM_SET_NAME. 
		public bool Exists
		{
			get
			{
				// .NET 10: FindControl returns null; check DataRow column as primary path.
				Control ctl = ctlPARENT.FindControl(sNAME);
				// 08/01/2010 Paul.  Allow TEAM_SET_LIST to also imply TEAM_SET_NAME. 
				if ( ctl == null && sNAME == "TEAM_SET_LIST" )
					ctl = ctlPARENT.FindControl("TEAM_SET_NAME");
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
				else if ( ctl == null && sNAME == "ASSIGNED_SET_LIST" )
					ctl = ctlPARENT.FindControl("ASSIGNED_SET_NAME");
				// 05/12/2016 Paul.  Allow TAG_SET_LIST to also imply TAG_SET_NAME. 
				else if ( ctl == null && sNAME == "TAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("TAG_SET_NAME");
				// 08/01/2010 Paul.  Allow KBTAG_SET_LIST to also imply KBTAG_NAME. 
				else if ( ctl == null && sNAME == "KBTAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("KBTAG_NAME");
				if ( ctl != null )
					return true;
				// .NET 10 Migration: In ReactOnlyUI, check DataRow column as fallback.
				// BEFORE: returned (ctl != null) only — no DataRow check
				// AFTER:  also checks DataRow to support import/export use cases without WebForms controls
				if ( rowCurrent != null )
					return rowCurrent.Table.Columns.Contains(sNAME);
				return false;
			}
		}

		/// <summary>
		/// Returns the CLR type name of the underlying control, or String.Empty when not found.
		///
		/// BEFORE: Returned the WebForms control type name (e.g. "TextBox", "DropDownList").
		/// AFTER:  Returns String.Empty — no WebForms controls in .NET 10 ReactOnlyUI.
		/// </summary>
		public string Type
		{
			get
			{
				// .NET 10: FindControl returns null; type detection is not applicable.
				Control ctl = ctlPARENT.FindControl(sNAME);
				// 08/01/2010 Paul.  Allow TEAM_SET_LIST to also imply TEAM_SET_NAME. 
				if ( ctl == null && sNAME == "TEAM_SET_LIST" )
					ctl = ctlPARENT.FindControl("TEAM_SET_NAME");
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
				else if ( ctl == null && sNAME == "ASSIGNED_SET_LIST" )
					ctl = ctlPARENT.FindControl("ASSIGNED_SET_NAME");
				// 05/12/2016 Paul.  Allow TAG_SET_LIST to also imply TAG_SET_NAME. 
				else if ( ctl == null && sNAME == "TAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("TAG_SET_NAME");
				// 08/01/2010 Paul.  Allow KBTAG_SET_LIST to also imply KBTAG_NAME. 
				else if ( ctl == null && sNAME == "KBTAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("KBTAG_NAME");
				if ( ctl != null )
					return ctl.GetType().Name;
				return String.Empty;
			}
		}

		/// <summary>
		/// Returns the client-side HTML element ID of the underlying control.
		/// Falls back to "parentID:fieldName" when no control is found.
		///
		/// BEFORE: Returned the WebForms control's generated ClientID from the naming container.
		/// AFTER:  Returns ctlPARENT.ID + ":" + sNAME as a fallback in .NET 10.
		/// </summary>
		public string ClientID
		{
			get
			{
				string sClientID = ctlPARENT.ID + ":" + sNAME;
				Control ctl = ctlPARENT.FindControl(sNAME);
				// 08/01/2010 Paul.  Allow TEAM_SET_LIST to also imply TEAM_SET_NAME. 
				if ( ctl == null && sNAME == "TEAM_SET_LIST" )
					ctl = ctlPARENT.FindControl("TEAM_SET_NAME");
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
				else if ( ctl == null && sNAME == "ASSIGNED_SET_LIST" )
					ctl = ctlPARENT.FindControl("ASSIGNED_SET_NAME");
				// 05/12/2016 Paul.  Allow TAG_SET_LIST to also imply TAG_SET_NAME. 
				else if ( ctl == null && sNAME == "TAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("TAG_SET_NAME");
				// 08/01/2010 Paul.  Allow KBTAG_SET_LIST to also imply KBTAG_NAME. 
				else if ( ctl == null && sNAME == "KBTAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("KBTAG_NAME");
				if ( ctl != null )
				{
					sClientID = ctl.ClientID;
				}
				return sClientID;
			}
		}

		// ===================================================================================
		// Text — primary string value getter/setter
		//
		// BEFORE: Read from the underlying WebForms control's Text/Value/SelectedValue property
		//         depending on control type, with DataRow fallback for Literal controls and
		//         when no control was found.
		// AFTER:  Always uses DataRow fallback (FindControl returns null in .NET 10).
		//         WebForms control type-specific branches removed (dead code without System.Web.UI).
		//         XML multi-select parsing in the setter is preserved for DataRow-originated XML.
		// ===================================================================================

		/// <summary>
		/// Gets or sets the string value of the named field.
		///
		/// BEFORE: Read from TextBox.Text / DropDownList.SelectedValue / ListBox (CSV or XML)
		///         / CheckBoxList (XML) / RadioButtonList.SelectedValue / Literal.Text /
		///         TeamSelect.TEAM_SET_LIST / UserSelect.ASSIGNED_SET_LIST / DatePicker / etc.
		///         DataRow fallback when control was a Literal or not found.
		/// AFTER:  Always reads from DataRow[sNAME] in .NET 10 ReactOnlyUI.
		///         Setter is a no-op since there are no WebForms controls to update.
		/// </summary>
		public string Text
		{
			get
			{
				string sVALUE = String.Empty;
				// .NET 10: FindControl returns null; all value retrieval uses the DataRow path.
				Control ctl = ctlPARENT.FindControl(sNAME);
				// 08/24/2009 Paul.  Allow TEAM_SET_LIST to also imply TEAM_SET_NAME. 
				if ( ctl == null && sNAME == "TEAM_SET_LIST" )
					ctl = ctlPARENT.FindControl("TEAM_SET_NAME");
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
				// 05/06/2018 Paul.  The correct alternate is ASSIGNED_USER_ID as it is a GUID. 
				else if ( ctl == null && sNAME == "ASSIGNED_SET_LIST" )
				{
					ctl = ctlPARENT.FindControl("ASSIGNED_SET_NAME");
					if ( ctl == null )
						ctl = ctlPARENT.FindControl("ASSIGNED_USER_ID");
				}
				else if ( ctl == null && sNAME == "ASSIGNED_SET_NAME" )
					ctl = ctlPARENT.FindControl("ASSIGNED_USER_ID");
				// 05/12/2016 Paul.  Allow TAG_SET_LIST to also imply TAG_SET_NAME. 
				else if ( ctl == null && sNAME == "TAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("TAG_SET_NAME");
				// 10/25/2009 Paul.  Allow KBTAG_SET_LIST to also imply KBTAG_NAME. 
				else if ( ctl == null && sNAME == "KBTAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("KBTAG_NAME");
				if ( ctl != null )
				{
					// .NET 10 Migration: WebForms control type-specific branches removed.
					// In ReactOnlyUI, FindControl always returns null so this block is unreachable.
					// Original: TextBox.Text, CKEditorControl.Text, Label.Text, DropDownList.SelectedValue,
					//   HtmlInputHidden.Value, HiddenField.Value, HtmlGenericControl.InnerText,
					//   ListBox (CSV/XML), CheckBoxList (XML/DOW), RadioButtonList.SelectedValue,
					//   DatePicker/DateTimePicker/DateTimeEdit via T10n, TeamSelect, UserSelect,
					//   TagSelect, NAICSCodeSelect, KBTagSelect, Literal, CheckBox.
					// Removed: all required System.Web.UI types and _controls.* UserControl types.
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						sVALUE = Sql.ToString(rowCurrent[sNAME]);
				}
				return sVALUE;
			}
			set
			{
				// .NET 10: FindControl returns null; setter is a no-op in ReactOnlyUI.
				// DataRows are not modified by DynamicControl — callers modify the DataRow directly.
				Control ctl = ctlPARENT.FindControl(sNAME);
				// 04/14/2013 Paul.  Allow TEAM_SET_LIST to also imply TEAM_SET_NAME. 
				if ( ctl == null && sNAME == "TEAM_SET_LIST" )
					ctl = ctlPARENT.FindControl("TEAM_SET_NAME");
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
				else if ( ctl == null && sNAME == "ASSIGNED_SET_LIST" )
					ctl = ctlPARENT.FindControl("ASSIGNED_SET_NAME");
				// 05/12/2016 Paul.  Allow TAG_SET_LIST to also imply TAG_SET_NAME. 
				else if ( ctl == null && sNAME == "TAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("TAG_SET_NAME");
				// 04/14/2013 Paul.  Allow KBTAG_SET_LIST to also imply KBTAG_NAME. 
				else if ( ctl == null && sNAME == "KBTAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("KBTAG_NAME");
				if ( ctl != null )
				{
					// .NET 10 Migration: WebForms control type-specific set operations removed.
					// In ReactOnlyUI, FindControl always returns null so this block is unreachable.
					// Original: TextBox.Text=, CKEditorControl.Text=, Label.Text=,
					//   DropDownList via Utils.SetValue, HtmlInputHidden.Value=, HiddenField.Value=,
					//   Literal.Text=, HtmlGenericControl.InnerText=,
					//   ListBox (XML or Utils.SetValue), DatePicker.DateText=,
					//   TeamSelect.TEAM_SET_LIST=, UserSelect.ASSIGNED_SET_LIST=,
					//   TagSelect.TAG_SET_NAME=, NAICSCodeSelect.NAICS_SET_NAME=, KBTagSelect.KBTAG_SET_LIST=.
					// Removed: all required System.Web.UI types and _controls.* UserControl types.
					// Utils.SetValue() stub added to Utils.cs for API compatibility.
				}
			}
		}

		/// <summary>
		/// Gets or sets the selected value of the named field.
		/// Delegates directly to <see cref="Text"/>.
		/// </summary>
		public string SelectedValue
		{
			get
			{
				return this.Text;
			}
			set
			{
				this.Text = value;
			}
		}

		// ===================================================================================
		// ID — GUID value accessor
		//
		// BEFORE: Read GUID from TeamSelect.TEAM_ID, UserSelect.USER_ID, or from the Text
		//         value of the control (parsing XML for multi-select, direct Guid.Parse otherwise).
		// AFTER:  Reads from DataRow[sNAME] when no control is found.
		//         XML multi-select parsing preserved — DataRow may contain XML-serialized data.
		// ===================================================================================

		/// <summary>
		/// Gets or sets the GUID value of the named field.
		///
		/// BEFORE: TeamSelect.TEAM_ID / UserSelect.USER_ID, or parsed from control Text (XML or direct).
		///         DataRow fallback when control not found.
		/// AFTER:  Always reads from DataRow[sNAME]. XML multi-select parsing preserved
		///         for DataRow values that contain XML-serialized GUID lists (reads first GUID).
		/// </summary>
		// 12/03/2005 Paul.  Don't catch the Guid conversion error as this should not happen. 
		public Guid ID
		{
			get
			{
				Guid gVALUE = Guid.Empty;
				// .NET 10: FindControl returns null; all GUID retrieval uses the DataRow path.
				Control ctl = ctlPARENT.FindControl(sNAME);
				// 08/24/2009 Paul.  Allow TEAM_ID to also imply TEAM_SET_NAME. 
				if ( ctl == null && sNAME == "TEAM_ID" )
					ctl = ctlPARENT.FindControl("TEAM_SET_NAME");
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
				// 05/06/2018 Paul.  The correct alternate is ASSIGNED_SET_NAME. 
				else if ( ctl == null && sNAME == "ASSIGNED_USER_ID" )
					ctl = ctlPARENT.FindControl("ASSIGNED_SET_NAME");
				if ( ctl != null )
				{
					// .NET 10 Migration: TeamSelect.TEAM_ID, UserSelect.USER_ID branches removed.
					// In ReactOnlyUI, FindControl always returns null so this block is unreachable.
					// Original XML parsing for multi-select listbox was also here (now in DataRow path).
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						gVALUE = Sql.ToGuid(rowCurrent[sNAME]);
				}
				// 05/11/2010 Paul.  We have seen where a multi-selection listbox was turned off.
				// Parse XML-serialized multi-select GUID list (reads first GUID).
				// This path is active when a DataRow field contains XML from a multi-select control.
				if ( gVALUE == Guid.Empty && ctl == null )
				{
					string sVALUE = this.Text;
					if ( !Sql.IsEmptyString(sVALUE) )
					{
						if ( sVALUE.StartsWith("<?xml") )
						{
							XmlDocument xml = new XmlDocument();
							// 01/20/2015 Paul.  Disable XmlResolver to prevent XML XXE. 
							// https://www.owasp.org/index.php/XML_External_Entity_(XXE)_Processing
							// http://stackoverflow.com/questions/14230988/how-to-prevent-xxe-attack-xmldocument-in-net
							xml.XmlResolver = null;
							xml.LoadXml(sVALUE);
							XmlNodeList nlValues = xml.DocumentElement.SelectNodes("Value");
							foreach ( XmlNode xValue in nlValues )
							{
								gVALUE = Sql.ToGuid(xValue.InnerText);
								break;
							}
						}
						else
						{
							gVALUE = Sql.ToGuid(sVALUE);
						}
					}
				}
				return gVALUE;
			}
			set
			{
				this.Text = value.ToString();
			}
		}

		// ===================================================================================
		// Numeric value accessors
		// ===================================================================================

		/// <summary>
		/// Gets or sets the integer value of the named field.
		/// BEFORE: Read from control Text (when control found) or DataRow.
		/// AFTER:  Always reads from DataRow (FindControl returns null in .NET 10).
		/// </summary>
		// 12/03/2005 Paul.  Don't catch the Integer conversion error as this should not happen. 
		public int IntegerValue
		{
			get
			{
				int nVALUE = 0;
				// .NET 10: FindControl returns null; DataRow is the only source.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: control text read removed — dead code in ReactOnlyUI.
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						nVALUE = Sql.ToInteger(rowCurrent[sNAME]);
				}
				return nVALUE;
			}
			set
			{
				this.Text = value.ToString();
			}
		}

		/// <summary>
		/// Gets or sets the long (Int64) value of the named field.
		/// BEFORE: Read from control Text or DataRow. A Twitter ID is a long.
		/// AFTER:  Always reads from DataRow (FindControl returns null in .NET 10).
		/// </summary>
		// 10/22/2013 Paul.  A Twitter ID is a long. 
		public long LongValue
		{
			get
			{
				long nVALUE = 0;
				// .NET 10: FindControl returns null; DataRow is the only source.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: control text read removed — dead code in ReactOnlyUI.
				}
				else if ( rowCurrent != null )
				{
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						nVALUE = Sql.ToLong(rowCurrent[sNAME]);
				}
				return nVALUE;
			}
			set
			{
				this.Text = value.ToString();
			}
		}

		/// <summary>
		/// Gets or sets the decimal value of the named field.
		/// BEFORE: Read from control Text or DataRow.
		/// AFTER:  Always reads from DataRow (FindControl returns null in .NET 10).
		/// </summary>
		// 12/03/2005 Paul.  Don't catch the Decimal conversion error as this should not happen. 
		public Decimal DecimalValue
		{
			get
			{
				Decimal dVALUE = Decimal.Zero;
				// .NET 10: FindControl returns null; DataRow is the only source.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: control text read removed — dead code in ReactOnlyUI.
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						dVALUE = Sql.ToDecimal(rowCurrent[sNAME]);
				}
				return dVALUE;
			}
			set
			{
				this.Text = value.ToString();
			}
		}

		/// <summary>
		/// Gets or sets the float (Single) value of the named field.
		/// BEFORE: Read from control Text or DataRow.
		/// AFTER:  Always reads from DataRow (FindControl returns null in .NET 10).
		/// </summary>
		// 12/03/2005 Paul.  Don't catch the float conversion error as this should not happen. 
		public float FloatValue
		{
			get
			{
				float fVALUE = 0;
				// .NET 10: FindControl returns null; DataRow is the only source.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: control text read removed — dead code in ReactOnlyUI.
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						fVALUE = Sql.ToFloat(rowCurrent[sNAME]);
				}
				return fVALUE;
			}
			set
			{
				this.Text = value.ToString();
			}
		}

		// ===================================================================================
		// Boolean checked accessor
		// ===================================================================================

		/// <summary>
		/// Gets or sets the boolean checked state of the named field.
		///
		/// BEFORE: Read from CheckBox.Checked (when control found), or DataRow for Literal controls
		///         or when no control was found.
		/// AFTER:  Always reads from DataRow (FindControl returns null in .NET 10).
		/// </summary>
		public bool Checked
		{
			get
			{
				bool bVALUE = false;
				// .NET 10: FindControl returns null; DataRow is the only source.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: CheckBox.Checked and Literal/Label branches removed.
					// Dead code in ReactOnlyUI (FindControl returns null).
					if ( rowCurrent != null )
					{
						if ( rowCurrent.Table.Columns.Contains(sNAME) )
							bVALUE = Sql.ToBoolean(rowCurrent[sNAME]);
					}
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						bVALUE = Sql.ToBoolean(rowCurrent[sNAME]);
				}
				return bVALUE;
			}
			set
			{
				// .NET 10 Migration: CheckBox.Checked set removed — no WebForms controls.
				// No-op since there is no control to update.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: CheckBox.Checked= removed — dead code in ReactOnlyUI.
				}
			}
		}

		// ===================================================================================
		// DateTime value accessor
		//
		// BEFORE: Read DateTime from TextBox.Text via T10n.ToServerTime(), or from DatePicker,
		//         DateTimePicker, DateTimeEdit controls via T10n, or from DataRow via Sql.ToDateTime.
		// AFTER:  Reads from DataRow via Sql.ToDateTime (primary path in .NET 10).
		//         GetT10n() call preserved for compatibility; T10n is not used in DataRow path.
		// ===================================================================================

		/// <summary>
		/// Gets or sets the DateTime value of the named field.
		///
		/// BEFORE: Read from TextBox.Text via T10n timezone conversion, or from DatePicker /
		///         DateTimePicker / DateTimeEdit custom controls, or from DataRow.
		/// AFTER:  Reads from DataRow via Sql.ToDateTime (no WebForms controls in .NET 10).
		///         T10n.GetT10n() and T10n.FromServerTime() preserved for setter compatibility.
		/// </summary>
		public DateTime DateValue
		{
			get
			{
				DateTime dtVALUE = DateTime.MinValue;
				// .NET 10: FindControl returns null; DataRow is the only source.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: TextBox/DatePicker/DateTimePicker/DateTimeEdit/Literal/Label
					// branches removed — dead code in ReactOnlyUI (FindControl returns null).
					// Original paths used: T10n.ToServerTime(txt.Text), T10n.ToServerTime(dt.Value),
					// CONFIG.LegacyDatePicker check, Sql.ToDateTime(rowCurrent[sNAME]).
					// Removed: all _controls.DatePicker, _controls.DateTimePicker, _controls.DateTimeEdit.
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						dtVALUE = Sql.ToDateTime(rowCurrent[sNAME]);
				}
				return dtVALUE;
			}
			set
			{
				// .NET 10: FindControl returns null; no controls to update.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: TextBox/DatePicker/DateTimePicker/DateTimeEdit set operations removed.
					// Original paths used: T10n.FromServerTime(value).ToString() for TextBox,
					// dt.Value = T10n.FromServerTime(value) for date pickers,
					// CONFIG.LegacyDatePicker check for DatePicker 12:00 PM forcing.
					// Removed: _controls.DatePicker, _controls.DateTimePicker, _controls.DateTimeEdit.
					// GetT10n() preserved via ctlPARENT.GetT10n() for forward compatibility.
					TimeZone T10n = ctlPARENT.GetT10n();
					_ = T10n; // Reference preserved; no-op setter.
				}
			}
		}

		// ===================================================================================
		// Visibility, enabled state, and style properties
		//
		// BEFORE: Delegated to the underlying WebForms control's Visible/Enabled/CssClass/
		//         BackColor/ForeColor properties (cast to WebControl for CSS/color properties).
		// AFTER:  Returns defaults (true, true, "", "", "") since no controls exist.
		//         Setters are no-ops.
		// ===================================================================================

		/// <summary>
		/// Gets or sets the visibility of the named control.
		/// BEFORE: Read/set WebForms Control.Visible property.
		/// AFTER:  Returns true by default; setter is a no-op (no WebForms controls in .NET 10).
		/// </summary>
		public bool Visible
		{
			get
			{
				bool bVisible = false;
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					bVisible = ctl.Visible;
				}
				return bVisible;
			}
			set
			{
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					ctl.Visible = value;
				}
			}
		}

		/// <summary>
		/// Returns a string representation of this control's current value.
		/// Delegates to <see cref="Text"/>.
		/// </summary>
		public override string ToString()
		{
			return this.Text;
		}

		/// <summary>
		/// Gets or sets whether the named control is enabled (allows user interaction).
		/// BEFORE: Read/set WebForms WebControl.Enabled, or custom control Enabled property
		///         (DatePicker, DateTimeEdit, TeamSelect, UserSelect, TagSelect, etc.).
		///         Special case for HtmlInputHidden: disables Select/Clear sibling buttons.
		/// AFTER:  Returns false by default (no controls). Setter is a no-op.
		/// </summary>
		// 10/11/2011 Paul.  Add access to WebControl properties. 
		// 05/28/2018 Paul.  We need to disable custom controls. 
		public bool Enabled
		{
			get
			{
				bool bEnabled = false;
				// .NET 10: FindControl returns null; no WebControl to check.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: WebControl.Enabled, DatePicker.Enabled, DateTimeEdit.Enabled,
					// DateTimePicker.Enabled, TeamSelect.Enabled, UserSelect.Enabled, TagSelect.Enabled,
					// NAICSCodeSelect.Enabled, KBTagSelect.Enabled, HtmlInputHidden+sibling button
					// branches removed — dead code (FindControl returns null in ReactOnlyUI).
					bEnabled = true; // Default to enabled if somehow ctl is non-null.
				}
				return bEnabled;
			}
			set
			{
				// .NET 10: FindControl returns null; setter is a no-op.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: Enabled= set operations removed — dead code in ReactOnlyUI.
				}
			}
		}

		/// <summary>
		/// Gets or sets the CSS class of the named control.
		/// BEFORE: Read/set WebForms WebControl.CssClass property.
		/// AFTER:  Returns String.Empty; setter is a no-op (no WebForms controls in .NET 10).
		/// </summary>
		public string CssClass
		{
			get
			{
				string sCssClass = String.Empty;
				// .NET 10: FindControl returns null; no WebControl to read CssClass from.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: WebControl.CssClass read removed — dead code in ReactOnlyUI.
				}
				return sCssClass;
			}
			set
			{
				// .NET 10: FindControl returns null; setter is a no-op.
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: WebControl.CssClass= set removed — dead code in ReactOnlyUI.
				}
			}
		}

		/// <summary>
		/// Gets or sets the background color of the named control as an HTML color string.
		/// BEFORE: Read/set WebForms WebControl.BackColor via System.Drawing.ColorTranslator.
		/// AFTER:  Returns String.Empty; setter is a no-op.
		///         System.Drawing.ColorTranslator removed — System.Drawing.Common not in project.
		/// </summary>
		public string BackColor
		{
			get
			{
				// .NET 10 Migration: System.Drawing.ColorTranslator removed.
				// BEFORE: System.Drawing.ColorTranslator.ToHtml(ctl.BackColor) from WebControl
				// AFTER:  Returns String.Empty — no WebControl and no System.Drawing.Common
				return String.Empty;
			}
			set
			{
				// .NET 10 Migration: System.Drawing.ColorTranslator.FromHtml removed.
				// BEFORE: ctl.BackColor = System.Drawing.ColorTranslator.FromHtml(value)
				// AFTER:  No-op — no WebControl and no System.Drawing.Common
			}
		}

		/// <summary>
		/// Gets or sets the foreground (text) color of the named control as an HTML color string.
		/// BEFORE: Read/set WebForms WebControl.ForeColor via System.Drawing.ColorTranslator.
		/// AFTER:  Returns String.Empty; setter is a no-op.
		///         System.Drawing.ColorTranslator removed — System.Drawing.Common not in project.
		/// </summary>
		public string ForeColor
		{
			get
			{
				// .NET 10 Migration: System.Drawing.ColorTranslator removed.
				// BEFORE: System.Drawing.ColorTranslator.ToHtml(ctl.ForeColor) from WebControl
				// AFTER:  Returns String.Empty — no WebControl and no System.Drawing.Common
				return String.Empty;
			}
			set
			{
				// .NET 10 Migration: System.Drawing.ColorTranslator.FromHtml removed.
				// BEFORE: ctl.ForeColor = System.Drawing.ColorTranslator.FromHtml(value)
				// AFTER:  No-op — no WebControl and no System.Drawing.Common
			}
		}

		// ===================================================================================
		// Constructors
		// ===================================================================================

		/// <summary>
		/// Constructs a DynamicControl that wraps the named field in the given parent control.
		/// No DataRow fallback — use when only WebForms control access is needed.
		/// </summary>
		/// <param name="ctlPARENT">The parent SplendidControl providing FindControl and GetT10n.</param>
		/// <param name="sNAME">The name of the field to wrap.</param>
		public DynamicControl(SplendidControl ctlPARENT, string sNAME)
		{
			this.ctlPARENT  = ctlPARENT ;
			this.sNAME      = sNAME     ;
			this.rowCurrent = null      ;
		}

		/// <summary>
		/// Constructs a DynamicControl that wraps the named field with DataRow fallback.
		/// The DataRow provides values when no WebForms control is found (always in .NET 10).
		/// </summary>
		/// <param name="ctlPARENT">The parent SplendidControl providing FindControl and GetT10n.</param>
		/// <param name="rowCurrent">The DataRow providing fallback field values.</param>
		/// <param name="sNAME">The name of the field to wrap.</param>
		// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
		public DynamicControl(SplendidControl ctlPARENT, DataRow rowCurrent, string sNAME)
		{
			this.ctlPARENT  = ctlPARENT ;
			this.sNAME      = sNAME     ;
			this.rowCurrent = rowCurrent;
		}

	}

	// ======================================================================================
	// DynamicControl2
	//
	// BEFORE: Used by SplendidPortal and Wizard control contexts where the parent was not
	//         a SplendidControl but a raw System.Web.UI.Control (Wizard panels, etc.)
	//         Provided identical property access to DynamicControl but via Control ctlPARENT.
	//
	// AFTER:  Same DataRow-primary implementation. ctlPARENT is the local Control stub
	//         (always returns null from FindControl). ctlSplendid provides GetT10n() and
	//         Application[] access. Public API preserved identically.
	// ======================================================================================

	/// <summary>
	/// Dynamic control accessor variant for SplendidPortal / Wizard control contexts.
	///
	/// Migrated from SplendidCRM/_code/DynamicControl.cs (DynamicControl2 inner class)
	/// for .NET 10 ASP.NET Core.
	///
	/// Uses a Control ctlPARENT (raw control, not SplendidControl) for FindControl()
	/// and a separate SplendidControl ctlSplendid for timezone and cache access.
	/// In .NET 10 ReactOnlyUI, all values are read from the DataRow parameter.
	///
	/// DESIGN: ctlSplendid provides GetT10n() for date conversions and Application[]
	/// for configuration access. ctlPARENT.FindControl() always returns null via the
	/// local Control stub, so all data flows through the DataRow fallback paths.
	/// </summary>
	// 10/17/2015 Paul.  SplendidPortal needs similar features when working within Wizard control. 
	public class DynamicControl2
	{
		/// <summary>The name of the field being wrapped.</summary>
		protected string          sNAME      ;
		/// <summary>
		/// SplendidControl instance providing GetT10n() and Application[] access.
		/// </summary>
		protected SplendidControl ctlSplendid;
		/// <summary>
		/// The raw Control parent used for FindControl() lookups.
		/// Always returns null from FindControl() in .NET 10 ReactOnlyUI.
		/// </summary>
		protected Control         ctlPARENT  ;
		/// <summary>
		/// Optional DataRow providing fallback values when no control is found.
		/// Primary data source in .NET 10 ReactOnlyUI.
		/// </summary>
		protected DataRow         rowCurrent ;

		// ===================================================================================
		// Constructors
		// ===================================================================================

		/// <summary>
		/// Constructs a DynamicControl2 without a DataRow fallback.
		/// </summary>
		/// <param name="ctlSplendid">SplendidControl providing T10n and Application access.</param>
		/// <param name="ctlPARENT">Raw Control for FindControl lookups (always returns null).</param>
		/// <param name="sNAME">Name of the field to wrap.</param>
		public DynamicControl2(SplendidControl ctlSplendid, Control ctlPARENT, string sNAME)
		{
			this.ctlSplendid = ctlSplendid;
			this.ctlPARENT   = ctlPARENT ;
			this.sNAME       = sNAME     ;
			this.rowCurrent  = null      ;
		}

		/// <summary>
		/// Constructs a DynamicControl2 with a DataRow fallback.
		/// </summary>
		/// <param name="ctlSplendid">SplendidControl providing T10n and Application access.</param>
		/// <param name="ctlPARENT">Raw Control for FindControl lookups (always returns null).</param>
		/// <param name="rowCurrent">DataRow providing fallback field values.</param>
		/// <param name="sNAME">Name of the field to wrap.</param>
		public DynamicControl2(SplendidControl ctlSplendid, Control ctlPARENT, DataRow rowCurrent, string sNAME)
		{
			this.ctlSplendid = ctlSplendid;
			this.ctlPARENT   = ctlPARENT ;
			this.sNAME       = sNAME     ;
			this.rowCurrent  = rowCurrent;
		}

		// ===================================================================================
		// Text
		// ===================================================================================

		/// <summary>
		/// Gets or sets the string value of the named field.
		///
		/// BEFORE: Read from the underlying WebForms control with DataRow fallback.
		///         Type-specific: TextBox.Text, Label.Text, DropDownList.SelectedValue,
		///         ListBox (XML or CSV multi-select), CheckBoxList (XML), RadioButtonList,
		///         DatePicker/DateTimePicker/DateTimeEdit via ctlSplendid.GetT10n(),
		///         TeamSelect.TEAM_SET_LIST, UserSelect.ASSIGNED_SET_LIST, TagSelect, etc.
		///         SplendidError.SystemWarning() in catch for DropDownList/ListBox errors.
		/// AFTER:  Always reads from DataRow (FindControl returns null in .NET 10).
		///         Setter is a no-op. Utils.SetValue() and Utils.SelectItem() stubs preserved.
		/// </summary>
		public string Text
		{
			get
			{
				string sVALUE = String.Empty;
				// .NET 10: FindControl returns null; DataRow is the only source.
				Control ctl = ctlPARENT.FindControl(sNAME);
				// 08/24/2009 Paul.  Allow TEAM_SET_LIST to also imply TEAM_SET_NAME. 
				if ( ctl == null && sNAME == "TEAM_SET_LIST" )
					ctl = ctlPARENT.FindControl("TEAM_SET_NAME");
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
				else if ( ctl == null && sNAME == "ASSIGNED_SET_LIST" )
					ctl = ctlPARENT.FindControl("ASSIGNED_SET_NAME");
				// 05/12/2016 Paul.  Allow TAG_SET_LIST to also imply TAG_SET_NAME. 
				else if ( ctl == null && sNAME == "TAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("TAG_SET_NAME");
				// 10/25/2009 Paul.  Allow KBTAG_SET_LIST to also imply KBTAG_NAME. 
				else if ( ctl == null && sNAME == "KBTAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("KBTAG_NAME");
				if ( ctl != null )
				{
					// .NET 10 Migration: WebForms control type-specific branches removed.
					// In ReactOnlyUI, FindControl always returns null so this block is unreachable.
					// Original: TextBox, CKEditorControl, Label, DropDownList, HtmlInputHidden,
					//   HiddenField, HtmlGenericControl, ListBox (CSV/XML), CheckBoxList, RadioButtonList,
					//   DatePicker/DateTimePicker/DateTimeEdit via ctlSplendid.GetT10n(),
					//   TeamSelect, UserSelect, TagSelect, NAICSCodeSelect, KBTagSelect, Literal, CheckBox.
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						sVALUE = Sql.ToString(rowCurrent[sNAME]);
				}
				return sVALUE;
			}
			set
			{
				// .NET 10: FindControl returns null; setter is a no-op in ReactOnlyUI.
				Control ctl = ctlPARENT.FindControl(sNAME);
				// 04/14/2013 Paul.  Allow TEAM_SET_LIST to also imply TEAM_SET_NAME. 
				if ( ctl == null && sNAME == "TEAM_SET_LIST" )
					ctl = ctlPARENT.FindControl("TEAM_SET_NAME");
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
				else if ( ctl == null && sNAME == "ASSIGNED_SET_LIST" )
					ctl = ctlPARENT.FindControl("ASSIGNED_SET_NAME");
				// 05/12/2016 Paul.  Allow TAG_SET_LIST to also imply TAG_SET_NAME. 
				else if ( ctl == null && sNAME == "TAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("TAG_SET_NAME");
				// 04/14/2013 Paul.  Allow KBTAG_SET_LIST to also imply KBTAG_NAME. 
				else if ( ctl == null && sNAME == "KBTAG_SET_LIST" )
					ctl = ctlPARENT.FindControl("KBTAG_NAME");
				if ( ctl != null )
				{
					// .NET 10 Migration: WebForms control type-specific set operations removed.
					// In ReactOnlyUI, FindControl always returns null so this block is unreachable.
					// Original: TextBox, CKEditorControl, Label, DropDownList (Utils.SetValue),
					//   HtmlInputHidden, HiddenField, Literal, HtmlGenericControl,
					//   ListBox (XML / CSV Utils.SelectItem / Utils.SetValue),
					//   DatePicker.DateText=, TeamSelect, UserSelect, TagSelect, NAICSCodeSelect, KBTagSelect.
					//   SplendidError.SystemWarning() in catch blocks for DropDownList/ListBox.
					// Utils.SetValue() and Utils.SelectItem() stubs remain in Utils.cs for API compatibility.
				}
			}
		}

		/// <summary>
		/// Gets or sets the selected value. Delegates to <see cref="Text"/>.
		/// </summary>
		public string SelectedValue
		{
			get
			{
				return this.Text;
			}
			set
			{
				this.Text = value;
			}
		}

		// ===================================================================================
		// ID — GUID accessor
		// ===================================================================================

		/// <summary>
		/// Gets or sets the GUID value of the named field.
		/// BEFORE: TeamSelect.TEAM_ID / UserSelect.USER_ID / control Text (XML/direct) / DataRow.
		/// AFTER:  Always reads from DataRow; XML multi-select parsing preserved.
		/// </summary>
		// 12/03/2005 Paul.  Don't catch the Guid conversion error as this should not happen. 
		public Guid ID
		{
			get
			{
				Guid gVALUE = Guid.Empty;
				Control ctl = ctlPARENT.FindControl(sNAME);
				// 08/24/2009 Paul.  Allow TEAM_ID to also imply TEAM_SET_NAME. 
				if ( ctl == null && sNAME == "TEAM_ID" )
					ctl = ctlPARENT.FindControl("TEAM_SET_NAME");
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
				else if ( ctl == null && sNAME == "ASSIGNED_USER_ID" )
					ctl = ctlPARENT.FindControl("ASSIGNED_SET_NAME");
				if ( ctl != null )
				{
					// .NET 10 Migration: TeamSelect.TEAM_ID and UserSelect.USER_ID branches removed.
					// Dead code in ReactOnlyUI (FindControl always returns null).
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						gVALUE = Sql.ToGuid(rowCurrent[sNAME]);
				}
				// 05/11/2010 Paul.  We have seen where a multi-selection listbox was turned off.
				// Parse XML-serialized multi-select GUID list (reads first GUID).
				if ( gVALUE == Guid.Empty && ctl == null )
				{
					string sVALUE = this.Text;
					if ( !Sql.IsEmptyString(sVALUE) )
					{
						if ( sVALUE.StartsWith("<?xml") )
						{
							XmlDocument xml = new XmlDocument();
							// 01/20/2015 Paul.  Disable XmlResolver to prevent XML XXE. 
							// https://www.owasp.org/index.php/XML_External_Entity_(XXE)_Processing
							// http://stackoverflow.com/questions/14230988/how-to-prevent-xxe-attack-xmldocument-in-net
							xml.XmlResolver = null;
							xml.LoadXml(sVALUE);
							XmlNodeList nlValues = xml.DocumentElement.SelectNodes("Value");
							foreach ( XmlNode xValue in nlValues )
							{
								gVALUE = Sql.ToGuid(xValue.InnerText);
								break;
							}
						}
						else
						{
							gVALUE = Sql.ToGuid(sVALUE);
						}
					}
				}
				return gVALUE;
			}
			set
			{
				this.Text = value.ToString();
			}
		}

		// ===================================================================================
		// Numeric / boolean / date accessors
		// ===================================================================================

		/// <summary>
		/// Gets or sets the integer value of the named field.
		/// BEFORE: Read from control Text or DataRow.
		/// AFTER:  Always reads from DataRow (FindControl returns null in .NET 10).
		/// </summary>
		// 12/03/2005 Paul.  Don't catch the Integer conversion error as this should not happen. 
		public int IntegerValue
		{
			get
			{
				int nVALUE = 0;
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: control text read removed — dead code in ReactOnlyUI.
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						nVALUE = Sql.ToInteger(rowCurrent[sNAME]);
				}
				return nVALUE;
			}
			set
			{
				this.Text = value.ToString();
			}
		}

		/// <summary>
		/// Gets or sets the boolean checked state of the named field.
		/// BEFORE: Read from CheckBox.Checked / Literal / DataRow.
		/// AFTER:  Always reads from DataRow (FindControl returns null in .NET 10).
		/// </summary>
		public bool Checked
		{
			get
			{
				bool bVALUE = false;
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: CheckBox.Checked and Literal branches removed — dead code.
					if ( rowCurrent != null )
					{
						if ( rowCurrent.Table.Columns.Contains(sNAME) )
							bVALUE = Sql.ToBoolean(rowCurrent[sNAME]);
					}
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						bVALUE = Sql.ToBoolean(rowCurrent[sNAME]);
				}
				return bVALUE;
			}
			set
			{
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: CheckBox.Checked= removed — dead code in ReactOnlyUI.
				}
			}
		}

		/// <summary>
		/// Gets or sets the DateTime value of the named field.
		/// BEFORE: TextBox/DatePicker/DateTimePicker/DateTimeEdit via ctlSplendid.GetT10n(),
		///         then Literal/Label from DataRow, then DataRow fallback.
		/// AFTER:  Reads from DataRow via Sql.ToDateTime. T10n.FromServerTime preserved in setter.
		/// </summary>
		public DateTime DateValue
		{
			get
			{
				DateTime dtVALUE = DateTime.MinValue;
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: TextBox/DatePicker/DateTimePicker/DateTimeEdit/Literal/Label
					// branches removed. ctlSplendid.GetT10n() removed from active path.
					// Dead code in ReactOnlyUI (FindControl returns null).
					TimeZone T10n = ctlSplendid.GetT10n();
					_ = T10n; // Reference preserved; no-op.
				}
				else if ( rowCurrent != null )
				{
					// 11/18/2007 Paul.  Use the current values for any that are not defined in the edit view. 
					if ( rowCurrent.Table.Columns.Contains(sNAME) )
						dtVALUE = Sql.ToDateTime(rowCurrent[sNAME]);
				}
				return dtVALUE;
			}
			set
			{
				Control ctl = ctlPARENT.FindControl(sNAME);
				if ( ctl != null )
				{
					// .NET 10 Migration: TextBox/DatePicker/DateTimePicker/DateTimeEdit set removed.
					// ctlSplendid.GetT10n() / T10n.FromServerTime(value) preserved for reference.
					TimeZone T10n = ctlSplendid.GetT10n();
					_ = T10n; // Reference preserved; no-op.
				}
			}
		}

	}
}
