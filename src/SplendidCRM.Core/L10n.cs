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
// .NET 10 Migration: SplendidCRM/_code/L10N.cs → src/SplendidCRM.Core/L10n.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.SessionState;
//   - ADDED:   using Microsoft.Extensions.Caching.Memory;
//   - HttpContext.Current.Application replaced by IMemoryCache parameter/field throughout.
//   - HttpApplicationState parameter type replaced by IMemoryCache in all static methods
//     (AlternateLanguage, Term, SetTerm, SetAlias).
//   - L10N constructor updated to L10N(string sNAME, IMemoryCache memoryCache).
//     Instance methods (Term, AccessKey, AliasedTerm, TermJavaScript) now use the stored _memoryCache field.
//   - SetTerm(string, string, string, string) and SetTerm(string, string, string, string, string) overloads
//     (originally delegating to HttpContext.Current.Application) preserved as no-ops for API compatibility.
//     Callers must migrate to the explicit IMemoryCache overloads.
//   - SplendidError.SystemMessage(Application, ...) → SplendidError.SystemMessage(IMemoryCache, ...)
//   - Cache key patterns are identical to the original Application[] keys:
//       '{culture}.{entryName}'                   — primary culture lookup
//       'en-US.{entryName}'                        — English fallback
//       'ALIAS_{module}.{name}'                    — alias mapping
//       'CONFIG.alternate_language.{culture}'      — culture remapping (e.g. en-CA → en-US)
#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Localization/internationalization utility class for SplendidCRM.
	/// Provides terminology lookups against the IMemoryCache-backed locale data loaded during
	/// application startup by SplendidInit (formerly loaded into HttpApplicationState).
	///
	/// Migrated from SplendidCRM/_code/L10N.cs for .NET 10 ASP.NET Core.
	/// Replaces all HttpApplicationState (Application[]) access with IMemoryCache injected via constructor.
	///
	/// DESIGN NOTES for callers:
	///   • Create an L10N instance per-request with the user's culture (from Security.USER_LANG or config).
	///   • Register IMemoryCache (AddMemoryCache) in the DI container; inject into services that need L10N.
	///   • Static overloads require an explicit IMemoryCache parameter.
	///   • SetTerm(string, string, string, string[, string]) without IMemoryCache are preserved no-ops;
	///     migrate callers to the IMemoryCache overloads.
	/// </summary>
	public class L10N
	{
		// =====================================================================================
		// Fields
		// =====================================================================================

		/// <summary>
		/// The normalized culture name for this L10N instance (e.g. "en-US", "fr-FR").
		/// Protected to allow subclass access consistent with the original .NET Framework version.
		/// </summary>
		protected string m_sNAME;

		/// <summary>
		/// The IMemoryCache used for all terminology lookups on this instance.
		/// Replaces HttpContext.Current.Application from the .NET Framework version.
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		// =====================================================================================
		// Properties
		// =====================================================================================

		/// <summary>
		/// Gets the normalized culture name for this L10N instance (e.g. "en-US", "fr-FR").
		/// Referenced by CronDescription and other locale-aware code throughout SplendidCRM.
		/// </summary>
		public string NAME
		{
			get { return m_sNAME; }
		}

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Creates an L10N instance for the specified culture, backed by the given IMemoryCache.
		/// </summary>
		/// <param name="sNAME">
		/// Culture name (e.g. "en-US", "en_US", "fr-FR"). Normalized via NormalizeCulture() —
		/// underscores are converted to hyphens and case is normalized.
		/// If null or empty, defaults to "en-US".
		/// </param>
		/// <param name="memoryCache">
		/// IMemoryCache containing the loaded terminology data.
		/// Replaces HttpApplicationState passed via HttpContext.Current.Application.
		/// </param>
		public L10N(string sNAME, IMemoryCache memoryCache)
		{
			// 11/19/2005 Paul.  We may be connecting to MySQL, so the language may have an underscore.
			m_sNAME       = NormalizeCulture(sNAME);
			_memoryCache  = memoryCache;
		}

		// =====================================================================================
		// Culture utility methods
		// =====================================================================================

		/// <summary>
		/// Determines whether the current culture is a right-to-left (RTL) language.
		/// RTL languages include: Arabic (ar), Hebrew (he), Urdu (ur), Farsi/Persian (fa).
		/// </summary>
		/// <returns>true if the current culture is RTL; false otherwise.</returns>
		public bool IsLanguageRTL()
		{
			bool bRTL = false;
			switch ( m_sNAME.Substring(0, 2) )
			{
				case "ar":  bRTL = true;  break;
				case "he":  bRTL = true;  break;
				case "ur":  bRTL = true;  break;
				case "fa":  bRTL = true;  break;  // 12/17/2008 Paul.  Farsi is also RTL.
			}
			return bRTL;
		}

		/// <summary>
		/// Maps a culture to its alternate/canonical culture name for terminology lookups.
		/// For example, maps "en-CA" → "en-US" when a mapping is configured.
		/// Reads the 'CONFIG.alternate_language.{culture}' key from IMemoryCache.
		///
		/// BEFORE: Sql.ToString(Application["CONFIG.alternate_language." + sCulture])
		/// AFTER:  Sql.ToString(memoryCache.Get&lt;object&gt;("CONFIG.alternate_language." + sCulture))
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache containing alternate language configuration.
		/// Replaces HttpApplicationState from the .NET Framework version.
		/// </param>
		/// <param name="sCulture">Culture name to potentially remap.</param>
		/// <returns>
		/// The mapped culture name if a mapping exists in the cache; otherwise the original sCulture.
		/// </returns>
		public static string AlternateLanguage(IMemoryCache memoryCache, string sCulture)
		{
			// 04/20/2018 Paul.  Alternate language mapping to convert en-CA to en_US.
			// BEFORE: string sAlternateName = Sql.ToString(Application["CONFIG.alternate_language." + sCulture]);
			// AFTER:  string sAlternateName = Sql.ToString(memoryCache.Get<object>("CONFIG.alternate_language." + sCulture));
			string sAlternateName = Sql.ToString(memoryCache.Get<object>("CONFIG.alternate_language." + sCulture));
			if ( !Sql.IsEmptyString(sAlternateName) )
				sCulture = sAlternateName;
			return sCulture;
		}

		/// <summary>
		/// Normalizes a culture identifier to the proper .NET culture format (e.g. "en-US", "fr-FR").
		/// Converts MySQL underscore format (e.g. "en_US") to hyphen format,
		/// and ensures proper casing (lowercase language code + uppercase region code).
		/// Defaults to "en-US" if the culture string is null or empty.
		/// </summary>
		/// <param name="sCulture">Raw culture name to normalize (may use underscore or hyphen separator).</param>
		/// <returns>Normalized culture name with hyphen separator in proper case (e.g. "en-US").</returns>
		public static string NormalizeCulture(string sCulture)
		{
			// 08/28/2005 Paul.  Default to English if nothing specified.
			// 09/02/2008 Paul.  Default to English if nothing specified.  This can happen if a user is created programmatically.
			if ( Sql.IsEmptyString(sCulture) )
				sCulture = "en-US";
			sCulture = sCulture.Replace("_", "-");
			// 05/20/2008 Paul.  We are now storing the language in the proper case, so make sure to normalize with proper case.
			sCulture = sCulture.Substring(0, 2).ToLower() + sCulture.Substring(2).ToUpper();
			return sCulture;
		}

		// =====================================================================================
		// Term — instance overloads (use stored _memoryCache and m_sNAME culture)
		// =====================================================================================

		/// <summary>
		/// Returns the localized display name for a value in a terminology list (e.g. dropdown list items).
		/// Looks up the key '{listName}{field}' in the current culture, falling back to en-US.
		///
		/// BEFORE: return Term(HttpContext.Current.Application, m_sNAME, sListName, oField);
		/// AFTER:  return Term(_memoryCache, m_sNAME, sListName, oField);
		/// </summary>
		/// <param name="sListName">
		/// Name of the terminology list (prefix, e.g. "account_type_dom").
		/// Concatenated with oField.ToString() to form the cache key.
		/// </param>
		/// <param name="oField">
		/// Field value whose string representation forms the term key suffix.
		/// If null or DBNull, the value is returned as-is without lookup.
		/// </param>
		/// <returns>
		/// Localized display name if found; the original oField if null/DBNull; empty string if sListName is empty.
		/// </returns>
		public object Term(string sListName, object oField)
		{
			// BEFORE: return Term(HttpContext.Current.Application, m_sNAME, sListName, oField);
			// AFTER:  return Term(_memoryCache, m_sNAME, sListName, oField);
			return Term(_memoryCache, m_sNAME, sListName, oField);
		}

		/// <summary>
		/// Returns the localized display name for the given terminology entry key.
		/// Looks up '{m_sNAME}.{sEntryName}' in IMemoryCache, falling back to 'en-US.{sEntryName}'.
		///
		/// BEFORE: return Term(HttpContext.Current.Application, m_sNAME, sEntryName);
		/// AFTER:  return Term(_memoryCache, m_sNAME, sEntryName);
		/// </summary>
		/// <param name="sEntryName">
		/// Terminology entry key (e.g. "LBL_ACCOUNT_NAME", "Accounts.LBL_LIST_FORM_TITLE").
		/// </param>
		/// <returns>
		/// Localized string if found; the entry name itself if not found in any culture.
		/// </returns>
		public string Term(string sEntryName)
		{
			// BEFORE: return Term(HttpContext.Current.Application, m_sNAME, sEntryName);
			// AFTER:  return Term(_memoryCache, m_sNAME, sEntryName);
			return Term(_memoryCache, m_sNAME, sEntryName);
		}

		// =====================================================================================
		// Term — static overloads with explicit IMemoryCache (replace HttpApplicationState)
		// =====================================================================================

		/// <summary>
		/// Returns the localized display name for a value in a terminology list.
		/// Constructs the lookup key as '{listName}{field}' and delegates to Term(IMemoryCache, string, string).
		///
		/// BEFORE: Term(HttpApplicationState Application, string sCultureName, string sListName, object oField)
		/// AFTER:  Term(IMemoryCache memoryCache, string sCultureName, string sListName, object oField)
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache containing cached terminology data. Replaces HttpApplicationState.
		/// </param>
		/// <param name="sCultureName">Culture name for the primary lookup (e.g. "en-US").</param>
		/// <param name="sListName">Terminology list prefix. Concatenated with oField.ToString() as the cache key suffix.</param>
		/// <param name="oField">
		/// Field value to look up. Returned unchanged if null or DBNull.Value.
		/// </param>
		/// <returns>
		/// Localized string if found; empty string if sListName is empty; oField unchanged if null/DBNull.
		/// </returns>
		public static object? Term(IMemoryCache memoryCache, string sCultureName, string sListName, object? oField)
		{
			// 08/17/2005 Paul.  Special Term function that helps with a list.
			// 01/11/2008 Paul.  Protect against uninitialized variables.
			if ( String.IsNullOrEmpty(sListName) )
				return String.Empty;

			if ( oField == null || oField == DBNull.Value )
				return oField;
			// 11/28/2005 Paul.  Convert field to string instead of cast.  Cast will not work for integer fields.
			return Term(memoryCache, sCultureName, sListName + oField.ToString());
		}

		/// <summary>
		/// Returns the localized display name for the given terminology entry key.
		/// Looks up '{sCultureName}.{sEntryName}' in IMemoryCache (TryGetValue),
		/// then falls back to 'en-US.{sEntryName}'. Under #if DEBUG, logs missing entries
		/// via SplendidError.SystemMessage when the key contains a dot (module-qualified names).
		///
		/// BEFORE: Term(HttpApplicationState Application, string sCultureName, string sEntryName)
		///           Application[sCultureName + "." + sEntryName]   — primary lookup
		///           Application["en-US." + sEntryName]             — English fallback
		///           Application["en-US." + sEntryName] = sEntryName — cache missing key to suppress duplicates
		/// AFTER:  Term(IMemoryCache memoryCache, string sCultureName, string sEntryName)
		///           memoryCache.TryGetValue(sCultureName + "." + sEntryName, ...)
		///           memoryCache.Get&lt;object&gt;("en-US." + sEntryName)
		///           memoryCache.Set("en-US." + sEntryName, sEntryName)
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache containing cached terminology data. Replaces HttpApplicationState.
		/// Returns empty string immediately if null.
		/// </param>
		/// <param name="sCultureName">
		/// Culture name for the primary lookup (e.g. "en-US", "fr-FR").
		/// </param>
		/// <param name="sEntryName">
		/// Terminology entry key (e.g. "LBL_ACCOUNT_NAME", "Accounts.LBL_LIST_FORM_TITLE").
		/// </param>
		/// <returns>
		/// Localized string if found in the requested culture or en-US fallback;
		/// the entry name itself if not found anywhere.
		/// Empty string for _BUTTON_KEY suffixes and when sEntryName is null/empty.
		/// </returns>
		public static string Term(IMemoryCache memoryCache, string sCultureName, string sEntryName)
		{
			// 01/11/2008 Paul.  Protect against uninitialized variables.
			if ( String.IsNullOrEmpty(sEntryName) || memoryCache == null )
				return String.Empty;

			// BEFORE: object oDisplayName = Application[sCultureName + "." + sEntryName];
			// AFTER:  Use IMemoryCache.TryGetValue() for the primary culture lookup.
			//         Key pattern preserved exactly from .NET Framework version.
			object? oDisplayName;
			if ( !memoryCache.TryGetValue(sCultureName + "." + sEntryName, out oDisplayName) )
				oDisplayName = null;

			if ( oDisplayName == null )
			{
				// 01/11/2007 Paul.  Default to English if term not found.
				// There are just too many untranslated terms when importing a SugarCRM Language Pack.
				// BEFORE: oDisplayName = Application["en-US." + sEntryName];
				// AFTER:  oDisplayName = memoryCache.Get<object>("en-US." + sEntryName);
				oDisplayName = memoryCache.Get<object>("en-US." + sEntryName);
				if ( oDisplayName == null )
				{
					// Prevent parameter out of range errors with <asp:Button AccessKey="" />
					if ( sEntryName.EndsWith("_BUTTON_KEY") )
						return String.Empty;
					// 07/07/2008 Paul.  If the entry name is not found, post a warning message
					// then define the entry so that we will only get one warning per run.
					if ( sEntryName.Contains(".") )
					{
						// BEFORE: Application["en-US." + sEntryName] = sEntryName;
						// AFTER:  memoryCache.Set("en-US." + sEntryName, sEntryName)
						//         Cache with no expiration to suppress duplicate warnings (matching Application[] behavior).
						memoryCache.Set("en-US." + sEntryName, (object)sEntryName);
#if DEBUG
						// 09/18/2009 Paul.  The end-user should not see these any more.
						// There are simply too many false-positives that are caused by a page or control being bound twice.
						// BEFORE: SplendidError.SystemMessage(Application, "Warning", new StackTrace(true).GetFrame(0), ...)
						// AFTER:  SplendidError.SystemMessage(memoryCache, "Warning", new StackTrace(true).GetFrame(0), ...)
						SplendidError.SystemMessage(memoryCache, "Warning", new StackTrace(true).GetFrame(0), "L10N.Term: \"" + sEntryName + "\" not found.");
#endif
					}
					return sEntryName;
				}
			}
			return oDisplayName?.ToString() ?? sEntryName;
		}

		// =====================================================================================
		// AccessKey — instance method using stored _memoryCache
		// =====================================================================================

		/// <summary>
		/// Returns the single-character access key for a UI element's terminology entry.
		/// Looks up the entry in IMemoryCache and returns only the first character.
		/// Returns empty string if the entry is not found or is empty.
		///
		/// BEFORE: HttpApplicationState Application = HttpContext.Current.Application;
		///         Application[NAME + "." + sEntryName]
		/// AFTER:  _memoryCache.TryGetValue(NAME + "." + sEntryName, ...)
		/// </summary>
		/// <param name="sEntryName">Terminology entry key for the access key label.</param>
		/// <returns>
		/// Single-character access key if the entry is found and non-empty; otherwise empty string.
		/// </returns>
		public string AccessKey(string sEntryName)
		{
			// 06/30/2007 Paul.  Prevent parameter out of range errors with <asp:Button AccessKey="" />.
			//                   Not all access keys end in _BUTTON_KEY.
			// 01/11/2008 Paul.  Protect against uninitialized variables.
			if ( String.IsNullOrEmpty(sEntryName) )
				return String.Empty;

			// BEFORE: HttpApplicationState Application = HttpContext.Current.Application;
			//         object oDisplayName = Application[NAME + "." + sEntryName];
			// AFTER:  Use _memoryCache.TryGetValue() for primary culture lookup.
			object? oDisplayName;
			if ( !_memoryCache.TryGetValue(NAME + "." + sEntryName, out oDisplayName) )
				oDisplayName = null;

			if ( oDisplayName == null )
			{
				// 01/11/2007 Paul.  Default to English if term not found.
				// BEFORE: oDisplayName = Application["en-US." + sEntryName];
				// AFTER:  oDisplayName = _memoryCache.Get<object>("en-US." + sEntryName);
				oDisplayName = _memoryCache.Get<object>("en-US." + sEntryName);
				if ( oDisplayName == null )
					return String.Empty;
			}
			// 06/30/2007 Paul.  AccessKey too long, cannot be more than one character.
			// 07/03/2007 Paul.  Protect against an empty AccessKey string.
			string sAccessKey = oDisplayName.ToString() ?? String.Empty;
			if ( sAccessKey.Length == 0 )
				return String.Empty;
			return sAccessKey.Substring(0, 1);
		}

		// =====================================================================================
		// AliasedTerm — instance method using stored _memoryCache
		// =====================================================================================

		/// <summary>
		/// Returns the terminology value for an aliased entry name.
		/// Checks the 'ALIAS_{entryName}' key in IMemoryCache. If an alias mapping exists,
		/// the mapped target entry name is looked up; otherwise the original entry name is used.
		///
		/// BEFORE: HttpApplicationState Application = HttpContext.Current.Application;
		///         object oAliasedName = Application["ALIAS_" + sEntryName];
		/// AFTER:  object oAliasedName = _memoryCache.Get&lt;object&gt;("ALIAS_" + sEntryName);
		/// </summary>
		/// <param name="sEntryName">The entry name that may have an alias mapping in IMemoryCache.</param>
		/// <returns>
		/// Localized string for the aliased target if an alias exists;
		/// otherwise the localized string for the original entry name.
		/// Empty string if sEntryName is null or empty.
		/// </returns>
		public string AliasedTerm(string sEntryName)
		{
			// 01/11/2008 Paul.  Protect against uninitialized variables.
			if ( String.IsNullOrEmpty(sEntryName) )
				return String.Empty;

			// BEFORE: HttpApplicationState Application = HttpContext.Current.Application;
			//         object oAliasedName = Application["ALIAS_" + sEntryName];
			// AFTER:  _memoryCache.Get<object>("ALIAS_" + sEntryName)
			object? oAliasedName = _memoryCache.Get<object>("ALIAS_" + sEntryName);
			if ( oAliasedName == null )
				return Term(sEntryName);
			return Term(oAliasedName.ToString() ?? sEntryName);
		}

		// =====================================================================================
		// SetTerm — static methods for populating terminology cache
		// =====================================================================================

		/// <summary>
		/// Preserved no-op overload for API compatibility.
		/// BEFORE: SetTerm(HttpContext.Current.Application, sLANG, sMODULE_NAME, sNAME, sDISPLAY_NAME)
		/// AFTER:  HttpContext.Current.Application is not available in .NET 10 ASP.NET Core.
		///         This overload performs no operation. Callers must migrate to:
		///         SetTerm(IMemoryCache, string, string, string, string).
		/// </summary>
		public static void SetTerm(string sLANG, string sMODULE_NAME, string sNAME, string sDISPLAY_NAME)
		{
			// 01/20/2009 Paul.  We need to pass the Application to the Term function.
			// BEFORE: SetTerm(HttpContext.Current.Application, sLANG, sMODULE_NAME, sNAME, sDISPLAY_NAME);
			// AFTER:  HttpContext.Current is not available in .NET 10 ASP.NET Core.
			//         Preserved as no-op for API compatibility. Callers must migrate to the IMemoryCache overload.
		}

		/// <summary>
		/// Populates a terminology module label in IMemoryCache for the given language, module, and name.
		/// Sets the key '{sLANG}.{sMODULE_NAME}.{sNAME}' = sDISPLAY_NAME.
		///
		/// BEFORE: Application[sLANG + "." + sMODULE_NAME + "." + sNAME] = sDISPLAY_NAME
		/// AFTER:  memoryCache.Set(sLANG + "." + sMODULE_NAME + "." + sNAME, sDISPLAY_NAME)
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache to populate. Replaces HttpApplicationState from the .NET Framework version.
		/// </param>
		/// <param name="sLANG">Language/culture code (e.g. "en-US").</param>
		/// <param name="sMODULE_NAME">Module name (e.g. "Accounts", "Contacts").</param>
		/// <param name="sNAME">Terminology entry name (e.g. "LBL_ACCOUNT_NAME").</param>
		/// <param name="sDISPLAY_NAME">Localized display value to cache.</param>
		public static void SetTerm(IMemoryCache memoryCache, string sLANG, string sMODULE_NAME, string sNAME, string sDISPLAY_NAME)
		{
			// BEFORE: Application[sLANG + "." + sMODULE_NAME + "." + sNAME] = sDISPLAY_NAME;
			// AFTER:  memoryCache.Set(...) — no expiration, matching Application[] lifetime behavior.
			memoryCache.Set(sLANG + "." + sMODULE_NAME + "." + sNAME, (object)sDISPLAY_NAME);
		}

		/// <summary>
		/// Preserved no-op overload for API compatibility.
		/// BEFORE: SetTerm(HttpContext.Current.Application, sLANG, sMODULE_NAME, sLIST_NAME, sNAME, sDISPLAY_NAME)
		/// AFTER:  HttpContext.Current.Application is not available in .NET 10 ASP.NET Core.
		///         This overload performs no operation. Callers must migrate to:
		///         SetTerm(IMemoryCache, string, string, string, string, string).
		/// </summary>
		public static void SetTerm(string sLANG, string sMODULE_NAME, string sLIST_NAME, string sNAME, string sDISPLAY_NAME)
		{
			// 01/20/2009 Paul.  We need to pass the Application to the Term function.
			// BEFORE: SetTerm(HttpContext.Current.Application, sLANG, sMODULE_NAME, sLIST_NAME, sNAME, sDISPLAY_NAME);
			// AFTER:  HttpContext.Current is not available in .NET 10 ASP.NET Core.
			//         Preserved as no-op for API compatibility. Callers must migrate to the IMemoryCache overload.
		}

		/// <summary>
		/// Populates a terminology list entry in IMemoryCache.
		/// The MODULE_NAME is intentionally set to empty for list terms,
		/// matching the original behavior where list keys do not include the module name component.
		/// Sets the key '{sLANG}..{sLIST_NAME}.{sNAME}' = sDISPLAY_NAME.
		///
		/// BEFORE: sMODULE_NAME = String.Empty;
		///         Application[sLANG + "." + sMODULE_NAME + "." + sLIST_NAME + "." + sNAME] = sDISPLAY_NAME
		/// AFTER:  sMODULE_NAME = String.Empty;
		///         memoryCache.Set(sLANG + "." + sMODULE_NAME + "." + sLIST_NAME + "." + sNAME, sDISPLAY_NAME)
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache to populate. Replaces HttpApplicationState from the .NET Framework version.
		/// </param>
		/// <param name="sLANG">Language/culture code (e.g. "en-US").</param>
		/// <param name="sMODULE_NAME">
		/// Module name — intentionally cleared to empty for list-scoped terminology.
		/// DropDownLists are populated without the module name in the list name.
		/// </param>
		/// <param name="sLIST_NAME">List/dropdown name (e.g. "account_type_dom").</param>
		/// <param name="sNAME">Entry name within the list (e.g. "Analyst").</param>
		/// <param name="sDISPLAY_NAME">Localized display value to cache.</param>
		public static void SetTerm(IMemoryCache memoryCache, string sLANG, string sMODULE_NAME, string sLIST_NAME, string sNAME, string sDISPLAY_NAME)
		{
			// 01/13/2006 Paul.  Don't include MODULE_NAME when used with a list.
			//                   DropDownLists are populated without the module name in the list name.
			// 01/13/2006 Paul.  We can remove the module, but not the dot.
			//                   Otherwise it breaks all other code that references a list term.
			sMODULE_NAME = String.Empty;
			// BEFORE: Application[sLANG + "." + sMODULE_NAME + "." + sLIST_NAME + "." + sNAME] = sDISPLAY_NAME;
			// AFTER:  memoryCache.Set(...) — no expiration, matching Application[] lifetime behavior.
			memoryCache.Set(sLANG + "." + sMODULE_NAME + "." + sLIST_NAME + "." + sNAME, (object)sDISPLAY_NAME);
		}

		// =====================================================================================
		// SetAlias — static method for populating alias mappings
		// =====================================================================================

		/// <summary>
		/// Populates an alias mapping in IMemoryCache that redirects one terminology entry to another.
		/// When a module-level alias (no list name), sets key 'ALIAS_{module}.{name}' = '{targetModule}.{targetName}'.
		/// When a list-level alias, sets key 'ALIAS_{module}.{list}.{name}' = '{targetModule}.{targetList}.{targetName}'.
		///
		/// BEFORE: Application["ALIAS_" + sALIAS_MODULE_NAME + "." + sALIAS_NAME] = sMODULE_NAME + "." + sNAME
		/// AFTER:  memoryCache.Set("ALIAS_" + sALIAS_MODULE_NAME + "." + sALIAS_NAME, sMODULE_NAME + "." + sNAME)
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache to populate with alias mappings. Replaces HttpApplicationState.
		/// </param>
		/// <param name="sALIAS_MODULE_NAME">Module name of the alias source.</param>
		/// <param name="sALIAS_LIST_NAME">
		/// List name of the alias source. If empty, creates a module-level alias;
		/// if non-empty, creates a list-level alias.
		/// </param>
		/// <param name="sALIAS_NAME">Entry name of the alias source.</param>
		/// <param name="sMODULE_NAME">Module name of the alias target.</param>
		/// <param name="sLIST_NAME">List name of the alias target (used when sALIAS_LIST_NAME is non-empty).</param>
		/// <param name="sNAME">Entry name of the alias target.</param>
		public static void SetAlias(IMemoryCache memoryCache, string sALIAS_MODULE_NAME, string sALIAS_LIST_NAME, string sALIAS_NAME, string sMODULE_NAME, string sLIST_NAME, string sNAME)
		{
			// BEFORE (simple alias): Application["ALIAS_" + sALIAS_MODULE_NAME + "." + sALIAS_NAME] = sMODULE_NAME + "." + sNAME
			// BEFORE (list alias):   Application["ALIAS_" + sALIAS_MODULE_NAME + "." + sALIAS_LIST_NAME + "." + sALIAS_NAME] = sMODULE_NAME + "." + sLIST_NAME + "." + sNAME
			// AFTER: memoryCache.Set(...) with the same key patterns.
			if ( Sql.IsEmptyString(sALIAS_LIST_NAME) )
				memoryCache.Set("ALIAS_" + sALIAS_MODULE_NAME + "." + sALIAS_NAME, (object)(sMODULE_NAME + "." + sNAME));
			else
				memoryCache.Set("ALIAS_" + sALIAS_MODULE_NAME + "." + sALIAS_LIST_NAME + "." + sALIAS_NAME, (object)(sMODULE_NAME + "." + sLIST_NAME + "." + sNAME));
		}

		// =====================================================================================
		// TermJavaScript — JavaScript-safe term lookup
		// =====================================================================================

		/// <summary>
		/// Returns the localized term for the given entry name with JavaScript-safe character escaping.
		/// Single quotes, double quotes, and CRLF sequences are escaped for safe embedding in JS string literals.
		/// Uses ControlChars.CrLf from SplendidCRM/VisualBasic.cs (VB compatibility helper, "\r\n")
		/// and replaces it with the JavaScript escape sequence @"\r\n".
		/// </summary>
		/// <param name="sEntryName">Terminology entry key to look up and JavaScript-escape.</param>
		/// <returns>JavaScript-safe localized string suitable for embedding in JS string literals.</returns>
		public string TermJavaScript(string sEntryName)
		{
			string sDisplayName = Term(sEntryName);
			sDisplayName = sDisplayName.Replace("\'", "\\\'");
			sDisplayName = sDisplayName.Replace("\"", "\\\"");
			// Replace CRLF with JavaScript-escaped newline literal.
			// ControlChars.CrLf is defined in SplendidCRM/VisualBasic.cs as "\r\n".
			sDisplayName = sDisplayName.Replace(ControlChars.CrLf, @"\r\n");
			return sDisplayName;
		}
	}
}
