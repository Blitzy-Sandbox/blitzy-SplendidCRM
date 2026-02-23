#nullable disable
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace SplendidCRM.Web.Controllers
{
	/// <summary>
	/// Admin impersonation controller — from Administration/Impersonation.svc.cs.
	/// </summary>
	[ApiController]
	[Route("Administration/Impersonation.svc")]
	[Authorize]
	public class ImpersonationController : ControllerBase
	{
		private readonly Security _security;
		private readonly SplendidInit _splendidInit;
		private readonly SqlProcs _sqlProcs;
		private readonly ILogger<ImpersonationController> _logger;

		public ImpersonationController(Security security, SplendidInit splendidInit, SqlProcs sqlProcs, ILogger<ImpersonationController> logger)
		{
			_security = security;
			_splendidInit = splendidInit;
			_sqlProcs = sqlProcs;
			_logger = logger;
		}

		/// <summary>
		/// POST Administration/Impersonation.svc/Impersonate — Impersonate a user (admin only).
		/// </summary>
		[HttpPost("Impersonate")]
		public IActionResult Impersonate([FromBody] ImpersonateRequest request)
		{
			if (!_security.IS_ADMIN)
				return StatusCode(403, new { error = "Admin access required for impersonation" });
			try
			{
				var row = _sqlProcs.spUSERS_GetByID(request.USER_ID);
				if (row == null)
					return NotFound(new { error = "User not found" });
				_security.IS_ADMIN_DELEGATE = true;
				_splendidInit.LoginUser(row);
				return Ok(new { d = new { USER_ID = _security.USER_ID, USER_NAME = _security.USER_NAME } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ImpersonationController.Impersonate error");
				return StatusCode(500, new { error = ex.Message });
			}
		}
	}

	public class ImpersonateRequest
	{
		public Guid USER_ID { get; set; }
	}
}
