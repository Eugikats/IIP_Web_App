# Customer acquisition: gap analysis, CIP (banking) against IIP (insurance)

Written 2026-10-06. Scope of this pass: the customer acquisition side only, meaning how a stranger who
messages us becomes a profiled lead, how staff see and work that lead, and how the lead becomes a
customer. Other areas (claims, retention, integrations outside acquisition) are not covered here.

## How this was done

Both code bases were read, not their documents. CIP: `CIP_Web_App/apps/credit-api` (the acquisition
contracts, controller, lead assessment, projection, conversation memory, WhatsApp agent pipeline) and
`apps/web` (the acquisition dashboard, lead detail, channel intelligence and WhatsApp inbox
components). IIP: the portal screens under `web/portal/lovable-pages`, the Application and Domain
code for leads and the Kato assistant, and the endpoint list in `Program.cs`. Every gap below names
the evidence. A claim marked "not verified" was not confirmed in code.

Two honest notes about CIP, so nothing is copied blindly:

- CIP's predictive models are `RunDemo` only (`AcquisitionPredictiveModelService`). They are not
  production models. They are not a gap worth closing.
- CIP's lead scoring (`AcquisitionLeadAssessmentService`) reads keywords and regular expressions
  over the joined text of all messages, and the file's own comment calls it legacy. Its structure
  (six scored dimensions with explanations and bands) is worth taking. Its method is not.

## 1. What CIP does (the reference)

| Stage | What exists in CIP |
|---|---|
| Capture | WhatsApp, a website chat widget (`WebChatIngestionService`, with site keys, origin allowlist, rate limits), an email relay (`InboxEmailRelay`), plus channel options for mobile app, branch, field agent, USSD and partners. |
| Understand | A rule extractor (`WhatsAppLoanIntakeExtractor`) for amount, purpose, product, business type and name, location, revenue, years trading, collateral and "I am already a customer". About 45 qualification fields across banking domains. A per-product conversation plan and a discovery engine that picks the next question. |
| Remember | A structured memory snapshot per conversation, saved as JSON (`ConversationMemorySummarizer`), and a journey stage per customer: NotStarted, GreetingExchanged, ProductDiscussed, IntakeInProgress, DocumentsPending, AssessmentComplete, Converted. |
| Profile | A person record (`AcquisitionCustomer`) separate from the opportunity. Business profiles. Relationship status (existing, new to bank, unknown), KYC status, identity match status and confidence, data permission level, relationship manager, branch. |
| Score | 0 to 100 from six dimensions (intent 25, profile completeness 20, affordability evidence 20, business maturity 15, document readiness 10, engagement 10). Bands: cold, early interest, warm, qualified, high priority. Explanations, confidence, positive and limiting factors, missing information. Recalculation on demand and by a projection worker with replay. |
| Decide next step | Next best action (continue qualification, request documents, book a relationship manager, create lead or opportunity, escalate, recommend product, end, resume, re-engage). Product recommendations with fit score, eligibility signals, missing information and a disclaimer. Lead and opportunity recommendation with probability and estimated value. Customer-safe guidance text composed from all of the above. |
| Work the lead | Assignment to a named user, notes, status history, tickets (escalations) with type, priority, team, due date and status, workflow actions created from a recommendation (skipping duplicates), document requirements per lead with request, upload, extraction and review, conversion eligibility and conversion to a borrower and loan request. |
| See it | An acquisition dashboard with five tabs (Customers, Escalations, Scores, Agents, Channel Intelligence), KPI cards with trend lines from a real trends endpoint, a filter toolbar with active chips, paged tables, CSV export, a lead detail page with sections (Customer, Product, Pre-qual, Documents, Notes, Activity), a score review page, a ticket page, and a floating AI assistant scoped to leads. |
| Watch the channels | Channel Intelligence: total interactions, new leads captured, qualified leads, conversion rate, cost per qualified lead, average response time, channel mix, rankings, live activity feed, AI insights, agent performance. |
| Staff inbox | A three-pane WhatsApp inbox. The right pane is the lead context (product interest, amount, business type, trading duration, KYC, account opening, assigned officer, score band, priority, confidence, notes, positive, risk and missing factors) and a contact journey. Live alerts over SignalR. Per-conversation auto-reply flag and an auto-reply monitor. |
| Stay safe | Identity and consent before pre-scoring (NIN validation, phone validation, OTP gate, consent text and record). A guard that refuses to store PINs, passwords, OTPs and card numbers. Input shield, reply output guard, funnel response guards. Idle nudges at 15 minutes and 23 hours inside quiet hours (08:00 to 20:00 Africa/Kampala). Opt-out detection. |
| Govern the agent | Per-turn decision log and trace, evaluation scorecards, analytics, prompt governance, AI operations SLA pages. |

## 2. What IIP has today (verified)

- One screen: `CustomerAcquisition.tsx`, "Leads Inbox". A manual capture form (six fields), and a
  list of lead cards. Opening a card shows four text blocks: what the customer needs, a summary,
  what is still needed, and the contact.
- A lead is one flat record, `SalesLeadDetailReadModel`, with 15 fields. Three endpoints exist:
  list, detail, create (`Program.cs` lines 899 to 904). There is no update, assign, status change,
  note, archive or delete endpoint.
- Leads are created by the WhatsApp assistant in seven places (`CustomerAssistantService`), and
  announced to the team on Teams. Nothing owns them after that.
- The Kato assistant (tools: account lookup, product list, policy search, register, apply for
  cover, file claim, claim link, hand to person) captures a name, an occupation and an area when it
  registers someone. They are written into one sentence of the lead's free-text summary. The
  customer record itself holds six fields (id, tenant, workspace, name, email, phone).
- The staff inbox (`Channels.tsx`) is two panes (list and thread) with channel, intent, language,
  "Unmatched customer" and approval badges.
- There are no acquisition numbers anywhere. `ConversionProbability` is set to `null` at every
  call site.

## 3. The gap register

Legend: **Verified** means confirmed in code in both systems. IDs are stable so a build prompt can
cite them.

### A. Data model

| ID | Gap | CIP | IIP (evidence) |
|---|---|---|---|
| A1 | No person-level prospect record separate from the lead | `AcquisitionCustomer`, `LeadOpportunity`, `BusinessProfile`, `LeadTicket` | One flat lead record, 15 fields (`SalesLeadQueryContracts.cs`) |
| A2 | A person who asks twice becomes two leads | Projection is idempotent and replayable | Lead id is random (`LEAD-date-random`), the store upserts on that id, and seven call sites create leads, so there is no merge |
| A3 | The customer record cannot hold a profile | Segment, maturity, product interest, relationship manager, lifetime value band, preferred channel | Six fields. Occupation and area are asked by Kato and only survive as words inside a lead summary string |
| A4 | A prospect cannot be told apart from a policyholder | `IsProspect`, `BankRelationshipStatus`, `RequiresAccountOpening` | Kato's "registered" contact is an ordinary `Customer` row with no policies |
| A5 | Nothing records how a lead changed | Status history, assignments, notes tables | None. A lead never changes after creation |
| A6 | No lead stage vocabulary | new, inquiry, nurturing, qualified, handoff ready, opted out; statuses; readiness bands | The only stage is `QuoteStatus = "New"`. The field name also implies quoting, which our guardrail forbids (see decision D2) |
| A7 | Lead source is free text | Controlled channel list | `leadSource` is typed text ("Manual capture", "WhatsApp - Kato registration") |
| A8 | No campaign or referral attribution | Channel attribution view | None. No click-to-WhatsApp ad referral, QR code, agent code or UTM |

### B. Building a profile from the conversation (the main ask)

| ID | Gap | CIP | IIP |
|---|---|---|---|
| B1 | No structured extraction of facts | Extractor plus qualification fields | Kato's model decides what to ask. Only name, occupation, area reach a tool call, and they are not stored as fields |
| B2 | No persisted conversation memory | Memory snapshot JSON per conversation, summarizer, state service | `CustomerConversationTurn` rows only. Each turn re-reads history. No "what we know, what is missing" state |
| B3 | No journey stage per person | Seven stages computed from CRM state | None |
| B4 | The "still needed" list is not real | Derived from missing fields and documents | Hardcoded three-item list on every lead (`Confirm contact details`, `Confirm insurable interest`, `Capture risk details for quote`) |
| B5 | No segment or relationship maturity to steer tone and next step | `CustomerSegment`, `RelationshipMaturity` | `SenderProfile` is a name and a list of policies |
| B6 | Staff cannot see or correct the profile | Lead context panel, editable display name, notes | No profile exists to show |
| B7 | No guard against storing sensitive input a customer pastes in | Unsafe input pattern (PIN, password, OTP, card) blocks storage | None found in the assistant code. A customer who pastes a card number or OTP has it kept in the conversation record |
| B8 | One person across several channels is not linked | Phone hash linked to the acquisition customer, multi-number service | Phone match only. An email enquiry and a WhatsApp enquiry from one person are two people |
| B9 | No record of what the profile is based on | Source confidence on business profiles, provenance for prescore facts | None. Nothing says whether a fact was stated by the customer, inferred, or confirmed by staff |

### C. Scoring and readiness

| ID | Gap | CIP | IIP |
|---|---|---|---|
| C1 | No lead score | Six-dimension 0 to 100 with bands | `ConversionProbability` is always `null` |
| C2 | No explanation of why a lead ranks where it does | Score explanation, confidence, positive factors, limiting factors, missing information | None |
| C3 | No score history or recalculation | Assessments table, recalculate endpoint, projection worker and replay | None |
| C4 | No trend of lead quality over time | KPI trends endpoint | None |

Do not port: the predictive demo models, and the keyword-over-joined-text scoring method. Port the
dimension structure, driven by the structured fields from B1.

### D. Qualification and next best action

| ID | Gap | CIP | IIP |
|---|---|---|---|
| D1 | No per-cover question plan | Conversation plan and product qualification profile | Kato's prompt decides what to ask |
| D2 | No next best action | Ten action types with reasons | Two hardcoded follow-ups on every lead |
| D3 | No opportunity signals (for example a customer with Motor asking about a shop) | `OpportunitySignal` | None |
| D4 | No stored product fit | `ProductRecommendationDto` with fit score, eligibility signals, missing information | Kato recommends conversationally and nothing is stored. See decision D1 |
| D5 | No briefing for the person who picks the lead up | Officer recommendation context | Lead detail shows four text blocks |
| D6 | The follow-up text says "Prepare indicative quote for human review" | Not applicable (bank) | Written into every lead. Conflicts with the rule that IIP never quotes or prices (CLAUDE.md, rule 13). Needs rewording |

### E. Working a lead (life cycle)

| ID | Gap | CIP | IIP |
|---|---|---|---|
| E1 | No way to change a lead | Assign, note, status, recalculate, document actions | Create and read only |
| E2 | Assignment is typed by hand | Assignable users list and an assign endpoint | "Assign to officer" is a free-text box. No user lookup, no notification to the person |
| E3 | No notes | Add and delete notes | None |
| E4 | No activity timeline | Lead activity events per customer | None |
| E5 | No owned follow-up or escalation | Tickets with type, priority, team, due date and status | A one-off Teams broadcast on creation. No task, no due date, no way to see a lead nobody has picked up |
| E6 | No response-time or follow-up SLA for leads | Ticket due dates, SLA reads | None. Claims have service standards; leads have none |
| E7 | Leads do not surface in the Brief or My Work | Not applicable (different shell) | `BriefService` and My Work do not read leads (verified by search) |
| E8 | No conversion workflow | Eligibility check and convert endpoint | A real-number application stays a lead with the text "Applied on WhatsApp". No review screen, no outcome recorded |
| E9 | No outcome capture (won, lost, why) | Conversion status, converted borrower and loan request | None. IIP does not issue policies, so the outcome has to come back from the licensed person |
| E10 | No bulk work, import or archive | Demo pack loader, soft-archive stale and all | None |

### F. The staff dashboard

| ID | Gap | CIP | IIP |
|---|---|---|---|
| F1 | No KPI strip | Total, existing, new to bank, account opening needed, KYC split, qualified, high priority, pending, declined, average score, each with a trend | None |
| F2 | No filters | Channel, relationship, KYC, readiness, score band, product, officer, ticket status, search, with chips | One unfiltered list |
| F3 | No paging or sorting | Paged tables, newest-first toggle | Every lead is rendered |
| F4 | No separate views | Customers, Escalations, Scores, Agents, Channels | One list |
| F5 | No export | Leads and tickets as CSV | None for leads |
| F6 | No lead detail page | Sections: Customer, Product, Pre-qual, Documents, Notes, Activity, plus score review and ticket pages | Inline accordion with four text blocks |
| F7 | No row actions | Follow up, assign, change status | None |
| F8 | No pipeline or funnel view | Stage counts, channel mix | None |
| F9 | No assistant for leads | Floating AI assistant scoped to leads, with history | The global assistant is not lead-aware (verified: no lead tools in Command Center) |
| F10 | No staff-drafted follow-up to a lead | Workflow action from recommendation | Draft-a-message exists for claims only |

### G. The inbox as a sales tool

| ID | Gap | CIP | IIP |
|---|---|---|---|
| G1 | No customer or lead context beside the conversation | Lead context panel, contact journey panel, contact drawer | Two panes. No third |
| G2 | **A person cannot reply to a prospect from the portal.** Kato tells the customer "someone will reply here shortly" | Officer composer in the inbox | The reply form says "Link this thread to an authorized case before replying", and the send service is case-linked. A lead has no case, so there is nothing to link |
| G3 | No takeover and handback | Per-conversation auto-reply flag, auto-reply monitor page | Kato's kill switch is workspace-wide, not per conversation |
| G4 | The badge says "Unmatched, link to customer" but nothing links | Customer match tools | No control found that links a thread to a customer |
| G5 | No live updates or unread alerts | SignalR notifier, live alerts | Not verified; no push channel found |
| G6 | No conversation filters by intent, score or unmatched | Filter bar | Search only |

### H. Channel analytics

| ID | Gap | CIP | IIP |
|---|---|---|---|
| H1 | No channel numbers | Interactions, new leads, qualified leads, conversion rate, cost per qualified lead, response time | Nothing (verified by search) |
| H2 | No channel mix, ranking or live activity feed | Dedicated pages | None |
| H3 | No measure of the agent itself | Agent performance panel | None: no hand-off rate, containment rate or time to first reply |
| H4 | No AI insights per channel | Insights page | None |

### I. Documents and identity for prospects

| ID | Gap | CIP | IIP |
|---|---|---|---|
| I1 | No documents tied to a lead | Per-lead requirements, request, upload, extraction, review queue | Documents attach to claims only |
| I2 | No identity verification for a new customer | NIN validation, phone validation, OTP, name matching | Identity is "the number matches". A new registrant is whoever types a name |
| I3 | No consent to profile | Prescore consent text and record | Consent is recorded as "customer started the conversation". Nothing captures agreement to build a profile or to be contacted about cover |

### J. Staying in touch, and staying safe

| ID | Gap | CIP | IIP |
|---|---|---|---|
| J1 | No follow-up when a prospect goes quiet | Nudges at 15 minutes and 23 hours | None. A prospect who stops replying is never re-engaged |
| J2 | No quiet hours | Configurable window, default 08:00 to 20:00 Kampala | None |
| J3 | **STOP does nothing** | Opt-out detection ends outreach | "stop" appears in IIP only as a word that is not a name. Consent withdrawal exists only as a staff API |
| J4 | No "welcome back" for a returning prospect | Resume opportunity action | None |
| J5 | No input shield or output guard layers on the sales agent | `InputShield`, `ReplyOutputGuard`, `FunnelResponseGuards` | None found in the assistant code. The auto-reply gate covers grounded answers, not the agent's free text |

### K. Channels in

| ID | Gap | CIP | IIP |
|---|---|---|---|
| K1 | No website chat | Widget with site key, origin allowlist, rate limit, own session token | No ingestion service exists. "Web" is only a label |
| K2 | No email to lead | Inbox email relay feeds acquisition | IIP's mail ingestion feeds claims. No lead path (verified by search) |
| K3 | No inbound SMS lead capture | SMS service in the funnel | Not verified |
| K4 | No agent, broker, branch or USSD source | Listed as channel options | Free-text source only |
| K5 | One WhatsApp number per workspace | Multi-number service | One connection per tenant |

### L. Governing the agent

| ID | Gap | CIP | IIP |
|---|---|---|---|
| L1 | No per-turn trace of why the agent said what it said | Decision log, turn trace | Conversation turns and audit events only |
| L2 | No quality scoring of agent replies | Evaluation results, scorecards | None |
| L3 | No place for staff to watch the agent live | Auto-reply monitor | None |
| L4 | Agent tuning is thinner | Prompt governance, knowledge ops | Versioned agent behaviour profiles exist (partial) |

### M. UI and UX

| ID | Gap |
|---|---|
| M1 | IIP has the table, filter tab, status pill, side drawer and state surfaces. It does not have: a KPI tile with a real trend line, a filter bar with active chips and URL sync, paging, a detail page shell with section tabs, a context-panel pattern, or a lead-aware assistant entry. |
| M2 | The empty state says "Connected channel and import sources appear only after configuration". It gives a new officer no first step. |
| M3 | Status in CIP is a coloured pill. IIP's rule is a neutral pill, a 6px dot and a word. The readiness bands need that treatment. |
| M4 | **Take CIP's components and density, not effects.** Compared against screenshots of the running CIP (2026-10-06), its look is a calm light canvas, white cards with hairline borders, large radius, a collapsed 80px icon rail, KPI tiles with trend lines, segmented tabs, a filter bar with a count badge and chips, uppercase table headers, initials avatars with status dots, icon tiles, a three-pane inbox with a customer panel, and a settings modal with an integration catalog. There is no real gradient or glass in those screens (only a faint lavender fade at the foot of two pages and a blurred modal backdrop). An earlier version of this row said otherwise, inferred from gradient classes in CIP's older components. Its richness comes from components, not effects. Where it differs from IIP's rules (blue accent, tinted status pills with coloured text, green/amber/red chart bars, a serif greeting with a coloured name) keep IIP's rule unless you decide otherwise. |
| M5 | **Copy CIP's honest empty trend.** Its KPI tiles show a dashed "No trend" box when there is no series, which fits IIP rule 8 (no fabricated data). Do not copy the unused `sparklineFromDisplayValue` helper in its code, which would invent a trend from one number. |

## 4. What IIP has that CIP does not (keep it, do not trade it away)

I found no equivalent in CIP for these:

- Voice notes in and voice replies out (Sunbird speech to text and text to speech).
- Luganda and Kiswahili understanding and replies, with honest fallback to English and per-language
  handling. CIP detects language; I found no translation of replies.
- Claims collected in conversation, with a read-back the customer must confirm, and papers only
  through a single-use secure link.
- Identification by the WhatsApp number, checked twice (in the assistant and again when a link is
  issued).
- Consent as a first-class record, and an audit trail that stores the language and outcome but never
  the words.
- A microinsurance catalogue written for the customer (hospital cash and funeral, boda rider
  accident, market stall stock, small shop cover, goods in transit, Mama Care), each with a plain
  summary and "how to join, pay and claim".
- The honest answer to "are you a person?".
- A background queue so Meta is answered at once, and database retry on a failed login.

## 5. Decisions only you can make

**D1. Product recommendation against the IRA licence.** `IIP_HACKATHON_READINESS_BLUEPRINT.md`
section 8.2 and CLAUDE.md rule 13 say IIP does not recommend a product to a customer, because that
is intermediation and Otic holds no licence. Kato already explains and suggests covers
conversationally (commit `df620ed`, "sell like a helpful salesperson"), and on real numbers it only
records an application for a licensed person. CIP's recommendation set (fit score, eligibility
signals) would deepen that. Before building D4, decide one of: (a) keep IIP strictly informational
and route to a licensed person, and drop recommendation scoring; (b) record a recommendation only as
"prepared for the licensed intermediary", never shown to the customer as advice; (c) confirm that
the insurer client holds the licence and the recommendation is theirs. I recommend (b) until you tell
me otherwise. The blueprint's honesty register should be updated to match whatever you choose,
because today it and Kato disagree.

**D2. What the score means.** CIP's score is a credit pre-score. For IIP it must be a follow-up
priority, advisory, deterministic, and explained, never an underwriting or pricing signal, and never
used to withhold information from a customer. Confirm that reading before it is built, and whether
the officer sees the dimensions or only the band.

**D3. Profiling consent.** Capturing occupation, area, income band and business details to build a
profile is processing under the Data Protection and Privacy Act. It needs a plain notice, an agreed
retention period (the retention policy table already exists) and a way to withdraw (J3). Confirm the
wording you want Kato to use, or that the insurer supplies it.

**D4. Where a real application goes.** IIP does not issue policies. Decide where a "licensed
person" works: inside IIP (a review screen and an outcome form, E8 and E9) or in the insurer's own
system, with IIP only recording the hand-off and the result.

## 6. Build order (full scope, in dependency order)

1. **Lead and person model** (A1 to A8): prospect person record, one lead per person per cover
   interest, stage and status vocabulary without quote language, controlled sources, history tables.
   Everything else reads this.
2. **Profile from conversation** (B1 to B9, J5, I3 notice): Kato's tool calls write structured
   fields with their provenance; a persisted memory snapshot and journey stage; an input guard for
   sensitive data; the "still needed" list derived from missing fields.
3. **Score** (C1 to C4): deterministic dimensions over the structured fields, with explanations and
   a trend, in Domain with tests.
4. **Work management** (E1 to E7, D2, D5, D6): assign to a real user, notes, status, follow-up
   tasks with due dates and a lead response standard, leads in My Work and the Brief.
5. **Inbox as a sales tool** (G1 to G6): context panel, person reply without a claim, takeover and
   handback, link-to-customer control, filters.
6. **Dashboard** (F1 to F10, H1 to H4, M1 to M5): KPI strip with real trends, filters with chips,
   paging, tabs, detail page, export, channel metrics, agent performance, lead-aware assistant, all
   in IIP's design language, opened in a browser at 1280px and 768px.
7. **Staying in touch** (J1 to J4): idle nudges inside quiet hours, STOP handling, welcome back.
8. **New channels in** (K1 to K5, A8): website chat, email to lead, sources and attribution.
9. **Conversion and outcome** (E8 to E10, I1, I2): per D4.
10. **Governing the agent** (L1 to L4): turn trace, quality scoring, a live monitor.

Each step ends with the verification the repo already requires: build, tests, portal lint and build,
the screen opened in a browser, and a live check on the Azure dev environment.
