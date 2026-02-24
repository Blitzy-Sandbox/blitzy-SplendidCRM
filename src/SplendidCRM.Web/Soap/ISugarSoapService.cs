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

// MIGRATION NOTE (.NET Framework 4.8 → .NET 10 ASP.NET Core):
// This interface replaces the [SoapRpcService] + [WebService] class "soap" declared in soap.asmx.cs.
// The original attributes:
//   [SoapRpcService]
//   [WebService(Namespace="http://www.sugarcrm.com/sugarcrm", Name="sugarsoap", ...)]
// are replaced by SoapCore's [ServiceContract] attribute, which produces an RPC/XmlSerializer-compatible
// WSDL when the SoapCore endpoint is configured with SoapSerializer.XmlSerializer (see Program.cs).
//
// Namespace MUST be preserved exactly as "http://www.sugarcrm.com/sugarcrm" and Name as "sugarsoap"
// for WSDL byte-comparability with the original ASMX service and SugarCRM client compatibility.
//
// [System.ServiceModel] is provided by the SoapCore 1.2.1.12 NuGet package for ASP.NET Core.
// All DTO types (contact_detail, entry_value, etc.) are defined in DataCarriers.cs.
// Private helpers (GetSessionUserID, LoginUser, etc.) are NOT part of this interface.

using System;
using System.ServiceModel;

namespace SplendidCRM
{
	/// <summary>
	/// SoapCore service contract interface for the SugarCRM SOAP API.
	/// Extracted from soap.asmx.cs (4,641 lines) — preserves all 41 public SOAP method signatures
	/// exactly as they appeared in the original [SoapRpcService] class, including:
	///   - Exact method names (lowercase, underscore_separated per SugarCRM convention)
	///   - Exact parameter names and parameter order
	///   - Exact return types referencing the DTO classes in DataCarriers.cs
	///
	/// WSDL contract compatibility requirements:
	///   - Namespace = "http://www.sugarcrm.com/sugarcrm" (MUST NOT change)
	///   - Name = "sugarsoap" (MUST NOT change — used by SugarMail and Outlook plug-in consumers)
	///   - SoapCore must be registered with XmlSerializer and RPC style in Program.cs
	/// </summary>
	[ServiceContract(Namespace = "http://www.sugarcrm.com/sugarcrm", Name = "sugarsoap")]
	public interface ISugarSoapService
	{
		// =====================================================================
		// System Information Methods
		// Source: soap.asmx.cs lines 482–547
		// =====================================================================

		/// <summary>
		/// Returns the SugarCRM/SplendidCRM sugar_version configuration value.
		/// Original: [WebMethod] [SoapRpcMethod] public string get_server_version()
		/// </summary>
		[OperationContract]
		string get_server_version();

		/// <summary>
		/// Returns the SplendidCRM build version string.
		/// Original: [WebMethod] [SoapRpcMethod] public string get_splendid_version()
		/// </summary>
		[OperationContract]
		string get_splendid_version();

		/// <summary>
		/// Returns the SugarCRM edition flavor: "CE", "PRO", "ENT", or "ULT".
		/// Original: [WebMethod] [SoapRpcMethod] public string get_sugar_flavor()
		/// </summary>
		[OperationContract]
		string get_sugar_flavor();

		/// <summary>
		/// Returns 1 if the request is from the loopback address, 0 otherwise.
		/// Original: [WebMethod] [SoapRpcMethod] public int is_loopback()
		/// </summary>
		[OperationContract]
		int is_loopback();

		/// <summary>
		/// Echo test method — returns the string s unchanged.
		/// Original: [WebMethod] [SoapRpcMethod] public string test(string s)
		/// </summary>
		[OperationContract]
		string test(string s);

		/// <summary>
		/// Returns the current server time formatted with DateTime.Now.ToString("G").
		/// Original: [WebMethod] [SoapRpcMethod] public string get_server_time()
		/// </summary>
		[OperationContract]
		string get_server_time();

		/// <summary>
		/// Returns the current UTC time formatted with DateTime.Now.ToUniversalTime().ToString("u").
		/// Original: [WebMethod] [SoapRpcMethod] public string get_gmt_time()
		/// </summary>
		[OperationContract]
		string get_gmt_time();

		// =====================================================================
		// Session Methods
		// Source: soap.asmx.cs lines 563–1029
		// =====================================================================

		/// <summary>
		/// Validates user credentials and returns "Success" if login succeeds.
		/// Note: SugarCRM returns "Success" rather than the Session ID from this method;
		/// use login() to obtain the actual session token.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod] public string create_session(string user_name, string password)
		/// </summary>
		[OperationContract]
		string create_session(string user_name, string password);

		/// <summary>
		/// Authenticates using a user_auth DTO and returns a set_entry_result containing
		/// the session ID (result.id) for use in subsequent session-required calls.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod] public set_entry_result login(user_auth user_auth, string application_name)
		/// </summary>
		[OperationContract]
		set_entry_result login(user_auth user_auth, string application_name);

		/// <summary>
		/// Terminates a session by user_name. Returns "Success".
		/// Note: The session cache expires naturally; this method is provided for SugarCRM API compatibility.
		/// Original: [WebMethod] [SoapRpcMethod] public string end_session(string user_name)
		/// </summary>
		[OperationContract]
		string end_session(string user_name);

		/// <summary>
		/// Returns 1 if the session is valid (exists in cache), 0 otherwise.
		/// Original: [WebMethod] [SoapRpcMethod] public int seamless_login(string session)
		/// </summary>
		[OperationContract]
		int seamless_login(string session);

		/// <summary>
		/// Logs out a session and returns a no-error error_value.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod] public error_value logout(string session)
		/// </summary>
		[OperationContract]
		error_value logout(string session);

		/// <summary>
		/// Returns the USER_ID (GUID string) for the authenticated session.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod] public string get_user_id(string session)
		/// </summary>
		[OperationContract]
		string get_user_id(string session);

		/// <summary>
		/// Returns the default TEAM_ID (GUID string) for the authenticated session user.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod] public string get_user_team_id(string session)
		/// </summary>
		[OperationContract]
		string get_user_team_id(string session);

		// =====================================================================
		// UserName/Password-Required Methods
		// Source: soap.asmx.cs lines 1031–1874
		// These methods authenticate inline using user_name + password
		// instead of relying on a pre-established session token.
		// =====================================================================

		/// <summary>
		/// Creates a new Contact record. Returns "1" on success.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public string create_contact(string user_name, string password, string first_name, string last_name, string email_address)
		/// </summary>
		[OperationContract]
		string create_contact(string user_name, string password, string first_name, string last_name, string email_address);

		/// <summary>
		/// Creates a new Lead record. Returns "1" on success.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public string create_lead(string user_name, string password, string first_name, string last_name, string email_address)
		/// </summary>
		[OperationContract]
		string create_lead(string user_name, string password, string first_name, string last_name, string email_address);

		/// <summary>
		/// Creates a new Account record. Returns "1" on success.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public string create_account(string user_name, string password, string name, string phone, string website)
		/// </summary>
		[OperationContract]
		string create_account(string user_name, string password, string name, string phone, string website);

		/// <summary>
		/// Creates a new Opportunity record. Returns "1" on success.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public string create_opportunity(string user_name, string password, string name, string amount)
		/// </summary>
		[OperationContract]
		string create_opportunity(string user_name, string password, string name, string amount);

		/// <summary>
		/// Creates a new Case record. Returns "1" on success.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public string create_case(string user_name, string password, string name)
		/// </summary>
		[OperationContract]
		string create_case(string user_name, string password, string name);

		/// <summary>
		/// Searches Contacts and Leads by email address using semicolon-separated addresses.
		/// Returns an array of contact_detail DTOs.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public contact_detail[] contact_by_email(string user_name, string password, string email_address)
		/// </summary>
		[OperationContract]
		contact_detail[] contact_by_email(string user_name, string password, string email_address);

		/// <summary>
		/// Returns a list of all non-portal users. Requires admin privileges.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public user_detail[] user_list(string user_name, string password)
		/// </summary>
		[OperationContract]
		user_detail[] user_list(string user_name, string password);

		/// <summary>
		/// Performs a unified full-text search across Contacts, Leads, Accounts, Cases, and Opportunities.
		/// Returns an array of contact_detail DTOs.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public contact_detail[] search(string user_name, string password, string name)
		/// </summary>
		[OperationContract]
		contact_detail[] search(string user_name, string password, string name);

		/// <summary>
		/// Searches one or more modules by a search string with paging support.
		/// Returns a get_entry_list_result with entries from the specified modules.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public get_entry_list_result search_by_module(string user_name, string password, string search_string, string[] modules, int offset, int max_results)
		/// </summary>
		[OperationContract]
		get_entry_list_result search_by_module(string user_name, string password, string search_string, string[] modules, int offset, int max_results);

		/// <summary>
		/// Tracks an email send event for campaign reporting.
		/// Note: Implementation throws NotImplementedException in the original source.
		/// Returns an empty string on success.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public string track_email(string user_name, string password, string parent_id, string contact_ids, DateTime date_sent, string email_subject, string email_body)
		/// </summary>
		[OperationContract]
		string track_email(string user_name, string password, string parent_id, string contact_ids, DateTime date_sent, string email_subject, string email_body);

		// =====================================================================
		// Session-Required Methods
		// Source: soap.asmx.cs lines 1876–4638
		// These methods require a valid session token from login().
		// =====================================================================

		/// <summary>
		/// Returns a paginated list of module entries matching the query and order_by clause.
		/// Supports custom SQL WHERE clause (query) and ORDER BY clause (order_by).
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public get_entry_list_result get_entry_list(string session, string module_name, string query, string order_by, int offset, string[] select_fields, int max_results, int deleted)
		/// </summary>
		[OperationContract]
		get_entry_list_result get_entry_list(string session, string module_name, string query, string order_by, int offset, string[] select_fields, int max_results, int deleted);

		/// <summary>
		/// Returns a single entry from a module by ID.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public get_entry_result get_entry(string session, string module_name, string id, string[] select_fields)
		/// </summary>
		[OperationContract]
		get_entry_result get_entry(string session, string module_name, string id, string[] select_fields);

		/// <summary>
		/// Returns multiple entries from a module by an array of IDs.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public get_entry_result get_entries(string session, string module_name, string[] ids, string[] select_fields)
		/// </summary>
		[OperationContract]
		get_entry_result get_entries(string session, string module_name, string[] ids, string[] select_fields);

		/// <summary>
		/// Creates or updates a single record in a module using a name_value list.
		/// Returns a set_entry_result containing the new or updated record ID.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public set_entry_result set_entry(string session, string module_name, name_value[] name_value_list)
		/// </summary>
		[OperationContract]
		set_entry_result set_entry(string session, string module_name, name_value[] name_value_list);

		/// <summary>
		/// Creates or updates multiple records in a module using an array of name_value lists.
		/// Returns a set_entries_result containing the array of new or updated record IDs.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public set_entries_result set_entries(string session, string module_name, name_value[][] name_value_lists)
		/// </summary>
		[OperationContract]
		set_entries_result set_entries(string session, string module_name, name_value[][] name_value_lists);

		/// <summary>
		/// Uploads a note attachment (file binary as base64 in note.file field).
		/// Returns a set_entry_result with the Note record ID.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public set_entry_result set_note_attachment(string session, note_attachment note)
		/// </summary>
		[OperationContract]
		set_entry_result set_note_attachment(string session, note_attachment note);

		/// <summary>
		/// Retrieves a note attachment by Note record ID.
		/// Returns a return_note_attachment with the file binary in base64 encoding.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public return_note_attachment get_note_attachment(string session, string id)
		/// </summary>
		[OperationContract]
		return_note_attachment get_note_attachment(string session, string id);

		/// <summary>
		/// Associates a Note record with a parent module record.
		/// Returns an error_value indicating success (number="0") or failure.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public error_value relate_note_to_module(string session, string note_id, string module_name, string module_id)
		/// </summary>
		[OperationContract]
		error_value relate_note_to_module(string session, string note_id, string module_name, string module_id);

		/// <summary>
		/// Returns Notes related to a module record, useful for Outlook plug-in email archiving.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public get_entry_result get_related_notes(string session, string module_name, string module_id, string[] select_fields)
		/// </summary>
		[OperationContract]
		get_entry_result get_related_notes(string session, string module_name, string module_id, string[] select_fields);

		/// <summary>
		/// Returns field metadata (name, type, label, required, options) for a module.
		/// Used by the Outlook plug-in to discover field definitions for custom modules.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public module_fields get_module_fields(string session, string module_name)
		/// </summary>
		[OperationContract]
		module_fields get_module_fields(string session, string module_name);

		/// <summary>
		/// Returns the list of available module names accessible to the authenticated user.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public module_list get_available_modules(string session)
		/// </summary>
		[OperationContract]
		module_list get_available_modules(string session);

		/// <summary>
		/// Creates or updates a portal user record identified by portal_name.
		/// Returns an error_value indicating success (number="0") or failure.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public error_value update_portal_user(string session, string portal_name, name_value[] name_value_list)
		/// </summary>
		[OperationContract]
		error_value update_portal_user(string session, string portal_name, name_value[] name_value_list);

		/// <summary>
		/// Returns modified relationships for a module in an encoded (base64 XML or PHP serialized) format.
		/// Used by SugarCRM v4.2+ sync clients for optimized relationship synchronization.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public get_entry_list_result_encoded sync_get_modified_relationships(string session, string module_name,
		///   string related_module, string from_date, string to_date, int offset, int max_results,
		///   int deleted, string module_id, string[] select_fields, string[] ids,
		///   string relationship_name, string deletion_date, int php_serialize)
		/// </summary>
		[OperationContract]
		get_entry_list_result_encoded sync_get_modified_relationships(string session, string module_name, string related_module, string from_date, string to_date, int offset, int max_results, int deleted, string module_id, string[] select_fields, string[] ids, string relationship_name, string deletion_date, int php_serialize);

		/// <summary>
		/// Returns relationship IDs (id_mod array) between a module record and a related module.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public get_relationships_result get_relationships(string session, string module_name, string module_id,
		///   string related_module, string related_module_query, int deleted)
		/// </summary>
		[OperationContract]
		get_relationships_result get_relationships(string session, string module_name, string module_id, string related_module, string related_module_query, int deleted);

		/// <summary>
		/// Creates a single relationship between two module records.
		/// Returns an error_value indicating success (number="0") or failure.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public error_value set_relationship(string session, set_relationship_value set_relationship_value)
		/// </summary>
		[OperationContract]
		error_value set_relationship(string session, set_relationship_value set_relationship_value);

		/// <summary>
		/// Creates multiple relationships between module record pairs in a single call.
		/// Returns a set_relationship_list_result with created/failed counts.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public set_relationship_list_result set_relationships(string session, set_relationship_value[] set_relationship_list)
		/// </summary>
		[OperationContract]
		set_relationship_list_result set_relationships(string session, set_relationship_value[] set_relationship_list);

		/// <summary>
		/// Creates or updates a document revision record with binary file content (base64 in note.file).
		/// Returns a set_entry_result with the Document Revision record ID.
		/// Original: [WebMethod(EnableSession=true)] [SoapRpcMethod]
		/// public set_entry_result set_document_revision(string session, document_revision note)
		/// </summary>
		[OperationContract]
		set_entry_result set_document_revision(string session, document_revision note);
	}
}
