# Channels, integrations and security: build plan to close the gaps with CIP

Written 2026-10-12. Security workstreams (S1 to S6) added 2026-10-14 after reading CIP's security code. Scope: everything CIP (the banking product) connects to or receives from, that IIP
does not yet, with the weight on channels and on data access through Drive, OneDrive and SharePoint.
Customer acquisition has its own analysis (`IIP_ACQUISITION_GAP_ANALYSIS_VS_CIP.md`); this plan points to it
where the two meet instead of repeating it.

The method is reuse. CIP and IIP belong to the same organisation and the IIP portal already grew out of CIP's
(the `cip-acq` styles and `components/cip` are in both). The plan therefore says, for each gap, which CIP files
to copy, where they land in IIP, and which seams must be rewired. It does not propose rewriting anything CIP
already does.

## How this was done, and what was not checked

CIP was read from the local copy of `github.com/Otic-Group/CIP_Web_App` (branch `main`, last commit 2026-10-06,
"widget changes and bugs fixed"). IIP was read from this repository and from the live settings of the dev
environment on 2026-10-12. Not checked: whether CIP's GitHub is ahead of that copy; whether CIP's connectors
work in a deployed CIP; how CIP's catalogue entries for `crb`, `mpesa` and `salesforce` behave (they are listed
as ids, their depth is not confirmed). Treat each "copy" below as "copy, then run CIP's own tests in IIP".

## The rules that do not bend when code is copied

1. Human approval boundary and grounding rules apply to everything a connector brings in. A file synced from a
   bank's SharePoint is **not** approved knowledge. It lands as pending, with sensitivity unknown, and stays out
   of retrieval until a person classifies and approves it (the Data Hub already enforces this).
2. Tenant isolation: CIP's `ITenantContext` (one tenant id) becomes IIP's `ITenantContextAccessor` with a
   `TenantScope` (tenant and workspace). Every copied query, cursor and subscription is scoped by it.
3. Secrets: OAuth refresh tokens and client secrets are encrypted with IIP's `IConnectionSecretProtector`
   (CIP's `IUserOAuthConnectionStore` is replaced, not copied). Secrets live in Key Vault, are write-only from the
   browser, and never appear in chat, documents or the repository.
4. Honest state: a connector that is not configured shows "Not connected" or "Not configured" with the reason.
   Never a green card for a service that has not been tested end to end.
5. Audit: connect, disconnect, sync, export and every send are written to the audit trail, without file contents.
6. Architecture: CIP is controllers over one project. IIP is Domain (interfaces), Application (use cases),
   Infrastructure (adapters), Api (minimal endpoints). Copied services are split along that line; CIP controllers
   become minimal-API endpoint classes beside `FileConnectionEndpoints`.
7. No real client names in fixtures or screenshots. All examples synthetic.

## The common seams (build these once, every workstream uses them)

| Seam | CIP | IIP today | Plan |
|---|---|---|---|
| Per-user tokens | `UserTokenProvider` (360 lines), `UserOAuthConnectionStore` (243), `UserConnectionService` (712), `MicrosoftTenantTokenProvider` (113) | `UserFileService` with `IConnectionSecretProtector`; connect and browse only | Extend IIP's service with token refresh and a `GetAccessTokenAsync(scope, user, provider)` the copied services call. Port CIP's refresh and revocation tests. |
| Work sink | `IngestionWorkQueue` (268) into CIP ingestion | Knowledge upload and classify endpoints; Service Bus queue live | Copied connectors call an `IKnowledgeIngestionSink` in IIP that creates a pending Knowledge document through the existing upload path. |
| Connector data | `DataConnector`, `ConnectorRun`, `ConnectorItem`, `ConnectorError`, `DataConnectorAccessGroup`, `ConnectorSecurityEvidence`, `ConnectorSyncCursor`, `ConnectorChangeSubscription` (in `Data/Entities.cs`) | `UserFileConnection` only | Add the entities to IIP Infrastructure with a new migration on the SaaS branch, tenant and workspace columns added. |
| Scheduling | Hosted services (hourly subscription renewal) | Hosted services exist (assistant worker, queue) | Copy the pattern; renew subscriptions hourly. |
| Catalogue and setup UI | `V2IntegrationsView`, `StorageIntegrationSetupModal`, `DestinationPickerList`, `IntegrationSetupShared`, `IntegrationStepCarousel`, `SuiteMapPickers`, `IntegrationBrandIcon`, `lib/integration-*`, `lib/google-picker.ts`, brand icons in `public/logos/integrations` | Settings, Integrations modal with provider cards | Copy the components into `web/portal/components/settings/integrations/`, restyle to the IIP tokens, point at IIP endpoints. |

Branch note: the SaaS branch (`saas/merge-v2-2026-10-08`, live) and `build/claims-core` (the temporary copy) have
separate migration lineages. This plan targets the SaaS branch. Anything claims-core also needs is merged
afterwards, migration by migration.

## Workstreams, in build order

Each has: the gap, the CIP evidence (all paths under `CIP_Web_App/apps/`), IIP today, what to copy, what to adapt,
the gate. "Gate" is what must be true and shown before the next workstream starts.

### W0. Registrations and settings (nothing else works without these)

- Gap: no Google OAuth client, no Picker key, no "IIP Files" Microsoft app, so every file card says Not connected.
- IIP today: code built and tested; zero `Files__*` settings on the live API (checked 2026-10-12); Key Vault holds
  only a translation key. Guide: `docs/05-deployment-release-enablement/IIP_FILE_CONNECTIONS_SETUP.md`.
- Reuse first: CIP already has a Google Cloud project and Microsoft registrations. Add IIP's redirect
  `https://app.insuranceintelligenceplatform.com/app/connect/callback` to them where that is acceptable, or
  create IIP's own. Decision for the owner (see Open decisions).
- Build: settings wiring, Key Vault references, restart. No code.
- Gate: the four checks at the end of the setup guide pass with a test account on both providers.

### W1. Make the existing list-import pick work end to end

- Gap: the pick-a-file import has never run against real Google or Microsoft accounts.
- Build: run the setup guide's checks; fix whatever the first real run shows; add Playwright coverage with the
  provider stubbed at the HTTP boundary.
- Gate: Acquisition, Import a list, Google Drive and OneDrive, loads columns from a real file.

### W2. Folder connectors that feed the Data Hub (the largest gap)

- Gap: IIP has no continuous access to a bank's or insurer's document libraries. Policy wordings, product
  sheets and procedures cannot flow into the Data Hub; they are uploaded by hand.
- CIP evidence:
  - Google: `Services/BankData/GoogleDriveConnectorService.cs` (1064), `Services/GoogleDriveFileDiscoveryService.cs`
    (213), `Services/Ingestion/GoogleChangesService.cs` (307), `Controllers/GoogleDriveNotificationsController.cs` (56).
  - Microsoft (OneDrive and SharePoint): `Services/BankData/BankDocumentConnectorService.cs` (605),
    `Services/GraphFileDiscoveryService.cs` (348), `Services/Ingestion/GraphDeltaService.cs` (383).
  - Shared: `Services/Ingestion/ChangeSubscriptionManager.cs` (327; Graph subscriptions on
    `/drives/{id}/root` and Google `changes.watch`, shared secret generated here with only its hash stored),
    `Services/ConnectorService.cs` (748), `Services/ConnectorSecurityService.cs` (186),
    `Services/BankData/BankDataConnectorPurgeService.cs`, `Services/BankData/BankDataProvenance.cs`.
  - Behaviour worth keeping, taken from their own comments: reads use the owner's current sign-in so access never
    exceeds what that person can open; an expired sign-in fails the run instead of looking like every file was
    deleted; files from a removed or re-scoped folder leave search at once; change notifications only queue a
    pull, and the pull decides what changed; Shared Drives are read with the sign-in of whoever saved them.
  - CIP tests to copy: the connector, drive, graph and change-source tests in `credit-api/tests` (about fourteen
    related files).
- IIP today: not present. `Sites.Selected` appears in `UserFileService` only as an optional scope.
- Adapt: replace the token layer and the work queue with the common seams above; drop CIP's bank-specific
  projection (`IStructuredBankSqlProjector`); map `ConnectorItem` to a pending Knowledge document; add a
  per-folder choice of sensitivity default (never "approved"); add an Admin screen listing connectors, folders,
  last run, errors and files pending approval.
- Gate: connect a test SharePoint library and a Google Shared Drive; files appear as pending Knowledge documents;
  approving one makes the Data Hub answer from it with a citation; removing the folder removes its files from
  retrieval immediately; an expired sign-in produces a failed run, not a purge. Tenant A cannot see tenant B's
  connector in a test.

### W3. Export to Drive, OneDrive and SharePoint

- Gap: IIP cannot save an evidence pack, a report or an audit export to the customer's own cloud storage.
- CIP evidence: `Services/Export/CloudExportService.cs` (347; Graph simple upload up to 4 MB then upload
  sessions in 320 KiB chunks; OneDrive limited to the app folder), `Controllers/CloudExportController.cs` (354),
  `Services/Settings/IntegrationDestinationPickerService.cs`, web `DestinationPickerList.tsx` and
  `lib/integration-destination-pickers.ts`.
- IIP today: the setup guide records "saving exports back is the next step; not built".
- Adapt: wire the export source to IIP's evidence-pack, claim and audit exports; require a named person to send
  an export (human approval for anything that leaves the platform); record destination and file name, never
  contents, in the audit trail.
- Gate: export one claim evidence pack to a test OneDrive and a test Shared Drive; the audit entry shows who,
  what and where.

### W4. Email: Outlook and Gmail as organisation connections, and email into the lead path

- Gap: IIP's live email, outbound communications and notifications are Disabled. CIP lets an administrator switch
  on Outlook or Gmail once for the whole organisation, then sends memo updates and reports from CIP's own address.
- CIP evidence: `Services/Settings/IntegrationSuiteConnectionService.cs` (525; admin consent and Google OAuth
  flags, per-card status), `Services/GmailIntegrationService.cs`, `Services/GraphMailSender.cs`,
  `Services/Email/PlatformEmailSender.cs`, `Services/Reports/OutlookDistributionService.cs` and
  `OutlookEmailRedactionService.cs`, `Services/Email/InboxEmailRelay.cs`; tests `GraphMailSenderTests`,
  `GraphMailSenderTenantSplitTests`, `OutlookConnectTests`.
- IIP today: `GraphMailProvider`, `GraphEmailAttachmentProcessor`, `SmtpEmailProvider` and the email-to-claim
  matcher are built. Live `emailProvider` and `communicationProvider` are Disabled. Email never feeds a lead
  (gap K2 in the acquisition analysis).
- Build in two steps. First, configure and verify what exists (Graph app permissions, a sending mailbox, admin
  consent screen) and add the organisation switch UI by copying `IntegrationSuiteConnectionService` and its
  cards. Second, add the lead path by copying `InboxEmailRelay` so an email from a stranger becomes a profiled
  lead, with the redaction service applied before any report is mailed.
- Gate: an administrator connects Outlook once; a claim update is sent to a test customer with approval; a
  stranger's email creates a lead with its source shown; no email leaves without a named approver.

### W5. Microsoft Teams: from channel webhooks to a bot

- Gap: IIP posts adaptive cards to a channel through an incoming webhook. CIP has a Teams app.
- CIP evidence: `Services/Teams/` (`TeamsBotConnector`, `TeamsBotInbound`, `TeamsAppInstaller`,
  `TeamsChannelMappingService`, `TeamsDeliveryProviders`, `TeamsNotificationOrchestrator`, `TeamsRedactionService`,
  `TeamsDeepLinkBuilder`, `TeamsDeliveryContextResolver`, `TeamsNotificationEventTypes`).
- IIP today: `IncomingWebhookTeamsProvider`, `TeamsAdaptiveCardBuilder`, a notification queue and repositories are
  built; live notifications are Disabled.
- Adapt: replace CIP's credit event types with IIP's (claim awaiting approval, service clock at risk, fraud signal
  escalated, document needs review, lead needs a person); keep redaction so no customer content is posted
  unredacted; deep links go to IIP screens.
- Gate: a claim approval request appears in a mapped Teams channel, redacted, and the deep link opens the claim.

### W6. Website chat widget

- Gap: IIP has no web channel (gap K1). "Web" is only a label.
- CIP evidence: `Services/WebChat/WebChatIngestionService.cs` (721), `WebChatAdminService.cs` (436),
  `Controllers/WebChatPublicController.cs` (191), `Contracts/WebChatDtos.cs`, migration `WebChatWidget`, public
  script `apps/web/public/widget.js`, `WebChatSetupModal.tsx`, `WebChatInbox.tsx`; tests `WebChatAgentTests`,
  `InboxNotifierTests`.
- Adapt: route the conversation into the Kato pipeline (the same assistant, memory and hand-over to a person as
  WhatsApp); site keys, origin allowlist, rate limits and session tokens copied as they are; widget served from
  the portal origin; an Inbox filter for web conversations.
- Gate: embed the widget on a test page; a visitor's message creates a lead and a conversation in the Inbox;
  an origin not on the allowlist is refused; Kato answers from approved sources only.

### W7. WhatsApp hardening (what CIP guards, and what the 12 October outage taught)

- Gap A, outbound guard: CIP sends through a relay with `ContentGuard`, `AllowlistValidator`, `RateLimiter`,
  `JwtValidator`, `RecipientResolver` and `AuditLogger` (`apps/whatsapp-relay/Services`, Service Bus trigger in
  `Functions/WhatsAppSendServiceBusFunction.cs`). IIP sends directly. Build an egress guard in IIP's outbound
  provider by copying those checks, not necessarily the separate Functions app.
- Gap B, inbound visibility: on 12 October the platform accepted Meta's webhooks and silently dropped every
  text for days, and no screen showed it. Build: a webhook receipt log (time, number, kinds, outcome, count
  saved) per phone number, visible in Admin; "last message received" shown on the WhatsApp card with a warning
  when it is old; request-level telemetry for the webhook route kept on permanently.
- Gap C, Meta-side checks: a "Check with Meta" action that reads the subscribed apps and the number's webhook
  configuration (the two reads used on 12 October) and says in words what is wrong: app unpublished, field not
  subscribed, address overridden.
- Gate: a test message sent with a deliberately wrong signature is visible in the receipt log as refused; an old
  last-message time shows a warning; the Meta check reports the three failure causes against a stub.

### W8. SMS

- Not a CIP gap (no SMS found in CIP). IIP has Africa's Talking and EGO SMS built and not live. Configure,
  verify one send, and put the card in the catalogue. No copying.

### W9. Integration Center: catalogue, status and test sends

- Gap: IIP's Settings shows connections but not CIP's catalogue with grouped cards, setup progress, per-card
  status and a "send a test" action.
- CIP evidence: `Services/IntegrationCenterService.cs` (696), `Services/Settings/OfficerIntegrationsCatalogService.cs`
  (355), `OfficerIntegrationsActionService.cs`, `IntegrationSendTestService.cs`, web `V2IntegrationsView.tsx`,
  `lib/use-integrations-catalog.ts`, `lib/integration-setup-progress.ts`, `lib/integration-suite-card-status.ts`.
- Adapt: IIP's catalogue entries (WhatsApp, Web chat, Email, Teams, SMS, Google Drive, OneDrive, SharePoint,
  Data sources); status comes from real health checks (see W7 gap B), never from "configured" alone.
- Gate: every card shows one of Connected, Not connected, Needs attention, Not configured, each backed by a real
  check, and the six UI states exist for the page.

### W10. Data and analytics connectors (decide each before building)

- CIP evidence: `Services/BankData/StructuredBankConnectorService.cs`, `StructuredBankSqlAccess.cs`,
  `BankDataHubService.cs` (read-only access to a bank's SQL data), `Services/DataProducts/`, Power BI and Fabric
  and Azure SQL cards in the catalogue, `Contracts/CrbDtos.cs`, M-Pesa, Salesforce entries.
- Candidates for IIP, each its own decision: a read-only policy-administration or core-system connector (the
  structured SQL access pattern, applied to an insurer's policy and claims systems); Power BI and Fabric export
  for the Fabric edition; mobile money (MTN, Airtel) for premium collection and claim payouts through the payment
  adapters; Salesforce only if a customer asks. The credit reference bureau connector does not apply to insurance.
- Gate for each chosen item: a written scope, the data classification under the data boundary (control plane
  versus customer data), and a synthetic provider first.

## Security workstreams (added 2026-10-14)

CIP's security code was read at the same commit (`1a4be27`). Its design note
`docs/security/CIP_Platform_MFA_Implementation_Plan.md` is headed "Draft, no implementation yet", but the code
and tests below exist, so the note's status line is stale and the code is the reference. IIP was searched for
each capability on the SaaS branch at `cc3ce0c`: it has no second factor of any kind, no webhook replay
protection, no legal hold and no security screen in Settings. It does have encrypted connection secrets
(`IConnectionSecretProtector`), retention policies and consent records, named rate limits on sign-up, invitation,
Marketplace and webhook routes, API keys and an audit trail.

### S1. Platform sign-in verification (two-factor), the headline gap

- Gap: after Microsoft or Google sign-in, IIP asks for nothing more. CIP adds its own second step that works the
  same whichever provider the person signed in with.
- What CIP has, all under `apps/credit-api/Services/Security/`:
  - Methods: authenticator app codes (standard TOTP, so Microsoft Authenticator and Google Authenticator both scan
    the same QR code; library `Otp.NET` 1.4.0), passkeys and security keys (WebAuthn; library `Fido2` 3.0.1),
    email codes, SMS codes (EGO SMS), and one-time backup codes stored as hashes.
  - `PlatformMfaEnrollmentService.cs` (906), `PlatformMfaWebAuthnService.cs` (540), `PlatformMfaLoginService.cs`
    (488), `PlatformMfaStatusService.cs` (171), `PlatformMfaSessionService.cs` (143),
    `PlatformMfaPolicyService.cs` (124), `PlatformMfaAdminService.cs` (233),
    `PlatformMfaEnforcementMiddleware.cs` (133), `PlatformMfaSecretProtector.cs` (60),
    `PlatformMfaEmailCopy.cs`, `PlatformMfaEmailSignInBootstrap.cs`, `Controllers/PlatformMfaAuthController.cs`,
    the MFA routes in `OfficerSettingsController.cs`, `Services/Sms/SmsOtpService.cs` (306).
  - Policy per organisation: `require_platform_mfa`, `allow_snooze`, `min_methods_required`.
  - Enforcement: middleware answers 403 `MFA_ENROLL_REQUIRED` or `MFA_REQUIRED` on every API route except an
    allowlist. Verification lasts until sign-out, an administrator reset, or a newer interactive sign-in
    (compared by the identity provider's `auth_time`, not token issue time, so silent token renewal does not
    force a new code).
  - Email code defaults: 6 digits, valid 30 minutes, 5 attempts, 60 seconds between sends.
  - Administrator tools: set the policy, reset a person's methods, see enrolment figures.
  - Web: `app/security/verify/page.tsx` (777), `PlatformMfaLoginGate.tsx`, `PlatformMfaNudgeHost.tsx` (273),
    `settings/V2SecurityView.tsx` (457) and `settings/mfa/*` (Microsoft and Google authenticator setup, passkey,
    SMS, email, backup codes, method cards, admin panel), `lib/auth/platform-mfa-rechallenge.ts`,
    `lib/auth/platform-session.ts`.
  - Tests: `PlatformMfaEnrollmentServiceTests` (378), `PlatformMfaPhase34Tests` (201),
    `PlatformMfaStatusServiceTests` (111), `PlatformMfaOnboardingLogoTests`, `SmsOtpServiceTests` (290).
- IIP today: nothing. No tables, no routes, no screen. Settings has no Security section.
- Adapt: tables gain workspace as well as tenant; secrets go through `IConnectionSecretProtector` with keys in Key
  Vault; SMS codes use IIP's existing SMS providers, and email codes need W4 step one (email is Disabled live
  today), so the first release ships authenticator app, passkey and backup codes, with email and SMS codes
  switched on when those channels are live; the WebAuthn relying party is the portal's own domain; the policy
  and reset actions need `ManageTenantSettings` and are audited; the allowlist is rewritten for IIP's routes
  (health, sign-in, the verification routes, and the public claimant and webhook routes, which have their own
  authentication).
- Gate: enrol Microsoft Authenticator and Google Authenticator from the same QR code; a signed-in session
  without verification gets 403 on a protected route and the portal shows the verify screen; a wrong code five
  times locks the challenge; a backup code works once; an administrator reset forces re-enrolment; with the
  policy off nothing changes for existing users; CIP's tests pass in IIP.

### S2. Security section in Settings and the first-run prompt

- CIP evidence: `V2SecurityView.tsx` (three tabs: platform verification, security posture, sign-in provider),
  `PlatformMfaNudgeHost.tsx` (a prompt after sign-up, a banner after a snooze, optional hard gate),
  `OfficerSecuritySummaryService.cs`.
- Adapt: add a `security` section to `SettingsModal`. Copy the method cards and wizards. Do **not** copy the
  posture text as written: CIP's summary states "AES-256" and "TLS 1.3 with mutual authentication" as fixed
  copy. IIP shows only what a real check returned, or "Not configured".
- Gate: the six UI states exist; a new organisation's creator is prompted once and can snooze within policy.

### S3. Webhook replay protection and rate limiting

- CIP evidence: `Services/WebhookSecurity/WhatsAppWebhookReplayProtectionService.cs` (65),
  `WebhookRateLimitingMiddleware.cs` (66).
- IIP today: signature check and a named "webhook" rate limit exist; no replay protection. Duplicate messages are
  ignored by message id after they are accepted.
- Build with W7 (same routes, same tests).
- Gate: a correctly signed webhook replayed outside the allowed window is refused and appears in the receipt log.

### S4. Field encryption with Key Vault keys

- CIP evidence: `Services/Encryption/FieldEncryptionService.cs` (142), `KeyVaultEncryptionKeyProvider.cs` (136).
  Its `NoOpEncryptionProvider.cs` is **not** copied.
- IIP today: connection secrets are protected with ASP.NET Data Protection. Whether its key ring is itself
  wrapped by a Key Vault key was not checked; check before deciding the size of this item.
- Gate: a written list of which fields are encrypted at field level, and key rotation exercised once on dev.

### S5. Retention, legal hold and redaction

- CIP evidence: `Services/Retention/` (`MessageRetentionService` 245, `RetentionPolicyService` 140,
  `LegalHoldService` 107, `RetentionBackgroundService`), report and Teams redaction services (W4, W5).
- IIP today: retention policies and consent records exist; no legal hold; one redaction use.
- Insurance note: a claim under dispute or litigation must not be purged, so legal hold matters more here than in
  CIP. Confirm periods with legal and compliance.
- Gate: a conversation under legal hold survives a retention run; the hold and its release are audited.

### S6. Security monitoring and self-audit

- CIP evidence: `Services/Audit/SecurityAuditService.cs` (620), `ComplianceAuditService.cs` (653),
  `Services/EnterpriseSecurity/SecurityMonitoringService.cs` (148), `EnterpriseSecurityComplianceService.cs`
  (585), `Services/ConnectorSecurityService.cs` (186, connector access evidence, used by W2).
- Not read in depth. Before copying, read what each check actually verifies; a check that always passes is worse
  than none.
- Gate: every check shown to an administrator is backed by a real test of the live system.

### Not in CIP either (decide whether IIP wants them)

Searched for and not found in CIP's code: idle session timeout, IP allowlists, SCIM provisioning, trusted
devices, sign-in lockout beyond code attempts, SAML. None is a CIP gap. Entra Conditional Access can supply
several of these for Microsoft work accounts without code.

## Order and dependencies

1. W0 and W1 (registrations and settings; prove the existing file pick). No code, unblocks demos.
2. S1 and S2 first release: authenticator app, passkey and backup codes, policy off by default. It depends on
   nothing else in this plan and protects everything built after it.
3. W7 gaps B and C with S3, and W9 status checks (small, protect everything after them).
4. W4 step one (switch on and verify email, Teams webhooks, SMS), then switch on email and SMS codes in S1.
5. W2 (folder connectors) with W3 behind it, because export reuses the same tokens and pickers.
6. W6 (web chat) and W4 step two (email to lead), which close the acquisition gaps K1 and K2.
7. W5 (Teams bot), then W7 gap A (egress guard).
8. S5 (legal hold), then S4 and S6 after the checks named in them.
9. W10 items as decided.

W2, W6 and W5 are independent of one another once the common seams exist, so they can run as parallel tracks.
S1 can run in parallel with W0 and W1.

## Required for every workstream before it is called done

- CIP's own tests for the copied code, run in IIP and passing, plus new tests for the seams.
- Backend: `dotnet build` and `dotnet test` on the solution; portal: lint, build and the Playwright specs at 1280
  and 768 wide, with the six states (loading, empty, empty after filter, error, not configured, no permission).
- Verified live on the dev environment with a real account before the card shows Connected.
- Documentation: the setup guide for the workstream, with the manual actions listed.
- A report that states what was checked, what was not, and any manual step still needed.

## Manual actions that only the owner can do

- Google Cloud: project, Drive and Picker APIs, OAuth consent screen, OAuth client, API key (W0, W2, W3).
- Microsoft Entra: the "IIP Files" app registration and its consent; admin consent for the mail and Teams
  permissions; for SharePoint, `Sites.Selected` needs each customer's administrator to approve (W2, W4, W5).
- Teams: upload the app package to the tenant (W5).
- DNS and hosting: the origin that serves the widget script (W6).
- Meta: keep the app published, with the "messages" field subscribed (W7).

## Open decisions for the owner

1. Reuse CIP's Google and Microsoft registrations for IIP, or create IIP's own? Reuse is faster; separate is
   cleaner for a customer's consent screen.
2. SharePoint: ship with `Sites.Selected` (needs each customer's administrator) or start with OneDrive and Drive
   only?
3. Should W2 sync only folders an administrator picks, or let each person connect their own? CIP supports both; the
   safer default is administrator-picked folders with a person's approval before anything reaches retrieval.
4. W10: which of the candidate connectors are wanted for the next customer?
5. Two-factor: required for every organisation from the start, or off by default with each organisation's
   administrator switching it on? CIP supports both through `require_platform_mfa`.
6. Two-factor: which methods in the first release? Recommended: authenticator app, passkey and backup codes,
   with email and SMS codes once those channels are live.
7. Two-factor: may a person snooze the setup prompt, and for how long?
8. Does two-factor also apply to email and password sign-in from the claims-core branch, when that merges?
