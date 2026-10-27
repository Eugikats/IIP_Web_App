# The ten bank questions, answered the way CIP answers them

Written 2026-10-27. Rule used: where CIP (the Credit Intelligence Platform, `CIP_Web_App`) has a written answer or a built control, IIP takes the same position and the same wording. Where CIP has no answer, the question is left **Unanswered** on purpose, not filled in from guesswork. Sources are CIP's own documents:

- `docs/architecture/SECURITY_ARCHITECTURE.md`
- `docs/architecture/HYBRID_ON_PREM_ARC_MCP_ARCHITECTURE.md`
- `docs/audits/CIP_STAGE15A2D8_AI_PLATFORM_SIZING_AND_INFRASTRUCTURE_AUDIT.md` (cited below as "the infrastructure audit")
- `docs/security/CIP_Platform_MFA_Implementation_Plan.md`

Two things to keep straight when reading. First, "CIP does it" describes what CIP's documents say, and some of it is a plan (marked **planned**). IIP must not claim a planned item as built. Second, the questions in `IIP_BANK_TECHNICAL_QUESTIONS.md` are the source of the numbering.

## 1. Where is our data stored, in which country, and can it leave?

**CIP's position.** CIP does not name a country or promise a region. Its infrastructure audit lists "target compliance frameworks and data residency requirements" as a gap that **requires a business decision**. What CIP does offer for strict residency is a pattern: keep raw files and the source database **on the bank's premises** and sync only the minimum fields CIP needs, with Azure Arc used for governance and monitoring only. Documents in the standard deployment sit in Azure Blob Storage "with bank-controlled data residency and access controls".

**IIP answer, same position.** No promised region today. If a bank requires data to stay in its premises, the answer is the hybrid pattern: the bank keeps raw data, IIP receives only the minimum fields (customer id, phone, event type and date, product code).

**Unanswered.** Which Azure region production will use, and what the Data Protection and Privacy Act 2019 requires for data held abroad. CIP has neither.

## 2. How do you stop one customer from seeing another customer's data?

**CIP's position.** From `SECURITY_ARCHITECTURE.md`:

- Every tenant-scoped record must include tenant context.
- Every query over tenant data must filter by tenant.
- A cross-tenant access attempt returns not found or forbidden without leaking data.
- Blob access, search retrieval, notifications and agent tool calls enforce tenant context too.
- Agent tool access inherits the acting user's tenant and permission scope.

**IIP answer, same position.** This matches how IIP already works (tenant and workspace id on every record, repositories filter by them, a test that fails when a table lacks a tenant id). Say it in the same words.

**Unanswered.** Row-level security inside the database, and an independent penetration test. CIP's documents do not claim either, so IIP does not either.

## 3. How is our data encrypted, who holds the keys, and what about WhatsApp?

**CIP's position.**

- In transit: HTTPS/TLS for browser, API, services and Teams. The infrastructure audit records a TLS 1.2 minimum on App Service, SQL, Storage, Service Bus and Key Vault.
- At rest: encrypted by Azure platform services (SQL transparent data encryption, storage service encryption, search encryption, Key Vault), with Microsoft-managed keys.
- Keys and secrets: secrets are never committed. Key Vault is the store for production secrets (standard SKU, role-based access, purge protection). Managed identity is preferred for service-to-service access. Local secrets stay in ignored local files.
- Customer-managed keys: **not configured** in CIP.

**IIP answer, same position.** Encrypted in transit and at rest by Azure with Microsoft-managed keys, secrets in Key Vault, managed identity preferred, customer-managed keys not offered today. Confirm each setting on IIP's own resources before saying it, because the CIP evidence is about CIP's resources. Keep the WhatsApp wording from the technical questions file: encrypted in transit, processed through Meta's business platform, never "end to end to you".

**Unanswered.** Customer-managed keys or hardware security modules, and what Meta retains. CIP lists the first as a business decision and does not cover the second.

## 4. How does data get from our data lake to you, and what do you do with it?

**CIP's position.** CIP has a worked design for a bank whose data stays on its side:

- A small **edge connector** runs inside the bank's environment (a .NET worker or container) and reads from the core banking system, a reporting database or read replica, file exports, SFTP or a shared folder.
- It sends CIP only the **minimum required fields**.
- MCP (the controlled tool interface for agents) is used only for approved tools, not as a data pipe.
- **No silent write-back**: CIP must not write decisions, limit changes, disbursement instructions or customer updates back to a core banking system. Any write-back needs explicit human approval, a bank-specific design, audit logging and a security review.

**IIP answer, same position.** Batch files or an extract from the bank's lake, read-only, minimum fields only, no write-back to the bank's systems. A bank-side connector for the lake follows the same edge pattern. Keep the file route (connected folders and file import) as the first real path.

**Unanswered.** Which kind of lake the bank has, how it authenticates, and whether the link is private. CIP's design does not decide these, and the bank has not answered.

## 5. Who at Otic can see our data, and how do we know?

**CIP's position.** CIP's answer is audit logging: records for user actions, agent runs, tool calls, data access, memo generation and edits, review actions, override reasons, notifications, connector events and **configuration changes**. Audit records should be append-only and traceable, and must support internal audit and bank compliance review. Audit tables exist for role, user and tenant changes, channel events, policy decisions and AI agent runs.

**IIP answer, same position.** Every access and change is audited, the audit trail is append-only in intent, and a bank can review it. State plainly what CIP also states: audit records are written; shipping logs to the bank's own monitoring system is not done.

**Unanswered.** A written support-access policy (who at Otic may look at customer data, how it is approved), a break-glass procedure, and Azure Lockbox. CIP has none of these. CIP also records that its application telemetry was not flowing in the pilot, so do not claim complete logging without checking IIP's own.

## 6. What does the AI do with our data, and who is responsible when it is wrong?

**CIP's position.**

- Agents and rules may assist, recommend, summarise, draft and flag risk. **Final approval, rejection, escalation, restructuring, write-off or disbursement is performed by an authorised human**, with the decision, actor, timestamp, comments and override reason captured.
- AI output is editable and versioned, and traceable after the fact.
- Compliance checks cover consent, audit trails, binding-decision language and retention (CIP's compliance audit passed 12 of 12 checks).

**IIP answer, same position.** The AI prepares and a person decides, with the decision recorded. This is already IIP's rule, so the wording carries across unchanged.

**Unanswered.** Where the models run, training use of customer data, and any regulator guidance on AI in financial institutions. CIP's documents do not settle them, so the technical questions file keeps the Azure OpenAI region and terms as items to confirm.

## 7. What certifications and independent assurance do you have?

**CIP's position.** The infrastructure audit says it directly: "No explicit ISO 27001, SOC 2, GDPR, or banking-regulator certification is documented." It records internal compliance and security audit commands and a pilot risk register, and lists the target frameworks as a business decision.

**IIP answer, same position.** None today. Do not imply otherwise. Describe what exists (written policies, internal checks, threat model) and that Microsoft's certifications cover the platform and not the application.

**Unanswered.** The roadmap to a certification and a penetration test. CIP has none.

## 8. How do our staff sign in, and who can approve what?

**CIP's position.**

- Microsoft Entra ID is the identity provider, with JWT bearer tokens validated on the server for tenant, user, role and permission.
- Role-based access is enforced **server-side, not only through UI visibility**, and accounts for branch, portfolio, assignment, approval authority and workflow state.
- The roles are fixed: relationship manager, credit analyst, credit manager, credit committee member, risk officer, bank IT admin, internal auditor.
- Multi-factor sign-in is handled by the identity provider (Entra), not by CIP. CIP's own plan states replacing identity-provider MFA is out of scope, and notes that Google-signed-in users get a different story.

**IIP answer, same position.** Sign-in through the identity provider, multi-factor from the identity provider, roles enforced on the server. Your own decision, that the bank admin chooses which roles may approve a rule, goes beyond CIP (whose roles are fixed), so IIP builds it and does not claim CIP has it.

**Unanswered.** Single sign-on with the bank's directory beyond Entra, automatic user provisioning (SCIM), and four-eyes approval. CIP does not document them.

## 9. How available is it, how do you back it up, and what happens if it fails?

**CIP's position.** CIP is open about its pilot: sized as an internal controlled pilot, single replicas, no high availability, no autoscale, and **no recovery time or recovery point objective defined**. Azure SQL keeps default automated backups with point-in-time recovery inside the region, plus geo-redundant backup storage, but there is no failover group and no disaster-recovery document. Its recommended production design is a virtual network, private endpoints, network security groups, a web application firewall, higher-tier services and alert rules, and it says that production sizing needs the customer's own input on concurrency, data volume and recovery targets.

**IIP answer, same position.** Say that today's environment is a development setup, with no uptime commitment and no recovery objectives, and describe the production design as the intended next step, not a thing that exists.

**Unanswered.** Uptime percentage, recovery targets, and a tested disaster-recovery plan. CIP has none.

## 10. What happens to our data if we leave, who else touches it, and who is liable?

**CIP's position.** CIP's documents describe retention checks in its compliance audit, but they set out no exit procedure, no sub-processor list and no liability terms.

**IIP answer.** Use IIP's own facts, since CIP offers no comparison: the organisation administrator can export what the organisation owns and can close the organisation (which deletes it). The sub-processors are as listed in `IIP_BANK_TECHNICAL_QUESTIONS.md`.

**Unanswered (from CIP).** Sub-processor commitments, breach-notification time, liability caps, and continuity if the vendor fails.

## What this tells us

| Where CIP gives an answer | IIP takes it as is |
|---|---|
| Tenant scoping on every query, record and tool call | Question 2 |
| TLS in transit, Azure encryption at rest, Key Vault, managed identity | Question 3 |
| Edge connector, minimum fields, no write-back | Question 4 |
| Append-only audit of actions, agent runs, config changes | Question 5 |
| Human decides, AI output editable and versioned | Question 6 |
| Entra sign-in, server-side roles | Question 8 |
| Honest pilot status, production design as a plan | Question 9 |

| Where CIP has no answer | Status for IIP |
|---|---|
| Region and residency, customer-managed keys | Unanswered, business decision |
| Row-level security, penetration test, certifications | Unanswered |
| Support-access policy, log export to a bank system | Unanswered |
| Recovery targets and disaster recovery | Unanswered |
| Exit terms, sub-processors, liability | Unanswered |

The rule for the room: say what CIP says it does, mark a plan as a plan, and answer "not yet" where the answer is "not yet".
