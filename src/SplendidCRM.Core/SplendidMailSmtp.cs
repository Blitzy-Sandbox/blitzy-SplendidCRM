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
//   3. REPLACED: HttpRuntime.Cache.Get("SMTP.X509Certificate")
//             → IMemoryCache.TryGetValue("SMTP.X509Certificate", out X509Certificate cert)
//              (HttpRuntime.Cache → IMemoryCache cross-cutting migration, AAP §0.7.2)
//   4. REPLACED: HttpRuntime.Cache.Insert(..., SplendidCache.DefaultCacheExpiration(), Cache.NoSlidingExpiration)
//             → IMemoryCache.Set(..., new MemoryCacheEntryOptions().SetAbsoluteExpiration(splendidCache.DefaultCacheExpiration()))
//   5. REPLACED: System.Web.Caching.Cache.NoSlidingExpiration → MemoryCacheEntryOptions with no sliding window
//              (only absolute expiration is set; no SlidingExpiration property means no sliding window)
//   6. ADDED: DI constructor (IHttpContextAccessor, IMemoryCache, IConfiguration, Security, SplendidCache)
//             for ASP.NET Core DI registration in Program.cs.
//   7. ADDED: Factory constructors with IMemoryCache replacing HttpApplicationState.
//   8. PRESERVED: Security.DecryptPassword() called on CONFIG.smtppass when password is non-empty,
//              preserving original constructor logic from SplendidMailSmtp(HttpApplicationState Application).
//   9. PRESERVED: SplendidError.SystemMessage() + Utils.ExpandException() for X509 certificate error logging
//              (replacing the original SplendidError.SystemMessage(Application, "Error", ...)).
//  10. PRESERVED: class inherits SplendidMailClient; override Send(MailMessage) delegates to smtpClient.Send().
//  11. PRESERVED: All SmtpClient configuration logic (SSL, timeout, credentials, X509 certificate).
//  12. NOTE: In factory constructors that do not receive a Security instance from DI, Security is
//             instantiated inline with null IHttpContextAccessor — this is safe because
//             Security.DecryptPassword(string) only reads from IMemoryCache, not from IHttpContextAccessor.
//  13. NOTE: In factory constructors that do not receive a SplendidCache instance from DI, the
//             X509 certificate cache entry falls back to DateTimeOffset.Now.AddDays(1) — the same
//             value returned by SplendidCache.DefaultCacheExpiration(), preserving behavioural parity.
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
	/// Standard SMTP mail transport client backed by <see cref="System.Net.Mail.SmtpClient"/>.
	/// Reads SMTP server configuration (server, port, authentication, SSL, credentials, X509 certificate)
	/// from the application <see cref="IMemoryCache"/> under the CONFIG.smtp* key family, which mirrors
	/// the original <c>HttpApplicationState Application["CONFIG.*"]</c> pattern of .NET Framework 4.8.
	///
	/// <para>Migration summary:</para>
	/// <list type="bullet">
	///   <item><description>
	///     <c>HttpApplicationState Application</c> constructor parameter →
	///     <c>IMemoryCache memoryCache</c> (cross-cutting Application[] → IMemoryCache migration, AAP §0.7.2)
	///   </description></item>
	///   <item><description>
	///     <c>HttpRuntime.Cache</c> for X509Certificate caching →
	///     <c>IMemoryCache.TryGetValue / IMemoryCache.Set</c> with absolute expiration
	///   </description></item>
	///   <item><description>
	///     All SMTP business logic, SSL handling, and credential configuration is preserved exactly.
	///   </description></item>
	/// </list>
	/// </summary>
	public class SplendidMailSmtp : SplendidMailClient
	{
		/// <summary>
		/// The configured <see cref="System.Net.Mail.SmtpClient"/>.
		/// Initialised in every constructor via <see cref="CreateSmtpClient"/>.
		/// </summary>
		private SmtpClient smtpClient;

		// =====================================================================================
		// Constructors
		// =====================================================================================

		/// <summary>
		/// Full DI constructor — resolved from the ASP.NET Core service container when
		/// Program.cs registers: <c>builder.Services.AddSingleton&lt;SplendidMailSmtp&gt;()</c>.
		///
		/// <para>.NET 10 Migration:</para>
		/// <list type="bullet">
		///   <item><description>
		///     <c>HttpApplicationState Application</c> → <c>IMemoryCache memoryCache</c>
		///   </description></item>
		///   <item><description>
		///     <c>Security.DecryptPassword(Application, sSmtpPassword)</c> →
		///     <c>security.DecryptPassword(sSmtpPassword)</c> (instance call on injected Security)
		///   </description></item>
		///   <item><description>
		///     <c>SplendidCache.DefaultCacheExpiration()</c> passed to <see cref="CreateSmtpClient"/>
		///     for proper X509 certificate cache lifetime.
		///   </description></item>
		/// </list>
		/// </summary>
		/// <param name="httpContextAccessor">Replaces HttpContext.Current static access.</param>
		/// <param name="memoryCache">Replaces Application[] state for CONFIG.* settings.</param>
		/// <param name="configuration">ASP.NET Core configuration; passed to base class.</param>
		/// <param name="security">Security service; provides <c>DecryptPassword</c> for CONFIG.smtppass.</param>
		/// <param name="splendidCache">Cache service; provides <c>DefaultCacheExpiration</c> for X509 cert TTL.</param>
		public SplendidMailSmtp(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache,
			IConfiguration       configuration,
			Security             security,
			SplendidCache        splendidCache)
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

			// 01/12/2008 Paul.  We must decrypt the password before using it.
			// .NET 10 Migration: Security.DecryptPassword(Application, sSmtpPassword)
			//                  → security.DecryptPassword(sSmtpPassword)  [instance on injected Security]
			if ( !Sql.IsEmptyString(sSmtpPassword) )
				sSmtpPassword = security.DecryptPassword(sSmtpPassword);

			smtpClient = CreateSmtpClient(
				memoryCache, sSmtpServer, nSmtpPort, bSmtpAuthReq, bSmtpSSL,
				sSmtpUser, sSmtpPassword, sX509Certificate, splendidCache);
		}

		/// <summary>
		/// Factory constructor — used by <see cref="SplendidMailClient.CreateMailClient(IMemoryCache)"/>
		/// when <c>CONFIG.mail_sendtype</c> is empty (default SMTP transport).
		///
		/// <para>Reads all SMTP configuration from <see cref="IMemoryCache"/> including user credentials.
		/// Decrypts CONFIG.smtppass via an inline Security instance (safe because
		/// <c>Security.DecryptPassword</c> only reads from <c>IMemoryCache</c> and does not require
		/// <c>IHttpContextAccessor</c>).</para>
		///
		/// <para>.NET 10 Migration: <c>HttpApplicationState Application</c> → <c>IMemoryCache memoryCache</c>.</para>
		/// </summary>
		/// <param name="memoryCache">Application memory cache containing CONFIG.* SMTP settings.</param>
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

			// 01/12/2008 Paul.  We must decrypt the password before using it.
			// Security is instantiated with null IHttpContextAccessor because DecryptPassword only
			// reads CONFIG.InboundEmailKey / CONFIG.InboundEmailIV from IMemoryCache; the HTTP context
			// is not accessed during decryption. This avoids a circular DI dependency in factory paths.
			if ( !Sql.IsEmptyString(sSmtpPassword) )
				sSmtpPassword = new Security(null, memoryCache).DecryptPassword(sSmtpPassword);

			// SplendidCache is not available in this factory constructor; CreateSmtpClient falls back
			// to DateTimeOffset.Now.AddDays(1) for the X509 certificate expiration — which is the
			// same value returned by SplendidCache.DefaultCacheExpiration().
			smtpClient = CreateSmtpClient(
				memoryCache, sSmtpServer, nSmtpPort, bSmtpAuthReq, bSmtpSSL,
				sSmtpUser, sSmtpPassword, sX509Certificate, null);
		}

		/// <summary>
		/// Overloaded factory constructor — accepts explicit server/port/auth/SSL parameters
		/// while reading user credentials from <see cref="IMemoryCache"/>.
		/// Falls back to cache values when <paramref name="sSmtpServer"/> is empty.
		///
		/// <para>.NET 10 Migration: <c>HttpApplicationState Application</c> → <c>IMemoryCache memoryCache</c>.</para>
		/// </summary>
		/// <param name="memoryCache">Application memory cache for credentials and fallback server config.</param>
		/// <param name="sSmtpServer">SMTP server hostname; falls back to CONFIG.smtpserver if empty.</param>
		/// <param name="nSmtpPort">SMTP port; falls back to CONFIG.smtpport when sSmtpServer is empty.</param>
		/// <param name="bSmtpAuthReq">Whether the server requires SMTP authentication.</param>
		/// <param name="bSmtpSSL">Whether to use SSL/TLS for the SMTP connection.</param>
		public SplendidMailSmtp(IMemoryCache memoryCache, string sSmtpServer, int nSmtpPort, bool bSmtpAuthReq, bool bSmtpSSL)
			: base(memoryCache)
		{
			// .NET 10 Migration: Application["CONFIG.*"] → memoryCache.Get<object>("CONFIG.*")
			string sSmtpUser        = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpuser"       ));
			string sSmtpPassword    = Sql.ToString (memoryCache.Get<object>("CONFIG.smtppass"       ));
			string sX509Certificate = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpcertificate"));

			// 01/12/2008 Paul.  We must decrypt the password before using it.
			if ( !Sql.IsEmptyString(sSmtpPassword) )
				sSmtpPassword = new Security(null, memoryCache).DecryptPassword(sSmtpPassword);

			// If no explicit server was provided, fall back entirely to cached system configuration.
			if ( Sql.IsEmptyString(sSmtpServer) )
			{
				sSmtpServer  = Sql.ToString (memoryCache.Get<object>("CONFIG.smtpserver"  ));
				nSmtpPort    = Sql.ToInteger(memoryCache.Get<object>("CONFIG.smtpport"    ));
				bSmtpAuthReq = Sql.ToBoolean(memoryCache.Get<object>("CONFIG.smtpauth_req"));
				bSmtpSSL     = Sql.ToBoolean(memoryCache.Get<object>("CONFIG.smtpssl"     ));
			}
			smtpClient = CreateSmtpClient(
				memoryCache, sSmtpServer, nSmtpPort, bSmtpAuthReq, bSmtpSSL,
				sSmtpUser, sSmtpPassword, sX509Certificate, null);
		}

		/// <summary>
		/// Overloaded factory constructor — accepts all SMTP parameters explicitly.
		/// No cache reads are performed; the caller supplies all credentials pre-resolved.
		/// The password is assumed to be in plaintext (already decrypted by the caller).
		///
		/// <para>.NET 10 Migration: <c>HttpApplicationState Application</c> → <c>IMemoryCache memoryCache</c>
		/// (required only for X509 certificate caching in CreateSmtpClient).</para>
		/// </summary>
		/// <param name="memoryCache">Application memory cache (used for X509 cert caching only).</param>
		/// <param name="sSmtpServer">SMTP server hostname.</param>
		/// <param name="nSmtpPort">SMTP port number.</param>
		/// <param name="bSmtpAuthReq">Whether authenticated SMTP is required.</param>
		/// <param name="bSmtpSSL">Whether SSL/TLS is required.</param>
		/// <param name="sSmtpUser">SMTP username for authentication.</param>
		/// <param name="sSmtpPassword">SMTP password in plaintext (caller-decrypted).</param>
		/// <param name="sX509Certificate">PEM-encoded X509 certificate for SSL client auth (may be empty).</param>
		public SplendidMailSmtp(IMemoryCache memoryCache, string sSmtpServer, int nSmtpPort, bool bSmtpAuthReq, bool bSmtpSSL, string sSmtpUser, string sSmtpPassword, string sX509Certificate)
			: base(memoryCache)
		{
			// 07/19/2010 Paul.  This constructor provides a way to skip the decryption of the system password.
			// All parameters are supplied by the caller; no decryption is performed here.
			smtpClient = CreateSmtpClient(
				memoryCache, sSmtpServer, nSmtpPort, bSmtpAuthReq, bSmtpSSL,
				sSmtpUser, sSmtpPassword, sX509Certificate, null);
		}

		// =====================================================================================
		// Private helpers
		// =====================================================================================

		// 07/19/2010 Paul.  Create a new method so we can provide a way to skip the decryption of the system password.
		// 07/18/2013 Paul.  Add support for multiple outbound emails.
		/// <summary>
		/// Creates and configures a <see cref="System.Net.Mail.SmtpClient"/> from the supplied parameters.
		///
		/// <para>.NET 10 Migration changes in this method:</para>
		/// <list type="bullet">
		///   <item><description>
		///     <c>HttpRuntime.Cache.Get("SMTP.X509Certificate")</c>
		///     → <c>memoryCache.TryGetValue("SMTP.X509Certificate", out X509Certificate cert)</c>
		///   </description></item>
		///   <item><description>
		///     <c>HttpRuntime.Cache.Insert("SMTP.X509Certificate", cert, null, SplendidCache.DefaultCacheExpiration(), Cache.NoSlidingExpiration)</c>
		///     → <c>memoryCache.Set("SMTP.X509Certificate", cert, new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiration))</c>
		///     where <c>expiration</c> = <c>splendidCache.DefaultCacheExpiration()</c> when available,
		///     or <c>DateTimeOffset.Now.AddDays(1)</c> (the same value) in factory paths.
		///   </description></item>
		///   <item><description>
		///     <c>SplendidError.SystemMessage(Application, "Error", ...)</c>
		///     → <c>SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), ...)</c>
		///   </description></item>
		///   <item><description>
		///     <c>Utils.ExpandException(ex)</c> preserved (static call, no framework dependency).
		///   </description></item>
		/// </list>
		/// </summary>
		/// <param name="memoryCache">Cache for X509 certificate lookup and storage.</param>
		/// <param name="sSmtpServer">SMTP server hostname (defaulted to 127.0.0.1 if empty).</param>
		/// <param name="nSmtpPort">SMTP port (defaulted to 25 if zero).</param>
		/// <param name="bSmtpAuthReq">True when SMTP authentication is required.</param>
		/// <param name="bSmtpSSL">True when SSL/TLS is required.</param>
		/// <param name="sSmtpUser">SMTP username for credential-based authentication.</param>
		/// <param name="sSmtpPassword">SMTP password in plaintext.</param>
		/// <param name="sX509Certificate">PEM-encoded X509 certificate string (may be empty).</param>
		/// <param name="splendidCache">Optional cache service for DefaultCacheExpiration(); null falls back to 1-day TTL.</param>
		/// <returns>A configured <see cref="System.Net.Mail.SmtpClient"/> ready for sending.</returns>
		private SmtpClient CreateSmtpClient(
			IMemoryCache  memoryCache,
			string        sSmtpServer,
			int           nSmtpPort,
			bool          bSmtpAuthReq,
			bool          bSmtpSSL,
			string        sSmtpUser,
			string        sSmtpPassword,
			string        sX509Certificate,
			SplendidCache splendidCache)
		{
			// 01/12/2008 Paul.  We must decrypt the password before using it.
			// 02/02/2017 Paul.  Password is always in non-encrypted format at this point.
			// The constructors that read from CONFIG.smtppass already call Security.DecryptPassword
			// before invoking CreateSmtpClient, so decryption is intentionally omitted here.
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
						// .NET 10 Migration: HttpRuntime.Cache.Get("SMTP.X509Certificate") as X509Certificate
						//                 → memoryCache.TryGetValue("SMTP.X509Certificate", out X509Certificate cert)
						if ( !memoryCache.TryGetValue("SMTP.X509Certificate", out X509Certificate cert) )
						{
							const string sCertHeader = "-----BEGIN CERTIFICATE-----";
							const string sCertFooter = "-----END CERTIFICATE-----";
							sX509Certificate = sX509Certificate.Trim();
							if ( sX509Certificate.StartsWith(sCertHeader) && sX509Certificate.EndsWith(sCertFooter) )
							{
								sX509Certificate = sX509Certificate.Substring(
									sCertHeader.Length,
									sX509Certificate.Length - sCertHeader.Length - sCertFooter.Length);
								byte[] byPKS8 = Convert.FromBase64String(sX509Certificate.Trim());
								cert = new X509Certificate(byPKS8);
							}
							else
							{
								throw new Exception("Invalid X509 Certificate.  Missing BEGIN CERTIFICATE or END CERTIFICATE.");
							}

							// .NET 10 Migration: HttpRuntime.Cache.Insert(..., SplendidCache.DefaultCacheExpiration(), Cache.NoSlidingExpiration)
							//                 → memoryCache.Set(..., new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiration))
							// When a SplendidCache instance is available (DI path), use its DefaultCacheExpiration() for
							// consistent cross-service cache TTL governance. Factory-path constructors pass null and fall
							// back to an equivalent 1-day absolute expiration (the same value DefaultCacheExpiration returns).
							DateTimeOffset expiration = (splendidCache != null)
								? splendidCache.DefaultCacheExpiration()
								: DateTimeOffset.Now.AddDays(1);
							memoryCache.Set(
								"SMTP.X509Certificate",
								cert,
								new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiration));
						}

						if ( cert != null )
							client.ClientCertificates.Add(cert);
					}
					catch ( Exception ex )
					{
						// .NET 10 Migration: SplendidError.SystemMessage(Application, "Error", new StackTrace(true).GetFrame(0), ...)
						//                 → SplendidError.SystemMessage("Error", new StackTrace(true).GetFrame(0), ...)
						// Utils.ExpandException(ex) preserved — formats exception + inner exceptions into a human-readable string.
						SplendidError.SystemMessage(
							"Error",
							new StackTrace(true).GetFrame(0),
							"Failed to add SMTP certificate to email: " + Utils.ExpandException(ex));
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

		// =====================================================================================
		// Send
		// =====================================================================================

		/// <summary>
		/// Sends the mail message via the configured SMTP server.
		/// Delegates directly to <see cref="System.Net.Mail.SmtpClient.Send(MailMessage)"/>.
		/// </summary>
		/// <param name="mail">The <see cref="MailMessage"/> to transmit.</param>
		/// <exception cref="System.Net.Mail.SmtpException">
		///   Thrown when the SMTP server is unavailable, rejects the connection, or refuses the message.
		/// </exception>
		override public void Send(MailMessage mail)
		{
			smtpClient.Send(mail);
		}
	}
}
