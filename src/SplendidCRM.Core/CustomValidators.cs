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
// .NET 10 Migration: SplendidCRM/_code/CustomValidators.cs → src/SplendidCRM.Core/CustomValidators.cs
// Changes applied:
//   - REMOVED: using System.Web.UI; using System.Web.UI.HtmlControls; using System.Web.UI.WebControls;
//              using SplendidCRM._controls; (all WebForms namespaces and _controls reference)
//   - ADDED:   Internal stub base types replacing System.Web.UI.WebControls.BaseValidator,
//              System.Web.UI.WebControls.Control, ListControl, DropDownList, TextBox,
//              HiddenField, HtmlInputHidden, and all SplendidCRM._controls custom control types
//              (DatePicker, TeamSelect, UserSelect, NAICSCodeSelect, TagSelect, KBTagSelect,
//              RelatedSelect) so that the file compiles under net10.0 without System.Web.
//   - CHANGED: ControlPropertiesValid() and EvaluateIsValid() visibility from protected override
//              to public override to satisfy the .NET 10 class library's members_exposed contract.
//   - CHANGED: typeof(System.Web.UI.HtmlControls.HtmlInputHidden) →
//              typeof(HtmlInputHidden) (stub type in SplendidCRM namespace)
//   - CHANGED: typeof(System.Web.UI.WebControls.HiddenField) →
//              typeof(HiddenField) (stub type in SplendidCRM namespace)
//   - PRESERVED: All 13 validator class definitions, their validation logic, and original comments
//   - NOTE: These validators are WebForms-specific and are preserved for Enterprise Edition
//           upgrade path and backward compatibility. They are not instantiated at runtime in the
//           .NET 10 ASP.NET Core host. Validation now happens in ASP.NET Core API controllers.
#nullable disable
using System;

namespace SplendidCRM
{
	// ====================================================================================
	// .NET 10 Migration: WebForms control stub types
	// These stubs replace System.Web.UI.Control, System.Web.UI.WebControls.*,
	// System.Web.UI.HtmlControls.*, and SplendidCRM._controls.* which do not exist in
	// .NET 10 ASP.NET Core. They provide the minimum surface area required for the 13
	// validator classes below to compile and preserve their original validation logic.
	// ====================================================================================

	/// <summary>
	/// Stub replacing System.Web.UI.Control for .NET 10 compilation compatibility.
	/// Provides ID, NamingContainer property, and FindControl() method used by validators.
	/// </summary>
	public abstract class Control
	{
		/// <summary>Control identifier, mirrors System.Web.UI.Control.ID.</summary>
		public string ID { get; set; }

		/// <summary>
		/// Naming container parent control. In WebForms this walks up the control tree to
		/// the nearest INamingContainer. In this stub it always returns null — validators
		/// that use NamingContainer (TeamSelect, UserSelect, etc.) will return false from
		/// ControlPropertiesValid() which is the correct safe default when no control tree exists.
		/// </summary>
		public virtual Control NamingContainer => null;

		/// <summary>
		/// Finds a child control by ID. In WebForms this walks the control tree.
		/// In this stub it always returns null — validators that use FindControl will
		/// return false from ControlPropertiesValid(), which is the correct safe default.
		/// </summary>
		public virtual Control FindControl(string id) => null;
	}

	/// <summary>
	/// Stub replacing System.Web.UI.WebControls.ListControl for .NET 10 compatibility.
	/// Provides SelectedIndex used by RequiredFieldValidatorForCheckBoxLists.
	/// </summary>
	public abstract class ListControl : Control
	{
		/// <summary>Zero-based index of selected item; -1 if nothing is selected.</summary>
		public int SelectedIndex { get; set; } = -1;

		/// <summary>Value of the selected list item.</summary>
		public string SelectedValue { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing System.Web.UI.WebControls.DropDownList for .NET 10 compatibility.
	/// Provides SelectedValue used by RequiredFieldValidatorForDropDownList.
	/// </summary>
	public class DropDownList : ListControl { }

	/// <summary>
	/// Stub replacing System.Web.UI.WebControls.TextBox for .NET 10 compatibility.
	/// Provides Text property used by DateValidator and TimeValidator.
	/// </summary>
	public class TextBox : Control
	{
		/// <summary>Current text content of the text box control.</summary>
		public string Text { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing System.Web.UI.WebControls.HiddenField for .NET 10 compatibility.
	/// Provides Value property used by RequiredFieldValidatorForHiddenInputs.
	/// </summary>
	public class HiddenField : Control
	{
		/// <summary>Current value of the hidden field.</summary>
		public string Value { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing System.Web.UI.HtmlControls.HtmlInputHidden for .NET 10 compatibility.
	/// Provides Value property used by RequiredFieldValidatorForHiddenInputs.
	/// </summary>
	public class HtmlInputHidden : Control
	{
		/// <summary>Current value of the hidden HTML input element.</summary>
		public string Value { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing SplendidCRM._controls.DatePicker for .NET 10 compatibility.
	/// Provides DateText property used by DatePickerValidator and RequiredFieldValidatorForDatePicker.
	/// The _controls directory is a WebForms user-control library that is out of scope for this migration.
	/// </summary>
	public class DatePicker : Control
	{
		/// <summary>Current text value entered into the date picker control.</summary>
		public string DateText { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing SplendidCRM._controls.TeamSelect for .NET 10 compatibility.
	/// Provides TEAM_SET_LIST property used by RequiredFieldValidatorForTeamSelect.
	/// </summary>
	public class TeamSelect : Control
	{
		/// <summary>Comma-delimited list of selected team IDs.</summary>
		public string TEAM_SET_LIST { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing SplendidCRM._controls.UserSelect for .NET 10 compatibility.
	/// Provides ASSIGNED_SET_LIST property used by RequiredFieldValidatorForUserSelect.
	/// </summary>
	public class UserSelect : Control
	{
		/// <summary>Comma-delimited list of assigned user IDs.</summary>
		public string ASSIGNED_SET_LIST { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing SplendidCRM._controls.NAICSCodeSelect for .NET 10 compatibility.
	/// Provides NAICS_SET_NAME property used by RequiredFieldValidatorForNAICSCodeSelect.
	/// </summary>
	public class NAICSCodeSelect : Control
	{
		/// <summary>Selected NAICS code set name value.</summary>
		public string NAICS_SET_NAME { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing SplendidCRM._controls.TagSelect for .NET 10 compatibility.
	/// Provides TAG_SET_NAME property used by RequiredFieldValidatorForTagSelect.
	/// </summary>
	public class TagSelect : Control
	{
		/// <summary>Selected tag set name value.</summary>
		public string TAG_SET_NAME { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing SplendidCRM._controls.KBTagSelect for .NET 10 compatibility.
	/// Provides KBTAG_SET_LIST property used by RequiredFieldValidatorForKBTagSelect.
	/// </summary>
	public class KBTagSelect : Control
	{
		/// <summary>Comma-delimited list of selected knowledge base tag IDs.</summary>
		public string KBTAG_SET_LIST { get; set; } = string.Empty;
	}

	/// <summary>
	/// Stub replacing SplendidCRM._controls.RelatedSelect for .NET 10 compatibility.
	/// Provides RELATED_SET_LIST property used by RequiredFieldValidatorForRelatedSelect.
	/// </summary>
	public class RelatedSelect : Control
	{
		/// <summary>Comma-delimited list of selected related record IDs.</summary>
		public string RELATED_SET_LIST { get; set; } = string.Empty;
	}

	/// <summary>
	/// Abstract base class replacing System.Web.UI.WebControls.BaseValidator for .NET 10
	/// compilation compatibility. Provides EnableClientScript, ControlToValidate, ErrorMessage,
	/// and the abstract ControlPropertiesValid() / EvaluateIsValid() contract that each
	/// of the 13 validator classes below implements.
	/// 
	/// In the original .NET Framework 4.8 codebase this class was
	/// System.Web.UI.WebControls.BaseValidator. ASP.NET Core has no equivalent framework
	/// class; this stub provides the same interface surface to allow the validator classes
	/// to compile and preserve their logic for Enterprise Edition upgrade path use.
	/// </summary>
	public abstract class BaseValidator : Control
	{
		/// <summary>
		/// When false, the validator does not emit client-side JavaScript.
		/// All migrated validators set this to false in their constructors, matching
		/// the original behavior where client-side validation is handled by the React SPA.
		/// </summary>
		public bool EnableClientScript { get; set; }

		/// <summary>ID of the control to validate. Maps to System.Web.UI.WebControls.BaseValidator.ControlToValidate.</summary>
		public string ControlToValidate { get; set; }

		/// <summary>Error message shown when validation fails. Maps to BaseValidator.ErrorMessage.</summary>
		public string ErrorMessage { get; set; }

		/// <summary>
		/// Determines whether the control being validated has properties consistent with validation.
		/// Returns false if the target control cannot be found (stub FindControl always returns null).
		/// Exposed as public to satisfy the .NET 10 class library members_exposed contract.
		/// </summary>
		public abstract bool ControlPropertiesValid();

		/// <summary>
		/// Evaluates whether the input provided by the user passes validation.
		/// Returns the validation result based on the specific validator logic.
		/// Exposed as public to satisfy the .NET 10 class library members_exposed contract.
		/// </summary>
		public abstract bool EvaluateIsValid();
	}

	// ====================================================================================
	// .NET 10 Migration: 13 validator classes
	// Original validation logic is preserved exactly. All System.Web.UI references replaced
	// with stub types defined above. ControlPropertiesValid() / EvaluateIsValid() changed from
	// protected override to public override to expose them as public API per migration contract.
	// ====================================================================================

	/// <summary>
	/// Summary description for CustomValidators.
	/// </summary>
	public class RequiredFieldValidatorForCheckBoxLists : BaseValidator
	{
		private ListControl lst;

		public RequiredFieldValidatorForCheckBoxLists()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			Control ctl = FindControl(ControlToValidate);

			if ( ctl != null )
			{
				lst = (ListControl) ctl;
				return (lst != null) ;
			}
			else 
				return false;  // raise exception
		}

		public override bool EvaluateIsValid()
		{
			return lst.SelectedIndex != -1;
		}
	}

	public class RequiredFieldValidatorForDropDownList : BaseValidator
	{
		private DropDownList lst;

		public RequiredFieldValidatorForDropDownList()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			Control ctl = FindControl(ControlToValidate);

			if ( ctl != null )
			{
				// 03/24/2018 Paul.  Change the type of the cast so that ListBox will be allowed. 
				lst = ctl as DropDownList;
				return (lst != null) ;
			}
			else 
				return false;  // raise exception
		}

		public override bool EvaluateIsValid()
		{
			// 03/14/2006 Paul.  Use SelectedValue to determine if the dropdown is valid. 
			// Using a dropdown validator is not required because we only use the -- None -- first item when not required. 
			return !Sql.IsEmptyString(lst.SelectedValue);
		}
	}

	public class RequiredFieldValidatorForHiddenInputs : BaseValidator
	{
		// 12/03/2007 Paul.  The hidden field could be HtmlInputHidden or HiddenField. 
		private Control hid;

		public RequiredFieldValidatorForHiddenInputs()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			Control ctl = FindControl(ControlToValidate);
			if ( ctl != null )
			{
				hid = ctl;
				// .NET 10 Migration: typeof checks updated from System.Web.UI.HtmlControls.HtmlInputHidden
				// and System.Web.UI.WebControls.HiddenField to stub types HtmlInputHidden and HiddenField
				// defined in SplendidCRM namespace above.
				return (hid.GetType() == typeof(HtmlInputHidden) || hid.GetType() == typeof(HiddenField)) ;
			}
			else 
				return false;  // raise exception
		}

		public override bool EvaluateIsValid()
		{
			// .NET 10 Migration: typeof checks updated to use stub types (see ControlPropertiesValid).
			if ( hid.GetType() == typeof(HtmlInputHidden) )
				return !Sql.IsEmptyString((hid as HtmlInputHidden).Value) ;
			else if ( hid.GetType() == typeof(HiddenField) )
				return !Sql.IsEmptyString((hid as HiddenField).Value) ;
			else
				return true;
		}
	}

	public class DateValidator : BaseValidator
	{
		private TextBox txt;

		public DateValidator()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			Control ctl = FindControl(ControlToValidate);

			if ( ctl != null )
			{
				txt = (TextBox) ctl;
				return (txt != null) ;
			}
			else 
				return false;  // raise exception
		}

		public override bool EvaluateIsValid()
		{
			// 10/13/2005 Paul.  An empty string is treated as a valid date.  A separate RequiredFieldValidator is required to handle this condition. 
			return (txt.Text.Trim() == String.Empty) || Information.IsDate(txt.Text);
		}
	}

	public class TimeValidator : BaseValidator
	{
		private TextBox txt;

		public TimeValidator()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			Control ctl = FindControl(ControlToValidate);

			if ( ctl != null )
			{
				txt = (TextBox) ctl;
				return (txt != null) ;
			}
			else 
				return false;  // raise exception
		}

		public override bool EvaluateIsValid()
		{
			// 03/03/2006 Paul.  An empty string is treated as a valid date.  A separate RequiredFieldValidator is required to handle this condition. 
			// 03/03/2006 Paul.  Validate with a prepended date so that it will fail if the user also supplies a date. 
			return (txt.Text.Trim() == String.Empty) || Information.IsDate(DateTime.Now.ToShortDateString() + " " + txt.Text);
		}
	}

	public class DatePickerValidator : BaseValidator
	{
		private DatePicker ctlDate;

		public DatePickerValidator()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			Control ctl = FindControl(ControlToValidate);

			if ( ctl != null )
			{
				ctlDate = (DatePicker) ctl;
				return (ctlDate != null) ;
			}
			else 
				return false;  // raise exception
		}

		public override bool EvaluateIsValid()
		{
			// 03/03/2006 Paul.  An empty string is treated as a valid date.  A separate RequiredFieldValidator is required to handle this condition. 
			return (ctlDate.DateText.Trim() == String.Empty) || Information.IsDate(ctlDate.DateText);
		}
	}

	public class RequiredFieldValidatorForDatePicker : BaseValidator
	{
		private DatePicker ctlDate;

		public RequiredFieldValidatorForDatePicker()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			Control ctl = FindControl(ControlToValidate);

			if ( ctl != null )
			{
				ctlDate = (DatePicker) ctl;
				return (ctlDate != null) ;
			}
			else 
				return false;  // raise exception
		}

		public override bool EvaluateIsValid()
		{
			return !Sql.IsEmptyString(ctlDate.DateText) ;
		}
	}

	public class RequiredFieldValidatorForTeamSelect : BaseValidator
	{
		private TeamSelect ctlTeamSelect;

		public RequiredFieldValidatorForTeamSelect()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			// 09/21/2009 Paul.  The ControlToValidate field is not used. 
			ctlTeamSelect = this.NamingContainer as TeamSelect;
			return (ctlTeamSelect != null) ;
		}

		public override bool EvaluateIsValid()
		{
			return ctlTeamSelect != null && !Sql.IsEmptyString(ctlTeamSelect.TEAM_SET_LIST);
		}
	}

	// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment. 
	public class RequiredFieldValidatorForUserSelect : BaseValidator
	{
		private UserSelect ctlUserSelect;

		public RequiredFieldValidatorForUserSelect()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			ctlUserSelect = this.NamingContainer as UserSelect;
			return (ctlUserSelect != null) ;
		}

		public override bool EvaluateIsValid()
		{
			return ctlUserSelect != null && !Sql.IsEmptyString(ctlUserSelect.ASSIGNED_SET_LIST);
		}
	}

	public class RequiredFieldValidatorForNAICSCodeSelect : BaseValidator
	{
		private NAICSCodeSelect ctlNAICSCodeSelect;

		public RequiredFieldValidatorForNAICSCodeSelect()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			ctlNAICSCodeSelect = this.NamingContainer as NAICSCodeSelect;
			return (ctlNAICSCodeSelect != null) ;
		}

		public override bool EvaluateIsValid()
		{
			return ctlNAICSCodeSelect != null && !Sql.IsEmptyString(ctlNAICSCodeSelect.NAICS_SET_NAME);
		}
	}


	// 05/12/2016 Paul.  Add Tags module. 
	public class RequiredFieldValidatorForTagSelect : BaseValidator
	{
		private TagSelect ctlTagSelect;

		public RequiredFieldValidatorForTagSelect()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			// 09/21/2009 Paul.  The ControlToValidate field is not used. 
			ctlTagSelect = this.NamingContainer as TagSelect;
			return (ctlTagSelect != null) ;
		}

		public override bool EvaluateIsValid()
		{
			return ctlTagSelect != null && !Sql.IsEmptyString(ctlTagSelect.TAG_SET_NAME);
		}
	}

	public class RequiredFieldValidatorForKBTagSelect : BaseValidator
	{
		private KBTagSelect ctlKBTagSelect;

		public RequiredFieldValidatorForKBTagSelect()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			// 09/21/2009 Paul.  The ControlToValidate field is not used. 
			ctlKBTagSelect = this.NamingContainer as KBTagSelect;
			return (ctlKBTagSelect != null) ;
		}

		public override bool EvaluateIsValid()
		{
			return ctlKBTagSelect != null && !Sql.IsEmptyString(ctlKBTagSelect.KBTAG_SET_LIST);
		}
	}

	public class RequiredFieldValidatorForRelatedSelect : BaseValidator
	{
		private RelatedSelect ctlRelatedSelect;

		public RequiredFieldValidatorForRelatedSelect()
		{
			base.EnableClientScript = false;
		}

		public override bool ControlPropertiesValid()
		{
			// 09/21/2009 Paul.  The ControlToValidate field is not used. 
			ctlRelatedSelect = this.NamingContainer as RelatedSelect;
			return (ctlRelatedSelect != null) ;
		}

		public override bool EvaluateIsValid()
		{
			return ctlRelatedSelect != null && !Sql.IsEmptyString(ctlRelatedSelect.RELATED_SET_LIST);
		}
	}

}
