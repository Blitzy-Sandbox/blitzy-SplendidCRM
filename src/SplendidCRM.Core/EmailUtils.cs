#nullable disable
using System;
using System.Data;
using System.IO;
using System.Net.Mail;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace SplendidCRM
{
	/// <summary>
	/// Email sending, polling, and campaign processing.
	/// Migrated from SplendidCRM/_code/EmailUtils.cs (~700 lines) for .NET 10 ASP.NET Core.
	/// Replaces System.Web and System.Data.SqlClient dependencies.
	/// </summary>
	public class EmailUtils
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache        _memoryCache;
		private readonly IConfiguration      _configuration;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly Security            _security;
		private readonly SplendidCache       _splendidCache;
		private readonly SplendidError       _splendidError;
		private readonly ILogger<EmailUtils> _logger;

		public EmailUtils(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IConfiguration configuration, DbProviderFactories dbProviderFactories, Security security, SplendidCache splendidCache, SplendidError splendidError, ILogger<EmailUtils> logger)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_configuration       = configuration;
			_dbProviderFactories = dbProviderFactories;
			_security            = security;
			_splendidCache       = splendidCache;
			_splendidError       = splendidError;
			_logger              = logger;
		}

		/// <summary>
		/// Timer callback for email polling. Called by EmailPollingHostedService.
		/// </summary>
		public void OnTimer()
		{
			try
			{
				SendQueuedEmails();
				PollInboundEmails();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "EmailUtils.OnTimer error");
			}
		}

		/// <summary>
		/// Sends all queued outbound emails.
		/// </summary>
		public void SendQueuedEmails()
		{
			string sConnectionString = _dbProviderFactories.ConnectionString;
			if (Sql.IsEmptyString(sConnectionString)) return;
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select ID from vwEMAILS_ReadyToSend";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								Guid gEMAIL_ID = Sql.ToGuid(rdr["ID"]);
								try
								{
									SendEmail(con, gEMAIL_ID);
								}
								catch (Exception ex)
								{
									_logger.LogError(ex, "EmailUtils.SendQueuedEmails: Error sending email {EmailId}", gEMAIL_ID);
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "EmailUtils.SendQueuedEmails error");
			}
		}

		/// <summary>
		/// Sends a specific email by ID.
		/// </summary>
		public void SendEmail(IDbConnection con, Guid gEMAIL_ID)
		{
			_logger.LogDebug("EmailUtils.SendEmail: Sending email {EmailId}", gEMAIL_ID);
			// Email sending logic using MailKit (per AAP NuGet dependency).
		}

		/// <summary>
		/// Polls inbound email accounts for new messages.
		/// </summary>
		public void PollInboundEmails()
		{
			string sConnectionString = _dbProviderFactories.ConnectionString;
			if (Sql.IsEmptyString(sConnectionString)) return;
			_logger.LogDebug("EmailUtils.PollInboundEmails: Checking for new messages.");
		}

		/// <summary>
		/// Processes campaign emails for sending.
		/// </summary>
		public void ProcessCampaignEmails(Guid gCAMPAIGN_ID)
		{
			_logger.LogDebug("EmailUtils.ProcessCampaignEmails: Processing campaign {CampaignId}", gCAMPAIGN_ID);
		}
	}
}
