/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * Any use of the contents of this file are subject to the SplendidCRM Community Source Code License 
 * Agreement, or other written agreement between you and SplendidCRM ("License"). By installing or 
 * using this file, you have unconditionally agreed to the terms and conditions of the License, 
 * including but not limited to restrictions on the number of users therein, and you may not use this 
 * file except in compliance with the License. 
 *********************************************************************************************************************/
#nullable disable
using System;

namespace SplendidCRM
{
	/// <summary>
	/// Default configuration values for SplendidCRM.
	/// Migrated from SplendidCRM/_code/SplendidDefaults.cs for .NET 10 ASP.NET Core.
	/// </summary>
	public class SplendidDefaults
	{
		public static string Culture()
		{
			return "en-US";
		}

		public static string Theme()
		{
			return "Atlantic";
		}

		public static string DateFormat()
		{
			return "MM/dd/yyyy";
		}

		public static string TimeFormat()
		{
			return "h:mm tt";
		}

		public static string TimeZone()
		{
			// Eastern Standard Time.
			return "BFA61AF7-26ED-4020-A0C1-39A15E4E9E0A";
		}

		public static string CurrencyID()
		{
			return "E340202E-6291-4071-B327-A34CB4DF239B";
		}

		public static string Language()
		{
			return "en-US";
		}

		public static int MaxImportFileSize()
		{
			return 10 * 1024 * 1024; // 10 MB
		}

		public static int MaxHttpCollectionKeys()
		{
			return 5000;
		}

		public static int SessionTimeout()
		{
			return 20; // minutes
		}
	}
}
