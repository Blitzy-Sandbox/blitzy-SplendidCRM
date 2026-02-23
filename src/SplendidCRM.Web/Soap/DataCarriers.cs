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
using System;
using System.Xml.Serialization;

namespace SplendidCRM
{
	// Data carrier DTOs extracted from soap.asmx.cs for SoapCore WSDL contract compatibility.
	// All classes preserve original field names, types, constructors, and default values exactly
	// to ensure the WSDL contract is byte-comparable with the original ASMX service.

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class contact_detail
	{
		public string        email_address;
		public string        name1        ;
		public string        name2        ;
		public string        association  ;
		public string        id           ;
		public string        msi_id       ;
		public string        type         ;

		public contact_detail()
		{
			email_address = String.Empty;
			name1         = String.Empty;
			name2         = String.Empty;
			association   = String.Empty;
			id            = String.Empty;
			msi_id        = String.Empty;
			type          = String.Empty;
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class document_revision
	{
		public string        id           ;
		public string        document_name;
		public string        revision     ;
		public string        filename     ;
		public string        file         ;

		public document_revision()
		{
			id            = String.Empty;
			document_name = String.Empty;
			revision      = String.Empty;
			filename      = String.Empty;
			file          = String.Empty;
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class error_value
	{
		public string        number       ;
		public string        name         ;
		public string        description  ;

		public error_value()
		{
			number      = "0";
			name        = "No Error";
			description = "No Error";
		}

		public error_value(string number, string name, string description)
		{
			this.number       = number      ;
			this.name         = name        ;
			this.description  = description ;
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class set_relationship_list_result
	{
		public int           created      ;
		public int           failed       ;
		public error_value   error        ;

		public set_relationship_list_result()
		{
			created = 0;
			failed  = 0;
			error   = new error_value();
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class set_relationship_value
	{
		public string        module1      ;
		public string        module1_id   ;
		public string        module2      ;
		public string        module2_id   ;

		public set_relationship_value()
		{
			module1    = String.Empty;
			module1_id = String.Empty;
			module2    = String.Empty;
			module2_id = String.Empty;
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class id_mod
	{
		public string        id           ;
		public string        date_modified;
		public int           deleted      ;

		public id_mod()
		{
			id            = String.Empty;
			date_modified = String.Empty;
			deleted       = 0;
		}
		public id_mod(string id, string date_modified, int deleted)
		{
			this.id            = id           ;
			this.date_modified = date_modified;
			this.deleted       = deleted      ;
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class get_relationships_result
	{
		public id_mod[]      ids          ;
		public error_value   error        ;

		public get_relationships_result()
		{
			ids   = new id_mod[0];
			error = new error_value();
		}
	}

	// 06/19/2007 Paul.  Starting with version 4.2, SugarCRM uses a function that optimizes syncing. 
	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class get_entry_list_result_encoded
	{
		public int           result_count ;
		public int           next_offset  ;
		public int           total_count  ;
		public string[]      field_list   ;
		public string        entry_list   ;  // Defaults to base64 encoded XML, but can also be PHP encoded. 
		public error_value   error        ;

		public get_entry_list_result_encoded()
		{
			result_count = 0;
			next_offset  = 0;
			total_count  = 0;
			field_list   = new string[0];
			entry_list   = String.Empty;
			error        = new error_value();
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class module_list
	{
		public string[]      modules      ;
		public error_value   error        ;

		public module_list()
		{
			modules = new string[0];
			error   = new error_value();
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class name_value
	{
		public string        name         ;
		public string        value        ;

		public name_value()
		{
			name  = String.Empty;
			value = String.Empty;
		}

		public name_value(string name, string value)
		{
			this.name  = name;
			this.value = value;
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class field
	{
		public string        name         ;
		public string        type         ;
		public string        label        ;
		public int           required     ;
		public name_value[]  options      ;

		public field()
		{
			name     = String.Empty;
			type     = String.Empty;
			label    = String.Empty;
			required = 0;
			options  = new name_value[0];
		}

		public field(string name, string type, string label, int required)
		{
			this.name     = name    ;
			this.type     = type    ;
			this.label    = label   ;
			this.required = required;
			options       = new name_value[0];
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class module_fields
	{
		public string        module_name  ;
		public field[]       module_fields1;
		public error_value   error        ;

		public module_fields()
		{
			module_name    = String.Empty;
			module_fields1 = new field[0];
			error          = new error_value();
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class note_attachment
	{
		public string        id           ;
		public string        filename     ;
		public string        file         ;

		public note_attachment()
		{
			id       = String.Empty;
			filename = String.Empty;
			file     = String.Empty;
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class return_note_attachment
	{
		public note_attachment note_attachment;
		public error_value     error          ;

		public return_note_attachment()
		{
			note_attachment = new note_attachment();
			error           = new error_value();
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class set_entries_result
	{
		public string[]      ids          ;
		public error_value   error        ;

		public set_entries_result()
		{
			ids   = new string[0];
			error = new error_value();
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class entry_value
	{
		public string        id           ;
		public string        module_name  ;
		public name_value[]  name_value_list;

		public entry_value()
		{
			id              = String.Empty;
			module_name     = String.Empty;
			name_value_list = new name_value[0];
		}
		public entry_value(string id, string module_name, string name, string value)
		{
			this.id                 = id;
			this.module_name        = module_name ;
			this.name_value_list    = new name_value[1];
			this.name_value_list[0] = new name_value(name, value);
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class get_entry_result
	{
		public field[]       field_list   ;
		public entry_value[] entry_list   ;
		public error_value   error        ;

		public get_entry_result()
		{
			field_list = new field      [0];
			entry_list = new entry_value[0];
			error      = new error_value();
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class get_entry_list_result
	{
		public int           result_count ;
		public int           next_offset  ;
		public field[]       field_list   ;
		public entry_value[] entry_list   ;
		public error_value   error        ;

		public get_entry_list_result()
		{
			result_count = 0;
			next_offset  = 0;
			field_list   = new field      [0];
			entry_list   = new entry_value[0];
			error        = new error_value();
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class set_entry_result
	{
		public string        id           ;
		public error_value   error        ;

		public set_entry_result()
		{
			id    = String.Empty;
			error = new error_value();
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class user_auth
	{
		public string        user_name    ;
		public string        password     ;
		public string        version      ;

		public user_auth()
		{
			user_name     = String.Empty;
			password      = String.Empty;
			version       = String.Empty;
		}
	}

	[Serializable]
	[XmlType(Namespace = "http://www.sugarcrm.com/sugarcrm")]
	public class user_detail
	{
		public string        email_address;
		public string        user_name    ;
		public string        first_name   ;
		public string        last_name    ;
		public string        department   ;
		public string        id           ;
		public string        title        ;

		public user_detail()
		{
			email_address = String.Empty;
			user_name     = String.Empty;
			first_name    = String.Empty;
			last_name     = String.Empty;
			department    = String.Empty;
			id            = String.Empty;
			title         = String.Empty;
		}
	}
}
