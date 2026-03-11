# Directive 2 — Materiality Classification

**Classification of SplendidCRM Components per COSO Principle 14 — Communicates Internally**

---

#### Report Executive Summary

**Theme: "Pervasive Materiality Driven by Monolithic Architecture and Centralized Governance Dependencies"**

The materiality classification of SplendidCRM Community Edition v15.2 reveals a **Theme of Failure** in the Information and Communication dimension of internal controls, as assessed under COSO Principle 14 (Communicates Internally). The monolithic architecture creates a high concentration of Material components because virtually every functional domain depends on the same centralized infrastructure layer — `Security.cs` for authorization, `SplendidCache.cs` for metadata, `Sql.cs` for database access, and `SplendidError.cs` for observability. This tight coupling means that a deficiency in any of these core components propagates across all dependent systems, amplifying the operational reliability impact of each finding from Directives 1 and 3. The absence of documentation — a direct COSO Principle 14 gap — compounds this risk: developers cannot understand the WHY behind implementation decisions, making safe modification of Material components effectively impossible without reverse-engineering the codebase. Per COSO Principle 10 (Selects and Develops Control Activities), the Control Activities component depends entirely on Material components functioning correctly; their identification is therefore the gateway to all subsequent quality assessment.

Of the **227 components** examined across the SplendidCRM codebase — comprising 74 `_code/` utility classes, 18 `_code/` subdirectories, 49 Administration sub-modules and files, 41 CRM module folders, 4 API surfaces, 11 SQL schema directories, 5 client applications, 3 configuration files, 7 root application files, 14 supporting directories, and 1 dependency store — **144 components are classified as Material** based on their governance of access control, audit logging, configuration hardening, network segmentation, system/software integrity, secret management, or core business logic. The remaining **83 components are classified as Non-Material** — primarily UI presentation layers, static assets, localization data, enterprise integration stubs, and the experimental Angular client. Material components will proceed to the Directive 3 Code Quality Audit per COSO Principle 10 (Control Activities); Non-Material components are explicitly excluded from detailed code quality analysis per the audit mandate.

This classification directly feeds COSO Principle 10 via Directive 3: only Material components undergo the code quality audit that assesses whether Control Activities are properly designed and operating effectively. The classification criteria are aligned with COSO Principle 14 (Communicates Internally) — Material components are those whose failure would impair the organization's ability to communicate information necessary for internal control to function. All `system_id` references in this report correspond to the authoritative registry in the [System Registry](../directive-0-system-registry/system-registry.md).

---

#### Attention Required

| Component Path | Primary Finding | Risk Severity | Governing NIST/CIS Control | COSO Principle |
|---|---|---|---|---|
| `SplendidCRM/_code/Security.cs` | Material — centralized authentication/authorization consumed by all systems; MD5 hashing, 4-tier ACL | Critical | NIST AC, IA; CIS Control 5, 6 | COSO Principle 10 |
| `SplendidCRM/_code/SplendidCache.cs` | Material — centralized cache consumed by virtually all systems; single point of failure for metadata | Critical | NIST CM; CIS Control 4 | COSO Principle 13 |
| `SplendidCRM/Rest.svc.cs` | Material — primary API gateway for all SPA clients; input validation and error handling boundary | Critical | NIST AC, SI; CIS Control 16 | COSO Principle 11 |
| `SplendidCRM/Web.config` | Material — security-critical configuration with deliberately weakened request validation controls | Critical | NIST CM-6; CIS Control 4 | COSO Principle 11 |
| `SplendidCRM/_code/Sql.cs` | Material — provider-agnostic DB access layer; transaction safety for all data operations | Critical | NIST SI; CIS Control 16 | COSO Principle 10 |
| `SplendidCRM/_code/SplendidError.cs` | Material — sole centralized error logging consumed by all systems; observability bottleneck | Moderate | NIST AU; CIS Control 8 | COSO Principle 17 |
| `SplendidCRM/_code/SchedulerUtils.cs` | Material — timer-based job dispatch with reentrancy concerns; background processing reliability | Moderate | NIST SI; CIS Control 16 | COSO Principle 10 |
| `SQL Scripts Community/Triggers/` | Material — audit trigger generation for all CRM entities; COSO Principle 16 monitoring mechanism | Moderate | NIST AU; CIS Control 8 | COSO Principle 16 |
| `SplendidCRM/_code/Spring.Social.*` | Non-Material — 8 enterprise stub directories; non-functional in Community Edition | Minor | NIST CM-7; CIS Control 2 | COSO Principle 10 |
| `SplendidCRM/Angular/` | Non-Material — experimental client (constraint C-007); explicitly non-production | Minor | NIST CM-7 | COSO Principle 10 |

---

## Materiality Classification Criteria

The classification methodology is aligned with COSO Principle 14 (Communicates Internally), which requires the organization to internally communicate information necessary to support the functioning of internal control. Material classification identifies components whose failure, deficiency, or compromise would impair the organization's ability to maintain operational reliability and effective internal controls. The criteria are further informed by the COSO Information & Communication component, which establishes that organizations must obtain, generate, and use relevant, quality information to support the functioning of internal control.

### Material Classification

A component is classified as **Material** if it governs, implements, or directly supports one or more of the following operational reliability dimensions:

| # | Dimension | Description | Governing Framework |
|---|---|---|---|
| 1 | **Access Control** | Authentication, authorization, session management, ACL enforcement | NIST AC; CIS Control 5, 6 |
| 2 | **Audit Logging** | Event recording, audit trail generation, accountability tracking | NIST AU; CIS Control 8 |
| 3 | **Configuration Hardening** | Security configuration, environment settings, deployment parameters | NIST CM; CIS Control 4 |
| 4 | **Network Segmentation** | Boundary protection, protocol security, transport encryption | NIST SC |
| 5 | **System/Software Integrity** | Input validation, error handling, flaw remediation, data integrity | NIST SI; CIS Control 16 |
| 6 | **Secret Management** | Credential storage, encryption, key management | NIST IA, SC |
| 7 | **Core Business Logic** | CRUD operations, data processing, workflow execution supporting CRM objectives | COSO Principle 10 |

### Non-Material Classification

A component is classified as **Non-Material** if it:

- Serves only presentation or UI purposes without security implications
- Is explicitly non-functional (stubs, experimental, orphaned code)
- Provides only static assets, localization, or theming
- Has no direct impact on operational reliability if it were to fail or be removed

### Classification Governance

Per COSO Principle 14, this classification determines which components receive detailed code quality audit in Directive 3. Non-Material components are **explicitly excluded** from Directive 3 per the audit mandate. This scoping decision is itself a control activity per COSO Principle 10 — focusing audit resources on components with the highest potential impact on the integrity of information and communication within the organization.

---

## Core Infrastructure Layer — _code/ Utility Classes

The `SplendidCRM/_code/` directory contains **74 C# utility classes** at the top level, forming the core infrastructure layer of the application. Per COSO Principle 10 (Selects and Develops Control Activities), these classes implement the fundamental control activities upon which all other systems depend. Per COSO Principle 14 (Communicates Internally), their classification as Material or Non-Material determines whether their implementation quality will be formally assessed in Directive 3.

### Material _code/ Utility Classes

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `_code/Security.cs` | `SYS-SECURITY` | **Material** | Access Control, Secret Management | 4-tier authorization, MD5 hashing, Rijndael encryption, session management, ACL enforcement. Consumed by ALL systems. |
| `_code/SplendidCache.cs` | `SYS-CACHE` | **Material** | System Integrity, Core Business Logic | Centralized metadata cache; thousands of query getters; cache invalidation. 11,582 lines. Consumed by virtually ALL systems. |
| `_code/SplendidInit.cs` | `SYS-INIT` | **Material** | Configuration Hardening, System Integrity | Application/session/user initialization, SQL build orchestration, TLS enforcement. 2,443 lines. |
| `_code/SplendidError.cs` | `SYS-ERROR-OBSERVABILITY` | **Material** | Audit Logging, System Integrity | Centralized error logging to SYSTEM_LOG, dual persistence (memory + SQL). Consumed by all systems. |
| `_code/Sql.cs` | `SYS-DB-ACCESS` | **Material** | System Integrity, Core Business Logic | Provider-agnostic DB operations, parameterized queries, transaction management. 4,082 lines. |
| `_code/SqlBuild.cs` | `SYS-DB-ACCESS` | **Material** | System Integrity | Database schema build and migration. Critical for data integrity across all CRM entities. |
| `_code/SqlClientFactory.cs` | `SYS-DB-ACCESS` | **Material** | System Integrity | SQL client provider factory; database connection management for all data access operations. |
| `_code/SqlProcs.cs` | `SYS-DB-ACCESS` | **Material** | Core Business Logic | Generated stored procedure wrappers; 75,511 lines — the largest single file in the codebase. All CRUD operations route through this. |
| `_code/SqlProcsDynamicFactory.cs` | `SYS-DB-ACCESS` | **Material** | Core Business Logic | Dynamic stored procedure invocation for runtime-resolved procedure calls. |
| `_code/DbProviderFactory.cs` | `SYS-DB-ACCESS` | **Material** | System Integrity | Database provider abstraction layer supporting multi-provider database connectivity. |
| `_code/DbProviderFactories.cs` | `SYS-DB-ACCESS` | **Material** | System Integrity | Provider factory registration managing available database drivers. |
| `_code/DbAcrhiveFactories.cs` | `SYS-DB-ACCESS` | **Material** | System Integrity | Archive database factory (note: filename contains typo "Acrhive"). |
| `_code/SchedulerUtils.cs` | `SYS-SCHEDULER` | **Material** | System Integrity, Core Business Logic | Timer-based job dispatch, reentrancy guards, cron parsing. 2,722 lines. |
| `_code/EmailUtils.cs` | `SYS-EMAIL` | **Material** | Core Business Logic, System Integrity | Campaign email processing, inbound/outbound polling, SMTP delivery. 2,722 lines. |
| `_code/RestUtil.cs` | `SYS-API-REST` | **Material** | System Integrity, Core Business Logic | REST JSON serialization, timezone/epoch math. 4,503 lines. |
| `_code/SplendidDynamic.cs` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Metadata-driven layout rendering; dynamic UI generation from database-stored layouts. 7,458 lines. |
| `_code/SearchBuilder.cs` | `SYS-BUSINESS-LOGIC` | **Material** | System Integrity | Provider-aware WHERE clause generation; SQL injection prevention layer. |
| `_code/SplendidImport.cs` | `SYS-IMPORT-EXPORT` | **Material** | Core Business Logic, System Integrity | Multi-format import pipeline; data integrity during import operations. |
| `_code/SplendidExport.cs` | `SYS-IMPORT-EXPORT` | **Material** | Core Business Logic | Multi-format export pipeline (CSV, XML, OpenXML). |
| `_code/ImportUtils.cs` | `SYS-IMPORT-EXPORT` | **Material** | Core Business Logic | Import utility methods supporting the import pipeline. |
| `_code/ModuleUtils.cs` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Module-level utility operations. 1,062 lines. |
| `_code/ActiveDirectory.cs` | `SYS-AUTH-AD` | **Material** | Access Control, Secret Management | AD/SSO integration, NTLM authentication, ADFS/Azure AD JWT validation. |
| `_code/SplendidMailClient.cs` | `SYS-EMAIL` | **Material** | System Integrity | Multi-provider mail client abstraction (SMTP, Exchange, Office365, Gmail). |
| `_code/MimeUtils.cs` | `SYS-EMAIL` | **Material** | System Integrity | MIME processing via MailKit; email security boundary for content parsing. |
| `_code/ImapUtils.cs` | `SYS-EMAIL` | **Material** | System Integrity | IMAP protocol handling for inbound email polling. |
| `_code/PopUtils.cs` | `SYS-EMAIL` | **Material** | System Integrity | POP3 protocol handling for inbound email polling. |
| `_code/SplendidPage.cs` | `SYS-WEBFORMS` | **Material** | Access Control | Base page class enforcing authentication and authorization for all WebForms pages. |
| `_code/SplendidControl.cs` | `SYS-WEBFORMS` | **Material** | Access Control | Base control class with security context propagation to child controls. |
| `_code/ACLGrid.cs` | `SYS-SECURITY` | **Material** | Access Control | ACL grid rendering for role management; displays and manages authorization policies. |
| `_code/Crm.cs` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Core CRM helper methods. 1,307 lines. |
| `_code/Utils.cs` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | General utility methods used across the application. 1,921 lines. |
| `_code/CustomValidators.cs` | `SYS-BUSINESS-LOGIC` | **Material** | System Integrity | Input validation controls for form data. |
| `_code/RulesUtil.cs` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Business rules engine utilities. 1,416 lines. |
| `_code/SplendidGrid.cs` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Grid data display with ACL-filtered queries. 1,731 lines. |
| `_code/DynamicControl.cs` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Dynamic form control rendering from metadata. 1,838 lines. |
| `_code/OrderUtils.cs` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Order processing utilities for sales pipeline operations. |
| `_code/RdlUtil.cs` | `SYS-REPORTING` | **Material** | System Integrity | RDL/RDS report payload sanitization; SQL command injection points. |
| `_code/ArchiveExternalDB.cs` | `SYS-DB-ACCESS` | **Material** | Core Business Logic | External database archival operations. 2,151 lines. |
| `_code/SyncUtils.cs` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Synchronization utilities for data sync operations. |
| `_code/SyncError.cs` | `SYS-ERROR-OBSERVABILITY` | **Material** | Audit Logging | Sync-specific error handling and logging. |
| `_code/WorkflowInit.cs` | `SYS-WORKFLOW` | **Material** | System Integrity | Workflow engine initialization (stubbed but called at startup from Global.asax.cs). |
| `_code/WorkflowUtils.cs` | `SYS-WORKFLOW` | **Material** | System Integrity | Workflow utility methods (stubbed but compiled and referenced). |
| `_code/SplendidDefaults.cs` | `SYS-CONFIG` | **Material** | Configuration Hardening | Application default settings governing system behavior. |

**Material _code/ Utility Classes Count: 43**

### Non-Material _code/ Utility Classes

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `_code/L10n.cs` | `SYS-L10N` | **Non-Material** | — | Localization/internationalization. UI text translation only; no security or integrity impact. |
| `_code/Currency.cs` | `SYS-L10N` | **Non-Material** | — | Currency formatting. Presentation layer utility with no security implications. |
| `_code/TimeZone.cs` | `SYS-L10N` | **Non-Material** | — | Timezone handling. Presentation/formatting utility only. |
| `_code/ChartUtil.cs` | `SYS-REPORTING` | **Non-Material** | — | Chart rendering utility. Presentation-only component. |
| `_code/DashletControl.cs` | `SYS-WEBFORMS` | **Non-Material** | — | Dashboard widget base class. UI presentation only. |
| `_code/InlineEditControl.cs` | `SYS-WEBFORMS` | **Non-Material** | — | Inline editing UI control. Presentation layer. |
| `_code/InlineScript.cs` | `SYS-WEBFORMS` | **Non-Material** | — | Script injection helper. UI utility with no data impact. |
| `_code/KeySortDropDownList.cs` | `SYS-WEBFORMS` | **Non-Material** | — | Sorted dropdown UI control. Presentation component. |
| `_code/KBDocuments.cs` | `SYS-CONTENT` | **Non-Material** | — | Knowledge base document handling. Content utility with narrow scope. |
| `_code/MassUpdate.cs` | `SYS-BUSINESS-LOGIC` | **Non-Material** | — | Mass update UI control. Batch UI operation with narrow scope. |
| `_code/NewRecordControl.cs` | `SYS-WEBFORMS` | **Non-Material** | — | New record creation UI. Presentation layer component. |
| `_code/SearchControl.cs` | `SYS-WEBFORMS` | **Non-Material** | — | Search UI control. Presentation layer component. |
| `_code/SubPanelControl.cs` | `SYS-WEBFORMS` | **Non-Material** | — | Sub-panel UI rendering. Presentation layer component. |
| `_code/PortalCache.cs` | `SYS-CACHE` | **Non-Material** | — | Portal-specific cache extension. Supplementary cache with narrow scope. |
| `_code/SplendidMailExchangePassword.cs` | `SYS-EMAIL` | **Non-Material** | — | Exchange password utility. Narrow scope provider-specific helper. |
| `_code/SplendidMailGmail.cs` | `SYS-EMAIL` | **Non-Material** | — | Gmail-specific mail client. Narrow scope provider implementation. |
| `_code/SplendidMailOffice365.cs` | `SYS-EMAIL` | **Non-Material** | — | Office 365-specific mail client. Narrow scope provider implementation. |
| `_code/SplendidMailSmtp.cs` | `SYS-EMAIL` | **Non-Material** | — | SMTP-specific mail client. Narrow scope provider implementation. |
| `_code/VisualBasic.cs` | `SYS-BUSINESS-LOGIC` | **Non-Material** | — | VB.NET compatibility helpers. Narrow utility with no governance function. |
| `_code/XmlUtil.cs` | `SYS-BUSINESS-LOGIC` | **Non-Material** | — | XML utility methods. Narrow format utility. |
| `_code/CsvDataReader.cs` | `SYS-IMPORT-EXPORT` | **Non-Material** | — | CSV data reader. Narrow format reader consumed by the import pipeline. |
| `_code/ACTImport.cs` | `SYS-IMPORT-EXPORT` | **Non-Material** | — | ACT! import handler. Narrow import format adapter. |
| `_code/LanguagePackImport.cs` | `SYS-L10N` | **Non-Material** | — | Language pack import. Localization maintenance utility. |
| `_code/ExchangeSync.cs` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Exchange sync stub. Non-functional in Community Edition. |
| `_code/ExchangeUtils.cs` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Exchange utilities stub. Non-functional in Community Edition. |
| `_code/FacebookUtils.cs` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Facebook utilities stub. Non-functional in Community Edition. |
| `_code/GoogleApps.cs` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Google Apps stub. Non-functional in Community Edition. |
| `_code/GoogleUtils.cs` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Google utilities stub. Non-functional in Community Edition. |
| `_code/GoogleSync.cs` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Google sync stub. Non-functional in Community Edition. |
| `_code/SocialImport.cs` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Social media import stub. Non-functional in Community Edition. |
| `_code/iCloudSync.cs` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | iCloud sync stub. Non-functional in Community Edition. |

**Non-Material _code/ Utility Classes Count: 31**

**Total _code/ Utility Classes: 74 (43 Material, 31 Non-Material — 58% Material)**

---

## Core Infrastructure Subdirectories

The `SplendidCRM/_code/` directory contains **14 subdirectories** in addition to the 74 top-level classes. Per COSO Principle 10 (Selects and Develops Control Activities), subdirectories containing security-boundary implementations or workflow hooks are Material regardless of stub status, as they are compiled and invoked at runtime.

### Material _code/ Subdirectories

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `_code/SignalR/` | `SYS-REALTIME` | **Material** | Access Control, System Integrity | Contains 10 files: SignalRUtils.cs (OWIN startup), SplendidHubAuthorize.cs (session-based hub authorization implementing IAuthorizeHubConnection and IAuthorizeHubMethodInvocation), ChatManagerHub.cs, TwilioManagerHub.cs, ChatManager.cs, TwilioManager.cs, AsteriskManager.cs, AvayaManager.cs, PhoneBurnerManager.cs, TwitterManager.cs. The authorization class is a security boundary. |
| `_code/DuoUniversal/` | `SYS-AUTH-DUO` | **Material** | Access Control, Secret Management | 2FA client library code. Exists alongside `SplendidCRM/Administration/DuoUniversal/` (admin UI). Material due to 2FA security boundary. |
| `_code/Workflow/` | `SYS-WORKFLOW` | **Material** | System Integrity | Workflow engine hooks (stubbed but compiled; called from WorkflowInit.cs at startup). |
| `_code/Workflow4/` | `SYS-WORKFLOW` | **Material** | System Integrity | Updated workflow hooks (stubbed but compiled and referenced). |

### Non-Material _code/ Subdirectories

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `_code/Spring.Social.Facebook/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub; non-functional in Community Edition. |
| `_code/Spring.Social.HubSpot/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub; non-functional in Community Edition. |
| `_code/Spring.Social.LinkedIn/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub; non-functional in Community Edition. |
| `_code/Spring.Social.Office365/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub; non-functional in Community Edition. |
| `_code/Spring.Social.PhoneBurner/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub; non-functional in Community Edition. |
| `_code/Spring.Social.QuickBooks/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub; non-functional in Community Edition. |
| `_code/Spring.Social.Salesforce/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub; non-functional in Community Edition. |
| `_code/Spring.Social.Twitter/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub; non-functional in Community Edition. |
| `_code/Excel/` | `SYS-IMPORT-EXPORT` | **Non-Material** | — | Excel processing helpers. Narrow format utility. |
| `_code/OpenXML/` | `SYS-IMPORT-EXPORT` | **Non-Material** | — | OpenXML document generation. Narrow format utility. |
| `_code/FileBrowser/` | `SYS-CONTENT` | **Non-Material** | — | File browser UI support. Presentation layer. |
| `_code/PayPal/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | PayPal integration stub. Non-functional in Community Edition. |
| `_code/QuickBooks/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | QuickBooks integration stub. Non-functional in Community Edition. |
| `_code/mono/` | `SYS-ASPNET-APP` | **Non-Material** | — | Mono compatibility shims. Platform compatibility only; no governance function. |

**Total _code/ Subdirectories: 18 (4 Material, 14 Non-Material — 22% Material)**

---

## Administration Layer — Sub-Modules

The `SplendidCRM/Administration/` directory contains **45 sub-module directories** plus approximately 4 top-level files (Rest.svc.cs, Impersonation.svc.cs, default.aspx.cs, ListView.ascx.cs, SystemView.ascx.cs, Versions.xml). Per COSO Principle 10, administration sub-modules that govern access control, audit logging, configuration hardening, or core business logic are Material. Per COSO Principle 3 (Establishes Structure, Authority, and Responsibility), the admin layer defines the governance structure for the entire application. All administration components are associated with `SYS-ADMIN` unless a more specific `system_id` applies.

### Material Administration Sub-Modules

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `Administration/Rest.svc.cs` | `SYS-API-ADMIN` | **Material** | Access Control, Configuration Hardening, Core Business Logic | 318KB admin REST API aggregator enforcing IS_ADMIN. Critical security boundary for all admin operations. |
| `Administration/Impersonation.svc.cs` | `SYS-API-ADMIN` | **Material** | Access Control | User impersonation endpoint; privilege escalation risk vector requiring IS_ADMIN access. |
| `Administration/ACLRoles/` | `SYS-ADMIN` | **Material** | Access Control | ACL role management; defines authorization policies governing all user access. |
| `Administration/AuditEvents/` | `SYS-ADMIN` | **Material** | Audit Logging | Audit event viewing and management; visibility into entity change history. |
| `Administration/Backups/` | `SYS-ADMIN` | **Material** | System Integrity | Database backup management. Data recovery capability. |
| `Administration/Configurator/` | `SYS-ADMIN` | **Material** | Configuration Hardening | System-wide configuration management affecting all system behavior. |
| `Administration/DuoUniversal/` | `SYS-AUTH-DUO` | **Material** | Access Control, Secret Management | 2FA configuration UI with Rest.svc.cs for 2FA API. Security boundary component. |
| `Administration/DynamicLayout/` | `SYS-ADMIN` | **Material** | Core Business Logic | Metadata-driven layout management affecting all module rendering. |
| `Administration/EmailMan/` | `SYS-ADMIN` | **Material** | Core Business Logic | Email campaign manager administration for campaign execution. |
| `Administration/FullTextSearch/` | `SYS-ADMIN` | **Material** | Core Business Logic | Full-text search configuration affecting data retrieval capabilities. |
| `Administration/Modules/` | `SYS-ADMIN` | **Material** | Configuration Hardening | Module enable/disable management; controls application feature availability. |
| `Administration/PasswordManager/` | `SYS-ADMIN` | **Material** | Secret Management | Password policy administration governing credential strength requirements. |
| `Administration/Schedulers/` | `SYS-ADMIN` | **Material** | Core Business Logic | Scheduler configuration UI for background processing management. |
| `Administration/SystemLog/` | `SYS-ADMIN` | **Material** | Audit Logging | System log viewing UI; primary observability interface for administrators. |
| `Administration/UserLogins/` | `SYS-ADMIN` | **Material** | Audit Logging | User login history viewing; authentication event monitoring. |
| `Administration/Undelete/` | `SYS-ADMIN` | **Material** | Core Business Logic | Soft-delete recovery; data integrity recovery mechanism. |
| `Administration/Config/` | `SYS-ADMIN` | **Material** | Configuration Hardening | Configuration management for system settings. |
| `Administration/InboundEmail/` | `SYS-ADMIN` | **Material** | Core Business Logic | Inbound email account configuration for mail processing. |
| `Administration/BusinessRules/` | `SYS-ADMIN` | **Material** | Core Business Logic | Business rules configuration governing automated process logic. |
| `Administration/FieldValidators/` | `SYS-ADMIN` | **Material** | System Integrity | Field-level validation rules enforcing data quality. |
| `Administration/ModulesArchiveRules/` | `SYS-ADMIN` | **Material** | Core Business Logic | Archive rule management for data lifecycle governance. |
| `Administration/DynamicButtons/` | `SYS-ADMIN` | **Material** | Core Business Logic | Dynamic button configuration affecting user workflow actions. |
| `Administration/EditCustomFields/` | `SYS-ADMIN` | **Material** | Core Business Logic | Custom field management extending the data model. |
| `Administration/ModuleBuilder/` | `SYS-ADMIN` | **Material** | Core Business Logic | Module creation tooling for custom module development. |
| `Administration/Import/` | `SYS-ADMIN` | **Material** | Core Business Logic | Import configuration for data ingestion management. |
| `Administration/Export/` | `SYS-ADMIN` | **Material** | Core Business Logic | Export configuration for data extraction management. |
| `Administration/Languages/` | `SYS-ADMIN` | **Material** | Configuration Hardening | Language management affecting system localization behavior. |

### Non-Material Administration Sub-Modules

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `Administration/BusinessMode/` | `SYS-ADMIN` | **Non-Material** | — | Presentation. Business mode selection UI with no security impact. |
| `Administration/ConfigureTabs/` | `SYS-ADMIN` | **Non-Material** | — | Presentation. Tab visibility UI for interface customization. |
| `Administration/Currencies/` | `SYS-ADMIN` | **Non-Material** | — | Presentation. Currency management UI. Localization utility. |
| `Administration/CurrencyLayer/` | `SYS-ADMIN` | **Non-Material** | — | Integration. Currency exchange rate service with narrow scope. |
| `Administration/Dropdown/` | `SYS-ADMIN` | **Non-Material** | — | Presentation. Dropdown value editor for picklist management. |
| `Administration/Facebook/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub. Facebook admin configuration; non-functional. |
| `Administration/LinkedIn/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub. LinkedIn admin configuration; non-functional. |
| `Administration/NAICSCodes/` | `SYS-ADMIN` | **Non-Material** | — | Reference data. NAICS code management with no operational impact. |
| `Administration/NumberSequences/` | `SYS-ADMIN` | **Non-Material** | — | Presentation. Number sequence management for auto-numbering. |
| `Administration/OutboundSms/` | `SYS-ADMIN` | **Non-Material** | — | Narrow scope. SMS configuration with limited operational footprint. |
| `Administration/Releases/` | `SYS-ADMIN` | **Non-Material** | — | Presentation. Release tracking for version management UI. |
| `Administration/RenameTabs/` | `SYS-ADMIN` | **Non-Material** | — | Presentation. Tab renaming UI for interface customization. |
| `Administration/Salesforce/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub. Salesforce admin configuration; non-functional. |
| `Administration/Shortcuts/` | `SYS-ADMIN` | **Non-Material** | — | Presentation. Menu shortcut management for navigation. |
| `Administration/Tags/` | `SYS-ADMIN` | **Non-Material** | — | Presentation. Tag management for content categorization. |
| `Administration/Terminology/` | `SYS-ADMIN` | **Non-Material** | — | Localization. Terminology management for UI labels. |
| `Administration/Twilio/` | `SYS-SMS-TELEPHONY` | **Non-Material** | — | Narrow integration. Twilio account configuration UI. |
| `Administration/Twitter/` | `SYS-INTEGRATION-STUBS` | **Non-Material** | — | Enterprise stub. Twitter admin configuration; non-functional. |
| `Administration/Updater/` | `SYS-ADMIN` | **Non-Material** | — | Maintenance. Update checker with no security governance function. |
| `Administration/ZipCodes/` | `SYS-ADMIN` | **Non-Material** | — | Reference data. Zip code management with no operational impact. |

### Material Administration Top-Level Files

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `Administration/default.aspx(.cs)` | `SYS-ADMIN` | **Material** | Access Control | Admin dashboard entry point with IS_ADMIN enforcement. |
| `Administration/ListView.ascx(.cs)` | `SYS-ADMIN` | **Material** | Access Control | Admin module list view with role enforcement. |
| `Administration/SystemView.ascx(.cs)` | `SYS-ADMIN` | **Material** | Configuration Hardening | System configuration view displaying security-relevant settings. |

### Non-Material Administration Top-Level Files

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `Administration/Versions.xml` | `SYS-ADMIN` | **Non-Material** | — | Reference data. Version history metadata with no governance function. |

**Total Administration Components: 49 (30 Material, 19 Non-Material — 61% Material)**

---

## CRM Business Logic Modules

The `SplendidCRM/` root directory contains **41 CRM module folders**, each implementing a distinct business capability. Per COSO Principle 10 (Selects and Develops Control Activities) and COSO Principle 14 (Communicates Internally), all CRM modules are classified as **Material** under the "Core Business Logic" criteria because they each implement:

1. **CRUD operations** via stored procedure calls through `SqlProcs.cs`
2. **ACL enforcement** via `Security.Filter()` for list views and `Security.GetUserAccess()` for create/read/update/delete actions
3. **Metadata-driven layout rendering** via `SplendidDynamic.cs`
4. **Database interaction** through the Material `SYS-DB-ACCESS` layer

Each module follows the same WebForms pattern with DetailView, EditView, and ListView pages. The uniformity of the pattern does not diminish their materiality — each module processes distinct business data that directly supports CRM business objectives.

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `Accounts/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Core CRM entity; customer account management |
| `Activities/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Activity aggregation (calls, meetings, tasks) |
| `ActivityStream/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Activity feed across all CRM entities |
| `Audit/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic, Audit Logging | Audit logging views for entity change history |
| `Bugs/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Bug/defect tracking for support operations |
| `Calendar/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Calendar management for scheduling |
| `CallMarketing/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Call campaign management for outbound campaigns |
| `Calls/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Call tracking for communication records |
| `CampaignTrackers/` | `SYS-CAMPAIGN` | **Material** | Core Business Logic | Campaign tracking with anonymous endpoints |
| `Campaigns/` | `SYS-CAMPAIGN` | **Material** | Core Business Logic | Campaign management for marketing operations |
| `Cases/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Support case management for customer service |
| `ChatChannels/` | `SYS-REALTIME` | **Material** | Core Business Logic | Chat channel management for real-time communication |
| `ChatDashboard/` | `SYS-REALTIME` | **Material** | Core Business Logic | Chat monitoring dashboard for real-time oversight |
| `ChatMessages/` | `SYS-REALTIME` | **Material** | Core Business Logic | Chat message management for communication records |
| `Contacts/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Contact management (core CRM entity) |
| `Dashboard/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Dashboard management for configurable views |
| `Documents/` | `SYS-CONTENT` | **Material** | Core Business Logic | Document management with version tracking |
| `EmailClient/` | `SYS-EMAIL` | **Material** | Core Business Logic | Web email client for in-application messaging |
| `EmailMarketing/` | `SYS-CAMPAIGN` | **Material** | Core Business Logic | Email marketing campaigns for campaign execution |
| `EmailTemplates/` | `SYS-EMAIL` | **Material** | Core Business Logic | Email template management for message composition |
| `Emails/` | `SYS-EMAIL` | **Material** | Core Business Logic | Email message management and records |
| `Employees/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Employee directory management |
| `Feeds/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | RSS/Atom feed management for content aggregation |
| `Home/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Home dashboard (application entry point) |
| `Import/` | `SYS-IMPORT-EXPORT` | **Material** | Core Business Logic | Data import UI for data ingestion |
| `Leads/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Lead management (core CRM entity) |
| `Meetings/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Meeting scheduling and management |
| `Notes/` | `SYS-CONTENT` | **Material** | Core Business Logic | Note/attachment management linked to entities |
| `Opportunities/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Opportunity/deal management (core CRM entity) |
| `Orders/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Order management for sales pipeline |
| `Parents/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Parent record relationship management |
| `ProjectTasks/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Project task management for project operations |
| `Projects/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Project management for project operations |
| `ProspectLists/` | `SYS-CAMPAIGN` | **Material** | Core Business Logic | Prospect list management for campaign targeting |
| `Prospects/` | `SYS-CAMPAIGN` | **Material** | Core Business Logic | Prospect management for marketing pipeline |
| `Reports/` | `SYS-REPORTING` | **Material** | Core Business Logic | Report management and generation |
| `RulesWizard/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Business rules wizard for automated logic |
| `SmsMessages/` | `SYS-SMS-TELEPHONY` | **Material** | Core Business Logic | SMS message management for communication |
| `Tasks/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Task management for activity tracking |
| `TwitterMessages/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic | Twitter message management for social communication |
| `Users/` | `SYS-BUSINESS-LOGIC` | **Material** | Core Business Logic, Access Control | User management; account creation, role assignment, access governance |

**Total CRM Module Folders: 41 (41 Material, 0 Non-Material — 100% Material)**

---

## API Surface

The API surface layer forms the primary external boundary of the SplendidCRM application. Per COSO Principle 11 (Selects and Develops General Controls over Technology), all API surfaces are classified as **Material** because they govern access control, input validation, and data exchange between clients and the backend — the critical boundary where NIST AC and SI controls are enforced.

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `SplendidCRM/Rest.svc.cs` | `SYS-API-REST` | **Material** | Access Control, System Integrity, Core Business Logic | Primary API gateway; 393,134 bytes; authentication, metadata, CRUD, relationships, sync, exports, OData queries. Security boundary for all SPA clients. |
| `SplendidCRM/soap.asmx.cs` | `SYS-API-SOAP` | **Material** | Access Control, Core Business Logic | SugarCRM-compatible SOAP API; 193,656 bytes; session auth, entity CRUD. Legacy protocol security boundary. |
| `SplendidCRM/Administration/Rest.svc.cs` | `SYS-API-ADMIN` | **Material** | Access Control, Configuration Hardening, Core Business Logic | Admin REST API; 318,344 bytes; IS_ADMIN enforcement; layout/config/module/ACL management. |
| `SplendidCRM/Administration/Impersonation.svc.cs` | `SYS-API-ADMIN` | **Material** | Access Control | User impersonation service; privilege escalation vector requiring administrative access. |

**Total API Surfaces: 4 (4 Material, 0 Non-Material — 100% Material)**

---

## SQL Database Layer

The `SQL Scripts Community/` directory contains the complete SQL Server schema, views, stored procedures, triggers, functions, and seed data for the SplendidCRM database. Per COSO Principle 16 (Conducts Ongoing and/or Separate Evaluations), the audit trigger infrastructure is particularly significant as it implements the entity-level change tracking that supports monitoring activities. Per COSO Principle 10, the stored procedures and views implement the data integrity controls that underpin all CRUD operations.

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `SQL Scripts Community/BaseTables/` | `SYS-SQL-DB` | **Material** | Core Business Logic, System Integrity | Core entity CREATE TABLE scripts defining the data model. 229 base tables. |
| `SQL Scripts Community/Tables/` | `SYS-SQL-DB` | **Material** | Core Business Logic | Schema upgrade scripts; data model evolution and migration. |
| `SQL Scripts Community/Views/` | `SYS-SQL-DB` | **Material** | Core Business Logic | 581 views providing read-only data access for all modules in multiple formats (list, detail, edit, sync, SOAP, relationship, metadata). |
| `SQL Scripts Community/Procedures/` | `SYS-SQL-DB` | **Material** | Core Business Logic, System Integrity | 833 stored procedures; all CRUD and relationship operations. Data integrity enforcement through parameterized operations. |
| `SQL Scripts Community/ProceduresDDL/` | `SYS-SQL-DB` | **Material** | System Integrity | 80 schema reflection utilities for runtime schema introspection. |
| `SQL Scripts Community/ViewsDDL/` | `SYS-SQL-DB` | **Material** | System Integrity | 26 meta-database helpers for schema management. |
| `SQL Scripts Community/Functions/` | `SYS-SQL-DB` | **Material** | Core Business Logic | 78 scalar and table-valued functions supporting data operations. |
| `SQL Scripts Community/Triggers/` | `SYS-AUDIT` | **Material** | Audit Logging | 11 trigger scripts including BuildAllAuditTables.1.sql generating audit triggers for ALL CRM entities. Critical for COSO Principle 16 monitoring. |
| `SQL Scripts Community/Data/` | `SYS-SQL-DB` | **Material** | Configuration Hardening | 135 configuration and metadata seed data scripts defining system defaults. |
| `SQL Scripts Community/Build.bat` | `SYS-BUILD-PIPELINE` | **Material** | System Integrity | Database build orchestrator; concatenates all scripts into Build.sql for deployment. |
| `SQL Scripts Community/Terminology/` | `SYS-L10N` | **Non-Material** | — | Localization. 112 en-US terminology seed scripts; no security or integrity impact. |

**Total SQL Schema Directories: 11 (10 Material, 1 Non-Material — 91% Material)**

---

## Configuration Layer

Per COSO Principle 12 (Deploys Through Policies and Procedures), configuration artifacts define the policies through which the application is deployed and operated. The `Web.config` file is particularly Material as it contains security-critical settings that have been deliberately weakened from their default secure values.

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `SplendidCRM/Web.config` | `SYS-IIS-CFG`, `SYS-CONFIG` | **Material** | Configuration Hardening, Access Control, Secret Management | Security-critical settings: `requestValidationMode="2.0"`, `customErrors="Off"`, session state InProc, authentication mode, assembly binding redirects. **Critical** component per NIST CM-6. |
| `SplendidCRM/SplendidCRM7_VS2017.csproj` | `SYS-BUILD-PIPELINE`, `SYS-DEPENDENCY-MGMT` | **Material** | System Integrity | MSBuild project file with 37 DLL HintPath references; defines the complete backend dependency graph. |
| `SplendidCRM/AssemblyInfo.cs` | `SYS-ASPNET-APP` | **Non-Material** | — | Assembly metadata. Version information only; no governance function. |

**Total Configuration Files: 3 (2 Material, 1 Non-Material — 67% Material)**

---

## Client Applications

Per COSO Principle 11 (Selects and Develops General Controls over Technology), client applications that implement security context management, process sensitive data, or contain known vulnerable dependencies are Material. The Angular client is explicitly Non-Material due to its experimental status (constraint C-007).

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `SplendidCRM/React/` | `SYS-REACT-SPA` | **Material** | System Integrity, Core Business Logic | Primary modern client (React 18.2.0, TypeScript 5.3.3, MobX 6.12.0). Client-side security context, state management, API interaction, SignalR client (v8.0.0 — version asymmetry with server v1.2.2). |
| `SplendidCRM/React/package.json` | `SYS-REACT-SPA`, `SYS-DEPENDENCY-MGMT` | **Material** | System Integrity | npm dependency manifest; defines client dependency surface with 80+ direct dependencies. |
| `SplendidCRM/html5/` | `SYS-HTML5-CLIENT` | **Material** | System Integrity | Legacy jQuery/RequireJS client with known vulnerable jQuery versions (1.4.2–2.2.4). Security posture assessment needed per NIST SI. |
| `SplendidCRM/Angular/` | `SYS-ANGULAR-CLIENT` | **Non-Material** | — | Explicitly experimental per constraint C-007. Non-production client with no active deployment. |
| `SplendidCRM/Angular/package.json` | `SYS-ANGULAR-CLIENT` | **Non-Material** | — | Experimental client; dependency list for non-production code. |

**Total Client Applications: 5 (3 Material, 2 Non-Material — 60% Material)**

---

## Application Root Files

Root-level `.cs` files in `SplendidCRM/` serve as application entry points, external-facing endpoints, and lifecycle managers. Per COSO Principle 10, files implementing bootstrap sequences, external-facing webhooks, or anonymous-access endpoints are Material.

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `Global.asax.cs` | `SYS-INIT`, `SYS-ASPNET-APP` | **Material** | Configuration Hardening, System Integrity | Application lifecycle, TLS enforcement, timer initialization, SameSite cookie hardening. Bootstrap sequence critical for COSO Principle 10 Control Activities. |
| `SystemCheck.aspx.cs` | `SYS-ASPNET-APP` | **Material** | System Integrity | System diagnostics endpoint; exposes system health information. |
| `TwiML.aspx.cs` | `SYS-SMS-TELEPHONY` | **Material** | System Integrity | Twilio TwiML webhook processor; external-facing endpoint receiving inbound messages. |
| `campaign_trackerv2.aspx.cs` | `SYS-CAMPAIGN` | **Material** | Core Business Logic | Campaign tracking endpoint; anonymous access for email open/click tracking. |
| `default.aspx.cs` | `SYS-WEBFORMS` | **Non-Material** | — | Presentation. Default page redirect with no business logic. |
| `RemoveMe.aspx.cs` | `SYS-ASPNET-APP` | **Non-Material** | — | Orphaned/placeholder file with no operational function. |
| `image.aspx.cs` | `SYS-BUSINESS-LOGIC` | **Non-Material** | — | Content serving. Image delivery endpoint with narrow scope. |

**Total Root Application Files: 7 (4 Material, 3 Non-Material — 57% Material)**

---

## Supporting Directories and Assets

Supporting directories contain shared UI controls, themes, static assets, and third-party components. Per COSO Principle 14, most supporting directories serve presentation purposes with no governance function. Exceptions are `_controls/` (shared form controls consumed by all modules) and `BackupBin2012/` (the critical dependency store).

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `SplendidCRM/_controls/` | `SYS-WEBFORMS` | **Material** | Core Business Logic | Shared WebForms user controls (CRON, DatePicker, SearchText, etc.) consumed across all CRM modules. |
| `BackupBin2012/` | `SYS-DEPENDENCY-MGMT` | **Material** | System Integrity | 24+ manually managed .NET DLLs; critical dependency store with no version management per NIST CM-3. |
| `SplendidCRM/App_MasterPages/` | `SYS-WEBFORMS` | **Non-Material** | — | Presentation. Master page templates (layout/chrome). |
| `SplendidCRM/App_Themes/` | `SYS-WEBFORMS` | **Non-Material** | — | Presentation. 7 CSS themes (Arctic, Atlantic, Mobile, Pacific, Seven, Six, Sugar). |
| `SplendidCRM/App_Browsers/` | `SYS-WEBFORMS` | **Non-Material** | — | Presentation. Browser capability definitions. |
| `SplendidCRM/App_Data/` | `SYS-ASPNET-APP` | **Non-Material** | — | Application data placeholder directory. |
| `SplendidCRM/ClientBin/` | `SYS-ASPNET-APP` | **Non-Material** | — | Client binaries (Silverlight remnant); obsolete technology. |
| `SplendidCRM/Help/` | `SYS-CONTENT` | **Non-Material** | — | Help pages. Static content with no operational impact. |
| `SplendidCRM/Images/` | `SYS-ASPNET-APP` | **Non-Material** | — | Static image assets. |
| `SplendidCRM/Include/` | `SYS-WEBFORMS` | **Non-Material** | — | Include files. JavaScript/CSS includes for page rendering. |
| `SplendidCRM/Properties/` | `SYS-ASPNET-APP` | **Non-Material** | — | Project properties. Build metadata only. |
| `SplendidCRM/ckeditor/` | `SYS-CONTENT` | **Non-Material** | — | CKEditor rich text editor. Third-party component with no governance function. |
| `SplendidCRM/iFrames/` | `SYS-WEBFORMS` | **Non-Material** | — | iFrame containers. Presentation layer component. |
| `SplendidCRM/_devtools/` | `SYS-BUILD-PIPELINE` | **Non-Material** | — | Development tools. Build-time only with no runtime impact. |

**Total Supporting Directories: 14 (2 Material, 12 Non-Material — 14% Material)**

---

## Dependency Management

Per COSO Principle 9 (Identifies and Analyzes Significant Change), the dependency management infrastructure — or its absence — is Material because changes to dependencies directly affect the security and integrity posture of the entire platform. The manual DLL management in `BackupBin2012/` without any package management system represents a Critical gap in NIST CM-3 (Configuration Change Control) and CIS Control 2 (Inventory and Control of Software Assets).

| Component Path | system_id | Classification | Criteria Match | Justification |
|---|---|---|---|---|
| `BackupBin2012/` | `SYS-DEPENDENCY-MGMT` | **Material** | System Integrity | 24+ .NET DLLs including security-critical libraries: BouncyCastle.Crypto.dll (cryptography), MailKit.dll (email security boundary), Microsoft.AspNet.SignalR.Core.dll (real-time security), Microsoft.Owin.Security.dll (authentication pipeline), Newtonsoft.Json.dll (API data handling), Twilio.Api.dll (external communication). No NuGet, no SBOM, no version management. |

**Total Dependency Components: 1 (1 Material, 0 Non-Material — 100% Material)**

---

## Materiality Classification Summary

The following table aggregates materiality classification counts across all component categories. Per COSO Principle 14 (Communicates Internally), this summary provides the information necessary for subsequent directives to scope their analysis appropriately.

| Category | Total Components | Material | Non-Material | Material % |
|---|---|---|---|---|
| _code/ Utility Classes | 74 | 43 | 31 | 58% |
| _code/ Subdirectories | 18 | 4 | 14 | 22% |
| Administration Sub-Modules and Files | 49 | 30 | 19 | 61% |
| CRM Module Folders | 41 | 41 | 0 | 100% |
| API Surfaces | 4 | 4 | 0 | 100% |
| SQL Schema Directories | 11 | 10 | 1 | 91% |
| Client Applications | 5 | 3 | 2 | 60% |
| Configuration Files | 3 | 2 | 1 | 67% |
| Root Application Files | 7 | 4 | 3 | 57% |
| Supporting Directories | 14 | 2 | 12 | 14% |
| Dependency Management | 1 | 1 | 0 | 100% |
| **TOTAL** | **227** | **144** | **83** | **63%** |

### Classification Analysis

The high Material classification rate (63%) reflects the monolithic architecture's tight coupling — per COSO Principle 13 (Uses Relevant Information), a high proportion of components participate in governance-relevant data processing because they all share the same centralized infrastructure layer.

**Key observations per COSO Principle 14 (Communicates Internally):**

- **100% Materiality in CRM Modules, API Surfaces, and Dependencies:** All 41 CRM module folders, all 4 API surfaces, and the dependency store are Material because they directly implement core business logic, enforce access control boundaries, or manage critical software assets. This reflects the reality that in a monolithic CRM application, every business module participates in data integrity and access control through shared infrastructure.

- **91% Materiality in SQL Schema:** 10 of 11 SQL schema directories are Material. Only the `Terminology/` directory (localization seed data) is Non-Material. The stored procedures (833), views (581), and triggers implement the data integrity and audit controls assessed under COSO Principle 16 (Conducts Ongoing and/or Separate Evaluations).

- **Primary Non-Material Blocks:** The 8 Spring.Social.* enterprise integration stub subdirectories (all `SYS-INTEGRATION-STUBS`), the experimental Angular client (`SYS-ANGULAR-CLIENT`), and the 12 presentation-only supporting directories account for the majority of Non-Material classifications. These components have no runtime governance function in the Community Edition.

- **Material components proceed to Directive 3 Code Quality Audit** — the approximately 144 Material components will receive detailed code smell, complexity metric, and security quality analysis per COSO Principle 10 (Selects and Develops Control Activities).

- **Non-Material components are explicitly EXCLUDED from Directive 3** per the audit mandate. Their classification as Non-Material means their implementation quality does not have a direct bearing on the effectiveness of internal controls over operational reliability.

---

## Cross-References

This Materiality Classification report builds upon and is consumed by the following audit documentation:

### Predecessor Documents (Directive 0)

- [System Registry](../directive-0-system-registry/system-registry.md) — Authoritative source for all `system_id` references and Static/Dynamic classifications used in this report
- [COSO Mapping](../directive-0-system-registry/coso-mapping.md) — COSO Principles 1–17 mapping per system_id; governance context for materiality criteria
- [NIST Mapping](../directive-0-system-registry/nist-mapping.md) — NIST SP 800-53 Rev 5 and NIST CSF control mapping; technical control references for classification criteria
- [CIS Mapping](../directive-0-system-registry/cis-mapping.md) — CIS Controls v8 IG2/IG3 safeguard mapping; implementation-level benchmarks for classification criteria

### Predecessor Report (Directive 1)

- [Structural Integrity Report](../directive-1-structural-integrity/structural-integrity-report.md) — Structural findings that inform the materiality assessment of components with broken cross-references or orphaned configurations

### Successor Report (Directive 3)

- [Code Quality Summary](../directive-3-code-quality/code-quality-summary.md) — Consumes this materiality classification to scope the Directive 3 Code Quality Audit to Material components only
