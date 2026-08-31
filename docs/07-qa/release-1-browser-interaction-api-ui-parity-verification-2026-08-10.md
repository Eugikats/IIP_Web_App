# Release 1 browser interaction and API/UI parity verification — 2026-08-10

## Scope and verdict

Local verification only. This pass inspected current client and API contracts, exercised the portal with the Playwright CLI, reran the existing governed-dialog accessibility diagnostic, and ran focused contract tests.

Overall status: **partial / not release evidence**.

- Browser loading-failure, unavailable, retry, unverified-environment, and provider `Not configured` behavior is honest.
- Main navigation is keyboard reachable in a stable order.
- Governed-dialog focus placement, focus containment, focus return, reduced-motion behavior, naming, labelling, contrast, and target sizing do not meet the interaction/accessibility contract.
- Server-side WhatsApp idempotency, unresolved-attempt reconciliation, delivery-event duplicate safety, and conversation optimistic concurrency pass focused tests.
- WhatsApp client retry idempotency is not API-parity safe: it creates a new key for every send invocation, including a retry after an ambiguous outcome.
- A configured durable API, authenticated identities, real providers, and protected staging were unavailable. No end-to-end send, audit correlation, session-expiry, or protected-runtime claim is made.

## Environment and evidence labels

| Item | Value |
|---|---|
| Date/time zone | 2026-08-10, Africa/Nairobi |
| Portal | local Next.js development server, `http://127.0.0.1:3102` |
| Fixture surface | local `LovableVisualParity` server, `http://127.0.0.1:3103`; synthetic and non-production |
| API | blocked: production API rejected inherited `INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory` and required the isolated local test host |
| Browser driver | prescribed Playwright CLI wrapper; named sessions `iip-r1` and `iip-fixture` |
| Browser artifacts | `output/playwright/.playwright-cli/` |
| Contract evidence | current source plus focused .NET and portal checks |
| Excluded | protected staging, real provider effects, practitioner validation, usability conformance |

## Browser behavior results

| Behavior | Result | Evidence |
|---|---|---|
| Environment failure | Pass | Live `/app/channels` announced an unverified environment and showed workspace/provider `Not configured`; it did not substitute fixture data or fake success. |
| Empty/unavailable | Pass | Live channels showed `No channel conversations` plus `Failed to fetch`. Live conversations showed `Conversations unavailable`, a safe reason, and `Retry`. |
| Keyboard path | Partial | Twelve successive `Tab` presses reached sidebar collapse, Search, New Conversation, All, then Command Center through Knowledge in DOM order. Several navigation links had no visible focus box-shadow in the diagnostic. |
| Dialog initial focus | Fail | Opening `Export claim evidence pack` left focus on the underlying `Export claim pack` trigger. |
| Dialog focus containment | Fail | The first five `Tab` stops after opening remained outside the modal before focus entered `Close confirmation`. |
| Escape and focus return | Fail | Escape closed the dialog but focus remained on the now-hidden `Cancel` control instead of returning to the trigger. |
| Dialog semantics | Partial | `role="dialog"`, `aria-modal`, and a labelled title exist; the diagnostic still found `aria-hidden-focus`, missing labels, and unnamed buttons. |
| Duplicate click suppression | Partial | `GovernedActionDialog` sets `isSubmitting`, disables close/cancel/confirm, and changes copy to `Submitting...` while its promise is active. No browser/API effect count could be proven without a configured API. |
| Reduced motion | Fail | With reduced motion requested, the dialog retained a `0.2s` transition and hover-lift retained `0.18s` transitions. |
| Touch targets | Fail | Diagnostic found many controls below 44 CSS px and several below 24 CSS px across My Work, claim review, and extraction review. |
| Automated accessibility | Fail | Critical accessible-name/label/select-name failures and serious contrast/scrollable-region/aria-hidden-focus failures remain. |

The three browser diagnostic tests passed as evidence collectors; this does **not** mean their recorded behavior passed accessibility requirements.

## Duplicate submission, state, and API/UI parity

| Contract | Client behavior | Server behavior | Result |
|---|---|---|---|
| Consequential dialog submission | Confirm disabled until an eight-character reason; disabled during the pending promise | Endpoint-specific authorization/audit remains server-owned | Partial: concurrent repeat is visually suppressed; focus and live announcement remain deficient |
| WhatsApp outbound idempotency | `channels.ts` creates `crypto.randomUUID()` inside each `send` call | Same key returns the original message; different payload with same key conflicts; unresolved attempt blocks a new key | **Gap:** retry after timeout creates a different key, so client cannot safely replay the same logical intent |
| WhatsApp provider ambiguity | UI reloads after a resolved response; no stable retry intent or reconciliation UI proven | Timeout/network ambiguity becomes `Unknown`; new sends are blocked pending reconciliation | Partial: safe server behavior, incomplete client recovery contract |
| WhatsApp authority | Client sends `humanApproved` and the interaction requires governed human confirmation | Server requires `ManageCustomerCommunications` and `SendCustomerUpdate` and independently validates approval/content/recipient/provider rules | Pass by source/test contract; live authorized and denied browser paths blocked |
| WhatsApp audit | Client does not claim audit completion | Server records `whatsapp.outbound.attempted` with state/message metadata and omits message body | Pass by source/test contract; live UI-to-audit correlation blocked |
| Conversation archive/restore concurrency | Client sends current `expectedVersion`, disables the changing row, maps HTTP 409 to refresh guidance, and removes the row only after success | Server compares expected version, returns `conversation_conflict`, and audits archive/restore | Pass by source and focused tests; live browser mutation blocked |
| Conversation unavailable/retry | Client exposes explicit unavailable copy and Retry | API unavailable in this environment | Pass for honest failure UI; recovery-to-success unverified |

### Required parity change

Persist one client-generated idempotency key for the complete logical WhatsApp send attempt and reuse it for ambiguous retry until reconciliation establishes a terminal outcome. Generate a new key only for a new user intent. Expose `Queued`, `Unknown`, reconciliation-required, failed, delivered, and retry-eligible states without treating provider acceptance as delivery.

No runtime change is authorized or made by this verification.

## Accessibility findings applied to interactions

Every consequential dialog and asynchronous action remains subject to these unmet gates:

1. Move focus into the dialog on open, trap it while open, and return it to the invoker on close.
2. Make background content inert while a modal is open; do not leave focusable descendants under `aria-hidden`.
3. Announce validation, submitting, success, failure, retry, and reconciliation status without moving focus unexpectedly.
4. Preserve keyboard activation and visible focus for every interactive control.
5. Meet accessible name/label, contrast, and touch-target requirements.
6. Disable non-essential transition/transform effects under `prefers-reduced-motion: reduce`.
7. Keep disabled reasons perceivable; do not rely on color, hover, or disabled state alone.

## Commands and actual results

| Command/check | Result |
|---|---|
| `Get-Command npx.cmd` | Pass: `C:\Program Files\nodejs\npx.cmd` |
| Playwright CLI live `/app/channels` and `/app/conversations` snapshots | Completed; honest unavailable/not-configured/retry states recorded |
| Playwright CLI 12-step keyboard trail | Completed; stable reachable order recorded |
| `pnpm exec playwright test -c tests/visual/playwright.config.ts tests/visual/specs/gate-13-interaction-accessibility.audit.spec.ts --project=desktop-chromium` | 3/3 diagnostic collectors passed; recorded failures above; Google Font downloads timed out and fallback fonts were used |
| Focused `WhatsAppMessagingServiceTests` and `CommandCenterConversationServiceTests` | 8/8 passed |
| `pnpm test` | Pass: runtime-data hygiene, provider status, role routes, UI release gates |
| `node tools/qa/validate-release1-journey-docs.mjs` | Pass: 16 artifacts, 91 requirements, 20 interactions; existing 8 accessibility, 3 runtime-parity, 8 surface-control, 8 recovery, and 10 permission/audit gaps remain registered |
| Production API local startup | Blocked by intentional InMemory-provider guard; no live API execution claimed |

## Readiness

- Source inspection: complete for the scoped interactions.
- Local browser verification: partial; failures recorded.
- Local API contract tests: passed for scoped idempotency/concurrency behavior.
- Integrated local client/API verification: blocked by durable API configuration.
- Protected staging/UAT: not run.
- Accessibility conformance: failed / not established.

Unresolved questions:

- Which client store owns the stable WhatsApp logical-attempt idempotency key across interruption and reauthentication?
- Which reconciliation endpoint/status contract is approved for an outbound `Unknown` state?
- When will a durable local or protected test environment and role identities be available for positive/denied browser journeys and audit correlation?
