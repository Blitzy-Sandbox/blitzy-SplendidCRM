# Database Changes Log

## SplendidCRM React 19 / Vite Migration — Prompt 2 of 3

This document logs any SQL Server schema changes (tables, stored procedures, views, indexes, functions, triggers, or data modifications) that were required as a last resort to unblock E2E test failures during the frontend modernization from React 18 / Webpack to React 19 / Vite with hosting decoupling.

### Scope

Database schema changes are **explicitly out of scope** for this prompt. The SQL Server schema is maintained via `SQL Scripts Community/` and is not modified by the frontend migration. Changes here are permitted **ONLY** as a last resort when:

1. An E2E test failure is directly caused by a database schema issue
2. The issue **cannot** be resolved by a frontend-only change
3. The change is the absolute minimum necessary to unblock the test

### Change Log

| # | Object Type | Object Name | Change Description | Failing Workflow | Why Frontend/Backend Fix Was Insufficient | Rollback Script |
|---|---|---|---|---|---|---|
| 1 | Table | `dbo.SplendidSessions` | Created session storage table for ASP.NET Core distributed SQL session provider. Columns: `SessionId NVARCHAR(88) PK`, `Value VARBINARY(MAX)`, `ExpiresAtTime DATETIMEOFFSET`, `SlidingExpirationInSeconds BIGINT NULL`, `AbsoluteExpiration DATETIMEOFFSET NULL`. | Workflow 1: Authentication | The .NET 10 backend uses `SESSION_PROVIDER=SqlServer` for distributed session storage. Without this table, every login attempt fails with a SQL exception. The table is a required infrastructure artifact for the .NET 10 migration (should have been included in Prompt 1 schema). | `DROP TABLE dbo.SplendidSessions;` |

### Summary

1 database schema change was required: creation of the `dbo.SplendidSessions` table for ASP.NET Core distributed session management. This table is a standard infrastructure requirement for .NET Core SQL Server session state and should be considered for inclusion in the canonical `SQL Scripts Community/` directory.

### Note

The `scripts/build-and-run.sh` script automatically creates this table during schema provisioning if it does not already exist, ensuring idempotent database setup.

### Policy

- All schema changes documented here include a corresponding rollback script
- Changes must be reviewed for inclusion in the canonical `SQL Scripts Community/` directory
- Each change includes clear justification linking to a specific E2E test failure
- Frontend-only solutions were exhausted before any schema modification
- Table modifications do not break backward compatibility with existing data
