# Getting IIP ready for the insurer meetings (Old Mutual, Britam)

Status: plan and brainstorm. Nothing here is built yet. Written 2026-10-27 after reading IIP (`iip_saas`, the live line) and CIP (`CIP_Web_App`).

## 1. What the meetings need to prove

Each meeting is a short demo plus hard questions. The story we want them to leave with:

> "In ten minutes this insurer is set up from its own documents. Its customers reach it on WhatsApp, claims arrive complete, renewals get chased automatically, and every decision still belongs to our people."

I have not been in these meetings, so the list of what Old Mutual and Britam will ask for is my judgement of what large insurers care about, not something they told us. Please correct it with anything you have heard from them.

| What they will probably want to see | Why it matters to them |
|---|---|
| **Setup that looks easy**: sign up, upload documents, confirm, connect WhatsApp | They fear a long IT project |
| **A real claim by WhatsApp**: accident, photos and papers arrive, the file is checked, a person decides | Claims cost and customer anger are their pain |
| **Proactive outreach**: renewal and document chasing without staff effort | Lapses and incomplete files cost money |
| **Control**: who approved what, an audit trail, human sign-off on decisions | Regulatory and reputational risk |
| **Security and data**: where documents live, who can see them, Uganda data protection | Their risk and legal teams will ask first |
| **Local language**: Luganda and Kiswahili, voice notes | Reach beyond English speakers |
| **A path to their systems** (policy admin, claims, CRM) | They will not retype data forever |
| **Numbers**: time saved, cost per claim, response time | The business case |

## 2. Where IIP stands today

I checked the code. Green means it exists and works, amber means partly there, red means missing.

### Workstream A: intent-based proactive outreach

| Piece | State | What exists / what is missing |
|---|---|---|
| Spot who to contact | Amber | Retention watch finds policies ending in 60, 30 and 14 days. A follow-up drafter finds prospects quiet for 3 days and policies ending within 30. Missing: premium due or missed, claim stuck waiting for documents, new-customer welcome, win-back |
| Write the message | Green | An outreach agent drafts a message per person |
| Consent, hours, rules | Green | A send is blocked unless the person agreed to contact, it is within 07:00 to 20:00, and WhatsApp's rules allow it. The reason is kept on the draft |
| Send outside the 24-hour window | **Red** | The sender can send a template, but there is **no template catalogue**: nothing records which templates exist, their language, their approval status, or which trigger uses which. Today the message is simply blocked with "needs an approved WhatsApp template" |
| Run without a person pressing send | **Red** | Everything is draft then a named person approves, by design. A demo needs a rule that sends by itself after the insurer approves the rule once |
| Keep the conversation going after the first message | Amber | Kato answers replies well, but does not yet know **why** it wrote first ("this is about your motor policy ending on 14 Nov"). Without that, the reply can feel like a stranger |
| Close it | **Red** | No defined end for a renewal conversation: renewed, wants a quote, not now, declined, lost. No hand-off task to a renewals person |
| Stop contacting | Amber | Consent can be declined or withdrawn. A plain "STOP" reply and frequency limits need checking |
| Add a customer to demo with | Amber | Policies can be imported and the demo seeder exists, but there is no simple "add a proof-of-concept customer" form with a policy number and expiry date |

### Workstream B: onboarding an insurer

IIP already has a self-serve checklist with six steps: organisation set up, connect WhatsApp, publish products, upload approved wordings, invite the team, look at the sandbox. That is a good skeleton. What the vision adds, step by step:

| Step you described | State | Notes |
|---|---|---|
| Sign up with email, capture company name and details | Amber | Organisation creation exists. Missing: licence number, lines of business, country, size, who signs the agreement, terms and data-processing acceptance |
| Upload documents, or connect SharePoint or OneDrive | Amber | Connected folders for OneDrive, SharePoint and Google Drive are built and tested, but not switched on (the Microsoft and Google app registrations are still to be done by you) and not placed inside onboarding |
| Reassure them about security | **Red** | No screen says where documents are stored, how they are protected and who can see them |
| Guide them to set up storage if they have none | **Red** | Not built. Simplest answer: IIP provides the storage in their own isolated workspace |
| An agent reads the wordings and builds the setup | **Red** | Products and the "documents needed for a claim" matrix are entered by hand today. Nothing reads a wording and proposes them |
| "Here is what I found, confirm it" screen | **Red** | The editors exist (products, document matrix, stages), so the confirm screen can reuse them. The extraction is the missing part |
| Set up the WhatsApp number: bring your own | Green | Number and credentials can be saved, tested and checked against Meta |
| Set up the WhatsApp number: ask us to create it | **Red** | This is CIP's managed flow. See section 6 |
| Team invites and roles | Green | Built |
| Sandbox to try it | Green | Built |

### Workstream C: customers sending claim documents on WhatsApp

| Piece | State |
|---|---|
| Customer reports a claim and sends photos and documents in chat | Green. Files are stored on the claim, the customer gets an acknowledgement, and missing documents are asked for |
| A secure link for bigger uploads | Green |
| Per-claim checklist of what is still missing | Green |
| Voice notes in Luganda and Kiswahili | Green (some provider-dependent) |
| Identity check before sharing claim details | Amber. A code check like CIP's would strengthen it |

A security point for the Old Mutual and Britam questions, because it is easy to get wrong: **WhatsApp messages are encrypted between the customer's phone and WhatsApp's servers, but with the WhatsApp Business API the business platform receives and processes them.** So do not tell an insurer that documents are "end to end encrypted to you". Say: encrypted in transit, received through Meta's business API, then stored in the insurer's own isolated workspace, with access logged. Please have counsel check the exact wording before it goes on a slide.

## 3. Workstream A in detail: intent-based outreach that can close

### 3.1 What "intent-based" means here

Outreach starts from a **reason**, not a blast. Each reason is a trigger with its own message and its own definition of success.

| Trigger (the reason) | Message | Customer replies... | Success looks like |
|---|---|---|---|
| Policy ends in 30 days | Renewal reminder with the cover and date | "Yes renew" | Renewal request logged and handed to a person, with the quote |
| Policy ends in 7 days, no reply | Short second reminder | Anything | Same, or marked "not now" |
| Premium due or missed | Payment reminder | "I paid" or "later" | Payment promise logged, or a person follows up |
| Policy has lapsed | Win-back offer | "Yes" | Reinstatement request logged |
| Claim waiting on documents for 3 days | Document reminder listing what is missing | Sends photos | Checklist complete |
| New customer or imported contact | Introduction, asks permission to continue | "Yes" | Consent recorded, conversation continues |
| Asked about a cover, then quiet 3 days | Gentle follow-up | Questions | Moves to quote or "not now" |
| Claim settled | Satisfaction check | A score | Score recorded, complaints routed to a person |

### 3.2 The loop, end to end

1. **Trigger fires** (for example, a policy reaches 30 days before expiry).
2. **Gate**: the person agreed to be contacted, it is within sending hours, they were not contacted in the last few days, and they have no open claim that makes a renewal message tactless.
3. **Choose the template** in the person's language, and fill it with their details.
4. **Send.** Outside the 24-hour window only an approved template can go.
5. **Remember the reason.** The conversation is tagged "renewal, policy NSA/MO/2026/00077, ends 14 Nov". When the person replies, Kato already knows what they are answering.
6. **Kato continues**: confirms they want to renew, explains the renewal cover and price from the approved wording, asks the one thing it needs, handles doubts.
7. **Close.** Kato records the outcome (renewal requested, wants changes, not now, declined, lost) and creates a task for the renewals team with the full context. The insurer's person confirms and issues. Kato never says a renewal is done unless the insurer has done it.
8. **Follow-ups on a cadence** (day 0, day 3, day 7) and then stop. A "STOP" reply ends outreach immediately.

### 3.3 A rule the insurer approves once

For the demo and for real use: a person at the insurer approves a **rule** once ("Remind motor customers 30 and 7 days before expiry, using these two templates, between 09:00 and 18:00, once only"). After that the system sends by itself within the rule. The person approves the rule and the wording, not every message. Every send is still logged and consent is still checked. This keeps the human-control promise while making it a real automation.

### 3.4 The proof-of-concept customer you described

A small screen, "Add a demo customer": name, WhatsApp number, product, **policy number**, **start and end dates**, and a consent box that records who ticked it and when. Two demo buttons:

- **Run the renewal rule now**, which pretends today is the day the rule fires.
- **Fast-forward**, which moves the policy to "ends in 29 days" so the 30-day rule fires.

Beside it, a **timeline** showing each step as it happens: trigger found, checks passed, template chosen, sent, delivered, replied, Kato's answer, outcome. That timeline is what an insurer will remember.

Demo customers must be real teammates who agreed, never made-up phone numbers, and every demo record is labelled as such in the system.

### 3.5 WhatsApp rules we have to respect

- To start a conversation outside 24 hours you need an **approved template**. Meta reviews each one, which can take hours or a few days, so **submit them now**.
- Templates are in categories. Renewal and payment reminders are normally "utility". Introductions and offers are "marketing", which has stricter opt-in and costs more per message.
- A new WhatsApp number starts with a low daily limit on people it can contact, which grows with good quality. A big list on day one will be throttled.
- For list outreach, you need evidence that each person opted in. That is why the "agreed to be contacted" record is central, and why the demo list must only include people who agreed.

## 4. The templates to set up

Submit each in English first, then Luganda and Kiswahili. Placeholders are shown as `{{1}}`. Keep each under about 300 characters and avoid promotional language in utility ones. Names are suggestions.

| # | Template name | Category | Purpose | Draft wording (English) | Buttons |
|---|---|---|---|---|---|
| 1 | `renewal_30_days` | Utility | Renewal reminder | Hello {{1}}, your {{2}} cover ({{3}}) ends on {{4}}. Would you like to renew it? | Yes, renew / Not now / Talk to a person |
| 2 | `renewal_7_days` | Utility | Second reminder | Hello {{1}}, a reminder: your {{2}} cover ends on {{3}}. Reply to renew or ask a question. | Renew / Not now |
| 3 | `premium_due` | Utility | Payment due | Hello {{1}}, your {{2}} premium of {{3}} is due on {{4}}. Reply if you need help paying. | I paid / Need help |
| 4 | `policy_lapsed` | Utility | Win-back | Hello {{1}}, your {{2}} cover ended on {{3}}. You can restart it. Would you like details? | Yes / No thanks |
| 5 | `claim_documents_needed` | Utility | Missing documents | Hello {{1}}, for claim {{2}} we still need: {{3}}. You can send photos here in this chat. | Send now / Need help |
| 6 | `claim_received` | Utility | Acknowledgement outside the window | Hello {{1}}, we received your claim {{2}}. A claims officer will review it. We will update you here. | (none) |
| 7 | `claim_status_update` | Utility | Status change | Hello {{1}}, an update on claim {{2}}: {{3}}. Reply with any question. | Talk to a person |
| 8 | `welcome_and_consent` | Utility or Marketing | First contact from a list | Hello {{1}}, this is Kato from {{2}}. May we message you here about your insurance? | Yes / No |
| 9 | `quote_follow_up` | Marketing | Quiet after a quote | Hello {{1}}, you asked about {{2}} cover. Would you like to go on? | Yes / Not now |
| 10 | `satisfaction_check` | Utility | After settlement | Hello {{1}}, how was your claim {{2}} experience? Reply 1 to 5. | 1 to 5 quick replies |
| 11 | `verify_code` | Authentication | Identity check | {{1}} is your verification code. | Copy code |

Start with 1, 2, 5, 6 and 8: they cover the demo. Which category Meta accepts for each is Meta's decision, so expect some to be reclassified.

## 5. Workstream B in detail: onboarding that looks easy

### 5.1 The journey, with the likely bottlenecks

| Step | What the insurer does | What can slow it down | How to remove it |
|---|---|---|---|
| 1 Sign up | Email, then company name, country, job title, phone | Verification email missed | Sign in with Microsoft work account too |
| 2 Company profile | Legal name, IRA licence number, lines of business, contact, who accepts the terms | Legal sign-off | Capture the licence number now, let a colleague accept the terms later |
| 3 Security and data | Reads how documents are protected, chooses where they live | IT and risk teams want answers first | One clear screen with the architecture, plus a downloadable security summary |
| 4 Documents in | Upload wordings, claim forms, product sheets, or connect SharePoint, OneDrive or Google Drive | IT must approve the connection; scanned PDFs read badly | Upload as the default, folders as the option. Flag unreadable scans at once |
| 5 AI reads them | Waits while IIP reads the documents | Slow or wrong extraction | Show progress, work page by page, show evidence for every item found |
| 6 Confirm | Checks each product, its covers, exclusions, waiting periods and the **documents needed for a claim**, edits, confirms | Boredom; blind approving | One product at a time, items it is unsure about shown first, a person's name recorded on approval |
| 7 WhatsApp number | Brings their own, or asks us to set one up | Meta verification and display-name approval take days | The managed flow in section 6, and a shared demo number until theirs is live |
| 8 Team | Invites officers, supervisors, compliance, sets roles | Waiting on IT | Invite by email, allow an admin to add the rest later |
| 9 Test and go live | Sends a test message, runs a sample claim in the sandbox, ticks a go-live checklist | Fear of going live | A "send me a test claim" button and a go-live checklist |

### 5.2 The extraction agent (the biggest missing piece)

An agent reads each uploaded wording and proposes a draft setup. For each product it extracts:

- name, line of business, who it is for, the insurer;
- what it covers and what it pays, with amounts where the wording gives them;
- exclusions, waiting periods, limits, excesses;
- premium and how it changes (if stated);
- **how to claim: the documents needed, by claim type**, the steps, and time limits;
- contacts and escalation points.

Rules for the agent (all consistent with how IIP already works):

1. **Every item carries the passage it came from** and a confidence. Items without a source are dropped, not guessed.
2. **It proposes; a person confirms.** Nothing is published until a named person approves each product.
3. **Unsure items are shown first** so the human attention goes where it matters.
4. **It never fills a gap from general knowledge.** A missing waiting period is shown as "not stated in the wording".
5. After confirmation, the draft becomes the real product and document matrix that Kato uses, using the screens that already exist.

### 5.3 The "here is what I found" screen

One product per page: a short summary on the left, the original wording passage on the right with the matched text highlighted, each extracted item as a row with confirm, edit, or remove. A progress bar ("4 of 7 products confirmed") and one "publish" button at the end.

## 6. The WhatsApp number: bring your own, or ask us (CIP's flow)

You asked about picking up CIP's OTP function. What CIP has, and what IIP needs, is the **phone number lifecycle with Meta**. In `CIP_Web_App`:

- `MetaPhoneNumberClient`: asks Meta to send a verification code to the number (by SMS or by voice call), verifies the code, registers the number with a PIN, and subscribes the app to the account. It translates Meta's errors into plain reasons (wrong code, code expired, number still on the WhatsApp app, rate limited, display name rejected).
- `WhatsAppOnboardingService`: the managed flow. The organisation submits the number it wants, its display name, a contact email and consent. A request appears in a **support queue**. Our team starts it, requests the code, the organisation enters it on a card in their screen, and the number goes live. An organisation can cancel an open request, and a number already verified in WhatsApp Manager can be registered directly.
- Emails to support and to the organisation at each step, and codes and PINs are never logged.

IIP has the "bring your own" path only: save the account and number identifiers, test, and check with Meta. The managed path is the one to port. A separate CIP service, `SmsOtpService`, sends one-time codes to customers by SMS for identity checks; it is a second thing to consider for the claim identity check, and is not the same as the number lifecycle.

Two things to know before we promise this to an insurer:

- **A number that is already used in the normal WhatsApp app cannot be used with the business API** until it is removed from that app. CIP handles this as a named failure ("number on WhatsApp app"). Tell customers up front to use a number that is not already on WhatsApp, or to remove it first.
- **The display name needs Meta's approval** and can be rejected. Ask for the name early.

## 7. What a good meeting storyline looks like

About 25 minutes.

1. **Setup in ten minutes (live).** Sign up, upload two real-looking wordings, watch the agent read them, confirm what it found, connect WhatsApp.
2. **A customer has an accident.** A teammate messages the number, in their own language, sends photos and a document. The claim opens, the missing-document list shrinks, the officer sees a complete file with the service clock.
3. **A policy is about to end.** The demo customer's rule fires; the template arrives on the teammate's phone; they reply; Kato handles it and a renewal task appears for a person.
4. **The staff view.** Acquisition screen, inbox, approvals, the audit trail.
5. **Trust.** Where the documents live, who can see them, human decisions only, the audit trail, Uganda data protection.
6. **Numbers.** What this saves per claim and per renewal, from the pilot data we can honestly show.
7. **The ask.** A small paid pilot on one line of business with success measures agreed in advance.

Everything shown must be what really happens. Demo data should be labelled, and anything not built should be said plainly, not faked.

## 8. What to do first (order of work)

Long lead items first, because they depend on other people:

| When | Task | Who |
|---|---|---|
| This week | Submit templates 1, 2, 5, 6, 8 to Meta in English; confirm the demo WhatsApp number, its business verification and display name | You |
| This week | Do the Microsoft and Google app registrations so folders and SharePoint work (`OWNER_SETUP_GOOGLE_MICROSOFT.md`) | You |
| This week | Write the security one-pager (storage, encryption in transit and at rest, access, audit, data location, data protection) and have counsel check it | You and counsel |
| Build 1 | Template catalogue (name, language, category, status, which trigger uses it) and the sender using it; the reason carried into the conversation | Build |
| Build 2 | Outreach rules the insurer approves once, with triggers for renewals, documents needed and welcome; stop and frequency rules; outcomes and the hand-off task | Build |
| Build 3 | "Add a demo customer" screen with the timeline and fast-forward | Build |
| Build 4 | Onboarding: company details, security screen, documents step, the extraction agent, the confirm screen | Build |
| Build 5 | Port CIP's managed number flow (request, support queue, code entry) | Build |
| Build 6 | Polish the claim-by-WhatsApp path and the claim identity check | Build |
| Before each meeting | Full rehearsal on a clean workspace, with a fallback if the network or WhatsApp fails | Both |

If time is short, the order of value for the meetings is: 1 (templates and sending), 3 (demo customer and timeline), 2 (rules), then 4 (onboarding with the extraction agent), then 5.

## 9. Questions for you

1. **When are the meetings?** That decides how much of section 8 we can finish.
2. Will each insurer use **our** WhatsApp number for the demo, or bring their own?
3. Do you have a **WhatsApp Business account with business verification** already, and which number is the demo number?
4. Which products or lines should the demo use? One simple line (for example motor) is better than three.
5. Can we get **real sample wordings** from either insurer, or should we use public or made-up ones for the extraction demo?
6. Who are the **teammates** who will play customers, and have they agreed to be contacted?
7. Should the renewal conversation end at "request logged for a person", or do you want Kato to take it as far as a payment link? I recommend the first for now.
8. Anything specific Old Mutual or Britam asked for that is not in section 1?
