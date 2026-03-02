/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
 *
 * .NET 10 Migration: New file — provides structured HTTP request/response logging for every request.
 * Logs method, path, status code, elapsed time, user identity, and correlation ID.
 * Propagates correlation ID via X-Correlation-ID response header for distributed tracing.
 *********************************************************************************************************************/
#nullable enable
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SplendidCRM.Middleware
{
	/// <summary>
	/// Middleware that logs every HTTP request with structured data:
	///   - HTTP method and path
	///   - Response status code
	///   - Elapsed time in milliseconds
	///   - Authenticated user name (or "anonymous")
	///   - Correlation ID (from X-Correlation-ID header or auto-generated)
	///
	/// Log levels:
	///   - Information for 2xx/3xx responses
	///   - Warning for 4xx responses
	///   - Error for 5xx responses
	///
	/// Registered in Program.cs AFTER UseAuthentication() and BEFORE UseAuthorization()
	/// so that the user identity is populated by the authentication middleware.
	/// </summary>
	public class RequestLoggingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<RequestLoggingMiddleware> _logger;

		/// <summary>
		/// Constructs the middleware with the next delegate and a logger instance.
		/// </summary>
		/// <param name="next">The next middleware in the pipeline.</param>
		/// <param name="logger">Structured logger for HTTP request data.</param>
		public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
		{
			_next   = next   ?? throw new ArgumentNullException(nameof(next));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Processes the HTTP request: reads or generates a correlation ID, starts a stopwatch,
		/// invokes the next middleware, then logs the completed request with timing and status data.
		/// </summary>
		/// <param name="context">The current HTTP context.</param>
		public async Task InvokeAsync(HttpContext context)
		{
			// Read or generate a correlation ID for distributed tracing.
			// If the caller provides X-Correlation-ID, reuse it; otherwise generate a short GUID prefix.
			string correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
			                       ?? Guid.NewGuid().ToString("N")[..12];
			context.Response.Headers["X-Correlation-ID"] = correlationId;

			var sw = Stopwatch.StartNew();
			try
			{
				await _next(context);
			}
			finally
			{
				sw.Stop();
				string user = context.User?.Identity?.Name ?? "anonymous";
				int statusCode = context.Response.StatusCode;

				LogLevel level = statusCode >= 500 ? LogLevel.Error
				               : statusCode >= 400 ? LogLevel.Warning
				               : LogLevel.Information;

				_logger.Log(level,
					"HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [User={User}] [CorrelationId={CorrelationId}]",
					context.Request.Method,
					context.Request.Path.Value,
					statusCode,
					sw.ElapsedMilliseconds,
					user,
					correlationId);
			}
		}
	}
}
