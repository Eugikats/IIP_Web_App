# Client Pilot Charter

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Client Pilot Charter |
| Version | 1.0 |
| Status | Draft |
| Audience | Client executives, pilot sponsor, operations leads, product owner, delivery team |
| Date | 2026-06-30 |

This document is part of the Insurance Intelligence Platform (IIP) documentation set. IIP is an Azure-native, human-reviewed insurance operations platform for insurers, brokers, TPAs, HMOs, bancassurance partners, and microinsurance providers. The current MVP direction is motor claims and document intelligence, with WhatsApp-first customer interaction feeding internal insurer workspaces for claims, channels, customers, documents, approvals, audit, and reporting. AI assists with classification, extraction, summarization, triage, routing, drafting, and evidence preparation. Humans retain authority for regulated and consequential decisions such as claim approval, rejection, settlement, fraud determination, payment release, policy binding, and final underwriting decisions.

> **Pilot principle:** The pilot must prove operational value while keeping all regulated insurance decisions under human control.


## 1. Pilot Objective

The pilot objective is to validate that IIP can support a motor claims and document intelligence workflow for a real insurer environment. The pilot should demonstrate faster claim intake, better document completeness visibility, improved internal coordination, auditable AI assistance, and clearer customer update workflows without replacing the insurer's core insurance system.

## 2. Pilot Scope

| Scope Area | Included in Pilot | Notes |
| --- | --- | --- |
| Customer intake | WhatsApp-first FNOL intake and internal channel visibility | Customers do not log into the internal IIP staff app. |
| Claims operations | Motor claim creation, review, assignment, SLA visibility, escalation signals | Human claims staff retain final decisions. |
| Document intelligence | Upload, classify, extract, validate, missing-document detection, human correction | Document types depend on client configuration. |
| AI assistance | Summaries, evidence preparation, recommended next steps, customer update drafts | AI must not approve or reject claims. |
| Audit and compliance | Audit trail for user actions, AI outputs, document review, decisions, and exports | Required for pilot review and governance. |
| Dashboards | Command Center and operational metrics for pilot scope | Metrics should be reviewed weekly. |


## 3. Explicit Exclusions

- [ ] Autonomous claim approval, rejection, settlement, or payment release.
- [ ] Autonomous fraud accusation or final fraud determination.
- [ ] Full replacement of the insurer core policy or claims administration system.
- [ ] Production-scale migration of all historic claims unless separately approved.
- [ ] Customer-facing portal login unless separately scoped.
- [ ] Full omnichannel production rollout beyond agreed pilot channels.


## 4. Pilot Participants and Responsibilities

| Role | Primary Responsibilities | Named Owner |
| --- | --- | --- |
| Client Executive Sponsor | Approve pilot scope, unblock senior decisions, review outcomes | Requires Confirmation |
| Client Operational Owner | Coordinate users, workflows, UAT, adoption, daily operations | Requires Confirmation |
| Client IT/Security Owner | Approve access, environment, integrations, security checks | Requires Confirmation |
| Claims Supervisor | Validate claims workflow, assignments, escalations, and SLA rules | Requires Confirmation |
| Claims Officers | Perform pilot claim review, document review, customer update workflows | Requires Confirmation |
| Compliance/Audit User | Review audit evidence, AI guardrails, records, and reports | Requires Confirmation |
| Otic Delivery Lead | Coordinate delivery, risks, timeline, status reporting, and pilot readiness | Requires Confirmation |
| Otic Product/Engineering | Configure, implement, support, and improve the platform | Requires Confirmation |


## 5. Pilot Timeline Template

| Phase | Target Duration | Key Activities | Exit Criteria |
| --- | --- | --- | --- |
| 0. Mobilization | 1 week | Confirm sponsor, users, pilot scope, access, data requirements | Pilot charter signed |
| 1. Configuration | 1-2 weeks | Configure tenant, roles, documents, SLAs, workflows, templates | Configuration workbook approved |
| 2. Integration and Data Setup | 1-2 weeks | Set up approved channels, email/Graph, storage, sample policies/claims | Post-deployment validation passed |
| 3. UAT and Training | 1 week | Run UAT scripts, fix blockers, train users | UAT sign-off complete |
| 4. Pilot Go-Live | 2-6 weeks | Run live pilot with weekly status reviews | Pilot KPIs measured weekly |
| 5. Closeout | 1 week | Review KPIs, issues, lessons, next-phase decision | Closeout report approved |


## 6. Success Criteria

| Success Area | Measurement | Target / Acceptance |
| --- | --- | --- |
| Operational adoption | Named users actively use IIP for agreed pilot workflow | Target to be confirmed with client |
| Document completeness | Missing-document status is visible and actionable | 100% of pilot claims show document status |
| Human review | AI outputs are reviewable, cited where applicable, and not final decisions | No autonomous regulated decisions |
| Auditability | Claim actions, document corrections, AI outputs, and user decisions are logged | Audit evidence available for pilot sample |
| User confidence | Claims users can explain what the system does and what AI can/cannot do | Training sign-off complete |
| Support readiness | Issues are logged, triaged, owned, and reviewed | Weekly issue review cadence active |


## 7. Governance Model

- Weekly pilot governance meeting with client sponsor, operational owner, Otic delivery lead, and relevant workstream owners.
- Daily or twice-weekly operational check-ins during go-live week.
- Formal issue log, risk register, decision log, and open question register.
- Changes to scope must follow the requirements change control process.
- Any security, privacy, integration, or AI safety concern must be escalated immediately.

## 8. Pilot Risks

| Risk | Impact | Mitigation | Owner |
| --- | --- | --- | --- |
| Client users unavailable for UAT | Pilot launch delay | Book UAT sessions in advance and nominate backups | Client Operational Owner |
| Integration access delayed | Incomplete pilot workflow | Use approved manual import or staged connector plan only if accepted | Client IT / Otic Delivery |
| Poor document quality | Lower extraction accuracy | Define document quality guidance and human correction workflow | Claims Supervisor |
| AI over-trust | Unsafe operational behavior | Train users that AI assists only and humans decide | Otic Product / Client Lead |
| Unclear success metrics | Weak pilot outcome review | Approve success measurement plan before go-live | Executive Sponsor |

## Open Questions and Required Confirmations

- Confirm the first pilot insurer, pilot sponsor, and operational owner.
- Confirm pilot duration, target launch date, and daily/weekly support cadence.
- Confirm which claim types are included in the first pilot beyond motor claims, if any.
- Confirm client-specific document checklist, SLA thresholds, branch structure, and approval workflow.
- Confirm integration readiness for WhatsApp Business API, Microsoft Graph/email, policy lookup, claims system access, and document storage.
- Confirm legal, compliance, privacy, and data residency requirements before production use.
## Sign-Off

| Role | Name | Decision | Date | Notes |
|---|---|---|---|---|
| Client Executive Sponsor |  | Approve / Reject / Defer |  |  |
| Client Operational Owner |  | Approve / Reject / Defer |  |  |
| Client IT / Security Owner |  | Approve / Reject / Defer |  |  |
| Otic Delivery Lead |  | Approve / Reject / Defer |  |  |
| Otic Product Owner |  | Approve / Reject / Defer |  |  |

