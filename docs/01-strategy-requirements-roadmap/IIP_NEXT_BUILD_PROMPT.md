# IIP next build prompt

One continuous build pass, written to be run with `/loop` (no interval, so the agent self-paces).
Paste the whole of section 2 onwards as the loop prompt, or point the loop at this file.

Companion research: the three briefs on making IIP proactive, integrating with Uganda's insurance
stack, and answering the two questions insurers ask (product launch and customer self-service).

---

## 1. How to run this

- Run with `/loop` and no interval.
- The agent does not stop to ask between outcomes. It stops when every acceptance criterion is met
  and verified on the Azure dev environment, or when it hits a genuine blocker: a missing
  credential, missing live access, or a decision only a person can make.
- Work stays on `feature/phase0-1-connections-channels`.
- The order below is a suggested order, not a gate. Nothing here is a phase.

---

## 2. Mission

Take IIP from a capable tool that answers when asked to a platform an insurer can adopt without
changing the system it already runs. Two things decide whether that lands:

1. **It fits their business.** An administrator configures a product's claim process, its document
   requirements, its service standards and its messages, with no developer and no release.
2. **It fits their customers.** A policyholder sees their policy and their claim, files a claim and
   sends a document, on the phone they already have.

Everything else in this prompt serves those two, or makes the daily work of a claims officer
feel like a colleague prepared it.

## 3. Read first, and follow

`AGENTS.md`, `CLAUDE.md`, `DESIGN.md` and the four UI documents it indexes, the 27 point checklist
in `docs/03-design-architecture/iip_ui_craft_guardrails.md`,
`docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md`, and
`docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` before any deploy.
Where a document and the code disagree, the code wins: re-verify, then correct the document.

## 4. Non-negotiables, in every change

1. AI prepares, people decide. Nothing automated approves, declines, settles, pays, binds, prices,
   or accuses anyone of fraud.
2. Priorities and decisions come from deterministic rules over real fields in the Domain layer,
   with tests. A model may write the sentence that explains a result, never the result itself.
3. Every actionable AI assertion carries a citation that opens its source.
4. Only low risk transactional messages may send automatically, and only where the tenant has
   opted in for that message type. Everything else is drafted and waits for a person.
5. No non-transactional message to a customer without a recorded consent.
6. Nothing personal is disclosed to a customer on any channel before an identity check passes.
7. Tenant isolation and permission checks stay server side. Never weaken a guard to pass a check.
8. Six states on every surface: loading, empty, empty after filter, error, not configured, no
   permission. Absent capability renders Not configured, never a fake success.
9. No emoji. No em dashes anywhere, including code, comments, commit messages and documentation.
10. No fabricated data, dead controls or placeholder names.
11. Business rules belong to the insurer, not to us. Service standards, process stages, routing
    teams, document requirements, authority limits, knowledge document types, nudge timings and
    the weights in the priority rule are tenant settings, with today's hardcoded values as their
    defaults, changed by an administrator and audited.
12. What stays fixed: the human decision boundary and the immutable platform preamble, the status
    vocabulary and its labels, the append only audit trail, a message once sent, and every
    citation and approval record. An edit never overwrites history; it writes a new version.
13. IIP configures how a product is **serviced**. It does not rate, bind, quote, price, or issue a
    policy, and it never recommends a product to a customer. That is licensed intermediation and
    it stays with the insurer and its licensed people.
14. Smallest coherent forward change. No competing architecture, no unrelated cleanup.

---

## 5. Outcomes and acceptance criteria

### A. Product configuration, the spine of everything else

A product record an administrator creates and edits: name, class, the IRA approval reference and
its effective date, and the approved wording attached. A product cannot be activated without an
approval reference, and the screen says so in plain words rather than failing silently.

Each product carries, and each is editable without a developer:

- **A claim process.** Ordered stages, each with an owner role, a service standard in working days,
  and whether it is automatic or needs a person. The default stage library covers what Ugandan
  claims actually do: intimation and reference, acknowledgement and document request, assessor or
  site visit appointment, adjustment or investigation report, offer or repudiation, discharge
  voucher, payment, and complaint escalation.
- **A document matrix.** Each item has an internal type, the words the customer sees, why it is
  needed, whether it is required or optional, at which stage it is needed, and whether it exists
  because the regulator or the wording demands it.
- **Message templates.** See outcome D.
- **Knowledge.** The approved wording and FAQs for this product, so a question about this product
  cannot be answered from another product's document.

Acceptance: an administrator can take a new product from nothing to a configured process, document
list and messages, run a test claim through it, and publish it, without a developer, a deploy, or
a database change. A claims officer sees the configured stages on a real claim.

### B. Service standards default to the regulator's own clock

IRA publishes claims service standards. Ship them as the defaults, in working days, respecting
Ugandan public holidays, and let an insurer commit to better:

| Stage | IRA indicative timeline |
|---|---|
| Claim intimation | As per policy conditions |
| Claim acknowledgement and document request or contact | Within 3 working days from intimation |
| Site visit or appointment of assessor or adjuster | Within 3 working days, 5 for complex claims needing IRA approval |
| Settlement offer or communicating repudiation | 5 working days from the final adjustment or investigation report, or from the final supporting documents |
| Settling the claim | Up to UGX 10M within 10 working days of the discharge voucher; above 10M to 50M within 15; above 50M within 20, or on the reinsurer cash call, whichever is first |

Acceptance: every SLA figure in the product, the brief, the nudges and the claim page is counted in
working days from these settings. The settlement clock uses the value tier. Changing a standard
changes what the screens show, and is audited.

### C. Configuration is versioned and safe to change

Every product, process, document matrix and template change creates a new version with an effective
date, an author and a reason. A claim runs under the version in force when it was filed, so
changing a document list never rewrites the rules for claims already open. Publishing requires a
second person to approve and is written to the audit trail. A dry run against a sample claim shows
what the change would do before it is published.

Acceptance: a test proves an open claim keeps its version after a product change is published.

### D. Messages: editable, composable, and template driven

Today the only way a customer message exists is that a model wrote it, and there is no edit path at
all. Fix the whole of it:

- Edit a drafted message before it is sent. Keep the model's original text alongside the final text,
  with who changed it and when, so the audit shows both.
- Write a message from scratch with no model involved.
- A template library, implementing the `CommunicationTemplate` type that exists in the Domain and is
  never used: a template belongs to a stage or reason and a channel, and holds a subject, a long
  body and a short SMS body, with variables for the customer's name, the claim reference, the
  missing documents and the officer's name.
- Every message carries the customer's channel and language and a short plain language form that
  fits an SMS, because most phones in this market are not smartphones.
- Editing an approved draft returns it to pending approval, unless the approver edits and approves
  in one step, which is recorded as exactly that. A sent message is never editable; a correction is
  a new message.
- Templates obey the same content checks and approval rules as any other message and cannot be used
  to bypass them. Seed the library from the messages already in code.

Acceptance: an officer can edit an AI draft, write their own, or send a template in one click, and
the audit trail shows which of the three happened.

### E. A claim is carried to the point of a decision

On submission through any channel: issue a reference and send the acknowledgement (transactional,
auto send allowed); check documents against the product's matrix; check the policy was in force on
the loss date and the amount against the sum insured; record advisory risk signals; draft the
missing document request in the customer's channel; assign a named officer by workload, product
line and authority rather than a team only, fixing the null assignment at intake and the demo seed
team taxonomy mismatch the blueprint records; and notify that officer with the one line reason.
Every step audited. Time from submission to a person's first available action is measurable.

### F. The brief replaces the empty chat as the landing screen

A Domain rule ranks each person's work from real fields: document completeness, working days
against the product's service standard, value at stake, risk signals, and how long the customer has
waited. Signing in shows at most five ranked items, each with a one sentence reason built from
those field values and exactly one primary action, plus a "Handled for you" list drawn from the
audit trail. The chat becomes a bar available on every screen rather than the screen itself. Honest
empty state when nothing needs the person.

### G. Follow through without a person remembering

A customer reminder when documents are still missing after the configured wait, and an officer
warning before an SLA breach rather than after it. Both idempotent, both audited, both visible in
the brief. Timings are settings.

### H. The claim page answers "what do I need to decide, and is it ready?"

Decision card first with a readiness checklist and a completeness meter, then the summary
restructured into what happened, what we have, what is missing and what to watch, every line
carrying its citation, then a compact customer profile, then evidence. Actions collapse to one
primary action and a More menu. The configured stages show as the claim's progress.

### I. My Work is ranked, and no raw identifier is rendered anywhere

My Work uses the same rule and shows reasons. Finish the identifier sweep: no GUIDs, no internal
ids, no camelCase field names on any screen, including the source and case lines in My Work.

### J. Guidance instead of a tour

The first run tour is off by default and available from help. Every priority and every AI line has
a "Why am I seeing this?" that explains the reason and the source in one sentence. Empty states
point at the next action.

### K. Documents: read, classify, and learn from corrections

Three layers, cheapest first:

1. The product's document matrix defines what exists and what is required. No model needed.
2. Classification and extraction through the existing Document Intelligence path, per tenant, with
   an explicit unknown class and a confidence threshold below which a person decides. Custom
   classification needs at least two classes and five samples per class and supports adding classes
   later, so design the labelling flow around that.
3. Every human correction, whether a re-label or a retyped value, is stored with the document as
   labelled data, improves the rules and prompts immediately, and accumulates for the next
   classifier version. Nothing is retrained silently. Training data never leaves the tenant.

Acceptance: accuracy per document type is measured and visible, and a low confidence document lands
in a person's queue with the reason shown.

### L. The customer signs in and sees their policy

Extend the existing claimant magic link into policyholder self service. No passwords: a one time
code or link, with identity verified before anything personal is shown. The same answers work as a
web view, as a WhatsApp conversation and as short SMS exchanges.

The customer can see their policies with cover dates and premium status, download the certificate
or schedule, see a claim's progress and what is still needed, file a claim, send a photo of a
document, ask a question about the wording and get a cited answer, and request a change to their
contact details for staff approval. Every disclosure is logged against the verified session.

### M. Consent is a first class record

What the customer consented to, on which channel, when, through which interaction, and how it was
withdrawn. Enforced before any non transactional message, visible on the customer view and in the
audit trail. Do not design the customer or policy model so that a person reached through a group,
or a payer who is not the policyholder, becomes impossible to add later.

### N. Meet the insurer's existing systems where they are

- **A file import.** Upload an export of policies, customers or open claims as CSV or Excel, map
  columns on screen, see a validation report, load read only. This is the connection an insurer
  whose core system has no API can accept in a day.
- **Read only lookups.** Extend the existing policy lookup port and its configurable REST adapter to
  customers and claims, with per insurer field mapping through the encrypted connection model.
- **A partner facing API description.** Publish a curated OpenAPI document of the endpoints a
  partner would call, named after ACORD NGDS objects where they map cleanly, with a sandbox tenant
  seeded with demo data.

Acceptance: a policy that came from a file or a partner API is visible on the claim page and in
self service, marked with where it came from and when it was last refreshed.

### O. Retention watch

Policies whose cover ends in 60, 30 and 14 days, scored by claim experience, premium change and
time since last contact. Reminder drafts prepared for approval, never auto sent, consent gated. A
screen of its own and a line in the brief showing premium at risk. When a claim closes, ask one
satisfaction question, and open a service recovery task on a poor answer.

### P. Channel assistant, within the licensing boundary

Implement the customer assistant in the blueprint's section 8.3 with its small intent set. Answer
general questions only from approved knowledge with citations; auto reply only when every condition
in that section holds; capture interest as a lead in the existing list and route it to a licensed
person; verify identity before anything case specific; never recommend a product, quote a price or
complete a sale. After a claim closes well, prepare a referral invitation that a person approves
and that consent gates.

### Q. Documentation and honesty

Update the agent registry document, the blueprint status tables and the deployment document to
match what is now true. Mark anything not verified live as exactly that, with the reason. Never
mark done what was not verified.

---

## 6. Verification

Run these and report the real results:

```
dotnet build InsuranceIntelligence.sln
dotnet test InsuranceIntelligence.sln
cd web/portal && pnpm lint && pnpm build
```

New tests for every behaviour above, including the ranking rule, working day and holiday counting,
configuration versioning against an open claim, consent enforcement, auto send conditions,
assignment, nudge idempotency, identity checks before disclosure, the file importer's validation,
and the assistant's intent routing.

Open every changed screen in a real browser at 1280px and 768px and look at it before calling it
done.

## 7. Commit and deploy

Commit and push after each coherent outcome on `feature/phase0-1-connections-channels`, with
messages that say what changed and why in plain language. Never bundle unrelated work.

Read the deployment document and confirm resources with `az` before deploying. Deploy the API and
the portal to the dev environment. Expect the portal container to take three to five minutes to
start, and the API to stall for several minutes after a restart while SQL connections recover.

Then verify by using it, not by inferring it:

1. Configure a product end to end and run a test claim through its stages.
2. Submit a claim through the claimant portal and confirm the acknowledgement, assignment, drafted
   document request and officer notification appear without a refresh.
3. Sign in as an officer and check the brief ranks real work with real reasons.
4. Open the claim, read the decision card, edit a draft, send a template.
5. Open self service as a customer, pass the identity check, see policies and claim progress.
6. Import a sample policy file and find those policies on screen.
7. Open the retention watch.

Report what you saw.

## 8. When something is blocked

If a live provider is not connected (WhatsApp, SMS, mailbox) or a credential is missing, build
against the existing provider interfaces, prove the behaviour with tests and the sandbox path, mark
the item code complete with live verification blocked and the reason, and move on. Do not fake a
success path, disable a guard, or weaken validation to make a check pass.

## 9. Reporting and stopping

Each time you report: which outcomes are complete and verified live, which are code complete but
blocked and why, what is left, and what you are doing next. Keep it short.

Stop when every acceptance criterion is met and verified live, or when a blocker genuinely needs a
person. Say which it is.
