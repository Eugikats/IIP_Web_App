# Insurance Intelligence Platform Domain Language

IIP is a tenant-isolated insurance operations platform that prepares evidence-grounded work for accountable humans. This glossary defines business meaning only; implementation and route details belong in owning specifications.

## Organization and authority

**Tenant**:
The insurance organization that owns a business-data and authority boundary in IIP.
_Avoid_: Customer, account, Azure subscription

**Workspace**:
A governed subdivision of a tenant in which membership, permissions, queues, configuration, evidence, and work are scoped.
_Avoid_: Tenant, browser-selected context

**Membership**:
The server-authoritative relationship granting a person roles and permissions within a tenant and workspace.
_Avoid_: Token claim, app role, group membership

**External identity hint**:
Validated identity-provider identifiers used to look up membership; they do not grant business authority by themselves.
_Avoid_: Authorization

**Accountable human**:
An authorized person who has the evidence, authority, capacity, and recourse responsibility for a consequential insurance decision.
_Avoid_: Human-in-the-loop clicker

## Claim work

**Claim**:
A request for indemnity or benefit under a policy.
_Avoid_: Case, task

**Case**:
IIP's tenant-scoped operational work container for claim-related intake, evidence, tasks, reviews, approvals, communication, and audit.
_Avoid_: Claim decision, settlement

**Work item**:
A queue-addressable view of the next owned operational outcome, preserving its source object, owner, work state, due state, evidence readiness, and required action. It may project a workflow task, approval, document review, communication review, or agent work item without merging their distinct state models.
_Avoid_: Record, notification, universal backend aggregate

**Claim file**:
The governed collection of source evidence and records associated with a claim.
_Avoid_: Chat history

**Complete claim file**:
A claim file that satisfies the current approved document and information rules for a defined review stage; it does not mean the claim is payable.
_Avoid_: Approved claim

**Claim notification**:
The insurer's receipt of notice that an event may give rise to a claim.
_Avoid_: Claim approval, admission of liability

**Claim decision**:
An authorized human determination about a claim outcome.
_Avoid_: Recommendation, workflow status

**Settlement offer**:
An authorized proposal of settlement terms made after the required claim decision process.
_Avoid_: Payment

**Claim settlement/payment**:
The confirmed financial discharge of an approved obligation through the insurer's authorized payment process.
_Avoid_: Approval, ready for export, closed

**Repudiation**:
An authorized insurer decision declining a claim, with required reasons and recourse.
_Avoid_: AI rejection, fraud flag

**Complaint**:
A policyholder or claimant expression of dissatisfaction handled through the insurer's complaint process.
_Avoid_: Appeal

**Review request**:
A governed request to reconsider or correct claim-related evidence or process.
_Avoid_: Final appeal outcome

## Evidence and automation

**Source evidence**:
An authoritative document, record, communication, or approved policy source used to support work or a decision.
_Avoid_: Model memory, generated summary

**Channel conversation**:
A customer or broker interaction thread received through WhatsApp or another approved external channel and linked to authorized operational work.
_Avoid_: Command Center conversation, claim file

**Outbound communication attempt**:
One idempotent provider request for exact customer-safe content that an authorized human reviewed against the recipient, purpose, consent or legal basis, language, and channel policy.
_Avoid_: Delivered message, approval token, automatic reply

**Provider accepted**:
The external channel returned a message reference for an outbound attempt; it does not prove delivery or reading.
_Avoid_: Sent, delivered, read

**Delivery receipt**:
A verified, replay-safe provider event that advances or explains an outbound message's provider-accepted, delivered, read, failed, or unknown state.
_Avoid_: Browser success state, inferred delivery

**Command Center conversation**:
A staff-owned operational investigation thread containing bounded focus pointers and user draft state; it is continuity context, not evidence, audit, or authority.
_Avoid_: Channel conversation, source evidence, model memory

**Derived artifact**:
A classification, extraction, summary, score, recommendation, draft, or projection produced from source evidence and retaining provenance.
_Avoid_: Source evidence

**Evidence gap**:
A required item that is missing, stale, conflicting, unsupported, or below the approved review threshold.
_Avoid_: Claim invalidity

**Proposed action**:
A bounded next step prepared for review; it has no authority until an authorized human or approved low-risk policy permits execution.
_Avoid_: Decision, completed action

**Consequential action**:
An action that can materially affect coverage, claim outcome, settlement, payment, underwriting, fraud treatment, complaint outcome, regulatory reporting, customer rights, or external communication.
_Avoid_: Routine technical processing

**Human approval**:
A recorded decision by an authorized human over the exact proposed action, current evidence, policy/rule version, scope, and reason.
_Avoid_: Generic approval token

**Execution**:
The controlled attempt to apply an approved action to IIP or an external system, with idempotency, reconciliation, and an explicit outcome.
_Avoid_: Approval

**Grounding**:
Linking an output to current approved tenant/workspace source evidence, with missing, conflicting, stale, or excluded sources made visible.
_Avoid_: Fluent explanation

**Simulation**:
Behavior that fabricates business data, provider processing, sends, write-backs, metrics, or success instead of executing the configured real capability.
_Avoid_: LocalTest double

**Not configured**:
The honest state that a required capability has no approved real provider or configuration in the current workspace.
_Avoid_: Success, empty result, fallback

## Release and evidence

**Source-complete**:
Required source changes and documents exist; verification or deployment may still be absent.

**Locally verified**:
The affected behavior passed the stated local checks in a controlled environment.

**Deployed**:
The artifact is running in a named environment; user acceptance and production readiness are separate.

**Staging-validated**:
Representative authenticated workflows passed in protected staging with real configured dependencies.

**Production candidate**:
All approved functional, safety, security, privacy, performance, accessibility, operational, recovery, UAT, and approval gates have matching evidence.

**Release evidence**:
Versioned proof linked to a requirement and environment, including commands/results, test records, limitations, approvers, and date.
_Avoid_: Checklist assertion
