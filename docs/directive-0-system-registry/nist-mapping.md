# Directive 0 — NIST SP 800-53 Rev 5 & NIST CSF Control Mapping

## Introduction

This document maps each system in the SplendidCRM v15.2 System Registry to applicable NIST SP 800-53 Rev 5 control families and NIST Cybersecurity Framework (CSF) functions. Together, these two NIST frameworks provide the technical control reference layer that bridges the governance-level COSO Internal Control Framework (particularly COSO Principle 11 — Selects and Develops General Controls over Technology) and the implementation-level CIS Controls v8 benchmarks. NIST SP 800-53 Rev 5 supplies the specific, actionable control requirements (AC, AU, CM, IA, SC, SI) against which each system's security posture is assessed, while the NIST CSF provides the organizational narrative structure — Identify → Protect → Detect → Respond → Recover — that frames the operational risk lifecycle.

Every mapping in this document references a `system_id` defined in the [System Registry](./system-registry.md). The system registry establishes the authoritative decomposition of SplendidCRM into 34 discrete, auditable systems classified as Static or Dynamic. Per COSO Principle 11, the selection and development of technology controls must be commensurate with the risk profile and operational behavior of each system. This document operationalizes that principle by mapping observed control implementations — and their gaps — to the NIST control taxonomy. Where NIST SP 800-53 and other frameworks (COSO, CIS Controls v8) present conflicting requirements, the more restrictive requirement is applied and the conflict is flagged.

### Reading Guide

This document is organized into two main sections:

- **Part A — NIST SP 800-53 Rev 5 Control Family Mapping** enumerates the six primary control families in scope (AC, AU, CM, IA, SC, SI) and maps each `system_id` to specific controls within each family. Each control mapping includes the implementation status observed in the codebase and source evidence.
- **Part B — NIST Cybersecurity Framework (CSF) Function Mapping** maps each `system_id` to the five CSF functions (Identify, Protect, Detect, Respond, Recover), providing an operational risk lifecycle perspective across the entire SplendidCRM platform.

Two summary sections follow: a consolidated NIST SP 800-53 Coverage Matrix and a NIST CSF Posture Summary narrative.

---

## Part A — NIST SP 800-53 Rev 5 Control Family Mapping

### Control Families in Scope

The following six NIST SP 800-53 Rev 5 control families are assessed as directly applicable to the SplendidCRM codebase. These families were selected based on the system's role as a self-hosted, single-server CRM application handling business data, user credentials, and external communications. Per COSO Principle 10 (Selects and Develops Control Activities), these families represent the minimum set of technical controls required for an application of this operational profile.

| Family Code | Family Name | Scope Description |
|---|---|---|
| **AC** | Access Control | Policies and mechanisms controlling who can access what resources, enforced at application, API, and data layers |
| **AU** | Audit and Accountability | Creation, review, and protection of audit records that trace user actions and system events |
| **CM** | Configuration Management | Baseline configurations, change control processes, and component inventories governing system composition |
| **IA** | Identification and Authentication | Mechanisms verifying user identity before granting system access, including credential management |
| **SC** | System and Communications Protection | Boundary protections, cryptographic mechanisms, and communication channel security |
| **SI** | System and Information Integrity | Flaw remediation, input validation, error handling, and system monitoring for integrity assurance |

---

### AC — Access Control

The Access Control family assesses how SplendidCRM regulates user and process access to system resources. SplendidCRM implements a sophisticated 4-tier authorization model (module ACL → team filtering → field-level → record-level) centralized in `Security.cs`, which constitutes one of the strongest observed control implementations. However, coarse-grained IIS-level authorization (`<allow users="*"/>`) and the absence of rate limiting present gaps. Per COSO Principle 5 (Enforces Accountability), access controls must ensure that each user is held accountable for their actions within the system.

| system_id | AC Control | Implementation Status | Evidence / Notes |
|---|---|---|---|
| `SYS-SECURITY` | AC-2 (Account Management) | ⚠️ Partial | User accounts managed via USERS table with session-based context in `HttpSessionState`. Account lifecycle (creation, modification, disabling) is handled through stored procedures. No automated account review or periodic recertification observed. Source: `SplendidCRM/_code/Security.cs` |
| `SYS-SECURITY` | AC-3 (Access Enforcement) | ✅ Implemented | 4-tier authorization: (1) module-level ACL via `GetUserAccess()`, (2) team-based record filtering via `Filter()` methods, (3) field-level access control, (4) record-level ownership checks via `ASSIGNED_USER_ID`. Source: `SplendidCRM/_code/Security.cs:L490` |
| `SYS-SECURITY` | AC-6 (Least Privilege) | ⚠️ Partial | `IS_ADMIN` and `IS_ADMIN_DELEGATE` roles with `AdminUserAccess()` downgrade provide role separation. However, admin delegation operates on a binary admin/non-admin model without granular privilege scoping. Source: `SplendidCRM/_code/Security.cs:L137–167, L688` |
| `SYS-SECURITY` | AC-7 (Unsuccessful Logon Attempts) | ⚠️ Partial | Login attempts logged to `USERS_LOGINS` table. However, no automated account lockout mechanism after repeated failures was observed in the codebase. Source: `SplendidCRM/_code/Security.cs` |
| `SYS-API-REST` | AC-3 (Access Enforcement) | ✅ Implemented | Every REST endpoint enforces `Security.IsAuthenticated()` check. Module-specific access verified via `Security.GetUserAccess(sMODULE_NAME, "list")` before data retrieval. Source: `SplendidCRM/Rest.svc.cs:L78–81, L1353` |
| `SYS-API-REST` | AC-4 (Information Flow Enforcement) | ⚠️ Partial | REST API enforces authentication and ACL per request. However, no request rate limiting, IP-based restrictions, or API key management observed. WCF `maxReceivedMessageSize=2147483647` (2 GB) permits very large payloads. Source: `SplendidCRM/Web.config:L149` |
| `SYS-API-SOAP` | AC-3 (Access Enforcement) | ✅ Implemented | SOAP API enforces session-based authentication via `sugarsoap` namespace. Entity CRUD operations require valid session tokens. Source: `SplendidCRM/soap.asmx.cs` |
| `SYS-API-SOAP` | AC-4 (Information Flow Enforcement) | ⚠️ Partial | SOAP operations require session authentication but lack granular flow control. No WSDL access restriction observed. Source: `SplendidCRM/soap.asmx.cs` |
| `SYS-API-ADMIN` | AC-3 (Access Enforcement) | ✅ Implemented | All admin API endpoints enforce `IS_ADMIN` or delegate-level access checks before permitting configuration changes, ACL modifications, or layout CRUD. Source: `SplendidCRM/Administration/Rest.svc.cs` |
| `SYS-API-ADMIN` | AC-6 (Least Privilege) | ⚠️ Partial | Admin API separates admin from non-admin access. Impersonation endpoint (`Impersonation.svc.cs`) allows admin users to assume another user's session — high-privilege operation with audit logging but no secondary authorization gate. Source: `SplendidCRM/Administration/Impersonation.svc.cs` |
| `SYS-REALTIME` | AC-3 (Access Enforcement) | ✅ Implemented | SignalR hubs use `SplendidHubAuthorize.cs` implementing `IAuthorizeHubConnection` and `IAuthorizeHubMethodInvocation`. Authorization checks validate session cookies against the in-memory `SplendidSession` dictionary. Source: `SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs:L26–27` |
| `SYS-REALTIME` | AC-17 (Remote Access) | ⚠️ Partial | SignalR authorization is coupled to ASP.NET session cookies. Session validation depends on the in-memory `dictSessions` dictionary, which is process-local and susceptible to loss on application pool recycling. Source: `SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs:L34–39` |
| `SYS-IIS-CFG` | AC-17 (Remote Access) | ⚠️ Partial | TLS 1.2 enforced programmatically in `Application_Start`. SameSite cookie hardening implemented in `Session_Start` with user-agent-aware compatibility handling. However, `<allow users="*"/>` in Web.config permits all users at the IIS level. Source: `SplendidCRM/Global.asax.cs:L98–101, L149–189`, `SplendidCRM/Web.config:L74` |
| `SYS-BUSINESS-LOGIC` | AC-3 (Access Enforcement) | ✅ Implemented | Every CRM module enforces `Security.Filter()` for list views and `Security.GetUserAccess()` for CRUD operations. Team-based filtering and record ownership checks are applied at the data layer. Source: `SplendidCRM/_code/Security.cs:L490` |
| `SYS-ADMIN` | AC-2 (Account Management) | ⚠️ Partial | Admin modules provide user management UI, ACL role assignment, and login tracking. No automated provisioning/deprovisioning workflows or periodic access reviews observed. Source: `SplendidCRM/Administration/` |
| `SYS-ADMIN` | AC-6 (Least Privilege) | ⚠️ Partial | 45 admin sub-modules are gated behind `IS_ADMIN` checks. Admin delegation exists but provides blanket admin access rather than per-module admin privileges. Source: `SplendidCRM/Administration/Rest.svc.cs` |
| `SYS-WEBFORMS` | AC-3 (Access Enforcement) | ✅ Implemented | WebForms pages inherit from `SplendidPage.cs` which enforces session-based authentication and module ACL checks. Source: `SplendidCRM/_code/SplendidPage.cs` |

**AC Family Gap Summary:**
- No automated account lockout after failed login attempts (AC-7 gap)
- No request rate limiting on any API surface (AC-4 gap)
- IIS-level authorization allows all users; application-level authorization compensates but represents defense-in-depth weakness (AC-17 gap)
- No periodic access recertification mechanism (AC-2 gap)
- Admin delegation is binary rather than granular (AC-6 gap)

---

### AU — Audit and Accountability

The Audit and Accountability family assesses the creation, protection, and review of audit records. SplendidCRM implements a dual audit mechanism: application-level error logging via `SplendidError.cs` → `SYSTEM_LOG` table, and database-level entity audit via SQL triggers → `_AUDIT` companion tables. Per COSO Principle 16 (Conducts Ongoing and/or Separate Evaluations), audit mechanisms must enable both real-time monitoring and periodic retrospective assessment. The observed implementation provides entity-level change tracking but lacks external log aggregation, automated alerting, and log integrity protections.

| system_id | AU Control | Implementation Status | Evidence / Notes |
|---|---|---|---|
| `SYS-ERROR-OBSERVABILITY` | AU-2 (Event Logging) | ⚠️ Partial | Application errors logged to `SYSTEM_LOG` table via `SplendidError.SystemError()` and `SplendidError.SystemWarning()`. Error type, stack frame, and message captured. However, successful operations are not logged — only errors and warnings. Source: `SplendidCRM/_code/SplendidError.cs:L36–66` |
| `SYS-ERROR-OBSERVABILITY` | AU-3 (Content of Audit Records) | ⚠️ Partial | Error records include error type ("Error"/"Warning"), stack frame source, and message text. Timestamp is implicit via SQL `getdate()`. User attribution is available via `HttpContext` when present but null-checked — background timer operations may log without user context. Source: `SplendidCRM/_code/SplendidError.cs:L69–74` |
| `SYS-ERROR-OBSERVABILITY` | AU-12 (Audit Record Generation) | ⚠️ Partial | Centralized through `SplendidError.cs` `SystemMessage()` method which writes to both in-memory `DataTable` and the `SYSTEM_LOG` SQL table via `spSYSTEM_LOG_InsertOnly`. Source: `SplendidCRM/_code/SplendidError.cs:L69` |
| `SYS-AUDIT` | AU-2 (Event Logging) | ✅ Implemented | SQL audit triggers automatically capture INSERT, UPDATE, and DELETE operations on all CRM entity tables. `BuildAllAuditTables.1.sql` generates `_AUDIT` companion tables and triggers for every base table. Source: `SQL Scripts Community/Triggers/BuildAllAuditTables.1.sql` |
| `SYS-AUDIT` | AU-3 (Content of Audit Records) | ✅ Implemented | Audit triggers capture complete before/after values, `AUDIT_ACTION` (insert/update/delete), `AUDIT_DATE`, and `AUDIT_USER_ID` for entity-level change tracking. Source: `SQL Scripts Community/Triggers/BuildAllAuditTables.1.sql` |
| `SYS-AUDIT` | AU-9 (Protection of Audit Information) | ❌ Not Implemented | Audit tables reside in the same SQL Server database as application data with no separation of privileges. Database administrators with access to the application database can modify audit records. No write-once protection, log signing, or external audit log replication observed. Source: `SQL Scripts Community/Triggers/` |
| `SYS-SECURITY` | AU-2 (Event Logging) | ⚠️ Partial | Login events recorded in `USERS_LOGINS` table with session ID, IP address, and timestamp. Successful and failed login attempts both tracked. However, authorization decisions (ACL grants/denials) are not individually logged. Source: `SplendidCRM/_code/Security.cs` |
| `SYS-SQL-DB` | AU-6 (Audit Record Review/Analysis) | ⚠️ Partial | Admin UI provides `SystemLog` and `AuditEvents` viewing modules for manual review. No automated analysis, threshold alerting, or anomaly detection. Source: `SplendidCRM/Administration/SystemLog/`, `SplendidCRM/Administration/AuditEvents/` |
| `SYS-SQL-DB` | AU-9 (Protection of Audit Information) | ❌ Not Implemented | All audit data stored in the same database instance as application data. No read-only audit archive, no external syslog forwarding, no log integrity validation. Source: `SQL Scripts Community/` |
| `SYS-ADMIN` | AU-6 (Audit Record Review/Analysis) | ⚠️ Partial | `SystemLog` admin module enables manual browsing and search of error logs. `UserLogins` admin module tracks login history. `AuditEvents` module provides entity change history viewing. All review is manual — no dashboards, no automated reports. Source: `SplendidCRM/Administration/` |
| `SYS-SCHEDULER` | AU-2 (Event Logging) | ⚠️ Partial | Scheduler job execution logged to `SYSTEM_LOG` via `SplendidError.SystemWarning()`. Timer activation, job start/completion, and errors are recorded. Source: `SplendidCRM/_code/SchedulerUtils.cs:L653` |

**AU Family Gap Summary:**
- No external log aggregation or SIEM integration (AU-6 gap — Critical)
- No automated alerting on security-relevant events (AU-5 gap — Critical)
- No audit log integrity protection — logs are mutable by database administrators (AU-9 gap — Critical)
- No log rotation policy or retention management (AU-11 gap — Moderate)
- Successful operations not logged, only errors/warnings (AU-2 gap — Moderate)
- No automated audit record review or analysis (AU-6 gap — Moderate)

---

### CM — Configuration Management

The Configuration Management family assesses how SplendidCRM maintains baseline configurations, controls changes, and inventories system components. This family reveals some of the most significant gaps in the codebase. Per COSO Principle 12 (Deploys Through Policies and Procedures), configuration management must enforce policies that govern how technology infrastructure is deployed and maintained. The observed state — 24 manually managed DLLs, security-weakening Web.config settings, 16 non-functional integration stubs, and no CI/CD pipeline — represents systemic failure in configuration change control.

| system_id | CM Control | Implementation Status | Evidence / Notes |
|---|---|---|---|
| `SYS-IIS-CFG` | CM-2 (Baseline Configuration) | ⚠️ Partial | `Web.config` defines the application baseline configuration including session state, authentication mode, request validation, and WCF bindings. However, several settings explicitly relax security defaults: `requestValidationMode="2.0"`, `customErrors mode="Off"`, `enableEventValidation="false"`, `validateRequest="false"`. Source: `SplendidCRM/Web.config:L111, L51, L115` |
| `SYS-IIS-CFG` | CM-6 (Configuration Settings) | ❌ Not Implemented | Multiple security-weakening configuration settings observed with no documented risk acceptance: `requestValidationMode="2.0"` (legacy mode bypassing ASP.NET 4.5+ input validation), `customErrors mode="Off"` (detailed error pages exposed to all clients), `enableEventValidation="false"` and `validateRequest="false"` (ASP.NET built-in protections disabled). Source: `SplendidCRM/Web.config:L51, L111, L115` |
| `SYS-CONFIG` | CM-2 (Baseline Configuration) | ⚠️ Partial | Database-level configuration stored in SQL `CONFIG` table, seeded by `SQL Scripts Community/Data/` scripts. Configuration values are mutable at runtime through the Admin `Configurator` module. No configuration versioning or change audit trail for config table modifications beyond entity-level audit triggers. Source: `SQL Scripts Community/Data/` |
| `SYS-DEPENDENCY-MGMT` | CM-3 (Configuration Change Control) | ❌ Not Implemented | 38 .NET DLLs in `BackupBin2012/` are manually managed with no package manager (NuGet), no lock file, no version pinning, no automated vulnerability scanning, and no SBOM. Version updates require manual DLL replacement. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj`, `BackupBin2012/` |
| `SYS-DEPENDENCY-MGMT` | CM-8 (System Component Inventory) | ❌ Not Implemented | No formal Software Bill of Materials (SBOM) exists. The .NET dependency inventory must be manually reconstructed from `SplendidCRM7_VS2017.csproj` HintPath references. React dependencies are inventoried in `package.json` but no centralized inventory spans all dependency ecosystems. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj`, `SplendidCRM/React/package.json` |
| `SYS-BUILD-PIPELINE` | CM-3 (Configuration Change Control) | ❌ Not Implemented | No CI/CD pipeline exists. Build processes are manual: `Build.bat` for SQL, `yarn build` for React, MSBuild for .NET. No automated testing, no static analysis, no build validation gates. No configuration drift detection. Source: `SQL Scripts Community/Build.bat`, `SplendidCRM/React/package.json` |
| `SYS-BUILD-PIPELINE` | CM-5 (Access Restrictions for Change) | ❌ Not Implemented | No source code access controls documented within the repository. SQL build orchestrator (`Build.bat`) concatenates scripts without validation. No code review requirements, no branch protection, no deployment approval gates. Source: `SQL Scripts Community/Build.bat` |
| `SYS-INTEGRATION-STUBS` | CM-7 (Least Functionality) | ❌ Not Implemented | 16+ Spring.Social.* enterprise integration stubs are compiled into the application but non-functional. These stubs preserve API signatures, DTOs, and exception contracts that consume compile time and increase attack surface without providing functionality. Source: `SplendidCRM/_code/Spring.Social.*/` |
| `SYS-SQL-DB` | CM-2 (Baseline Configuration) | ⚠️ Partial | Schema definitions use INFORMATION_SCHEMA guards for idempotent deployment. `Build.bat` orchestrates sequential script execution across 11 subdirectories. However, no schema versioning mechanism, no migration rollback capability, and no schema diff tooling observed. Source: `SQL Scripts Community/Build.bat` |
| `SYS-SQL-DB` | CM-3 (Configuration Change Control) | ⚠️ Partial | SQL schema changes are managed through additive scripts in `SQL Scripts Community/Tables/` (upgrade scripts) but no formal change control process, no migration versioning, and no rollback scripts. Source: `SQL Scripts Community/Tables/` |
| `SYS-INIT` | CM-2 (Baseline Configuration) | ✅ Implemented | Application initialization follows a deterministic sequence: TLS 1.2 enforcement, unobserved task exception handling, `SplendidInit.InitApp()`, workflow initialization, timer activation, SignalR initialization. Source: `SplendidCRM/Global.asax.cs:L92–220` |
| `SYS-CACHE` | CM-6 (Configuration Settings) | ⚠️ Partial | Cache uses `HttpApplicationState` and `HttpRuntime.Cache` with process-local scope. No cache configuration for TTL, size limits, or eviction policies is explicitly defined — defaults are inherited from ASP.NET runtime. Source: `SplendidCRM/_code/SplendidCache.cs` |
| `SYS-REACT-SPA` | CM-8 (System Component Inventory) | ⚠️ Partial | React dependencies tracked via `package.json` with `yarn.lock` for version pinning. 80+ direct dependencies enumerated. However, no automated vulnerability scanning (e.g., `npm audit`, Snyk) integrated into the build. Source: `SplendidCRM/React/package.json` |
| `SYS-ANGULAR-CLIENT` | CM-8 (System Component Inventory) | ⚠️ Partial | Angular dependencies tracked via `package.json`. Experimental status — no production use. Source: `SplendidCRM/Angular/package.json` |
| `SYS-ASPNET-APP` | CM-2 (Baseline Configuration) | ⚠️ Partial | MSBuild project file (`SplendidCRM7_VS2017.csproj`) defines compilation settings, assembly references, and build targets. No build reproducibility guarantees — depends on local environment state. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj` |

**CM Family Gap Summary:**
- No package manager for .NET dependencies — 38 manually managed DLLs (CM-3 gap — Critical)
- No CI/CD pipeline or automated build validation (CM-3 gap — Critical)
- No SBOM spanning all dependency ecosystems (CM-8 gap — Critical)
- Security-weakening Web.config settings without documented risk acceptance (CM-6 gap — Critical)
- 16+ non-functional integration stubs increasing attack surface (CM-7 gap — Moderate)
- No schema versioning or migration rollback for SQL database (CM-3 gap — Moderate)

---

### IA — Identification and Authentication

The Identification and Authentication family assesses how SplendidCRM verifies user identities. The platform supports multiple authentication mechanisms — a strength for operational flexibility. However, the use of MD5 for password hashing represents a Critical cryptographic weakness that undermines the entire IA family's effectiveness. Per COSO Principle 5 (Enforces Accountability), reliable identification is the prerequisite for all accountability mechanisms.

| system_id | IA Control | Implementation Status | Evidence / Notes |
|---|---|---|---|
| `SYS-SECURITY` | IA-2 (Identification and Authentication) | ✅ Implemented | Multi-mechanism authentication: (1) Forms-based login via `Security.LoginUser()`, (2) Windows/NTLM integrated authentication via `ActiveDirectory.cs`, (3) DuoUniversal 2FA challenge/callback. All mechanisms converge to session-based identity binding via `HttpSessionState`. Source: `SplendidCRM/_code/Security.cs`, `SplendidCRM/_code/ActiveDirectory.cs` |
| `SYS-SECURITY` | IA-5 (Authenticator Management) | ❌ Not Implemented | Password hashing uses MD5 (`MD5CryptoServiceProvider`) for SugarCRM backward compatibility. MD5 is cryptographically broken — vulnerable to collision attacks and rainbow table lookups. No salting observed. This is a Critical weakness. Source: `SplendidCRM/_code/Security.cs:L393–406` |
| `SYS-SECURITY` | IA-5 (Authenticator Management) — Credential Storage | ⚠️ Partial | Stored credentials (e.g., inbound email passwords) encrypted using Rijndael symmetric encryption with keys derived from GUIDs stored in `HttpApplicationState`. Key material is process-local and not rotatable without application restart. Source: `SplendidCRM/_code/Security.cs:L412–454` |
| `SYS-AUTH-AD` | IA-2 (Identification and Authentication) | ⚠️ Partial | Windows/NTLM authentication integration implemented. ADFS/Azure AD JWT validation methods exist but are partially stubbed in Community Edition — some code paths throw descriptive exceptions for unsupported enterprise features. Source: `SplendidCRM/_code/ActiveDirectory.cs` |
| `SYS-AUTH-DUO` | IA-2 (Identification and Authentication) | ✅ Implemented | DuoUniversal two-factor authentication challenge/callback flow implemented. When enabled, users are redirected to the Duo service for secondary verification after primary credential validation. Source: `SplendidCRM/Administration/DuoUniversal/` |
| `SYS-AUTH-DUO` | IA-2(1) (Multi-Factor Authentication) | ✅ Implemented | Duo Universal Prompt provides a standards-compliant second authentication factor. Enrollment, challenge, and callback endpoints are present. Source: `SplendidCRM/Administration/DuoUniversal/` |
| `SYS-API-SOAP` | IA-8 (Identification and Authentication — Non-Org Users) | ⚠️ Partial | SOAP API accepts external session-based authentication via `sugarsoap` namespace. Session tokens are issued upon successful credential validation. No OAuth, API key, or certificate-based authentication for external consumers. Source: `SplendidCRM/soap.asmx.cs` |
| `SYS-IIS-CFG` | IA-11 (Re-authentication) | ⚠️ Partial | Session timeout configured at 20 minutes (`sessionState mode="InProc" timeout="20"`). InProc mode means sessions are lost on application pool recycling. No sliding expiration or re-authentication prompt before session expiry observed. Source: `SplendidCRM/Web.config:L100` |
| `SYS-IIS-CFG` | IA-2 (Identification and Authentication) | ⚠️ Partial | `authentication mode="Windows"` configured in Web.config. IIS-level authentication delegates to Windows for NTLM/Kerberos. Application-level forms authentication handled separately by `Security.cs`. Source: `SplendidCRM/Web.config:L65` |

**IA Family Gap Summary:**
- MD5 password hashing without salting — cryptographically broken (IA-5 gap — Critical)
- ADFS/Azure AD JWT validation partially stubbed in Community Edition (IA-2 gap — Moderate)
- No automated account lockout after failed authentication attempts (IA-5 gap — Moderate)
- Rijndael key material is process-local and non-rotatable (IA-5 gap — Moderate)
- No OAuth, API key, or certificate-based authentication for API consumers (IA-8 gap — Moderate)
- Session-based identity with InProc storage — lost on app pool recycle (IA-11 gap — Moderate)

---

### SC — System and Communications Protection

The System and Communications Protection family assesses boundary protections, cryptographic mechanisms, and communication security. SplendidCRM enforces TLS 1.2 for transport security and implements session-cookie hardening, which are positive observations. However, the single-server architecture with no network segmentation, oversized request limits creating potential denial-of-service vectors, and weak cryptographic primitives (MD5) present significant gaps. Per COSO Principle 11 (Selects and Develops General Controls over Technology), cryptographic controls must be commensurate with the sensitivity of the data being protected.

| system_id | SC Control | Implementation Status | Evidence / Notes |
|---|---|---|---|
| `SYS-IIS-CFG` | SC-8 (Transmission Confidentiality) | ✅ Implemented | TLS 1.2 enforced programmatically at `Application_Start` via `ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12`. SameSite cookie attribute set with user-agent-aware compatibility for older browsers. Secure cookie flag set when `Request.IsSecureConnection` is true. Source: `SplendidCRM/Global.asax.cs:L98–101, L174–186` |
| `SYS-IIS-CFG` | SC-5 (Denial of Service Protection) | ❌ Not Implemented | `maxRequestLength=104857600` (100 MB) and `executionTimeout=600` (10 minutes) in `httpRuntime`. WCF binding sets `maxReceivedMessageSize=2147483647` (2 GB), `maxBufferPoolSize=2147483647`, `maxBufferSize=2147483647`. These oversized limits create potential denial-of-service vectors by allowing very large payloads and long-running requests to consume server resources. Source: `SplendidCRM/Web.config:L111, L149` |
| `SYS-IIS-CFG` | SC-7 (Boundary Protection) | ❌ Not Implemented | Single-server architecture with InProc session state. No network segmentation between application tier and database tier. No DMZ, no reverse proxy, no WAF. The application, database, and all client interfaces share the same server boundary. Source: `SplendidCRM/Web.config:L100` |
| `SYS-SECURITY` | SC-12 (Cryptographic Key Management) | ⚠️ Partial | Rijndael encryption keys derived from GUIDs stored in `HttpApplicationState` (`INBOUND_EMAIL_KEY`, `INBOUND_EMAIL_IV`). Keys are generated at application startup and persist in process memory. No key rotation mechanism, no HSM integration, no key escrow or recovery procedures. Source: `SplendidCRM/_code/Security.cs:L436–440` |
| `SYS-SECURITY` | SC-13 (Cryptographic Protection) | ⚠️ Partial | MD5 for password hashing (cryptographically broken — Critical weakness). Rijndael for symmetric credential encryption (adequate algorithm but weak key management). `BouncyCastle.Crypto.dll` available in the dependency inventory for additional cryptographic operations. Source: `SplendidCRM/_code/Security.cs:L397–406, L412–454` |
| `SYS-SECURITY` | SC-28 (Protection of Information at Rest) | ⚠️ Partial | Stored credentials (inbound email passwords) encrypted via Rijndael before database persistence. Application data at rest depends on SQL Server edition — Transparent Data Encryption (TDE) is available only in Enterprise edition. No application-level data-at-rest encryption beyond credential storage. Source: `SplendidCRM/_code/Security.cs:L413–454` |
| `SYS-SQL-DB` | SC-28 (Protection of Information at Rest) | ⚠️ Partial | SQL Server data-at-rest encryption depends entirely on the SQL Server edition deployed. Community Edition recommends SQL Server Express, which does not include TDE. No application-level encryption of sensitive CRM data (customer PII, financial records) beyond credential fields. Source: `SQL Scripts Community/` |
| `SYS-API-REST` | SC-5 (Denial of Service Protection) | ❌ Not Implemented | No request rate limiting, no payload size validation beyond IIS defaults, no connection throttling. Large REST payloads accepted per WCF binding configuration (`maxReceivedMessageSize=2GB`). Source: `SplendidCRM/Web.config:L149` |
| `SYS-API-REST` | SC-8 (Transmission Confidentiality) | ⚠️ Partial | REST API inherits TLS 1.2 enforcement from the IIS/application level. No per-endpoint encryption or message-level security beyond transport-level TLS. Source: `SplendidCRM/Rest.svc.cs` |
| `SYS-REALTIME` | SC-8 (Transmission Confidentiality) | ⚠️ Partial | SignalR communication inherits TLS 1.2 from the hosting IIS application. `SplendidHubAuthorize.cs` validates session cookies for WebSocket authorization. Session-cookie coupling means real-time channel security depends on session integrity. Source: `SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs` |
| `SYS-EMAIL` | SC-8 (Transmission Confidentiality) | ⚠️ Partial | `SplendidMailClient.cs` abstracts multi-provider mail delivery (SMTP, Exchange, Gmail, Office365). TLS support depends on individual provider configuration. No forced TLS requirement for SMTP connections observed at the application level. Source: `SplendidCRM/_code/SplendidMailClient.cs` |

**SC Family Gap Summary:**
- No network segmentation — single-server architecture (SC-7 gap — Critical)
- Oversized request limits creating DoS vectors (100 MB HTTP, 2 GB WCF) (SC-5 gap — Critical)
- MD5 for password hashing — cryptographically broken (SC-13 gap — Critical)
- No key rotation, HSM, or formal key management (SC-12 gap — Moderate)
- No application-level data-at-rest encryption beyond credentials (SC-28 gap — Moderate)
- No WAF, reverse proxy, or request rate limiting (SC-5 gap — Moderate)

---

### SI — System and Information Integrity

The System and Information Integrity family assesses flaw remediation, input validation, error handling, and system monitoring capabilities. This family reveals the most pervasive control deficiency in the SplendidCRM codebase: the complete absence of automated testing infrastructure across all tiers. Per COSO Principle 10 (Selects and Develops Control Activities), control activities must include mechanisms for detecting and remediating flaws. With zero unit tests, zero integration tests, zero end-to-end tests, and no static analysis, the flaw remediation posture is critically deficient.

| system_id | SI Control | Implementation Status | Evidence / Notes |
|---|---|---|---|
| `SYS-BUILD-PIPELINE` | SI-2 (Flaw Remediation) | ❌ Not Implemented | Zero automated testing across all tiers — no unit tests, no integration tests, no E2E tests, no static analysis tools, no CI/CD pipeline. Flaw detection relies entirely on manual testing and production error observation. This is a Critical systemic gap. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj` (no test project references), `SplendidCRM/React/package.json` (no test dependencies) |
| `SYS-IIS-CFG` | SI-10 (Information Input Validation) | ❌ Not Implemented | `requestValidationMode="2.0"` reverts ASP.NET input validation to legacy .NET 2.0 behavior, bypassing improvements in .NET 4.0+. `validateRequest="false"` and `enableEventValidation="false"` disable page-level validation entirely. These settings were explicitly configured for backward compatibility with WebForms HTML editing fields. Source: `SplendidCRM/Web.config:L111, L115` |
| `SYS-API-REST` | SI-10 (Information Input Validation) | ⚠️ Partial | REST API performs endpoint-specific input validation via `Sql.To*()` type conversion methods and `Sql.IsEmptyString()`/`Sql.IsEmptyGuid()` null checks. However, no centralized input validation framework, no request schema validation, and no allowlist-based input filtering. Validation is inconsistent across endpoints. Source: `SplendidCRM/Rest.svc.cs` |
| `SYS-ERROR-OBSERVABILITY` | SI-4 (System Monitoring) | ⚠️ Partial | `SplendidError.cs` logs application errors to `SYSTEM_LOG` table and in-memory `DataTable`. `SystemCheck.aspx.cs` provides a diagnostics endpoint for database connectivity and configuration validation. However, no external APM, no distributed tracing, no health check endpoints for load balancers, no automated alerting, and no SLA monitoring. Source: `SplendidCRM/_code/SplendidError.cs`, `SplendidCRM/SystemCheck.aspx.cs` |
| `SYS-ERROR-OBSERVABILITY` | SI-11 (Error Handling) | ⚠️ Partial | `SplendidError.cs` centralizes error logging via `SystemMessage()` method. Stack traces are captured and included in log entries. However, `customErrors mode="Off"` in Web.config may expose detailed error pages (including stack traces) to end users, potentially revealing internal application state. Source: `SplendidCRM/_code/SplendidError.cs:L56–66`, `SplendidCRM/Web.config:L51` |
| `SYS-IIS-CFG` | SI-11 (Error Handling) | ❌ Not Implemented | `customErrors mode="Off"` explicitly configured — detailed ASP.NET error information displayed to all clients regardless of origin. Default redirect to `~/Home/ServerError.aspx` is defined but mode="Off" overrides it. Source: `SplendidCRM/Web.config:L51` |
| `SYS-IIS-CFG` | SI-3 (Malicious Code Protection) | — Not Applicable | Malicious code protection (antivirus/antimalware) is an operating system and host-level concern, outside the application codebase scope. The application relies on the host Windows Server and IIS environment for malware scanning. |
| `SYS-REACT-SPA` | SI-10 (Information Input Validation) | ⚠️ Partial | React SPA provides client-side form validation. However, client-side validation is inherently bypassable — server-side validation is the authoritative control. Source: `SplendidCRM/React/src/` |
| `SYS-HTML5-CLIENT` | SI-10 (Information Input Validation) | ⚠️ Partial | Legacy HTML5 client uses jQuery-based form handling. jQuery version range (1.4.2–2.2.4) includes known XSS vulnerabilities in older versions. Client-side validation only. Source: `SplendidCRM/html5/` |
| `SYS-DB-ACCESS` | SI-10 (Information Input Validation) | ⚠️ Partial | `Sql.cs` provides parameterized query execution and type-safe data conversion methods (`Sql.ToGuid()`, `Sql.ToString()`, `Sql.ToBoolean()`, etc.) which mitigate SQL injection. However, `SearchBuilder.cs` constructs WHERE clauses dynamically, which requires careful review. Source: `SplendidCRM/_code/Sql.cs`, `SplendidCRM/_code/SearchBuilder.cs` |
| `SYS-CACHE` | SI-4 (System Monitoring) | ⚠️ Partial | Cache state is observable through the admin `SystemLog` module when cache-related errors occur. No dedicated cache monitoring, no hit/miss ratio tracking, no cache size alerting. Source: `SplendidCRM/_code/SplendidCache.cs` |
| `SYS-SCHEDULER` | SI-4 (System Monitoring) | ⚠️ Partial | Scheduler job execution status logged to `SYSTEM_LOG`. Timer activation messages logged via `SplendidError.SystemWarning()`. Reentrancy guards (`bInsideTimer` flags) prevent concurrent execution. However, no job health monitoring, no SLA tracking, no dead-letter queue for failed jobs. Source: `SplendidCRM/_code/SchedulerUtils.cs:L34, L653–659` |
| `SYS-ASPNET-APP` | SI-2 (Flaw Remediation) | ❌ Not Implemented | No static code analysis integrated. No security scanning (SAST/DAST). Unobserved task exceptions are caught and suppressed via `TaskScheduler_UnobservedTaskException` to prevent app crashes, but no remediation pipeline exists. Source: `SplendidCRM/Global.asax.cs:L96, L104–107` |

**SI Family Gap Summary:**
- Zero automated testing infrastructure across all tiers (SI-2 gap — Critical)
- ASP.NET input validation disabled via Web.config settings (SI-10 gap — Critical)
- Detailed error pages exposed to all clients via `customErrors mode="Off"` (SI-11 gap — Critical)
- No external monitoring, APM, or automated alerting (SI-4 gap — Critical)
- No static analysis, SAST, or DAST tools (SI-2 gap — Critical)
- Inconsistent per-endpoint input validation in REST API (SI-10 gap — Moderate)
- jQuery 1.4.2 in legacy client has known XSS vulnerabilities (SI-10 gap — Moderate)

---

## Part B — NIST Cybersecurity Framework (CSF) Function Mapping

### Overview

The NIST Cybersecurity Framework organizes cybersecurity activities into five concurrent and continuous functions: **Identify**, **Protect**, **Detect**, **Respond**, and **Recover**. These functions provide a strategic, outcome-oriented view of an organization's cybersecurity risk management posture. Per COSO Principle 11 (Selects and Develops General Controls over Technology), effective technology governance requires capabilities across all five functions — not merely protective controls.

The following assessment maps each `system_id` to the CSF functions where it has observable implementations, providing an operational risk lifecycle view of the SplendidCRM platform. Each function section includes an assessment of the strength of the observed implementation.

### Identify (ID)

**Mapping:** Aligns with Directive 0 (System Registry) and Directive 2 (Materiality Classification).

The Identify function involves understanding the organizational context, assets, and risk landscape. In the SplendidCRM codebase, asset identification is implicit rather than formal — there is no dedicated asset inventory system, no risk register, and no formal risk management framework.

| system_id | Identify Capability | Assessment |
|---|---|---|
| `SYS-CONFIG` | Application configuration identified through `Web.config` and SQL `CONFIG` table | ⚠️ Partial — configuration is discoverable but not inventoried in a risk-aware manner |
| `SYS-SQL-DB` | Database schema identified through `SQL Scripts Community/` (229 tables, 581 views, 833 procedures) | ⚠️ Partial — schema is defined but no data classification or sensitivity labeling exists |
| `SYS-DEPENDENCY-MGMT` | Software inventory identifiable through `.csproj` (38 DLLs) and `package.json` (80+ npm packages) | ⚠️ Partial — dependencies are trackable but no centralized SBOM or vulnerability tracking |
| `SYS-INTEGRATION-STUBS` | 16+ non-functional integration stubs identified as orphaned code | ⚠️ Partial — stubs are identifiable but their presence represents unmanaged change (COSO Principle 9) |
| `SYS-L10N` | Localization assets identified through 112 terminology seed scripts | ✅ Implemented — well-structured, categorized language assets |
| `SYS-WORKFLOW` | Workflow stubs identified as compilation placeholders | ⚠️ Partial — presence is known but functional status is ambiguous |
| `SYS-BUILD-PIPELINE` | Build processes identified through `Build.bat`, `package.json` scripts, `.csproj` | ⚠️ Partial — build steps are defined but no formal build inventory or reproducibility guarantees |
| `SYS-REACT-SPA` | React SPA dependencies inventoried in `package.json` (v15.2.9366) | ⚠️ Partial — version-locked via yarn but no vulnerability scanning |
| `SYS-ANGULAR-CLIENT` | Angular client dependencies inventoried in `package.json` | ⚠️ Partial — experimental client, tracked but not actively maintained |
| `SYS-HTML5-CLIENT` | Legacy client libraries identifiable in `html5/` directory | ⚠️ Partial — no formal inventory; jQuery version range spans multiple major versions |

**Identify Function Assessment:** Partial. Asset identification is possible through code analysis but no formal asset inventory, risk register, or data classification framework exists. Software composition is implicit in build files rather than explicitly managed through a dedicated inventory tool. Per COSO Principle 7 (Identifies and Analyzes Risk), the absence of formal risk identification processes is a governance gap.

---

### Protect (PR)

**Mapping:** Aligns with Directive 1 (Structural Integrity) and Directive 3 (Security Quality).

The Protect function encompasses access control, awareness training, data security, information protection processes, maintenance, and protective technology. SplendidCRM's strongest cybersecurity posture is in access protection through its 4-tier authorization model.

| system_id | Protect Capability | Assessment |
|---|---|---|
| `SYS-SECURITY` | 4-tier authorization model (module ACL, team filtering, field-level, record-level), multi-mechanism authentication | ✅ Implemented — comprehensive access protection; weakened by MD5 hashing |
| `SYS-AUTH-AD` | Windows/NTLM integration for enterprise environments | ⚠️ Partial — NTLM implemented; ADFS/Azure AD partially stubbed |
| `SYS-AUTH-DUO` | DuoUniversal 2FA providing secondary authentication factor | ✅ Implemented — standards-compliant MFA |
| `SYS-IIS-CFG` | TLS 1.2 enforcement, SameSite cookie hardening, session timeout | ⚠️ Partial — transport protection strong; input validation disabled |
| `SYS-API-REST` | Authentication enforcement on all endpoints, ACL-based data filtering | ✅ Implemented — consistent auth checks; lacks rate limiting |
| `SYS-API-SOAP` | Session-based SOAP authentication, entity-level ACL | ✅ Implemented — legacy protocol with session security |
| `SYS-API-ADMIN` | IS_ADMIN enforcement on all admin endpoints | ✅ Implemented — admin privilege boundary enforced |
| `SYS-REALTIME` | SignalR session-cookie authorization via `SplendidHubAuthorize.cs` | ⚠️ Partial — session-coupled; vulnerable to session loss |
| `SYS-DB-ACCESS` | Parameterized queries via `Sql.cs` preventing SQL injection | ✅ Implemented — type-safe data access layer |
| `SYS-BUSINESS-LOGIC` | Per-module ACL enforcement, team-based record filtering | ✅ Implemented — consistent across 40 CRM modules |
| `SYS-WEBFORMS` | Session-based authentication via `SplendidPage.cs` base class | ✅ Implemented — inherited protection pattern |
| `SYS-INIT` | TLS enforcement at startup, exception handler registration | ✅ Implemented — deterministic security initialization |
| `SYS-EMAIL` | Multi-provider mail abstraction with provider-specific TLS | ⚠️ Partial — TLS depends on provider configuration |
| `SYS-IMPORT-EXPORT` | ACL-aware field filtering on export operations | ⚠️ Partial — export filtering implemented; import validation varies |

**Protect Function Assessment:** Partial. Access control is the strongest area — the 4-tier authorization model is consistently enforced across all API surfaces and business logic modules. Transport security (TLS 1.2) is properly enforced. However, critical weaknesses in cryptographic protection (MD5 hashing), disabled input validation (Web.config settings), and absence of rate limiting significantly diminish the overall Protect posture. Per COSO Principle 11, the selection of MD5 as a cryptographic primitive fails to meet the standard of "develops general controls over technology" for password protection.

---

### Detect (DE)

**Mapping:** Aligns with Directive 5 (Documentation Coverage) and Directive 4 (Dependency Monitoring).

The Detect function encompasses security continuous monitoring, detection processes, and anomaly identification. This is one of the weakest CSF functions in the SplendidCRM implementation.

| system_id | Detect Capability | Assessment |
|---|---|---|
| `SYS-AUDIT` | SQL audit triggers detect entity-level changes (INSERT/UPDATE/DELETE) across all CRM tables | ✅ Implemented — comprehensive entity change detection |
| `SYS-ERROR-OBSERVABILITY` | `SplendidError.cs` detects and logs application errors to `SYSTEM_LOG` | ⚠️ Partial — error detection present but no anomaly detection or alerting |
| `SYS-SCHEDULER` | Job execution logging detects failed scheduler runs | ⚠️ Partial — failure detection via error logging only |
| `SYS-ADMIN` | Admin UI modules (`SystemLog`, `AuditEvents`, `UserLogins`) enable manual review | ⚠️ Partial — manual detection only; no automated monitoring |
| `SYS-SQL-DB` | Audit triggers provide change detection at the data layer | ✅ Implemented — trigger-based detection is reliable |
| `SYS-REPORTING` | Report engine can produce ad-hoc queries for security review | ⚠️ Partial — reporting capability exists but no security-specific reports |

**Detect Function Assessment:** Minimal. Entity-level change detection via SQL audit triggers is the sole robust detection mechanism. Application-level error logging exists but captures only errors — not security events, access anomalies, or suspicious patterns. No external monitoring, SIEM integration, intrusion detection, anomaly detection, or automated alerting exists. The Detect function is critically deficient for proactive threat identification. Per COSO Principle 16 (Conducts Ongoing and/or Separate Evaluations), the absence of continuous monitoring mechanisms means that deficiencies may persist undetected.

---

### Respond (RS)

**Mapping:** Aligns with Directive 1 (Error Handling Assessment) and Directive 7 (Developer Guide).

The Respond function encompasses response planning, communications, analysis, mitigation, and improvements after detected cybersecurity events. SplendidCRM has minimal response capabilities.

| system_id | Respond Capability | Assessment |
|---|---|---|
| `SYS-ERROR-OBSERVABILITY` | Centralized error logging provides post-incident forensic data in `SYSTEM_LOG` | ⚠️ Partial — logging for forensics exists but no incident response automation |
| `SYS-SCHEDULER` | Reentrancy guards (`bInsideTimer` flags) prevent cascading failures from scheduler jobs | ⚠️ Partial — self-healing for scheduler only; not a general response mechanism |
| `SYS-INIT` | `TaskScheduler_UnobservedTaskException` handler prevents unhandled exceptions from crashing the application | ⚠️ Partial — defensive coding but not an incident response mechanism |
| `SYS-ADMIN` | Admin UI for SystemLog review enables manual incident investigation | ⚠️ Partial — manual review capability only |

**Respond Function Assessment:** Minimal. Error logging provides forensic data for post-incident analysis, and reentrancy guards prevent certain cascading failures. However, no automated incident response procedures, no runbooks, no communication templates, no escalation paths, and no mitigation playbooks exist. Per COSO Principle 17 (Evaluates and Communicates Deficiencies), the absence of structured response procedures means that identified deficiencies cannot be systematically addressed.

---

### Recover (RC)

**Mapping:** Aligns with Directive 6 (Accuracy Validation) and Directive 7 (Remediation Priorities).

The Recover function encompasses recovery planning, improvements, and communications to restore capabilities after a cybersecurity incident. SplendidCRM has basic backup capabilities but no documented recovery procedures.

| system_id | Recover Capability | Assessment |
|---|---|---|
| `SYS-SQL-DB` | SQL Server native backup capabilities available | ⚠️ Partial — backup technology exists but recovery procedures undocumented |
| `SYS-SCHEDULER` | `BackupDatabase` and `BackupTransactionLog` scheduler jobs configured in `SchedulerUtils.cs` | ⚠️ Partial — backup jobs defined (Source: `SplendidCRM/_code/SchedulerUtils.cs:L45–46`) but restoration testing not observed |
| `SYS-BUILD-PIPELINE` | SQL `Build.bat` and React `yarn build` can reconstruct application artifacts | ⚠️ Partial — build reproducibility possible but not guaranteed |

**Recover Function Assessment:** Minimal. Scheduled database backup jobs (`BackupDatabase`, `BackupTransactionLog`) exist in the scheduler job catalog, and SQL Server native backup capabilities are leveraged. However, no documented RPO/RTO targets, no disaster recovery runbooks, no recovery testing procedures, and no communication plans for recovery scenarios exist. Per COSO Principle 10, recovery control activities must be defined and tested — the current implementation provides backup capability without recovery assurance.

---

## NIST SP 800-53 Control Coverage Matrix

The following matrix summarizes the NIST SP 800-53 Rev 5 control family coverage for each `system_id`. Symbols indicate: ✅ Implemented, ⚠️ Partial, ❌ Not Implemented, — Not Applicable.

| system_id | AC | AU | CM | IA | SC | SI | Overall |
|---|---|---|---|---|---|---|---|
| `SYS-SECURITY` | ✅ | ⚠️ | — | ⚠️ | ⚠️ | — | ⚠️ Partial |
| `SYS-AUTH-AD` | ⚠️ | — | — | ⚠️ | — | — | ⚠️ Partial |
| `SYS-AUTH-DUO` | — | — | — | ✅ | — | — | ✅ Implemented |
| `SYS-API-REST` | ✅ | — | — | — | ⚠️ | ⚠️ | ⚠️ Partial |
| `SYS-API-SOAP` | ✅ | — | — | ⚠️ | — | — | ⚠️ Partial |
| `SYS-API-ADMIN` | ✅ | — | — | — | — | — | ✅ Implemented |
| `SYS-CACHE` | — | — | ⚠️ | — | — | ⚠️ | ⚠️ Partial |
| `SYS-INIT` | — | — | ✅ | — | — | — | ✅ Implemented |
| `SYS-DB-ACCESS` | — | — | — | — | — | ⚠️ | ⚠️ Partial |
| `SYS-ERROR-OBSERVABILITY` | — | ⚠️ | — | — | — | ⚠️ | ⚠️ Partial |
| `SYS-BUSINESS-LOGIC` | ✅ | — | — | — | — | — | ✅ Implemented |
| `SYS-IMPORT-EXPORT` | — | — | — | — | — | ⚠️ | ⚠️ Partial |
| `SYS-CAMPAIGN` | — | — | — | — | — | ⚠️ | ⚠️ Partial |
| `SYS-EMAIL` | — | — | — | — | ⚠️ | — | ⚠️ Partial |
| `SYS-SMS-TELEPHONY` | — | — | — | — | ⚠️ | — | ⚠️ Partial |
| `SYS-REALTIME` | ⚠️ | — | — | — | ⚠️ | — | ⚠️ Partial |
| `SYS-SCHEDULER` | — | ⚠️ | — | — | — | ⚠️ | ⚠️ Partial |
| `SYS-WORKFLOW` | — | — | — | — | — | — | — N/A |
| `SYS-CONFIG` | — | — | ⚠️ | — | — | — | ⚠️ Partial |
| `SYS-ADMIN` | ⚠️ | ⚠️ | — | — | — | — | ⚠️ Partial |
| `SYS-REPORTING` | — | ⚠️ | — | — | — | — | ⚠️ Partial |
| `SYS-CONTENT` | — | — | — | — | — | ⚠️ | ⚠️ Partial |
| `SYS-L10N` | — | — | — | — | — | — | — N/A |
| `SYS-INTEGRATION-STUBS` | — | — | ❌ | — | — | — | ❌ Not Implemented |
| `SYS-SQL-DB` | — | ⚠️ | ⚠️ | — | ⚠️ | — | ⚠️ Partial |
| `SYS-AUDIT` | — | ✅ | — | — | — | — | ✅ Implemented |
| `SYS-ASPNET-APP` | — | — | ⚠️ | — | — | ❌ | ⚠️ Partial |
| `SYS-IIS-CFG` | ⚠️ | — | ⚠️ | ⚠️ | ⚠️ | ❌ | ⚠️ Partial |
| `SYS-REACT-SPA` | — | — | ⚠️ | — | — | ⚠️ | ⚠️ Partial |
| `SYS-ANGULAR-CLIENT` | — | — | ⚠️ | — | — | — | ⚠️ Partial |
| `SYS-HTML5-CLIENT` | — | — | — | — | — | ⚠️ | ⚠️ Partial |
| `SYS-WEBFORMS` | ✅ | — | — | — | — | — | ✅ Implemented |
| `SYS-BUILD-PIPELINE` | — | — | ❌ | — | — | ❌ | ❌ Not Implemented |
| `SYS-DEPENDENCY-MGMT` | — | — | ❌ | — | — | — | ❌ Not Implemented |

### Coverage Summary

| Control Family | Fully Implemented | Partially Implemented | Not Implemented | Not Applicable |
|---|---|---|---|---|
| **AC** | 6 systems | 6 systems | 0 systems | 22 systems |
| **AU** | 1 system | 5 systems | 1 system | 27 systems |
| **CM** | 1 system | 8 systems | 4 systems | 21 systems |
| **IA** | 2 systems | 4 systems | 1 system | 27 systems |
| **SC** | 1 system | 7 systems | 2 systems | 24 systems |
| **SI** | 0 systems | 10 systems | 3 systems | 21 systems |

---

## NIST CSF Posture Summary

The overall NIST Cybersecurity Framework posture of SplendidCRM v15.2 reflects an organization that has invested substantially in **Protect** capabilities while leaving **Detect**, **Respond**, and **Recover** functions critically underdeveloped. Per COSO Principle 11 (Selects and Develops General Controls over Technology), effective technology governance requires balanced investment across all five CSF functions — the current imbalance creates a security posture that can prevent some threats but cannot reliably detect, respond to, or recover from successful attacks.

**Protect** is the strongest function, anchored by the 4-tier authorization model in `Security.cs`, TLS 1.2 transport enforcement, multi-mechanism authentication (Forms, Windows/NTLM, DuoUniversal 2FA), and consistent authentication checks across all API surfaces. However, even the Protect function is weakened by the Critical-severity MD5 password hashing, disabled ASP.NET input validation, and absence of rate limiting on API endpoints.

**Identify** is partially implemented — system assets are identifiable through build files and source code analysis, but no formal asset inventory, risk register, data classification scheme, or risk management framework exists. Per COSO Principle 7, risk identification is a prerequisite for risk management, and the absence of formal identification processes limits the organization's ability to prioritize security investments.

**Detect** is minimally implemented. SQL audit triggers provide entity-level change detection, and `SplendidError.cs` logs application errors. However, the complete absence of external monitoring (APM, SIEM), anomaly detection, automated alerting, intrusion detection, and security event correlation means that security incidents may persist undetected. Per COSO Principle 16, ongoing evaluations require detection mechanisms — the current state does not support continuous monitoring.

**Respond** is minimally implemented. Centralized error logging provides forensic data, and scheduler reentrancy guards prevent certain cascading failures. However, no incident response procedures, no escalation paths, no communication templates, and no mitigation playbooks exist. Per COSO Principle 17, deficiency communication requires structured response mechanisms.

**Recover** is minimally implemented. Scheduled database backup jobs exist in the `SchedulerUtils.cs` job catalog, and SQL Server native backup capabilities are leveraged. However, no RPO/RTO targets, no disaster recovery runbooks, no recovery testing, and no post-incident improvement processes are documented. Per COSO Principle 10, recovery controls must be both selected and tested — backup without tested recovery is an incomplete control.

| CSF Function | Maturity | Key Strength | Key Gap |
|---|---|---|---|
| **Identify** | ⚠️ Partial | Asset identification via build files | No formal risk register or data classification |
| **Protect** | ⚠️ Partial | 4-tier authorization model | MD5 hashing, disabled input validation |
| **Detect** | ❌ Minimal | SQL audit triggers | No external monitoring, SIEM, or alerting |
| **Respond** | ❌ Minimal | Error logging for forensics | No incident response procedures |
| **Recover** | ❌ Minimal | Scheduled backup jobs | No RPO/RTO targets or recovery testing |

The CSF posture analysis underscores a fundamental architectural truth: SplendidCRM was designed as a feature-rich CRM application with operational access controls, not as a security-monitored enterprise platform. The Protect investments reflect necessary application-level controls, while the Detect/Respond/Recover gaps reflect the absence of operational security infrastructure. Per COSO Principle 11, the path to maturity requires investment in detection, response, and recovery capabilities proportionate to the sensitivity of the data managed by the platform.

---

*Cross-references: [System Registry](./system-registry.md) · [COSO Mapping](./coso-mapping.md) · [CIS Mapping](./cis-mapping.md)*
