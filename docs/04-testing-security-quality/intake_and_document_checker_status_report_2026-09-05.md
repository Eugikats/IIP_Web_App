# Intake/FNOL and Document Checker — Verification Status Report

**Date:** 2026-09-05
**Method:** code trace to file:line, unit-test runs, and a real SQL Server 2022 instance (Docker) exercised
directly through the production repository/service classes — not summarized from docs or commit messages.
**Rule applied:** nothing below is called "built" on the strength of a passing unit test alone.

---

## 1. Headline findings

1. **The FNOL and Document Checker business logic is real** — real EF repositories, real rule-based
   classification/routing, a real human-approval gate on document extraction. Verified by running it
   against an actual SQL Server 2022 container (see §4).
2. **There is no integration test in this repository that hits a real database.** Every "endpoint test"
   calls the endpoint's static method directly against `InMemoryCaseRepository`/`InMemoryPolicyRepository`/etc.
   No `WebApplicationFactory`, no Testcontainers, no SQL Server/Postgres connection anywhere under `tests/`.
3. **A real, currently-existing, previously-unknown-to-this-audit defect was found by testing against the
   real service that no unit test could ever catch**: applying the EF Core migration chain to a genuinely
   fresh SQL Server database fails outright (`There is already an object named 'AgentStates' in the
   database`). This is not new since last night — it dates to `20260807082756_AddCommandCenterConversations`
   (committed 2026-09-02/03) — but it means a fresh environment cannot be stood up without a manual,
   undocumented admin call (`ForceReconcileSchema`) first. See §4.2.
4. **"Auto-tagged as new" is true only in the trivial sense that every claim gets `Status = New` by default
   regardless of channel.** There is no channel-specific tagging logic, and the omnichannel "channel inbox"
   (WhatsApp/email conversation list) is a disconnected read-only subsystem with no code path that turns an
   inbound channel message into a case. See §2.2.
5. **Document Checker validation is presence/completeness only — there is no per-insurance-line policy
   knowledge base.** No file in the repository encodes a rule like "Ugandan NIN must be 14 characters."
   Grep for "national ID" / "NIN" / length constraints across `src/` returns nothing except a PII-redaction
   keyword list. See §3.2.
6. **The human-confirmation gate exists in code, but it gates a narrower thing than "the claim advancing."**
   It blocks the `ApproveExtraction` action on a document; it does **not** block a document from counting
   toward checklist completeness, and it does not block the claim's own status. See §3.4.

---

## 2. Intake / FNOL

### 2.1 Trace: entry point → persistence

| Step | File | What happens |
|---|---|---|
| UI form | [`web/portal/lovable-pages/FNOL.tsx`](../../web/portal/lovable-pages/FNOL.tsx) | Real 6-step wizard (`STEPS`, line 95) matching `docs/03-design-architecture/iip_fnol_six_step_intake_design.md` |
| Step 2 API call | `FNOL.tsx:164-166` | `GET /api/v1/policy-lookup?policyNumber=...` |
| Step 5 API call | `FNOL.tsx:194-197` | `GET /api/v1/document-checklist/preview?productLine=...&claimType=...&injuryIndicator=...` |
| Step 6 API call | `FNOL.tsx:284-300` | `POST /api/v1/cases`, body includes `channel: "StaffEntry"` (hardcoded, line 294) |
| Policy lookup endpoint | [`src/InsuranceIntelligence.Api/Cases/PolicyLookupEndpoints.cs:21-76`](../../src/InsuranceIntelligence.Api/Cases/PolicyLookupEndpoints.cs) | Calls `IPolicyRepository.GetByPolicyNumberAsync` → `ICustomerRepository.GetAsync` → `ICaseRepository.ListAsync` (duplicate-claim scan) |
| Policy repo (real) | [`EfRepositories.cs:391-413`](../../src/InsuranceIntelligence.Infrastructure/Persistence/EfRepositories.cs) | `EfPolicyRepository.GetByPolicyNumberAsync` loads all tenant policies and does an in-memory `FirstOrDefault` (JSON-payload storage pattern — no SQL-level `WHERE` on policy number) |
| Case creation endpoint | `src/InsuranceIntelligence.Api/Cases/CaseEndpoints.cs:16-71` | Builds `CreateCaseCommand`, calls `CaseIntakeService.CreateCaseAsync` |
| Domain intake service | [`CaseIntakeService.cs:9-51`](../../src/InsuranceIntelligence.Domain/Cases/CaseIntakeService.cs) | Validates required fields, calls `ClaimRoutingPolicy.Evaluate`, builds `ClaimCase`, saves, writes audit event |
| Case repo (real) | `EfRepositories.cs:16-32` | `EfCaseRepository` persists through `InsuranceIntelligenceDbContext.Cases` (JSON payload column) |
| Migrations | `src/InsuranceIntelligence.Infrastructure/Migrations/*.cs` | Real EF Core migrations exist — but see §4.2, the chain is broken from a fresh DB |

No stub/mock in this specific chain for the supported "SqlServer" storage provider. `InMemory` storage is
explicitly gated to `LocalTest`/`Development`/`Test` runtime profiles only
(`InfrastructureRegistration.cs:61-73, 212-221`); `SQLite` is refused outright (`InfrastructureRegistration.cs:44-48`).

### 2.2 Auto-tagging as "new" — partially confirmed, materially different from the ask

- `CaseIntakeService.cs:32`: `Status = routing.InitialStatus`. Every non-injury/fraud/high-risk claim
  routes through `ClaimRoutingPolicy.HappyOwnDamage/HappyThirdParty/UnknownDefault`, all of which set
  `CaseStatus.New` (`ClaimRoutingPolicy.cs:112,127,192`). This is **channel-independent** — it happens for
  every claim regardless of how it arrived.
- `Channel` (`CreateCaseRequest.cs:12`, required in `CaseIntakeService.cs:67`) is stored verbatim as a free
  string on `ClaimCase.Channel`. It is **read nowhere** in `ClaimRoutingPolicy` or `ClaimTypeClassifier` —
  confirmed by grep and by reading both files in full. Channel has zero effect on status, routing, priority,
  or SLA.
- The separate "omnichannel inbox" subsystem — `ChannelQueryService.cs`, `IChannelReadRepository` (real EF
  repo, `Phase2ReadRepositories.cs:371`) — is a **read-only overview** of a `ChannelConversations`-style table
  (`DetectedIntent`, `SuggestedRouting`, `AiConfidence` columns). There is **no writer** anywhere in `src/`
  that inserts a row into this table from a real inbound WhatsApp/email/webchat event, and **no code path**
  that promotes a channel conversation into a case. The only place `Channel` gets populated on a `ClaimCase`
  is `DemoSeedService.cs:115` (`Channels[rng.Next(...)]` — random demo data) and the FNOL form's hardcoded
  `"StaffEntry"`.
- **Conclusion:** claims default to `New` status universally, not because of channel-aware logic. The
  "claim coming in through a channel gets auto-tagged" framing implies an ingestion pipeline that does not
  exist; only manual staff/portal entry through the FNOL form actually creates a case today.

### 2.3 Classification & routing — real, not stubbed

`ClaimTypeClassifier.Classify` (`ClaimTypeClassifier.cs:30-68`) does real keyword matching
(`ContainsAny(normalized, "theft", "stolen", "total loss", ...)`) and — as of commit `d199e3f` — returns
`Unknown` immediately for any non-Motor product line, fixing a real bug where Medical claims were
force-escalated because their claim-type text contained the word "medical". `ClaimRoutingPolicy.Evaluate`
(`ClaimRoutingPolicy.cs:19-76`) branches on classification plus fraud/contested/settlement signals to set
team, priority, SLA, and status. This is deterministic domain logic, not a hardcoded per-tenant table.

### 2.4 Document checklist step — real

`DocumentChecklistRules.Resolve`/`.ForCase` (`DocumentChecklistRules.cs`) computes required/optional
document sets from `productLine`/`claimType`/injury indicator via `ClaimTypeClassifier`. `GET
/api/v1/document-checklist/preview` (`DocumentEndpoints.cs:578-603`) calls it directly — no hardcoded
per-tenant document lists found.

---

## 3. Document Checker

### 3.1 Trace: entry point → persistence

| Step | File | What happens |
|---|---|---|
| Upload endpoint | `src/InsuranceIntelligence.Api/Documents/DocumentEndpoints.cs:20-151` (`UploadDocuments`) | `IDocumentStorageProvider.StoreAsync` → build `CaseDocument` (placeholder classification, see §3.5) → `ICaseDocumentRepository.SaveAsync` → audit write → `IDocumentProcessingProvider.ProcessAsync` → re-save with real result → second audit write → completeness rebuild |
| Extraction (real, opt-in) | `src/InsuranceIntelligence.Api/Azure/AzureDocumentIntelligenceProvider.cs:8-176` | Genuine Azure AI Document Intelligence call (`AnalyzeDocumentAsync`, `prebuilt-document` model), flattens `Fields`/`KeyValuePairs` with confidence scores |
| Extraction (default/test) | `src/InsuranceIntelligence.LocalTestDoubles/MockDocumentProcessingProvider.cs:5-74` | **No OCR.** Classifies by filename substring match only (`"claim"`, `"police"`, `"invoice"`, `"medical"`, `"photo"`, `"id"`) |
| Wiring | `src/InsuranceIntelligence.Api/Program.cs:237-244` | Azure provider only wired if `DocumentProvider == "AzureDocumentIntelligence"`; otherwise `NotConfiguredDocumentProcessingProvider` throws `ProviderNotConfiguredException` (fails closed, does not silently fake data) |
| Checklist/completeness | `src/InsuranceIntelligence.Api/Local/StaticDocumentChecklistProvider.cs:8-35` | Presence-only: counts a document as "received" once its type is in `{Processed, ExtractionApproved}` |
| Human approval gate | `src/InsuranceIntelligence.Api/Documents/DocumentMapper.cs:158-183, 306-339` | See §3.4 |

### 3.2 Extraction is real when configured — validation is not

- **Extraction/tokenization**: real when `AzureDocumentIntelligence` is configured
  (`AzureDocumentIntelligenceProvider.cs`); a filename-keyword guess otherwise (`MockDocumentProcessingProvider.cs`).
  Confirmed no other OCR integration exists anywhere in the repo.
- **Per-insurance-line policy validation**: `DocumentChecklistRules.cs` only decides *which document types*
  are required per product line/claim type (e.g., Motor+Injury needs `MedicalReport`). It never inspects a
  document's extracted field values or formats.
- **Search for Uganda-specific rules** (`grep -i "national id\|NIN\|14 character"` across `src/`): the only
  hit is `DocumentMapper.cs:33`, a PII-**redaction** keyword (`"nationalid"`) used to hide sensitive field
  names from API responses — not a validation rule. **No `.cs` file anywhere validates a Ugandan national
  ID's length, format, or any other insurer-specific business rule.** All "national ID" / "14 character"
  hits repo-wide are in `docs/**` (aspirational) or `web/portal/lib/lovable-parity/mock.ts` (UI mock data).
- **Conclusion**: there is no policy knowledge base backing document validation. If a reviewer or an LLM is
  ever asked "is this a valid Ugandan national ID," nothing in this codebase currently supplies that
  domain fact — confirming the audit's concern exactly.

### 3.3 No LLM is in the document-checking path

`src/InsuranceIntelligence.Application/Agents/Tools/DocumentReadTool` (tag `"document-checker"`,
`SingleEntityReadTools.cs:41-62`) and `DocumentsGapsReadTool` (`ClaimsTools.cs:44-58`) are **read-only**: they
list already-computed documents/gaps, no LLM call. The only LLM calls in the codebase
(`AzureOpenAiChatCompletionProvider`, `AzureOpenAiAssistantProvider`, `AzureOpenAiCustomerUpdateDraftingProvider`
in `AzureOpenAiProviders.cs`) are for knowledge Q&A and customer-message drafting — none are invoked from
document upload/validation.

### 3.4 Human confirmation gate — real in code, narrower in scope than "the claim advances"

Exact guard (`DocumentMapper.cs:324-339`):

```csharp
public static IReadOnlyCollection<BlockedActionResponse> BlockedActions(CaseDocument document)
{
    if (document.Extraction.Approved || (document.Classification.ConfidenceScore >= 0.8m && document.ValidationWarnings.Count == 0))
    {
        return [];
    }

    return
    [
        new BlockedActionResponse(
            "ApproveExtraction",
            "HUMAN_VERIFICATION_REQUIRED",
            "Low-confidence or warned extraction requires human verification before approval.")
    ];
}
```

and `ApproveExtractionBlockedReason` (`DocumentMapper.cs:158-183`) refuses the approve action a second time,
on unprocessed documents, on failed documents, on unclassified documents, and while a blocking
(error/critical severity) validation warning is outstanding.

**However**, `StaticDocumentChecklistProvider.cs:15` counts a document toward checklist completeness once
its status is `Processed` **or** `ExtractionApproved` — i.e., a document does **not** need human approval to
satisfy the checklist. Separately, `ClaimAuthorityPolicy.EnsureActorMayPerform`
(`src/InsuranceIntelligence.Domain/Cases/ClaimAuthorityPolicy.cs:13-22`) gates claim-level actions
(`ApproveClaim`, `EvidencePackExport`, advance to `ReadyForExport`) on the *actor type* being `Human` — it
does not check whether any document's extraction was itself human-approved. **These are two independent
gates that are never connected**: a claim can reach `ReadyForExport` (human-actor-gated) while every
document on it still sits at `Processed`/unapproved extraction (checklist-complete, but never
human-verified at the field level). If the intended contract is "the claim only advances after a human has
confirmed the document contents," that contract does not exist in code today — only "a human, not an AI,
must click Approve/Export" and, separately, "a human must click ApproveExtraction if confidence is low or a
warning fired," with no link between the two.

### 3.5 Flagged issues

- `DocumentEndpoints.CreateUploadedDocument` (`DocumentEndpoints.cs:559,563`) hardcodes the classification/
  extraction `ProviderName` to the literal string `"Mock"` for every newly uploaded document, before
  `ProcessAsync` overwrites it — cosmetic (gets replaced immediately) but means any observer of the
  pre-processing state (a race, a crash mid-upload) sees "Mock" even when Azure Document Intelligence is the
  configured provider.
- `MockDocumentProcessingProvider` (filename-substring classifier) is what tests and local/demo runs
  exercise by default — none of the 55 API-layer tests or the domain document tests run against the real
  Azure Document Intelligence provider.
- `NotConfiguredDocumentProcessingProvider` fails closed (throws) rather than fabricating a result — good;
  it also confirms Document Intelligence is **not enabled by default**.

---

## 4. Evidence

### 4.1 Unit tests (real command, real output — but not the real service)

```
dotnet test InsuranceIntelligence.sln -c Release --filter "FullyQualifiedName~CaseEndpointsTests|...DocumentEndpointsTests|...ClaimGovernedPathServiceTests|...CaseIntakeServiceTests|...ClaimRoutingPolicyTests|...DocumentCompletenessTests"
```
Result: **55/55 passed** (`InsuranceIntelligence.Api.Tests`), **45/45 passed** (`InsuranceIntelligence.Domain.Tests`).
All 100 tests run against `InMemoryCaseRepository`/`InMemoryPolicyRepository`/`InMemoryCaseDocumentRepository`
(`InsuranceIntelligence.LocalTestDoubles`) or EF Core's `UseInMemoryDatabase` provider. **Per the team's own
rule, this is not evidence that the system works — it is evidence the domain logic is internally
consistent.** No `WebApplicationFactory`, no Testcontainers, no real SQL connection exists anywhere in
`tests/`.

### 4.2 Real-service probe (written for this audit, no prior integration test existed)

Spun up `mcr.microsoft.com/mssql/server:2022-latest` in Docker and exercised the actual production
repository classes (not test doubles) against it.

**First attempt — the real deploy path, `Database.MigrateAsync()` against a fresh database:**

```
Unhandled exception. Microsoft.Data.SqlClient.SqlException (0x80131904):
There is already an object named 'AgentStates' in the database.
   at ... Microsoft.EntityFrameworkCore.Migrations.Internal.Migrator.MigrateAsync(...)
Error Number:2714,State:6,Class:16
```

Root cause, confirmed by reading the migration source: two separate migrations both call
`migrationBuilder.CreateTable(name: "AgentStates", ...)` —
[`20260716123910_AddAgentWorkflowControlPlane.cs:14-27`](../../src/InsuranceIntelligence.Infrastructure/Migrations/20260716123910_AddAgentWorkflowControlPlane.cs)
and
[`20260807082756_AddCommandCenterConversations.cs:14-27`](../../src/InsuranceIntelligence.Infrastructure/Migrations/20260807082756_AddCommandCenterConversations.cs)
(the latter, per its own author's later fix commit `1baf505`, "recreates ~28 tables that already exist ...
alongside the one genuinely new table, CommandCenterConversations"). The team already knows about this
(see `src/InsuranceIntelligence.Api/Admin/DatabaseMigrationEndpoints.cs:81-227`, `ForceReconcileSchema`) and
built a manual, admin-triggered SQL patch for their **specific already-drifted dev database** — but that
patch is not run automatically, and the underlying migration file itself is still broken. **This means
`dotnet ef database update` / the plain `ApplyPending` admin action cannot stand up this schema from a
genuinely empty SQL Server today** — a new environment, disaster recovery, or a review-app database would
hit exactly the error above. This predates last night's build (introduced 2026-09-02/03, see §5) but was
previously undetected because nothing exercises `Database.Migrate()` against a real engine.

**Second pass — proving the repository/service layer itself, via `EnsureCreated` (bypassing the broken
incremental chain) on a second fresh database:**

```
--- Building schema on real SQL Server via EnsureCreated ---
Schema created.

--- Wrote Policy NSA/MO/2026/AUDIT-6141F6 and Customer f4c47528-... via EfPolicyRepository/EfCustomerRepository ---

--- Re-read via fresh DbContext: GetByPolicyNumberAsync('NSA/MO/2026/AUDIT-6141F6') ---
OK: found PolicyId=625986ff-fd82-4df6-aa3b-3287f05f0f57 ProductLine=Motor CustomerId=f4c47528-...

--- Created case via CaseIntakeService (Channel=WhatsApp) ---
CaseId=53e0b5bb-6258-4c86-80b7-0f85d79e1b23 Status=New Channel=WhatsApp AssignedTeam=Claims Intake

--- Re-read case 53e0b5bb-6258-4c86-80b7-0f85d79e1b23 via fresh DbContext ---
OK: Status=New Channel=WhatsApp CompletenessScore=0
Audit events persisted for this case: 1 (e.g. case.created)

--- Persisted CaseDocument 22a319a5-... via EfCaseDocumentRepository ---

--- Re-read documents for case 53e0b5bb-... via fresh DbContext ---
Documents found: 1, ApprovedExtraction pending human confirmation for all: True
```

Independently confirmed with raw `sqlcmd` (no EF involved), same database:

```
Cases        : 1
Policies     : 1
Documents    : 1
AuditEvents  : 1
```

Each read used a **brand-new `DbContext`/connection**, ruling out an in-process cache artifact. This is
real evidence: `CaseIntakeService`, `EfCaseRepository`, `EfPolicyRepository`, `EfCustomerRepository`,
`EfCaseDocumentRepository`, and `EfAuditRepository` genuinely persist to SQL Server, and the "Channel"
field round-trips unchanged (confirming §2.2 — it's stored, not acted on). The harness source is at
`RealDbAudit/Program.cs` (kept in the session scratchpad, not committed — this is audit tooling, not a
product change). Container was removed after the run (`docker rm -f iip-audit-sql`); this evidence is not
reproducible by re-running `dotnet test`, only by re-running this same real-database probe.

**Explicit statement per the audit's evidence rule:** no *committed* integration test in this repository
exercises a real database. The above is a one-off, throwaway probe built for this audit, run once, and torn
down — it demonstrates the capability works, but there is no standing, CI-enforced test that will catch a
regression here in the future. That gap should itself be treated as a finding.

---

## 5. Regression check (commits touching these two workflows, filtered from ~30 commits on 2026-09-04)

| Commit | Time (2026-09-04) | Touches | Assessment |
|---|---|---|---|
| `d199e3f` | 06:59 | `ClaimTypeClassifier.cs`, `ClaimRoutingPolicy.cs`, FNOL.tsx | **Genuine bug fix**, not a regression: added `productLine` gate so Medical claims stop being misclassified as `InjuryFatality` because their text contains "medical". Existing Motor callers pass `productLine: null` and are unaffected by construction. |
| `1289bb4` | 20:56 | Knowledge-document reclassification (not Document Checker's case-document path — a separate "Knowledge" RAG module) | Found live: 5 approved knowledge documents, 0 retrieval-eligible, because an earlier WO-17 approval-atomicity change left older approved docs stuck at `Sensitivity=Unknown`. This is a plausible source of "AI answers got inconsistent" if the complaint is about the **knowledge assistant**, not FNOL/Document Checker. Out of this audit's two-workflow scope but flagged since it matches the symptom description. |
| `6e20075` | 21:52 | FNOL.tsx full rewrite, new `PolicyLookupEndpoints.cs` | The six-step restructuring. Commit message itself flags a pre-existing, untouched bug: the motor injury-indicator field defaults to `"Unknown"`, and `ClaimTypeClassifier.IndicatesInjuryOrFatality` treats anything other than an explicit "No" as injury-indicated — so every claim submitted without changing that dropdown force-escalates. Not introduced by this commit, but not fixed either. |
| `93af084` | 23:41 | SLA breach counting (closed claims excluded) | Unrelated to intake/document logic; affects dashboards only. |
| `22f06cd`, `c1342e8`, `5b5c826`, etc. | various | CVE patch, repo hygiene, design tokens | No functional touch to FNOL/Document Checker code paths. |

**No prompt or model-parameter change was found in the FNOL/Document Checker path** — no commit in the
audited range touches `AzureOpenAiProviders.cs`, agent tool prompts, or `CommandCenter`/`Knowledge` files
in a way that intersects Intake or Document Checker (the LLM is not in either workflow's code path at all,
per §3.3). If "AI responses got inconsistent" refers to the Command Center / knowledge assistant, the
likely culprit is the `1289bb4` retrieval-eligibility bug above, not these two workflows.

---

## 6. Spec: how these two workflows actually behave today

### Intake / FNOL

- **Input:** staff/portal user fills a 6-step wizard (claimant → policy → incident → product-line fields →
  documents → review). No customer-facing/magic-link entry point exists in code yet (design doc says this
  is planned; not found implemented).
- **Steps 1, 3, 4:** client-side required-field validation only; no backend call per step.
- **Step 2:** `GET /api/v1/policy-lookup?policyNumber=` — real EF-backed lookup, flags open (non-terminal)
  duplicate claims on the same policy, derives product line from the policy or falls back to a manual
  selector.
- **Step 5:** `GET /api/v1/document-checklist/preview` — real rule-based required-document list from
  product line + claim type + injury indicator.
- **Step 6 (submit):** `POST /api/v1/cases` → `CaseIntakeService.CreateCaseAsync` → validates required
  fields → `ClaimRoutingPolicy.Evaluate` (classification, team, priority, SLA, initial status — always
  `New` unless a high-risk/injury signal forces `Escalated`) → persists `ClaimCase` via EF to SQL Server →
  writes one audit event (`case.created`).
- **Channel:** captured as a free-text field (`"StaffEntry"` from this form), stored, never acted upon.
  The omnichannel inbox (WhatsApp/email conversation list) is a separate, disconnected read surface with no
  writer and no link to case creation.
- **Failure modes:** missing required field → `ValidationProblem` (400); missing/invalid tenant scope →
  `ValidationProblem`; unknown policy number → manual product-line fallback, no hard failure; motor claim
  with injury indicator left at default `"Unknown"` → silently force-escalated (known, unfixed).

### Document Checker

- **Input:** a file uploaded against an existing case (`POST` under `DocumentEndpoints`).
- **Storage:** `IDocumentStorageProvider.StoreAsync` (blob or `NotConfigured`, fails closed if unset).
- **Extraction:** if `DocumentProvider=AzureDocumentIntelligence` is configured, real OCR/form extraction
  via Azure AI Document Intelligence with per-field confidence; otherwise a filename-keyword guess
  (`MockDocumentProcessingProvider`) with no field-level extraction — this is the default in local/test/demo.
- **Validation:** presence/completeness against `DocumentChecklistRules` (which document *types* are
  required per product line/claim type). No field-format or per-insurer regulatory rule (e.g. ID length) is
  encoded anywhere; there is no policy knowledge base.
- **Human gate:** `ApproveExtraction` is blocked (`HUMAN_VERIFICATION_REQUIRED`) whenever confidence
  `< 0.8` or a blocking validation warning exists, and is permanently locked once approved. This gate is
  scoped to the single "approve this extraction" action — it does not block checklist completeness (a
  `Processed`-but-unapproved document already counts) and is not connected to the separate claim-level
  `EnsureActorMayPerform`/`ReadyForExport` human-actor gate.
- **Failure modes:** unsupported file type → rejected; missing tenant/workspace header → rejected;
  low-confidence/warned extraction → approval blocked until a human corrects/approves; re-approval or
  reclassification of an already-approved extraction → rejected ("locked").

---

## 7. Unresolved / needs a decision

- No committed real-database integration test exists for either workflow — recommend adding one (e.g.
  Testcontainers-based `WebApplicationFactory` test) before trusting future changes here.
- The broken migration chain (`20260807082756_AddCommandCenterConversations`) needs a real fix (a corrective
  migration), not just the existing manual admin workaround, before this app can be deployed to any
  genuinely fresh database/environment.
- Decide whether "claim advances only after human confirmation" should mean the document-level
  `ExtractionApproved` gate must be satisfied before checklist completeness / claim `ReadyForExport` — today
  it does not, and that may or may not be the intended contract.
- Decide what, if anything, should back a real per-insurance-line policy knowledge base (e.g. Uganda NIN
  format) — currently nothing does, generic LLM knowledge would not fill this gap either.
- Confirm whether an actual channel-to-case ingestion pipeline (WhatsApp/email → case) is in scope; today
  it does not exist despite the read-only "channel inbox" UI implying one.
