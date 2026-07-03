# Pilot Proposal Document

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Pilot Proposal Document |
| Version | 1.0 |
| Status | Draft |
| Audience | Prospective insurer client, executive sponsor, pilot steering committee, Otic delivery team |
| Date | 2026-06-30 |

## Core Principles Used Across This Pack

- IIP is the official product name. Older ACIP references are not used as official naming.
- The MVP is centered on motor claims and document intelligence for an insurer pilot.
- IIP assists insurance work; humans make consequential insurance decisions.
- AI may summarize, classify, extract, flag, draft, recommend, and route, but it must not approve or reject claims, bind policies, set final premiums, accuse customers of fraud, promise settlement, or release payment.
- The target architecture is Azure-first with Next.js/React/TypeScript frontend, .NET 8 backend, Azure SQL, Blob Storage, Key Vault, AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus/Event Grid, Application Insights, Entra ID/MSAL, and Microsoft Graph where appropriate.
- Customer interaction is WhatsApp-first but can extend to web chat, SMS, email, broker, call center, and Microsoft channels.
- Internal users work through role-aware staff workspaces; customers do not log into the internal app.
- Staging and production must not rely on fake data or mock endpoints. Empty, error, unauthorized, loading, and Not configured states must be handled clearly.


## Proposal Summary

This pilot proposal defines a controlled introduction of IIP for a mid-to-large insurer, focused on motor claims and document intelligence. The pilot is intended to prove operational value, responsible AI behavior, staff usability, and integration feasibility before wider rollout.

## Proposed Pilot Objective

To demonstrate that IIP can improve motor claims intake, document review, claim visibility, customer update handling, SLA monitoring, and management oversight while preserving human control over insurance decisions.

## Proposed Pilot Scope

| Area | Included in pilot |
|---|---|
| Primary business process | Motor claims intake, document review, claim completeness, and claim review support |
| Primary users | Claims officers, claims supervisors, compliance/audit users, admins, customer service users, executives/viewers |
| Primary channels | WhatsApp-first intake and updates; email or internal upload as supporting channels |
| Documents | Claim form, police report, national ID, driving permit, vehicle photos, repair quotation, policy schedule, assessor report if available |
| AI capabilities | Summaries, extraction, classification, completeness scoring, risk signals, draft customer updates, RAG Q&A over approved knowledge |
| Human review | Required for approvals, rejections, settlements, payment release, customer commitments, fraud determinations, and final underwriting or claim decisions |

## Exclusions

The pilot should not attempt to replace the client's policy administration system, claims core system, finance system, or regulatory reporting platform. Unless explicitly approved, the pilot should not include autonomous claim settlement, final fraud decisioning, final premium pricing, automated payment release, or production write-back to core systems.

## Pilot Timeline

| Phase | Duration | Key activities | Exit criteria |
|---|---:|---|---|
| 1. Mobilization | Week 1 | Confirm sponsor, pilot scope, users, data, success metrics, and governance | Signed pilot charter and data/configuration checklist |
| 2. Configuration | Weeks 2-3 | Configure tenant, roles, workflows, document checklists, channels, templates, and environments | Configured pilot environment |
| 3. Integration and data readiness | Weeks 3-5 | Set up WhatsApp/email/upload paths, sample data, Blob storage, AI services, and optional read-only policy lookup | Working end-to-end test flow |
| 4. UAT and training | Weeks 5-6 | Train users and execute UAT scenarios | UAT sign-off or approved issue list |
| 5. Pilot go-live | Weeks 7-10 | Run controlled pilot with selected claims/users | Weekly pilot reports and support log |
| 6. Closeout | Weeks 11-12 | Assess KPIs, user feedback, risks, lessons, next-phase decision | Pilot closeout report and decision |

## Deliverables

| Deliverable | Description | Owner |
|---|---|---|
| Pilot charter | Scope, objectives, success criteria, assumptions, and governance | Otic + Client |
| Configured pilot environment | Staging/pilot environment with users, roles, and workflows | Otic |
| Client configuration workbook | Completed claim types, documents, SLAs, roles, templates, channels | Client + Otic |
| UAT script and results | Client testing scenarios and sign-off | Client + Otic |
| Training sessions | Role-based training for pilot users | Otic |
| Weekly status reports | Progress, blockers, metrics, issues, risks | Otic |
| Pilot closeout report | Outcomes, lessons, value assessment, recommendations | Otic + Client |

## Client Responsibilities

| Responsibility | Description |
|---|---|
| Executive sponsor | Provide senior sponsorship, decision support, and pilot governance |
| Product/process owner | Confirm motor claims workflow, SLAs, document requirements, and approvals |
| User participants | Attend training, test UAT scenarios, and use IIP during pilot |
| Technical contact | Support tenant, identity, integrations, email, WhatsApp, and data access |
| Compliance/legal contact | Review data handling, AI governance, and customer communication requirements |
| Sample data | Provide representative sample claims, documents, templates, and process rules |

## Otic Responsibilities

| Responsibility | Description |
|---|---|
| Pilot delivery | Configure, deploy, support, and manage pilot execution |
| Product configuration | Configure workspaces, roles, document checklists, SLAs, and dashboards |
| AI/document setup | Configure extraction, classification, summaries, and review queues |
| Training | Deliver role-based training and user support |
| Reporting | Provide weekly status reports and closeout assessment |
| Responsible AI controls | Enforce human review, citations, audit logging, and prohibited-action guardrails |

## Success Criteria

| Category | Success criterion | Measurement |
|---|---|---|
| Operational | Pilot users can create and manage motor claims through IIP | UAT and pilot usage logs |
| Document intelligence | Documents can be uploaded, classified, extracted, and reviewed | Extraction quality and review logs |
| Human review | AI outputs are reviewed and cannot make final insurance decisions | Audit logs and role permissions |
| Visibility | Command Center shows open claims, incomplete claims, SLA risk, pending approvals, and backlog | Dashboard validation |
| User adoption | Pilot users complete training and use assigned workflows | Attendance and usage metrics |
| Supportability | Issues are tracked, triaged, and resolved through support process | Issue log and weekly status reports |

## Commercial Assumptions

| Item | Working assumption |
|---|---|
| Pilot pricing | Requires Confirmation |
| Production subscription | Requires Confirmation |
| Implementation fee | Requires Confirmation |
| Azure costs | Depends on deployment model, usage, region, and client tenancy |
| Support tier | Pilot support included in pilot scope; production support requires separate agreement |

## Governance

| Meeting | Frequency | Purpose |
|---|---|---|
| Pilot kickoff | Once | Confirm goals, roles, scope, timeline, and success metrics |
| Weekly delivery review | Weekly | Review progress, blockers, risks, issues, and next steps |
| Steering review | Every 2 weeks or as needed | Resolve decisions and confirm executive alignment |
| Closeout meeting | End of pilot | Review KPI outcomes, lessons, and next-phase decision |

## Next Steps

1. Confirm pilot sponsor and business owner.
2. Confirm pilot scope and excluded items.
3. Complete client readiness checklist.
4. Complete configuration workbook.
5. Confirm legal/security review pathway.
6. Approve pilot timeline and sign-off process.
7. Begin pilot mobilization.

## Open Questions and Required Confirmations

| Item | Status | Notes |
|---|---|---|
| First pilot insurer | Requires Confirmation | ICEA-like mid-to-large insurer is the working assumption, but the named pilot client must be confirmed. |
| Commercial currency | Requires Confirmation | Pricing can be expressed in USD for investor/client modeling, but local contract currency may vary. |
| Final pilot duration | Requires Confirmation | An 8 to 12 week pilot is assumed unless client procurement or integration timelines require adjustment. |
| Production deployment model | Requires Confirmation | Azure Managed Application is preferred for enterprise clients; Otic-hosted SaaS may be used for demos or smaller clients. |
| Regulatory sign-off requirements | Requires Confirmation | Local insurance, privacy, outsourcing, and client legal requirements must be validated before production. |

