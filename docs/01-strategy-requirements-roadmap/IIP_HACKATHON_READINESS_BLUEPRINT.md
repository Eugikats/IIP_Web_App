# IIP Production Readiness and Hackathon Submission Blueprint

**Status:** Working blueprint. Written 2026-09-09, revised 2026-09-11 to cover every gap end to end.
**Plan:** 8 phases (0-7), 45-57 working days for a complete system; 27-34 for the demo-ready subset. See section 14.
**Scope:** Everything required to take IIP from "internal claims-operations platform with real AI plumbing" to "demonstrable end-to-end product with working customer channels, configurable agents, and a defensible regulatory story."
**Audience:** Whoever implements this (agent or human), and whoever answers due-diligence questions about it.

**Implementation status (2026-09-11):** Phase 1 is essentially complete — the Connection model, `EgoSmsProvider`, `TenantAwareSmsProvider`, SMTP email (`SmtpEmailProvider`, hand-rolled connection validator), `TenantAwareEmailSendingProvider`, the `/app/admin/connections` screen (SMS + Email), the WhatsApp deployment-gate fix, WhatsApp webhook auto-registration, and the claimant magic-link delivery-visibility fix are all source-complete and locally verified (909/909 automated tests passing, clean solution build, clean TypeScript compile, all portal validation scripts passing). **None of it has been deployed or exercised against a live SMS, SMTP, or WhatsApp account.** Do not tell a room it works until it has been — and the SMTP validator's STARTTLS path specifically has not even been tested against a fake server, only its plaintext AUTH LOGIN path has. See the status table in section 14's Phase 0/1 rows for exactly what is done and what remains: inbound email (IMAP ingestion), 0.2 App Insights, 0.3 Service Bus verification, and everything from Phase 2 onward.

---

## 0. How to use this document

This is the single index for the readiness work. It does three things:

1. **States what is actually true today**, verified by reading the code — not by reading older documentation.
2. **States what is missing**, in the specific terms of the gap rather than as a wish.
3. **Specifies how to close each gap**, at file-and-interface level, so implementation is execution rather than rediscovery.

### 0.1 Verification method

Every "today" claim in this document was verified on 2026-09-09 by reading the source at the path cited next to it. Where a claim could not be verified from source, it is marked `UNVERIFIED` and the reason is given. Do not upgrade an `UNVERIFIED` line to a fact without re-checking the code.

Where this document and any older document disagree, **re-read the code before trusting either.** The repository has a documented history of documentation drifting ahead of implementation, and the governance rule in `AGENTS.md` is explicit that implementation reality wins.

### 0.2 Status legend

| Marker | Meaning |
|---|---|
| **REAL** | Implemented, wired to a real backend path, works when configured. |
| **PARTIAL** | Implemented but incomplete in a way that blocks the stated goal. |
| **STATIC** | Renders, but the content is hardcoded in the component — no backend, no persistence, no effect. |
| **MISSING** | Does not exist. |
| **BLOCKED** | Exists in code but unreachable or inert in a deployed environment because of configuration or navigation. |

### 0.3 The one-sentence summary

The AI and channel *plumbing* is genuinely built and mostly real; the *configuration surfaces* that would let a non-engineer turn any of it on are almost entirely absent, and the *navigation* hides the work that does exist — so the platform is far more capable than it is able to demonstrate.

---

## 1. Executive gap table

This is the A-to-Z list. Each row links to the section that specifies the fix.

| # | Capability | Today | Gap in one line | Fix |
|---|---|---|---|---|
| 1 | Azure OpenAI / LLM | **REAL** | None — live and working. | — |
| 2 | Document extraction (Document Intelligence) | **REAL** | None — live and working. | — |
| 3 | Knowledge retrieval (Azure AI Search) | **REAL** | None at the provider level. | — |
| 4 | Knowledge base management UI | **REAL** | Upload, classify, approve, re-index, retrieval-eligibility gating all work. | §6 |
| 5 | System prompt configuration | **MISSING** | Prompts are hardcoded C# string literals; no tenant can change agent voice or rules. | §6.2 |
| 6 | Agent response configuration | **MISSING** | Registry has a kill switch only — no tone, no scope, no escalation rules, no templates. | §6.3 |
| 7 | Tenant agent policy | **STATIC** | The page is a hardcoded array; nothing it displays is enforced *by that page*, and its "configurable" threshold has no control. | §6.4 |
| 8 | WhatsApp credentials | **REAL** | Validated against Meta, encrypted at rest, governed and audited. | — |
| 9 | WhatsApp webhook registration | **REAL** (2026-09-11, unverified live) | Auto-registered via `subscribed_apps` on save; callback URL shown with copy button when manual setup is needed. Not yet exercised against real Meta traffic. | §4.3, §14 Phase 1 |
| 10 | WhatsApp go-live | **BLOCKED** | Saving valid credentials is not enough — a deploy-time env var must also be changed and the app restarted. | §3 |
| 11 | SMS sending | **REAL** (2026-09-11, unverified live) | Both EgoSMS and Africa's Talking adapters exist, both resolvable via a UI at `/app/admin/connections` with no redeploy. Not yet exercised against a live account. | §5, §14 Phase 1 |
| 12 | SMS delivery receipts | **MISSING** | No webhook receiver for transaction status. | §5.4 |
| 13 | Email ingestion | **REAL** | Graph mailbox path exists with an admin surface. | §7 |
| 14 | Outbound email provider choice | **PARTIAL** (2026-09-11) | SMTP adapter now exists and is connectable from `/app/admin/connections`; its STARTTLS path is unverified. Graph remains the only inbound-ingestion provider. | §7.2, §14 Phase 1 |
| 15 | Runtime provider configuration | **PARTIAL** (2026-09-11) | SMS now resolves through the Connection model (§3.2's three-step order); every other provider (AI, retrieval, documents, email, notifications) is still startup-env-var-only. | §3, §14 Phase 1 |
| 16 | Customer acquisition | **PARTIAL** | Leads inbox is real but manual-capture only; the page itself states channel ingestion is not connected. | §8 |
| 17 | WhatsApp knowledge-base assistant | **MISSING** | No inbound-message-to-grounded-answer path exists. | §8.3 |
| 18 | FNOL intake | **REAL** | `/app/claims/new` renders a real FNOL form. | §9 |
| 19 | FNOL discoverability | **BLOCKED** | Not in the navigation rail; reachable only if another page happens to link to it. | §10 |
| 20 | Claimant self-service portal | **PARTIAL** (2026-09-11) | The fire-and-forget delivery bug is fixed (failures are now logged and recorded on the token, never silently discarded) and SMS delivery is now reachable via a connected provider — but no live send has actually been exercised yet. | §9.3, §14 Phase 1 |
| 21 | Navigation legibility | **PARTIAL** | 64px icon-only rail, no labels, no expand, 7 of ~14 real destinations shown. | §10.2 |
| 21b | Rail scroll / pinned icons | **PARTIAL** | Pinned top and bottom regions squeeze the one scrollable region; clipped destinations look non-existent. | §10.4 |
| 21c | Claim ownership on intake | **PARTIAL** | Claims are routed to a team but never to a person (`AssignedUserId = null`). | §14 WO 5.1 |
| 21d | FNOL acknowledgement | **MISSING** | No reference number or confirmation sent to the customer on submission. | §14 WO 5.2 |
| 21e | AML/CFT risk capture | **MISSING** | Required by IRA directive of 8 April 2026; fraud signals are not AML. | §14 WO 7.3 |
| 22 | Consent capture / DPPA record | **MISSING** | No consent entity anywhere in the domain. | §12.2 |
| 23 | Data retention policy surface | **MISSING** | Referenced in Admin as informational text only. | §12.3 |
| 24 | Data subject rights (access/erasure) | **MISSING** | No endpoint, no UI. | §12.4 |
| 25 | Insurer core-system (PAS) integration | **MISSING** | No adapter of any kind. | §13.1 |
| 26 | Application Insights telemetry | **PARTIAL** | Resource exists; zero request/exception rows captured over 24h in dev. | §13.2 |

**Reading of the table:** rows 5, 6, 9, 15, 17 and 22 stand between the current build and the product being described to stakeholders. Rows 19, 21, 21b and 21c are cheap to fix and disproportionately improve how the product *feels* in a demo. Every row in this table has a work order in section 14 — nothing is left unaddressed.

---

## 2. Regulatory foundation

Two distinct regimes apply. Conflating them is the most common mistake in the pitch material, so they are separated here.

> **Ownership:** regulatory filings, licensing and regulator relationships are handled by the business, not by engineering. This section is kept as the research record and because it determines two hard product constraints — what the customer-facing assistant may say (section 8.2) and why consent records exist (section 12.2). No work order in section 14 depends on a regulatory outcome.

### 2.1 Insurance regulation — Insurance Regulatory Authority of Uganda (IRA)

**Governing law:** the Insurance Act, 2017 (Cap. 213), administered by the IRA, with subsidiary regulations gazetted from 2019 onward.

**Verified facts about the current regime (researched 2026-09-09):**

- The IRA licenses market participants annually. For 2026 it cleared **144 players** effective 2 January 2026, across a defined set of licence classes: reinsurers, non-life insurers, life insurers, microinsurers, Takaful, HMOs, reinsurance brokers, **insurance brokers (49)**, **bancassurance agents (22)**, and loss assessors/adjusters/risk advisors (33).
- The IRA issues binding circulars continuously. The 2026 set includes licensing and compliance circulars for brokers, bancassurance agents, microinsurance organisations, HMOs, and risk advisors, plus an **AML/CFT risk-assessment directive (8 April 2026)** and the **ESG Guidelines for the Insurance Sector 2026 (29 April 2026)**, which impose governance, underwriting, investment-screening and periodic-reporting obligations on every licensed insurer, reinsurer, broker and intermediary.
- The IRA operates a **Complaints Bureau and Tribunal** for policyholder disputes.

**What this means for IIP's positioning:**

IIP is not an insurer and must not present as one. There is no licence class in the IRA's 2026 clearance list called "technology platform" — which cuts both ways:

1. **If IIP only provides software to a licensed insurer** (the platform is deployed into the insurer's environment, the insurer makes every decision, IIP touches no premium and binds no risk), IIP is a **vendor**, not a licensee. This is the current architecture and it is the defensible position. The Managed Application / SaaS Fabric editions both preserve it.
2. **If IIP intermediates** — recommends products to a consumer, collects their details for the purpose of placing cover, earns commission, or completes a sale — that is **intermediation**, and intermediation requires a licence (broker or agent class). No amount of "we're just a distribution layer" wording changes that; the activity determines the classification.

> **This is the single most important compliance decision in the project.** The submission material describes conversational advice-and-purchase. The codebase implements insurer-side operations. Those are different regulatory animals. Section 8 specifies how to build the customer-facing capability *without* crossing into intermediation, and section 15 states exactly what must not be claimed.

**Concrete obligations IIP must support in software** (because its insurer customers carry them):

| Obligation | Source | What IIP must provide |
|---|---|---|
| Auditable claims handling and decision trail | Insurance Act 2017; Complaints Bureau practice | Immutable audit of every decision, who made it, on what evidence — **already built** |
| Human accountability for claim outcomes | Insurance Act 2017 | Named human reviewer per decision — **already built** |
| Complaint/dispute evidence production | Complaints Bureau & Tribunal | Ability to export a full case file with timeline and citations — **PARTIAL**, see §12.5 |
| AML/CFT risk assessment | IRA directive, 8 April 2026 | Ability to flag and record risk signals — fraud signals exist as advisory; AML specifically is **MISSING** |
| ESG periodic reporting | ESG Guidelines 2026 | Out of IIP's scope; note as a non-goal |

**Regulatory sandbox:** the submission claims participation in an IRA sandbox. `UNVERIFIED` — I could not confirm from public IRA sources that a formal insurance regulatory sandbox with published entry criteria currently exists and is open. **Do not state sandbox participation as a fact in the submission until someone confirms it directly with the IRA.** If it does not exist, the honest framing is "we are seeking IRA engagement on the appropriate regulatory treatment," which is credible and safe.

### 2.2 Data protection — Data Protection and Privacy Act, 2019

**Regulator:** the Personal Data Protection Office (PDPO), an independent office under NITA-U, operational since August 2021.

**Verified obligations:**

- **Registration is mandatory** for every data collector, data processor and data controller under Regulation 15(1) of the Data Protection and Privacy Regulations. **There is no minimum-size threshold** — a startup handling any personal data must register.
- Registration requires **Form 2**: a description of the personal data collected/processed and the purposes for which it is processed.
- Applicants are expected to have, at minimum, an **Information Security Policy** and a **Data Retention Policy** in place at the point of application.
- Registration is valid for **one year**; renewal must be applied for **within three months before expiry**.
- Registrants must file an **annual compliance report within 90 days** of the end of each financial year.

**What this means for IIP:**

Otic itself is a data collector/processor the moment it handles a Ugandan customer's name, phone number or claim details — including in a demo with real people's data, and including through a WhatsApp test number. Two consequences:

1. **Organisational:** Otic needs PDPO registration, an Information Security Policy, and a Data Retention Policy. These are documents, not code, and they are cheap to produce relative to the risk of being asked for them. **Owner: business, not engineering.**
2. **Product:** the platform must be able to *evidence* lawful processing — consent captured, purpose recorded, retention enforced, and subject rights serviceable. Today it can evidence almost none of this. See §12.

**A note on the demo seed data.** The environment seeds 50 synthetic customers with `@example.ug` addresses and `+2567…` numbers from a fixed random seed. This is genuinely synthetic and carries no DPPA exposure — a real strength worth stating plainly when asked "what customer data do you hold?" The answer today is: none.

### 2.3 The honest regulatory story

Assembled, the defensible position is:

> IIP is enterprise software for licensed insurers. It does not underwrite, bind, price, settle or decide — every consequential action is gated on a named human reviewer, and the platform is architecturally incapable of an autonomous claim decision. It holds no real customer data today; its demo environment is deterministically synthetic. Otic will complete PDPO registration as a data processor before handling any live customer data on a customer's behalf, and the platform provides the consent, retention and audit surfaces its insurer customers need to discharge their own obligations.

Every clause of that paragraph is either true today or specified in this document. Nothing in it requires a licence Otic does not hold.

---

## 3. The architectural change that unlocks everything else

### 3.1 The problem, precisely

`src/InsuranceIntelligence.Infrastructure/Configuration/InfrastructureOptions.cs:28-37` declares provider selection as immutable startup configuration:

```csharp
public string AiProvider { get; init; } = "Mock";
public string DocumentProvider { get; init; } = "Mock";
public string RetrievalProvider { get; init; } = "Mock";
public string QueueProvider { get; init; } = "Mock";
public string EmailProvider { get; init; } = "Mock";
public string CommunicationProvider { get; init; } = "Mock";
public string SmsProvider { get; init; } = "Mock";
public string NotificationProvider { get; init; } = "Mock";
public string ChannelProvider { get; init; } = "Mock";
public string ProvisionerProvider { get; init; } = "Mock";
```

These are bound from `INSURANCEINTELLIGENCE_*` environment variables at startup. Credentials for each provider live in the same options tree (`options.AfricasTalking.Username`, `options.AfricasTalking.ApiKey`, and so on).

The consequence, stated as the user experiences it: **to turn on SMS you must have Azure portal access, set two app settings, and restart the App Service.** There is no screen. The same is true of email, retrieval, notifications, and the queue.

WhatsApp is the sole exception, and it is only a half-exception. Its *credentials* are stored per-tenant, encrypted, in the database (`WhatsAppChannelConfiguration`), validated against Meta before saving, and audited. But its *provider selection* is still the `ChannelProvider` env var — which is why `WhatsAppConfigurationEndpoints.cs:53-54` has to return a flag called `RequiresChannelProviderDeploymentChange`, and why the UI at `WhatsAppSettings.tsx:164-169` shows this message:

> "Credentials are saved and verified, but this environment isn't yet routing WhatsApp traffic through them. Ask whoever manages your deployment to set `INSURANCEINTELLIGENCE_CHANNEL_PROVIDER=WhatsAppCloudApi` to go live."

That message is honest, well-written, and an admission that the model is wrong. A tenant admin who has just successfully proven their credentials work is told to go find a devops person.

### 3.2 The fix: a first-class Connection model

Generalise exactly what WhatsApp already does, to every provider.

**New domain concept — `IntegrationConnection`:**

```
IntegrationConnection
  Scope                  TenantScope       // tenant + workspace isolation, as everywhere else
  ConnectionType         enum              // Sms | Email | Channel | Ai | Retrieval | Documents | Notification
  ProviderKey            string            // "EgoSms" | "AfricasTalking" | "Graph" | "Smtp" | "WhatsAppCloudApi" | ...
  EncryptedSettingsJson  string            // provider-specific secrets, protected at rest
  NonSecretSettingsJson  string            // sender IDs, base URLs, display names — readable back
  IsActive               bool              // exactly one active connection per (Scope, ConnectionType)
  LastValidatedAt        DateTimeOffset?
  LastValidationStatus   string?           // "Success" | "Failed"
  LastValidationError    string?           // sanitized, safe for display
  ConfiguredByUserId     string
  ConfiguredAt           DateTimeOffset
```

**New interfaces:**

```csharp
// One per provider. Proves credentials work against the real service before anything is persisted.
public interface IConnectionValidator
{
    string ProviderKey { get; }
    ConnectionType ConnectionType { get; }
    Task<ConnectionValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default);
}

public sealed record ConnectionValidationResult(
    bool Success,
    string? SafeError,
    IReadOnlyDictionary<string, string>? DiscoveredSettings); // e.g. verified display name, WABA id, balance

// Resolves the live provider for a tenant at call time, not at startup.
public interface IConnectionResolver
{
    Task<TProvider?> ResolveAsync<TProvider>(
        TenantScope scope,
        ConnectionType type,
        CancellationToken cancellationToken = default) where TProvider : class;
}
```

**Resolution order (this is the whole design):**

1. An **active, validated `IntegrationConnection`** for this tenant and connection type → use it.
2. Otherwise, the **environment variable** provider, if it is not `Mock` → use it. *(Preserves every existing deployment and all current behaviour.)*
3. Otherwise → **`Not configured`**. Honest, explicit, never a fake success.

This ordering is what makes the change safe to ship incrementally: nothing that works today stops working, and each provider can be migrated to the Connection model one at a time.

### 3.3 Why this specific design

- **It preserves the protected-runtime rule.** `AzureProviderStartupValidator` currently fails closed in Staging/Production when a required provider is `Mock`. Extend it to also accept "a validated Connection exists," rather than removing it. A tenant still cannot run production on mocks.
- **It preserves tenant isolation.** Connections are scoped, so two workspaces can hold different SMS accounts. The env-var model cannot express this at all — it is process-global, which is a genuine multi-tenancy defect, not just an inconvenience.
- **It preserves the audit and human-approval boundary.** Saving a connection is a governed action with a required reason, exactly like `WhatsAppConfigurationEndpoints.SaveConfiguration` already is.
- **It reuses a proven path.** `DataProtectionWhatsAppCredentialProtector` already provides at-rest protection via ASP.NET Data Protection. Generalise it to `IConnectionSecretProtector`; do not invent a second crypto path.
- **Secrets stay write-only from the browser.** The existing WhatsApp endpoint never returns a stored secret — only whether one is present. That property must hold for every connection type. It is the difference between a settings page and a credential-exfiltration endpoint.

### 3.4 One screen, many connections

With this in place, a single `/app/admin/connections` screen handles every integration with one interaction pattern:

```
Connections
├── Messaging
│   ├── WhatsApp Business      [Connected · verified 2 min ago]   Test  Disconnect
│   ├── SMS                    [Not configured]                   Connect
│   └── Email                  [Connected · Microsoft Graph]      Test  Disconnect
├── Intelligence
│   ├── AI model               [Connected · Azure OpenAI]         Test
│   ├── Document extraction    [Connected · Doc Intelligence]     Test
│   └── Knowledge retrieval    [Connected · Azure AI Search]      Test
└── Platform
    ├── Work queue             [Connected · Service Bus]          Test
    └── Notifications          [Not configured]                   Connect
```

Each row: pick a provider from the registered validators for that type, fill the fields that provider declares, hit **Validate and connect**, and it is live — no restart, no env var, no devops. That is the "I should only need to input credentials" requirement, satisfied structurally rather than per-integration.

The existing WhatsApp page becomes one detail view inside this screen, unchanged in behaviour.

---

## 4. WhatsApp Business API

### 4.1 What is already real

Verified in `src/InsuranceIntelligence.Api/Integrations/WhatsAppConfigurationEndpoints.cs` and `web/portal/lovable-pages/admin/WhatsAppSettings.tsx`:

- Four credential fields with genuinely helpful inline guidance telling the operator exactly where in Meta's console each value lives: phone number ID, permanent access token, app secret, webhook verify token.
- **Credentials are validated against the real Meta Graph API before anything is persisted** (`SaveConfiguration` calls `IWhatsAppGraphValidationClient.ValidateAsync` and returns Meta's own sanitized error on failure). A wrong token fails at the moment of entry, not silently at the first inbound message.
- Secrets are encrypted at rest and are **write-only from the browser** — `GetStatus` returns presence, never values.
- Save and disconnect are **governed actions** requiring a typed reason, written to the audit trail.
- **Test connection** re-validates on demand and records the outcome.
- Inbound webhook receiver (`WhatsAppIntegrationEndpoints.ReceiveWebhook`) reads the **raw body before model binding** so the HMAC covers the exact bytes Meta signed — correct, and a detail that is frequently got wrong.
- Signature verification, subscription verification (`hub.challenge`), media ingestion onto case documents, delivery-status events, and outbound send with an idempotency key and a human-approval flag.

This is a well-built integration. The gaps below are about *connecting* it, not about its quality.

### 4.2 Gap: the webhook is never generated or shown

The operator enters a verify token they invent. Nothing then tells them **what URL to give Meta**, and nothing registers it. They must know that the callback is `https://<api-host>/integrations/whatsapp/webhook`, navigate Meta's console, paste both values, and subscribe to the right webhook fields. Every one of those steps is an opportunity to fail silently, and the failure mode is "messages just never arrive."

### 4.3 Fix: generate and auto-register the webhook

Meta supports programmatic webhook registration through **callback URL overrides**. This is the mechanism that makes the requirement achievable.

**Endpoint (WABA-level override):**

```
POST https://graph.facebook.com/v25.0/<WABA_ID>/subscribed_apps
Authorization: Bearer <PERMANENT_ACCESS_TOKEN>
Content-Type: application/json

{
  "override_callback_uri": "https://<api-host>/integrations/whatsapp/webhook",
  "verify_token": "<the token the platform generated>"
}
```

Response on success:

```json
{ "success": true }
```

**Alternative (phone-number-level override), if per-number routing is wanted:**

```
POST https://graph.facebook.com/v25.0/<BUSINESS_PHONE_NUMBER_ID>
Authorization: Bearer <PERMANENT_ACCESS_TOKEN>
Content-Type: application/json

{
  "webhook_configuration": {
    "override_callback_uri": "https://<api-host>/integrations/whatsapp/webhook",
    "verify_token": "<token>"
  }
}
```

**Routing precedence Meta applies:** phone-number override → WABA override → the app's default callback URL. Registering at WABA level is the right default for IIP: one workspace, one WABA, one callback.

Constraints to respect: `override_callback_uri` is capped at **200 characters**; Meta verifies the endpoint using the standard `hub.mode`/`hub.verify_token`/`hub.challenge` handshake, which `WhatsAppIntegrationEndpoints.VerifyWebhook` **already implements correctly** — so the platform will pass Meta's verification as soon as the URL is registered and the credentials are saved.

**Implementation changes:**

1. **Capture the WABA ID.** Add `wabaId` to `WhatsAppSaveConfigurationRequest` and to `WhatsAppChannelConfiguration`. It is available in Meta Business Settings next to the phone number, and can also be discovered from the phone number ID via `GET /<PHONE_NUMBER_ID>?fields=whatsapp_business_account` — prefer discovery, and fall back to asking.
2. **Generate the verify token; stop asking for it.** Replace the "Webhook verify token" input with a server-generated cryptographically random value. The operator inventing a shared secret is a needless step and a weak-secret risk. Keep it encrypted, exactly as now.
3. **Derive and expose the callback URL.** Add `WebhookCallbackUrl` to `WhatsAppConfigurationStatusResponse`, built from configured public base URL + the existing route. Display it on the settings page with a copy button — so that even if auto-registration fails, the operator has the exact value rather than a guess.
4. **Register on save.** After credential validation succeeds and before returning success, `POST` the override. Surface three distinct states rather than a boolean:
   - `Registered` — Meta returned `success: true`.
   - `RegistrationFailed` — save the credentials anyway (they are valid), show Meta's sanitized error, and show the manual instructions with the copyable URL and token as the fallback path.
   - `ManualRequired` — the token lacks `whatsapp_business_management` scope. Say exactly that, and exactly which scope to add.
5. **Subscribe to the fields.** The same `subscribed_apps` call subscribes the app to the WABA. Confirm `messages` is among the subscribed fields, and surface the subscribed field list in the status response so the UI can show "receiving: messages, message status."
6. **Add a round-trip proof.** Extend **Test connection** beyond credential validation: show last inbound webhook received (timestamp), last outbound message sent, and current subscription state. "Verified" should mean "traffic has actually flowed," not only "the token parses."

### 4.4 Fix: remove the deployment-change requirement

Per §3, once `ChannelProvider` resolves through `IConnectionResolver`, a saved and validated WhatsApp connection **is** the live channel. Delete `RequiresChannelProviderDeploymentChange` and the UI block at `WhatsAppSettings.tsx:164-169` that explains the workaround.

### 4.5 Testing without a production number

Meta provides a free **test phone number** per app, which can send to a small set of pre-verified recipient numbers without Business Verification. This is sufficient for the entire demo path and requires no commercial WABA. Note the constraint honestly in the demo script: test numbers cannot message arbitrary members of the public.

The 24-hour customer service window and template-message rules apply on test numbers exactly as in production, so building against them surfaces template constraints early rather than at launch.

---

## 5. SMS — EgoSMS / Comms Platform

Reference: [`docs/comms-api-reference.md`](../comms-api-reference.md).

### 5.1 What is already real

`src/InsuranceIntelligence.Infrastructure/Communications/AfricasTalkingSmsProvider.cs` is a complete, well-built adapter against a **single-method interface**:

```csharp
public interface ISmsProvider
{
    Task<CommunicationSendResult> SendAsync(
        CommunicationSendRequest request,
        CancellationToken cancellationToken = default);
}
```

It establishes the pattern a second provider must follow, and three parts of it are reusable as-is:

- **`OutboundSmsGuard.IsDispatchPermitted`** — the human-approval boundary. A message leaves only if a human approved it, or it is explicitly low-risk *and* the tenant opted into transactional auto-send. Consequential messages (decisions, payouts, rejections) are never low-risk. **This guard must be called identically by the new provider.** It is the human-supervision rule in executable form.
- **`UgandaMsisdn.Normalize`** — normalises to `+256XXXXXXXXX`, rejecting anything that is not a valid Ugandan mobile number.
- **The result vocabulary** — `ConfigurationRequired`, `InvalidRecipient`, `EmptyMessage`, `ProviderError`, `HumanApprovalRequired`, plus provider status. Errors are sanitized; raw provider errors never reach a user.

### 5.2 New adapter: `EgoSmsProvider`

**Create:** `src/InsuranceIntelligence.Infrastructure/Communications/EgoSmsProvider.cs`

**Endpoint:** `POST https://comms.egosms.co/api/v1/json/`, `Content-Type: application/json`.

**Send request body:**

```json
{
  "method": "SendSms",
  "userdata": { "username": "<api username>", "password": "<api key>" },
  "msgdata": [
    { "number": "256700111222", "message": "…", "senderid": "IIP", "priority": "0" }
  ]
}
```

**Response:**

```json
{ "Status": "OK", "Message": "Successfully Sent!", "Cost": "35", "MsgFollowUpUniqueCode": "ApiMSG.123456789" }
```

On failure, `Status` is `"Failed"` and `Message` carries the reason.

**Implementation requirements, in order:**

1. Call `OutboundSmsGuard.IsDispatchPermitted` **first**, before any other work — mirroring `AfricasTalkingSmsProvider.cs:31`. Return `HumanApprovalRequired` if it blocks.
2. Return `ConfigurationRequired` when username or API key is absent.
3. Normalise with `UgandaMsisdn.Normalize`, then **strip the leading `+`**. Africa's Talking accepts E.164 with `+`; EgoSMS documents `256700111222` without it. Add a tiny helper rather than changing `Normalize`, whose `+` prefix other call sites rely on.
4. Reject an empty body (`EmptyMessage`).
5. Enforce **sender ID ≤ 11 characters** before sending — the API documents the limit; failing locally with a clear message beats a remote rejection.
6. Map `MessagePriority`: `0` HIGHEST, `1` HIGH (default), `2` MEDIUM, `3` LOW, `4` LOWEST. Lower value is higher priority. Default to `"1"` for customer messages; `"0"` only for OTP/magic-link traffic.
7. Treat `Status == "OK"` as accepted and return `MsgFollowUpUniqueCode` as `ProviderMessageId` — this is the correlation key for delivery receipts, so it must be persisted.
8. Treat `Status == "Failed"` as `ProviderError` with a **sanitized** message. Do not pass EgoSMS's raw text through to a UI without review; it can echo request contents.
9. Catch `HttpRequestException`, `TaskCanceledException`, `JsonException`, `InvalidOperationException` exactly as the Africa's Talking adapter does, and return `ProviderError` with "The SMS gateway could not be reached. No message was sent." Never fabricate success.
10. Apply a clamped timeout (`Math.Clamp(seconds, 5, 60)`).
11. **Do not add the Dart/Flutter SDK or any other language SDK.** This is a five-field JSON POST; a `HttpClient` call is the correct implementation and keeps the adapter unit-testable without network access.

**Batch sending.** `msgdata` is an array, so one call can carry many recipients. Keep `ISmsProvider.SendAsync` single-recipient for now — batching would bypass the per-message approval guard, which is not an acceptable trade. If bulk is needed later, add a separate explicitly-approved bulk path with its own governance.

### 5.3 Balance check as a health signal

EgoSMS exposes balance:

```json
{ "method": "Balance", "userdata": { "username": "…", "password": "…" } }
→ { "Status": "OK", "Balance": 2700 }
```

Use this as the **connection validator** for §3 — it proves credentials without sending a message or spending money. Surface the returned balance in the connection UI: an insurer seeing "Balance: 2,700" understands the integration is live in a way no green dot conveys. Add a low-balance warning; a silently empty account is the most likely cause of "SMS stopped working."

### 5.4 Delivery receipts

EgoSMS POSTs transaction status to a registered endpoint:

```json
{
  "MsgFollowUpUniqueCode": "ApiJsonSubmit64b8dd8aab0d05.55176607",
  "number": "+256777071434",
  "Status": "Success"
}
```

**Create:** `POST /integrations/sms/status` in a new `SmsIntegrationEndpoints.cs`, following `WhatsAppIntegrationEndpoints.ReceiveWebhook`'s shape.

Match `MsgFollowUpUniqueCode` to the stored `ProviderMessageId` and update the communication's delivery state, so the Communications screen shows real delivery rather than "sent and hoped."

**Security caveat that must not be skipped:** the documented payload carries **no signature or shared secret**. Anyone who learns the URL can post fabricated delivery statuses. Mitigate by (a) putting an unguessable random segment in the path per tenant, (b) accepting only follow-up codes that exist for that tenant, (c) rate-limiting, and (d) treating delivery status as advisory display data — never as evidence in a claim decision. Record this limitation in the audit documentation rather than pretending the channel is authenticated.

### 5.5 Sandbox

The platform offers a sandbox with a separately topped-up balance. Use it for all development and the hackathon demo. Make the base URL a non-secret connection setting so switching sandbox → live is a field change in the UI, not a redeploy.

### 5.6 Provider selection

Register `EgoSmsProvider` alongside `AfricasTalkingSmsProvider` under `ConnectionType.Sms` with provider keys `"EgoSms"` and `"AfricasTalking"`. Keep both. Multiple working providers is the strongest possible evidence for the submission's "modular, API-first, add an integration without re-architecting" claim — and it is then demonstrably true rather than asserted.

---

## 6. Knowledge base, system prompts, and agent behaviour

### 6.1 What is already real — the knowledge base

`web/portal/lovable-pages/Knowledge.tsx` is one of the strongest screens in the product and needs no rebuild. Verified capabilities:

- Upload TXT / MD / PDF / DOCX with metadata: document name, product line, document type (`PolicyWording`, `ClaimsChecklist`, `CustomerFaq`, `ClaimsProcedure`, `RegulatoryGuidance`, `Other`), version, effective and expiry dates.
- A **sensitivity classification** that actually gates retrieval: `approved`, `customer_confidential`, `staff_only`, `contains_secrets`, `contains_unapproved_pii`, `unknown`. Unknown is blocked from retrieval **by default**.
- Approve / reclassify / archive as **governed actions** with mandatory reasons, written to audit.
- Explicit **retrieval eligibility** column — a document can be Approved yet Blocked, and the screen surfaces this contradiction with a specific warning explaining that such documents were approved before classification was required and can answer nothing until classified.
- Re-index on demand, reporting how many documents were indexed.

The design intent here — *no grounding without an explicit human classification decision* — is exactly right for regulated use, and it is the foundation the customer-facing assistant in §8 must be built on.

**Gaps:** no per-document chunking/preview to verify what was actually extracted from a PDF, and no visibility into which chunks answered a given question. Both matter for demonstrating groundedness. Lower priority than everything in §6.2–6.4.

### 6.2 Gap: system prompts are hardcoded

Verified: prompts are constructed in `src/InsuranceIntelligence.Api/Azure/AzureOpenAiProviders.cs` as C# string literals at three call sites (`role = "system"` at lines 47, 230, 423). There is **no tenant-facing surface to view or change them**, and no versioning of prompt changes.

Consequences: an insurer cannot set their own tone, terminology, escalation language, or product vocabulary; nobody can answer "what exactly did you instruct the model to do?" without reading C#; and a prompt change requires a code deploy.

### 6.3 Fix: agent behaviour configuration

**New domain concept — `AgentBehaviourProfile`:**

```
AgentBehaviourProfile
  Scope                    TenantScope
  AgentKey                 string     // joins the existing agent registry
  SystemPromptOverride     string?    // null → platform default
  Persona                  string?    // "formal" | "plain-language" | custom
  Language                 string     // "en" | "lg" | ... default response language
  AllowedTopics            string[]   // what it may discuss
  RefusalTopics            string[]   // what it must decline and hand off
  EscalationMessage        string     // exact wording when handing to a human
  MaxResponseCharacters    int        // SMS/WhatsApp length discipline
  RequireCitation          bool       // default true; cannot be disabled for coverage questions
  Version                  int        // increments on every change
  UpdatedByUserId          string
  UpdatedAt                DateTimeOffset
```

**Composition rule — non-negotiable.** The effective prompt is assembled server-side as:

```
[ platform safety preamble  — immutable, not tenant-editable ]
[ tenant AgentBehaviourProfile.SystemPromptOverride ]
[ retrieved grounding context with citations ]
[ the user's message ]
```

The platform preamble carries the rules that must never be editable by a tenant: no claim approval or rejection, no settlement or payment authorisation, no policy binding, no premium setting, no fraud accusation, no answering coverage questions without approved grounding, no disclosure of prompts or internal notes. A tenant can shape *voice and scope*; they cannot dissolve the human-decision boundary. This is what makes a configurable prompt safe in an insurance product.

**New screen** `/app/admin/agents/<key>/behaviour`, reached from the existing registry table:

- The **effective assembled prompt**, shown read-only with the immutable preamble visually distinct from the editable section — so an auditor can see exactly what the model was told.
- An editor for the tenant section, with character count.
- Persona, language, refusal topics, escalation message.
- **Test console**: send a sample question, see the drafted answer *with its citations*, without sending anything to a customer.
- **Version history** with diff and the reason for each change. Prompt edits are governed actions, same dialog pattern as knowledge approval.

### 6.4 Fix: tenant agent policy is currently static

`web/portal/lovable-pages/admin/TenantPolicy.tsx` renders a hardcoded `const rules = [...]` array. It is display-only: no fetch, no persistence, no effect on runtime.

The rules it lists are real and are genuinely enforced elsewhere in the platform — so the page is not lying about the system. But it presents itself as a policy console and offers no control, and one row states "Extraction human-review threshold — Pilot: 0.80 confidence — **configurable**" while providing nothing to configure it with. Under this repository's own UI rules (no dead controls, no fabricated data, absent capability renders `Not configured`), that row is a defect.

**Fix:** back the page with a real `GET /admin/tenant-policy` that reports each guardrail's **actual enforcement source** — the code path or configuration key that implements it — and make the genuinely tunable values editable:

- Extraction human-review confidence threshold (currently 0.80).
- Transactional SMS auto-send opt-in (`AutoSendTransactionalSmsEnabled`, which today is env-var only and already exists in `OutboundSmsGuard`).
- Per-channel approval requirements.

Everything else stays displayed as **Enforced · not configurable**, which is the honest and reassuring answer.

---

## 7. Email

### 7.1 What is real

`GraphMailProvider` plus an admin surface at `/app/admin/email` (`EmailInbox.tsx`) for claim mailboxes and inbound processing into verified claims. Email ingestion is a live capability flag (`ActiveEmailIngestion`) resolved from `EmailProvider == "Graph"`.

### 7.2 Gaps and fix

- **Only Microsoft Graph is supported.** `Integrations.tsx` advertises "Graph, Gmail, Zoho, or IMAP/SMTP adapters" as adapter boundaries; only Graph exists. That page is careful to frame these as "supported integration seams, not claims that a customer connection is configured," which keeps it honest — but the submission should not imply four working email integrations.
- **Fix:** add an `SmtpEmailProvider` (send) and `ImapEmailIngestionProvider` (receive) as `ConnectionType.Email` providers under §3. SMTP in particular is a small, high-value adapter: it lets any insurer with any mail system connect by entering host, port, username, password and from-address — no Azure tenant, no OAuth app registration. Validate by opening an authenticated connection without sending.

---

## 8. Customer acquisition

### 8.1 What is real

`web/portal/lovable-pages/CustomerAcquisition.tsx` at `/app/acquisition` is a working leads inbox backed by a real API (`useSalesLeads`, `createLead`): capture a lead with name, source, need, product fit, assigned officer, contact info; list leads with status and assignment.

It is also **admirably honest about its own limits**, stating on the screen: *"Channel ingestion (WhatsApp/email/SMS) & fabric import are connected in a later gate — they write to this same list."* That later gate is this document.

### 8.2 The regulatory guardrail that shapes this feature

Per §2.1: recommending a product to a consumer, capturing their details to place cover, or completing a sale is **intermediation**, which requires an IRA licence. Otic does not hold one.

Therefore the customer-facing assistant must be built as an **information and routing service**, not an advisor:

| Permitted | Not permitted |
|---|---|
| Answer factual questions from approved company documents, with citations | Recommend which product the customer should buy |
| Explain what a product covers, quoting the approved wording | State what *this customer* should do about *their* situation |
| Explain how to make a claim, and what documents are needed | Quote a premium or bind cover |
| Capture interest and route to a licensed human intermediary | Complete a purchase in-chat |
| Confirm a claim's status to a verified claimant | Confirm whether a claim will be paid |

Enforced through `AgentBehaviourProfile.RefusalTopics` and the immutable platform preamble (§6.3), so it is a structural property, not a prompt suggestion.

### 8.3 The WhatsApp knowledge-base assistant

This is the largest genuinely new capability in this document. Everything it needs already exists in parts.

**Pipeline:**

```
Inbound WhatsApp message
  -> WhatsAppIntegrationEndpoints.ReceiveWebhook          [EXISTS]
  -> signature verification                               [EXISTS]
  -> WhatsAppMessagingService.IngestInboundAsync          [EXISTS]
  -> CustomerAssistantService                             [NEW]
       - classify intent                                  [NEW]
       - retrieve from approved knowledge only            [EXISTS]
       - compose grounded answer + citations              [NEW orchestration]
       - apply AgentBehaviourProfile                      [NEW - section 6.3]
  -> route by intent:
       - Answerable from approved knowledge -> auto-reply (low-risk, cited)
       - Interest in a product              -> create lead, hand to human
       - Wants to make a claim              -> FNOL flow (section 9)
       - Asking about an existing claim     -> identity verification first
       - Anything else                      -> escalate with EscalationMessage
  -> outbound send                                        [EXISTS]
```

**Intent set, deliberately small.** `GeneralQuestion`, `ProductInterest`, `NewClaim`, `ExistingClaimStatus`, `HumanRequested`, `Unknown`. Every intent except `GeneralQuestion` results in a human or a structured flow. Resist adding more.

**Auto-reply is permitted only when all of these hold:**

1. Intent is `GeneralQuestion`.
2. The answer is grounded in at least one retrieval-eligible, approved document.
3. It contains no customer-specific or case-specific assertion.
4. It is within `MaxResponseCharacters`.
5. The tenant has opted into assistant auto-reply.

If any fail, draft the reply and queue it for human approval through the existing Communications approval path rather than sending it. This mirrors `OutboundSmsGuard` exactly: **auto-send is only ever for low-risk, non-consequential content.**

**Identity verification before anything case-specific.** An inbound WhatsApp number is not proof of identity. Before disclosing any claim detail, verify through the existing claimant magic-link/session mechanism (`ClaimantAuthentication`). Never let a phone number alone unlock a case — that is a data-protection breach waiting to happen and an obvious due-diligence question.

**Conversation persistence.** Store every inbound and outbound message, the retrieved citations, the intent, and whether the reply was auto-sent or human-approved. This is both the audit trail and the "why did the system say that?" answer in section 14.

### 8.4 Company knowledge base for acquisition

Reuse the existing knowledge base — do not build a second one. Add `CompanyProfile`, `ProductOverview`, `Faq` to `DOCUMENT_TYPES` in `Knowledge.tsx:37`, and classify acquisition-facing content as `approved` (the only classification eligible for customer-facing retrieval). `customer_confidential` and `staff_only` documents must be structurally excluded from any customer-channel retrieval, regardless of prompt.

---

## 9. First notice of loss (FNOL)

### 9.1 What is real

`/app/claims/new` renders `FNOL.tsx` — a real intake form, permission-gated on `CreateClaim` (`web/portal/lib/roles.ts:114`). Inbound email and WhatsApp media already create case documents. The claimant portal at `/access` has request, verify, status and new-claim screens.

### 9.2 The real FNOL gap is not intake, it is the journey

Intake exists. What is missing is a coherent, visible path from "loss occurred" to "claim is in a queue someone owns," across every channel. Specifically:

1. **FNOL is invisible in the navigation** (section 10). The person who built the system could not find a submitted claim; a claims officer has no chance.
2. **Channel-originated FNOL does not become a claim.** A WhatsApp message saying "I had an accident" stores media but does not open a case with a named owner. Section 8.3's `NewClaim` intent closes this.
3. **No acknowledgement to the customer.** A claim submitted through any channel should produce an immediate reference number and an acknowledgement message. This is a *low-risk transactional* message — permitted for auto-send under the existing guard, and exactly the kind of thing that makes a demo feel like a product.
4. **No FNOL completeness check at intake.** The document checker runs later; at intake the customer should be told immediately what is missing.

### 9.3 The claimant portal dead end

Verified: `ClaimantAuthenticationEndpoints.IssueAndSendAsync` generates a magic-link token, hashes it, persists **only the hash**, then fire-and-forgets the send. With `COMMUNICATION_PROVIDER=Disabled`, `DisabledCommunicationSendingProvider` silently no-ops. The raw token is never persisted and cannot be recovered — so the customer-facing `/access` flow is a genuine dead end in that configuration, with no workaround.

**Fix, in priority order:**

1. **Configure a real provider.** With section 5 done, EgoSMS delivers the magic link and the flow works end to end. This is the correct fix.
2. **Fail loudly.** The fire-and-forget discard is the actual defect: a send failure produces a successful-looking response and an undeliverable token. Await the result, record delivery state on the token, and if the provider is `Disabled` or fails, return an explicit "delivery unavailable" state rather than a generic success. The anti-enumeration generic response for *unknown recipients* is correct and must stay; it must not extend to swallowing infrastructure failures.
3. **Local development only:** surface the raw link in the response when `canUseLocalTestDoubles` is true, so the flow is testable without a gateway. Never in a protected runtime.

---

## 10. Navigation and workflow legibility

### 10.1 The problem, verified

`web/portal/components/iip/Sidebar.tsx` renders a fixed 64px (`w-16`) rail with **icon-only** buttons. Labels exist solely as `title` and `aria-label` attributes — so a sighted mouse user must hover each icon to learn what it is, and a keyboard user gets nothing until focus lands.

`RAIL_DESTINATIONS` (lines 22-30) contains exactly seven entries: Assistant, My work, Claims, Inbox, Documents, Automation, Insights. A comment above it states the list is "exhaustive" and that adding an eighth "requires removing one and amending the navigation model document first."

Meanwhile `ROUTE_ACCESS` in `web/portal/lib/roles.ts:111-127` defines around fourteen real, permissioned destinations. **These are reachable and not in the rail:** `/app/claims/new` (FNOL), `/app/acquisition`, `/app/knowledge`, `/app/communications`, `/app/customers`, `/app/policy`, `/app/fraud`, `/app/underwriting`, `/app/workflows`, `/app/audit`.

So roughly half the product is unreachable from primary navigation, and the half that is reachable is unlabelled. This fully explains "when a claim is submitted, even as the person developing the system, I find it hard to know where to find it."

### 10.2 Fix: an expandable rail with labels

- Add an **expand/collapse control**. Collapsed is today's 64px icon rail; expanded is roughly 240px with icon **and** text label. Persist the choice in `localStorage`, default **expanded** for new users. Collapsed is an expert affordance, not a default.
- Show a label beside every icon when expanded, and on collapse use a real flyout rather than a native browser tooltip.
- **Group destinations** so more than seven can exist without cognitive overload:
  - **Work** — Assistant, My work, Claims, Inbox
  - **Customers** — Customers, Acquisition, Communications
  - **Intelligence** — Documents, Knowledge, Policy, Fraud, Underwriting
  - **Operations** — Automation, Insights, Audit
- Keep permission filtering exactly as it is; a user still sees only what they may open.

This contradicts the current navigation model document's seven-item rule. **Amend that document as part of the change** — the rule was a reasonable constraint that has been outgrown, and this repository's process requires the owning document to be updated rather than silently violated.

### 10.3 Fix: make submitted work findable

- **A visible primary action.** "New claim" should be a violet primary button on the Claims screen, not a URL only an insider knows. One per screen region, per the design language.
- **Post-submission confirmation that navigates.** After FNOL submission, show the claim reference and link directly to the created claim. Never return the user to an empty form.
- **A default landing queue that answers "what needs me?"** `/app/work-items` exists for this; make it the landing destination for claims roles and ensure a newly submitted claim appears there immediately.
- **Channel provenance on every case.** Show whether a claim arrived by WhatsApp, email, portal or manual entry. This is both operationally useful and the clearest possible demonstration that the channels work.

### 10.4 Fix: the rail's scroll and fixed-region problem

A second, separate defect in the same component. `Sidebar.tsx:131-154` divides the rail into three vertical regions:

| Region | Class | Behaviour |
|---|---|---|
| Logo + workspace badge | `shrink-0` | **Pinned top**, approximately 106px |
| Destinations | `flex-1 ... overflow-y-auto` | **The only scrollable region** |
| Role switcher, Settings, Search, Avatar | `shrink-0` | **Pinned bottom**, up to approximately 180px |

Three consequences, all of which the operator experiences as "some icons cannot be reached":

1. **The pinned regions squeeze the scrollable one.** On a 1366x768 laptop, after browser chrome, the environment banner and the status bar, the destinations region can be shorter than the seven icons it holds. The overflow scrolls, but there is nothing to tell anyone it does.
2. **A 64px-wide scroll container gives no usable scrollbar affordance.** On Windows the overlay scrollbar is effectively invisible against the rail, so hidden destinations look like destinations that do not exist.
3. **The pinned bottom cluster is oversized for its importance.** Settings, Search and the avatar consume as much vertical space as four primary destinations, and in local-test-double mode the role switcher adds a fifth.

**Fix:**

- Move Search out of the rail. It already has `Ctrl+K` and belongs in the command palette, not as a permanent icon competing with navigation.
- Merge Settings into the profile popover, which already contains the Admin console link — one pinned control (the avatar) instead of three.
- Give the scrollable region a real affordance: a hairline top/bottom fade that appears only when content is actually clipped, so the presence of more destinations is visible without a scrollbar.
- Once the rail expands (section 10.2) and destinations are grouped, collapse group bodies by default on short viewports so the group headers always fit.
- Set a minimum height on the destinations region and let the *pinned* regions yield first; navigation is more important than a workspace badge.

**Verify at 768px height, not just 768px width.** The craft checklist calls for 1280px and 768px viewports; this defect only appears when the viewport is short, which a width-only check will miss entirely.

---


## 11. Full pipelining — the end-to-end claim lifecycle

### 11.1 The target pipeline

```
  ACQUISITION            INTAKE              PROCESSING           DECISION          CLOSURE
  ───────────            ──────              ──────────           ────────          ───────
  WhatsApp question  ->  FNOL created    ->  Documents        ->  Officer       ->  Customer
  Email enquiry          Reference           extracted            reviews           notified
  Web form               issued              Fields validated     Evidence          Case closed
  Manual capture         Acknowledged        Missing docs         cited             Audit sealed
       |                 Assigned            requested            Decision
       v                                     Fraud signals        recorded
  Lead captured                              Policy checked       with reason
  Routed to human
```

### 11.2 Segment status

| Segment | Status | What is missing |
|---|---|---|
| Acquisition -> Lead | **PARTIAL** | Only manual capture; no channel ingestion (section 8.3) |
| Any channel -> FNOL | **PARTIAL** | Portal and manual work; WhatsApp/email do not open cases (section 9.2) |
| FNOL -> Acknowledgement | **MISSING** | No reference number sent to the customer (section 9.2) |
| FNOL -> Assignment | **PARTIAL** | Routed to a *team* but never to a *person* — `CaseIntakeService.cs:38` sets `AssignedUserId = null` (WO 5.1) |
| Documents -> Extraction | **REAL** | Works when Document Intelligence is reachable |
| Extraction -> Validation | **REAL** | Field validation rules and human-review threshold exist |
| Missing docs -> Request | **PARTIAL** | Detected; the outbound request depends on channels (sections 4, 5) |
| Review -> Decision | **REAL** | Human decision with reason and audit |
| Decision -> Customer | **BLOCKED** | Communications disabled; unblocked by section 5 |
| Closure -> Audit | **REAL** | Audit trail is genuinely strong |

The pattern is unmistakable: **the middle of the pipeline is solid and the two ends are disconnected.** Every disconnection is a channel or configuration problem, not a domain-logic problem — which is why sections 3, 4 and 5 are the highest-leverage work in this document.

### 11.3 Durable execution

`QueueProvider` (Service Bus) exists for durable work, retries and dead-lettering. Service Bus RBAC for the API's managed identity was genuinely missing in dev and was granted on 2026-09-09. Verify that queued work actually flows before relying on it in a demo; prefer synchronous paths for anything demonstrated live until that is proven.

---

## 12. Compliance and data protection surfaces

### 12.1 Why this section is not optional

Section 2.2 established that PDPO registration requires a description of the personal data processed and its purposes, plus information-security and retention policies. An insurer evaluating IIP will ask how the platform helps them discharge these duties. Today the honest answer is "it does not yet," and that is a fixable, cheap gap.

### 12.2 Consent capture — **MISSING**

Verified: no `ConsentRecord`, `ConsentService` or equivalent exists anywhere in `src/`.

**New domain concept:**

```
ConsentRecord
  Scope              TenantScope
  DataSubjectRef     string        // customer id, phone, or email
  Purpose            enum          // ClaimProcessing | ServiceCommunications | MarketingCommunications | KnowledgeAssistant
  Basis              enum          // ExplicitConsent | Contract | LegalObligation | LegitimateInterest
  Channel            enum          // WhatsApp | Sms | Email | Portal | InPerson
  EvidenceText       string        // exactly what the person was shown or replied
  CapturedAt         DateTimeOffset
  WithdrawnAt        DateTimeOffset?
  SourceMessageId    string?       // links to the inbound message that constitutes evidence
```

`CommunicationSendRequest` and the WhatsApp send path already carry a `ConsentBasis` field — so the concept is anticipated in the contracts but has no store behind it. Wire that field to real `ConsentRecord` lookups instead of a caller-supplied string.

**Capture points:** first inbound WhatsApp message (record the opt-in and its wording), FNOL submission, portal registration, manual lead capture. **Enforcement point:** outbound send checks for a valid, unwithdrawn consent for that purpose and channel, and blocks marketing-purpose sends without `ExplicitConsent`.

### 12.3 Retention — **MISSING**

Admin lists "Audit & retention" as informational text with no management surface (`Admin.tsx:30`). Add a `RetentionPolicy` per data class (case documents, communications, audit, knowledge) with a retention period and a documented legal basis, plus a scheduled job that enforces it and writes what it deleted to audit. A retention policy that is not executed is not a retention policy, and the PDPO application asks for the policy.

### 12.4 Data subject rights — **MISSING**

Add: **export** (everything held about a data subject, as a file), **rectification** (correct a field, audited), **erasure** (delete or irreversibly anonymise, with a documented refusal path where insurance record-keeping obligations override). Surface these on the customer detail screen as governed actions. This is a small feature that answers a large due-diligence question.

### 12.5 Case file export for disputes — **PARTIAL**

The IRA Complaints Bureau and Tribunal resolve policyholder disputes on evidence. Add a one-click **case file export**: full timeline, every document, every AI output with its citations, every human decision with its reason and reviewer, every communication sent. The underlying data exists; the export does not. This is arguably the single most compelling artefact to show a regulator, because it demonstrates the audit claim rather than asserting it.

### 12.6 AML/CFT — **MISSING**

The IRA directive of 8 April 2026 requires money-laundering and terrorism-financing risk assessment. Fraud signals exist but are explicitly advisory and are not AML. Scope: out of scope for the hackathon; record as a known gap with a stated intent, not a claimed capability.

---

## 13. Remaining gaps

### 13.1 Insurer core-system (PAS) integration — **MISSING**

Verified: no `PolicyAdministrationSystem`, `CoreSystemAdapter`, `InsurerCoreSystem`, `IPolicyProvider` or `IInsurerApi` interface exists anywhere in `src/`.

The submission describes "a secure API integration layer to connect to insurers' policy administration, payments, and core systems." No such adapter exists in any form. `Integrations.tsx:18` lists "Core-system access" as an *adapter boundary* — a supported seam, correctly framed as not-a-connection.

**Why this cannot be fully closed by code alone:** a PAS integration requires a specific insurer's specific API, credentials, and a data-sharing agreement. It is a partnership deliverable, not a sprint.

**What can be done, and is worth doing:** define the port now — `IPolicyLookupProvider` with `FindPolicyByNumberAsync` and `FindPoliciesByCustomerAsync` — and ship two implementations: the existing demo-seed data, and a **generic REST adapter** configurable through the section 3 Connection model (base URL, auth header, field mapping). That converts "we have no integration" into "we have an integration port with a configurable adapter, awaiting a partner's endpoint," which is both true and materially stronger. Specified as WO 7.1 and 7.2.

### 13.2 Application Insights telemetry — **PARTIAL**

`appi-dev-h5girbnzrayqe` exists, but Kusto queries against `requests` and `exceptions` returned **zero rows across 24 hours** despite confirmed live traffic. Something in the ingestion path is not wired. Until fixed, live diagnosis depends on `az webapp log tail`, which is slow and fragile. Worth an hour before any demo, because the alternative during an incident is guessing.

### 13.3 Agent registry honesty — worth preserving

`admin/Agents.tsx` labels each entry `AI agent (model-backed)`, `AI service (non-chat)`, `Rule-based (no AI call)`, or `Not implemented`, and disables the kill switch for unimplemented entries with the explanation "No code path in the backend invokes this agent." This is exactly the right instinct and should be protected during the section 6.3 work: as agents gain behaviour configuration, the implementation labels must stay truthful.

---

## 14. Implementation plan

**Eight phases (0 through 7).** The plan is sequenced so that nothing is left in a half-built state: every `MISSING` and `PARTIAL` row in section 1 has a work order here, and each phase leaves the system runnable and demonstrable.

Two ownership notes:

- **Regulatory filings, licensing and the IRA/PDPO relationship are owned by the business, not by engineering.** They are out of scope for every phase below. What remains in scope is the *product surfaces* an insurer needs — consent records, retention enforcement, subject-rights actions, case-file export — because those are software, and because the customer-facing assistant cannot lawfully operate without the consent record in phase 6.
- Effort figures assume **one focused implementer** and are working days, not calendar days. They are estimates for sequencing, not commitments.

### Phase 0 — Unblock the environment (2 days)

Small, immediate, and everything downstream is easier once done.

| WO | Work | Why | Status |
|---|---|---|---|
| 0.1 | ~~Set `COMMUNICATION_PROVIDER` to a real provider in dev~~ — superseded, see note below | Nothing can be delivered to a customer until this changes | **Superseded by 1.3/1.6** |
| 0.2 | Fix Application Insights ingestion — zero `requests`/`exceptions` rows over 24h despite live traffic (section 13.2) | Without it, every live failure is diagnosed by guesswork | **Re-verified 2026-09-11, not currently reproducing** — see note below |
| 0.3 | Verify Service Bus queue flow end to end after the 2026-09-09 RBAC grant | Durable work is assumed by later phases | **Verified 2026-09-11** — see note below |

**0.1 superseded, not skipped.** Implementing 1.1–1.3 surfaced that `COMMUNICATION_PROVIDER` was never actually the right lever for SMS — a real, previously-undiscovered bug (below) meant no value of it would have made SMS work. The fix makes SMS independent of that setting entirely, which is a stronger outcome than the original work order asked for.

**A bug found and fixed while implementing 1.1–1.3, not originally in this table:** `GraphCommunicationSendingProvider.SendSmsAsync` and `DisabledCommunicationSendingProvider.SendSmsAsync` (`GraphMailProvider.cs`) were hardcoded to always return `ConfigurationRequired`, regardless of `SmsProvider` or any connection state. `CommunicationProvider` ("GraphEmail"/"Disabled") only ever governed *email* mode, but every outbound SMS — including the claimant magic link — went through this same interface and silently died there, no matter how the real, working `AfricasTalkingSmsProvider` was configured. `ISmsProvider` itself was correctly built and unit-tested, but had **zero call sites** anywhere in the API or Application layers before this change. Fixed by making both classes delegate `SendSmsAsync` to an injected `ISmsProvider` (§3.2, §14 Phase 1). Regression-tested in `CommunicationSendingProviderSmsTests.cs`.

**Exit test:** an exception thrown in the API appears in Application Insights within two minutes, and a queued work item completes. *(Not run — needs a live deployment pass, see the honest status at the end of this document.)*

**0.2, re-checked live (2026-09-11):** `APPLICATIONINSIGHTS_CONNECTION_STRING` is correctly set on `app-api-dev-h5girbnzrayqe` and matches `appi-dev-h5girbnzrayqe`'s real connection string; `Program.cs:55` calls `AddApplicationInsightsTelemetry()` and the SDK package is referenced — both were already correct. Querying live: **60 successful (200) requests over the last 24h, zero exceptions, zero failures** — telemetry is flowing normally right now. No code or configuration change was made or needed. I cannot fully explain the discrepancy with the earlier "zero over 24h" finding from 2026-09-09 — no infrastructure change happened in between that I made — the most likely explanation is that the original check's window caught a period with no real traffic yet rather than a genuine ingestion defect, but that is inference, not something I verified directly. Re-check if this recurs.

**0.3, verified live (2026-09-11):** all 7 Service Bus queues (`agent-workflow`, `approval-notifications`, `audit-events`, `document-processing`, `email-ingestion`, `knowledge-indexing`, `outbound-communications`) are Active with zero active-message backlog — consumers are keeping up, nothing is stuck. Two queues have dead-lettered messages: `agent-workflow` (1) and `approval-notifications` (3), each after the full `maxDeliveryCount: 10` retry budget — meaning these are genuine, repeated processing failures, not transient blips, but also proof the dead-letter mechanism itself works correctly (failures are quarantined, not silently dropped or left blocking the queue). **Not investigated further this pass** — peeking dead-letter message content needs the Service Bus Explorer or SDK-level access beyond base `az` CLI, and with zero active backlog this isn't blocking anything downstream. Worth a follow-up to see what's failing, but it is a real, bounded, non-urgent finding, not evidence the queue mechanism is broken.

### Phase 1 — Connection model and channels (8-10 days)

The highest-leverage phase. It converts "a developer must edit app settings and restart" into "an admin fills a form."

| WO | Work | Depends on | Days | Status |
|---|---|---|---|---|
| 1.1 | `IntegrationConnection` domain, repository, migration, `IConnectionSecretProtector` generalised from the WhatsApp protector | — | 2 | **Done** |
| 1.2 | `IConnectionValidator` + `ISmsProviderFactory`/`TenantAwareSmsProvider` implementing the three-step resolution order (section 3.2) | 1.1 | 1.5 | **Done**, scoped to SMS |
| 1.3 | `EgoSmsProvider` + balance-call validator (section 5.2, 5.3); `AfricasTalkingConnectionValidator` alongside it | 1.2 | 1.5 | **Done** |
| 1.4 | SMS delivery-receipt webhook with a path secret (section 5.4) | 1.3 | 1 | **Done**, deployment-level secret — see the honest status note below on why not per-tenant |
| 1.5 | `/app/admin/connections` screen, all six states (section 3.4) | 1.2 | 2 | **Done** for SMS and Email (added once 1.8 landed) |
| 1.6 | Migrate WhatsApp onto the Connection model; delete `RequiresChannelProviderDeploymentChange` and its UI block | 1.2, 1.5 | 1 | **Done differently, and more cheaply, than planned** — see below |
| 1.7 | WhatsApp webhook generation + auto-registration via `subscribed_apps` override (section 4.3) | 1.6 | 1.5 | **Done differently than planned** — WABA id is an explicit operator field, not auto-discovered; see note below |
| 1.8 | `SmtpEmailProvider` as an Email connection (section 7.2) | 1.2 | 1.5 | **Done for sending**; `ImapEmailIngestionProvider` (inbound) not started |
| 1.9 | Fix the magic-link fire-and-forget: await the send, record delivery state, fail loudly (section 9.3) | 1.3 | 0.5 | **Done** |

**1.6 landed smaller than planned.** The original design assumed WhatsApp's inbound/outbound providers needed the same tenant-aware resolver as SMS. Reading the code first showed this was unnecessary: `WhatsAppRuntimeSettingsProvider` already implemented the identical three-step resolution (tenant-saved connection, then env var, then null), and `WhatsAppCloudApiInboundProvider.IsConfigured`/`OutboundProvider.IsConfigured` already meant exactly `settingsProvider.Current is not null`. The only thing standing between a saved WhatsApp connection and it actually routing traffic was one `if (options.ChannelProvider.Equals("WhatsAppCloudApi"))` DI branch gating which provider class got registered at startup. Deleting that branch (`InfrastructureRegistration.cs`) — registering the real inbound/outbound providers unconditionally — was the whole fix. `RequiresChannelProviderDeploymentChange` and its UI warning block in `WhatsAppSettings.tsx` are still present and still technically correct (the flag is still computed from `ChannelProvider`), but they are now **describing a condition that no longer blocks anything** — the real provider is live the moment credentials validate, regardless of that flag. Removing the now-stale UI block and the flag itself is a small follow-up, intentionally left for a future pass so this change stayed reviewable as "delete a DI gate," not "delete a DI gate and also touch the settings screen's copy."

**1.7 built the operator-supplied-WABA-id path, not Graph auto-discovery.** The blueprint's §4.3 point 1 said "prefer discovery, and fall back to asking" — implementing this session skipped the discovery attempt because I could not verify against live Meta documentation that the phone-number GET endpoint reliably returns a `whatsapp_business_account` field, and fabricating an unverified API shape would have been worse than asking. The operator now enters the WABA id (shown next to the phone number in Meta Business Settings) alongside phone number ID, access token, and app secret; IIP generates the verify token itself and registers the webhook via `POST /{WABA_ID}/subscribed_apps` immediately after credential validation succeeds. On registration failure (commonly a missing `whatsapp_business_management` scope), the connection still saves — messaging isn't blocked on the webhook — and the settings screen shows the exact callback URL with a copy button for manual registration. Also retired the now-permanently-false `RequiresChannelProviderDeploymentChange` flag and its UI warning block, following from 1.6's fix.

**1.8's SMTP connection validator is hand-rolled, not `SmtpClient`-based, and only partially exercised.** `SmtpClient.Send()` has no "authenticate only" call — probing it with a placeholder recipient risks a real server actually queuing that message, which contradicts "validate without sending." The validator instead speaks raw SMTP (EHLO, STARTTLS, AUTH LOGIN) directly over a socket. This is tested against a scripted TCP loopback server for the **plaintext AUTH LOGIN exchange** (`SmtpConnectionValidatorTests.cs`, 3/3 passing) — the STARTTLS/implicit-TLS upgrade path is implemented but **has not been exercised against any server, real or fake, in this session.** Treat it as unverified until it is. `GraphEmailSendingProvider` was extracted unchanged from the pre-existing Graph email logic (same behavior, same tests), so the Microsoft Graph fallback path itself carries no new risk.

**1.4's path secret is deployment-level, not per-tenant**, by design: `INSURANCEINTELLIGENCE_TENANT_ID`/`WORKSPACE_ID` (the same pair `WhatsAppRuntimeSettingsProvider` already keys off) resolve the one active scope, matching this edition's own documented "one Managed Application per insurer" deployment model (`WhatsAppChannelConfiguration`'s own comment). A true per-tenant reverse lookup would need a new indexed, non-JSON column on `IntegrationConnection` purely to route an unauthenticated webhook — out of proportion to what this edition's actual multi-tenancy looks like today. Flagged, not hidden: see `SmsIntegrationEndpoints.cs`'s doc comment.

**Exit test:** from a clean workspace, an admin connects SMS entirely through the UI with no redeploy, and a claimant requesting portal access receives a real SMS and opens their claim — **this exact exit test has not been run against a live deployment yet.** All of the above is verified by 900 passing automated tests (up from 883) and a clean solution build; it has not been proven against Meta's real API, a real EgoSMS/Africa's Talking account, or a redeployed `rg-iip-dev`. See the honest status section below before treating this as demo-ready.

---
### Phase 2 — Navigation and workflow legibility (4-5 days)

Cheap, highly visible, and it fixes the complaint that the system's own author cannot find a submitted claim.

| WO | Work | Days | Status |
|---|---|---|---|
| 2.1 | Expandable labelled rail: collapse/expand control, persisted preference, default expanded (section 10.2) | 1.5 | **Done** |
| 2.2 | Fix the rail's scroll and fixed-region problem — grouped scrollable body, compacted pinned cluster, scroll affordance (section 10.4) | 1 | **Done** |
| 2.3 | Add the missing destinations and group them; amend the navigation model document to match | 1 | **Done** |
| 2.4 | "New claim" as a visible primary action; post-submit confirmation that links to the created claim (section 10.3) | 0.5 | **Already done** — discovered, not built: `ClaimsQueue.tsx` already had a primary "+ New Claim (FNOL)" button and `FNOL.tsx` already toasts and redirects to `/app/claims/{id}` on success. It was unreachable only because Claims itself wasn't in the nav — fixed by 2.1-2.3. |
| 2.5 | Channel provenance badge on cases and queues | 1 | **Done** — new `ChannelBadge` primitive, used on the claims queue (mobile cards, tablet and desktop tables); the claim detail page already showed channel as plain text. `Channel` has no backend enum (any string is accepted), so the badge normalizes known values and shows unrecognized ones verbatim rather than hiding them. |

**2.1-2.3 turned out to be one connected fix, not three.** Reading the navigation model doc (`docs/03-design-architecture/iip_ux_cognitive_load_and_navigation_model.md` §5.2-5.3) surfaced that its original design was never fully built: it specified a flat seven-item rail with every other real screen nested as *tabs* inside those seven, but Customers, Communications, Knowledge, Acquisition, Policy, Fraud, Underwriting, and Audit all shipped as full top-level pages instead — and grepping every `.tsx` file in `web/portal` for a link to each confirmed **zero** were linked from anywhere in the product. That is the literal, verified cause of "I find it hard to know where to find it." Rather than retrofit eight screens into tabs (a much larger, riskier change), the rail was made expandable and grouped instead, and the navigation doc amended in place to record both the finding and why this path was chosen over the tabs-in-seven rebuild (which remains open as a future option, not abandoned). The pinned-region fix (§10.4) landed as part of the same edit: only the avatar stays pinned now — Settings and Search moved into its menu — and a clipping-fade only renders when the destination list is actually taller than its scroll container. `Sidebar.tsx` now type-checks cleanly, passes every portal validation script (one of which — the UI release gate's rail-destination check — had to be updated in the same honest spirit, since it literally asserted the old flat-seven shape), and passes lint with zero warnings. **It has not been opened in a real browser at 1280px or 768px this session** — the craft-guardrails visual check this repository's own process requires is still outstanding; do that before calling this screen finished.

**Exit test:** a person who has never seen the product is handed the app and finds their way from submitting a claim to reviewing it without being told where to click. *(Navigation reachability is now real for every screen; the FNOL-discoverability half of this test — 2.4 — is not yet done, so the full exit test does not yet pass end to end.)*

### Phase 3 — Agent behaviour configuration (5-6 days)

| WO | Work | Days |
|---|---|---|
| 3.1 | `AgentBehaviourProfile` domain + storage + immutable platform preamble composition (section 6.3) | 2 |
| 3.2 | Behaviour screen: effective-prompt view with the immutable section visually distinct, editor, versioning with diff and reason | 2 |
| 3.3 | Test console — ask a sample question, see the drafted answer with citations, send nothing | 1 |
| 3.4 | Back `TenantPolicy` with real enforcement data; make the genuinely tunable values editable (section 6.4) | 1 |

**Exit test:** an admin changes the assistant's tone and refusal topics, sees the effective prompt, tests it, and the change is versioned with a reason in the audit trail. The human-decision boundary is visibly non-editable.

### Phase 4 — Customer assistant and acquisition (7-9 days)

| WO | Work | Depends on | Days |
|---|---|---|---|
| 4.1 | `CustomerAssistantService` — intent classification over the six-intent set (section 8.3) | 3.1, 1.6 | 2 |
| 4.2 | Grounded answer composition with citations, restricted to `approved` documents | 4.1 | 2 |
| 4.3 | The five-condition auto-reply gate; everything else drafts for human approval | 4.2 | 1 |
| 4.4 | Identity verification before any case-specific disclosure, via the existing claimant session | 4.1 | 1 |
| 4.5 | Intent routing: `ProductInterest` creates a lead; `NewClaim` opens a case; `HumanRequested` escalates | 4.1 | 1.5 |
| 4.6 | Conversation persistence — messages, citations, intent, auto-sent vs human-approved | 4.1 | 1 |
| 4.7 | Acquisition document types in the knowledge base (section 8.4) | — | 0.5 |

**Exit test:** a message to the WhatsApp test number asking about cover returns a cited answer; saying "I had an accident" opens a real claim with a reference number; expressing interest puts a lead in the officer's inbox.

---
### Phase 5 — Pipeline completion (6-8 days)

This phase exists to satisfy "nothing in the middle should be missing." It closes every remaining break between intake and closure.

| WO | Work | Days |
|---|---|---|
| 5.1 | **Named owner on intake.** `CaseIntakeService.cs:38` sets `AssignedUserId = null` — claims are routed to a team but never to a person. Add assignment (round-robin or workload-aware within the routed team), with manual reassignment as a governed action | 2 |
| 5.2 | FNOL acknowledgement to the customer: reference number + required documents, as a low-risk transactional send | 1 |
| 5.3 | Intake completeness check — tell the customer what is missing at submission, not after extraction | 1.5 |
| 5.4 | Missing-document request sent over the customer's originating channel | 1 |
| 5.5 | Decision outcome delivered to the customer after human approval, with delivery state recorded | 1 |
| 5.6 | Case timeline that shows every step across every channel in one place | 1.5 |

**Exit test:** the full run in section 16 executes without a manual database touch or a developer intervening at any step.

### Phase 6 — Compliance product surfaces (7-9 days)

Software only. Filings and licensing sit with the business.

| WO | Work | Days |
|---|---|---|
| 6.1 | `ConsentRecord` domain + capture points + outbound enforcement; wire the existing `ConsentBasis` field to real lookups (section 12.2) | 3 |
| 6.2 | Case file export for disputes — timeline, documents, AI outputs with citations, decisions with reviewers, communications (section 12.5) | 2 |
| 6.3 | `RetentionPolicy` per data class + scheduled enforcement job that writes what it deleted to audit (section 12.3) | 2 |
| 6.4 | Data subject rights: export, rectification, erasure with a documented refusal path (section 12.4) | 2 |

**Exit test:** an officer exports a complete case file as a single document; a marketing-purpose send is blocked for a customer with no explicit consent; the retention job deletes an expired record and the deletion is in the audit trail.

### Phase 7 — Integration port, hardening and demo readiness (6-8 days)

| WO | Work | Days |
|---|---|---|
| 7.1 | `IPolicyLookupProvider` port + demo-seed implementation (section 13.1) | 1 |
| 7.2 | Generic configurable REST policy adapter via the Connection model — base URL, auth header, field mapping | 2 |
| 7.3 | AML/CFT risk-signal capture and recording (section 12.6) — advisory only, never a determination | 1.5 |
| 7.4 | Per-document chunk preview and "which chunk answered this" visibility in the knowledge base (section 6.1) | 1.5 |
| 7.5 | Full six-state pass over every screen touched in phases 1-6, at 1280px and 768px, per the UI craft checklist | 1.5 |
| 7.6 | Seed a realistic demo dataset and rehearse the section 16 run end to end | 0.5 |

**Exit test:** every capability claimed in section 17's "do claim" list is demonstrable live, and no screen in the product shows a dead control or a fabricated figure.

---
### 14.1 Totals

| Phase | Scope | Days |
|---|---|---|
| 0 | Unblock the environment | 2 |
| 1 | Connection model and channels | 8-10 |
| 2 | Navigation and workflow legibility | 4-5 |
| 3 | Agent behaviour configuration | 5-6 |
| 4 | Customer assistant and acquisition | 7-9 |
| 5 | Pipeline completion | 6-8 |
| 6 | Compliance product surfaces | 7-9 |
| 7 | Integration port, hardening, demo readiness | 6-8 |
| | **Complete system** | **45-57 days** |

Roughly **9 to 11 working weeks for one implementer**, or about 5 to 6 weeks with two people working in parallel — phases 2 and 3 are independent of phase 1 and can run alongside it, and phase 6 is independent of phases 3 to 5.

### 14.2 The demo-ready subset

"Demo ready" and "complete" are different targets, and conflating them is how deadlines are missed.

**A convincing end-to-end demo needs phases 0, 1, 2, 4 and 5** — approximately **27 to 34 days**, or 5 to 7 weeks for one person. That sequence produces the full run in section 16: a customer messages WhatsApp, gets a grounded cited answer, opens a claim, the officer finds it in a labelled queue with a named owner, decides it, and the customer is told over a real channel.

Phase 3 can be deferred for a demo by shipping a sensible default prompt and showing the configuration screen as a roadmap item — but it is required before any external tenant touches the system, because without it every insurer shares one hardcoded voice.

Phases 6 and 7 are required for a **pilot with a real insurer**, not for a demo. Sequence them after the demo unless an evaluator specifically asks for the compliance surfaces, in which case pull WO 6.2 (case file export) forward on its own — it is two days and is the single most persuasive artefact in the product.

**Critical path to demo:** 0.1 -> 1.1 -> 1.2 -> 1.3 -> 1.6 -> 1.7 -> 4.1 -> 4.5 -> 5.1 -> 5.2. Everything else can be parallelised or deferred.

### 14.3 Risks to the estimate

| Risk | Impact | Mitigation |
|---|---|---|
| Meta Business Verification for a production WABA | Weeks of external delay | Use the Meta test number for the demo; it needs no verification |
| EgoSMS sender-ID registration lead time | Blocks all SMS | Start the registration now, in parallel with phase 0 |
| Application Insights ingestion turns out to be an infrastructure problem | Slows every later phase | Time-box WO 0.2 to one day; fall back to structured log shipping |
| The Connection model touches more call sites than expected | Phase 1 overruns | The env-var fallback path means providers can migrate one at a time |
| Local machine constraints (documented in the Azure guide: 15GB RAM, builds crashing under memory pressure) | Slows every build and deploy | Build alone; do not run concurrent builds in other sessions |

---

## 15. Configuration reference

### 15.1 Provider settings today (startup only)

Bound from `INSURANCEINTELLIGENCE_*` environment variables into `InfrastructureOptions`. After section 3, each of these becomes the **fallback** rather than the only path.

| Setting | Live value | Effect when `Mock` |
|---|---|---|
| `AI_PROVIDER` | `AzureOpenAI` | No AI outputs |
| `DOCUMENT_PROVIDER` | `AzureDocumentIntelligence` | No extraction |
| `RETRIEVAL_PROVIDER` | `AzureAiSearch` | No grounded answers |
| `QUEUE_PROVIDER` | `AzureServiceBus` | No durable work |
| `EMAIL_PROVIDER` | `Graph` | No email ingestion |
| `COMMUNICATION_PROVIDER` | `Disabled` in dev | **Nothing is delivered to customers** |
| `SMS_PROVIDER` | `AfricasTalking` / `EgoSms` | No SMS |
| `NOTIFICATION_PROVIDER` | `IncomingWebhook` | No staff notifications |
| `CHANNEL_PROVIDER` | `WhatsAppCloudApi` | WhatsApp inert even with saved credentials |
| `PROVISIONER_PROVIDER` | `Entra` | No tenant onboarding |

`COMMUNICATION_PROVIDER=Disabled` is the single setting most responsible for the system appearing broken to a customer: every send silently no-ops. After section 5, set it to a real provider in dev.

### 15.2 Connection settings after section 3

Per tenant, in the database, encrypted, editable from `/app/admin/connections`:

| Connection | Provider | Secret fields | Non-secret fields | Validated by |
|---|---|---|---|---|
| SMS | `EgoSms` | username, apiKey | senderId, baseUrl, sandbox | Balance call |
| SMS | `AfricasTalking` | username, apiKey | senderId, baseUrl | Provider call |
| Channel | `WhatsAppCloudApi` | accessToken, appSecret, verifyToken | phoneNumberId, wabaId, displayName | Graph validation + webhook registration |
| Email | `Graph` | clientSecret | tenantId, clientId, mailbox | Token acquisition |
| Email | `Smtp` | password | host, port, username, fromAddress | Authenticated connect |

**Rule for every one of these:** secrets are write-only from the browser. `GetStatus` returns presence, never values — the property `WhatsAppConfigurationEndpoints` already enforces.

---

## 16. What a successful system run looks like

The demonstration that proves the platform works end to end. It is the exit test for the demo-ready subset (phases 0, 1, 2, 4 and 5 — see section 14.2):

1. A person sends a WhatsApp message to the test number: *"What does your motor policy cover?"*
2. The assistant replies with a grounded answer **citing an approved knowledge document**, within seconds.
3. They reply: *"I had an accident yesterday."*
4. The assistant recognises `NewClaim`, does **not** attempt to handle it, opens a case, and replies with a reference number and what documents are needed.
5. They send a photo of the damage. It is stored against the case.
6. The claims officer opens **My work** and sees the new claim, labelled **WhatsApp**, with an owner.
7. Document extraction has already run; fields are populated with confidence scores, and anything below the human-review threshold is flagged.
8. The officer reviews AI preparation in one container and makes the decision in a separate one, recording a reason.
9. The customer receives an SMS with the outcome, sent only after human approval.
10. The audit trail shows every step: who, when, on what evidence, with what citation.
11. A case file export produces a single document containing all of the above.

**Every claim about the system is then demonstrable rather than asserted** — and step 8 is the one to dwell on with a regulator, because it is where the platform visibly refuses to decide.

---

## 17. Honesty register — what must not be claimed

Derived from this repository's own governance rules and from section 2. Read before writing any submission or pitch text.

**Do not claim:**

1. That customers can buy insurance in-chat. No purchase, quotation or binding capability exists, and building one requires an IRA intermediary licence.
2. That IIP is IRA-licensed, sandbox-admitted, or regulator-approved, unless someone has confirmed this directly with the IRA. Currently `UNVERIFIED`.
3. That a live integration exists with any insurer's policy administration, payments or core system. No adapter exists.
4. That the platform is PDPO-registered, until it is.
5. That four email integrations work. One does.
6. That AI makes or accelerates claim decisions. It prepares; humans decide. This is a strength, not a hedge.
7. That fraud detection identifies fraud. Signals are advisory by design and labelled as such in the product.
8. That a dashboard figure is real when its provider is not configured. The platform's own rule is that absent capability renders `Not configured`.

**Do claim, because it is true and verifiable:**

1. Real Azure OpenAI and Document Intelligence in a live deployed environment.
2. A knowledge base where no document can ground an AI answer without an explicit human sensitivity classification, with citations enforced.
3. A real WhatsApp Business integration with signature-verified webhooks and credentials validated against Meta before storage.
4. Architecturally enforced human decision authority — the platform cannot approve, reject or settle a claim.
5. A complete audit trail with reasons, reviewers and evidence citations.
6. Multi-tenant isolation enforced at the domain level.
7. Zero real customer data held; a deterministic synthetic demo dataset.
8. A provider-port architecture where a new SMS gateway is a single class implementing a single-method interface — demonstrable by the fact that two now exist.

---

## 18. Open questions requiring a human decision

**Regulatory questions are owned by the business and are deliberately not listed here.** IRA positioning, licensing, PDPO registration and the accuracy of any sandbox claim sit outside engineering scope. Section 2 remains in this document as the research record and as the source of the product constraints in section 8.2 — not as an engineering task list.

What still needs a decision before or during implementation:

1. **Which WhatsApp number** for the demo — the Meta test number (available immediately, can only message pre-verified recipients) or a real WABA with Business Verification (unrestricted, but weeks of external lead time). This determines whether phase 4's exit test can run on schedule.
2. **EgoSMS sender ID** — must be registered with the provider and is capped at 11 characters. Start this in parallel with phase 0; it blocks every SMS work order if left late.
3. **Sandbox or live EgoSMS for the demo** — sandbox needs a balance top-up from their customer care.
4. **Assignment strategy for WO 5.1** — round-robin within the routed team, workload-aware, or supervisor-assigned. Round-robin is the smallest change and is recommended unless there is an operational reason otherwise.
5. **Which insurer, if any, has agreed to a pilot** — determines whether WO 7.2's REST policy adapter has a real endpoint to target, or stays a configurable port awaiting one.
6. **Tenant opt-in defaults** — should assistant auto-reply and transactional SMS auto-send default to on or off for a new workspace? Recommended: off, with the first admin turning them on deliberately.

---

## 19. Related documents

- [`AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md`](../05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md) — which Azure subscriptions and resource groups actually hold IIP resources, and the verified deploy procedure.
- [`comms-api-reference.md`](../comms-api-reference.md) — the EgoSMS / Comms Platform API reference underpinning section 5.
- [`DESIGN.md`](../../DESIGN.md) — UI authority index; read before any change in section 10.
- [`AGENTS.md`](../../AGENTS.md) — the repository authority index and human-approval boundary.

---

## 20. Sources for the regulatory research in section 2

- [Insurance Regulatory Authority of Uganda](https://ira.go.ug/)
- [144 players set to drive insurance business in 2026 — IRA](https://ira.go.ug/144-players-set-to-drive-insurance-business-in-2026/)
- [IRA 2026 Circulars](https://ira.go.ug/2026-circulars/)
- [Insurance Act 2017, full text](https://ira.go.ug/cp/uploads/theinsuranceact.pdf)
- [IRA Complaints Bureau and Tribunal](https://ira.go.ug/cp/uploads/COMPLAINTS%20BUREAU%20&%20TRIBUNAL.pdf)
- [Data Protection and Privacy Act 2019, ULII](https://ulii.org/akn/ug/act/2019/9/eng@2019-05-03/source)
- [Registration compliance under the Data Protection and Privacy Act 2019, KAA](https://www.kaa.co.ug/registration-compliance-under-the-data-protection-and-privacy-act-2019/)
- [How to register with PDPO Uganda](https://globallawexperts.com/how-to-register-with-pdpo-uganda/)
- [Meta, WhatsApp webhook callback URL overrides](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/override/)
- [Meta, WhatsApp Cloud API webhook setup](https://developers.facebook.com/docs/whatsapp/cloud-api/guides/set-up-webhooks/)
