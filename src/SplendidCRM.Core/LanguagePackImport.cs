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
// .NET 10 Migration: SplendidCRM/_code/LanguagePackImport.cs → src/SplendidCRM.Core/LanguagePackImport.cs
// Changes applied:
//   - No System.Web dependencies present in source — no using replacements required
//   - All public static method signatures preserved unchanged (minimal change clause)
//   - DI constructor added for ASP.NET Core service registration compatibility
//   - SYSLIB0001 warning suppressed: Encoding.UTF7 is obsolete in .NET 5+ but preserved here
//     for SugarCRM language pack backward compatibility (specific Asian/Eastern European language packs
//     require UTF-7 encoding detection to correctly parse character sequences)
//   - #nullable disable applied to avoid nullable reference warnings in complex parser state machine
//     that closely mirrors the original source structure
#nullable disable
#pragma warning disable SYSLIB0001 // Encoding.UTF7 is obsolete but preserved for SugarCRM language pack backward compatibility
using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using ICSharpCode.SharpZipLib.Zip;

namespace SplendidCRM
{
	/// <summary>
	/// Language pack import utility for SplendidCRM.
	/// Parses and imports SugarCRM-format PHP language pack files into the SplendidCRM
	/// terminology database tables (TERMINOLOGY and TERMINOLOGY_HELP).
	///
	/// Supports both file-system-based import (reading directly from .php files) and
	/// ZIP-stream-based import (reading PHP files from within a .langpack.zip archive
	/// without extracting to disk).
	///
	/// Migrated from SplendidCRM/_code/LanguagePackImport.cs for .NET 10 ASP.NET Core.
	/// No System.Web dependencies were present in the source; migration is structural only.
	/// </summary>
	public class LanguagePackImport
	{
		// .NET 10 Migration: DI constructor added for ASP.NET Core service registration.
		// All public methods are static and do not require injected services — this constructor
		// allows registration via services.AddScoped<LanguagePackImport>() if needed by callers
		// that prefer to resolve the class through the DI container.
		public LanguagePackImport()
		{
		}

		/*
		$mod_strings = array (
		//DON'T CONVERT THESE THEY ARE MAPPINGS
		  array (
		 ),

		);

		$mod_list_strings = Array(
		);

		$app_list_strings = array (
		$app_strings = array (
		*/

		/// <summary>
		/// Extracts the CRM module name from a language pack file path.
		/// The module name is the directory immediately above the "language" folder in the path.
		/// Returns null when the parent directory is "include" (application-level strings).
		/// </summary>
		/// <param name="sPathName">Full or relative path to a language pack .php file.</param>
		/// <returns>Module name string, or null for application-level (include) language files.</returns>
		public static string GetModuleName(string sPathName)
		{
			// .NET 10 Migration (Goal 10 — Platform Independence): split on both '\' and '/' to
			// support Linux forward-slash paths (from ZIP archive entries) and Windows backslash paths.
			// Source used Split('\\') which only works on Windows; ZIP archives always use '/'.
			string[] arrFolders = (Path.GetDirectoryName(sPathName) ?? String.Empty).Split(new char[] { '\\', '/' });
			string strModuleName = String.Empty;
			if ( arrFolders.Length >= 2 )
			{
				if ( String.Compare(arrFolders[arrFolders.Length-1], "language", true) == 0 )
				{
					strModuleName = arrFolders[arrFolders.Length-2];
					if ( String.Compare(strModuleName, "include", true) == 0 )
						strModuleName = null;
				}
			}
			return strModuleName;
		}

		/// <summary>
		/// Extracts the language/locale code from a language pack file name.
		/// Handles both two-part (en_us.lang.php) and three-part (de_de.lang.php) file names,
		/// as well as the four-part German language pack variant.
		/// Converts underscores to hyphens and uppercases the region code (e.g. "en-US", "de-DE").
		/// </summary>
		/// <param name="sPathName">Full or relative path to a language pack .php file.</param>
		/// <returns>Language/locale code string (e.g. "en-US", "de-DE", "ja-JA").</returns>
		public static string GetLanguage(string sPathName)
		{
			// 11/13/2006 Paul.  There is a file the the SugarCRM German language pack that has 4 parts.  
			string[] arrParts = Path.GetFileName(sPathName).Split('.');
			string sLang = arrParts[arrParts.Length-3];
			sLang = sLang.Replace("_", "-");
			if ( sLang.IndexOf("-") >= 0 )
			{
				string[] arrLang = sLang.Split('-');
				sLang = arrLang[0] + '-' + arrLang[1].ToUpper();
			}
			return sLang;
		}

		/// <summary>
		/// Returns the appropriate text encoding for a language pack file based on the language code.
		/// UTF-8 is used for Japanese (ja), Traditional Chinese (tw), Simplified Chinese (zh), and
		/// Czech (cz) language packs. All other languages default to UTF-7.
		/// </summary>
		/// <param name="sLang">Language/locale code string (e.g. "en-US", "ja-JA").</param>
		/// <returns>Encoding instance to use when reading the language pack file.</returns>
		public static Encoding GetEncoding(string sLang)
		{
			// 10/09/2005 Paul.  I expected UTF-8 to always work, but it does not.  Only use UTF-8 for known languages. 
			System.Text.Encoding enc = Encoding.UTF7;
			if ( sLang.StartsWith("ja") || sLang.StartsWith("tw") || sLang.StartsWith("zh") || sLang.StartsWith("cz") )
				enc = Encoding.UTF8;
			return enc;
		}

		/// <summary>
		/// Reads a language pack PHP file from disk and imports all terminology entries into the database.
		/// Parses $app_strings, $app_list_strings, $mod_strings, and $mod_list_strings PHP array definitions.
		/// </summary>
		/// <param name="sPathName">Full path to the language pack .php file on disk.</param>
		/// <param name="bForceUTF8">When true, overrides the language-based encoding selection and always uses UTF-8.</param>
		public static void InsertTerms(string sPathName, bool bForceUTF8)
		{
			string sModuleName = GetModuleName(sPathName);
			string sLang       = GetLanguage  (sPathName);
			System.Text.Encoding enc = GetEncoding(sLang);
			if ( bForceUTF8 )
				enc = Encoding.UTF8;
			using ( StreamReader sr = new StreamReader(sPathName, enc, true) )
			{
				string sData = sr.ReadToEnd();
				int nListStart = sData.IndexOf("$app_strings");
				if ( nListStart >= 0 )
					LanguagePackImport.ParseList(sModuleName, sLang, "$app_strings", sData, nListStart);
				nListStart = sData.IndexOf("$app_list_strings");
				if ( nListStart >= 0 )
					LanguagePackImport.ParseList(sModuleName, sLang, "$app_list_strings", sData, nListStart);
				nListStart = sData.IndexOf("$mod_strings");
				if ( nListStart >= 0 )
					LanguagePackImport.ParseList(sModuleName, sLang, "$mod_strings", sData, nListStart);
				nListStart = sData.IndexOf("$mod_list_strings");
				if ( nListStart >= 0 )
					LanguagePackImport.ParseList(sModuleName, sLang, "$mod_list_strings", sData, nListStart);
			}
		}

		/// <summary>
		/// Reads a language pack HTML help file from disk and imports its content into the TERMINOLOGY_HELP table.
		/// Strips the DOCTYPE or HTML comment header, then stores the remaining HTML body as the help text.
		/// Silently swallows all exceptions to allow bulk import operations to continue on individual file failures.
		/// </summary>
		/// <param name="sPathName">Full path to the language pack help .html file on disk.</param>
		/// <param name="bForceUTF8">When true, overrides the language-based encoding selection and always uses UTF-8.</param>
		public static void InsertHelp(string sPathName, bool bForceUTF8)
		{
			try
			{
				const string sDocType = "<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\">";
				string sModuleName  = GetModuleName(sPathName);
				string sLang        = GetLanguage  (sPathName);
				string strEntryName = Path.GetFileName(sPathName).Split('.')[2];
				System.Text.Encoding enc = GetEncoding(sLang);
				if ( bForceUTF8 )
					enc = Encoding.UTF8;
				using ( StreamReader sr = new StreamReader(sPathName, enc, true) )
				{
					string sData = sr.ReadToEnd();
					int nDocType = sData.IndexOf(sDocType);
					if ( nDocType >= 0 )
					{
						nDocType += sDocType.Length;
					}
					else
					{
						nDocType = sData.IndexOf("-->");
						if ( nDocType >= 0 )
							nDocType += 3;
					}
					while ( sData[nDocType] == ControlChars.Cr || sData[nDocType] == ControlChars.Lf )
						nDocType++;
					sData = sData.Substring(nDocType);
					Guid gID = Guid.Empty;
					SqlProcs.spTERMINOLOGY_HELP_Update(ref gID, strEntryName, sLang, sModuleName, sData);
				}
			}
			catch
			{
			}
		}

		/// <summary>
		/// Reads a language pack PHP file from a ZIP archive stream and imports all terminology entries.
		/// Used during bulk language pack ZIP import to process PHP files in-memory without disk extraction.
		/// On stream read failure (e.g. corrupted or truncated ZIP entry), logs the error via SplendidError
		/// and returns early so that processing of remaining ZIP entries can continue.
		/// </summary>
		/// <param name="sPathName">Virtual path of the PHP file within the ZIP archive (used for module/language extraction).</param>
		/// <param name="stmZip">Active ZipInputStream positioned at the current entry to read.</param>
		/// <param name="bForceUTF8">When true, overrides the language-based encoding selection and always uses UTF-8.</param>
		public static void InsertTerms(string sPathName, ZipInputStream stmZip, bool bForceUTF8)
		{
			string sModuleName = GetModuleName(sPathName);
			string sLang       = GetLanguage  (sPathName);
			System.Text.Encoding enc = GetEncoding(sLang);
			if ( bForceUTF8 )
				enc = Encoding.UTF8;
			// 01/12/2006 Paul.  Don't use using as it is closing the zip stream. 
			StreamReader sr = new StreamReader(stmZip, enc, true);
			{
				string sData = String.Empty;
				try
				{
					// 01/12/2006 Paul.  Problem importing it_it_3.5.1.langpack.zip, modules/OptimisticLock/language/it_it.lang.php
					// Specified argument was out of the range of valid values. Parameter name: length 
					// Catch the error and allow processing of the rest of the files. 
					sData = sr.ReadToEnd();
				}
				catch(Exception ex)
				{
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), sPathName + "; " + ex.Message);
					return;
				}
				int nListStart = sData.IndexOf("$app_strings");
				if ( nListStart >= 0 )
					LanguagePackImport.ParseList(sModuleName, sLang, "$app_strings", sData, nListStart);
				nListStart = sData.IndexOf("$app_list_strings");
				if ( nListStart >= 0 )
					LanguagePackImport.ParseList(sModuleName, sLang, "$app_list_strings", sData, nListStart);
				nListStart = sData.IndexOf("$mod_strings");
				if ( nListStart >= 0 )
					LanguagePackImport.ParseList(sModuleName, sLang, "$mod_strings", sData, nListStart);
				nListStart = sData.IndexOf("$mod_list_strings");
				if ( nListStart >= 0 )
					LanguagePackImport.ParseList(sModuleName, sLang, "$mod_list_strings", sData, nListStart);
			}
		}

		/// <summary>
		/// Parses a single PHP array variable declaration from language pack data and inserts
		/// all key-value pairs into the TERMINOLOGY table via SqlProcs.spTERMINOLOGY_Update.
		///
		/// Handles nested PHP arrays (array() values map to list entries with list name and order),
		/// single-quoted and double-quoted string keys and values, escape sequences (\\', \\",
		/// \\\\, \\n), and inline comments (// line comments are skipped).
		///
		/// The parser is a hand-written state machine that closely follows the original SugarCRM
		/// PHP array syntax, tolerating malformed entries and exiting cleanly on truncated input.
		/// </summary>
		/// <param name="sModuleName">CRM module name for this terminology group (null for application-level).</param>
		/// <param name="sLang">Language/locale code for these terminology entries.</param>
		/// <param name="strListGroup">PHP variable name being parsed (e.g. "$app_strings", "$mod_list_strings").</param>
		/// <param name="sData">Full content of the language pack PHP file as a string.</param>
		/// <param name="nListStart">Character offset into sData where the PHP variable declaration begins.</param>
		public static void ParseList(string sModuleName, string sLang, string strListGroup, string sData, int nListStart)
		{
			nListStart = sData.IndexOf("(", nListStart);
			if ( nListStart >= 0 )
			{
				nListStart++;
				int nMode = 0;  // 
				int nListOrder = 0 ;
				string strTemp = String.Empty;
				string strListName    = String.Empty;
				string strEntryName   = String.Empty;
				string strDisplayName = String.Empty;
				bool bInsideArray = false;
				bool bContinueParsing = true;
				while ( bContinueParsing )
				{
					switch ( nMode )
					{
						case 0:  // Search for next entry. 
							while ( Char.IsWhiteSpace(sData, nListStart) || sData[nListStart] == ',' )
							{
								nListStart++;
								if ( nListStart >= sData.Length )
								{
									bContinueParsing = false;
									break;
								}
							}
							if ( bInsideArray && sData[nListStart] == ')' )
							{
								nListStart++;
								bInsideArray = false;  // turn off array processing. 
								strListName = String.Empty;
								nListOrder = 0;
								nMode = 0;
							}
							else
							{
								nMode = 1;
							}
							break;
						case 1:  // Extract entry name. 
						{
							if ( nListStart >= sData.Length || (sData[nListStart] == ')' && sData[nListStart+1] == ';') )
							{
								bContinueParsing = false;
								break;
							}
							char chPunctuation = sData[nListStart];
							if ( chPunctuation != '\'' && chPunctuation != '\"' )
							{
								strTemp = sData.Substring(nListStart, Math.Min(100, sData.Length-nListStart));
								if ( sData[nListStart] == '/' && sData[nListStart+1] == '/' )
								{
									// Ignore comment. 
									// 05/19/2006 Paul.  lang-pt_br12-351b.zip ends the line with '\n'
									while ( nListStart < sData.Length && sData[nListStart] != '\r' && sData[nListStart] != '\n' )
										nListStart++;
								}
								// If character is not expected, then exit. 
								nListStart++;
								nMode = 0;
								break;
							}
							nListStart++;
							int nValueStart = nListStart;
							while ( chPunctuation != sData[nListStart] && nListStart < sData.Length )
								nListStart++;
							int nValueEnd = nListStart;
							nListStart++;
							if ( nValueEnd - nValueStart == 0 )
								nMode = 0 ;
							else
							{
								strEntryName = sData.Substring(nValueStart, nValueEnd - nValueStart);
								nMode = 2;
							}
							break;
						}
						case 2: // Search for display name or inner list
						{
							while ( Char.IsWhiteSpace(sData, nListStart) )
							{
								nListStart++;
								// 10/09/2005 Paul.  Just in case the file has been truncated, 
								// exit if there are not enough characters for a fully formed display name.
								if ( nListStart+4 >= sData.Length )
								{
									bContinueParsing = false;
									break;
								}
							}
							if ( sData[nListStart] == '=' && sData[nListStart+1] == '>' )
							{
								nListStart++;
								nListStart++;
								while ( Char.IsWhiteSpace(sData, nListStart) )
									nListStart++;
								char chPunctuation = sData[nListStart];
								if ( chPunctuation != '\'' && chPunctuation != '\"' )
								{
									string strArray = sData.Substring(nListStart, Math.Min(5, sData.Length-nListStart));
									if ( strArray == "array" )
									{
										nListStart += 5;
										while ( Char.IsWhiteSpace(sData, nListStart) || sData[nListStart] == '(' )
											nListStart++;
										bInsideArray = true;
										strListName = strEntryName;
										nListOrder = 0 ;
										nMode = 0;
										break;
									}
									else
									{
										strTemp = sData.Substring(nListStart, Math.Min(100, sData.Length-nListStart));
										// If character is not expected, then exit. 
										nListStart++;
										nMode = 0;
									}
									break;
								}
								nListStart++;
								int nValueStart = nListStart;
								// 10/09/2005 Paul.  Watch out for 'LBL_CURRENCY_SYMBOL' => '\\', where the punctuation end has a slash before it. 
								//  'NTC_LOGIN_MESSAGE' => 'Prego fai login all\\\'applicazione.',
								// 10/09/2005 Paul.  Use the bLastCharExcaped flag as a simple way to keep track of escaped characters 
								// to prevent early termination or allow proper termination of the string. 
								bool bLastCharExcaped = false;
								while ( nListStart < sData.Length && (chPunctuation != sData[nListStart] || (chPunctuation == sData[nListStart] && sData[nListStart-1] == '\\' && !bLastCharExcaped)) )
								{
									// If last character was escaped, then don't escape this one. 
									if ( !bLastCharExcaped && sData[nListStart-1] == '\\' )
										bLastCharExcaped = true;
									else
										bLastCharExcaped = false;
									nListStart++;
								}
								int nValueEnd = nListStart;
								nListStart++;
								if ( nValueEnd - nValueStart == 0 )
									nMode = 0 ;
								else
								{
									strDisplayName = sData.Substring(nValueStart, nValueEnd - nValueStart);
									// 10/09/2005 Paul.  Need to remove escape characters. 
									if ( strDisplayName.IndexOf('\\', 0) >= 0 )
									{
										// 10/09/2005 Paul. We must escape twice because we have escapes within escapes. \\\' should become '.  
										// First, escape the punctuation delimiter. 
										strDisplayName = strDisplayName.Replace("\\" + chPunctuation.ToString(), chPunctuation.ToString());
										// Second, escape the escape character. 
										strDisplayName = strDisplayName.Replace("\\\\", "\\");
										// Now escape all other known escape values. 
										strDisplayName = strDisplayName.Replace("\\\'", "'");
										strDisplayName = strDisplayName.Replace("\\\"", "\"");
										strDisplayName = strDisplayName.Replace("\\n", ControlChars.CrLf);
									}
									if ( strListName.Length > 0 )
									{
										nListOrder++;
									}
									SqlProcs.spTERMINOLOGY_Update(strEntryName, sLang, sModuleName, strListName, nListOrder, strDisplayName);
									nMode = 0;
								}
							}
							else
							{
								strTemp = sData.Substring(nListStart, Math.Min(100, sData.Length-nListStart));
								// If character is not expected, then exit. 
								nListStart++;
								nMode = 0;
								break;
							}
							break;
						}
						default:
							bContinueParsing = false;
							break;
					}
					if ( nListStart >= sData.Length )
						break;
				}
			}
		}

	}
}
#pragma warning restore SYSLIB0001
