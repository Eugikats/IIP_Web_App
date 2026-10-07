# IIP bancassurance acquisition build prompt

One continuous build pass, written to be run with `/loop` (no interval, so the agent self-paces).
Paste the whole of section 2 onwards as the loop prompt, or point the loop at this file.

Written 2026-10-06 for the bancassurance presentation, reordered 2026-10-07 by the owner: the
fundamentals are built first and the interface last. Whatever is finished at any moment is a working
slice of the acquisition path. Nothing here is a reduced version of anything else.

---

## 1. How to run this

- Run with `/loop` and no interval.
- Work on a new branch, `feature/bancassurance-acquisition`, cut from `main`. Do not touch
  `agenticIip-saas`; finished work reaches it through git later.
- Do not stop to ask between outcomes. Stop only for a genuine blocker: a missing credential, an
  external approval, or a decision only the owner can make. Name it and carry on with the next
  outcome that does not depend on it.
- Deploy to the Azure dev environment (`rg-iip-dev`) after each outcome that changes behaviour, using
  the procedure in `docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md`.
  The API stalls for several minutes after a restart while SQL connections recover, so deploy early
  and warm it before anyone looks. The owner sets a deploy freeze time before the presentation;
  after it, only verify.
- The WhatsApp connection already works on dev. Do not reconfigure it.
- Follow the order in section 5.0. Do not start Stage 2 before Stage 1 is verified live on dev, and do not
  polish a screen during Stage 1 beyond making it work.

---

## 2. Mission

A bank that sells insurance to its own customers (bancassurance) holds lists of people who already use
its products. IIP turns such a list into a worked pipeline:

1. The list is imported (a file now, SharePoint, OneDrive or Google Drive next).
2. A triage agent scores every person and names the two products most likely to suit them and why,
   citing the product documents already uploaded.
3. A composer agent drafts a personal message for each person from an approved template and appends
   it to that person's row.
4. A named person approves the campaign. Messages go out on WhatsApp (templates) and email, in bulk
   or one person at a time from their profile.
5. Replies continue on WhatsApp with Kato, who qualifies the person and updates the same record.
6. A follow-up agent finds prospects who went quiet and policies about to end, drafts reminders for
   approval, and flags them on the acquisition screen.

The same pipeline also serves a stranger who texts IIP on WhatsApp: they appear as a scored prospect
as they talk.

The acquisition dashboard and a Settings modal take on the look of the CIP platform shown in the
owner's screenshots (section 4, rule 10).

## 3. Read first, and follow

`AGENTS.md`, `CLAUDE.md`, `DESIGN.md` and the four documents it indexes (amended by outcome E),
`docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md` section 8,
`docs/01-strategy-requirements-roadmap/IIP_ACQUISITION_GAP_ANALYSIS_VS_CIP.md` (the gap register this
build closes, cited below by id), and `IIP_NEXT_BUILD_PROMPT.md` for the working conventions.

CIP reference code, read for structure and never copied wholesale (it is .NET 9 and uses its own
styling): `Projects/CIP_Web_App/apps/credit-api/Services/Acquisition*`,
`Services/WhatsAppBankingAgent/ConversationMemory`, `apps/web/components/dashboard/v2/acquisition`,
`apps/web/components/dashboard/v2/settings`, `apps/web/components/marketing/motion.tsx`.

WhatsApp workflow reference: `Projects/Ziora/wacrm` (MIT licensed, Next.js and Supabase). Read it for the
inbox (`src/components/inbox`), the template lifecycle (`src/lib/whatsapp/template-*.ts`,
`meta-api.ts`), broadcasts (`broadcast-core.ts`), the AI handoff note (`src/lib/ai/handoff.ts`),
assignment notifications and presence. It is TypeScript, so port the logic and its test cases to .NET
and keep its licence notice wherever code or tests are adapted. Never open or copy its `.env*` files.

## 4. Non-negotiables, in every change

1. AI prepares, people decide. Scores, product fits and message drafts are prepared. A named person
   approves a campaign before any marketing message leaves.
2. Scores and fits come from deterministic rules over real fields, in Domain, with tests. The model
   only writes the sentence that explains a result. Sensitive attributes (religion, ethnicity,
   health, disability, political views) are never fields, never scoring inputs, and never reach a
   prompt. A score orders follow-up. It never withholds information from a customer and is never an
   underwriting or pricing signal.
3. Every actionable AI assertion carries a citation that opens its source. A fit with no citation to
   an approved product document is not shown.
4. Marketing messages: only to a person with a recorded marketing consent, only after the campaign is
   approved, only inside the workspace's sending hours, never to a person who replied STOP.
   WhatsApp outside the 24-hour customer window goes only through an approved template.
5. A demo workspace sends only to numbers and addresses marked as demo contacts. Every other
   recipient shows "Not sent, demo workspace". Never "Sent".
6. The imported list is personal data of the bank's customers. Keep only the columns needed, mask
   phone numbers on screen (for example `25***32`), apply the retention table, and cover prospects in
   data export and erasure.
7. No emoji. No em dashes anywhere. No fabricated data: a trend renders only from a real series,
   otherwise a dashed "No trend". Six states on every surface. Absent capability renders
   "Not configured", never a fake success. People are shown by name, never by id.
8. Schema changes through `dotnet ef migrations add`, never hand-written.
9. Business rules belong to the insurer: product target profiles, scoring weights, template wording,
   sending hours and the follow-up periods are tenant settings with defaults, versioned and audited.
10. **Design (owner decisions 2026-10-06):** IIP is CIP's sister product and is to look like it. CIP
    has no written design language, so the reference is its running v2 interface (the owner's
    screenshots and `apps/web`), and outcome O turns that into one shared written design authority.
    The look is applied in Stage 2, and until outcome O lands it reaches the acquisition dashboard, the
    Settings modal and the inbox through a scoped theme on those areas only, with `DESIGN.md` and the guardrails amended for
    that scope in the same change (including the automated gradient and pill checks in
    `web/portal/scripts/check-ui-release-gates.mjs`) so the gates pass. Everywhere else the existing
    design language stands until outcome O promotes the theme. Motion respects the reduced-motion
    setting. Contrast stays at WCAG AA: any CIP colour that fails is adjusted minimally and recorded.
11. Smallest coherent forward change. No competing architecture. Reuse the policy import parser, the
    claim triage queue pattern, the retention watch, the consent record, the connection model and
    the agent registry.

---

## 5. Outcomes and acceptance criteria

### 5.0 Build order

The owner's rule: the fundamentals work first, the interface is the last thing done. Stage 1 builds the
agents, the data, the rules and the sending, and gives each one the plainest working screen in the
current design so it can be operated and checked. Stage 2 gives the screens their final form.

**Stage 1, fundamentals** (current design, plain working screens)

1. A. The prospect model.
2. Q. Kato qualifies a lead (his prompt, tools and consent capture).
3. P. Conversation insights: summaries of each lead that are worth reading.
4. I (takeover and reply). A person can step into a conversation and reply, without a claim.
5. B. List import.
6. C. Product target profiles.
7. D. Triage agent: score each person, name the best products and why.
8. F. Message templates.
9. G and H. Campaign composer, approval, editing, bulk send and single send.
10. J. Follow-up agent.

**Stage 2, screens** (the CIP look; the chat screen is rebuilt here)

11. E. The acquisition dashboard.
12. I (screens). The three-pane inbox and chat, with the insights panel.
13. K. The Settings modal.
14. L. File connectors.

**Stage 3, close-out**

15. N. The rehearsed demo. 16. M. Documentation and honesty. 17. O. One design authority.


### A. The prospect model

Closes gaps A1 to A6, B2, B3, B9.

- Domain, Application, EF migration: `ProspectList` (an import batch), `Prospect` (a person: source
  list, WhatsApp or manual; name, phone, email, age band, occupation, location, tenure, products held
  at the bank, balance and income or turnover bands, marketing consent status and its source),
  `ProspectFit` (product, score, reasons, citation), `ProspectActivity` (timeline), notes, assigned
  officer (a real user).
- One prospect per person: duplicates by normalised phone merge, never multiply.
- Every write audited. Tenant scoping on every record. Provenance on every fact: stated by the
  customer, imported from a list, inferred, or confirmed by staff.
- Accepted when: tests cover merge by phone, tenant isolation, provenance, and audit; the migration
  was generated by the tool.

### B. List import

- Upload CSV or XLSX through the existing policy import parser pattern: read file, suggest a column
  mapping, show a validation report, commit. A marketing-consent column is mapped like any other; a
  row without one imports as "No consent recorded" and cannot be messaged.
- A Demo-only seed route builds a realistic synthetic bank customer list of about 40 people,
  including the owner's own number (+256776678989) marked as a demo contact, and a second demo
  contact the owner supplies.
- Accepted when: the demo list imports; bad phone numbers are rejected with the row and reason;
  re-importing updates instead of duplicating; the import history shows who, when, counts.

### C. Product target profiles

- Extend product configuration with an insurer-owned target profile per product (age range,
  occupation tags, income or turnover band, bank-product signals, life-stage signals), versioned,
  published like any other product change. Seed profiles for the demonstration products from their
  uploaded documents.
- Accepted when: an admin edits a profile in the products screen; the change is versioned and
  audited; the fit rules read it.

### D. Triage agent

Closes C1 to C3, D1 to D5 for lists.

- A new `prospect-triage` agent in the registry (with the kill switch), run from a background queue
  the way `ClaimTriageQueue` runs the claims agent.
- Deterministic dimensions in Domain: relationship strength, capacity, need signals, product fit,
  contactability. A 0 to 100 score, a readiness band (cold, early, warm, qualified, high priority),
  explanations, confidence, missing information, and the top two products with the reasons.
- The model writes one grounded sentence per fit ("why this person suits this product") from the
  facts and retrieved product documents, and the sentence carries its citation. No citation, no
  render.
- Accepted when: rule tests cover each dimension and band; a rerun is idempotent; the sentence is
  refused without a citation; progress shows on screen while a list is being triaged.

### Q. Kato qualifies a lead

Closes B1, B4 to B6 and the profile half of the conversation.

- **Prompt.** Keep the platform preamble. Change Kato's prompt in `CustomerAssistantService.Agent.cs`
  so that discovery has a goal: for the cover a person leans toward, learn work, area, a rough income
  or turnover band, who depends on them, what cover they already hold, what worries them, what they
  could pay monthly, and when they would want cover, one or two questions at a time and never as a
  form. He never states a score or says a person "qualifies". He recommends the cover that fits and says
  why, himself (owner decision 2026-10-07: no "let me check with the team"; a person is brought in
  only when he truly cannot help). He never asks for or infers religion, ethnicity, health, disability or political views.
- **Tools.** Add `record_profile` (a field, the customer's own words or value, and the message it came
  from), `set_interest` (a cover, how strong, why) and `ask_follow_up_consent` (asks once, plainly, and
  records the answer with the wording shown). The tool refuses a field that is not on the allowed list
  and refuses a value the customer never said.
- **Consent in the conversation.** Agreement to be contacted about cover is recorded with its wording,
  time and message, as a marketing consent separate from the consent to answer a customer who wrote
  first.
- **Rules, not the model, decide the stage.** The lead stage (new, discovering, interested,
  considering, ready, handed to a person, not interested) is derived in Domain from the recorded facts
  and interest, with tests. The score and top fits come from outcome D's rules over the same facts.
- **Takeover awareness.** When a person has taken over a conversation, Kato stays silent but the
  messages are still recorded and still feed the insights.
- Accepted when: tests cover each tool's refusals, the stage derivation, the consent capture and
  silence under takeover; a scripted conversation (English and Luganda through translation) ends with a
  prospect holding the stated facts, a stage, a score and a fit; a live conversation from the demo phone
  on dev does the same.

### P. Conversation insights

Closes the summaries the owner asked for.

- A new `conversation-insights` agent in the registry (with the kill switch), run from the same
  background queue as the WhatsApp assistant. It runs after a quiet moment following a customer
  message, after a set number of new messages, and on a person's "Refresh". It reads the previous
  snapshot plus the new messages, not the whole thread, so cost stays bounded, and it counts against
  the tenant's usage limits.
- Output, as validated structured data: a short summary of who this person is and what they want;
  the covers they are interested in and how strongly; the problems and worries they described; their
  doubts and objections; where they are in the journey; their tone; what is still unknown; and one
  suggested next step for the officer, which is only ever a suggestion.
- Evidence: every point carries the tags of the messages it came from, the way the Customer Summary
  Agent tags its sources, and a tag opens that message in the thread. A point with no valid tag is
  dropped. With too little conversation the agent says "Not enough conversation yet" instead of
  inventing a summary. It never characterises a person's honesty and never uses sensitive attributes.
- Snapshots are versioned on the prospect and shown with "Updated 3 minutes ago, from 14 messages".
- Stage 1 shows the latest snapshot at the top of the prospect's panel in the existing inbox. Stage 2
  gives it its final place in the customer panel.
- Accepted when: tests with fixed conversations check that each point's tags exist in the thread, that
  an unsupported point is dropped, that an incremental run equals a full run on the same messages, and
  that a short thread yields the "not enough" state; on dev, a real conversation produces a summary a
  person reads and agrees with, and a click on a tag opens the message.

### E. The acquisition dashboard

Closes F1 to F10, H1 to H4, M1 to M5.

- Stage 2. First amend `DESIGN.md` and the guardrails for the scoped theme (rule 10), including the checks.
- Shared components in Storybook, each with its six states: KPI tile with a real trend line or
  "No trend", filter bar with a count badge and chips, segmented tabs, paged table with uppercase
  headers, initials avatar with status dot, icon tile, modal with blurred backdrop, side drawer,
  and the transitions (tab change, drawer, modal, list entrance).
- Tabs: Prospects, Campaigns, Follow-up, Scores, Agents, Channels. KPI tiles from a real KPI trends
  endpoint (total, new, qualified, high priority, awaiting follow-up, unassigned, average score,
  reply rate, with trends). Filters by channel, band, product, officer, consent, status, date,
  search. Paging, sorting, CSV export.
- Prospect profile drawer: details, score and why, the two product fits with citations, timeline,
  notes, assign to a real user, drafted message, and send.
- A collapsed icon rail by default on this area, expanding to the labelled panel.
- Accepted when: the 27-point checklist (as amended) is cleared, each screen is opened in a browser
  at 1280px and 768px and compared with the owner's screenshots, and every list and tile shows real
  data or an honest empty state.

### F. Message templates

- A template catalogue: name, language, category, body with numbered variables mapped to prospect
  fields, example values, a live preview. Email templates (subject and body) alongside.
- Submit a WhatsApp template to Meta through the WhatsApp Business Account's template endpoint and
  show its status honestly: Draft, Pending, Approved, Rejected with Meta's reason.
- Port the template lifecycle from wacrm: the components builder, the variable validators, the preview,
  create and list against the WhatsApp Business Account, and status changes arriving from Meta's
  template webhook (Pending, Approved, Rejected, Paused, Disabled), with its test cases.
- A template that is not Approved cannot be used to start a conversation. Inside the 24-hour window a
  free-form message is allowed, as the existing messaging service already enforces.
- Accepted when: tests with a fake Meta client cover submit, status change and the use gate; a real
  submission is made on dev and its status is shown.

### G. Campaign composer and sending

Closes the outreach half of the mission.

- A new `campaign-composer` agent: for each chosen prospect it picks the template, fills the
  variables from the prospect's facts, writes the one personal line inside the variable's length
  limit, and appends the finished message to that prospect's row.
- A campaign: audience filter, channel, template, drafted messages, sending hours. A named approver
  sees a preview of sampled messages and approves or rejects. Approval is recorded.
- Sending: bulk to WhatsApp and email, per recipient status updated from delivery events, failures
  shown with a plain reason. Rules 4 and 5 enforced in the send path, not only in the screen.
- One recipient row per person carrying the provider message id, so delivery and read events update
  that row; a retry for the numbers that failed; a view of who failed and why (wacrm's
  `broadcast-core` and its retry view are the model).
- STOP and its common variants (also in Luganda and Kiswahili) withdraw consent at once and end
  outreach.
- Accepted when: tests prove a person without consent is blocked with the reason, an unapproved
  campaign cannot send, outside hours is held, a demo workspace sends only to demo contacts, and STOP
  withdraws consent.

### H. Send to one person

- From a prospect's profile: choose channel and template or write a message, edit, send. The sender
  is the named human decision and is audited.
- Accepted when: the same consent, hours, opt-out and demo rules apply to a single send.

### I. Takeover, reply and the inbox

The takeover and reply parts are Stage 1; the three-pane screen and the rebuilt chat are Stage 2. Qualification
and summaries moved to outcomes Q and P.

Closes B1, B4 to B6, G1 to G6 for the WhatsApp path.

- Kato writes structured facts (name, work, area, interest, amount, anything the customer states) to
  the prospect with provenance as the conversation goes, then recomputes the score and top fit. A
  stranger who texts IIP appears in the Prospects tab, scored, with the reason.
- Stage 2. The inbox becomes three panes: conversations, thread, customer panel with the insights card
  at the top, then profile, score and why, fits, notes and missing information. The chat itself is
  rebuilt: clearer conversation rows (avatar, status dot, intent tag, unread), distinct bubbles for the
  customer, Kato and each officer, date separators, a composer with quick replies, templates and
  attachments, and an unmistakable "you are in control" state.
- A person can reply to a prospect from the portal without a claim. Each conversation has a control
  mode: Kato, a person, or paused. Sending as a person, or pressing Take over, switches it to a person;
  Hand back returns it to Kato, and it returns on its own after a configured idle period. The reply is
  a named human's, inside the 24-hour window as free text and outside it only as an approved template,
  with the restricted-content check and an audit record.
- Conversation workflow as in wacrm's inbox: status (open, pending, closed), an owner with a
  notification to whoever is assigned, presence dots for colleagues, quick replies, reply-quote,
  unread and tag filters.
- Bringing in a colleague: besides the owner, a conversation has participants. An officer adds one
  with an @mention in an internal note or an Add person control; the colleague is notified and the
  conversation appears in their My Work. wacrm only has a single assignee, so this part is new.
  Internal notes never reach the customer.
- When Kato hands over, he leaves a one-line internal note: why, and the customer's last message
  (wacrm's handoff summary).
- Accepted when: tests cover participants and @mentions notifying the right person and an internal note
  never being sent to the customer; tests cover facts from a conversation updating a prospect; a live message from the
  demo phone on dev creates or updates a prospect visible in the dashboard; a person replies from
  the inbox and the customer receives it.

### J. Follow-up agent

Closes J1 to J4.

- A new `follow-up` agent: prospects with no reply a configured number of days after a message get a
  reminder draft; policies ending within a configured number of days (from the retention watch) get a
  renewal reminder draft. Both wait for approval and obey rules 4 and 5.
- A Follow-up tab lists them with the reason, and the Prospects table flags them. A daily timer and a
  "Run now" control.
- Accepted when: tests cover quiet detection and the renewal window; a seeded quiet prospect and a
  seeded renewal appear on screen with a draft.

### K. The Settings modal

- A modal with a left navigation and these sections, each reusing the existing admin content where it
  exists: Integrations (a Catalog and Yours view with search and connect, disconnect and test for
  WhatsApp, email, SMS, Sunbird, Teams, SharePoint, OneDrive, Google Drive), Data Hub (prospect lists
  and import history), Agent rules (behaviour profiles, kill switches, the new agents), Security and
  Login, Notifications, Usage, Billing, Account seats, Organization, Profile, Governance and
  Policies.
- Accepted when: every section shows real data or "Not configured" with what is needed, and none
  duplicates a screen that is left behind without a link.

### L. File connectors

- Pick a file from a connected SharePoint, OneDrive or Google Drive account and feed it to outcome B's
  pipeline. Credentials are per tenant, encrypted, validated before save.
- Accepted when: with real OAuth app registrations a file is imported; without them the cards show
  "Not configured" and say which registrations are needed.

### M. Documentation and honesty

- Update the readiness blueprint section 8, the agent registry text, the gap analysis (mark closed
  gaps), and the stakeholder overview's claims list. State what is demonstration data, what is
  simulated, and what was not verified live.
- Accepted when: nothing claims a send, a connector or a score method that was not verified.

### N. The demo

- A reset command and a rehearsed run: import the list, triage, approve a campaign, receive it on the
  demo phone, reply, watch the prospect qualify, open the follow-up tab. A one-page run sheet in
  `docs/`.
- Accepted when: the run is done end to end on dev, twice, and what was seen is reported.

### O. One design authority for CIP and IIP

Runs after the demo outcomes, and does not block them.

- Extract the design language from CIP v2 into one written document and one token set: colour (with
  the accent and the status colours), type scale, radius, elevation, spacing, motion, the shell (icon
  rail and expanding panel, top bar, assistant entry), and the component contracts (KPI tile, tabs,
  filter bar, table, status pill, avatar, icon tile, modal, drawer, settings modal, three-pane inbox,
  empty and error states). Hard-coded hex values in CIP become tokens during extraction; they are not
  carried over. Where CIP's own documents disagree with its running interface (its token document lists
  blue `#2d5a87`, the interface uses `#1769FF`), the running interface wins and the document is
  corrected.
- One source of truth, consumed by both platforms: a tokens stylesheet, a Tailwind preset and the shared
  components, kept in one place and vendored into the other repository by a script, with a check that
  fails when the two copies drift. Tenant theming (CIP loads a bank's colours and logo at runtime) is
  part of it, which also serves per-insurer branding in the SaaS edition.
- Rewrite IIP's design authority on top of it: `DESIGN.md`, `iip_design_language_v2.md`,
  `iip_ui_craft_guardrails.md` (banned patterns, the checklist and the contrast ledger) and the
  navigation model, and retire the Bevaya reference. Keep, restyled and unchanged in substance, the
  rules that are about the product and not the look: no emoji and no em dashes, six states, no
  fabricated data, AI-prepared work kept apart from human decisions, citations on every actionable AI
  assertion, and the workflow legibility contract with its status vocabulary drawn from the Domain
  enums.
- Update `check-ui-release-gates.mjs`, the Storybook stories and the visual and accessibility tests to the
  new rules and baselines; promote the theme from the scoped areas to the whole portal.
- Decide the landing screen with the owner: IIP's ranked Brief, CIP's chat-first Command Center, or the
  Command Center with the Brief inside it. This is a product decision and is not made by the build.
- Accepted when: both platforms render the same components from the same tokens, the drift check is
  green, every IIP screen is opened at 1280px and 768px against the new baselines, the axe checks pass,
  and the rewritten documents contain no rule the code contradicts.

---

## 6. Verification

```
dotnet build InsuranceIntelligence.sln
dotnet test InsuranceIntelligence.sln
cd web/portal && pnpm lint && pnpm build
```

Open every changed screen in a real browser at 1280px and 768px and compare it with the screenshots.
After each deploy, check the API health, then walk the affected part of the demo path on dev and read
the Application Insights exceptions for the window.

## 7. When something is blocked

- **Meta template approval** is Meta's, not ours. Submit templates as soon as outcome F exists. A
  demo number that has messaged in the last 24 hours can receive free-form messages without one.
- **Email sending** needs a configured provider (a tenant SMTP connection or Microsoft Graph).
  Without one, email shows "Not configured" and drafts are still composed.
- **SharePoint, OneDrive and Google Drive** need OAuth app registrations only the owner's admins can
  create (Google also needs consent-screen verification for production use; test users work for a
  demo). Outcome B's file upload is the working path meanwhile.
- **A real bank's customer list** needs a data-sharing agreement and recorded marketing consent
  before any message. Only synthetic data is used until then.

Do not fake a success path, disable a guard, or weaken validation to make a check pass.

## 8. Reporting and stopping

Each time you report: which outcomes are complete and verified live, which are code complete but
blocked and why, what is left, and what you are doing next. Keep it short. Stop when every outcome is
verified live or a blocker needs a person. Say which it is.
