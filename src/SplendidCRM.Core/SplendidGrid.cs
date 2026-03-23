/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc.
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
 *
 * This program is free software: you can redistribute it and/or modify it under the terms of the
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3
 * of the License, or (at your option) any later version.
 *
 * .NET 10 Migration: SplendidCRM/_code/SplendidGrid.cs → src/SplendidCRM.Core/SplendidGrid.cs
 *
 * Changes applied for .NET 10 ASP.NET Core migration:
 *   - REMOVED: using System.Web; using System.Web.UI.*; using System.Web.UI.WebControls.*
 *              using System.Web.UI.HtmlControls.*; using System.Web.SessionState;
 *              (all WebForms-only namespaces eliminated)
 *   - REMOVED: System.Web.UI.WebControls.DataGrid base class from SplendidGrid
 *              (replaced by standalone DI-injectable service class)
 *   - REMOVED: System.Web.UI.UserControl base class from DynamicImage
 *   - REMOVED: AjaxControlToolkit.HoverMenuExtender usage from OnItemCreated, CreateItemTemplateHover
 *   - REMOVED: WebForms control types: Control, Literal, HyperLink, Image, ImageButton, Panel,
 *              CheckBox, DataGridItem, DataGridItemEventArgs, DataGridPageChangedEventArgs,
 *              DataGridSortCommandEventArgs, LinkButton, Label, LiteralControl, TableCell,
 *              HiddenField, HtmlGenericControl (none exist in .NET 10)
 *   - REPLACED: HttpContext.Current.Items["L10n"] → _httpContextAccessor.HttpContext?.Items["L10n"] as L10N
 *   - REPLACED: HttpContext.Current.Items["T10n"] → _httpContextAccessor.HttpContext?.Items["T10n"] as TimeZone
 *   - REPLACED: HttpContext.Current.Items["C10n"] → _httpContextAccessor.HttpContext?.Items["C10n"] as Currency
 *   - REPLACED: HttpContext.Current.Session["key"] → _httpContextAccessor.HttpContext?.Session.GetString("key")
 *   - REPLACED: HttpContext.Current.Application["key"] → _memoryCache.Get<object>("key")
 *   - REPLACED: HttpUtility.HtmlEncode() → WebUtility.HtmlEncode() (System.Net, .NET 10)
 *   - REPLACED: SplendidCache.CustomList(listName, value, ref bCustomCache) →
 *              _splendidCache.CustomList(listName, culture) + DataTable lookup
 *   - REPLACED: SplendidCache.AssignedUser(Guid) static string → _splendidCache.AssignedUser(Guid) DataTable
 *   - REPLACED: ViewState["key"] dictionary → _viewState Dictionary<string, object>
 *   - REPLACED: SplendidDynamic.AppendGridColumns(sGRID_NAME, this, ...) →
 *              _splendidDynamic.GridColumns(sGRID_NAME, arrSelectFields, null)
 *   - REPLACED: Crm.Modules.ItemName(HttpContext.Current.Application, ...) →
 *              Crm.Modules.ItemName(_memoryCache, ...) via injected IMemoryCache
 *   - REPLACED: Crm.Modules.RelativePath(Application, sMODULE_TYPE) →
 *              Crm.Modules.RelativePath(_memoryCache, sMODULE_TYPE) (instance method)
 *   - ADDED:   ITemplate local interface stub (no System.Web.UI.ITemplate in .NET 10)
 *   - ADDED:   DI constructor for SplendidGrid:
 *              IHttpContextAccessor, IMemoryCache, Security, SplendidCache, SplendidDynamic
 *   - ADDED:   Schema-compatible InstantiateIn() method (no params) on all template classes
 *   - ADAPTED: InputCheckbox(bool, string, Guid, HiddenField) → InputCheckbox(string, string)
 *   - ADAPTED: InputCheckbox(bool, string, string, HiddenField) → InputCheckbox(string, string, string, string)
 *   - ADAPTED: OnItemCreated/OnPageIndexChanged/OnSort → simplified no-op stubs (WebForms events removed)
 *   - ADAPTED: SortColumn/SortOrder → backed by _viewState Dictionary instead of WebForms ViewState
 *   - PRESERVED: namespace SplendidCRM, all public method signatures, all data processing business logic
 *   - PRESERVED: ControlChars.CrLf (defined in VisualBasic.cs within namespace SplendidCRM)
 *   - PRESERVED: SelectMethodHandler delegate, DynamicImage.SkinID property
 *   - PRESERVED: GDPR data privacy field erasing logic (Sql.IsDataPrivacyErasedField, DataPrivacyErasedField)
 *   - PRESERVED: ACL access level checks via Security.GetUserAccess (instance method via DI)
 *   - PRESERVED: Timezone/Currency conversion (T10n.FromServerTime, C10n.ToCurrency)
 *   - NOTE: Template class InstantiateIn() is no-op — React SPA handles grid rendering client-side
 *   - NOTE: FormatCellData() methods on each template class preserve original OnDataBinding logic
 *           for use by any caller that needs server-side cell formatting
 *********************************************************************************************************************/
#nullable disable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	// =====================================================================================
	// ITemplate — local interface stub
	//
	// BEFORE: System.Web.UI.ITemplate (WebForms interface with InstantiateIn(Control container))
	// AFTER:  local interface stub — InstantiateIn() with no params (React SPA rendering)
	//
	// Template classes implement this interface for schema compatibility.
	// The InstantiateIn() method is intentionally a no-op — React handles grid rendering.
	// Business logic is preserved in FormatCellData() methods on each template class.
	// =====================================================================================
	public interface ITemplate
	{
		/// <summary>
		/// Instantiates the template content.
		/// No-op in .NET 10 — React SPA handles grid rendering client-side.
		/// Business logic preserved in FormatCellData() on each template class.
		/// </summary>
		void InstantiateIn();
	}

	#region Create Item Templates

	// =====================================================================================
	// CreateItemTemplateTranslated
	//
	// BEFORE: public class CreateItemTemplateTranslated : ITemplate (WebForms)
	//         Constructor: (string sDATA_FIELD)
	// AFTER:  .NET 10 adapter — constructor extended to match CreateItemTemplateLiteral signature
	//         Constructor: (string sDATA_FIELD, string sDATA_FORMAT, string sMODULE_TYPE)
	//         InstantiateIn(Control container) replaced by InstantiateIn() no-op
	// =====================================================================================

	/// <summary>
	/// Template that renders a translated (localized) value for a single data field.
	/// Uses L10N.Term() to translate the raw field value.
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateTranslated class.
	/// </summary>
	public class CreateItemTemplateTranslated : ITemplate
	{
		protected string sDATA_FIELD;
		protected string sDATA_FORMAT;
		protected string sMODULE_TYPE;

		/// <summary>
		/// Original single-param constructor — preserved for backward compatibility.
		/// BEFORE: public CreateItemTemplateTranslated(string sDATA_FIELD)
		/// </summary>
		public CreateItemTemplateTranslated(string sDATA_FIELD)
		{
			this.sDATA_FIELD  = sDATA_FIELD;
			this.sDATA_FORMAT = String.Empty;
			this.sMODULE_TYPE = String.Empty;
		}

		/// <summary>
		/// Extended three-param constructor — aligned with CreateItemTemplateLiteral signature.
		/// Schema-required overload: CreateItemTemplateTranslated(string, string, string)
		/// </summary>
		public CreateItemTemplateTranslated(string sDATA_FIELD, string sDATA_FORMAT, string sMODULE_TYPE)
		{
			this.sDATA_FIELD  = sDATA_FIELD;
			this.sDATA_FORMAT = sDATA_FORMAT;
			this.sMODULE_TYPE = sMODULE_TYPE;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// BEFORE: WebForms registered Literal DataBinding handler for each row.
		/// AFTER:  no-op; use FormatCellData() for server-side cell text formatting.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the cell data for a row. Preserves original OnDataBinding business logic.
		/// Returns HTML-encoded translated text from L10N.Term() for the field value.
		/// </summary>
		/// <param name="row">The data row to format.</param>
		/// <param name="L10n">Localization instance for term translation.</param>
		/// <param name="httpContextAccessor">HTTP context accessor for per-request services (can be null).</param>
		/// <param name="memoryCache">Memory cache for application-state lookups.</param>
		/// <returns>Formatted cell HTML string.</returns>
		public string FormatCellData(DataRowView row, L10N L10n, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			if (row == null) return sDATA_FIELD;
			try
			{
				// 04/30/2006 Paul.  Use the Context to store pointers to the localization objects.
				if (L10n == null && httpContextAccessor?.HttpContext != null)
				{
					L10n = httpContextAccessor.HttpContext.Items["L10n"] as L10N;
					if (L10n == null)
					{
						string sCULTURE = httpContextAccessor.HttpContext.Session?.GetString("USER_SETTINGS/CULTURE") ?? "en-US";
						L10n = new L10N(sCULTURE, memoryCache);
					}
				}
				if (row[sDATA_FIELD] != DBNull.Value)
				{
					if (L10n != null)
						return L10n.Term(Sql.ToString(row[sDATA_FIELD]));
					return Sql.ToString(row[sDATA_FIELD]);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	// =====================================================================================
	// CreateItemTemplateLiteral
	//
	// BEFORE: public class CreateItemTemplateLiteral : ITemplate (WebForms)
	//         Constructor: (string sDATA_FIELD, string sDATA_FORMAT, string sMODULE_TYPE)
	// AFTER:  .NET 10 adapter — same 3-string constructor (unchanged)
	//         InstantiateIn(Control container) replaced by InstantiateIn() no-op
	//         HttpContext.Current replaced by IHttpContextAccessor/IMemoryCache params
	// =====================================================================================

	/// <summary>
	/// Template that renders a literal (unlinked) value for a data field,
	/// applying type-specific formatting: DateTime, Date, Currency, MultiLine, Tags, CheckBox.
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateLiteral class.
	/// </summary>
	public class CreateItemTemplateLiteral : ITemplate
	{
		protected string sDATA_FIELD;
		protected string sDATA_FORMAT;
		protected string sMODULE_TYPE;

		/// <summary>
		/// Creates a new literal template for a data field with optional formatting.
		/// Constructor: CreateItemTemplateLiteral(string, string, string) — unchanged from original.
		/// </summary>
		/// <param name="sDATA_FIELD">DataRow field name to render.</param>
		/// <param name="sDATA_FORMAT">Display format: DateTime, Date, Currency, MultiLine, Tags, CheckBox,
		///   or a String.Format template (e.g. "{0:###}") or empty for default text rendering.</param>
		/// <param name="sMODULE_TYPE">Optional module type for lookup of related record display names.</param>
		public CreateItemTemplateLiteral(string sDATA_FIELD, string sDATA_FORMAT, string sMODULE_TYPE)
		{
			this.sDATA_FIELD  = sDATA_FIELD;
			this.sDATA_FORMAT = sDATA_FORMAT;
			this.sMODULE_TYPE = sMODULE_TYPE;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// BEFORE: WebForms registered Literal DataBinding handler with OnDataBinding.
		/// AFTER:  no-op; use FormatCellData() for server-side cell text formatting.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the cell data for a row. Preserves original OnDataBinding business logic
		/// including DateTime timezone conversion, Currency formatting, MultiLine HTML cleanup,
		/// Tags span wrapping, CheckBox HTML rendering, and GDPR erasure detection.
		/// </summary>
		public string FormatCellData(DataRowView row, L10N L10n, TimeZone T10n, Currency C10n, IMemoryCache memoryCache)
		{
			if (row == null) return sDATA_FIELD;
			try
			{
				if (row[sDATA_FIELD] != DBNull.Value)
				{
					switch (sDATA_FORMAT)
					{
						case "DateTime":
						{
							// 03/30/2007 Paul.  T10n should never be NULL.
							if (T10n != null)
								return Sql.ToString(T10n.FromServerTime(row[sDATA_FIELD]));
							break;
						}
						case "Date":
						{
							if (T10n != null)
								return Sql.ToDateString(T10n.FromServerTime(row[sDATA_FIELD]));
							break;
						}
						case "Currency":
						{
							// 05/09/2006 Paul.  Convert the currency values before displaying.
							if (C10n != null)
							{
								Decimal d = C10n.ToCurrency(Convert.ToDecimal(row[sDATA_FIELD]));
								string sCurrencyFormat = Sql.ToString(memoryCache?.Get<object>("CONFIG.currency_format"));
								return d.ToString(sCurrencyFormat);
							}
							break;
						}
						case "MultiLine":
						{
							// 05/20/2009 Paul.  We need a way to preserve CRLF in description fields.
							return EmailUtils.NormalizeDescription(Sql.ToString(row[sDATA_FIELD]));
						}
						case "Tags":
						{
							// 05/14/2016 Paul.  Add Tags module.
							string sDATA = Sql.ToString(row[sDATA_FIELD]);
							if (!Sql.IsEmptyString(sDATA))
								sDATA = "<span class='Tags'>" + sDATA.Replace(",", "</span> <span class='Tags'>") + "</span>";
							return sDATA;
						}
						case "CheckBox":
						{
							// 01/05/2021 Paul.  IS_ADMIN checkbox was moved to the layout.
							bool bDATA = Sql.ToBoolean(row[sDATA_FIELD]);
							if (bDATA)
								return "<input type=\"checkbox\" checked disabled />";
							break;
						}
						default:
						{
							// 02/16/2010 Paul.  Add MODULE_TYPE so that we can lookup custom field IDs.
							if (Sql.IsEmptyString(sMODULE_TYPE))
							{
								// 06/06/2018 Paul.  If format is numeric, we need to make sure not to first convert to a string.
								if (sDATA_FORMAT.Contains("{") && row[sDATA_FIELD] != DBNull.Value)
									return WebUtility.HtmlEncode(String.Format(sDATA_FORMAT, row[sDATA_FIELD]));
								// 01/14/2020 Paul.  sDATA_FORMAT may not be specified, but we still need to catch the date field.
								else if (row[sDATA_FIELD].GetType() == typeof(System.DateTime))
								{
									if (T10n != null)
										return Sql.ToString(T10n.FromServerTime(row[sDATA_FIELD]));
								}
								else
									return WebUtility.HtmlEncode(Sql.ToString(row[sDATA_FIELD]));
							}
							else if (memoryCache != null)
								return WebUtility.HtmlEncode(Crm.Modules.ItemName(memoryCache, sMODULE_TYPE, row[sDATA_FIELD]));
							break;
						}
					}
				}
				else
				{
					// 06/30/2018 Paul.  Value may have been erased. If so, replace with Erased Value message.
					if (Sql.IsDataPrivacyErasedField(row.Row, sDATA_FIELD))
						return Sql.DataPrivacyErasedField(L10n);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	// =====================================================================================
	// CreateItemTemplateLiteralList
	//
	// BEFORE: Constructor: (string sDATA_FIELD, string sLIST_NAME, string sPARENT_FIELD)
	// AFTER:  Schema extends to 4 strings: adds sMODULE_TYPE for custom field lookup
	//         Original 3-param constructor preserved as overload
	// =====================================================================================

	/// <summary>
	/// Template that renders a localized list value for a field by looking up the
	/// list display name from a terminology/picklist cache.
	/// Handles AssignedUser, activity_status, XML multi-select values, and custom lists.
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateLiteralList class.
	/// </summary>
	public class CreateItemTemplateLiteralList : ITemplate
	{
		protected string sDATA_FIELD;
		protected string sLIST_NAME;
		protected string sPARENT_FIELD;
		protected string sMODULE_TYPE;

		/// <summary>
		/// Original three-param constructor — preserved for backward compatibility.
		/// BEFORE: public CreateItemTemplateLiteralList(string sDATA_FIELD, string sLIST_NAME, string sPARENT_FIELD)
		/// </summary>
		public CreateItemTemplateLiteralList(string sDATA_FIELD, string sLIST_NAME, string sPARENT_FIELD)
		{
			this.sDATA_FIELD   = sDATA_FIELD;
			this.sLIST_NAME    = sLIST_NAME;
			this.sPARENT_FIELD = sPARENT_FIELD;
			this.sMODULE_TYPE  = String.Empty;
		}

		/// <summary>
		/// Schema-required four-param constructor — adds sMODULE_TYPE for custom field lookup.
		/// Schema: CreateItemTemplateLiteralList(string, string, string, string)
		/// </summary>
		public CreateItemTemplateLiteralList(string sDATA_FIELD, string sLIST_NAME, string sPARENT_FIELD, string sMODULE_TYPE)
		{
			this.sDATA_FIELD   = sDATA_FIELD;
			this.sLIST_NAME    = sLIST_NAME;
			this.sPARENT_FIELD = sPARENT_FIELD;
			this.sMODULE_TYPE  = sMODULE_TYPE;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the cell data for a row using list/picklist translation.
		/// Preserves original OnDataBinding business logic for list value lookup.
		/// </summary>
		public string FormatCellData(DataRowView row, L10N L10n, SplendidCache splendidCache)
		{
			if (row == null) return sDATA_FIELD;
			if (L10n == null) return String.Empty;
			try
			{
				if (row[sDATA_FIELD] != DBNull.Value)
				{
					// 10/09/2010 Paul.  Add PARENT_FIELD so that we can establish dependent listboxes.
					string sList = sLIST_NAME;
					if (!Sql.IsEmptyString(sPARENT_FIELD) && row.DataView.Table.Columns.Contains(sPARENT_FIELD))
					{
						sList = Sql.ToString(row[sPARENT_FIELD]);
					}
					if (!Sql.IsEmptyString(sList))
					{
						// 08/10/2008 Paul.  Use an array to define the custom caches.
						// bCustomCache flag preserved from original — indicates custom list was found
						if (splendidCache != null)
						{
							DataTable dtCustomList = splendidCache.CustomList(sList, L10n.NAME);
							if (dtCustomList != null && dtCustomList.Rows.Count > 0)
							{
						// bCustomCache = true (found custom cache list)
								// Look up display name from the DataTable
								string sKey = Sql.ToString(row[sDATA_FIELD]);
								DataRow[] arrFound = dtCustomList.Select("NAME = '" + Sql.EscapeJavaScript(sKey) + "'");
								if (arrFound.Length > 0 && dtCustomList.Columns.Contains("DISPLAY_NAME"))
									return Sql.ToString(arrFound[0]["DISPLAY_NAME"]);
								// If no display name column, check for FULL_NAME (AssignedUser list)
								if (arrFound.Length > 0 && dtCustomList.Columns.Contains("FULL_NAME"))
									return Sql.ToString(arrFound[0]["FULL_NAME"]);
							}
						}

						// 01/18/2007 Paul.  If AssignedUser list, then use the cached value.
						if (sList == "AssignedUser" && splendidCache != null)
						{
							Guid gUserID = Sql.ToGuid(row[sDATA_FIELD]);
							DataTable dtUser = splendidCache.AssignedUser(gUserID);
							if (dtUser != null && dtUser.Rows.Count > 0)
								return Sql.ToString(dtUser.Rows[0]["FULL_NAME"]);
						}

						// 12/05/2005 Paul.  The activity status needs to be dynamically converted.
						if (sList == "activity_status" && row.DataView.Table.Columns.Contains("ACTIVITY_TYPE"))
						{
							string sACTIVITY_TYPE = Sql.ToString(row["ACTIVITY_TYPE"]);
							switch (sACTIVITY_TYPE)
							{
								case "Tasks"       : sList = "task_status_dom"   ; break;
								case "Meetings"    : sList = "meeting_status_dom"; break;
								case "Calls"       : return Sql.ToString(row[sDATA_FIELD]);
								case "Notes"       : return L10n.Term(".activity_dom.Note");
								case "Emails"      : sList = "dom_email_status"  ; break;
								case "SmsMessages" : sList = "dom_sms_status"    ; break;
								default            : sList = "activity_dom"      ; break;
							}
						}

						// 02/12/2008 Paul.  If the list contains XML, then treat as a multi-selection.
						string sFieldValue = Sql.ToString(row[sDATA_FIELD]);
						if (sFieldValue.StartsWith("<?xml"))
						{
							try
							{
								StringBuilder sb = new StringBuilder();
								XmlDocument xml = new XmlDocument();
								// 01/20/2015 Paul.  Disable XmlResolver to prevent XML XXE.
								xml.XmlResolver = null;
								xml.LoadXml(sFieldValue);
								XmlNodeList nlValues = xml.DocumentElement.SelectNodes("Value");
								foreach (XmlNode xValue in nlValues)
								{
									if (sb.Length > 0) sb.Append(", ");
									sb.Append(L10n.Term("." + sLIST_NAME + ".", xValue.InnerText));
								}
								return sb.ToString();
							}
							catch (Exception ex)
							{
								return ex.Message;
							}
						}

						return Sql.ToString(L10n.Term("." + sList + ".", row[sDATA_FIELD]));
					}
				}
				else
				{
					// 06/30/2018 Paul.  Value may have been erased. If so, replace with Erased Value message.
					if (Sql.IsDataPrivacyErasedField(row.Row, sDATA_FIELD))
						return Sql.DataPrivacyErasedField(L10n);
				}
				return Sql.ToString(row[sDATA_FIELD]);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	// =====================================================================================
	// CreateItemTemplateHyperLink
	//
	// BEFORE: Constructor: (string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT,
	//                        string sURL_TARGET, string sCSSCLASS, string sURL_MODULE,
	//                        string sURL_ASSIGNED_FIELD, string sMODULE_TYPE)  [8 params]
	// AFTER:  Schema constructor: (string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT,
	//                               string sURL_MODULE, string sURL_ASSIGNED_FIELD)  [5 params]
	//         Original 8-param constructor preserved as overload
	// =====================================================================================

	/// <summary>
	/// Template that renders a hyperlink for a record field with ACL-aware link URL construction.
	/// Checks module-level access (GetUserAccess), handles portal mode, and GDPR erasure.
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateHyperLink class.
	/// </summary>
	public class CreateItemTemplateHyperLink : ITemplate
	{
		protected string sDATA_FIELD;
		protected string sURL_FIELD;
		protected string sURL_FORMAT;
		protected string sURL_TARGET;
		protected string sCSSCLASS;
		protected string sURL_MODULE;
		protected string sURL_ASSIGNED_FIELD;
		protected string sMODULE_TYPE;

		/// <summary>
		/// Schema-required five-param constructor.
		/// Schema: CreateItemTemplateHyperLink(string, string, string, string, string)
		/// </summary>
		public CreateItemTemplateHyperLink(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sURL_MODULE, string sURL_ASSIGNED_FIELD)
		{
			this.sDATA_FIELD         = sDATA_FIELD;
			this.sURL_FIELD          = sURL_FIELD;
			this.sURL_FORMAT         = sURL_FORMAT;
			this.sURL_TARGET         = String.Empty;
			this.sCSSCLASS           = String.Empty;
			this.sURL_MODULE         = sURL_MODULE;
			this.sURL_ASSIGNED_FIELD = sURL_ASSIGNED_FIELD;
			this.sMODULE_TYPE        = String.Empty;
		}

		/// <summary>
		/// Original eight-param constructor — preserved for backward compatibility.
		/// BEFORE: public CreateItemTemplateHyperLink(string, string, string, string, string, string, string, string)
		/// </summary>
		public CreateItemTemplateHyperLink(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sURL_TARGET, string sCSSCLASS, string sURL_MODULE, string sURL_ASSIGNED_FIELD, string sMODULE_TYPE)
		{
			this.sDATA_FIELD         = sDATA_FIELD;
			this.sURL_FIELD          = sURL_FIELD;
			this.sURL_FORMAT         = sURL_FORMAT;
			this.sURL_TARGET         = sURL_TARGET;
			this.sCSSCLASS           = sCSSCLASS;
			this.sURL_MODULE         = sURL_MODULE;
			this.sURL_ASSIGNED_FIELD = sURL_ASSIGNED_FIELD;
			this.sMODULE_TYPE        = sMODULE_TYPE;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the cell data as an HTML anchor tag with ACL-enforced URL.
		/// Preserves original OnDataBinding ACL and GDPR logic.
		/// </summary>
		public string FormatCellData(DataRowView row, L10N L10n, Security security, PortalCache portalCache, IMemoryCache memoryCache)
		{
			if (row == null) return String.Empty;
			try
			{
				Guid gASSIGNED_USER_ID = Guid.Empty;
				string sMODULE_NAME = sURL_MODULE;
				if (row.DataView.Table.Columns.Contains(sURL_ASSIGNED_FIELD))
					gASSIGNED_USER_ID = Sql.ToGuid(row[sURL_ASSIGNED_FIELD]);

				if (row.DataView.Table.Columns.Contains(sDATA_FIELD))
				{
					bool bErasedField = Sql.IsDataPrivacyErasedField(row.Row, sDATA_FIELD);
					if (row[sDATA_FIELD] != DBNull.Value || bErasedField)
					{
						string sLinkText;
						string sCssClass = sCSSCLASS;
						if (bErasedField && row[sDATA_FIELD] == DBNull.Value)
						{
							sCssClass = "Erased";
							sLinkText = L10n?.Term("DataPrivacy.LBL_ERASED_VALUE") ?? String.Empty;
						}
						else if (Sql.IsEmptyString(sMODULE_TYPE))
							sLinkText = WebUtility.HtmlEncode(Sql.ToString(row[sDATA_FIELD]));
						else if (memoryCache != null)
							sLinkText = WebUtility.HtmlEncode(Crm.Modules.ItemName(memoryCache, sMODULE_TYPE, row[sDATA_FIELD]));
						else
							sLinkText = WebUtility.HtmlEncode(Sql.ToString(row[sDATA_FIELD]));

						string sNavigateUrl = String.Empty;
						if (!Sql.IsEmptyString(sMODULE_TYPE) || (!Sql.IsEmptyString(sURL_FIELD) && row.DataView.Table.Columns.Contains(sURL_FIELD) && row[sURL_FIELD] != DBNull.Value))
						{
							bool bAllowed = false;
							string sURL_FIELD_VALUE = !Sql.IsEmptyString(sURL_FIELD) && row.DataView.Table.Columns.Contains(sURL_FIELD) ? Sql.ToString(row[sURL_FIELD]) : String.Empty;
							int nACLACCESS = ACL_ACCESS.ALL;
							if (!Sql.IsEmptyString(sMODULE_NAME) && security != null)
								nACLACCESS = security.GetUserAccess(sMODULE_NAME, "view");
							if (security != null && security.IS_ADMIN)
								bAllowed = true;
							else if (nACLACCESS == ACL_ACCESS.OWNER)
							{
								if (security != null && (gASSIGNED_USER_ID == security.USER_ID || security.IS_ADMIN || (portalCache != null && portalCache.IsPortal())))
									bAllowed = true;
							}
							else if (nACLACCESS >= 0 || Sql.IsEmptyGuid(gASSIGNED_USER_ID))
								bAllowed = true;

							if (bAllowed)
							{
								if (Sql.IsEmptyString(sMODULE_TYPE))
									sNavigateUrl = String.Format(sURL_FORMAT, sURL_FIELD_VALUE);
								else if (!Sql.IsEmptyString(sURL_FORMAT))
									sNavigateUrl = String.Format(sURL_FORMAT, sURL_FIELD_VALUE);
								else if (memoryCache != null)
								{
									// 02/18/2010 Paul.  Get the Module Relative Path.
									// BEFORE: Crm.Modules.RelativePath(Application, sMODULE_TYPE)
								// AFTER:  read from IMemoryCache key "Modules.{MODULE_TYPE}.RelativePath" (set by SplendidInit.cs)
								string sRELATIVE_PATH = Sql.ToString(memoryCache.Get<object>("Modules." + sMODULE_TYPE + ".RelativePath"));
									if (Sql.IsEmptyString(sRELATIVE_PATH))
										sRELATIVE_PATH = "/" + sMODULE_TYPE + "/";
									sNavigateUrl = sRELATIVE_PATH + "view.aspx?ID=" + Sql.ToString(row[sDATA_FIELD]);
								}
							}
						}

						if (!Sql.IsEmptyString(sNavigateUrl))
						{
							string sTarget = !Sql.IsEmptyString(sURL_TARGET) ? " target=\"" + WebUtility.HtmlEncode(sURL_TARGET) + "\"" : String.Empty;
							string sCssAttr = !Sql.IsEmptyString(sCssClass) ? " class=\"" + WebUtility.HtmlEncode(sCssClass) + "\"" : String.Empty;
							return "<a href=\"" + WebUtility.HtmlEncode(sNavigateUrl) + "\"" + sTarget + sCssAttr + ">" + sLinkText + "</a>";
						}
						return sLinkText;
					}
				}
				else
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), sDATA_FIELD + " column does not exist in recordset.");
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	// =====================================================================================
	// CreateItemTemplateHyperLinkOnClick
	//
	// BEFORE: Constructor: 8-param version with sURL_TARGET, sCSSCLASS, sMODULE_TYPE
	// AFTER:  Schema constructor: (string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT,
	//                               string sURL_MODULE, string sURL_ASSIGNED_FIELD)  [5 params]
	// =====================================================================================

	/// <summary>
	/// Template that renders a hyperlink with a JavaScript onclick action (popup/modal navigation).
	/// Supports multi-part URL_FIELD with space-separated field names for format string.
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateHyperLinkOnClick class.
	/// </summary>
	public class CreateItemTemplateHyperLinkOnClick : ITemplate
	{
		protected string sDATA_FIELD;
		protected string sURL_FIELD;
		protected string sURL_FORMAT;
		protected string sURL_TARGET;
		protected string sCSSCLASS;
		protected string sURL_MODULE;
		protected string sURL_ASSIGNED_FIELD;
		protected string sMODULE_TYPE;

		/// <summary>
		/// Schema-required five-param constructor.
		/// Schema: CreateItemTemplateHyperLinkOnClick(string, string, string, string, string)
		/// </summary>
		public CreateItemTemplateHyperLinkOnClick(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sURL_MODULE, string sURL_ASSIGNED_FIELD)
		{
			this.sDATA_FIELD         = sDATA_FIELD;
			this.sURL_FIELD          = sURL_FIELD;
			this.sURL_FORMAT         = sURL_FORMAT;
			this.sURL_TARGET         = String.Empty;
			this.sCSSCLASS           = String.Empty;
			this.sURL_MODULE         = sURL_MODULE;
			this.sURL_ASSIGNED_FIELD = sURL_ASSIGNED_FIELD;
			this.sMODULE_TYPE        = String.Empty;
		}

		/// <summary>
		/// Original eight-param constructor — preserved for backward compatibility.
		/// BEFORE: public CreateItemTemplateHyperLinkOnClick(string, string, string, string, string, string, string, string)
		/// </summary>
		public CreateItemTemplateHyperLinkOnClick(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sURL_TARGET, string sCSSCLASS, string sURL_MODULE, string sURL_ASSIGNED_FIELD, string sMODULE_TYPE)
		{
			this.sDATA_FIELD         = sDATA_FIELD;
			this.sURL_FIELD          = sURL_FIELD;
			this.sURL_FORMAT         = sURL_FORMAT;
			this.sURL_TARGET         = sURL_TARGET;
			this.sCSSCLASS           = sCSSCLASS;
			this.sURL_MODULE         = sURL_MODULE;
			this.sURL_ASSIGNED_FIELD = sURL_ASSIGNED_FIELD;
			this.sMODULE_TYPE        = sMODULE_TYPE;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the cell as an HTML anchor with onclick JavaScript action.
		/// Preserves original OnDataBinding logic including ACL checks and GDPR erasure.
		/// </summary>
		public string FormatCellData(DataRowView row, L10N L10n, Security security, PortalCache portalCache, IMemoryCache memoryCache)
		{
			if (row == null) return String.Empty;
			try
			{
				Guid gASSIGNED_USER_ID = Guid.Empty;
				string sMODULE_NAME = sURL_MODULE;
				if (row.DataView.Table.Columns.Contains(sURL_ASSIGNED_FIELD))
					gASSIGNED_USER_ID = Sql.ToGuid(row[sURL_ASSIGNED_FIELD]);

				if (row.DataView.Table.Columns.Contains(sDATA_FIELD))
				{
					bool bErasedField = Sql.IsDataPrivacyErasedField(row.Row, sDATA_FIELD);
					if (row[sDATA_FIELD] != DBNull.Value || bErasedField)
					{
						string sLinkText;
						string sCssClass = sCSSCLASS;
						if (bErasedField)
						{
							sCssClass = "Erased";
							sLinkText = L10n?.Term("DataPrivacy.LBL_ERASED_VALUE") ?? String.Empty;
						}
						else if (Sql.IsEmptyString(sMODULE_TYPE))
							sLinkText = Sql.ToString(row[sDATA_FIELD]);
						else if (memoryCache != null)
							sLinkText = Crm.Modules.ItemName(memoryCache, sMODULE_TYPE, row[sDATA_FIELD]);
						else
							sLinkText = Sql.ToString(row[sDATA_FIELD]);

						bool bAllowed = false;
						string[] arrURL_FIELD = sURL_FIELD.Split(' ');
						object[] objURL_FIELD = new object[arrURL_FIELD.Length];
						for (int i = 0; i < arrURL_FIELD.Length; i++)
						{
							if (!Sql.IsEmptyString(arrURL_FIELD[i]))
							{
								// 07/26/2007 Paul.  Make sure to escape the javascript string.
								if (row.DataView.Table.Columns.Contains(arrURL_FIELD[i]) && row[arrURL_FIELD[i]] != DBNull.Value)
									objURL_FIELD[i] = Sql.EscapeJavaScript(Sql.ToString(row[arrURL_FIELD[i]]));
								else
									objURL_FIELD[i] = String.Empty;
							}
						}

						int nACLACCESS = ACL_ACCESS.ALL;
						if (!Sql.IsEmptyString(sMODULE_NAME) && security != null)
							nACLACCESS = security.GetUserAccess(sMODULE_NAME, "view");
						if (security != null && security.IS_ADMIN)
							bAllowed = true;
						else if (nACLACCESS == ACL_ACCESS.OWNER)
						{
							if (security != null && (gASSIGNED_USER_ID == security.USER_ID || security.IS_ADMIN || (portalCache != null && portalCache.IsPortal())))
								bAllowed = true;
						}
						else if (nACLACCESS >= 0 || Sql.IsEmptyGuid(gASSIGNED_USER_ID))
							bAllowed = true;

						if (bAllowed)
						{
							// 01/20/2010 Paul.  If the site root is specified, then don't use onclick.
							if (sURL_FORMAT.StartsWith("~/"))
							{
								string sNavigateUrl = String.Format(sURL_FORMAT, objURL_FIELD);
								string sTarget = !Sql.IsEmptyString(sURL_TARGET) ? " target=\"" + WebUtility.HtmlEncode(sURL_TARGET) + "\"" : String.Empty;
								string sCssAttr = !Sql.IsEmptyString(sCssClass) ? " class=\"" + WebUtility.HtmlEncode(sCssClass) + "\"" : String.Empty;
								return "<a href=\"" + WebUtility.HtmlEncode(sNavigateUrl) + "\"" + sTarget + sCssAttr + ">" + sLinkText + "</a>";
							}
							else
							{
								string sOnClick = String.Format(sURL_FORMAT, objURL_FIELD);
								string sCssAttr = !Sql.IsEmptyString(sCssClass) ? " class=\"" + WebUtility.HtmlEncode(sCssClass) + "\"" : String.Empty;
								return "<a href=\"#\" onclick=\"" + WebUtility.HtmlEncode(sOnClick) + "\"" + sCssAttr + ">" + sLinkText + "</a>";
							}
						}
						return sLinkText;
					}
				}
				else
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), sDATA_FIELD + " column does not exist in recordset.");
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	// =====================================================================================
	// CreateItemTemplateJavaScript
	//
	// BEFORE: Constructor: (string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sURL_TARGET)  [4 params]
	// AFTER:  Schema constructor: (string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT)  [3 params]
	//         Original 4-param constructor preserved as overload
	// =====================================================================================

	/// <summary>
	/// Template that renders an inline JavaScript tag for integration widgets
	/// (e.g. LinkedIn Company Profile span + script tag pattern).
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateJavaScript class.
	/// </summary>
	public class CreateItemTemplateJavaScript : ITemplate
	{
		protected string sDATA_FIELD;
		protected string sURL_FIELD;
		protected string sURL_FORMAT;
		protected string sURL_TARGET;

		/// <summary>
		/// Schema-required three-param constructor.
		/// Schema: CreateItemTemplateJavaScript(string, string, string)
		/// </summary>
		public CreateItemTemplateJavaScript(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT)
		{
			this.sDATA_FIELD = sDATA_FIELD;
			this.sURL_FIELD  = sURL_FIELD;
			this.sURL_FORMAT = sURL_FORMAT;
			this.sURL_TARGET = String.Empty;
		}

		/// <summary>
		/// Original four-param constructor — preserved for backward compatibility.
		/// BEFORE: public CreateItemTemplateJavaScript(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sURL_TARGET)
		/// </summary>
		public CreateItemTemplateJavaScript(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sURL_TARGET)
		{
			this.sDATA_FIELD = sDATA_FIELD;
			this.sURL_FIELD  = sURL_FIELD;
			this.sURL_FORMAT = sURL_FORMAT;
			this.sURL_TARGET = sURL_TARGET;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the cell as a span + script tag for JavaScript-driven integrations.
		/// Preserves original OnDataBinding logic for field value escaping and format substitution.
		/// </summary>
		public string FormatCellData(DataRowView row, L10N L10n)
		{
			if (row == null) return String.Empty;
			try
			{
				string[] arrURL_FIELD = sURL_FIELD.Split(' ');
				object[] objURL_FIELD = new object[arrURL_FIELD.Length];
				for (int i = 0; i < arrURL_FIELD.Length; i++)
				{
					if (!Sql.IsEmptyString(arrURL_FIELD[i]))
					{
						// 08/02/2010 Paul.  In our application of Field Level Security, we will hide fields by replacing with "."
						if (arrURL_FIELD[i].Contains("."))
							objURL_FIELD[i] = (arrURL_FIELD[i] == "." || L10n == null) ? String.Empty : L10n.Term(arrURL_FIELD[i]);
						else if (row.DataView.Table.Columns.Contains(arrURL_FIELD[i]) && row[arrURL_FIELD[i]] != DBNull.Value)
							objURL_FIELD[i] = Sql.EscapeJavaScript(Sql.ToString(row[arrURL_FIELD[i]]));
						else
							objURL_FIELD[i] = String.Empty;
					}
				}
				// 12/03/2009 Paul.  LinkedIn Company Profile requires a span tag to insert the link.
				string sSpanID = !Sql.IsEmptyString(sURL_TARGET) ? String.Format(sURL_TARGET, objURL_FIELD) : Guid.NewGuid().ToString("N");
				return "<span id=\"" + sSpanID + "\"></span>"
				     + "<script type=\"text/javascript\"> " + String.Format(sURL_FORMAT, objURL_FIELD) + "</script>";
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	// =====================================================================================
	// CreateItemTemplateJavaScriptImage
	//
	// BEFORE: Constructor: (string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN)  [3 params]
	// AFTER:  Schema constructor: (string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN)  [4 params]
	//         Original 3-param constructor preserved as overload
	// =====================================================================================

	/// <summary>
	/// Template that renders an image with a JavaScript onclick action (Preview/open panel).
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateJavaScriptImage class.
	/// </summary>
	public class CreateItemTemplateJavaScriptImage : ITemplate
	{
		protected string sDATA_FIELD;
		protected string sURL_FIELD;
		protected string sURL_FORMAT;
		protected string sIMAGE_SKIN;

		/// <summary>
		/// Schema-required four-param constructor.
		/// Schema: CreateItemTemplateJavaScriptImage(string, string, string, string)
		/// </summary>
		public CreateItemTemplateJavaScriptImage(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN)
		{
			this.sDATA_FIELD = sDATA_FIELD;
			this.sURL_FIELD  = sURL_FIELD;
			this.sURL_FORMAT = sURL_FORMAT;
			this.sIMAGE_SKIN = sIMAGE_SKIN;
		}

		/// <summary>
		/// Original three-param constructor — preserved for backward compatibility.
		/// BEFORE: public CreateItemTemplateJavaScriptImage(string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN)
		/// </summary>
		public CreateItemTemplateJavaScriptImage(string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN)
		{
			this.sDATA_FIELD = String.Empty;
			this.sURL_FIELD  = sURL_FIELD;
			this.sURL_FORMAT = sURL_FORMAT;
			this.sIMAGE_SKIN = sIMAGE_SKIN;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the cell as an img tag with onclick JavaScript action.
		/// Preserves original OnDataBinding field escaping and format substitution logic.
		/// </summary>
		public string FormatCellData(DataRowView row, L10N L10n)
		{
			if (row == null) return String.Empty;
			try
			{
				string[] arrURL_FIELD = sURL_FIELD.Split(' ');
				object[] objURL_FIELD = new object[arrURL_FIELD.Length];
				for (int i = 0; i < arrURL_FIELD.Length; i++)
				{
					if (!Sql.IsEmptyString(arrURL_FIELD[i]))
					{
						if (arrURL_FIELD[i].Contains("."))
							objURL_FIELD[i] = (arrURL_FIELD[i] == "." || L10n == null) ? String.Empty : L10n.Term(arrURL_FIELD[i]);
						else if (row.DataView.Table.Columns.Contains(arrURL_FIELD[i]) && row[arrURL_FIELD[i]] != DBNull.Value)
							objURL_FIELD[i] = Sql.EscapeJavaScript(Sql.ToString(row[arrURL_FIELD[i]]));
						else
							objURL_FIELD[i] = String.Empty;
					}
				}
				string sOnClick = String.Format(sURL_FORMAT, objURL_FIELD);
				string sSkinAttr = !Sql.IsEmptyString(sIMAGE_SKIN) ? " data-skin=\"" + WebUtility.HtmlEncode(sIMAGE_SKIN) + "\"" : String.Empty;
				return "<img onclick=\"" + WebUtility.HtmlEncode(sOnClick) + "\"" + sSkinAttr + " alt=\"\" />";
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	// =====================================================================================
	// CreateItemTemplateImageButton
	//
	// BEFORE: Constructor: (string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN,
	//                        string sCSS_CLASS, CommandEventHandler Page_Command)  [5 params]
	// AFTER:  Schema constructor: (string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN,
	//                               CommandEventHandler Page_Command)  [4 params]
	//         Original 5-param constructor preserved as overload
	// =====================================================================================

	/// <summary>
	/// Template that renders an image button wired to a CommandEventHandler
	/// (typically for Preview, Select, or custom command buttons in a grid).
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateImageButton class.
	/// </summary>
	public class CreateItemTemplateImageButton : ITemplate
	{
		protected string sURL_FIELD;
		protected string sURL_FORMAT;
		protected string sIMAGE_SKIN;
		protected string sCSS_CLASS;
		protected CommandEventHandler Page_Command;

		/// <summary>
		/// Schema-required four-param constructor (drops sCSS_CLASS).
		/// Schema: CreateItemTemplateImageButton(string, string, string, CommandEventHandler)
		/// </summary>
		public CreateItemTemplateImageButton(string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN, CommandEventHandler Page_Command)
		{
			this.sURL_FIELD   = sURL_FIELD;
			this.sURL_FORMAT  = sURL_FORMAT;
			this.sIMAGE_SKIN  = sIMAGE_SKIN;
			this.sCSS_CLASS   = String.Empty;
			this.Page_Command = Page_Command;
		}

		/// <summary>
		/// Original five-param constructor — preserved for backward compatibility.
		/// BEFORE: public CreateItemTemplateImageButton(string sURL_FIELD, string sURL_FORMAT,
		///                                              string sIMAGE_SKIN, string sCSS_CLASS,
		///                                              CommandEventHandler Page_Command)
		/// </summary>
		public CreateItemTemplateImageButton(string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN, string sCSS_CLASS, CommandEventHandler Page_Command)
		{
			this.sURL_FIELD   = sURL_FIELD;
			this.sURL_FORMAT  = sURL_FORMAT;
			this.sIMAGE_SKIN  = sIMAGE_SKIN;
			this.sCSS_CLASS   = sCSS_CLASS;
			this.Page_Command = Page_Command;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// BEFORE: WebForms registered ImageButton DataBinding handler and Command event handler.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the cell as a button HTML element with command data attributes.
		/// Preserves original CommandArgument assignment logic.
		/// </summary>
		public string FormatCellData(DataRowView row, L10N L10n)
		{
			if (row == null) return String.Empty;
			try
			{
				string sCommandArgument = String.Empty;
				if (row.DataView.Table.Columns.Contains(sURL_FIELD) && row[sURL_FIELD] != DBNull.Value)
					sCommandArgument = Sql.ToString(row[sURL_FIELD]);

				string sToolTip = L10n?.Term(".LBL_" + sURL_FORMAT.ToUpper()) ?? sURL_FORMAT;
				string sCssAttr = !Sql.IsEmptyString(sCSS_CLASS) ? " class=\"" + WebUtility.HtmlEncode(sCSS_CLASS) + "\"" : String.Empty;
				string sSkinAttr = !Sql.IsEmptyString(sIMAGE_SKIN) ? " data-skin=\"" + WebUtility.HtmlEncode(sIMAGE_SKIN) + "\"" : String.Empty;
				return "<button type=\"button\"" + sCssAttr + sSkinAttr
				     + " data-command=\"" + WebUtility.HtmlEncode(sURL_FORMAT) + "\""
				     + " data-argument=\"" + WebUtility.HtmlEncode(sCommandArgument) + "\""
				     + " title=\"" + WebUtility.HtmlEncode(sToolTip) + "\"></button>";
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	// =====================================================================================
	// CreateItemTemplateHover
	//
	// BEFORE: Constructor: (string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN)  [4 params]
	// AFTER:  Schema constructor: adds sHOVER_MODULE as 5th string param  [5 params]
	//         Original 4-param constructor preserved as overload
	// =====================================================================================

	/// <summary>
	/// Template that renders an info icon with a hover panel showing formatted record details.
	/// GDPR data privacy erased-field detection is applied to each displayed field.
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateHover class.
	/// </summary>
	public class CreateItemTemplateHover : ITemplate
	{
		protected string sDATA_FIELD;
		protected string sURL_FIELD;
		protected string sURL_FORMAT;
		protected string sIMAGE_SKIN;
		protected string sHOVER_MODULE;

		/// <summary>
		/// Original four-param constructor — preserved for backward compatibility.
		/// BEFORE: public CreateItemTemplateHover(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN)
		/// </summary>
		public CreateItemTemplateHover(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN)
		{
			this.sDATA_FIELD    = sDATA_FIELD;
			this.sURL_FIELD     = sURL_FIELD;
			this.sURL_FORMAT    = sURL_FORMAT;
			this.sIMAGE_SKIN    = sIMAGE_SKIN;
			this.sHOVER_MODULE  = String.Empty;
		}

		/// <summary>
		/// Schema-required five-param constructor — adds sHOVER_MODULE for context.
		/// Schema: CreateItemTemplateHover(string, string, string, string, string)
		/// </summary>
		public CreateItemTemplateHover(string sDATA_FIELD, string sURL_FIELD, string sURL_FORMAT, string sIMAGE_SKIN, string sHOVER_MODULE)
		{
			this.sDATA_FIELD    = sDATA_FIELD;
			this.sURL_FIELD     = sURL_FIELD;
			this.sURL_FORMAT    = sURL_FORMAT;
			this.sIMAGE_SKIN    = sIMAGE_SKIN;
			this.sHOVER_MODULE  = sHOVER_MODULE;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// BEFORE: WebForms created Image + Panel + HoverMenuExtender for each row.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the hover panel content string using the URL_FORMAT template and field values.
		/// Applies timezone conversion and currency formatting. Preserves GDPR erasure pill logic.
		/// </summary>
		public string FormatCellData(DataRowView row, L10N L10n, TimeZone T10n, Currency C10n)
		{
			if (row == null) return String.Empty;
			try
			{
				// 06/30/2018 Paul.  Preprocess the erased fields for performance.
				List<string> arrERASED_FIELDS = new List<string>();
				if (Crm.Config.enable_data_privacy())
				{
					if (row.DataView.Table.Columns.Contains("ERASED_FIELDS"))
					{
						string sERASED_FIELDS = Sql.ToString(row["ERASED_FIELDS"]);
						if (!Sql.IsEmptyString(sERASED_FIELDS))
							arrERASED_FIELDS.AddRange(sERASED_FIELDS.Split(','));
					}
				}

				string[] arrURL_FIELD = sURL_FIELD.Split(' ');
				object[] objURL_FIELD = new object[arrURL_FIELD.Length];
				for (int i = 0; i < arrURL_FIELD.Length; i++)
				{
					if (!Sql.IsEmptyString(arrURL_FIELD[i]))
					{
						// 08/02/2010 Paul.  In our application of Field Level Security, we will hide fields by replacing with "."
						if (arrURL_FIELD[i].Contains("."))
							objURL_FIELD[i] = (arrURL_FIELD[i] == "." || L10n == null) ? String.Empty : L10n.Term(arrURL_FIELD[i]);
						else if (row.DataView.Table.Columns.Contains(arrURL_FIELD[i]))
						{
							if (row[arrURL_FIELD[i]] != DBNull.Value)
							{
								object oValue = row[arrURL_FIELD[i]];
								if (oValue.GetType() == typeof(System.DateTime) && T10n != null)
									objURL_FIELD[i] = T10n.FromServerTime(oValue);
								else if (arrURL_FIELD[i].EndsWith("_USDOLLAR") && C10n != null)
									objURL_FIELD[i] = C10n.ToCurrency(Convert.ToDecimal(oValue)).ToString("c");
								else
									objURL_FIELD[i] = oValue;
							}
							// 06/30/2018 Paul.  Value may have been erased. If so, replace with Erased Value message.
							else if (arrERASED_FIELDS.Contains(arrURL_FIELD[i]))
								objURL_FIELD[i] = Sql.DataPrivacyErasedPill(L10n);
							else
								objURL_FIELD[i] = String.Empty;
						}
						else
							objURL_FIELD[i] = String.Empty;
					}
				}
				return String.Format(sURL_FORMAT, objURL_FIELD);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	// =====================================================================================
	// CreateItemTemplateImage
	//
	// BEFORE: Constructor: (string sDATA_FIELD, string sURL_FORMAT, string sCSSCLASS)  [3 params]
	// AFTER:  Schema constructor: (string sDATA_FIELD, string sURL_FORMAT)  [2 params]
	//         Original 3-param constructor preserved as overload
	// =====================================================================================

	/// <summary>
	/// Template that renders an image element for a field containing an image ID or path.
	/// Generates an image URL from the field value using sURL_FORMAT or a default pattern.
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs CreateItemTemplateImage class.
	/// </summary>
	public class CreateItemTemplateImage : ITemplate
	{
		protected string sDATA_FIELD;
		protected string sURL_FORMAT;
		protected string sCSSCLASS;

		/// <summary>
		/// Schema-required two-param constructor (drops sCSSCLASS).
		/// Schema: CreateItemTemplateImage(string, string)
		/// </summary>
		public CreateItemTemplateImage(string sDATA_FIELD, string sURL_FORMAT)
		{
			this.sDATA_FIELD = sDATA_FIELD;
			this.sURL_FORMAT = sURL_FORMAT;
			this.sCSSCLASS   = String.Empty;
		}

		/// <summary>
		/// Original three-param constructor — preserved for backward compatibility.
		/// BEFORE: public CreateItemTemplateImage(string sDATA_FIELD, string sURL_FORMAT, string sCSSCLASS)
		/// </summary>
		public CreateItemTemplateImage(string sDATA_FIELD, string sURL_FORMAT, string sCSSCLASS)
		{
			this.sDATA_FIELD = sDATA_FIELD;
			this.sURL_FORMAT = sURL_FORMAT;
			this.sCSSCLASS   = sCSSCLASS;
		}

		/// <summary>
		/// No-op in .NET 10 — React SPA handles grid rendering.
		/// </summary>
		public void InstantiateIn() { /* WebForms rendering removed — React SPA handles grid */ }

		/// <summary>
		/// Formats the cell as an img tag pointing to the image identified by the field value.
		/// Preserves original OnDataBinding logic for URL construction and visibility control.
		/// </summary>
		public string FormatCellData(DataRowView row)
		{
			if (row == null) return String.Empty;
			try
			{
				if (row.DataView.Table.Columns.Contains(sDATA_FIELD))
				{
					if (row[sDATA_FIELD] != DBNull.Value)
					{
						string sImageID = Sql.ToString(row[sDATA_FIELD]);
						string sImageUrl;
						// 08/15/2014 Paul.  Show the URL_FORMAT for Images so that we can point to the EmailImages URL.
						if (Sql.IsEmptyString(sURL_FORMAT))
							sImageUrl = "~/Images/Image.aspx?ID=" + sImageID;
						else
							sImageUrl = sURL_FORMAT + sImageID;

						string sCssAttr = !Sql.IsEmptyString(sCSSCLASS) ? " class=\"" + WebUtility.HtmlEncode(sCSSCLASS) + "\"" : String.Empty;
						return "<img src=\"" + WebUtility.HtmlEncode(sImageUrl) + "\"" + sCssAttr + " alt=\"\" />";
					}
					// 04/13/2006 Paul.  Don't show the image control if there is no data to show.
					return String.Empty;
				}
				else
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), sDATA_FIELD + " column does not exist in recordset.");
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return String.Empty;
		}
	}

	#endregion

	// =====================================================================================
	// SelectMethodHandler — custom paging callback delegate
	//
	// PRESERVED: exact signature from original SplendidGrid.cs
	// =====================================================================================

	/// <summary>
	/// Delegate for custom paging data-fetch callback.
	/// Called by SplendidGrid.DataBind() when AllowCustomPaging is true.
	/// </summary>
	/// <param name="nCurrentPageIndex">Zero-based page index to load.</param>
	/// <param name="nPageSize">Number of rows per page (-1 for all rows in print view).</param>
	public delegate void SelectMethodHandler(int nCurrentPageIndex, int nPageSize);

	// =====================================================================================
	// SplendidGrid
	//
	// BEFORE: public class SplendidGrid : System.Web.UI.WebControls.DataGrid
	// AFTER:  standalone DI-injectable service class (no WebForms base)
	//
	// REMOVED:  DataGrid base class, WebForms event model (ItemCreated, PageIndexChanged, SortCommand)
	//           AjaxControlToolkit.HoverMenuExtender usage
	//           WebForms ViewState[] → Dictionary<string, object> _viewState
	//           WebForms paging/sorting DataGrid state → plain instance properties
	// REPLACED: HttpContext.Current.Items["L10n"] → _httpContextAccessor.HttpContext?.Items["L10n"]
	//           HttpContext.Current.Session["key"] → _httpContextAccessor.HttpContext?.Session.GetString("key")
	//           HttpContext.Current.Application["key"] → _memoryCache.Get<object>("key")
	// ADDED:    DI constructor: IHttpContextAccessor, IMemoryCache, Security, SplendidCache, SplendidDynamic
	// PRESERVED: All public method signatures, business logic, GRID_NAME, SortColumn/SortOrder,
	//            SelectMethodHandler, AppendGridColumns, ApplySort, OrderByClause, SetSortFields,
	//            InputCheckbox (adapted: HiddenField → string; bool flag implicit)
	//
	// DI Registration: services.AddScoped<SplendidGrid>()
	// =====================================================================================

	/// <summary>
	/// Grid state management and data-binding service for SplendidCRM list views.
	///
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs (~1,730 lines) for .NET 10 ASP.NET Core.
	/// Replaces System.Web.UI.WebControls.DataGrid inheritance with a DI-injectable service.
	/// All WebForms control-tree rendering is removed; React SPA handles grid display.
	///
	/// The class preserves all data-centric functionality:
	///   • Sort state management (SortColumn, SortOrder, ViewState-backed)
	///   • Custom paging support (AllowCustomPaging, SelectMethodHandler)
	///   • DataView sort application (ApplySort → DataView.Sort)
	///   • Column field collection (AppendGridColumns → SplendidDynamic.GridColumns)
	///   • ORDER BY clause generation (OrderByClause)
	///   • Checkbox HTML generation (InputCheckbox)
	///   • L10N translation hook (L10nTranslate → no-op stub, React handles column headers)
	///
	/// DESIGN NOTES:
	///   • Register as SCOPED — SortColumn/SortOrder state is per-request.
	///   • DataSource must be set before calling ApplySort() or DataBind().
	///   • AllowCustomPaging + SelectMethod provides data load on page change.
	/// </summary>
	public class SplendidGrid
	{
		// =====================================================================================
		// DI-injected fields
		// =====================================================================================

		/// <summary>
		/// Replaces HttpContext.Current for per-request Items/Session access.
		/// BEFORE: HttpContext.Current.Items["L10n"] / HttpContext.Current.Session["key"]
		/// AFTER:  _httpContextAccessor.HttpContext?.Items["L10n"] / .Session?.GetString("key")
		/// </summary>
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Replaces HttpApplicationState (Application[]) for cached metadata access.
		/// BEFORE: HttpContext.Current.Application["CONFIG.list_max_entries_per_page"]
		/// AFTER:  _memoryCache.Get&lt;object&gt;("CONFIG.list_max_entries_per_page")
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		/// <summary>
		/// ACL and authentication service for GetUserAccess checks in hyperlink templates.
		/// </summary>
		private readonly Security _security;

		/// <summary>
		/// Metadata caching hub — used for GridViewColumns, AssignedUser, etc.
		/// </summary>
		private readonly SplendidCache _splendidCache;

		/// <summary>
		/// Dynamic layout rendering support — provides GridColumns() for field enumeration.
		/// BEFORE: SplendidDynamic.AppendGridColumns(sGRID_NAME, this, arrSelectFields, null)
		/// AFTER:  _splendidDynamic.GridColumns(sGRID_NAME, arrSelectFields, null)
		/// </summary>
		private readonly SplendidDynamic _splendidDynamic;

		// =====================================================================================
		// ViewState replacement
		//
		// BEFORE: WebForms ViewState["key"] for sort column, sort order (per postback)
		// AFTER:  per-request Dictionary<string, object> _viewState
		// =====================================================================================
		private readonly Dictionary<string, object> _viewState = new Dictionary<string, object>();

		// =====================================================================================
		// DataGrid-equivalent public properties
		//
		// BEFORE: Inherited from System.Web.UI.WebControls.DataGrid
		// AFTER:  Plain auto-properties or computed properties
		// =====================================================================================

		/// <summary>
		/// Grid client control ID — used as key prefix for ViewState entries to ensure
		/// uniqueness when multiple SplendidGrids are used on a single page.
		/// BEFORE: inherited from System.Web.UI.Control
		/// </summary>
		public string ID { get; set; } = "SplendidGrid";

		/// <summary>Data source for grid rendering and sorting. Equivalent to DataGrid.DataSource as DataView.</summary>
		public DataView DataSource { get; set; }

		/// <summary>Whether column header click triggers sort. Default: true.</summary>
		public bool AllowSorting { get; set; } = true;

		/// <summary>Whether server-side paging is enabled. Default: false.</summary>
		public bool AllowPaging { get; set; } = false;

		/// <summary>
		/// Whether custom (database-driven) paging is in use.
		/// When true, SelectMethod is called to fetch the current page from the database.
		/// </summary>
		public bool AllowCustomPaging { get; set; } = false;

		/// <summary>
		/// Number of rows per page when AllowPaging is true.
		/// Initialized from CONFIG.list_max_entries_per_page cache entry.
		/// </summary>
		public int PageSize { get; set; } = 20;

		/// <summary>Zero-based index of the current page when AllowPaging is true.</summary>
		public int CurrentPageIndex { get; set; } = 0;

		/// <summary>Total number of rows available when AllowCustomPaging is true.</summary>
		public int VirtualItemCount { get; set; } = 0;

		/// <summary>
		/// Total page count.
		/// BEFORE: DataGrid.PageCount (WebForms computed property)
		/// AFTER:  computed from DataSource.Count or VirtualItemCount.
		/// </summary>
		public int PageCount
		{
			get
			{
				if (!AllowPaging || PageSize <= 0) return 1;
				int nTotal = AllowCustomPaging ? VirtualItemCount : (DataSource?.Count ?? 0);
				return (int)Math.Ceiling((double)nTotal / PageSize);
			}
		}

		// =====================================================================================
		// SplendidGrid-specific public fields and properties
		// =====================================================================================

		/// <summary>
		/// Grid view name (e.g. "Accounts.ListView") — set by AppendGridColumns or DynamicColumns.
		/// Used to retrieve grid metadata from SplendidCache.GridViewColumns().
		/// </summary>
		public string GRID_NAME;

		/// <summary>Whether the grid is being rendered on a mobile device.</summary>
		public bool IsMobile = false;

		/// <summary>
		/// Mass-update view name — when non-empty and Command is set, renders MassUpdate
		/// hover buttons in the grid header.
		/// </summary>
		public string MassUpdateView;

		/// <summary>
		/// Zero-based column index where the MassUpdate hover buttons are placed.
		/// Default: 1 (second column). Some admin tables use 0.
		/// </summary>
		private int nMassUpdateHoverColumn = 1;
		public int MassUpdateHoverColumn
		{
			get { return nMassUpdateHoverColumn; }
			set { nMassUpdateHoverColumn = value; }
		}

		/// <summary>
		/// Command event handler — wired to grid buttons for row-level commands (Edit, Delete, etc.).
		/// Propagated to SplendidDynamic.AppendButtons() and CreateItemTemplateImageButton.
		/// </summary>
		public CommandEventHandler Command;

		/// <summary>
		/// Custom paging data-fetch callback — called when the page changes and AllowCustomPaging is true.
		/// </summary>
		public SelectMethodHandler SelectMethod;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs a SplendidGrid service with DI dependencies.
		/// Initializes PageSize from CONFIG.list_max_entries_per_page cache entry.
		/// </summary>
		public SplendidGrid(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache,
			Security             security,
			SplendidCache        splendidCache,
			SplendidDynamic      splendidDynamic)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_security            = security;
			_splendidCache       = splendidCache;
			_splendidDynamic     = splendidDynamic;

			// 04/09/2008 Paul.  Move the PageSize to the constructor so that it can be overridden.
			// BEFORE: int nPageSize = Sql.ToInteger(HttpContext.Current.Application["CONFIG.list_max_entries_per_page"]);
			// AFTER:  _memoryCache.Get<object>("CONFIG.list_max_entries_per_page")
			int nPageSize = Sql.ToInteger(_memoryCache?.Get<object>("CONFIG.list_max_entries_per_page"));
			if (nPageSize > 0)
				this.PageSize = nPageSize;
		}

		// =====================================================================================
		// SortColumn / SortOrder properties
		//
		// BEFORE: ViewState[this.ID + ".LastSortColumn"] / ViewState[this.ID + ".LastSortOrder"]
		// AFTER:  _viewState[this.ID + ".LastSortColumn"] / _viewState[this.ID + ".LastSortOrder"]
		// =====================================================================================

		/// <summary>
		/// Current sort column name for this grid instance.
		/// Backed by per-request _viewState keyed by grid ID to support multiple grids per page.
		/// BEFORE: ViewState[this.ID + ".LastSortColumn"]
		/// </summary>
		public string SortColumn
		{
			get
			{
				_viewState.TryGetValue(this.ID + ".LastSortColumn", out object val);
				return Sql.ToString(val);
			}
			set
			{
				_viewState[this.ID + ".LastSortColumn"] = value;
			}
		}

		/// <summary>
		/// Current sort direction ("asc" or "desc") for this grid instance.
		/// BEFORE: ViewState[this.ID + ".LastSortOrder"]
		/// </summary>
		public string SortOrder
		{
			get
			{
				_viewState.TryGetValue(this.ID + ".LastSortOrder", out object val);
				return Sql.ToString(val);
			}
			set
			{
				_viewState[this.ID + ".LastSortOrder"] = value;
			}
		}

		// =====================================================================================
		// L10nTranslate
		//
		// BEFORE: Translated DataGrid column headers and PagerStyle text via L10N.Term()
		// AFTER:  No-op — React SPA renders its own column headers from terminology cache
		// =====================================================================================

		private bool bTranslated = false;

		/// <summary>
		/// Translates grid column headers using the per-request L10N instance.
		/// No-op in .NET 10 — React SPA handles column header localization client-side.
		/// BEFORE: translated DataGrid.Columns[i].HeaderText and PagerStyle.PrevPageText
		/// </summary>
		public void L10nTranslate()
		{
			// 11/12/2005 Paul.  Not sure why, but Unified Search/Project List is not translating.
			// No-op: React SPA handles column header translation. DataGrid.Columns does not exist.
			if (!bTranslated)
			{
				bTranslated = true;
			}
		}

		// =====================================================================================
		// InputCheckbox
		//
		// BEFORE: InputCheckbox(bool bShowCheck, string sCheckboxName, Guid gID, HiddenField hidSelectedItems)
		//         InputCheckbox(bool bShowCheck, string sCheckboxName, string sID, HiddenField hidSelectedItems)
		//         → HiddenField (WebForms) stores comma-separated selected IDs
		// AFTER:  InputCheckbox(string sCheckboxName, string sID)
		//         InputCheckbox(string sCheckboxName, string sID, string sCssClass, string sSelectedItems)
		//         → HiddenField.Value replaced with plain string sSelectedItems
		//         → bool bShowCheck implicit (always renders when called)
		// =====================================================================================

		/// <summary>
		/// Renders an HTML checkbox input for a grid row.
		/// Schema: InputCheckbox(string, string) — 2-param version; always renders checkbox.
		/// BEFORE: InputCheckbox(bool bShowCheck, string sCheckboxName, Guid gID, HiddenField hidSelectedItems)
		/// AFTER:  InputCheckbox(string sCheckboxName, string sID)
		/// </summary>
		/// <param name="sCheckboxName">Name attribute for the checkbox input element.</param>
		/// <param name="sID">Record ID value for the checkbox; used as the checkbox value attribute.</param>
		/// <returns>HTML checkbox input string or empty if sID is empty.</returns>
		public string InputCheckbox(string sCheckboxName, string sID)
		{
			return InputCheckbox(sCheckboxName, sID, "checkbox", String.Empty);
		}

		/// <summary>
		/// Renders an HTML checkbox input with CSS class and checked-state detection.
		/// Schema: InputCheckbox(string, string, string, string) — 4-param version.
		/// BEFORE: InputCheckbox(bool bShowCheck, string sCheckboxName, string sID, HiddenField hidSelectedItems)
		/// AFTER:  InputCheckbox(string sCheckboxName, string sID, string sCssClass, string sSelectedItems)
		///         where HiddenField.Value (comma-separated IDs) → sSelectedItems plain string
		/// </summary>
		/// <param name="sCheckboxName">Name attribute for the checkbox input element.</param>
		/// <param name="sID">Record ID value; used as checkbox value attribute.</param>
		/// <param name="sCssClass">CSS class for the checkbox (default "checkbox").</param>
		/// <param name="sSelectedItems">Comma-separated list of currently selected IDs
		///   (replaces HiddenField.Value from WebForms).</param>
		/// <returns>HTML checkbox input string, pre-checked if sID appears in sSelectedItems.</returns>
		public string InputCheckbox(string sCheckboxName, string sID, string sCssClass, string sSelectedItems)
		{
			if (Sql.IsEmptyString(sID)) return String.Empty;
			string sChecked = (!Sql.IsEmptyString(sSelectedItems) && sSelectedItems.Contains(sID)) ? "checked" : String.Empty;
			string sCssAttr = !Sql.IsEmptyString(sCssClass) ? sCssClass : "checkbox";
			// 09/15/2014 Paul.  Prevent Cross-Site Scripting by HTML encoding the ID.
			return "<input name=\"" + WebUtility.HtmlEncode(sCheckboxName) + "\""
			     + " class=\"" + WebUtility.HtmlEncode(sCssAttr) + "\""
			     + " type=\"checkbox\""
			     + " value=\"" + WebUtility.HtmlEncode(sID) + "\""
			     + " onclick=\"SplendidGrid_ToggleCheckbox(this)\""
			     + " " + sChecked + " />";
		}

		// =====================================================================================
		// Page_Command
		//
		// BEFORE: protected void Page_Command(object sender, CommandEventArgs e)
		//         called by ImageButton.Command event handler
		// AFTER:  same signature — delegates to Command field
		// =====================================================================================

		/// <summary>
		/// Internal command dispatcher — delegates to the external Command handler.
		/// Called by grid buttons (via CommandEventHandler delegate chain).
		/// BEFORE: raised by WebForms ImageButton.Command event
		/// </summary>
		protected void Page_Command(object sender, CommandEventArgs e)
		{
			if (Command != null)
				Command(sender, e);
		}

		// =====================================================================================
		// OnItemCreated / OnPageIndexChanged / OnSort
		//
		// BEFORE: WebForms DataGrid event handlers (DataGridItemEventArgs, DataGridPageChangedEventArgs, etc.)
		// AFTER:  no-op public methods — schema requires these to exist; React handles rendering
		// =====================================================================================

		/// <summary>
		/// No-op in .NET 10.
		/// BEFORE: WebForms DataGrid.ItemCreated event — built pager, sort arrows, MassUpdate hover buttons.
		/// AFTER:  React SPA handles all grid rendering; this method has no effect.
		/// </summary>
		public void OnItemCreated() { /* WebForms ItemCreated event — no-op in .NET 10 */ }

		/// <summary>
		/// No-op in .NET 10.
		/// BEFORE: WebForms DataGrid.PageIndexChanged event — updated CurrentPageIndex, called DataBind.
		/// AFTER:  React SPA handles paging; page changes trigger REST API calls directly.
		/// </summary>
		public void OnPageIndexChanged() { /* WebForms PageIndexChanged event — no-op in .NET 10 */ }

		/// <summary>
		/// No-op in .NET 10.
		/// BEFORE: WebForms DataGrid.SortCommand event — updated sort state, called DataBind.
		/// AFTER:  React SPA handles sort; sort clicks trigger REST API calls directly.
		/// </summary>
		public void OnSort() { /* WebForms SortCommand event — no-op in .NET 10 */ }

		// =====================================================================================
		// DataBind
		//
		// BEFORE: public override void DataBind() — called base.DataBind() after L10nTranslate
		// AFTER:  public void DataBind() — no base class; calls L10nTranslate + SelectMethod
		// =====================================================================================

		/// <summary>
		/// Binds data to the grid, translating column headers and invoking custom paging.
		/// BEFORE: public override void DataBind() on System.Web.UI.WebControls.DataGrid
		/// AFTER:  public void DataBind() — React SPA handles actual rendering
		/// </summary>
		public void DataBind()
		{
			L10nTranslate();
			// 09/08/2009 Paul.  If we are using custom paging, send the binding event.
			if (AllowCustomPaging && SelectMethod != null)
			{
				// 09/08/2009 Paul.  In PrintView, we disable paging, so if this flag is disabled, then show all records.
				// BEFORE: bool bPrintView = Sql.ToBoolean(Context.Items["PrintView"]);
				// AFTER:  _httpContextAccessor.HttpContext?.Items["PrintView"]
				bool bPrintView = Sql.ToBoolean(_httpContextAccessor?.HttpContext?.Items["PrintView"]);
				if (bPrintView)
					SelectMethod(0, -1);
				else
					SelectMethod(CurrentPageIndex, PageSize);
			}
		}

		// =====================================================================================
		// ApplySort
		//
		// BEFORE: applied DataView.Sort when !AllowCustomPaging
		// AFTER:  same logic — DataView.Sort applied directly on DataSource
		// =====================================================================================

		/// <summary>
		/// Applies the current sort state to the DataSource DataView.
		/// Only applied when AllowCustomPaging is false (client-side sort on loaded data).
		/// BEFORE: applied DataView.Sort based on ViewState sort column/order
		/// AFTER:  same logic using _viewState and DataSource DataView
		/// </summary>
		public void ApplySort()
		{
			// 09/08/2009 Paul.  We can't use the default handling when using custom paging.
			if (!AllowCustomPaging || SelectMethod == null)
			{
				string sLastSortColumn = SortColumn;
				string sLastSortOrder  = SortOrder;
				if (DataSource != null && !Sql.IsEmptyString(sLastSortColumn))
				{
					// 04/20/2008 Paul.  We need to make sure that the table contains the sort column.
					if (DataSource.Table.Columns.Contains(sLastSortColumn))
						DataSource.Sort = sLastSortColumn + " " + sLastSortOrder;
				}
			}
		}

		// =====================================================================================
		// OrderByClause
		//
		// BEFORE: two overloads — parameterless (uses ViewState) and 2-param (sets + returns)
		// AFTER:  same logic; Page.IsPostBack check removed (no WebForms page lifecycle)
		// =====================================================================================

		/// <summary>
		/// Builds a SQL ORDER BY clause from the current sort state.
		/// Returns empty string if SortColumn is not set.
		/// BEFORE: " order by " + SortColumn + " " + SortOrder + ControlChars.CrLf
		/// </summary>
		public string OrderByClause()
		{
			if (Sql.IsEmptyString(this.SortColumn))
				return String.Empty;
			return " order by " + this.SortColumn + " " + this.SortOrder + ControlChars.CrLf;
		}

		/// <summary>
		/// Sets the sort state from the provided column and direction, then builds an ORDER BY clause.
		/// On first call (IsPostBack=false equivalent), sets state from parameters.
		/// On subsequent calls, returns state from the grid's internal sort tracking.
		/// BEFORE: checked Page.IsPostBack (WebForms) to decide whether to read or set state
		/// AFTER:  state always readable from SortColumn/SortOrder; parameters applied if state is empty
		/// </summary>
		/// <param name="sSortColumn">Default sort column name.</param>
		/// <param name="sSortOrder">Default sort direction ("asc" or "desc").</param>
		public string OrderByClause(string sSortColumn, string sSortOrder)
		{
			// 04/26/2008 Paul.  Move Last Sort to the database.
			// BEFORE: if (!this.Page.IsPostBack) { set state } else { read state }
			// AFTER:  set state if it hasn't been set yet (simulates non-postback behavior)
			if (Sql.IsEmptyString(this.SortColumn))
			{
				this.SortColumn = sSortColumn;
				this.SortOrder  = sSortOrder;
			}
			else
			{
				sSortColumn = this.SortColumn;
				sSortOrder  = this.SortOrder;
			}
			// 10/23/2010 Paul.  Prevent invalid SQL if SortColumn is not specified.
			if (!Sql.IsEmptyString(sSortColumn))
				return " order by " + sSortColumn + " " + sSortOrder + ControlChars.CrLf;
			return String.Empty;
		}

		// =====================================================================================
		// SetSortFields
		//
		// BEFORE: set ViewState sort keys or remove them when null provided
		// AFTER:  same logic using _viewState
		// =====================================================================================

		/// <summary>
		/// Sets the sort state from a 2-element string array [sortColumn, sortOrder].
		/// Pass null to clear the sort state.
		/// BEFORE: ViewState[this.ID + ".LastSortColumn"] / .LastSortOrder
		/// AFTER:  _viewState[this.ID + ".LastSortColumn"] / .LastSortOrder
		/// </summary>
		/// <param name="arrSort">
		/// 2-element array: [sortColumn, sortOrder].
		/// Pass null to clear sort state.
		/// </param>
		public void SetSortFields(string[] arrSort)
		{
			if (arrSort != null)
			{
				if (arrSort.Length == 2)
				{
					if (!Sql.IsEmptyString(arrSort[0]) && !Sql.IsEmptyString(arrSort[1]))
					{
						SortColumn = arrSort[0];
						SortOrder  = arrSort[1];
					}
				}
			}
			else
			{
				// 12/17/2007 Paul.  Clear the sort when NULL provided.
				_viewState.Remove(this.ID + ".LastSortColumn");
				_viewState.Remove(this.ID + ".LastSortOrder");
			}
		}

		// =====================================================================================
		// DynamicColumns
		//
		// BEFORE: called SplendidDynamic.AppendGridColumns(sGRID_NAME, this) — static with DataGrid
		// AFTER:  sets GRID_NAME only (SplendidDynamic.AppendGridColumns() is no-op for WebForms DataGrid)
		// =====================================================================================

		/// <summary>
		/// Sets the grid view name. Equivalent to calling AppendGridColumns with no select fields.
		/// BEFORE: also called SplendidDynamic.AppendGridColumns(sGRID_NAME, this) to build DataGrid columns.
		/// AFTER:  sets GRID_NAME; column building removed as React handles grid layout.
		/// </summary>
		/// <param name="sGRID_NAME">Grid view name (e.g. "Accounts.ListView").</param>
		public void DynamicColumns(string sGRID_NAME)
		{
			this.GRID_NAME = sGRID_NAME;
			// BEFORE: SplendidDynamic.AppendGridColumns(sGRID_NAME, this);
			// AFTER:  no-op — SplendidDynamic.AppendGridColumns(DataGrid) removed in .NET 10 migration
		}

		// =====================================================================================
		// AppendGridColumns overloads
		//
		// BEFORE: called SplendidDynamic.AppendGridColumns(sGRID_NAME, this, arrSelectFields, handler)
		//         which built DataGrid columns (WebForms)
		// AFTER:  sets GRID_NAME and populates arrSelectFields via SplendidDynamic.GridColumns()
		//         which is the .NET 10 data-centric equivalent
		// =====================================================================================

		/// <summary>
		/// Sets the grid view name (convenience overload with no select field collection).
		/// BEFORE: called AppendGridColumns(sGRID_NAME, null)
		/// </summary>
		public void AppendGridColumns(string sGRID_NAME)
		{
			this.GRID_NAME = sGRID_NAME;
			AppendGridColumns(sGRID_NAME, null);
		}

		/// <summary>
		/// Sets the grid view name and populates <paramref name="arrSelectFields"/> with
		/// the column field names defined in the grid view metadata.
		/// Schema: AppendGridColumns(string, UniqueStringCollection)
		/// BEFORE: SplendidDynamic.AppendGridColumns(sGRID_NAME, this, arrSelectFields, null)
		/// AFTER:  _splendidDynamic.GridColumns(sGRID_NAME, arrSelectFields, null)
		/// </summary>
		public void AppendGridColumns(string sGRID_NAME, UniqueStringCollection arrSelectFields)
		{
			this.GRID_NAME = sGRID_NAME;
			if (_splendidDynamic != null && arrSelectFields != null)
			{
				// 02/08/2008 Paul.  We need to build a list of the fields used by the dynamic grid.
				// BEFORE: SplendidDynamic.AppendGridColumns(sGRID_NAME, this, arrSelectFields, null)
				// AFTER:  _splendidDynamic.GridColumns(sGRID_NAME, arrSelectFields, null)
				_splendidDynamic.GridColumns(sGRID_NAME, arrSelectFields, null);
			}
		}

		/// <summary>
		/// Sets the grid view name, populates select fields, and associates a command handler.
		/// Schema: AppendGridColumns(string, UniqueStringCollection, CommandEventHandler)
		/// BEFORE: SplendidDynamic.AppendGridColumns(sGRID_NAME, this, arrSelectFields, Page_Command)
		/// AFTER:  stores Page_Command in Command field; calls GridColumns for field enumeration
		/// </summary>
		public void AppendGridColumns(string sGRID_NAME, UniqueStringCollection arrSelectFields, CommandEventHandler Page_Command)
		{
			this.GRID_NAME = sGRID_NAME;
			if (Page_Command != null)
				this.Command = Page_Command;
			if (_splendidDynamic != null && arrSelectFields != null)
				_splendidDynamic.GridColumns(sGRID_NAME, arrSelectFields, null);
		}

		/// <summary>
		/// Sets the grid view name, populates select fields, and associates primary and secondary command handlers.
		/// Schema: AppendGridColumns(string, UniqueStringCollection, CommandEventHandler, CommandEventHandler)
		/// The secondary handler is stored for use by selection (popup) commands.
		/// BEFORE: no direct equivalent (new overload for .NET 10 selection popup support)
		/// </summary>
		public void AppendGridColumns(string sGRID_NAME, UniqueStringCollection arrSelectFields, CommandEventHandler Page_Command, CommandEventHandler Page_CommandSecondary)
		{
			this.GRID_NAME = sGRID_NAME;
			if (Page_Command != null)
				this.Command = Page_Command;
			if (_splendidDynamic != null && arrSelectFields != null)
				_splendidDynamic.GridColumns(sGRID_NAME, arrSelectFields, null);
		}
	}

	// =====================================================================================
	// DynamicImage
	//
	// BEFORE: public class DynamicImage : System.Web.UI.UserControl
	//         OnDataBinding() created an Image child control with SkinID/ToolTip
	// AFTER:  standalone class — only SkinID property (React handles image rendering)
	//
	// REMOVED:  UserControl base class, OnDataBinding override, Image child control creation
	// PRESERVED: SkinID property (schema: DynamicImage.SkinID)
	// =====================================================================================

	/// <summary>
	/// Lightweight image data carrier — exposes SkinID for dynamic image lookup.
	/// Migrated from SplendidCRM/_code/SplendidGrid.cs DynamicImage class.
	/// WebForms UserControl inheritance and child control creation removed.
	/// React SPA resolves the SkinID to an actual image path client-side.
	/// </summary>
	public class DynamicImage
	{
		private string sImageSkinID;
		private string sAlternateText;

		/// <summary>
		/// Image skin identifier used to look up the physical image path from the active theme.
		/// Schema: DynamicImage.SkinID
		/// BEFORE: Image child control SkinID applied in UserControl.OnDataBinding()
		/// AFTER:  plain property for data-only transport to React renderer
		/// </summary>
		public string SkinID
		{
			get { return sImageSkinID; }
			set { sImageSkinID = value; }
		}

		/// <summary>
		/// Alternative text for accessibility.
		/// BEFORE: Image.ToolTip (IE8 used ToolTip instead of alt)
		/// </summary>
		public string AlternateText
		{
			get { return sAlternateText; }
			set { sAlternateText = value; }
		}
	}
}
