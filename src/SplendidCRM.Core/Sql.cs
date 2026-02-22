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
 * IN NO EVENT SHALL SPLENDIDCRM BE RESPONSIBLE FOR ANY DAMAGES OF ANY NATURE WHATSOEVER ARISING OUT 
 * OF OR IN CONNECTION WITH THE USE OF OR INABILITY TO USE THE SOFTWARE, EVEN IF SPLENDIDCRM HAS BEEN 
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGES.
 *********************************************************************************************************************/
#nullable disable
using System;

namespace SplendidCRM
{
	/// <summary>
	/// SQL helper utilities for parameterized query builders and type-safe conversions.
	/// Migrated from SplendidCRM/_code/Sql.cs for .NET 10 ASP.NET Core.
	/// NOTE: This is a minimal forward-declaration stub providing only the methods
	/// needed for the currently compiled files. The full implementation will be
	/// provided when the complete Sql.cs migration is processed.
	/// </summary>
	public class Sql
	{
		/// <summary>
		/// Returns true if the string is null or empty.
		/// </summary>
		public static bool IsEmptyString(string str)
		{
			if ( str == null || str == String.Empty )
				return true;
			return false;
		}

		/// <summary>
		/// Returns true if the object is null, DBNull, or its string representation is empty.
		/// </summary>
		public static bool IsEmptyString(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return true;
			if ( obj.ToString() == String.Empty )
				return true;
			return false;
		}

		/// <summary>
		/// Identity conversion for Boolean values.
		/// </summary>
		public static Boolean ToBoolean(Boolean b)
		{
			return b;
		}

		/// <summary>
		/// Converts an integer to Boolean (0 = false, non-zero = true).
		/// </summary>
		public static Boolean ToBoolean(Int32 n)
		{
			return (n == 0) ? false : true;
		}

		/// <summary>
		/// Converts an object to Boolean with comprehensive type handling.
		/// Supports Int32, Byte, SByte, Int16, Decimal, String, and Boolean types.
		/// </summary>
		public static Boolean ToBoolean(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return false;
			if ( obj.GetType() == Type.GetType("System.Int32") )
				return (Convert.ToInt32(obj) == 0) ? false : true;
			if ( obj.GetType() == Type.GetType("System.Byte") )
				return (Convert.ToByte(obj) == 0) ? false : true;
			if ( obj.GetType() == Type.GetType("System.SByte") )
				return (Convert.ToSByte(obj) == 0) ? false : true;
			if ( obj.GetType() == Type.GetType("System.Int16") )
				return (Convert.ToInt16(obj) == 0) ? false : true;
			if ( obj.GetType() == Type.GetType("System.Decimal") )
				return (Convert.ToDecimal(obj) == 0) ? false : true;
			if ( obj.GetType() == Type.GetType("System.String") )
			{
				string s = obj.ToString().ToLower();
				return (s == "true" || s == "on" || s == "1" || s == "y") ? true : false;
			}
			if ( obj.GetType() != Type.GetType("System.Boolean") )
				return false;
			bool bValue = false;
			bool.TryParse(obj.ToString(), out bValue);
			return bValue;
		}

		/// <summary>
		/// Converts a string to a Guid. Returns Guid.Empty for null or invalid values.
		/// </summary>
		public static Guid ToGuid(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return Guid.Empty;
			if ( obj.GetType() == Type.GetType("System.Guid") )
				return (Guid) obj;
			Guid gValue = Guid.Empty;
			Guid.TryParse(obj.ToString(), out gValue);
			return gValue;
		}

		/// <summary>
		/// Converts an object to a string. Returns String.Empty for null or DBNull.
		/// </summary>
		public static string ToString(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return String.Empty;
			return obj.ToString();
		}

		/// <summary>
		/// Returns the string or String.Empty if null.
		/// </summary>
		public static string ToString(string str)
		{
			if ( str == null )
				return String.Empty;
			return str;
		}
	}
}
