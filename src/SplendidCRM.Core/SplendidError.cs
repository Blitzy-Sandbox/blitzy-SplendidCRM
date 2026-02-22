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
using System.Diagnostics;

namespace SplendidCRM
{
	/// <summary>
	/// Error logging/handling utility for SplendidCRM.
	/// Migrated from SplendidCRM/_code/SplendidError.cs for .NET 10 ASP.NET Core.
	/// NOTE: This is a minimal forward-declaration stub providing only the methods
	/// needed for the currently compiled files. The full implementation will be
	/// provided when the complete SplendidError.cs migration is processed.
	/// </summary>
	public class SplendidError
	{
		/// <summary>
		/// Logs a system error with stack frame location and message.
		/// </summary>
		public static void SystemError(StackFrame stack, string sMESSAGE)
		{
			string sFileName   = String.Empty;
			string sMethod     = String.Empty;
			int    nLineNumber = 0;
			if ( stack != null )
			{
				sFileName   = stack.GetFileName();
				sMethod     = stack.GetMethod()?.Name ?? String.Empty;
				nLineNumber = stack.GetFileLineNumber();
			}
			// Minimal stub: log to debug output. Full implementation will log to database.
			System.Diagnostics.Debug.WriteLine($"SplendidError.SystemError: {sFileName}::{sMethod} line {nLineNumber}: {sMESSAGE}");
		}

		/// <summary>
		/// Logs a system error with stack frame location and exception.
		/// </summary>
		public static void SystemError(StackFrame stack, Exception ex)
		{
			if ( ex != null )
			{
				SystemError(stack, ex.Message);
			}
		}
	}
}
