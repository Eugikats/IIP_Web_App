# IIP Release 1 Low-Fidelity Wireframe State Inventory

| Field | Value |
|---|---|
| Version | 0.3 |
| Date | 2026-08-10 |
| Status | Source-complete state contract; expert-remediated critical-flow prototype rendered; participant validation and final direction pending |
| Route/screen authority | `iip_information_architecture_navigation_and_screen_inventory.md` |
| Behavior authority | Release 1 task-flow, interaction, accessibility and recovery contracts |
| Responsive authority | `iip_release_1_responsive_adaptive_reflow_contract.md` |
| Complex-pattern authority | `iip_release_1_complex_operational_pattern_contracts.md` |
| Accessibility authority | `iip_release_1_interaction_accessibility_and_runtime_parity.md` |
| Scope | All canonical `IA-SCR-*` screens; critical Release 1 screens receive first wireframe priority |

## 1. Purpose and boundary

This inventory prevents low-fidelity layouts from representing only a populated happy path. Every canonical screen inherits explicit **loading, empty, error, permission, partial, success and recovery** variants. It does not create routes, permissions, domain states, provider behavior, insurer policy or implementation-complete claims.

The development-only prototype at `/prototype/release-1-low-fidelity?variant=A` renders three competing structures against 12 critical surface/mode entries, eight release roles, all seven governed state families and wide/intermediate/compact contexts. It now exposes the record/work/conversation/evidence/proposal/approval/audit model, exact role and state guards, mediated external handoffs, interruption/resume and low-bandwidth/offline behavior. Run it with `cd web/portal && pnpm dev`. It uses synthetic in-memory data, performs no API/provider mutation, is unavailable in production builds, and is design-review evidence only—not implementation, accessibility conformance, usability, UAT or protected-runtime evidence. The expert diagnostic, participant protocol and remaining gate are recorded in `../research/iip-release-1-low-fidelity-usability-evaluation.md`.

Stable variant IDs are `LFW-{IA-SCR-ID}-{STATE}`, where `{STATE}` is one of `LOADING`, `EMPTY`, `ERROR`, `PERMISSION`, `PARTIAL`, `SUCCESS` or `RECOVERY`. `ERROR` may resolve to the owning `FAILED`, `TIMEOUT`, `UNAVAILABLE` or `NOT-CONFIGURED` contract; `PERMISSION` may resolve to `UNAUTHORIZED`, `RESTRICTED` or read-only. The wireframe must name the exact condition rather than expose those distinctions as generic error.

`ALL-7` means all seven variants are required. It never means all seven use identical copy or controls.

## 2. Shared low-fidelity composition

Wide layout:

```text
+--------------------------------------------------------------------------------+
| Workspace / global status / account                                             |
+----------------------+---------------------------------------------------------+
| Navigation           | Page title + purpose + authority cue                    |
|                      +---------------------------------------------------------+
|                      | Context / filters / state summary                       |
|                      +---------------------------------------------------------+
|                      | Independently changing primary region                   |
|                      | loading | empty | partial | permission | error           |
|                      +--------------------------------+------------------------+
|                      | Evidence / detail region       | Action / status region |
+----------------------+--------------------------------+------------------------+
| Recovery owner / preserved work / next valid exit                               |
+--------------------------------------------------------------------------------+
```

Narrow/reflow layout:

```text
+--------------------------------------+
| Menu | workspace | global status     |
+--------------------------------------+
| Title / purpose / authority cue       |
| State summary and next safe action    |
+--------------------------------------+
| Primary region                       |
+--------------------------------------+
| Evidence/detail (sequential)          |
+--------------------------------------+
| Actions; consequential action last    |
+--------------------------------------+
| Recovery/preserved-work status        |
+--------------------------------------+
```

The shell remains stable while an independently changing region loads or fails. A whole-screen replacement is allowed only when current authority prevents any safe screen content from being shown. The companion responsive contract owns the exact desktop, tablet and mobile transformations, intrinsic breakpoint rationale, state preservation and test scenarios; these sketches establish composition only.

## 3. Seven-state contract

| State | Data condition and required low-fidelity content | Feedback, accessibility and exit |
|---|---|---|
| `LOADING` | Required authoritative data unresolved; show structural placeholders for actual regions, named stage for long work, no fake rows or enabled dependent actions. | Set the owning region busy, retain its accessible name, announce delayed progress sparingly, preserve focus; exit to success, empty, partial, permission or exact error. |
| `EMPTY` | Query succeeded with zero permitted items or a valid first-use object has no content. State whether this is first use, cleared filters or valid zero data where policy permits. | Focus remains on heading/filter or valid create/import action; screen reader receives one useful status; never imply restricted records do not exist. |
| `ERROR` | Request failed, timed out, provider is unavailable/not configured, validation is terminal, or an external effect is unknown. Show affected scope, preserved work, possible effect and safe reference. | Error summary receives programmatic association or focus when task-blocking; raw provider errors stay hidden; exit by correct, reconcile, retry when eligible, replace, escalate or safe return. |
| `PERMISSION` | Server denies the screen/action, removes restricted regions, or permits read-only use. Do not infer authority from navigation visibility. | No protected content/count/existence leak; announce changed authority; move focus to explanation/return action; exit to permitted route, reauthentication or access-owner path. |
| `PARTIAL` | Some independently owned regions/items succeeded and others failed, are stale, conflicted, restricted or still processing. Preserve all safe successful content. | Identify available/missing scope and freshness without color alone; region-local recovery avoids replaying successful work; unsafe consequential actions remain disabled. |
| `SUCCESS` | Authoritative owner confirms the exact read/write/provider outcome. For read screens this is populated/current; for writes it is the durable committed state, not button activation. | Announce once in the owning status region without stealing focus; show object/reference/version and successor task or stable terminal state. |
| `RECOVERY` | Reauthentication, reload, retry, reconciliation, re-evaluation, replay, correction, compensation or escalation is active or available. | Name owner, attempt/stage, preserved state and cancellation boundary; suppress duplicate submit; return focus to recovered region or documented fallback; exit to confirmed success, safe failure or owned manual intervention. |

Every variant also covers keyboard order, visible/unobscured focus, deterministic entry/return/fallback focus, screen-reader heading/landmark/name/role/state/status, 44px high-impact touch targets, AA contrast/non-color cues, reduced-motion equivalents, 200% text, 400%/320px reflow, localization/text expansion, offline/interruption and reauthentication. Each rendered frame must annotate these behaviors using the accessibility authority contract. These are requirements, not conformance claims.

## 4. Screen inventory matrix

| ID | Task/flow | Surface | Actor and entry | Data/system condition | Role/permission | Viewport/input | Requirements | Dependency | Priority/status | State set and notes |
|---|---|---|---|---|---|---|---|---|---|---|
| `IA-SCR-001` | Orientation to current work | Command Center | Workforce user; `/app` | Query/conversation/recent-work regions | `ViewCommandCenter` | Wide/narrow; keyboard/touch | R1-FUN-001/016; R1-UX-001/004/007 | Command query, conversations, shell | P0 / wireframe first | `ALL-7`; partial preserves available answer/recent work; success links exact work |
| `IA-SCR-002` | `FLW-R1-QUEUE-01` | My Work | Claims/support roles; nav/handoff | Five source-owned projections and filters | Per-source view permission | Table/list/reflow; high volume | R1-FUN-003; R1-SEC-002; R1-UX-001 | My Work projection and source APIs | P0 / wireframe first | `ALL-7`; empty says no authorized matches; recovery preserves filter/order |
| `IA-SCR-003` | Find/open claim | Claims list | Claims roles; nav/search return | Claim list/filter/pagination | Assigned/all claim view | Table/list; keyboard/touch | R1-FUN-003/018; R1-UX-001 | Claim query and access filter | P0 / wireframe first | `ALL-7`; permission must not leak counts; partial identifies unavailable fields/source |
| `IA-SCR-004` | `FLW-R1-INTAKE-01` | New claim notification | Claims Officer; channel/manual handoff | Pristine/invalid/duplicate/saving/created | `CreateClaim` | Form; wide/narrow/mobile | R1-FUN-002; R1-DAT-006; R1-UX-004/007 | Case intake and duplicate match | P0 / wireframe first | `ALL-7`; empty is pristine first use; success opens exact case; recovery preserves safe input |
| `IA-SCR-005` | Case/evidence/decision hub | Case workspace | Claims/approver/compliance; queue/search | Header, facts, tasks, evidence, comms, timeline | Assigned/all case plus action guards | Multi-region wide/sequential narrow | R1-FUN-004/009–013/019/020; R1-UX-001–007 | Case and source-owned regions | P0 / wireframe first | `ALL-7`; region-level partial mandatory; recovery compares current evidence/authority |
| `IA-SCR-006` | `FLW-R1-EVIDENCE-01` | Extraction review | Evidence reviewer; case document | Source preview, fields, confidence, dependencies | Claim read plus current document action | Split wide/sequential narrow | R1-FUN-006–009; R1-A11Y-001–009 | Blob/content, extraction, correction | P0 / wireframe first | `ALL-7`; permission separates view/edit; success triggers visible invalidation/re-evaluation |
| `IA-SCR-007` | `FLW-R1-WA-01`/`COMMS-01` | Channels | Customer Service/Claims; nav/case handoff | Threads, linkage, draft, provider status | `ViewChannels` plus case/action guards | Thread wide/narrow/mobile/low bandwidth | R1-WA-001–004; R1-FUN-014/020 | WhatsApp/provider and communication state | P0 / wireframe first | `ALL-7`; empty distinguishes no threads from filters; recovery reconciles unknown send |
| `IA-SCR-008` | Find customer | Customers list | Customer Service/Claims; nav | Authorized customer list/filter | `ViewCustomers` | Table/list/reflow | R1-FUN-004/018; R1-PRI-001 | Customer query/access filter | P1 / family wireframe | `ALL-7`; empty/restricted never expose hidden customer existence |
| `IA-SCR-009` | Inspect customer context | Customer record | Authorized staff; list/case handoff | Customer facts, linked claims/comms | `ViewCustomers` plus object scope | Record wide/narrow | R1-FUN-004; R1-PRI-001/003 | Customer/claim/communication reads | P1 / family wireframe | `ALL-7`; partial keeps safe known facts; recovery rechecks object access |
| `IA-SCR-010` | Global document work | Documents | Authorized document/claims staff | Document list/status/provider | `ViewAllClaims` current contract | Table/list; high volume | R1-FUN-005–009; R1-UX-001 | Document query/provider | P1 / family wireframe | `ALL-7`; permission highlights assigned-only limitation; provider absence is exact error subtype |
| `IA-SCR-011` | Review outbound/inbound history | Communications | Customer Service/Claims | Communication list/delivery states | Assigned/all claim view; action guards | Table/list/reflow | R1-FUN-014; R1-WA-003–004 | Communication/provider history | P1 / family wireframe | `ALL-7`; success distinguishes draft/approved/accepted/delivered; recovery reconciles unknown |
| `IA-SCR-012` | Manage approved knowledge | Knowledge | Knowledge Manager; nav | Sources, ingestion/index state | `ManageKnowledgeBase` | Table/panel/reflow | R1-AI-004/005; R1-UX-001 | Knowledge provider/source config | P1 conditional / family wireframe | `ALL-7`; empty differs from not configured; recovery shows re-index/owner |
| `IA-SCR-013` | Acquisition work | Acquisition | Sales role; nav | Lead records | `ViewSalesLeads` | Table/list | Outside Release 1 | Sales API | P3 deferred | `ALL-7`; state inventory only, no Release 1 task expansion |
| `IA-SCR-014` | Consult guidance | Policy Guidance | Authorized claims/knowledge role | Guidance source/query/output | AI-output or knowledge permission | Search/result/reflow | R1-AI-002/004/005; R1-UX-001 | Approved knowledge/provider | P1 conditional / family wireframe | `ALL-7`; empty/no source and not configured stay distinct; success remains guidance, not decision |
| `IA-SCR-015` | Inspect advisory signal | Risk Signals | Authorized specialist; nav/case | Advisory signals/evidence | `ViewFraudSignals` | List/detail/reflow | R1-AI-001–004; R1-PRI-003 | Advisory-only API | P3 outside R1 decision path | `ALL-7`; permission/customer-safety strict; success never labels confirmed fraud |
| `IA-SCR-016` | Underwriting advisory | Underwriting | Authorized underwriting role | Advisory records/output | `ViewUnderwriting` | List/detail/reflow | R1-AI-001–004 | Advisory-only API | P3 deferred | `ALL-7`; no Release 1 consequential workflow inferred |
| `IA-SCR-017` | View workflow definitions | Flows | Workflow viewer/operator | Definition list/version state | `ViewAgentWorkflows` | Table/list/reflow | R1-AI-005; R1-OPS-001 | Workflow registry | P1 supporting / family wireframe | `ALL-7`; partial shows version/provider gaps; recovery retains filters |
| `IA-SCR-018` | Create workflow | New flow | Authorized workflow manager | Pristine/invalid/draft/save/publish | Manage/run permission by action | Form/canvas; keyboard/reflow alternative | R1-AI-005/007; R1-UX-005–007 | Workflow validation/publish contract | P3 outside basic R1 | `ALL-7`; empty is pristine; success distinguishes saved from published; recovery preserves draft |
| `IA-SCR-019` | Inspect workflow | Flow detail | Workflow viewer/operator | Definition/version/configuration | `ViewAgentWorkflows`; actions rechecked | Record/panel/reflow | R1-AI-005/007; R1-OPS-001 | Workflow registry/runtime | P1 supporting / family wireframe | `ALL-7`; partial separates definition from provider/run health |
| `IA-SCR-020` | Inspect run | Run detail | Workflow viewer/operator | Run steps/events/errors | `ViewAgentWorkflows` | Timeline/detail/reflow | R1-AI-005/007; R1-OPS-001/002 | Run/work item/audit | P1 supporting / family wireframe | `ALL-7`; error and recovery expose safe operator path, not raw provider payload |
| `IA-SCR-021` | Review operations metrics | Operations Intelligence | Authorized leader; nav | Metric panels/freshness/coverage | `ViewCommandCenter` current contract | Dashboard/reflow/table alternative | R1-BUS-004; R1-DAT-008 | Reporting/telemetry | P2 conditional | `ALL-7`; partial/freshness mandatory; success is observed metric, not ROI proof |
| `IA-SCR-022` | Reconstruct/export evidence | Audit & Compliance | Compliance; nav/case handoff | Audit events/filter/export | `ViewAuditLog` plus scope | Table/print/reflow | R1-FUN-015; R1-DAT-011; R1-PRI-004 | Audit/export | P0 / wireframe first | `ALL-7`; empty is valid scoped zero; partial export names omissions; recovery preserves filters |
| `IA-SCR-023` | Provider/config health | Integrations & Health | IT/Security/operator | Capability/config/health/attempts | Configure integration or tenant settings | Status/table/reflow | R1-SEC-004; R1-OPS-001/002 | Provider registry/health | P0 / wireframe first | `ALL-7`; error distinguishes not configured/unavailable/timeout; recovery names owner/attempt |
| `IA-SCR-024` | Tenant settings | Admin Console | Tenant admin | Settings/validation/version | `ManageTenantSettings` | Form/panel/reflow | R1-SEC-001/004/005; R1-UX-007 | Tenant configuration | P1 operational / family wireframe | `ALL-7`; partial sections remain explicit; success gives version; recovery compares concurrent change |
| `IA-SCR-025` | Agent controls | Agents | Agent admin/operator | Agent list/health/kill state | `ManageAgents` | Table/panel/reflow | R1-AI-007; R1-OPS-001/002 | Agent registry/kill switch | P1 supporting / family wireframe | `ALL-7`; recovery includes controlled restart, affected work and authority |
| `IA-SCR-026` | Govern agent policy | Agent Policy | Tenant admin | Policy values/version/validation | `ManageTenantSettings` | Form/table/reflow | R1-AI-005–007; R1-SEC-005 | Policy configuration | P1 supporting / family wireframe | `ALL-7`; empty means no approved policy and blocks unsafe use; recovery preserves prior valid version |
| `IA-SCR-027` | Tenant onboarding | Tenant Onboarding | Platform onboarding admin | Steps/config/evidence/readiness | `ManageTenantOnboarding` | Stepper/reflow/keyboard | R1-REL-001–004 | Identity/config/provider/deployment owners | P3 outside staff work | `ALL-7`; partial readiness never becomes success; recovery resumes exact incomplete step |
| `IA-SCR-028` | Billing/usage | Billing & Usage | Commercial admin | Usage/invoice/config data | `ViewBilling` | Table/card/reflow | Outside Release 1 | Billing API | P3 deferred | `ALL-7`; state inventory only; no claims authority or workflow inferred |
| `IA-SCR-029` | Compatibility redirect | My Work alias | Authorized user; old deep link | Redirect/target state | Target permissions | Redirect status; keyboard | IA alias contract | Target `IA-SCR-002` | P2 remove/migrate | `ALL-7`; variants are redirecting, target state or recovery; never independent content |
| `IA-SCR-030` | Compatibility redirect | Integrations alias | Authorized operator; old deep link | Redirect/target state | Target permissions | Redirect status; keyboard | IA alias contract | Target `IA-SCR-023` | P2 remove/migrate | `ALL-7`; variants delegate to target; permission/error does not reveal target data |
| `IA-SCR-031` | Compatibility redirect | Admin alias | Tenant admin; old deep link | Redirect/target state | Target permissions | Redirect status; keyboard | IA alias contract | Target `IA-SCR-024` | P2 remove/migrate | `ALL-7`; recovery offers canonical route; no competing state owner |
| `IA-SCR-032` | Authorized retrieval | Search IIP | Claims/approval roles; top bar | Query/results/group/access/return | Assigned/all claims; approval permission for approval hits | List/grouped; keyboard/narrow | R1-FUN-018; R1-SEC-002; R1-UX-001 | Current source repositories | P1 / wireframe first | `ALL-7`; empty means no authorized results; partial identifies source class without leaking counts |
| `IA-SCR-033` | Reopen prior work | All conversations | Command Center user; nav/sidebar | History/filter/archive/access | `ViewCommandCenter` plus owner/scope | List/grouped/narrow | R1-FUN-016; R1-DAT-005 | Conversation history | P1 / wireframe first | `ALL-7`; empty is first use/filters; recovery handles version conflict/access change |
| `IA-SCR-034` | Continue grounded work | Command Center conversation | Owner; conversation list/deep link | Historical messages/current-evidence changes/run state | `ViewCommandCenter` plus owner/scope | Conversation wide/narrow/mobile | R1-FUN-016; R1-DAT-005; R1-UX-007 | Conversation/query/current evidence | P1 / wireframe first | `ALL-7`; partial separates history from current grounding; recovery reauthorizes and summarizes change |

## 5. Family-level state transitions

| Family | Screens | Loading → settled | Failure/permission branch | Recovery → successor |
|---|---|---|---|---|
| Dashboard/conversation | 001, 021, 034 | Region skeletons → current populated/valid zero | Failed region remains local; historical and current evidence stay distinct | Reauthenticate/re-ground → current answer/work link or safe blocked state |
| Queue/list/search | 002, 003, 008, 010–013, 017, 021–023, 025, 028, 032, 033 | Shell + filter retained → empty/populated/partial | Permission filters before matching/counting; failed source cannot erase successful rows | Retry failed source/restore filter → refreshed list and prior position |
| Record/detail | 005, 009, 014–016, 019, 020 | Identity/context first → independently settled regions | Whole-screen denial only when no safe context; stale/conflict blocks unsafe action | Reload/compare/re-evaluate → current record or owned escalation |
| Form/configuration | 004, 018, 024, 026, 027 | Pristine valid zero → validation/saving → durable success | Invalid, conflict, permission and provider errors preserve safe input | Reauthenticate/reload/compare/reapply → saved version or safe cancel |
| Evidence/review | 006 | Source/fields/dependencies load independently → reviewable/partial | Missing source, restricted content, provider absence and conflict remain distinct | Correct/retry/re-evaluate → exact current reviewed version |
| Channel/communication | 007, 011 | Thread/history/preflight/provider settle independently | Unknown external effect is not failure or success; permission removes protected content | Reconcile before retry → confirmed state or manual intervention |
| Audit/export | 022 | Filter/table/export regions settle independently | Partial export names omissions; permission never leaks events | Retry failed safe scope → immutable pack or explicit partial/failure |
| Provider/operations | 023, 025 | Capability/config/health/attempt resolve separately | Not configured, unavailable, timeout and failed remain distinct | Configure/restore/reconcile/controlled restart → verified readiness, never simulated |
| Compatibility redirect | 029–031 | Redirecting → canonical target | Target permission/error is rendered by canonical owner | Canonical link/re-auth → target; alias retains no state |

## 6. Roles, entry points and handoffs

| Role | Primary low-fidelity screens | Entry/handoff to preserve |
|---|---|---|
| Claims Officer | 001–007, 010–011, 032–034 | Channel/queue/search → case → evidence/assessor/approval preparation → communication |
| Supervisor/Authorized Approver | 002, 005, 022, 032 | Consequential queue → exact proposal/evidence → human outcome → return/execution status |
| Customer Service/Complaints | 002, 005, 007–009, 011 | Channel/customer → case-safe context → draft/send or mediated correction/contestability |
| Compliance | 005, 022, 032 | Case/review request → lineage/audit/export → remedy handoff |
| Knowledge/IT/Security/Admin | 012, 017–020, 023–027 | Configuration/health/workflow → incident or controlled recovery |
| External assessor/claim party/broker | No staff screen | Staff-mediated channel and case handoffs only; no Release 1 external portal is invented |

## 7. Content/data dependencies

- Labels/routes: canonical IA only.
- Business and provider state: owning API/Application/provider contract, never the wireframe.
- Permissions: server enum/action/object/state enforcement; a hidden control is not authorization.
- Evidence/currentness: source/version/provenance and invalidation contracts.
- Success: authoritative owner confirmation and safe audit reference.
- Recovery: stable idempotency/correlation, preserved work, current authority and explicit owner.
- Copy: what happened, affected scope, preserved work, possible external effect, next safe action and owner; never raw provider errors or restricted data.

## 8. Unresolved rules and gaps

| ID | Gap | Treatment |
|---|---|---|
| `LFW-GAP-01` | A critical-flow interactive prototype is rendered and expert-remediated, but screen-specific variants have not been participant-tested or cross-functionally approved against every canonical P0 state condition. | Run `UT-LFW-01`–`10`, select/combine a direction from evidence, then annotate each P0 screen with all seven exact variant IDs and findings. |
| `LFW-GAP-02` | Practitioner comprehension, task completion and recovery evidence is absent. | Test P0 layouts with representative roles; do not claim usability validation. |
| `LFW-GAP-03` | Target-insurer titles, permissions, disclosure/configuration and SLA rules remain conditional. | Use role/authority labels and decision-required annotations; do not invent values. |
| `LFW-GAP-04` | Desktop/tablet/mobile reflow, touch, screen-reader, forced-colors and reduced-motion behavior is specified in the responsive companion but not verified in rendered wireframes/code. | Review every P0 state at desktop, tablet and 320px/mobile anchors, keyboard and supported assistive-technology checkpoints. |
| `LFW-GAP-05` | Deferred/out-of-scope routes have state coverage but no Release 1 task justification for visual expansion. | Keep low priority; do not spend visual-design effort without scope approval. |

## 9. Design and QA priority

1. P0 first: `IA-SCR-001`–`007`, `IA-SCR-022`, `IA-SCR-023`.
2. P1 handoffs: `IA-SCR-032`–`034`, then supporting family screens.
3. For each screen, render `LOADING`, `EMPTY`, `ERROR`, `PERMISSION`, `PARTIAL`, `SUCCESS`, `RECOVERY` before visual polish.
4. Test region-local failure, safe focus/announcement, preserved work, duplicate suppression, narrow layout and reauthentication.
5. Review with Product, Insurance Operations, Engineering, Security/Privacy, Accessibility and representative practitioners; record expected/actual result and defects.

Source completeness means the screen/state matrix is internally consistent and mechanically checked. The interactive prototype supplies rendered design-review material only; it does not mean layouts are approved, participant-validated, implemented, accessibility-conformant or verified in a protected runtime.
