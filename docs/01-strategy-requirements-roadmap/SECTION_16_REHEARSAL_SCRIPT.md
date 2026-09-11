# Section 16 rehearsal script

Blueprint §16 ("What a successful system run looks like") is the exit test for the demo-ready
subset. This document maps each of its 11 steps to the exact screen, API call, and backing code
that executes it — written so the rehearsal itself is a checklist run against a live deployment,
not a fresh investigation each time. Built for WO 7.6; the seeded dataset half of that WO is done
and verified (see below). The live rehearsal itself requires credentials this development
environment does not have — see "What is blocked" at the end.

## Prerequisites

1. A live Azure deployment (or a fully local stack) with:
   - `AI_PROVIDER=AzureOpenAI`, `DOCUMENT_PROVIDER=AzureDocumentIntelligence`, a real Azure OpenAI
     chat + embedding deployment, and Azure AI Search configured for retrieval.
   - `CHANNEL_PROVIDER=WhatsAppCloudApi` with a WhatsApp Business number connected — either the
     Meta test number (works immediately, can only message pre-verified recipient numbers — see
     blueprint §18.1) or a Business-Verified WABA.
   - An SMS provider connection (EgoSMS or Africa's Talking) configured and validated through the
     Connections screen, with `AfricasTalking:AutoSendTransactionalSmsEnabled` left at its
     conservative default (`false`) so step 9 below genuinely demonstrates the human-approval gate.
2. The demo dataset seeded (see next section) so the assistant has an approved, cited knowledge
   source to answer from, and My Work / Claims Queue are not empty on first login.
3. A signed-in `ClaimsOfficer` (or a role with `ViewAssignedClaims`) session in the portal.

## Seeding the demo dataset (verified, real, already built)

`POST /api/v1/admin/demo-seed` (`DemoSeedEndpoints` → `DemoSeedService`) builds a full,
deterministic dataset for a synthetic Ugandan composite insurer ("Nile Shield Assurance") — real
customers, policies, and 50 claim cases spanning Motor/Medical/Life/General/Agriculture and every
workflow status, written through the same repositories live API traffic uses (no seed-only
storage). Confirmed by `DemoSeedServiceTests`:
`SeedAsync_ProducesFiftyCasesAcrossEveryProductLineAndStatus` and
`SeedAsync_IsDeterministicAcrossRuns` — both passing as of this document.

For step 1 to return a grounded, cited answer, at least one knowledge document must also be
uploaded and approved with `Sensitivity: Approved` for the product line the rehearsal question
targets (Motor) — `DemoSeedService` seeds cases and policies, not knowledge documents; upload one
through **Knowledge** first if the workspace doesn't already have an approved Motor source.

## The 11 steps

| # | Step | Where it actually runs | What proves it worked |
|---|---|---|---|
| 1 | WhatsApp message: *"What does your motor policy cover?"* | Meta sends to the configured webhook → `WhatsAppIntegrationEndpoints.ReceiveWebhook` → `CustomerAssistantService.HandleInboundMessagesAsync` (`KeywordCustomerIntentClassifier` → `GeneralQuestion`) | A reply arrives on the test number |
| 2 | Grounded answer citing an approved document, within seconds | `CustomerAssistantService.HandleGeneralQuestionAsync` → `IRetrievalProvider.RetrieveAsync` → `IAiAssistantProvider.AnswerPolicyQuestionAsync`, gated by `CustomerAutoReplyGate.IsAutoReplyPermitted` (needs `WhatsAppOptions.AssistantAutoReplyEnabled = true` for this tenant, or the answer is queued for human approval instead — a deliberate choice to make for the demo) | The reply text names the source document; check `CustomerConversationTurn.CitationTitles` in the audit trail |
| 3 | *"I had an accident yesterday."* | Same webhook path; classifier resolves `NewClaim` | — |
| 4 | Assistant does not handle it, opens a case, replies with a reference + required documents | `CustomerAssistantService.HandleNewClaimAsync` → `CaseIntakeService.CreateCaseAsync` (`RequiresConfirmation: true`) → reply composed from the real `IDocumentChecklistProvider` checklist (WO 5.2/5.3 — `FnolAcknowledgementService` also fires for non-WhatsApp channels, but WhatsApp's own inline reply is the one the customer sees here) | An 8-character reference in the reply; a new case in **Claims Queue** labelled channel `WhatsApp` |
| 5 | A photo of the damage is stored against the case | `WhatsAppInboundDocumentIngestionService` (media message path, not `CustomerAssistantService`, which only reasons about text) | The case's **Documents** tab shows the image |
| 6 | The claims officer opens **My work** and sees the claim, labelled WhatsApp, with a named owner | `MyWorkService` / `ClaimsQueue.tsx` (`ChannelBadge`, WO 2.5) + `CaseAssignmentPolicy` (WO 5.1) — the very first claim on a fresh workspace may land team-only (no case history yet for the policy's routed team to infer an owner from); assign one manually via **Claims Queue → Assign** the first time, after which `CaseAssignmentPolicy` has a real roster to route subsequent claims to automatically | The claim appears in the signed-in officer's My Work with a channel badge and an owner |
| 7 | Document extraction has run; fields populated with confidence, low-confidence fields flagged | `IDocumentProcessingProvider` (Azure Document Intelligence) via the existing extraction pipeline | The claim's extracted-fields panel shows values + confidence scores |
| 8 | Officer reviews AI preparation and decides in a separate container, recording a reason | `ClaimReview.tsx` — AI panel vs. human-decision panel are structurally separate containers (design system rule); `ApprovalEndpoints`/`ClaimAuthorityPolicy` enforce the human-decision boundary server-side, not just visually | An audit event records the reviewer, the reason, and that a human (not `ActorType.Integration`) made the call |
| 9 | Customer receives an SMS with the outcome, sent only after human approval | `CustomerUpdateEndpoints.DraftCustomerUpdate` (`CustomerUpdateReasonCategory.DecisionOutcome` — WO 5.5, never states the decision itself, always `RequiresApproval`) → officer approves → `CommunicationEndpoints.SendSms` | SMS delivery status visible in **Communications**; the customer-safe message never states the actual decision, matching AGENTS.md |
| 10 | Audit trail shows every step: who, when, on what evidence, with what citation | `WorkflowEndpoints.GetTimeline` → `ICaseTimelineProvider.Build` (WO 5.6 — comprehensive action-name coverage across every channel this run touches) | The case's **Activity timeline** shows a specific, real label for every event above, not a generic fallback |
| 11 | A case file export produces a single document with all of the above | `ComplianceEndpoints.ExportAuditPack` → `AuditPack.Build` (WO 6.2), then `ComplianceEndpoints.DownloadAuditPackExport` to actually retrieve the file (WO 6.2's own fix — the export used to be write-only) | A downloaded JSON file containing the timeline, documents, AI actions with citations, the approval with its reviewer, and the communications |

## What is blocked (genuine, not a code gap)

Every step above has real, tested code behind it — the blocker is entirely credentials and a
live environment this development session does not have access to:

- **No live Meta WhatsApp Business number is connected here.** Steps 1–6 need one (the test number
  is fine — see blueprint §18.1 for the tradeoff against a Business-Verified WABA).
- **No live Azure OpenAI / Document Intelligence / AI Search deployment is targeted from this
  session.** Steps 2 and 7 need real deployments, not the `Mock` provider.
- **No live SMS gateway connection is configured for this rehearsal.** Step 9 needs EgoSMS or
  Africa's Talking credentials validated through the Connections screen.

None of this is a code problem — every piece named in the table above is real, already built, and
covered by this session's own test suite (1090/1090 backend tests passing as of the work that
produced this document). Running the actual 11-step sequence live is the next action, and it
belongs to whoever holds those credentials — this script is what makes that run a checklist
instead of a rediscovery.
