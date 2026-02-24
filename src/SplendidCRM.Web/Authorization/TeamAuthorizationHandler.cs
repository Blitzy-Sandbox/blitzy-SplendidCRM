#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SplendidCRM.Web.Authorization
{
	/// <summary>
	/// TeamAuthorizationHandler — ASP.NET Core authorization pipeline entry point for the Team tier
	/// of the 4-tier ACL model (Module→Team→Field→Record).
	/// Migrated from SplendidCRM/_code/Security.cs for .NET 10 ASP.NET Core.
	///
	/// This handler delegates to Security.GetUserAccess for access-level checks.
	/// The actual team hierarchy SQL predicate injection (filtering query results by team membership)
	/// is performed by Security.Filter() overloads, which append team-aware WHERE clauses to queries.
	/// This separation keeps centralized enforcement in Security.cs while providing the ASP.NET Core
	/// authorization pipeline integration point here.
	/// </summary>
	public class TeamAuthorizationHandler
	{
		private readonly Security _security;
		private readonly ILogger<TeamAuthorizationHandler> _logger;

		public TeamAuthorizationHandler(Security security, ILogger<TeamAuthorizationHandler> logger)
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
