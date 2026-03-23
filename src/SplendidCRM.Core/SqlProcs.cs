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
// .NET 10 ASP.NET Core Migration Notes:
//   - REMOVED: using System.Web; (not used in this file)
//   - CHANGED: Security.USER_ID (static property) → _security?.USER_ID ?? Guid.Empty (instance via ambient field)
//   - CHANGED: DbProviderFactories.GetFactory() (static) → _dbProviderFactories!.GetFactory() (instance via ambient field)
//   - ADDED:   private static Security _security; ambient field (set via SetAmbient)
//   - ADDED:   private static DbProviderFactories _dbProviderFactories; ambient field (set via SetAmbient)
//   - ADDED:   public static void SetAmbient(Security, DbProviderFactories) registration method
//   - PRESERVED: namespace SplendidCRM, all public static method signatures, partial class keyword
//   - PRESERVED: All stored procedure calling patterns, parameter mappings, IDbTransaction overloads
//   - PRESERVED: Factory() method with DynamicFactory fallback for unknown procedure names
using System;
using System.Data;
using System.Data.Common;
using System.Xml;

namespace SplendidCRM
{
	/// <summary>
	/// SqlProcs: Partial class providing static stored procedure wrapper methods for common CRM entities.
	/// Migrated from .NET Framework 4.8 to .NET 10 ASP.NET Core.
	/// Static ambient fields (_security, _dbProviderFactories) replace the previous static HttpContext/Application access.
	/// Call SetAmbient() once during application startup (typically from Program.cs or a DI-aware initializer).
	/// </summary>
	public partial class SqlProcs
	{
		// .NET 10 Migration: Ambient static fields replace HttpContext.Current.Application and static Security access.
		// Set via SetAmbient() during DI container initialization (Program.cs or IHostedService startup).
		private static Security?            _security;
		private static DbProviderFactories? _dbProviderFactories;

		/// <summary>
		/// Registers the ambient service instances used by all static stored procedure wrapper methods.
		/// Must be called once during application startup before any sp* methods are invoked.
		/// </summary>
		public static void SetAmbient(Security security, DbProviderFactories dbProviderFactories)
		{
			_security            = security;
			_dbProviderFactories = dbProviderFactories;
		}

		/// <summary>
		/// Writes the expanded SQL command text to debug output for tracing/diagnostics.
		/// Preserved from source — used by transaction-accepting overloads.
		/// </summary>
		private static void Trace(IDbCommand cmd)
		{
			System.Diagnostics.Debug.WriteLine("SqlProcs.Trace:	exec dbo." + Sql.ExpandParameters(cmd) + ";");
		}

		#region spACCOUNTS_CONTACTS_Update
		public static void spACCOUNTS_CONTACTS_Update(Guid gACCOUNT_ID, Guid gCONTACT_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spACCOUNTS_CONTACTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parACCOUNT_ID       = Sql.AddParameter(cmd, "@ACCOUNT_ID"      , gACCOUNT_ID        );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							cmd.ExecuteNonQuery();
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

		public static void spACCOUNTS_CONTACTS_Update(Guid gACCOUNT_ID, Guid gCONTACT_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spACCOUNTS_CONTACTS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
				IDbDataParameter parACCOUNT_ID       = Sql.AddParameter(cmd, "@ACCOUNT_ID"      , gACCOUNT_ID        );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spACCOUNTS_Delete
		public static void spACCOUNTS_Delete(Guid gID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spACCOUNTS_Delete";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                              );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty );
							cmd.ExecuteNonQuery();
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

		public static void spACCOUNTS_Delete(Guid gID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spACCOUNTS_Delete";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                              );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spACCOUNTS_New
		public static void spACCOUNTS_New(ref Guid gID, string sNAME, string sPHONE_OFFICE, string sWEBSITE, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spACCOUNTS_New";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                              );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty );
							IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME                            , 150);
							IDbDataParameter parPHONE_OFFICE     = Sql.AddParameter(cmd, "@PHONE_OFFICE"    , sPHONE_OFFICE                    ,  25);
							IDbDataParameter parWEBSITE          = Sql.AddParameter(cmd, "@WEBSITE"         , sWEBSITE                         , 255);
							IDbDataParameter parASSIGNED_USER_ID = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", gASSIGNED_USER_ID                );
							IDbDataParameter parTEAM_ID          = Sql.AddParameter(cmd, "@TEAM_ID"         , gTEAM_ID                         );
							IDbDataParameter parTEAM_SET_LIST    = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"   , sTEAM_SET_LIST                   , 8000);
							IDbDataParameter parASSIGNED_SET_LIST= Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST              , 8000);
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

		public static void spACCOUNTS_New(ref Guid gID, string sNAME, string sPHONE_OFFICE, string sWEBSITE, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spACCOUNTS_New";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                              );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty );
				IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME                            , 150);
				IDbDataParameter parPHONE_OFFICE     = Sql.AddParameter(cmd, "@PHONE_OFFICE"    , sPHONE_OFFICE                    ,  25);
				IDbDataParameter parWEBSITE          = Sql.AddParameter(cmd, "@WEBSITE"         , sWEBSITE                         , 255);
				IDbDataParameter parASSIGNED_USER_ID = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", gASSIGNED_USER_ID                );
				IDbDataParameter parTEAM_ID          = Sql.AddParameter(cmd, "@TEAM_ID"         , gTEAM_ID                         );
				IDbDataParameter parTEAM_SET_LIST    = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"   , sTEAM_SET_LIST                   , 8000);
				IDbDataParameter parASSIGNED_SET_LIST= Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST              , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion


		#region spACCOUNTS_Update
		public static void spACCOUNTS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, string sACCOUNT_TYPE, Guid gPARENT_ID, string sINDUSTRY, string sANNUAL_REVENUE, string sPHONE_FAX, string sBILLING_ADDRESS_STREET, string sBILLING_ADDRESS_CITY, string sBILLING_ADDRESS_STATE, string sBILLING_ADDRESS_POSTALCODE, string sBILLING_ADDRESS_COUNTRY, string sDESCRIPTION, string sRATING, string sPHONE_OFFICE, string sPHONE_ALTERNATE, string sEMAIL1, string sEMAIL2, string sWEBSITE, string sOWNERSHIP, string sEMPLOYEES, string sSIC_CODE, string sTICKER_SYMBOL, string sSHIPPING_ADDRESS_STREET, string sSHIPPING_ADDRESS_CITY, string sSHIPPING_ADDRESS_STATE, string sSHIPPING_ADDRESS_POSTALCODE, string sSHIPPING_ADDRESS_COUNTRY, string sACCOUNT_NUMBER, Guid gTEAM_ID, string sTEAM_SET_LIST, bool bEXCHANGE_FOLDER, string sPICTURE, string sTAG_SET_NAME, string sNAICS_SET_NAME, bool bDO_NOT_CALL, bool bEMAIL_OPT_OUT, bool bINVALID_EMAIL, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spACCOUNTS_Update";
							IDbDataParameter parID                          = Sql.AddParameter(cmd, "@ID"                         , gID                              );
							IDbDataParameter parMODIFIED_USER_ID            = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"           , _security?.USER_ID ?? Guid.Empty );
							IDbDataParameter parASSIGNED_USER_ID            = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"           , gASSIGNED_USER_ID                );
							IDbDataParameter parNAME                        = Sql.AddParameter(cmd, "@NAME"                       , sNAME                            , 150);
							IDbDataParameter parACCOUNT_TYPE                = Sql.AddParameter(cmd, "@ACCOUNT_TYPE"               , sACCOUNT_TYPE                    ,  25);
							IDbDataParameter parPARENT_ID                   = Sql.AddParameter(cmd, "@PARENT_ID"                  , gPARENT_ID                       );
							IDbDataParameter parINDUSTRY                    = Sql.AddParameter(cmd, "@INDUSTRY"                   , sINDUSTRY                        ,  25);
							IDbDataParameter parANNUAL_REVENUE              = Sql.AddParameter(cmd, "@ANNUAL_REVENUE"             , sANNUAL_REVENUE                  ,  25);
							IDbDataParameter parPHONE_FAX                   = Sql.AddParameter(cmd, "@PHONE_FAX"                  , sPHONE_FAX                       ,  25);
							IDbDataParameter parBILLING_ADDRESS_STREET      = Sql.AddParameter(cmd, "@BILLING_ADDRESS_STREET"     , sBILLING_ADDRESS_STREET          , 150);
							IDbDataParameter parBILLING_ADDRESS_CITY        = Sql.AddParameter(cmd, "@BILLING_ADDRESS_CITY"       , sBILLING_ADDRESS_CITY            , 100);
							IDbDataParameter parBILLING_ADDRESS_STATE       = Sql.AddParameter(cmd, "@BILLING_ADDRESS_STATE"      , sBILLING_ADDRESS_STATE           , 100);
							IDbDataParameter parBILLING_ADDRESS_POSTALCODE  = Sql.AddParameter(cmd, "@BILLING_ADDRESS_POSTALCODE" , sBILLING_ADDRESS_POSTALCODE      ,  20);
							IDbDataParameter parBILLING_ADDRESS_COUNTRY     = Sql.AddParameter(cmd, "@BILLING_ADDRESS_COUNTRY"    , sBILLING_ADDRESS_COUNTRY         , 100);
							IDbDataParameter parDESCRIPTION                 = Sql.AddParameter(cmd, "@DESCRIPTION"                , sDESCRIPTION                     );
							IDbDataParameter parRATING                      = Sql.AddParameter(cmd, "@RATING"                     , sRATING                          ,  25);
							IDbDataParameter parPHONE_OFFICE                = Sql.AddParameter(cmd, "@PHONE_OFFICE"               , sPHONE_OFFICE                    ,  25);
							IDbDataParameter parPHONE_ALTERNATE             = Sql.AddParameter(cmd, "@PHONE_ALTERNATE"            , sPHONE_ALTERNATE                 ,  25);
							IDbDataParameter parEMAIL1                      = Sql.AddParameter(cmd, "@EMAIL1"                     , sEMAIL1                          , 100);
							IDbDataParameter parEMAIL2                      = Sql.AddParameter(cmd, "@EMAIL2"                     , sEMAIL2                          , 100);
							IDbDataParameter parWEBSITE                     = Sql.AddParameter(cmd, "@WEBSITE"                    , sWEBSITE                         , 255);
							IDbDataParameter parOWNERSHIP                   = Sql.AddParameter(cmd, "@OWNERSHIP"                  , sOWNERSHIP                       , 100);
							IDbDataParameter parEMPLOYEES                   = Sql.AddParameter(cmd, "@EMPLOYEES"                  , sEMPLOYEES                       ,  10);
							IDbDataParameter parSIC_CODE                    = Sql.AddParameter(cmd, "@SIC_CODE"                   , sSIC_CODE                        ,  10);
							IDbDataParameter parTICKER_SYMBOL               = Sql.AddParameter(cmd, "@TICKER_SYMBOL"              , sTICKER_SYMBOL                   ,  10);
							IDbDataParameter parSHIPPING_ADDRESS_STREET     = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_STREET"    , sSHIPPING_ADDRESS_STREET         , 150);
							IDbDataParameter parSHIPPING_ADDRESS_CITY       = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_CITY"      , sSHIPPING_ADDRESS_CITY           , 100);
							IDbDataParameter parSHIPPING_ADDRESS_STATE      = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_STATE"     , sSHIPPING_ADDRESS_STATE          , 100);
							IDbDataParameter parSHIPPING_ADDRESS_POSTALCODE = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_POSTALCODE", sSHIPPING_ADDRESS_POSTALCODE     ,  20);
							IDbDataParameter parSHIPPING_ADDRESS_COUNTRY    = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_COUNTRY"   , sSHIPPING_ADDRESS_COUNTRY        , 100);
							IDbDataParameter parACCOUNT_NUMBER              = Sql.AddParameter(cmd, "@ACCOUNT_NUMBER"             , sACCOUNT_NUMBER                  ,  30);
							IDbDataParameter parTEAM_ID                     = Sql.AddParameter(cmd, "@TEAM_ID"                    , gTEAM_ID                         );
							IDbDataParameter parTEAM_SET_LIST               = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"              , sTEAM_SET_LIST                   , 8000);
							IDbDataParameter parEXCHANGE_FOLDER             = Sql.AddParameter(cmd, "@EXCHANGE_FOLDER"            , bEXCHANGE_FOLDER                 );
							IDbDataParameter parPICTURE                     = Sql.AddParameter(cmd, "@PICTURE"                    , sPICTURE                         );
							IDbDataParameter parTAG_SET_NAME                = Sql.AddParameter(cmd, "@TAG_SET_NAME"               , sTAG_SET_NAME                    , 4000);
							IDbDataParameter parNAICS_SET_NAME              = Sql.AddParameter(cmd, "@NAICS_SET_NAME"             , sNAICS_SET_NAME                  , 4000);
							IDbDataParameter parDO_NOT_CALL                 = Sql.AddParameter(cmd, "@DO_NOT_CALL"                , bDO_NOT_CALL                     );
							IDbDataParameter parEMAIL_OPT_OUT               = Sql.AddParameter(cmd, "@EMAIL_OPT_OUT"              , bEMAIL_OPT_OUT                   );
							IDbDataParameter parINVALID_EMAIL               = Sql.AddParameter(cmd, "@INVALID_EMAIL"              , bINVALID_EMAIL                   );
							IDbDataParameter parASSIGNED_SET_LIST           = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"          , sASSIGNED_SET_LIST               , 8000);
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

		public static void spACCOUNTS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, string sACCOUNT_TYPE, Guid gPARENT_ID, string sINDUSTRY, string sANNUAL_REVENUE, string sPHONE_FAX, string sBILLING_ADDRESS_STREET, string sBILLING_ADDRESS_CITY, string sBILLING_ADDRESS_STATE, string sBILLING_ADDRESS_POSTALCODE, string sBILLING_ADDRESS_COUNTRY, string sDESCRIPTION, string sRATING, string sPHONE_OFFICE, string sPHONE_ALTERNATE, string sEMAIL1, string sEMAIL2, string sWEBSITE, string sOWNERSHIP, string sEMPLOYEES, string sSIC_CODE, string sTICKER_SYMBOL, string sSHIPPING_ADDRESS_STREET, string sSHIPPING_ADDRESS_CITY, string sSHIPPING_ADDRESS_STATE, string sSHIPPING_ADDRESS_POSTALCODE, string sSHIPPING_ADDRESS_COUNTRY, string sACCOUNT_NUMBER, Guid gTEAM_ID, string sTEAM_SET_LIST, bool bEXCHANGE_FOLDER, string sPICTURE, string sTAG_SET_NAME, string sNAICS_SET_NAME, bool bDO_NOT_CALL, bool bEMAIL_OPT_OUT, bool bINVALID_EMAIL, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spACCOUNTS_Update";
				IDbDataParameter parID                          = Sql.AddParameter(cmd, "@ID"                         , gID                              );
				IDbDataParameter parMODIFIED_USER_ID            = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"           , _security?.USER_ID ?? Guid.Empty );
				IDbDataParameter parASSIGNED_USER_ID            = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"           , gASSIGNED_USER_ID                );
				IDbDataParameter parNAME                        = Sql.AddParameter(cmd, "@NAME"                       , sNAME                            , 150);
				IDbDataParameter parACCOUNT_TYPE                = Sql.AddParameter(cmd, "@ACCOUNT_TYPE"               , sACCOUNT_TYPE                    ,  25);
				IDbDataParameter parPARENT_ID                   = Sql.AddParameter(cmd, "@PARENT_ID"                  , gPARENT_ID                       );
				IDbDataParameter parINDUSTRY                    = Sql.AddParameter(cmd, "@INDUSTRY"                   , sINDUSTRY                        ,  25);
				IDbDataParameter parANNUAL_REVENUE              = Sql.AddParameter(cmd, "@ANNUAL_REVENUE"             , sANNUAL_REVENUE                  ,  25);
				IDbDataParameter parPHONE_FAX                   = Sql.AddParameter(cmd, "@PHONE_FAX"                  , sPHONE_FAX                       ,  25);
				IDbDataParameter parBILLING_ADDRESS_STREET      = Sql.AddParameter(cmd, "@BILLING_ADDRESS_STREET"     , sBILLING_ADDRESS_STREET          , 150);
				IDbDataParameter parBILLING_ADDRESS_CITY        = Sql.AddParameter(cmd, "@BILLING_ADDRESS_CITY"       , sBILLING_ADDRESS_CITY            , 100);
				IDbDataParameter parBILLING_ADDRESS_STATE       = Sql.AddParameter(cmd, "@BILLING_ADDRESS_STATE"      , sBILLING_ADDRESS_STATE           , 100);
				IDbDataParameter parBILLING_ADDRESS_POSTALCODE  = Sql.AddParameter(cmd, "@BILLING_ADDRESS_POSTALCODE" , sBILLING_ADDRESS_POSTALCODE      ,  20);
				IDbDataParameter parBILLING_ADDRESS_COUNTRY     = Sql.AddParameter(cmd, "@BILLING_ADDRESS_COUNTRY"    , sBILLING_ADDRESS_COUNTRY         , 100);
				IDbDataParameter parDESCRIPTION                 = Sql.AddParameter(cmd, "@DESCRIPTION"                , sDESCRIPTION                     );
				IDbDataParameter parRATING                      = Sql.AddParameter(cmd, "@RATING"                     , sRATING                          ,  25);
				IDbDataParameter parPHONE_OFFICE                = Sql.AddParameter(cmd, "@PHONE_OFFICE"               , sPHONE_OFFICE                    ,  25);
				IDbDataParameter parPHONE_ALTERNATE             = Sql.AddParameter(cmd, "@PHONE_ALTERNATE"            , sPHONE_ALTERNATE                 ,  25);
				IDbDataParameter parEMAIL1                      = Sql.AddParameter(cmd, "@EMAIL1"                     , sEMAIL1                          , 100);
				IDbDataParameter parEMAIL2                      = Sql.AddParameter(cmd, "@EMAIL2"                     , sEMAIL2                          , 100);
				IDbDataParameter parWEBSITE                     = Sql.AddParameter(cmd, "@WEBSITE"                    , sWEBSITE                         , 255);
				IDbDataParameter parOWNERSHIP                   = Sql.AddParameter(cmd, "@OWNERSHIP"                  , sOWNERSHIP                       , 100);
				IDbDataParameter parEMPLOYEES                   = Sql.AddParameter(cmd, "@EMPLOYEES"                  , sEMPLOYEES                       ,  10);
				IDbDataParameter parSIC_CODE                    = Sql.AddParameter(cmd, "@SIC_CODE"                   , sSIC_CODE                        ,  10);
				IDbDataParameter parTICKER_SYMBOL               = Sql.AddParameter(cmd, "@TICKER_SYMBOL"              , sTICKER_SYMBOL                   ,  10);
				IDbDataParameter parSHIPPING_ADDRESS_STREET     = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_STREET"    , sSHIPPING_ADDRESS_STREET         , 150);
				IDbDataParameter parSHIPPING_ADDRESS_CITY       = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_CITY"      , sSHIPPING_ADDRESS_CITY           , 100);
				IDbDataParameter parSHIPPING_ADDRESS_STATE      = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_STATE"     , sSHIPPING_ADDRESS_STATE          , 100);
				IDbDataParameter parSHIPPING_ADDRESS_POSTALCODE = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_POSTALCODE", sSHIPPING_ADDRESS_POSTALCODE     ,  20);
				IDbDataParameter parSHIPPING_ADDRESS_COUNTRY    = Sql.AddParameter(cmd, "@SHIPPING_ADDRESS_COUNTRY"   , sSHIPPING_ADDRESS_COUNTRY        , 100);
				IDbDataParameter parACCOUNT_NUMBER              = Sql.AddParameter(cmd, "@ACCOUNT_NUMBER"             , sACCOUNT_NUMBER                  ,  30);
				IDbDataParameter parTEAM_ID                     = Sql.AddParameter(cmd, "@TEAM_ID"                    , gTEAM_ID                         );
				IDbDataParameter parTEAM_SET_LIST               = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"              , sTEAM_SET_LIST                   , 8000);
				IDbDataParameter parEXCHANGE_FOLDER             = Sql.AddParameter(cmd, "@EXCHANGE_FOLDER"            , bEXCHANGE_FOLDER                 );
				IDbDataParameter parPICTURE                     = Sql.AddParameter(cmd, "@PICTURE"                    , sPICTURE                         );
				IDbDataParameter parTAG_SET_NAME                = Sql.AddParameter(cmd, "@TAG_SET_NAME"               , sTAG_SET_NAME                    , 4000);
				IDbDataParameter parNAICS_SET_NAME              = Sql.AddParameter(cmd, "@NAICS_SET_NAME"             , sNAICS_SET_NAME                  , 4000);
				IDbDataParameter parDO_NOT_CALL                 = Sql.AddParameter(cmd, "@DO_NOT_CALL"                , bDO_NOT_CALL                     );
				IDbDataParameter parEMAIL_OPT_OUT               = Sql.AddParameter(cmd, "@EMAIL_OPT_OUT"              , bEMAIL_OPT_OUT                   );
				IDbDataParameter parINVALID_EMAIL               = Sql.AddParameter(cmd, "@INVALID_EMAIL"              , bINVALID_EMAIL                   );
				IDbDataParameter parASSIGNED_SET_LIST           = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"          , sASSIGNED_SET_LIST               , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region cmdACCOUNTS_Update
		public static IDbCommand cmdACCOUNTS_Update(IDbConnection con)
		{
			IDbCommand cmd = con.CreateCommand();
			cmd.CommandType = CommandType.StoredProcedure;
			cmd.CommandText = "spACCOUNTS_Update";
			IDbDataParameter parID                          = Sql.CreateParameter(cmd, "@ID"                         , "Guid",  16);
			IDbDataParameter parMODIFIED_USER_ID            = Sql.CreateParameter(cmd, "@MODIFIED_USER_ID"           , "Guid",  16);
			IDbDataParameter parASSIGNED_USER_ID            = Sql.CreateParameter(cmd, "@ASSIGNED_USER_ID"           , "Guid",  16);
			IDbDataParameter parNAME                        = Sql.CreateParameter(cmd, "@NAME"                       , "string", 150);
			IDbDataParameter parACCOUNT_TYPE                = Sql.CreateParameter(cmd, "@ACCOUNT_TYPE"               , "string",  25);
			IDbDataParameter parPARENT_ID                   = Sql.CreateParameter(cmd, "@PARENT_ID"                  , "Guid",  16);
			IDbDataParameter parINDUSTRY                    = Sql.CreateParameter(cmd, "@INDUSTRY"                   , "string",  25);
			IDbDataParameter parANNUAL_REVENUE              = Sql.CreateParameter(cmd, "@ANNUAL_REVENUE"             , "string",  25);
			IDbDataParameter parPHONE_FAX                   = Sql.CreateParameter(cmd, "@PHONE_FAX"                  , "string",  25);
			IDbDataParameter parBILLING_ADDRESS_STREET      = Sql.CreateParameter(cmd, "@BILLING_ADDRESS_STREET"     , "string", 150);
			IDbDataParameter parBILLING_ADDRESS_CITY        = Sql.CreateParameter(cmd, "@BILLING_ADDRESS_CITY"       , "string", 100);
			IDbDataParameter parBILLING_ADDRESS_STATE       = Sql.CreateParameter(cmd, "@BILLING_ADDRESS_STATE"      , "string", 100);
			IDbDataParameter parBILLING_ADDRESS_POSTALCODE  = Sql.CreateParameter(cmd, "@BILLING_ADDRESS_POSTALCODE" , "string",  20);
			IDbDataParameter parBILLING_ADDRESS_COUNTRY     = Sql.CreateParameter(cmd, "@BILLING_ADDRESS_COUNTRY"    , "string", 100);
			IDbDataParameter parDESCRIPTION                 = Sql.CreateParameter(cmd, "@DESCRIPTION"                , "string", 104857600);
			IDbDataParameter parRATING                      = Sql.CreateParameter(cmd, "@RATING"                     , "string",  25);
			IDbDataParameter parPHONE_OFFICE                = Sql.CreateParameter(cmd, "@PHONE_OFFICE"               , "string",  25);
			IDbDataParameter parPHONE_ALTERNATE             = Sql.CreateParameter(cmd, "@PHONE_ALTERNATE"            , "string",  25);
			IDbDataParameter parEMAIL1                      = Sql.CreateParameter(cmd, "@EMAIL1"                     , "string", 100);
			IDbDataParameter parEMAIL2                      = Sql.CreateParameter(cmd, "@EMAIL2"                     , "string", 100);
			IDbDataParameter parWEBSITE                     = Sql.CreateParameter(cmd, "@WEBSITE"                    , "string", 255);
			IDbDataParameter parOWNERSHIP                   = Sql.CreateParameter(cmd, "@OWNERSHIP"                  , "string", 100);
			IDbDataParameter parEMPLOYEES                   = Sql.CreateParameter(cmd, "@EMPLOYEES"                  , "string",  10);
			IDbDataParameter parSIC_CODE                    = Sql.CreateParameter(cmd, "@SIC_CODE"                   , "string",  10);
			IDbDataParameter parTICKER_SYMBOL               = Sql.CreateParameter(cmd, "@TICKER_SYMBOL"              , "string",  10);
			IDbDataParameter parSHIPPING_ADDRESS_STREET     = Sql.CreateParameter(cmd, "@SHIPPING_ADDRESS_STREET"    , "string", 150);
			IDbDataParameter parSHIPPING_ADDRESS_CITY       = Sql.CreateParameter(cmd, "@SHIPPING_ADDRESS_CITY"      , "string", 100);
			IDbDataParameter parSHIPPING_ADDRESS_STATE      = Sql.CreateParameter(cmd, "@SHIPPING_ADDRESS_STATE"     , "string", 100);
			IDbDataParameter parSHIPPING_ADDRESS_POSTALCODE = Sql.CreateParameter(cmd, "@SHIPPING_ADDRESS_POSTALCODE", "string",  20);
			IDbDataParameter parSHIPPING_ADDRESS_COUNTRY    = Sql.CreateParameter(cmd, "@SHIPPING_ADDRESS_COUNTRY"   , "string", 100);
			IDbDataParameter parACCOUNT_NUMBER              = Sql.CreateParameter(cmd, "@ACCOUNT_NUMBER"             , "string",  30);
			IDbDataParameter parTEAM_ID                     = Sql.CreateParameter(cmd, "@TEAM_ID"                    , "Guid",  16);
			IDbDataParameter parTEAM_SET_LIST               = Sql.CreateParameter(cmd, "@TEAM_SET_LIST"              , "ansistring", 8000);
			IDbDataParameter parEXCHANGE_FOLDER             = Sql.CreateParameter(cmd, "@EXCHANGE_FOLDER"            , "bool",   1);
			IDbDataParameter parPICTURE                     = Sql.CreateParameter(cmd, "@PICTURE"                    , "string", 104857600);
			IDbDataParameter parTAG_SET_NAME                = Sql.CreateParameter(cmd, "@TAG_SET_NAME"               , "string", 4000);
			IDbDataParameter parNAICS_SET_NAME              = Sql.CreateParameter(cmd, "@NAICS_SET_NAME"             , "string", 4000);
			IDbDataParameter parDO_NOT_CALL                 = Sql.CreateParameter(cmd, "@DO_NOT_CALL"                , "bool",   1);
			IDbDataParameter parEMAIL_OPT_OUT               = Sql.CreateParameter(cmd, "@EMAIL_OPT_OUT"              , "bool",   1);
			IDbDataParameter parINVALID_EMAIL               = Sql.CreateParameter(cmd, "@INVALID_EMAIL"              , "bool",   1);
			IDbDataParameter parASSIGNED_SET_LIST           = Sql.CreateParameter(cmd, "@ASSIGNED_SET_LIST"          , "ansistring", 8000);
			parID.Direction = ParameterDirection.InputOutput;
			return cmd;
		}
		#endregion


		#region spCALLS_CONTACTS_Update
		public static void spCALLS_CONTACTS_Update(Guid gCALL_ID, Guid gCONTACT_ID, bool bREQUIRED, string sACCEPT_STATUS)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCALLS_CONTACTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parCALL_ID          = Sql.AddParameter(cmd, "@CALL_ID"         , gCALL_ID          );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID       );
							IDbDataParameter parREQUIRED         = Sql.AddParameter(cmd, "@REQUIRED"        , bREQUIRED         );
							IDbDataParameter parACCEPT_STATUS    = Sql.AddParameter(cmd, "@ACCEPT_STATUS"   , sACCEPT_STATUS    ,  25);
							cmd.ExecuteNonQuery();
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

		public static void spCALLS_CONTACTS_Update(Guid gCALL_ID, Guid gCONTACT_ID, bool bREQUIRED, string sACCEPT_STATUS, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCALLS_CONTACTS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
				IDbDataParameter parCALL_ID          = Sql.AddParameter(cmd, "@CALL_ID"         , gCALL_ID          );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID       );
				IDbDataParameter parREQUIRED         = Sql.AddParameter(cmd, "@REQUIRED"        , bREQUIRED         );
				IDbDataParameter parACCEPT_STATUS    = Sql.AddParameter(cmd, "@ACCEPT_STATUS"   , sACCEPT_STATUS    ,  25);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spCALLS_EmailReminderSent
		public static void spCALLS_EmailReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCALLS_EmailReminderSent";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID               );
							IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE     ,  25);
							IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID       );
							cmd.ExecuteNonQuery();
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

		public static void spCALLS_EmailReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCALLS_EmailReminderSent";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID               );
				IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE     ,  25);
				IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID       );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spCALLS_SmsReminderSent
		public static void spCALLS_SmsReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCALLS_SmsReminderSent";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID               );
							IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE     ,  25);
							IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID       );
							cmd.ExecuteNonQuery();
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

		public static void spCALLS_SmsReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCALLS_SmsReminderSent";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID               );
				IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE     ,  25);
				IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID       );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion
		#region spCALLS_Update
		/// <summary>
		/// spCALLS_Update
		/// </summary>
		public static void spCALLS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, Int32 nDURATION_HOURS, Int32 nDURATION_MINUTES, DateTime dtDATE_TIME, string sPARENT_TYPE, Guid gPARENT_ID, string sSTATUS, string sDIRECTION, Int32 nREMINDER_TIME, string sDESCRIPTION, string sINVITEE_LIST, Guid gTEAM_ID, string sTEAM_SET_LIST, Int32 nEMAIL_REMINDER_TIME, bool bALL_DAY_EVENT, string sREPEAT_TYPE, Int32 nREPEAT_INTERVAL, string sREPEAT_DOW, DateTime dtREPEAT_UNTIL, Int32 nREPEAT_COUNT, Int32 nSMS_REMINDER_TIME, string sTAG_SET_NAME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCALLS_Update";
							IDbDataParameter parID                  = Sql.AddParameter(cmd, "@ID"                 , gID                   );
							IDbDataParameter parMODIFIED_USER_ID    = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"   ,  _security?.USER_ID ?? Guid.Empty     );
							IDbDataParameter parASSIGNED_USER_ID    = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"   , gASSIGNED_USER_ID     );
							IDbDataParameter parNAME                = Sql.AddParameter(cmd, "@NAME"               , sNAME                 , 150);
							IDbDataParameter parDURATION_HOURS      = Sql.AddParameter(cmd, "@DURATION_HOURS"     , nDURATION_HOURS       );
							IDbDataParameter parDURATION_MINUTES    = Sql.AddParameter(cmd, "@DURATION_MINUTES"   , nDURATION_MINUTES     );
							IDbDataParameter parDATE_TIME           = Sql.AddParameter(cmd, "@DATE_TIME"          , dtDATE_TIME           );
							IDbDataParameter parPARENT_TYPE         = Sql.AddParameter(cmd, "@PARENT_TYPE"        , sPARENT_TYPE          ,  25);
							IDbDataParameter parPARENT_ID           = Sql.AddParameter(cmd, "@PARENT_ID"          , gPARENT_ID            );
							IDbDataParameter parSTATUS              = Sql.AddParameter(cmd, "@STATUS"             , sSTATUS               ,  25);
							IDbDataParameter parDIRECTION           = Sql.AddParameter(cmd, "@DIRECTION"          , sDIRECTION            ,  25);
							IDbDataParameter parREMINDER_TIME       = Sql.AddParameter(cmd, "@REMINDER_TIME"      , nREMINDER_TIME        );
							IDbDataParameter parDESCRIPTION         = Sql.AddParameter(cmd, "@DESCRIPTION"        , sDESCRIPTION          );
							IDbDataParameter parINVITEE_LIST        = Sql.AddAnsiParam(cmd, "@INVITEE_LIST"       , sINVITEE_LIST         , 8000);
							IDbDataParameter parTEAM_ID             = Sql.AddParameter(cmd, "@TEAM_ID"            , gTEAM_ID              );
							IDbDataParameter parTEAM_SET_LIST       = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"      , sTEAM_SET_LIST        , 8000);
							IDbDataParameter parEMAIL_REMINDER_TIME = Sql.AddParameter(cmd, "@EMAIL_REMINDER_TIME", nEMAIL_REMINDER_TIME  );
							IDbDataParameter parALL_DAY_EVENT       = Sql.AddParameter(cmd, "@ALL_DAY_EVENT"      , bALL_DAY_EVENT        );
							IDbDataParameter parREPEAT_TYPE         = Sql.AddParameter(cmd, "@REPEAT_TYPE"        , sREPEAT_TYPE          ,  25);
							IDbDataParameter parREPEAT_INTERVAL     = Sql.AddParameter(cmd, "@REPEAT_INTERVAL"    , nREPEAT_INTERVAL      );
							IDbDataParameter parREPEAT_DOW          = Sql.AddParameter(cmd, "@REPEAT_DOW"         , sREPEAT_DOW           ,   7);
							IDbDataParameter parREPEAT_UNTIL        = Sql.AddParameter(cmd, "@REPEAT_UNTIL"       , dtREPEAT_UNTIL        );
							IDbDataParameter parREPEAT_COUNT        = Sql.AddParameter(cmd, "@REPEAT_COUNT"       , nREPEAT_COUNT         );
							IDbDataParameter parSMS_REMINDER_TIME   = Sql.AddParameter(cmd, "@SMS_REMINDER_TIME"  , nSMS_REMINDER_TIME    );
							IDbDataParameter parTAG_SET_NAME        = Sql.AddParameter(cmd, "@TAG_SET_NAME"       , sTAG_SET_NAME         , 4000);
							IDbDataParameter parIS_PRIVATE          = Sql.AddParameter(cmd, "@IS_PRIVATE"         , bIS_PRIVATE           );
							IDbDataParameter parASSIGNED_SET_LIST   = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"  , sASSIGNED_SET_LIST    , 8000);
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
		#endregion

		#region spCALLS_Update
		/// <summary>
		/// spCALLS_Update
		/// </summary>
		public static void spCALLS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, Int32 nDURATION_HOURS, Int32 nDURATION_MINUTES, DateTime dtDATE_TIME, string sPARENT_TYPE, Guid gPARENT_ID, string sSTATUS, string sDIRECTION, Int32 nREMINDER_TIME, string sDESCRIPTION, string sINVITEE_LIST, Guid gTEAM_ID, string sTEAM_SET_LIST, Int32 nEMAIL_REMINDER_TIME, bool bALL_DAY_EVENT, string sREPEAT_TYPE, Int32 nREPEAT_INTERVAL, string sREPEAT_DOW, DateTime dtREPEAT_UNTIL, Int32 nREPEAT_COUNT, Int32 nSMS_REMINDER_TIME, string sTAG_SET_NAME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCALLS_Update";
				IDbDataParameter parID                  = Sql.AddParameter(cmd, "@ID"                 , gID                   );
				IDbDataParameter parMODIFIED_USER_ID    = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"   ,  _security?.USER_ID ?? Guid.Empty     );
				IDbDataParameter parASSIGNED_USER_ID    = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"   , gASSIGNED_USER_ID     );
				IDbDataParameter parNAME                = Sql.AddParameter(cmd, "@NAME"               , sNAME                 , 150);
				IDbDataParameter parDURATION_HOURS      = Sql.AddParameter(cmd, "@DURATION_HOURS"     , nDURATION_HOURS       );
				IDbDataParameter parDURATION_MINUTES    = Sql.AddParameter(cmd, "@DURATION_MINUTES"   , nDURATION_MINUTES     );
				IDbDataParameter parDATE_TIME           = Sql.AddParameter(cmd, "@DATE_TIME"          , dtDATE_TIME           );
				IDbDataParameter parPARENT_TYPE         = Sql.AddParameter(cmd, "@PARENT_TYPE"        , sPARENT_TYPE          ,  25);
				IDbDataParameter parPARENT_ID           = Sql.AddParameter(cmd, "@PARENT_ID"          , gPARENT_ID            );
				IDbDataParameter parSTATUS              = Sql.AddParameter(cmd, "@STATUS"             , sSTATUS               ,  25);
				IDbDataParameter parDIRECTION           = Sql.AddParameter(cmd, "@DIRECTION"          , sDIRECTION            ,  25);
				IDbDataParameter parREMINDER_TIME       = Sql.AddParameter(cmd, "@REMINDER_TIME"      , nREMINDER_TIME        );
				IDbDataParameter parDESCRIPTION         = Sql.AddParameter(cmd, "@DESCRIPTION"        , sDESCRIPTION          );
				IDbDataParameter parINVITEE_LIST        = Sql.AddAnsiParam(cmd, "@INVITEE_LIST"       , sINVITEE_LIST         , 8000);
				IDbDataParameter parTEAM_ID             = Sql.AddParameter(cmd, "@TEAM_ID"            , gTEAM_ID              );
				IDbDataParameter parTEAM_SET_LIST       = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"      , sTEAM_SET_LIST        , 8000);
				IDbDataParameter parEMAIL_REMINDER_TIME = Sql.AddParameter(cmd, "@EMAIL_REMINDER_TIME", nEMAIL_REMINDER_TIME  );
				IDbDataParameter parALL_DAY_EVENT       = Sql.AddParameter(cmd, "@ALL_DAY_EVENT"      , bALL_DAY_EVENT        );
				IDbDataParameter parREPEAT_TYPE         = Sql.AddParameter(cmd, "@REPEAT_TYPE"        , sREPEAT_TYPE          ,  25);
				IDbDataParameter parREPEAT_INTERVAL     = Sql.AddParameter(cmd, "@REPEAT_INTERVAL"    , nREPEAT_INTERVAL      );
				IDbDataParameter parREPEAT_DOW          = Sql.AddParameter(cmd, "@REPEAT_DOW"         , sREPEAT_DOW           ,   7);
				IDbDataParameter parREPEAT_UNTIL        = Sql.AddParameter(cmd, "@REPEAT_UNTIL"       , dtREPEAT_UNTIL        );
				IDbDataParameter parREPEAT_COUNT        = Sql.AddParameter(cmd, "@REPEAT_COUNT"       , nREPEAT_COUNT         );
				IDbDataParameter parSMS_REMINDER_TIME   = Sql.AddParameter(cmd, "@SMS_REMINDER_TIME"  , nSMS_REMINDER_TIME    );
				IDbDataParameter parTAG_SET_NAME        = Sql.AddParameter(cmd, "@TAG_SET_NAME"       , sTAG_SET_NAME         , 4000);
				IDbDataParameter parIS_PRIVATE          = Sql.AddParameter(cmd, "@IS_PRIVATE"         , bIS_PRIVATE           );
				IDbDataParameter parASSIGNED_SET_LIST   = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"  , sASSIGNED_SET_LIST    , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region cmdCALLS_Update
		/// <summary>
		/// spCALLS_Update
		/// </summary>
		public static IDbCommand cmdCALLS_Update(IDbConnection con)
		{
			IDbCommand cmd = con.CreateCommand();
			cmd.CommandType = CommandType.StoredProcedure;
			cmd.CommandText = "spCALLS_Update";
			IDbDataParameter parID                  = Sql.CreateParameter(cmd, "@ID"                 , "Guid",  16);
			IDbDataParameter parMODIFIED_USER_ID    = Sql.CreateParameter(cmd, "@MODIFIED_USER_ID"   , "Guid",  16);
			IDbDataParameter parASSIGNED_USER_ID    = Sql.CreateParameter(cmd, "@ASSIGNED_USER_ID"   , "Guid",  16);
			IDbDataParameter parNAME                = Sql.CreateParameter(cmd, "@NAME"               , "string", 150);
			IDbDataParameter parDURATION_HOURS      = Sql.CreateParameter(cmd, "@DURATION_HOURS"     , "Int32",   4);
			IDbDataParameter parDURATION_MINUTES    = Sql.CreateParameter(cmd, "@DURATION_MINUTES"   , "Int32",   4);
			IDbDataParameter parDATE_TIME           = Sql.CreateParameter(cmd, "@DATE_TIME"          , "DateTime",   8);
			IDbDataParameter parPARENT_TYPE         = Sql.CreateParameter(cmd, "@PARENT_TYPE"        , "string",  25);
			IDbDataParameter parPARENT_ID           = Sql.CreateParameter(cmd, "@PARENT_ID"          , "Guid",  16);
			IDbDataParameter parSTATUS              = Sql.CreateParameter(cmd, "@STATUS"             , "string",  25);
			IDbDataParameter parDIRECTION           = Sql.CreateParameter(cmd, "@DIRECTION"          , "string",  25);
			IDbDataParameter parREMINDER_TIME       = Sql.CreateParameter(cmd, "@REMINDER_TIME"      , "Int32",   4);
			IDbDataParameter parDESCRIPTION         = Sql.CreateParameter(cmd, "@DESCRIPTION"        , "string", 104857600);
			IDbDataParameter parINVITEE_LIST        = Sql.CreateParameter(cmd, "@INVITEE_LIST"       , "ansistring", 8000);
			IDbDataParameter parTEAM_ID             = Sql.CreateParameter(cmd, "@TEAM_ID"            , "Guid",  16);
			IDbDataParameter parTEAM_SET_LIST       = Sql.CreateParameter(cmd, "@TEAM_SET_LIST"      , "ansistring", 8000);
			IDbDataParameter parEMAIL_REMINDER_TIME = Sql.CreateParameter(cmd, "@EMAIL_REMINDER_TIME", "Int32",   4);
			IDbDataParameter parALL_DAY_EVENT       = Sql.CreateParameter(cmd, "@ALL_DAY_EVENT"      , "bool",   1);
			IDbDataParameter parREPEAT_TYPE         = Sql.CreateParameter(cmd, "@REPEAT_TYPE"        , "string",  25);
			IDbDataParameter parREPEAT_INTERVAL     = Sql.CreateParameter(cmd, "@REPEAT_INTERVAL"    , "Int32",   4);
			IDbDataParameter parREPEAT_DOW          = Sql.CreateParameter(cmd, "@REPEAT_DOW"         , "string",   7);
			IDbDataParameter parREPEAT_UNTIL        = Sql.CreateParameter(cmd, "@REPEAT_UNTIL"       , "DateTime",   8);
			IDbDataParameter parREPEAT_COUNT        = Sql.CreateParameter(cmd, "@REPEAT_COUNT"       , "Int32",   4);
			IDbDataParameter parSMS_REMINDER_TIME   = Sql.CreateParameter(cmd, "@SMS_REMINDER_TIME"  , "Int32",   4);
			IDbDataParameter parTAG_SET_NAME        = Sql.CreateParameter(cmd, "@TAG_SET_NAME"       , "string", 4000);
			IDbDataParameter parIS_PRIVATE          = Sql.CreateParameter(cmd, "@IS_PRIVATE"         , "bool",   1);
			IDbDataParameter parASSIGNED_SET_LIST   = Sql.CreateParameter(cmd, "@ASSIGNED_SET_LIST"  , "ansistring", 8000);
			parID.Direction = ParameterDirection.InputOutput;
			return cmd;
		}
		#endregion

		#region spCAMPAIGN_LOG_BannerTracker
		/// <summary>
		/// spCAMPAIGN_LOG_BannerTracker
		/// </summary>
		public static void spCAMPAIGN_LOG_BannerTracker(string sACTIVITY_TYPE, Guid gCAMPAIGN_TRKRS_ID, string sMORE_INFORMATION)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCAMPAIGN_LOG_BannerTracker";
							IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
							IDbDataParameter parACTIVITY_TYPE     = Sql.AddParameter(cmd, "@ACTIVITY_TYPE"    , sACTIVITY_TYPE      ,  25);
							IDbDataParameter parCAMPAIGN_TRKRS_ID = Sql.AddParameter(cmd, "@CAMPAIGN_TRKRS_ID", gCAMPAIGN_TRKRS_ID  );
							IDbDataParameter parMORE_INFORMATION  = Sql.AddParameter(cmd, "@MORE_INFORMATION" , sMORE_INFORMATION   , 100);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spCAMPAIGN_LOG_BannerTracker
		/// <summary>
		/// spCAMPAIGN_LOG_BannerTracker
		/// </summary>
		public static void spCAMPAIGN_LOG_BannerTracker(string sACTIVITY_TYPE, Guid gCAMPAIGN_TRKRS_ID, string sMORE_INFORMATION, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCAMPAIGN_LOG_BannerTracker";
				IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
				IDbDataParameter parACTIVITY_TYPE     = Sql.AddParameter(cmd, "@ACTIVITY_TYPE"    , sACTIVITY_TYPE      ,  25);
				IDbDataParameter parCAMPAIGN_TRKRS_ID = Sql.AddParameter(cmd, "@CAMPAIGN_TRKRS_ID", gCAMPAIGN_TRKRS_ID  );
				IDbDataParameter parMORE_INFORMATION  = Sql.AddParameter(cmd, "@MORE_INFORMATION" , sMORE_INFORMATION   , 100);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spCAMPAIGN_LOG_UpdateTracker
		/// <summary>
		/// spCAMPAIGN_LOG_UpdateTracker
		/// </summary>
		public static void spCAMPAIGN_LOG_UpdateTracker(Guid gTARGET_TRACKER_KEY, string sACTIVITY_TYPE, Guid gCAMPAIGN_TRKRS_ID, ref Guid gTARGET_ID, ref string sTARGET_TYPE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCAMPAIGN_LOG_UpdateTracker";
							IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
							IDbDataParameter parTARGET_TRACKER_KEY = Sql.AddParameter(cmd, "@TARGET_TRACKER_KEY", gTARGET_TRACKER_KEY  );
							IDbDataParameter parACTIVITY_TYPE      = Sql.AddParameter(cmd, "@ACTIVITY_TYPE"     , sACTIVITY_TYPE       ,  25);
							IDbDataParameter parCAMPAIGN_TRKRS_ID  = Sql.AddParameter(cmd, "@CAMPAIGN_TRKRS_ID" , gCAMPAIGN_TRKRS_ID   );
							IDbDataParameter parTARGET_ID          = Sql.AddParameter(cmd, "@TARGET_ID"         , gTARGET_ID           );
							IDbDataParameter parTARGET_TYPE        = Sql.AddParameter(cmd, "@TARGET_TYPE"       , sTARGET_TYPE         ,  25);
							parTARGET_ID.Direction = ParameterDirection.InputOutput;
							parTARGET_TYPE.Direction = ParameterDirection.InputOutput;
							cmd.ExecuteNonQuery();
							gTARGET_ID = Sql.ToGuid(parTARGET_ID.Value);
							sTARGET_TYPE = Sql.ToString(parTARGET_TYPE.Value);
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
		#endregion

		#region spCAMPAIGN_LOG_UpdateTracker
		/// <summary>
		/// spCAMPAIGN_LOG_UpdateTracker
		/// </summary>
		public static void spCAMPAIGN_LOG_UpdateTracker(Guid gTARGET_TRACKER_KEY, string sACTIVITY_TYPE, Guid gCAMPAIGN_TRKRS_ID, ref Guid gTARGET_ID, ref string sTARGET_TYPE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCAMPAIGN_LOG_UpdateTracker";
				IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
				IDbDataParameter parTARGET_TRACKER_KEY = Sql.AddParameter(cmd, "@TARGET_TRACKER_KEY", gTARGET_TRACKER_KEY  );
				IDbDataParameter parACTIVITY_TYPE      = Sql.AddParameter(cmd, "@ACTIVITY_TYPE"     , sACTIVITY_TYPE       ,  25);
				IDbDataParameter parCAMPAIGN_TRKRS_ID  = Sql.AddParameter(cmd, "@CAMPAIGN_TRKRS_ID" , gCAMPAIGN_TRKRS_ID   );
				IDbDataParameter parTARGET_ID          = Sql.AddParameter(cmd, "@TARGET_ID"         , gTARGET_ID           );
				IDbDataParameter parTARGET_TYPE        = Sql.AddParameter(cmd, "@TARGET_TYPE"       , sTARGET_TYPE         ,  25);
				parTARGET_ID.Direction = ParameterDirection.InputOutput;
				parTARGET_TYPE.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gTARGET_ID = Sql.ToGuid(parTARGET_ID.Value);
				sTARGET_TYPE = Sql.ToString(parTARGET_TYPE.Value);
			}
		}
		#endregion

		#region spCAMPAIGNS_OptOut
		/// <summary>
		/// spCAMPAIGNS_OptOut
		/// </summary>
		public static void spCAMPAIGNS_OptOut(Guid gRELATED_ID, string sRELATED_TYPE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCAMPAIGNS_OptOut";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parRELATED_ID       = Sql.AddParameter(cmd, "@RELATED_ID"      , gRELATED_ID        );
							IDbDataParameter parRELATED_TYPE     = Sql.AddParameter(cmd, "@RELATED_TYPE"    , sRELATED_TYPE      ,  25);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spCAMPAIGNS_OptOut
		/// <summary>
		/// spCAMPAIGNS_OptOut
		/// </summary>
		public static void spCAMPAIGNS_OptOut(Guid gRELATED_ID, string sRELATED_TYPE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCAMPAIGNS_OptOut";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parRELATED_ID       = Sql.AddParameter(cmd, "@RELATED_ID"      , gRELATED_ID        );
				IDbDataParameter parRELATED_TYPE     = Sql.AddParameter(cmd, "@RELATED_TYPE"    , sRELATED_TYPE      ,  25);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spCASES_New
		/// <summary>
		/// spCASES_New
		/// </summary>
		public static void spCASES_New(ref Guid gID, string sNAME, string sACCOUNT_NAME, Guid gACCOUNT_ID, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gB2C_CONTACT_ID, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCASES_New";
							IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
							IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
							IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sNAME               , 150);
							IDbDataParameter parACCOUNT_NAME      = Sql.AddParameter(cmd, "@ACCOUNT_NAME"     , sACCOUNT_NAME       , 100);
							IDbDataParameter parACCOUNT_ID        = Sql.AddParameter(cmd, "@ACCOUNT_ID"       , gACCOUNT_ID         );
							IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
							IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
							IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
							IDbDataParameter parB2C_CONTACT_ID    = Sql.AddParameter(cmd, "@B2C_CONTACT_ID"   , gB2C_CONTACT_ID     );
							IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
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
		#endregion

		#region spCASES_New
		/// <summary>
		/// spCASES_New
		/// </summary>
		public static void spCASES_New(ref Guid gID, string sNAME, string sACCOUNT_NAME, Guid gACCOUNT_ID, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gB2C_CONTACT_ID, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCASES_New";
				IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
				IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
				IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sNAME               , 150);
				IDbDataParameter parACCOUNT_NAME      = Sql.AddParameter(cmd, "@ACCOUNT_NAME"     , sACCOUNT_NAME       , 100);
				IDbDataParameter parACCOUNT_ID        = Sql.AddParameter(cmd, "@ACCOUNT_ID"       , gACCOUNT_ID         );
				IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
				IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
				IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
				IDbDataParameter parB2C_CONTACT_ID    = Sql.AddParameter(cmd, "@B2C_CONTACT_ID"   , gB2C_CONTACT_ID     );
				IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spCONTACTS_BUGS_Update
		/// <summary>
		/// spCONTACTS_BUGS_Update
		/// </summary>
		public static void spCONTACTS_BUGS_Update(Guid gCONTACT_ID, Guid gBUG_ID, string sCONTACT_ROLE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCONTACTS_BUGS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							IDbDataParameter parBUG_ID           = Sql.AddParameter(cmd, "@BUG_ID"          , gBUG_ID            );
							IDbDataParameter parCONTACT_ROLE     = Sql.AddParameter(cmd, "@CONTACT_ROLE"    , sCONTACT_ROLE      ,  50);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spCONTACTS_BUGS_Update
		/// <summary>
		/// spCONTACTS_BUGS_Update
		/// </summary>
		public static void spCONTACTS_BUGS_Update(Guid gCONTACT_ID, Guid gBUG_ID, string sCONTACT_ROLE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCONTACTS_BUGS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				IDbDataParameter parBUG_ID           = Sql.AddParameter(cmd, "@BUG_ID"          , gBUG_ID            );
				IDbDataParameter parCONTACT_ROLE     = Sql.AddParameter(cmd, "@CONTACT_ROLE"    , sCONTACT_ROLE      ,  50);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spCONTACTS_CASES_Update
		/// <summary>
		/// spCONTACTS_CASES_Update
		/// </summary>
		public static void spCONTACTS_CASES_Update(Guid gCONTACT_ID, Guid gCASE_ID, string sCONTACT_ROLE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCONTACTS_CASES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							IDbDataParameter parCASE_ID          = Sql.AddParameter(cmd, "@CASE_ID"         , gCASE_ID           );
							IDbDataParameter parCONTACT_ROLE     = Sql.AddParameter(cmd, "@CONTACT_ROLE"    , sCONTACT_ROLE      ,  50);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spCONTACTS_CASES_Update
		/// <summary>
		/// spCONTACTS_CASES_Update
		/// </summary>
		public static void spCONTACTS_CASES_Update(Guid gCONTACT_ID, Guid gCASE_ID, string sCONTACT_ROLE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCONTACTS_CASES_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				IDbDataParameter parCASE_ID          = Sql.AddParameter(cmd, "@CASE_ID"         , gCASE_ID           );
				IDbDataParameter parCONTACT_ROLE     = Sql.AddParameter(cmd, "@CONTACT_ROLE"    , sCONTACT_ROLE      ,  50);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spCONTACTS_Delete
		/// <summary>
		/// spCONTACTS_Delete
		/// </summary>
		public static void spCONTACTS_Delete(Guid gID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCONTACTS_Delete";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spCONTACTS_Delete
		/// <summary>
		/// spCONTACTS_Delete
		/// </summary>
		public static void spCONTACTS_Delete(Guid gID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCONTACTS_Delete";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spCONTACTS_New
		/// <summary>
		/// spCONTACTS_New
		/// </summary>
		public static void spCONTACTS_New(ref Guid gID, string sFIRST_NAME, string sLAST_NAME, string sPHONE_WORK, string sEMAIL1, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCONTACTS_New";
							IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
							IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
							IDbDataParameter parFIRST_NAME        = Sql.AddParameter(cmd, "@FIRST_NAME"       , sFIRST_NAME         , 100);
							IDbDataParameter parLAST_NAME         = Sql.AddParameter(cmd, "@LAST_NAME"        , sLAST_NAME          , 100);
							IDbDataParameter parPHONE_WORK        = Sql.AddParameter(cmd, "@PHONE_WORK"       , sPHONE_WORK         ,  25);
							IDbDataParameter parEMAIL1            = Sql.AddParameter(cmd, "@EMAIL1"           , sEMAIL1             , 100);
							IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
							IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
							IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
							IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
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
		#endregion

		#region spCONTACTS_New
		/// <summary>
		/// spCONTACTS_New
		/// </summary>
		public static void spCONTACTS_New(ref Guid gID, string sFIRST_NAME, string sLAST_NAME, string sPHONE_WORK, string sEMAIL1, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCONTACTS_New";
				IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
				IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
				IDbDataParameter parFIRST_NAME        = Sql.AddParameter(cmd, "@FIRST_NAME"       , sFIRST_NAME         , 100);
				IDbDataParameter parLAST_NAME         = Sql.AddParameter(cmd, "@LAST_NAME"        , sLAST_NAME          , 100);
				IDbDataParameter parPHONE_WORK        = Sql.AddParameter(cmd, "@PHONE_WORK"       , sPHONE_WORK         ,  25);
				IDbDataParameter parEMAIL1            = Sql.AddParameter(cmd, "@EMAIL1"           , sEMAIL1             , 100);
				IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
				IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
				IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
				IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spCONTACTS_Update
		/// <summary>
		/// spCONTACTS_Update
		/// </summary>
		public static void spCONTACTS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sSALUTATION, string sFIRST_NAME, string sLAST_NAME, Guid gACCOUNT_ID, string sLEAD_SOURCE, string sTITLE, string sDEPARTMENT, Guid gREPORTS_TO_ID, DateTime dtBIRTHDATE, bool bDO_NOT_CALL, string sPHONE_HOME, string sPHONE_MOBILE, string sPHONE_WORK, string sPHONE_OTHER, string sPHONE_FAX, string sEMAIL1, string sEMAIL2, string sASSISTANT, string sASSISTANT_PHONE, bool bEMAIL_OPT_OUT, bool bINVALID_EMAIL, string sPRIMARY_ADDRESS_STREET, string sPRIMARY_ADDRESS_CITY, string sPRIMARY_ADDRESS_STATE, string sPRIMARY_ADDRESS_POSTALCODE, string sPRIMARY_ADDRESS_COUNTRY, string sALT_ADDRESS_STREET, string sALT_ADDRESS_CITY, string sALT_ADDRESS_STATE, string sALT_ADDRESS_POSTALCODE, string sALT_ADDRESS_COUNTRY, string sDESCRIPTION, string sPARENT_TYPE, Guid gPARENT_ID, bool bSYNC_CONTACT, Guid gTEAM_ID, string sTEAM_SET_LIST, string sSMS_OPT_IN, string sTWITTER_SCREEN_NAME, string sPICTURE, Guid gLEAD_ID, bool bEXCHANGE_FOLDER, string sTAG_SET_NAME, string sCONTACT_NUMBER, string sASSIGNED_SET_LIST, string sDP_BUSINESS_PURPOSE, DateTime dtDP_CONSENT_LAST_UPDATED)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCONTACTS_Update";
							IDbDataParameter parID                         = Sql.AddParameter(cmd, "@ID"                        , gID                          );
							IDbDataParameter parMODIFIED_USER_ID           = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"          ,  _security?.USER_ID ?? Guid.Empty            );
							IDbDataParameter parASSIGNED_USER_ID           = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"          , gASSIGNED_USER_ID            );
							IDbDataParameter parSALUTATION                 = Sql.AddParameter(cmd, "@SALUTATION"                , sSALUTATION                  ,  25);
							IDbDataParameter parFIRST_NAME                 = Sql.AddParameter(cmd, "@FIRST_NAME"                , sFIRST_NAME                  , 100);
							IDbDataParameter parLAST_NAME                  = Sql.AddParameter(cmd, "@LAST_NAME"                 , sLAST_NAME                   , 100);
							IDbDataParameter parACCOUNT_ID                 = Sql.AddParameter(cmd, "@ACCOUNT_ID"                , gACCOUNT_ID                  );
							IDbDataParameter parLEAD_SOURCE                = Sql.AddParameter(cmd, "@LEAD_SOURCE"               , sLEAD_SOURCE                 , 100);
							IDbDataParameter parTITLE                      = Sql.AddParameter(cmd, "@TITLE"                     , sTITLE                       ,  50);
							IDbDataParameter parDEPARTMENT                 = Sql.AddParameter(cmd, "@DEPARTMENT"                , sDEPARTMENT                  , 100);
							IDbDataParameter parREPORTS_TO_ID              = Sql.AddParameter(cmd, "@REPORTS_TO_ID"             , gREPORTS_TO_ID               );
							IDbDataParameter parBIRTHDATE                  = Sql.AddParameter(cmd, "@BIRTHDATE"                 , dtBIRTHDATE                  );
							IDbDataParameter parDO_NOT_CALL                = Sql.AddParameter(cmd, "@DO_NOT_CALL"               , bDO_NOT_CALL                 );
							IDbDataParameter parPHONE_HOME                 = Sql.AddParameter(cmd, "@PHONE_HOME"                , sPHONE_HOME                  ,  25);
							IDbDataParameter parPHONE_MOBILE               = Sql.AddParameter(cmd, "@PHONE_MOBILE"              , sPHONE_MOBILE                ,  25);
							IDbDataParameter parPHONE_WORK                 = Sql.AddParameter(cmd, "@PHONE_WORK"                , sPHONE_WORK                  ,  25);
							IDbDataParameter parPHONE_OTHER                = Sql.AddParameter(cmd, "@PHONE_OTHER"               , sPHONE_OTHER                 ,  25);
							IDbDataParameter parPHONE_FAX                  = Sql.AddParameter(cmd, "@PHONE_FAX"                 , sPHONE_FAX                   ,  25);
							IDbDataParameter parEMAIL1                     = Sql.AddParameter(cmd, "@EMAIL1"                    , sEMAIL1                      , 100);
							IDbDataParameter parEMAIL2                     = Sql.AddParameter(cmd, "@EMAIL2"                    , sEMAIL2                      , 100);
							IDbDataParameter parASSISTANT                  = Sql.AddParameter(cmd, "@ASSISTANT"                 , sASSISTANT                   ,  75);
							IDbDataParameter parASSISTANT_PHONE            = Sql.AddParameter(cmd, "@ASSISTANT_PHONE"           , sASSISTANT_PHONE             ,  25);
							IDbDataParameter parEMAIL_OPT_OUT              = Sql.AddParameter(cmd, "@EMAIL_OPT_OUT"             , bEMAIL_OPT_OUT               );
							IDbDataParameter parINVALID_EMAIL              = Sql.AddParameter(cmd, "@INVALID_EMAIL"             , bINVALID_EMAIL               );
							IDbDataParameter parPRIMARY_ADDRESS_STREET     = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_STREET"    , sPRIMARY_ADDRESS_STREET      , 150);
							IDbDataParameter parPRIMARY_ADDRESS_CITY       = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_CITY"      , sPRIMARY_ADDRESS_CITY        , 100);
							IDbDataParameter parPRIMARY_ADDRESS_STATE      = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_STATE"     , sPRIMARY_ADDRESS_STATE       , 100);
							IDbDataParameter parPRIMARY_ADDRESS_POSTALCODE = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_POSTALCODE", sPRIMARY_ADDRESS_POSTALCODE  ,  20);
							IDbDataParameter parPRIMARY_ADDRESS_COUNTRY    = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_COUNTRY"   , sPRIMARY_ADDRESS_COUNTRY     , 100);
							IDbDataParameter parALT_ADDRESS_STREET         = Sql.AddParameter(cmd, "@ALT_ADDRESS_STREET"        , sALT_ADDRESS_STREET          , 150);
							IDbDataParameter parALT_ADDRESS_CITY           = Sql.AddParameter(cmd, "@ALT_ADDRESS_CITY"          , sALT_ADDRESS_CITY            , 100);
							IDbDataParameter parALT_ADDRESS_STATE          = Sql.AddParameter(cmd, "@ALT_ADDRESS_STATE"         , sALT_ADDRESS_STATE           , 100);
							IDbDataParameter parALT_ADDRESS_POSTALCODE     = Sql.AddParameter(cmd, "@ALT_ADDRESS_POSTALCODE"    , sALT_ADDRESS_POSTALCODE      ,  20);
							IDbDataParameter parALT_ADDRESS_COUNTRY        = Sql.AddParameter(cmd, "@ALT_ADDRESS_COUNTRY"       , sALT_ADDRESS_COUNTRY         , 100);
							IDbDataParameter parDESCRIPTION                = Sql.AddParameter(cmd, "@DESCRIPTION"               , sDESCRIPTION                 );
							IDbDataParameter parPARENT_TYPE                = Sql.AddParameter(cmd, "@PARENT_TYPE"               , sPARENT_TYPE                 ,  25);
							IDbDataParameter parPARENT_ID                  = Sql.AddParameter(cmd, "@PARENT_ID"                 , gPARENT_ID                   );
							IDbDataParameter parSYNC_CONTACT               = Sql.AddParameter(cmd, "@SYNC_CONTACT"              , bSYNC_CONTACT                );
							IDbDataParameter parTEAM_ID                    = Sql.AddParameter(cmd, "@TEAM_ID"                   , gTEAM_ID                     );
							IDbDataParameter parTEAM_SET_LIST              = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"             , sTEAM_SET_LIST               , 8000);
							IDbDataParameter parSMS_OPT_IN                 = Sql.AddParameter(cmd, "@SMS_OPT_IN"                , sSMS_OPT_IN                  ,  25);
							IDbDataParameter parTWITTER_SCREEN_NAME        = Sql.AddParameter(cmd, "@TWITTER_SCREEN_NAME"       , sTWITTER_SCREEN_NAME         ,  20);
							IDbDataParameter parPICTURE                    = Sql.AddParameter(cmd, "@PICTURE"                   , sPICTURE                     );
							IDbDataParameter parLEAD_ID                    = Sql.AddParameter(cmd, "@LEAD_ID"                   , gLEAD_ID                     );
							IDbDataParameter parEXCHANGE_FOLDER            = Sql.AddParameter(cmd, "@EXCHANGE_FOLDER"           , bEXCHANGE_FOLDER             );
							IDbDataParameter parTAG_SET_NAME               = Sql.AddParameter(cmd, "@TAG_SET_NAME"              , sTAG_SET_NAME                , 4000);
							IDbDataParameter parCONTACT_NUMBER             = Sql.AddParameter(cmd, "@CONTACT_NUMBER"            , sCONTACT_NUMBER              ,  30);
							IDbDataParameter parASSIGNED_SET_LIST          = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"         , sASSIGNED_SET_LIST           , 8000);
							IDbDataParameter parDP_BUSINESS_PURPOSE        = Sql.AddParameter(cmd, "@DP_BUSINESS_PURPOSE"       , sDP_BUSINESS_PURPOSE         );
							IDbDataParameter parDP_CONSENT_LAST_UPDATED    = Sql.AddParameter(cmd, "@DP_CONSENT_LAST_UPDATED"   , dtDP_CONSENT_LAST_UPDATED    );
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
		#endregion

		#region spCONTACTS_Update
		/// <summary>
		/// spCONTACTS_Update
		/// </summary>
		public static void spCONTACTS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sSALUTATION, string sFIRST_NAME, string sLAST_NAME, Guid gACCOUNT_ID, string sLEAD_SOURCE, string sTITLE, string sDEPARTMENT, Guid gREPORTS_TO_ID, DateTime dtBIRTHDATE, bool bDO_NOT_CALL, string sPHONE_HOME, string sPHONE_MOBILE, string sPHONE_WORK, string sPHONE_OTHER, string sPHONE_FAX, string sEMAIL1, string sEMAIL2, string sASSISTANT, string sASSISTANT_PHONE, bool bEMAIL_OPT_OUT, bool bINVALID_EMAIL, string sPRIMARY_ADDRESS_STREET, string sPRIMARY_ADDRESS_CITY, string sPRIMARY_ADDRESS_STATE, string sPRIMARY_ADDRESS_POSTALCODE, string sPRIMARY_ADDRESS_COUNTRY, string sALT_ADDRESS_STREET, string sALT_ADDRESS_CITY, string sALT_ADDRESS_STATE, string sALT_ADDRESS_POSTALCODE, string sALT_ADDRESS_COUNTRY, string sDESCRIPTION, string sPARENT_TYPE, Guid gPARENT_ID, bool bSYNC_CONTACT, Guid gTEAM_ID, string sTEAM_SET_LIST, string sSMS_OPT_IN, string sTWITTER_SCREEN_NAME, string sPICTURE, Guid gLEAD_ID, bool bEXCHANGE_FOLDER, string sTAG_SET_NAME, string sCONTACT_NUMBER, string sASSIGNED_SET_LIST, string sDP_BUSINESS_PURPOSE, DateTime dtDP_CONSENT_LAST_UPDATED, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCONTACTS_Update";
				IDbDataParameter parID                         = Sql.AddParameter(cmd, "@ID"                        , gID                          );
				IDbDataParameter parMODIFIED_USER_ID           = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"          ,  _security?.USER_ID ?? Guid.Empty            );
				IDbDataParameter parASSIGNED_USER_ID           = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"          , gASSIGNED_USER_ID            );
				IDbDataParameter parSALUTATION                 = Sql.AddParameter(cmd, "@SALUTATION"                , sSALUTATION                  ,  25);
				IDbDataParameter parFIRST_NAME                 = Sql.AddParameter(cmd, "@FIRST_NAME"                , sFIRST_NAME                  , 100);
				IDbDataParameter parLAST_NAME                  = Sql.AddParameter(cmd, "@LAST_NAME"                 , sLAST_NAME                   , 100);
				IDbDataParameter parACCOUNT_ID                 = Sql.AddParameter(cmd, "@ACCOUNT_ID"                , gACCOUNT_ID                  );
				IDbDataParameter parLEAD_SOURCE                = Sql.AddParameter(cmd, "@LEAD_SOURCE"               , sLEAD_SOURCE                 , 100);
				IDbDataParameter parTITLE                      = Sql.AddParameter(cmd, "@TITLE"                     , sTITLE                       ,  50);
				IDbDataParameter parDEPARTMENT                 = Sql.AddParameter(cmd, "@DEPARTMENT"                , sDEPARTMENT                  , 100);
				IDbDataParameter parREPORTS_TO_ID              = Sql.AddParameter(cmd, "@REPORTS_TO_ID"             , gREPORTS_TO_ID               );
				IDbDataParameter parBIRTHDATE                  = Sql.AddParameter(cmd, "@BIRTHDATE"                 , dtBIRTHDATE                  );
				IDbDataParameter parDO_NOT_CALL                = Sql.AddParameter(cmd, "@DO_NOT_CALL"               , bDO_NOT_CALL                 );
				IDbDataParameter parPHONE_HOME                 = Sql.AddParameter(cmd, "@PHONE_HOME"                , sPHONE_HOME                  ,  25);
				IDbDataParameter parPHONE_MOBILE               = Sql.AddParameter(cmd, "@PHONE_MOBILE"              , sPHONE_MOBILE                ,  25);
				IDbDataParameter parPHONE_WORK                 = Sql.AddParameter(cmd, "@PHONE_WORK"                , sPHONE_WORK                  ,  25);
				IDbDataParameter parPHONE_OTHER                = Sql.AddParameter(cmd, "@PHONE_OTHER"               , sPHONE_OTHER                 ,  25);
				IDbDataParameter parPHONE_FAX                  = Sql.AddParameter(cmd, "@PHONE_FAX"                 , sPHONE_FAX                   ,  25);
				IDbDataParameter parEMAIL1                     = Sql.AddParameter(cmd, "@EMAIL1"                    , sEMAIL1                      , 100);
				IDbDataParameter parEMAIL2                     = Sql.AddParameter(cmd, "@EMAIL2"                    , sEMAIL2                      , 100);
				IDbDataParameter parASSISTANT                  = Sql.AddParameter(cmd, "@ASSISTANT"                 , sASSISTANT                   ,  75);
				IDbDataParameter parASSISTANT_PHONE            = Sql.AddParameter(cmd, "@ASSISTANT_PHONE"           , sASSISTANT_PHONE             ,  25);
				IDbDataParameter parEMAIL_OPT_OUT              = Sql.AddParameter(cmd, "@EMAIL_OPT_OUT"             , bEMAIL_OPT_OUT               );
				IDbDataParameter parINVALID_EMAIL              = Sql.AddParameter(cmd, "@INVALID_EMAIL"             , bINVALID_EMAIL               );
				IDbDataParameter parPRIMARY_ADDRESS_STREET     = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_STREET"    , sPRIMARY_ADDRESS_STREET      , 150);
				IDbDataParameter parPRIMARY_ADDRESS_CITY       = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_CITY"      , sPRIMARY_ADDRESS_CITY        , 100);
				IDbDataParameter parPRIMARY_ADDRESS_STATE      = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_STATE"     , sPRIMARY_ADDRESS_STATE       , 100);
				IDbDataParameter parPRIMARY_ADDRESS_POSTALCODE = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_POSTALCODE", sPRIMARY_ADDRESS_POSTALCODE  ,  20);
				IDbDataParameter parPRIMARY_ADDRESS_COUNTRY    = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_COUNTRY"   , sPRIMARY_ADDRESS_COUNTRY     , 100);
				IDbDataParameter parALT_ADDRESS_STREET         = Sql.AddParameter(cmd, "@ALT_ADDRESS_STREET"        , sALT_ADDRESS_STREET          , 150);
				IDbDataParameter parALT_ADDRESS_CITY           = Sql.AddParameter(cmd, "@ALT_ADDRESS_CITY"          , sALT_ADDRESS_CITY            , 100);
				IDbDataParameter parALT_ADDRESS_STATE          = Sql.AddParameter(cmd, "@ALT_ADDRESS_STATE"         , sALT_ADDRESS_STATE           , 100);
				IDbDataParameter parALT_ADDRESS_POSTALCODE     = Sql.AddParameter(cmd, "@ALT_ADDRESS_POSTALCODE"    , sALT_ADDRESS_POSTALCODE      ,  20);
				IDbDataParameter parALT_ADDRESS_COUNTRY        = Sql.AddParameter(cmd, "@ALT_ADDRESS_COUNTRY"       , sALT_ADDRESS_COUNTRY         , 100);
				IDbDataParameter parDESCRIPTION                = Sql.AddParameter(cmd, "@DESCRIPTION"               , sDESCRIPTION                 );
				IDbDataParameter parPARENT_TYPE                = Sql.AddParameter(cmd, "@PARENT_TYPE"               , sPARENT_TYPE                 ,  25);
				IDbDataParameter parPARENT_ID                  = Sql.AddParameter(cmd, "@PARENT_ID"                 , gPARENT_ID                   );
				IDbDataParameter parSYNC_CONTACT               = Sql.AddParameter(cmd, "@SYNC_CONTACT"              , bSYNC_CONTACT                );
				IDbDataParameter parTEAM_ID                    = Sql.AddParameter(cmd, "@TEAM_ID"                   , gTEAM_ID                     );
				IDbDataParameter parTEAM_SET_LIST              = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"             , sTEAM_SET_LIST               , 8000);
				IDbDataParameter parSMS_OPT_IN                 = Sql.AddParameter(cmd, "@SMS_OPT_IN"                , sSMS_OPT_IN                  ,  25);
				IDbDataParameter parTWITTER_SCREEN_NAME        = Sql.AddParameter(cmd, "@TWITTER_SCREEN_NAME"       , sTWITTER_SCREEN_NAME         ,  20);
				IDbDataParameter parPICTURE                    = Sql.AddParameter(cmd, "@PICTURE"                   , sPICTURE                     );
				IDbDataParameter parLEAD_ID                    = Sql.AddParameter(cmd, "@LEAD_ID"                   , gLEAD_ID                     );
				IDbDataParameter parEXCHANGE_FOLDER            = Sql.AddParameter(cmd, "@EXCHANGE_FOLDER"           , bEXCHANGE_FOLDER             );
				IDbDataParameter parTAG_SET_NAME               = Sql.AddParameter(cmd, "@TAG_SET_NAME"              , sTAG_SET_NAME                , 4000);
				IDbDataParameter parCONTACT_NUMBER             = Sql.AddParameter(cmd, "@CONTACT_NUMBER"            , sCONTACT_NUMBER              ,  30);
				IDbDataParameter parASSIGNED_SET_LIST          = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"         , sASSIGNED_SET_LIST           , 8000);
				IDbDataParameter parDP_BUSINESS_PURPOSE        = Sql.AddParameter(cmd, "@DP_BUSINESS_PURPOSE"       , sDP_BUSINESS_PURPOSE         );
				IDbDataParameter parDP_CONSENT_LAST_UPDATED    = Sql.AddParameter(cmd, "@DP_CONSENT_LAST_UPDATED"   , dtDP_CONSENT_LAST_UPDATED    );
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region cmdCONTACTS_Update
		/// <summary>
		/// spCONTACTS_Update
		/// </summary>
		public static IDbCommand cmdCONTACTS_Update(IDbConnection con)
		{
			IDbCommand cmd = con.CreateCommand();
			cmd.CommandType = CommandType.StoredProcedure;
			cmd.CommandText = "spCONTACTS_Update";
			IDbDataParameter parID                         = Sql.CreateParameter(cmd, "@ID"                        , "Guid",  16);
			IDbDataParameter parMODIFIED_USER_ID           = Sql.CreateParameter(cmd, "@MODIFIED_USER_ID"          , "Guid",  16);
			IDbDataParameter parASSIGNED_USER_ID           = Sql.CreateParameter(cmd, "@ASSIGNED_USER_ID"          , "Guid",  16);
			IDbDataParameter parSALUTATION                 = Sql.CreateParameter(cmd, "@SALUTATION"                , "string",  25);
			IDbDataParameter parFIRST_NAME                 = Sql.CreateParameter(cmd, "@FIRST_NAME"                , "string", 100);
			IDbDataParameter parLAST_NAME                  = Sql.CreateParameter(cmd, "@LAST_NAME"                 , "string", 100);
			IDbDataParameter parACCOUNT_ID                 = Sql.CreateParameter(cmd, "@ACCOUNT_ID"                , "Guid",  16);
			IDbDataParameter parLEAD_SOURCE                = Sql.CreateParameter(cmd, "@LEAD_SOURCE"               , "string", 100);
			IDbDataParameter parTITLE                      = Sql.CreateParameter(cmd, "@TITLE"                     , "string",  50);
			IDbDataParameter parDEPARTMENT                 = Sql.CreateParameter(cmd, "@DEPARTMENT"                , "string", 100);
			IDbDataParameter parREPORTS_TO_ID              = Sql.CreateParameter(cmd, "@REPORTS_TO_ID"             , "Guid",  16);
			IDbDataParameter parBIRTHDATE                  = Sql.CreateParameter(cmd, "@BIRTHDATE"                 , "DateTime",   8);
			IDbDataParameter parDO_NOT_CALL                = Sql.CreateParameter(cmd, "@DO_NOT_CALL"               , "bool",   1);
			IDbDataParameter parPHONE_HOME                 = Sql.CreateParameter(cmd, "@PHONE_HOME"                , "string",  25);
			IDbDataParameter parPHONE_MOBILE               = Sql.CreateParameter(cmd, "@PHONE_MOBILE"              , "string",  25);
			IDbDataParameter parPHONE_WORK                 = Sql.CreateParameter(cmd, "@PHONE_WORK"                , "string",  25);
			IDbDataParameter parPHONE_OTHER                = Sql.CreateParameter(cmd, "@PHONE_OTHER"               , "string",  25);
			IDbDataParameter parPHONE_FAX                  = Sql.CreateParameter(cmd, "@PHONE_FAX"                 , "string",  25);
			IDbDataParameter parEMAIL1                     = Sql.CreateParameter(cmd, "@EMAIL1"                    , "string", 100);
			IDbDataParameter parEMAIL2                     = Sql.CreateParameter(cmd, "@EMAIL2"                    , "string", 100);
			IDbDataParameter parASSISTANT                  = Sql.CreateParameter(cmd, "@ASSISTANT"                 , "string",  75);
			IDbDataParameter parASSISTANT_PHONE            = Sql.CreateParameter(cmd, "@ASSISTANT_PHONE"           , "string",  25);
			IDbDataParameter parEMAIL_OPT_OUT              = Sql.CreateParameter(cmd, "@EMAIL_OPT_OUT"             , "bool",   1);
			IDbDataParameter parINVALID_EMAIL              = Sql.CreateParameter(cmd, "@INVALID_EMAIL"             , "bool",   1);
			IDbDataParameter parPRIMARY_ADDRESS_STREET     = Sql.CreateParameter(cmd, "@PRIMARY_ADDRESS_STREET"    , "string", 150);
			IDbDataParameter parPRIMARY_ADDRESS_CITY       = Sql.CreateParameter(cmd, "@PRIMARY_ADDRESS_CITY"      , "string", 100);
			IDbDataParameter parPRIMARY_ADDRESS_STATE      = Sql.CreateParameter(cmd, "@PRIMARY_ADDRESS_STATE"     , "string", 100);
			IDbDataParameter parPRIMARY_ADDRESS_POSTALCODE = Sql.CreateParameter(cmd, "@PRIMARY_ADDRESS_POSTALCODE", "string",  20);
			IDbDataParameter parPRIMARY_ADDRESS_COUNTRY    = Sql.CreateParameter(cmd, "@PRIMARY_ADDRESS_COUNTRY"   , "string", 100);
			IDbDataParameter parALT_ADDRESS_STREET         = Sql.CreateParameter(cmd, "@ALT_ADDRESS_STREET"        , "string", 150);
			IDbDataParameter parALT_ADDRESS_CITY           = Sql.CreateParameter(cmd, "@ALT_ADDRESS_CITY"          , "string", 100);
			IDbDataParameter parALT_ADDRESS_STATE          = Sql.CreateParameter(cmd, "@ALT_ADDRESS_STATE"         , "string", 100);
			IDbDataParameter parALT_ADDRESS_POSTALCODE     = Sql.CreateParameter(cmd, "@ALT_ADDRESS_POSTALCODE"    , "string",  20);
			IDbDataParameter parALT_ADDRESS_COUNTRY        = Sql.CreateParameter(cmd, "@ALT_ADDRESS_COUNTRY"       , "string", 100);
			IDbDataParameter parDESCRIPTION                = Sql.CreateParameter(cmd, "@DESCRIPTION"               , "string", 104857600);
			IDbDataParameter parPARENT_TYPE                = Sql.CreateParameter(cmd, "@PARENT_TYPE"               , "string",  25);
			IDbDataParameter parPARENT_ID                  = Sql.CreateParameter(cmd, "@PARENT_ID"                 , "Guid",  16);
			IDbDataParameter parSYNC_CONTACT               = Sql.CreateParameter(cmd, "@SYNC_CONTACT"              , "bool",   1);
			IDbDataParameter parTEAM_ID                    = Sql.CreateParameter(cmd, "@TEAM_ID"                   , "Guid",  16);
			IDbDataParameter parTEAM_SET_LIST              = Sql.CreateParameter(cmd, "@TEAM_SET_LIST"             , "ansistring", 8000);
			IDbDataParameter parSMS_OPT_IN                 = Sql.CreateParameter(cmd, "@SMS_OPT_IN"                , "string",  25);
			IDbDataParameter parTWITTER_SCREEN_NAME        = Sql.CreateParameter(cmd, "@TWITTER_SCREEN_NAME"       , "string",  20);
			IDbDataParameter parPICTURE                    = Sql.CreateParameter(cmd, "@PICTURE"                   , "string", 104857600);
			IDbDataParameter parLEAD_ID                    = Sql.CreateParameter(cmd, "@LEAD_ID"                   , "Guid",  16);
			IDbDataParameter parEXCHANGE_FOLDER            = Sql.CreateParameter(cmd, "@EXCHANGE_FOLDER"           , "bool",   1);
			IDbDataParameter parTAG_SET_NAME               = Sql.CreateParameter(cmd, "@TAG_SET_NAME"              , "string", 4000);
			IDbDataParameter parCONTACT_NUMBER             = Sql.CreateParameter(cmd, "@CONTACT_NUMBER"            , "string",  30);
			IDbDataParameter parASSIGNED_SET_LIST          = Sql.CreateParameter(cmd, "@ASSIGNED_SET_LIST"         , "ansistring", 8000);
			IDbDataParameter parDP_BUSINESS_PURPOSE        = Sql.CreateParameter(cmd, "@DP_BUSINESS_PURPOSE"       , "string", 104857600);
			IDbDataParameter parDP_CONSENT_LAST_UPDATED    = Sql.CreateParameter(cmd, "@DP_CONSENT_LAST_UPDATED"   , "DateTime",   8);
			parID.Direction = ParameterDirection.InputOutput;
			return cmd;
		}
		#endregion

		#region spCONTACTS_USERS_Delete
		/// <summary>
		/// spCONTACTS_USERS_Delete
		/// </summary>
		public static void spCONTACTS_USERS_Delete(Guid gCONTACT_ID, Guid gUSER_ID, string sSERVICE_NAME)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCONTACTS_USERS_Delete";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
							IDbDataParameter parSERVICE_NAME     = Sql.AddParameter(cmd, "@SERVICE_NAME"    , sSERVICE_NAME      ,  25);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spCONTACTS_USERS_Delete
		/// <summary>
		/// spCONTACTS_USERS_Delete
		/// </summary>
		public static void spCONTACTS_USERS_Delete(Guid gCONTACT_ID, Guid gUSER_ID, string sSERVICE_NAME, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCONTACTS_USERS_Delete";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
				IDbDataParameter parSERVICE_NAME     = Sql.AddParameter(cmd, "@SERVICE_NAME"    , sSERVICE_NAME      ,  25);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spCONTACTS_USERS_Update
		/// <summary>
		/// spCONTACTS_USERS_Update
		/// </summary>
		public static void spCONTACTS_USERS_Update(Guid gCONTACT_ID, Guid gUSER_ID, string sSERVICE_NAME)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCONTACTS_USERS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
							IDbDataParameter parSERVICE_NAME     = Sql.AddParameter(cmd, "@SERVICE_NAME"    , sSERVICE_NAME      ,  25);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spCONTACTS_USERS_Update
		/// <summary>
		/// spCONTACTS_USERS_Update
		/// </summary>
		public static void spCONTACTS_USERS_Update(Guid gCONTACT_ID, Guid gUSER_ID, string sSERVICE_NAME, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCONTACTS_USERS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
				IDbDataParameter parSERVICE_NAME     = Sql.AddParameter(cmd, "@SERVICE_NAME"    , sSERVICE_NAME      ,  25);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spCONTRACTS_CONTACTS_Update
		/// <summary>
		/// spCONTRACTS_CONTACTS_Update
		/// </summary>
		public static void spCONTRACTS_CONTACTS_Update(Guid gCONTRACT_ID, Guid gCONTACT_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCONTRACTS_CONTACTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parCONTRACT_ID      = Sql.AddParameter(cmd, "@CONTRACT_ID"     , gCONTRACT_ID       );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spCONTRACTS_CONTACTS_Update
		/// <summary>
		/// spCONTRACTS_CONTACTS_Update
		/// </summary>
		public static void spCONTRACTS_CONTACTS_Update(Guid gCONTRACT_ID, Guid gCONTACT_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spCONTRACTS_CONTACTS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parCONTRACT_ID      = Sql.AddParameter(cmd, "@CONTRACT_ID"     , gCONTRACT_ID       );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spDOCUMENT_REVISIONS_Insert
		/// <summary>
		/// spDOCUMENT_REVISIONS_Insert
		/// </summary>
		public static void spDOCUMENT_REVISIONS_Insert(ref Guid gID, Guid gDOCUMENT_ID, string sREVISION, string sCHANGE_LOG, string sFILENAME, string sFILE_EXT, string sFILE_MIME_TYPE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spDOCUMENT_REVISIONS_Insert";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parDOCUMENT_ID      = Sql.AddParameter(cmd, "@DOCUMENT_ID"     , gDOCUMENT_ID       );
							IDbDataParameter parREVISION         = Sql.AddParameter(cmd, "@REVISION"        , sREVISION          ,  25);
							IDbDataParameter parCHANGE_LOG       = Sql.AddParameter(cmd, "@CHANGE_LOG"      , sCHANGE_LOG        , 255);
							IDbDataParameter parFILENAME         = Sql.AddParameter(cmd, "@FILENAME"        , sFILENAME          , 255);
							IDbDataParameter parFILE_EXT         = Sql.AddParameter(cmd, "@FILE_EXT"        , sFILE_EXT          ,  25);
							IDbDataParameter parFILE_MIME_TYPE   = Sql.AddParameter(cmd, "@FILE_MIME_TYPE"  , sFILE_MIME_TYPE    , 100);
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
		#endregion

		#region spDOCUMENT_REVISIONS_Insert
		/// <summary>
		/// spDOCUMENT_REVISIONS_Insert
		/// </summary>
		public static void spDOCUMENT_REVISIONS_Insert(ref Guid gID, Guid gDOCUMENT_ID, string sREVISION, string sCHANGE_LOG, string sFILENAME, string sFILE_EXT, string sFILE_MIME_TYPE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spDOCUMENT_REVISIONS_Insert";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parDOCUMENT_ID      = Sql.AddParameter(cmd, "@DOCUMENT_ID"     , gDOCUMENT_ID       );
				IDbDataParameter parREVISION         = Sql.AddParameter(cmd, "@REVISION"        , sREVISION          ,  25);
				IDbDataParameter parCHANGE_LOG       = Sql.AddParameter(cmd, "@CHANGE_LOG"      , sCHANGE_LOG        , 255);
				IDbDataParameter parFILENAME         = Sql.AddParameter(cmd, "@FILENAME"        , sFILENAME          , 255);
				IDbDataParameter parFILE_EXT         = Sql.AddParameter(cmd, "@FILE_EXT"        , sFILE_EXT          ,  25);
				IDbDataParameter parFILE_MIME_TYPE   = Sql.AddParameter(cmd, "@FILE_MIME_TYPE"  , sFILE_MIME_TYPE    , 100);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spEMAIL_CLIENT_SYNC_Update
		/// <summary>
		/// spEMAIL_CLIENT_SYNC_Update
		/// </summary>
		public static void spEMAIL_CLIENT_SYNC_Update(Guid gASSIGNED_USER_ID, Guid gLOCAL_ID, string sREMOTE_KEY, string sMODULE_NAME, Guid gPARENT_ID, DateTime dtREMOTE_DATE_MODIFIED, DateTime dtREMOTE_DATE_MODIFIED_UTC)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAIL_CLIENT_SYNC_Update";
							IDbDataParameter parMODIFIED_USER_ID         = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"        ,  _security?.USER_ID ?? Guid.Empty          );
							IDbDataParameter parASSIGNED_USER_ID         = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"        , gASSIGNED_USER_ID          );
							IDbDataParameter parLOCAL_ID                 = Sql.AddParameter(cmd, "@LOCAL_ID"                , gLOCAL_ID                  );
							IDbDataParameter parREMOTE_KEY               = Sql.AddAnsiParam(cmd, "@REMOTE_KEY"              , sREMOTE_KEY                , 800);
							IDbDataParameter parMODULE_NAME              = Sql.AddParameter(cmd, "@MODULE_NAME"             , sMODULE_NAME               ,  25);
							IDbDataParameter parPARENT_ID                = Sql.AddParameter(cmd, "@PARENT_ID"               , gPARENT_ID                 );
							IDbDataParameter parREMOTE_DATE_MODIFIED     = Sql.AddParameter(cmd, "@REMOTE_DATE_MODIFIED"    , dtREMOTE_DATE_MODIFIED     );
							IDbDataParameter parREMOTE_DATE_MODIFIED_UTC = Sql.AddParameter(cmd, "@REMOTE_DATE_MODIFIED_UTC", dtREMOTE_DATE_MODIFIED_UTC );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAIL_CLIENT_SYNC_Update
		/// <summary>
		/// spEMAIL_CLIENT_SYNC_Update
		/// </summary>
		public static void spEMAIL_CLIENT_SYNC_Update(Guid gASSIGNED_USER_ID, Guid gLOCAL_ID, string sREMOTE_KEY, string sMODULE_NAME, Guid gPARENT_ID, DateTime dtREMOTE_DATE_MODIFIED, DateTime dtREMOTE_DATE_MODIFIED_UTC, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAIL_CLIENT_SYNC_Update";
				IDbDataParameter parMODIFIED_USER_ID         = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"        ,  _security?.USER_ID ?? Guid.Empty          );
				IDbDataParameter parASSIGNED_USER_ID         = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"        , gASSIGNED_USER_ID          );
				IDbDataParameter parLOCAL_ID                 = Sql.AddParameter(cmd, "@LOCAL_ID"                , gLOCAL_ID                  );
				IDbDataParameter parREMOTE_KEY               = Sql.AddAnsiParam(cmd, "@REMOTE_KEY"              , sREMOTE_KEY                , 800);
				IDbDataParameter parMODULE_NAME              = Sql.AddParameter(cmd, "@MODULE_NAME"             , sMODULE_NAME               ,  25);
				IDbDataParameter parPARENT_ID                = Sql.AddParameter(cmd, "@PARENT_ID"               , gPARENT_ID                 );
				IDbDataParameter parREMOTE_DATE_MODIFIED     = Sql.AddParameter(cmd, "@REMOTE_DATE_MODIFIED"    , dtREMOTE_DATE_MODIFIED     );
				IDbDataParameter parREMOTE_DATE_MODIFIED_UTC = Sql.AddParameter(cmd, "@REMOTE_DATE_MODIFIED_UTC", dtREMOTE_DATE_MODIFIED_UTC );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spEMAIL_IMAGES_Insert
		/// <summary>
		/// spEMAIL_IMAGES_Insert
		/// </summary>
		public static void spEMAIL_IMAGES_Insert(ref Guid gID, Guid gPARENT_ID, string sFILENAME, string sFILE_EXT, string sFILE_MIME_TYPE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAIL_IMAGES_Insert";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parPARENT_ID        = Sql.AddParameter(cmd, "@PARENT_ID"       , gPARENT_ID         );
							IDbDataParameter parFILENAME         = Sql.AddParameter(cmd, "@FILENAME"        , sFILENAME          , 255);
							IDbDataParameter parFILE_EXT         = Sql.AddParameter(cmd, "@FILE_EXT"        , sFILE_EXT          ,  25);
							IDbDataParameter parFILE_MIME_TYPE   = Sql.AddParameter(cmd, "@FILE_MIME_TYPE"  , sFILE_MIME_TYPE    , 100);
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
		#endregion

		#region spEMAIL_IMAGES_Insert
		/// <summary>
		/// spEMAIL_IMAGES_Insert
		/// </summary>
		public static void spEMAIL_IMAGES_Insert(ref Guid gID, Guid gPARENT_ID, string sFILENAME, string sFILE_EXT, string sFILE_MIME_TYPE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAIL_IMAGES_Insert";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parPARENT_ID        = Sql.AddParameter(cmd, "@PARENT_ID"       , gPARENT_ID         );
				IDbDataParameter parFILENAME         = Sql.AddParameter(cmd, "@FILENAME"        , sFILENAME          , 255);
				IDbDataParameter parFILE_EXT         = Sql.AddParameter(cmd, "@FILE_EXT"        , sFILE_EXT          ,  25);
				IDbDataParameter parFILE_MIME_TYPE   = Sql.AddParameter(cmd, "@FILE_MIME_TYPE"  , sFILE_MIME_TYPE    , 100);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spEMAILMAN_SendFailed
		/// <summary>
		/// spEMAILMAN_SendFailed
		/// </summary>
		public static void spEMAILMAN_SendFailed(Guid gID, string sACTIVITY_TYPE, bool bABORT)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILMAN_SendFailed";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parACTIVITY_TYPE    = Sql.AddParameter(cmd, "@ACTIVITY_TYPE"   , sACTIVITY_TYPE     ,  25);
							IDbDataParameter parABORT            = Sql.AddParameter(cmd, "@ABORT"           , bABORT             );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILMAN_SendFailed
		/// <summary>
		/// spEMAILMAN_SendFailed
		/// </summary>
		public static void spEMAILMAN_SendFailed(Guid gID, string sACTIVITY_TYPE, bool bABORT, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILMAN_SendFailed";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parACTIVITY_TYPE    = Sql.AddParameter(cmd, "@ACTIVITY_TYPE"   , sACTIVITY_TYPE     ,  25);
				IDbDataParameter parABORT            = Sql.AddParameter(cmd, "@ABORT"           , bABORT             );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spEMAILMAN_SendSuccessful
		/// <summary>
		/// spEMAILMAN_SendSuccessful
		/// </summary>
		public static void spEMAILMAN_SendSuccessful(Guid gID, Guid gTARGET_TRACKER_KEY, Guid gEMAIL_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILMAN_SendSuccessful";
							IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
							IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
							IDbDataParameter parTARGET_TRACKER_KEY = Sql.AddParameter(cmd, "@TARGET_TRACKER_KEY", gTARGET_TRACKER_KEY  );
							IDbDataParameter parEMAIL_ID           = Sql.AddParameter(cmd, "@EMAIL_ID"          , gEMAIL_ID            );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILMAN_SendSuccessful
		/// <summary>
		/// spEMAILMAN_SendSuccessful
		/// </summary>
		public static void spEMAILMAN_SendSuccessful(Guid gID, Guid gTARGET_TRACKER_KEY, Guid gEMAIL_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILMAN_SendSuccessful";
				IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
				IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
				IDbDataParameter parTARGET_TRACKER_KEY = Sql.AddParameter(cmd, "@TARGET_TRACKER_KEY", gTARGET_TRACKER_KEY  );
				IDbDataParameter parEMAIL_ID           = Sql.AddParameter(cmd, "@EMAIL_ID"          , gEMAIL_ID            );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spEMAILS_ArchiveContent
		/// <summary>
		/// spEMAILS_ArchiveContent
		/// </summary>
		public static void spEMAILS_ArchiveContent(Guid gID, string sNAME, string sDESCRIPTION, string sDESCRIPTION_HTML, bool bINCLUDE_CC)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_ArchiveContent";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME              , 255);
							IDbDataParameter parDESCRIPTION      = Sql.AddParameter(cmd, "@DESCRIPTION"     , sDESCRIPTION       );
							IDbDataParameter parDESCRIPTION_HTML = Sql.AddParameter(cmd, "@DESCRIPTION_HTML", sDESCRIPTION_HTML  );
							IDbDataParameter parINCLUDE_CC       = Sql.AddParameter(cmd, "@INCLUDE_CC"      , bINCLUDE_CC        );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_ArchiveContent
		/// <summary>
		/// spEMAILS_ArchiveContent
		/// </summary>
		public static void spEMAILS_ArchiveContent(Guid gID, string sNAME, string sDESCRIPTION, string sDESCRIPTION_HTML, bool bINCLUDE_CC, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILS_ArchiveContent";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME              , 255);
				IDbDataParameter parDESCRIPTION      = Sql.AddParameter(cmd, "@DESCRIPTION"     , sDESCRIPTION       );
				IDbDataParameter parDESCRIPTION_HTML = Sql.AddParameter(cmd, "@DESCRIPTION_HTML", sDESCRIPTION_HTML  );
				IDbDataParameter parINCLUDE_CC       = Sql.AddParameter(cmd, "@INCLUDE_CC"      , bINCLUDE_CC        );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spEMAILS_CampaignRef
		/// <summary>
		/// spEMAILS_CampaignRef
		/// </summary>
		public static void spEMAILS_CampaignRef(ref Guid gID, string sNAME, string sPARENT_TYPE, Guid gPARENT_ID, string sDESCRIPTION, string sDESCRIPTION_HTML, string sFROM_ADDR, string sFROM_NAME, string sTO_ADDRS, string sTO_ADDRS_IDS, string sTO_ADDRS_NAMES, string sTO_ADDRS_EMAILS, string sTYPE, string sSTATUS, string sRELATED_TYPE, Guid gRELATED_ID, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, Guid gTEAM_SET_ID, Guid gASSIGNED_SET_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_CampaignRef";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME              , 255);
							IDbDataParameter parPARENT_TYPE      = Sql.AddParameter(cmd, "@PARENT_TYPE"     , sPARENT_TYPE       ,  25);
							IDbDataParameter parPARENT_ID        = Sql.AddParameter(cmd, "@PARENT_ID"       , gPARENT_ID         );
							IDbDataParameter parDESCRIPTION      = Sql.AddParameter(cmd, "@DESCRIPTION"     , sDESCRIPTION       );
							IDbDataParameter parDESCRIPTION_HTML = Sql.AddParameter(cmd, "@DESCRIPTION_HTML", sDESCRIPTION_HTML  );
							IDbDataParameter parFROM_ADDR        = Sql.AddParameter(cmd, "@FROM_ADDR"       , sFROM_ADDR         , 100);
							IDbDataParameter parFROM_NAME        = Sql.AddParameter(cmd, "@FROM_NAME"       , sFROM_NAME         , 100);
							IDbDataParameter parTO_ADDRS         = Sql.AddParameter(cmd, "@TO_ADDRS"        , sTO_ADDRS          );
							IDbDataParameter parTO_ADDRS_IDS     = Sql.AddAnsiParam(cmd, "@TO_ADDRS_IDS"    , sTO_ADDRS_IDS      , 8000);
							IDbDataParameter parTO_ADDRS_NAMES   = Sql.AddParameter(cmd, "@TO_ADDRS_NAMES"  , sTO_ADDRS_NAMES    );
							IDbDataParameter parTO_ADDRS_EMAILS  = Sql.AddParameter(cmd, "@TO_ADDRS_EMAILS" , sTO_ADDRS_EMAILS   );
							IDbDataParameter parTYPE             = Sql.AddParameter(cmd, "@TYPE"            , sTYPE              ,  25);
							IDbDataParameter parSTATUS           = Sql.AddParameter(cmd, "@STATUS"          , sSTATUS            ,  25);
							IDbDataParameter parRELATED_TYPE     = Sql.AddParameter(cmd, "@RELATED_TYPE"    , sRELATED_TYPE      ,  25);
							IDbDataParameter parRELATED_ID       = Sql.AddParameter(cmd, "@RELATED_ID"      , gRELATED_ID        );
							IDbDataParameter parASSIGNED_USER_ID = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", gASSIGNED_USER_ID  );
							IDbDataParameter parTEAM_ID          = Sql.AddParameter(cmd, "@TEAM_ID"         , gTEAM_ID           );
							IDbDataParameter parTEAM_SET_ID      = Sql.AddParameter(cmd, "@TEAM_SET_ID"     , gTEAM_SET_ID       );
							IDbDataParameter parASSIGNED_SET_ID  = Sql.AddParameter(cmd, "@ASSIGNED_SET_ID" , gASSIGNED_SET_ID   );
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
		#endregion

		#region spEMAILS_CampaignRef
		/// <summary>
		/// spEMAILS_CampaignRef
		/// </summary>
		public static void spEMAILS_CampaignRef(ref Guid gID, string sNAME, string sPARENT_TYPE, Guid gPARENT_ID, string sDESCRIPTION, string sDESCRIPTION_HTML, string sFROM_ADDR, string sFROM_NAME, string sTO_ADDRS, string sTO_ADDRS_IDS, string sTO_ADDRS_NAMES, string sTO_ADDRS_EMAILS, string sTYPE, string sSTATUS, string sRELATED_TYPE, Guid gRELATED_ID, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, Guid gTEAM_SET_ID, Guid gASSIGNED_SET_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILS_CampaignRef";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME              , 255);
				IDbDataParameter parPARENT_TYPE      = Sql.AddParameter(cmd, "@PARENT_TYPE"     , sPARENT_TYPE       ,  25);
				IDbDataParameter parPARENT_ID        = Sql.AddParameter(cmd, "@PARENT_ID"       , gPARENT_ID         );
				IDbDataParameter parDESCRIPTION      = Sql.AddParameter(cmd, "@DESCRIPTION"     , sDESCRIPTION       );
				IDbDataParameter parDESCRIPTION_HTML = Sql.AddParameter(cmd, "@DESCRIPTION_HTML", sDESCRIPTION_HTML  );
				IDbDataParameter parFROM_ADDR        = Sql.AddParameter(cmd, "@FROM_ADDR"       , sFROM_ADDR         , 100);
				IDbDataParameter parFROM_NAME        = Sql.AddParameter(cmd, "@FROM_NAME"       , sFROM_NAME         , 100);
				IDbDataParameter parTO_ADDRS         = Sql.AddParameter(cmd, "@TO_ADDRS"        , sTO_ADDRS          );
				IDbDataParameter parTO_ADDRS_IDS     = Sql.AddAnsiParam(cmd, "@TO_ADDRS_IDS"    , sTO_ADDRS_IDS      , 8000);
				IDbDataParameter parTO_ADDRS_NAMES   = Sql.AddParameter(cmd, "@TO_ADDRS_NAMES"  , sTO_ADDRS_NAMES    );
				IDbDataParameter parTO_ADDRS_EMAILS  = Sql.AddParameter(cmd, "@TO_ADDRS_EMAILS" , sTO_ADDRS_EMAILS   );
				IDbDataParameter parTYPE             = Sql.AddParameter(cmd, "@TYPE"            , sTYPE              ,  25);
				IDbDataParameter parSTATUS           = Sql.AddParameter(cmd, "@STATUS"          , sSTATUS            ,  25);
				IDbDataParameter parRELATED_TYPE     = Sql.AddParameter(cmd, "@RELATED_TYPE"    , sRELATED_TYPE      ,  25);
				IDbDataParameter parRELATED_ID       = Sql.AddParameter(cmd, "@RELATED_ID"      , gRELATED_ID        );
				IDbDataParameter parASSIGNED_USER_ID = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", gASSIGNED_USER_ID  );
				IDbDataParameter parTEAM_ID          = Sql.AddParameter(cmd, "@TEAM_ID"         , gTEAM_ID           );
				IDbDataParameter parTEAM_SET_ID      = Sql.AddParameter(cmd, "@TEAM_SET_ID"     , gTEAM_SET_ID       );
				IDbDataParameter parASSIGNED_SET_ID  = Sql.AddParameter(cmd, "@ASSIGNED_SET_ID" , gASSIGNED_SET_ID   );
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spEMAILS_CONTACTS_Update
		/// <summary>
		/// spEMAILS_CONTACTS_Update
		/// </summary>
		public static void spEMAILS_CONTACTS_Update(Guid gEMAIL_ID, Guid gCONTACT_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_CONTACTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID          );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_CONTACTS_Update
		/// <summary>
		/// spEMAILS_CONTACTS_Update
		/// </summary>
		public static void spEMAILS_CONTACTS_Update(Guid gEMAIL_ID, Guid gCONTACT_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILS_CONTACTS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID          );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spEMAILS_GetMailbox
		/// <summary>
		/// spEMAILS_GetMailbox
		/// </summary>
		public static void spEMAILS_GetMailbox(Guid gID, ref string sMAIL_SENDTYPE, ref string sMAIL_SMTPSERVER, ref Int32 nMAIL_SMTPPORT, ref string sMAIL_SMTPUSER, ref string sMAIL_SMTPPASS, ref bool bMAIL_SMTPAUTH_REQ, ref bool bMAIL_SMTPSSL, ref Guid gOAUTH_USER_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_GetMailbox";
							IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
							IDbDataParameter parMAIL_SENDTYPE     = Sql.AddParameter(cmd, "@MAIL_SENDTYPE"    , sMAIL_SENDTYPE      ,  25);
							IDbDataParameter parMAIL_SMTPSERVER   = Sql.AddParameter(cmd, "@MAIL_SMTPSERVER"  , sMAIL_SMTPSERVER    , 100);
							IDbDataParameter parMAIL_SMTPPORT     = Sql.AddParameter(cmd, "@MAIL_SMTPPORT"    , nMAIL_SMTPPORT      );
							IDbDataParameter parMAIL_SMTPUSER     = Sql.AddParameter(cmd, "@MAIL_SMTPUSER"    , sMAIL_SMTPUSER      , 100);
							IDbDataParameter parMAIL_SMTPPASS     = Sql.AddParameter(cmd, "@MAIL_SMTPPASS"    , sMAIL_SMTPPASS      , 100);
							IDbDataParameter parMAIL_SMTPAUTH_REQ = Sql.AddParameter(cmd, "@MAIL_SMTPAUTH_REQ", bMAIL_SMTPAUTH_REQ  );
							IDbDataParameter parMAIL_SMTPSSL      = Sql.AddParameter(cmd, "@MAIL_SMTPSSL"     , bMAIL_SMTPSSL       );
							IDbDataParameter parOAUTH_USER_ID     = Sql.AddParameter(cmd, "@OAUTH_USER_ID"    , gOAUTH_USER_ID      );
							parMAIL_SENDTYPE.Direction = ParameterDirection.InputOutput;
							parMAIL_SMTPSERVER.Direction = ParameterDirection.InputOutput;
							parMAIL_SMTPPORT.Direction = ParameterDirection.InputOutput;
							parMAIL_SMTPUSER.Direction = ParameterDirection.InputOutput;
							parMAIL_SMTPPASS.Direction = ParameterDirection.InputOutput;
							parMAIL_SMTPAUTH_REQ.Direction = ParameterDirection.InputOutput;
							parMAIL_SMTPSSL.Direction = ParameterDirection.InputOutput;
							parOAUTH_USER_ID.Direction = ParameterDirection.InputOutput;
							cmd.ExecuteNonQuery();
							sMAIL_SENDTYPE = Sql.ToString(parMAIL_SENDTYPE.Value);
							sMAIL_SMTPSERVER = Sql.ToString(parMAIL_SMTPSERVER.Value);
							nMAIL_SMTPPORT = Sql.ToInteger(parMAIL_SMTPPORT.Value);
							sMAIL_SMTPUSER = Sql.ToString(parMAIL_SMTPUSER.Value);
							sMAIL_SMTPPASS = Sql.ToString(parMAIL_SMTPPASS.Value);
							bMAIL_SMTPAUTH_REQ = Sql.ToBoolean(parMAIL_SMTPAUTH_REQ.Value);
							bMAIL_SMTPSSL = Sql.ToBoolean(parMAIL_SMTPSSL.Value);
							gOAUTH_USER_ID = Sql.ToGuid(parOAUTH_USER_ID.Value);
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
		#endregion

		#region spEMAILS_GetMailbox
		/// <summary>
		/// spEMAILS_GetMailbox
		/// </summary>
		public static void spEMAILS_GetMailbox(Guid gID, ref string sMAIL_SENDTYPE, ref string sMAIL_SMTPSERVER, ref Int32 nMAIL_SMTPPORT, ref string sMAIL_SMTPUSER, ref string sMAIL_SMTPPASS, ref bool bMAIL_SMTPAUTH_REQ, ref bool bMAIL_SMTPSSL, ref Guid gOAUTH_USER_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILS_GetMailbox";
				IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
				IDbDataParameter parMAIL_SENDTYPE     = Sql.AddParameter(cmd, "@MAIL_SENDTYPE"    , sMAIL_SENDTYPE      ,  25);
				IDbDataParameter parMAIL_SMTPSERVER   = Sql.AddParameter(cmd, "@MAIL_SMTPSERVER"  , sMAIL_SMTPSERVER    , 100);
				IDbDataParameter parMAIL_SMTPPORT     = Sql.AddParameter(cmd, "@MAIL_SMTPPORT"    , nMAIL_SMTPPORT      );
				IDbDataParameter parMAIL_SMTPUSER     = Sql.AddParameter(cmd, "@MAIL_SMTPUSER"    , sMAIL_SMTPUSER      , 100);
				IDbDataParameter parMAIL_SMTPPASS     = Sql.AddParameter(cmd, "@MAIL_SMTPPASS"    , sMAIL_SMTPPASS      , 100);
				IDbDataParameter parMAIL_SMTPAUTH_REQ = Sql.AddParameter(cmd, "@MAIL_SMTPAUTH_REQ", bMAIL_SMTPAUTH_REQ  );
				IDbDataParameter parMAIL_SMTPSSL      = Sql.AddParameter(cmd, "@MAIL_SMTPSSL"     , bMAIL_SMTPSSL       );
				IDbDataParameter parOAUTH_USER_ID     = Sql.AddParameter(cmd, "@OAUTH_USER_ID"    , gOAUTH_USER_ID      );
				parMAIL_SENDTYPE.Direction = ParameterDirection.InputOutput;
				parMAIL_SMTPSERVER.Direction = ParameterDirection.InputOutput;
				parMAIL_SMTPPORT.Direction = ParameterDirection.InputOutput;
				parMAIL_SMTPUSER.Direction = ParameterDirection.InputOutput;
				parMAIL_SMTPPASS.Direction = ParameterDirection.InputOutput;
				parMAIL_SMTPAUTH_REQ.Direction = ParameterDirection.InputOutput;
				parMAIL_SMTPSSL.Direction = ParameterDirection.InputOutput;
				parOAUTH_USER_ID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				sMAIL_SENDTYPE = Sql.ToString(parMAIL_SENDTYPE.Value);
				sMAIL_SMTPSERVER = Sql.ToString(parMAIL_SMTPSERVER.Value);
				nMAIL_SMTPPORT = Sql.ToInteger(parMAIL_SMTPPORT.Value);
				sMAIL_SMTPUSER = Sql.ToString(parMAIL_SMTPUSER.Value);
				sMAIL_SMTPPASS = Sql.ToString(parMAIL_SMTPPASS.Value);
				bMAIL_SMTPAUTH_REQ = Sql.ToBoolean(parMAIL_SMTPAUTH_REQ.Value);
				bMAIL_SMTPSSL = Sql.ToBoolean(parMAIL_SMTPSSL.Value);
				gOAUTH_USER_ID = Sql.ToGuid(parOAUTH_USER_ID.Value);
			}
		}
		#endregion

		#region spEMAILS_InsertInbound
		/// <summary>
		/// spEMAILS_InsertInbound
		/// </summary>
		public static void spEMAILS_InsertInbound(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, DateTime dtDATE_TIME, string sDESCRIPTION, string sDESCRIPTION_HTML, string sFROM_ADDR, string sFROM_NAME, string sTO_ADDRS, string sCC_ADDRS, string sBCC_ADDRS, string sTO_ADDRS_NAMES, string sTO_ADDRS_EMAILS, string sCC_ADDRS_NAMES, string sCC_ADDRS_EMAILS, string sBCC_ADDRS_NAMES, string sBCC_ADDRS_EMAILS, string sTYPE, string sSTATUS, string sMESSAGE_ID, string sREPLY_TO_NAME, string sREPLY_TO_ADDR, string sINTENT, Guid gMAILBOX_ID, Guid gTARGET_TRACKER_KEY, string sRAW_SOURCE, Guid gTEAM_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_InsertInbound";
							IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
							IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
							IDbDataParameter parASSIGNED_USER_ID   = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"  , gASSIGNED_USER_ID    );
							IDbDataParameter parNAME               = Sql.AddParameter(cmd, "@NAME"              , sNAME                , 255);
							IDbDataParameter parDATE_TIME          = Sql.AddParameter(cmd, "@DATE_TIME"         , dtDATE_TIME          );
							IDbDataParameter parDESCRIPTION        = Sql.AddParameter(cmd, "@DESCRIPTION"       , sDESCRIPTION         );
							IDbDataParameter parDESCRIPTION_HTML   = Sql.AddParameter(cmd, "@DESCRIPTION_HTML"  , sDESCRIPTION_HTML    );
							IDbDataParameter parFROM_ADDR          = Sql.AddParameter(cmd, "@FROM_ADDR"         , sFROM_ADDR           , 100);
							IDbDataParameter parFROM_NAME          = Sql.AddParameter(cmd, "@FROM_NAME"         , sFROM_NAME           , 100);
							IDbDataParameter parTO_ADDRS           = Sql.AddParameter(cmd, "@TO_ADDRS"          , sTO_ADDRS            );
							IDbDataParameter parCC_ADDRS           = Sql.AddParameter(cmd, "@CC_ADDRS"          , sCC_ADDRS            );
							IDbDataParameter parBCC_ADDRS          = Sql.AddParameter(cmd, "@BCC_ADDRS"         , sBCC_ADDRS           );
							IDbDataParameter parTO_ADDRS_NAMES     = Sql.AddParameter(cmd, "@TO_ADDRS_NAMES"    , sTO_ADDRS_NAMES      );
							IDbDataParameter parTO_ADDRS_EMAILS    = Sql.AddAnsiParam(cmd, "@TO_ADDRS_EMAILS"   , sTO_ADDRS_EMAILS     , 8000);
							IDbDataParameter parCC_ADDRS_NAMES     = Sql.AddParameter(cmd, "@CC_ADDRS_NAMES"    , sCC_ADDRS_NAMES      );
							IDbDataParameter parCC_ADDRS_EMAILS    = Sql.AddAnsiParam(cmd, "@CC_ADDRS_EMAILS"   , sCC_ADDRS_EMAILS     , 8000);
							IDbDataParameter parBCC_ADDRS_NAMES    = Sql.AddParameter(cmd, "@BCC_ADDRS_NAMES"   , sBCC_ADDRS_NAMES     );
							IDbDataParameter parBCC_ADDRS_EMAILS   = Sql.AddAnsiParam(cmd, "@BCC_ADDRS_EMAILS"  , sBCC_ADDRS_EMAILS    , 8000);
							IDbDataParameter parTYPE               = Sql.AddParameter(cmd, "@TYPE"              , sTYPE                ,  25);
							IDbDataParameter parSTATUS             = Sql.AddParameter(cmd, "@STATUS"            , sSTATUS              ,  25);
							IDbDataParameter parMESSAGE_ID         = Sql.AddAnsiParam(cmd, "@MESSAGE_ID"        , sMESSAGE_ID          , 851);
							IDbDataParameter parREPLY_TO_NAME      = Sql.AddParameter(cmd, "@REPLY_TO_NAME"     , sREPLY_TO_NAME       , 100);
							IDbDataParameter parREPLY_TO_ADDR      = Sql.AddParameter(cmd, "@REPLY_TO_ADDR"     , sREPLY_TO_ADDR       , 100);
							IDbDataParameter parINTENT             = Sql.AddParameter(cmd, "@INTENT"            , sINTENT              ,  25);
							IDbDataParameter parMAILBOX_ID         = Sql.AddParameter(cmd, "@MAILBOX_ID"        , gMAILBOX_ID          );
							IDbDataParameter parTARGET_TRACKER_KEY = Sql.AddParameter(cmd, "@TARGET_TRACKER_KEY", gTARGET_TRACKER_KEY  );
							IDbDataParameter parRAW_SOURCE         = Sql.AddParameter(cmd, "@RAW_SOURCE"        , sRAW_SOURCE          );
							IDbDataParameter parTEAM_ID            = Sql.AddParameter(cmd, "@TEAM_ID"           , gTEAM_ID             );
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
		#endregion

		#region spEMAILS_InsertInbound
		/// <summary>
		/// spEMAILS_InsertInbound
		/// </summary>
		public static void spEMAILS_InsertInbound(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, DateTime dtDATE_TIME, string sDESCRIPTION, string sDESCRIPTION_HTML, string sFROM_ADDR, string sFROM_NAME, string sTO_ADDRS, string sCC_ADDRS, string sBCC_ADDRS, string sTO_ADDRS_NAMES, string sTO_ADDRS_EMAILS, string sCC_ADDRS_NAMES, string sCC_ADDRS_EMAILS, string sBCC_ADDRS_NAMES, string sBCC_ADDRS_EMAILS, string sTYPE, string sSTATUS, string sMESSAGE_ID, string sREPLY_TO_NAME, string sREPLY_TO_ADDR, string sINTENT, Guid gMAILBOX_ID, Guid gTARGET_TRACKER_KEY, string sRAW_SOURCE, Guid gTEAM_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILS_InsertInbound";
				IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
				IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
				IDbDataParameter parASSIGNED_USER_ID   = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"  , gASSIGNED_USER_ID    );
				IDbDataParameter parNAME               = Sql.AddParameter(cmd, "@NAME"              , sNAME                , 255);
				IDbDataParameter parDATE_TIME          = Sql.AddParameter(cmd, "@DATE_TIME"         , dtDATE_TIME          );
				IDbDataParameter parDESCRIPTION        = Sql.AddParameter(cmd, "@DESCRIPTION"       , sDESCRIPTION         );
				IDbDataParameter parDESCRIPTION_HTML   = Sql.AddParameter(cmd, "@DESCRIPTION_HTML"  , sDESCRIPTION_HTML    );
				IDbDataParameter parFROM_ADDR          = Sql.AddParameter(cmd, "@FROM_ADDR"         , sFROM_ADDR           , 100);
				IDbDataParameter parFROM_NAME          = Sql.AddParameter(cmd, "@FROM_NAME"         , sFROM_NAME           , 100);
				IDbDataParameter parTO_ADDRS           = Sql.AddParameter(cmd, "@TO_ADDRS"          , sTO_ADDRS            );
				IDbDataParameter parCC_ADDRS           = Sql.AddParameter(cmd, "@CC_ADDRS"          , sCC_ADDRS            );
				IDbDataParameter parBCC_ADDRS          = Sql.AddParameter(cmd, "@BCC_ADDRS"         , sBCC_ADDRS           );
				IDbDataParameter parTO_ADDRS_NAMES     = Sql.AddParameter(cmd, "@TO_ADDRS_NAMES"    , sTO_ADDRS_NAMES      );
				IDbDataParameter parTO_ADDRS_EMAILS    = Sql.AddAnsiParam(cmd, "@TO_ADDRS_EMAILS"   , sTO_ADDRS_EMAILS     , 8000);
				IDbDataParameter parCC_ADDRS_NAMES     = Sql.AddParameter(cmd, "@CC_ADDRS_NAMES"    , sCC_ADDRS_NAMES      );
				IDbDataParameter parCC_ADDRS_EMAILS    = Sql.AddAnsiParam(cmd, "@CC_ADDRS_EMAILS"   , sCC_ADDRS_EMAILS     , 8000);
				IDbDataParameter parBCC_ADDRS_NAMES    = Sql.AddParameter(cmd, "@BCC_ADDRS_NAMES"   , sBCC_ADDRS_NAMES     );
				IDbDataParameter parBCC_ADDRS_EMAILS   = Sql.AddAnsiParam(cmd, "@BCC_ADDRS_EMAILS"  , sBCC_ADDRS_EMAILS    , 8000);
				IDbDataParameter parTYPE               = Sql.AddParameter(cmd, "@TYPE"              , sTYPE                ,  25);
				IDbDataParameter parSTATUS             = Sql.AddParameter(cmd, "@STATUS"            , sSTATUS              ,  25);
				IDbDataParameter parMESSAGE_ID         = Sql.AddAnsiParam(cmd, "@MESSAGE_ID"        , sMESSAGE_ID          , 851);
				IDbDataParameter parREPLY_TO_NAME      = Sql.AddParameter(cmd, "@REPLY_TO_NAME"     , sREPLY_TO_NAME       , 100);
				IDbDataParameter parREPLY_TO_ADDR      = Sql.AddParameter(cmd, "@REPLY_TO_ADDR"     , sREPLY_TO_ADDR       , 100);
				IDbDataParameter parINTENT             = Sql.AddParameter(cmd, "@INTENT"            , sINTENT              ,  25);
				IDbDataParameter parMAILBOX_ID         = Sql.AddParameter(cmd, "@MAILBOX_ID"        , gMAILBOX_ID          );
				IDbDataParameter parTARGET_TRACKER_KEY = Sql.AddParameter(cmd, "@TARGET_TRACKER_KEY", gTARGET_TRACKER_KEY  );
				IDbDataParameter parRAW_SOURCE         = Sql.AddParameter(cmd, "@RAW_SOURCE"        , sRAW_SOURCE          );
				IDbDataParameter parTEAM_ID            = Sql.AddParameter(cmd, "@TEAM_ID"           , gTEAM_ID             );
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spEMAILS_Update
		/// <summary>
		/// spEMAILS_Update
		/// </summary>
		public static void spEMAILS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, DateTime dtDATE_TIME, string sPARENT_TYPE, Guid gPARENT_ID, string sDESCRIPTION, string sDESCRIPTION_HTML, string sFROM_ADDR, string sFROM_NAME, string sTO_ADDRS, string sCC_ADDRS, string sBCC_ADDRS, string sTO_ADDRS_IDS, string sTO_ADDRS_NAMES, string sTO_ADDRS_EMAILS, string sCC_ADDRS_IDS, string sCC_ADDRS_NAMES, string sCC_ADDRS_EMAILS, string sBCC_ADDRS_IDS, string sBCC_ADDRS_NAMES, string sBCC_ADDRS_EMAILS, string sTYPE, string sMESSAGE_ID, string sREPLY_TO_NAME, string sREPLY_TO_ADDR, string sINTENT, Guid gMAILBOX_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sTAG_SET_NAME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_Update";
							IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
							IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
							IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
							IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sNAME               , 255);
							IDbDataParameter parDATE_TIME         = Sql.AddParameter(cmd, "@DATE_TIME"        , dtDATE_TIME         );
							IDbDataParameter parPARENT_TYPE       = Sql.AddParameter(cmd, "@PARENT_TYPE"      , sPARENT_TYPE        ,  25);
							IDbDataParameter parPARENT_ID         = Sql.AddParameter(cmd, "@PARENT_ID"        , gPARENT_ID          );
							IDbDataParameter parDESCRIPTION       = Sql.AddParameter(cmd, "@DESCRIPTION"      , sDESCRIPTION        );
							IDbDataParameter parDESCRIPTION_HTML  = Sql.AddParameter(cmd, "@DESCRIPTION_HTML" , sDESCRIPTION_HTML   );
							IDbDataParameter parFROM_ADDR         = Sql.AddParameter(cmd, "@FROM_ADDR"        , sFROM_ADDR          , 100);
							IDbDataParameter parFROM_NAME         = Sql.AddParameter(cmd, "@FROM_NAME"        , sFROM_NAME          , 100);
							IDbDataParameter parTO_ADDRS          = Sql.AddParameter(cmd, "@TO_ADDRS"         , sTO_ADDRS           );
							IDbDataParameter parCC_ADDRS          = Sql.AddParameter(cmd, "@CC_ADDRS"         , sCC_ADDRS           );
							IDbDataParameter parBCC_ADDRS         = Sql.AddParameter(cmd, "@BCC_ADDRS"        , sBCC_ADDRS          );
							IDbDataParameter parTO_ADDRS_IDS      = Sql.AddAnsiParam(cmd, "@TO_ADDRS_IDS"     , sTO_ADDRS_IDS       , 8000);
							IDbDataParameter parTO_ADDRS_NAMES    = Sql.AddParameter(cmd, "@TO_ADDRS_NAMES"   , sTO_ADDRS_NAMES     );
							IDbDataParameter parTO_ADDRS_EMAILS   = Sql.AddParameter(cmd, "@TO_ADDRS_EMAILS"  , sTO_ADDRS_EMAILS    );
							IDbDataParameter parCC_ADDRS_IDS      = Sql.AddAnsiParam(cmd, "@CC_ADDRS_IDS"     , sCC_ADDRS_IDS       , 8000);
							IDbDataParameter parCC_ADDRS_NAMES    = Sql.AddParameter(cmd, "@CC_ADDRS_NAMES"   , sCC_ADDRS_NAMES     );
							IDbDataParameter parCC_ADDRS_EMAILS   = Sql.AddParameter(cmd, "@CC_ADDRS_EMAILS"  , sCC_ADDRS_EMAILS    );
							IDbDataParameter parBCC_ADDRS_IDS     = Sql.AddAnsiParam(cmd, "@BCC_ADDRS_IDS"    , sBCC_ADDRS_IDS      , 8000);
							IDbDataParameter parBCC_ADDRS_NAMES   = Sql.AddParameter(cmd, "@BCC_ADDRS_NAMES"  , sBCC_ADDRS_NAMES    );
							IDbDataParameter parBCC_ADDRS_EMAILS  = Sql.AddParameter(cmd, "@BCC_ADDRS_EMAILS" , sBCC_ADDRS_EMAILS   );
							IDbDataParameter parTYPE              = Sql.AddParameter(cmd, "@TYPE"             , sTYPE               ,  25);
							IDbDataParameter parMESSAGE_ID        = Sql.AddAnsiParam(cmd, "@MESSAGE_ID"       , sMESSAGE_ID         , 851);
							IDbDataParameter parREPLY_TO_NAME     = Sql.AddParameter(cmd, "@REPLY_TO_NAME"    , sREPLY_TO_NAME      , 100);
							IDbDataParameter parREPLY_TO_ADDR     = Sql.AddParameter(cmd, "@REPLY_TO_ADDR"    , sREPLY_TO_ADDR      , 100);
							IDbDataParameter parINTENT            = Sql.AddParameter(cmd, "@INTENT"           , sINTENT             ,  25);
							IDbDataParameter parMAILBOX_ID        = Sql.AddParameter(cmd, "@MAILBOX_ID"       , gMAILBOX_ID         );
							IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
							IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
							IDbDataParameter parTAG_SET_NAME      = Sql.AddParameter(cmd, "@TAG_SET_NAME"     , sTAG_SET_NAME       , 4000);
							IDbDataParameter parIS_PRIVATE        = Sql.AddParameter(cmd, "@IS_PRIVATE"       , bIS_PRIVATE         );
							IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
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
		#endregion

		#region spEMAILS_Update
		/// <summary>
		/// spEMAILS_Update
		/// </summary>
		public static void spEMAILS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, DateTime dtDATE_TIME, string sPARENT_TYPE, Guid gPARENT_ID, string sDESCRIPTION, string sDESCRIPTION_HTML, string sFROM_ADDR, string sFROM_NAME, string sTO_ADDRS, string sCC_ADDRS, string sBCC_ADDRS, string sTO_ADDRS_IDS, string sTO_ADDRS_NAMES, string sTO_ADDRS_EMAILS, string sCC_ADDRS_IDS, string sCC_ADDRS_NAMES, string sCC_ADDRS_EMAILS, string sBCC_ADDRS_IDS, string sBCC_ADDRS_NAMES, string sBCC_ADDRS_EMAILS, string sTYPE, string sMESSAGE_ID, string sREPLY_TO_NAME, string sREPLY_TO_ADDR, string sINTENT, Guid gMAILBOX_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sTAG_SET_NAME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILS_Update";
				IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
				IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
				IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
				IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sNAME               , 255);
				IDbDataParameter parDATE_TIME         = Sql.AddParameter(cmd, "@DATE_TIME"        , dtDATE_TIME         );
				IDbDataParameter parPARENT_TYPE       = Sql.AddParameter(cmd, "@PARENT_TYPE"      , sPARENT_TYPE        ,  25);
				IDbDataParameter parPARENT_ID         = Sql.AddParameter(cmd, "@PARENT_ID"        , gPARENT_ID          );
				IDbDataParameter parDESCRIPTION       = Sql.AddParameter(cmd, "@DESCRIPTION"      , sDESCRIPTION        );
				IDbDataParameter parDESCRIPTION_HTML  = Sql.AddParameter(cmd, "@DESCRIPTION_HTML" , sDESCRIPTION_HTML   );
				IDbDataParameter parFROM_ADDR         = Sql.AddParameter(cmd, "@FROM_ADDR"        , sFROM_ADDR          , 100);
				IDbDataParameter parFROM_NAME         = Sql.AddParameter(cmd, "@FROM_NAME"        , sFROM_NAME          , 100);
				IDbDataParameter parTO_ADDRS          = Sql.AddParameter(cmd, "@TO_ADDRS"         , sTO_ADDRS           );
				IDbDataParameter parCC_ADDRS          = Sql.AddParameter(cmd, "@CC_ADDRS"         , sCC_ADDRS           );
				IDbDataParameter parBCC_ADDRS         = Sql.AddParameter(cmd, "@BCC_ADDRS"        , sBCC_ADDRS          );
				IDbDataParameter parTO_ADDRS_IDS      = Sql.AddAnsiParam(cmd, "@TO_ADDRS_IDS"     , sTO_ADDRS_IDS       , 8000);
				IDbDataParameter parTO_ADDRS_NAMES    = Sql.AddParameter(cmd, "@TO_ADDRS_NAMES"   , sTO_ADDRS_NAMES     );
				IDbDataParameter parTO_ADDRS_EMAILS   = Sql.AddParameter(cmd, "@TO_ADDRS_EMAILS"  , sTO_ADDRS_EMAILS    );
				IDbDataParameter parCC_ADDRS_IDS      = Sql.AddAnsiParam(cmd, "@CC_ADDRS_IDS"     , sCC_ADDRS_IDS       , 8000);
				IDbDataParameter parCC_ADDRS_NAMES    = Sql.AddParameter(cmd, "@CC_ADDRS_NAMES"   , sCC_ADDRS_NAMES     );
				IDbDataParameter parCC_ADDRS_EMAILS   = Sql.AddParameter(cmd, "@CC_ADDRS_EMAILS"  , sCC_ADDRS_EMAILS    );
				IDbDataParameter parBCC_ADDRS_IDS     = Sql.AddAnsiParam(cmd, "@BCC_ADDRS_IDS"    , sBCC_ADDRS_IDS      , 8000);
				IDbDataParameter parBCC_ADDRS_NAMES   = Sql.AddParameter(cmd, "@BCC_ADDRS_NAMES"  , sBCC_ADDRS_NAMES    );
				IDbDataParameter parBCC_ADDRS_EMAILS  = Sql.AddParameter(cmd, "@BCC_ADDRS_EMAILS" , sBCC_ADDRS_EMAILS   );
				IDbDataParameter parTYPE              = Sql.AddParameter(cmd, "@TYPE"             , sTYPE               ,  25);
				IDbDataParameter parMESSAGE_ID        = Sql.AddAnsiParam(cmd, "@MESSAGE_ID"       , sMESSAGE_ID         , 851);
				IDbDataParameter parREPLY_TO_NAME     = Sql.AddParameter(cmd, "@REPLY_TO_NAME"    , sREPLY_TO_NAME      , 100);
				IDbDataParameter parREPLY_TO_ADDR     = Sql.AddParameter(cmd, "@REPLY_TO_ADDR"    , sREPLY_TO_ADDR      , 100);
				IDbDataParameter parINTENT            = Sql.AddParameter(cmd, "@INTENT"           , sINTENT             ,  25);
				IDbDataParameter parMAILBOX_ID        = Sql.AddParameter(cmd, "@MAILBOX_ID"       , gMAILBOX_ID         );
				IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
				IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
				IDbDataParameter parTAG_SET_NAME      = Sql.AddParameter(cmd, "@TAG_SET_NAME"     , sTAG_SET_NAME       , 4000);
				IDbDataParameter parIS_PRIVATE        = Sql.AddParameter(cmd, "@IS_PRIVATE"       , bIS_PRIVATE         );
				IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spEMAILS_UpdateContent
		/// <summary>
		/// spEMAILS_UpdateContent
		/// </summary>
		public static void spEMAILS_UpdateContent(Guid gID, string sNAME, string sDESCRIPTION, string sDESCRIPTION_HTML)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_UpdateContent";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME              , 255);
							IDbDataParameter parDESCRIPTION      = Sql.AddParameter(cmd, "@DESCRIPTION"     , sDESCRIPTION       );
							IDbDataParameter parDESCRIPTION_HTML = Sql.AddParameter(cmd, "@DESCRIPTION_HTML", sDESCRIPTION_HTML  );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_UpdateContent
		/// <summary>
		/// spEMAILS_UpdateContent
		/// </summary>
		public static void spEMAILS_UpdateContent(Guid gID, string sNAME, string sDESCRIPTION, string sDESCRIPTION_HTML, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILS_UpdateContent";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME              , 255);
				IDbDataParameter parDESCRIPTION      = Sql.AddParameter(cmd, "@DESCRIPTION"     , sDESCRIPTION       );
				IDbDataParameter parDESCRIPTION_HTML = Sql.AddParameter(cmd, "@DESCRIPTION_HTML", sDESCRIPTION_HTML  );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spEMAILS_UpdateStatus
		/// <summary>
		/// spEMAILS_UpdateStatus
		/// </summary>
		public static void spEMAILS_UpdateStatus(Guid gID, string sSTATUS)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_UpdateStatus";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parSTATUS           = Sql.AddParameter(cmd, "@STATUS"          , sSTATUS            ,  25);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_UpdateStatus
		/// <summary>
		/// spEMAILS_UpdateStatus
		/// </summary>
		public static void spEMAILS_UpdateStatus(Guid gID, string sSTATUS, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILS_UpdateStatus";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parSTATUS           = Sql.AddParameter(cmd, "@STATUS"          , sSTATUS            ,  25);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spEMAILS_USERS_Update
		/// <summary>
		/// spEMAILS_USERS_Update
		/// </summary>
		public static void spEMAILS_USERS_Update(Guid gEMAIL_ID, Guid gUSER_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_USERS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID          );
							IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_USERS_Update
		/// <summary>
		/// spEMAILS_USERS_Update
		/// </summary>
		public static void spEMAILS_USERS_Update(Guid gEMAIL_ID, Guid gUSER_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spEMAILS_USERS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID          );
				IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spINBOUND_EMAILS_ExchangeWatermark
		/// <summary>
		/// spINBOUND_EMAILS_ExchangeWatermark
		/// </summary>
		public static void spINBOUND_EMAILS_ExchangeWatermark(Guid gID, string sEXCHANGE_WATERMARK)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							if ( Sql.IsOracle(cmd) )
								cmd.CommandText = "spINBOUND_EMAILS_ExchangeWater";
							else
								cmd.CommandText = "spINBOUND_EMAILS_ExchangeWatermark";
							IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
							IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
							IDbDataParameter parEXCHANGE_WATERMARK = Sql.AddAnsiParam(cmd, "@EXCHANGE_WATERMARK", sEXCHANGE_WATERMARK  , 100);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spINBOUND_EMAILS_ExchangeWatermark
		/// <summary>
		/// spINBOUND_EMAILS_ExchangeWatermark
		/// </summary>
		public static void spINBOUND_EMAILS_ExchangeWatermark(Guid gID, string sEXCHANGE_WATERMARK, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				if ( Sql.IsOracle(cmd) )
					cmd.CommandText = "spINBOUND_EMAILS_ExchangeWater";
				else
					cmd.CommandText = "spINBOUND_EMAILS_ExchangeWatermark";
				IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
				IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
				IDbDataParameter parEXCHANGE_WATERMARK = Sql.AddAnsiParam(cmd, "@EXCHANGE_WATERMARK", sEXCHANGE_WATERMARK  , 100);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spINBOUND_EMAILS_UpdateLastUID
		/// <summary>
		/// spINBOUND_EMAILS_UpdateLastUID
		/// </summary>
		public static void spINBOUND_EMAILS_UpdateLastUID(Guid gID, Int64 lLAST_EMAIL_UID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spINBOUND_EMAILS_UpdateLastUID";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parLAST_EMAIL_UID   = Sql.AddParameter(cmd, "@LAST_EMAIL_UID"  , lLAST_EMAIL_UID    );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spINBOUND_EMAILS_UpdateLastUID
		/// <summary>
		/// spINBOUND_EMAILS_UpdateLastUID
		/// </summary>
		public static void spINBOUND_EMAILS_UpdateLastUID(Guid gID, Int64 lLAST_EMAIL_UID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spINBOUND_EMAILS_UpdateLastUID";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parLAST_EMAIL_UID   = Sql.AddParameter(cmd, "@LAST_EMAIL_UID"  , lLAST_EMAIL_UID    );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spLEADS_New
		/// <summary>
		/// spLEADS_New
		/// </summary>
		public static void spLEADS_New(ref Guid gID, string sFIRST_NAME, string sLAST_NAME, string sPHONE_WORK, string sEMAIL1, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spLEADS_New";
							IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
							IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
							IDbDataParameter parFIRST_NAME        = Sql.AddParameter(cmd, "@FIRST_NAME"       , sFIRST_NAME         , 100);
							IDbDataParameter parLAST_NAME         = Sql.AddParameter(cmd, "@LAST_NAME"        , sLAST_NAME          , 100);
							IDbDataParameter parPHONE_WORK        = Sql.AddParameter(cmd, "@PHONE_WORK"       , sPHONE_WORK         ,  25);
							IDbDataParameter parEMAIL1            = Sql.AddParameter(cmd, "@EMAIL1"           , sEMAIL1             , 100);
							IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
							IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
							IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
							IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
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
		#endregion

		#region spLEADS_New
		/// <summary>
		/// spLEADS_New
		/// </summary>
		public static void spLEADS_New(ref Guid gID, string sFIRST_NAME, string sLAST_NAME, string sPHONE_WORK, string sEMAIL1, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spLEADS_New";
				IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
				IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
				IDbDataParameter parFIRST_NAME        = Sql.AddParameter(cmd, "@FIRST_NAME"       , sFIRST_NAME         , 100);
				IDbDataParameter parLAST_NAME         = Sql.AddParameter(cmd, "@LAST_NAME"        , sLAST_NAME          , 100);
				IDbDataParameter parPHONE_WORK        = Sql.AddParameter(cmd, "@PHONE_WORK"       , sPHONE_WORK         ,  25);
				IDbDataParameter parEMAIL1            = Sql.AddParameter(cmd, "@EMAIL1"           , sEMAIL1             , 100);
				IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
				IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
				IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
				IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spLEADS_Update
		/// <summary>
		/// spLEADS_Update
		/// </summary>
		public static void spLEADS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sSALUTATION, string sFIRST_NAME, string sLAST_NAME, string sTITLE, string sREFERED_BY, string sLEAD_SOURCE, string sLEAD_SOURCE_DESCRIPTION, string sSTATUS, string sSTATUS_DESCRIPTION, string sDEPARTMENT, Guid gREPORTS_TO_ID, bool bDO_NOT_CALL, string sPHONE_HOME, string sPHONE_MOBILE, string sPHONE_WORK, string sPHONE_OTHER, string sPHONE_FAX, string sEMAIL1, string sEMAIL2, bool bEMAIL_OPT_OUT, bool bINVALID_EMAIL, string sPRIMARY_ADDRESS_STREET, string sPRIMARY_ADDRESS_CITY, string sPRIMARY_ADDRESS_STATE, string sPRIMARY_ADDRESS_POSTALCODE, string sPRIMARY_ADDRESS_COUNTRY, string sALT_ADDRESS_STREET, string sALT_ADDRESS_CITY, string sALT_ADDRESS_STATE, string sALT_ADDRESS_POSTALCODE, string sALT_ADDRESS_COUNTRY, string sDESCRIPTION, string sACCOUNT_NAME, Guid gCAMPAIGN_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gCONTACT_ID, Guid gACCOUNT_ID, bool bEXCHANGE_FOLDER, DateTime dtBIRTHDATE, string sASSISTANT, string sASSISTANT_PHONE, string sWEBSITE, string sSMS_OPT_IN, string sTWITTER_SCREEN_NAME, string sPICTURE, string sTAG_SET_NAME, string sLEAD_NUMBER, string sASSIGNED_SET_LIST, string sDP_BUSINESS_PURPOSE, DateTime dtDP_CONSENT_LAST_UPDATED)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spLEADS_Update";
							IDbDataParameter parID                         = Sql.AddParameter(cmd, "@ID"                        , gID                          );
							IDbDataParameter parMODIFIED_USER_ID           = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"          ,  _security?.USER_ID ?? Guid.Empty            );
							IDbDataParameter parASSIGNED_USER_ID           = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"          , gASSIGNED_USER_ID            );
							IDbDataParameter parSALUTATION                 = Sql.AddParameter(cmd, "@SALUTATION"                , sSALUTATION                  ,  25);
							IDbDataParameter parFIRST_NAME                 = Sql.AddParameter(cmd, "@FIRST_NAME"                , sFIRST_NAME                  , 100);
							IDbDataParameter parLAST_NAME                  = Sql.AddParameter(cmd, "@LAST_NAME"                 , sLAST_NAME                   , 100);
							IDbDataParameter parTITLE                      = Sql.AddParameter(cmd, "@TITLE"                     , sTITLE                       , 100);
							IDbDataParameter parREFERED_BY                 = Sql.AddParameter(cmd, "@REFERED_BY"                , sREFERED_BY                  , 100);
							IDbDataParameter parLEAD_SOURCE                = Sql.AddParameter(cmd, "@LEAD_SOURCE"               , sLEAD_SOURCE                 , 100);
							IDbDataParameter parLEAD_SOURCE_DESCRIPTION    = Sql.AddParameter(cmd, "@LEAD_SOURCE_DESCRIPTION"   , sLEAD_SOURCE_DESCRIPTION     );
							IDbDataParameter parSTATUS                     = Sql.AddParameter(cmd, "@STATUS"                    , sSTATUS                      , 100);
							IDbDataParameter parSTATUS_DESCRIPTION         = Sql.AddParameter(cmd, "@STATUS_DESCRIPTION"        , sSTATUS_DESCRIPTION          );
							IDbDataParameter parDEPARTMENT                 = Sql.AddParameter(cmd, "@DEPARTMENT"                , sDEPARTMENT                  , 100);
							IDbDataParameter parREPORTS_TO_ID              = Sql.AddParameter(cmd, "@REPORTS_TO_ID"             , gREPORTS_TO_ID               );
							IDbDataParameter parDO_NOT_CALL                = Sql.AddParameter(cmd, "@DO_NOT_CALL"               , bDO_NOT_CALL                 );
							IDbDataParameter parPHONE_HOME                 = Sql.AddParameter(cmd, "@PHONE_HOME"                , sPHONE_HOME                  ,  25);
							IDbDataParameter parPHONE_MOBILE               = Sql.AddParameter(cmd, "@PHONE_MOBILE"              , sPHONE_MOBILE                ,  25);
							IDbDataParameter parPHONE_WORK                 = Sql.AddParameter(cmd, "@PHONE_WORK"                , sPHONE_WORK                  ,  25);
							IDbDataParameter parPHONE_OTHER                = Sql.AddParameter(cmd, "@PHONE_OTHER"               , sPHONE_OTHER                 ,  25);
							IDbDataParameter parPHONE_FAX                  = Sql.AddParameter(cmd, "@PHONE_FAX"                 , sPHONE_FAX                   ,  25);
							IDbDataParameter parEMAIL1                     = Sql.AddParameter(cmd, "@EMAIL1"                    , sEMAIL1                      , 100);
							IDbDataParameter parEMAIL2                     = Sql.AddParameter(cmd, "@EMAIL2"                    , sEMAIL2                      , 100);
							IDbDataParameter parEMAIL_OPT_OUT              = Sql.AddParameter(cmd, "@EMAIL_OPT_OUT"             , bEMAIL_OPT_OUT               );
							IDbDataParameter parINVALID_EMAIL              = Sql.AddParameter(cmd, "@INVALID_EMAIL"             , bINVALID_EMAIL               );
							IDbDataParameter parPRIMARY_ADDRESS_STREET     = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_STREET"    , sPRIMARY_ADDRESS_STREET      , 150);
							IDbDataParameter parPRIMARY_ADDRESS_CITY       = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_CITY"      , sPRIMARY_ADDRESS_CITY        , 100);
							IDbDataParameter parPRIMARY_ADDRESS_STATE      = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_STATE"     , sPRIMARY_ADDRESS_STATE       , 100);
							IDbDataParameter parPRIMARY_ADDRESS_POSTALCODE = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_POSTALCODE", sPRIMARY_ADDRESS_POSTALCODE  ,  20);
							IDbDataParameter parPRIMARY_ADDRESS_COUNTRY    = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_COUNTRY"   , sPRIMARY_ADDRESS_COUNTRY     , 100);
							IDbDataParameter parALT_ADDRESS_STREET         = Sql.AddParameter(cmd, "@ALT_ADDRESS_STREET"        , sALT_ADDRESS_STREET          , 150);
							IDbDataParameter parALT_ADDRESS_CITY           = Sql.AddParameter(cmd, "@ALT_ADDRESS_CITY"          , sALT_ADDRESS_CITY            , 100);
							IDbDataParameter parALT_ADDRESS_STATE          = Sql.AddParameter(cmd, "@ALT_ADDRESS_STATE"         , sALT_ADDRESS_STATE           , 100);
							IDbDataParameter parALT_ADDRESS_POSTALCODE     = Sql.AddParameter(cmd, "@ALT_ADDRESS_POSTALCODE"    , sALT_ADDRESS_POSTALCODE      ,  20);
							IDbDataParameter parALT_ADDRESS_COUNTRY        = Sql.AddParameter(cmd, "@ALT_ADDRESS_COUNTRY"       , sALT_ADDRESS_COUNTRY         , 100);
							IDbDataParameter parDESCRIPTION                = Sql.AddParameter(cmd, "@DESCRIPTION"               , sDESCRIPTION                 );
							IDbDataParameter parACCOUNT_NAME               = Sql.AddParameter(cmd, "@ACCOUNT_NAME"              , sACCOUNT_NAME                , 150);
							IDbDataParameter parCAMPAIGN_ID                = Sql.AddParameter(cmd, "@CAMPAIGN_ID"               , gCAMPAIGN_ID                 );
							IDbDataParameter parTEAM_ID                    = Sql.AddParameter(cmd, "@TEAM_ID"                   , gTEAM_ID                     );
							IDbDataParameter parTEAM_SET_LIST              = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"             , sTEAM_SET_LIST               , 8000);
							IDbDataParameter parCONTACT_ID                 = Sql.AddParameter(cmd, "@CONTACT_ID"                , gCONTACT_ID                  );
							IDbDataParameter parACCOUNT_ID                 = Sql.AddParameter(cmd, "@ACCOUNT_ID"                , gACCOUNT_ID                  );
							IDbDataParameter parEXCHANGE_FOLDER            = Sql.AddParameter(cmd, "@EXCHANGE_FOLDER"           , bEXCHANGE_FOLDER             );
							IDbDataParameter parBIRTHDATE                  = Sql.AddParameter(cmd, "@BIRTHDATE"                 , dtBIRTHDATE                  );
							IDbDataParameter parASSISTANT                  = Sql.AddParameter(cmd, "@ASSISTANT"                 , sASSISTANT                   ,  75);
							IDbDataParameter parASSISTANT_PHONE            = Sql.AddParameter(cmd, "@ASSISTANT_PHONE"           , sASSISTANT_PHONE             ,  25);
							IDbDataParameter parWEBSITE                    = Sql.AddParameter(cmd, "@WEBSITE"                   , sWEBSITE                     , 255);
							IDbDataParameter parSMS_OPT_IN                 = Sql.AddParameter(cmd, "@SMS_OPT_IN"                , sSMS_OPT_IN                  ,  25);
							IDbDataParameter parTWITTER_SCREEN_NAME        = Sql.AddParameter(cmd, "@TWITTER_SCREEN_NAME"       , sTWITTER_SCREEN_NAME         ,  20);
							IDbDataParameter parPICTURE                    = Sql.AddParameter(cmd, "@PICTURE"                   , sPICTURE                     );
							IDbDataParameter parTAG_SET_NAME               = Sql.AddParameter(cmd, "@TAG_SET_NAME"              , sTAG_SET_NAME                , 4000);
							IDbDataParameter parLEAD_NUMBER                = Sql.AddParameter(cmd, "@LEAD_NUMBER"               , sLEAD_NUMBER                 ,  30);
							IDbDataParameter parASSIGNED_SET_LIST          = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"         , sASSIGNED_SET_LIST           , 8000);
							IDbDataParameter parDP_BUSINESS_PURPOSE        = Sql.AddParameter(cmd, "@DP_BUSINESS_PURPOSE"       , sDP_BUSINESS_PURPOSE         );
							IDbDataParameter parDP_CONSENT_LAST_UPDATED    = Sql.AddParameter(cmd, "@DP_CONSENT_LAST_UPDATED"   , dtDP_CONSENT_LAST_UPDATED    );
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
		#endregion

		#region spLEADS_Update
		/// <summary>
		/// spLEADS_Update
		/// </summary>
		public static void spLEADS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sSALUTATION, string sFIRST_NAME, string sLAST_NAME, string sTITLE, string sREFERED_BY, string sLEAD_SOURCE, string sLEAD_SOURCE_DESCRIPTION, string sSTATUS, string sSTATUS_DESCRIPTION, string sDEPARTMENT, Guid gREPORTS_TO_ID, bool bDO_NOT_CALL, string sPHONE_HOME, string sPHONE_MOBILE, string sPHONE_WORK, string sPHONE_OTHER, string sPHONE_FAX, string sEMAIL1, string sEMAIL2, bool bEMAIL_OPT_OUT, bool bINVALID_EMAIL, string sPRIMARY_ADDRESS_STREET, string sPRIMARY_ADDRESS_CITY, string sPRIMARY_ADDRESS_STATE, string sPRIMARY_ADDRESS_POSTALCODE, string sPRIMARY_ADDRESS_COUNTRY, string sALT_ADDRESS_STREET, string sALT_ADDRESS_CITY, string sALT_ADDRESS_STATE, string sALT_ADDRESS_POSTALCODE, string sALT_ADDRESS_COUNTRY, string sDESCRIPTION, string sACCOUNT_NAME, Guid gCAMPAIGN_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gCONTACT_ID, Guid gACCOUNT_ID, bool bEXCHANGE_FOLDER, DateTime dtBIRTHDATE, string sASSISTANT, string sASSISTANT_PHONE, string sWEBSITE, string sSMS_OPT_IN, string sTWITTER_SCREEN_NAME, string sPICTURE, string sTAG_SET_NAME, string sLEAD_NUMBER, string sASSIGNED_SET_LIST, string sDP_BUSINESS_PURPOSE, DateTime dtDP_CONSENT_LAST_UPDATED, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spLEADS_Update";
				IDbDataParameter parID                         = Sql.AddParameter(cmd, "@ID"                        , gID                          );
				IDbDataParameter parMODIFIED_USER_ID           = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"          ,  _security?.USER_ID ?? Guid.Empty            );
				IDbDataParameter parASSIGNED_USER_ID           = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"          , gASSIGNED_USER_ID            );
				IDbDataParameter parSALUTATION                 = Sql.AddParameter(cmd, "@SALUTATION"                , sSALUTATION                  ,  25);
				IDbDataParameter parFIRST_NAME                 = Sql.AddParameter(cmd, "@FIRST_NAME"                , sFIRST_NAME                  , 100);
				IDbDataParameter parLAST_NAME                  = Sql.AddParameter(cmd, "@LAST_NAME"                 , sLAST_NAME                   , 100);
				IDbDataParameter parTITLE                      = Sql.AddParameter(cmd, "@TITLE"                     , sTITLE                       , 100);
				IDbDataParameter parREFERED_BY                 = Sql.AddParameter(cmd, "@REFERED_BY"                , sREFERED_BY                  , 100);
				IDbDataParameter parLEAD_SOURCE                = Sql.AddParameter(cmd, "@LEAD_SOURCE"               , sLEAD_SOURCE                 , 100);
				IDbDataParameter parLEAD_SOURCE_DESCRIPTION    = Sql.AddParameter(cmd, "@LEAD_SOURCE_DESCRIPTION"   , sLEAD_SOURCE_DESCRIPTION     );
				IDbDataParameter parSTATUS                     = Sql.AddParameter(cmd, "@STATUS"                    , sSTATUS                      , 100);
				IDbDataParameter parSTATUS_DESCRIPTION         = Sql.AddParameter(cmd, "@STATUS_DESCRIPTION"        , sSTATUS_DESCRIPTION          );
				IDbDataParameter parDEPARTMENT                 = Sql.AddParameter(cmd, "@DEPARTMENT"                , sDEPARTMENT                  , 100);
				IDbDataParameter parREPORTS_TO_ID              = Sql.AddParameter(cmd, "@REPORTS_TO_ID"             , gREPORTS_TO_ID               );
				IDbDataParameter parDO_NOT_CALL                = Sql.AddParameter(cmd, "@DO_NOT_CALL"               , bDO_NOT_CALL                 );
				IDbDataParameter parPHONE_HOME                 = Sql.AddParameter(cmd, "@PHONE_HOME"                , sPHONE_HOME                  ,  25);
				IDbDataParameter parPHONE_MOBILE               = Sql.AddParameter(cmd, "@PHONE_MOBILE"              , sPHONE_MOBILE                ,  25);
				IDbDataParameter parPHONE_WORK                 = Sql.AddParameter(cmd, "@PHONE_WORK"                , sPHONE_WORK                  ,  25);
				IDbDataParameter parPHONE_OTHER                = Sql.AddParameter(cmd, "@PHONE_OTHER"               , sPHONE_OTHER                 ,  25);
				IDbDataParameter parPHONE_FAX                  = Sql.AddParameter(cmd, "@PHONE_FAX"                 , sPHONE_FAX                   ,  25);
				IDbDataParameter parEMAIL1                     = Sql.AddParameter(cmd, "@EMAIL1"                    , sEMAIL1                      , 100);
				IDbDataParameter parEMAIL2                     = Sql.AddParameter(cmd, "@EMAIL2"                    , sEMAIL2                      , 100);
				IDbDataParameter parEMAIL_OPT_OUT              = Sql.AddParameter(cmd, "@EMAIL_OPT_OUT"             , bEMAIL_OPT_OUT               );
				IDbDataParameter parINVALID_EMAIL              = Sql.AddParameter(cmd, "@INVALID_EMAIL"             , bINVALID_EMAIL               );
				IDbDataParameter parPRIMARY_ADDRESS_STREET     = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_STREET"    , sPRIMARY_ADDRESS_STREET      , 150);
				IDbDataParameter parPRIMARY_ADDRESS_CITY       = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_CITY"      , sPRIMARY_ADDRESS_CITY        , 100);
				IDbDataParameter parPRIMARY_ADDRESS_STATE      = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_STATE"     , sPRIMARY_ADDRESS_STATE       , 100);
				IDbDataParameter parPRIMARY_ADDRESS_POSTALCODE = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_POSTALCODE", sPRIMARY_ADDRESS_POSTALCODE  ,  20);
				IDbDataParameter parPRIMARY_ADDRESS_COUNTRY    = Sql.AddParameter(cmd, "@PRIMARY_ADDRESS_COUNTRY"   , sPRIMARY_ADDRESS_COUNTRY     , 100);
				IDbDataParameter parALT_ADDRESS_STREET         = Sql.AddParameter(cmd, "@ALT_ADDRESS_STREET"        , sALT_ADDRESS_STREET          , 150);
				IDbDataParameter parALT_ADDRESS_CITY           = Sql.AddParameter(cmd, "@ALT_ADDRESS_CITY"          , sALT_ADDRESS_CITY            , 100);
				IDbDataParameter parALT_ADDRESS_STATE          = Sql.AddParameter(cmd, "@ALT_ADDRESS_STATE"         , sALT_ADDRESS_STATE           , 100);
				IDbDataParameter parALT_ADDRESS_POSTALCODE     = Sql.AddParameter(cmd, "@ALT_ADDRESS_POSTALCODE"    , sALT_ADDRESS_POSTALCODE      ,  20);
				IDbDataParameter parALT_ADDRESS_COUNTRY        = Sql.AddParameter(cmd, "@ALT_ADDRESS_COUNTRY"       , sALT_ADDRESS_COUNTRY         , 100);
				IDbDataParameter parDESCRIPTION                = Sql.AddParameter(cmd, "@DESCRIPTION"               , sDESCRIPTION                 );
				IDbDataParameter parACCOUNT_NAME               = Sql.AddParameter(cmd, "@ACCOUNT_NAME"              , sACCOUNT_NAME                , 150);
				IDbDataParameter parCAMPAIGN_ID                = Sql.AddParameter(cmd, "@CAMPAIGN_ID"               , gCAMPAIGN_ID                 );
				IDbDataParameter parTEAM_ID                    = Sql.AddParameter(cmd, "@TEAM_ID"                   , gTEAM_ID                     );
				IDbDataParameter parTEAM_SET_LIST              = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"             , sTEAM_SET_LIST               , 8000);
				IDbDataParameter parCONTACT_ID                 = Sql.AddParameter(cmd, "@CONTACT_ID"                , gCONTACT_ID                  );
				IDbDataParameter parACCOUNT_ID                 = Sql.AddParameter(cmd, "@ACCOUNT_ID"                , gACCOUNT_ID                  );
				IDbDataParameter parEXCHANGE_FOLDER            = Sql.AddParameter(cmd, "@EXCHANGE_FOLDER"           , bEXCHANGE_FOLDER             );
				IDbDataParameter parBIRTHDATE                  = Sql.AddParameter(cmd, "@BIRTHDATE"                 , dtBIRTHDATE                  );
				IDbDataParameter parASSISTANT                  = Sql.AddParameter(cmd, "@ASSISTANT"                 , sASSISTANT                   ,  75);
				IDbDataParameter parASSISTANT_PHONE            = Sql.AddParameter(cmd, "@ASSISTANT_PHONE"           , sASSISTANT_PHONE             ,  25);
				IDbDataParameter parWEBSITE                    = Sql.AddParameter(cmd, "@WEBSITE"                   , sWEBSITE                     , 255);
				IDbDataParameter parSMS_OPT_IN                 = Sql.AddParameter(cmd, "@SMS_OPT_IN"                , sSMS_OPT_IN                  ,  25);
				IDbDataParameter parTWITTER_SCREEN_NAME        = Sql.AddParameter(cmd, "@TWITTER_SCREEN_NAME"       , sTWITTER_SCREEN_NAME         ,  20);
				IDbDataParameter parPICTURE                    = Sql.AddParameter(cmd, "@PICTURE"                   , sPICTURE                     );
				IDbDataParameter parTAG_SET_NAME               = Sql.AddParameter(cmd, "@TAG_SET_NAME"              , sTAG_SET_NAME                , 4000);
				IDbDataParameter parLEAD_NUMBER                = Sql.AddParameter(cmd, "@LEAD_NUMBER"               , sLEAD_NUMBER                 ,  30);
				IDbDataParameter parASSIGNED_SET_LIST          = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"         , sASSIGNED_SET_LIST           , 8000);
				IDbDataParameter parDP_BUSINESS_PURPOSE        = Sql.AddParameter(cmd, "@DP_BUSINESS_PURPOSE"       , sDP_BUSINESS_PURPOSE         );
				IDbDataParameter parDP_CONSENT_LAST_UPDATED    = Sql.AddParameter(cmd, "@DP_CONSENT_LAST_UPDATED"   , dtDP_CONSENT_LAST_UPDATED    );
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spMEETINGS_CONTACTS_Update
		/// <summary>
		/// spMEETINGS_CONTACTS_Update
		/// </summary>
		public static void spMEETINGS_CONTACTS_Update(Guid gMEETING_ID, Guid gCONTACT_ID, bool bREQUIRED, string sACCEPT_STATUS)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spMEETINGS_CONTACTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parMEETING_ID       = Sql.AddParameter(cmd, "@MEETING_ID"      , gMEETING_ID        );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							IDbDataParameter parREQUIRED         = Sql.AddParameter(cmd, "@REQUIRED"        , bREQUIRED          );
							IDbDataParameter parACCEPT_STATUS    = Sql.AddParameter(cmd, "@ACCEPT_STATUS"   , sACCEPT_STATUS     ,  25);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spMEETINGS_CONTACTS_Update
		/// <summary>
		/// spMEETINGS_CONTACTS_Update
		/// </summary>
		public static void spMEETINGS_CONTACTS_Update(Guid gMEETING_ID, Guid gCONTACT_ID, bool bREQUIRED, string sACCEPT_STATUS, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spMEETINGS_CONTACTS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parMEETING_ID       = Sql.AddParameter(cmd, "@MEETING_ID"      , gMEETING_ID        );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				IDbDataParameter parREQUIRED         = Sql.AddParameter(cmd, "@REQUIRED"        , bREQUIRED          );
				IDbDataParameter parACCEPT_STATUS    = Sql.AddParameter(cmd, "@ACCEPT_STATUS"   , sACCEPT_STATUS     ,  25);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spMEETINGS_EmailReminderSent
		/// <summary>
		/// spMEETINGS_EmailReminderSent
		/// </summary>
		public static void spMEETINGS_EmailReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spMEETINGS_EmailReminderSent";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE      ,  25);
							IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID        );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spMEETINGS_EmailReminderSent
		/// <summary>
		/// spMEETINGS_EmailReminderSent
		/// </summary>
		public static void spMEETINGS_EmailReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spMEETINGS_EmailReminderSent";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE      ,  25);
				IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID        );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spMEETINGS_SmsReminderSent
		/// <summary>
		/// spMEETINGS_SmsReminderSent
		/// </summary>
		public static void spMEETINGS_SmsReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spMEETINGS_SmsReminderSent";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE      ,  25);
							IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID        );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spMEETINGS_SmsReminderSent
		/// <summary>
		/// spMEETINGS_SmsReminderSent
		/// </summary>
		public static void spMEETINGS_SmsReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spMEETINGS_SmsReminderSent";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE      ,  25);
				IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID        );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spMEETINGS_Update
		/// <summary>
		/// spMEETINGS_Update
		/// </summary>
		public static void spMEETINGS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, string sLOCATION, Int32 nDURATION_HOURS, Int32 nDURATION_MINUTES, DateTime dtDATE_TIME, string sSTATUS, string sPARENT_TYPE, Guid gPARENT_ID, Int32 nREMINDER_TIME, string sDESCRIPTION, string sINVITEE_LIST, Guid gTEAM_ID, string sTEAM_SET_LIST, Int32 nEMAIL_REMINDER_TIME, bool bALL_DAY_EVENT, string sREPEAT_TYPE, Int32 nREPEAT_INTERVAL, string sREPEAT_DOW, DateTime dtREPEAT_UNTIL, Int32 nREPEAT_COUNT, Int32 nSMS_REMINDER_TIME, string sTAG_SET_NAME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spMEETINGS_Update";
							IDbDataParameter parID                  = Sql.AddParameter(cmd, "@ID"                 , gID                   );
							IDbDataParameter parMODIFIED_USER_ID    = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"   ,  _security?.USER_ID ?? Guid.Empty     );
							IDbDataParameter parASSIGNED_USER_ID    = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"   , gASSIGNED_USER_ID     );
							IDbDataParameter parNAME                = Sql.AddParameter(cmd, "@NAME"               , sNAME                 , 150);
							IDbDataParameter parLOCATION            = Sql.AddParameter(cmd, "@LOCATION"           , sLOCATION             ,  50);
							IDbDataParameter parDURATION_HOURS      = Sql.AddParameter(cmd, "@DURATION_HOURS"     , nDURATION_HOURS       );
							IDbDataParameter parDURATION_MINUTES    = Sql.AddParameter(cmd, "@DURATION_MINUTES"   , nDURATION_MINUTES     );
							IDbDataParameter parDATE_TIME           = Sql.AddParameter(cmd, "@DATE_TIME"          , dtDATE_TIME           );
							IDbDataParameter parSTATUS              = Sql.AddParameter(cmd, "@STATUS"             , sSTATUS               ,  25);
							IDbDataParameter parPARENT_TYPE         = Sql.AddParameter(cmd, "@PARENT_TYPE"        , sPARENT_TYPE          ,  25);
							IDbDataParameter parPARENT_ID           = Sql.AddParameter(cmd, "@PARENT_ID"          , gPARENT_ID            );
							IDbDataParameter parREMINDER_TIME       = Sql.AddParameter(cmd, "@REMINDER_TIME"      , nREMINDER_TIME        );
							IDbDataParameter parDESCRIPTION         = Sql.AddParameter(cmd, "@DESCRIPTION"        , sDESCRIPTION          );
							IDbDataParameter parINVITEE_LIST        = Sql.AddAnsiParam(cmd, "@INVITEE_LIST"       , sINVITEE_LIST         , 8000);
							IDbDataParameter parTEAM_ID             = Sql.AddParameter(cmd, "@TEAM_ID"            , gTEAM_ID              );
							IDbDataParameter parTEAM_SET_LIST       = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"      , sTEAM_SET_LIST        , 8000);
							IDbDataParameter parEMAIL_REMINDER_TIME = Sql.AddParameter(cmd, "@EMAIL_REMINDER_TIME", nEMAIL_REMINDER_TIME  );
							IDbDataParameter parALL_DAY_EVENT       = Sql.AddParameter(cmd, "@ALL_DAY_EVENT"      , bALL_DAY_EVENT        );
							IDbDataParameter parREPEAT_TYPE         = Sql.AddParameter(cmd, "@REPEAT_TYPE"        , sREPEAT_TYPE          ,  25);
							IDbDataParameter parREPEAT_INTERVAL     = Sql.AddParameter(cmd, "@REPEAT_INTERVAL"    , nREPEAT_INTERVAL      );
							IDbDataParameter parREPEAT_DOW          = Sql.AddParameter(cmd, "@REPEAT_DOW"         , sREPEAT_DOW           ,   7);
							IDbDataParameter parREPEAT_UNTIL        = Sql.AddParameter(cmd, "@REPEAT_UNTIL"       , dtREPEAT_UNTIL        );
							IDbDataParameter parREPEAT_COUNT        = Sql.AddParameter(cmd, "@REPEAT_COUNT"       , nREPEAT_COUNT         );
							IDbDataParameter parSMS_REMINDER_TIME   = Sql.AddParameter(cmd, "@SMS_REMINDER_TIME"  , nSMS_REMINDER_TIME    );
							IDbDataParameter parTAG_SET_NAME        = Sql.AddParameter(cmd, "@TAG_SET_NAME"       , sTAG_SET_NAME         , 4000);
							IDbDataParameter parIS_PRIVATE          = Sql.AddParameter(cmd, "@IS_PRIVATE"         , bIS_PRIVATE           );
							IDbDataParameter parASSIGNED_SET_LIST   = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"  , sASSIGNED_SET_LIST    , 8000);
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
		#endregion

		#region spMEETINGS_Update
		/// <summary>
		/// spMEETINGS_Update
		/// </summary>
		public static void spMEETINGS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, string sLOCATION, Int32 nDURATION_HOURS, Int32 nDURATION_MINUTES, DateTime dtDATE_TIME, string sSTATUS, string sPARENT_TYPE, Guid gPARENT_ID, Int32 nREMINDER_TIME, string sDESCRIPTION, string sINVITEE_LIST, Guid gTEAM_ID, string sTEAM_SET_LIST, Int32 nEMAIL_REMINDER_TIME, bool bALL_DAY_EVENT, string sREPEAT_TYPE, Int32 nREPEAT_INTERVAL, string sREPEAT_DOW, DateTime dtREPEAT_UNTIL, Int32 nREPEAT_COUNT, Int32 nSMS_REMINDER_TIME, string sTAG_SET_NAME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spMEETINGS_Update";
				IDbDataParameter parID                  = Sql.AddParameter(cmd, "@ID"                 , gID                   );
				IDbDataParameter parMODIFIED_USER_ID    = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"   ,  _security?.USER_ID ?? Guid.Empty     );
				IDbDataParameter parASSIGNED_USER_ID    = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"   , gASSIGNED_USER_ID     );
				IDbDataParameter parNAME                = Sql.AddParameter(cmd, "@NAME"               , sNAME                 , 150);
				IDbDataParameter parLOCATION            = Sql.AddParameter(cmd, "@LOCATION"           , sLOCATION             ,  50);
				IDbDataParameter parDURATION_HOURS      = Sql.AddParameter(cmd, "@DURATION_HOURS"     , nDURATION_HOURS       );
				IDbDataParameter parDURATION_MINUTES    = Sql.AddParameter(cmd, "@DURATION_MINUTES"   , nDURATION_MINUTES     );
				IDbDataParameter parDATE_TIME           = Sql.AddParameter(cmd, "@DATE_TIME"          , dtDATE_TIME           );
				IDbDataParameter parSTATUS              = Sql.AddParameter(cmd, "@STATUS"             , sSTATUS               ,  25);
				IDbDataParameter parPARENT_TYPE         = Sql.AddParameter(cmd, "@PARENT_TYPE"        , sPARENT_TYPE          ,  25);
				IDbDataParameter parPARENT_ID           = Sql.AddParameter(cmd, "@PARENT_ID"          , gPARENT_ID            );
				IDbDataParameter parREMINDER_TIME       = Sql.AddParameter(cmd, "@REMINDER_TIME"      , nREMINDER_TIME        );
				IDbDataParameter parDESCRIPTION         = Sql.AddParameter(cmd, "@DESCRIPTION"        , sDESCRIPTION          );
				IDbDataParameter parINVITEE_LIST        = Sql.AddAnsiParam(cmd, "@INVITEE_LIST"       , sINVITEE_LIST         , 8000);
				IDbDataParameter parTEAM_ID             = Sql.AddParameter(cmd, "@TEAM_ID"            , gTEAM_ID              );
				IDbDataParameter parTEAM_SET_LIST       = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"      , sTEAM_SET_LIST        , 8000);
				IDbDataParameter parEMAIL_REMINDER_TIME = Sql.AddParameter(cmd, "@EMAIL_REMINDER_TIME", nEMAIL_REMINDER_TIME  );
				IDbDataParameter parALL_DAY_EVENT       = Sql.AddParameter(cmd, "@ALL_DAY_EVENT"      , bALL_DAY_EVENT        );
				IDbDataParameter parREPEAT_TYPE         = Sql.AddParameter(cmd, "@REPEAT_TYPE"        , sREPEAT_TYPE          ,  25);
				IDbDataParameter parREPEAT_INTERVAL     = Sql.AddParameter(cmd, "@REPEAT_INTERVAL"    , nREPEAT_INTERVAL      );
				IDbDataParameter parREPEAT_DOW          = Sql.AddParameter(cmd, "@REPEAT_DOW"         , sREPEAT_DOW           ,   7);
				IDbDataParameter parREPEAT_UNTIL        = Sql.AddParameter(cmd, "@REPEAT_UNTIL"       , dtREPEAT_UNTIL        );
				IDbDataParameter parREPEAT_COUNT        = Sql.AddParameter(cmd, "@REPEAT_COUNT"       , nREPEAT_COUNT         );
				IDbDataParameter parSMS_REMINDER_TIME   = Sql.AddParameter(cmd, "@SMS_REMINDER_TIME"  , nSMS_REMINDER_TIME    );
				IDbDataParameter parTAG_SET_NAME        = Sql.AddParameter(cmd, "@TAG_SET_NAME"       , sTAG_SET_NAME         , 4000);
				IDbDataParameter parIS_PRIVATE          = Sql.AddParameter(cmd, "@IS_PRIVATE"         , bIS_PRIVATE           );
				IDbDataParameter parASSIGNED_SET_LIST   = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"  , sASSIGNED_SET_LIST    , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spMODULES_ARCHIVE_LOG_InsertRule
		/// <summary>
		/// spMODULES_ARCHIVE_LOG_InsertRule
		/// </summary>
		public static void spMODULES_ARCHIVE_LOG_InsertRule(Guid gARCHIVE_RULE_ID, string sMODULE_NAME, string sTABLE_NAME)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							if ( Sql.IsOracle(cmd) )
								cmd.CommandText = "spMODULES_ARCHIVE_LOG_InsertRu";
							else
								cmd.CommandText = "spMODULES_ARCHIVE_LOG_InsertRule";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parARCHIVE_RULE_ID  = Sql.AddParameter(cmd, "@ARCHIVE_RULE_ID" , gARCHIVE_RULE_ID   );
							IDbDataParameter parMODULE_NAME      = Sql.AddParameter(cmd, "@MODULE_NAME"     , sMODULE_NAME       ,  25);
							IDbDataParameter parTABLE_NAME       = Sql.AddParameter(cmd, "@TABLE_NAME"      , sTABLE_NAME        ,  50);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spMODULES_ARCHIVE_LOG_InsertRule
		/// <summary>
		/// spMODULES_ARCHIVE_LOG_InsertRule
		/// </summary>
		public static void spMODULES_ARCHIVE_LOG_InsertRule(Guid gARCHIVE_RULE_ID, string sMODULE_NAME, string sTABLE_NAME, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				if ( Sql.IsOracle(cmd) )
					cmd.CommandText = "spMODULES_ARCHIVE_LOG_InsertRu";
				else
					cmd.CommandText = "spMODULES_ARCHIVE_LOG_InsertRule";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parARCHIVE_RULE_ID  = Sql.AddParameter(cmd, "@ARCHIVE_RULE_ID" , gARCHIVE_RULE_ID   );
				IDbDataParameter parMODULE_NAME      = Sql.AddParameter(cmd, "@MODULE_NAME"     , sMODULE_NAME       ,  25);
				IDbDataParameter parTABLE_NAME       = Sql.AddParameter(cmd, "@TABLE_NAME"      , sTABLE_NAME        ,  50);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spNOTE_ATTACHMENTS_Insert
		/// <summary>
		/// spNOTE_ATTACHMENTS_Insert
		/// </summary>
		public static void spNOTE_ATTACHMENTS_Insert(ref Guid gID, Guid gNOTE_ID, string sDESCRIPTION, string sFILENAME, string sFILE_EXT, string sFILE_MIME_TYPE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spNOTE_ATTACHMENTS_Insert";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parNOTE_ID          = Sql.AddParameter(cmd, "@NOTE_ID"         , gNOTE_ID           );
							IDbDataParameter parDESCRIPTION      = Sql.AddParameter(cmd, "@DESCRIPTION"     , sDESCRIPTION       , 255);
							IDbDataParameter parFILENAME         = Sql.AddParameter(cmd, "@FILENAME"        , sFILENAME          , 255);
							IDbDataParameter parFILE_EXT         = Sql.AddParameter(cmd, "@FILE_EXT"        , sFILE_EXT          ,  25);
							IDbDataParameter parFILE_MIME_TYPE   = Sql.AddParameter(cmd, "@FILE_MIME_TYPE"  , sFILE_MIME_TYPE    , 100);
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
		#endregion

		#region spNOTE_ATTACHMENTS_Insert
		/// <summary>
		/// spNOTE_ATTACHMENTS_Insert
		/// </summary>
		public static void spNOTE_ATTACHMENTS_Insert(ref Guid gID, Guid gNOTE_ID, string sDESCRIPTION, string sFILENAME, string sFILE_EXT, string sFILE_MIME_TYPE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spNOTE_ATTACHMENTS_Insert";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parNOTE_ID          = Sql.AddParameter(cmd, "@NOTE_ID"         , gNOTE_ID           );
				IDbDataParameter parDESCRIPTION      = Sql.AddParameter(cmd, "@DESCRIPTION"     , sDESCRIPTION       , 255);
				IDbDataParameter parFILENAME         = Sql.AddParameter(cmd, "@FILENAME"        , sFILENAME          , 255);
				IDbDataParameter parFILE_EXT         = Sql.AddParameter(cmd, "@FILE_EXT"        , sFILE_EXT          ,  25);
				IDbDataParameter parFILE_MIME_TYPE   = Sql.AddParameter(cmd, "@FILE_MIME_TYPE"  , sFILE_MIME_TYPE    , 100);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spNOTES_LinkAttachment
		/// <summary>
		/// spNOTES_LinkAttachment
		/// </summary>
		public static void spNOTES_LinkAttachment(ref Guid gID, string sNAME, string sPARENT_TYPE, Guid gPARENT_ID, string sDESCRIPTION, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, Guid gTEAM_SET_ID, Guid gNOTE_ATTACHMENT_ID, Guid gASSIGNED_SET_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spNOTES_LinkAttachment";
							IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
							IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
							IDbDataParameter parNAME               = Sql.AddParameter(cmd, "@NAME"              , sNAME                , 255);
							IDbDataParameter parPARENT_TYPE        = Sql.AddParameter(cmd, "@PARENT_TYPE"       , sPARENT_TYPE         ,  25);
							IDbDataParameter parPARENT_ID          = Sql.AddParameter(cmd, "@PARENT_ID"         , gPARENT_ID           );
							IDbDataParameter parDESCRIPTION        = Sql.AddParameter(cmd, "@DESCRIPTION"       , sDESCRIPTION         );
							IDbDataParameter parASSIGNED_USER_ID   = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"  , gASSIGNED_USER_ID    );
							IDbDataParameter parTEAM_ID            = Sql.AddParameter(cmd, "@TEAM_ID"           , gTEAM_ID             );
							IDbDataParameter parTEAM_SET_ID        = Sql.AddParameter(cmd, "@TEAM_SET_ID"       , gTEAM_SET_ID         );
							IDbDataParameter parNOTE_ATTACHMENT_ID = Sql.AddParameter(cmd, "@NOTE_ATTACHMENT_ID", gNOTE_ATTACHMENT_ID  );
							IDbDataParameter parASSIGNED_SET_ID    = Sql.AddParameter(cmd, "@ASSIGNED_SET_ID"   , gASSIGNED_SET_ID     );
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
		#endregion

		#region spNOTES_LinkAttachment
		/// <summary>
		/// spNOTES_LinkAttachment
		/// </summary>
		public static void spNOTES_LinkAttachment(ref Guid gID, string sNAME, string sPARENT_TYPE, Guid gPARENT_ID, string sDESCRIPTION, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, Guid gTEAM_SET_ID, Guid gNOTE_ATTACHMENT_ID, Guid gASSIGNED_SET_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spNOTES_LinkAttachment";
				IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
				IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
				IDbDataParameter parNAME               = Sql.AddParameter(cmd, "@NAME"              , sNAME                , 255);
				IDbDataParameter parPARENT_TYPE        = Sql.AddParameter(cmd, "@PARENT_TYPE"       , sPARENT_TYPE         ,  25);
				IDbDataParameter parPARENT_ID          = Sql.AddParameter(cmd, "@PARENT_ID"         , gPARENT_ID           );
				IDbDataParameter parDESCRIPTION        = Sql.AddParameter(cmd, "@DESCRIPTION"       , sDESCRIPTION         );
				IDbDataParameter parASSIGNED_USER_ID   = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"  , gASSIGNED_USER_ID    );
				IDbDataParameter parTEAM_ID            = Sql.AddParameter(cmd, "@TEAM_ID"           , gTEAM_ID             );
				IDbDataParameter parTEAM_SET_ID        = Sql.AddParameter(cmd, "@TEAM_SET_ID"       , gTEAM_SET_ID         );
				IDbDataParameter parNOTE_ATTACHMENT_ID = Sql.AddParameter(cmd, "@NOTE_ATTACHMENT_ID", gNOTE_ATTACHMENT_ID  );
				IDbDataParameter parASSIGNED_SET_ID    = Sql.AddParameter(cmd, "@ASSIGNED_SET_ID"   , gASSIGNED_SET_ID     );
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spNOTES_Update
		/// <summary>
		/// spNOTES_Update
		/// </summary>
		public static void spNOTES_Update(ref Guid gID, string sNAME, string sPARENT_TYPE, Guid gPARENT_ID, Guid gCONTACT_ID, string sDESCRIPTION, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gASSIGNED_USER_ID, string sTAG_SET_NAME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spNOTES_Update";
							IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
							IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
							IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sNAME               , 255);
							IDbDataParameter parPARENT_TYPE       = Sql.AddParameter(cmd, "@PARENT_TYPE"      , sPARENT_TYPE        ,  25);
							IDbDataParameter parPARENT_ID         = Sql.AddParameter(cmd, "@PARENT_ID"        , gPARENT_ID          );
							IDbDataParameter parCONTACT_ID        = Sql.AddParameter(cmd, "@CONTACT_ID"       , gCONTACT_ID         );
							IDbDataParameter parDESCRIPTION       = Sql.AddParameter(cmd, "@DESCRIPTION"      , sDESCRIPTION        );
							IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
							IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
							IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
							IDbDataParameter parTAG_SET_NAME      = Sql.AddParameter(cmd, "@TAG_SET_NAME"     , sTAG_SET_NAME       , 4000);
							IDbDataParameter parIS_PRIVATE        = Sql.AddParameter(cmd, "@IS_PRIVATE"       , bIS_PRIVATE         );
							IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
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
		#endregion

		#region spNOTES_Update
		/// <summary>
		/// spNOTES_Update
		/// </summary>
		public static void spNOTES_Update(ref Guid gID, string sNAME, string sPARENT_TYPE, Guid gPARENT_ID, Guid gCONTACT_ID, string sDESCRIPTION, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gASSIGNED_USER_ID, string sTAG_SET_NAME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spNOTES_Update";
				IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
				IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
				IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sNAME               , 255);
				IDbDataParameter parPARENT_TYPE       = Sql.AddParameter(cmd, "@PARENT_TYPE"      , sPARENT_TYPE        ,  25);
				IDbDataParameter parPARENT_ID         = Sql.AddParameter(cmd, "@PARENT_ID"        , gPARENT_ID          );
				IDbDataParameter parCONTACT_ID        = Sql.AddParameter(cmd, "@CONTACT_ID"       , gCONTACT_ID         );
				IDbDataParameter parDESCRIPTION       = Sql.AddParameter(cmd, "@DESCRIPTION"      , sDESCRIPTION        );
				IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
				IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
				IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
				IDbDataParameter parTAG_SET_NAME      = Sql.AddParameter(cmd, "@TAG_SET_NAME"     , sTAG_SET_NAME       , 4000);
				IDbDataParameter parIS_PRIVATE        = Sql.AddParameter(cmd, "@IS_PRIVATE"       , bIS_PRIVATE         );
				IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spOPPORTUNITIES_CONTACTS_Update
		/// <summary>
		/// spOPPORTUNITIES_CONTACTS_Update
		/// </summary>
		public static void spOPPORTUNITIES_CONTACTS_Update(Guid gOPPORTUNITY_ID, Guid gCONTACT_ID, string sCONTACT_ROLE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							if ( Sql.IsOracle(cmd) )
								cmd.CommandText = "spOPPORTUNITIES_CONTACTS_Updat";
							else
								cmd.CommandText = "spOPPORTUNITIES_CONTACTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parOPPORTUNITY_ID   = Sql.AddParameter(cmd, "@OPPORTUNITY_ID"  , gOPPORTUNITY_ID    );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							IDbDataParameter parCONTACT_ROLE     = Sql.AddParameter(cmd, "@CONTACT_ROLE"    , sCONTACT_ROLE      ,  50);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spOPPORTUNITIES_CONTACTS_Update
		/// <summary>
		/// spOPPORTUNITIES_CONTACTS_Update
		/// </summary>
		public static void spOPPORTUNITIES_CONTACTS_Update(Guid gOPPORTUNITY_ID, Guid gCONTACT_ID, string sCONTACT_ROLE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				if ( Sql.IsOracle(cmd) )
					cmd.CommandText = "spOPPORTUNITIES_CONTACTS_Updat";
				else
					cmd.CommandText = "spOPPORTUNITIES_CONTACTS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parOPPORTUNITY_ID   = Sql.AddParameter(cmd, "@OPPORTUNITY_ID"  , gOPPORTUNITY_ID    );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				IDbDataParameter parCONTACT_ROLE     = Sql.AddParameter(cmd, "@CONTACT_ROLE"    , sCONTACT_ROLE      ,  50);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spOPPORTUNITIES_New
		/// <summary>
		/// spOPPORTUNITIES_New
		/// </summary>
		public static void spOPPORTUNITIES_New(ref Guid gID, Guid gACCOUNT_ID, string sNAME, decimal dAMOUNT, Guid gCURRENCY_ID, DateTime dtDATE_CLOSED, string sSALES_STAGE, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gB2C_CONTACT_ID, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spOPPORTUNITIES_New";
							IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
							IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
							IDbDataParameter parACCOUNT_ID        = Sql.AddParameter(cmd, "@ACCOUNT_ID"       , gACCOUNT_ID         );
							IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sNAME               , 150);
							IDbDataParameter parAMOUNT            = Sql.AddParameter(cmd, "@AMOUNT"           , dAMOUNT             );
							IDbDataParameter parCURRENCY_ID       = Sql.AddParameter(cmd, "@CURRENCY_ID"      , gCURRENCY_ID        );
							IDbDataParameter parDATE_CLOSED       = Sql.AddParameter(cmd, "@DATE_CLOSED"      , dtDATE_CLOSED       );
							IDbDataParameter parSALES_STAGE       = Sql.AddParameter(cmd, "@SALES_STAGE"      , sSALES_STAGE        ,  25);
							IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
							IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
							IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
							IDbDataParameter parB2C_CONTACT_ID    = Sql.AddParameter(cmd, "@B2C_CONTACT_ID"   , gB2C_CONTACT_ID     );
							IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
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
		#endregion

		#region spOPPORTUNITIES_New
		/// <summary>
		/// spOPPORTUNITIES_New
		/// </summary>
		public static void spOPPORTUNITIES_New(ref Guid gID, Guid gACCOUNT_ID, string sNAME, decimal dAMOUNT, Guid gCURRENCY_ID, DateTime dtDATE_CLOSED, string sSALES_STAGE, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gB2C_CONTACT_ID, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spOPPORTUNITIES_New";
				IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , gID                 );
				IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" ,  _security?.USER_ID ?? Guid.Empty   );
				IDbDataParameter parACCOUNT_ID        = Sql.AddParameter(cmd, "@ACCOUNT_ID"       , gACCOUNT_ID         );
				IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sNAME               , 150);
				IDbDataParameter parAMOUNT            = Sql.AddParameter(cmd, "@AMOUNT"           , dAMOUNT             );
				IDbDataParameter parCURRENCY_ID       = Sql.AddParameter(cmd, "@CURRENCY_ID"      , gCURRENCY_ID        );
				IDbDataParameter parDATE_CLOSED       = Sql.AddParameter(cmd, "@DATE_CLOSED"      , dtDATE_CLOSED       );
				IDbDataParameter parSALES_STAGE       = Sql.AddParameter(cmd, "@SALES_STAGE"      , sSALES_STAGE        ,  25);
				IDbDataParameter parASSIGNED_USER_ID  = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID" , gASSIGNED_USER_ID   );
				IDbDataParameter parTEAM_ID           = Sql.AddParameter(cmd, "@TEAM_ID"          , gTEAM_ID            );
				IDbDataParameter parTEAM_SET_LIST     = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"    , sTEAM_SET_LIST      , 8000);
				IDbDataParameter parB2C_CONTACT_ID    = Sql.AddParameter(cmd, "@B2C_CONTACT_ID"   , gB2C_CONTACT_ID     );
				IDbDataParameter parASSIGNED_SET_LIST = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST", sASSIGNED_SET_LIST  , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spOPPORTUNITIES_Update
		/// <summary>
		/// spOPPORTUNITIES_Update
		/// </summary>
		public static void spOPPORTUNITIES_Update(ref Guid gID, Guid gASSIGNED_USER_ID, Guid gACCOUNT_ID, string sNAME, string sOPPORTUNITY_TYPE, string sLEAD_SOURCE, decimal dAMOUNT, Guid gCURRENCY_ID, DateTime dtDATE_CLOSED, string sNEXT_STEP, string sSALES_STAGE, float flPROBABILITY, string sDESCRIPTION, string sPARENT_TYPE, Guid gPARENT_ID, string sACCOUNT_NAME, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gCAMPAIGN_ID, bool bEXCHANGE_FOLDER, Guid gB2C_CONTACT_ID, Guid gLEAD_ID, string sTAG_SET_NAME, string sOPPORTUNITY_NUMBER, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spOPPORTUNITIES_Update";
							IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
							IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
							IDbDataParameter parASSIGNED_USER_ID   = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"  , gASSIGNED_USER_ID    );
							IDbDataParameter parACCOUNT_ID         = Sql.AddParameter(cmd, "@ACCOUNT_ID"        , gACCOUNT_ID          );
							IDbDataParameter parNAME               = Sql.AddParameter(cmd, "@NAME"              , sNAME                , 150);
							IDbDataParameter parOPPORTUNITY_TYPE   = Sql.AddParameter(cmd, "@OPPORTUNITY_TYPE"  , sOPPORTUNITY_TYPE    , 255);
							IDbDataParameter parLEAD_SOURCE        = Sql.AddParameter(cmd, "@LEAD_SOURCE"       , sLEAD_SOURCE         ,  50);
							IDbDataParameter parAMOUNT             = Sql.AddParameter(cmd, "@AMOUNT"            , dAMOUNT              );
							IDbDataParameter parCURRENCY_ID        = Sql.AddParameter(cmd, "@CURRENCY_ID"       , gCURRENCY_ID         );
							IDbDataParameter parDATE_CLOSED        = Sql.AddParameter(cmd, "@DATE_CLOSED"       , dtDATE_CLOSED        );
							IDbDataParameter parNEXT_STEP          = Sql.AddParameter(cmd, "@NEXT_STEP"         , sNEXT_STEP           , 100);
							IDbDataParameter parSALES_STAGE        = Sql.AddParameter(cmd, "@SALES_STAGE"       , sSALES_STAGE         ,  25);
							IDbDataParameter parPROBABILITY        = Sql.AddParameter(cmd, "@PROBABILITY"       , flPROBABILITY        );
							IDbDataParameter parDESCRIPTION        = Sql.AddParameter(cmd, "@DESCRIPTION"       , sDESCRIPTION         );
							IDbDataParameter parPARENT_TYPE        = Sql.AddParameter(cmd, "@PARENT_TYPE"       , sPARENT_TYPE         ,  25);
							IDbDataParameter parPARENT_ID          = Sql.AddParameter(cmd, "@PARENT_ID"         , gPARENT_ID           );
							IDbDataParameter parACCOUNT_NAME       = Sql.AddParameter(cmd, "@ACCOUNT_NAME"      , sACCOUNT_NAME        , 100);
							IDbDataParameter parTEAM_ID            = Sql.AddParameter(cmd, "@TEAM_ID"           , gTEAM_ID             );
							IDbDataParameter parTEAM_SET_LIST      = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"     , sTEAM_SET_LIST       , 8000);
							IDbDataParameter parCAMPAIGN_ID        = Sql.AddParameter(cmd, "@CAMPAIGN_ID"       , gCAMPAIGN_ID         );
							IDbDataParameter parEXCHANGE_FOLDER    = Sql.AddParameter(cmd, "@EXCHANGE_FOLDER"   , bEXCHANGE_FOLDER     );
							IDbDataParameter parB2C_CONTACT_ID     = Sql.AddParameter(cmd, "@B2C_CONTACT_ID"    , gB2C_CONTACT_ID      );
							IDbDataParameter parLEAD_ID            = Sql.AddParameter(cmd, "@LEAD_ID"           , gLEAD_ID             );
							IDbDataParameter parTAG_SET_NAME       = Sql.AddParameter(cmd, "@TAG_SET_NAME"      , sTAG_SET_NAME        , 4000);
							IDbDataParameter parOPPORTUNITY_NUMBER = Sql.AddParameter(cmd, "@OPPORTUNITY_NUMBER", sOPPORTUNITY_NUMBER  ,  30);
							IDbDataParameter parASSIGNED_SET_LIST  = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST" , sASSIGNED_SET_LIST   , 8000);
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
		#endregion

		#region spOPPORTUNITIES_Update
		/// <summary>
		/// spOPPORTUNITIES_Update
		/// </summary>
		public static void spOPPORTUNITIES_Update(ref Guid gID, Guid gASSIGNED_USER_ID, Guid gACCOUNT_ID, string sNAME, string sOPPORTUNITY_TYPE, string sLEAD_SOURCE, decimal dAMOUNT, Guid gCURRENCY_ID, DateTime dtDATE_CLOSED, string sNEXT_STEP, string sSALES_STAGE, float flPROBABILITY, string sDESCRIPTION, string sPARENT_TYPE, Guid gPARENT_ID, string sACCOUNT_NAME, Guid gTEAM_ID, string sTEAM_SET_LIST, Guid gCAMPAIGN_ID, bool bEXCHANGE_FOLDER, Guid gB2C_CONTACT_ID, Guid gLEAD_ID, string sTAG_SET_NAME, string sOPPORTUNITY_NUMBER, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spOPPORTUNITIES_Update";
				IDbDataParameter parID                 = Sql.AddParameter(cmd, "@ID"                , gID                  );
				IDbDataParameter parMODIFIED_USER_ID   = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"  ,  _security?.USER_ID ?? Guid.Empty    );
				IDbDataParameter parASSIGNED_USER_ID   = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"  , gASSIGNED_USER_ID    );
				IDbDataParameter parACCOUNT_ID         = Sql.AddParameter(cmd, "@ACCOUNT_ID"        , gACCOUNT_ID          );
				IDbDataParameter parNAME               = Sql.AddParameter(cmd, "@NAME"              , sNAME                , 150);
				IDbDataParameter parOPPORTUNITY_TYPE   = Sql.AddParameter(cmd, "@OPPORTUNITY_TYPE"  , sOPPORTUNITY_TYPE    , 255);
				IDbDataParameter parLEAD_SOURCE        = Sql.AddParameter(cmd, "@LEAD_SOURCE"       , sLEAD_SOURCE         ,  50);
				IDbDataParameter parAMOUNT             = Sql.AddParameter(cmd, "@AMOUNT"            , dAMOUNT              );
				IDbDataParameter parCURRENCY_ID        = Sql.AddParameter(cmd, "@CURRENCY_ID"       , gCURRENCY_ID         );
				IDbDataParameter parDATE_CLOSED        = Sql.AddParameter(cmd, "@DATE_CLOSED"       , dtDATE_CLOSED        );
				IDbDataParameter parNEXT_STEP          = Sql.AddParameter(cmd, "@NEXT_STEP"         , sNEXT_STEP           , 100);
				IDbDataParameter parSALES_STAGE        = Sql.AddParameter(cmd, "@SALES_STAGE"       , sSALES_STAGE         ,  25);
				IDbDataParameter parPROBABILITY        = Sql.AddParameter(cmd, "@PROBABILITY"       , flPROBABILITY        );
				IDbDataParameter parDESCRIPTION        = Sql.AddParameter(cmd, "@DESCRIPTION"       , sDESCRIPTION         );
				IDbDataParameter parPARENT_TYPE        = Sql.AddParameter(cmd, "@PARENT_TYPE"       , sPARENT_TYPE         ,  25);
				IDbDataParameter parPARENT_ID          = Sql.AddParameter(cmd, "@PARENT_ID"         , gPARENT_ID           );
				IDbDataParameter parACCOUNT_NAME       = Sql.AddParameter(cmd, "@ACCOUNT_NAME"      , sACCOUNT_NAME        , 100);
				IDbDataParameter parTEAM_ID            = Sql.AddParameter(cmd, "@TEAM_ID"           , gTEAM_ID             );
				IDbDataParameter parTEAM_SET_LIST      = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"     , sTEAM_SET_LIST       , 8000);
				IDbDataParameter parCAMPAIGN_ID        = Sql.AddParameter(cmd, "@CAMPAIGN_ID"       , gCAMPAIGN_ID         );
				IDbDataParameter parEXCHANGE_FOLDER    = Sql.AddParameter(cmd, "@EXCHANGE_FOLDER"   , bEXCHANGE_FOLDER     );
				IDbDataParameter parB2C_CONTACT_ID     = Sql.AddParameter(cmd, "@B2C_CONTACT_ID"    , gB2C_CONTACT_ID      );
				IDbDataParameter parLEAD_ID            = Sql.AddParameter(cmd, "@LEAD_ID"           , gLEAD_ID             );
				IDbDataParameter parTAG_SET_NAME       = Sql.AddParameter(cmd, "@TAG_SET_NAME"      , sTAG_SET_NAME        , 4000);
				IDbDataParameter parOPPORTUNITY_NUMBER = Sql.AddParameter(cmd, "@OPPORTUNITY_NUMBER", sOPPORTUNITY_NUMBER  ,  30);
				IDbDataParameter parASSIGNED_SET_LIST  = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST" , sASSIGNED_SET_LIST   , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spPROJECTS_CONTACTS_Update
		/// <summary>
		/// spPROJECTS_CONTACTS_Update
		/// </summary>
		public static void spPROJECTS_CONTACTS_Update(Guid gPROJECT_ID, Guid gCONTACT_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spPROJECTS_CONTACTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parPROJECT_ID       = Sql.AddParameter(cmd, "@PROJECT_ID"      , gPROJECT_ID        );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spPROJECTS_CONTACTS_Update
		/// <summary>
		/// spPROJECTS_CONTACTS_Update
		/// </summary>
		public static void spPROJECTS_CONTACTS_Update(Guid gPROJECT_ID, Guid gCONTACT_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spPROJECTS_CONTACTS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parPROJECT_ID       = Sql.AddParameter(cmd, "@PROJECT_ID"      , gPROJECT_ID        );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spQUOTES_CONTACTS_Update
		/// <summary>
		/// spQUOTES_CONTACTS_Update
		/// </summary>
		public static void spQUOTES_CONTACTS_Update(Guid gQUOTE_ID, Guid gCONTACT_ID, string sCONTACT_ROLE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spQUOTES_CONTACTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parQUOTE_ID         = Sql.AddParameter(cmd, "@QUOTE_ID"        , gQUOTE_ID          );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
							IDbDataParameter parCONTACT_ROLE     = Sql.AddParameter(cmd, "@CONTACT_ROLE"    , sCONTACT_ROLE      ,  25);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spQUOTES_CONTACTS_Update
		/// <summary>
		/// spQUOTES_CONTACTS_Update
		/// </summary>
		public static void spQUOTES_CONTACTS_Update(Guid gQUOTE_ID, Guid gCONTACT_ID, string sCONTACT_ROLE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spQUOTES_CONTACTS_Update";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parQUOTE_ID         = Sql.AddParameter(cmd, "@QUOTE_ID"        , gQUOTE_ID          );
				IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID        );
				IDbDataParameter parCONTACT_ROLE     = Sql.AddParameter(cmd, "@CONTACT_ROLE"    , sCONTACT_ROLE      ,  25);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spSCHEDULERS_UpdateLastRun
		/// <summary>
		/// spSCHEDULERS_UpdateLastRun
		/// </summary>
		public static void spSCHEDULERS_UpdateLastRun(Guid gID, DateTime dtLAST_RUN)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spSCHEDULERS_UpdateLastRun";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parLAST_RUN         = Sql.AddParameter(cmd, "@LAST_RUN"        , dtLAST_RUN         );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spSCHEDULERS_UpdateLastRun
		/// <summary>
		/// spSCHEDULERS_UpdateLastRun
		/// </summary>
		public static void spSCHEDULERS_UpdateLastRun(Guid gID, DateTime dtLAST_RUN, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spSCHEDULERS_UpdateLastRun";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parLAST_RUN         = Sql.AddParameter(cmd, "@LAST_RUN"        , dtLAST_RUN         );
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spSMS_MESSAGES_UpdateStatus
		/// <summary>
		/// spSMS_MESSAGES_UpdateStatus
		/// </summary>
		public static void spSMS_MESSAGES_UpdateStatus(Guid gID, string sSTATUS, string sMESSAGE_SID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spSMS_MESSAGES_UpdateStatus";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parSTATUS           = Sql.AddParameter(cmd, "@STATUS"          , sSTATUS            ,  25);
							IDbDataParameter parMESSAGE_SID      = Sql.AddParameter(cmd, "@MESSAGE_SID"     , sMESSAGE_SID       , 100);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spSMS_MESSAGES_UpdateStatus
		/// <summary>
		/// spSMS_MESSAGES_UpdateStatus
		/// </summary>
		public static void spSMS_MESSAGES_UpdateStatus(Guid gID, string sSTATUS, string sMESSAGE_SID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spSMS_MESSAGES_UpdateStatus";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parSTATUS           = Sql.AddParameter(cmd, "@STATUS"          , sSTATUS            ,  25);
				IDbDataParameter parMESSAGE_SID      = Sql.AddParameter(cmd, "@MESSAGE_SID"     , sMESSAGE_SID       , 100);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spSqlPruneDatabase
		/// <summary>
		/// spSqlPruneDatabase
		/// </summary>
		public static void spSqlPruneDatabase()
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spSqlPruneDatabase";
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spSqlPruneDatabase
		/// <summary>
		/// spSqlPruneDatabase
		/// </summary>
		public static void spSqlPruneDatabase(IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spSqlPruneDatabase";
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spSqlTableColumnExists
		/// <summary>
		/// spSqlTableColumnExists
		/// </summary>
		public static void spSqlTableColumnExists(ref bool bEXISTS, string sTABLE_NAME, string sCOLUMN_NAME, string sARCHIVE_DATABASE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spSqlTableColumnExists";
							IDbDataParameter parEXISTS           = Sql.AddParameter(cmd, "@EXISTS"          , bEXISTS            );
							IDbDataParameter parTABLE_NAME       = Sql.AddParameter(cmd, "@TABLE_NAME"      , sTABLE_NAME        ,  80);
							IDbDataParameter parCOLUMN_NAME      = Sql.AddParameter(cmd, "@COLUMN_NAME"     , sCOLUMN_NAME       ,  80);
							IDbDataParameter parARCHIVE_DATABASE = Sql.AddParameter(cmd, "@ARCHIVE_DATABASE", sARCHIVE_DATABASE  ,  50);
							parEXISTS.Direction = ParameterDirection.InputOutput;
							cmd.ExecuteNonQuery();
							bEXISTS = Sql.ToBoolean(parEXISTS.Value);
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
		#endregion

		#region spSqlTableColumnExists
		/// <summary>
		/// spSqlTableColumnExists
		/// </summary>
		public static void spSqlTableColumnExists(ref bool bEXISTS, string sTABLE_NAME, string sCOLUMN_NAME, string sARCHIVE_DATABASE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spSqlTableColumnExists";
				IDbDataParameter parEXISTS           = Sql.AddParameter(cmd, "@EXISTS"          , bEXISTS            );
				IDbDataParameter parTABLE_NAME       = Sql.AddParameter(cmd, "@TABLE_NAME"      , sTABLE_NAME        ,  80);
				IDbDataParameter parCOLUMN_NAME      = Sql.AddParameter(cmd, "@COLUMN_NAME"     , sCOLUMN_NAME       ,  80);
				IDbDataParameter parARCHIVE_DATABASE = Sql.AddParameter(cmd, "@ARCHIVE_DATABASE", sARCHIVE_DATABASE  ,  50);
				parEXISTS.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				bEXISTS = Sql.ToBoolean(parEXISTS.Value);
			}
		}
		#endregion

		#region spSYSTEM_LOG_InsertOnly
		/// <summary>
		/// spSYSTEM_LOG_InsertOnly
		/// </summary>
		public static void spSYSTEM_LOG_InsertOnly(Guid gUSER_ID, string sUSER_NAME, string sMACHINE, string sASPNET_SESSIONID, string sREMOTE_HOST, string sSERVER_HOST, string sTARGET, string sRELATIVE_PATH, string sPARAMETERS, string sERROR_TYPE, string sFILE_NAME, string sMETHOD, Int32 nLINE_NUMBER, string sMESSAGE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spSYSTEM_LOG_InsertOnly";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
							IDbDataParameter parUSER_NAME        = Sql.AddParameter(cmd, "@USER_NAME"       , sUSER_NAME         , 255);
							IDbDataParameter parMACHINE          = Sql.AddParameter(cmd, "@MACHINE"         , sMACHINE           ,  60);
							IDbDataParameter parASPNET_SESSIONID = Sql.AddParameter(cmd, "@ASPNET_SESSIONID", sASPNET_SESSIONID  ,  50);
							IDbDataParameter parREMOTE_HOST      = Sql.AddParameter(cmd, "@REMOTE_HOST"     , sREMOTE_HOST       , 100);
							IDbDataParameter parSERVER_HOST      = Sql.AddParameter(cmd, "@SERVER_HOST"     , sSERVER_HOST       , 100);
							IDbDataParameter parTARGET           = Sql.AddParameter(cmd, "@TARGET"          , sTARGET            , 255);
							IDbDataParameter parRELATIVE_PATH    = Sql.AddParameter(cmd, "@RELATIVE_PATH"   , sRELATIVE_PATH     , 255);
							IDbDataParameter parPARAMETERS       = Sql.AddParameter(cmd, "@PARAMETERS"      , sPARAMETERS        , 2000);
							IDbDataParameter parERROR_TYPE       = Sql.AddParameter(cmd, "@ERROR_TYPE"      , sERROR_TYPE        ,  25);
							IDbDataParameter parFILE_NAME        = Sql.AddParameter(cmd, "@FILE_NAME"       , sFILE_NAME         , 255);
							IDbDataParameter parMETHOD           = Sql.AddParameter(cmd, "@METHOD"          , sMETHOD            , 450);
							IDbDataParameter parLINE_NUMBER      = Sql.AddParameter(cmd, "@LINE_NUMBER"     , nLINE_NUMBER       );
							IDbDataParameter parMESSAGE          = Sql.AddParameter(cmd, "@MESSAGE"         , sMESSAGE           );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spSYSTEM_LOG_InsertOnly
		/// <summary>
		/// spSYSTEM_LOG_InsertOnly
		/// </summary>
		public static void spSYSTEM_LOG_InsertOnly(Guid gUSER_ID, string sUSER_NAME, string sMACHINE, string sASPNET_SESSIONID, string sREMOTE_HOST, string sSERVER_HOST, string sTARGET, string sRELATIVE_PATH, string sPARAMETERS, string sERROR_TYPE, string sFILE_NAME, string sMETHOD, Int32 nLINE_NUMBER, string sMESSAGE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spSYSTEM_LOG_InsertOnly";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
				IDbDataParameter parUSER_NAME        = Sql.AddParameter(cmd, "@USER_NAME"       , sUSER_NAME         , 255);
				IDbDataParameter parMACHINE          = Sql.AddParameter(cmd, "@MACHINE"         , sMACHINE           ,  60);
				IDbDataParameter parASPNET_SESSIONID = Sql.AddParameter(cmd, "@ASPNET_SESSIONID", sASPNET_SESSIONID  ,  50);
				IDbDataParameter parREMOTE_HOST      = Sql.AddParameter(cmd, "@REMOTE_HOST"     , sREMOTE_HOST       , 100);
				IDbDataParameter parSERVER_HOST      = Sql.AddParameter(cmd, "@SERVER_HOST"     , sSERVER_HOST       , 100);
				IDbDataParameter parTARGET           = Sql.AddParameter(cmd, "@TARGET"          , sTARGET            , 255);
				IDbDataParameter parRELATIVE_PATH    = Sql.AddParameter(cmd, "@RELATIVE_PATH"   , sRELATIVE_PATH     , 255);
				IDbDataParameter parPARAMETERS       = Sql.AddParameter(cmd, "@PARAMETERS"      , sPARAMETERS        , 2000);
				IDbDataParameter parERROR_TYPE       = Sql.AddParameter(cmd, "@ERROR_TYPE"      , sERROR_TYPE        ,  25);
				IDbDataParameter parFILE_NAME        = Sql.AddParameter(cmd, "@FILE_NAME"       , sFILE_NAME         , 255);
				IDbDataParameter parMETHOD           = Sql.AddParameter(cmd, "@METHOD"          , sMETHOD            , 450);
				IDbDataParameter parLINE_NUMBER      = Sql.AddParameter(cmd, "@LINE_NUMBER"     , nLINE_NUMBER       );
				IDbDataParameter parMESSAGE          = Sql.AddParameter(cmd, "@MESSAGE"         , sMESSAGE           );
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spSYSTEM_SYNC_LOG_InsertOnly
		/// <summary>
		/// spSYSTEM_SYNC_LOG_InsertOnly
		/// </summary>
		public static void spSYSTEM_SYNC_LOG_InsertOnly(Guid gUSER_ID, string sMACHINE, string sREMOTE_URL, string sERROR_TYPE, string sFILE_NAME, string sMETHOD, Int32 nLINE_NUMBER, string sMESSAGE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spSYSTEM_SYNC_LOG_InsertOnly";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
							IDbDataParameter parMACHINE          = Sql.AddParameter(cmd, "@MACHINE"         , sMACHINE           ,  60);
							IDbDataParameter parREMOTE_URL       = Sql.AddParameter(cmd, "@REMOTE_URL"      , sREMOTE_URL        , 255);
							IDbDataParameter parERROR_TYPE       = Sql.AddParameter(cmd, "@ERROR_TYPE"      , sERROR_TYPE        ,  25);
							IDbDataParameter parFILE_NAME        = Sql.AddParameter(cmd, "@FILE_NAME"       , sFILE_NAME         , 255);
							IDbDataParameter parMETHOD           = Sql.AddParameter(cmd, "@METHOD"          , sMETHOD            , 450);
							IDbDataParameter parLINE_NUMBER      = Sql.AddParameter(cmd, "@LINE_NUMBER"     , nLINE_NUMBER       );
							IDbDataParameter parMESSAGE          = Sql.AddParameter(cmd, "@MESSAGE"         , sMESSAGE           );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spSYSTEM_SYNC_LOG_InsertOnly
		/// <summary>
		/// spSYSTEM_SYNC_LOG_InsertOnly
		/// </summary>
		public static void spSYSTEM_SYNC_LOG_InsertOnly(Guid gUSER_ID, string sMACHINE, string sREMOTE_URL, string sERROR_TYPE, string sFILE_NAME, string sMETHOD, Int32 nLINE_NUMBER, string sMESSAGE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spSYSTEM_SYNC_LOG_InsertOnly";
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
				IDbDataParameter parMACHINE          = Sql.AddParameter(cmd, "@MACHINE"         , sMACHINE           ,  60);
				IDbDataParameter parREMOTE_URL       = Sql.AddParameter(cmd, "@REMOTE_URL"      , sREMOTE_URL        , 255);
				IDbDataParameter parERROR_TYPE       = Sql.AddParameter(cmd, "@ERROR_TYPE"      , sERROR_TYPE        ,  25);
				IDbDataParameter parFILE_NAME        = Sql.AddParameter(cmd, "@FILE_NAME"       , sFILE_NAME         , 255);
				IDbDataParameter parMETHOD           = Sql.AddParameter(cmd, "@METHOD"          , sMETHOD            , 450);
				IDbDataParameter parLINE_NUMBER      = Sql.AddParameter(cmd, "@LINE_NUMBER"     , nLINE_NUMBER       );
				IDbDataParameter parMESSAGE          = Sql.AddParameter(cmd, "@MESSAGE"         , sMESSAGE           );
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spTASKS_EmailReminderSent
		/// <summary>
		/// spTASKS_EmailReminderSent
		/// </summary>
		public static void spTASKS_EmailReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spTASKS_EmailReminderSent";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE      ,  25);
							IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID        );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spTASKS_EmailReminderSent
		/// <summary>
		/// spTASKS_EmailReminderSent
		/// </summary>
		public static void spTASKS_EmailReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spTASKS_EmailReminderSent";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE      ,  25);
				IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID        );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spTASKS_SmsReminderSent
		/// <summary>
		/// spTASKS_SmsReminderSent
		/// </summary>
		public static void spTASKS_SmsReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spTASKS_SmsReminderSent";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE      ,  25);
							IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID        );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spTASKS_SmsReminderSent
		/// <summary>
		/// spTASKS_SmsReminderSent
		/// </summary>
		public static void spTASKS_SmsReminderSent(Guid gID, string sINVITEE_TYPE, Guid gINVITEE_ID, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spTASKS_SmsReminderSent";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parINVITEE_TYPE     = Sql.AddParameter(cmd, "@INVITEE_TYPE"    , sINVITEE_TYPE      ,  25);
				IDbDataParameter parINVITEE_ID       = Sql.AddParameter(cmd, "@INVITEE_ID"      , gINVITEE_ID        );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spTASKS_Update
		/// <summary>
		/// spTASKS_Update
		/// </summary>
		public static void spTASKS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, string sSTATUS, DateTime dtDATE_TIME_DUE, DateTime dtDATE_TIME_START, string sPARENT_TYPE, Guid gPARENT_ID, Guid gCONTACT_ID, string sPRIORITY, string sDESCRIPTION, Guid gTEAM_ID, string sTEAM_SET_LIST, string sTAG_SET_NAME, Int32 nREMINDER_TIME, Int32 nEMAIL_REMINDER_TIME, Int32 nSMS_REMINDER_TIME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spTASKS_Update";
							IDbDataParameter parID                  = Sql.AddParameter(cmd, "@ID"                 , gID                   );
							IDbDataParameter parMODIFIED_USER_ID    = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"   ,  _security?.USER_ID ?? Guid.Empty     );
							IDbDataParameter parASSIGNED_USER_ID    = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"   , gASSIGNED_USER_ID     );
							IDbDataParameter parNAME                = Sql.AddParameter(cmd, "@NAME"               , sNAME                 ,  50);
							IDbDataParameter parSTATUS              = Sql.AddParameter(cmd, "@STATUS"             , sSTATUS               ,  25);
							IDbDataParameter parDATE_TIME_DUE       = Sql.AddParameter(cmd, "@DATE_TIME_DUE"      , dtDATE_TIME_DUE       );
							IDbDataParameter parDATE_TIME_START     = Sql.AddParameter(cmd, "@DATE_TIME_START"    , dtDATE_TIME_START     );
							IDbDataParameter parPARENT_TYPE         = Sql.AddParameter(cmd, "@PARENT_TYPE"        , sPARENT_TYPE          ,  25);
							IDbDataParameter parPARENT_ID           = Sql.AddParameter(cmd, "@PARENT_ID"          , gPARENT_ID            );
							IDbDataParameter parCONTACT_ID          = Sql.AddParameter(cmd, "@CONTACT_ID"         , gCONTACT_ID           );
							IDbDataParameter parPRIORITY            = Sql.AddParameter(cmd, "@PRIORITY"           , sPRIORITY             ,  25);
							IDbDataParameter parDESCRIPTION         = Sql.AddParameter(cmd, "@DESCRIPTION"        , sDESCRIPTION          );
							IDbDataParameter parTEAM_ID             = Sql.AddParameter(cmd, "@TEAM_ID"            , gTEAM_ID              );
							IDbDataParameter parTEAM_SET_LIST       = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"      , sTEAM_SET_LIST        , 8000);
							IDbDataParameter parTAG_SET_NAME        = Sql.AddParameter(cmd, "@TAG_SET_NAME"       , sTAG_SET_NAME         , 4000);
							IDbDataParameter parREMINDER_TIME       = Sql.AddParameter(cmd, "@REMINDER_TIME"      , nREMINDER_TIME        );
							IDbDataParameter parEMAIL_REMINDER_TIME = Sql.AddParameter(cmd, "@EMAIL_REMINDER_TIME", nEMAIL_REMINDER_TIME  );
							IDbDataParameter parSMS_REMINDER_TIME   = Sql.AddParameter(cmd, "@SMS_REMINDER_TIME"  , nSMS_REMINDER_TIME    );
							IDbDataParameter parIS_PRIVATE          = Sql.AddParameter(cmd, "@IS_PRIVATE"         , bIS_PRIVATE           );
							IDbDataParameter parASSIGNED_SET_LIST   = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"  , sASSIGNED_SET_LIST    , 8000);
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
		#endregion

		#region spTASKS_Update
		/// <summary>
		/// spTASKS_Update
		/// </summary>
		public static void spTASKS_Update(ref Guid gID, Guid gASSIGNED_USER_ID, string sNAME, string sSTATUS, DateTime dtDATE_TIME_DUE, DateTime dtDATE_TIME_START, string sPARENT_TYPE, Guid gPARENT_ID, Guid gCONTACT_ID, string sPRIORITY, string sDESCRIPTION, Guid gTEAM_ID, string sTEAM_SET_LIST, string sTAG_SET_NAME, Int32 nREMINDER_TIME, Int32 nEMAIL_REMINDER_TIME, Int32 nSMS_REMINDER_TIME, bool bIS_PRIVATE, string sASSIGNED_SET_LIST, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spTASKS_Update";
				IDbDataParameter parID                  = Sql.AddParameter(cmd, "@ID"                 , gID                   );
				IDbDataParameter parMODIFIED_USER_ID    = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"   ,  _security?.USER_ID ?? Guid.Empty     );
				IDbDataParameter parASSIGNED_USER_ID    = Sql.AddParameter(cmd, "@ASSIGNED_USER_ID"   , gASSIGNED_USER_ID     );
				IDbDataParameter parNAME                = Sql.AddParameter(cmd, "@NAME"               , sNAME                 ,  50);
				IDbDataParameter parSTATUS              = Sql.AddParameter(cmd, "@STATUS"             , sSTATUS               ,  25);
				IDbDataParameter parDATE_TIME_DUE       = Sql.AddParameter(cmd, "@DATE_TIME_DUE"      , dtDATE_TIME_DUE       );
				IDbDataParameter parDATE_TIME_START     = Sql.AddParameter(cmd, "@DATE_TIME_START"    , dtDATE_TIME_START     );
				IDbDataParameter parPARENT_TYPE         = Sql.AddParameter(cmd, "@PARENT_TYPE"        , sPARENT_TYPE          ,  25);
				IDbDataParameter parPARENT_ID           = Sql.AddParameter(cmd, "@PARENT_ID"          , gPARENT_ID            );
				IDbDataParameter parCONTACT_ID          = Sql.AddParameter(cmd, "@CONTACT_ID"         , gCONTACT_ID           );
				IDbDataParameter parPRIORITY            = Sql.AddParameter(cmd, "@PRIORITY"           , sPRIORITY             ,  25);
				IDbDataParameter parDESCRIPTION         = Sql.AddParameter(cmd, "@DESCRIPTION"        , sDESCRIPTION          );
				IDbDataParameter parTEAM_ID             = Sql.AddParameter(cmd, "@TEAM_ID"            , gTEAM_ID              );
				IDbDataParameter parTEAM_SET_LIST       = Sql.AddAnsiParam(cmd, "@TEAM_SET_LIST"      , sTEAM_SET_LIST        , 8000);
				IDbDataParameter parTAG_SET_NAME        = Sql.AddParameter(cmd, "@TAG_SET_NAME"       , sTAG_SET_NAME         , 4000);
				IDbDataParameter parREMINDER_TIME       = Sql.AddParameter(cmd, "@REMINDER_TIME"      , nREMINDER_TIME        );
				IDbDataParameter parEMAIL_REMINDER_TIME = Sql.AddParameter(cmd, "@EMAIL_REMINDER_TIME", nEMAIL_REMINDER_TIME  );
				IDbDataParameter parSMS_REMINDER_TIME   = Sql.AddParameter(cmd, "@SMS_REMINDER_TIME"  , nSMS_REMINDER_TIME    );
				IDbDataParameter parIS_PRIVATE          = Sql.AddParameter(cmd, "@IS_PRIVATE"         , bIS_PRIVATE           );
				IDbDataParameter parASSIGNED_SET_LIST   = Sql.AddAnsiParam(cmd, "@ASSIGNED_SET_LIST"  , sASSIGNED_SET_LIST    , 8000);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spTERMINOLOGY_HELP_Update
		/// <summary>
		/// spTERMINOLOGY_HELP_Update
		/// </summary>
		public static void spTERMINOLOGY_HELP_Update(ref Guid gID, string sNAME, string sLANG, string sMODULE_NAME, string sDISPLAY_TEXT)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spTERMINOLOGY_HELP_Update";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME              ,  50);
							IDbDataParameter parLANG             = Sql.AddParameter(cmd, "@LANG"            , sLANG              ,  10);
							IDbDataParameter parMODULE_NAME      = Sql.AddParameter(cmd, "@MODULE_NAME"     , sMODULE_NAME       ,  25);
							IDbDataParameter parDISPLAY_TEXT     = Sql.AddParameter(cmd, "@DISPLAY_TEXT"    , sDISPLAY_TEXT      );
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
		#endregion

		#region spTERMINOLOGY_HELP_Update
		/// <summary>
		/// spTERMINOLOGY_HELP_Update
		/// </summary>
		public static void spTERMINOLOGY_HELP_Update(ref Guid gID, string sNAME, string sLANG, string sMODULE_NAME, string sDISPLAY_TEXT, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spTERMINOLOGY_HELP_Update";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parNAME             = Sql.AddParameter(cmd, "@NAME"            , sNAME              ,  50);
				IDbDataParameter parLANG             = Sql.AddParameter(cmd, "@LANG"            , sLANG              ,  10);
				IDbDataParameter parMODULE_NAME      = Sql.AddParameter(cmd, "@MODULE_NAME"     , sMODULE_NAME       ,  25);
				IDbDataParameter parDISPLAY_TEXT     = Sql.AddParameter(cmd, "@DISPLAY_TEXT"    , sDISPLAY_TEXT      );
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spTERMINOLOGY_Update
		/// <summary>
		/// spTERMINOLOGY_Update
		/// </summary>
		public static void spTERMINOLOGY_Update(string sNAME, string sLANG, string sMODULE_NAME, string sLIST_NAME, Int32 nLIST_ORDER, string sDISPLAY_NAME)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spTERMINOLOGY_Update";
							IDbDataParameter parNAME         = Sql.AddParameter(cmd, "@NAME"        , sNAME          , 150);
							IDbDataParameter parLANG         = Sql.AddParameter(cmd, "@LANG"        , sLANG          ,  10);
							IDbDataParameter parMODULE_NAME  = Sql.AddParameter(cmd, "@MODULE_NAME" , sMODULE_NAME   ,  25);
							IDbDataParameter parLIST_NAME    = Sql.AddParameter(cmd, "@LIST_NAME"   , sLIST_NAME     ,  50);
							IDbDataParameter parLIST_ORDER   = Sql.AddParameter(cmd, "@LIST_ORDER"  , nLIST_ORDER    );
							IDbDataParameter parDISPLAY_NAME = Sql.AddParameter(cmd, "@DISPLAY_NAME", sDISPLAY_NAME  );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spTERMINOLOGY_Update
		/// <summary>
		/// spTERMINOLOGY_Update
		/// </summary>
		public static void spTERMINOLOGY_Update(string sNAME, string sLANG, string sMODULE_NAME, string sLIST_NAME, Int32 nLIST_ORDER, string sDISPLAY_NAME, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spTERMINOLOGY_Update";
				IDbDataParameter parNAME         = Sql.AddParameter(cmd, "@NAME"        , sNAME          , 150);
				IDbDataParameter parLANG         = Sql.AddParameter(cmd, "@LANG"        , sLANG          ,  10);
				IDbDataParameter parMODULE_NAME  = Sql.AddParameter(cmd, "@MODULE_NAME" , sMODULE_NAME   ,  25);
				IDbDataParameter parLIST_NAME    = Sql.AddParameter(cmd, "@LIST_NAME"   , sLIST_NAME     ,  50);
				IDbDataParameter parLIST_ORDER   = Sql.AddParameter(cmd, "@LIST_ORDER"  , nLIST_ORDER    );
				IDbDataParameter parDISPLAY_NAME = Sql.AddParameter(cmd, "@DISPLAY_NAME", sDISPLAY_NAME  );
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spUSERS_InsertNTLM
		/// <summary>
		/// spUSERS_InsertNTLM
		/// </summary>
		public static void spUSERS_InsertNTLM(ref Guid gID, string sUSER_DOMAIN, string sUSER_NAME, bool bIS_ADMIN)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spUSERS_InsertNTLM";
							IDbDataParameter parID          = Sql.AddParameter(cmd, "@ID"         , gID           );
							IDbDataParameter parUSER_DOMAIN = Sql.AddParameter(cmd, "@USER_DOMAIN", sUSER_DOMAIN  ,  20);
							IDbDataParameter parUSER_NAME   = Sql.AddParameter(cmd, "@USER_NAME"  , sUSER_NAME    ,  60);
							IDbDataParameter parIS_ADMIN    = Sql.AddParameter(cmd, "@IS_ADMIN"   , bIS_ADMIN     );
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
		#endregion

		#region spUSERS_InsertNTLM
		/// <summary>
		/// spUSERS_InsertNTLM
		/// </summary>
		public static void spUSERS_InsertNTLM(ref Guid gID, string sUSER_DOMAIN, string sUSER_NAME, bool bIS_ADMIN, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spUSERS_InsertNTLM";
				IDbDataParameter parID          = Sql.AddParameter(cmd, "@ID"         , gID           );
				IDbDataParameter parUSER_DOMAIN = Sql.AddParameter(cmd, "@USER_DOMAIN", sUSER_DOMAIN  ,  20);
				IDbDataParameter parUSER_NAME   = Sql.AddParameter(cmd, "@USER_NAME"  , sUSER_NAME    ,  60);
				IDbDataParameter parIS_ADMIN    = Sql.AddParameter(cmd, "@IS_ADMIN"   , bIS_ADMIN     );
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spUSERS_LOGINS_InsertOnly
		/// <summary>
		/// spUSERS_LOGINS_InsertOnly
		/// </summary>
		public static void spUSERS_LOGINS_InsertOnly(ref Guid gID, Guid gUSER_ID, string sUSER_NAME, string sLOGIN_TYPE, string sLOGIN_STATUS, string sASPNET_SESSIONID, string sREMOTE_HOST, string sSERVER_HOST, string sTARGET, string sRELATIVE_PATH, string sUSER_AGENT)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spUSERS_LOGINS_InsertOnly";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
							IDbDataParameter parUSER_NAME        = Sql.AddParameter(cmd, "@USER_NAME"       , sUSER_NAME         ,  60);
							IDbDataParameter parLOGIN_TYPE       = Sql.AddParameter(cmd, "@LOGIN_TYPE"      , sLOGIN_TYPE        ,  25);
							IDbDataParameter parLOGIN_STATUS     = Sql.AddParameter(cmd, "@LOGIN_STATUS"    , sLOGIN_STATUS      ,  25);
							IDbDataParameter parASPNET_SESSIONID = Sql.AddParameter(cmd, "@ASPNET_SESSIONID", sASPNET_SESSIONID  ,  50);
							IDbDataParameter parREMOTE_HOST      = Sql.AddParameter(cmd, "@REMOTE_HOST"     , sREMOTE_HOST       , 100);
							IDbDataParameter parSERVER_HOST      = Sql.AddParameter(cmd, "@SERVER_HOST"     , sSERVER_HOST       , 100);
							IDbDataParameter parTARGET           = Sql.AddParameter(cmd, "@TARGET"          , sTARGET            , 255);
							IDbDataParameter parRELATIVE_PATH    = Sql.AddParameter(cmd, "@RELATIVE_PATH"   , sRELATIVE_PATH     , 255);
							IDbDataParameter parUSER_AGENT       = Sql.AddParameter(cmd, "@USER_AGENT"      , sUSER_AGENT        , 255);
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
		#endregion

		#region spUSERS_LOGINS_InsertOnly
		/// <summary>
		/// spUSERS_LOGINS_InsertOnly
		/// </summary>
		public static void spUSERS_LOGINS_InsertOnly(ref Guid gID, Guid gUSER_ID, string sUSER_NAME, string sLOGIN_TYPE, string sLOGIN_STATUS, string sASPNET_SESSIONID, string sREMOTE_HOST, string sSERVER_HOST, string sTARGET, string sRELATIVE_PATH, string sUSER_AGENT, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spUSERS_LOGINS_InsertOnly";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID           );
				IDbDataParameter parUSER_NAME        = Sql.AddParameter(cmd, "@USER_NAME"       , sUSER_NAME         ,  60);
				IDbDataParameter parLOGIN_TYPE       = Sql.AddParameter(cmd, "@LOGIN_TYPE"      , sLOGIN_TYPE        ,  25);
				IDbDataParameter parLOGIN_STATUS     = Sql.AddParameter(cmd, "@LOGIN_STATUS"    , sLOGIN_STATUS      ,  25);
				IDbDataParameter parASPNET_SESSIONID = Sql.AddParameter(cmd, "@ASPNET_SESSIONID", sASPNET_SESSIONID  ,  50);
				IDbDataParameter parREMOTE_HOST      = Sql.AddParameter(cmd, "@REMOTE_HOST"     , sREMOTE_HOST       , 100);
				IDbDataParameter parSERVER_HOST      = Sql.AddParameter(cmd, "@SERVER_HOST"     , sSERVER_HOST       , 100);
				IDbDataParameter parTARGET           = Sql.AddParameter(cmd, "@TARGET"          , sTARGET            , 255);
				IDbDataParameter parRELATIVE_PATH    = Sql.AddParameter(cmd, "@RELATIVE_PATH"   , sRELATIVE_PATH     , 255);
				IDbDataParameter parUSER_AGENT       = Sql.AddParameter(cmd, "@USER_AGENT"      , sUSER_AGENT        , 255);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spUSERS_PASSWORD_LINK_InsertOnly
		/// <summary>
		/// spUSERS_PASSWORD_LINK_InsertOnly
		/// </summary>
		public static void spUSERS_PASSWORD_LINK_InsertOnly(ref Guid gID, string sUSER_NAME)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							if ( Sql.IsOracle(cmd) )
								cmd.CommandText = "spUSERS_PASSWORD_LINK_InsertOn";
							else
								cmd.CommandText = "spUSERS_PASSWORD_LINK_InsertOnly";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parUSER_NAME        = Sql.AddParameter(cmd, "@USER_NAME"       , sUSER_NAME         ,  60);
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
		#endregion

		#region spUSERS_PASSWORD_LINK_InsertOnly
		/// <summary>
		/// spUSERS_PASSWORD_LINK_InsertOnly
		/// </summary>
		public static void spUSERS_PASSWORD_LINK_InsertOnly(ref Guid gID, string sUSER_NAME, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				if ( Sql.IsOracle(cmd) )
					cmd.CommandText = "spUSERS_PASSWORD_LINK_InsertOn";
				else
					cmd.CommandText = "spUSERS_PASSWORD_LINK_InsertOnly";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parUSER_NAME        = Sql.AddParameter(cmd, "@USER_NAME"       , sUSER_NAME         ,  60);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region spUSERS_PasswordUpdate
		/// <summary>
		/// spUSERS_PasswordUpdate
		/// </summary>
		public static void spUSERS_PasswordUpdate(Guid gID, string sUSER_HASH)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spUSERS_PasswordUpdate";
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
							IDbDataParameter parUSER_HASH        = Sql.AddParameter(cmd, "@USER_HASH"       , sUSER_HASH         ,  32);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spUSERS_PasswordUpdate
		/// <summary>
		/// spUSERS_PasswordUpdate
		/// </summary>
		public static void spUSERS_PasswordUpdate(Guid gID, string sUSER_HASH, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spUSERS_PasswordUpdate";
				IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gID                );
				IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID",  _security?.USER_ID ?? Guid.Empty  );
				IDbDataParameter parUSER_HASH        = Sql.AddParameter(cmd, "@USER_HASH"       , sUSER_HASH         ,  32);
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
			}
		}
		#endregion

		#region spUSERS_Update
		/// <summary>
		/// spUSERS_Update
		/// </summary>
		public static void spUSERS_Update(ref Guid gID, string sUSER_NAME, string sFIRST_NAME, string sLAST_NAME, Guid gREPORTS_TO_ID, bool bIS_ADMIN, bool bRECEIVE_NOTIFICATIONS, string sDESCRIPTION, string sTITLE, string sDEPARTMENT, string sPHONE_HOME, string sPHONE_MOBILE, string sPHONE_WORK, string sPHONE_OTHER, string sPHONE_FAX, string sEMAIL1, string sEMAIL2, string sSTATUS, string sADDRESS_STREET, string sADDRESS_CITY, string sADDRESS_STATE, string sADDRESS_POSTALCODE, string sADDRESS_COUNTRY, string sUSER_PREFERENCES, bool bPORTAL_ONLY, string sEMPLOYEE_STATUS, string sMESSENGER_ID, string sMESSENGER_TYPE, string sPARENT_TYPE, Guid gPARENT_ID, bool bIS_GROUP, Guid gDEFAULT_TEAM, bool bIS_ADMIN_DELEGATE, string sMAIL_SMTPUSER, string sMAIL_SMTPPASS, bool bSYSTEM_GENERATED_PASSWORD, bool bGOOGLEAPPS_SYNC_CONTACTS, bool bGOOGLEAPPS_SYNC_CALENDAR, string sGOOGLEAPPS_USERNAME, string sGOOGLEAPPS_PASSWORD, string sFACEBOOK_ID, bool bICLOUD_SYNC_CONTACTS, bool bICLOUD_SYNC_CALENDAR, string sICLOUD_USERNAME, string sICLOUD_PASSWORD, string sTHEME, string sDATE_FORMAT, string sTIME_FORMAT, string sLANG, Guid gCURRENCY_ID, Guid gTIMEZONE_ID, bool bSAVE_QUERY, bool bGROUP_TABS, bool bSUBPANEL_TABS, string sEXTENSION, string sSMS_OPT_IN, string sPICTURE, string sMAIL_SMTPSERVER, Int32 nMAIL_SMTPPORT, bool bMAIL_SMTPAUTH_REQ, Int32 nMAIL_SMTPSSL, string sMAIL_SENDTYPE)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spUSERS_Update";
							IDbDataParameter parID                        = Sql.AddParameter(cmd, "@ID"                       , gID                         );
							IDbDataParameter parMODIFIED_USER_ID          = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"         ,  _security?.USER_ID ?? Guid.Empty           );
							IDbDataParameter parUSER_NAME                 = Sql.AddParameter(cmd, "@USER_NAME"                , sUSER_NAME                  ,  60);
							IDbDataParameter parFIRST_NAME                = Sql.AddParameter(cmd, "@FIRST_NAME"               , sFIRST_NAME                 ,  30);
							IDbDataParameter parLAST_NAME                 = Sql.AddParameter(cmd, "@LAST_NAME"                , sLAST_NAME                  ,  30);
							IDbDataParameter parREPORTS_TO_ID             = Sql.AddParameter(cmd, "@REPORTS_TO_ID"            , gREPORTS_TO_ID              );
							IDbDataParameter parIS_ADMIN                  = Sql.AddParameter(cmd, "@IS_ADMIN"                 , bIS_ADMIN                   );
							IDbDataParameter parRECEIVE_NOTIFICATIONS     = Sql.AddParameter(cmd, "@RECEIVE_NOTIFICATIONS"    , bRECEIVE_NOTIFICATIONS      );
							IDbDataParameter parDESCRIPTION               = Sql.AddParameter(cmd, "@DESCRIPTION"              , sDESCRIPTION                );
							IDbDataParameter parTITLE                     = Sql.AddParameter(cmd, "@TITLE"                    , sTITLE                      ,  50);
							IDbDataParameter parDEPARTMENT                = Sql.AddParameter(cmd, "@DEPARTMENT"               , sDEPARTMENT                 ,  50);
							IDbDataParameter parPHONE_HOME                = Sql.AddParameter(cmd, "@PHONE_HOME"               , sPHONE_HOME                 ,  50);
							IDbDataParameter parPHONE_MOBILE              = Sql.AddParameter(cmd, "@PHONE_MOBILE"             , sPHONE_MOBILE               ,  50);
							IDbDataParameter parPHONE_WORK                = Sql.AddParameter(cmd, "@PHONE_WORK"               , sPHONE_WORK                 ,  50);
							IDbDataParameter parPHONE_OTHER               = Sql.AddParameter(cmd, "@PHONE_OTHER"              , sPHONE_OTHER                ,  50);
							IDbDataParameter parPHONE_FAX                 = Sql.AddParameter(cmd, "@PHONE_FAX"                , sPHONE_FAX                  ,  50);
							IDbDataParameter parEMAIL1                    = Sql.AddParameter(cmd, "@EMAIL1"                   , sEMAIL1                     , 100);
							IDbDataParameter parEMAIL2                    = Sql.AddParameter(cmd, "@EMAIL2"                   , sEMAIL2                     , 100);
							IDbDataParameter parSTATUS                    = Sql.AddParameter(cmd, "@STATUS"                   , sSTATUS                     ,  25);
							IDbDataParameter parADDRESS_STREET            = Sql.AddParameter(cmd, "@ADDRESS_STREET"           , sADDRESS_STREET             , 150);
							IDbDataParameter parADDRESS_CITY              = Sql.AddParameter(cmd, "@ADDRESS_CITY"             , sADDRESS_CITY               , 100);
							IDbDataParameter parADDRESS_STATE             = Sql.AddParameter(cmd, "@ADDRESS_STATE"            , sADDRESS_STATE              , 100);
							IDbDataParameter parADDRESS_POSTALCODE        = Sql.AddParameter(cmd, "@ADDRESS_POSTALCODE"       , sADDRESS_POSTALCODE         ,   9);
							IDbDataParameter parADDRESS_COUNTRY           = Sql.AddParameter(cmd, "@ADDRESS_COUNTRY"          , sADDRESS_COUNTRY            ,  25);
							IDbDataParameter parUSER_PREFERENCES          = Sql.AddParameter(cmd, "@USER_PREFERENCES"         , sUSER_PREFERENCES           );
							IDbDataParameter parPORTAL_ONLY               = Sql.AddParameter(cmd, "@PORTAL_ONLY"              , bPORTAL_ONLY                );
							IDbDataParameter parEMPLOYEE_STATUS           = Sql.AddParameter(cmd, "@EMPLOYEE_STATUS"          , sEMPLOYEE_STATUS            ,  25);
							IDbDataParameter parMESSENGER_ID              = Sql.AddParameter(cmd, "@MESSENGER_ID"             , sMESSENGER_ID               ,  25);
							IDbDataParameter parMESSENGER_TYPE            = Sql.AddParameter(cmd, "@MESSENGER_TYPE"           , sMESSENGER_TYPE             ,  25);
							IDbDataParameter parPARENT_TYPE               = Sql.AddParameter(cmd, "@PARENT_TYPE"              , sPARENT_TYPE                ,  25);
							IDbDataParameter parPARENT_ID                 = Sql.AddParameter(cmd, "@PARENT_ID"                , gPARENT_ID                  );
							IDbDataParameter parIS_GROUP                  = Sql.AddParameter(cmd, "@IS_GROUP"                 , bIS_GROUP                   );
							IDbDataParameter parDEFAULT_TEAM              = Sql.AddParameter(cmd, "@DEFAULT_TEAM"             , gDEFAULT_TEAM               );
							IDbDataParameter parIS_ADMIN_DELEGATE         = Sql.AddParameter(cmd, "@IS_ADMIN_DELEGATE"        , bIS_ADMIN_DELEGATE          );
							IDbDataParameter parMAIL_SMTPUSER             = Sql.AddParameter(cmd, "@MAIL_SMTPUSER"            , sMAIL_SMTPUSER              ,  60);
							IDbDataParameter parMAIL_SMTPPASS             = Sql.AddParameter(cmd, "@MAIL_SMTPPASS"            , sMAIL_SMTPPASS              , 100);
							IDbDataParameter parSYSTEM_GENERATED_PASSWORD = Sql.AddParameter(cmd, "@SYSTEM_GENERATED_PASSWORD", bSYSTEM_GENERATED_PASSWORD  );
							IDbDataParameter parGOOGLEAPPS_SYNC_CONTACTS  = Sql.AddParameter(cmd, "@GOOGLEAPPS_SYNC_CONTACTS" , bGOOGLEAPPS_SYNC_CONTACTS   );
							IDbDataParameter parGOOGLEAPPS_SYNC_CALENDAR  = Sql.AddParameter(cmd, "@GOOGLEAPPS_SYNC_CALENDAR" , bGOOGLEAPPS_SYNC_CALENDAR   );
							IDbDataParameter parGOOGLEAPPS_USERNAME       = Sql.AddParameter(cmd, "@GOOGLEAPPS_USERNAME"      , sGOOGLEAPPS_USERNAME        , 100);
							IDbDataParameter parGOOGLEAPPS_PASSWORD       = Sql.AddParameter(cmd, "@GOOGLEAPPS_PASSWORD"      , sGOOGLEAPPS_PASSWORD        , 100);
							IDbDataParameter parFACEBOOK_ID               = Sql.AddParameter(cmd, "@FACEBOOK_ID"              , sFACEBOOK_ID                ,  25);
							IDbDataParameter parICLOUD_SYNC_CONTACTS      = Sql.AddParameter(cmd, "@ICLOUD_SYNC_CONTACTS"     , bICLOUD_SYNC_CONTACTS       );
							IDbDataParameter parICLOUD_SYNC_CALENDAR      = Sql.AddParameter(cmd, "@ICLOUD_SYNC_CALENDAR"     , bICLOUD_SYNC_CALENDAR       );
							IDbDataParameter parICLOUD_USERNAME           = Sql.AddParameter(cmd, "@ICLOUD_USERNAME"          , sICLOUD_USERNAME            , 100);
							IDbDataParameter parICLOUD_PASSWORD           = Sql.AddParameter(cmd, "@ICLOUD_PASSWORD"          , sICLOUD_PASSWORD            , 100);
							IDbDataParameter parTHEME                     = Sql.AddParameter(cmd, "@THEME"                    , sTHEME                      ,  25);
							IDbDataParameter parDATE_FORMAT               = Sql.AddParameter(cmd, "@DATE_FORMAT"              , sDATE_FORMAT                ,  25);
							IDbDataParameter parTIME_FORMAT               = Sql.AddParameter(cmd, "@TIME_FORMAT"              , sTIME_FORMAT                ,  25);
							IDbDataParameter parLANG                      = Sql.AddParameter(cmd, "@LANG"                     , sLANG                       ,  10);
							IDbDataParameter parCURRENCY_ID               = Sql.AddParameter(cmd, "@CURRENCY_ID"              , gCURRENCY_ID                );
							IDbDataParameter parTIMEZONE_ID               = Sql.AddParameter(cmd, "@TIMEZONE_ID"              , gTIMEZONE_ID                );
							IDbDataParameter parSAVE_QUERY                = Sql.AddParameter(cmd, "@SAVE_QUERY"               , bSAVE_QUERY                 );
							IDbDataParameter parGROUP_TABS                = Sql.AddParameter(cmd, "@GROUP_TABS"               , bGROUP_TABS                 );
							IDbDataParameter parSUBPANEL_TABS             = Sql.AddParameter(cmd, "@SUBPANEL_TABS"            , bSUBPANEL_TABS              );
							IDbDataParameter parEXTENSION                 = Sql.AddParameter(cmd, "@EXTENSION"                , sEXTENSION                  ,  25);
							IDbDataParameter parSMS_OPT_IN                = Sql.AddParameter(cmd, "@SMS_OPT_IN"               , sSMS_OPT_IN                 ,  25);
							IDbDataParameter parPICTURE                   = Sql.AddParameter(cmd, "@PICTURE"                  , sPICTURE                    );
							IDbDataParameter parMAIL_SMTPSERVER           = Sql.AddParameter(cmd, "@MAIL_SMTPSERVER"          , sMAIL_SMTPSERVER            , 100);
							IDbDataParameter parMAIL_SMTPPORT             = Sql.AddParameter(cmd, "@MAIL_SMTPPORT"            , nMAIL_SMTPPORT              );
							IDbDataParameter parMAIL_SMTPAUTH_REQ         = Sql.AddParameter(cmd, "@MAIL_SMTPAUTH_REQ"        , bMAIL_SMTPAUTH_REQ          );
							IDbDataParameter parMAIL_SMTPSSL              = Sql.AddParameter(cmd, "@MAIL_SMTPSSL"             , nMAIL_SMTPSSL               );
							IDbDataParameter parMAIL_SENDTYPE             = Sql.AddParameter(cmd, "@MAIL_SENDTYPE"            , sMAIL_SENDTYPE              ,  25);
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
		#endregion

		#region spUSERS_Update
		/// <summary>
		/// spUSERS_Update
		/// </summary>
		public static void spUSERS_Update(ref Guid gID, string sUSER_NAME, string sFIRST_NAME, string sLAST_NAME, Guid gREPORTS_TO_ID, bool bIS_ADMIN, bool bRECEIVE_NOTIFICATIONS, string sDESCRIPTION, string sTITLE, string sDEPARTMENT, string sPHONE_HOME, string sPHONE_MOBILE, string sPHONE_WORK, string sPHONE_OTHER, string sPHONE_FAX, string sEMAIL1, string sEMAIL2, string sSTATUS, string sADDRESS_STREET, string sADDRESS_CITY, string sADDRESS_STATE, string sADDRESS_POSTALCODE, string sADDRESS_COUNTRY, string sUSER_PREFERENCES, bool bPORTAL_ONLY, string sEMPLOYEE_STATUS, string sMESSENGER_ID, string sMESSENGER_TYPE, string sPARENT_TYPE, Guid gPARENT_ID, bool bIS_GROUP, Guid gDEFAULT_TEAM, bool bIS_ADMIN_DELEGATE, string sMAIL_SMTPUSER, string sMAIL_SMTPPASS, bool bSYSTEM_GENERATED_PASSWORD, bool bGOOGLEAPPS_SYNC_CONTACTS, bool bGOOGLEAPPS_SYNC_CALENDAR, string sGOOGLEAPPS_USERNAME, string sGOOGLEAPPS_PASSWORD, string sFACEBOOK_ID, bool bICLOUD_SYNC_CONTACTS, bool bICLOUD_SYNC_CALENDAR, string sICLOUD_USERNAME, string sICLOUD_PASSWORD, string sTHEME, string sDATE_FORMAT, string sTIME_FORMAT, string sLANG, Guid gCURRENCY_ID, Guid gTIMEZONE_ID, bool bSAVE_QUERY, bool bGROUP_TABS, bool bSUBPANEL_TABS, string sEXTENSION, string sSMS_OPT_IN, string sPICTURE, string sMAIL_SMTPSERVER, Int32 nMAIL_SMTPPORT, bool bMAIL_SMTPAUTH_REQ, Int32 nMAIL_SMTPSSL, string sMAIL_SENDTYPE, IDbTransaction trn)
		{
			IDbConnection con = trn.Connection!;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "spUSERS_Update";
				IDbDataParameter parID                        = Sql.AddParameter(cmd, "@ID"                       , gID                         );
				IDbDataParameter parMODIFIED_USER_ID          = Sql.AddParameter(cmd, "@MODIFIED_USER_ID"         ,  _security?.USER_ID ?? Guid.Empty           );
				IDbDataParameter parUSER_NAME                 = Sql.AddParameter(cmd, "@USER_NAME"                , sUSER_NAME                  ,  60);
				IDbDataParameter parFIRST_NAME                = Sql.AddParameter(cmd, "@FIRST_NAME"               , sFIRST_NAME                 ,  30);
				IDbDataParameter parLAST_NAME                 = Sql.AddParameter(cmd, "@LAST_NAME"                , sLAST_NAME                  ,  30);
				IDbDataParameter parREPORTS_TO_ID             = Sql.AddParameter(cmd, "@REPORTS_TO_ID"            , gREPORTS_TO_ID              );
				IDbDataParameter parIS_ADMIN                  = Sql.AddParameter(cmd, "@IS_ADMIN"                 , bIS_ADMIN                   );
				IDbDataParameter parRECEIVE_NOTIFICATIONS     = Sql.AddParameter(cmd, "@RECEIVE_NOTIFICATIONS"    , bRECEIVE_NOTIFICATIONS      );
				IDbDataParameter parDESCRIPTION               = Sql.AddParameter(cmd, "@DESCRIPTION"              , sDESCRIPTION                );
				IDbDataParameter parTITLE                     = Sql.AddParameter(cmd, "@TITLE"                    , sTITLE                      ,  50);
				IDbDataParameter parDEPARTMENT                = Sql.AddParameter(cmd, "@DEPARTMENT"               , sDEPARTMENT                 ,  50);
				IDbDataParameter parPHONE_HOME                = Sql.AddParameter(cmd, "@PHONE_HOME"               , sPHONE_HOME                 ,  50);
				IDbDataParameter parPHONE_MOBILE              = Sql.AddParameter(cmd, "@PHONE_MOBILE"             , sPHONE_MOBILE               ,  50);
				IDbDataParameter parPHONE_WORK                = Sql.AddParameter(cmd, "@PHONE_WORK"               , sPHONE_WORK                 ,  50);
				IDbDataParameter parPHONE_OTHER               = Sql.AddParameter(cmd, "@PHONE_OTHER"              , sPHONE_OTHER                ,  50);
				IDbDataParameter parPHONE_FAX                 = Sql.AddParameter(cmd, "@PHONE_FAX"                , sPHONE_FAX                  ,  50);
				IDbDataParameter parEMAIL1                    = Sql.AddParameter(cmd, "@EMAIL1"                   , sEMAIL1                     , 100);
				IDbDataParameter parEMAIL2                    = Sql.AddParameter(cmd, "@EMAIL2"                   , sEMAIL2                     , 100);
				IDbDataParameter parSTATUS                    = Sql.AddParameter(cmd, "@STATUS"                   , sSTATUS                     ,  25);
				IDbDataParameter parADDRESS_STREET            = Sql.AddParameter(cmd, "@ADDRESS_STREET"           , sADDRESS_STREET             , 150);
				IDbDataParameter parADDRESS_CITY              = Sql.AddParameter(cmd, "@ADDRESS_CITY"             , sADDRESS_CITY               , 100);
				IDbDataParameter parADDRESS_STATE             = Sql.AddParameter(cmd, "@ADDRESS_STATE"            , sADDRESS_STATE              , 100);
				IDbDataParameter parADDRESS_POSTALCODE        = Sql.AddParameter(cmd, "@ADDRESS_POSTALCODE"       , sADDRESS_POSTALCODE         ,   9);
				IDbDataParameter parADDRESS_COUNTRY           = Sql.AddParameter(cmd, "@ADDRESS_COUNTRY"          , sADDRESS_COUNTRY            ,  25);
				IDbDataParameter parUSER_PREFERENCES          = Sql.AddParameter(cmd, "@USER_PREFERENCES"         , sUSER_PREFERENCES           );
				IDbDataParameter parPORTAL_ONLY               = Sql.AddParameter(cmd, "@PORTAL_ONLY"              , bPORTAL_ONLY                );
				IDbDataParameter parEMPLOYEE_STATUS           = Sql.AddParameter(cmd, "@EMPLOYEE_STATUS"          , sEMPLOYEE_STATUS            ,  25);
				IDbDataParameter parMESSENGER_ID              = Sql.AddParameter(cmd, "@MESSENGER_ID"             , sMESSENGER_ID               ,  25);
				IDbDataParameter parMESSENGER_TYPE            = Sql.AddParameter(cmd, "@MESSENGER_TYPE"           , sMESSENGER_TYPE             ,  25);
				IDbDataParameter parPARENT_TYPE               = Sql.AddParameter(cmd, "@PARENT_TYPE"              , sPARENT_TYPE                ,  25);
				IDbDataParameter parPARENT_ID                 = Sql.AddParameter(cmd, "@PARENT_ID"                , gPARENT_ID                  );
				IDbDataParameter parIS_GROUP                  = Sql.AddParameter(cmd, "@IS_GROUP"                 , bIS_GROUP                   );
				IDbDataParameter parDEFAULT_TEAM              = Sql.AddParameter(cmd, "@DEFAULT_TEAM"             , gDEFAULT_TEAM               );
				IDbDataParameter parIS_ADMIN_DELEGATE         = Sql.AddParameter(cmd, "@IS_ADMIN_DELEGATE"        , bIS_ADMIN_DELEGATE          );
				IDbDataParameter parMAIL_SMTPUSER             = Sql.AddParameter(cmd, "@MAIL_SMTPUSER"            , sMAIL_SMTPUSER              ,  60);
				IDbDataParameter parMAIL_SMTPPASS             = Sql.AddParameter(cmd, "@MAIL_SMTPPASS"            , sMAIL_SMTPPASS              , 100);
				IDbDataParameter parSYSTEM_GENERATED_PASSWORD = Sql.AddParameter(cmd, "@SYSTEM_GENERATED_PASSWORD", bSYSTEM_GENERATED_PASSWORD  );
				IDbDataParameter parGOOGLEAPPS_SYNC_CONTACTS  = Sql.AddParameter(cmd, "@GOOGLEAPPS_SYNC_CONTACTS" , bGOOGLEAPPS_SYNC_CONTACTS   );
				IDbDataParameter parGOOGLEAPPS_SYNC_CALENDAR  = Sql.AddParameter(cmd, "@GOOGLEAPPS_SYNC_CALENDAR" , bGOOGLEAPPS_SYNC_CALENDAR   );
				IDbDataParameter parGOOGLEAPPS_USERNAME       = Sql.AddParameter(cmd, "@GOOGLEAPPS_USERNAME"      , sGOOGLEAPPS_USERNAME        , 100);
				IDbDataParameter parGOOGLEAPPS_PASSWORD       = Sql.AddParameter(cmd, "@GOOGLEAPPS_PASSWORD"      , sGOOGLEAPPS_PASSWORD        , 100);
				IDbDataParameter parFACEBOOK_ID               = Sql.AddParameter(cmd, "@FACEBOOK_ID"              , sFACEBOOK_ID                ,  25);
				IDbDataParameter parICLOUD_SYNC_CONTACTS      = Sql.AddParameter(cmd, "@ICLOUD_SYNC_CONTACTS"     , bICLOUD_SYNC_CONTACTS       );
				IDbDataParameter parICLOUD_SYNC_CALENDAR      = Sql.AddParameter(cmd, "@ICLOUD_SYNC_CALENDAR"     , bICLOUD_SYNC_CALENDAR       );
				IDbDataParameter parICLOUD_USERNAME           = Sql.AddParameter(cmd, "@ICLOUD_USERNAME"          , sICLOUD_USERNAME            , 100);
				IDbDataParameter parICLOUD_PASSWORD           = Sql.AddParameter(cmd, "@ICLOUD_PASSWORD"          , sICLOUD_PASSWORD            , 100);
				IDbDataParameter parTHEME                     = Sql.AddParameter(cmd, "@THEME"                    , sTHEME                      ,  25);
				IDbDataParameter parDATE_FORMAT               = Sql.AddParameter(cmd, "@DATE_FORMAT"              , sDATE_FORMAT                ,  25);
				IDbDataParameter parTIME_FORMAT               = Sql.AddParameter(cmd, "@TIME_FORMAT"              , sTIME_FORMAT                ,  25);
				IDbDataParameter parLANG                      = Sql.AddParameter(cmd, "@LANG"                     , sLANG                       ,  10);
				IDbDataParameter parCURRENCY_ID               = Sql.AddParameter(cmd, "@CURRENCY_ID"              , gCURRENCY_ID                );
				IDbDataParameter parTIMEZONE_ID               = Sql.AddParameter(cmd, "@TIMEZONE_ID"              , gTIMEZONE_ID                );
				IDbDataParameter parSAVE_QUERY                = Sql.AddParameter(cmd, "@SAVE_QUERY"               , bSAVE_QUERY                 );
				IDbDataParameter parGROUP_TABS                = Sql.AddParameter(cmd, "@GROUP_TABS"               , bGROUP_TABS                 );
				IDbDataParameter parSUBPANEL_TABS             = Sql.AddParameter(cmd, "@SUBPANEL_TABS"            , bSUBPANEL_TABS              );
				IDbDataParameter parEXTENSION                 = Sql.AddParameter(cmd, "@EXTENSION"                , sEXTENSION                  ,  25);
				IDbDataParameter parSMS_OPT_IN                = Sql.AddParameter(cmd, "@SMS_OPT_IN"               , sSMS_OPT_IN                 ,  25);
				IDbDataParameter parPICTURE                   = Sql.AddParameter(cmd, "@PICTURE"                  , sPICTURE                    );
				IDbDataParameter parMAIL_SMTPSERVER           = Sql.AddParameter(cmd, "@MAIL_SMTPSERVER"          , sMAIL_SMTPSERVER            , 100);
				IDbDataParameter parMAIL_SMTPPORT             = Sql.AddParameter(cmd, "@MAIL_SMTPPORT"            , nMAIL_SMTPPORT              );
				IDbDataParameter parMAIL_SMTPAUTH_REQ         = Sql.AddParameter(cmd, "@MAIL_SMTPAUTH_REQ"        , bMAIL_SMTPAUTH_REQ          );
				IDbDataParameter parMAIL_SMTPSSL              = Sql.AddParameter(cmd, "@MAIL_SMTPSSL"             , nMAIL_SMTPSSL               );
				IDbDataParameter parMAIL_SENDTYPE             = Sql.AddParameter(cmd, "@MAIL_SENDTYPE"            , sMAIL_SENDTYPE              ,  25);
				parID.Direction = ParameterDirection.InputOutput;
				Sql.Trace(cmd);
				cmd.ExecuteNonQuery();
				gID = Sql.ToGuid(parID.Value);
			}
		}
		#endregion

		#region cmdUSERS_Update
		/// <summary>
		/// spUSERS_Update
		/// </summary>
		public static IDbCommand cmdUSERS_Update(IDbConnection con)
		{
			IDbCommand cmd = con.CreateCommand();
			cmd.CommandType = CommandType.StoredProcedure;
			cmd.CommandText = "spUSERS_Update";
			IDbDataParameter parID                        = Sql.CreateParameter(cmd, "@ID"                       , "Guid",  16);
			IDbDataParameter parMODIFIED_USER_ID          = Sql.CreateParameter(cmd, "@MODIFIED_USER_ID"         , "Guid",  16);
			IDbDataParameter parUSER_NAME                 = Sql.CreateParameter(cmd, "@USER_NAME"                , "string",  60);
			IDbDataParameter parFIRST_NAME                = Sql.CreateParameter(cmd, "@FIRST_NAME"               , "string",  30);
			IDbDataParameter parLAST_NAME                 = Sql.CreateParameter(cmd, "@LAST_NAME"                , "string",  30);
			IDbDataParameter parREPORTS_TO_ID             = Sql.CreateParameter(cmd, "@REPORTS_TO_ID"            , "Guid",  16);
			IDbDataParameter parIS_ADMIN                  = Sql.CreateParameter(cmd, "@IS_ADMIN"                 , "bool",   1);
			IDbDataParameter parRECEIVE_NOTIFICATIONS     = Sql.CreateParameter(cmd, "@RECEIVE_NOTIFICATIONS"    , "bool",   1);
			IDbDataParameter parDESCRIPTION               = Sql.CreateParameter(cmd, "@DESCRIPTION"              , "string", 104857600);
			IDbDataParameter parTITLE                     = Sql.CreateParameter(cmd, "@TITLE"                    , "string",  50);
			IDbDataParameter parDEPARTMENT                = Sql.CreateParameter(cmd, "@DEPARTMENT"               , "string",  50);
			IDbDataParameter parPHONE_HOME                = Sql.CreateParameter(cmd, "@PHONE_HOME"               , "string",  50);
			IDbDataParameter parPHONE_MOBILE              = Sql.CreateParameter(cmd, "@PHONE_MOBILE"             , "string",  50);
			IDbDataParameter parPHONE_WORK                = Sql.CreateParameter(cmd, "@PHONE_WORK"               , "string",  50);
			IDbDataParameter parPHONE_OTHER               = Sql.CreateParameter(cmd, "@PHONE_OTHER"              , "string",  50);
			IDbDataParameter parPHONE_FAX                 = Sql.CreateParameter(cmd, "@PHONE_FAX"                , "string",  50);
			IDbDataParameter parEMAIL1                    = Sql.CreateParameter(cmd, "@EMAIL1"                   , "string", 100);
			IDbDataParameter parEMAIL2                    = Sql.CreateParameter(cmd, "@EMAIL2"                   , "string", 100);
			IDbDataParameter parSTATUS                    = Sql.CreateParameter(cmd, "@STATUS"                   , "string",  25);
			IDbDataParameter parADDRESS_STREET            = Sql.CreateParameter(cmd, "@ADDRESS_STREET"           , "string", 150);
			IDbDataParameter parADDRESS_CITY              = Sql.CreateParameter(cmd, "@ADDRESS_CITY"             , "string", 100);
			IDbDataParameter parADDRESS_STATE             = Sql.CreateParameter(cmd, "@ADDRESS_STATE"            , "string", 100);
			IDbDataParameter parADDRESS_POSTALCODE        = Sql.CreateParameter(cmd, "@ADDRESS_POSTALCODE"       , "string",   9);
			IDbDataParameter parADDRESS_COUNTRY           = Sql.CreateParameter(cmd, "@ADDRESS_COUNTRY"          , "string",  25);
			IDbDataParameter parUSER_PREFERENCES          = Sql.CreateParameter(cmd, "@USER_PREFERENCES"         , "string", 104857600);
			IDbDataParameter parPORTAL_ONLY               = Sql.CreateParameter(cmd, "@PORTAL_ONLY"              , "bool",   1);
			IDbDataParameter parEMPLOYEE_STATUS           = Sql.CreateParameter(cmd, "@EMPLOYEE_STATUS"          , "string",  25);
			IDbDataParameter parMESSENGER_ID              = Sql.CreateParameter(cmd, "@MESSENGER_ID"             , "string",  25);
			IDbDataParameter parMESSENGER_TYPE            = Sql.CreateParameter(cmd, "@MESSENGER_TYPE"           , "string",  25);
			IDbDataParameter parPARENT_TYPE               = Sql.CreateParameter(cmd, "@PARENT_TYPE"              , "string",  25);
			IDbDataParameter parPARENT_ID                 = Sql.CreateParameter(cmd, "@PARENT_ID"                , "Guid",  16);
			IDbDataParameter parIS_GROUP                  = Sql.CreateParameter(cmd, "@IS_GROUP"                 , "bool",   1);
			IDbDataParameter parDEFAULT_TEAM              = Sql.CreateParameter(cmd, "@DEFAULT_TEAM"             , "Guid",  16);
			IDbDataParameter parIS_ADMIN_DELEGATE         = Sql.CreateParameter(cmd, "@IS_ADMIN_DELEGATE"        , "bool",   1);
			IDbDataParameter parMAIL_SMTPUSER             = Sql.CreateParameter(cmd, "@MAIL_SMTPUSER"            , "string",  60);
			IDbDataParameter parMAIL_SMTPPASS             = Sql.CreateParameter(cmd, "@MAIL_SMTPPASS"            , "string", 100);
			IDbDataParameter parSYSTEM_GENERATED_PASSWORD = Sql.CreateParameter(cmd, "@SYSTEM_GENERATED_PASSWORD", "bool",   1);
			IDbDataParameter parGOOGLEAPPS_SYNC_CONTACTS  = Sql.CreateParameter(cmd, "@GOOGLEAPPS_SYNC_CONTACTS" , "bool",   1);
			IDbDataParameter parGOOGLEAPPS_SYNC_CALENDAR  = Sql.CreateParameter(cmd, "@GOOGLEAPPS_SYNC_CALENDAR" , "bool",   1);
			IDbDataParameter parGOOGLEAPPS_USERNAME       = Sql.CreateParameter(cmd, "@GOOGLEAPPS_USERNAME"      , "string", 100);
			IDbDataParameter parGOOGLEAPPS_PASSWORD       = Sql.CreateParameter(cmd, "@GOOGLEAPPS_PASSWORD"      , "string", 100);
			IDbDataParameter parFACEBOOK_ID               = Sql.CreateParameter(cmd, "@FACEBOOK_ID"              , "string",  25);
			IDbDataParameter parICLOUD_SYNC_CONTACTS      = Sql.CreateParameter(cmd, "@ICLOUD_SYNC_CONTACTS"     , "bool",   1);
			IDbDataParameter parICLOUD_SYNC_CALENDAR      = Sql.CreateParameter(cmd, "@ICLOUD_SYNC_CALENDAR"     , "bool",   1);
			IDbDataParameter parICLOUD_USERNAME           = Sql.CreateParameter(cmd, "@ICLOUD_USERNAME"          , "string", 100);
			IDbDataParameter parICLOUD_PASSWORD           = Sql.CreateParameter(cmd, "@ICLOUD_PASSWORD"          , "string", 100);
			IDbDataParameter parTHEME                     = Sql.CreateParameter(cmd, "@THEME"                    , "string",  25);
			IDbDataParameter parDATE_FORMAT               = Sql.CreateParameter(cmd, "@DATE_FORMAT"              , "string",  25);
			IDbDataParameter parTIME_FORMAT               = Sql.CreateParameter(cmd, "@TIME_FORMAT"              , "string",  25);
			IDbDataParameter parLANG                      = Sql.CreateParameter(cmd, "@LANG"                     , "string",  10);
			IDbDataParameter parCURRENCY_ID               = Sql.CreateParameter(cmd, "@CURRENCY_ID"              , "Guid",  16);
			IDbDataParameter parTIMEZONE_ID               = Sql.CreateParameter(cmd, "@TIMEZONE_ID"              , "Guid",  16);
			IDbDataParameter parSAVE_QUERY                = Sql.CreateParameter(cmd, "@SAVE_QUERY"               , "bool",   1);
			IDbDataParameter parGROUP_TABS                = Sql.CreateParameter(cmd, "@GROUP_TABS"               , "bool",   1);
			IDbDataParameter parSUBPANEL_TABS             = Sql.CreateParameter(cmd, "@SUBPANEL_TABS"            , "bool",   1);
			IDbDataParameter parEXTENSION                 = Sql.CreateParameter(cmd, "@EXTENSION"                , "string",  25);
			IDbDataParameter parSMS_OPT_IN                = Sql.CreateParameter(cmd, "@SMS_OPT_IN"               , "string",  25);
			IDbDataParameter parPICTURE                   = Sql.CreateParameter(cmd, "@PICTURE"                  , "string", 104857600);
			IDbDataParameter parMAIL_SMTPSERVER           = Sql.CreateParameter(cmd, "@MAIL_SMTPSERVER"          , "string", 100);
			IDbDataParameter parMAIL_SMTPPORT             = Sql.CreateParameter(cmd, "@MAIL_SMTPPORT"            , "Int32",   4);
			IDbDataParameter parMAIL_SMTPAUTH_REQ         = Sql.CreateParameter(cmd, "@MAIL_SMTPAUTH_REQ"        , "bool",   1);
			IDbDataParameter parMAIL_SMTPSSL              = Sql.CreateParameter(cmd, "@MAIL_SMTPSSL"             , "Int32",   4);
			IDbDataParameter parMAIL_SENDTYPE             = Sql.CreateParameter(cmd, "@MAIL_SENDTYPE"            , "string",  25);
			parID.Direction = ParameterDirection.InputOutput;
			return cmd;
		}
		#endregion


		#region Factory
		/// <summary>
		/// Factory: Returns an IDbCommand pre-configured for the named stored procedure.
		/// Cases are limited to the cmd* methods implemented in this partial class.
		/// All other procedure names fall through to SqlProcs.DynamicFactory().
		/// </summary>
		public static IDbCommand Factory(IDbConnection con, string sProcedureName)
		{
			IDbCommand? cmd = null;
			switch ( sProcedureName.ToUpper() )
			{
				case "SPACCOUNTS_UPDATE"   :  cmd = cmdACCOUNTS_Update  (con);  break;
				case "SPCALLS_UPDATE"      :  cmd = cmdCALLS_Update     (con);  break;
				case "SPCONTACTS_UPDATE"   :  cmd = cmdCONTACTS_Update  (con);  break;
				case "SPUSERS_UPDATE"      :  cmd = cmdUSERS_Update     (con);  break;
				// 11/26/2021 Paul.  In order to support dynamically created modules in the React client, we need to load the procedures dynamically.
				default:  cmd = SqlProcs.DynamicFactory(con, sProcedureName);  break;
			}
			// 11/11/2008 Paul.  PostgreSQL has issues treating integers as booleans and booleans as integers.
			if ( Sql.IsPostgreSQL(cmd) )
			{
				foreach ( IDbDataParameter par in cmd.Parameters )
				{
					if ( par.DbType == DbType.Boolean )
						par.DbType = DbType.Int32;
				}
			}
			return cmd!;
		}
		#endregion

	#region spCALLS_USERS_Update
		/// <summary>spCALLS_USERS_Update — links a user to a call invitation.</summary>
		public static void spCALLS_USERS_Update(Guid gCALL_ID, Guid gUSER_ID, bool bREQUIRED, string sACCEPT_STATUS)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCALLS_USERS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parCALL_ID          = Sql.AddParameter(cmd, "@CALL_ID"         , gCALL_ID          );
							IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID          );
							IDbDataParameter parREQUIRED         = Sql.AddParameter(cmd, "@REQUIRED"        , bREQUIRED         );
							IDbDataParameter parACCEPT_STATUS    = Sql.AddParameter(cmd, "@ACCEPT_STATUS"   , sACCEPT_STATUS    ,  25);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spMEETINGS_USERS_Update
		/// <summary>spMEETINGS_USERS_Update — links a user to a meeting invitation.</summary>
		public static void spMEETINGS_USERS_Update(Guid gMEETING_ID, Guid gUSER_ID, bool bREQUIRED, string sACCEPT_STATUS)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spMEETINGS_USERS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parMEETING_ID       = Sql.AddParameter(cmd, "@MEETING_ID"      , gMEETING_ID       );
							IDbDataParameter parUSER_ID          = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID          );
							IDbDataParameter parREQUIRED         = Sql.AddParameter(cmd, "@REQUIRED"        , bREQUIRED         );
							IDbDataParameter parACCEPT_STATUS    = Sql.AddParameter(cmd, "@ACCEPT_STATUS"   , sACCEPT_STATUS    ,  25);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_ACCOUNTS_Update
		/// <summary>spEMAILS_ACCOUNTS_Update — links an email to an account.</summary>
		public static void spEMAILS_ACCOUNTS_Update(Guid gEMAIL_ID, Guid gACCOUNT_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_ACCOUNTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID         );
							IDbDataParameter parACCOUNT_ID       = Sql.AddParameter(cmd, "@ACCOUNT_ID"      , gACCOUNT_ID       );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_LEADS_Update
		/// <summary>spEMAILS_LEADS_Update — links an email to a lead.</summary>
		public static void spEMAILS_LEADS_Update(Guid gEMAIL_ID, Guid gLEAD_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_LEADS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID         );
							IDbDataParameter parLEAD_ID          = Sql.AddParameter(cmd, "@LEAD_ID"         , gLEAD_ID          );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spACCOUNTS_BUGS_Update
		/// <summary>spACCOUNTS_BUGS_Update — links an account to a bug.</summary>
		public static void spACCOUNTS_BUGS_Update(Guid gACCOUNT_ID, Guid gBUG_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spACCOUNTS_BUGS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parACCOUNT_ID       = Sql.AddParameter(cmd, "@ACCOUNT_ID"      , gACCOUNT_ID       );
							IDbDataParameter parBUG_ID           = Sql.AddParameter(cmd, "@BUG_ID"          , gBUG_ID           );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spACCOUNTS_OPPORTUNITIES_Update
		/// <summary>spACCOUNTS_OPPORTUNITIES_Update — links an account to an opportunity.</summary>
		public static void spACCOUNTS_OPPORTUNITIES_Update(Guid gACCOUNT_ID, Guid gOPPORTUNITY_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spACCOUNTS_OPPORTUNITIES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parACCOUNT_ID       = Sql.AddParameter(cmd, "@ACCOUNT_ID"      , gACCOUNT_ID       );
							IDbDataParameter parOPPORTUNITY_ID   = Sql.AddParameter(cmd, "@OPPORTUNITY_ID"  , gOPPORTUNITY_ID   );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spPROJECTS_ACCOUNTS_Update
		/// <summary>spPROJECTS_ACCOUNTS_Update — links a project to an account.</summary>
		public static void spPROJECTS_ACCOUNTS_Update(Guid gPROJECT_ID, Guid gACCOUNT_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spPROJECTS_ACCOUNTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parPROJECT_ID       = Sql.AddParameter(cmd, "@PROJECT_ID"      , gPROJECT_ID       );
							IDbDataParameter parACCOUNT_ID       = Sql.AddParameter(cmd, "@ACCOUNT_ID"      , gACCOUNT_ID       );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spQUOTES_ACCOUNTS_Update
		/// <summary>spQUOTES_ACCOUNTS_Update — links a quote to an account.</summary>
		public static void spQUOTES_ACCOUNTS_Update(Guid gQUOTE_ID, Guid gACCOUNT_ID, string sTEAM_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spQUOTES_ACCOUNTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parQUOTE_ID         = Sql.AddParameter(cmd, "@QUOTE_ID"        , gQUOTE_ID         );
							IDbDataParameter parACCOUNT_ID       = Sql.AddParameter(cmd, "@ACCOUNT_ID"      , gACCOUNT_ID       );
							IDbDataParameter parTEAM_SET_LIST    = Sql.AddParameter(cmd, "@TEAM_SET_LIST"   , sTEAM_SET_LIST    , 8000);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spORDERS_ACCOUNTS_Update
		/// <summary>spORDERS_ACCOUNTS_Update — links an order to an account.</summary>
		public static void spORDERS_ACCOUNTS_Update(Guid gORDER_ID, Guid gACCOUNT_ID, string sTEAM_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spORDERS_ACCOUNTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parORDER_ID         = Sql.AddParameter(cmd, "@ORDER_ID"        , gORDER_ID         );
							IDbDataParameter parACCOUNT_ID       = Sql.AddParameter(cmd, "@ACCOUNT_ID"      , gACCOUNT_ID       );
							IDbDataParameter parTEAM_SET_LIST    = Sql.AddParameter(cmd, "@TEAM_SET_LIST"   , sTEAM_SET_LIST    , 8000);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spORDERS_CONTACTS_Update
		/// <summary>spORDERS_CONTACTS_Update — links an order to a contact.</summary>
		public static void spORDERS_CONTACTS_Update(Guid gORDER_ID, Guid gCONTACT_ID, string sTEAM_SET_LIST)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spORDERS_CONTACTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parORDER_ID         = Sql.AddParameter(cmd, "@ORDER_ID"        , gORDER_ID         );
							IDbDataParameter parCONTACT_ID       = Sql.AddParameter(cmd, "@CONTACT_ID"      , gCONTACT_ID       );
							IDbDataParameter parTEAM_SET_LIST    = Sql.AddParameter(cmd, "@TEAM_SET_LIST"   , sTEAM_SET_LIST    , 8000);
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spTASKS_Delete
		/// <summary>spTASKS_Delete — deletes a task by ID (used by SugarCRM plug-in unsyncing).</summary>
		public static void spTASKS_Delete(Guid gTASK_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spTASKS_Delete";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parID               = Sql.AddParameter(cmd, "@ID"              , gTASK_ID          );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_BUGS_Update
		/// <summary>spEMAILS_BUGS_Update — links an email to a bug.</summary>
		public static void spEMAILS_BUGS_Update(Guid gEMAIL_ID, Guid gBUG_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_BUGS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID         );
							IDbDataParameter parBUG_ID           = Sql.AddParameter(cmd, "@BUG_ID"          , gBUG_ID           );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_CASES_Update
		/// <summary>spEMAILS_CASES_Update — links an email to a case.</summary>
		public static void spEMAILS_CASES_Update(Guid gEMAIL_ID, Guid gCASE_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_CASES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID         );
							IDbDataParameter parCASE_ID          = Sql.AddParameter(cmd, "@CASE_ID"         , gCASE_ID          );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_OPPORTUNITIES_Update
		/// <summary>spEMAILS_OPPORTUNITIES_Update — links an email to an opportunity.</summary>
		public static void spEMAILS_OPPORTUNITIES_Update(Guid gEMAIL_ID, Guid gOPPORTUNITY_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_OPPORTUNITIES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID         );
							IDbDataParameter parOPPORTUNITY_ID   = Sql.AddParameter(cmd, "@OPPORTUNITY_ID"  , gOPPORTUNITY_ID   );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_PROJECTS_Update
		/// <summary>spEMAILS_PROJECTS_Update — links an email to a project.</summary>
		public static void spEMAILS_PROJECTS_Update(Guid gEMAIL_ID, Guid gPROJECT_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_PROJECTS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID         );
							IDbDataParameter parPROJECT_ID       = Sql.AddParameter(cmd, "@PROJECT_ID"      , gPROJECT_ID       );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_QUOTES_Update
		/// <summary>spEMAILS_QUOTES_Update — links an email to a quote.</summary>
		public static void spEMAILS_QUOTES_Update(Guid gEMAIL_ID, Guid gQUOTE_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_QUOTES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID         );
							IDbDataParameter parQUOTE_ID         = Sql.AddParameter(cmd, "@QUOTE_ID"        , gQUOTE_ID         );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_TASKS_Update
		/// <summary>spEMAILS_TASKS_Update — links an email to a task.</summary>
		public static void spEMAILS_TASKS_Update(Guid gEMAIL_ID, Guid gTASK_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_TASKS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID         );
							IDbDataParameter parTASK_ID          = Sql.AddParameter(cmd, "@TASK_ID"         , gTASK_ID          );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spQUOTES_OPPORTUNITIES_Update
		/// <summary>spQUOTES_OPPORTUNITIES_Update — links a quote to an opportunity.</summary>
		public static void spQUOTES_OPPORTUNITIES_Update(Guid gQUOTE_ID, Guid gOPPORTUNITY_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spQUOTES_OPPORTUNITIES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parQUOTE_ID         = Sql.AddParameter(cmd, "@QUOTE_ID"        , gQUOTE_ID         );
							IDbDataParameter parOPPORTUNITY_ID   = Sql.AddParameter(cmd, "@OPPORTUNITY_ID"  , gOPPORTUNITY_ID   );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spPROJECTS_QUOTES_Update
		/// <summary>spPROJECTS_QUOTES_Update — links a project to a quote.</summary>
		public static void spPROJECTS_QUOTES_Update(Guid gPROJECT_ID, Guid gQUOTE_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spPROJECTS_QUOTES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parPROJECT_ID       = Sql.AddParameter(cmd, "@PROJECT_ID"      , gPROJECT_ID       );
							IDbDataParameter parQUOTE_ID         = Sql.AddParameter(cmd, "@QUOTE_ID"        , gQUOTE_ID         );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spEMAILS_ORDERS_Update
		/// <summary>spEMAILS_ORDERS_Update — links an email to an order.</summary>
		public static void spEMAILS_ORDERS_Update(Guid gEMAIL_ID, Guid gORDER_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spEMAILS_ORDERS_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parEMAIL_ID         = Sql.AddParameter(cmd, "@EMAIL_ID"        , gEMAIL_ID         );
							IDbDataParameter parORDER_ID         = Sql.AddParameter(cmd, "@ORDER_ID"        , gORDER_ID         );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spORDERS_OPPORTUNITIES_Update
		/// <summary>spORDERS_OPPORTUNITIES_Update — links an order to an opportunity.</summary>
		public static void spORDERS_OPPORTUNITIES_Update(Guid gORDER_ID, Guid gOPPORTUNITY_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spORDERS_OPPORTUNITIES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parORDER_ID         = Sql.AddParameter(cmd, "@ORDER_ID"        , gORDER_ID         );
							IDbDataParameter parOPPORTUNITY_ID   = Sql.AddParameter(cmd, "@OPPORTUNITY_ID"  , gOPPORTUNITY_ID   );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spCONTRACTS_OPPORTUNITIES_Update
		/// <summary>spCONTRACTS_OPPORTUNITIES_Update — links a contract to an opportunity.</summary>
		public static void spCONTRACTS_OPPORTUNITIES_Update(Guid gCONTRACT_ID, Guid gOPPORTUNITY_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spCONTRACTS_OPPORTUNITIES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parCONTRACT_ID      = Sql.AddParameter(cmd, "@CONTRACT_ID"     , gCONTRACT_ID      );
							IDbDataParameter parOPPORTUNITY_ID   = Sql.AddParameter(cmd, "@OPPORTUNITY_ID"  , gOPPORTUNITY_ID   );
							cmd.ExecuteNonQuery();
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
		#endregion

		#region spPROJECTS_OPPORTUNITIES_Update
		/// <summary>spPROJECTS_OPPORTUNITIES_Update — links a project to an opportunity.</summary>
		public static void spPROJECTS_OPPORTUNITIES_Update(Guid gPROJECT_ID, Guid gOPPORTUNITY_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories!.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction = trn;
							cmd.CommandType = CommandType.StoredProcedure;
							cmd.CommandText = "spPROJECTS_OPPORTUNITIES_Update";
							IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security?.USER_ID ?? Guid.Empty);
							IDbDataParameter parPROJECT_ID       = Sql.AddParameter(cmd, "@PROJECT_ID"      , gPROJECT_ID       );
							IDbDataParameter parOPPORTUNITY_ID   = Sql.AddParameter(cmd, "@OPPORTUNITY_ID"  , gOPPORTUNITY_ID   );
							cmd.ExecuteNonQuery();
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
		#endregion

	}
}
