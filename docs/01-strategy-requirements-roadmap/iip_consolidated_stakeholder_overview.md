# IIP Consolidated Stakeholder Overview

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document | Consolidated Stakeholder Overview |
| As of | 2026-10-05 |
| Reconciled against | Source on `main` and `feature/claim-intake`, and the commit history through 2026-10-05 |
| Status | Draft for stakeholder review |
| Audience | Executive stakeholders, pilot sponsors, product owners, engineering leads, insurer stakeholders |

## Executive Summary

Insurance Intelligence Platform (IIP) is an Azure-native, human-reviewed insurance operations platform for African insurers, brokers, TPAs, HMOs, bancassurance partners and microinsurance providers. It turns fragmented customer, document, email and WhatsApp inputs into structured, auditable case work: AI prepares the work, named human reviewers make every regulated insurance decision.

The current focus is Release 1: a Motor Claims and Document Intelligence pilot for a mid-to-large insurer, Uganda-first with an Africa-configurable design. The platform's core AI and channel plumbing (claims workflow, document extraction, WhatsApp/SMS/email intake, knowledge retrieval) is built and largely real. Live-traffic proof of those channels, and formal stakeholder and regulatory sign-off, are the main gaps between the current build and a pilot launch.

Since the last version of this document the WhatsApp assistant has become **Kato**, a helper that holds a conversation and acts through controlled tools: it greets customers by name, answers in the language they write or speak (English, Luganda or Kiswahili), understands and answers voice notes, explains and demonstrates microinsurance cover, registers new customers, takes applications for demo cover and files claims. Kato was prepared for an unsupervised field test with shopkeepers, market traders and boda riders in Kampala. It runs in the dev environment only, on demonstration products, and no field-test results are recorded here.

IIP has two surfaces. Insurer staff work in the **staff workspace**, signed in through Microsoft Entra. Policyholders use a **customer portal** that needs no account and no password: they open a single-use secure link, then start a claim, upload documents, follow progress, view their policy, ask about their wording and request a contact-detail change. Customers also reach the insurer by WhatsApp, SMS and email. On WhatsApp, customers can write or send a voice note in Luganda, Kiswahili or English and be answered in the same language, in the dev environment only, using machine translation and speech models that have not yet been reviewed by a native speaker (see Languages and Kato). Everything else a customer sees is English.

## What IIP Is and Is Not

| | |
|---|---|
| **Is** | An Azure-native, human-reviewed insurance operations layer over existing insurer processes and systems. |
| **Is** | A tool that converts fragmented claims, document, email and WhatsApp inputs into structured, evidence-linked case work. |
| **Is** | A pilot-first product, starting with Motor Claims and Document Intelligence to prove operational value before broader rollout. |
| **Is not** | A replacement for policy administration, claims management, finance or other core insurer systems. It integrates with or references them. |
| **Is not** | A payment engine. It does not release claim payments. |
| **Is not** | An autonomous fraud or claims decision system. It flags risk signals; humans decide and communicate outcomes. |
| **Is** | Two surfaces: a staff workspace behind Microsoft Entra, and a customer portal reached by a single-use secure link where a policyholder files a claim, uploads documents, tracks progress, views their policy, asks about their wording and requests a contact-detail change. |
| **Is** | Configurable by the insurer: products, claim stages, document requirements, service standards and customer message templates are settings an administrator owns, not code. |
| **Is not** | A customer account system. Customers have no password and no stored login; each secure link opens one session scoped to that customer. |
| **Is not** | A place to buy a policy, change cover or pay a premium. IIP configures how a product is serviced, not how it is rated, priced or sold. The one exception is the demonstration: on the dev environment's demo phone, Kato issues demo cover at once; for any other number an application waits for a person to act on it. |

The governing principle: **AI prepares insurance work; humans make regulated insurance decisions.** This covers claim approval or rejection, payment release, policy binding, final premiums, underwriting and fraud determinations, and any regulated customer communication.

## Stakeholder Value

| Stakeholder | Primary value |
|---|---|
| Executive leadership | Operational visibility, ROI evidence, controlled AI adoption, full auditability. |
| Claims leadership | Queue visibility, SLA control, document completeness tracking, escalation management. |
| Claims officers | Faster review through AI summaries, document extraction and customer context. |
| Compliance and audit | Traceable actions, source-grounded AI outputs, exportable approval evidence. |
| Customer service | AI-assisted customer update drafting with full channel context. |
| Policyholders and claimants | Start a claim, send documents, see what is still needed and follow progress without calling, from any phone with a browser or WhatsApp, and without creating an account. |
| IT and security | Azure-first deployment, Microsoft Entra identity, Key Vault secrets, role-based access, monitoring. |
| Otic and product team | A clear MVP boundary, roadmap control and a defined pilot success path. |

## Two Surfaces: Staff Workspace and Customer Portal

| | Staff workspace | Customer portal |
|---|---|---|
| Who uses it | Claims officers, supervisors, customer service, compliance, administrators | Policyholders and claimants |
| How they get in | Sign in with Microsoft Entra; access is a role in the insurer's workspace | A single-use secure link sent to the email, phone or WhatsApp number on the claim; no password, no account |
| What they can do | Work claims, review documents, approve drafts, configure products, manage agents and settings | Start a claim, upload documents, follow progress, view their policy, ask about their wording, request a contact-detail change, ask for a human review |
| What they cannot see | Other insurers' data (tenant isolation) | Reserves, fraud or risk signals, internal notes, other customers' data, other insurers' data |
| Language | English | English (WhatsApp, which reaches the same claim, also works in Luganda and Kiswahili) |

### How a customer gets in

1. The customer opens the portal address and enters their policy number and the email or phone number on their claim.
2. IIP sends a one-time secure link to that contact. The portal never reveals whether a policy exists, so it cannot be used to find out who is insured. A link requested on the web page is valid for 15 minutes; a link the WhatsApp assistant sends after opening a claim is valid for 24 hours and goes only to the WhatsApp number the conversation came from.
3. Opening the link starts a session scoped to that customer. Nothing personal is shown before the link is opened.
4. Where a workspace is not connected to SMS or email (the current dev environment), the link is shown on screen with a label saying so. This is never done in staging, production or reviewer environments.

### What the customer does there

| Screen | What the customer can do |
|---|---|
| Start a claim | Choose the claim type, give the date, describe what happened, and add vehicle registration, location, whether a third party was involved and whether anyone was injured. The claim is created and the AI prepares it for review automatically, the same way as a staff-entered claim. |
| Track a claim | See the claim's status, what happens next, and a checklist of the documents required for their product, marked received or missing. Upload documents from the same page, or ask for a human review with a reason. |
| Your policy | See cover dates, annual premium and what is covered. Ask a question about the wording and get an answer grounded in the insurer's approved policy documents, with a source they can check. Request a change to their email or phone number, which staff review before anything changes. |

Customers reach the same claim by WhatsApp: Kato recognises a customer by phone number, collects the claim in conversation, reads it back, opens the claim only after the customer confirms, and sends the secure link so the customer can upload photos and papers. Kato never asks for ID documents or permits in the chat. A claim opened from a Luganda or Kiswahili message keeps the customer's own words and language beside the English description, and the claim page says the description is a computer translation.

### Known limits of the customer portal

- The portal screens are English only, with no language selector. Only the WhatsApp conversation works in Luganda and Kiswahili.
- Documents are sent one at a time against the required-documents checklist, with replace and remove. Nothing a customer sends is ever deleted: a replaced or removed file is marked superseded and kept, with its history, for staff.
- Premium status shows Not configured, because no billing or policy system feed is connected.
- Status wording shows the internal status names rather than a plain-language customer vocabulary.
- Sending the secure link and the delivery of messages have not been proven against a live SMS, email or WhatsApp account.
- The portal does not take payment, sell cover or change cover.

## Languages

| | Position |
|---|---|
| **Working in dev** | WhatsApp customers can write or speak in **Luganda, Kiswahili or English** and receive a reply in the same language, as text, or as a voice note when they sent one. The language a customer uses in a message wins over an earlier preference, so a short follow-up stays in it. The customer's message is translated to English, the normal claim process runs on the English, and the reply is translated back. Staff see the customer's own words and language next to the English in the inbox, and a translated reply says a computer translated it. |
| **Engines** | Translation: a tenant administrator can connect **Sunbird AI** from the Connections screen with no redeploy (the key is stored encrypted and never shown again); dev uses it. The self-hosted AfriSLM model (0.8 billion parameters, running in its own Azure container) remains the deployment fallback for a tenant with no connection. Speech: Sunbird transcribes voice notes and Sunbird speech synthesis produces the spoken reply. Translation prompts require word-for-word output with nothing added or removed. Greetings reach Kato as greetings, product names are protected from translation, and reply sentences are translated in parallel. AfriSLM is accurate from Luganda or Kiswahili into English, good into Kiswahili, and makes mistakes into Luganda with insurance words, so those are kept in English. Sunbird has not yet been compared with it by a native speaker. |
| **Not reviewed** | No native speaker has checked any translation or spoken reply. A review sheet of 120 rows of real output from the earlier engine exists. Where translated replies are off, customers are still understood but answered in English. If translation or speech fails, the customer gets the English original, or a written reply, and a message that cannot be understood goes to a person with its original words. |
| **Still English** | The staff workspace, the customer portal screens, email, SMS, and everything a customer is sent outside WhatsApp. Voice is WhatsApp voice notes only, not phone calls. |
| **Not covered** | Lugisu, Ateso, Runyankole and Acholi are outside the chosen model. |

The design is built so that translation can never make a decision:

- A customer writes in Luganda, Kiswahili or English, the assistant understands it, and replies in the same language. Staff always see the English next to what the customer actually sent.
- Translation is a step on either side of the existing assistant, not a second assistant. The human-decision boundary, the auto-reply conditions, consent and identity checks all run on the English text.
- Kato's own fixed replies are short (two or three sentences in Luganda or Kiswahili), are written to be readable aloud by someone with limited literacy, and are machine translated like any other reply, labelled once per conversation. Human-written, reviewed translations that replace them are the planned next step, and the review sheet is where they come from.
- A machine translation is shown to a customer only after it passes validation. If it fails, the customer receives the English original, marked as such. A message that cannot be translated is escalated to a person with its original text.
- Policy numbers, amounts, dates and phone numbers are protected from translation.

The risk to state plainly: translation and speech models can produce fluent-looking output that is wrong, most visibly in Luganda. Native-speaker review is a gate before any customer outside a test relies on it, not a follow-up. Customer words and voice notes are also sent to a third-party service (Sunbird) for translation and speech, which needs a data-processing decision before live customer data is involved (see Open Decisions).

## Kato: the WhatsApp Assistant

Kato is the name the WhatsApp assistant uses for the demonstration insurer, Nile Shield. He introduces himself as Nile Shield's helper on WhatsApp, says in plain outcome words what he can do (for example, if you have cover and something has happened, he can help you get your money), and always offers a person. He does not claim to be a person; if asked, he answers truthfully and simply.

| | Position |
|---|---|
| **How he works** | Kato is an agent on Azure OpenAI that holds the conversation and acts only through tools: look up the sender's account, list products, search policies, register a customer, apply for cover, file a claim, send a claim link and hand over to a person. If the model fails, the earlier rule-based replies take over. |
| **Rules live in the tools, not the prompt** | A tool sees only the sending number's own records. A claim is filed only when the customer's latest reply confirms Kato's read-back. Papers go only through the secure link. An application is issued at once only on the demo phone (the operator's standing approval); for any other number it waits for a person. |
| **Link safety** | A secure link is issued only when the sending number matches the policyholder's number on record, checked in the assistant and again where the link is made. A typed policy number unlocks nothing. This was treated as a security fix. |
| **No duplicate claims** | If the sender has an open claim on the same policy from the last 72 hours, Kato adds to it and asks whether this is a new incident. Photos never open a claim; they attach to the latest open claim or wait for a person. |
| **People without a policy** | Kato asks about the person's work and life, pictures the risk in their own numbers, recommends one or two covers and the level that fits, answers doubts truthfully, says exactly what a cover would pay, and asks for the decision without pressure. He records interest as a lead and never recommends across insurers. |
| **Identity** | Greets by the WhatsApp profile name and asks when there is none. A new contact is saved without any cover. Placeholder names such as Customer are never used. |
| **Voice** | A voice note is transcribed and handled exactly like typed text, and answered with a voice note in the reply language. Links and the translation note are not read aloud; a reply with a link also goes as text. If speech fails the customer gets text. Silence is not an outcome. |
| **Demonstration products** | Microinsurance only, all sums insured under UGX 10 million and labelled as microinsurance: hospital cash and funeral, boda rider personal accident with daily income, market stall stock cover, Small Business Shop Cover (levels of 2, 4 and 8 million), Goods in Transit for traders carrying stock, and Mama Care Maternity. Each has key information (waiting period, exclusions, grace period), wording and a how-to-join, pay and claim page. They carry an unmistakable demo marker; no IRA approval reference is shown, because none exists. |
| **Operator commands** | `#reset` and `#demo` work only from the one configured demo number and only in the Demo runtime profile. `#reset` returns that number to a first-contact state for the next participant. |

What Kato does not do: decide or promise a claim outcome, tell anyone a loss is not covered, complete a sale for a real customer, take payment, or speak in a phone call. Voice is voice notes.

Status: built and tested; items 1 to 9 of the field-test list are deployed to dev, and the last two fixes (about 9 seconds saved per message by skipping an unused model call, and Luganda wording) are committed but not yet on dev. Document requirements that vary by claim value are not built. Live proof from a real handset and the results of the field test are not recorded here.

## Release 1 Scope

Release 1 targets a **Motor Claims and Document Intelligence** pilot for a mid-to-large insurer.

In scope:

- Guided First Notice of Loss (FNOL) intake through WhatsApp, email and other channels.
- Document upload, OCR and field extraction, missing-document detection and evidence linking.
- Claim summary generation and advisory fraud-signal flagging (never an autonomous decision).
- Human review, supervisor approval and audited next-step execution.
- Customer-safe update drafting, held for human approval before sending. Staff can edit an AI draft, write their own, or send from a template with the customer's name and claim reference filled in, and every message carries a short SMS form.
- A customer portal, reached by a secure link, for starting a claim, uploading documents and following progress.
- Product configuration: claim stages, document requirements and service standards for each product, versioned and published with a second person's approval.
- A ranked brief on sign-in showing each person the work that needs them most, with a reason.
- Full audit logging and an exportable evidence and claim pack.

Out of scope for Release 1: customer accounts with passwords, buying or changing cover, premium payment, payment release, policy binding, and autonomous underwriting or fraud determinations. The Kato assistant, local-language WhatsApp replies and voice notes exist in dev as a field-test capability and are not part of the approved Release 1 baseline.

## Platform and Architecture

IIP is built on .NET 8 (Domain, Application, Infrastructure and API layers) with a Next.js, React, TypeScript and Tailwind staff portal, deployed on Azure. Identity runs through Microsoft Entra, with tenant- and workspace-scoped authorization resolved server-side. Every provider (AI, document extraction, storage, messaging) sits behind an interface with an honest "Not configured" state when a real credential or environment is missing, so the interface never shows a working integration that is not one.

Internal staff modules: Command Center, Channels, Customers, Sales and Leads, Claims, Documents, Knowledge Base, Approvals, Compliance and Audit, Reports and Analytics, Integrations, Admin Settings, and Support and Health. Customer engagement runs through WhatsApp, SMS, email and the customer portal at `/access`. Customers never sign in to the staff workspace, and the customer portal is scoped so that a customer sees only their own claim and policy.

Staff experience: signing in opens a ranked brief of the work that needs the person most, each item with a reason built from real fields (document completeness, working days against the product's service standard, value at stake, risk signals and how long the customer has waited) and one action. My Work uses the same ranking. The claim page opens on what needs a decision and whether it is ready, with the claim's configured stages shown as progress. The first-run tour is opt-in, and priorities and AI lines explain why they appear.

Service standards default to the regulator's own published claims timelines, counted in working days against a default Ugandan public holiday calendar, with a value-tiered settlement clock. An insurer can set its own standards. The holiday default does not compute the lunar-dated Eid holidays; a workspace that needs them adds them by hand.

## Human Oversight and Governance

AI may summarize claims, classify documents and extract fields, identify missing documents, draft customer-safe updates, retrieve grounded policy knowledge, highlight risk signals, recommend next steps and prepare audit evidence.

AI may not approve or reject a claim, release or deny a payment, bind a policy, set a final premium, make a final underwriting or fraud determination, or send a final regulated communication without human approval. Every consequential action is gated on a named human reviewer. Sensitive actions are audited, and tenant and workspace isolation is enforced server-side rather than by client-supplied identifiers.

## Current Status

This section was reconciled against the commit history through 2026-10-05 and spot-checked against source. "Built" means the code exists and is tested; it does not mean proven against a live insurer, a live Meta account or live SMS traffic. No live customer data is held today; the demo environment is deterministically synthetic.

| Area | Status |
|---|---|
| AI (Azure OpenAI, Document Intelligence, AI Search) | Built and working in the dev environment. Azure OpenAI also drives Kato's tool-calling agent. |
| WhatsApp | Connection setup, webhook registration and a customer assistant that recognises customers by phone, collects a claim in conversation and sends an upload link are built. Fixes for real inbound messages landed 2026-09-25. Meta now gets an immediate acknowledgement while the assistant runs in the background, and a bug that refused every staff manual reply was fixed and verified live in dev on 2026-09-28. Live Meta traffic proof from the field test is not recorded. |
| SMS (EgoSMS, Africa's Talking) | Built and tenant-configurable; not yet exercised against a live account |
| Email | Inbound collection built 2026-09-14 (scheduled polling and on-demand ingestion); SMTP outbound built, its STARTTLS path unverified |
| Claim intake (FNOL) | Built: reference-number acknowledgement, required-documents checklist, named owner assigned on intake, and a grouped sidebar that reaches every screen |
| Agent configuration | Built: tenants can set an agent's voice and scope; the safety preamble carrying the human-decision boundary is not tenant-editable |
| Consent, retention and data-subject rights | Built: consent records enforced on outbound WhatsApp, scheduled retention enforcement, data export, rectification and erasure with a refusal path |
| AML/CFT | Partial: two advisory rules on the fraud-signal engine. This is not a full AML/CFT risk assessment programme. |
| Insurer core-system (policy administration) integration | Partial: an integration port and a generic REST adapter exist; no insurer system has been connected. Policies can also be loaded from a file. |
| Products and policies | Built: versioned product configuration with an IRA approval reference required before publishing and a second person's approval to publish, a default claim-stage library with service standards in working days (value-tiered for settlement), a document matrix per product, an admin screen to configure it, claims pinned to the product version in force, and an advisory policy-in-force and sum-insured check at intake. There is no dry run or test-claim preview yet. |
| Document types | Built: a tenant-owned, versioned catalogue of document types on top of the eight built-in types. A product's document matrix can no longer hold a key that silently drops out of completeness tracking; an unrecognised key is refused, and a report lists any existing product that holds one. |
| Document learning | Built: classification that learns from human corrections, with accuracy measured per document type. Not yet exercised on a real insurer's document set. |
| Retention and leads | Built, not activated: renewal watching, a satisfaction question when a claim closes, lead routing to a licensed person, and referral invitations after a good close. Marketing-style messages are gated on recorded consent. |
| Customer portal | Built: sign in by single-use secure link (no password), start a claim, upload documents one by one against a required-documents checklist with replace and remove, follow claim status, ask for a human review, view a policy, ask a grounded question about wording and request a contact-detail change that staff review. English only, premium status not connected, and live link delivery is not proven. |
| Kato (WhatsApp assistant) | Built and deployed to dev, except the two latest fixes: tool-using agent, link safety, duplicate-claim protection, microinsurance products and a no-policy sales path, greeting by name, voice notes in and out, per-conversation language. Prepared for a field test; results and live handset proof not recorded. See Kato. |
| Languages | WhatsApp (text and voice) works in Luganda and Kiswahili in dev through a tenant-configurable Sunbird AI connection, with AfriSLM as fallback; translated replies are labelled and unreviewed. Everything else is English. |
| Staff experience | Built: ranked brief on sign-in, My Work ranked with reasons, a claim page organised around the decision, follow-through reminders (a customer reminder when documents stay missing, and an officer warning before an SLA breach), and an opt-in tour with explanations for priorities. Merged to `main`; live verification of these screens is not recorded here. |
| Inbox and approvals | Built: the inbox shows the customer's name (by phone, even before a claim is linked), their language and what was understood. Approving, rejecting or sending a routine customer-update draft is one click with an optional reason; settling or closing a claim still requires one. The WhatsApp reply form defaults purpose, consent and language sensibly. Demo seeding creates a real published product per line (Motor, Medical, Life, General, Agriculture) with a claim process and document matrix. |
| Customer messages | Built: edit an AI draft before it is sent, write a message from scratch, or send from a template library with variables; the original AI text is kept beside the final text; each message has a short SMS form. |
| Interface | All 39 screens migrated to the current design language on 2026-09-09; raw identifiers and status codes removed from user-facing text |

Still open: SMS delivery receipts, staging and production environments (none provisioned), any live-traffic proof for WhatsApp, SMS and the core-system adapter, and the field-test results.

Insurer readiness: a research note (`IIP_INSURER_READINESS_GAPS.md`) names the three main gaps between this build and a real insurer. First, IIP does not yet talk to insurers' own systems (no connector is built; policies load from a file). Second, several operational controls are partly missing (secret management, an in-memory queue, rate limiting, penetration test, tested restore, Data Protection registration, hosting region). Third, there is no packaged way for an insurer to sign up and start. A multi-tenant software-as-a-service edition addressing these is being built in a separate workspace; it is not deployed and is not part of this document's status.

Regulatory position: IIP does not underwrite, bind, price, settle or decide, and every consequential action is gated on a named human reviewer. Otic will complete data-processor registration under Uganda's Data Protection and Privacy Act before handling live customer data on an insurer's behalf.

## Roadmap and Near-Term Milestones

The roadmap's own phase table was last updated in July and predates most of the work above, so the statuses below combine it with the commit history.

| Phase | Focus | Status |
|---|---|---|
| 1 to 2 | App foundation, identity, core staff workspace shell | Done |
| 3 | Cloud foundation (Azure dev environment) | Done for dev; staging and production not provisioned |
| 4 | Motor Claims workflow MVP | Built and deployed to dev; a full authenticated end-to-end proof is not recorded |
| 5 | Document Intelligence and AI review | Built, including classification that learns from corrections and accuracy measurement |
| 6 | Integrations and resilience (email, WhatsApp, SMS, queues) | Built; live-traffic proof outstanding |
| 7 | Security, compliance and observability hardening | Partial: consent, retention and data-subject rights built; Application Insights capture unverified |
| 8 | QA, UAT and pilot readiness | In progress; pilot client and baselines not yet agreed |
| 9 | Staging and Marketplace packaging | Not started; needs a subscription able to host staging |
| 10 | Pilot launch and stabilization | Not started |
| 11 | Commercial modules beyond Motor Claims | Partly built (renewal watching, satisfaction question, lead routing, referrals); not activated |

The near-term priority is a live end-to-end proof of the built channels and the Motor Claims path, then a named pilot client and baselines, before staging and production packaging.

## Claims We Cannot Yet Support

Use this list when reviewing any external material, including sales decks, against the current position.

| Claim to avoid | Current position |
|---|---|
| AI decides or executes underwriting, pricing or claims decisions | AI prepares work; a named human makes every regulated decision. An audit sample showing an AI agent approving a claim contradicts the product design. |
| Coverage across growth, underwriting, pricing, claims, fraud and executive reporting as finished capability | Release 1 is motor claims and document intelligence. Other modules are partly built or not activated. |
| Connected to core systems and payments | A core-system integration port and generic REST adapter exist but no insurer system is connected. Payments are out of scope. |
| Live in 1 to 2 weeks inside the insurer's tenant | Dev environment only; staging is empty, no pilot client is named, six functional approvals are pending and pilot launch has not started. |
| "Compliant" with the IRA and the Data Protection and Privacy Act | Data-processor registration is not complete and AML/CFT is advisory only. Consent, retention and data-subject-rights features exist to help insurers meet their own obligations. |
| Immutable logs, customer-managed keys and confidential computing by default | Audit logging exists; the other three are not documented as built. Treat as roadmap until evidenced. |
| Quantified ROI, such as a conversion uplift or a fraud-loss reduction | No pilot metrics or baselines exist. The product charter withdrew unsupported ROI claims. Remove or source any figure. |
| A proof point from another sector, such as a banking training exercise, presented as an insurance deployment | No insurer deployment or design partner is recorded. |
| Speaks Luganda, Kiswahili or other local languages, or is "multilingual" | Works for WhatsApp text and voice notes in dev using unreviewed machine translation and speech models. Do not claim quality, do not claim it for other channels or screens, and do not claim other local languages. |
| Kato sells insurance, issues policies or is IRA-approved | Kato explains and demonstrates microinsurance on demonstration products. Cover is issued at once only on the dev demo phone; otherwise an application waits for a person. The products carry a demo marker and have no IRA approval. |
| Kato is a person, or a phone-call voice agent | Kato is an assistant and says so if asked. Voice is WhatsApp voice notes, not calls. |
| Field-tested or proven with real users | A field test was prepared on 2026-09-30. No results are recorded. |
| Customers log in to an account, or manage their policy and payments online | Customers open a single-use secure link. There are no accounts or passwords, no premium payment and no buying or changing cover. |
| The customer portal is live for customers | Built and working in the dev environment. Live delivery of the secure link by SMS, email and WhatsApp is not proven, and no customer uses it yet. |
| Multiple product names for one platform | The official name is Insurance Intelligence Platform (IIP); staff application name is IIP Operations. |

## Open Decisions and Risks

| Decision or risk | Why it matters |
|---|---|
| Pilot client and sponsor | Determines governance, timeline and acceptance criteria. |
| Pilot deployment path and Azure subscription | Only the dev environment exists, on a sponsored subscription; the roadmap records a pay-as-you-go subscription as deferred pending sign-off. Staging is empty. |
| Named functional approval (Product, Insurance, Operations, Security and Privacy, Architecture, Delivery) | The Release 1 baseline is not yet formally approved; all six functions show pending. |
| Data-processor registration | Required under Uganda's Data Protection and Privacy Act before any live customer data is handled. |
| AML/CFT risk assessment | Required by an IRA directive of 8 April 2026; only two advisory rules exist, so a full risk-assessment capability is still open. |
| Live-traffic proof | WhatsApp, SMS, and the core-system adapter have not been exercised against live providers or an insurer system. |
| Pilot document checklist and success metrics | Needed to configure document intelligence and define how pilot value is proven. |
| Local-language release | A native speaker of Luganda and of Kiswahili must score the review sheet, and a sample of Sunbird translation and spoken output, before any customer outside a test relies on it. The AfriSLM container is an always-on billable resource in dev; whether to keep it as the fallback now that a hosted Sunbird connection exists needs a decision. |
| Third-party processing of customer words and voice | With Sunbird connected, customers' messages and voice notes are sent to a third-party service for translation and speech. This needs a data-processing and cross-border decision, and a place in the Data Protection registration, before live customer data is used. |
| Kato sales conduct | Kato recommends and explains cover to people with no policy. The insurer must decide what he may say, who follows up a recorded lead, and how this fits the IRA's rules on microinsurance and selling, before any real customer is offered cover. |
| Field-test review | The field-test conversations need reading for wrong answers, unsafe claims and drop-off points before the next build. |
| Customer-facing wording | The customer portal shows internal status names. A plain-language customer status vocabulary needs agreeing before a customer pilot. |
| Premium and policy feed | The customer portal cannot show premium status until an insurer's billing or policy data is connected. |

No approval should rely on simulated or unverified provider behavior. Every claim here is either evidenced in current code or marked as a target.

## Sources

This overview consolidates:

- [iip_master_product_context.md](iip_master_product_context.md): product definition and positioning (June 2026 baseline).
- [iip_stakeholder_alignment_brief.md](iip_stakeholder_alignment_brief.md): stakeholder value and alignment questions.
- [IIP-CONSOLIDATED-ROADMAP.md](../08-program-plan/IIP-CONSOLIDATED-ROADMAP.md): phase sequencing and build status (last detailed entries July 2026).
- [IIP_HACKATHON_READINESS_BLUEPRINT.md](IIP_HACKATHON_READINESS_BLUEPRINT.md): code-verified feature status as of September 2026.
- [AGENTS.md](../../AGENTS.md): governing product, architecture and security contract.
- [IIP_LOCAL_LANGUAGE_BUILD_PLAN.md](IIP_LOCAL_LANGUAGE_BUILD_PLAN.md): the Luganda and Kiswahili plan, built in dev since 2026-09-27.
- [IIP_KATO_FIELD_TEST_AUDIT.md](IIP_KATO_FIELD_TEST_AUDIT.md) and [IIP_KATO_BUILD_PROMPT.md](IIP_KATO_BUILD_PROMPT.md): what Kato was before the field test, and the build pass against it.
- [IIP_WHATSAPP_VOICE_DESIGN.md](IIP_WHATSAPP_VOICE_DESIGN.md): the voice design.
- [IIP_INSURER_READINESS_GAPS.md](IIP_INSURER_READINESS_GAPS.md): the three main gaps between the build and a real insurer.
- [IIP_NEXT_BUILD_PROMPT.md](IIP_NEXT_BUILD_PROMPT.md): the configuration, integration and customer self-service build that most of the recent status above comes from.

Also used: the git history through 2026-10-05 and [AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md](../05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md) for what exists in Azure.

Where these sources disagree, current code and `AGENTS.md` take precedence. Known stale sources: `iip_master_product_context.md` (row C9) still says the product is not customer-facing and that external flows use only WhatsApp and web channels, which predates the customer portal; the readiness blueprint's gap table still lists consent, retention, data-subject rights, the core-system port, agent configuration and navigation as missing or blocked although the code and commits show them built, and the roadmap's phase table predates September.
