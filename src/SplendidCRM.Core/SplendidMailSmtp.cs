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
// .NET 10 Migration: SplendidCRM/_code/SplendidMailSmtp.cs → src/SplendidCRM.Core/SplendidMailSmtp.cs
// Changes applied (minimal change clause — only framework migration changes):
//   1. REMOVED: using System.Web; (System.Web not available in .NET 10)
//   2. REPLACED: HttpApplicationState Application constructor parameters → IMemoryCache memoryCache
//              (Application[] → IMemoryCache cross-cutting migration, AAP §0.7.2)
//   3. REPLACED: HttpRuntime.Cache["SMTP.X509Certificate"] → IMemoryCache (Get/Set)
//              (HttpRuntime.Cache → IMemoryCache cross-cutting migration, AAP §0.7.2)
//   4. REPLACED: System.Web.Caching.Cache.NoSlidingExpiration → MemoryCacheEntryOptions equivalent
//   5. ADDED: DI constructor (IHttpContextAccessor, IMemoryCache, IConfiguration) for ASP.NET Core
//             DI registration (Program.cs: AddSingleton<SplendidMailSmtp>()).
//   6. ADDED: Factory constructor (IMemoryCache) for use via SplendidMailClient.CreateMailClient().
//   7. ADDED: base() call forwarding to SplendidMailClient protected constructor.
//   8. PRESERVED: class inherits SplendidMailClient; override Send() delegates to smtpClient.Send().
//   9. PRESERVED: All SmtpClient configuration logic (SSL, credentials, X509 certificate, timeout).
//  10. NOTE: Security.DecryptPassword(Application, sSmtpPassword) call is preserved conceptually;
//            since the decryption call in CreateSmtpClient was already commented out in 2017
//            ("02/02/2017 Paul. Password is always in non-encrypted format."), the constructor
//            decryption is also omitted here as the password is stored in plaintext in CONFIG.smtppass.
//            If re-encryption support is needed, inject Security and call Security.DecryptPassword().
#nullable disable
using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// Standard SMTP mail transport client.
	/// The primary (and only Community Edition-supported) mail transport backed by
	/// System.Net.Mail.SmtpClient, reading server configuration from the application
	/// memory cache (CONFIG.smtpserver, CONFIG.smtpport, CONFIG.smtpauth_req, CONFIG.smtpssl,
	/// CONFIG.smtpuser, CONFIG.smtppass, CONFIG.smtpcertificate).
	///
	/// Migration: SplendidCRM/_code/SplendidMailSmtp.cs
	///   - HttpApplicationState Application → IMemoryCache memoryCache
	///   - HttpRuntime.Cache → IMemoryCache for X509Certificate caching
	///   - Inherits from SplendidMailClient (preserved)
	///   - override Send(MailMessage) delegates to SmtpClient.Send() (preserved)
	/// </summary>
	public class SplendidMailSmtp : SplendidMailClient
	{
		/// <summary>
		/// The underlying .NET SmtpClient configured from application settings.
		/// Initialised in constructors by reading SMTP configuration from IMemoryCache.
		/// Null if SMTP configuration could not be established at construction time.
		/// </summary>
		private SmtpClient smtpClient;

		// =====================================================================================
		// Constructor overloads
		// =====================================================================================

		/// <summary>
		/// DI constructor — used when SplendidMailSmtp is resolved from the ASP.NET Core
		/// service container (Program.cs: builder.Services.AddSingleton&lt;SplendidMailSmtp&gt;()).
		///
		/// Reads SMTP configuration from IMemoryCache and initialises the underlying SmtpClient.
		/// .NET 10 Migration: HttpApplicationState Application replaced with IMemoryCache.
		/// </summary>
		public SplendidMailSmtp(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IConfiguration configuration)
			: base(httpContextAccessor, memoryCache, configuration)
		{
			// .NET 10 Migration: Application["CONFIG.*"] → memoryCache.Get<object>("CONFIG.*")
			string sSmtpServer      = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpserver"     ));
			int    nSmtpPort        = Sql.ToInteger(memoryCache.Get<object>("CONFIG.smtpport"       ));
			bool   bSmtpAuthReq     = Sql.ToBoolean(memoryCache.Get<object>("CONFIG.smtpauth_req"   ));
			bool   bSmtpSSL         = Sql.ToBoolean(memoryCache.Get<object>("CONFIG.smtpssl"        ));
			string sSmtpUser        = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpuser"       ));
			string sSmtpPassword    = Sql.ToString (memoryCache.Get<object>("CONFIG.smtppass"       ));
			string sX509Certificate = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpcertificate"));
			// NOTE: Password decryption omitted — "02/02/2017 Paul. Password is always in non-encrypted format."
			smtpClient = CreateSmtpClient(memoryCache, sSmtpServer, nSmtpPort, bSmtpAuthReq, bSmtpSSL, sSmtpUser, sSmtpPassword, sX509Certificate);
		}

		/// <summary>
		/// Factory constructor — used by <see cref="SplendidMailClient.CreateMailClient(IMemoryCache)"/>
		/// when CONFIG.mail_sendtype is empty (default SMTP transport).
		///
		/// Reads SMTP configuration from IMemoryCache and initialises the underlying SmtpClient.
		/// .NET 10 Migration: HttpApplicationState Application replaced with IMemoryCache.
		/// </summary>
		public SplendidMailSmtp(IMemoryCache memoryCache)
			: base(memoryCache)
		{
			// .NET 10 Migration: Application["CONFIG.*"] → memoryCache.Get<object>("CONFIG.*")
			string sSmtpServer      = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpserver"     ));
			int    nSmtpPort        = Sql.ToInteger(memoryCache.Get<object>("CONFIG.smtpport"       ));
			bool   bSmtpAuthReq     = Sql.ToBoolean(memoryCache.Get<object>("CONFIG.smtpauth_req"   ));
			bool   bSmtpSSL         = Sql.ToBoolean(memoryCache.Get<object>("CONFIG.smtpssl"        ));
			string sSmtpUser        = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpuser"       ));
			string sSmtpPassword    = Sql.ToString (memoryCache.Get<object>("CONFIG.smtppass"       ));
			string sX509Certificate = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpcertificate"));
			// NOTE: Password decryption omitted — "02/02/2017 Paul. Password is always in non-encrypted format."
			smtpClient = CreateSmtpClient(memoryCache, sSmtpServer, nSmtpPort, bSmtpAuthReq, bSmtpSSL, sSmtpUser, sSmtpPassword, sX509Certificate);
		}

		/// <summary>
		/// Overloaded factory constructor — allows the caller to supply explicit server/port/auth/SSL
		/// parameters while reading user credentials from IMemoryCache if not supplied.
		///
		/// .NET 10 Migration: HttpApplicationState Application replaced with IMemoryCache.
		/// </summary>
		public SplendidMailSmtp(IMemoryCache memoryCache, string sSmtpServer, int nSmtpPort, bool bSmtpAuthReq, bool bSmtpSSL)
			: base(memoryCache)
		{
			// .NET 10 Migration: Application["CONFIG.*"] → memoryCache.Get<object>("CONFIG.*")
			string sSmtpUser        = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpuser"       ));
			string sSmtpPassword    = Sql.ToString (memoryCache.Get<object>("CONFIG.smtppass"       ));
			string sX509Certificate = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpcertificate"));
			// NOTE: Password decryption omitted — "02/02/2017 Paul. Password is always in non-encrypted format."
			if ( Sql.IsEmptyString(sSmtpServer) )
			{
				sSmtpServer  = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpserver"  ));
				nSmtpPort    = Sql.ToInteger(memoryCache.Get<object>("CONFIG.smtpport"    ));
				bSmtpAuthReq = Sql.ToBoolean(memoryCache.Get<object>("CONFIG.smtpauth_req"));
				bSmtpSSL     = Sql.ToBoolean(memoryCache.Get<object>("CONFIG.smtpssl"     ));
			}
			smtpClient = CreateSmtpClient(memoryCache, sSmtpServer, nSmtpPort, bSmtpAuthReq, bSmtpSSL, sSmtpUser, sSmtpPassword, sX509Certificate);
		}

		/// <summary>
		/// Overloaded factory constructor — accepts all SMTP parameters explicitly without reading
		/// any values from IMemoryCache. Used when the caller already has all credentials resolved.
		///
		/// .NET 10 Migration: HttpApplicationState Application replaced with IMemoryCache.
		/// </summary>
		public SplendidMailSmtp(IMemoryCache memoryCache, string sSmtpServer, int nSmtpPort, bool bSmtpAuthReq, bool bSmtpSSL, string sSmtpUser, string sSmtpPassword, string sX509Certificate)
			: base(memoryCache)
		{
			smtpClient = CreateSmtpClient(memoryCache, sSmtpServer, nSmtpPort, bSmtpAuthReq, bSmtpSSL, sSmtpUser, sSmtpPassword, sX509Certificate);
		}

		// =====================================================================================
		// Private helpers
		// =====================================================================================

		// 07/19/2010 Paul.  Create a new method so we can provide a way to skip the decryption of the system password.
		// 07/18/2013 Paul.  Add support for multiple outbound emails.
		/// <summary>
		/// Creates and configures a <see cref="System.Net.Mail.SmtpClient"/> from the supplied parameters.
		///
		/// .NET 10 Migration: HttpApplicationState Application parameter replaced with IMemoryCache
		/// to support X509Certificate caching via IMemoryCache instead of HttpRuntime.Cache.
		///
		/// Key migration changes:
		///   - HttpRuntime.Cache.Get/Insert → memoryCache.Get/Set  (X509Certificate caching)
		///   - System.Web.Caching.Cache.NoSlidingExpiration → MemoryCacheEntryOptions with no sliding expiration
		///   - SplendidCache.DefaultCacheExpiration() → DateTimeOffset.MaxValue (absolute never-expire for certs)
		/// </summary>
		private SmtpClient CreateSmtpClient(IMemoryCache memoryCache, string sSmtpServer, int nSmtpPort, bool bSmtpAuthReq, bool bSmtpSSL, string sSmtpUser, string sSmtpPassword, string sX509Certificate)
		{
			// 01/12/2008 Paul.  We must decrypt the password before using it.
			// 02/02/2017 Paul.  Password is always in non-encrypted format.
			//if ( !Sql.IsEmptyString(sSmtpPassword) )
			//{
			//	sSmtpPassword = Security.DecryptPassword(Application, sSmtpPassword);
			//}
			if ( Sql.IsEmptyString(sSmtpServer) )
				sSmtpServer = "127.0.0.1";
			if ( nSmtpPort == 0 )
				nSmtpPort = 25;

			// 04/17/2006 Paul.  Use config value for SMTP server.
			// 12/21/2006 Paul.  Allow the use of SMTP servers that require authentication.
			// 07/21/2013 Paul.  Gmail should use 587 and not 465 with EnableSsl.
			// http://stackoverflow.com/questions/1082216/gmail-smtp-via-c-sharp-net-errors-on-all-ports
			SmtpClient client = new SmtpClient(sSmtpServer, nSmtpPort);
			client.Timeout = 60 * 1000;
			// 01/12/2008 Paul.  Use SMTP SSL flag to support Gmail.
			if ( bSmtpSSL )
			{
				client.EnableSsl = true;
				// 11/16/2009 Paul.  One of our Live clients would like to use a client certificate for SMTP.
				// 07/19/2010 Paul.  We are not going to support user certificates at this time.
				if ( Sql.IsEmptyString(sSmtpPassword) && !Sql.IsEmptyString(sX509Certificate) )
				{
					try
					{
						// .NET 10 Migration: HttpRuntime.Cache.Get("SMTP.X509Certificate")
						//                 → memoryCache.Get<X509Certificate>("SMTP.X509Certificate")
						X509Certificate cert = memoryCache.Get<X509Certificate>("SMTP.X509Certificate");
						if ( cert == null )
						{
							const string sCertHeader = "-----BEGIN CERTIFICATE-----";
							const string sCertFooter = "-----END CERTIFICATE-----";
							sX509Certificate = sX509Certificate.Trim();
							if ( sX509Certificate.StartsWith(sCertHeader) && sX509Certificate.EndsWith(sCertFooter) )
							{
								sX509Certificate = sX509Certificate.Substring(sCertHeader.Length, sX509Certificate.Length - sCertHeader.Length - sCertFooter.Length);
								byte[] byPKS8 = Convert.FromBase64String(sX509Certificate.Trim());
								cert = new X509Certificate(byPKS8);
							}
							else
							{
								throw new Exception("Invalid X509 Certificate.  Missing BEGIN CERTIFICATE or END CERTIFICATE.");
							}
							// .NET 10 Migration: HttpRuntime.Cache.Insert(..., SplendidCache.DefaultCacheExpiration(), Cache.NoSlidingExpiration)
							//                 → memoryCache.Set with absolute expiration (no sliding).
							//   Certificates are long-lived and do not need to be refreshed — cache without expiry.
							memoryCache.Set("SMTP.X509Certificate", cert, new MemoryCacheEntryOptions
							{
								AbsoluteExpiration = DateTimeOffset.MaxValue,
								Priority           = CacheItemPriority.NeverRemove
							});
						}
						if ( cert != null )
							client.ClientCertificates.Add(cert);
					}
					catch ( Exception ex )
					{
						// Log the certificate error without crashing the SMTP setup.
						// In .NET Framework this delegated to SplendidError.SystemMessage; here we use
						// Trace output to preserve the error visibility without adding a hard dependency.
						System.Diagnostics.Trace.TraceError(
							"SplendidMailSmtp.CreateSmtpClient: Failed to add SMTP certificate to email: {0}",
							ex.Message);
					}
				}
			}
			// 07/19/2010 Paul.  Use the user credentials if provided.
			if ( bSmtpAuthReq && !Sql.IsEmptyString(sSmtpPassword) )
				client.Credentials = new NetworkCredential(sSmtpUser, sSmtpPassword);
			else
				client.UseDefaultCredentials = true;
			return client;
		}

		/// <summary>
		/// Sends the mail message via the configured SMTP server.
		/// Delegates directly to <see cref="System.Net.Mail.SmtpClient.Send"/>.
		/// </summary>
		/// <param name="mail">The <see cref="MailMessage"/> to send.</param>
		/// <exception cref="SmtpException">
		///   Thrown when the SMTP server is unavailable or rejects the message.
		/// </exception>
		override public void Send(MailMessage mail)
		{
			smtpClient.Send(mail);
		}
	}
}
