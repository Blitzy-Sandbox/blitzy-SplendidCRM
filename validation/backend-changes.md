# Backend Changes Log

## SplendidCRM React 19 / Vite Migration — Prompt 2 of 3

This document logs all ASP.NET Core C# backend code changes that were required as a last resort to unblock E2E test failures during the frontend modernization from React 18 / Webpack to React 19 / Vite.

### Scope

Backend code changes are **explicitly out of scope** for this prompt. The backend was migrated to .NET 10 in Prompt 1 (confirmed complete: 85.5%, 600 tests passing, zero build errors). Changes here are permitted **ONLY** as a last resort when:

1. An E2E test failure is directly caused by a backend issue
2. The issue **cannot** be resolved by a frontend-only change
3. The change is the absolute minimum necessary to unblock the test

### Change Log

| # | File Changed | Function/Method | Change Description | Failing Workflow | Why Frontend Fix Was Insufficient |
|---|---|---|---|---|---|
| 1 | `src/SplendidCRM.Web/Controllers/RestController.cs` | `GetModuleTerminologyList`, `GetAllTerminology` | Added `bAllowAnonymous` parameter with culture fallback to `en-US` when session culture is unavailable during pre-auth terminology loading. | Workflow 1: Authentication | Frontend requests terminology before login; without anonymous access the 401 blocked app initialization. No frontend-only workaround exists — the endpoint must allow unauthenticated access. |
| 2 | `src/SplendidCRM.Web/Controllers/RestController.cs` | `GetModuleItem`, `GetModuleItemByAudit`, and 4 other single-row endpoints | Fixed JSON response format: unwrap inner `"d"` key from `RowsToDictionary` result and re-wrap as `{d: {results: row}}` matching the contract expected by `SplendidRequest.ts`. | Workflow 2: Sales CRUD (detail view) | The response shape `{d: {d: {results: ...}}}` was double-wrapped. `SplendidRequest.ts` correctly expects `{d: {results: row}}`. The bug was in the controller's response serialization, not the frontend parsing. |
| 3 | `src/SplendidCRM.Core/SplendidCache.cs` | `TAB_MENU` loading methods | Three fixes: (a) added `bHasUserId` flag to conditionally include `USER_ID` in WHERE clause only when sessions table has it; (b) fixed `ORDER BY` to use `MODULE_NAME` instead of non-existent column; (c) fixed `WHERE` clause to filter on `PORTAL_TYPE IS NULL`. | Workflow 1: Authentication | Backend was generating invalid SQL queries against the `SYSTEM_REST_TABLES` view, causing 500 errors during menu loading. SQL query construction is entirely server-side — no frontend fix possible. |
| 4 | `src/SplendidCRM.Web/Controllers/AdminRestController.cs` | `PostAdminTable` (line ~2804) | Fixed swapped `sFILTER` and `sORDER_BY` parameter order in the call to `RestUtil.GetAdminTable`. | Workflow 6: Admin Panel (Users list) | The controller was passing filter as order-by and vice versa, causing SQL errors. Parameter ordering is a server-side concern. |
| 5 | `src/SplendidCRM.Web/Controllers/AdminRestController.cs` | `PostAdminTable` response construction | Replaced `ToJson(cmd)` with `RowsToDictionary(cmd)` + manual `{d: {results, __total}}` construction to prevent double `d` wrapping. | Workflow 6: Admin Panel (Users list) | `ToJson` wraps results in `{d: ...}`, and the controller wrapped that again. The response format is a server-side serialization concern. |
| 6 | `src/SplendidCRM.Core/RestUtil.cs` | `GetAdminTable` (~line 962) | Replaced raw SQL string concatenation of `$filter` value with proper `ConvertODataFilter(sWHERE, cmd)` call, matching the pattern used by `GetModuleTable`. | Workflow 9: Metadata Views (Dynamic Layout Editor) | The `$filter` from OData query was being injected as raw SQL instead of being properly converted. This is a server-side SQL injection vulnerability fix. |
| 7 | `src/SplendidCRM.Core/RestUtil.cs` | `GetAdminTable` (~line 996) | Removed hardcoded `DATE_MODIFIED desc` default ORDER BY. Admin views like `vwEDITVIEWS_FIELDS` lack a `DATE_MODIFIED` column. Now uses empty string, letting `Sql.PageResults` apply its safe `order by 1` fallback. | Workflow 9: Metadata Views (Dynamic Layout Editor) | The `DATE_MODIFIED` column does not exist in admin views served by this method. The default ORDER BY is a server-side concern — the frontend does not control it. |

### Summary

7 backend changes were required across 3 files to unblock E2E test workflows. All changes are minimal bug fixes in response serialization, SQL query construction, and parameter handling. No new features or architectural changes were introduced.

### Policy

- All changes documented here must be reviewed for backport to the Prompt 1 backend codebase
- Each change includes clear justification linking to a specific E2E test failure
- Frontend-only solutions were exhausted before each backend modification
