/*
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * Any use of the contents of this file are subject to the SplendidCRM Community Source Code License
 * Agreement, or other written agreement between you and SplendidCRM ("License"). By installing or
 * using this file, you have unconditionally agreed to the terms and conditions of the License,
 * including but not limited to restrictions on the number of users therein, and you may not use this
 * file except in compliance with the License. You may obtain a copy of the License by contacting
 * SplendidCRM Software, Inc. See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Migrated from .NET Framework 4.8 to .NET 10 ASP.NET Core.
 * Key changes:
 *   - System.Web.HttpContext.Current   → IHttpContextAccessor (DI constructor injection)
 *   - HttpContext.Current.Application[]→ IMemoryCache (DI)
 *   - System.Data.SqlClient            → Microsoft.Data.SqlClient
 *   - ConfigurationManager.AppSettings → IConfiguration (DI)
 *   - System.Web.HttpUtility.HtmlEncode→ System.Net.WebUtility.HtmlEncode
 *   - TwilioManager.SendText           → direct Twilio SDK (TwilioClient + MessageResource)
 *   - SplendidError.SystemMessage      → called as static (no instance needed)
 *   - ModuleUtils.Notes.Attachment.WriteStream(id, con, bw)  — needs IDbConnection + BinaryWriter
 */
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Data.Common;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Mail;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit;
using MailKit.Net.Pop3;
using MailKit.Net.Imap;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SplendidCRM
{
	public class EmailUtils
	{
		// ------------------------------------------------------------------
		// Reentrancy guards — preserved exactly from original Global.asax.cs
		// and EmailUtils.cs. Volatile for safe cross-thread visibility.
		// ------------------------------------------------------------------
		public static volatile bool bInsideSendQueue           = false;
		public static volatile bool bInsideCheckInbound        = false;
		public static volatile bool bInsideCheckOutbound       = false;
		public static volatile bool bInsideActivityReminder    = false;
		public static volatile bool bInsideSmsActivityReminder = false;
		public static          Guid CAMPAIGN_MANAGER_ID        = Guid.Empty;

		// ------------------------------------------------------------------
		// DI-injected services (replacing static HttpContext.Current / Application[])
		// ------------------------------------------------------------------
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache;
		private readonly IConfiguration       _configuration;
		private readonly DbProviderFactories  _dbProviderFactories;
		private readonly Security             _security;
		private readonly SplendidCache        _splendidCache;
		private readonly Utils                _utils;
		private readonly GoogleApps           _googleApps;
		private readonly ILogger<EmailUtils>  _logger;

		public EmailUtils(
			IHttpContextAccessor  httpContextAccessor,
			IMemoryCache          memoryCache,
			IConfiguration        configuration,
			DbProviderFactories   dbProviderFactories,
			Security              security,
			SplendidCache         splendidCache,
			Utils                 utils,
			GoogleApps            googleApps,
			ILogger<EmailUtils>   logger)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_configuration       = configuration;
			_dbProviderFactories = dbProviderFactories;
			_security            = security;
			_splendidCache       = splendidCache;
			_utils               = utils;
			_googleApps          = googleApps;
			_logger              = logger;
		}

		// ------------------------------------------------------------------
		// ValidCampaignManagerSettings
		// Returns true when sufficient SMTP/OAuth settings exist to send
		// campaign emails.
		// ------------------------------------------------------------------
		public bool ValidCampaignManagerSettings()
		{
			string sMailSendType = Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_sendtype"      ));
			string sMailServer   = Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_smtpserver"    ));
			string sFromAddress  = Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_from_address"  ));
			bool   bOffice365    = sMailSendType == "Office365";
			bool   bGoogleApps   = sMailSendType == "GoogleApps";
			if ( bOffice365 || bGoogleApps )
				return !Sql.IsEmptyString(sFromAddress);
			return !Sql.IsEmptyString(sMailServer) && !Sql.IsEmptyString(sFromAddress);
		}

		// ------------------------------------------------------------------
		// FormatEmailDisplayName  — pure static, no dependencies
		// ------------------------------------------------------------------
		public static string FormatEmailDisplayName(string sFirst, string sLast)
		{
			return (sFirst + " " + sLast).Trim();
		}

		// ------------------------------------------------------------------
		// IsValidEmail  — pure static RFC-5322 simplified validation
		// ------------------------------------------------------------------
		public static bool IsValidEmail(string sEmail)
		{
			if ( Sql.IsEmptyString(sEmail) )
				return false;
			return Regex.IsMatch(sEmail.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
		}

		// ------------------------------------------------------------------
		// XssFilter  — strip dangerous HTML from user-supplied input
		// ------------------------------------------------------------------
		public static string XssFilter(string sValue, string sType)
		{
			if ( Sql.IsEmptyString(sValue) )
				return sValue;
			sValue = Regex.Replace(sValue, @"<script[^>]*?>.*?</script>", String.Empty,
			         RegexOptions.IgnoreCase | RegexOptions.Singleline);
			sValue = Regex.Replace(sValue, @"<script[^>]*?/>",            String.Empty, RegexOptions.IgnoreCase);
			sValue = Regex.Replace(sValue, @"on\w+\s*=\s*""[^""]*""",    String.Empty, RegexOptions.IgnoreCase);
			sValue = Regex.Replace(sValue, @"on\w+\s*=\s*'[^']*'",       String.Empty, RegexOptions.IgnoreCase);
			sValue = Regex.Replace(sValue, @"javascript:",                String.Empty, RegexOptions.IgnoreCase);
			return sValue;
		}

		// ------------------------------------------------------------------
		// SplitMailAddress  — parse "Display Name <email@example.com>"
		// Returns string[2]: [0]=display name, [1]=email address
		// ------------------------------------------------------------------
		public static string[] SplitMailAddress(string sAddress)
		{
			string[] arr = new string[2];
			if ( !Sql.IsEmptyString(sAddress) )
			{
				sAddress = sAddress.Trim();
				Match m = Regex.Match(sAddress, @"^(.*?)\s*<([^>]+)>\s*$");
				if ( m.Success )
				{
					arr[0] = m.Groups[1].Value.Trim().Trim('"');
					arr[1] = m.Groups[2].Value.Trim();
				}
				else
				{
					arr[0] = String.Empty;
					arr[1] = sAddress;
				}
			}
			return arr;
		}

		// ------------------------------------------------------------------
		// SortedTableColumns  — returns DataView sorted by column-name length
		// descending so longer names are substituted first in templates.
		// ------------------------------------------------------------------
		public static DataView SortedTableColumns(DataTable dt)
		{
			if ( dt == null ) return new DataView();
			DataTable dtSort = new DataTable();
			dtSort.Columns.Add("COLUMN_NAME", typeof(string));
			dtSort.Columns.Add("COL_LEN",     typeof(int   ));
			foreach ( DataColumn col in dt.Columns )
			{
				DataRow r = dtSort.NewRow();
				r["COLUMN_NAME"] = col.ColumnName;
				r["COL_LEN"    ] = col.ColumnName.Length;
				dtSort.Rows.Add(r);
			}
			DataView dv = new DataView(dtSort);
			dv.Sort = "COL_LEN DESC";
			return dv;
		}

		// ------------------------------------------------------------------
		// CurrencyColumns  — find _usdollar columns in a sorted column view
		// Returns Hashtable keyed on column name for quick lookup.
		// ------------------------------------------------------------------
		public static Hashtable CurrencyColumns(DataView dvColumns)
		{
			Hashtable ht = new Hashtable(StringComparer.OrdinalIgnoreCase);
			if ( dvColumns == null ) return ht;
			foreach ( DataRowView drv in dvColumns )
			{
				string sCol = Sql.ToString(drv["COLUMN_NAME"]);
				if ( sCol.EndsWith("_usdollar", StringComparison.OrdinalIgnoreCase) )
					ht[sCol] = true;
			}
			return ht;
		}

		// ------------------------------------------------------------------
		// NormalizeDescription  — normalise line endings for plain-text email
		// ------------------------------------------------------------------
		public static string NormalizeDescription(string sValue)
		{
			if ( Sql.IsEmptyString(sValue) ) return sValue;
			return sValue.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
		}

		// ------------------------------------------------------------------
		// EnumColumns  — returns list of enum column names for a module table
		// ------------------------------------------------------------------
		public string[] EnumColumns(string sModuleTableName)
		{
			List<string> lst = new List<string>();
			DataTable dtColumns = _splendidCache.ReportingFilterColumns(sModuleTableName);
			if ( dtColumns != null )
			{
				foreach ( DataRow row in dtColumns.Rows )
				{
					string sListName = Sql.ToString(row["LIST_NAME"]);
					if ( !Sql.IsEmptyString(sListName) )
						lst.Add(Sql.ToString(row["COLUMN_NAME"]));
				}
			}
			return lst.ToArray();
		}

		// ------------------------------------------------------------------
		// FillEmail (DataRow overload)
		// Replace {COLUMN_NAME} template variables in sSubject / sBody with
		// the corresponding values from row, translating enum values via L10N.
		// ------------------------------------------------------------------
		public void FillEmail(string sModuleName, string sModuleTable, DataRow row, DataTable dtTemplates,
		                      ref string sSubject, ref string sBody)
		{
			if ( row == null ) return;
			string   sCultureName  = SplendidDefaults.Culture(_memoryCache);
			DataView dvCols        = SortedTableColumns(row.Table);

			// Build column → list-name lookup from the module's reporting filter columns
			DataTable dtReportCols = _splendidCache.ReportingFilterColumns(sModuleTable);
			var enumListNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if ( dtReportCols != null
			  && dtReportCols.Columns.Contains("COLUMN_NAME")
			  && dtReportCols.Columns.Contains("LIST_NAME") )
			{
				foreach ( DataRow rCol in dtReportCols.Rows )
				{
					string sColName  = Sql.ToString(rCol["COLUMN_NAME"]);
					string sListName = Sql.ToString(rCol["LIST_NAME"  ]);
					if ( !Sql.IsEmptyString(sColName) && !Sql.IsEmptyString(sListName) )
						enumListNames[sColName] = sListName;
				}
			}

			foreach ( DataRowView drv in dvCols )
			{
				string sColumn = Sql.ToString(drv["COLUMN_NAME"]);
				if ( !row.Table.Columns.Contains(sColumn) ) continue;
				object oValue  = row[sColumn];
				string sValue;
				if ( enumListNames.TryGetValue(sColumn, out string sListName2) )
				{
					sValue = L10N.Term(_memoryCache, sCultureName, "." + sListName2 + "." + Sql.ToString(oValue));
				}
				else
				{
					sValue = Sql.ToString(oValue);
				}
				sSubject = sSubject.Replace("{" + sColumn + "}", sValue);
				sBody    = sBody   .Replace("{" + sColumn + "}", sValue);
			}
		}

		// ------------------------------------------------------------------
		// FillEmail (DataRowView overload)
		// ------------------------------------------------------------------
		public void FillEmail(string sModuleName, string sModuleTable, DataRowView drv, DataTable dtTemplates,
		                      ref string sSubject, ref string sBody)
		{
			if ( drv == null ) return;
			FillEmail(sModuleName, sModuleTable, drv.Row, dtTemplates, ref sSubject, ref sBody);
		}

		// ------------------------------------------------------------------
		// CampaignTrackers  — load tracker records for a campaign
		// ------------------------------------------------------------------
		public DataTable CampaignTrackers(Guid gCAMPAIGN_ID)
		{
			DataTable dt  = new DataTable();
			DbProviderFactory dbf = _dbProviderFactories.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				string sSQL = "select *"                    + ControlChars.CrLf
				            + "  from vwCAMPAIGN_TRACKERS"  + ControlChars.CrLf
				            + " where 1 = 1"                + ControlChars.CrLf;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.CommandText = sSQL;
					var sbWhere = new StringBuilder();
					Sql.AppendParameter(cmd, sbWhere, "CAMPAIGN_ID", gCAMPAIGN_ID);
					cmd.CommandText += sbWhere.ToString();
					DbDataAdapter da = dbf.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					da.Fill(dt);
				}
			}
			return dt;
		}

		// ------------------------------------------------------------------
		// EmailTemplateAttachments  — load attachment rows for an email template
		// ------------------------------------------------------------------
		public DataTable EmailTemplateAttachments(Guid gEMAIL_TEMPLATE_ID)
		{
			DataTable dt  = new DataTable();
			DbProviderFactory dbf = _dbProviderFactories.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				string sSQL = "select *"                                  + ControlChars.CrLf
				            + "  from vwEMAIL_TEMPLATES_ATTACHMENT_List"  + ControlChars.CrLf
				            + " where 1 = 1"                              + ControlChars.CrLf;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.CommandText = sSQL;
					var sbWhere = new StringBuilder();
					Sql.AppendParameter(cmd, sbWhere, "EMAIL_TEMPLATE_ID", gEMAIL_TEMPLATE_ID);
					cmd.CommandText += sbWhere.ToString();
					DbDataAdapter da = dbf.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					da.Fill(dt);
				}
			}
			return dt;
		}

		// ------------------------------------------------------------------
		// FillTrackers
		// Replace tracker-key placeholders in the email body and fix href
		// attributes to point at the campaign tracker controller endpoint.
		// (WebUtility.HtmlEncode replaces legacy System.Web.HttpUtility.HtmlEncode)
		// ------------------------------------------------------------------
		public string FillTrackers(string sBody, DataTable dtTrackers, string sSITE_URL, Guid gCONTACT_ID)
		{
			if ( Sql.IsEmptyString(sBody) || dtTrackers == null || dtTrackers.Rows.Count == 0 )
				return sBody;
			foreach ( DataRow row in dtTrackers.Rows )
			{
				string sTrackerKey  = Sql.ToString(row["TRACKER_KEY" ]);
				string sTrackerName = Sql.ToString(row["NAME"        ]);
				Guid   gTrackerID   = Sql.ToGuid  (row["ID"          ]);
				string sTrackerURL  = sSITE_URL + "campaign_trackerv2.aspx"
				                    + "?track=" + gTrackerID.ToString()
				                    + "&contact_id=" + gCONTACT_ID.ToString();
				if ( !Sql.IsEmptyString(sTrackerKey) )
					sBody = sBody.Replace(sTrackerKey, sTrackerURL);
				if ( !Sql.IsEmptyString(sTrackerName) )
					sBody = Regex.Replace(sBody,
					        @"href=""" + Regex.Escape(sTrackerName) + @"""",
					        "href=\"" + sTrackerURL + "\"",
					        RegexOptions.IgnoreCase);
			}
			return sBody;
		}

		// ------------------------------------------------------------------
		// BuildMailClient  — helper that selects the correct mail transport
		// from per-email/campaign settings then falls back to global config.
		// ------------------------------------------------------------------
		private SplendidMailClient BuildMailClient(string sMAIL_SENDTYPE, string sMAIL_SMTPSERVER,
		                                           int nMAIL_SMTPPORT, bool bMAIL_SMTPAUTH_REQ, bool bMAIL_SMTPSSL,
		                                           string sMAIL_SMTPUSER, string sMAIL_SMTPPASS,
		                                           Guid gOAUTH_TOKEN_ID)
		{
			if ( Sql.IsEmptyString(sMAIL_SENDTYPE) )
				sMAIL_SENDTYPE = Sql.ToString(_memoryCache.Get<object>("CONFIG.mail_sendtype"));
			// Fallback to global SMTP settings when per-record values are absent
			if ( Sql.IsEmptyString(sMAIL_SMTPSERVER) )
			{
				sMAIL_SMTPSERVER   = Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_smtpserver"   ));
				nMAIL_SMTPPORT     = Sql.ToInteger(_memoryCache.Get<object>("CONFIG.mail_smtpport"     ));
				bMAIL_SMTPAUTH_REQ = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.mail_smtpauth_req" ));
				bMAIL_SMTPSSL      = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.mail_smtpssl"      ));
				sMAIL_SMTPUSER     = Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_smtpuser"     ));
				sMAIL_SMTPPASS     = Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_smtppass"     ));
			}
			if ( Sql.IsEmptyGuid(gOAUTH_TOKEN_ID) )
			{
				if ( sMAIL_SENDTYPE == "Office365" )
					gOAUTH_TOKEN_ID = Sql.ToGuid(_memoryCache.Get<object>("CONFIG.mail_sendtype_office365_token"));
				else if ( sMAIL_SENDTYPE == "GoogleApps" )
					gOAUTH_TOKEN_ID = Sql.ToGuid(_memoryCache.Get<object>("CONFIG.mail_sendtype_googleapps_token"));
			}
			if ( nMAIL_SMTPPORT == 0 ) nMAIL_SMTPPORT = 25;
			string sCertName     = Sql.ToString(_memoryCache.Get<object>("CONFIG.mail_smtpcert"));
			string sDecryptedPass = _security.DecryptPassword(sMAIL_SMTPPASS);

			if ( sMAIL_SENDTYPE == "Office365" && !Sql.IsEmptyGuid(gOAUTH_TOKEN_ID) )
				return new SplendidMailOffice365(_memoryCache, gOAUTH_TOKEN_ID);
			if ( sMAIL_SENDTYPE == "GoogleApps" && !Sql.IsEmptyGuid(gOAUTH_TOKEN_ID) )
				return new SplendidMailGmail(_memoryCache, gOAUTH_TOKEN_ID);
			return new SplendidMailSmtp(_memoryCache, sMAIL_SMTPSERVER, nMAIL_SMTPPORT,
			                           bMAIL_SMTPAUTH_REQ, bMAIL_SMTPSSL,
			                           sMAIL_SMTPUSER, sDecryptedPass, sCertName);
		}

		// ------------------------------------------------------------------
		// SendTestMessage  — sends a single test email using current mail config
		// ------------------------------------------------------------------
		public void SendTestMessage(string sToAddress, string sFromAddress, string sFromName)
		{
			HttpContext Context = _httpContextAccessor.HttpContext;
			try
			{
				SplendidMailClient mail = BuildMailClient(
					Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_sendtype"   )),
					Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_smtpserver" )),
					Sql.ToInteger(_memoryCache.Get<object>("CONFIG.mail_smtpport"   )),
					Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.mail_smtpauth_req")),
					Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.mail_smtpssl"    )),
					Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_smtpuser"   )),
					Sql.ToString (_memoryCache.Get<object>("CONFIG.mail_smtppass"   )),
					Guid.Empty);
				System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage();
				msg.From       = new System.Net.Mail.MailAddress(sFromAddress, sFromName);
				msg.To.Add(new System.Net.Mail.MailAddress(sToAddress));
				msg.Subject    = "SplendidCRM Test Message";
				msg.Body       = "This is a test message from SplendidCRM.";
				msg.IsBodyHtml = false;
				mail.Send(msg);
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
				throw;
			}
		}

		// ------------------------------------------------------------------
		// SendEmail
		// Send a single email record (gID) from the EMAILS table.
		// Reads from vwEMAILS_ReadyToSend; fills parent data; attaches note
		// blobs; sends via the selected mail transport.
		// nErrorCount is incremented on each failure.
		// ------------------------------------------------------------------
		public void SendEmail(Guid gID, ref int nErrorCount)
		{
			HttpContext Context = _httpContextAccessor.HttpContext;
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					// Read email envelope + per-email SMTP settings
					string   sFROM_ADDR         = String.Empty;
					string   sFROM_NAME         = String.Empty;
					string   sTO_ADDRS          = String.Empty;
					string   sCC_ADDRS          = String.Empty;
					string   sBCC_ADDRS         = String.Empty;
					string   sREPLY_TO_ADDR     = String.Empty;
					string   sNAME              = String.Empty;
					string   sDESCRIPTION       = String.Empty;
					string   sDESCRIPTION_HTML  = String.Empty;
					string   sPARENT_TYPE       = String.Empty;
					Guid     gPARENT_ID         = Guid.Empty;
					string   sMAIL_SENDTYPE     = "smtp";
					string   sMAIL_SMTPSERVER   = String.Empty;
					int      nMAIL_SMTPPORT     = 25;
					bool     bMAIL_SMTPAUTH_REQ = false;
					bool     bMAIL_SMTPSSL      = false;
					string   sMAIL_SMTPUSER     = String.Empty;
					string   sMAIL_SMTPPASS     = String.Empty;
					Guid     gOAUTH_TOKEN_ID    = Guid.Empty;
					bool     bReadyToSend       = false;

					string sSQL = "select *"                    + ControlChars.CrLf
					            + "  from vwEMAILS_ReadyToSend" + ControlChars.CrLf
					            + " where ID = @ID"             + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText    = sSQL;
						cmd.CommandTimeout = 0;
						Sql.AddParameter(cmd, "@ID", gID);
						using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
						{
							if ( rdr.Read() )
							{
								bReadyToSend        = true;
								sFROM_ADDR          = Sql.ToString (rdr["FROM_ADDR"         ]);
								sFROM_NAME          = Sql.ToString (rdr["FROM_NAME"         ]);
								sTO_ADDRS           = Sql.ToString (rdr["TO_ADDRS"          ]);
								sCC_ADDRS           = Sql.ToString (rdr["CC_ADDRS"          ]);
								sBCC_ADDRS          = Sql.ToString (rdr["BCC_ADDRS"         ]);
								sREPLY_TO_ADDR      = Sql.ToString (rdr["REPLY_TO_ADDR"     ]);
								sNAME               = Sql.ToString (rdr["NAME"              ]);
								sDESCRIPTION        = Sql.ToString (rdr["DESCRIPTION"       ]);
								sDESCRIPTION_HTML   = Sql.ToString (rdr["DESCRIPTION_HTML"  ]);
								sPARENT_TYPE        = Sql.ToString (rdr["PARENT_TYPE"       ]);
								gPARENT_ID          = Sql.ToGuid   (rdr["PARENT_ID"         ]);
								try { sMAIL_SMTPUSER    = Sql.ToString (rdr["MAIL_SMTPUSER"    ]); } catch {}
								try { sMAIL_SMTPPASS    = Sql.ToString (rdr["MAIL_SMTPPASS"    ]); } catch {}
								try { sMAIL_SMTPSERVER  = Sql.ToString (rdr["MAIL_SMTPSERVER"  ]); } catch {}
								try { nMAIL_SMTPPORT    = Sql.ToInteger(rdr["MAIL_SMTPPORT"    ]); } catch {}
								try { bMAIL_SMTPAUTH_REQ= Sql.ToBoolean(rdr["MAIL_SMTPAUTH_REQ"]); } catch {}
								try { bMAIL_SMTPSSL     = Sql.ToBoolean(rdr["MAIL_SMTPSSL"     ]); } catch {}
								try { sMAIL_SENDTYPE    = Sql.ToString (rdr["MAIL_SENDTYPE"    ]); } catch {}
								try { gOAUTH_TOKEN_ID   = Sql.ToGuid   (rdr["OAUTH_TOKEN_ID"   ]); } catch {}
							}
						}
					}
					if ( !bReadyToSend ) return;

					// Fill parent module data into subject/body
					if ( !Sql.IsEmptyGuid(gPARENT_ID) && !Sql.IsEmptyString(sPARENT_TYPE) )
					{
						DataTable dtParent = Crm.Modules.Parent(_memoryCache, sPARENT_TYPE, gPARENT_ID);
						if ( dtParent != null && dtParent.Rows.Count > 0 )
						{
							FillEmail(sPARENT_TYPE, sPARENT_TYPE.ToUpper(), dtParent.Rows[0], null,
							         ref sNAME, ref sDESCRIPTION_HTML);
							FillEmail(sPARENT_TYPE, sPARENT_TYPE.ToUpper(), dtParent.Rows[0], null,
							         ref sNAME, ref sDESCRIPTION);
						}
					}

					// Build MailMessage
					System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage();
					var lstAtt = new List<System.Net.Mail.Attachment>();
					try
					{
						if ( !Sql.IsEmptyString(sFROM_ADDR) )
						{
							msg.From = !Sql.IsEmptyString(sFROM_NAME)
							         ? new System.Net.Mail.MailAddress(sFROM_ADDR, sFROM_NAME)
							         : new System.Net.Mail.MailAddress(sFROM_ADDR);
						}
						if ( !Sql.IsEmptyString(sREPLY_TO_ADDR) )
							msg.ReplyToList.Add(new System.Net.Mail.MailAddress(sREPLY_TO_ADDR));
						foreach ( string sAddr in sTO_ADDRS.Split(new char[]{',',';'}, StringSplitOptions.RemoveEmptyEntries) )
						{ string st = sAddr.Trim(); if (IsValidEmail(st)) msg.To.Add(st); }
						foreach ( string sAddr in sCC_ADDRS.Split(new char[]{',',';'}, StringSplitOptions.RemoveEmptyEntries) )
						{ string st = sAddr.Trim(); if (IsValidEmail(st)) msg.CC.Add(st); }
						foreach ( string sAddr in sBCC_ADDRS.Split(new char[]{',',';'}, StringSplitOptions.RemoveEmptyEntries) )
						{ string st = sAddr.Trim(); if (IsValidEmail(st)) msg.Bcc.Add(st); }
						msg.Subject = sNAME;
						if ( !Sql.IsEmptyString(sDESCRIPTION_HTML) )
						{ msg.Body = sDESCRIPTION_HTML; msg.IsBodyHtml = true; }
						else
						{ msg.Body = sDESCRIPTION;      msg.IsBodyHtml = false; }

						// Attach note blobs
						DataTable dtNotes = new DataTable();
						string sAttSQL = "select ID, FILENAME, FILE_MIME_TYPE" + ControlChars.CrLf
						               + "  from vwNOTES_ATTACHMENT_List"      + ControlChars.CrLf
						               + " where 1 = 1"                        + ControlChars.CrLf;
						using ( IDbCommand cmdAtt = con.CreateCommand() )
						{
							cmdAtt.CommandText = sAttSQL;
							var sbAtt = new StringBuilder();
							Sql.AppendParameter(cmdAtt, sbAtt, "NOTE_ID", gID);
							cmdAtt.CommandText += sbAtt.ToString();
							DbDataAdapter da = dbf.CreateDataAdapter();
							((IDbDataAdapter)da).SelectCommand = cmdAtt;
							da.Fill(dtNotes);
						}
						foreach ( DataRow noteRow in dtNotes.Rows )
						{
							Guid   gNID = Sql.ToGuid  (noteRow["ID"            ]);
							string sFN  = Sql.ToString(noteRow["FILENAME"      ]);
							string sMT  = Sql.ToString(noteRow["FILE_MIME_TYPE"]);
							MemoryStream stm = new MemoryStream();
							BinaryWriter bw  = new BinaryWriter(stm);
							ModuleUtils.Notes.Attachment.WriteStream(gNID, con, bw);
							bw.Flush();
							stm.Seek(0, SeekOrigin.Begin);
							var att = new System.Net.Mail.Attachment(stm, sFN, sMT);
							lstAtt.Add(att);
							msg.Attachments.Add(att);
						}

						SplendidMailClient mc = BuildMailClient(sMAIL_SENDTYPE, sMAIL_SMTPSERVER,
						                        nMAIL_SMTPPORT, bMAIL_SMTPAUTH_REQ, bMAIL_SMTPSSL,
						                        sMAIL_SMTPUSER, sMAIL_SMTPPASS, gOAUTH_TOKEN_ID);
						mc.Send(msg);
						SqlProcs.spEMAILS_UpdateStatus(gID, "sent");
					}
					finally
					{
						foreach (var a in lstAtt) a.Dispose();
						msg.Dispose();
					}
				}
			}
			catch(Exception ex)
			{
				nErrorCount++;
				SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
			}
		}

		// ------------------------------------------------------------------
		// SendQueued
		// Process the mass-mailing queue for a campaign/email-marketing entry.
		// Reads from vwEMAIL_MARKETING_PROSPECT_LISTS and personalises each
		// message, attaches template files, then sends.
		// ------------------------------------------------------------------
		public void SendQueued(Guid gCAMPAIGN_ID, Guid gEMAIL_MARKETING_ID, bool bCheckSendDate)
		{
			HttpContext Context = _httpContextAccessor.HttpContext;
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				string   sFromName          = String.Empty;
				string   sFromAddress       = String.Empty;
				string   sReplyToAddress    = String.Empty;
				string   sTemplateSubject   = String.Empty;
				string   sTemplateBodyText  = String.Empty;
				string   sTemplateBodyHtml  = String.Empty;
				Guid     gEMAIL_TEMPLATE_ID = Guid.Empty;
				DateTime dtSendDate         = DateTime.MinValue;
				string   sMAIL_SENDTYPE     = String.Empty;
				string   sMAIL_SMTPSERVER   = String.Empty;
				int      nMAIL_SMTPPORT     = 25;
				bool     bMAIL_SMTPAUTH_REQ = false;
				bool     bMAIL_SMTPSSL      = false;
				string   sMAIL_SMTPUSER     = String.Empty;
				string   sMAIL_SMTPPASS     = String.Empty;
				Guid     gOAUTH_TOKEN_ID    = Guid.Empty;

				// Read campaign email-marketing schedule record
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL = "select vwEM.*, vwC.NAME as CAMPAIGN_NAME"                + ControlChars.CrLf
					            + "  from vwEMAIL_MARKETING_EMAILS vwEM"                    + ControlChars.CrLf
					            + " inner join vwCAMPAIGNS vwC on vwC.ID = vwEM.CAMPAIGN_ID"+ ControlChars.CrLf
					            + " where 1 = 1"                                            + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						var sb = new StringBuilder();
						Sql.AppendParameter(cmd, sb, "CAMPAIGN_ID",        gCAMPAIGN_ID       );
						Sql.AppendParameter(cmd, sb, "EMAIL_MARKETING_ID", gEMAIL_MARKETING_ID);
						cmd.CommandText += sb.ToString();
						using ( IDataReader rdr = cmd.ExecuteReader() )
						{
							if ( rdr.Read() )
							{
								sFromName         = Sql.ToString (rdr["FROM_NAME"         ]);
								sFromAddress      = Sql.ToString (rdr["FROM_ADDR"         ]);
								sReplyToAddress   = Sql.ToString (rdr["REPLY_TO_ADDR"     ]);
								sTemplateSubject  = Sql.ToString (rdr["NAME"              ]);
								gEMAIL_TEMPLATE_ID= Sql.ToGuid   (rdr["EMAIL_TEMPLATE_ID" ]);
								dtSendDate        = Sql.ToDateTime(rdr["SEND_DATE_TIME"    ]);
								try { sMAIL_SENDTYPE    = Sql.ToString (rdr["MAIL_SENDTYPE"    ]); } catch {}
								try { sMAIL_SMTPSERVER  = Sql.ToString (rdr["MAIL_SMTPSERVER"  ]); } catch {}
								try { nMAIL_SMTPPORT    = Sql.ToInteger(rdr["MAIL_SMTPPORT"    ]); } catch {}
								try { bMAIL_SMTPAUTH_REQ= Sql.ToBoolean(rdr["MAIL_SMTPAUTH_REQ"]); } catch {}
								try { bMAIL_SMTPSSL     = Sql.ToBoolean(rdr["MAIL_SMTPSSL"     ]); } catch {}
								try { sMAIL_SMTPUSER    = Sql.ToString (rdr["MAIL_SMTPUSER"    ]); } catch {}
								try { sMAIL_SMTPPASS    = Sql.ToString (rdr["MAIL_SMTPPASS"    ]); } catch {}
								try { gOAUTH_TOKEN_ID   = Sql.ToGuid   (rdr["OAUTH_TOKEN_ID"   ]); } catch {}
							}
						}
					}
					// Load email template body
					if ( !Sql.IsEmptyGuid(gEMAIL_TEMPLATE_ID) )
					{
						sSQL = "select SUBJECT, BODY, BODY_HTML"    + ControlChars.CrLf
						     + "  from vwEMAIL_TEMPLATES"           + ControlChars.CrLf
						     + " where ID = @ID"                    + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@ID", gEMAIL_TEMPLATE_ID);
							using ( IDataReader rdr = cmd.ExecuteReader() )
							{
								if ( rdr.Read() )
								{
									sTemplateSubject  = Sql.ToString(rdr["SUBJECT"  ]);
									sTemplateBodyText = Sql.ToString(rdr["BODY"     ]);
									sTemplateBodyHtml = Sql.ToString(rdr["BODY_HTML"]);
								}
							}
						}
					}
				}

				if ( bCheckSendDate && dtSendDate != DateTime.MinValue && dtSendDate > DateTime.Now )
					return;

				DataTable dtTemplateAtts = EmailTemplateAttachments(gEMAIL_TEMPLATE_ID);
				DataTable dtTrackers     = CampaignTrackers(gCAMPAIGN_ID);
				string    sSiteURL       = _utils.MassEmailerSiteURL();

				SplendidMailClient mc = BuildMailClient(sMAIL_SENDTYPE, sMAIL_SMTPSERVER,
				                        nMAIL_SMTPPORT, bMAIL_SMTPAUTH_REQ, bMAIL_SMTPSSL,
				                        sMAIL_SMTPUSER, sMAIL_SMTPPASS, gOAUTH_TOKEN_ID);

				// Query pending prospect queue entries (1000-row batch)
				DataTable dtQueue = new DataTable();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL = "select *"                                  + ControlChars.CrLf
					            + "  from vwEMAIL_MARKETING_PROSPECT_LISTS"   + ControlChars.CrLf
					            + " where 1 = 1"                              + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						var sb = new StringBuilder();
						Sql.AppendParameter(cmd, sb, "EMAIL_MARKETING_ID", gEMAIL_MARKETING_ID);
						cmd.CommandText += sb.ToString();
						string sLimitSQL = Sql.LimitResults(cmd, 1000);
						if ( !Sql.IsEmptyString(sLimitSQL) ) cmd.CommandText = sLimitSQL;
						DbDataAdapter da = dbf.CreateDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						da.Fill(dtQueue);
					}

					// Process each queued prospect
					foreach ( DataRow qRow in dtQueue.Rows )
					{
						try
						{
							Guid   gRECIPIENT_ID   = Sql.ToGuid  (qRow["PROSPECT_ID"      ]);
							string sEmailAddress   = Sql.ToString (qRow["EMAIL_ADDRESS"    ]);
							string sFirstName      = Sql.ToString (qRow["FIRST_NAME"       ]);
							string sLastName       = Sql.ToString (qRow["LAST_NAME"        ]);
							Guid   gEMAIL_LOG_ID   = Sql.ToGuid   (qRow["EMAIL_LOG_ID"     ]);
							Guid   gTRACKER_KEY    = Sql.ToGuid   (qRow["TARGET_TRACKER_KEY"]);
							if ( !IsValidEmail(sEmailAddress) ) continue;

							string sSubject  = sTemplateSubject;
							string sBodyHtml = sTemplateBodyHtml;
							string sBodyText = sTemplateBodyText;
							sSubject  = sSubject .Replace("{FIRST_NAME}", sFirstName).Replace("{LAST_NAME}", sLastName);
							sBodyHtml = sBodyHtml.Replace("{FIRST_NAME}", sFirstName).Replace("{LAST_NAME}", sLastName);
							sBodyText = sBodyText.Replace("{FIRST_NAME}", sFirstName).Replace("{LAST_NAME}", sLastName);
							sBodyHtml = FillTrackers(sBodyHtml, dtTrackers, sSiteURL, gRECIPIENT_ID);

							System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage();
							var lstAtt = new List<System.Net.Mail.Attachment>();
							try
							{
								msg.From = !Sql.IsEmptyString(sFromName)
								         ? new System.Net.Mail.MailAddress(sFromAddress, sFromName)
								         : new System.Net.Mail.MailAddress(sFromAddress);
								if ( !Sql.IsEmptyString(sReplyToAddress) )
									msg.ReplyToList.Add(new System.Net.Mail.MailAddress(sReplyToAddress));
								msg.To.Add(new System.Net.Mail.MailAddress(sEmailAddress));
								msg.Subject = sSubject;
								if ( !Sql.IsEmptyString(sBodyHtml) )
								{ msg.Body = sBodyHtml; msg.IsBodyHtml = true; }
								else
								{ msg.Body = sBodyText; msg.IsBodyHtml = false; }

								// Attach template files
								foreach ( DataRow aRow in dtTemplateAtts.Rows )
								{
									Guid   gNID = Sql.ToGuid  (aRow["ID"            ]);
									string sFN  = Sql.ToString(aRow["FILENAME"      ]);
									string sMT  = Sql.ToString(aRow["FILE_MIME_TYPE"]);
									MemoryStream stm = new MemoryStream();
									BinaryWriter bw  = new BinaryWriter(stm);
									ModuleUtils.Notes.Attachment.WriteStream(gNID, con, bw);
									bw.Flush();
									stm.Seek(0, SeekOrigin.Begin);
									var att = new System.Net.Mail.Attachment(stm, sFN, sMT);
									lstAtt.Add(att);
									msg.Attachments.Add(att);
								}
								mc.Send(msg);
								// Record successful send
								SqlProcs.spEMAILMAN_SendSuccessful(gEMAIL_LOG_ID, gTRACKER_KEY, Guid.Empty);
							}
							catch(Exception sendEx)
							{
								SqlProcs.spEMAILMAN_SendFailed(gEMAIL_LOG_ID, "Emails", false);
								SplendidError.SystemMessage(_memoryCache, Context, "Warning", new StackFrame(true), Utils.ExpandException(sendEx));
							}
							finally
							{
								foreach (var a in lstAtt) a.Dispose();
								msg.Dispose();
							}
						}
						catch(Exception rowEx)
						{
							SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(rowEx));
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
			}
		}

		// ------------------------------------------------------------------
		// IsUndeliverableSubject  — detect bounce / DSN subjects
		// ------------------------------------------------------------------
		private static bool IsUndeliverableSubject(string sSubject)
		{
			if ( Sql.IsEmptyString(sSubject) ) return false;
			string s = sSubject.ToUpperInvariant();
			return s.Contains("DELIVERY STATUS NOTIFICATION")
			    || s.Contains("DELIVERY FAILURE")
			    || s.Contains("UNDELIVERABLE")
			    || s.Contains("MAIL DELIVERY FAILED")
			    || s.Contains("RETURNED MAIL")
			    || s.Contains("FAILED MAIL");
		}

		// ------------------------------------------------------------------
		// CheckInbound
		// Poll an inbound email account and import new messages.
		// bBounceHandling = true  → bounce / DSN mailbox
		// bBounceHandling = false → monitored general mailbox
		// Supports: Exchange-Password, GoogleApps, Office365, IMAP, POP3
		// ------------------------------------------------------------------
		public void CheckInbound(Guid gINBOUND_EMAIL_ID, bool bBounceHandling)
		{
			HttpContext Context = _httpContextAccessor.HttpContext;
			try
			{
				DataTable dtInbound = bBounceHandling
				                     ? _splendidCache.InboundEmailBounce()
				                     : _splendidCache.InboundEmailMonitored();
				if ( dtInbound == null || dtInbound.Rows.Count == 0 ) return;

				DataRow rowInbound = null;
				foreach ( DataRow row in dtInbound.Rows )
				{
					if ( Sql.ToGuid(row["ID"]) == gINBOUND_EMAIL_ID ) { rowInbound = row; break; }
				}
				if ( rowInbound == null ) return;

				string sProtocol         = Sql.ToString (rowInbound["PROTOCOL"          ]);
				string sMailServerUrl    = Sql.ToString (rowInbound["SERVER_URL"        ]);
				int    nMailServerPort   = Sql.ToInteger(rowInbound["MAIL_SERVER_PORT"  ]);
				bool   bSsl             = Sql.ToBoolean(rowInbound["USE_SSL"           ]);
				string sUserName         = Sql.ToString (rowInbound["EMAIL_USER"        ]);
				string sPasswordEnc      = Sql.ToString (rowInbound["EMAIL_PASSWORD"    ]);
				string sMailBoxName      = Sql.ToString (rowInbound["MAILBOX"           ]);
				Guid   gOAUTH_TOKEN_ID   = Sql.ToGuid   (rowInbound["OAUTH_TOKEN_ID"    ]);
				string sMailSendType     = Sql.ToString (rowInbound["MAIL_SENDTYPE"     ]);
				long   nLastUID          = Sql.ToLong   (rowInbound["LAST_UID"          ]);
				Guid   gGROUP_ID         = Sql.ToGuid   (rowInbound["GROUP_ID"          ]);
				Guid   gGROUP_TEAM_ID    = Sql.ToGuid   (rowInbound["GROUP_TEAM_ID"     ]);
				string sIntent           = bBounceHandling ? "bounce" : "monitored";
				string sPassword         = _security.DecryptPassword(sPasswordEnc);

				DbProviderFactory dbf = _dbProviderFactories.GetFactory();

				// ----- Exchange-Password -----
				if ( sProtocol == "Exchange-Password" || sMailSendType == "Exchange-Password" )
				{
					try
					{
						string sWatermark = Sql.ToString(rowInbound["EXCHANGE_WATERMARK"]);
						DataTable dtMsgs = ExchangeUtils.GetFolderMessages(Context, sUserName, sPassword, gINBOUND_EMAIL_ID, sMailBoxName, true, sWatermark);
						if ( dtMsgs != null )
						{
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								foreach ( DataRow msgRow in dtMsgs.Rows )
								{
									string sMsgId     = Sql.ToString(msgRow["ID"        ]);
									string sNewWmk    = Sql.ToString(msgRow["WATERMARK" ]);
									string sMsgSubj   = Sql.ToString(msgRow["SUBJECT"   ]);
									string sUniqueMsg = Sql.ToString(msgRow["UNIQUE_ID" ]);
									try
									{
										if ( bBounceHandling && !IsUndeliverableSubject(sMsgSubj) ) continue;
										ExchangeUtils.ImportInboundEmail(Context, con, gINBOUND_EMAIL_ID, sIntent, gGROUP_ID, gGROUP_TEAM_ID, sMsgId, sUniqueMsg);
										SqlProcs.spINBOUND_EMAILS_ExchangeWatermark(gINBOUND_EMAIL_ID, sNewWmk);
										ExchangeUtils.MarkAsUnread(Context, gINBOUND_EMAIL_ID, sMsgId);
									}
									catch(Exception msgEx)
									{
										SplendidError.SystemMessage(_memoryCache, Context, "Warning", new StackFrame(true), Utils.ExpandException(msgEx));
									}
								}
							}
						}
					}
					catch(Exception ex)
					{
						SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
					}
					return;
				}

				// ----- GoogleApps -----
				if ( sProtocol == "GoogleApps" || sMailSendType == "GoogleApps" )
				{
					try
					{
						DataTable dtMsgs = _googleApps.GetFolderMessages(Context, gOAUTH_TOKEN_ID, sMailBoxName, true, nLastUID, 100);
						if ( dtMsgs != null )
						{
							long nMaxUID = nLastUID;
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								foreach ( DataRow msgRow in dtMsgs.Rows )
								{
									string sMsgId   = Sql.ToString(msgRow["ID" ]);
									long   nMsgUID  = Sql.ToLong  (msgRow["UID"]);
									string sMsgSubj = Sql.ToString(msgRow["SUBJECT"]);
									try
									{
										if ( bBounceHandling && !IsUndeliverableSubject(sMsgSubj) ) continue;
										MimeMessage mm = _googleApps.GetMimeMessage(Context, gOAUTH_TOKEN_ID, sMsgId);
										MimeUtils.ImportInboundEmail(Context, con, mm, gINBOUND_EMAIL_ID, sIntent, gGROUP_ID, gGROUP_TEAM_ID, sMsgId);
										_googleApps.MarkAsUnread(Context, gOAUTH_TOKEN_ID, sMsgId);
										if ( nMsgUID > nMaxUID ) nMaxUID = nMsgUID;
									}
									catch(Exception msgEx)
									{
										SplendidError.SystemMessage(_memoryCache, Context, "Warning", new StackFrame(true), Utils.ExpandException(msgEx));
									}
								}
							}
							if ( nMaxUID > nLastUID )
								SqlProcs.spINBOUND_EMAILS_UpdateLastUID(gINBOUND_EMAIL_ID, nMaxUID);
						}
					}
					catch(Exception ex)
					{
						SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
					}
					return;
				}

				// ----- Office365 -----
				if ( sProtocol == "Office365" || sMailSendType == "Office365" )
				{
					try
					{
						string sFolderId = Office365Utils.GetFolderId(Context, sUserName, sPassword, gINBOUND_EMAIL_ID, sMailBoxName);
						DataTable dtMsgs = Office365Utils.GetFolderMessages(Context, sUserName, sPassword, gINBOUND_EMAIL_ID, sMailBoxName, true, String.Empty);
						if ( dtMsgs != null )
						{
							long nMaxUID = nLastUID;
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								foreach ( DataRow msgRow in dtMsgs.Rows )
								{
									string sMsgId   = Sql.ToString(msgRow["ID"     ]);
									long   nMsgUID  = Sql.ToLong  (msgRow["UID"    ]);
									string sMsgSubj = Sql.ToString(msgRow["SUBJECT"]);
									string sEmail   = Sql.ToString(msgRow["EMAIL"  ]);
									try
									{
										if ( bBounceHandling && !IsUndeliverableSubject(sMsgSubj) ) continue;
										Office365Utils.ImportInboundEmail(Context, con, gINBOUND_EMAIL_ID, sIntent, gGROUP_ID, gGROUP_TEAM_ID, sMsgId, sMsgId, sEmail);
										Office365Utils.MarkAsUnread(Context, gINBOUND_EMAIL_ID, sMsgId);
										if ( nMsgUID > nMaxUID ) nMaxUID = nMsgUID;
									}
									catch(Exception msgEx)
									{
										SplendidError.SystemMessage(_memoryCache, Context, "Warning", new StackFrame(true), Utils.ExpandException(msgEx));
									}
								}
							}
							if ( nMaxUID > nLastUID )
								SqlProcs.spINBOUND_EMAILS_UpdateLastUID(gINBOUND_EMAIL_ID, nMaxUID);
						}
					}
					catch(Exception ex)
					{
						SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
					}
					return;
				}

				// ----- IMAP -----
				if ( sProtocol == "IMAP" )
				{
					try
					{
						using ( ImapClient imapClient = new ImapClient() )
						{
							imapClient.Connect(sMailServerUrl, nMailServerPort, bSsl);
							imapClient.Authenticate(sUserName, sPassword);
							IMailFolder folder = imapClient.GetFolder(sMailBoxName);
							folder.Open(MailKit.FolderAccess.ReadWrite);
							var uids = folder.Search(MailKit.Search.SearchQuery.All);
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								long nMaxUID = nLastUID;
								foreach ( UniqueId uid in uids )
								{
									if ( uid.Id <= (uint)nLastUID ) continue;
									try
									{
										MimeMessage mm  = folder.GetMessage(uid);
										string sSubject = mm.Subject ?? String.Empty;
										if ( bBounceHandling && !IsUndeliverableSubject(sSubject) ) continue;
										string sMsgId = uid.ToString();
										MimeUtils.ImportInboundEmail(Context, con, mm, gINBOUND_EMAIL_ID, sIntent, gGROUP_ID, gGROUP_TEAM_ID, sMsgId);
										if ( uid.Id > (uint)nMaxUID ) nMaxUID = (long)uid.Id;
									}
									catch(Exception msgEx)
									{
										SplendidError.SystemMessage(_memoryCache, Context, "Warning", new StackFrame(true), Utils.ExpandException(msgEx));
									}
								}
								if ( nMaxUID > nLastUID )
									SqlProcs.spINBOUND_EMAILS_UpdateLastUID(gINBOUND_EMAIL_ID, nMaxUID);
							}
							imapClient.Disconnect(true);
						}
					}
					catch(Exception ex)
					{
						SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
					}
					return;
				}

				// ----- POP3 (default fallback) -----
				try
				{
					using ( Pop3Client pop3Client = new Pop3Client() )
					{
						pop3Client.Connect(sMailServerUrl, nMailServerPort, bSsl);
						pop3Client.Authenticate(sUserName, sPassword);
						int nCount = pop3Client.GetMessageCount();
						using ( IDbConnection con = dbf.CreateConnection() )
						{
							con.Open();
							for ( int i = 0; i < nCount; i++ )
							{
								try
								{
									MimeMessage mm  = pop3Client.GetMessage(i);
									string sSubject = mm.Subject ?? String.Empty;
									if ( bBounceHandling && !IsUndeliverableSubject(sSubject) ) continue;
									string sMsgId = pop3Client.GetMessageUid(i);
									MimeUtils.ImportInboundEmail(Context, con, mm, gINBOUND_EMAIL_ID, sIntent, gGROUP_ID, gGROUP_TEAM_ID, sMsgId);
								}
								catch(Exception msgEx)
								{
									SplendidError.SystemMessage(_memoryCache, Context, "Warning", new StackFrame(true), Utils.ExpandException(msgEx));
								}
							}
						}
						pop3Client.Disconnect(true);
					}
				}
				catch(Exception ex)
				{
					SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
			}
		}

		// ------------------------------------------------------------------
		// CheckBounced  — check the bounce / DSN inbound mailbox
		// ------------------------------------------------------------------
		public void CheckBounced(Guid gINBOUND_EMAIL_ID)
		{
			CheckInbound(gINBOUND_EMAIL_ID, true);
		}

		// ------------------------------------------------------------------
		// CheckMonitored  — check the monitored general inbound mailbox
		// ------------------------------------------------------------------
		public void CheckMonitored(Guid gINBOUND_EMAIL_ID)
		{
			CheckInbound(gINBOUND_EMAIL_ID, false);
		}

		// ------------------------------------------------------------------
		// SendOutbound  — send all pending outbound emails in the queue
		// ------------------------------------------------------------------
		public void SendOutbound()
		{
			HttpContext Context = _httpContextAccessor.HttpContext;
			try
			{
				if ( _utils.IsOfflineClient ) return;
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				DataTable dtEmails = new DataTable();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL = "select ID"                   + ControlChars.CrLf
					            + "  from vwEMAILS_Outbound"    + ControlChars.CrLf
					            + " where STATUS = 'queued'"    + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						string sLimitSQL = Sql.LimitResults(cmd, 200);
						if ( !Sql.IsEmptyString(sLimitSQL) ) cmd.CommandText = sLimitSQL;
						DbDataAdapter da = dbf.CreateDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						da.Fill(dtEmails);
					}
				}
				int nErrorCount = 0;
				foreach ( DataRow row in dtEmails.Rows )
				{
					try
					{
						SendEmail(Sql.ToGuid(row["ID"]), ref nErrorCount);
					}
					catch(Exception sendEx)
					{
						nErrorCount++;
						SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(sendEx));
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
			}
		}

		// ------------------------------------------------------------------
		// OnTimer  — entry point called by SchedulerHostedService on each tick
		// Checks job-election, reentrancy guard, then dispatches reminders.
		// sender may be null when called from IHostedService (no timer object).
		// ------------------------------------------------------------------
		public void OnTimer(object sender = null)
		{
			HttpContext Context = _httpContextAccessor.HttpContext;
			try
			{
				// Machine-name-based job election (preserved from original)
				string sJobServer = _configuration["SPLENDID_JOB_SERVER"];
				if ( !Sql.IsEmptyString(sJobServer)
				  && String.Compare(sJobServer, System.Environment.MachineName, StringComparison.OrdinalIgnoreCase) != 0 )
					return;
				if ( bInsideActivityReminder ) return;
				bInsideActivityReminder = true;
				try
				{
					SendActivityReminders(sender);
					SendSmsActivityReminders(sender);
					GoogleSync.GoogleWebhook.ProcessAllNotifications(Context);
				}
				finally
				{
					bInsideActivityReminder = false;
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
			}
		}

		// ------------------------------------------------------------------
		// SendActivityReminders  — email reminders for Meetings/Calls/Tasks
		// ------------------------------------------------------------------
		public void SendActivityReminders(object sender = null)
		{
			HttpContext Context = _httpContextAccessor.HttpContext;
			try
			{
				if ( _utils.IsOfflineClient ) return;
				bool bEnabled = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_activity_reminder"));
				if ( !bEnabled ) return;
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				string sFromAddress  = Sql.ToString(_memoryCache.Get<object>("CONFIG.mail_from_address"));
				string sFromName     = Sql.ToString(_memoryCache.Get<object>("CONFIG.mail_from_name"   ));
				string sCultureName  = SplendidDefaults.Culture(_memoryCache);
				SplendidMailClient mc = SplendidMailClient.CreateMailClient(_memoryCache);

				foreach ( string sModuleName in new string[]{"Meetings","Calls","Tasks"} )
				{
					string sTableName = sModuleName.ToUpper();
					DataTable dtActivities = new DataTable();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						// Query activities with pending email reminders that are due
						string sSQL = "select *"                                    + ControlChars.CrLf
						            + "  from vw" + sTableName + "_Reminders"       + ControlChars.CrLf
						            + " where REMINDER_EMAIL_SENT = 0"              + ControlChars.CrLf
						            + "   and DATE_START > @NOW"                    + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@NOW", DateTime.Now);
							DbDataAdapter da = dbf.CreateDataAdapter();
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dtActivities);
						}
					}
					foreach ( DataRow row in dtActivities.Rows )
					{
						Guid     gACTIVITY_ID = Sql.ToGuid   (row["ID"          ]);
						string   sSubject     = Sql.ToString  (row["NAME"        ]);
						string   sEmailAddress= Sql.ToString  (row["EMAIL1"      ]);
						string   sFullName    = Sql.ToString  (row["FULL_NAME"   ]);
						Guid     gUSER_ID     = Sql.ToGuid    (row["USER_ID"     ]);
						DateTime dtStart      = Sql.ToDateTime(row["DATE_START"  ]);
						Guid     gTIMEZONE_ID = Sql.ToGuid    (row["TIMEZONE_ID" ]);
						string   sINVITEE_TYPE= Sql.ToString  (row["INVITEE_TYPE"]);
						Guid     gINVITEE_ID  = Sql.ToGuid    (row["INVITEE_ID"  ]);
						try
						{
							if ( !IsValidEmail(sEmailAddress) ) continue;
							SplendidCRM.TimeZone tz = SplendidCRM.TimeZone.CreateTimeZone(_memoryCache, gTIMEZONE_ID);
							DateTime dtLocal        = tz.FromServerTime(dtStart);
							string sReminderSubj    = sModuleName.TrimEnd('s') + " Reminder: " + sSubject;
							string sReminderBody    = L10N.Term(_memoryCache, sCultureName, ".LBL_REMINDER_BODY_START")
							                        + " " + sSubject + " "
							                        + L10N.Term(_memoryCache, sCultureName, ".LBL_REMINDER_BODY_DATE")
							                        + " " + dtLocal.ToString("f");
							System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage();
							try
							{
								msg.From = !Sql.IsEmptyString(sFromName)
								         ? new System.Net.Mail.MailAddress(sFromAddress, sFromName)
								         : new System.Net.Mail.MailAddress(sFromAddress);
								msg.To.Add(new System.Net.Mail.MailAddress(sEmailAddress, sFullName));
								msg.Subject    = sReminderSubj;
								msg.Body       = sReminderBody;
								msg.IsBodyHtml = false;
								mc.Send(msg);
								// Mark reminder as sent
								if ( sModuleName == "Meetings" )
									SqlProcs.spMEETINGS_EmailReminderSent(gACTIVITY_ID, sINVITEE_TYPE, gINVITEE_ID);
								else if ( sModuleName == "Calls" )
									SqlProcs.spCALLS_EmailReminderSent(gACTIVITY_ID, sINVITEE_TYPE, gINVITEE_ID);
								else if ( sModuleName == "Tasks" )
									SqlProcs.spTASKS_EmailReminderSent(gACTIVITY_ID, sINVITEE_TYPE, gINVITEE_ID);
							}
							finally { msg.Dispose(); }
						}
						catch(Exception rowEx)
						{
							SplendidError.SystemMessage(_memoryCache, Context, "Warning", new StackFrame(true), Utils.ExpandException(rowEx));
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
			}
		}

		// ------------------------------------------------------------------
		// SendSmsActivityReminders  — SMS reminders via Twilio SDK
		// Replaces original TwilioManager.SendText() with direct SDK calls.
		// ------------------------------------------------------------------
		public void SendSmsActivityReminders(object sender = null)
		{
			HttpContext Context = _httpContextAccessor.HttpContext;
			try
			{
				if ( _utils.IsOfflineClient ) return;
				bool bEnabled = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_activity_sms_reminder"));
				if ( !bEnabled ) return;
				string sAccountSID   = Sql.ToString(_memoryCache.Get<object>("CONFIG.Twilio.AccountSID" ));
				string sAuthToken    = Sql.ToString(_memoryCache.Get<object>("CONFIG.Twilio.AuthToken"  ));
				string sTwilioFrom   = Sql.ToString(_memoryCache.Get<object>("CONFIG.Twilio.FromNumber" ));
				if ( Sql.IsEmptyString(sAccountSID) || Sql.IsEmptyString(sAuthToken) || Sql.IsEmptyString(sTwilioFrom) )
					return;
				// Initialise Twilio SDK (replaces TwilioManager.SendText)
				TwilioClient.Init(sAccountSID, sAuthToken);
				string sCultureName = SplendidDefaults.Culture(_memoryCache);
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();

				foreach ( string sModuleName in new string[]{"Meetings","Calls","Tasks"} )
				{
					string sTableName = sModuleName.ToUpper();
					DataTable dtActivities = new DataTable();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL = "select *"                                + ControlChars.CrLf
						            + "  from vw" + sTableName + "_Reminders"   + ControlChars.CrLf
						            + " where REMINDER_SMS_SENT = 0"           + ControlChars.CrLf
						            + "   and DATE_START > @NOW"               + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@NOW", DateTime.Now);
							DbDataAdapter da = dbf.CreateDataAdapter();
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dtActivities);
						}
					}
					foreach ( DataRow row in dtActivities.Rows )
					{
						Guid     gACTIVITY_ID = Sql.ToGuid   (row["ID"           ]);
						string   sSubject     = Sql.ToString  (row["NAME"         ]);
						string   sPhoneNo     = Sql.ToString  (row["PHONE_MOBILE" ]);
						DateTime dtStart      = Sql.ToDateTime(row["DATE_START"   ]);
						Guid     gTIMEZONE_ID = Sql.ToGuid    (row["TIMEZONE_ID"  ]);
						string   sINVITEE_TYPE= Sql.ToString  (row["INVITEE_TYPE" ]);
						Guid     gINVITEE_ID  = Sql.ToGuid    (row["INVITEE_ID"   ]);
						try
						{
							if ( Sql.IsEmptyString(sPhoneNo) ) continue;
							SplendidCRM.TimeZone tz = SplendidCRM.TimeZone.CreateTimeZone(_memoryCache, gTIMEZONE_ID);
							DateTime dtLocal = tz.FromServerTime(dtStart);
							string sSmsBody  = sModuleName.TrimEnd('s') + " Reminder: " + sSubject + " at " + dtLocal.ToString("g");
							// Direct Twilio SDK call (replacing TwilioManager.SendText)
							var options = new CreateMessageOptions(new PhoneNumber(sPhoneNo));
							options.From = new PhoneNumber(sTwilioFrom);
							options.Body = sSmsBody;
							MessageResource.Create(options);
							if ( sModuleName == "Meetings" )
								SqlProcs.spMEETINGS_SmsReminderSent(gACTIVITY_ID, sINVITEE_TYPE, gINVITEE_ID);
							else if ( sModuleName == "Calls" )
								SqlProcs.spCALLS_SmsReminderSent(gACTIVITY_ID, sINVITEE_TYPE, gINVITEE_ID);
							else if ( sModuleName == "Tasks" )
								SqlProcs.spTASKS_SmsReminderSent(gACTIVITY_ID, sINVITEE_TYPE, gINVITEE_ID);
						}
						catch(Exception rowEx)
						{
							SplendidError.SystemMessage(_memoryCache, Context, "Warning", new StackFrame(true), Utils.ExpandException(rowEx));
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
			}
		}

		// ------------------------------------------------------------------
		// SendActivityInvites  — send meeting/call invitations with ICS attach
		// gACTIVITY_ID — ID of the Meeting or Call to invite to
		// ------------------------------------------------------------------
		public void SendActivityInvites(Guid gACTIVITY_ID)
		{
			HttpContext Context = _httpContextAccessor.HttpContext;
			try
			{
				if ( _utils.IsOfflineClient ) return;
				string sFromAddress  = Sql.ToString(_memoryCache.Get<object>("CONFIG.mail_from_address"));
				string sFromName     = Sql.ToString(_memoryCache.Get<object>("CONFIG.mail_from_name"   ));
				string sCultureName  = SplendidDefaults.Culture(_memoryCache);
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();

				// Determine activity type (Meeting or Call)
				string   sType        = String.Empty;
				string   sName        = String.Empty;
				string   sDescription = String.Empty;
				string   sLocation    = String.Empty;
				DateTime dtStart      = DateTime.MinValue;
				DateTime dtEnd        = DateTime.MinValue;
				DataRow  actRow       = null;

				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					// Try Meetings first
					string sSQL = "select * from vwMEETINGS where ID = @ID";
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", gACTIVITY_ID);
						DataTable dtAct = new DataTable();
						DbDataAdapter da = dbf.CreateDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						da.Fill(dtAct);
						if ( dtAct.Rows.Count > 0 )
						{
							actRow   = dtAct.Rows[0];
							sType    = "Meeting";
							sName    = Sql.ToString (actRow["NAME"            ]);
							sDescription = Sql.ToString(actRow["DESCRIPTION" ]);
							sLocation    = Sql.ToString(actRow["LOCATION"    ]);
							dtStart  = Sql.ToDateTime(actRow["DATE_START"     ]);
							int nHrs = Sql.ToInteger(actRow["DURATION_HOURS"  ]);
							int nMin = Sql.ToInteger(actRow["DURATION_MINUTES"]);
							dtEnd    = dtStart.AddHours(nHrs).AddMinutes(nMin);
						}
					}
					if ( Sql.IsEmptyString(sType) )
					{
						// Try Calls
						sSQL = "select * from vwCALLS where ID = @ID";
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@ID", gACTIVITY_ID);
							DataTable dtAct = new DataTable();
							DbDataAdapter da = dbf.CreateDataAdapter();
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dtAct);
							if ( dtAct.Rows.Count > 0 )
							{
								actRow   = dtAct.Rows[0];
								sType    = "Call";
								sName    = Sql.ToString (actRow["NAME"            ]);
								sDescription = Sql.ToString(actRow["DESCRIPTION" ]);
								dtStart  = Sql.ToDateTime(actRow["DATE_START"     ]);
								int nHrs = Sql.ToInteger(actRow["DURATION_HOURS"  ]);
								int nMin = Sql.ToInteger(actRow["DURATION_MINUTES"]);
								dtEnd    = dtStart.AddHours(nHrs).AddMinutes(nMin);
							}
						}
					}
					if ( Sql.IsEmptyString(sType) || actRow == null ) return;

					// Generate iCalendar
					string sVCal = Utils.GenerateVCalendar(actRow, true);

					// Load invitees
					DataTable dtInvitees = new DataTable();
					string sInvSQL = "select *"                                            + ControlChars.CrLf
					               + "  from vw" + sType.ToUpper() + "S_INVITEES"         + ControlChars.CrLf
					               + " where 1 = 1"                                       + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sInvSQL;
						var sb = new StringBuilder();
						Sql.AppendParameter(cmd, sb, sType.ToUpper() + "_ID", gACTIVITY_ID);
						cmd.CommandText += sb.ToString();
						DbDataAdapter da = dbf.CreateDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						da.Fill(dtInvitees);
					}

					SplendidMailClient mc = SplendidMailClient.CreateMailClient(_memoryCache);
					foreach ( DataRow invRow in dtInvitees.Rows )
					{
						string sEmailAddress = Sql.ToString (invRow["EMAIL1"     ]);
						string sFullName     = Sql.ToString (invRow["FULL_NAME"  ]);
						Guid   gTZID         = Sql.ToGuid   (invRow["TIMEZONE_ID"]);
						try
						{
							if ( !IsValidEmail(sEmailAddress) ) continue;
							SplendidCRM.TimeZone tz = SplendidCRM.TimeZone.CreateTimeZone(_memoryCache, gTZID);
							DateTime dtLocal = tz.FromServerTime(dtStart);
							string sSubj = sType + " Invite: " + sName;
							string sBody = L10N.Term(_memoryCache, sCultureName, ".LBL_INVITE_BODY")
							             + " " + sName + " at " + dtLocal.ToString("f");
							if ( !Sql.IsEmptyString(sLocation) )
								sBody += "\r\nLocation: " + sLocation;
							if ( !Sql.IsEmptyString(sDescription) )
								sBody += "\r\n\r\n" + sDescription;
							System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage();
							try
							{
								msg.From = !Sql.IsEmptyString(sFromName)
								         ? new System.Net.Mail.MailAddress(sFromAddress, sFromName)
								         : new System.Net.Mail.MailAddress(sFromAddress);
								msg.To.Add(new System.Net.Mail.MailAddress(sEmailAddress, sFullName));
								msg.Subject    = sSubj;
								msg.Body       = sBody;
								msg.IsBodyHtml = false;
								if ( !Sql.IsEmptyString(sVCal) )
								{
									byte[]       icsBytes = System.Text.Encoding.UTF8.GetBytes(sVCal);
									MemoryStream icsStm   = new MemoryStream(icsBytes);
									msg.Attachments.Add(new System.Net.Mail.Attachment(icsStm, "invite.ics", "text/calendar"));
								}
								mc.Send(msg);
							}
							finally { msg.Dispose(); }
						}
						catch(Exception invEx)
						{
							SplendidError.SystemMessage(_memoryCache, Context, "Warning", new StackFrame(true), Utils.ExpandException(invEx));
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(_memoryCache, Context, "Error", new StackFrame(true), Utils.ExpandException(ex));
			}
		}
	}  // class EmailUtils
}  // namespace SplendidCRM
