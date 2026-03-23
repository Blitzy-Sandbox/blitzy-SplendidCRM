/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * Any use of the contents of this file are subject to the SplendidCRM Community Source Code License 
 * Agreement, or other written agreement between you and SplendidCRM ("License"). By installing or 
 * using this file, you have unconditionally agreed to the terms and conditions of the License, 
 * including but not limited to restrictions on the number of users therein, and you may not use this 
 * file except in compliance with the License. 
 * 
 * SplendidCRM owns all proprietary rights, including all copyrights, patents, trade secrets, and 
 * trademarks, in and to the contents of this file.  You will not link to or in any way combine the 
 * contents of this file or any derivatives with any Open Source Code in any manner that would require 
 * the contents of this file to be made available to any third party. 
 * 
 * IN NO EVENT SHALL SPLENDIDCRM BE RESPONSIBLE FOR ANY DAMAGES OF ANY KIND, INCLUDING ANY DIRECT, 
 * SPECIAL, PUNITIVE, INDIRECT, INCIDENTAL OR CONSEQUENTIAL DAMAGES.  Other limitations of liability 
 * and disclaimers set forth in the License. 
 * 
 *********************************************************************************************************************/
using System;
using System.Collections.Generic;

namespace SplendidCRM
{
	/// <summary>
	/// Standalone .NET 10 replacement for the WebForms KeySortDropDownList control.
	/// Originally inherited from System.Web.UI.WebControls.DropDownList; migrated to a
	/// standalone class with Dictionary-based Attributes and an int SelectedIndex property,
	/// preserving the JavaScript keyboard-sort event attribute injection logic verbatim.
	/// </summary>
	public class KeySortDropDownList
	{
		/// <summary>
		/// HTML element attributes dictionary, replacing the WebForms AttributeCollection.
		/// Consumers call Attributes["key"] = value or use the Add helper below.
		/// </summary>
		public Dictionary<string, string> Attributes { get; private set; }

		/// <summary>
		/// Index of the currently selected item, mirroring the WebForms DropDownList.SelectedIndex property.
		/// </summary>
		public int SelectedIndex { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="KeySortDropDownList"/> class.
		/// </summary>
		public KeySortDropDownList()
		{
			Attributes = new Dictionary<string, string>();
			SelectedIndex = -1;
		}

		/// <summary>
		/// Injects JavaScript keyboard-sort event handler attributes onto the control.
		/// Mirrors the original WebForms OnPreRender override, preserving all JavaScript
		/// handler strings verbatim for backward-compatible client-side behavior.
		/// </summary>
		/// <param name="e">Event arguments (unused, preserved for signature compatibility).</param>
		public virtual void OnPreRender(EventArgs e)
		{
			string sFireEvent = "if(typeof(event.initEvent)!='undefined'){var evt=document.createEvent('HTMLEvents');evt.initEvent('change',true,true);this.dispatchEvent(evt);}else{this.fireEvent('onchange');}";

			// 06/21/2009 Paul.  We don't use base.OnPreRender because there is no WebForms base class.
			Attributes["onkeypress"] = "KeySortDropDownList_onkeypress(this, false)";
			Attributes["onkeydown" ] = "var code=(typeof(event)!='undefined')?event.keyCode:e.keyCode;if(code==13||code==9){" + sFireEvent + "}else if(code==27){this.selectedIndex=" + SelectedIndex.ToString() + ";" + sFireEvent + "}";
			Attributes["onclick"   ] = "if(this.selectedIndex!=" + SelectedIndex.ToString() + "){" + sFireEvent + "}";
			Attributes["onblur"    ] = "if(this.selectedIndex!=" + SelectedIndex.ToString() + "){" + sFireEvent + "}";
		}
	}
}
