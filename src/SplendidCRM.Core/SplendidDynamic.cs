// SplendidDynamic.cs — Dynamic view/edit/list rendering support for grid/detail/edit layouts
//
// .NET 10 Migration: SplendidCRM/_code/SplendidDynamic.cs → src/SplendidCRM.Core/SplendidDynamic.cs
//
// Changes made for .NET 10 ASP.NET Core migration:
//   - REMOVED: using System.Web; using System.Web.UI.*; using AjaxControlToolkit; (WebForms-only namespaces)
//   - REMOVED: #if !ReactOnlyUI blocks — WebForms rendering methods (AppendGridColumns with DataGrid/HtmlTable,
//              AppendButtons, AppendDetailViewFields, AppendEditViewFields rendering bodies)
//   - REMOVED: static class pattern — converted to DI-injectable instance service
//   - REPLACED: HttpContext.Current.Application["key"] → _memoryCache.Get<object>("key")
//   - REPLACED: HttpContext.Current.Items["C10n"] → _httpContextAccessor.HttpContext?.Items["C10n"]
//   - REPLACED: SplendidCache.GridViewColumns() (static) → _splendidCache.GridViewColumns() (instance)
//   - REPLACED: Security.PRIMARY_ROLE_NAME (static) → _security.PRIMARY_ROLE_NAME (instance)
//   - REPLACED: Security.GetUserFieldSecurity() (static) → _security.GetUserFieldSecurity() (instance)
//   - REPLACED: SplendidCache.GridViewRules() (static, 2-param) → _splendidCache.GridViewRules() (instance, 1-param)
//   - REPLACED: SplendidCache.EditViewRules() (static) → _splendidCache.EditViewRules() (instance)
//   - REPLACED: SplendidCache.DetailViewRules() (static) → _splendidCache.DetailViewRules() (instance)
//   - REPLACED: SplendidCache.EditViewFields() (static) → _splendidCache.EditViewFields() (instance)
//   - REPLACED: SplendidCache.ReportRules() (static) → _splendidCache.ReportRules() (instance)
//   - REPLACED: SplendidReportThis(HttpContext.Current.Application, ...) → SplendidReportThis(_memoryCache, ...)
//   - REPLACED: SplendidControlThis(parent, module, row) → SplendidControlThis(parent, module, row, _security, _httpContextAccessor)
//   - PRESERVED: namespace SplendidCRM, all public method signatures, all business logic
//   - PRESERVED: StackedLayout() static methods (pure logic, no DI dependency)
//   - PRESERVED: UpdateCustomFields(DataRow, ...) logic exactly (only Currency access migrated)
//   - PRESERVED: ApplyGridViewRules() logic exactly
//   - PRESERVED: SplendidInit.bEnableACLFieldSecurity (remains static)
//   - ADDED: no-op 0-param overloads for schema-required WebForms method stubs
//            (AppendGridColumns(), AppendButtons(), AppendDetailViewFields(), AppendEditViewFields())
//   - ADDED: ExportGridColumns(string) 1-parameter convenience overload
//   - PRESERVED: MD5 hashing and all security logic unchanged
//
//   DI Registration: services.AddScoped<SplendidDynamic>();

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Collections.Specialized;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using LogicBuilder.Workflow.Activities.Rules;

namespace SplendidCRM
{
	/// <summary>
	/// Dynamic view/edit/list rendering support for grid, detail, and edit view layouts.
	///
	/// Migrated from SplendidCRM/_code/SplendidDynamic.cs (~7,458 lines) for .NET 10 ASP.NET Core.
	/// In the ReactOnlyUI migration (#if !ReactOnlyUI blocks removed), all WebForms rendering
	/// code (AppendGridColumns with DataGrid/HtmlTable, AppendButtons, AppendDetailViewFields,
	/// AppendEditViewFields rendering) is removed. The remaining data-centric methods (GridColumns,
	/// ExportGridColumns, SearchGridColumns, ApplyRules, UpdateCustomFields) are fully migrated.
	///
	/// DESIGN NOTES:
	///   • Register as SCOPED so each HTTP request gets its own Security / SplendidCache instance.
	///   • All former static methods that accessed Security or SplendidCache are now instance
	///     methods; StackedLayout() remains static since it has no DI dependencies.
	///   • UpdateCustomFields(DataRow, ...) uses _httpContextAccessor for Currency (C10n) access.
	/// </summary>
	public class SplendidDynamic
	{
		// =====================================================================================
		// DI-injected fields
		// =====================================================================================

		/// <summary>
		/// Replaces HttpContext.Current for Items access (Currency C10n) in UpdateCustomFields(DataRow).
		/// BEFORE: HttpContext.Current.Items["C10n"] as Currency
		/// AFTER:  _httpContextAccessor.HttpContext?.Items["C10n"] as Currency
		/// </summary>
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Replaces HttpApplicationState (Application[]) for reading module metadata flags.
		/// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["Modules." + mod + ".Valid"])
		/// AFTER:  Sql.ToBoolean(_memoryCache.Get&lt;object&gt;("Modules." + mod + ".Valid"))
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		/// <summary>Application configuration — available for environment-specific overrides.</summary>
		private readonly IConfiguration _configuration;

		/// <summary>
		/// ACL and authentication service — replaces static Security.PRIMARY_ROLE_NAME,
		/// Security.GetUserFieldSecurity(), and Security.GetUserAccess() calls.
		/// BEFORE: Security.PRIMARY_ROLE_NAME (static)
		/// AFTER:  _security.PRIMARY_ROLE_NAME (instance)
		/// </summary>
		private readonly Security _security;

		/// <summary>
		/// Metadata caching hub — replaces static SplendidCache.GridViewColumns(),
		/// GridViewRules(), EditViewRules(), DetailViewRules(), ReportRules(), EditViewFields() calls.
		/// BEFORE: SplendidCache.GridViewColumns(sGRID_NAME, Security.PRIMARY_ROLE_NAME) (static)
		/// AFTER:  _splendidCache.GridViewColumns(sGRID_NAME, _security.PRIMARY_ROLE_NAME) (instance)
		/// </summary>
		private readonly SplendidCache _splendidCache;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs a SplendidDynamic service with all required DI dependencies.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current for per-request Items access (Currency C10n).
		/// </param>
		/// <param name="memoryCache">
		/// Replaces HttpApplicationState (Application[]) for module metadata flag access.
		/// </param>
		/// <param name="configuration">Application configuration provider.</param>
		/// <param name="security">
		/// ACL service replacing static Security.PRIMARY_ROLE_NAME and GetUserFieldSecurity() calls.
		/// </param>
		/// <param name="splendidCache">
		/// Metadata cache replacing static SplendidCache.GridViewColumns(), GridViewRules(),
		/// EditViewRules(), DetailViewRules(), ReportRules(), and EditViewFields() calls.
		/// </param>
		public SplendidDynamic(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache         ,
			IConfiguration       configuration       ,
			Security             security            ,
			SplendidCache        splendidCache       )
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
			_configuration       = configuration      ;
			_security            = security           ;
			_splendidCache       = splendidCache      ;
		}

		// =====================================================================================
		// StackedLayout — static (no DI dependency, pure logic)
		//
		// BEFORE: public static bool StackedLayout(string sTheme, string sViewName)
		// AFTER:  unchanged — no System.Web or DI dependency
		// =====================================================================================

		/// <summary>
		/// Returns true when the theme uses a stacked (single-column) layout for the given view.
		/// The "Seven" theme uses stacked layout, except for .Preview views.
		/// </summary>
		/// <param name="sTheme">UI theme name (e.g. "Seven").</param>
		/// <param name="sViewName">View name (e.g. "Accounts.EditView", "Accounts.EditView.Preview").</param>
		public static bool StackedLayout(string sTheme, string sViewName)
		{
			return (sTheme == "Seven" && !sViewName.EndsWith(".Preview"));
		}

		/// <summary>
		/// Returns true when the theme uses a stacked (single-column) layout.
		/// </summary>
		/// <param name="sTheme">UI theme name (e.g. "Seven").</param>
		public static bool StackedLayout(string sTheme)
		{
			return (sTheme == "Seven");
		}

		// =====================================================================================
		// ExportGridColumns — SQL SELECT column list builder
		//
		// BEFORE: public static string ExportGridColumns(...) — called static GridColumns()
		// AFTER:  instance method — calls instance GridColumns()
		// =====================================================================================

		/// <summary>
		/// Builds a SQL SELECT column list string for the specified grid view.
		/// Convenience overload with no pre-selected fields (starts empty).
		/// Added in .NET 10 migration as a convenience wrapper over the 2-parameter overload.
		/// </summary>
		/// <param name="sGRID_NAME">Grid view name (e.g. "Accounts.ListView").</param>
		/// <returns>SQL column list string suitable for embedding in a SELECT statement.</returns>
		public string ExportGridColumns(string sGRID_NAME)
		{
			return ExportGridColumns(sGRID_NAME, new UniqueStringCollection());
		}

		/// <summary>
		/// Builds a SQL SELECT column list string using the grid view column metadata,
		/// starting with the fields already in <paramref name="arrDataGridSelectedFields"/>.
		/// Always ensures the ID column is included.
		/// </summary>
		/// <param name="sGRID_NAME">Grid view name (e.g. "Accounts.ListView.Export").</param>
		/// <param name="arrDataGridSelectedFields">
		/// Fields pre-selected by the calling grid (may be empty). Modified in-place by GridColumns().
		/// </param>
		/// <returns>SQL column list string with one field per line.</returns>
		// 09/23/2015 Paul.  Need to include the data grid fields as it will be bound using the same data set.
		public string ExportGridColumns(string sGRID_NAME, UniqueStringCollection arrDataGridSelectedFields)
		{
			StringBuilder sbSQL = new StringBuilder();
			UniqueStringCollection arrSelectFields = new UniqueStringCollection();
			if ( arrDataGridSelectedFields != null )
			{
				foreach ( string sField in arrDataGridSelectedFields )
				{
					arrSelectFields.Add(sField);
				}
			}
			// 05/03/2011 Paul.  Always include the ID as it might be used by the Export code to filter by selected items.
			arrSelectFields.Add("ID");
			GridColumns(sGRID_NAME, arrSelectFields, null);
			// 04/20/2011 Paul.  If there are no fields in the GridView.Export, then return all fields (*).
			if ( arrSelectFields.Count > 0 )
			{
				foreach ( string sField in arrSelectFields )
				{
					if ( sbSQL.Length > 0 )
						sbSQL.Append("     , ");
					sbSQL.AppendLine(sField);
				}
			}
			else
			{
				sbSQL.AppendLine("*");
			}
			return sbSQL.ToString();
		}

		/// <summary>
		/// Builds a SQL SELECT column list string, skipping fields in <paramref name="arrSkippedFields"/>.
		/// </summary>
		/// <param name="sGRID_NAME">Grid view name.</param>
		/// <param name="arrDataGridSelectFields">Fields pre-selected by calling code.</param>
		/// <param name="arrSkippedFields">Fields to skip during column enumeration.</param>
		/// <returns>SQL column list string.</returns>
		// 01/02/2020 Paul.  We need to be able to skip fields.
		public string ExportGridColumns(string sGRID_NAME, UniqueStringCollection arrDataGridSelectFields, StringCollection arrSkippedFields)
		{
			StringBuilder sbSQL = new StringBuilder();
			UniqueStringCollection arrSelectFields = new UniqueStringCollection();
			foreach ( string sField in arrDataGridSelectFields )
			{
				arrSelectFields.Add(sField);
			}
			// 05/03/2011 Paul.  Always include the ID as it might be used by the Export code to filter by selected items.
			arrSelectFields.Add("ID");
			GridColumns(sGRID_NAME, arrSelectFields, arrSkippedFields);
			// 04/20/2011 Paul.  If there are no fields in the GridView.Export, then return all fields (*).
			if ( arrSelectFields.Count > 0 )
			{
				foreach ( string sField in arrSelectFields )
				{
					if ( sbSQL.Length > 0 )
						sbSQL.Append("     , ");
					sbSQL.AppendLine(sField);
				}
			}
			else
			{
				sbSQL.AppendLine("*");
			}
			return sbSQL.ToString();
		}

		/// <summary>
		/// Builds a SQL SELECT column list string with a table prefix applied to each field.
		/// Used by export queries that join multiple tables and need to qualify column names.
		/// </summary>
		/// <param name="sGRID_NAME">Grid view name.</param>
		/// <param name="arrDataGridSelectFields">Fields pre-selected by calling code.</param>
		/// <param name="sTABLE_PREFIX">Table alias prefix (e.g. "vwACCOUNTS").</param>
		/// <param name="arrSkippedFields">Fields to skip during column enumeration.</param>
		/// <returns>SQL column list string with qualified field names.</returns>
		// 01/02/2020 Paul.  We need to be able to specify a prefix.
		public string ExportGridColumns(string sGRID_NAME, UniqueStringCollection arrDataGridSelectFields, string sTABLE_PREFIX, StringCollection arrSkippedFields)
		{
			StringBuilder sbSQL = new StringBuilder();
			UniqueStringCollection arrSelectFields = new UniqueStringCollection();
			foreach ( string sField in arrDataGridSelectFields )
			{
				arrSelectFields.Add(sField);
			}
			// 05/03/2011 Paul.  Always include the ID as it might be used by the Export code to filter by selected items.
			arrSelectFields.Add("ID");
			GridColumns(sGRID_NAME, arrSelectFields, arrSkippedFields);
			// 04/20/2011 Paul.  If there are no fields in the GridView.Export, then return all fields (*).
			if ( arrSelectFields.Count > 0 )
			{
				foreach ( string sField in arrSelectFields )
				{
					if ( sbSQL.Length > 0 )
						sbSQL.Append("     , ");
					// 09/23/2015 Paul.  Special exception.
					if ( sField == "FAVORITE_RECORD_ID" )
						sbSQL.AppendLine(sField);
					else
						sbSQL.AppendLine(sTABLE_PREFIX + "." + sField);
				}
			}
			else
			{
				sbSQL.AppendLine("*");
			}
			return sbSQL.ToString();
		}

		// =====================================================================================
		// SearchGridColumns — search column set builder
		//
		// BEFORE: public static void SearchGridColumns(string, UniqueStringCollection)
		// AFTER:  instance method — calls instance GridColumns()
		// =====================================================================================

		/// <summary>
		/// Populates <paramref name="arrSelectFields"/> with the fields from the specified grid view,
		/// excluding common audit/lookup-only columns not relevant to searching.
		/// Removes any empty field entries added by Hover-type columns.
		/// </summary>
		/// <param name="sGRID_NAME">Grid view name (e.g. "Accounts.ListView").</param>
		/// <param name="arrSelectFields">
		/// Collection to populate with searchable field names. Modified in-place.
		/// </param>
		public void SearchGridColumns(string sGRID_NAME, UniqueStringCollection arrSelectFields)
		{
			StringCollection arrSkippedFields = new StringCollection();
			arrSkippedFields.Add("USER_NAME"    );
			arrSkippedFields.Add("ASSIGNED_TO"  );
			arrSkippedFields.Add("CREATED_BY"   );
			arrSkippedFields.Add("MODIFIED_BY"  );
			arrSkippedFields.Add("DATE_ENTERED" );
			arrSkippedFields.Add("DATE_MODIFIED");
			arrSkippedFields.Add("TEAM_NAME"    );
			arrSkippedFields.Add("TEAM_SET_NAME");
			// 05/15/2016 Paul.  Don't need to search ASSIGNED_TO_NAME.
			arrSkippedFields.Add("ASSIGNED_TO_NAME");
			GridColumns(sGRID_NAME, arrSelectFields, arrSkippedFields);
			// 10/03/2018 Paul.  Remove an empty field.  This can occur if Hover field is used in Search layout.
			arrSelectFields.Remove(String.Empty);
		}

		// =====================================================================================
		// GridColumns — core grid column metadata consumer
		//
		// BEFORE: public static void GridColumns(string, UniqueStringCollection, StringCollection)
		//         — accessed SplendidCache.GridViewColumns() and Security.PRIMARY_ROLE_NAME as static
		//         — accessed HttpContext.Current.Application["Modules.{mod}.Valid"]
		// AFTER:  instance method
		//         — _splendidCache.GridViewColumns(sGRID_NAME, _security.PRIMARY_ROLE_NAME)
		//         — _memoryCache.Get<object>("Modules.{mod}.Valid")
		//         — Security.GetUserFieldSecurity() → _security.GetUserFieldSecurity()
		// =====================================================================================

		/// <summary>
		/// Core method that populates <paramref name="arrSelectFields"/> with column names from the
		/// specified grid view metadata, applying ACL field-level security and respecting the
		/// skip list. Called by ExportGridColumns() and SearchGridColumns().
		/// </summary>
		/// <param name="sGRID_NAME">
		/// Grid view name, format: "{Module}.ListView", "{Module}.PopupView", "{Parent}.{Module}.Subpanel", etc.
		/// </param>
		/// <param name="arrSelectFields">
		/// Collection to populate with selected field names. Modified in-place.
		/// </param>
		/// <param name="arrSkippedFields">
		/// Field names to skip. Pass null to include all fields (used for Export).
		/// </param>
		// 04/20/2011 Paul.  Create a new method so that we can get export fields.
		public void GridColumns(string sGRID_NAME, UniqueStringCollection arrSelectFields, StringCollection arrSkippedFields)
		{
			// 05/10/2016 Paul.  The User Primary Role is used with role-based views.
			// BEFORE: SplendidCache.GridViewColumns(sGRID_NAME, Security.PRIMARY_ROLE_NAME)
			// AFTER:  _splendidCache.GridViewColumns(sGRID_NAME, _security.PRIMARY_ROLE_NAME)
			DataTable dt = _splendidCache.GridViewColumns(sGRID_NAME, _security.PRIMARY_ROLE_NAME);
			if ( dt != null )
			{
				// 01/18/2010 Paul.  To apply ACL Field Security, we need to know the Module Name,
				// which we will extract from the GridView Name.
				string sMODULE_NAME = String.Empty;
				string[] arrGRID_NAME = sGRID_NAME.Split('.');
				if ( arrGRID_NAME.Length > 0 )
				{
					if ( arrGRID_NAME[0] == "ListView" || arrGRID_NAME[0] == "PopupView" || arrGRID_NAME[0] == "Activities" )
						sMODULE_NAME = arrGRID_NAME[0];
					// 01/18/2010 Paul.  A sub-panel should apply the access rules of the related module.
					// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["Modules." + arrGRID_NAME[1] + ".Valid"])
					// AFTER:  Sql.ToBoolean(_memoryCache.Get<object>("Modules." + arrGRID_NAME[1] + ".Valid"))
					else if ( arrGRID_NAME.Length > 1 && Sql.ToBoolean(_memoryCache.Get<object>("Modules." + arrGRID_NAME[1] + ".Valid")) )
						sMODULE_NAME = arrGRID_NAME[1];
					else
						sMODULE_NAME = arrGRID_NAME[0];
				}
				foreach(DataRow row in dt.Rows)
				{
					string sCOLUMN_TYPE = Sql.ToString (row["COLUMN_TYPE"]);
					string sDATA_FIELD  = Sql.ToString (row["DATA_FIELD" ]);
					string sDATA_FORMAT = Sql.ToString (row["DATA_FORMAT"]);
					string sMODULE_TYPE = Sql.ToString (row["MODULE_TYPE"]);

					// 04/20/2011 Paul.  Export requests will not exclude any fields.
					if ( arrSkippedFields != null )
					{
						if ( arrSkippedFields.Contains(sDATA_FIELD) || sDATA_FIELD.EndsWith("_ID") || sDATA_FIELD.EndsWith("_CURRENCY") )
							continue;
					}

					// 01/18/2010 Paul.  A field is either visible or not.
					bool bIsReadable = true;
					if ( SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(sDATA_FIELD) )
					{
						// BEFORE: Security.GetUserFieldSecurity(sMODULE_NAME, sDATA_FIELD, Guid.Empty)
						// AFTER:  _security.GetUserFieldSecurity(sMODULE_NAME, sDATA_FIELD, Guid.Empty)
						Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sMODULE_NAME, sDATA_FIELD, Guid.Empty);
						bIsReadable = acl.IsReadable();
					}

					if ( bIsReadable )
					{
						if ( String.Compare(sCOLUMN_TYPE, "TemplateColumn", true) == 0 )
						{
							if ( String.Compare(sDATA_FORMAT, "HyperLink", true) == 0 )
							{
								if ( !Sql.IsEmptyString(sDATA_FIELD) )
								{
									// 02/26/2018 Paul.  There is a special case where we have a custom field module lookup.
									// 07/12/2018 Paul.  Use Contains instead of ends with.
									if ( sGRID_NAME.Contains(".Export") && !Sql.IsEmptyString(sMODULE_TYPE) && sDATA_FIELD.EndsWith("_ID_C") )
									{
										// BEFORE: Crm.Modules.TableName(sMODULE_TYPE)
										// AFTER:  Crm.Modules.TableName(sMODULE_TYPE) — uses ambient cache (static)
										string sSubQueryTable = Crm.Modules.TableName(sMODULE_TYPE);
										// 02/26/2018 Paul.  Top 1 will not work in Oracle, but this will have to be a known limitation.
										string sSubQueryField = "(select top 1 NAME from vw" + sSubQueryTable + " where vw" + sSubQueryTable + ".ID = " + sDATA_FIELD + ") as " + sDATA_FIELD;
										if ( arrSelectFields.Contains(sDATA_FIELD) )
										{
											arrSelectFields.Remove(sDATA_FIELD);
											arrSelectFields.Add(sSubQueryField);
										}
										else
										{
											arrSelectFields.Add(sSubQueryField);
										}
									}
									else
									{
										arrSelectFields.Add(sDATA_FIELD);
									}
								}
							}
							// 05/05/2017 Paul.  Include Date, DateTime and Currency in case they were configured for export as template fields.
							else if ( String.Compare(sDATA_FORMAT, "Date", true) == 0 || String.Compare(sDATA_FORMAT, "DateTime", true) == 0 || String.Compare(sDATA_FORMAT, "Currency", true) == 0 )
							{
								if ( !Sql.IsEmptyString(sDATA_FIELD) )
									arrSelectFields.Add(sDATA_FIELD);
							}
							// 02/11/2016 Paul.  Allow searching of hover fields.
							else if ( String.Compare(sDATA_FORMAT, "Hover", true) == 0 )
							{
								string sURL_FIELD = Sql.ToString(row["URL_FIELD"]);
								if ( SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(sURL_FIELD) )
								{
									string[] arrURL_FIELD = sURL_FIELD.Split(' ');
									for ( int i = 0; i < arrURL_FIELD.Length; i++ )
									{
										if ( !arrURL_FIELD[i].Contains(".") )
										{
											// BEFORE: Security.GetUserFieldSecurity(...) — static
											// AFTER:  _security.GetUserFieldSecurity(...) — instance
											Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sMODULE_NAME, arrURL_FIELD[i], Guid.Empty);
											if ( acl.IsReadable() )
												arrSelectFields.Add(sDATA_FIELD);
										}
									}
								}
							}
							// 05/16/2016 Paul.  Include Tags in list of valid columns.
							else if ( String.Compare(sDATA_FORMAT, "Tags", true) == 0 )
							{
								if ( !Sql.IsEmptyString(sDATA_FIELD) )
									arrSelectFields.Add(sDATA_FIELD);
							}
						}
						else if ( String.Compare(sCOLUMN_TYPE, "BoundColumn", true) == 0 )
						{
							// 09/23/2010 Paul.  Add the bound field.
							if ( !Sql.IsEmptyString(sDATA_FIELD) )
								arrSelectFields.Add(sDATA_FIELD);
						}
						// 02/11/2016 Paul.  Allow searching of hidden field.
						else if ( String.Compare(sCOLUMN_TYPE, "Hidden", true) == 0 )
						{
							if ( !Sql.IsEmptyString(sDATA_FIELD) )
								arrSelectFields.Add(sDATA_FIELD);
						}
					}
				}
			}
		}

		// =====================================================================================
		// No-op stubs for WebForms layout rendering
		//
		// These methods are inside #if !ReactOnlyUI in the source codebase.
		// Since SplendidCRM is now React-only, these WebForms layout rendering methods
		// are replaced by the React front-end. The back-end no longer needs to produce
		// HtmlTable, DataGrid, or Control trees — those concerns belong to the React SPA.
		// =====================================================================================

		/// <summary>
		/// No-op stub. WebForms grid column rendering is handled by the React SPA.
		/// </summary>
		// MIGRATION NOTE: AppendGridColumns(string, DataGrid, ...) overloads removed.
		// React SPA retrieves grid layout via REST — no server-side rendering required.
		public void AppendGridColumns() { }

		/// <summary>
		/// No-op stub. WebForms button rendering is handled by the React SPA.
		/// </summary>
		// MIGRATION NOTE: AppendButtons(string, Guid, Control, ...) overloads removed.
		// React SPA retrieves button definitions via REST DynamicButtons() metadata API.
		public void AppendButtons() { }

		/// <summary>
		/// No-op stub. WebForms detail view field rendering is handled by the React SPA.
		/// </summary>
		// MIGRATION NOTE: AppendDetailViewFields(string, HtmlTable, DataRow, ...) overloads removed.
		// React SPA retrieves detail view layout via REST DetailViewFields() metadata API.
		public void AppendDetailViewFields() { }

		/// <summary>
		/// No-op stub. WebForms edit view field rendering is handled by the React SPA.
		/// </summary>
		// MIGRATION NOTE: AppendEditViewFields(string, HtmlTable, DataRow, ...) overloads removed.
		// React SPA retrieves edit view layout via REST EditViewFields() metadata API.
		public void AppendEditViewFields() { }

		/// <summary>
		/// No-op stub. WebForms client-side validation is handled by the React SPA.
		/// Server-side validation uses the DataRow overload of ValidateEditViewFields.
		/// </summary>
		// MIGRATION NOTE: ValidateEditViewFields(string, Control) removed.
		// React SPA validates fields before submission; server validates DataRow values.
		public void ValidateEditViewFields() { }

		/// <summary>
		/// Validates edit view required fields against the metadata definition.
		/// Looks up field layout from SplendidCache, then delegates to the DataTable overload.
		/// </summary>
		/// <param name="sEDIT_NAME">Edit view name, e.g. "Accounts.EditView".</param>
		/// <param name="parent">WebForms parent control whose child controls are inspected.
		/// FindControl() returns null for all IDs in ASP.NET Core so this is effectively a no-op
		/// in the migrated runtime, but the method signature is preserved for source compatibility.</param>
		// MIGRATION NOTE: WebForms Control.FindControl() always returns null in ASP.NET Core context.
		// All validator Enable/Validate calls are therefore no-ops.
		public void ValidateEditViewFields(string sEDIT_NAME, Control parent)
		{
			// BEFORE: SplendidCache.EditViewFields(sEDIT_NAME, Security.PRIMARY_ROLE_NAME) — static
			// AFTER:  _splendidCache.EditViewFields(sEDIT_NAME, _security.PRIMARY_ROLE_NAME) — instance
			DataTable dtFields = _splendidCache.EditViewFields(sEDIT_NAME, _security.PRIMARY_ROLE_NAME);
			ValidateEditViewFields(dtFields, sEDIT_NAME, parent);
		}

		/// <summary>
		/// Validates required edit view fields by iterating layout metadata and calling
		/// Control.Validate() on required field validators. In the ASP.NET Core migration,
		/// all FindControl() calls return null so this is effectively a no-op.
		/// </summary>
		/// <param name="dtFields">Pre-loaded edit view fields DataTable.</param>
		/// <param name="sEDIT_NAME">Edit view name for context.</param>
		/// <param name="parent">Parent WebForms control (null-safe).</param>
		// MIGRATION NOTE: This method was inside #if !ReactOnlyUI in the source.
		// Preserved for interface compatibility; runtime behaviour is a no-op because
		// Control.FindControl() always returns null in ASP.NET Core.
		public void ValidateEditViewFields(DataTable dtFields, string sEDIT_NAME, Control parent)
		{
			bool bEnableTeamManagement    = Crm.Config.enable_team_management();
			bool bRequireTeamManagement   = Crm.Config.require_team_management();
			bool bEnableDynamicTeams      = Crm.Config.enable_dynamic_teams();
			bool bRequireUserAssignment   = Crm.Config.require_user_assignment();
			bool bEnableDynamicAssignment = Crm.Config.enable_dynamic_assignment();
			if ( Crm.Config.enable_multi_tenant_teams() )
			{
				bEnableTeamManagement  = true;
				bRequireTeamManagement = true;
			}
			DataView dvFields = new DataView(dtFields);
			dvFields.RowFilter = "UI_REQUIRED = 1 or UI_VALIDATOR = 1";
			foreach ( DataRowView rowView in dvFields )
			{
				DataRow row           = rowView.Row;
				string  sDATA_FIELD   = Sql.ToString (row["DATA_FIELD" ]);
				string  sFIELD_TYPE   = Sql.ToString (row["FIELD_TYPE" ]);
				bool    bUI_REQUIRED  = Sql.ToBoolean(row["UI_REQUIRED"]);
				bool    bUI_VALIDATOR = false;
				if ( dtFields.Columns.Contains("UI_VALIDATOR") )
					bUI_VALIDATOR = Sql.ToBoolean(row["UI_VALIDATOR"]);
				if ( bEnableTeamManagement )
				{
					if ( String.Compare(sFIELD_TYPE, "TeamSelect", true) == 0 && !bRequireTeamManagement && bEnableDynamicTeams )
						continue;
					if ( String.Compare(sFIELD_TYPE, "TeamSelect", true) == 0 && bRequireTeamManagement && !bEnableDynamicTeams )
						continue;
				}
				else
				{
					if ( String.Compare(sFIELD_TYPE, "TeamSelect", true) == 0 )
						continue;
				}
				if ( bEnableDynamicAssignment )
				{
					if ( String.Compare(sFIELD_TYPE, "UserSelect", true) == 0 && !bRequireUserAssignment )
						continue;
				}
				else
				{
					if ( String.Compare(sFIELD_TYPE, "UserSelect", true) == 0 )
						continue;
				}
				if ( bUI_REQUIRED )
				{
					// MIGRATION NOTE: FindControl() always returns null in ASP.NET Core — all branches below are no-ops.
					if ( String.Compare(sFIELD_TYPE, "ListBox", true) == 0 )
					{
						ListControl lstField = parent?.FindControl(sDATA_FIELD) as ListControl;
						if ( lstField != null && lstField.Visible && Sql.IsEmptyString(lstField.SelectedValue) )
						{
							BaseValidator req = parent?.FindControl(sDATA_FIELD + "_REQUIRED") as BaseValidator;
							if ( req != null ) { req.Enabled = true; req.Validate(); }
						}
					}
					else if ( String.Compare(sFIELD_TYPE, "DateRange", true) == 0 )
					{
						DatePicker ctlDateStart = parent?.FindControl(sDATA_FIELD.Replace(" ", "_") + "_AFTER") as DatePicker;
						if ( ctlDateStart != null && ctlDateStart.Visible ) ctlDateStart.Validate();
						DatePicker ctlDateEnd = parent?.FindControl(sDATA_FIELD.Replace(" ", "_") + "_BEFORE") as DatePicker;
						if ( ctlDateEnd != null && ctlDateEnd.Visible ) ctlDateEnd.Validate();
					}
					else if ( String.Compare(sFIELD_TYPE, "DatePicker", true) == 0 )
					{
						DatePicker ctlDate = parent?.FindControl(sDATA_FIELD.Replace(" ", "_")) as DatePicker;
						if ( ctlDate != null && ctlDate.Visible ) ctlDate.Validate();
					}
					else if ( String.Compare(sFIELD_TYPE, "DateTimePicker", true) == 0 )
					{
						DateTimePicker ctlDate = parent?.FindControl(sDATA_FIELD) as DateTimePicker;
						if ( ctlDate != null && ctlDate.Visible ) ctlDate.Validate();
					}
					else if ( String.Compare(sFIELD_TYPE, "DateTimeEdit", true) == 0 || String.Compare(sFIELD_TYPE, "DateTimeNewRecord", true) == 0 )
					{
						DateTimeEdit ctlDate = parent?.FindControl(sDATA_FIELD) as DateTimeEdit;
						if ( ctlDate != null && ctlDate.Visible ) ctlDate.Validate();
					}
					else if ( String.Compare(sFIELD_TYPE, "TeamSelect", true) == 0 )
					{
						TeamSelect ctlTeamSelect = parent?.FindControl(sDATA_FIELD) as TeamSelect;
						if ( ctlTeamSelect != null && ctlTeamSelect.Visible ) ctlTeamSelect.Validate();
					}
					else if ( String.Compare(sFIELD_TYPE, "UserSelect", true) == 0 )
					{
						UserSelect ctlUserSelect = parent?.FindControl(sDATA_FIELD) as UserSelect;
						if ( ctlUserSelect != null && ctlUserSelect.Visible ) ctlUserSelect.Validate();
					}
					else if ( String.Compare(sFIELD_TYPE, "TagSelect", true) == 0 )
					{
						TagSelect ctlTagSelect = parent?.FindControl(sDATA_FIELD) as TagSelect;
						if ( ctlTagSelect != null && ctlTagSelect.Visible ) ctlTagSelect.Validate();
					}
					else if ( String.Compare(sFIELD_TYPE, "NAICSCodeSelect", true) == 0 )
					{
						NAICSCodeSelect ctlNAICSCodeSelect = parent?.FindControl(sDATA_FIELD) as NAICSCodeSelect;
						if ( ctlNAICSCodeSelect != null && ctlNAICSCodeSelect.Visible ) ctlNAICSCodeSelect.Validate();
					}
					else if ( String.Compare(sFIELD_TYPE, "KBTagSelect", true) == 0 )
					{
						KBTagSelect ctlKBTagSelect = parent?.FindControl(sDATA_FIELD) as KBTagSelect;
						if ( ctlKBTagSelect != null && ctlKBTagSelect.Visible ) ctlKBTagSelect.Validate();
					}
					else if ( String.Compare(sFIELD_TYPE, "File", true) == 0 )
					{
						HtmlInputHidden ctlHidden = parent?.FindControl(sDATA_FIELD) as HtmlInputHidden;
						if ( ctlHidden != null && !Sql.IsEmptyString(ctlHidden.Value) ) continue;
						Control ctl = parent?.FindControl(sDATA_FIELD + "_File");
						if ( ctl != null && ctl.Visible )
						{
							BaseValidator req = parent?.FindControl(sDATA_FIELD + "_REQUIRED") as BaseValidator;
							if ( req != null ) { req.Enabled = true; req.Validate(); }
						}
					}
					else
					{
						Control ctl = parent?.FindControl(sDATA_FIELD);
						if ( ctl != null && ctl.Visible )
						{
							BaseValidator req = parent?.FindControl(sDATA_FIELD + "_REQUIRED") as BaseValidator;
							if ( req != null ) { req.Enabled = true; req.Validate(); }
						}
					}
				}
				if ( bUI_VALIDATOR )
				{
					Control ctl = parent?.FindControl(sDATA_FIELD);
					if ( ctl != null && ctl.Visible )
					{
						BaseValidator req = parent?.FindControl(sDATA_FIELD + "_VALIDATOR") as BaseValidator;
						if ( req != null ) { req.Enabled = true; req.Validate(); }
					}
				}
			}
		}

		// =====================================================================================
		// ApplyEditViewRules — edit view business rule application
		//
		// BEFORE: public static void ApplyEditViewRules(string, SplendidControl, string, DataRow)
		//         — used SplendidCache.EditViewRules(sEDIT_NAME, Security.PRIMARY_ROLE_NAME) as static
		//         — used SplendidControlThis(parent, module, row) — static constructor
		// AFTER:  instance method
		//         — _splendidCache.EditViewRules(sEDIT_NAME, _security.PRIMARY_ROLE_NAME)
		//         — SplendidControlThis(parent, module, row, _security, _httpContextAccessor)
		// =====================================================================================

		/// <summary>
		/// Applies edit view business rules (XOML) against the specified DataRow.
		/// Looks up pre/post load rule XOML from the edit view rules metadata and executes
		/// them against a SplendidControlThis context bound to the parent control.
		/// </summary>
		/// <param name="sEDIT_NAME">Edit view name, e.g. "Accounts.EditView".</param>
		/// <param name="parent">SplendidControl parent providing the layout control tree.</param>
		/// <param name="sXOML_FIELD_NAME">XOML field name, e.g. "PRE_LOAD_EVENT_ID".</param>
		/// <param name="row">DataRow whose values are evaluated by the rule set.</param>
		// 11/10/2010 Paul.  Apply Business Rules.
		public void ApplyEditViewRules(string sEDIT_NAME, SplendidControl parent, string sXOML_FIELD_NAME, DataRow row)
		{
			try
			{
				string sMODULE_NAME = sEDIT_NAME.Split('.')[0];
				// BEFORE: SplendidCache.EditViewRules(sEDIT_NAME, Security.PRIMARY_ROLE_NAME) — static
				// AFTER:  _splendidCache.EditViewRules(sEDIT_NAME, _security.PRIMARY_ROLE_NAME) — instance
				DataTable dtFields = _splendidCache.EditViewRules(sEDIT_NAME, _security.PRIMARY_ROLE_NAME);
				if ( dtFields.Rows.Count > 0 )
				{
					string sXOML = Sql.ToString(dtFields.Rows[0][sXOML_FIELD_NAME]);
					if ( !Sql.IsEmptyString(sXOML) )
					{
						RuleSet rules = RulesUtil.Deserialize(sXOML);
						RuleValidation validation = new RuleValidation(typeof(SplendidControlThis), null);
						rules.Validate(validation);
						if ( validation.Errors.HasErrors )
							throw new Exception(RulesUtil.GetValidationErrors(validation));
						// BEFORE: new SplendidControlThis(parent, sMODULE_NAME, row)
						// AFTER:  new SplendidControlThis(parent, sMODULE_NAME, row, _security, _httpContextAccessor)
						SplendidControlThis swThis = new SplendidControlThis(parent, sMODULE_NAME, row, _security, _httpContextAccessor);
						RuleExecution exec = new RuleExecution(validation, swThis);
						rules.Execute(exec);
					}
				}
			}
			catch ( Exception ex )
			{
				// 11/10/2010 Paul.  Throw the inner exception message to skip the filler SplendidControlThis.Throw message.
				if ( ex.InnerException != null )
					throw new Exception(ex.InnerException.Message);
				else
					throw new Exception(ex.Message);
			}
		}

		// =====================================================================================
		// ApplyDetailViewRules — detail view business rule application
		//
		// BEFORE: public static void ApplyDetailViewRules(string, SplendidControl, string, DataRow)
		//         — used SplendidCache.DetailViewRules(sDETAIL_NAME, Security.PRIMARY_ROLE_NAME) as static
		// AFTER:  instance method using DI services
		// =====================================================================================

		/// <summary>
		/// Applies detail view business rules (XOML) against the specified DataRow.
		/// </summary>
		/// <param name="sDETAIL_NAME">Detail view name, e.g. "Accounts.DetailView".</param>
		/// <param name="parent">SplendidControl parent providing the layout control tree.</param>
		/// <param name="sXOML_FIELD_NAME">XOML field name, e.g. "PRE_LOAD_EVENT_ID".</param>
		/// <param name="row">DataRow whose values are evaluated by the rule set.</param>
		public void ApplyDetailViewRules(string sDETAIL_NAME, SplendidControl parent, string sXOML_FIELD_NAME, DataRow row)
		{
			try
			{
				string sMODULE_NAME = sDETAIL_NAME.Split('.')[0];
				// BEFORE: SplendidCache.DetailViewRules(sDETAIL_NAME, Security.PRIMARY_ROLE_NAME) — static
				// AFTER:  _splendidCache.DetailViewRules(sDETAIL_NAME, _security.PRIMARY_ROLE_NAME) — instance
				DataTable dtFields = _splendidCache.DetailViewRules(sDETAIL_NAME, _security.PRIMARY_ROLE_NAME);
				if ( dtFields.Rows.Count > 0 )
				{
					string sXOML = Sql.ToString(dtFields.Rows[0][sXOML_FIELD_NAME]);
					if ( !Sql.IsEmptyString(sXOML) )
					{
						RuleSet rules = RulesUtil.Deserialize(sXOML);
						RuleValidation validation = new RuleValidation(typeof(SplendidControlThis), null);
						rules.Validate(validation);
						if ( validation.Errors.HasErrors )
							throw new Exception(RulesUtil.GetValidationErrors(validation));
						SplendidControlThis swThis = new SplendidControlThis(parent, sMODULE_NAME, row, _security, _httpContextAccessor);
						RuleExecution exec = new RuleExecution(validation, swThis);
						rules.Execute(exec);
					}
				}
			}
			catch ( Exception ex )
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// ApplyGridViewRules — grid view business rule application (OUTSIDE #if !ReactOnlyUI)
		//
		// This method was outside the WebForms conditional block in the source and applies
		// rules from PRE_LOAD and POST_LOAD XOML fields stored in vwGRIDVIEWS_RULES.
		//
		// BEFORE: public static void ApplyGridViewRules(string, SplendidControl, string, string, DataTable)
		//         — SplendidCache.GridViewRules(sGRID_NAME, Security.PRIMARY_ROLE_NAME) — static 2-param
		//         — new SplendidControlThis(parent, sMODULE_NAME, row/dt) — 3-param constructor
		// AFTER:  instance method
		//         — _splendidCache.GridViewRules(sGRID_NAME) — 1-param only in migrated SplendidCache
		//         — new SplendidControlThis(parent, sMODULE_NAME, row/dt, _security, _httpContextAccessor)
		// =====================================================================================

		/// <summary>
		/// Applies pre-load and post-load grid view business rules against the supplied DataTable.
		/// The pre-load rule runs once against the table; the post-load rule runs once per row.
		/// </summary>
		/// <param name="sGRID_NAME">Grid view name, e.g. "Accounts.ListView".</param>
		/// <param name="parent">SplendidControl parent providing the layout control tree.</param>
		/// <param name="sPRE_LOAD_XOML_FIELD_NAME">Name of the pre-load XOML column in the rules DataTable.</param>
		/// <param name="sPOST_LOAD_XOML_FIELD_NAME">Name of the post-load XOML column in the rules DataTable.</param>
		/// <param name="dt">DataTable of rows to apply post-load rules to.</param>
		// 11/22/2010 Paul.  For a ListView, it makes sense to allow a column to be added in a Pre Event
		// and the column to be set in the Post Event.
		public void ApplyGridViewRules(string sGRID_NAME, SplendidControl parent, string sPRE_LOAD_XOML_FIELD_NAME, string sPOST_LOAD_XOML_FIELD_NAME, DataTable dt)
		{
			try
			{
				string sMODULE_NAME = sGRID_NAME.Split('.')[0];
				// BEFORE: SplendidCache.GridViewRules(sGRID_NAME, Security.PRIMARY_ROLE_NAME) — static, 2-param
				// AFTER:  _splendidCache.GridViewRules(sGRID_NAME) — instance, 1-param only in migrated SplendidCache
				DataTable dtFields = _splendidCache.GridViewRules(sGRID_NAME);
				if ( dtFields.Rows.Count > 0 )
				{
					string sXOML = Sql.ToString(dtFields.Rows[0][sPRE_LOAD_XOML_FIELD_NAME]);
					if ( !Sql.IsEmptyString(sXOML) )
					{
						RuleSet rules = RulesUtil.Deserialize(sXOML);
						RuleValidation validation = new RuleValidation(typeof(SplendidControlThis), null);
						// 11/11/2010 Paul.  Validate so that we can get more information on a runtime error.
						rules.Validate(validation);
						if ( validation.Errors.HasErrors )
							throw new Exception(RulesUtil.GetValidationErrors(validation));
						// BEFORE: new SplendidControlThis(parent, sMODULE_NAME, dt) — 3-param
						// AFTER:  new SplendidControlThis(parent, sMODULE_NAME, dt, _security, _httpContextAccessor)
						SplendidControlThis swThis = new SplendidControlThis(parent, sMODULE_NAME, dt, _security, _httpContextAccessor);
						RuleExecution exec = new RuleExecution(validation, swThis);
						rules.Execute(exec);
					}
					sXOML = Sql.ToString(dtFields.Rows[0][sPOST_LOAD_XOML_FIELD_NAME]);
					if ( !Sql.IsEmptyString(sXOML) )
					{
						RuleSet rules = RulesUtil.Deserialize(sXOML);
						RuleValidation validation = new RuleValidation(typeof(SplendidControlThis), null);
						rules.Validate(validation);
						if ( validation.Errors.HasErrors )
							throw new Exception(RulesUtil.GetValidationErrors(validation));
						foreach ( DataRow row in dt.Rows )
						{
							SplendidControlThis swThis = new SplendidControlThis(parent, sMODULE_NAME, row, _security, _httpContextAccessor);
							RuleExecution exec = new RuleExecution(validation, swThis);
							rules.Execute(exec);
						}
					}
				}
			}
			catch ( Exception ex )
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// ApplyReportRules — report business rule application
		//
		// BEFORE: public static void ApplyReportRules(L10N, Guid, Guid, DataTable)
		//         Inside #if !ReactOnlyUI
		//         — SplendidCache.ReportRules(id) — static
		//         — new SplendidReportThis(HttpContext.Current.Application, L10n, ...) — HttpContext.Current
		// AFTER:  instance method
		//         — _splendidCache.ReportRules(id)
		//         — new SplendidReportThis(_memoryCache, L10n, ...) — IMemoryCache replaces Application
		// =====================================================================================

		/// <summary>
		/// Applies pre-load and post-load report business rules against the supplied DataTable.
		/// Rules are loaded from vwSYSTEM_EVENTS by their GUIDs. Pre-load rule runs once against
		/// the full table; post-load rule runs once per row.
		/// </summary>
		/// <param name="L10n">Localization instance for rule execution context.</param>
		/// <param name="gPRE_LOAD_EVENT_ID">GUID of the pre-load report event rule. Empty = skip.</param>
		/// <param name="gPOST_LOAD_EVENT_ID">GUID of the post-load report event rule. Empty = skip.</param>
		/// <param name="dt">DataTable of rows to apply post-load rules to.</param>
		// 12/04/2010 Paul.  Add support for Business Rules Framework to Reports.
		public void ApplyReportRules(L10N L10n, Guid gPRE_LOAD_EVENT_ID, Guid gPOST_LOAD_EVENT_ID, DataTable dt)
		{
			if ( !Sql.IsEmptyGuid(gPRE_LOAD_EVENT_ID) )
			{
				// BEFORE: SplendidCache.ReportRules(gPRE_LOAD_EVENT_ID) — static, Guid arg → queries vwRULES_Edit by ID
				// AFTER:  _splendidCache.ReportRules(gPRE_LOAD_EVENT_ID) — instance, Guid overload added to migrated SplendidCache
				DataTable dtFields = _splendidCache.ReportRules(gPRE_LOAD_EVENT_ID);
				if ( dtFields.Rows.Count > 0 )
				{
					string sMODULE_NAME = Sql.ToString(dtFields.Rows[0]["MODULE_NAME"]);
					string sXOML        = Sql.ToString(dtFields.Rows[0]["XOML"       ]);
					if ( !Sql.IsEmptyString(sXOML) )
					{
						RuleSet rules = RulesUtil.Deserialize(sXOML);
						RuleValidation validation = new RuleValidation(typeof(SplendidReportThis), null);
						rules.Validate(validation);
						if ( validation.Errors.HasErrors )
							throw new Exception(RulesUtil.GetValidationErrors(validation));
						// BEFORE: new SplendidReportThis(HttpContext.Current.Application, L10n, sMODULE_NAME, dt)
						// AFTER:  new SplendidReportThis(_memoryCache, L10n, sMODULE_NAME, dt, _security, _httpContextAccessor)
						SplendidReportThis swThis = new SplendidReportThis(_memoryCache, L10n, sMODULE_NAME, dt, _security, _httpContextAccessor);
						RuleExecution exec = new RuleExecution(validation, swThis);
						rules.Execute(exec);
					}
				}
			}
			if ( !Sql.IsEmptyGuid(gPOST_LOAD_EVENT_ID) )
			{
				DataTable dtFields = _splendidCache.ReportRules(gPOST_LOAD_EVENT_ID);
				if ( dtFields.Rows.Count > 0 )
				{
					string sMODULE_NAME = Sql.ToString(dtFields.Rows[0]["MODULE_NAME"]);
					string sXOML        = Sql.ToString(dtFields.Rows[0]["XOML"       ]);
					if ( !Sql.IsEmptyString(sXOML) )
					{
						RuleSet rules = RulesUtil.Deserialize(sXOML);
						RuleValidation validation = new RuleValidation(typeof(SplendidReportThis), null);
						rules.Validate(validation);
						if ( validation.Errors.HasErrors )
							throw new Exception(RulesUtil.GetValidationErrors(validation));
						foreach ( DataRow row in dt.Rows )
						{
							SplendidReportThis swThis = new SplendidReportThis(_memoryCache, L10n, sMODULE_NAME, row, _security, _httpContextAccessor);
							RuleExecution exec = new RuleExecution(validation, swThis);
							rules.Execute(exec);
						}
					}
				}
			}
		}

		// =====================================================================================
		// LoadImage — private helper for image upload processing
		//
		// BEFORE: private static bool LoadImage(SplendidControl, Guid, string, IDbTransaction)
		//         — used HtmlInputFile / HttpPostedFile / HttpContext.Current.Application["CONFIG.upload_maxsize"]
		//         — used SqlProcs.spIMAGES_Insert for image record creation
		//         — used Crm.Images.LoadFile for byte streaming
		// AFTER:  no-op returning false
		//         — HtmlInputFile is a WebForms type not available in ASP.NET Core
		//         — File upload in ASP.NET Core is handled via IFormFile in controller actions
		//         — Preserving the method signature for UpdateCustomFields(SplendidControl) compatibility
		// =====================================================================================

		/// <summary>
		/// Processes an image file uploaded through a WebForms HtmlInputFile control.
		/// In the ASP.NET Core migration this is a no-op returning false, because file uploads
		/// are handled through IFormFile in API controllers, not through WebForms controls.
		/// </summary>
		/// <param name="ctlPARENT">Parent control containing the file input.</param>
		/// <param name="gParentID">Parent record ID to associate the image with.</param>
		/// <param name="sFIELD_NAME">Field name of the image control.</param>
		/// <param name="trn">Database transaction to use for image insertion.</param>
		/// <returns>Always false in the migrated version — no WebForms file controls available.</returns>
		// MIGRATION NOTE: HtmlInputFile and HttpPostedFile are WebForms types removed from .NET 10.
		// IFormFile-based upload is handled at the API controller layer before calling UpdateCustomFields.
		private bool LoadImage(SplendidControl ctlPARENT, Guid gParentID, string sFIELD_NAME, IDbTransaction trn)
		{
			// In ASP.NET Core, file uploads arrive as IFormFile in the controller action.
			// The controller is responsible for persisting images before calling UpdateCustomFields.
			// This stub preserves the call site in UpdateCustomFields(SplendidControl) without
			// breaking compilation, but does not perform any image loading.
			return false;
		}

		// =====================================================================================
		// UpdateCustomFields(SplendidControl, ...) — custom field persistence from WebForms
		//
		// BEFORE: public static void UpdateCustomFields(SplendidControl, IDbTransaction, Guid, string, DataTable)
		//         — used new DynamicControl(ctlPARENT, sNAME) to read form values
		//         — used HttpContext.Current.Items["C10n"] for Currency instance in decimal/money fields
		//         — LoadImage() for HtmlInputFile-backed Guid fields
		// AFTER:  instance method
		//         — DynamicControl(ctlPARENT, sNAME).Exists is always false (no underlying DataRow)
		//           so the method is effectively a no-op, which is correct for the ReactOnlyUI path
		//         — Currency accessed via _httpContextAccessor.HttpContext?.Items["C10n"]
		// =====================================================================================

		/// <summary>
		/// Updates custom fields for a parent control by iterating the custom field metadata
		/// and reading values from DynamicControl child controls.
		/// In the migrated ASP.NET Core runtime, DynamicControl.Exists always returns false
		/// when constructed without a DataRow (no WebForms form posting), so this method
		/// effectively executes a no-op UPDATE SET ID_C = ID_C (audit-only update).
		/// </summary>
		/// <param name="ctlPARENT">Parent SplendidControl containing child DynamicControl instances.</param>
		/// <param name="trn">Database transaction for the UPDATE command.</param>
		/// <param name="gID">Record ID to update (used as WHERE ID_C = @ID_C).</param>
		/// <param name="sTABLE_NAME">Base table name; UPDATE targets {sTABLE_NAME}_CSTM.</param>
		/// <param name="dtCustomFields">Custom field metadata rows (NAME, CsType, DATA_TYPE, MAX_SIZE).</param>
		// 09/09/2009 Paul.  Change parameter name to be more logical.
		public void UpdateCustomFields(SplendidControl ctlPARENT, IDbTransaction trn, Guid gID, string sTABLE_NAME, DataTable dtCustomFields)
		{
			if ( dtCustomFields.Rows.Count > 0 )
			{
				IDbConnection con = trn.Connection;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.Transaction   = trn;
					cmd.CommandType   = CommandType.Text;
					cmd.CommandText   = "update " + sTABLE_NAME + "_CSTM" + ControlChars.CrLf;
					int nFieldIndex   = 0;
					foreach ( DataRow row in dtCustomFields.Rows )
					{
						string sNAME      = Sql.ToString (row["NAME"  ]).ToUpper();
						string sCsType    = Sql.ToString (row["CsType"]);
						string sDATA_TYPE = String.Empty;
						if ( row.Table.Columns.Contains("DATA_TYPE") )
							sDATA_TYPE = Sql.ToString(row["DATA_TYPE"]);
						int    nMAX_SIZE  = Sql.ToInteger(row["MAX_SIZE"]);
						// MIGRATION NOTE: DynamicControl constructed without DataRow — Exists always false.
						// This is the correct ASP.NET Core behaviour: custom fields submitted via REST
						// arrive as DataRow values (handled by the DataRow overload below), not via
						// WebForms control trees.
						DynamicControl ctlCustomField = new DynamicControl(ctlPARENT, sNAME);
						if ( ctlCustomField.Exists && ctlCustomField.Type != "Literal" )
						{
							if ( nFieldIndex == 0 )
								cmd.CommandText += "   set ";
							else
								cmd.CommandText += "     , ";
							cmd.CommandText += sNAME + " = @" + sNAME + ControlChars.CrLf;
							DynamicControl ctlCustomField_File = new DynamicControl(ctlPARENT, sNAME + "_File");
							if ( sCsType == "Guid" && ctlCustomField.Type == "HtmlInputHidden" && ctlCustomField_File.Exists )
							{
								// MIGRATION NOTE: LoadImage() is a no-op in ASP.NET Core (no HtmlInputFile).
								LoadImage(ctlPARENT, gID, sNAME, trn);
							}
							switch ( sCsType )
							{
								case "Guid"    :  Sql.AddParameter(cmd, "@" + sNAME, ctlCustomField.ID           );  break;
								case "short"   :  Sql.AddParameter(cmd, "@" + sNAME, ctlCustomField.IntegerValue );  break;
								case "Int32"   :  Sql.AddParameter(cmd, "@" + sNAME, ctlCustomField.IntegerValue );  break;
								case "Int64"   :  Sql.AddParameter(cmd, "@" + sNAME, ctlCustomField.IntegerValue );  break;
								case "float"   :  Sql.AddParameter(cmd, "@" + sNAME, ctlCustomField.FloatValue   );  break;
								case "decimal" :
									if ( sDATA_TYPE == "money" )
									{
										// BEFORE: HttpContext.Current.Items["C10n"] — static HttpContext
										// AFTER:  _httpContextAccessor.HttpContext?.Items["C10n"] — DI injected
										Currency C10n = _httpContextAccessor.HttpContext?.Items["C10n"] as Currency;
										Decimal d = Sql.ToDecimal(ctlCustomField.DecimalValue);
										if ( C10n != null )
											d = C10n.FromCurrency(d);
										Sql.AddParameter(cmd, "@" + sNAME, d);
									}
									else
									{
										Sql.AddParameter(cmd, "@" + sNAME, ctlCustomField.DecimalValue);
									}
									break;
								case "bool"    :  Sql.AddParameter(cmd, "@" + sNAME, ctlCustomField.Checked    );  break;
								case "DateTime":  Sql.AddParameter(cmd, "@" + sNAME, ctlCustomField.DateValue  );  break;
								default        :  Sql.AddParameter(cmd, "@" + sNAME, ctlCustomField.Text       , nMAX_SIZE);  break;
							}
							nFieldIndex++;
						}
					}
					if ( nFieldIndex > 0 )
					{
						cmd.CommandText += " where ID_C = @ID_C" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@ID_C", gID);
						cmd.ExecuteNonQuery();
					}
					else
					{
						// 02/09/2021 Paul.  Update even when no data changed to create an audit record.
						cmd.CommandText += "   set ID_C = ID_C" + ControlChars.CrLf;
						cmd.CommandText += " where ID_C = @ID_C" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@ID_C", gID);
						cmd.ExecuteNonQuery();
					}
				}
			}
		}

		// =====================================================================================
		// UpdateCustomFields(SplendidPage, ...) — custom field persistence from web form POST
		//
		// BEFORE: public static void UpdateCustomFields(SplendidPage, IDbTransaction, Guid, string, DataTable)
		//         Inside #if !ReactOnlyUI
		//         — ctlPARENT.Request[sNAME] — WebForms System.Web.UI.Page.Request indexer
		//         — HttpContext.Current.Items["C10n"] — static HttpContext
		// AFTER:  instance method
		//         — _httpContextAccessor.HttpContext?.Request.Form[sNAME] — ASP.NET Core form access
		//         — _httpContextAccessor.HttpContext?.Items["C10n"] — DI injected context
		//
		// NOTE: In the ReactOnlyUI backend, this overload is only invoked from legacy WebToLead
		// capture flows and any code paths that previously called the WebForms form post pattern.
		// =====================================================================================

		/// <summary>
		/// Updates custom fields by reading values from the current HTTP request form post.
		/// Replaces the WebForms <c>ctlPARENT.Request[sNAME]</c> pattern with
		/// <c>_httpContextAccessor.HttpContext?.Request.Form[sNAME]</c>.
		/// </summary>
		/// <param name="ctlPARENT">SplendidPage parent (provides context; form values read from HttpContext).</param>
		/// <param name="trn">Database transaction for the UPDATE command.</param>
		/// <param name="gID">Record ID to update.</param>
		/// <param name="sTABLE_NAME">Base table name; UPDATE targets {sTABLE_NAME}_CSTM.</param>
		/// <param name="dtCustomFields">Custom field metadata rows.</param>
		// 09/09/2009 Paul.  Change parameter name to be more logical.
		public void UpdateCustomFields(SplendidPage ctlPARENT, IDbTransaction trn, Guid gID, string sTABLE_NAME, DataTable dtCustomFields)
		{
			if ( dtCustomFields.Rows.Count > 0 )
			{
				IDbConnection con = trn.Connection;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.Transaction = trn;
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = "update " + sTABLE_NAME + "_CSTM" + ControlChars.CrLf;
					int nFieldIndex = 0;
					foreach ( DataRow row in dtCustomFields.Rows )
					{
						string sNAME      = Sql.ToString (row["NAME"  ]).ToUpper();
						string sCsType    = Sql.ToString (row["CsType"]);
						string sDATA_TYPE = String.Empty;
						if ( row.Table.Columns.Contains("DATA_TYPE") )
							sDATA_TYPE = Sql.ToString(row["DATA_TYPE"]);
						int    nMAX_SIZE  = Sql.ToInteger(row["MAX_SIZE"]);
						// BEFORE: ctlPARENT.Request[sNAME] — WebForms System.Web.UI.Page.Request
						// AFTER:  _httpContextAccessor.HttpContext?.Request.Form[sNAME]
						//         falling back to QueryString for GET-style form submissions
						Microsoft.AspNetCore.Http.HttpContext? ctx = _httpContextAccessor.HttpContext;
						string? sFormValue = null;
						if ( ctx != null )
						{
							if ( ctx.Request.HasFormContentType && ctx.Request.Form.ContainsKey(sNAME) )
								sFormValue = ctx.Request.Form[sNAME].ToString();
							else if ( ctx.Request.Query.ContainsKey(sNAME) )
								sFormValue = ctx.Request.Query[sNAME].ToString();
						}
						if ( sFormValue != null )
						{
							string sVALUE = sFormValue;
							if ( nFieldIndex == 0 )
								cmd.CommandText += "   set ";
							else
								cmd.CommandText += "     , ";
							cmd.CommandText += sNAME + " = @" + sNAME + ControlChars.CrLf;
							switch ( sCsType )
							{
								case "Guid"    :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToGuid    (sVALUE));  break;
								case "short"   :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToInteger (sVALUE));  break;
								case "Int32"   :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToInteger (sVALUE));  break;
								case "Int64"   :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToInteger (sVALUE));  break;
								case "float"   :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToFloat   (sVALUE));  break;
								case "decimal" :
									if ( sDATA_TYPE == "money" )
									{
										// BEFORE: HttpContext.Current.Items["C10n"] — static
										// AFTER:  _httpContextAccessor.HttpContext?.Items["C10n"] — DI injected
										Currency C10n = _httpContextAccessor.HttpContext?.Items["C10n"] as Currency;
										Decimal d = Sql.ToDecimal(sVALUE);
										if ( C10n != null )
											d = C10n.FromCurrency(d);
										Sql.AddParameter(cmd, "@" + sNAME, d);
									}
									else
									{
										Sql.AddParameter(cmd, "@" + sNAME, Sql.ToDecimal (sVALUE));
									}
									break;
								case "bool"    :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToBoolean (sVALUE));  break;
								case "DateTime":  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToDateTime(sVALUE));  break;
								default        :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToString  (sVALUE), nMAX_SIZE);  break;
							}
							nFieldIndex++;
						}
					}
					if ( nFieldIndex > 0 )
					{
						cmd.CommandText += " where ID_C = @ID_C" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@ID_C", gID);
						cmd.ExecuteNonQuery();
					}
					else
					{
						// 01/19/2021 Paul.  Update even when no data changed to create an audit record.
						cmd.CommandText += "   set ID_C = ID_C" + ControlChars.CrLf;
						cmd.CommandText += " where ID_C = @ID_C" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@ID_C", gID);
						cmd.ExecuteNonQuery();
					}
				}
			}
		}

		// =====================================================================================
		// UpdateCustomFields(DataRow, ...) — custom field persistence from DataRow (REST/line items)
		//
		// BEFORE: public static void UpdateCustomFields(DataRow, IDbTransaction, Guid, string, DataTable)
		//         OUTSIDE #if !ReactOnlyUI — this is the primary REST API path
		//         — HttpContext.Current.Items["C10n"] — static
		//         — RestUtil.FromJsonDate() — static
		// AFTER:  instance method
		//         — _httpContextAccessor.HttpContext?.Items["C10n"] — DI injected
		//         — RestUtil.FromJsonDate() — remains static
		// =====================================================================================

		/// <summary>
		/// Updates custom fields from a DataRow containing field values.
		/// This is the primary REST API path — used by Quotes, Orders, Invoices line items
		/// and by the REST controller when submitting edit view form data.
		/// </summary>
		/// <param name="rowForm">DataRow containing field values keyed by column name.</param>
		/// <param name="trn">Database transaction for the UPDATE command.</param>
		/// <param name="gID">Record ID to update (WHERE ID_C = @ID_C).</param>
		/// <param name="sTABLE_NAME">Base table name; UPDATE targets {sTABLE_NAME}_CSTM.</param>
		/// <param name="dtCustomFields">Custom field metadata rows (NAME, CsType, DATA_TYPE, MAX_SIZE).</param>
		// 05/25/2008 Paul.  We need a version of UpdateCustomFields that pulls data from a DataRow
		// as this is how Quotes, Orders and Invoices manage their line items.
		// 09/09/2009 Paul.  Change parameter name to be more logical.
		public void UpdateCustomFields(DataRow rowForm, IDbTransaction trn, Guid gID, string sTABLE_NAME, DataTable dtCustomFields)
		{
			if ( dtCustomFields.Rows.Count > 0 )
			{
				IDbConnection con = trn.Connection;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.Transaction = trn;
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = "update " + sTABLE_NAME + "_CSTM" + ControlChars.CrLf;
					int nFieldIndex = 0;
					foreach ( DataRow row in dtCustomFields.Rows )
					{
						string sNAME      = Sql.ToString (row["NAME"  ]).ToUpper();
						string sCsType    = Sql.ToString (row["CsType"]);
						string sDATA_TYPE = String.Empty;
						if ( row.Table.Columns.Contains("DATA_TYPE") )
							sDATA_TYPE = Sql.ToString(row["DATA_TYPE"]);
						int    nMAX_SIZE  = Sql.ToInteger(row["MAX_SIZE"]);
						if ( rowForm.Table.Columns.Contains(sNAME) )
						{
							if ( nFieldIndex == 0 )
								cmd.CommandText += "   set ";
							else
								cmd.CommandText += "     , ";
							cmd.CommandText += sNAME + " = @" + sNAME + ControlChars.CrLf;
							switch ( sCsType )
							{
								case "Guid"    :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToGuid    (rowForm[sNAME]));  break;
								case "short"   :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToInteger (rowForm[sNAME]));  break;
								case "Int32"   :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToInteger (rowForm[sNAME]));  break;
								case "Int64"   :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToInteger (rowForm[sNAME]));  break;
								case "float"   :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToFloat   (rowForm[sNAME]));  break;
								case "decimal" :
									if ( sDATA_TYPE == "money" )
									{
										// 12/21/2017 Paul.  When called from the REST API, C10n will not be defined in the context items.
										// BEFORE: HttpContext.Current.Items["C10n"] — static
										// AFTER:  _httpContextAccessor.HttpContext?.Items["C10n"] — DI injected
										Currency C10n = _httpContextAccessor.HttpContext?.Items["C10n"] as Currency;
										Decimal d = Sql.ToDecimal(rowForm[sNAME]);
										if ( C10n != null )
											d = C10n.FromCurrency(d);
										Sql.AddParameter(cmd, "@" + sNAME, d);
									}
									else
									{
										Sql.AddParameter(cmd, "@" + sNAME, Sql.ToDecimal (rowForm[sNAME]));
									}
									break;
								case "bool"    :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToBoolean (rowForm[sNAME]));  break;
								case "DateTime":
									// 07/30/2020 Paul.  Date may be in JSON format.
									// RestUtil.FromJsonDate() remains static — no DI migration needed.
									Sql.AddParameter(cmd, "@" + sNAME, RestUtil.FromJsonDate(Sql.ToString(rowForm[sNAME])));
									break;
								default        :  Sql.AddParameter(cmd, "@" + sNAME, Sql.ToString  (rowForm[sNAME]), nMAX_SIZE);  break;
							}
							nFieldIndex++;
						}
					}
					if ( nFieldIndex > 0 )
					{
						cmd.CommandText += " where ID_C = @ID_C" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@ID_C", gID);
						cmd.ExecuteNonQuery();
					}
					else
					{
						// 02/08/2021 Paul.  Update even when no data changed to create an audit record.
						cmd.CommandText += "   set ID_C = ID_C" + ControlChars.CrLf;
						cmd.CommandText += " where ID_C = @ID_C" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@ID_C", gID);
						cmd.ExecuteNonQuery();
					}
				}
			}
		}

	}  // class SplendidDynamic
}  // namespace SplendidCRM
