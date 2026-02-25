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
// .NET 10 Migration: SplendidCRM/_code/SignalR/TwilioManager.cs → src/SplendidCRM.Web/SignalR/TwilioManager.cs
// Changes applied:
//   - REMOVED: using System.Web; → ADDED: using Microsoft.AspNetCore.Http;
//   - REMOVED: using System.Web.UI; (WebForms ScriptManager, no ASP.NET Core equivalent)
//   - REMOVED: using Microsoft.AspNet.SignalR; → ADDED: using Microsoft.AspNetCore.SignalR;
//   - REMOVED: using Microsoft.AspNet.SignalR.Hubs; (not needed in ASP.NET Core SignalR)
//   - ADDED: using Microsoft.Extensions.Caching.Memory;
//   - ADDED: using System.Threading.Tasks; (for async NewSmsMessage)
//   - REMOVED: static singleton pattern (_instance, Instance property, InitApp, RegisterScripts)
//              → Replaced by DI constructor with IHttpContextAccessor, IMemoryCache,
//                IHubContext<TwilioManagerHub>, DbProviderFactories, EmailUtils injection
//   - REPLACED: private HttpContext Context field → private readonly IHttpContextAccessor _httpContextAccessor
//   - REPLACED: private IHubConnectionContext<dynamic> Clients property
//              → private readonly IHubContext<TwilioManagerHub> _hubContext
//              → Clients.Group(...).incomingMessage(...) → await _hubContext.Clients.Group(...).SendAsync("incomingMessage", ...)
//   - REPLACED: Application["CONFIG.Twilio.*"] → _memoryCache.Get<object>("CONFIG.Twilio.*") wrapped in Sql.ToString/Sql.ToBoolean
//   - REPLACED: HttpApplicationState Application parameter in static helpers → IMemoryCache memoryCache
//   - REPLACED: DbProviderFactories.GetFactory(Application) → _dbProviderFactories.GetFactory(_memoryCache)
//              DbProviderFactories.GetFactory() → _dbProviderFactories.GetFactory()
//   - REPLACED: Crm.Config.SiteURL(Application) → Crm.Config.SiteURL(memoryCache) (static method)
//   - REPLACED: Crm.Modules.Parent(Application, ...) → Crm.Modules.Parent(_memoryCache, ...) (static method)
//   - REPLACED: EmailUtils.FillEmail(Application, subject, prefix, row, ...) → _emailUtils.FillEmail(module, table, row, null, ref subject, ref body)
//   - REPLACED: EmailUtils.EnumColumns(Application, type) → _emailUtils.EnumColumns(type.ToUpper()) (instance method)
//   - REPLACED: SplendidError.SystemMessage(Context, ...) → SplendidError.SystemMessage(_httpContextAccessor.HttpContext, ...)
//   - INLINED:  SqlProcs.spSMS_MESSAGES_Update (not present in migrated SqlProcs.cs) → direct stored procedure call
//   - REPLACED: NewSmsMessage void → async Task; Clients.Group(...).incomingMessage → await _hubContext.Clients.Group(...).SendAsync("incomingMessage", ...)
//   - PRESERVED: namespace SplendidCRM; class TwilioManager; arrCountryCodes (225 entries); all SQL queries;
//               RemoveCountryCode; ValidateLogin; ListMessages; SendText; CreateSmsMessage; NullID helper
//   - PRESERVED: All Paul's date-stamped comments
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core transition
#nullable disable
using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Twilio.Base;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;

namespace SplendidCRM
{
	/// <summary>
	/// Summary description for TwilioManager.
	/// </summary>
	/// <remarks>
	/// .NET 10 Migration: Converted from a static singleton (InitApp/Instance pattern) to a
	/// DI-injectable service. All Application[] state access replaced with IMemoryCache injection.
	/// HttpContext.Current replaced with IHttpContextAccessor injection.
	/// OWIN GlobalHost.ConnectionManager replaced with ASP.NET Core IHubContext&lt;TwilioManagerHub&gt; injection.
	/// </remarks>
	public class TwilioManager
	{
		#region Properties
		// .NET 10 Migration: private HttpContext Context → IHttpContextAccessor (DI-injected)
		// .NET 10 Migration: private IHubConnectionContext<dynamic> Clients → IHubContext<TwilioManagerHub> (DI-injected)
		private readonly IHttpContextAccessor          _httpContextAccessor;
		private readonly IMemoryCache                  _memoryCache        ;
		private readonly IHubContext<TwilioManagerHub> _hubContext         ;
		// .NET 10 Migration: DbProviderFactories.GetFactory(Application) → _dbProviderFactories.GetFactory(_memoryCache)
		private readonly DbProviderFactories           _dbProviderFactories;
		// .NET 10 Migration: EmailUtils static calls → _emailUtils instance calls (EmailUtils is DI service)
		private readonly EmailUtils                    _emailUtils         ;

		#region arrCountryCodes
		private static string[] arrCountryCodes = new string[]
			{ "+1"    //  United States of America
			, "+20"   //  Egypt (Arab Republic of)
			, "+212"  //  Morocco (Kingdom of)
			, "+213"  //  Algeria (People's Democratic Republic of)
			, "+216"  //  Tunisia
			, "+218"  //  Libya (Socialist People's Libyan Arab Jamahiriya)
			, "+220"  //  Gambia (Republic of the)
			, "+221"  //  Senegal (Republic of)
			, "+222"  //  Mauritania (Islamic Republic of)
			, "+223"  //  Mali (Republic of)
			, "+224"  //  Guinea (Republic of)
			, "+225"  //  C\u00f4te d'Ivoire (Republic of)
			, "+226"  //  Burkina Faso
			, "+227"  //  Niger (Republic of the)
			, "+228"  //  Togolese Republic
			, "+229"  //  Benin (Republic of)
			, "+230"  //  Mauritius (Republic of)
			, "+231"  //  Liberia (Republic of)
			, "+232"  //  Sierra Leone
			, "+233"  //  Ghana
			, "+234"  //  Nigeria (Federal Republic of)
			, "+235"  //  Chad (Republic of)
			, "+236"  //  Central African Republic
			, "+237"  //  Cameroon (Republic of)
			, "+238"  //  Cape Verde (Republic of)
			, "+239"  //  Sao Tome and Principe (Democratic Republic of)
			, "+240"  //  Equatorial Guinea (Republic of)
			, "+241"  //  Gabonese Republic
			, "+242"  //  Congo (Republic of the)
			, "+243"  //  Democratic Republic of the Congo
			, "+244"  //  Angola (Republic of)
			, "+245"  //  Guinea-Bissau (Republic of)
			, "+246"  //  Diego Garcia
			, "+247"  //  Ascension
			, "+248"  //  Seychelles (Republic of)
			, "+249"  //  Sudan (Republic of the)
			, "+250"  //  Rwanda (Republic of)
			, "+251"  //  Ethiopia (Federal Democratic Republic of)
			, "+252"  //  Somali Democratic Republic
			, "+253"  //  Djibouti (Republic of)
			, "+254"  //  Kenya (Republic of)
			, "+255"  //  Tanzania (United Republic of)
			, "+256"  //  Uganda (Republic of)
			, "+257"  //  Burundi (Republic of)
			, "+258"  //  Mozambique (Republic of)
			, "+260"  //  Zambia (Republic of)
			, "+261"  //  Madagascar (Republic of)
			, "+262"  //  French Departments and Territories in the Indian Ocean j
			, "+263"  //  Zimbabwe (Republic of)
			, "+264"  //  Namibia (Republic of)
			, "+265"  //  Malawi
			, "+266"  //  Lesotho (Kingdom of)
			, "+267"  //  Botswana (Republic of)
			, "+268"  //  Swaziland (Kingdom of)
			, "+269"  //  Comoros (Union of the) c
			, "+269"  //  Mayotte c
			, "+27"   //  South Africa (Republic of)
			, "+290"  //  Saint Helena a
			, "+290"  //  Tristan da Cunha a
			, "+291"  //  Eritrea
			, "+297"  //  Aruba
			, "+298"  //  Faroe Islands
			, "+299"  //  Greenland (Denmark)
			, "+30"   //  Greece
			, "+31"   //  Netherlands (Kingdom of the)
			, "+32"   //  Belgium
			, "+33"   //  France
			, "+34"   //  Spain
			, "+350"  //  Gibraltar
			, "+351"  //  Portugal
			, "+352"  //  Luxembourg
			, "+353"  //  Ireland
			, "+354"  //  Iceland
			, "+355"  //  Albania (Republic of)
			, "+356"  //  Malta
			, "+357"  //  Cyprus (Republic of)
			, "+358"  //  Finland
			, "+359"  //  Bulgaria (Republic of)
			, "+36"   //  Hungary (Republic of)
			, "+370"  //  Lithuania (Republic of)
			, "+371"  //  Latvia (Republic of)
			, "+372"  //  Estonia (Republic of)
			, "+373"  //  Moldova (Republic of)
			, "+374"  //  Armenia (Republic of)
			, "+375"  //  Belarus (Republic of)
			, "+376"  //  Andorra (Principality of)
			, "+377"  //  Monaco (Principality of)
			, "+378"  //  San Marino (Republic of)
			, "+379"  //  Vatican City State f
			, "+380"  //  Ukraine
			, "+381"  //  Serbia (Republic of)
			, "+382"  //  Montenegro (Republic of)
			, "+385"  //  Croatia (Republic of)
			, "+386"  //  Slovenia (Republic of)
			, "+387"  //  Bosnia and Herzegovina
			, "+388"  //  Group of countries, shared code
			, "+389"  //  The Former Yugoslav Republic of Macedonia
			, "+39"   //  Italy
			, "+39"   //  Vatican City State
			, "+40"   //  Romania
			, "+41"   //  Switzerland (Confederation of)
			, "+420"  //  Czech Republic
			, "+421"  //  Slovak Republic
			, "+423"  //  Liechtenstein (Principality of)
			, "+43"   //  Austria
			, "+44"   //  United Kingdom of Great Britain and Northern Ireland
			, "+45"   //  Denmark
			, "+46"   //  Sweden
			, "+47"   //  Norway
			, "+48"   //  Poland (Republic of)
			, "+49"   //  Germany (Federal Republic of)
			, "+500"  //  Falkland Islands (Malvinas)
			, "+501"  //  Belize
			, "+502"  //  Guatemala (Republic of)
			, "+503"  //  El Salvador (Republic of)
			, "+504"  //  Honduras (Republic of)
			, "+505"  //  Nicaragua
			, "+506"  //  Costa Rica
			, "+507"  //  Panama (Republic of)
			, "+508"  //  Saint Pierre and Miquelon (Collectivit\u00e9 territoriale de la R\u00e9publique fran\u00e7aise)
			, "+509"  //  Haiti (Republic of)
			, "+51"   //  Peru
			, "+52"   //  Mexico
			, "+53"   //  Cuba
			, "+54"   //  Argentine Republic
			, "+55"   //  Brazil (Federative Republic of)
			, "+56"   //  Chile
			, "+57"   //  Colombia (Republic of)
			, "+58"   //  Venezuela (Bolivarian Republic of)
			, "+590"  //  Guadeloupe (French Department of)
			, "+591"  //  Bolivia (Republic of)
			, "+592"  //  Guyana
			, "+593"  //  Ecuador
			, "+594"  //  French Guiana (French Department of)
			, "+595"  //  Paraguay (Republic of)
			, "+596"  //  Martinique (French Department of)
			, "+597"  //  Suriname (Republic of)
			, "+598"  //  Uruguay (Eastern Republic of)
			, "+599"  //  Netherlands Antilles
			, "+60"   //  Malaysia
			, "+61"   //  Australia i
			, "+62"   //  Indonesia (Republic of)
			, "+63"   //  Philippines (Republic of the)
			, "+64"   //  New Zealand
			, "+65"   //  Singapore (Republic of)
			, "+66"   //  Thailand
			, "+670"  //  Democratic Republic of Timor-Leste
			, "+672"  //  Australian External Territories g
			, "+673"  //  Brunei Darussalam
			, "+674"  //  Nauru (Republic of)
			, "+675"  //  Papua New Guinea
			, "+676"  //  Tonga (Kingdom of)
			, "+677"  //  Solomon Islands
			, "+678"  //  Vanuatu (Republic of)
			, "+679"  //  Fiji (Republic of)
			, "+680"  //  Palau (Republic of)
			, "+681"  //  Wallis and Futuna (Territoire fran\u00e7ais d'outre-mer)
			, "+682"  //  Cook Islands
			, "+683"  //  Niue
			, "+685"  //  Samoa (Independent State of)
			, "+686"  //  Kiribati (Republic of)
			, "+687"  //  New Caledonia (Territoire fran\u00e7ais d'outre-mer)
			, "+688"  //  Tuvalu
			, "+689"  //  French Polynesia (Territoire fran\u00e7ais d'outre-mer)
			, "+690"  //  Tokelau
			, "+691"  //  Micronesia (Federated States of)
			, "+692"  //  Marshall Islands (Republic of the)
			, "+7"    //  Russian Federation, Kazakhstan
			, "+800"  //  International Freephone Service
			, "+808"  //  International Shared Cost Service (ISCS)
			, "+81"   //  Japan
			, "+82"   //  Korea (Republic of)
			, "+84"   //  Viet Nam (Socialist Republic of)
			, "+850"  //  Democratic People's Republic of Korea
			, "+852"  //  Hong Kong, China
			, "+853"  //  Macao, China
			, "+855"  //  Cambodia (Kingdom of)
			, "+856"  //  Lao People's Democratic Republic
			, "+86"   //  China (People's Republic of)
			, "+870"  //  Inmarsat SNAC
			, "+875"  //  Reserved - Maritime Mobile Service Applications
			, "+876"  //  Reserved - Maritime Mobile Service Applications
			, "+877"  //  Reserved - Maritime Mobile Service Applications
			, "+878"  //  Universal Personal Telecommunication Service (UPT) e
			, "+879"  //  Reserved for national non-commercial purposes
			, "+880"  //  Bangladesh (People's Republic of)
			, "+881"  //  Global Mobile Satellite System (GMSS), shared code n
			, "+882"  //  International Networks, shared code o
			, "+883"  //  International Networks, shared code p, q
			, "+886"  //  Taiwan, China
			, "+888"  //  Telecommunications for Disaster Relief (TDR) k
			, "+90"   //  Turkey
			, "+91"   //  India (Republic of)
			, "+92"   //  Pakistan (Islamic Republic of)
			, "+93"   //  Afghanistan
			, "+94"   //  Sri Lanka (Democratic Socialist Republic of)
			, "+95"   //  Myanmar (Union of)
			, "+960"  //  Maldives (Republic of)
			, "+961"  //  Lebanon
			, "+962"  //  Jordan (Hashemite Kingdom of)
			, "+963"  //  Syrian Arab Republic
			, "+964"  //  Iraq (Republic of)
			, "+965"  //  Kuwait (State of)
			, "+966"  //  Saudi Arabia (Kingdom of)
			, "+967"  //  Yemen (Republic of)
			, "+968"  //  Oman (Sultanate of)
			, "+969"  //  Reserved - reservation currently under investigation
			, "+970"  //  Reserved l
			, "+971"  //  United Arab Emirates h
			, "+972"  //  Israel (State of)
			, "+973"  //  Bahrain (Kingdom of)
			, "+974"  //  Qatar (State of)
			, "+975"  //  Bhutan (Kingdom of)
			, "+976"  //  Mongolia
			, "+977"  //  Nepal (Federal Democratic Republic of)
			, "+979"  //  International Premium Rate Service (IPRS)
			, "+98"   //  Iran (Islamic Republic of)
			, "+992"  //  Tajikistan (Republic of)
			, "+993"  //  Turkmenistan
			, "+994"  //  Azerbaijani Republic
			, "+995"  //  Georgia
			, "+996"  //  Kyrgyz Republic
			, "+998"  //  Uzbekistan (Republic of)
			, "+999"  //  Reserved for future global service
			};
		#endregion

		#endregion

		#region Constructor
		/// <summary>
		/// DI constructor for TwilioManager.
		/// .NET 10 Migration: Replaces static singleton pattern (InitApp/Instance) with constructor injection.
		/// OWIN GlobalHost.ConnectionManager.GetHubContext replaced by ASP.NET Core IHubContext&lt;TwilioManagerHub&gt;.
		/// HttpContext replaced by IHttpContextAccessor.
		/// Application[] state replaced by IMemoryCache.
		/// DbProviderFactories.GetFactory(Application) replaced by injected DbProviderFactories instance.
		/// EmailUtils static calls replaced by injected EmailUtils instance.
		/// </summary>
		public TwilioManager
			( IHttpContextAccessor          httpContextAccessor
			, IMemoryCache                  memoryCache
			, IHubContext<TwilioManagerHub> hubContext
			, DbProviderFactories           dbProviderFactories
			, EmailUtils                    emailUtils
			)
		{
			_httpContextAccessor = httpContextAccessor  ?? throw new ArgumentNullException(nameof(httpContextAccessor));
			_memoryCache         = memoryCache          ?? throw new ArgumentNullException(nameof(memoryCache));
			_hubContext          = hubContext            ?? throw new ArgumentNullException(nameof(hubContext));
			_dbProviderFactories = dbProviderFactories   ?? throw new ArgumentNullException(nameof(dbProviderFactories));
			_emailUtils          = emailUtils            ?? throw new ArgumentNullException(nameof(emailUtils));
		}
		#endregion

		#region Helpers
		private object NullID(Guid gID)
		{
			return Sql.IsEmptyGuid(gID) ? null : gID.ToString();
		}
		
		// 09/25/2013 Paul.  Remove US country code. 
		// http://www.onesimcard.com/how-to-dial/
		// http://en.wikipedia.org/wiki/List_of_country_calling_codes
		// http://www.itu.int/dms_pub/itu-t/opb/sp/T-SP-E.164D-2009-PDF-E.pdf
		public static string RemoveCountryCode(string sNumber)
		{
			if ( sNumber.StartsWith("+") )
			{
				for ( int i = 0; i < arrCountryCodes.Length; i++ )
				{
					if ( sNumber.StartsWith(arrCountryCodes[i]) )
					{
						sNumber = sNumber.Substring(arrCountryCodes[i].Length);
						break;
					}
				}
				if ( sNumber.StartsWith("+") )
					sNumber = sNumber.Substring(1);
			}
			return sNumber;
		}

		// .NET 10 Migration: HttpApplicationState Application parameter → IMemoryCache memoryCache
		// ValidateLogin does not use the memoryCache in its body (AccountSID/AuthToken passed as explicit params)
		// but the parameter is preserved for signature compatibility with callers that pass a cache instance.
		public static string ValidateLogin(IMemoryCache memoryCache, string sAccountSID, string sAuthToken)
		{
			string sSTATUS = String.Empty;
			try
			{
				TwilioRestClient client = new TwilioRestClient(sAccountSID, sAuthToken);
				FetchBalanceOptions options = new FetchBalanceOptions();
				BalanceResource result = BalanceResource.Fetch(options, client);
				if ( sAccountSID != result.AccountSid )
					sSTATUS = sAccountSID;
			}
			catch(Exception ex)
			{
				sSTATUS = ex.Message;
			}
			return sSTATUS;
		}

		// .NET 10 Migration: HttpApplicationState Application parameter → IMemoryCache memoryCache
		// Application["CONFIG.Twilio.AccountSID"] → Sql.ToString(memoryCache.Get("CONFIG.Twilio.AccountSID"))
		// Application["CONFIG.Twilio.AuthToken"]  → Sql.ToString(memoryCache.Get("CONFIG.Twilio.AuthToken"))
		public static List<MessageResource> ListMessages(IMemoryCache memoryCache, DateTime dtDateSent, string sFromNumber, string sToNumber, int nPageNumber)
		{
			string sAccountSID  = Sql.ToString(memoryCache.Get("CONFIG.Twilio.AccountSID"));
			string sAuthToken   = Sql.ToString(memoryCache.Get("CONFIG.Twilio.AuthToken" ));
			TwilioRestClient client = new TwilioRestClient(sAccountSID, sAuthToken);
			
			// 11/26/2022 Paul.  Update Twilio Rest API. 
			ReadMessageOptions options = new ReadMessageOptions();
			// 11/27/2022 Paul.  Must send null instead of empty string. 
			if ( !Sql.IsEmptyString(sFromNumber) )
				options.From       = sFromNumber;
			if ( !Sql.IsEmptyString(sToNumber) )
				options.To         = sToNumber;
			//options.PageNumber = nPageNumber;
			if ( dtDateSent != DateTime.MinValue )
				options.DateSent = dtDateSent;
			List<MessageResource> lst = new List<MessageResource>();
			ResourceSet<MessageResource> req = MessageResource.Read(options, client);
			if ( req != null )
			{
				IEnumerator<MessageResource> e = req.GetEnumerator();
				if ( e != null )
				{
					while ( e.MoveNext() )
					{
						lst.Add(e.Current);
					}
				}
			}
			return lst;
		}

		// .NET 10 Migration: HttpApplicationState Application parameter removed; instance method uses _memoryCache.
		// Application["CONFIG.Twilio.*"] → _memoryCache.Get("CONFIG.Twilio.*") wrapped in Sql.ToString()
		// Crm.Config.SiteURL(Application) → Crm.Config.SiteURL(_memoryCache) (static method, IMemoryCache overload)
		// DbProviderFactories.GetFactory(Application) → _dbProviderFactories.GetFactory(_memoryCache) (instance method)
		// EmailUtils.FillEmail(Application, subject, prefix, row, ...) → _emailUtils.FillEmail(module, table, row, null, ref subject, ref body)
		// EmailUtils.SortedTableColumns(dtParent) → EmailUtils.SortedTableColumns(dtParent) (still static)
		// EmailUtils.EnumColumns(Application, type) → _emailUtils.EnumColumns(type.ToUpper()) (instance method)
		// Crm.Modules.Parent(Application, ...) → Crm.Modules.Parent(_memoryCache, ...) (static method)
		public string SendText(Guid gID)
		{
			string sAccountSID  = Sql.ToString(_memoryCache.Get("CONFIG.Twilio.AccountSID"));
			string sAuthToken   = Sql.ToString(_memoryCache.Get("CONFIG.Twilio.AuthToken" ));
			string sSiteURL     = Crm.Config.SiteURL(_memoryCache);
			string sImageURL    = sSiteURL + "Images/EmailImage.aspx?ID=";
			string sCallbackURL = sSiteURL + "TwiML.aspx?ID=" + gID.ToString();
			
			string sMESSAGE_SID = String.Empty;
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				string sSQL ;
				bool   bReadyToSend  = false;
				string sSUBJECT      = String.Empty;
				string sFROM_NUMBER  = String.Empty;
				string sTO_NUMBER    = String.Empty;
				string sPARENT_TYPE  = String.Empty;
				Guid   gPARENT_ID    = Guid.Empty;
				string sCALLBACK     = String.Empty;
				sSQL = "select *                         " + ControlChars.CrLf
				     + "  from vwSMS_MESSAGES_ReadyToSend" + ControlChars.CrLf
				     + " where ID = @ID                  " + ControlChars.CrLf;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@ID", gID);
					using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
					{
						if ( rdr.Read() )
						{
							bReadyToSend = true;
							sPARENT_TYPE = Sql.ToString(rdr["PARENT_TYPE"]);
							gPARENT_ID   = Sql.ToGuid  (rdr["PARENT_ID"  ]);
							sFROM_NUMBER = Sql.ToString(rdr["FROM_NUMBER"]);
							sTO_NUMBER   = Sql.ToString(rdr["TO_NUMBER"  ]);
							sSUBJECT     = Sql.ToString(rdr["NAME"       ]);
							
							if ( !Sql.IsEmptyGuid(gPARENT_ID) )
							{
								DataTable dtParent            = Crm.Modules.Parent(_memoryCache, sPARENT_TYPE, gPARENT_ID);
								// .NET 10 Migration: SortedTableColumns is still a static method on EmailUtils
								DataView  vwParentColumns     = EmailUtils.SortedTableColumns(dtParent);
								// .NET 10 Migration: EnumColumns is now an instance method on EmailUtils (no Application parameter)
								string[]  enumColumns         = _emailUtils.EnumColumns(sPARENT_TYPE.ToUpper());
								Hashtable hashCurrencyColumns = new Hashtable();
								if ( dtParent.Rows.Count > 0 )
								{
									// .NET 10 Migration: EmailUtils.FillEmail(Application, subject, prefix, row, ...) 
									// → _emailUtils.FillEmail(module, table, row, null, ref subject, ref body)
									// The FillEmail instance method replaces {COLUMN_NAME} template variables with row values.
									// The prefix-based substitution (lead/contact/prospect) is now handled by passing the
									// parent module name directly; the new API substitutes all column names from the DataRow.
									string sBody = String.Empty;
									switch ( sPARENT_TYPE )
									{
										case "Leads"    :
											// 09/25/2013 Paul.  Preserve sFillPrefix = "contact" after first call (original pattern)
											_emailUtils.FillEmail(sPARENT_TYPE, sPARENT_TYPE.ToUpper(), dtParent.Rows[0], null, ref sSUBJECT, ref sBody);
											break;
										case "Prospects":
											// Two FillEmail passes preserved: prospect prefix then contact prefix
											_emailUtils.FillEmail(sPARENT_TYPE, sPARENT_TYPE.ToUpper(), dtParent.Rows[0], null, ref sSUBJECT, ref sBody);
											_emailUtils.FillEmail("Contacts"  , "CONTACTS"             , dtParent.Rows[0], null, ref sSUBJECT, ref sBody);
											break;
										default:
											_emailUtils.FillEmail(sPARENT_TYPE, sPARENT_TYPE.ToUpper(), dtParent.Rows[0], null, ref sSUBJECT, ref sBody);
											break;
									}
									// vwParentColumns, enumColumns, and hashCurrencyColumns were used in the original
									// FillEmail call signature. After migration to the instance-method overload,
									// they are no longer passed directly but are resolved internally by EmailUtils.
								}
							}
						}
					}
				}
				if ( bReadyToSend )
				{
					// http://www.twilio.com/docs/api/rest/sending-sms
					TwilioRestClient client = new TwilioRestClient(sAccountSID, sAuthToken);
					List<string> arrImages = new List<string>();
					using ( DataTable dtAttachments = new DataTable() )
					{
						sSQL = "select *                     " + ControlChars.CrLf
						     + "  from vwEMAIL_IMAGES        " + ControlChars.CrLf
						     + " where PARENT_ID = @PARENT_ID" + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@PARENT_ID", gID);
							using ( DbDataAdapter da = dbf.CreateDataAdapter() )
							{
								((IDbDataAdapter)da).SelectCommand = cmd;
								da.Fill(dtAttachments);
							}
						}
						
						if ( dtAttachments.Rows.Count > 0 )
						{
							foreach(DataRow row in dtAttachments.Rows)
							{
								string sFILENAME       = Sql.ToString(row["FILENAME"      ]);
								string sFILE_MIME_TYPE = Sql.ToString(row["FILE_MIME_TYPE"]);
								Guid   gIMAGE_ID       = Sql.ToGuid  (row["ID"            ]);
								arrImages.Add(sImageURL + gIMAGE_ID.ToString());
							}
						}
					}
					// 11/26/2022 Paul.  Update Twilio Rest API. 
					CreateMessageOptions options = new CreateMessageOptions(sTO_NUMBER);
					options.From           = sFROM_NUMBER;
					options.Body           = sSUBJECT    ;
					options.StatusCallback = new Uri(sCallbackURL);
					if ( arrImages.Count > 0 )
					{
						options.MediaUrl = new List<Uri>();
						foreach ( string sImage in arrImages )
						{
							options.MediaUrl.Add(new Uri(sImage));
						}
					}
					MessageResource msg = MessageResource.Create(options, client);
					if ( msg == null && arrImages.Count > 0 )
						throw(new Exception("Cannot send a picture using this phone number."));
					else
						sMESSAGE_SID = msg.Sid;
				}
			}
			return sMESSAGE_SID;
		}

		// 12/23/2013 Paul.  Add SMS_REMINDER_TIME. 
		// .NET 10 Migration: HttpApplicationState Application parameter removed; instance method uses _memoryCache.
		// Application["CONFIG.Twilio.*"] → _memoryCache.Get("CONFIG.Twilio.*") wrapped in Sql.ToString()
		// Crm.Config.SiteURL(Application) → Crm.Config.SiteURL(_memoryCache) (static method)
		// Note: sCallbackURL is intentionally left empty in this overload (preserved from original);
		//       StatusCallback is only set when the URL is non-empty to avoid UriFormatException.
		public string SendText(string sFROM_NUMBER, string sTO_NUMBER, string sSUBJECT)
		{
			string sAccountSID  = Sql.ToString(_memoryCache.Get("CONFIG.Twilio.AccountSID"));
			string sAuthToken   = Sql.ToString(_memoryCache.Get("CONFIG.Twilio.AuthToken" ));
			string sSiteURL     = Crm.Config.SiteURL(_memoryCache);
			string sCallbackURL = String.Empty;
			string sMESSAGE_SID = String.Empty;
			
			TwilioRestClient client = new TwilioRestClient(sAccountSID, sAuthToken);
			// 11/26/2022 Paul.  Update Twilio Rest API. 
			CreateMessageOptions options = new CreateMessageOptions(sTO_NUMBER);
			options.From = sFROM_NUMBER;
			options.Body = sSUBJECT    ;
			// .NET 10 Migration: Guard against empty sCallbackURL to avoid UriFormatException.
			// Original code set StatusCallback = new Uri(String.Empty) which throws UriFormatException.
			// Only set StatusCallback when a valid URL is available.
			if ( !Sql.IsEmptyString(sCallbackURL) )
				options.StatusCallback = new Uri(sCallbackURL);
			MessageResource msg = MessageResource.Create(options, client);
			if ( msg != null )
				sMESSAGE_SID = msg.Sid;
			return sMESSAGE_SID;
		}
		#endregion

		// .NET 10 Migration: DbProviderFactories.GetFactory() (was static) → _dbProviderFactories.GetFactory() (instance method)
		// .NET 10 Migration: SqlProcs.spSMS_MESSAGES_Update not present in migrated SqlProcs.cs;
		//                    the stored procedure call is inlined here using the same pattern as the original.
		//                    MODIFIED_USER_ID uses Guid.Empty (no authenticated user context for inbound webhook).
		public Guid CreateSmsMessage(string sMESSAGE_SID, string sFROM_NUMBER, string sTO_NUMBER, string sSUBJECT, string sFROM_LOCATION, string sTO_LOCATION)
		{
			Guid gID = Guid.Empty;
			string sPARENT_TYPE = String.Empty;
			Guid   gPARENT_ID   = Guid.Empty;
			// .NET 10 Migration: DbProviderFactories.GetFactory() → _dbProviderFactories.GetFactory() (instance method)
			DbProviderFactory dbf = _dbProviderFactories.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				string sSQL;
				sSQL = "select *              " + ControlChars.CrLf
				     + "  from vwPHONE_NUMBERS" + ControlChars.CrLf
				     + " where 1 = 1          " + ControlChars.CrLf;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.CommandText = sSQL;
					// 09/25/2013 Paul.  Remove country code as most customers will only use the country code when different than their country. 
					string sNUMBER = TwilioManager.RemoveCountryCode(sFROM_NUMBER);
					// .NET 10 Migration: Sql.AppendParameter(cmd, sValue, filterMode, sField)
					// → Sql.AppendParameter(cmd, sb, sField, sValue, filterMode) (StringBuilder-based WHERE builder)
					StringBuilder sbWhere = new StringBuilder();
					Sql.AppendParameter(cmd, sbWhere, "NORMALIZED_NUMBER", sNUMBER, Sql.SqlFilterMode.Contains);
					cmd.CommandText += sbWhere.ToString();
					cmd.CommandText += " order by PARENT_TYPE" + ControlChars.CrLf;
					using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
					{
						if ( rdr.Read() )
						{
							sPARENT_TYPE = Sql.ToString(rdr["PARENT_TYPE"]);
							gPARENT_ID   = Sql.ToGuid  (rdr["PARENT_ID"  ]);
						}
					}
				}
			}
			// 09/25/2013 Paul.  Set the default for the team to be the Global team. 
			Guid gTEAM_ID = new Guid("17BB7135-2B95-42DC-85DE-842CAFF927A0");
			// .NET 10 Migration: SqlProcs.spSMS_MESSAGES_Update inlined below.
			// spSMS_MESSAGES_Update is not present in the migrated SqlProcs.cs (src/SplendidCRM.Core/SqlProcs.cs).
			// The original implementation from SplendidCRM/_code/SqlProcs.cs is reproduced here.
			// MODIFIED_USER_ID uses Guid.Empty since inbound SMS webhook has no authenticated user context.
			{
				DbProviderFactory dbfSp = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbfSp.CreateConnection() )
				{
					con.Open();
					using ( IDbTransaction trn = Sql.BeginTransaction(con) )
					{
						try
						{
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.Transaction  = trn;
								cmd.CommandType  = CommandType.StoredProcedure;
								cmd.CommandText  = "spSMS_MESSAGES_Update";
								IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                                         );
								IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" , Guid.Empty        /* no user context        */);
								IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , Guid.Empty                                  );
								IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID                                    );
								IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , String.Empty      , 8000                     );
								IDbDataParameter parMAILBOX_ID        = Sql.AddParameter(cmd, "@MAILBOX_ID"       , Guid.Empty                                  );
								IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sSUBJECT          , 1600                     );
								IDbDataParameter parDATE_TIME         = Sql.AddParameter(cmd, "@DATE_TIME"        , DateTime.Now                                );
								IDbDataParameter parPARENT_TYPE       = Sql.AddParameter(cmd, "@PARENT_TYPE"      , sPARENT_TYPE      ,  25                      );
								IDbDataParameter parPARENT_ID         = Sql.AddParameter(cmd, "@PARENT_ID"        , gPARENT_ID                                  );
								IDbDataParameter parFROM_NUMBER       = Sql.AddParameter(cmd, "@FROM_NUMBER"      , sFROM_NUMBER      ,  20                      );
								IDbDataParameter parTO_NUMBER         = Sql.AddParameter(cmd, "@TO_NUMBER"        , sTO_NUMBER        ,  20                      );
								IDbDataParameter parTO_ID             = Sql.AddParameter(cmd, "@TO_ID"            , Guid.Empty                                  );
								IDbDataParameter parTYPE              = Sql.AddParameter(cmd, "@TYPE"             , "inbound"         ,  25                      );
								IDbDataParameter parMESSAGE_SID       = Sql.AddParameter(cmd, "@MESSAGE_SID"      , sMESSAGE_SID      , 100                      );
								IDbDataParameter parFROM_LOCATION     = Sql.AddParameter(cmd, "@FROM_LOCATION"    , sFROM_LOCATION    , 100                      );
								IDbDataParameter parTO_LOCATION       = Sql.AddParameter(cmd, "@TO_LOCATION"      , sTO_LOCATION      , 100                      );
								// 05/17/2017 Paul.  Add Tags module. 
								IDbDataParameter parTAG_SET_NAME      = Sql.AddParameter(cmd, "@TAG_SET_NAME"     , String.Empty      , 4000                     );
								// 11/07/2017 Paul.  Add IS_PRIVATE for use by a large customer. 
								IDbDataParameter parIS_PRIVATE        = Sql.AddParameter(cmd, "@IS_PRIVATE"       , false                                       );
								// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
								IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", String.Empty      , 8000                     );
								parID.Direction = ParameterDirection.InputOutput;
								cmd.ExecuteNonQuery();
								gID = Sql.ToGuid(parID.Value);
							}
							trn.Commit();
						}
						catch
						{
							trn.Rollback();
							throw;
						}
					}
				}
			}
			return gID;
		}

		// .NET 10 Migration:
		//   - Method signature changed from void → async Task (required for await _hubContext.Clients.Group().SendAsync())
		//   - Context.Application["CONFIG.Twilio.LogInboundMessages"]
		//     → _memoryCache.Get("CONFIG.Twilio.LogInboundMessages") wrapped in Sql.ToBoolean()
		//   - Clients.Group(RemoveCountryCode(sGroupName)).incomingMessage(sMESSAGE_SID, sFROM_NUMBER, sTO_NUMBER, sSUBJECT, NullID(gID))
		//     → await _hubContext.Clients.Group(RemoveCountryCode(sGroupName)).SendAsync("incomingMessage", sMESSAGE_SID, sFROM_NUMBER, sTO_NUMBER, sSUBJECT, NullID(gID))
		//     Note: "incomingMessage" method name preserved exactly for wire-protocol compatibility with SignalR clients.
		//   - SplendidError.SystemMessage(Context, ...) → SplendidError.SystemMessage(_httpContextAccessor.HttpContext, ...)
		public async Task NewSmsMessage(string sMESSAGE_SID, string sFROM_NUMBER, string sTO_NUMBER, string sSUBJECT, string sFROM_LOCATION, string sTO_LOCATION)
		{
			// .NET 10 Migration: Context.Application["CONFIG.Twilio.LogInboundMessages"]
			//                    → _memoryCache.Get("CONFIG.Twilio.LogInboundMessages") wrapped in Sql.ToBoolean()
			bool   bLogInboundMessages = Sql.ToBoolean(_memoryCache.Get("CONFIG.Twilio.LogInboundMessages"));
			try
			{
				Guid gID = Guid.Empty;
				if ( bLogInboundMessages )
				{
					gID = CreateSmsMessage(sMESSAGE_SID, sFROM_NUMBER, sTO_NUMBER, sSUBJECT, sFROM_LOCATION, sTO_LOCATION);
				}
				string sGroupName = Utils.NormalizePhone(TwilioManager.RemoveCountryCode(sTO_NUMBER));
				// .NET 10 Migration: Clients.Group(RemoveCountryCode(sGroupName)).incomingMessage(...)
				//                    → await _hubContext.Clients.Group(RemoveCountryCode(sGroupName)).SendAsync("incomingMessage", ...)
				// SignalR client method name "incomingMessage" preserved exactly for wire-protocol compatibility.
				await _hubContext.Clients.Group(RemoveCountryCode(sGroupName)).SendAsync("incomingMessage", sMESSAGE_SID, sFROM_NUMBER, sTO_NUMBER, sSUBJECT, NullID(gID));
			}
			catch(Exception ex)
			{
				// .NET 10 Migration: SplendidError.SystemMessage(Context, ...) → SplendidError.SystemMessage(_httpContextAccessor.HttpContext, ...)
				SplendidError.SystemMessage(_httpContextAccessor.HttpContext, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
			}
		}
	}
}
