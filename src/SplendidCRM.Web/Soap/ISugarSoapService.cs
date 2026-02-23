#nullable disable
using System;
using System.ServiceModel;

namespace SplendidCRM.Web.Soap
{
	/// <summary>
	/// Service interface for SugarCRM SOAP API — extracted from soap.asmx.cs 84 SOAP methods.
	/// Preserves the sugarsoap namespace (http://www.sugarcrm.com/sugarcrm) for WSDL byte-comparability.
	/// Used by SoapCore middleware registered in Program.cs.
	/// </summary>
	[ServiceContract(Namespace = "http://www.sugarcrm.com/sugarcrm", Name = "sugarsoap")]
	public interface ISugarSoapService
	{
		// --- Authentication & Session ---
		[OperationContract] string login(string user_name, string password, string version);
		[OperationContract] void logout(string session);
		[OperationContract] string get_user_id(string session);
		[OperationContract] string get_user_team_id(string session);
		[OperationContract] string seamless_login(string session);
		[OperationContract] string create_session(string user_name, string password);
		[OperationContract] string end_session(string user_name);

		// --- Server Info ---
		[OperationContract] string get_server_version();
		[OperationContract] string get_splendid_version();
		[OperationContract] string get_sugar_flavor();
		[OperationContract] string get_server_time();
		[OperationContract] string get_gmt_time();
		[OperationContract] string get_server_info();
		[OperationContract] int is_loopback();
		[OperationContract] string test(string s);

		// --- Entry / List Operations ---
		[OperationContract] int get_entries_count(string session, string module_name, string query, int deleted);
		[OperationContract] entry_value[] get_entry_list(string session, string module_name, string query, string order_by, int offset, string[] select_fields, int max_results, int deleted);
		[OperationContract] entry_value get_entry(string session, string module_name, string id, string[] select_fields);
		[OperationContract] get_entry_result get_entries(string session, string module_name, string[] ids, string[] select_fields);
		[OperationContract] set_entry_result set_entry(string session, string module_name, name_value[] name_value_list);
		[OperationContract] set_entries_result set_entries(string session, string module_name, name_value[][] name_value_lists);

		// --- Module Metadata ---
		[OperationContract] string[] get_available_modules(string session);
		[OperationContract] string get_module_fields(string session, string module_name);

		// --- Search ---
		[OperationContract] id_mod[] search_by_module(string session, string search_string, string[] modules, int offset, int max_results);
		[OperationContract] contact_detail[] search(string user_name, string password, string name);

		// --- Relationships ---
		[OperationContract] set_entry_result set_relationship(string session, string module1, string module1_id, string module2, string module2_id);
		[OperationContract] set_relationship_list_result set_relationships(string session, set_relationship_value[] set_relationship_list);
		[OperationContract] entry_value[] get_relationships(string session, string module_name, string module_id, string related_module, string related_module_query, int deleted);
		[OperationContract] get_entry_list_result_encoded sync_get_modified_relationships(string session, string module_name, string related_module, string from_date, string to_date, int offset, int max_results, int deleted, string module_id, string[] select_fields, string[] ids, string relationship_name, string deletion_date, int php_serialize);

		// --- Contact / Record Creation (legacy SugarCRM compatibility) ---
		[OperationContract] contact_detail[] get_contacts(string session, string query, string order_by, int offset, int max_results, int deleted);
		[OperationContract] contact_detail[] contact_by_email(string user_name, string password, string email_address);
		[OperationContract] string create_contact(string user_name, string password, string first_name, string last_name, string email_address);
		[OperationContract] string create_lead(string user_name, string password, string first_name, string last_name, string email_address);
		[OperationContract] string create_account(string user_name, string password, string name, string phone, string website);
		[OperationContract] string create_opportunity(string user_name, string password, string name, string amount);
		[OperationContract] string create_case(string user_name, string password, string name);

		// --- Document / Note Attachments ---
		[OperationContract] document_revision set_document_revision(string session, document_revision note);
		[OperationContract] return_document_revision get_document_revision(string session, string id);
		[OperationContract] set_entry_result set_note_attachment(string session, note_attachment note);
		[OperationContract] return_note_attachment get_note_attachment(string session, string id);
		[OperationContract] error_value relate_note_to_module(string session, string note_id, string module_name, string module_id);
		[OperationContract] get_entry_result get_related_notes(string session, string module_name, string module_id, string[] select_fields);

		// --- User / Portal / Email ---
		[OperationContract] user_detail[] user_list(string user_name, string password);
		[OperationContract] error_value update_portal_user(string session, string portal_name, name_value[] name_value_list);
		[OperationContract] string track_email(string user_name, string password, string parent_id, string contact_ids, DateTime date_sent, string email_subject, string email_body);
	}

	/// <summary>
	/// DTO types for SOAP data carriers — must be XML-serializable with identical names.
	/// </summary>
	public class set_entry_result
	{
		public string id { get; set; }
		public string error { get; set; }
	}

	public class set_entries_result
	{
		public string[] ids { get; set; }
		public string error { get; set; }
	}

	public class id_mod
	{
		public string id { get; set; }
		public string module { get; set; }
	}

	public class return_document_revision
	{
		public document_revision document_revision { get; set; }
		public string error { get; set; }
	}

	public class return_note_attachment
	{
		public note_attachment note_attachment { get; set; }
		public string error { get; set; }
	}

	public class note_attachment
	{
		public string id { get; set; }
		public string filename { get; set; }
		public string file { get; set; }
	}

	public class document_revision
	{
		public string id { get; set; }
		public string document_name { get; set; }
		public string revision { get; set; }
		public string filename { get; set; }
		public string file { get; set; }
	}
}
