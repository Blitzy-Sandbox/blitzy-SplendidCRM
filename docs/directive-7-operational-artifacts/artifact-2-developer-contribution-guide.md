# Directive 7, Artifact 2 — Developer Contribution Guide

## SplendidCRM Community Edition v15.2 — Secure Extension Requirements

**Audit Framework:** COSO Internal Control — Integrated Framework (2013), NIST SP 800-53 Rev 5, NIST CSF, CIS Controls v8 IG2/IG3
**Document Type:** Developer-Facing Contribution Guide
**Scope:** 9 Mandatory GATE: PASS/FAIL Checkpoints for Code Contributions

---

## Introduction

This guide defines the **9 mandatory quality gates** that all code contributions to SplendidCRM must pass before acceptance. Each gate is aligned to COSO Internal Control principles and NIST/CIS technical controls. Per COSO Principle 10 (Selects and Develops Control Activities), these gates function as the control activities governing the code contribution process — they are not optional recommendations but required checkpoints.

The SplendidCRM v15.2 codebase audit (Directives 0–6) identified systemic gaps across every dimension of software quality: access control documentation is absent, authentication relies on NIST-deprecated MD5 hashing, audit logging depends on a single SQL Server instance with no fallback, configuration management lacks change tracking, input validation is weakened by disabled ASP.NET protections, error handling has an empty global exception handler, 38 DLLs are manually managed without NuGet, documentation coverage is approximately 0% across ~140 Material components, and zero automated tests exist at any tier. Per COSO Principle 14 (Communicates Internally), these gates ensure new contributions do not perpetuate the deficiencies identified in the audit — see the [Code Quality Summary](../directive-3-code-quality/code-quality-summary.md) and [Documentation Coverage Report](../directive-5-documentation-coverage/documentation-coverage-report.md) for complete findings.

**How to use this guide:** Before submitting any code contribution, self-assess against all 9 gates. Every gate must receive a **PASS** determination. A **FAIL** on any gate blocks the contribution until remediated. Per COSO Principle 10, these gates ARE the control activities for code contributions. All gates are mapped to COSO Principles (1–17), NIST SP 800-53 Rev 5 control families, NIST CSF functions, and CIS Controls v8 IG2/IG3 safeguards per the audit's framework authority hierarchy — where frameworks conflict, the more restrictive requirement applies.

---

## GATE Summary

| GATE | Title | Primary Framework Controls | COSO Principle | NIST CSF Function |
|---|---|---|---|---|
| 1 | Access Control Compliance | NIST AC, CIS Control 6 | Principle 5, 10 | Protect |
| 2 | Authentication Standards | NIST IA, CIS Control 5 | Principle 5 | Protect |
| 3 | Audit Logging Requirements | NIST AU, CIS Control 8 | Principle 16 | Detect |
| 4 | Configuration Management | NIST CM, CIS Control 4 | Principle 10 | Identify, Protect |
| 5 | Input Validation and Encoding | NIST SI, CIS Control 16 | Principle 11 | Protect |
| 6 | Error Handling Standards | NIST SI, CIS Control 16 | Principle 10 | Respond |
| 7 | Dependency Management | NIST CM-3, CIS Control 2 | Principle 9 | Identify |
| 8 | Documentation Requirements | COSO Principle 14 | Principle 14 | Identify |
| 9 | Testing Requirements | NIST CA, CIS Control 16 | Principle 16 | Detect |

---

## GATE 1: Access Control Compliance

**Governing Controls:** NIST AC (Access Control), CIS Control 6 (Access Control Management)
**COSO Principle:** Principle 5 (Enforces Accountability), Principle 10 (Selects and Develops Control Activities)
**NIST CSF Function:** Protect

### Current Codebase Context

SplendidCRM implements a 4-tier authorization model in `Security.cs`: module ACL → team filtering → field-level ACL → record-level ownership. Source: `SplendidCRM/_code/Security.cs:L842-L1303`

The `Security.Filter()` method has 5 overloads that inject WHERE clauses enforcing team/role-based data isolation. Source: `SplendidCRM/_code/Security.cs:L842, L848, L856, L1085, L1303`

API endpoints enforce `Security.IsAuthenticated` before processing. Source: `SplendidCRM/Rest.svc.cs`

Admin endpoints enforce `Security.IS_ADMIN || Security.IS_ADMIN_DELEGATE` before processing. Source: `SplendidCRM/Administration/Rest.svc.cs`

**Audit finding:** The 4-tier model is architecturally effective but has zero documentation explaining its design rationale — a COSO Principle 14 gap identified in the [Documentation Coverage Report](../directive-5-documentation-coverage/documentation-coverage-report.md).

### PASS Criteria

Your contribution receives a **PASS** on GATE 1 when ALL of the following are true:

- [ ] All new data access queries include `Security.Filter()` calls appropriate to the module context
- [ ] All new API endpoints enforce `Security.IsAuthenticated` at the method entry point
- [ ] Admin-only endpoints verify `Security.IS_ADMIN` or `Security.IS_ADMIN_DELEGATE` before processing
- [ ] Field-level access respects `ACL_FIELD_ACCESS` constants (`FULL_ACCESS`, `OWNER_UNOWNED_FULL`, `OWNER_READ_UNOWNED_FULL`, etc.) from `Security.cs`
- [ ] New modules register ACL entries in the `ACL_ACCESS` table structure
- [ ] No direct SQL queries bypass the `Security.Filter()` mechanism
- [ ] Role-based data isolation is maintained — users only see records their team/role grants access to

### FAIL Criteria

Your contribution receives a **FAIL** on GATE 1 if ANY of the following are true:

- Any data access query that does not apply `Security.Filter()` for the appropriate module
- Any API endpoint that processes requests without checking `Security.IsAuthenticated`
- Any admin operation accessible without `IS_ADMIN` verification
- Any hardcoded role or permission bypass

---

## GATE 2: Authentication Standards

**Governing Controls:** NIST IA (Identification and Authentication), CIS Control 5 (Account Management)
**COSO Principle:** Principle 5 (Enforces Accountability)
**NIST CSF Function:** Protect

### Current Codebase Context

SplendidCRM uses MD5 hashing for passwords via `MD5CryptoServiceProvider` — a **CRITICAL** security weakness inherited from SugarCRM backward compatibility. Source: `SplendidCRM/_code/Security.cs:L393-406`

Rijndael symmetric encryption is used for stored credentials via `EncryptPassword`/`DecryptPassword` methods. Source: `SplendidCRM/_code/Security.cs`

Session management is InProc (`sessionState mode="InProc"`). Source: `SplendidCRM/Web.config:L100`

Multi-mechanism authentication is supported: Forms auth, Windows NTLM/SSO via `ActiveDirectory.cs`, and DuoUniversal 2FA. Source: `SplendidCRM/_code/ActiveDirectory.cs`, `SplendidCRM/Administration/DuoUniversal/`

**Audit finding:** MD5 is NIST-deprecated for password hashing (NIST IA-5). This is a Critical finding under COSO Principle 10 — see the [Security Domain Quality Report](../directive-3-code-quality/security-domain-quality.md). New contributions must not extend MD5 usage.

### PASS Criteria

Your contribution receives a **PASS** on GATE 2 when ALL of the following are true:

- [ ] New authentication mechanisms use industry-standard hashing (bcrypt, Argon2, PBKDF2 with SHA-256) — NOT MD5
- [ ] Password handling never stores plaintext credentials
- [ ] Session tokens use secure, HttpOnly, SameSite cookies (consistent with `Global.asax.cs` SameSite hardening at L98-101)
- [ ] Multi-factor authentication paths (DuoUniversal) are preserved — new auth flows must not bypass 2FA
- [ ] All credential-bearing network calls use TLS 1.2+ (enforced at `Global.asax.cs:L98-101`)
- [ ] OAuth/OIDC tokens are validated using standard libraries (not custom parsing)
- [ ] Session fixation is prevented — new session ID on authentication state change

### FAIL Criteria

Your contribution receives a **FAIL** on GATE 2 if ANY of the following are true:

- Any use of MD5 for new password hashing
- Any plaintext credential storage
- Any authentication bypass path
- Any credential transmission over non-TLS channels

---

## GATE 3: Audit Logging Requirements

**Governing Controls:** NIST AU (Audit and Accountability), CIS Control 8 (Audit Log Management)
**COSO Principle:** Principle 16 (Conducts Ongoing and/or Separate Evaluations)
**NIST CSF Function:** Detect

### Current Codebase Context

SplendidCRM logs errors centrally via `SplendidError.cs` → `spSYSTEM_LOG_InsertOnly` → `SYSTEM_LOG` table. Source: `SplendidCRM/_code/SplendidError.cs:L41-L69`

SQL audit triggers auto-generate `_AUDIT` tables for all CRM entities via `BuildAllAuditTables.1.sql`. Source: `SQL Scripts Community/Triggers/BuildAllAuditTables.1.sql`

Login events are tracked in the `USERS_LOGINS` table. Source: `SplendidCRM/_code/Security.cs` (LoginUser method)

**Audit finding:** No external SIEM, no centralized log aggregation, no automated alerting — the `SYSTEM_LOG` is the sole observability mechanism. If SQL Server is unavailable, all error logging fails silently. See the [Structural Integrity Report](../directive-1-structural-integrity/structural-integrity-report.md).

**Audit finding:** No audit trail for configuration changes or admin operations — a COSO Principle 16 gap identified in the [Documentation Coverage Report](../directive-5-documentation-coverage/documentation-coverage-report.md).

### PASS Criteria

Your contribution receives a **PASS** on GATE 3 when ALL of the following are true:

- [ ] All error conditions route through `SplendidError.SystemError()` or `SplendidError.SystemWarning()` — never swallowed silently
- [ ] All new CRM entity tables include corresponding `_AUDIT` table triggers (via the `BuildAllAuditTables` pattern)
- [ ] Login/logout events for new authentication mechanisms are logged to `USERS_LOGINS`
- [ ] Admin-level operations (configuration changes, ACL modifications, user management) are logged
- [ ] Log entries include: timestamp, user ID, action, affected entity, and result
- [ ] Sensitive data (passwords, tokens, PII) is NOT logged in cleartext

### FAIL Criteria

Your contribution receives a **FAIL** on GATE 3 if ANY of the following are true:

- Any error handling path that silently swallows exceptions without logging
- Any new entity table without corresponding audit triggers
- Any admin operation without an audit trail
- Any logging of sensitive credentials or PII in cleartext

---

## GATE 4: Configuration Management

**Governing Controls:** NIST CM (Configuration Management), CIS Control 4 (Secure Configuration of Enterprise Assets and Software)
**COSO Principle:** Principle 10 (Selects and Develops Control Activities)
**NIST CSF Function:** Identify, Protect

### Current Codebase Context

`Web.config` contains several security-weakening settings with no documented rationale:

- `requestValidationMode="2.0"` — reduces XSS protection. Source: `SplendidCRM/Web.config:L111`
- `customErrors="Off"` — exposes stack traces to end users. Source: `SplendidCRM/Web.config:L51`
- `enableEventValidation="false"` — disables ViewState validation. Source: `SplendidCRM/Web.config:L115`
- `validateRequest="false"` — disables request validation on pages. Source: `SplendidCRM/Web.config:L115`

InProc session state precludes horizontal scaling. Source: `SplendidCRM/Web.config:L100`

38 DLLs in `BackupBin2012/` are managed without NuGet — manual dependency management with no version tracking. Source: `BackupBin2012/`

The `.csproj` has phantom HintPath references to non-existent `BackupBin2022/` and `BackupBin2025/` directories. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj`

**Audit finding:** Zero configuration change tracking, no PR templates, no change management documentation — a NIST CM-3 FAIL identified in the [Cross-Cutting Dependency Report](../directive-4-dependency-audit/cross-cutting-dependency-report.md).

### PASS Criteria

Your contribution receives a **PASS** on GATE 4 when ALL of the following are true:

- [ ] No new security-weakening configuration settings without documented justification
- [ ] No hardcoded credentials, connection strings, or API keys in source code
- [ ] Configuration changes are documented with rationale (WHY this setting is necessary)
- [ ] New configuration parameters have secure defaults
- [ ] Environment-specific values use environment variables or configuration transforms — not hardcoded values
- [ ] New DLL dependencies are managed via NuGet — not manual file drops into BackupBin directories
- [ ] Assembly binding redirects in `Web.config` are tested and version-specific

### FAIL Criteria

Your contribution receives a **FAIL** on GATE 4 if ANY of the following are true:

- Any hardcoded credentials or secrets in source code
- Any new security-weakening configuration without documented rationale
- Any manually managed DLL added to BackupBin directories instead of NuGet
- Any configuration that disables a security feature without explicit justification

---

## GATE 5: Input Validation and Encoding

**Governing Controls:** NIST SI (System and Information Integrity), CIS Control 16 (Application Software Security)
**COSO Principle:** Principle 11 (Selects and Develops General Controls over Technology)
**NIST CSF Function:** Protect

### Current Codebase Context

ASP.NET request validation is reduced to mode 2.0: `requestValidationMode="2.0"`. Source: `SplendidCRM/Web.config:L111`

Several pages explicitly disable validation: `validateRequest="false"`. Source: `SplendidCRM/Web.config:L115`

The REST API (`Rest.svc.cs`, 8,369 LOC) processes JSON input with limited server-side validation — it relies primarily on stored procedure parameter types for input constraint. Source: `SplendidCRM/Rest.svc.cs`

SQL injection is mitigated by parameterized queries via `Sql.cs` and stored procedure calls. Source: `SplendidCRM/_code/Sql.cs`

**Audit finding:** The reliance on disabled ASP.NET request validation creates an XSS-permissive posture. Input validation at the application layer is minimal — a Critical finding under COSO Principle 11 identified in the [API Surface Quality Report](../directive-3-code-quality/api-surface-quality.md).

### PASS Criteria

Your contribution receives a **PASS** on GATE 5 when ALL of the following are true:

- [ ] All user-supplied input is validated on the server side before processing
- [ ] SQL queries use parameterized queries or stored procedures — never string concatenation for user input
- [ ] HTML output encoding is applied to prevent XSS (use `HttpUtility.HtmlEncode()` or equivalent)
- [ ] URL parameters and form fields have whitelist validation (expected types, lengths, patterns)
- [ ] File uploads validate file type, size, and content — not just extension
- [ ] JSON input is deserialized using strongly-typed models with validation attributes
- [ ] All user input destined for SQL, HTML, URL, or OS command contexts is properly escaped for the target context

### FAIL Criteria

Your contribution receives a **FAIL** on GATE 5 if ANY of the following are true:

- Any SQL query built through string concatenation with user input
- Any HTML output that renders user input without encoding
- Any file upload without content validation
- Any API endpoint accepting unbounded input without size/type constraints

---

## GATE 6: Error Handling Standards

**Governing Controls:** NIST SI (System and Information Integrity), CIS Control 16 (Application Software Security)
**COSO Principle:** Principle 10 (Selects and Develops Control Activities)
**NIST CSF Function:** Respond

### Current Codebase Context

`SplendidError.cs` provides centralized error handling with 3 methods: `SystemWarning()` (L36), `SystemError()` (L41), `SystemMessage()` (L69). Source: `SplendidCRM/_code/SplendidError.cs`

All errors converge at `spSYSTEM_LOG_InsertOnly` for database persistence. Source: `SplendidCRM/_code/SplendidError.cs:L69`

Six error categories are documented: API Surface errors, background processing errors, SignalR errors, initialization errors, module page errors, and WebForms errors. All converge at `SplendidError.cs` — see the [Structural Integrity Report](../directive-1-structural-integrity/structural-integrity-report.md).

**Audit finding:** `customErrors="Off"` in `Web.config` means unhandled exceptions expose internal stack traces to end users. Source: `SplendidCRM/Web.config:L51`

**Audit finding:** `Application_OnError` in `Global.asax.cs` is **EMPTY** — no global exception handler. Source: `SplendidCRM/Global.asax.cs:L87-90`

**Audit finding:** `ThreadAbortException` is filtered at `SplendidError.cs:L48-52` but other exception types have no categorized handling.

### PASS Criteria

Your contribution receives a **PASS** on GATE 6 when ALL of the following are true:

- [ ] All exception handling uses `SplendidError.SystemError()` for logging — never `catch (Exception) { /* empty */ }`
- [ ] Error messages returned to users do NOT expose internal state (stack traces, file paths, SQL errors, connection strings)
- [ ] All try/catch blocks either handle the exception meaningfully OR rethrow after logging
- [ ] Background processing errors do not crash the timer — use the existing reentrancy guard pattern from `SchedulerUtils.cs` (`bInsideTimer` at L34)
- [ ] API endpoints return appropriate HTTP status codes (400 for validation, 401 for auth, 403 for authz, 500 for server errors) — not generic 200 with error payload
- [ ] Resource cleanup (database connections, file handles, streams) uses `finally` blocks or `using` statements

### FAIL Criteria

Your contribution receives a **FAIL** on GATE 6 if ANY of the following are true:

- Any empty catch block (`catch (Exception) { }`)
- Any error response that exposes internal state (stack traces, SQL errors, file system paths)
- Any exception handling that does not route through `SplendidError` for logging
- Any resource leak (missing `using`/`finally` for `IDisposable` resources)

---

## GATE 7: Dependency Management

**Governing Controls:** NIST CM-3 (Configuration Change Control), CIS Control 2 (Inventory and Control of Software Assets)
**COSO Principle:** Principle 9 (Identifies and Analyzes Significant Change)
**NIST CSF Function:** Identify

### Current Codebase Context

38 .NET DLLs are manually managed in `BackupBin2012/` without NuGet. Source: `BackupBin2012/`

The `.csproj` has phantom HintPath references to `BackupBin2022/` and `BackupBin2025/` directories that do not exist in the repository. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj`

10+ DLLs are security-critical: `BouncyCastle.Crypto`, `MailKit`, `Microsoft.AspNet.SignalR.Core`, `Microsoft.Owin.Security`, `Microsoft.IdentityModel.*`, `Twilio`. No version tracking for the majority of these DLLs.

SignalR version asymmetry: server v1.2.2 (`BackupBin2012`) vs client v8.0.0 (`React/package.json`) vs legacy v2.4.3 (`React/package.json`).

React npm dependencies are managed via `package.json`/yarn but no `npm audit` or automated vulnerability scanning is configured.

**Audit finding:** Zero SBOM (Software Bill of Materials), zero automated dependency scanning, zero CI/CD pipeline — CRITICAL findings under NIST CM-3 and CIS Control 2 identified in the [Cross-Cutting Dependency Report](../directive-4-dependency-audit/cross-cutting-dependency-report.md).

### PASS Criteria

Your contribution receives a **PASS** on GATE 7 when ALL of the following are true:

- [ ] New .NET dependencies are added via NuGet with pinned versions — not manually dropped DLLs
- [ ] New npm dependencies specify exact versions or caret ranges in `package.json`
- [ ] All new dependencies have been checked for known CVEs (Common Vulnerabilities and Exposures)
- [ ] New dependencies are documented with: package name, version, purpose, license
- [ ] Transitive dependencies are reviewed for security implications
- [ ] No introduction of additional version asymmetry (e.g., different versions of the same library in different clients)
- [ ] Dependencies are the minimum necessary — no unnecessary libraries added

### FAIL Criteria

Your contribution receives a **FAIL** on GATE 7 if ANY of the following are true:

- Any manually managed DLL added to BackupBin directories
- Any dependency with known Critical CVEs at time of introduction
- Any dependency added without version pinning
- Any dependency added without documented purpose and license

---

## GATE 8: Documentation Requirements

**Governing Controls:** COSO Principle 14 (Communicates Internally)
**COSO Principle:** Principle 14 (Communicates Internally)
**NIST CSF Function:** Identify

### Current Codebase Context

The entire repository has **ONE** documentation file: `README.md` (78 lines, build prerequisites only). Source: `README.md`

Zero XML doc comments across 60+ `_code/` utility classes (beyond license headers). Source: [Documentation Coverage Report](../directive-5-documentation-coverage/documentation-coverage-report.md)

Zero API documentation — no Swagger/OpenAPI, no WSDL documentation. Source: [Documentation Coverage Report](../directive-5-documentation-coverage/documentation-coverage-report.md)

Zero architecture decision records, zero design rationale, zero security documentation.

WHY documentation coverage across ~140 Material components: approximately **0%**.

**Audit finding:** COSO Principle 14 assessment is **NOT COMPLIANT** — systemic failure to communicate information necessary for internal control. This is a CRITICAL finding identified in the [Documentation Coverage Report](../directive-5-documentation-coverage/documentation-coverage-report.md).

### PASS Criteria

Your contribution receives a **PASS** on GATE 8 when ALL of the following are true:

- [ ] All new public methods have XML doc comments (`/// <summary>`, `/// <param>`, `/// <returns>`) explaining the method's PURPOSE (WHY it exists) — not just WHAT it does
- [ ] All new classes have a file-level summary comment explaining the class's role in the architecture
- [ ] All new API endpoints are documented with: endpoint URL, HTTP method, request/response format, authentication requirements, error codes
- [ ] All new configuration settings are documented with: setting name, valid values, default value, impact of change, and WHY this setting exists
- [ ] Significant design decisions are recorded as inline comments or commit messages explaining WHY (not just WHAT)
- [ ] Cross-cutting utilities consumed by 3+ systems include: ownership declaration, blast radius warning, modification guidelines
- [ ] `README.md` is updated when new build steps, prerequisites, or setup procedures are introduced

### FAIL Criteria

Your contribution receives a **FAIL** on GATE 8 if ANY of the following are true:

- Any new public class without a summary doc comment explaining its purpose
- Any new public method without XML doc comments
- Any new API endpoint without external documentation
- Any new configuration setting without documented rationale

---

## GATE 9: Testing Requirements

**Governing Controls:** NIST CA (Security Assessment and Authorization), CIS Control 16 (Application Software Security)
**COSO Principle:** Principle 16 (Conducts Ongoing and/or Separate Evaluations)
**NIST CSF Function:** Detect

### Current Codebase Context

**ZERO automated testing infrastructure exists across ALL tiers.** No unit tests, no integration tests, no E2E tests, no static analysis, no CI/CD pipeline. Source: Repository-wide search — zero test files found (only Angular's experimental `test.ts` and `tsconfig.spec.json`).

No testing frameworks are referenced in any project file or `package.json` (no xUnit, no NUnit, no MSTest, no Jest, no Mocha, no Cypress).

No CI/CD configuration files exist (no `.yml`, `.yaml`, `Jenkinsfile`, `.travis`, `.github/workflows`, `azure-pipelines`). Source: [Structural Integrity Report](../directive-1-structural-integrity/structural-integrity-report.md)

**Audit finding:** The complete absence of testing is a **CRITICAL** finding under COSO Principle 16 (Monitoring Activities) and NIST SI (System and Information Integrity). The codebase has NO mechanism to verify that internal controls function correctly. COSO Principles 10–12 all FAIL on verification controls — see the [Code Quality Summary](../directive-3-code-quality/code-quality-summary.md).

### PASS Criteria

Your contribution receives a **PASS** on GATE 9 when ALL of the following are true:

- [ ] All new C# code includes unit tests with minimum 80% line coverage for the new code
- [ ] All new API endpoints include integration tests verifying: authentication enforcement, authorization checks, input validation, error handling, happy-path responses
- [ ] All new database operations include tests verifying: CRUD correctness, transaction safety, audit trigger activation
- [ ] Tests are automated and can be run without manual intervention
- [ ] Tests include both positive (happy path) and negative (error path, invalid input, unauthorized access) scenarios
- [ ] Security-sensitive code includes explicit test cases for: authentication bypass attempts, authorization escalation attempts, injection attempts, invalid session handling
- [ ] Tests do not depend on external services (use mocks/stubs for database, email, SSO)

### FAIL Criteria

Your contribution receives a **FAIL** on GATE 9 if ANY of the following are true:

- Any code contribution without accompanying automated tests
- Any security-sensitive code without negative test cases
- Any API endpoint without integration tests covering auth/authz
- Any database operation without transaction safety tests

---

## Quick Reference: All 9 GATEs

| GATE | Key Question | PASS If... | FAIL If... | Framework |
|---|---|---|---|---|
| 1 | Does the code enforce access control? | All queries use `Security.Filter()`; all endpoints check `IsAuthenticated` | Any unfiltered query or unauthenticated endpoint | NIST AC, CIS Control 6 |
| 2 | Does auth use secure standards? | No MD5; secure session handling; TLS enforced | MD5 usage; plaintext credentials; auth bypass | NIST IA, CIS Control 5 |
| 3 | Are operations audit-logged? | Errors via `SplendidError`; entities have `_AUDIT` tables; admin ops logged | Silent error swallowing; missing audit trails | NIST AU, CIS Control 8 |
| 4 | Is configuration managed securely? | No hardcoded secrets; documented settings; NuGet for deps | Hardcoded credentials; undocumented security config | NIST CM, CIS Control 4 |
| 5 | Is all input validated? | Parameterized SQL; HTML encoding; whitelist validation | String-concat SQL; unencoded output; missing validation | NIST SI, CIS Control 16 |
| 6 | Are errors handled properly? | `SplendidError` routing; no internal state exposure; `using`/`finally` | Empty catch blocks; stack trace exposure; resource leaks | NIST SI, CIS Control 16 |
| 7 | Are dependencies managed? | NuGet/npm; CVE-checked; pinned versions; documented | Manual DLLs; known CVEs; undocumented deps | NIST CM-3, CIS Control 2 |
| 8 | Is the code documented? | XML doc comments; API docs; WHY rationale; ownership | No doc comments; no API docs; no rationale | COSO Principle 14 |
| 9 | Are there automated tests? | Unit tests 80%+; integration tests; negative test cases; automated | No tests; missing auth tests; manual-only | NIST CA, CIS Control 16 |

---

## Contribution Self-Assessment Checklist

Copy and complete the following checklist before submitting any code contribution. All 9 GATEs must receive a **PASS** determination. Per COSO Principle 10 (Selects and Develops Control Activities), self-assessment is the first line of quality assurance.

```markdown
## Contribution Self-Assessment

**Contributor:** _______________
**Date:** _______________
**Component/Module:** _______________
**system_id (from registry):** _______________

| GATE | Status | Notes |
|---|---|---|
| GATE 1: Access Control | PASS / FAIL | |
| GATE 2: Authentication | PASS / FAIL | |
| GATE 3: Audit Logging | PASS / FAIL | |
| GATE 4: Configuration | PASS / FAIL | |
| GATE 5: Input Validation | PASS / FAIL | |
| GATE 6: Error Handling | PASS / FAIL | |
| GATE 7: Dependencies | PASS / FAIL | |
| GATE 8: Documentation | PASS / FAIL | |
| GATE 9: Testing | PASS / FAIL | |

**All 9 GATEs PASS?** YES / NO
**Ready for review?** YES / NO
```

Look up your component's `system_id` in the [System Registry](../directive-0-system-registry/system-registry.md) before completing this form.

---

## Cross-References

This guide synthesizes findings from the complete SplendidCRM v15.2 codebase audit (Directives 0–6). Consult the following reports for detailed evidence supporting each GATE's context section:

| Report | Path | Relevance |
|---|---|---|
| System Registry | [system-registry.md](../directive-0-system-registry/system-registry.md) | System IDs and Static/Dynamic classification |
| COSO Mapping | [coso-mapping.md](../directive-0-system-registry/coso-mapping.md) | COSO Principles 1–17 assessment per system |
| NIST Mapping | [nist-mapping.md](../directive-0-system-registry/nist-mapping.md) | NIST SP 800-53 / NIST CSF control mapping |
| CIS Mapping | [cis-mapping.md](../directive-0-system-registry/cis-mapping.md) | CIS Controls v8 IG2/IG3 safeguard mapping |
| Structural Integrity | [structural-integrity-report.md](../directive-1-structural-integrity/structural-integrity-report.md) | Broken references, orphaned configs, error handling gaps |
| Materiality Classification | [materiality-classification.md](../directive-2-materiality/materiality-classification.md) | Material vs Non-Material component inventory |
| Code Quality Summary | [code-quality-summary.md](../directive-3-code-quality/code-quality-summary.md) | Aggregate code quality findings |
| Security Domain Quality | [security-domain-quality.md](../directive-3-code-quality/security-domain-quality.md) | Security.cs, ActiveDirectory.cs, SignalR auth findings |
| API Surface Quality | [api-surface-quality.md](../directive-3-code-quality/api-surface-quality.md) | REST, SOAP, Admin REST API assessment |
| Infrastructure Quality | [infrastructure-quality.md](../directive-3-code-quality/infrastructure-quality.md) | Cache, Init, Sql, Error core infrastructure |
| Background Processing Quality | [background-processing-quality.md](../directive-3-code-quality/background-processing-quality.md) | Scheduler, Email, timer architecture |
| Database Quality | [database-quality.md](../directive-3-code-quality/database-quality.md) | Stored procedures, views, triggers |
| Cross-Cutting Dependencies | [cross-cutting-dependency-report.md](../directive-4-dependency-audit/cross-cutting-dependency-report.md) | Dependency map, shared utilities, blast radius scores |
| Documentation Coverage | [documentation-coverage-report.md](../directive-5-documentation-coverage/documentation-coverage-report.md) | WHY documentation verification per Material component |
| Accuracy Validation | [accuracy-validation-report.md](../directive-6-accuracy-validation/accuracy-validation-report.md) | Statistical sampling results, PASS/FAIL per system |

---

*This Developer Contribution Guide is Directive 7, Artifact 2 of the SplendidCRM v15.2 Codebase Audit. Per COSO Principle 17 (Evaluates and Communicates Deficiencies), the 9 GATEs documented herein communicate the minimum control requirements necessary to prevent new contributions from perpetuating the systemic deficiencies identified across the audit's 8 preceding directives.*
