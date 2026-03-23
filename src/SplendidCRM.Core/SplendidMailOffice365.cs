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
// .NET 10 Migration: SplendidCRM/_code/SplendidMailOffice365.cs → src/SplendidCRM.Core/SplendidMailOffice365.cs
// Changes applied (minimal change clause — only framework migration changes):
//   1. REMOVED: using System.Web; (System.Web not available in .NET 10)
//   2. REPLACED: HttpApplicationState Application constructor parameter → IMemoryCache memoryCache
//              (Application[] → IMemoryCache cross-cutting migration, AAP §0.7.2)
//   3. ADDED: base(memoryCache) call to initialize protected SplendidMailClient fields.
//   4. PRESERVED: class inherits SplendidMailClient, override Send() pattern unchanged.
//   5. PRESERVED: Exception message ("Office 365 integration is not supported.") preserved as-is.
//              Community Edition stub — Enterprise Edition provides the full Microsoft Graph implementation.
#nullable disable
using System;
using System.Net.Mail;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Office 365 mail transport client (Community Edition stub).
	/// The full Microsoft Graph OAuth 2.0 implementation is provided by the Enterprise Edition only.
	/// All operations throw an exception in the Community Edition.
	///
	/// Migration: SplendidCRM/_code/SplendidMailOffice365.cs
	///   - HttpApplicationState Application → IMemoryCache memoryCache
	///   - Inherits from SplendidMailClient (preserved)
	///   - override Send(MailMessage) preserved with identical exception message
	/// </summary>
	public class SplendidMailOffice365 : SplendidMailClient
	{
		/// <summary>
		/// Initialises the Office 365 mail client.
		/// .NET 10 Migration: HttpApplicationState Application replaced with IMemoryCache memoryCache.
		///
		/// Community Edition: throws immediately to indicate the integration is not supported.
		/// </summary>
		/// <param name="memoryCache">
		///   In-process memory cache (replaces Application[] in .NET Framework).
		/// </param>
		/// <param name="gOAUTH_TOKEN_ID">
		///   OAuth token bucket GUID; preserved from original constructor signature.
		///   In the Enterprise Edition this identifies the OAuth 2.0 token record for the
		///   Office 365 service account (ExchangeUtils.EXCHANGE_ID).
		/// </param>
		/// <exception cref="Exception">Always thrown — Office 365 integration is not supported.</exception>
		public SplendidMailOffice365(IMemoryCache memoryCache, Guid gOAUTH_TOKEN_ID)
			: base(memoryCache)
		{
			throw new Exception("Office 365 integration is not supported.");
		}

		/// <inheritdoc/>
		/// <exception cref="Exception">Always thrown — Office 365 integration is not supported.</exception>
		override public void Send(MailMessage mail)
		{
			throw new Exception("Office 365 integration is not supported.");
		}
	}
}
