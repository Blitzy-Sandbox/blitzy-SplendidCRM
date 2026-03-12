# Directive 0 — CIS Controls v8 IG2/IG3 Safeguard Mapping

## Introduction

This document maps each system in the SplendidCRM Community Edition v15.2 System Registry to applicable CIS Controls v8 safeguards at Implementation Group 2 (IG2) and Implementation Group 3 (IG3) levels. The system_id registry defined in [system-registry.md](./system-registry.md) serves as the authoritative reference for all mappings herein. CIS Controls v8 provides implementation-level security benchmarks that complement the governance framework of COSO — specifically COSO Principle 10 (Selects and Develops Control Activities), which mandates that organizations select and develop control activities that contribute to the mitigation of risks to the achievement of objectives — and the technical specificity of NIST SP 800-53 Rev 5. Together, these three frameworks form a layered assurance model: COSO establishes governance intent, NIST defines technical control requirements, and CIS Controls prescribe measurable implementation safeguards.

The assessment targets IG2 and IG3 safeguards because SplendidCRM operates as a business-critical CRM platform handling sensitive customer data, financial records, and communication histories. Per COSO Principle 11 (Selects and Develops General Controls over Technology), the technology controls governing this platform must be evaluated against benchmarks commensurate with the data sensitivity and operational criticality of the system. IG2/IG3 safeguards provide the appropriate rigor for this assessment. Where CIS Controls conflict with COSO or NIST SP 800-53 requirements, the more restrictive requirement is applied and the conflict is flagged.

### How to Read This Document

Each CIS Control section contains a mapping table with the following columns:

| Column | Description |
|---|---|
| **system_id** | Unique system identifier from the [System Registry](./system-registry.md) |
| **CIS Safeguard** | Specific CIS Controls v8 safeguard identifier and name |
| **Coverage** | Assessment status: **Implemented**, **Partial**, or **Not Implemented** |
| **Evidence/Notes** | Observed codebase evidence with file path citations |

Coverage assessments reflect only what is actually observed in the codebase. No aspirational controls are documented.

---

## CIS Controls v8 Reference

The following CIS Controls v8 safeguards are assessed in this audit. Each control is applicable across all three Implementation Groups (IG1/IG2/IG3), with IG2 building on IG1 and IG3 building on IG2. This assessment evaluates compliance at the IG2/IG3 level, which demands more rigorous implementation than IG1 baselines.

| CIS Control | Full Name | IG Applicability | Audit Scope |
|---|---|---|---|
| **CIS Control 1** | Inventory and Control of Enterprise Assets | IG1 / IG2 / IG3 | Infrastructure assets: servers, databases, client interfaces |
| **CIS Control 2** | Inventory and Control of Software Assets | IG1 / IG2 / IG3 | Software dependencies: .NET DLLs, npm packages, client libraries |
| **CIS Control 3** | Data Protection | IG1 / IG2 / IG3 | Data-at-rest encryption, data classification, access controls on data stores |
| **CIS Control 4** | Secure Configuration of Enterprise Assets and Software | IG1 / IG2 / IG3 | Web.config hardening, session configuration, IIS pipeline settings |
| **CIS Control 5** | Account Management | IG1 / IG2 / IG3 | User authentication, password hashing, credential storage |
| **CIS Control 6** | Access Control Management | IG1 / IG2 / IG3 | Authorization model, ACL enforcement, admin privilege boundaries |
| **CIS Control 8** | Audit Log Management | IG1 / IG2 / IG3 | Audit trails, system logging, event monitoring |
| **CIS Control 9** | Email and Web Browser Protections | IG1 / IG2 / IG3 | TLS enforcement, email security, cookie hardening |
| **CIS Control 16** | Application Software Security | IG1 / IG2 / IG3 | Input validation, testing infrastructure, secure development practices |

Per COSO Principle 12 (Deploys Through Policies and Procedures), these controls should be deployed through documented policies. The assessment evaluates both the presence of technical controls and the existence of supporting policy documentation.

---

## CIS Control 1 — Inventory and Control of Enterprise Assets

CIS Control 1 requires organizations to actively manage all enterprise assets connected to the infrastructure, ensuring only authorized assets are present and unauthorized assets are identified and managed. Per COSO Principle 3 (Establishes Structure, Authority, and Responsibility), the organization must maintain visibility into its asset landscape to establish accountability.

### Safeguard 1.1 — Establish and Maintain Detailed Enterprise Asset Inventory

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SQL-DB` | 1.1 — Asset Inventory | Partial | SQL Server is the sole data store. Connection string in `Web.config` references `(local)\SplendidCRM` as the server instance. No formal infrastructure inventory exists. Source: `SplendidCRM/Web.config:L5` |
| `SYS-IIS-CFG` | 1.1 — Asset Inventory | Partial | IIS configuration is defined in `Web.config` with Integrated Pipeline settings. No infrastructure-as-code inventory, no deployment manifest beyond the configuration file. Source: `SplendidCRM/Web.config` |
| `SYS-ASPNET-APP` | 1.1 — Asset Inventory | Partial | ASP.NET 4.8 application server defined via `.csproj` file. No formal asset catalog beyond the project file. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj` |
| `SYS-REACT-SPA` | 1.1 — Asset Inventory | Partial | React SPA client identified in `package.json` (v15.2.9366). No unified asset registry spanning all client interfaces. Source: `SplendidCRM/React/package.json` |
| `SYS-ANGULAR-CLIENT` | 1.1 — Asset Inventory | Partial | Angular experimental client identified in its own `package.json`. No cross-client asset tracking. Source: `SplendidCRM/Angular/package.json` |
| `SYS-HTML5-CLIENT` | 1.1 — Asset Inventory | Not Implemented | Legacy HTML5/jQuery client resides in `SplendidCRM/html5/` directory. No formal asset inventory entry; discovered only through directory structure inspection. |
| `SYS-WEBFORMS` | 1.1 — Asset Inventory | Partial | WebForms pages distributed across 40+ CRM module folders. No centralized inventory of WebForms assets. |

### Safeguard 1.2 — Address Unauthorized Assets

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-INTEGRATION-STUBS` | 1.2 — Unauthorized Assets | Not Implemented | 16+ enterprise integration stub directories exist as compiled but non-functional code. No process observed for identifying or removing unauthorized/unused assets. Source: `SplendidCRM/_code/Spring.Social.*` |
| `SYS-WORKFLOW` | 1.2 — Unauthorized Assets | Not Implemented | Workflow engine stubs compile but perform no runtime business logic. No asset lifecycle management observed for decommissioning unused capabilities. |

### Coverage Assessment

**Overall CIS Control 1 Coverage: Partial** — Enterprise assets are identifiable through source code structure (project files, package manifests, directory layout), but no formal enterprise asset inventory exists. The four coexisting client interfaces (`SYS-REACT-SPA`, `SYS-ANGULAR-CLIENT`, `SYS-HTML5-CLIENT`, `SYS-WEBFORMS`) lack a unified asset registry. This represents a gap against COSO Principle 1 (Demonstrates Commitment to Integrity and Ethical Values), as comprehensive asset visibility is foundational to maintaining control integrity.

---

## CIS Control 2 — Inventory and Control of Software Assets

CIS Control 2 requires organizations to actively manage all software on the network so that only authorized software is installed and can execute, and unauthorized software is found and prevented from installation or execution. Per COSO Principle 9 (Identifies and Analyzes Significant Change), changes to the software inventory — including dependency updates — must be identified and assessed for risk impact.

### Safeguard 2.1 — Establish and Maintain a Software Inventory

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-DEPENDENCY-MGMT` | 2.1 — Software Inventory | Not Implemented | 38 manually managed DLLs in `BackupBin2012/` referenced via HintPath in the `.csproj` file. No NuGet package management, no Software Bill of Materials (SBOM), no centralized software inventory. Risk Severity: **Critical**. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj:L56–100` |
| `SYS-REACT-SPA` | 2.1 — Software Inventory | Partial | npm-managed dependencies listed in `SplendidCRM/React/package.json` with 80+ direct dependencies. `yarn.lock` provides version pinning. No SBOM generation observed. Source: `SplendidCRM/React/package.json` |
| `SYS-ANGULAR-CLIENT` | 2.1 — Software Inventory | Partial | npm-managed dependencies in `SplendidCRM/Angular/package.json`. Experimental client (Angular ~13.3.0). Package version trails main release (14.5.8220 vs 15.2). Source: `SplendidCRM/Angular/package.json` |
| `SYS-HTML5-CLIENT` | 2.1 — Software Inventory | Not Implemented | jQuery (1.4.2–2.2.4), RequireJS 2.3.3, and other libraries are manually included as static files. No package management system. No version tracking mechanism. Risk Severity: **Critical**. |
| `SYS-BUILD-PIPELINE` | 2.1 — Software Inventory | Partial | Build tooling identified in three independent pipelines (SQL Build.bat, React yarn/webpack, .NET MSBuild). No unified build manifest. Source: `SQL Scripts Community/Build.bat`, `SplendidCRM/React/package.json` |
| `SYS-INTEGRATION-STUBS` | 2.1 — Software Inventory | Not Implemented | Spring.Social.*, Spring.Rest, TweetinCore, and related integration libraries are compiled stubs with no version management. No tracking of these unused software components. |

### Safeguard 2.2 — Ensure Authorized Software is Currently Supported

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-DEPENDENCY-MGMT` | 2.2 — Supported Software | Not Implemented | No automated vulnerability scanning. No dependency auditing tool (e.g., `npm audit`, NuGet vulnerability scan, OWASP Dependency-Check). DLL versions are fixed without a documented update process. Risk Severity: **Critical**. |
| `SYS-REACT-SPA` | 2.2 — Supported Software | Partial | React 18.2.0 and TypeScript 5.3.3 are current. However, no `npm audit` integration is observed in build scripts. SignalR client version asymmetry: server uses v1.2.2 (legacy) while React client uses @microsoft/signalr 8.0.0. Risk Severity: **Moderate**. Source: `SplendidCRM/React/package.json` |
| `SYS-HTML5-CLIENT` | 2.2 — Supported Software | Not Implemented | jQuery 1.4.2 is critically outdated (released 2010). jQuery 2.2.4 is end-of-life. No mechanism to track or update these libraries. Risk Severity: **Critical**. |

### Safeguard 2.3 — Address Unauthorized Software

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-DEPENDENCY-MGMT` | 2.3 — Unauthorized Software | Not Implemented | No allowlist/denylist mechanism for software dependencies. No process for identifying unauthorized or rogue dependencies. |

### Coverage Assessment

**Overall CIS Control 2 Coverage: Not Implemented** — The .NET backend relies entirely on manually managed DLLs with no package management system, no SBOM, and no vulnerability scanning. While the React SPA uses npm with version pinning, no automated security auditing is integrated. The HTML5 legacy client uses critically outdated libraries with no update path. This represents a fundamental gap against COSO Principle 10 (Selects and Develops Control Activities) — the absence of software inventory controls means the organization cannot effectively select and develop control activities to mitigate software supply chain risks.

---

## CIS Control 3 — Data Protection

CIS Control 3 requires organizations to develop processes and technical controls to identify, classify, securely handle, retain, and dispose of data. Per COSO Principle 13 (Uses Relevant Information), data management controls must ensure the quality and protection of information assets.

### Safeguard 3.1 — Establish and Maintain a Data Management Process

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SQL-DB` | 3.1 — Data Management | Partial | SQL Server serves as the sole persistent data store with 229 base tables, 581 views, and 833 stored procedures. Data lifecycle is managed through stored procedures, but no formal data classification scheme or data retention policy is documented. Source: `SQL Scripts Community/` |
| `SYS-DB-ACCESS` | 3.1 — Data Management | Partial | `Sql.cs` provides parameterized query execution and connection management. Transaction safety is per-operation (no distributed transaction coordinator). Data access patterns exist but are not documented as a data management process. Source: `SplendidCRM/_code/Sql.cs` |

### Safeguard 3.6 — Encrypt Data on End-User Devices

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-REACT-SPA` | 3.6 — End-User Encryption | Partial | IndexedDB offline caching via `idb` library (8.0.0) stores data on end-user devices. No application-level encryption of cached data is observed. TLS protects data in transit. Source: `SplendidCRM/React/package.json` |
| `SYS-HTML5-CLIENT` | 3.6 — End-User Encryption | Partial | AES encryption utilities present in the HTML5 client. Offline manifest support caches data locally. Source: `SplendidCRM/html5/` |

### Safeguard 3.10 — Encrypt Sensitive Data in Transit

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SQL-DB` | 3.10 — Transit Encryption | Partial | TLS 1.2 enforced at the application level in `Global.asax.cs` `Application_Start`. Database connection uses local connection string `(local)\SplendidCRM` — transit encryption to the database depends on SQL Server TLS configuration, which is not managed by the application. Source: `SplendidCRM/Global.asax.cs:L98–101` |
| `SYS-DB-ACCESS` | 3.10 — Transit Encryption | Partial | Application enforces TLS 1.2 for external communications via `SecurityProtocolType.Tls12`. Internal database connections rely on SQL Server configuration. Source: `SplendidCRM/Global.asax.cs:L98–101` |

### Safeguard 3.11 — Encrypt Sensitive Data at Rest

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 3.11 — At-Rest Encryption | Partial | Stored credentials encrypted with Rijndael symmetric encryption using Guid-based keys (128-bit block/key). Passwords hashed with MD5 (weak). No database-level Transparent Data Encryption (TDE) is configured by the application. Source: `SplendidCRM/_code/Security.cs:L412–454` |
| `SYS-SQL-DB` | 3.11 — At-Rest Encryption | Not Implemented | No application-level data-at-rest encryption for CRM entity data. SQL Server TDE configuration is outside application control. |

### Coverage Assessment

**Overall CIS Control 3 Coverage: Partial** — TLS 1.2 is enforced for external communications, and credential encryption (Rijndael) is implemented for stored secrets. However, MD5 password hashing represents a Critical weakness, no data classification scheme exists, and data-at-rest encryption for CRM entity data is absent. Per COSO Principle 11 (Selects and Develops General Controls over Technology), the absence of comprehensive data protection controls across all data states (transit, rest, processing) represents a significant technology control gap.

---

## CIS Control 4 — Secure Configuration of Enterprise Assets and Software

CIS Control 4 requires establishing and maintaining secure configurations for enterprise assets and software. Per COSO Principle 12 (Deploys Through Policies and Procedures), configuration standards should be deployed through documented policies with enforced baselines. This control is among the most broadly applicable across SplendidCRM systems.

### Safeguard 4.1 — Establish and Maintain a Secure Configuration Process

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-IIS-CFG` | 4.1 — Secure Config Process | Not Implemented | No documented secure configuration baseline exists. `Web.config` contains multiple security relaxations without documented justification. Source: `SplendidCRM/Web.config` |
| `SYS-CONFIG` | 4.1 — Secure Config Process | Not Implemented | Application configuration spans `Web.config` and SQL `CONFIG` table. No configuration management policy or change control documentation observed. Source: `SplendidCRM/Web.config`, `SQL Scripts Community/Data/` |

### Safeguard 4.2 — Establish and Maintain a Secure Configuration Process for Network Infrastructure

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-IIS-CFG` | 4.2 — Network Config | Not Implemented | IIS Integrated Pipeline configuration exists but no network infrastructure hardening documentation is present. `runAllManagedModulesForAllRequests="true"` routes all requests through managed modules without documented security justification. Source: `SplendidCRM/Web.config:L135` |

### Safeguard 4.4 — Implement and Manage a Firewall on Servers

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-IIS-CFG` | 4.4 — Server Firewall | Not Implemented | No application-level firewall configuration observed. `Web.config` permits request sizes up to 100MB (`maxRequestLength="104857600"`) and execution timeouts of 10 minutes (`executionTimeout="600"`) without rate limiting. Source: `SplendidCRM/Web.config:L111` |
| `SYS-ASPNET-APP` | 4.4 — Server Firewall | Not Implemented | No IP filtering, rate limiting, or request throttling mechanisms observed in the ASP.NET application layer. |

### Safeguard 4.6 — Securely Manage Enterprise Assets and Software

Specific configuration findings mapped to system_ids:

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-IIS-CFG` | 4.6 — Secure Management | Not Implemented | `requestValidationMode="2.0"` downgrades ASP.NET request validation to legacy mode, bypassing .NET 4.5+ validation improvements. Risk Severity: **Critical**. Source: `SplendidCRM/Web.config:L111` |
| `SYS-IIS-CFG` | 4.6 — Secure Management | Not Implemented | `enableEventValidation="false"` disables WebForms event validation. `validateRequest="false"` disables page-level request validation. Both increase XSS and injection attack surface. Risk Severity: **Critical**. Source: `SplendidCRM/Web.config:L115` |
| `SYS-IIS-CFG` | 4.6 — Secure Management | Not Implemented | `customErrors mode="Off"` exposes detailed ASP.NET error information including stack traces to all clients (local and remote). Risk Severity: **Critical**. Source: `SplendidCRM/Web.config:L51` |
| `SYS-IIS-CFG` | 4.6 — Secure Management | Implemented | `compilation debug="false"` is correctly set for production, preventing debug symbol exposure. Source: `SplendidCRM/Web.config:L34` |
| `SYS-CONFIG` | 4.6 — Secure Management | Partial | Session state configured as `mode="InProc"` with `timeout="20"`. InProc session is non-resilient (lost on app pool recycle) but appropriate for single-server deployment. Risk Severity: **Moderate**. Source: `SplendidCRM/Web.config:L100` |
| `SYS-CONFIG` | 4.6 — Secure Management | Partial | `authentication mode="Windows"` delegates authentication to IIS. Appropriate for intranet deployments but requires IIS-level hardening documentation that is absent. Source: `SplendidCRM/Web.config:L65` |
| `SYS-INIT` | 4.6 — Secure Management | Implemented | TLS 1.2 enforcement in `Application_Start` via `SecurityProtocolType.Tls12`. Unobserved task exception handling prevents SignalR crashes from crashing the application. Source: `SplendidCRM/Global.asax.cs:L96–101` |
| `SYS-CACHE` | 4.6 — Secure Management | Partial | In-memory caching via `HttpApplicationState` and `HttpRuntime.Cache`. Cache invalidation is implemented but cache poisoning protections are not documented. Source: `SplendidCRM/_code/SplendidCache.cs` |
| `SYS-SCHEDULER` | 4.6 — Secure Management | Partial | Timer intervals are configured constants (Scheduler: 5 min, Email: 1 min, Archive: 5 min). Reentrancy guards (`bInsideTimer`) prevent concurrent execution. No externally configurable schedule hardening. Source: `SplendidCRM/_code/SchedulerUtils.cs:L34–36` |
| `SYS-ADMIN` | 4.6 — Secure Management | Partial | Administration modules enforce `IS_ADMIN` checks. Configuration changes propagate through cache invalidation. No configuration change audit trail beyond `SYSTEM_LOG`. Source: `SplendidCRM/Administration/Rest.svc.cs` |
| `SYS-WORKFLOW` | 4.6 — Secure Management | Partial | Stubbed implementations maintain compilation compatibility. Non-functional stubs pose minimal configuration risk. Source: `SplendidCRM/_code/WorkflowInit.cs` |
| `SYS-L10N` | 4.6 — Secure Management | Implemented | Localization data is seeded from 112 SQL terminology scripts at deployment time. No runtime configuration exposure. Source: `SQL Scripts Community/Terminology/` |
| `SYS-SQL-DB` | 4.6 — Secure Management | Partial | Database connection string in `Web.config` uses `user id=sa` with a plaintext password. Risk Severity: **Critical**. Stored procedures use parameterized queries mitigating SQL injection. Source: `SplendidCRM/Web.config:L5` |
| `SYS-BUILD-PIPELINE` | 4.6 — Secure Management | Partial | Build scripts exist for SQL (`Build.bat`), React (`yarn build`), and .NET (MSBuild). No build hardening or integrity verification. Source: `SQL Scripts Community/Build.bat` |
| `SYS-API-ADMIN` | 4.6 — Secure Management | Partial | Admin REST API enforces `IS_ADMIN` checks on all endpoints. No API rate limiting or request throttling observed. Source: `SplendidCRM/Administration/Rest.svc.cs` |

### Coverage Assessment

**Overall CIS Control 4 Coverage: Not Implemented** — Multiple Critical-severity secure configuration gaps exist in `Web.config`: disabled request validation (`requestValidationMode="2.0"`, `validateRequest="false"`), disabled event validation (`enableEventValidation="false"`), exposed error details (`customErrors mode="Off"`), and plaintext SA credentials in the connection string. While some positive configurations exist (TLS 1.2 enforcement, debug mode disabled, parameterized queries), the aggregate configuration posture fails to meet IG2/IG3 safeguard requirements. Per COSO Principle 10, the platform's control activities for secure configuration are materially deficient.

---

## CIS Control 5 — Account Management

CIS Control 5 requires using processes and tools to assign and manage authorization to credentials for user accounts. Per COSO Principle 5 (Enforces Accountability), account management controls are essential for establishing individual accountability for actions within the system.

### Safeguard 5.1 — Establish and Maintain an Inventory of Accounts

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 5.1 — Account Inventory | Partial | User accounts are maintained in the `USERS` table with `IS_ADMIN`, `IS_ADMIN_DELEGATE`, `PORTAL_ONLY`, `STATUS`, and `TEAM_ID` attributes stored in session state. Login tracking via `USERS_LOGINS` table. No automated account inventory review process observed. Source: `SplendidCRM/_code/Security.cs:L40–215` |
| `SYS-AUTH-AD` | 5.1 — Account Inventory | Partial | Active Directory integration provides external account management when Windows authentication is used. AD account inventory is managed externally. Source: `SplendidCRM/_code/ActiveDirectory.cs` |
| `SYS-ADMIN` | 5.1 — Account Inventory | Partial | Administration modules include `UserLogins/` for login tracking and `Users/` management. No automated periodic account review mechanism observed. Source: `SplendidCRM/Administration/UserLogins/` |

### Safeguard 5.2 — Use Unique Passwords

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 5.2 — Unique Passwords | Partial | MD5 password hashing (`MD5CryptoServiceProvider`) for SugarCRM backward compatibility. MD5 is cryptographically broken and unsuitable for password hashing (collision attacks demonstrated). Risk Severity: **Critical**. Source: `SplendidCRM/_code/Security.cs:L397–406` |
| `SYS-SECURITY` | 5.2 — Unique Passwords | Partial | Rijndael symmetric encryption (128-bit) used for stored credential encryption with Guid-based keys. Source: `SplendidCRM/_code/Security.cs:L412–454` |

### Safeguard 5.3 — Disable Dormant Accounts

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 5.3 — Dormant Accounts | Not Implemented | No automated dormant account detection or disabling mechanism observed in the application layer. User status management is manual through Administration UI. |
| `SYS-ADMIN` | 5.3 — Dormant Accounts | Not Implemented | `PasswordManager/` admin module exists, but no automated account lifecycle management (dormancy detection, scheduled deactivation) is observed. Source: `SplendidCRM/Administration/PasswordManager/` |

### Safeguard 5.4 — Restrict Administrator Privileges to Dedicated Administrator Accounts

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 5.4 — Admin Privileges | Partial | `IS_ADMIN` and `IS_ADMIN_DELEGATE` flags distinguish admin from non-admin accounts. Admin delegation allows partial admin privileges. However, no separation of duty enforcement (e.g., requiring dedicated admin accounts separate from daily-use accounts) is observed. Source: `SplendidCRM/_code/Security.cs:L137–167` |
| `SYS-AUTH-DUO` | 5.4 — Admin Privileges | Partial | DuoUniversal 2FA provides an additional authentication factor for admin accounts when enabled. Optional feature that must be explicitly configured. Source: `SplendidCRM/Administration/DuoUniversal/` |

### Safeguard 5.5 — Establish and Maintain an Inventory of Service Accounts

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 5.5 — Service Accounts | Not Implemented | Background timers execute under the application's IIS identity. Scheduler and email timers use `Guid.Empty` as `USER_ID` when `HttpContext` is null, effectively operating as an anonymous service identity. No formal service account inventory. Source: `SplendidCRM/_code/Security.cs:L48–49` |

### Coverage Assessment

**Overall CIS Control 5 Coverage: Partial** — Account management infrastructure exists with user tables, login tracking, admin/delegate role separation, AD integration, and optional 2FA. However, the MD5 password hashing is a Critical weakness, no automated dormant account management exists, no service account inventory is maintained, and password complexity enforcement is not visible at the application layer. Per COSO Principle 5 (Enforces Accountability), the weaknesses in credential protection (MD5) undermine the accountability chain that depends on authentic user identification.

---

## CIS Control 6 — Access Control Management

CIS Control 6 requires using processes and tools to create, assign, manage, and revoke access credentials and privileges for user, administrator, and service accounts. Per COSO Principle 3 (Establishes Structure, Authority, and Responsibility), access control defines organizational boundaries and authority within the system.

### Safeguard 6.1 — Establish an Access Granting Process

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 6.1 — Access Granting | Partial | 4-tier authorization model: (1) Module ACL → (2) Team filtering → (3) Field-level access → (4) Record-level ownership checks. ACL is cached in Session and Application state via `SetModuleAccess()`, `SetUserAccess()`, `GetUserAccess()`. No formal documented access granting process. Source: `SplendidCRM/_code/Security.cs:L489–567` |
| `SYS-ADMIN` | 6.1 — Access Granting | Partial | `ACLRoles/` module provides role-based access management UI. Roles can be assigned to users with module-level granularity (access, view, list, edit, delete, import, export). Source: `SplendidCRM/Administration/ACLRoles/` |

### Safeguard 6.2 — Establish an Access Revoking Process

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 6.2 — Access Revoking | Partial | User status can be set to inactive via Administration. Session-based ACL means active sessions retain permissions until session expires (20-minute timeout) or is explicitly invalidated. No immediate session termination on access revocation is observed. Risk Severity: **Moderate**. Source: `SplendidCRM/Web.config:L100` |

### Safeguard 6.3 — Require MFA for Externally-Exposed Applications

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-AUTH-DUO` | 6.3 — MFA | Partial | DuoUniversal 2FA integration available as optional feature. When enabled, provides TOTP/Push-based second factor. Not enforced by default — requires explicit administrative configuration. Source: `SplendidCRM/Administration/DuoUniversal/` |
| `SYS-AUTH-AD` | 6.3 — MFA | Partial | Windows/NTLM authentication delegates MFA to Active Directory policy. ADFS/Azure AD JWT validation supports external MFA providers. Some SSO methods are stubbed in Community Edition. Source: `SplendidCRM/_code/ActiveDirectory.cs` |

### Safeguard 6.4 — Require MFA for Remote Network Access

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-API-REST` | 6.4 — Remote MFA | Not Implemented | REST API authentication via `Security.IsAuthenticated()` validates session presence but does not enforce MFA on API access. Source: `SplendidCRM/Rest.svc.cs` |
| `SYS-API-SOAP` | 6.4 — Remote MFA | Not Implemented | SOAP API uses session-based authentication without MFA enforcement. Source: `SplendidCRM/soap.asmx.cs` |

### Safeguard 6.5 — Require MFA for Administrative Access

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-API-ADMIN` | 6.5 — Admin MFA | Not Implemented | Admin REST API enforces `IS_ADMIN` privilege checks but does not require MFA for administrative operations. Risk Severity: **Moderate**. Source: `SplendidCRM/Administration/Rest.svc.cs` |

### Safeguard 6.7 — Centralize Access Control

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 6.7 — Centralized ACL | Implemented | `Security.cs` serves as the centralized access control enforcement point. All API endpoints, WebForms pages, and background operations route through `Security.IsAuthenticated()` and `Security.GetUserAccess()`. `Security.Filter()` injects team membership joins and assignment-set joins directly into SQL commands. Source: `SplendidCRM/_code/Security.cs:L355, L842–856` |
| `SYS-BUSINESS-LOGIC` | 6.7 — Centralized ACL | Implemented | All 40+ CRM modules enforce ACL via centralized `Security.Filter()` calls that inject WHERE clause restrictions based on user context, team membership, and module-level permissions. Source: `SplendidCRM/_code/Security.cs:L842` |
| `SYS-REALTIME` | 6.7 — Centralized ACL | Implemented | SignalR authorization via `SplendidHubAuthorize.cs` implements `IAuthorizeHubConnection` and `IAuthorizeHubMethodInvocation`, routing through session-based security context. Source: `SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs` |
| `SYS-WEBFORMS` | 6.7 — Centralized ACL | Implemented | WebForms pages enforce access control through `SplendidPage.cs` base class which validates authentication and module access on page load. |

### Safeguard 6.8 — Define and Maintain Role-Based Access Control

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 6.8 — RBAC | Implemented | Role-based access control implemented through `ACL_FIELD_ACCESS` class with constants: `FULL_ACCESS` (100), `READ_OWNER_WRITE`, `READ_ONLY`, `OWNER_READ_OWNER_WRITE`, `OWNER_READ_ONLY`, `NONE` (-98), `NOT_SET` (-1). Field-level and module-level permissions are combined to produce effective access rights. Source: `SplendidCRM/_code/Security.cs:L715–818` |
| `SYS-ADMIN` | 6.8 — RBAC | Implemented | ACL role management via `Administration/ACLRoles/` provides CRUD operations for role definitions with per-module, per-action granularity. Source: `SplendidCRM/Administration/ACLRoles/` |

### Coverage Assessment

**Overall CIS Control 6 Coverage: Partial** — A robust 4-tier authorization model is implemented with centralized enforcement through `Security.cs`, role-based access control with field-level granularity, and consistent ACL application across APIs, WebForms, and SignalR. However, MFA is optional and not enforced by default, no MFA is required for API or administrative access, and access revocation has a 20-minute session persistence window. Per COSO Principle 10, the authorization control activities are well-developed but the authentication enforcement (MFA gaps) undermines their effectiveness.

---

## CIS Control 8 — Audit Log Management

CIS Control 8 requires collecting, alerting, reviewing, and retaining audit logs of events that could help detect, understand, or recover from an attack. Per COSO Principle 16 (Conducts Ongoing and/or Separate Evaluations), audit logging is the foundation of monitoring activities that enable the organization to evaluate control effectiveness.

### Safeguard 8.1 — Establish and Maintain an Audit Log Management Process

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-AUDIT` | 8.1 — Log Management | Partial | SQL audit triggers generate `_AUDIT` companion tables for all CRM entities, logging INSERT/UPDATE/DELETE operations with timestamps and user attribution. Trigger generation is conditional on `SYSTEM_SYNC_CONFIG` table presence. No formal log management policy or retention schedule documented. Source: `SQL Scripts Community/Triggers/BuildAllAuditTables.1.sql` |
| `SYS-ERROR-OBSERVABILITY` | 8.1 — Log Management | Partial | `SplendidError.cs` logs all exceptions to `SYSTEM_LOG` table via `spSYSTEM_LOG_InsertOnly`. Maintains Application-state `DataTable` for in-memory error tracking. No log lifecycle management or retention policy observed. Source: `SplendidCRM/_code/SplendidError.cs:L36–74` |

### Safeguard 8.2 — Collect Audit Logs

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-AUDIT` | 8.2 — Collect Logs | Implemented | Entity-level audit triggers capture all data modifications across CRM tables. `_AUDIT` tables store before/after values for each changed field. Source: `SQL Scripts Community/Triggers/BuildAllAuditTables.1.sql` |
| `SYS-SECURITY` | 8.2 — Collect Logs | Implemented | Login events captured in `USERS_LOGINS` table with timestamps, IP addresses, and authentication results. Source: `SplendidCRM/_code/Security.cs` |
| `SYS-ERROR-OBSERVABILITY` | 8.2 — Collect Logs | Implemented | All application errors and warnings logged to `SYSTEM_LOG` with error type classification (Error/Warning), stack trace, and timestamp. Source: `SplendidCRM/_code/SplendidError.cs:L69–73` |
| `SYS-SCHEDULER` | 8.2 — Collect Logs | Implemented | Scheduler job execution start/end events logged via `SplendidError.SystemWarning`. Timer activation events recorded. Source: `SplendidCRM/Global.asax.cs:L57`, `SplendidCRM/_code/SchedulerUtils.cs` |
| `SYS-REPORTING` | 8.2 — Collect Logs | Partial | Report generation events are logged through the general `SYSTEM_LOG` mechanism. No dedicated reporting audit trail. Source: `SplendidCRM/_code/SplendidError.cs` |

### Safeguard 8.3 — Ensure Adequate Audit Log Storage

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-AUDIT` | 8.3 — Log Storage | Partial | Audit data stored in SQL Server alongside operational data. `CleanSystemLog` scheduled job exists for log pruning, but no documented retention policy, storage sizing, or archival strategy observed. Source: `SplendidCRM/_code/SchedulerUtils.cs:L47` |
| `SYS-ERROR-OBSERVABILITY` | 8.3 — Log Storage | Partial | System logs stored in SQL `SYSTEM_LOG` table. `CleanSystemLog` job runs on schedule. No log storage capacity monitoring. |

### Safeguard 8.5 — Collect Detailed Audit Logs

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-AUDIT` | 8.5 — Detailed Logs | Implemented | Audit triggers capture field-level before/after values, user ID, timestamp, and operation type. Comprehensive entity-level audit trail. |
| `SYS-ERROR-OBSERVABILITY` | 8.5 — Detailed Logs | Partial | `SplendidError.SystemMessage` captures error type, source file/line via `StackFrame`, and message. Stack traces included for error-type events. Source: `SplendidCRM/_code/SplendidError.cs:L62–64` |

### Safeguard 8.9 — Centralize Audit Logs

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-AUDIT` | 8.9 — Centralized Logs | Not Implemented | All audit data resides in the local SQL Server database. No external log aggregation, SIEM integration, log forwarding to centralized log management (e.g., Splunk, ELK, Azure Sentinel), or syslog export. Risk Severity: **Critical**. |
| `SYS-ERROR-OBSERVABILITY` | 8.9 — Centralized Logs | Not Implemented | Error logs are database-internal only. No external APM integration, no distributed tracing, no log shipping. Risk Severity: **Critical**. |

### Safeguard 8.11 — Conduct Audit Log Reviews

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-ADMIN` | 8.11 — Log Reviews | Partial | Admin UI provides `SystemLog/` and `AuditEvents/` modules for manual log review. No automated log review, anomaly detection, or alerting mechanisms observed. Source: `SplendidCRM/Administration/SystemLog/`, `SplendidCRM/Administration/AuditEvents/` |

### Safeguard 8.12 — Collect Service Provider Logs

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-EMAIL` | 8.12 — Provider Logs | Not Implemented | Email operations log errors via `SplendidError` but no SMTP/IMAP/POP3 service provider logs are collected or centralized. |
| `SYS-SMS-TELEPHONY` | 8.12 — Provider Logs | Not Implemented | Twilio webhook events are processed but no external service provider audit logs are collected or retained. |

### Coverage Assessment

**Overall CIS Control 8 Coverage: Partial** — Robust internal audit logging exists through SQL audit triggers (entity-level change tracking), `SYSTEM_LOG` (error/warning logging), and `USERS_LOGINS` (authentication events). However, all audit data is database-internal with no external SIEM integration, no centralized log aggregation, no automated alerting on security events, no formal log retention policy, and no automated log review. Per COSO Principle 16, the monitoring capabilities exist at a foundational level but lack the operational maturity required for ongoing evaluation of control effectiveness.

---

## CIS Control 9 — Email and Web Browser Protections

CIS Control 9 requires improving protections and detections of threats from email and web vectors. Per COSO Principle 11 (Selects and Develops General Controls over Technology), email and web browser protections represent essential technology controls for platforms that process communications.

### Safeguard 9.1 — Ensure Use of Only Fully Supported Browsers and Email Clients

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-REACT-SPA` | 9.1 — Supported Browsers | Partial | React 18.2.0 targets modern browsers. Build tooling (Webpack 5.90.2) produces modern JavaScript bundles. No explicit browser compatibility policy or minimum browser version enforcement observed. Source: `SplendidCRM/React/package.json` |
| `SYS-HTML5-CLIENT` | 9.1 — Supported Browsers | Not Implemented | Legacy client supports outdated browsers via jQuery 1.4.2 compatibility. No mechanism to enforce minimum browser version. Risk Severity: **Moderate**. |

### Safeguard 9.2 — Use DNS Filtering Services

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-EMAIL` | 9.2 — DNS Filtering | Not Implemented | No DNS filtering or domain reputation checking observed in the email processing pipeline. `EmailUtils.cs` processes inbound emails without DNS-based spam/malware filtering at the application level. Source: `SplendidCRM/_code/EmailUtils.cs` |

### Safeguard 9.3 — Maintain and Enforce Network-Based URL Filters

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-CAMPAIGN` | 9.3 — URL Filtering | Not Implemented | Campaign tracker endpoints (`campaign_trackerv2.aspx.cs`, `image.aspx.cs`) are publicly accessible without authentication. No URL filtering or reputation checking for tracked links. |

### Safeguard 9.5 — Implement DMARC

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-EMAIL` | 9.5 — DMARC | Not Implemented | No DMARC, SPF, or DKIM validation observed in the email processing pipeline. `SplendidMailClient.cs` handles SMTP delivery but does not verify sender authentication protocols. |

### Safeguard 9.7 — Deploy and Maintain Email Server Anti-Malware Protections

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-EMAIL` | 9.7 — Email Anti-Malware | Not Implemented | `MimeUtils.cs` processes MIME content. `ImapUtils.cs` and `PopUtils.cs` handle inbound email retrieval. No anti-malware scanning of email content or attachments observed. Source: `SplendidCRM/_code/MimeUtils.cs`, `SplendidCRM/_code/ImapUtils.cs` |

### Transport Security Findings

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-INIT` | 9 — Transport | Implemented | TLS 1.2 enforced in `Application_Start` via `ServicePointManager.SecurityProtocol`. Source: `SplendidCRM/Global.asax.cs:L98–101` |
| `SYS-INIT` | 9 — Transport | Implemented | SameSite cookie hardening with browser-specific compatibility checks in `Session_Start`. `DisallowsSameSiteNone()` method handles user-agent-specific SameSite incompatibilities. Source: `SplendidCRM/Global.asax.cs:L110–147` |
| `SYS-EMAIL` | 9 — Transport | Partial | MailKit (IMAP/POP3/SMTP) supports TLS connections. Stored credentials are encrypted with Rijndael. Source: `SplendidCRM/_code/SplendidMailClient.cs` |
| `SYS-SMS-TELEPHONY` | 9 — Transport | Partial | Twilio API calls use TLS 1.2 for outbound communication. Webhook endpoint (`TwiML.aspx.cs`) should enforce TLS for inbound requests via IIS configuration. |

### Coverage Assessment

**Overall CIS Control 9 Coverage: Partial** — TLS 1.2 enforcement and SameSite cookie hardening demonstrate positive transport security controls. MailKit supports TLS connections for email operations. However, no DNS filtering, DMARC/SPF/DKIM validation, URL filtering, or email anti-malware scanning is implemented at the application level. Per COSO Principle 11, the email and web browser technology controls address transport security but lack defense-in-depth protections for content-level threats.

---

## CIS Control 16 — Application Software Security

CIS Control 16 requires managing the security life cycle of in-house developed, hosted, or acquired software to prevent, detect, and remediate security weaknesses before they can impact the enterprise. Per COSO Principle 10 (Selects and Develops Control Activities), application security controls are fundamental control activities for any software-intensive organization.

### Safeguard 16.1 — Establish and Maintain a Secure Application Development Process

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-BUILD-PIPELINE` | 16.1 — Secure SDLC | Not Implemented | No CI/CD pipeline exists. No static analysis tools configured. No secure development lifecycle (SDLC) documentation or security requirements in the build process. Risk Severity: **Critical**. Source: `SQL Scripts Community/Build.bat`, `SplendidCRM/SplendidCRM7_VS2017.csproj` |

### Safeguard 16.2 — Establish and Maintain a Process to Accept and Address Software Vulnerabilities

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-BUILD-PIPELINE` | 16.2 — Vulnerability Process | Not Implemented | No vulnerability disclosure policy, no security advisory process, no automated vulnerability scanning in the build pipeline. |
| `SYS-DEPENDENCY-MGMT` | 16.2 — Vulnerability Process | Not Implemented | No `npm audit`, NuGet vulnerability scan, or OWASP Dependency-Check integration. 38 manually managed DLLs have no version update or vulnerability tracking process. |

### Safeguard 16.3 — Perform Root Cause Analysis on Security Vulnerabilities

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-ERROR-OBSERVABILITY` | 16.3 — Root Cause Analysis | Partial | `SplendidError.cs` captures stack traces for error-type events, providing debugging data. No formal root cause analysis process or security incident investigation workflow exists. Source: `SplendidCRM/_code/SplendidError.cs:L62–64` |

### Safeguard 16.4 — Establish and Manage an Inventory of Third-Party Software Components

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-DEPENDENCY-MGMT` | 16.4 — Third-Party Inventory | Not Implemented | No SBOM. 38 .NET DLLs in `BackupBin2012/` are inventoried only through `.csproj` HintPath references. No third-party component tracking system. Risk Severity: **Critical**. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj` |

### Safeguard 16.5 — Use Up-to-Date and Trusted Third-Party Software Components

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-DEPENDENCY-MGMT` | 16.5 — Updated Components | Not Implemented | DLL versions are fixed without a documented update process. No mechanism to verify component trust or check for known vulnerabilities. Risk Severity: **Critical**. |
| `SYS-HTML5-CLIENT` | 16.5 — Updated Components | Not Implemented | jQuery 1.4.2 (2010) contains numerous known CVEs. jQuery 2.2.4 is end-of-life. No update path exists. Risk Severity: **Critical**. |
| `SYS-REACT-SPA` | 16.5 — Updated Components | Partial | React 18.2.0 and TypeScript 5.3.3 are current releases. SignalR server/client version asymmetry (v1.2.2 server vs v8.0.0 client) creates compatibility risk. Source: `SplendidCRM/React/package.json` |

### Safeguard 16.6 — Establish and Maintain a Severity Rating System and Process for Application Vulnerabilities

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-BUILD-PIPELINE` | 16.6 — Severity Ratings | Not Implemented | No vulnerability severity rating system. No security defect classification or prioritization process. |

### Safeguard 16.9 — Train Developers in Application Security Concepts and Secure Coding

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-BUILD-PIPELINE` | 16.9 — Security Training | Not Implemented | No `CONTRIBUTING.md`, no developer security guidelines, no secure coding standards documentation found in the repository. |

### Safeguard 16.10 — Apply Secure Design Principles in Application Architectures

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 16.10 — Secure Design | Partial | 4-tier authorization model demonstrates defense-in-depth design for access control. Centralized security enforcement via `Security.cs`. However, MD5 hashing and disabled request validation contradict secure design principles. Source: `SplendidCRM/_code/Security.cs` |
| `SYS-API-REST` | 16.10 — Secure Design | Partial | All REST endpoints enforce authentication. JSON serialization via `Newtonsoft.Json`. Input validation gaps due to `requestValidationMode="2.0"` at the pipeline level. No Swagger/OpenAPI spec. Source: `SplendidCRM/Rest.svc.cs` |
| `SYS-API-SOAP` | 16.10 — Secure Design | Partial | SOAP API enforces session authentication. WSDL is auto-generated only at runtime — no maintained API documentation artifact. Source: `SplendidCRM/soap.asmx.cs` |

### Safeguard 16.11 — Leverage Vetted Modules or Services for Application Security Components

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-SECURITY` | 16.11 — Vetted Modules | Partial | Uses `BouncyCastle.Crypto.dll` (vetted cryptographic library), `Microsoft.Owin.Security.dll` (vetted authentication), and `Microsoft.AspNet.SignalR.Core.dll` (vetted real-time). MD5 usage from `System.Security.Cryptography` is a standard library but cryptographically broken for password hashing. Source: `SplendidCRM/SplendidCRM7_VS2017.csproj` |

### Safeguard 16.12 — Implement Code-Level Security Checks

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-BUILD-PIPELINE` | 16.12 — Code Security Checks | Not Implemented | No static application security testing (SAST). No dynamic application security testing (DAST). No code review process documented. Zero automated testing infrastructure — no unit tests, integration tests, or E2E tests across all tiers. Risk Severity: **Critical**. |
| `SYS-ASPNET-APP` | 16.12 — Code Security Checks | Not Implemented | No security scanning integrated into the build process. No code security analysis tools configured. |

### Input Validation Findings

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-IIS-CFG` | 16 — Input Validation | Not Implemented | `requestValidationMode="2.0"` downgrades request validation. `validateRequest="false"` disables page-level validation. Combined effect: ASP.NET built-in XSS/injection protections are disabled. Risk Severity: **Critical**. Source: `SplendidCRM/Web.config:L111, L115` |
| `SYS-API-REST` | 16 — Input Validation | Partial | REST endpoints process JSON payloads through WCF deserialization. Application-level input validation exists in some methods but is not uniformly applied across all endpoints. Source: `SplendidCRM/Rest.svc.cs` |
| `SYS-DB-ACCESS` | 16 — Input Validation | Implemented | `Sql.cs` uses parameterized queries for database operations, mitigating SQL injection at the data access layer. Source: `SplendidCRM/_code/Sql.cs` |

### Error Information Exposure Findings

| system_id | CIS Safeguard | Coverage | Evidence/Notes |
|---|---|---|---|
| `SYS-IIS-CFG` | 16 — Error Exposure | Not Implemented | `customErrors mode="Off"` exposes detailed ASP.NET error pages including stack traces, source code snippets, and framework version information to all clients. Risk Severity: **Critical**. Source: `SplendidCRM/Web.config:L51` |
| `SYS-ERROR-OBSERVABILITY` | 16 — Error Exposure | Partial | `SplendidError.cs` logs errors internally. However, error responses returned to API clients may contain detailed exception information due to disabled custom errors. Source: `SplendidCRM/_code/SplendidError.cs` |

### Coverage Assessment

**Overall CIS Control 16 Coverage: Not Implemented** — Zero automated testing infrastructure across all tiers (no unit tests, integration tests, E2E tests, CI/CD, or static analysis) represents the most significant application security gap. Combined with disabled request validation, exposed error details, no vulnerability management process, no SBOM, and critically outdated third-party libraries, the application software security posture fails to meet IG2/IG3 safeguard requirements. Per COSO Principle 10, the absence of application security control activities — particularly testing and vulnerability management — constitutes a material deficiency in the control environment.

---

## CIS Controls Coverage Matrix

The following matrix provides a consolidated view of CIS Controls coverage for all 34 system_ids from the [System Registry](./system-registry.md). Coverage symbols: ✅ Implemented, ⚠️ Partial, ❌ Not Implemented, — Not Applicable.

| system_id | CIS Control 1 | CIS Control 2 | CIS Control 3 | CIS Control 4 | CIS Control 5 | CIS Control 6 | CIS Control 8 | CIS Control 9 | CIS Control 16 |
|---|---|---|---|---|---|---|---|---|---|
| `SYS-SECURITY` | — | — | ⚠️ | — | ⚠️ | ✅ | ✅ | — | ⚠️ |
| `SYS-AUTH-AD` | — | — | — | — | ⚠️ | ⚠️ | — | — | — |
| `SYS-AUTH-DUO` | — | — | — | — | ⚠️ | ⚠️ | — | — | — |
| `SYS-API-REST` | — | — | — | — | — | ❌ | — | — | ⚠️ |
| `SYS-API-SOAP` | — | — | — | — | — | ❌ | — | — | ⚠️ |
| `SYS-API-ADMIN` | — | — | — | ⚠️ | — | ❌ | — | — | — |
| `SYS-CACHE` | — | — | — | ⚠️ | — | — | — | — | ⚠️ |
| `SYS-INIT` | — | — | — | ✅ | — | — | — | ✅ | — |
| `SYS-DB-ACCESS` | — | — | ⚠️ | — | — | — | — | — | ✅ |
| `SYS-ERROR-OBSERVABILITY` | — | — | — | — | — | — | ⚠️ | — | ⚠️ |
| `SYS-BUSINESS-LOGIC` | — | — | — | — | — | ✅ | — | — | ⚠️ |
| `SYS-IMPORT-EXPORT` | — | — | — | — | — | — | — | — | ⚠️ |
| `SYS-CAMPAIGN` | — | — | — | — | — | — | — | ❌ | ⚠️ |
| `SYS-EMAIL` | — | — | — | — | — | — | ❌ | ⚠️ | ⚠️ |
| `SYS-SMS-TELEPHONY` | — | — | — | — | — | — | ❌ | ⚠️ | ⚠️ |
| `SYS-REALTIME` | — | — | — | — | — | ✅ | — | — | ⚠️ |
| `SYS-SCHEDULER` | — | — | — | ⚠️ | — | — | ✅ | — | — |
| `SYS-WORKFLOW` | — | — | — | ⚠️ | — | — | — | — | — |
| `SYS-CONFIG` | — | — | — | ⚠️ | — | — | — | — | — |
| `SYS-ADMIN` | — | — | — | ⚠️ | ⚠️ | ✅ | ⚠️ | — | — |
| `SYS-REPORTING` | — | — | — | — | — | — | ⚠️ | — | — |
| `SYS-CONTENT` | — | — | — | — | — | — | — | — | ⚠️ |
| `SYS-L10N` | — | — | — | ✅ | — | — | — | — | — |
| `SYS-INTEGRATION-STUBS` | ❌ | ❌ | — | — | — | — | — | — | — |
| `SYS-SQL-DB` | ⚠️ | — | ⚠️ | ⚠️ | — | — | ✅ | — | — |
| `SYS-AUDIT` | — | — | — | — | — | — | ⚠️ | — | — |
| `SYS-ASPNET-APP` | ⚠️ | — | — | ❌ | — | — | — | — | ❌ |
| `SYS-IIS-CFG` | ⚠️ | — | — | ❌ | — | — | — | — | ❌ |
| `SYS-REACT-SPA` | ⚠️ | ⚠️ | ⚠️ | — | — | — | — | ⚠️ | ⚠️ |
| `SYS-ANGULAR-CLIENT` | ⚠️ | ⚠️ | — | — | — | — | — | — | — |
| `SYS-HTML5-CLIENT` | ❌ | ❌ | ⚠️ | — | — | — | — | ❌ | ❌ |
| `SYS-WEBFORMS` | ⚠️ | — | — | — | — | ✅ | — | — | ⚠️ |
| `SYS-BUILD-PIPELINE` | — | ⚠️ | — | ⚠️ | — | — | — | — | ❌ |
| `SYS-DEPENDENCY-MGMT` | — | ❌ | — | — | — | — | — | — | ❌ |

### Coverage Distribution Summary

| Coverage Level | Count | Percentage |
|---|---|---|
| ✅ Implemented | 16 | ~12% of assessed cells |
| ⚠️ Partial | 49 | ~37% of assessed cells |
| ❌ Not Implemented | 19 | ~14% of assessed cells |
| — Not Applicable | 48 | ~37% of cells (not assessed) |

---

## CIS Controls Gap Analysis

This gap analysis summarizes the most critical CIS Controls deficiencies observed across the SplendidCRM v15.2 codebase. Per COSO Principle 10 (Selects and Develops Control Activities), these gaps represent areas where the organization has not selected or developed adequate control activities to mitigate risks to acceptable levels.

### Critical Gaps

| CIS Control | Gap Description | Risk Severity | Affected system_ids | COSO Principle |
|---|---|---|---|---|
| **CIS Control 2** — Software Inventory | 38 manually managed .NET DLLs in `BackupBin2012/` with no NuGet package management, no SBOM, and no automated vulnerability scanning. jQuery 1.4.2 in HTML5 client is critically outdated. | **Critical** | `SYS-DEPENDENCY-MGMT`, `SYS-HTML5-CLIENT`, `SYS-INTEGRATION-STUBS` | COSO Principle 9 |
| **CIS Control 4** — Secure Configuration | Multiple `Web.config` security relaxations: `requestValidationMode="2.0"`, `enableEventValidation="false"`, `validateRequest="false"`, `customErrors mode="Off"`, and plaintext SA credentials in connection string. | **Critical** | `SYS-IIS-CFG`, `SYS-CONFIG`, `SYS-ASPNET-APP` | COSO Principle 12 |
| **CIS Control 8** — Audit Log Management | All audit data is database-internal with no external SIEM integration, no centralized log aggregation, no automated alerting on security events, and no formal log retention policy. | **Critical** | `SYS-AUDIT`, `SYS-ERROR-OBSERVABILITY`, `SYS-EMAIL`, `SYS-SMS-TELEPHONY` | COSO Principle 16 |
| **CIS Control 16** — Application Security | Zero automated testing infrastructure across all tiers (no unit tests, integration tests, E2E tests). No CI/CD pipeline. No static analysis. No vulnerability management process. | **Critical** | `SYS-BUILD-PIPELINE`, `SYS-ASPNET-APP`, `SYS-DEPENDENCY-MGMT`, `SYS-HTML5-CLIENT` | COSO Principle 10 |

### Moderate Gaps

| CIS Control | Gap Description | Risk Severity | Affected system_ids | COSO Principle |
|---|---|---|---|---|
| **CIS Control 1** — Enterprise Asset Inventory | No formal enterprise asset inventory. Four coexisting client interfaces lack unified asset tracking. Integration stubs and workflow engine remain as unused compiled assets. | **Moderate** | `SYS-HTML5-CLIENT`, `SYS-INTEGRATION-STUBS`, `SYS-WORKFLOW` | COSO Principle 1 |
| **CIS Control 5** — Account Management | MD5 password hashing (Critical weakness). No automated dormant account management. No service account inventory. Password complexity not enforced at application layer. | **Moderate** | `SYS-SECURITY`, `SYS-ADMIN` | COSO Principle 5 |
| **CIS Control 6** — MFA Enforcement | MFA (DuoUniversal) is optional, not enforced by default. No MFA on API access or administrative operations. Session-based access revocation has 20-minute persistence window. | **Moderate** | `SYS-AUTH-DUO`, `SYS-API-REST`, `SYS-API-SOAP`, `SYS-API-ADMIN` | COSO Principle 10 |
| **CIS Control 9** — Email/Web Protections | No DMARC/SPF/DKIM validation. No email anti-malware scanning. No DNS filtering. Campaign tracker endpoints publicly accessible without authentication. | **Moderate** | `SYS-EMAIL`, `SYS-CAMPAIGN`, `SYS-SMS-TELEPHONY` | COSO Principle 11 |

### Strengths Observed

Despite the significant gaps identified above, the following CIS Controls implementations demonstrate positive security engineering within the codebase. Per COSO Principle 10, these represent areas where effective control activities have been selected and developed:

| CIS Control | Strength | Evidence |
|---|---|---|
| **CIS Control 6** — Centralized Access Control | 4-tier authorization model with centralized enforcement via `Security.cs`. Consistent ACL application across REST, SOAP, WebForms, and SignalR endpoints. Role-based access with field-level granularity. | `SYS-SECURITY`, `SYS-BUSINESS-LOGIC`, `SYS-REALTIME`, `SYS-WEBFORMS` |
| **CIS Control 8** — Entity Audit Trails | Comprehensive SQL audit triggers for all CRM entities with field-level before/after value tracking. Login auditing in `USERS_LOGINS`. System error logging in `SYSTEM_LOG`. | `SYS-AUDIT`, `SYS-SECURITY`, `SYS-ERROR-OBSERVABILITY` |
| **CIS Control 9** — Transport Security | TLS 1.2 enforced at application startup. SameSite cookie hardening with browser-specific compatibility handling. MailKit TLS for email. | `SYS-INIT`, `SYS-EMAIL` |
| **CIS Control 16** — Parameterized Queries | `Sql.cs` consistently uses parameterized queries for database operations, effectively mitigating SQL injection at the data access layer. | `SYS-DB-ACCESS` |
| **CIS Control 16** — Vetted Libraries | Security-critical operations use established libraries: BouncyCastle for cryptography, Microsoft.Owin.Security for authentication, MailKit for email, Newtonsoft.Json for serialization. | `SYS-SECURITY`, `SYS-EMAIL` |

### Cross-Reference to Companion Mappings

The CIS Controls gaps identified in this document correspond to governance and technical control deficiencies documented in the companion framework mapping files:

- **COSO Mapping:** [./coso-mapping.md](./coso-mapping.md) — Maps all 17 COSO Principles to system_ids, identifying present/functioning and deficient principles per system.
- **NIST Mapping:** [./nist-mapping.md](./nist-mapping.md) — Maps NIST SP 800-53 Rev 5 control families (AC, AU, CM, IA, SC, SI) and NIST CSF functions (Identify, Protect, Detect, Respond, Recover) to system_ids.

Where CIS Controls and NIST SP 800-53 controls overlap (e.g., CIS Control 5/6 maps to NIST AC/IA; CIS Control 8 maps to NIST AU; CIS Control 4 maps to NIST CM), the more restrictive requirement is applied per the framework authority hierarchy. No conflicts between CIS and NIST were observed in this assessment — CIS Controls consistently prescribe equal or more specific implementation requirements than the corresponding NIST control families.
