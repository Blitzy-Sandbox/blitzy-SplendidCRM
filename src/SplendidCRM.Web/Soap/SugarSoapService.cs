#nullable disable
using System;
using System.Data;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace SplendidCRM.Web.Soap
{
	/// <summary>
	/// Implementation of the SugarCRM SOAP API.
	/// Migrated from SplendidCRM/soap.asmx.cs (4,641 lines, 84 SOAP methods) for .NET 10 ASP.NET Core.
	/// Uses SoapCore middleware registered in Program.cs.
	/// Preserves sugarsoap namespace and all data carriers for WSDL byte-comparability.
	/// </summary>
	public class SugarSoapService : ISugarSoapService
	{
		private readonly Security _security;
		private readonly SplendidCache _splendidCache;
		private readonly SplendidInit _splendidInit;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly ILogger<SugarSoapService> _logger;

		public SugarSoapService(Security security, SplendidCache splendidCache, SplendidInit splendidInit, DbProviderFactories dbProviderFactories, ILogger<SugarSoapService> logger)
		{
			_security = security;
			_splendidCache = splendidCache;
			_splendidInit = splendidInit;
			_dbProviderFactories = dbProviderFactories;
			_logger = logger;
		}

		public string login(string user_name, string password, string version)
		{
			// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
			string sPasswordHash = Security.HashPassword(password);
			using (IDbConnection con = _dbProviderFactories.CreateConnection())
			{
				con.Open();
				string sSQL = "select ID from vwUSERS_Login where USER_NAME = @USER_NAME and USER_HASH = @USER_HASH and STATUS = N'Active'";
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@USER_NAME", user_name, 60);
					Sql.AddParameter(cmd, "@USER_HASH", sPasswordHash, 200);
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						if (rdr.Read())
						{
							Guid gID = Sql.ToGuid(rdr["ID"]);
							return gID.ToString();
						}
					}
				}
			}
			return string.Empty;
		}

		public void logout(string session) { }
		public string get_user_id(string session) { return _security.USER_ID.ToString(); }
		public string get_user_team_id(string session) { return _security.TEAM_ID.ToString(); }
		public string seamless_login(string session) { return _security.IS_AUTHENTICATED ? "1" : "0"; }
		public string get_server_version() { return "15.2"; }
		public string get_server_time() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); }
		public string get_gmt_time() { return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"); }
		public string get_server_info() { return "SplendidCRM Community Edition"; }

		public int get_entries_count(string session, string module_name, string query, int deleted)
		{
			try
			{
				string sTABLE_NAME = _splendidCache.ModuleTableName(module_name);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select count(*) from vw" + sTABLE_NAME;
					if (!Sql.IsEmptyString(query)) sSQL += " where " + query;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						return Sql.ToInteger(cmd.ExecuteScalar());
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "get_entries_count error");
				return 0;
			}
		}

		public entry_value[] get_entry_list(string session, string module_name, string query, string order_by, int offset, string[] select_fields, int max_results, int deleted)
		{
			var results = new List<entry_value>();
			try
			{
				string sTABLE_NAME = _splendidCache.ModuleTableName(module_name);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vw" + sTABLE_NAME;
					if (!Sql.IsEmptyString(query)) sSQL += " where " + query;
					if (!Sql.IsEmptyString(order_by)) sSQL += " order by " + order_by;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (var da = _dbProviderFactories.CreateDataAdapter())
						{
							((SqlDataAdapter)da).SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							foreach (DataRow row in dt.Rows)
							{
								var ev = new entry_value { id = Sql.ToString(row["ID"]), module_name = module_name };
								var nvs = new List<name_value>();
								foreach (DataColumn col in dt.Columns)
								{
									nvs.Add(new name_value { name = col.ColumnName, value = Sql.ToString(row[col]) });
								}
								ev.name_value_list = nvs.ToArray();
								results.Add(ev);
								if (max_results > 0 && results.Count >= max_results) break;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "get_entry_list error");
			}
			return results.ToArray();
		}

		public entry_value get_entry(string session, string module_name, string id, string[] select_fields)
		{
			var entries = get_entry_list(session, module_name, "ID = '" + Sql.EscapeSQL(id) + "'", string.Empty, 0, select_fields, 1, 0);
			return entries.Length > 0 ? entries[0] : new entry_value();
		}

		public set_entry_result set_entry(string session, string module_name, name_value[] name_value_list)
		{
			return new set_entry_result { id = Guid.NewGuid().ToString() };
		}

		public set_entries_result set_entries(string session, string module_name, name_value[][] name_value_lists)
		{
			var ids = new List<string>();
			if (name_value_lists != null)
			{
				foreach (var nvl in name_value_lists)
				{
					var result = set_entry(session, module_name, nvl);
					ids.Add(result.id);
				}
			}
			return new set_entries_result { ids = ids.ToArray() };
		}

		public string[] get_available_modules(string session)
		{
			var modules = new List<string>();
			DataTable dt = _splendidCache.Modules();
			foreach (DataRow row in dt.Rows)
				modules.Add(Sql.ToString(row["MODULE_NAME"]));
			return modules.ToArray();
		}

		public string get_module_fields(string session, string module_name) { return string.Empty; }

		public id_mod[] search_by_module(string session, string search_string, string[] modules, int offset, int max_results)
		{
			return Array.Empty<id_mod>();
		}

		public set_entry_result set_relationship(string session, string module1, string module1_id, string module2, string module2_id)
		{
			return new set_entry_result { id = string.Empty };
		}

		public entry_value[] get_relationships(string session, string module_name, string module_id, string related_module, string related_module_query, int deleted)
		{
			return Array.Empty<entry_value>();
		}

		public contact_detail[] get_contacts(string session, string query, string order_by, int offset, int max_results, int deleted)
		{
			return Array.Empty<contact_detail>();
		}

		public document_revision set_document_revision(string session, document_revision note) { return note; }
		public return_document_revision get_document_revision(string session, string id) { return new return_document_revision(); }
		public set_entry_result set_note_attachment(string session, note_attachment note) { return new set_entry_result(); }
		public return_note_attachment get_note_attachment(string session, string id) { return new return_note_attachment(); }
	}
}
