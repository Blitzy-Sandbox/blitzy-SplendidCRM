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
		[OperationContract] string login(string user_name, string password, string version);
		[OperationContract] void logout(string session);
		[OperationContract] string get_user_id(string session);
		[OperationContract] string get_user_team_id(string session);
		[OperationContract] string seamless_login(string session);
		[OperationContract] string get_server_version();
		[OperationContract] string get_server_time();
		[OperationContract] string get_gmt_time();
		[OperationContract] string get_server_info();
		[OperationContract] int get_entries_count(string session, string module_name, string query, int deleted);
		[OperationContract] entry_value[] get_entry_list(string session, string module_name, string query, string order_by, int offset, string[] select_fields, int max_results, int deleted);
		[OperationContract] entry_value get_entry(string session, string module_name, string id, string[] select_fields);
		[OperationContract] set_entry_result set_entry(string session, string module_name, name_value[] name_value_list);
		[OperationContract] set_entries_result set_entries(string session, string module_name, name_value[][] name_value_lists);
		[OperationContract] string[] get_available_modules(string session);
		[OperationContract] string get_module_fields(string session, string module_name);
		[OperationContract] id_mod[] search_by_module(string session, string search_string, string[] modules, int offset, int max_results);
		[OperationContract] set_entry_result set_relationship(string session, string module1, string module1_id, string module2, string module2_id);
		[OperationContract] entry_value[] get_relationships(string session, string module_name, string module_id, string related_module, string related_module_query, int deleted);
		[OperationContract] contact_detail[] get_contacts(string session, string query, string order_by, int offset, int max_results, int deleted);
		[OperationContract] document_revision set_document_revision(string session, document_revision note);
		[OperationContract] return_document_revision get_document_revision(string session, string id);
		[OperationContract] set_entry_result set_note_attachment(string session, note_attachment note);
		[OperationContract] return_note_attachment get_note_attachment(string session, string id);
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
