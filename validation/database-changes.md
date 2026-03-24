# Database Changes Log

## SplendidCRM React 19 / Vite Migration — Prompt 2 of 3

This document logs any SQL Server schema changes (tables, stored procedures, views, indexes, functions, triggers, or data modifications) that were required as a last resort to unblock E2E test failures during the frontend modernization from React 18 / Webpack to React 19 / Vite with hosting decoupling.

### Scope

Database schema changes are **explicitly out of scope** for this prompt. The SQL Server schema is maintained via `SQL Scripts Community/` and is not modified by the frontend migration. Changes here are permitted **ONLY** as a last resort when:

1. An E2E test failure is directly caused by a database schema issue
2. The issue **cannot** be resolved by a frontend-only change
3. The change is the absolute minimum necessary to unblock the test

### Change Log

| Date | Object Type | Object Name | Change Description | Justification | E2E Test Unblocked | Rollback Script |
|------|-------------|-------------|--------------------|---------------|--------------------|-----------------|
| —    | —           | —           | No database changes required | N/A | N/A | N/A |

### Policy

- All schema changes documented here must include a corresponding rollback script
- Changes must be reviewed for inclusion in the canonical `SQL Scripts Community/` directory
- Each change must include clear justification linking to a specific E2E test failure
- Frontend-only solutions must be exhausted before any schema modification is considered
- Table modifications must not break backward compatibility with existing data
