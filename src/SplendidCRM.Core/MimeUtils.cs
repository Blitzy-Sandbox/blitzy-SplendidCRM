#nullable disable
using System;
using System.IO;
using System.Text;
using MimeKit;

namespace SplendidCRM
{
	/// <summary>
	/// MIME message construction utility.
	/// Migrated from SplendidCRM/_code/MimeUtils.cs (~400 lines) for .NET 10 ASP.NET Core.
	/// </summary>
	public class MimeUtils
	{
		public MimeUtils() { }

		/// <summary>
		/// Creates a MIME message from the provided parameters.
		/// </summary>
		public MimeMessage CreateMessage(string sFrom, string sTo, string sSubject, string sBody, bool bIsHtml)
		{
			MimeMessage message = new MimeMessage();
			if (!Sql.IsEmptyString(sFrom)) message.From.Add(MailboxAddress.Parse(sFrom));
			if (!Sql.IsEmptyString(sTo))
			{
				string[] arrTo = sTo.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string addr in arrTo)
				{
					string sTrimmed = addr.Trim();
					if (!Sql.IsEmptyString(sTrimmed))
						message.To.Add(MailboxAddress.Parse(sTrimmed));
				}
			}
			message.Subject = sSubject ?? string.Empty;
			BodyBuilder bodyBuilder = new BodyBuilder();
			if (bIsHtml)
				bodyBuilder.HtmlBody = sBody;
			else
				bodyBuilder.TextBody = sBody;
			message.Body = bodyBuilder.ToMessageBody();
			return message;
		}

		/// <summary>
		/// Adds an attachment to a MIME message.
		/// </summary>
		public void AddAttachment(BodyBuilder bodyBuilder, string sFileName, byte[] byData, string sContentType)
		{
			if (byData != null && byData.Length > 0)
			{
				bodyBuilder.Attachments.Add(sFileName, byData, ContentType.Parse(sContentType ?? "application/octet-stream"));
			}
		}
	}
}
