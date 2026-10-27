# Ten questions a bank's technical team will ask us

Written 2026-10-27, for research and for deciding what the product needs. It is based on how the SaaS edition is actually built (`iip_saas`), checked against its own documents and code.

## 0. Read this first: one thing to get right before any meeting

You said every customer gets their own Azure SQL and their own Fabric. **That is not how the SaaS edition we run today works, and a bank's technical team will find out in the first ten minutes.**

| | What the documents and code say |
|---|---|
| Decision of 2026-10-03 (`IIP_SAAS_BUILD_PLAN.md`) | "Data: Otic-hosted **pooled Azure SQL**. Every insurer **shares the database**, separated by tenant on every record." |
| Fabric | "Stays the **Enterprise option** (customer's own Fabric, customer's own capacity), **parked** until a confirmed Enterprise customer needs it." |
| Code | Every stored record carries a tenant and workspace id and repositories filter by it. A database test requires that every table either carries a tenant id or is listed as platform data on purpose |

So today: **one shared Azure SQL database, with logical separation between customers.** Per-customer SQL and per-customer Fabric are what an Enterprise tier could be. The Fabric edition was designed (`IIP_SaaS_Fabric_Edition_Implementation_Guide.md`), and its code is parked.

This matters because the honest answer to "is our data isolated?" is "logically, yes, and we can show how". A bank may want more (a dedicated database, their own keys, or their own data lake). The sensible position to take into the room:

> "Standard: pooled and logically isolated. Dedicated option: your own database and keys, or your own Fabric, for institutions that require it."

That is only truthful once a dedicated option exists. Until it does, call it "available on request, designed", not "available".

The bank's own data lake idea fits the Enterprise option well: their lake is the source, IIP reads a curated extract from it. See question 4.

## How to use this list

Each question has: why they ask, what we can truthfully say today, what to research, and what it might mean for the product. "Say today" is limited to what I found in the code and documents. Where I could not verify something, it says so.

---

## 1. Where is our data stored, in which country, and can it leave?

**Why they ask.** Banks in Uganda answer to the Bank of Uganda, and customer data is regulated by the Data Protection and Privacy Act, 2019, which restricts sending personal data abroad. Their risk team will want a country and a region.

**Say today.** Our deployment notes put the development environment in **West US 2** (SQL, App Services, storage, search) and **East US** (Azure OpenAI). Nothing is in Uganda or Africa. A South Africa North region exists in the subscription for one resource but not for IIP's main data. We have not chosen or promised a production region.

**Research.**
- What the DPPA 2019 and Bank of Uganda outsourcing and cloud guidance require for customer data held outside Uganda: consent, contracts, approval from the regulator, or a copy that must stay in-country.
- Which Azure region is closest and allowed: South Africa North (Johannesburg), or Europe.
- Whether Azure OpenAI is available in an African region for the models we use.

**Product impact.** A **region choice per customer** (and per AI call) is likely to become a requirement. This is the single most likely deal-breaker for a Ugandan bank.

## 2. How do you stop one customer from seeing another customer's data?

**Why they ask.** Pooled databases are the usual worry: one bug in a filter exposes another bank.

**Say today.**
- Every record carries a tenant and a workspace id, and repositories filter by them.
- The signed-in user's organisation comes from IIP's own membership store, **not from anything the browser sends**. A request that matches several organisations and does not say which is refused, never guessed.
- There is a database-level test that fails if a new table has no tenant id and is not listed as platform data on purpose, plus tests that one organisation cannot overwrite or read another's records.
- The Academy is kept apart from all insurers' data.

**Not verified / not found.** I did not see **row-level security enforced inside the database**, so protection depends on the application's filters, not the database. I also did not see an independent penetration test.

**Research.**
- Azure SQL **Row-Level Security** as a second lock, so a missed filter in code is still caught by the database.
- A third-party penetration test report focused on tenant isolation.
- How banks usually accept pooled multi-tenancy (or insist on dedicated).

**Product impact.** Add row-level security and a dedicated-database tier. Both are real, bounded pieces of work.

## 3. How is our data encrypted, who holds the keys, and what about WhatsApp?

**Why they ask.** Standard bank questionnaire: encryption at rest and in transit, key management, customer-managed keys, and what happens to messages on WhatsApp.

**Say today.**
- Credentials and tokens we store for providers are encrypted before saving and never sent to a browser.
- Azure SQL and storage encrypt at rest by default (I did not confirm the settings on our resources, so check them in the portal before saying so).
- Moving the encryption key ring to Key Vault is still on your to-do list, so today the keys live with the application.
- **WhatsApp:** messages are encrypted between the customer's phone and WhatsApp's servers, and with the Business API the platform receives and processes them. **Never say "end to end encrypted to you".** Say: encrypted in transit, received through Meta's business platform, stored in the customer's own isolated area, access logged. Please have counsel check the wording.

**Research.**
- Customer-managed keys (bring your own key) in Azure SQL and storage, and what they cost to run.
- What Meta stores, for how long, and where, for the Cloud API.
- Whether any bank has accepted WhatsApp for claims documents, and with what safeguards.

**Product impact.** Finish the Key Vault move, offer customer-managed keys in the dedicated tier, and give customers the choice to take documents by a secure link instead of WhatsApp for sensitive files (the secure link already exists).

## 4. How does data get from our data lake to you, and what do you do with it?

**Why they ask.** This is their plan: the bank's databases feed a **data lake** in batch, and IIP reads from the lake. They will ask exactly which data, how it is secured in transit, whether IIP can write back, and how long you keep it.

**Say today.**
- IIP does not read from a bank data lake yet.
- What exists is **connected folders** (OneDrive, SharePoint, Google Drive) and file import for lists, built and tested but not switched on. Fabric is parked.
- The data classification in the Fabric design says real customer content should live with the customer, and the control plane holds only what is allowed (accounts, settings, usage).

**Research.**
- What the bank's lake is: Azure Data Lake or **Microsoft Fabric / OneLake**, or something else (Databricks, on-premises Hadoop). This decides the connector.
- Access patterns that banks accept: a **read-only identity** on a curated folder or table, a **private connection** (private endpoint) rather than the open internet, and an IP allow list.
- Whether the bank pushes files to us (SFTP, secure upload) or we pull from their lake.
- Which fields they will release. Insist on the minimum: customer id, phone, event type and date, product code. Not balances and not transaction history.

**Product impact.** A **data lake connector** with read-only access, a field mapping screen, schedules, error reports, and a record of what was read. For an Enterprise customer on Fabric, a OneLake shortcut is the cleanest option.

## 5. Who at Otic can see our data, and how do we know?

**Why they ask.** Insider risk and support access.

**Say today.**
- Actions are written to an audit trail, with content omitted from the audit evidence.
- Sign-in uses Microsoft Entra. Two-factor sign-in and passkey support have been built. Roles control who can do what.
- A person at the customer closes or exports their organisation's data themselves.

**Not found.** A written **support access policy** (who at Otic may look at a customer's data, how it is approved, how it is logged), background checks, and a "break glass" procedure.

**Research.**
- Typical SaaS support-access models: customer-approved temporary access, time-limited, fully logged.
- Whether Azure Lockbox (Microsoft's own staff access control) applies to our resources.

**Product impact.** A **customer-approved support access** feature with expiry, and the ability to send audit logs to the bank's own monitoring system (SIEM).

## 6. What does the AI do with our data, and who is responsible when it is wrong?

**Why they ask.** Regulators and risk teams are wary of AI. They will ask about the model, where it runs, training, hallucination, and mis-selling.

**Say today.**
- The AI prepares and a person decides. Claim decisions, payments and policy binding are never the AI's.
- Answers about policy come only from wordings the customer has approved, with citations, and it must not invent prices or rules.
- Customer-facing texts are checked, and sensitive categories (health, religion, tribe, politics) are not asked or scored.
- The models are **Azure OpenAI** resources in **East US**, called through our own provider layer. Microsoft's stated position is that customer data sent to Azure OpenAI is not used to train its models (confirm against the current terms before stating it).
- AI calls are recorded for usage and credits, without the prompt or answer text.

**Research.**
- The current Azure OpenAI data handling terms: retention, abuse monitoring, whether you can turn off human review of prompts, region options.
- Guidance on **AI use in financial institutions** from the Bank of Uganda and the IRA, if any exists.
- Model risk practice: an AI inventory, evaluation results, red-team testing, and monitoring.

**Product impact.** An **AI inventory and evaluation pack** (what models, what for, what tests), a way to choose the model region, and conduct controls shown to the customer (the rules about commission and fairness in the marketplace and bancassurance plans).

## 7. What certifications and independent assurance do you have?

**Why they ask.** Vendor due-diligence checklist: ISO 27001, SOC 2, penetration tests, policies, DPPA registration.

**Say today.** As far as I know, **Otic holds none of these**. The Azure infrastructure carries Microsoft's own certifications, but those cover Microsoft's platform and not our application. We have written security, privacy and incident documents, and a threat model.

**Research.**
- Whether Otic must register with the Uganda **Personal Data Protection Office** as a data controller or processor.
- The cheapest credible path: an independent **penetration test** now, **ISO 27001** or **SOC 2 Type 1** over the next year, and the bank's own vendor questionnaire answered in full.
- What Old Mutual and Britam accept from a young vendor: often a pen test, a security questionnaire, and a short pilot under strict limits.

**Product impact.** Mostly process, not code: policies, evidence, and the audit log exports.

## 8. How do our staff sign in, and who can approve what?

**Why they ask.** Access control is the first control a bank reviews.

**Say today.**
- Sign-in through Microsoft Entra (work accounts), with Google and email codes planned through External ID.
- Two-factor sign-in, passkeys, and organisation roles exist. Everything is checked on the server.
- Approvals are recorded with the name of the person.

**Your decision:** the bank admin chooses which roles may approve an outreach rule. This is a good feature to promise, and it needs **configurable roles and permissions per organisation**. Today the roles are fixed (for example claims officer, supervisor, compliance).

**Research.**
- **Single sign-on** with the bank's own directory (Entra or ADFS), and automatic user provisioning (SCIM).
- Session length, device rules, IP restrictions.
- Segregation of duties: the person who creates a rule cannot be the only person who approves it.

**Product impact.** Customisable roles, a permission picker for "who can approve rules", the four-eyes option, and SCIM.

## 9. How available is it, how do you back it up, and what happens if it fails?

**Why they ask.** A claims and customer channel cannot be down for a day.

**Say today.** Be careful here. **The environment we run today is a development setup**: a single small App Service plan (B1) hosting both the API and the portal, a single SQL database, and a measured slow start. It is not a production-grade high-availability design, and we have no service level agreement, recovery time or recovery point commitment.

**Research and plan.**
- What a bank expects: an uptime commitment (often 99.9%), recovery objectives (for example recover within hours, lose at most minutes of data), tested backups, a documented disaster recovery plan, and a status page.
- The cost of a real production tier: multiple instances in two zones, a bigger SQL tier with geo-redundant backups, health checks, and alerts.
- The throughput WhatsApp allows a new number and how a flood of inbound messages is queued.

**Product impact.** A **production environment** separate from development, with scaled infrastructure, backup tests, and a load test. This also protects the live demo environment from a deploy mistake. Note the Azure credit situation: costs and a production tier need funding.

## 10. What happens to our data if we leave, who else touches it, and who is liable?

**Why they ask.** Exit rights, sub-processors, and the contract.

**Say today.**
- The organisation's administrator can **export everything the organisation owns** as one file, without stored credentials, and can **close the organisation, which deletes everything it owns** (it needs the organisation's exact name, and it cannot be undone).
- Sub-processors we use today: **Microsoft Azure** (hosting, SQL, storage, search, AI, document reading), **Meta** (WhatsApp), and SMS and email providers for messages. Payments in the Academy use **Flutterwave**, which is separate from insurer data.

**Research.**
- A sub-processor list and a promise to tell customers before adding one.
- Retention schedules by data type, and how long backups keep deleted data.
- Standard contract terms banks ask for: data processing agreement, liability caps, indemnity for data breaches, audit rights, breach notification within a fixed time (often 24 to 72 hours), and escrow or continuity if the vendor fails.
- Whether Ugandan law requires the bank to approve sub-processors.

**Product impact.** A downloadable **sub-processor list and data-processing agreement**, retention settings per customer, and an export that a bank can verify.

---

## Other questions that will probably come up

- **Integration with core banking and policy systems:** our API, webhooks, rate limits, versioning, and whether we can write back.
- **Consent for marketing:** how consent is captured, recorded and withdrawn, and how a customer can ask for their data to be deleted.
- **Customer support and incidents:** response times, escalation, a named contact.
- **Pricing and who pays for AI usage and WhatsApp conversation fees.**
- **Business continuity of Otic itself:** funding, team size, and how long you will be here. Your Azure credit being spent is the kind of fact a careful bank will find out. Have a plain answer ready.

## What this tells us to build or decide

| Item | Why |
|---|---|
| **Decide the region story** and test a non-US deployment | Question 1 is the most likely blocker |
| **Row-level security** in Azure SQL | Question 2 |
| **A dedicated-database or dedicated-Fabric tier** | Questions 2 and 4: banks may require it |
| **Finish the Key Vault key move**, then customer-managed keys for the dedicated tier | Question 3 |
| **Data lake connector** (read-only, minimal fields, private connection) | Question 4, and your batch plan |
| **Customer-approved support access** and **log export to a SIEM** | Question 5 |
| **AI inventory and evaluation pack** | Question 6 |
| **Penetration test**, DPPA registration check, certification roadmap | Question 7 |
| **Configurable roles**, four-eyes approval, SSO and SCIM | Question 8, and your "bank admin chooses who approves rules" |
| **A production environment** with scale, backups and a recovery plan | Question 9 |
| **Sub-processor list, data-processing agreement, retention settings** | Question 10 |
