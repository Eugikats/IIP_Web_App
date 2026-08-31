# IIP Release 1 Requirements Traceability Matrix

| Field | Value |
|---|---|
| Version | 2.4 |
| Date | 2026-08-10 |
| Status | Reconciled mapping; approval and missing evidence remain explicit |
| Requirement baseline | `iip_release_1_requirements_baseline.md` v0.14 |

## Traceability rule

Every Release 1 requirement must link to design, API/data contract, implementation seam, test/evaluation, and release evidence. A route or file is a mapping—not proof of correctness. `Mapped` means a likely seam exists; `Local evidence` means cited tests/results exist; `Blocked` means a required live/human fact is absent; `Gap` means no adequate seam/evidence is identified.

Abbreviations: `Flow` = `../03-design-architecture/iip_end_to_end_user_flow_architecture.md`; `Journeys` = Release 1 role journey maps; `Blueprint` = Release 1 intelligence-layer service blueprint; `Catalog` = Release 1 task-flow/exception/state catalog; `UX` = platform UX behavior specification; `API map` = `../02-development-engineering/iip_release_1_api_data_contract_map.md`; `AIA` = `../04-testing-security-quality/iip_release_1_algorithmic_impact_assessment.md`; `AC` = acceptance register; `RF10` = `../07-qa/rf10-release-foundation-go-no-go-evidence.md`.

## Business and functional traceability

| Requirement | Design/research | API/data | Implementation seam | Test/evaluation | Release evidence/status |
|---|---|---|---|---|---|
| R1-BUS-001 | Charter; scope register; Flow C | API map R1 route groups | Release 1 workflow allow-list | `AgentWorkflowReleaseOneBoundTests` | Local evidence; six-function approval blocked |
| R1-BUS-002 | Flow responsibility/decision table; AIA | Approval/workflow contracts | `ApprovalDecisionGuard`; graph validator; worker | `AgentWorkflowHumanReviewInvariantTests`; approval tests | Local evidence; protected-runtime proof blocked |
| R1-BUS-003 | Charter; Flow responsibility | API map data ownership | Cases/Application + adapters | Contract/architecture review | Mapped; insurer system decision blocked |
| R1-BUS-004 | Research program; ROI model | Baseline/metric data contract | Reporting/telemetry seams | Pilot measurement plan | Blocked: insurer baseline/finance approval absent |
| R1-BUS-005 | Research repository/change control | Evidence register | Documentation/decision workflow | Provenance audit | Mapped; formal approval workflow not executed |
| R1-FUN-001 | Flow B; canonical IA role visibility | `GET /api/v1/me`; membership/effective permissions | `AuthenticatedTenantResolver`; permission repository; portal permission-derived route presentation | `SecurityAuthorizationTests`; tenant tests; portal role-route check | Local/source evidence; multi-role staging UAT blocked |
| R1-FUN-002 | Flow C intake | `POST /api/v1/cases`; Case data | `CaseEndpoints`; `CaseIntakeService` | `CaseEndpointsTests`; `CaseIntakeServiceTests` | Local evidence; representative UAT blocked |
| R1-FUN-003 | Canonical IA and Universal My Work specification | `GET /api/v1/my-work`; source-owned task/approval/document/update/agent-work records | `MyWorkService`; `MyWorkEndpoints`; `UniversalMyWork`; source handoffs | `MyWorkServiceTests`; `MyWorkEndpointsTests`; Gate 9 visual fixture; portal guards | Five-source baseline implemented locally; protected multi-role, performance, accessibility, practitioner validation and named approval pending |
| R1-FUN-004 | Canonical IA record anatomy; UX shared/claim workspace | case, claim, timeline, document, approval, communication reads | claim review route/components/query services | claims/document/approval API + browser tests | Mapped; end-to-end UAT blocked |
| R1-FUN-005 | Flow C receive/validate/store | case-document upload/content; Blob+SQL | `DocumentEndpoints`; storage provider | `DocumentUploadValidationTests`; `DocumentEndpointsTests` | Local evidence; malware/real-storage staging proof incomplete |
| R1-FUN-006 | Flow C extract; UX document split | document classification/extraction contracts | document processing provider port | document endpoint/provider tests | Blocked: real Document Intelligence capacity/provider proof |
| R1-FUN-007 | Flow C review | reclassify/approve/correct routes; correction records | document review use cases/components | document endpoint tests; browser review test | Local mapping; manual accessibility/UAT blocked |
| R1-FUN-008 | Flow D-08; UX checklist | document checklist; rule version gap | completeness service/rules | `DocumentCompletenessTests` | Local evidence; insurer rule set blocked |
| R1-FUN-009 | Flow correction/re-evaluate | correction + workflow dependency lineage | descendant invalidation/re-evaluation | `AgentWorkflowDescendantInvalidationTests`; regression suite | Local evidence on current branch; merge/deploy evidence separate |
| R1-FUN-010 | Flow prepare; UX provenance | draft context, knowledge, assistant, workflow versions | grounding/provider/Application seams | severe stale/conflict/citation evaluations | Partial; real provider/corpus blocked |
| R1-FUN-011 | UX human review cockpit | case approval create/detail | approval service/route/UI | approval workflow/API tests | Mapped/local tests; exact-action UAT blocked |
| R1-FUN-012 | Flow Decide | approve/reject/override/escalate | approval guard/endpoints/panel | approval guard/API tests | Local evidence; target authority/delegation blocked |
| R1-FUN-013 | Flow Execute/reconcile | execute-next-step; workflow worker/integration history | approval execution + Functions worker | execution worker contract/API tests | Code/local evidence reported; live worker telemetry blocked RF-06 |
| R1-FUN-014 | UX communication preflight | customer-update draft/list/approve/reject/send | customer update and provider adapters | send/approval/provider failure tests | Partial; channel/template/legal/provider blocked |
| R1-FUN-015 | Flow audit/export | compliance audit-pack/export | compliance query/export/storage | `ComplianceTenantAuthorityTests`; export tests | Local evidence; protected-runtime pack/UAT blocked |
| R1-FUN-016 | Canonical IA handoff/recent-work contract; conversation spec | owner-scoped conversation routes + SQL history; current live query on continuation | `CommandCenterConversationService`; EF repository/migration; `/app/conversations*`; sidebar recent list | `CommandCenterConversationServiceTests`; query endpoint regression; portal lint/test/build; local synthetic Playwright list/reopen/archive/recovery proof; protected interruption/accessibility pending | Local implementation/evidence; linked-evidence change summary, retention/privacy approval, protected multi-role, interruption/accessibility and practitioner validation pending |
| R1-FUN-017 | Flow review/recourse; AIA | review-request list/acknowledge/resolve | `ContestabilityEndpoints`; panel | API + correction propagation tests | Mapped; insurer response/remedy policy blocked |
| R1-FUN-018 | Canonical IA global-search contract; AIA search-ranking control | `GET /api/v1/search`; authorized case/document/task/approval result contract | `GlobalSearchService`; `SearchEndpoints`; `/app/search`; `TopBar` | `GlobalSearchServiceTests`; `GlobalSearchEndpointsTests`; portal build/route guards; browser validation pending | Local implementation/evidence; protected multi-role staging, accessibility, practitioner validation and named Security/Privacy approval pending |
| R1-FUN-019 | `JRN-R1-LOSS-ASSESSOR`; `FLW-R1-ASSESSOR-01`; DEC-018; assessor-channel ADR; Blueprint §6.1 | No dedicated route; current document intake is only an implementation seam, not an approved assessor contract | Controlled manual baseline and optional configured Graph are specified; referral/pack/attempt/acknowledgement/report domain, permission and audit contracts remain unimplemented; portal/core integration deferred | `UAT-ASSESSOR-001` prepared; disclosure, exact-version acknowledgement, expiry/late return, lost response/reconciliation, duplicate/match, provenance and revision tests required | Scope decided; blocked for runtime by target-insurer configuration, permission/audit implementation and protected UAT |
| R1-FUN-020 | Assessor ethical review; `JRN-R1-POLICYHOLDER`; `FLW-R1-ASSESSOR-01` | Review-request seam exists; no assessor-specific `Contested` state/reliance guard contract | `ContestabilityEndpoints` is adjacent only; claimant mediated intake, exact-version dispute, reliance pause and independent reconsideration remain unimplemented | AC-ETH-01; `UAT-ASSESSORETH-001`; accessible notice/dispute/correction/remedy matrix | Design requirement mapped; insurer policy, permissions, runtime and claimant/practitioner evidence blocked |
| R1-WA-001 | WhatsApp intake/follow-up; channel inbox | `GET/POST /api/v1/integrations/whatsapp/webhook`; WhatsApp configuration | signed Meta provider; text/media parser; durable message repository; matcher/attachment processor | `WhatsAppInboundProviderTests`; `WhatsAppMessagingServiceTests`; provider registration tests | Source/local: complete; protected-staging webhook/replay proof pending |
| R1-WA-002 | Channel thread + case workspace | `GET /api/v1/channels/whatsapp/threads/{threadId}`; Channels overview | `WhatsAppMessagingService`; `EfWhatsAppMessageRepository`; `EfChannelReadRepository`; Channels UI | messaging/read repository tests; portal lint/build; multi-role browser/UAT required | Source/local: complete; staging role/accessibility/UAT pending |
| R1-WA-003 | Communication preflight + WhatsApp follow-up | `POST /api/v1/channels/whatsapp/threads/{threadId}/messages` | reviewed outbound service; Meta adapter; idempotent SQL record; Channels preflight | messaging/provider tests; real provider/template/UAT required | Source/local: complete; provider/policy/live-send approval pending |
| R1-WA-004 | Delivery/reconciliation states | signed WhatsApp webhook status events; thread delivery state | delivery parser; monotonic reconciler; unique provider reference | duplicate/out-of-order callback and provider failure tests; staging reconciliation drill required | Source/local: complete; live callback/operations evidence pending |

## Responsible algorithm and data traceability

| Requirement | Design/governance | API/data | Implementation seam | Test/evaluation | Release evidence/status |
|---|---|---|---|---|---|
| R1-AI-001 | AIA prohibited use; Flow boundary | approval/action/workflow contracts | guard, validator, worker allow-list | human-review invariant, bypass/red-team tests | Local evidence; release-wide/live proof blocked |
| R1-AI-002 | UX provenance; AIA explanation | evidence/version fields | evidence rail, approval/workflow DTOs | reviewer comprehension + citation tests | Partial; corpus/participant evidence absent |
| R1-AI-003 | AIA oversight | review/return/escalate contracts | queues, approval, contestability | wrong-output/workload/recourse usability tests | Functional seams mapped; human evidence absent |
| R1-AI-004 | UX untrusted-content state; AIA | input/provider/tool permission boundaries | adapters, validators, workflow/tool allow-list | prompt injection/tool escalation/privacy tests | Partial tests; full adversarial evidence gap |
| R1-AI-005 | UX agent registry; AIA governance | workflow definitions/versions/runs; provider metadata | immutable workflow lineage | `WorkflowVersionLineageContractTests` | Local evidence; complete model/prompt/rule lineage review pending |
| R1-AI-006 | AIA | data/model/rule inventory | governance process | AIA completeness/residual-risk review | Document drafted; named approvals blocked |
| R1-AI-007 | AIA incident/stop | kill switch, run/work-item/audit contracts | agent kill switch, workflow stop/invalidator | incident/kill/correction/restart exercise | Mapped; exercise and owners absent |
| R1-AI-008 | Assessor ethical review; AIA feedback/contestability controls | No approved training/ranking/retrieval/scoring data-use contract | Source/config/dataset/retrieval/model/analytics inventory is the enforcement seam; no new use authorized | AC-ETH-01; inventory and prohibited-reuse tests | Constraint mapped; implementation/data-governance evidence absent |
| R1-DAT-001 | API map data ownership | owning contracts/repositories | bounded Application/Domain modules | architecture/data review | Mapped; client authoritative systems conditional |
| R1-DAT-002 | Flow scope boundary | scope across SQL/Blob/search/message/audit | tenant-scoped repositories/adapters | API/worker/blob/search/export tenant tests | Partial local evidence; full coverage audit pending |
| R1-DAT-003 | Flow correction; UX provenance | source/derived/version/invalidation fields | correction + descendant invalidation | correction/lineage tests | Local evidence; cross-artifact coverage review pending |
| R1-DAT-004 | Flow separate state domains | CaseStatus, document, task, approval, execution states | domain enums/models | transition/contract tests | **Conflict:** Flow state table requires reconciliation to current code |
| R1-DAT-005 | conversation spec; guardrails | bounded session contract | conversation/session persistence | payload/storage privacy inspection | Design rule; implementation audit gap |
| R1-DAT-006 | Flow retry/reconcile | correlation/idempotency/integration history | worker/provider adapters | duplicate/lost-response/replay tests | Partial local tests; live provider ambiguity drill blocked |
| R1-DAT-007 | research/regulatory evidence; API map | lifecycle matrix across stores | retention/deletion mechanisms | deletion/legal-hold/backup propagation rehearsal | Blocked: client/legal schedules and implementation evidence |
| R1-DAT-008 | API map derived stores | search/cache/projection contracts | projection/index services | rebuild/repair/freshness tests | Gap: complete repair evidence not identified |
| R1-DAT-009 | Catalog evidence decision tree; Blueprint evidence eligibility/lineage | Evidence/source/version/review fields across document, workflow, approval and export contracts | Existing source/correction/invalidation seams; unified eligibility review remains partial | AC-EVD-01; `UAT-DOCS-002`; decision-tree contract/browser matrix required | Design mapped; complete runtime matrix and target lifecycle/access approval pending |
| R1-DAT-010 | Interaction accessibility/runtime parity; state conflict contract | Expected version/state and machine-readable conflict contracts | Conversation expected-version path current; queue/evidence/approval/update/export client coverage partial | AC-CON-01; UAT-UX-002; concurrent API/browser matrix | Partial; `PAR-GAP-01` and export/upload gaps open |
| R1-DAT-011 | Interaction requirements RTM; audit field contract | Audit factories/services plus safe event schema | Current case/document/workflow/approval/update/WhatsApp/conversation/export/agent events | AC-AUD-01; source validator; protected correlation/sampling | Mapped; `AUD-GAP-01`–`05` and protected evidence open |

## Security and privacy traceability

| Requirement | Design/governance | API/data | Implementation seam | Test/evaluation | Release evidence/status |
|---|---|---|---|---|---|
| R1-SEC-001 | Flow authority | all sensitive routes | middleware/accessor/Application guards | `SecurityAuthorizationTests`; endpoint tenant tests | Partial local evidence; route-wide inventory pending |
| R1-SEC-002 | Flow safe denial; UX restricted state | safe error/not-found contracts | access guards/error translation | enumeration/cross-tenant tests | Local patterns; complete route evidence pending |
| R1-SEC-003 | guardrails; deployment docs | configuration/secret references | managed identity/Key Vault/config | secret scan and artifact/log review | Mapped; environment evidence required |
| R1-SEC-004 | Flow provider/readiness states | health/readiness/capabilities | startup validation/provider resolution | configuration matrix/provider state tests | Local evidence; protected environment proof blocked |
| R1-SEC-005 | Journeys `PERM-GAP-01`–`05`; Catalog and interaction-RTM permission registers; AIA oversight | Current `PlatformRole`, `Permission`, `RolePermissionMap`, endpoint/object/state guards | Current mappings include broad/coarse permissions; assessor disclosure/action permission is absent; no change authorized by docs | AC-SOD-01; route/action inventory, negative/revoked/delegation/expiry/SoD/disclosure tests | Blocked: target role/assessor policy and current permission-gap disposition pending |
| R1-SEC-006 | Assessor threat assessment `THR-R1-ASR-01/02`; `FLW-R1-ASSESSOR-01`; ADR | No assessor route/domain contract; current mailbox/sender fields are data only | Proposed insurer-owned directory/appointment, recipient snapshot and exact preflight; `PERM-GAP-05` | AC-ASR-01; UAT-ASSESSORSEC-001 identity/disclosure matrix | Blocked: directory, permission, policy, implementation and protected proof absent |
| R1-SEC-007 | Assessor threat assessment `THR-R1-ASR-03`; evidence decision tree | Current staff upload size/extension seam; Graph attachment processor/store/process seam | Graph path buffers provider bytes and does not invoke shared validation or verified quarantine/malware-clearance contract; `SEC-GAP-ASR-01` | AC-ASR-01; hostile file corpus, scanner unavailable, parser/resource and quarantine-bypass tests | Blocked; do not reuse current Graph processor for assessor activation |
| R1-SEC-008 | Assessor threat assessment `THR-R1-ASR-04/05`; ADR non-revocation boundary | No assessor link/portal route; private Blob and scope-prefix read seam | Public/reusable bearer evidence URLs/SAS prohibited; expiry is workflow state; email forwarding residual policy external | AC-ASR-01; link/token replay, cancellation/expiry/late-return, source/log/browser inspection | Constraint mapped; insurer forwarding/DLP/incident residual-risk approval blocked |
| R1-SEC-009 | Assessor threat assessment `THR-R1-ASR-06`; Blueprint/provider boundary | Scoped repositories/Blob paths; Graph mailbox configuration/startup assertion | Assessor contract absent; `MailboxAccessScoped=true` does not prove actual Exchange restriction | AC-ASR-01; route/worker/blob/match/replay/audit/export isolation plus protected unapproved-mailbox denial | Partial adjacent controls; assessor and independently verified Graph scope blocked |
| R1-PRI-001 | AIA data inventory | field/data lifecycle map | DTO/view/export/log minimization | privacy inventory and disclosure tests | Partial; exact client purpose/authority blocked |
| R1-PRI-002 | market/regulatory pack; AIA | deployment/data-flow record | IaC/provider/support architecture | legal/security review + data-flow verification | Blocked: exact client/deployment and counsel approval |
| R1-PRI-003 | UX customer-safe rules | communication/export/view DTOs | customer-safe filters and authorization | adversarial message/export/redaction tests | Partial local evidence; full end-to-end proof pending |
| R1-PRI-004 | guardrails; Flow analytics | audit/telemetry safe metadata | audit writer/telemetry | `Phase5DomainTests` + production sampling | Local tests; protected-runtime sampling blocked |
| R1-PRI-005 | Assessor ethical review minimum-disclosure test; Catalog preflight | No disclosure-manifest route/schema | Proposed immutable per-question/per-item manifest, default-deny preflight and byte-to-manifest check | AC-ETH-01; `UAT-ASSESSORETH-001`; disclosure sampling | Blocked: taxonomy, authority, permission, API/UI/runtime absent |
| R1-PRI-006 | Assessor ethical review claimant-notice contract; claimant journey | No claimant-notice/exception contract | WhatsApp/staff-assisted communication are possible mediated seams only | AC-ETH-01; language/accessibility/timing/exception/comprehension tests | Blocked: insurer basis/wording/timing/exceptions, implementation and claimant validation absent |
| R1-PRI-007 | Assessor ethical review external-professional boundary; DEC-018 | No external seat; manual/Graph transport only; no handling-terms data contract | Contract/configuration approval and restricted export are gates; external portal remains deferred | AC-ETH-01; access-negative, contract/config and residual-risk review | Blocked: contractual terms, assurance, implementation and residual-risk acceptance absent |

## UX, accessibility, multilingual, performance and operations traceability

| Requirement | Design/source | API/data | Implementation seam | Test/evaluation | Release evidence/status |
|---|---|---|---|---|---|
| R1-UX-001 | Canonical route/state inventory; low-fidelity wireframe state inventory; Flow error matrix | error/provider/status contracts | portal pages/components | AC-LFW-01; 34 screens mechanically mapped to loading, empty, error, permission, partial, success and recovery; rendered wireframe/browser state matrix pending | Source-complete screen-state coverage; rendering, implementation proof, responsive/accessibility review and participant validation incomplete |
| R1-UX-002 | Complex operational patterns; UX evidence/decision hierarchy | Evidence/review/approval/execution contracts and exact ownership boundaries | Claim review, evidence and approval UI; parity unverified | AC-CPLX-01; CP-TST-EVIDENCE-01–03; CP-TST-APPROVAL-01–03; stale/wrong-evidence comprehension | Source-complete pattern contract; rendered/component/protected/practitioner evidence absent |
| R1-A11Y-001 | UX validation; WCAG 2.2 | semantic/error/status contracts | portal/components/styles | axe + manual keyboard/screen reader/zoom | Not executed comprehensively; blocked |
| R1-A11Y-002 | Accessibility v0.2 keyboard/focus application across 20 actions, six patterns and seven states | Action/state/dialog/table/panel contracts | Portal controls, queues, evidence, approvals, panels and dialogs | AC-A11Y-02/03; CP-TST-*; UAT-A11Y-001/002; keyboard-only critical journeys | Source-complete design; current modal/name/focus gaps and manual/browser evidence block verification |
| R1-A11Y-003 | Responsive/adaptive reflow contract; low-fidelity state inventory | Intrinsic table/document/form/dialog/navigation priorities and state-preservation rules | Portal layouts/components; parity unverified | AC-RSP-01; RSP-TST-01–12; 200% text, 400%/320px, orientation, virtual-keyboard, mixed-input and physical-device checks | Source-complete design contract; rendered/browser/device/practitioner evidence absent |
| R1-A11Y-004 | Accessibility v0.2 semantics/status matrix for all seven states and six patterns | Status/error/progress payloads and state owners | Live regions, error association and focus management | AC-A11Y-03; state/pattern announcement scenarios; supported screen-reader tests | Source-complete design; `A11Y-GAP-02/04/08`, manual AT and protected evidence remain open |
| R1-A11Y-005 | Accessibility v0.2 modal/panel/dialog contract aligned to WAI-ARIA APG | Modal state/safe-close contract | `GovernedActionDialog`, `SideDrawer`, related overlays | AC-A11Y-02; CP-TST-DIALOG-01–03; CP-TST-PANEL-01–03; UAT-A11Y-001 | **Fail:** `A11Y-GAP-01`; local diagnostic proves entry/containment/return defects |
| R1-A11Y-006 | Interaction accessibility name/role/state contract | Accessible name/state/error/status semantics | Critical portal controls, forms, tables and status regions | AC-A11Y-03; UAT-A11Y-002; axe + supported screen readers | **Fail:** critical label/name/select and announcement gaps found locally |
| R1-A11Y-007 | Accessibility v0.2 per-pattern touch/pointer contract | WCAG 24px floor plus IIP 44px high-impact target | Portal action, icon, navigation, tab, filter, queue, evidence, approval and dialog controls | AC-A11Y-04; CP-TST-* touch cases; UAT-A11Y-001; desktop/mobile/physical bounds | **Fail:** many sub-44 and some sub-24 targets found locally; physical-device proof absent |
| R1-A11Y-008 | Visual tokens plus interaction accessibility contrast contract | Rendered text/control/focus state colors | portal styles/components and forced-colors behavior | AC-A11Y-05; UAT-A11Y-002; rendered contrast/focus/forced-colors | **Fail:** serious contrast and inconsistent focus indication found locally |
| R1-A11Y-009 | Accessibility v0.2 reduced-motion application across patterns/states; responsive contract | Motion/state semantics and non-motion equivalents | Portal transitions, overlays, progress, queues, evidence, approvals and status components | AC-A11Y-06; CP-TST-*; UAT-A11Y-001; computed-style/manual walkthrough | **Fail:** retained dialog/hover transitions found locally; design requirement is stricter than WCAG AA |
| R1-UX-003 | research program; UX validation | task/telemetry definitions | critical browser workflows | moderated task completion | Blocked: representative participants absent |
| R1-UX-004 | Flow recovery matrix | failure/state/idempotency contracts | portal recovery paths | injected-failure usability test | Blocked: participant/manual evidence absent |
| R1-UX-005 | Action purpose/human authority; complex approval/dialog patterns; interaction preflight contract | Exact target/effect/evidence/version/authority payloads | Governed dialogs, approval cockpit, communication/export preflight | AC-CPLX-01; AC-PRE-01; CP-TST-APPROVAL-01–03; CP-TST-DIALOG-01–03; UAT-UX-001 | Source-complete design; complete component/runtime parity pending |
| R1-UX-006 | Complex operational patterns; surface/control and recovery contracts | Action-state/provider-result contracts | Controls plus owning queue/evidence/approval/panel/dialog/status regions | AC-CPLX-01; AC-ACT-01; CP-TST-*; UAT-UX-001/002 | Pattern design mapped; shared status, duplicate behavior and parity incomplete |
| R1-UX-007 | Complex operational patterns; state/exception/recovery and accessibility parity contracts | Machine-readable validation/permission/conflict/provider/session failures | Portal error mapping, preserved queue context/draft/input, panel/dialog focus recovery | AC-CPLX-01; AC-REC-01; AC-CON-01; CP-TST-* recovery cases; UAT-UX-002; UAT-A11Y-001/002 | Partial; `A11Y-GAP-08`, `PAR-GAP-01`–`03`, `CP-GAP-01`–`07` open |
| R1-L10N-001 | Uganda research; UX language posture | Unicode/string contracts | frontend/backend/storage | Unicode/original-text round trip | Mapped; systematic evidence gap |
| R1-L10N-002 | research/UX content | language/translation provenance fields | document/communication models | mixed-language contract/UI tests | Gap: contract fields not confirmed |
| R1-L10N-003 | Flow provider failure; AIA | unsupported-language outcome | document provider adapter/review queue | unsupported-language severe test | Gap/conditional provider behavior |
| R1-L10N-004 | UX communication preflight | template/language/channel approval data | customer-update/provider policy | unapproved-language/template send block | Blocked: client policy/provider absent |
| R1-L10N-005 | UX content/data rules | canonical value + locale render | formatting utilities/components | locale round-trip tests | Gap: approved locale matrix absent |
| R1-PERF-001 | AC; UAT script | claims/health routes | API/query/persistence | performance smoke harness | **Fail:** dev claims p95 >800 ms; staging p95/p99 blocked RF-09 |
| R1-PERF-002 | AC; web-vitals standard | portal route metrics | portal/RUM | LCP/INP/CLS field/lab | Evidence absent |
| R1-PERF-003 | Flow long-running work | run/work-item status contracts | workflow/provider async seams | delayed-provider acknowledgement/status test | Partial mapping; target evidence absent |
| R1-RES-001 | UX low-bandwidth; AC | compact/paginated/status contracts | portal/API | throttled critical-flow suite | Proposed profile; evidence absent |
| R1-RES-002 | Flow resume/retry | draft/upload/run idempotency | portal/upload/worker | 30-second disconnect/reconnect | Evidence absent |
| R1-RES-003 | AC; deployment/recovery docs | SQL/Blob/config backups | Azure resources/runbooks | timed restore rehearsal | Blocked: business target + rehearsal absent |
| R1-RES-004 | Flow failure model | queue/run/integration contracts | Service Bus/Functions/adapters | duplicate/retry/DLQ/replay tests | Local worker tests reported; live telemetry/drill blocked RF-06 |
| R1-OPS-001 | Flow analytics; guardrails | telemetry/audit/version dimensions | App Insights/audit/worker telemetry | incident trace exercise | Partial; deployed worker/full-flow trace blocked |
| R1-OPS-002 | UX provider states; API map | health/provider status | provider registry/health endpoints | dependency outage/runbook drill | Partial; real provider drill blocked |
| R1-OPS-003 | architecture/guardrails | schemas/config/workflow versions | migrations/workflow versioning | migration rollback/compatibility rehearsal | Workflow local evidence; Azure SQL window blocked RF-08 |
| R1-OPS-004 | release docs/AC | artifact/config/version record | deployment/release pipeline | rollback/support handoff review | Partial docs; protected release record absent |
| R1-REL-001 | AC gate model | evidence metadata | release governance | gate validation | Defined; no current approval |
| R1-REL-002 | AC production-candidate gate | RTM/evidence pack | CI/deploy/UAT/support processes | full evidence audit | RF10 verdict NO; blockers remain |
| R1-REL-003 | baseline approval record | versioned decision record | governance process | approval completeness check | Blocked: all six named approvals pending |
| R1-REL-004 | Interaction requirements RTM | Requirement/acceptance/permission/audit/flow/state/screen/API/test link schema | validator over 20 `ACT-R1-*` rows and current enum/action sources | AC-INT-01; `validate-release1-journey-docs.mjs` | Source mapping added; stakeholder review and protected evidence pending |

## Coverage metrics

Metrics are calculated over the 100 baseline IDs, not over files or broad capabilities.

| Metric | Rule | Current result |
|---|---|---|
| Requirement rows | One unique RTM row per baseline ID | 100 expected; validator required |
| P0 design mapping | Non-empty current design/source | To validate mechanically |
| P0 API/data mapping | Owning contract or explicit non-API governance seam | To validate mechanically/review |
| P0 implementation mapping | Current code/process seam or explicit gap | Complete mapping; many gaps/blocks remain |
| P0 test mapping | Named test/evaluation or explicit gap | Complete mapping; execution evidence incomplete |
| P0 release evidence | Environment-matched proof and approval | Not complete; RF10 is NO-GO |
| Orphan current Release 1 route | Route has no requirement | Must be checked from current route inventory |
| Orphan critical design state | State has no requirement/test | Must be checked during design reconciliation |

## Maintenance

- Baseline ID changes require synchronized scope, AC, RTM, risk/open-question and roadmap updates.
- Current code/OpenAPI overrides stale API prose; update the owning map rather than blending routes.
- A blank is not allowed: use a concrete link, `Gap`, `Blocked`, `Deferred`, or `Not applicable` with reason.
- Status advances only with cited evidence; approval requires named people and exact version.
