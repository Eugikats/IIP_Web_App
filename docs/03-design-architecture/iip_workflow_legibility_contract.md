# IIP Workflow Legibility Contract

| Field | Value |
|---|---|
| Version | 1.0 |
| Date | 2026-09-08 |
| Status | **Binding UI contract.** Owns how the claim lifecycle is made visible across every surface: stage vocabulary, ownership, completeness display, evidence display, and the human-decision boundary in the interface. |
| Applies to | `web/portal`, every surface that renders a claim, work item, document, or agent output. |
| Source of truth for state | The Domain enums cited in §10. This document never invents a state; it renders the ones that exist. If code and this document disagree, code wins and this document is corrected in the same change. |
| Companion documents | `iip_design_language_v2.md` (component treatment), `iip_ux_cognitive_load_and_navigation_model.md` (where things live), `iip_ui_craft_guardrails.md` (craft floor) |

---

## 1. Why this document exists

A user must be able to answer, from any screen that shows a claim, without clicking anything:

- What stage is this claim at?
- Who owns it right now — the customer, an agent, or a named human?
- What did the automation already do?
- What is missing?
- What is the one thing that happens next, and who does it?

Today those answers are scattered. This contract fixes the vocabulary and the display rules so that the same claim reads identically on the Assistant, in My work, in the Claims queue, on the claim detail, and in a notification.

**The rule underneath all of it:** the interface must never make the automation look more authoritative than it is, and never make a human decision look like it has already been made.

---

## 2. The canonical claim journey

This is the governed path. Every stage below maps to real backend state; nothing here is aspirational.

```
  CUSTOMER              AGENT (automation)                    HUMAN (accountable)
  ────────              ──────────────────                    ───────────────────
  Submits FNOL
  via WhatsApp,   ──▶   Intake: create claim, classify
  email, portal         claim type, extract core fields
  or branch
                        Triage: route by claim type,
                        assign path (HappyPath /
                        ExceptionRoute), set priority
                                    │
                        Document check: classify each
                        received document against the
                        required checklist for this
                        claim type
                                    │
                        Completeness score:
                        received ÷ required, list what
                        is missing
                                    │
                 ┌──────────────────┴──────────────────┐
                 ▼                                     ▼
        Incomplete                              Complete
        MissingDocuments                        ReadyForReview
                 │                                     │
  Receives  ◀────┤ Drafts a request for the            │
  request        │ missing documents ──▶ HUMAN APPROVES THE SEND
                 │                                     │
  Supplies  ─────┘                                     ▼
  documents                              Extraction + case review:
  (loop)                                 summary, open questions,
                                         risk signals, confidence,
                                         evidence refs
                                                       │
                                                       ▼
                                              ┌────────────────────┐
                                              │  HUMAN REVIEW      │
                                              │  Accept / Return / │
                                              │  Escalate          │
                                              └────────┬───────────┘
                                                       ▼
                                              PendingApproval
                                              (authorised approver)
                                                       │
                                    ┌──────────────────┼──────────────────┐
                                    ▼                  ▼                  ▼
                               Escalated          Settled            Closed
                                                       │
                                              Customer communication
                                              (human-approved) and
                                              audited write-back
```

Everything in the AGENT column is preparation. Everything in the HUMAN column is authority. The interface must make that column boundary visible at all times — see §9.

---

## 3. Stage map

The binding table. For each stage: the backend state, who holds it, what the UI must show, and the single next action.

| # | Stage | Backend state | Held by | The UI must show | Next action, and whose |
|---:|---|---|---|---|---|
| 1 | Received | `CaseStatus.New` | System | Channel it arrived on, timestamp, claimant, claim type if classified | Agent triages — automatic |
| 2 | Triaged and assigned | `CaseStatus.Assigned` | Named human | Assignee avatar and name, priority, SLA target and remaining time | Owner opens the claim |
| 3 | Gathering evidence | `CaseStatus.MissingDocuments` | Customer | Completeness percentage, received list, **missing list by name**, whether a request has been sent and when | Human approves the document request; then customer supplies |
| 4 | Under review | `CaseStatus.InReview` | Named human | AI narrative card, extracted fields with confidence, case-review summary, open questions, risk signals, every citation | Reviewer accepts, returns, or escalates |
| 5 | Ready for review | `CaseStatus.ReadyForReview` | Named human | Everything complete, prepared recommendation, what the approver will see | Reviewer submits for approval |
| 6 | Awaiting approval | `CaseStatus.PendingApproval` | Authorised approver | Who it is with, since when, what is being asked, prepared evidence pack | Approver decides |
| 7 | Escalated | `CaseStatus.Escalated` | Designated human | Escalation trigger, who raised it, who owns it now, why | Designated human resolves |
| 8 | Settled | `CaseStatus.Settled` | System / human | Decision, decider, timestamp, communication status | Write-back and close |
| 9 | Ready for export | `CaseStatus.ReadyForExport` | System | What will be written back and to which system of record | Export executes, audited |
| 10 | Closed | `CaseStatus.Closed` | — | Full timeline, all decisions and deciders | None; read-only |

Rules:

- **Owner is always a name or an explicit non-human label.** Never blank. Never "system" when a human is actually accountable. The three permitted owner labels are a named person, `Customer`, and `Automation`.
- **Every stage displays its SLA state** (`OnTrack` / `AtRisk` / `Breached`) using the dot-plus-label pill.
- **The next action is singular.** If a stage appears to have two next actions, the stage model is wrong — fix the model, not the screen.

---

## 4. The progress indicator

A horizontal stage strip at the top of the claim detail, below the header block.

Design: stage labels in `12px/600`, connected by `1px` `--color-border-subtle` rules. Completed stages show a `14px` check in `--color-success-text`. The current stage is `--color-text-primary` weight 700 with a `6px` dot in its SLA colour. Future stages are `--color-text-muted`. No fill bars, no percentages, no animation.

Two constraints, both carried over from the recorded critique in `iip_visual_direction_review.md` (finding 1), which warned that a permanent stage spine implies every claim is linear:

1. **Render the strip only for claims on a path whose stages are actually known** (`ClaimPathMode.HappyPath`). For `ExceptionRoute`, render the case timeline of real events instead — do not draw a linear spine over a non-linear claim.
2. **Never show a stage that cannot occur** for this claim type. The strip reflects this claim's configured path, not a generic template.

Beneath the strip on every claim, always: `Owner · SLA · Priority · Completeness`, as four labelled values on one row.

---

## 5. Triage: what the interface must say

When an agent triages a claim, the UI reports it as a completed, attributable action — never as an ambient background event.

The claim detail's AI narrative card (design language §8.19) states, in this order:

1. **What was received** — "6 documents across 2 messages, received 14:22 today."
2. **What was recognised** — "Classified as motor own damage. Matched against the motor own-damage checklist (7 required documents)."
3. **What was found** — "5 of 7 required documents received. Police abstract and customer ID are missing."
4. **What needs a human** — "The garage estimate scored 0.71 on total amount, below the 0.85 threshold. Please verify against the invoice."

Every one of those sentences carries a citation chip. The card names the agent and the model version that produced it, and the time. Triage output that cannot say which agent produced it must not render.

Triage never states liability, never proposes a settlement amount, and never characterises a claimant.

---

## 6. Document intelligence display

For each document, the row shows: document type (classified), original filename, source channel, received time, processing state, and — where extraction ran — a confidence summary.

`DocumentProcessingStatus` renders as:

| State | Label | Dot | Means |
|---|---|---|---|
| `Uploaded` | Received | info | Stored, not yet processed |
| `Processing` | Processing | info | In flight; show elapsed time if over 30s |
| `Processed` | Processed | success | Extraction complete, within thresholds |
| `ReviewRequired` | Needs review | warning | Below threshold or failed validation — a human must look |
| `Reviewed` | Reviewed | success | A named human checked it; show who and when |
| `ExtractionApproved` | Approved | success | Extracted values accepted into the claim |
| `Failed` | Failed | danger | Processing failed; show a plain-language reason and a retry, never a raw provider error |
| `Rejected` | Rejected | danger | A human rejected it; show who, when, and why |

A document in `ReviewRequired` is the only kind that generates work. Surface it in My work; do not rely on the user browsing the Documents screen to discover it.

Extracted fields render as label, value, confidence (design language §8.22), and a citation chip that opens the source region in the side drawer. Fields below threshold sort to the top and are expanded; fields above threshold collapse behind `{n} fields verified`.

---

## 7. Completeness — exact display rules

Completeness is `CompletenessScore(Percentage, RequiredReceived, RequiredTotal)`, computed as received required documents ÷ required documents for that claim type.

Mandatory display, everywhere completeness appears:

1. **The percentage**, from the backend. Never computed in the portal.
2. **The denominator in words** — `5 of 7 required documents received`. A percentage alone is prohibited; 71% means nothing without the fraction behind it.
3. **The missing items by name**, using the insurer's document names (`Police abstract`, `Customer ID`), never enum identifiers, never counts alone.
4. **What is being done about it** — whether a request has been drafted, whether a human approved sending it, when it was sent, and whether a reminder is due.

Colour rules: the percentage takes `--color-text-primary` at any value. It does **not** turn red at a low number — a claim at 20% on its first day is normal, not a problem. Urgency comes from SLA state, not from completeness. This distinction matters: conflating them trains users to ignore red.

`RequiredTotal` of zero returns 100% by design. Where that occurs, render `No documents required for this claim type` rather than a bare `100%`, which would otherwise read as a fully-evidenced claim.

---

## 8. Evidence and confidence

- Every AI-asserted fact a human may act on carries a citation chip (design language §8.20). No citation, no render.
- Clicking a citation opens the side drawer at that document, scrolled to and highlighting the source region. The decision the user was making stays visible behind the drawer.
- Confidence is always a number plus a word, never a bar alone, never a colour alone.
- Thresholds come from backend configuration. The portal must not hardcode `0.85` or any other value.
- The case review summary (`CaseReviewSummary`) renders its overview, its open questions, and its risk signals as **advisory observations**, explicitly labelled as such, with the generating provider and model version shown. Risk signals are never rendered as findings, determinations, or accusations — the domain model itself records that they are advisory only, and the UI must not upgrade them.

---

## 9. The human-decision boundary in the interface

This section restates `AGENTS.md` invariants as pixel-level rules. It is not negotiable.

**The automation may render:** summaries, classifications, extracted fields, missing-document lists, completeness scores, retrieved policy text with citations, drafted customer messages marked as drafts, advisory risk observations, recommended next actions, and prepared evidence packs.

**The automation must never render:** an approved or rejected claim, a settlement or payment decision, a bound policy or final premium, a final underwriting decision, a fraud determination, an accusation about any person, or a sent regulated communication.

Interface rules that enforce this:

1. **Visual separation.** Prepared work sits in the violet-tinted AI narrative card. The human decision sits in a separate, neutral, bordered decision bar with its own heading. They are never in the same container and never share a background.
2. **The decision bar states the authority.** `This decision is yours as Claims Supervisor.` If the signed-in user lacks the authority, the bar says who does and offers `Send to approver` instead — never a disabled button with no explanation.
3. **Verbs are explicit and human.** `Approve claim`, `Return for correction`, `Escalate to supervisor`. Never `Confirm`, `OK`, `Submit`, `Continue`, or `Accept AI recommendation`.
4. **Consequence preview before commitment.** The confirmation modal names what will happen, to whom, and whether it is reversible — "This approves CLM-2026-0184 for settlement preparation and notifies the claimant. This cannot be undone."
5. **A draft is visibly a draft** until a human approves it. A drafted customer message renders with a `Draft — not sent` pill and an explicit send approval step. `CustomerUpdateStatus` must be shown, never assumed.
6. **Recommendations are labelled as recommendations** and never pre-selected. A pre-checked AI recommendation is a decision made by software.
7. **Attribution is permanent.** Every decision displays who made it, in what role, when, and on what evidence — on the object and in the audit trail.

---

## 10. Status vocabulary

One label per state, product-wide. The same state must never appear as "Needs review" on one screen and "Requires review" on another — inconsistent vocabulary is the cheapest way to make a product feel unfinished.

### 10.1 Claim — `CaseStatus`

| Enum | Label | Dot | Owner shown |
|---|---|---|---|
| `New` | Received | info | Automation |
| `Assigned` | Assigned | info | Named person |
| `InReview` | Under review | info | Named person |
| `MissingDocuments` | Waiting on documents | warning | Customer |
| `ReadyForReview` | Ready for review | info | Named person |
| `PendingApproval` | Awaiting approval | warning | Named approver |
| `ReadyForExport` | Ready for export | info | Automation |
| `Escalated` | Escalated | danger | Named person |
| `Settled` | Settled | success | — |
| `Closed` | Closed | neutral (no dot) | — |

### 10.2 Work item — `AgentWorkItemStatus`

| Enum | Label | Dot |
|---|---|---|
| `RequiresReview` | Requires review | danger |
| `InProgress` | In progress | info |
| `Completed` | Completed | success |
| `ReturnedForCorrection` | Returned for correction | warning |
| `Escalated` | Escalated | danger |
| `Cancelled` | Cancelled | neutral (no dot) |

### 10.3 Approval — `ApprovalStatus`

| Enum | Label | Dot |
|---|---|---|
| `Pending` | Awaiting decision | warning |
| `Approved` | Approved | success |
| `Rejected` | Rejected | danger |
| `Overridden` | Overridden | warning |
| `Escalated` | Escalated | danger |

### 10.4 SLA — `SlaStatus`

| Enum | Label | Dot |
|---|---|---|
| `OnTrack` | On track | success |
| `AtRisk` | At risk | warning |
| `Breached` | Breached | danger |

### 10.5 Priority — `CasePriority`

Rendered as a plain text label in `--color-text-secondary`, **not** as a coloured pill: `Low`, `Normal`, `High`, `Urgent`. Priority and SLA are different things and must not both compete for colour. Only SLA gets a dot.

### 10.6 Document type — `DocumentType`

`ClaimForm` → Claim form · `PoliceAbstract` → Police abstract · `Invoice` → Invoice · `MedicalReport` → Medical report · `PhotoEvidence` → Photo evidence · `CustomerId` → Customer ID · `GarageEstimate` → Garage estimate · `Unknown` → Unclassified.

`Unknown` renders as `Unclassified` with a warning dot and a `Classify` action. It is never hidden — an unclassified document is exactly the kind of thing that silently stalls a claim.

### 10.7 Recommended next action — `RecommendedNextActionType`

Rendered as the button verb on the claim: `RequestMissingDocuments` → `Request missing documents` · `MarkReadyForReview` → `Mark ready for review` · `SupervisorReview` → `Send to supervisor` · `HumanReview` → `Review now` · `FollowUpCustomer` → `Follow up with customer`.

---

## 11. Cross-surface consistency

The same claim must present identically everywhere. Binding minimum for each surface:

| Surface | Must show |
|---|---|
| Assistant action card | Reference, claim type, status pill, owner, SLA pill, primary verb |
| My work row | Reference, claim type, status pill, SLA pill, completeness, age, primary verb |
| Claims queue row | Reference, claimant, claim type, status pill, owner, SLA pill, updated time |
| Claim detail header | All of the above, plus the stage strip and the four-value row from §4 |
| Toast | Reference, one-line reason, one action |
| Notification panel row | Reference, reason, time, read state |

A surface may show more. It may never show less, and it may never use a different label for the same state.

---

## 12. Worked example

One claim, end to end, as the user experiences it. An implementing agent should be able to build every screen below from this document alone.

**08:14** — A claimant sends a WhatsApp message with three photos and a claim form. It lands in **Inbox**, unclassified, showing the channel, the sender, and the time.

**08:14** — The intake agent creates `CLM-2026-0184`, classifies it as motor own damage, extracts the policy number and incident date, and sets `CaseStatus.New`. The Inbox row now links to the claim.

**08:15** — Triage assigns it to Sarah Nakato by claim type and workload, sets priority `Normal`, path `HappyPath`, and a 48-hour SLA. State becomes `Assigned`. Sarah's rail badge on **My work** increments. No toast — nothing needs her *now*.

**08:15** — The document agent checks the three photos and the claim form against the motor own-damage checklist. Four of seven required documents are present. Completeness `57.14%`. Missing: police abstract, garage estimate, customer ID. State becomes `MissingDocuments`, owner shown as `Customer`.

**08:40** — Sarah opens the Assistant and types "what's waiting on me?". The reply lists her queue and ends with action cards. `CLM-2026-0184` shows `Waiting on documents`, `57% · 4 of 7`, `On track`, and a `Review claim` button. She clicks it. The claim detail opens with a context line: *From your question: "what's waiting on me?"*

**08:41** — The AI narrative card reads: *"I received 4 documents by WhatsApp at 08:14 and classified this as motor own damage. Four of the seven documents required for this claim type are present. The police abstract, garage estimate, and customer ID are missing. I have drafted a request for them."* Every sentence carries a citation chip. Below, the missing items are listed by name, and the drafted message sits in a card marked `Draft — not sent`.

**08:42** — Sarah reads the draft, edits one sentence, and clicks `Send request to customer`. A confirmation modal names the recipient, the channel, and the three documents being requested. She confirms. The message sends; the claim records who approved the send. An inline confirmation appears where she acted — not a toast, because it was her own action on her own screen.

**14:10** — The claimant replies with the police abstract and a garage estimate. Extraction runs. The abstract is clean at 0.94. The garage estimate scores 0.71 on total amount — below the configured 0.85 threshold — so that document becomes `ReviewRequired` and a work item enters Sarah's queue as `RequiresReview`. Completeness is now `85.71%`, 6 of 7.

**14:10** — Because this is her work, it happened off-screen, and she can act on it in one click, a toast appears: *"CLM-2026-0184 — extraction needs checking"* / *"Garage estimate · total amount 0.71"* / `Review`.

**14:12** — Sarah clicks `Review`. The extracted fields open with the low-confidence field expanded at the top. She clicks its citation chip; the side drawer opens the garage estimate at page 1 with the amount region highlighted, while the field editor stays visible behind it. The figure was misread. She corrects it, and the correction is recorded against her name.

**14:15** — The customer ID arrives. Completeness reaches `100%`, 7 of 7. State becomes `ReadyForReview`. The case-review agent produces a summary, two open questions, and one advisory underwriting observation — each labelled advisory, each cited, each naming the model that produced it.

**14:30** — Sarah reviews and clicks `Send to approver`. State becomes `PendingApproval`, owner shown as the named approver. Her decision bar is replaced by a read-only line: *Sent to J. Okello for approval at 14:30.*

**16:05** — The approver decides. Sarah gets a toast: *"Your submission on CLM-2026-0184 was approved by J. Okello."* The claim shows the decision, the decider, the role, the timestamp, and the evidence pack it was decided on — permanently.

At no point did a machine approve anything. At no point did Sarah have to remember a reference number, work out which screen to open, or wonder what the system had already done.

---

## 13. Acceptance criteria

1. Every claim-bearing surface shows the six §11 minimums and uses the §10 vocabulary exactly.
2. The owner of a claim is never blank and never mislabelled as a human when automation holds it.
3. Completeness never renders as a bare percentage.
4. Every AI assertion a user can act on carries a working citation that opens the source.
5. The AI narrative card and the human decision bar are separate containers on every claim detail.
6. No consequential action is reachable without an explicit verb and a consequence preview.
7. Every displayed state maps to a real backend enum value; no UI-only states exist.
8. The §12 walkthrough can be reproduced in the running application end to end.
