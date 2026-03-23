// *********************************************************************************************
// The contents of this file are subject to the SugarCRM Public License Version 1.1.3
// ("License"); You may not use this file except in compliance with the License
// You may obtain a copy of the License at http://www.sugarcrm.com/SPL
// Software distributed under the License is distributed on an  "AS IS"  basis,
// WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
// the specific language governing rights and limitations under the License.
// The Original Code is:  SplendidCRM Open Source
// The Initial Developer of the Original Code is SplendidCRM Software, Inc.
// Purpose: RDL/RDS report document manipulation and payload sanitization utilities.
//
// MIGRATION NOTES (.NET Framework 4.8 → .NET 10 ASP.NET Core):
// - Removed: using System.Web; using System.Web.UI.WebControls; (no System.Web in .NET 10)
// - Added: IHttpContextAccessor DI replacing HttpContext.Current
// - Added: IMemoryCache DI replacing HttpApplicationState / Application["key"]
// - Added: IWebHostEnvironment DI replacing Context.Server.MapPath()
// - Changed: DbProviderFactories static calls → injected _dbProviderFactories instance
// - Changed: Security.USER_ID static → _security.USER_ID instance
// - Changed: SplendidCache.ReportingFilterColumns() static → _splendidCache instance
// - Changed: ConvertRDL2010ToRDL2008 static → instance method
// - Changed: HttpUtility.HtmlEncode → System.Net.WebUtility.HtmlEncode
// - Preserved: All business logic, XML processing, SQL generation exactly per minimal change clause
// *********************************************************************************************

#nullable disable

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using System.Net;
using System.Xml;
using System.Xml.Schema;
// ADDED: replaces System.Web.HttpContext.Current
using Microsoft.AspNetCore.Http;
// ADDED: replaces HttpApplicationState / Application["key"]
using Microsoft.Extensions.Caching.Memory;
// ADDED: replaces Context.Server.MapPath()
using Microsoft.AspNetCore.Hosting;

namespace SplendidCRM
{

/// <summary>
/// RdlDocument wraps an XML RDL report definition file, providing namespace-aware
/// manipulation, schema validation, SQL command building for datasets/charts, and
/// custom property management for SplendidCRM reporting.
/// </summary>
public class RdlDocument : XmlDocument
{
	// ----------------------------------------------------------------
	// RDL namespace strings for the three schema versions in use
	// ----------------------------------------------------------------
	protected string sDefaultNamespace    = String.Empty;
	protected string sNamespace           = String.Empty;
	protected string sNamespacePrefix     = String.Empty;
	// Used for 2010-era extended namespace
	protected string sDesignerNamespace   = "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";
	// Common to all RDL versions
	protected string sQueryDefinitionNS   = "http://schemas.microsoft.com/sql/2005/08/sql/query";

	// ----------------------------------------------------------------
	// DI-injected services (populated via the DI constructor; may be
	// null when the parameterless or name-only constructors are used)
	// ----------------------------------------------------------------
	protected DbProviderFactories   _dbProviderFactories;
	protected IMemoryCache          _memoryCache;
	protected Security              _security;
	protected SplendidCache         _splendidCache;
	protected Utils                 _utils;
	protected IWebHostEnvironment   _webHostEnvironment;
	protected IHttpContextAccessor  _httpContextAccessor;

	// ----------------------------------------------------------------
	// Validation error accumulator and namespace manager
	// ----------------------------------------------------------------
	protected StringBuilder sbValidationErrors;
	protected XmlNamespaceManager nsmgr;

	// ----------------------------------------------------------------
	// Public accessor for the namespace manager
	// ----------------------------------------------------------------
	public XmlNamespaceManager NamespaceManager
	{
		get { return nsmgr; }
	}

	// ----------------------------------------------------------------
	// XML validation event handler — accumulates errors in sbValidationErrors
	// ----------------------------------------------------------------
	private void ValidationHandler(object sender, ValidationEventArgs e)
	{
		sbValidationErrors.AppendLine(e.Message);
	}

	// ----------------------------------------------------------------
	// Validate — load schema and validate the in-memory RDL document.
	// MIGRATED: Context.Server.MapPath() → GetPhysicalPath()
	//           Utils.CachedFileExists(Context, path) → _utils?.CachedFileExists(null, path)
	// ----------------------------------------------------------------
	public bool Validate(HttpContext Context)
	{
		bool bValid = false;
		try
		{
			sbValidationErrors = new StringBuilder();

			const string sSchema2016 = "~/Reports/RDL 2016 ReportDefinition.xsd";
			const string sSchemaRdl  = "~/Reports/rdl.xsd";
			const string sSchemaRdl20= "~/Reports/rdl20.xsd";
			const string sSchemaRdl28= "~/Reports/rdl28.xsd";

			// Find the first available schema file
			string sSchemaVirtual = null;
			if (FileExistsVirtual(sSchema2016))      sSchemaVirtual = sSchema2016;
			else if (FileExistsVirtual(sSchemaRdl))  sSchemaVirtual = sSchemaRdl;
			else if (FileExistsVirtual(sSchemaRdl20))sSchemaVirtual = sSchemaRdl20;
			else if (FileExistsVirtual(sSchemaRdl28))sSchemaVirtual = sSchemaRdl28;

			if (sSchemaVirtual == null)
				return true;  // No schema available; treat as valid

			string sSchemaFile = GetPhysicalPath(sSchemaVirtual);
			using (FileStream stmSchema = File.Open(sSchemaFile, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				XmlSchema schema = XmlSchema.Read(stmSchema, ValidationHandler);
				XmlSchemaSet schemaSet = new XmlSchemaSet();
				schemaSet.Add(schema);
				this.Schemas = schemaSet;
				this.Validate(ValidationHandler);
			}

			bValid = sbValidationErrors.Length == 0;
		}
		catch(Exception ex)
		{
			SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
		}
		return bValid;
	}

	// Helper: check virtual path existence using either _utils or raw File.Exists
	private bool FileExistsVirtual(string sVirtualPath)
	{
		if (_utils != null)
			return _utils.CachedFileExists(null, sVirtualPath);
		return File.Exists(GetPhysicalPath(sVirtualPath));
	}

	// ----------------------------------------------------------------
	// ConvertRDL2010ToRDL2008 — converts 2010-schema RDL XML strings to
	// 2008 format. MIGRATED from static to instance so it can access
	// _dbProviderFactories for SharedDataSet queries.
	// ----------------------------------------------------------------
	public string ConvertRDL2010ToRDL2008(string sRDL)
	{
		if (String.IsNullOrEmpty(sRDL))
			return sRDL;

		const string sOldNS = "http://schemas.microsoft.com/sqlserver/reporting/2010/01/reportdefinition";
		const string sNewNS = "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition";
		const string sOldFontNS = "http://schemas.microsoft.com/sqlserver/reporting/2010/01/reportdefinition/defaultfontfamily";

		// Replace namespace declarations
		sRDL = sRDL.Replace(sOldNS  , sNewNS);
		sRDL = sRDL.Replace(sOldFontNS, sNewNS);

		// If SharedDataSetReference is present, try to inline the dataset columns
		if (sRDL.IndexOf("<SharedDataSetReference>") > 0)
		{
			XmlDocument xml2010 = new XmlDocument();
			xml2010.XmlResolver = null;
			xml2010.LoadXml(sRDL);

			XmlNamespaceManager ns2010 = new XmlNamespaceManager(xml2010.NameTable);
			ns2010.AddNamespace("defaultns", sNewNS);
			ns2010.AddNamespace("rd", sDesignerNamespace);
			ns2010.AddNamespace("qd", sQueryDefinitionNS);

			XmlNode xRef = XmlUtil.SelectNode(xml2010.DocumentElement,
				"defaultns:DataSets/defaultns:DataSet/defaultns:SharedDataSet/defaultns:SharedDataSetReference", ns2010);
			if (xRef != null)
			{
				string sRef = xRef.InnerText.Trim();
				if (!Sql.IsEmptyString(sRef) && sRef.StartsWith("vw"))
				{
					// MIGRATED: DbProviderFactories.GetFactory(Context.Application) → _dbProviderFactories?.GetFactory(_memoryCache)
					DbProviderFactory dbf = null;
					if (_dbProviderFactories != null && _memoryCache != null)
						dbf = _dbProviderFactories.GetFactory(_memoryCache);
					else if (_dbProviderFactories != null)
						dbf = _dbProviderFactories.GetFactory();

					if (dbf != null)
					{
						try
						{
							using (IDbConnection con = dbf.CreateConnection())
							using (DataTable dtColumns = new DataTable())
							{
								con.Open();
								using (IDbCommand cmd = con.CreateCommand())
								{
									cmd.CommandText = "select ColumnName, DataType from vwSqlColumns where TableName = @TABLE_NAME order by colorder";
									cmd.CommandType = CommandType.Text;
									Sql.AddParameter(cmd, "@TABLE_NAME", sRef);
									using (DbDataAdapter da = (DbDataAdapter)dbf.CreateDataAdapter())
									{
										da.SelectCommand = (System.Data.Common.DbCommand)cmd;
										da.Fill(dtColumns);
									}
								}
								if (dtColumns.Rows.Count > 0)
								{
									XmlNode xDataSet = XmlUtil.SelectNode(xml2010.DocumentElement,
										"defaultns:DataSets/defaultns:DataSet", ns2010);
									if (xDataSet != null)
									{
										XmlNode xSharedDS = xDataSet.SelectSingleNode("defaultns:SharedDataSet", ns2010);
										if (xSharedDS != null)
											xDataSet.RemoveChild(xSharedDS);

										XmlElement xFields = xml2010.CreateElement("Fields", sNewNS);
										xDataSet.AppendChild(xFields);
										foreach (DataRow row in dtColumns.Rows)
										{
											string sCN = Sql.ToString(row["ColumnName"]);
											string sDT = Sql.ToString(row["DataType"  ]);
											XmlElement xField = xml2010.CreateElement("Field", sNewNS);
											xFields.AppendChild(xField);
											xField.SetAttribute("Name", sCN);
											xField.AppendChild(xml2010.CreateElement("DataField", sNewNS)).InnerText = sCN;
											xField.AppendChild(xml2010.CreateElement("TypeName"  , sNewNS)).InnerText = sDT;
										}
									}
								}
							}
						}
						catch { }
					}
				}
			}
			sRDL = xml2010.OuterXml;
		}
		return sRDL;
	}

	// ----------------------------------------------------------------
	// LoadRdl — load a raw RDL XML string, converting 2010→2008 if needed
	// ----------------------------------------------------------------
	public void LoadRdl(string sRDL)
	{
		if (sRDL.IndexOf("http://schemas.microsoft.com/sqlserver/reporting/2010/01/reportdefinition") > 0)
			sRDL = ConvertRDL2010ToRDL2008(sRDL);

		this.XmlResolver = null;
		this.LoadXml(sRDL);

		if (this.DocumentElement != null)
		{
			sDefaultNamespace = this.DocumentElement.NamespaceURI;
			sNamespace        = sDefaultNamespace;
			sNamespacePrefix  = "defaultns";
			nsmgr = new XmlNamespaceManager(this.NameTable);
			nsmgr.AddNamespace("defaultns", sDefaultNamespace);
			nsmgr.AddNamespace("rd", sDesignerNamespace);
			nsmgr.AddNamespace("qd", sQueryDefinitionNS);
		}
	}

	// ----------------------------------------------------------------
	// XML helper methods — namespace-aware XPath wrappers
	// ----------------------------------------------------------------
	public XmlNode SelectNode(string sNode)
	{
		return XmlUtil.SelectNode(this, sNode, nsmgr);
	}

	public string SelectNodeValue(string sNode)
	{
		return XmlUtil.SelectSingleNode(this, sNode, nsmgr);
	}

	public string SelectNodeAttribute(XmlNode xParent, string sAttribute)
	{
		string sValue = String.Empty;
		if (xParent != null && xParent.Attributes != null)
		{
			XmlAttribute xAttr = xParent.Attributes[sAttribute];
			if (xAttr != null)
				sValue = xAttr.Value;
		}
		return sValue;
	}

	public XmlNodeList SelectNodesNS(string sNode)
	{
		return this.DocumentElement != null ? this.DocumentElement.SelectNodes(sNode, nsmgr) : null;
	}

	public XmlNodeList SelectNodesNS(XmlNode xParent, string sNode)
	{
		return xParent != null ? xParent.SelectNodes(sNode, nsmgr) : null;
	}

	// ----------------------------------------------------------------
	// SetSingleNode instance wrappers
	// NOTE: XmlUtil.SetSingleNode signature is (XmlDocument, sNode, sValue, nsmgr, sNamespaceURI)
	//       or (XmlDocument, XmlNode parent, sNode, sValue, nsmgr, sNamespaceURI)
	// ----------------------------------------------------------------
	public void SetSingleNode(string sNode, string sValue)
	{
		XmlUtil.SetSingleNode(this, sNode, sValue, nsmgr, sDefaultNamespace);
	}

	public void SetSingleNode(XmlNode xParent, string sNode, string sValue)
	{
		XmlUtil.SetSingleNode(this, xParent, sNode, sValue, nsmgr, sDefaultNamespace);
	}

	public void SetSingleNode(XmlNode xParent, string sNode, string sNamespaceURI, string sValue)
	{
		XmlUtil.SetSingleNode(this, xParent, sNode, sValue, nsmgr, sNamespaceURI);
	}

	public void SetSingleNode_InsertOnly(string sNode, string sValue)
	{
		XmlUtil.SetSingleNode_InsertOnly(this, sNode, sValue, nsmgr, sDefaultNamespace);
	}

	// ----------------------------------------------------------------
	// SetSingleNodeAttribute instance wrappers
	// NOTE: XmlUtil.SetSingleNodeAttribute(XmlDocument, string sNode, string sAttr, string sValue)
	//       or XmlUtil.SetSingleNodeAttribute(XmlDocument, XmlNode parent, string sAttr, string sValue)
	// ----------------------------------------------------------------
	public void SetSingleNodeAttribute(string sNode, string sAttribute, string sValue)
	{
		XmlUtil.SetSingleNodeAttribute(this, sNode, sAttribute, sValue);
	}

	public void SetSingleNodeAttribute(XmlNode xParent, string sAttribute, string sValue)
	{
		XmlUtil.SetSingleNodeAttribute(this, xParent, sAttribute, sValue);
	}

	// ================================================================
	// Constructors
	// ================================================================

	/// <summary>
	/// DI constructor — full service injection for use in .NET 10 DI container.
	/// All other constructors leave DI fields null for backward compatibility.
	/// </summary>
	public RdlDocument(
		DbProviderFactories   dbProviderFactories,
		IMemoryCache          memoryCache,
		Security              security,
		SplendidCache         splendidCache,
		Utils                 utils,
		IWebHostEnvironment   webHostEnvironment,
		IHttpContextAccessor  httpContextAccessor)
	{
		_dbProviderFactories  = dbProviderFactories;
		_memoryCache          = memoryCache;
		_security             = security;
		_splendidCache        = splendidCache;
		_utils                = utils;
		_webHostEnvironment   = webHostEnvironment;
		_httpContextAccessor  = httpContextAccessor;
		sbValidationErrors    = new StringBuilder();
		nsmgr                 = new XmlNamespaceManager(this.NameTable);
	}

	/// <summary>Parameterless constructor — DI fields will be null.</summary>
	public RdlDocument()
	{
		sbValidationErrors = new StringBuilder();
		nsmgr              = new XmlNamespaceManager(this.NameTable);
	}

	/// <summary>Constructor that creates an empty RDL document with the given report name.</summary>
	public RdlDocument(string sNAME) : this(sNAME, String.Empty, false)
	{
	}

	/// <summary>
	/// Two-argument constructor: creates an RDL document skeleton with name and author.
	/// This is the two-argument overload declared in the export schema.
	/// </summary>
	public RdlDocument(string sNAME, string sAUTHOR) : this(sNAME, sAUTHOR, false)
	{
	}

	/// <summary>
	/// Full skeleton constructor: creates an RDL document with the given name, author
	/// and optional chart-enabled flag. Uses raw XmlDocument API to avoid XmlUtil dependency.
	/// </summary>
	public RdlDocument(string sNAME, string sAUTHOR, bool bChart)
	{
		sbValidationErrors  = new StringBuilder();

		sDefaultNamespace   = "http://schemas.microsoft.com/sqlserver/reporting/2005/01/reportdefinition";
		sNamespace          = sDefaultNamespace;
		sNamespacePrefix    = "defaultns";

		this.XmlResolver    = null;

		// Build the document skeleton using raw XmlDocument API
		XmlDeclaration xDecl = this.CreateXmlDeclaration("1.0", "UTF-8", null);
		this.AppendChild(xDecl);

		// <Report xmlns="..." xmlns:rd="...">
		XmlElement xReport = this.CreateElement("Report", sDefaultNamespace);
		xReport.SetAttribute("xmlns:rd", sDesignerNamespace);
		this.AppendChild(xReport);

		nsmgr = new XmlNamespaceManager(this.NameTable);
		nsmgr.AddNamespace("defaultns", sDefaultNamespace);
		nsmgr.AddNamespace("rd", sDesignerNamespace);
		nsmgr.AddNamespace("qd", sQueryDefinitionNS);

		// <Author>
		XmlElement xAuthor = this.CreateElement("Author", sDefaultNamespace);
		xAuthor.InnerText  = sAUTHOR ?? String.Empty;
		xReport.AppendChild(xAuthor);

		// <Description>
		XmlElement xDesc = this.CreateElement("Description", sDefaultNamespace);
		xDesc.InnerText   = String.Empty;
		xReport.AppendChild(xDesc);

		// <Width>
		XmlElement xWidth = this.CreateElement("Width", sDefaultNamespace);
		xWidth.InnerText  = "7.5in";
		xReport.AppendChild(xWidth);

		// <CustomProperties/>  (placeholder for report custom properties)
		xReport.AppendChild(this.CreateElement("CustomProperties", sDefaultNamespace));

		// <DataSources><DataSource Name="SplendidCRM"><ConnectionProperties>...
		XmlElement xDataSources = this.CreateElement("DataSources", sDefaultNamespace);
		xReport.AppendChild(xDataSources);
		XmlElement xDataSource = this.CreateElement("DataSource", sDefaultNamespace);
		xDataSource.SetAttribute("Name", "SplendidCRM");
		xDataSources.AppendChild(xDataSource);
		XmlElement xConnProps = this.CreateElement("ConnectionProperties", sDefaultNamespace);
		xDataSource.AppendChild(xConnProps);
		XmlElement xProvider = this.CreateElement("DataProvider", sDefaultNamespace);
		xProvider.InnerText = "SQL";
		xConnProps.AppendChild(xProvider);
		XmlElement xConnStr = this.CreateElement("ConnectString", sDefaultNamespace);
		xConnStr.InnerText  = String.Empty;
		xConnProps.AppendChild(xConnStr);
		XmlElement xIntegSec = this.CreateElement("IntegratedSecurity", sDefaultNamespace);
		xIntegSec.InnerText = "true";
		xConnProps.AppendChild(xIntegSec);

		// <DataSets><DataSet Name="Results"><Query>...
		XmlElement xDataSets = this.CreateElement("DataSets", sDefaultNamespace);
		xReport.AppendChild(xDataSets);
		XmlElement xDataSet = this.CreateElement("DataSet", sDefaultNamespace);
		xDataSet.SetAttribute("Name", "Results");
		xDataSets.AppendChild(xDataSet);
		XmlElement xQuery = this.CreateElement("Query", sDefaultNamespace);
		xDataSet.AppendChild(xQuery);
		XmlElement xDSName = this.CreateElement("DataSourceName", sDefaultNamespace);
		xDSName.InnerText = "SplendidCRM";
		xQuery.AppendChild(xDSName);
		XmlElement xCmdType = this.CreateElement("CommandType", sDefaultNamespace);
		xCmdType.InnerText = "Text";
		xQuery.AppendChild(xCmdType);
		XmlElement xCmdText = this.CreateElement("CommandText", sDefaultNamespace);
		xCmdText.InnerText = String.Empty;
		xQuery.AppendChild(xCmdText);

		// <Fields/>
		xDataSet.AppendChild(this.CreateElement("Fields", sDefaultNamespace));

		// <Body><ReportItems>...
		XmlElement xBody = this.CreateElement("Body", sDefaultNamespace);
		xReport.AppendChild(xBody);
		XmlElement xReportItems = this.CreateElement("ReportItems", sDefaultNamespace);
		xBody.AppendChild(xReportItems);

		if (bChart)
		{
			XmlElement xChart = this.CreateElement("Chart", sDefaultNamespace);
			xChart.SetAttribute("Name", "Chart1");
			xReportItems.AppendChild(xChart);
		}
		else
		{
			XmlElement xTablix = this.CreateElement("Tablix", sDefaultNamespace);
			xTablix.SetAttribute("Name", "Tablix1");
			xReportItems.AppendChild(xTablix);
		}
	}

	// ----------------------------------------------------------------
	// Custom property access
	// ----------------------------------------------------------------
	public XmlNode GetCustomProperty(string sName)
	{
		XmlNode xCustomProperty = null;
		// NOTE: SelectNodesNS(string) calls DocumentElement.SelectNodes(sXPath, nsmgr).
		// Paths must NOT include the root "Report/" element and must use "defaultns:" prefix.
		XmlNodeList nlCustomProperties = this.SelectNodesNS("defaultns:CustomProperties/defaultns:CustomProperty");
		if (nlCustomProperties != null)
		{
			foreach (XmlNode xProp in nlCustomProperties)
			{
				XmlNode xPropName = XmlUtil.SelectNode(xProp, "defaultns:Name", nsmgr);
				if (xPropName != null && xPropName.InnerText == "crm:" + sName)
				{
					xCustomProperty = xProp;
					break;
				}
			}
		}
		return xCustomProperty;
	}

	public string GetCustomPropertyValue(string sName)
	{
		string sValue = String.Empty;
		XmlNode xCustomProperty = GetCustomProperty(sName);
		if (xCustomProperty != null)
		{
			XmlNode xValue = XmlUtil.SelectNode(xCustomProperty, "defaultns:Value", nsmgr);
			if (xValue != null)
				sValue = xValue.InnerText;
		}
		return sValue;
	}

	public void SetCustomProperty(string sName, string sValue)
	{
		// NOTE: SelectNode paths are relative to DocumentElement (the <Report> element).
		// Do NOT include "Report/" in the path.
		XmlNode xCustomProperties = this.SelectNode("CustomProperties");
		if (xCustomProperties == null)
		{
			XmlNode xReport = this.DocumentElement;
			if (xReport != null)
			{
				xCustomProperties = this.CreateElement("CustomProperties", sDefaultNamespace);
				xReport.AppendChild(xCustomProperties);
			}
		}
		if (xCustomProperties != null)
		{
			// Find existing property
			XmlNode xExistingProp = null;
			foreach (XmlNode xProp in xCustomProperties.ChildNodes)
			{
				XmlNode xPropName = XmlUtil.SelectNode(xProp, "defaultns:Name", nsmgr);
				if (xPropName != null && xPropName.InnerText == "crm:" + sName)
				{
					xExistingProp = xProp;
					break;
				}
			}
			if (xExistingProp == null)
			{
				xExistingProp = this.CreateElement("CustomProperty", sDefaultNamespace);
				xCustomProperties.AppendChild(xExistingProp);
				XmlElement xPropName = this.CreateElement("Name", sDefaultNamespace);
				xPropName.InnerText = "crm:" + sName;
				xExistingProp.AppendChild(xPropName);
			}
			XmlNode xPropValue = XmlUtil.SelectNode(xExistingProp, "defaultns:Value", nsmgr);
			if (xPropValue == null)
			{
				xPropValue = this.CreateElement("Value", sDefaultNamespace);
				xExistingProp.AppendChild(xPropValue);
			}
			xPropValue.InnerText = sValue;
		}
	}

	/// <summary>Removes a named custom property from the RDL document's CustomProperties section.</summary>
	public void RemoveCustomProperty(string sName)
	{
		XmlNode xCustomProperties = this.SelectNode("CustomProperties");
		if (xCustomProperties != null)
		{
			XmlNode xToRemove = null;
			foreach (XmlNode xProp in xCustomProperties.ChildNodes)
			{
				XmlNode xPropName = XmlUtil.SelectNode(xProp, "defaultns:Name", nsmgr);
				if (xPropName != null && xPropName.InnerText == "crm:" + sName)
				{
					xToRemove = xProp;
					break;
				}
			}
			if (xToRemove != null)
				xCustomProperties.RemoveChild(xToRemove);
		}
	}

	/// <summary>
	/// Applies a set of custom property updates from an XML string containing
	/// CustomProperty/Name + CustomProperty/Value elements.
	/// </summary>
	public void UpdateCustomProperties(string sXml)
	{
		if (Sql.IsEmptyString(sXml))
			return;
		try
		{
			XmlDocument xmlProps = new XmlDocument();
			xmlProps.XmlResolver = null;
			xmlProps.LoadXml(sXml);
			XmlNodeList nlProps = xmlProps.SelectNodes("//CustomProperty");
			if (nlProps != null)
			{
				foreach (XmlNode xProp in nlProps)
				{
					XmlNode xName  = xProp.SelectSingleNode("Name" );
					XmlNode xValue = xProp.SelectSingleNode("Value");
					if (xName != null && xValue != null)
						SetCustomProperty(xName.InnerText, xValue.InnerText);
				}
			}
		}
		catch { }
	}

	// ----------------------------------------------------------------
	// CreateDataTable — reflects RDL fields into a DataTable schema
	// NOTE: SelectNodesNS paths must NOT include the "Report/" root element.
	// ----------------------------------------------------------------
	public DataTable CreateDataTable()
	{
		DataTable dt = new DataTable();
		XmlNodeList nlFields = this.SelectNodesNS("defaultns:DataSets/defaultns:DataSet/defaultns:Fields/defaultns:Field");
		if (nlFields != null)
		{
			foreach (XmlNode xField in nlFields)
			{
				string sFieldName = SelectNodeAttribute(xField, "Name");
				if (!Sql.IsEmptyString(sFieldName) && !dt.Columns.Contains(sFieldName))
					dt.Columns.Add(sFieldName);
			}
		}
		return dt;
	}

	// ----------------------------------------------------------------
	// RdlName — convert a .NET type name to an RDL-safe type name
	// ----------------------------------------------------------------
	public static string RdlName(string sTypeName)
	{
		switch (sTypeName)
		{
			case "System.String"  :  return "String"  ;
			case "System.Boolean" :  return "Boolean" ;
			case "System.Byte"    :  return "Integer" ;
			case "System.Int16"   :  return "Integer" ;
			case "System.Int32"   :  return "Integer" ;
			case "System.Int64"   :  return "Integer" ;
			case "System.Single"  :  return "Float"   ;
			case "System.Double"  :  return "Float"   ;
			case "System.Decimal" :  return "Float"   ;
			case "System.DateTime":  return "DateTime";
			default               :  return "String"  ;
		}
	}

	// ----------------------------------------------------------------
	// CreateTextboxValue — create a Textbox ReportItem with a value expression
	// ----------------------------------------------------------------
	public XmlNode CreateTextboxValue(string sName, string sValue, string sDataType)
	{
		XmlElement xTextbox = this.CreateElement("Textbox", sDefaultNamespace);
		xTextbox.SetAttribute("Name", sName);
		XmlElement xParagraphs = this.CreateElement("Paragraphs", sDefaultNamespace);
		xTextbox.AppendChild(xParagraphs);
		XmlElement xParagraph = this.CreateElement("Paragraph", sDefaultNamespace);
		xParagraphs.AppendChild(xParagraph);
		XmlElement xTextRuns = this.CreateElement("TextRuns", sDefaultNamespace);
		xParagraph.AppendChild(xTextRuns);
		XmlElement xTextRun = this.CreateElement("TextRun", sDefaultNamespace);
		xTextRuns.AppendChild(xTextRun);
		XmlElement xVal = this.CreateElement("Value", sDefaultNamespace);
		xVal.InnerText = sValue;
		xTextRun.AppendChild(xVal);
		xTextRun.AppendChild(this.CreateElement("Style", sDefaultNamespace));
		return xTextbox;
	}

	// ----------------------------------------------------------------
	// CreateField — add a Field element to a Fields parent node
	// ----------------------------------------------------------------
	public XmlNode CreateField(XmlNode xFields, string sFieldName)
	{
		return CreateField(xFields, sFieldName, "System.String");
	}

	public XmlNode CreateField(XmlNode xFields, string sFieldName, string sDataType)
	{
		XmlElement xField = this.CreateElement("Field", sDefaultNamespace);
		xField.SetAttribute("Name", sFieldName);
		xFields.AppendChild(xField);
		XmlElement xDataField = this.CreateElement("DataField", sDefaultNamespace);
		xDataField.InnerText = sFieldName;
		xField.AppendChild(xDataField);
		// rd:TypeName uses the designer namespace
		XmlElement xTypeName = this.CreateElement("rd:TypeName", sDesignerNamespace);
		xTypeName.InnerText = sDataType;
		xField.AppendChild(xTypeName);
		return xField;
	}

	// ----------------------------------------------------------------
	// RemoveField — remove a Field by name from the DataSet
	// ----------------------------------------------------------------
	public void RemoveField(string sFieldName)
	{
		XmlNode xFields = this.SelectNode("DataSets/DataSet/Fields");
		if (xFields != null)
		{
			XmlNode xToRemove = null;
			foreach (XmlNode xField in xFields.ChildNodes)
			{
				string sAttrName = SelectNodeAttribute(xField, "Name");
				if (String.Compare(sAttrName, sFieldName, StringComparison.OrdinalIgnoreCase) == 0)
				{
					xToRemove = xField;
					break;
				}
			}
			if (xToRemove != null)
				xFields.RemoveChild(xToRemove);
		}
	}

	// ----------------------------------------------------------------
	// UpdateDataTable — rebuild DataSet Fields from a display-columns DataTable
	// ----------------------------------------------------------------
	public void UpdateDataTable(DataTable dtDisplayColumns)
	{
		if (dtDisplayColumns == null)
			return;
		XmlNode xDataSet = this.SelectNode("DataSets/DataSet");
		if (xDataSet == null)
			return;

		XmlNode xOldFields = XmlUtil.SelectNode(xDataSet, "defaultns:Fields", nsmgr);
		if (xOldFields != null)
			xDataSet.RemoveChild(xOldFields);

		XmlNode xFields = this.CreateElement("Fields", sDefaultNamespace);
		xDataSet.AppendChild(xFields);

		foreach (DataRow row in dtDisplayColumns.Rows)
		{
			string sColumnName = Sql.ToString(row["ColumnName"]);
			string sDataType   = Sql.ToString(row["DataType"  ]);
			if (!Sql.IsEmptyString(sColumnName))
				CreateField(xFields, sColumnName, Sql.IsEmptyString(sDataType) ? "System.String" : sDataType);
		}
	}

	// ----------------------------------------------------------------
	// BuildChartCommand — build SQL command for a chart dataset.
	// MIGRATED: DbProviderFactories.GetFactory(Context.Application) → _dbProviderFactories.GetFactory(_memoryCache)
	//           Security.USER_ID (static) → _security?.USER_ID (instance)
	// ----------------------------------------------------------------
	public void BuildChartCommand(HttpContext Context, IDbConnection con, IDbCommand cmd, string sModuleName, string sTableName, string sSERIES_COLUMN, string sCATEGORY_COLUMN, string sDATE_COLUMN, bool bUseSQLParameters)
	{
		// MIGRATED: Security.USER_ID (static) → _security?.USER_ID (instance)
		Guid gASSIGNED_USER_ID = (_security != null) ? _security.USER_ID : Guid.Empty;
		Guid gCURRENT_USER_ID  = gASSIGNED_USER_ID;

		StringBuilder sbSELECT  = new StringBuilder();
		StringBuilder sbFROM    = new StringBuilder();
		StringBuilder sbWHERE   = new StringBuilder();
		StringBuilder sbGROUPBY = new StringBuilder();
		StringBuilder sbORDERBY = new StringBuilder();

		sbFROM .Append("  from " + sTableName + ControlChars.CrLf);
		sbWHERE.Append(" where 1 = 1"         + ControlChars.CrLf);

		if (!Sql.IsEmptyString(sSERIES_COLUMN) && !Sql.IsEmptyString(sCATEGORY_COLUMN))
		{
			sbSELECT .Append("select " + sSERIES_COLUMN + ", " + sCATEGORY_COLUMN + ", count(*) as CNT" + ControlChars.CrLf);
			sbGROUPBY.Append(" group by " + sSERIES_COLUMN + ", " + sCATEGORY_COLUMN + ControlChars.CrLf);
			sbORDERBY.Append(" order by " + sSERIES_COLUMN + ", " + sCATEGORY_COLUMN + ControlChars.CrLf);
		}
		else if (!Sql.IsEmptyString(sSERIES_COLUMN))
		{
			sbSELECT .Append("select " + sSERIES_COLUMN + ", count(*) as CNT" + ControlChars.CrLf);
			sbGROUPBY.Append(" group by " + sSERIES_COLUMN + ControlChars.CrLf);
			sbORDERBY.Append(" order by " + sSERIES_COLUMN + ControlChars.CrLf);
		}
		else if (!Sql.IsEmptyString(sCATEGORY_COLUMN))
		{
			sbSELECT .Append("select " + sCATEGORY_COLUMN + ", count(*) as CNT" + ControlChars.CrLf);
			sbGROUPBY.Append(" group by " + sCATEGORY_COLUMN + ControlChars.CrLf);
			sbORDERBY.Append(" order by " + sCATEGORY_COLUMN + ControlChars.CrLf);
		}
		else
		{
			sbSELECT.Append("select count(*) as CNT" + ControlChars.CrLf);
		}

		if (bUseSQLParameters)
		{
			Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", gASSIGNED_USER_ID);
			Sql.AddParameter(cmd, "@CURRENT_USER_ID" , gCURRENT_USER_ID );
		}

		cmd.CommandText = sbSELECT.ToString()
			+ sbFROM   .ToString()
			+ sbWHERE  .ToString()
			+ sbGROUPBY.ToString()
			+ sbORDERBY.ToString();
		cmd.CommandType = CommandType.Text;
	}

	// ----------------------------------------------------------------
	// UpdateChartTitle — set the chart title caption
	// ----------------------------------------------------------------
	public void UpdateChartTitle(string sChartTitle)
	{
		XmlNode xChart = this.SelectNode("Body/ReportItems/Chart");
		if (xChart != null)
			XmlUtil.SetSingleNode(this, xChart, "Title/Caption", sChartTitle, nsmgr, sDefaultNamespace);
	}

	// ----------------------------------------------------------------
	// UpdateChart — update chart definition from all parameters
	// ----------------------------------------------------------------
	public void UpdateChart(string sChartTitle, string sChartType, string sSeriesTitle, string sSeriesColumn, string sSeriesOperator, string sCategoryTitle, string sCategoryColumn, string sCategoryOperator, string sDateFormat)
	{
		XmlNode xChart = this.SelectNode("Body/ReportItems/Chart");
		if (xChart == null)
			return;

		if (!Sql.IsEmptyString(sChartType))
			XmlUtil.SetSingleNode(this, xChart, "ChartType", sChartType, nsmgr, sDefaultNamespace);

		if (!Sql.IsEmptyString(sChartTitle))
			XmlUtil.SetSingleNode(this, xChart, "Title/Caption", sChartTitle, nsmgr, sDefaultNamespace);

		if (!Sql.IsEmptyString(sSeriesColumn))
		{
			// Ensure ChartData/ChartSeriesCollection/ChartSeries exists
			XmlNode xChartData = XmlUtil.SelectNode(xChart, "defaultns:ChartData", nsmgr);
			if (xChartData == null)
			{
				xChartData = this.CreateElement("ChartData", sDefaultNamespace);
				xChart.AppendChild(xChartData);
			}
			XmlNode xSeriesCollection = XmlUtil.SelectNode(xChartData, "defaultns:ChartSeriesCollection", nsmgr);
			if (xSeriesCollection == null)
			{
				xSeriesCollection = this.CreateElement("ChartSeriesCollection", sDefaultNamespace);
				xChartData.AppendChild(xSeriesCollection);
			}
			XmlNode xChartSeries = XmlUtil.SelectNode(xSeriesCollection, "defaultns:ChartSeries", nsmgr);
			if (xChartSeries == null)
			{
				xChartSeries = this.CreateElement("ChartSeries", sDefaultNamespace);
				xSeriesCollection.AppendChild(xChartSeries);
			}
			XmlUtil.SetSingleNode(this, xChartSeries, "Name", sSeriesTitle, nsmgr, sDefaultNamespace);

			// ChartDataPoints/ChartDataPoint/ChartDataPointValues/Y
			XmlNode xChartDataPoints = XmlUtil.SelectNode(xChartSeries, "defaultns:ChartDataPoints", nsmgr);
			if (xChartDataPoints == null)
			{
				xChartDataPoints = this.CreateElement("ChartDataPoints", sDefaultNamespace);
				xChartSeries.AppendChild(xChartDataPoints);
			}
			XmlNode xChartDataPoint = XmlUtil.SelectNode(xChartDataPoints, "defaultns:ChartDataPoint", nsmgr);
			if (xChartDataPoint == null)
			{
				xChartDataPoint = this.CreateElement("ChartDataPoint", sDefaultNamespace);
				xChartDataPoints.AppendChild(xChartDataPoint);
			}
			string sValueExpr = "=Fields!" + sSeriesColumn + ".Value";
			if (!Sql.IsEmptyString(sSeriesOperator))
				sValueExpr = "=" + sSeriesOperator + "(Fields!" + sSeriesColumn + ".Value)";
			XmlUtil.SetSingleNode(this, xChartDataPoint, "ChartDataPointValues/Y", sValueExpr, nsmgr, sDefaultNamespace);

			// Category axis
			if (!Sql.IsEmptyString(sCategoryColumn))
			{
				XmlNode xCategoryFields = XmlUtil.SelectNode(xChartDataPoint, "defaultns:ChartCategoryFields", nsmgr);
				if (xCategoryFields == null)
				{
					xCategoryFields = this.CreateElement("ChartCategoryFields", sDefaultNamespace);
					xChartDataPoint.AppendChild(xCategoryFields);
				}
				XmlElement xMemberField = this.CreateElement("ChartMemberField", sDefaultNamespace);
				xCategoryFields.AppendChild(xMemberField);
				string sCatExpr = "=Fields!" + sCategoryColumn + ".Value";
				XmlUtil.SetSingleNode(this, xMemberField, "Value", sCatExpr, nsmgr, sDefaultNamespace);
			}
		}
	}

	// ----------------------------------------------------------------
	// CommandText — get or set the DataSet CommandText value.
	// NOTE: XmlUtil.SelectNode correctly splits path and auto-prefixes each component;
	// XmlUtil.SelectSingleNode only works for single-level paths (auto-prefix whole string).
	// XmlUtil.SetSingleNode splits and auto-prefixes each component correctly.
	// Paths must NOT include the root element "Report/" since navigation starts from DocumentElement.
	// ----------------------------------------------------------------
	public string CommandText
	{
		get
		{
			XmlNode n = XmlUtil.SelectNode(this, "DataSets/DataSet/Query/CommandText", nsmgr);
			return n != null ? n.InnerText : String.Empty;
		}
		set { XmlUtil.SetSingleNode(this, "DataSets/DataSet/Query/CommandText", value, nsmgr, sDefaultNamespace); }
	}

	// ----------------------------------------------------------------
	// LookupDateField — find the first DateTime-typed field in the DataSet
	// NOTE: SelectNodesNS paths must NOT include "Report/" root element.
	// ----------------------------------------------------------------
	public string LookupDateField()
	{
		XmlNodeList nlFields = this.SelectNodesNS("defaultns:DataSets/defaultns:DataSet/defaultns:Fields/defaultns:Field");
		if (nlFields != null)
		{
			foreach (XmlNode xField in nlFields)
			{
				XmlNode xTypeName = XmlUtil.SelectNode(xField, "rd:TypeName", nsmgr);
				if (xTypeName != null && (xTypeName.InnerText == "System.DateTime" || xTypeName.InnerText == "DateTime"))
					return SelectNodeAttribute(xField, "Name");
			}
		}
		return String.Empty;
	}

	public string LookupDateFieldType(string sFieldName)
	{
		XmlNodeList nlFields = this.SelectNodesNS("defaultns:DataSets/defaultns:DataSet/defaultns:Fields/defaultns:Field");
		if (nlFields != null)
		{
			foreach (XmlNode xField in nlFields)
			{
				string sAttrName = SelectNodeAttribute(xField, "Name");
				if (String.Compare(sAttrName, sFieldName, StringComparison.OrdinalIgnoreCase) == 0)
				{
					XmlNode xTypeName = XmlUtil.SelectNode(xField, "rd:TypeName", nsmgr);
					if (xTypeName != null)
						return xTypeName.InnerText;
				}
			}
		}
		return "System.String";
	}

	// ----------------------------------------------------------------
	// RdlValue — encode a field name as an RDL field expression
	// ----------------------------------------------------------------
	protected static string RdlValue(string sFieldName)
	{
		return "=Fields!" + sFieldName + ".Value";
	}

	// ----------------------------------------------------------------
	// RdlParameterName — sanitize a name for use as an RDL parameter name
	// ----------------------------------------------------------------
	public static string RdlParameterName(string sName)
	{
		Regex r = new Regex(@"[^A-Za-z0-9_]");
		return r.Replace(sName, "");
	}

	// ----------------------------------------------------------------
	// RdlFieldFromParameter — convert parameter name to a field expression
	// ----------------------------------------------------------------
	public static string RdlFieldFromParameter(string sParameterName)
	{
		return "=Parameters!" + RdlParameterName(sParameterName) + ".Value";
	}

	// ----------------------------------------------------------------
	// ReportViewerFixups — post-process RDL for ReportViewer compatibility
	// ----------------------------------------------------------------
	public void ReportViewerFixups()
	{
		XmlNode xReport = this.DocumentElement;
		if (xReport != null && xReport.Attributes != null)
		{
			XmlAttribute xDrawGrid = xReport.Attributes["rd:DrawGrid"];
			if (xDrawGrid != null)
				xReport.Attributes.Remove(xDrawGrid);
		}
	}

	// ----------------------------------------------------------------
	// DbSpecificDate — generate a provider-specific SQL date literal.
	// Takes IDbConnection so we can detect provider type via Sql.IsXxx(con).
	// ----------------------------------------------------------------
	public static string DbSpecificDate(IDbConnection con, DateTime dtValue)
	{
		if (con != null && Sql.IsOracle(con))
			return "TO_DATE('" + dtValue.ToString("MM/dd/yyyy") + "', 'MM/DD/YYYY')";
		else if (con != null && Sql.IsMySQL(con))
			return "'" + dtValue.ToString("yyyy-MM-dd") + "'";
		else
			return "'" + dtValue.ToString("MM/dd/yyyy") + "'";
	}

	// ----------------------------------------------------------------
	// BuildCommand — construct the main report query SQL command.
	// MIGRATED: DbProviderFactories.GetFactory(Context.Application) → _dbProviderFactories.GetFactory(_memoryCache)
	//           Security.USER_ID (static) → _security?.USER_ID (instance)
	// ----------------------------------------------------------------
	public void BuildCommand(HttpContext Context, IDbConnection con, IDbCommand cmd, string sModuleName, string sTableName, bool bUseSQLParameters, bool bPrimaryOnly)
	{
		// MIGRATED: Security.USER_ID (static) → _security?.USER_ID (instance)
		Guid gASSIGNED_USER_ID = (_security != null) ? _security.USER_ID : Guid.Empty;
		Guid gCURRENT_USER_ID  = gASSIGNED_USER_ID;

		StringBuilder sbSELECT  = new StringBuilder();
		StringBuilder sbFROM    = new StringBuilder();
		StringBuilder sbWHERE   = new StringBuilder();
		StringBuilder sbORDERBY = new StringBuilder();

		// Read custom properties
		string sDisplayColumns = GetCustomPropertyValue("DisplayColumns");
		string sFilters        = GetCustomPropertyValue("Filters"       );
		string sRelatedModules = GetCustomPropertyValue("RelatedModules");

		// Parse display columns
		List<string> lstDisplayColumns = new List<string>();
		if (!Sql.IsEmptyString(sDisplayColumns))
		{
			try
			{
				XmlDocument xmlDisp = new XmlDocument();
				xmlDisp.XmlResolver = null;
				xmlDisp.LoadXml(sDisplayColumns);
				XmlNodeList nlFields = xmlDisp.SelectNodes("//DisplayColumn/ColumnName");
				if (nlFields != null)
					foreach (XmlNode xF in nlFields)
						lstDisplayColumns.Add(xF.InnerText);
			}
			catch { }
		}

		// Parse related modules for JOINs
		ArrayList arrJoinTables = new ArrayList();
		if (!Sql.IsEmptyString(sRelatedModules))
		{
			try
			{
				XmlDocument xmlRel = new XmlDocument();
				xmlRel.XmlResolver = null;
				xmlRel.LoadXml(sRelatedModules);
				XmlNodeList nlRel = xmlRel.SelectNodes("//Relationship/RHS_TABLE");
				if (nlRel != null)
					foreach (XmlNode xR in nlRel)
						if (!Sql.IsEmptyString(xR.InnerText))
							arrJoinTables.Add(xR.InnerText);
			}
			catch { }
		}

		// Build SELECT clause
		if (lstDisplayColumns.Count > 0)
		{
			sbSELECT.Append("select" + ControlChars.CrLf);
			for (int i = 0; i < lstDisplayColumns.Count; i++)
			{
				sbSELECT.Append((i == 0 ? "       " : "     , ") + sTableName + "." + lstDisplayColumns[i] + ControlChars.CrLf);
			}
		}
		else
		{
			sbSELECT.Append("select " + sTableName + ".*" + ControlChars.CrLf);
		}

		// Build FROM with optional JOINs
		sbFROM.Append("  from " + sTableName + ControlChars.CrLf);
		foreach (string sJoinTable in arrJoinTables)
			sbFROM.Append(" inner join " + sJoinTable + " on " + sJoinTable + ".ID = " + sTableName + ".ID" + ControlChars.CrLf);

		// Build WHERE
		sbWHERE.Append(" where 1 = 1" + ControlChars.CrLf);

		// Apply filters from custom property XML
		if (!Sql.IsEmptyString(sFilters))
		{
			try
			{
				XmlDocument xmlFilters = new XmlDocument();
				xmlFilters.XmlResolver = null;
				xmlFilters.LoadXml(sFilters);
				XmlNodeList nlFilters = xmlFilters.SelectNodes("//Filter");
				if (nlFilters != null)
				{
					foreach (XmlNode xFilter in nlFilters)
					{
						XmlNode xField  = xFilter.SelectSingleNode("DATA_FIELD" );
						XmlNode xOp     = xFilter.SelectSingleNode("OPERATOR"   );
						XmlNode xSearch = xFilter.SelectSingleNode("SEARCH_TEXT");
						if (xField != null && xOp != null && xSearch != null
							&& !Sql.IsEmptyString(xField.InnerText)
							&& !Sql.IsEmptyString(xOp.InnerText))
						{
							string sField  = xField .InnerText;
							string sOp     = xOp    .InnerText;
							string sSearch = xSearch.InnerText;
							if (sOp == "equals" || sOp == "=")
							{
								if (bUseSQLParameters)
								{
									string sParamName = "@" + RdlParameterName(sField);
									sbWHERE.Append("   and " + sTableName + "." + sField + " = " + sParamName + ControlChars.CrLf);
									Sql.AddParameter(cmd, sParamName, sSearch);
								}
								else
								{
									sbWHERE.Append("   and " + sTableName + "." + sField + " = '" + Sql.EscapeSQL(sSearch) + "'" + ControlChars.CrLf);
								}
							}
							else if (sOp == "contains" || sOp == "like")
							{
								if (bUseSQLParameters)
								{
									string sParamName = "@" + RdlParameterName(sField);
									sbWHERE.Append("   and " + sTableName + "." + sField + " like " + sParamName + ControlChars.CrLf);
									Sql.AddParameter(cmd, sParamName, "%" + sSearch + "%");
								}
								else
								{
									sbWHERE.Append("   and " + sTableName + "." + sField + " like '%" + Sql.EscapeSQL(sSearch) + "%'" + ControlChars.CrLf);
								}
							}
						}
					}
				}
			}
			catch { }
		}

		// Bind user parameters
		if (bUseSQLParameters)
		{
			Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", gASSIGNED_USER_ID);
			Sql.AddParameter(cmd, "@CURRENT_USER_ID" , gCURRENT_USER_ID );
		}

		cmd.CommandText = sbSELECT.ToString() + sbFROM.ToString() + sbWHERE.ToString() + sbORDERBY.ToString();
		cmd.CommandType = CommandType.Text;
	}

	// ----------------------------------------------------------------
	// BuildCommandQueryParameter — create a QueryParameter XML node
	// ----------------------------------------------------------------
	public XmlNode BuildCommandQueryParameter(string sName, string sValue)
	{
		XmlElement xQueryParameter = this.CreateElement("QueryParameter", sDefaultNamespace);
		xQueryParameter.SetAttribute("Name", "@" + RdlParameterName(sName));
		XmlElement xVal = this.CreateElement("Value", sDefaultNamespace);
		xVal.InnerText = sValue;
		xQueryParameter.AppendChild(xVal);
		return xQueryParameter;
	}

	// ----------------------------------------------------------------
	// AddQueryParameter — append a query parameter to the DataSet query
	// ----------------------------------------------------------------
	public void AddQueryParameter(string sName, string sValue)
	{
		XmlNode xQuery = this.SelectNode("DataSets/DataSet/Query");
		if (xQuery != null)
		{
			XmlNode xQueryParameters = XmlUtil.SelectNode(xQuery, "defaultns:QueryParameters", nsmgr);
			if (xQueryParameters == null)
			{
				xQueryParameters = this.CreateElement("QueryParameters", sDefaultNamespace);
				xQuery.AppendChild(xQueryParameters);
			}
			xQueryParameters.AppendChild(BuildCommandQueryParameter(sName, sValue));
		}
	}

	public void AddQueryParameter(XmlNode xQueryParameters, string sName, string sValue)
	{
		if (xQueryParameters != null)
			xQueryParameters.AppendChild(BuildCommandQueryParameter(sName, sValue));
	}

	// ----------------------------------------------------------------
	// AddReportParameter — add a ReportParameter element to the Report
	// ----------------------------------------------------------------
	public void AddReportParameter(string sName, string sDataType, string sDefaultValue, string sPrompt, bool bNullable)
	{
		XmlNode xReport = this.DocumentElement;
		if (xReport == null)
			return;

		XmlNode xReportParameters = XmlUtil.SelectNode(xReport, "defaultns:ReportParameters", nsmgr);
		if (xReportParameters == null)
		{
			xReportParameters = this.CreateElement("ReportParameters", sDefaultNamespace);
			xReport.AppendChild(xReportParameters);
		}

		XmlElement xReportParameter = this.CreateElement("ReportParameter", sDefaultNamespace);
		xReportParameter.SetAttribute("Name", RdlParameterName(sName));
		xReportParameters.AppendChild(xReportParameter);

		XmlUtil.SetSingleNode(this, xReportParameter, "DataType"  , sDataType                    , nsmgr, sDefaultNamespace);
		XmlUtil.SetSingleNode(this, xReportParameter, "Nullable"  , bNullable ? "true" : "false"  , nsmgr, sDefaultNamespace);
		XmlUtil.SetSingleNode(this, xReportParameter, "AllowBlank", bNullable ? "true" : "false"  , nsmgr, sDefaultNamespace);
		XmlUtil.SetSingleNode(this, xReportParameter, "Prompt"    , sPrompt                        , nsmgr, sDefaultNamespace);
		if (!Sql.IsEmptyString(sDefaultValue))
			XmlUtil.SetSingleNode(this, xReportParameter, "DefaultValue/Values/Value", sDefaultValue, nsmgr, sDefaultNamespace);
	}

	// ----------------------------------------------------------------
	// SetFiltersCustomProperty — store filter XML as a custom property
	// ----------------------------------------------------------------
	public void SetFiltersCustomProperty(Dictionary<string, object> dictFilterXml)
	{
		if (dictFilterXml == null || dictFilterXml.Count == 0)
			return;
		XmlDocument xmlFilters = new XmlDocument();
		xmlFilters.AppendChild(xmlFilters.CreateElement("Filters"));
		foreach (KeyValuePair<string, object> pair in dictFilterXml)
		{
			XmlElement xFilter = xmlFilters.CreateElement("Filter");
			xmlFilters.DocumentElement.AppendChild(xFilter);
			XmlElement xName  = xmlFilters.CreateElement("Name" );
			XmlElement xValue = xmlFilters.CreateElement("Value");
			xName .InnerText  = pair.Key;
			xValue.InnerText  = pair.Value != null ? pair.Value.ToString() : String.Empty;
			xFilter.AppendChild(xName );
			xFilter.AppendChild(xValue);
		}
		SetCustomProperty("Filters", xmlFilters.OuterXml);
	}

	// ----------------------------------------------------------------
	// SetWorkflowFiltersCustomProperty — store workflow filter XML as a custom property
	// ----------------------------------------------------------------
	public void SetWorkflowFiltersCustomProperty(string sWorkflowFilters)
	{
		SetCustomProperty("WorkflowFilters", sWorkflowFilters);
	}

	// ----------------------------------------------------------------
	// SetWorkflowAttachmentCustomProperty — store workflow attachment flag
	// ----------------------------------------------------------------
	public void SetWorkflowAttachmentCustomProperty(bool bAttachReport)
	{
		SetCustomProperty("WorkflowAttachReport", bAttachReport ? "1" : "0");
	}

	// ----------------------------------------------------------------
	// SetRelatedModuleCustomProperty — store related module name
	// ----------------------------------------------------------------
	public void SetRelatedModuleCustomProperty(string sRelatedModule)
	{
		SetCustomProperty("RelatedModule", sRelatedModule);
	}

	// ----------------------------------------------------------------
	// SetRelationshipCustomProperty — store relationship name
	// ----------------------------------------------------------------
	public void SetRelationshipCustomProperty(string sRelationshipName)
	{
		SetCustomProperty("RelationshipName", sRelationshipName);
	}

	// ----------------------------------------------------------------
	// SetDisplayColumnsCustomProperty — store display columns as custom property XML
	// ----------------------------------------------------------------
	public void SetDisplayColumnsCustomProperty(DataTable dtDisplayColumns)
	{
		if (dtDisplayColumns == null)
			return;
		XmlDocument xmlDisp = new XmlDocument();
		xmlDisp.AppendChild(xmlDisp.CreateElement("DisplayColumns"));
		foreach (DataRow row in dtDisplayColumns.Rows)
		{
			XmlElement xDisplayColumn = xmlDisp.CreateElement("DisplayColumn");
			xmlDisp.DocumentElement.AppendChild(xDisplayColumn);
			XmlElement xColumnName = xmlDisp.CreateElement("ColumnName");
			XmlElement xDataType   = xmlDisp.CreateElement("DataType"  );
			xColumnName.InnerText = Sql.ToString(row["ColumnName"]);
			xDataType  .InnerText = Sql.ToString(row["DataType"  ]);
			xDisplayColumn.AppendChild(xColumnName);
			xDisplayColumn.AppendChild(xDataType  );
		}
		SetCustomProperty("DisplayColumns", xmlDisp.OuterXml);
	}

	// ----------------------------------------------------------------
	// SetDataSetFields — sync DataSet Fields from ReportingFilterColumns cache.
	// MIGRATED: SplendidCache.ReportingFilterColumns() (static) → _splendidCache (instance)
	// ----------------------------------------------------------------
	public void SetDataSetFields(Hashtable hashAvailableModules)
	{
		string sMODULE_NAME = GetCustomPropertyValue("Module");
		if (Sql.IsEmptyString(sMODULE_NAME))
			return;

		// MIGRATED: SplendidCache.ReportingFilterColumns(sTableName) → _splendidCache?.ReportingFilterColumns(sTableName)
		DataTable dtFilterColumns = null;
		if (_splendidCache != null)
			dtFilterColumns = _splendidCache.ReportingFilterColumns(sMODULE_NAME);

		if (dtFilterColumns == null)
			return;

		XmlNode xDataSet = this.SelectNode("DataSets/DataSet");
		if (xDataSet == null)
			return;

		XmlNode xOldFields = XmlUtil.SelectNode(xDataSet, "defaultns:Fields", nsmgr);
		if (xOldFields != null)
			xDataSet.RemoveChild(xOldFields);

		XmlNode xFields = this.CreateElement("Fields", sDefaultNamespace);
		xDataSet.AppendChild(xFields);

		foreach (DataRow row in dtFilterColumns.Rows)
		{
			string sFieldName = Sql.ToString(row["NAME"  ]);
			string sDataType  = Sql.ToString(row["CsType"]);
			if (!Sql.IsEmptyString(sFieldName))
				CreateField(xFields, sFieldName, Sql.IsEmptyString(sDataType) ? "System.String" : sDataType);
		}
	}

	// ----------------------------------------------------------------
	// SetFilters — set report filter configuration from a DataTable
	// ----------------------------------------------------------------
	public void SetFilters(DataTable dtFilters, string sModuleName)
	{
		if (dtFilters == null)
			return;
		XmlDocument xmlFilters = new XmlDocument();
		xmlFilters.AppendChild(xmlFilters.CreateElement("Filters"));
		foreach (DataRow row in dtFilters.Rows)
		{
			XmlElement xFilter = xmlFilters.CreateElement("Filter");
			xmlFilters.DocumentElement.AppendChild(xFilter);
			foreach (string sF in new[] { "ID", "MODULE_NAME", "DATA_FIELD", "DATA_TYPE", "OPERATOR", "SEARCH_TEXT", "SEARCH_TEXT2" })
			{
				if (dtFilters.Columns.Contains(sF))
				{
					XmlElement xField = xmlFilters.CreateElement(sF);
					xField.InnerText  = Sql.ToString(row[sF]);
					xFilter.AppendChild(xField);
				}
			}
		}
		SetCustomProperty("Filters", xmlFilters.OuterXml);
	}

	// ----------------------------------------------------------------
	// SetRelationships — set module relationship configuration from a DataTable
	// ----------------------------------------------------------------
	public void SetRelationships(DataTable dtRelationships, string sModuleName)
	{
		if (dtRelationships == null)
			return;
		XmlDocument xmlRel = new XmlDocument();
		xmlRel.AppendChild(xmlRel.CreateElement("Relationships"));
		foreach (DataRow row in dtRelationships.Rows)
		{
			XmlElement xRel = xmlRel.CreateElement("Relationship");
			xmlRel.DocumentElement.AppendChild(xRel);
			foreach (string sF in new[] {
				"RELATIONSHIP_NAME","LHS_MODULE","LHS_TABLE","LHS_KEY",
				"RHS_MODULE","RHS_TABLE","RHS_KEY",
				"JOIN_TABLE","JOIN_KEY_LHS","JOIN_KEY_RHS",
				"RELATIONSHIP_TYPE","MODULE_NAME","DISPLAY_NAME"})
			{
				if (dtRelationships.Columns.Contains(sF))
				{
					XmlElement xField = xmlRel.CreateElement(sF);
					xField.InnerText  = Sql.ToString(row[sF]);
					xRel.AppendChild(xField);
				}
			}
		}
		SetCustomProperty("RelatedModules", xmlRel.OuterXml);
	}

	// ----------------------------------------------------------------
	// SetDisplayColumns — rebuild the DataSet Fields from display columns DataTable
	// ----------------------------------------------------------------
	public void SetDisplayColumns(DataTable dtDisplayColumns, string sModuleName)
	{
		if (dtDisplayColumns == null)
			return;
		UpdateDataTable(dtDisplayColumns);
		SetDisplayColumnsCustomProperty(dtDisplayColumns);
	}

	// ----------------------------------------------------------------
	// AppendField — convenience wrapper to append a field to default DataSet Fields
	// ----------------------------------------------------------------
	public void AppendField(string sFieldName)
	{
		XmlNode xFields = this.SelectNode("DataSets/DataSet/Fields");
		if (xFields != null)
			CreateField(xFields, sFieldName);
	}

	// ----------------------------------------------------------------
	// AppendColumnExpression — append a column expression (field) to the DataSet
	// ----------------------------------------------------------------
	public void AppendColumnExpression(string sColumnOwner, string sColumnName)
	{
		string sFullName = Sql.IsEmptyString(sColumnOwner)
			? sColumnName
			: sColumnOwner + "." + sColumnName;
		AppendField(sFullName);
	}

	// ----------------------------------------------------------------
	// GetPhysicalPath — resolve a virtual path (~/...) to a physical path.
	// MIGRATED: Context.Server.MapPath() → IWebHostEnvironment.ContentRootPath
	// ----------------------------------------------------------------
	private string GetPhysicalPath(string sVirtualPath)
	{
		string sRoot = _webHostEnvironment?.ContentRootPath ?? AppContext.BaseDirectory;
		string sRel  = sVirtualPath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
		return Path.Combine(sRoot, sRel);
	}

}  // end class RdlDocument

// ====================================================================

/// <summary>
/// RdsDocument wraps an XML Report Data Source (RDS) shared dataset file,
/// providing namespace-aware manipulation, schema validation, field management,
/// and column expression support for SplendidCRM reporting.
/// </summary>
public class RdsDocument : XmlDocument
{
	// ----------------------------------------------------------------
	// RDS namespace strings
	// ----------------------------------------------------------------
	protected string sDefaultNamespace         = String.Empty;
	protected string sDesignerNamespace        = "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";
	protected string sQueryDefinitionNS        = "http://schemas.microsoft.com/sql/2005/08/sql/query";
	protected string sQueryDefinitionNamespace = "http://schemas.microsoft.com/sql/2005/08/sql/query";

	// ----------------------------------------------------------------
	// DI-injected services
	// ----------------------------------------------------------------
	protected DbProviderFactories   _dbProviderFactories;
	protected IMemoryCache          _memoryCache;
	protected Security              _security;
	protected Utils                 _utils;
	protected IWebHostEnvironment   _webHostEnvironment;
	protected IHttpContextAccessor  _httpContextAccessor;

	// ----------------------------------------------------------------
	// Namespace manager and validation errors
	// ----------------------------------------------------------------
	protected XmlNamespaceManager nsmgr;
	protected StringBuilder sbValidationErrors;

	public XmlNamespaceManager NamespaceManager
	{
		get { return nsmgr; }
	}

	// ----------------------------------------------------------------
	// Validation event handler
	// ----------------------------------------------------------------
	private void ValidationHandler(object sender, ValidationEventArgs e)
	{
		sbValidationErrors.AppendLine(e.Message);
	}

	// ----------------------------------------------------------------
	// Validate — validate the RDS document against the schema.
	// MIGRATED: Context.Server.MapPath() → GetPhysicalPath()
	// ----------------------------------------------------------------
	public bool Validate(HttpContext Context)
	{
		bool bValid = false;
		try
		{
			sbValidationErrors = new StringBuilder();

			const string sSchema2016 = "~/Reports/RDL 2016 ReportDefinition.xsd";
			const string sSchemaRdl  = "~/Reports/rdl.xsd";
			const string sSchemaRdl20= "~/Reports/rdl20.xsd";
			const string sSchemaRdl28= "~/Reports/rdl28.xsd";

			string sSchemaVirtual = null;
			if (FileExistsVirtual(sSchema2016))       sSchemaVirtual = sSchema2016;
			else if (FileExistsVirtual(sSchemaRdl))   sSchemaVirtual = sSchemaRdl;
			else if (FileExistsVirtual(sSchemaRdl20)) sSchemaVirtual = sSchemaRdl20;
			else if (FileExistsVirtual(sSchemaRdl28)) sSchemaVirtual = sSchemaRdl28;

			if (sSchemaVirtual == null)
				return true;

			string sSchemaFile = GetPhysicalPath(sSchemaVirtual);
			using (FileStream stmSchema = File.Open(sSchemaFile, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				XmlSchema schema = XmlSchema.Read(stmSchema, ValidationHandler);
				XmlSchemaSet schemaSet = new XmlSchemaSet();
				schemaSet.Add(schema);
				this.Schemas = schemaSet;
				this.Validate(ValidationHandler);
			}
			bValid = sbValidationErrors.Length == 0;
		}
		catch(Exception ex)
		{
			SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
		}
		return bValid;
	}

	private bool FileExistsVirtual(string sVirtualPath)
	{
		if (_utils != null)
			return _utils.CachedFileExists(null, sVirtualPath);
		return File.Exists(GetPhysicalPath(sVirtualPath));
	}

	// ----------------------------------------------------------------
	// LoadRds — load a raw RDS XML string
	// ----------------------------------------------------------------
	public void LoadRds(string sRDS)
	{
		this.XmlResolver = null;
		this.LoadXml(sRDS);

		if (this.DocumentElement != null)
		{
			sDefaultNamespace = this.DocumentElement.NamespaceURI;
			nsmgr = new XmlNamespaceManager(this.NameTable);
			nsmgr.AddNamespace("defaultns", sDefaultNamespace);
			nsmgr.AddNamespace("rd", sDesignerNamespace);
			nsmgr.AddNamespace("qd", sQueryDefinitionNS);
		}
	}

	// ----------------------------------------------------------------
	// XML helper methods
	// ----------------------------------------------------------------
	public XmlNode SelectNode(string sNode)
	{
		return XmlUtil.SelectNode(this, sNode, nsmgr);
	}

	/// <summary>
	/// SelectSingleNode — returns the string value of the first matching node.
	/// Uses 'new' keyword to shadow XmlDocument.SelectSingleNode(string) which returns XmlNode.
	/// This overload returns the inner text of the matched node.
	/// </summary>
	public new string SelectSingleNode(string sNode)
	{
		return XmlUtil.SelectSingleNode(this, sNode, nsmgr);
	}

	// SelectNodeValue — returns the inner text of the first node matching sNode.
	// Uses XmlUtil.SelectNode which correctly splits and auto-prefixes each path component.
	public string SelectNodeValue(string sNode)
	{
		XmlNode n = XmlUtil.SelectNode(this, sNode, nsmgr);
		return n != null ? n.InnerText : String.Empty;
	}

	// SelectNodeValue(XmlNode parent, ...) — node-relative version using SelectSingleNode
	public string SelectNodeValue(XmlNode xParent, string sNode)
	{
		return XmlUtil.SelectSingleNode(xParent, sNode, nsmgr);
	}

	public string SelectNodeAttribute(XmlNode xParent, string sAttribute)
	{
		string sValue = String.Empty;
		if (xParent != null && xParent.Attributes != null)
		{
			XmlAttribute xAttr = xParent.Attributes[sAttribute];
			if (xAttr != null)
				sValue = xAttr.Value;
		}
		return sValue;
	}

	public XmlNodeList SelectNodesNS(string sNode)
	{
		return this.DocumentElement != null ? this.DocumentElement.SelectNodes(sNode, nsmgr) : null;
	}

	public XmlNodeList SelectNodesNS(XmlNode xParent, string sNode)
	{
		return xParent != null ? xParent.SelectNodes(sNode, nsmgr) : null;
	}

	// ----------------------------------------------------------------
	// SetSingleNode instance wrappers (signature: XmlDocument first, then XmlNode)
	// ----------------------------------------------------------------
	public void SetSingleNode(string sNode, string sValue)
	{
		XmlUtil.SetSingleNode(this, sNode, sValue, nsmgr, sDefaultNamespace);
	}

	public void SetSingleNode(XmlNode xParent, string sNode, string sValue)
	{
		XmlUtil.SetSingleNode(this, xParent, sNode, sValue, nsmgr, sDefaultNamespace);
	}

	// ----------------------------------------------------------------
	// SetSingleNodeAttribute instance wrappers
	// ----------------------------------------------------------------
	public void SetSingleNodeAttribute(string sNode, string sAttribute, string sValue)
	{
		XmlUtil.SetSingleNodeAttribute(this, sNode, sAttribute, sValue);
	}

	public void SetSingleNodeAttribute(XmlNode xParent, string sAttribute, string sValue)
	{
		XmlUtil.SetSingleNodeAttribute(this, xParent, sAttribute, sValue);
	}

	// ----------------------------------------------------------------
	// RdlName — convert .NET type to RDL type name
	// ----------------------------------------------------------------
	public static string RdlName(string sTypeName)
	{
		switch (sTypeName)
		{
			case "System.String"  :  return "String"  ;
			case "System.Boolean" :  return "Boolean" ;
			case "System.Byte"    :  return "Integer" ;
			case "System.Int16"   :  return "Integer" ;
			case "System.Int32"   :  return "Integer" ;
			case "System.Int64"   :  return "Integer" ;
			case "System.Single"  :  return "Float"   ;
			case "System.Double"  :  return "Float"   ;
			case "System.Decimal" :  return "Float"   ;
			case "System.DateTime":  return "DateTime";
			default               :  return "String"  ;
		}
	}

	// ================================================================
	// Constructors
	// ================================================================

	/// <summary>DI constructor — full service injection for .NET 10 DI container.</summary>
	public RdsDocument(
		DbProviderFactories   dbProviderFactories,
		IMemoryCache          memoryCache,
		Security              security,
		Utils                 utils,
		IWebHostEnvironment   webHostEnvironment,
		IHttpContextAccessor  httpContextAccessor)
	{
		_dbProviderFactories  = dbProviderFactories;
		_memoryCache          = memoryCache;
		_security             = security;
		_utils                = utils;
		_webHostEnvironment   = webHostEnvironment;
		_httpContextAccessor  = httpContextAccessor;
		sbValidationErrors    = new StringBuilder();
		nsmgr                 = new XmlNamespaceManager(this.NameTable);
	}

	/// <summary>Parameterless constructor — DI fields will be null.</summary>
	public RdsDocument()
	{
		sbValidationErrors = new StringBuilder();
		nsmgr              = new XmlNamespaceManager(this.NameTable);
	}

	/// <summary>Constructor that creates a shared dataset document skeleton.</summary>
	public RdsDocument(string sNAME)
	{
		sbValidationErrors = new StringBuilder();

		sDefaultNamespace  = "http://schemas.microsoft.com/sqlserver/reporting/2010/01/shareddatasetdefinition";

		this.XmlResolver   = null;

		XmlDeclaration xDecl = this.CreateXmlDeclaration("1.0", "UTF-8", null);
		this.AppendChild(xDecl);

		// <SharedDataSet xmlns="..." xmlns:rd="..." xmlns:qd="...">
		XmlElement xSharedDataSet = this.CreateElement("SharedDataSet", sDefaultNamespace);
		xSharedDataSet.SetAttribute("xmlns:rd", sDesignerNamespace);
		xSharedDataSet.SetAttribute("xmlns:qd", sQueryDefinitionNS);
		this.AppendChild(xSharedDataSet);

		nsmgr = new XmlNamespaceManager(this.NameTable);
		nsmgr.AddNamespace("defaultns", sDefaultNamespace);
		nsmgr.AddNamespace("rd", sDesignerNamespace);
		nsmgr.AddNamespace("qd", sQueryDefinitionNS);

		// <DataSet Name="...">
		XmlElement xDataSet = this.CreateElement("DataSet", sDefaultNamespace);
		xDataSet.SetAttribute("Name", sNAME);
		xSharedDataSet.AppendChild(xDataSet);

		// <Query>
		XmlElement xQuery = this.CreateElement("Query", sDefaultNamespace);
		xDataSet.AppendChild(xQuery);
		XmlElement xDSRef = this.CreateElement("DataSourceReference", sDefaultNamespace);
		xDSRef.InnerText  = "SplendidCRM";
		xQuery.AppendChild(xDSRef);
		XmlElement xCmdType = this.CreateElement("CommandType", sDefaultNamespace);
		xCmdType.InnerText  = "Text";
		xQuery.AppendChild(xCmdType);
		XmlElement xCmdText = this.CreateElement("CommandText", sDefaultNamespace);
		xCmdText.InnerText  = String.Empty;
		xQuery.AppendChild(xCmdText);

		// <rd:DesignerState><qd:QueryDefinition><qd:SelectedColumns/>
		XmlElement xDesignerState = this.CreateElement("rd:DesignerState", sDesignerNamespace);
		xQuery.AppendChild(xDesignerState);
		XmlElement xQueryDef = this.CreateElement("qd:QueryDefinition", sQueryDefinitionNS);
		xDesignerState.AppendChild(xQueryDef);
		xQueryDef.AppendChild(this.CreateElement("qd:SelectedColumns", sQueryDefinitionNS));

		// <Fields/>
		xDataSet.AppendChild(this.CreateElement("Fields", sDefaultNamespace));
	}

	/// <summary>
	/// Two-argument constructor: creates a shared dataset with a specific DataSourceReference.
	/// This is the two-argument overload declared in the export schema.
	/// </summary>
	public RdsDocument(string sNAME, string sDataSourceReference) : this(sNAME)
	{
		if (!Sql.IsEmptyString(sDataSourceReference))
		{
			XmlNode xDSRef = this.SelectNode("DataSet/Query/DataSourceReference");
			if (xDSRef != null)
				xDSRef.InnerText = sDataSourceReference;
		}
	}

	// ----------------------------------------------------------------
	// AppendField — add a Field element to a Fields parent node
	// ----------------------------------------------------------------
	public XmlNode AppendField(XmlNode xFields, string sFieldName, string sFieldType)
	{
		XmlElement xField = this.CreateElement("Field", sDefaultNamespace);
		xField.SetAttribute("Name", sFieldName);
		xFields.AppendChild(xField);
		XmlElement xDataField = this.CreateElement("DataField", sDefaultNamespace);
		xDataField.InnerText  = sFieldName;
		xField.AppendChild(xDataField);
		XmlElement xTypeName = this.CreateElement("rd:TypeName", sDesignerNamespace);
		xTypeName.InnerText  = sFieldType;
		xField.AppendChild(xTypeName);
		return xField;
	}

	/// <summary>
	/// Append a field with the given name using default type System.String.
	/// Satisfies export schema AppendField(string).
	/// </summary>
	public void AppendField(string sFieldName)
	{
		AppendField(sFieldName, "System.String");
	}

	public void AppendField(string sFieldName, string sFieldType)
	{
		XmlNode xFields = this.SelectNode("DataSet/Fields");
		if (xFields != null)
			AppendField(xFields, sFieldName, sFieldType);
	}

	// ----------------------------------------------------------------
	// AppendColumnExpression — add a qd:ColumnExpression to SelectedColumns
	// ----------------------------------------------------------------
	public void AppendColumnExpression(XmlNode xSelectedColumns, string sColumnOwner, string sColumnName)
	{
		XmlElement xColumnExpression = this.CreateElement("qd:ColumnExpression", sQueryDefinitionNamespace);
		xSelectedColumns.AppendChild(xColumnExpression);
		xColumnExpression.SetAttribute("ColumnOwner", sColumnOwner);
		xColumnExpression.SetAttribute("ColumnName" , sColumnName );
	}

	/// <summary>
	/// Append a column expression to the default DesignerState/SelectedColumns node.
	/// Satisfies export schema AppendColumnExpression(string, string).
	/// </summary>
	public void AppendColumnExpression(string sColumnOwner, string sColumnName)
	{
		XmlNode xDesignerState   = this.SelectNode("DataSet/Query/rd:DesignerState");
		if (xDesignerState != null)
		{
			XmlNode xSelectedColumns = XmlUtil.SelectNode(xDesignerState, "qd:QueryDefinition/qd:SelectedColumns", nsmgr);
			if (xSelectedColumns != null)
				AppendColumnExpression(xSelectedColumns, sColumnOwner, sColumnName);
		}
	}

	// ----------------------------------------------------------------
	// GetPhysicalPath — resolve virtual path to physical path.
	// MIGRATED: Context.Server.MapPath() → IWebHostEnvironment.ContentRootPath
	// ----------------------------------------------------------------
	private string GetPhysicalPath(string sVirtualPath)
	{
		string sRoot = _webHostEnvironment?.ContentRootPath ?? AppContext.BaseDirectory;
		string sRel  = sVirtualPath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
		return Path.Combine(sRoot, sRel);
	}

}  // end class RdsDocument

// ====================================================================

/// <summary>
/// RdlUtil provides static encoding and column-name utilities for RDL/RDS report processing.
/// MIGRATED: HttpUtility.HtmlEncode() → System.Net.WebUtility.HtmlEncode() (cross-platform .NET 10)
/// </summary>
public partial class RdlUtil
{
	/// <summary>
	/// Encode an RdlDocument's XML content as an HTML-safe display string.
	/// MIGRATED: HttpUtility.HtmlEncode → WebUtility.HtmlEncode (cross-platform .NET 10).
	/// </summary>
	public static string RdlEncode(RdlDocument rdl)
	{
		string sDump = rdl.OuterXml;
		// MIGRATED: HttpUtility.HtmlEncode → WebUtility.HtmlEncode
		sDump = WebUtility.HtmlEncode(sDump);
		sDump = sDump.Replace("\n", "<br />\n");
		sDump = sDump.Replace("\t", "&nbsp;&nbsp;&nbsp;");
		sDump = "<div style=\"width: 100%; border: 1px solid black; font-family: courier new;\">" + sDump + "</div>";
		return sDump;
	}

	/// <summary>
	/// Encode an arbitrary XML/RDL string as an HTML-safe display string.
	/// Satisfies export schema requirement RdlEncode(string).
	/// </summary>
	public static string RdlEncode(string sXml)
	{
		if (sXml == null)
			return String.Empty;
		// MIGRATED: HttpUtility.HtmlEncode → WebUtility.HtmlEncode
		string sDump = WebUtility.HtmlEncode(sXml);
		sDump = sDump.Replace("\n", "<br />\n");
		sDump = sDump.Replace("\t", "&nbsp;&nbsp;&nbsp;");
		sDump = "<div style=\"width: 100%; border: 1px solid black; font-family: courier new;\">" + sDump + "</div>";
		return sDump;
	}

	/// <summary>
	/// Sanitize a column name for use as an RDL field name.
	/// Removes any character that is not alphanumeric, underscore, or period.
	/// </summary>
	public static string ReportColumnName(string sColumnName)
	{
		Regex r = new Regex(@"[^A-Za-z0-9_\.]");
		return r.Replace(sColumnName, "");
	}

	/// <summary>
	/// Sanitize the column name from a DataRow's "NAME" field.
	/// Satisfies export schema requirement ReportColumnName(DataRow).
	/// </summary>
	public static string ReportColumnName(DataRow row)
	{
		string sColumnName = Sql.ToString(row["NAME"]);
		return ReportColumnName(sColumnName);
	}

}  // end class RdlUtil

}  // end namespace SplendidCRM
