#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SplendidCRM.Web.Authorization
{
	/// <summary>
	/// RecordAuthorizationHandler — implements part of the 4-tier ACL model (Module→Team→Field→Record).
	/// Migrated from SplendidCRM/_code/Security.cs for .NET 10 ASP.NET Core.
	/// </summary>
	public class RecordAuthorizationHandler
	{
		private readonly Security _security;
		private readonly ILogger<RecordAuthorizationHandler> _logger;

		public RecordAuthorizationHandler(Security security, ILogger<RecordAuthorizationHandler> logger)
		{
			_security = security;
			_logger = logger;
		}

		/// <summary>
		/// Checks if the current user has the required access level.
		/// </summary>
		public bool HasAccess(string sModuleName, string sAccessType)
		{
			if (_security.IS_ADMIN)
				return true;
			int nAccess = _security.GetUserAccess(sModuleName, sAccessType);
			return nAccess >= Security.ACL_ACCESS_ENABLED || nAccess == Security.ACL_ACCESS_OWNER || nAccess == Security.ACL_ACCESS_ALL;
		}

		/// <summary>
		/// Checks if the current user owns the specified record.
		/// </summary>
		public bool IsOwner(Guid gASSIGNED_USER_ID)
		{
			return _security.USER_ID == gASSIGNED_USER_ID;
		}
	}
}
