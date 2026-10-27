# Insurer demo build ledger

Updated at the end of every iteration. Newest facts win. Keep it short.

Worktree: `Projects/iip_saas_insurer_demo`, branch `build/insurer-demo-2026-10-27`, from dff5ab3. Local only, no deploys.

## Decisions made (owner away, recommended choices)

- One generic per-workspace store, table `WorkspaceRecords` (tenant, workspace, kind, key, JSON), behind `IWorkspaceRecordStore`. Brand, templates, rules, events, decision log and outcomes use it, so each piece needs no migration of its own. It carries a tenant id, so export and purge cover it automatically. Event volume can move to its own table later if a bank sends large files.
- Whose assistant Kato is: `KatoBrand.Current` now reads an ambient per-request brand (AsyncLocal, `KatoBrand.Use`) and falls back to the start-up default. Webhook inbound, outreach drafting, triage and conversation insights resolve the workspace brand at their entry points. The `Assistant:*` settings remain the default for workspaces that set nothing.
- Life versus general is `InsuranceClass` on each product (loan protector, funeral and school fees are life; the rest general) and `LineFocus` on the brand filters products by it.
- Brand API is `GET/PUT /api/v1/workspace/brand`, ConfigureIntegrations permission, audited (`workspace.brand.saved`). Its screen comes with piece 2 (scenario switch) and piece 14 (company profile).
- Nile Life is a separate life company in the Nile group, "Nile Shield Life Assurance", with three life covers (loan protector, family funeral, school fees) built from the partner life covers. Nile General is the existing "Nile Shield Assurance" restricted to general covers. `MicroinsuranceCatalog.Offered` holds every cover that can be sold; `All` is unchanged. Lookups by product line must go through `KatoBrand.Find` because several insurers share a line.
- A demonstration scenario is an organisation of its own (name ends "(demonstration)"), created for the signed-in person who becomes its admin, marked `IsDemonstration` with its `ScenarioId` in the brand record. Switching is the portal choosing which organisation to act in (localStorage override sent as X-Organisation-Id, or X-Tenant-Id in local mode, plus the workspace). Never available under a protected runtime profile; the Admin tile shows only in Demo, LocalTest and Development profiles. Re-running setup is idempotent and refreshes relative dates.
- Mavuno Bank loads the existing 39-person invented bank list (consent recorded). Eugene is added by piece 11 (demo customer form), because his phone must be a real consenting teammate's.

- Templates: stored as `template` records in the workspace store, keyed name:language. A workspace shows the 12 standard drafts (the plan's 11 plus `bank_cover_intro`) until it has its own. Only a person records WhatsApp's decision (submit, approve, reject with reason, pause) until provider sync exists; an owner can import a JSON file of templates that keeps its status. An approved template is never edited in place. The Cloud API provider now sends numbered placeholders in order without a parameter name (named placeholders still carry theirs).
- First-touch sending without a prior inbound message: `TemplateMessageService` sends an approved template to a person who has not written in, when the caller has checked consent and names it. It refuses a draft, a withdrawn consent, a missing value, marketing outside 07:00 to 20:00 EAT, an unconnected number and an unconfigured provider; a retry with the same key never sends twice. The existing free-text outreach flow is unchanged and still needs an open window.

- Events: nine event types from plan 14.3 (code, label, reason, suggested offer, timing). An event stores phone (normalised to 256...), type, when, source and only four safe details (amount, term_months, product_code, branch); a balance or account number column is never read and is named as left out. The id is a hash of type, phone, day and the bank's own reference, so a resent file changes nothing. Phones are masked in the list API. Acquisition has a new "Bank events" tab (record form with "when" choices for rehearsal, CSV upload).
- Connected folders can be marked `purpose: BankEvents` (API field on the folder entry): CSV files are read by `IBankEventFileSink`, state `Imported`, never become knowledge, never retired. Tested with a fake provider only. The folder picker screen has no purpose control yet; add it in piece 15 with the storage step.

- Rules: a rule is stored as `rule` records, approved once; its fingerprint (trigger, conditions, wait, covers, template, hours, limits, follow-up) ties the approval to the exact content, so any edit returns it to Draft and clears the approver, and a paused rule changed behind the back cannot resume. Who may approve is the workspace's `rule-settings` (default Admin; the bank admin changes it; optional four-eyes). The check uses the signed-in roles on the server.
- The engine (`RuleEngine.RunAsync`, run by the 5-minute `RuleRunWorker` and by "Run the rules now") works through each live rule and pending event: conditions, known customer, consent, cover already held, contacted recently, best fit (existing ProspectScoring, no commission anywhere), same cover, wait, hours, daily limit, template, send. Every decision, sent or not, is in the `decision` log with its gates. Scheduled decisions are retried when due. A decision is final once Sent, DemoSent, Skipped or Blocked.
- Demonstration honesty: in a demonstration workspace with no connected number or no approved template, the offer is recorded as `DemoSent` ("Shown as sent, not delivered to a phone") with a message row in a demo number thread. In any other workspace it is `Blocked` with the reason. A `ConversationReason` (why the conversation began) is saved per phone for piece 6.
- Not done in piece 5: the open-complaint gate (no complaint record to check yet), follow-up nudges and STOP handling (piece 7), time-based triggers such as expiry (pieces 9 and 10).

- Reason in the conversation: Kato's system prompt gets a "Why this conversation exists" block when a rule wrote first within 14 days (event kind, the opening text, the cover and insurer, what to do on yes / not now / stop / why did you write, never amounts). In a bancassurance brand Kato is also told to answer honestly that the bank earns a commission from the insurer and that it never changes the recommendation.

- Outcomes and STOP: `OutcomeService` classifies short replies narrowly (Yes show me, Not now, No thanks, Renew, STOP and its plain variants; "please stop by my shop" is not a stop) and records them against the offer that began the conversation (14 day window). A STOP from anyone, with or without a rule, makes Kato send one plain acknowledgement, then withdraw consent on the prospect record and in the consent history, and the model is never called. Later rule runs skip the person with "asked not to be contacted". An answer that needs a person (details, renew) is a hand-off: listed under "Replies" in the Rules tab with the rule, cover, opening words and their reply, plus a lead activity, until marked followed up. One gentle follow-up (rule.FollowUpAfterDays and template) goes after silence, never after any answer. "Applied" outcome is written by piece 12 conversions.
- Limitation: the hand-off is a list in the Rules tab, not a task in the staff My Work queue.

- Bank first-touch (piece 8): demo rules for Mavuno Bank are seeded as unapproved drafts when a scenario is set up (loan disbursed, salary account opened, school fees paid; wait 0 for rehearsal). A new gate skips someone with an open claim. Eligibility by age is not built because no age is recorded; ranking uses the existing ProspectScoring with no commission input. Eugene himself is added by the demo-customer form (piece 11).
- Renewals and premiums (piece 9): `PolicySweepService` turns policy dates into events (policy_ending_30, policy_ending_7, policy_lapsed, premium_due for life covers three days ahead) with fixed identities, run at the start of each rule run when a live rule uses one. They go through the same gates; "already holds the cover" and the same-cover limit do not apply to them, and the one-offer-per-person limit applies per rule so a 30 day and a 7 day reminder both go. Values (policy number, end date, due date, amount) come from event details, never invented. Nile demo customers get a contact record with agreement to receive reminders about their own policy. A missed premium is not detected: no payment records exist. Kato is told a colleague completes any renewal or payment.
- Claims as rules (piece 10): `CaseSweepService` turns claims into events (claim_received within two days of opening; claim_documents_needed every three days while the status is MissingDocuments, naming the papers from the same `IDocumentChecklistProvider` the claims team sees; claim_status_update once per stage, in words that describe where the claim is and never an outcome, tested against approved, rejected and paid wording). They use a separate gate path: the claimant may be messaged if an agreement is on their record or they wrote to us on WhatsApp, never if they declined or withdrew. Their decisions are `notice` kind so they do not count as a marketing offer. Kato is told to use get_my_account and send_claim_link, never to promise or decide, and not to mention other covers. Nile General seeds three claims (two waiting for papers) so the rules have work. The existing scheduled follow-through service is unchanged.
- Piece 10 verification: Domain.Tests 731 and Api.Tests 1673 passed, tsc clean, lint 0 errors; renewals.spec.ts (Nile Life renewals, Nile General claims) passes at 1280 and 768. The checklist provider was not exercised against a real product matrix.
- Demo customer and story (piece 11): `DemoCustomerService` adds a person to a demonstration workspace only (refused otherwise, and under protected profiles), with facts, a stated consent (agreed, never asked, declined; given 30 days ago so the story reads in order) and a loan event "days ago". The Bank events tab has "Add a demonstration customer" (visible only when `GET /api/v1/demo/status` says demonstration) with a "Fill in Eugene's details" button that leaves name and number for the real teammate. `PersonTimelineService` and the "Why this person" drawer (from a decision row or an event row) show consent, the bank event, each rule decision with every check, what went out and their reply, oldest first, from this workspace's records only. Fast-forward is not built: rehearsal uses "when it happened" choices, wait 0 in the demo rules, and policy dates relative to today. The sidebar workspace chip still shows the workspace id.
- Conversions and results (pieces 12 and 13): a "Conversions" tab in Acquisition for staff only. `ConversionReportService` counts per insurer: offers, answers, details asked, and people converted (applied for a cover of the offered insurer, or hold a policy of that insurer that began after the offer). It holds no commission rate and says so on screen; there is no commission property anywhere. `CampaignReportService` gives results per rule (looked at, sent, shown as sent, delivered and read from WhatsApp's own state for real sends only, answered, converted, and the reasons everyone else was not messaged, which add up to the looked-at count).
- Verification: Domain.Tests 731, Api.Tests 1688 passed, tsc clean, lint 0 errors; demo-customer.spec.ts (customer, story, conversions, results) passes at 1280 and 768. Delivered and read were tested with fake delivery states; nothing was measured against real WhatsApp.
- Onboarding profile and terms (piece 14): the organisation's `company-profile` (registered name, licence kind and number, regulator and country defaulting to Uganda, contact, website, "about you") and `terms-acceptance` (a named person, position, an explicit "authorised" tick, and the versions shown) live in the organisation's workspace. Acceptance counts only for the versions in force (`CompanyProfileService.CurrentTermsVersion` and `CurrentDataProcessingVersion`, mirrored in `iip-data-processing-content.ts`); change either and everyone is asked again. The checklist gains "profile" and "terms" after sign-up when the service is registered. New page `/app/admin/setup` (Admin tile "Set up your organisation") with the checklist and both forms. New public page `/data-processing` with a plain data processing agreement written to match what the product does (no certifications claimed, Azure regions outside Uganda stated, WhatsApp not described as end-to-end encrypted). It has NOT been reviewed by counsel: that is on the owner's list. Kato's prompt now includes the organisation's own "about you" and licence when it exists.
- Verification: full suite green, lint 0 errors, tsc clean; setup.spec.ts passes at 1280 and 768 (invalid licence refused, saved profile survives a reload, terms need the tick, legal pages are public).

## Piece status

| # | Piece | State |
|---|---|---|
| 1 | Per-workspace brand and scenario settings | done (backend, API, tests; screen with piece 2) |
| 2 | Demo scenarios and the switch | done |
| 3 | Template catalogue | done |
| 4 | Events (store, form, file import) | done |
| 5 | Rules engine | done (follow-up and STOP in piece 7) |
| 6 | Reason carried into the conversation | done |
| 7 | Outcomes and hand-off, STOP | done |
| 8 | Bank first-touch flow | done (Eugene in piece 11) |
| 9 | Renewal and premium flows | done |
| 10 | Document chasing and claim updates | done (verify with the full run, see note) |
| 11 | Demo customer and timeline screens | done (no fast-forward) |
| 12 | Conversion view for bank staff | done |
| 13 | Campaign reporting | done |
| 14 | Onboarding: company profile, terms, licence | done |
| 15 | Onboarding: security screen, storage step | next |
| 16 | Extraction agent for wordings | |
| 17 | "Here is what I found" confirm screen | |
| 18 | Managed WhatsApp number flow | |
| 19 | Claim by WhatsApp polish and identity check | |
| 20 | Security pack | |
| 21 | Rehearsal | |

## Verified so far

- Pieces 8 and 9: Api.Tests all pass (RuleEngineTests 46, Kato 119), tsc clean, lint 0 errors. All insurer-demo Playwright specs (demonstrations, templates, bank-events, rules, renewals) pass at 1280 and 768 together: 28 tests. Renewals spec drives Nile Life from policy dates to logged reminders in a browser. Not run at 390 px.
- Piece 7: Api.Tests all pass (RuleEngineTests 40, Kato 118 incl. STOP honoured with the model never called), tsc clean, lint 0 errors, rules.spec.ts 6 pass at 1280 and 768 (replies section shows its empty state). A reply flowing through a real webhook was not driven in a browser.
- Piece 6: Kato tests 116 passed (3 new: reason present, reason too old or other number ignored, bank commission line). Prompt content is asserted against a scripted model; no real model call was made.
- Piece 5: Api.Tests 1638 passed (21 RuleEngineTests incl. the Eugene case, consent, cover held, limits, hours, daily limit, approval roles, tamper, demo vs real, isolation), Domain.Tests 731, tsc clean, lint 0 errors. Playwright rules.spec.ts 3 tests pass at 1280 and 768 (write, approve, run, read the log with its checks; edit clears approval; empty cover refused); desktop screenshot reviewed.
- Piece 4: Api.Tests 1617 passed (17 BankEventTests, 5 connector event-folder tests), Domain.Tests 731, tsc clean, lint 0 errors. Playwright bank-events.spec.ts 4 tests pass at 1280 and 768; tablet screenshot reviewed. Not run at 390 px.
- Piece 3: Api.Tests 1595 passed (20 new in TemplateCatalogTests), Domain.Tests 731, tsc clean. Playwright templates.spec.ts 4 tests pass at 1280 and 768; screenshot of the draft drawer reviewed. Not run at 390 px. The send path is proven only against a recording fake provider; no real WhatsApp call was made.
- Piece 2: Domain.Tests 731 passed (9 new DemoScenarioTests), Api.Tests 1575 passed, tsc clean, lint warnings only (pre-existing img warnings). Live check against the offline API on :5167: scenario list and activate work; the bank organisation holds 39 prospects and the default tenant 0; second activate adds nothing. Playwright workspace spec demonstrations.spec.ts passes at 1280 and 768 (3 tests each); screenshots reviewed (Demonstrations page, Nile Life retention with 3, 7, 14, 21, 30 day policies). Not run at 390 px. Known gap: the sidebar workspace chip still shows the workspace id ("main"), not the organisation name.
- Piece 1: dotnet build clean; full Api.Tests 1575 passed (incl. 12 new in WorkspaceBrandTests: workspace isolation, concurrent brands, line focus); Domain.Tests 722 passed. Migration AddWorkspaceRecords generated, not applied to any database.

## Deploy checklist (for when the owner says deploy)

- Build API and portal from this branch, run `db-migrate`, check `/health/ready`, confirm the deploy guard passes (no demo banner).
- Owner actions: submit templates to Meta, Microsoft and Google app registrations, counsel review of the security pack.

## Questions for the owner (asked at the very end)

- None yet.
