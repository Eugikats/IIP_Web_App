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

Everything above is built; the dependency order is in section 15.

## 9. Questions for you

1. (Set aside for now: the meeting dates.)
2. Will each insurer use **our** WhatsApp number for the demo, or bring their own?
3. Do you have a **WhatsApp Business account with business verification** already, and which number is the demo number?
4. Which products or lines should the demo use? One simple line (for example motor) is better than three.
5. Can we get **real sample wordings** from either insurer, or should we use public or made-up ones for the extraction demo?
6. Who are the **teammates** who will play customers, and have they agreed to be contacted?
7. Should the renewal conversation end at "request logged for a person", or do you want Kato to take it as far as a payment link? I recommend the first for now.
8. Anything specific Old Mutual or Britam asked for that is not in section 1?

---

# Addendum (2026-10-27): demo scenarios, the bank first-touch message, and how others do this

Meeting dates are set aside. Three new things to plan for, from your notes.

## 10. A demo switch: Bancassurance (Mavuno Bank) or Insurance (Nile), life or general

### 10.1 What exists today

- **Mavuno Bank is already in the demo data.** There is a "Mavuno Bank customer list (demonstration)" of 22 people. Each has a branch, the bank products they hold (for example "Salary account, school fees loan"), income, dependants, a consent status, and a likely cover.
- **The partner insurers already split into life and general:** Mpingo Life Assurance and Kifaru General Insurance, alongside Nile Shield Assurance, whose catalogue has the small-business and rider covers.
- **But the choice of "who Kato speaks as" is fixed when the system starts**, from three settings (`Assistant:Organisation`, `Assistant:ShortName`, `Assistant:PartnerInsurers`). Changing it means changing settings and restarting, and all demo data belongs to one world. That is why a button cannot do it today.

### 10.2 What the switch should change

| Thing | Bancassurance (Mavuno Bank) | Insurance (Nile) |
|---|---|---|
| Who Kato is | The bank's insurance assistant | The insurer's own assistant |
| Covers it can sell | The partners' covers, always naming the insurer | The insurer's own covers |
| Demo people | Bank customers with accounts and loans, no policies yet | Policyholders with policies, expiry dates and some claims |
| What the staff screens emphasise | Acquisition: who to offer cover to | Retention and claims: renewals due, claims waiting |
| First message | The bank's cross-sell template (section 11) | The renewal and claims templates (section 4) |
| Wordings in the Data Hub | The partners' approved wordings | The insurer's approved wordings |

### 10.3 Life or general, inside each

The two classes need different triggers, so the sub-choice should change the demo and not only the product list.

| | Life | General |
|---|---|---|
| Typical covers | Funeral, term life, credit life (loan protector), school fees, savings or endowment | Motor, shop and stock, home contents, goods in transit, personal accident |
| Policy pattern | Monthly premiums, long term | Annual policies |
| Main outreach triggers | Premium due or missed, lapse and reinstatement, beneficiary review, life events (new child, new loan) | Expiry renewal at 30 and 7 days, claim documents chasing, add-on cover at renewal |
| What "closing" means | A new policy or a restored policy | A renewed policy |
| Bank hook (bancassurance) | A loan or salary account, so credit life or funeral cover | An asset the bank financed (vehicle or equipment), so motor or equipment cover |

### 10.4 How to build it safely

I recommend **one demo workspace per scenario, and a switch that moves between them**, instead of one workspace that is wiped and refilled:

- Three ready-made demo workspaces: **Mavuno Bank**, **Nile Life**, **Nile General**. (Please confirm the names. Today's insurer brand is "Nile Shield Assurance".)
- A **Demo scenario** control in the admin area, shown only in a demo environment and never in a workspace with real customers. It switches the active workspace, brand and templates in one click, and shows which scenario is active at all times.
- Switching changes nothing in the other scenarios, so there is no risk of mixing data, and no waiting for a reseed.
- Each scenario carries its own approved wordings, customers, policies with **dates relative to today** (so a "30 days to expiry" customer is always in range), consent records, and its own outreach rules and templates.
- The big technical change: the brand is currently a single value for the whole system. It has to become a **per-workspace setting** that Kato, the outreach service and the fixed reply texts read from. This touches several places, so it is real work, but it is also what a real multi-organisation product needs anyway.

Everything on screen stays honest: the scenario is labelled "Demonstration", and the people are teammates who agreed or clearly made-up records that never get a real message.

## 11. The bank's first-touch message: suggest a cover from what we know, and start the conversation

### 11.1 What you described

You put "Eugene" in the system with what he has done with the bank. The agent works out the best cover for him, then messages him:

> "Eugene, we know you have been saving with us and using the salary loan. Did you know you also qualify for X? Here is what it covers and what it costs. Happy to talk about whether it fits you."

### 11.2 How it works step by step

1. **Eligibility first (plain rules).** Which covers can this person have? Age, whether they already hold it, whether there is a loan that needs protecting, and whether the person agreed to be contacted by the bank about insurance.
2. **Fit ranking second.** Using the bank facts already in IIP (products held, work, dependants, income range), rank the eligible covers. IIP's existing prospect scoring already reads facts like "holds a salary account and a school fees loan" and returns fits with reasons.
3. **Suppress last.** Do not send if they were contacted recently, have an open complaint, asked not to be contacted, or are mid-way through a claim.
4. **Choose one cover, the best fit.** One message, one cover. Offering three at once reads like spam.
5. **Send the opener as a template.** It carries only the hook.
6. **When they tap "Tell me more", Kato sends the detail**, because a reply opens the 24-hour window and free-form text is allowed. This is where the benefits and the price go.
7. **Kato handles the conversation** from the approved wording, and when they say yes, takes an application for the insurer to confirm, as it does today in bancassurance mode.

Why the details go in the second message and not the first: a template has to be approved in advance, so it cannot hold a personalised list of benefits and prices. Other companies do it the same way (section 12).

### 11.3 The opener template (marketing)

`bank_cover_intro`:

> Hello {{1}}, this is Kato from {{2}}. As you bank with us ({{3}}), you may qualify for {{4}}. Would you like to see what it covers and what it costs?

Buttons: **Yes, show me** / **Not now** / **Stop messages**.

Example filled in: "Hello Eugene, this is Kato from Mavuno Bank. As you bank with us (a salary account and a loan), you may qualify for Loan Protector. Would you like to see what it covers and what it costs?"

### 11.4 Kato's second message after "Yes, show me" (free-form)

Built from the approved product data, never from memory:

> "Loan Protector from Mpingo Life Assurance pays off your loan balance if you die or are permanently disabled, so your family is not left with the debt. It also pays UGX [amount] towards funeral costs. It costs about UGX [price] a month, which is roughly UGX [per day] a day. There is a waiting period of [period] and it does not cover [main exclusion]. Mpingo pays the claims, and Mavuno Bank offers the cover. Would you like me to explain how it works, or set it up?"

Rules built in:
- Always name who provides the cover and who pays claims.
- Say the waiting period and the main exclusion, not only the benefits.
- No pressure, no invented urgency or discounts, and no promise that a claim will be paid.
- Offer a person at any time.

### 11.5 Data and consent: a point to settle before you show this

Using a customer's bank history to decide an insurance offer, and then messaging them on WhatsApp, is a use of personal data that needs a clear lawful basis and the customer's agreement to marketing. For a real bank this is a question for the bank's compliance team and counsel, and should be asked in the meeting. In the demo:

- Use only **your own teammates' data, with their agreement**.
- The message should mention only that they bank with us and which kind of product (not balances or transactions).
- The consent record must be in the system and visible. IIP already will not send without it.

## 12. How other companies do automated WhatsApp outreach and conversation handling

This is from a quick search, so treat the specifics as leads to verify, not facts. Where a number comes from a vendor or a consultancy, I say so.

### 12.1 The pieces they all use

1. **A WhatsApp Business Platform provider** that connects to Meta. Examples named in the results include [Infobip](https://www.infobip.com/agentos/conversational-ai-insurance) and [Haptik](https://www.haptik.ai/blog/whatsapp-commerce-for-insurance-takeaways-from-webinar). Others in the same space are Yellow.ai, Gupshup and Twilio, but my search did not return details for those two, so I cannot describe how they work.
2. **A trigger source**: a policy system or CRM saying "this policy ends in 45 days" or "this customer qualifies".
3. **A campaign layer**: segments, schedules, and a sequence of messages. One vendor blog describes a renewal sequence at about 45 days and again at 21 days before expiry (D7 Networks, a vendor, with a renewal-rate improvement claim that has no source, so I would not quote it).
4. **Approved templates** to open conversations, with one-tap reply buttons. Spoki, another vendor, describes renewal-time offers for roadside or travel cover answered with a single tap.
5. **A conversational AI that takes over once the customer replies**, pulls data from the insurer's systems, and hands over to a person with the context when needed. Infobip's insurance page lists proactive outreach for renewals, payments and upsell, across WhatsApp, SMS, voice and web chat.
6. **Recommendation logic.** Haptik's webinar notes describe a recommendation engine that suggests which products to bundle for cross-selling, and a bot that acts as a virtual adviser by asking questions to understand needs.
7. **Reporting**: sent, delivered, read, replied, converted.

### 12.2 The rules they have to follow

From a German consultancy's write-up on proactive WhatsApp outreach ([Nordflux](https://nordflux.de/en/insights/whatsapp-ai-agent-customer-outreach)), which matches how we have designed IIP:

- Consent must be collected and documented before messaging.
- Each message that opens a conversation must be an approved template, classed as marketing or utility, and Meta can reject or later block them.
- Once the customer replies, a 24-hour window opens for free-form messages.
- Since July 2025 Meta charges per delivered template rather than per conversation, with the provider's own fees on top. Their cost figures are for Germany and dated, so check Meta's current pricing for Uganda before quoting anything.

### 12.3 How banks pick whom to offer what

- BCG's [article on scaling AI-powered bancassurance](https://www.bcg.com/publications/2023/steps-for-scaling-ai-powered-Bancassurance) says AI-driven personalisation can decide which clients to contact, when, with what message, and on which channel. It estimates conversion gains of three to five times, and up to eight times for high-propensity customers. These are BCG's own estimates, not independently checked.
- A useful design point from a next-best-action write-up: keep three decisions separate. **Is the customer eligible? How likely are they to respond? Should we make an offer at all (suppression)?** That is the order in section 11.2, and it keeps consent and complaints from being overridden by a high score.
- A [DataRobot example](https://pathfinder.datarobot.com/use-case/predict-which-insurance-products-to-offer) scores customers with a model and feeds the scores into a rules engine that picks the offer. The dataset is synthetic, so it is only an illustration.

### 12.4 What I could not find

- Details of how Yellow.ai and Gupshup implement template submission and opt-in tracking.
- Insurance-specific WhatsApp consent rules for Uganda. This needs counsel and the data protection regulator's guidance, not a blog.

### 12.5 Where IIP is already like them, and where it is not

| | IIP today |
|---|---|
| Consent gate, sending hours, 24-hour window rule, human approval | Yes, stricter than most vendor tools |
| Conversation handling grounded in approved wordings, with citations | Yes, a real strength next to generic bots |
| Local languages and voice notes | Yes |
| Templates catalogue, campaign rules, sequences | Not yet (sections 3 and 4) |
| Segments, scheduling and campaign reports | Not yet |
| Connection to a policy or core banking system for triggers | Not yet. Today the data is loaded, not synced |
| Scale across a big list | Untested. A new number has low daily limits |

The honest pitch is: we are not a bulk-messaging tool. We are the careful, explainable, insurer-grade version, with the conversation and the controls built in.

## 13. What this changes in the plan

Move up in the order of work, because they frame every demo:

1. **Demo scenarios and the switch** (section 10), with the brand moved from a startup setting to a per-workspace setting.
2. **The bank first-touch flow** (section 11): the cover recommendation, the `bank_cover_intro` template, and Kato's detail message.
3. Then the templates catalogue, outreach rules and the demo customer screen from sections 3 and 4.

Add to the templates to submit now: `bank_cover_intro` (marketing).

Add to the questions for you:

9. Confirm the three scenario names: **Mavuno Bank**, and **Nile Life** and **Nile General** (or keep one "Nile" insurer with a life or general choice).
10. For bancassurance, should "life" mean Mpingo Life and "general" Kifaru General, as the partners are set up today?
11. For the first-touch demo, which bank facts will you enter for Eugene: products held, a loan, salary, savings? I would keep it to two or three, so the story is easy to follow.
12. Do you want the bank first-touch to need a person's approval of the message each time, or a rule approved once (as in section 3.3)? For a bank demo I would show the rule approved once.

---

# Addendum 2 (2026-10-27): bancassurance as an agent, "how do we know it is time", and the full ordered build

Decisions from your note: **everything gets built** (so the plan is one full ordered build, section 15). The three scenario names stay as proposed. A rule approved once is the way to run outreach.

## 14. How we know it is time to reach out to someone

### 14.1 What bancassurance means for the design

A bank in bancassurance is an **agent or distributor for insurers**. It does not underwrite and it does not pay claims. It sells cover on behalf of partner insurers and earns commission. A bank usually sells **life, general, or both**. This changes four things in IIP:

- The **Mavuno Bank scenario sells both** life and general covers from its partners. Kato always names the insurer who provides and pays for each cover (it already does).
- **Commission must never decide the ranking.** If the bank earns more on one insurer's cover, the temptation is to push it. The fit ranking cannot see commission, and Kato tells a customer who asks that the bank earns a commission from the insurer. This is both fair and, I expect, a regulatory expectation. Please confirm the exact disclosure rules for bancassurance with counsel and the IRA, because I have not verified them.
- Staff get a **commission and conversion view** (applications, policies, commission earned by insurer), shown to bank staff only and never to the customer.
- The bank's **licensing and agency agreement** with each insurer sits behind everything. We should have a place to record which insurer a bank may sell for, and which lines.

### 14.2 The core idea: triggers are events, not guesses

The system cannot know "this customer just did something at the bank" unless the bank tells it. So the mechanism is:

1. **Events come in.** Each is a small record: who (customer id and phone), what happened (event type), when, and a few safe details (product code, amount band). Not balances, and not transaction lists.
2. **Rules listen for events.** Each rule says "when this event happens, offer this".
3. **Every event is checked against the rules**, then through the same gates as any outreach: consent, not already covered, not contacted recently, sending hours, no open complaint.
4. **A decision is recorded either way** ("considered Eugene, sent" or "considered Eugene, skipped because he declined marketing"). That log is the compliance answer and the demo timeline.

### 14.3 What counts as "a recent action in the bank" (events and the offer they suggest)

| Event from the bank | Reason it matters | Offer it suggests (life or general) | Suggested timing |
|---|---|---|---|
| Loan disbursed | A debt now exists that a family could inherit | **Credit life** (Loan Protector), life | 2 to 3 days after disbursement |
| Vehicle or equipment loan disbursed | The bank financed an asset at risk | **Motor or equipment cover**, general | Before first use, within 2 days |
| Business loan or working-capital loan | Stock and premises are at risk | **Shop and stock cover**, general | 3 days after |
| Salary account opened or first salary credited | A new earner with dependants | **Funeral or personal accident cover**, life | 7 days after |
| School-fees payment or a school-fees loan | Education is the family's biggest cost | **School fees protector**, life | Two weeks before term |
| Savings reaches a milestone or a standing order starts | Building savings | **Education or endowment**, life | Same week |
| Loan nearly repaid or fully repaid | A natural moment to talk | A new cover, or a thank you and a new offer | At repayment |
| Mortgage taken | Large debt and an asset | **Mortgage life** (life) and **property cover** (general) | Within a week |
| An insured policy nearing expiry (general) | Renewal | Renewal reminder | 30 and 7 days before |
| A premium missed (life) | Risk of lapse | Payment reminder, then reinstatement | Day 3, then day 10 |

For the demo, I would use three: **loan disbursed**, **salary account opened**, **school-fees payment**.

### 14.4 How the bank tells us (three routes)

| Route | How | Realistic when |
|---|---|---|
| **A. A person records it** | An "Record a bank event" form: pick the customer, the event, the date | The demo, and a bank's first pilot |
| **B. A file** | The bank drops a daily file of events (one row per event) in a folder IIP can read, or uploads it. IIP already has connected folders and list import to build on | The first real step: most banks can produce a daily extract, and it needs little IT work |
| **C. A live feed** | The bank's system calls IIP when an event happens, or IIP reads an events API | Later, once their IT agrees. Larger banks will ask for this |

Start with A for the demo and B as the real pilot path. Say plainly in the meeting that C is the target and needs the bank's IT.

### 14.5 What a rule looks like (approved once)

A person at the bank (or at IIP for the demo) approves a rule once. It has these parts:

| Part | Example for Eugene |
|---|---|
| Name | "Loan Protector after a salary loan" |
| **When** (the event) | A loan is disbursed |
| **Only if** (conditions) | Loan amount is UGX 2 million or more, term 12 months or more |
| **Who** (audience) | Has agreed to be contacted by the bank about insurance, does not already hold the cover, is a Mavuno customer |
| **Wait** | 2 days after the event (0 for the demo) |
| **Offer** | The best-fit cover among the allowed covers (here: credit life), or one fixed cover |
| **Opener** | Template `bank_cover_intro` |
| **Hours** | 09:00 to 18:00 East Africa time |
| **Never more than** | One offer per person in 30 days, and the same cover only once in 90 days |
| **Stop when** | They say no, reply STOP, raise a complaint, or already have the cover |
| **Follow-up** | One gentle nudge after 3 days of silence, then nothing |
| **Daily limit** | For example 50 people a day, so a big file does not trip WhatsApp's limits |
| **Approved by** | Name and date, kept in the audit trail |

The person approves the **rule and the wording**, not each message. Changing any part makes it need approval again.

### 14.6 Time-based triggers work alongside

Not everything is an event from the bank. Some are dates already in the data: a policy expiring, a premium due, a claim waiting for documents, a prospect gone quiet. A daily sweep (the retention watch already does this for policies) turns those into the same kind of trigger, so one rules engine handles both.

### 14.7 The decision log

Every time an event or date arrives, IIP records, for each person considered: what triggered it, which rule, each gate and whether it passed, and the outcome (sent, or skipped with the reason). Staff can open it per person ("Why did Eugene get this?") and per rule ("Who got it and who did not, and why not"). It is what a compliance officer will ask for first.

### 14.8 Eugene's guessed facts for the demo

Because you said I can guess them. These are made up for the demo and should be entered for a real, consenting teammate (rename as needed):

| Fact | Value |
|---|---|
| Name | Eugene K. (use your teammate's real first name) |
| Bank | Mavuno Bank, Ntinda branch |
| Products held | Salary account, savings account, salary loan |
| Salary | About UGX 2.4 million a month |
| Loan | UGX 8 million, 36 months, disbursed 2 days ago |
| Dependants | 2 children |
| Work | Employed, office job |
| Consent | Agreed to be contacted by the bank about insurance, recorded with a date |
| Cover held | None |

What the system would then do: the **loan disbursed** event arrives, the rule matches, the consent and "no cover held" gates pass, credit life ranks first among eligible covers, and `bank_cover_intro` goes out. After he taps "Yes, show me", Kato sends the benefits and price, and handles questions.

### 14.9 The demo controls

- **Record a bank event** (the form for route A), with a "pretend this was 2 days ago" option.
- **Rules** screen: the rule in 14.5 as a readable card, an approve button, and a pause button.
- **Why this person** timeline: every step of the decision, then the conversation.
- **Switch scenario** (section 10).

## 15. The full ordered build

Everything below is built, in this order, because each step needs the one before. Nothing is optional or cut for time; the order is by dependency, with the long-lead items outside the build running alongside.

### Alongside the build (people and approvals, not code)

| Task | Who |
|---|---|
| Submit templates 1, 2, 5, 6, 8 and `bank_cover_intro` to Meta; confirm the demo number, business verification, display name | You |
| Microsoft and Google app registrations (`OWNER_SETUP_GOOGLE_MICROSOFT.md`) | You |
| Security one-pager checked by counsel | You and counsel |
| Counsel's view on bancassurance disclosure, commission, and marketing consent | You and counsel |
| Real sample wordings from the insurers, or public ones | You |

### The build

| # | Piece | Depends on | What it delivers |
|---|---|---|---|
| 1 | **Per-workspace brand and scenario settings** | none | Kato, outreach and fixed texts read the brand from the workspace, not a startup setting |
| 2 | **Demo scenarios and the switch**: Mavuno Bank, Nile Life, Nile General, each with wordings, customers, policies with dates relative to today, consent | 1 | One click between worlds, clearly labelled as demonstration |
| 3 | **Template catalogue**: name, language, category, status, parameters, which rule uses it; sending uses it outside the 24-hour window | none | Messages no longer blocked for lack of a template |
| 4 | **Events**: an event store, the "Record a bank event" form, and a file import using connected folders | 2 | The input side of triggers |
| 5 | **Rules engine** with the parts in 14.5, approval once, pause, limits, and the decision log | 3, 4 | Outreach that runs itself within an approved rule |
| 6 | **Reason carried into the conversation**: the thread knows it began from "loan protector offer" or "renewal" | 5 | Kato answers a reply knowing why it wrote first |
| 7 | **Outcomes and hand-off**: renewal requested, wants a quote, not now, declined; a task for the right person with full context; STOP handling | 5, 6 | The loop closes |
| 8 | **Bank first-touch flow**: eligibility, fit ranking without commission, suppression, `bank_cover_intro`, Kato's detail message from approved wording | 2, 3, 5, 6 | The Eugene demo |
| 9 | **Renewal and premium flows** for the Nile scenarios (30 and 7 days, premium due, lapse and win-back) | 5, 6, 7 | The insurer demos, life and general |
| 10 | **Document chasing and claim updates** as rules (documents needed, received, status) | 5 | Proactive claims handling |
| 11 | **Demo customer and timeline screens**: add a demo customer, record an event, fast-forward, "why this person" | 4, 5 | The part the audience sees |
| 12 | **Commission and conversion view** for bank staff | 8 | Applications, policies, commission by insurer |
| 13 | **Campaign reporting**: sent, delivered, read, replied, converted, skipped and why | 5, 7 | The numbers slide, from real demo data |
| 14 | **Onboarding: company profile, terms and data-processing acceptance, licence number** | none | A better step 1 and 2 |
| 15 | **Onboarding: security screen and storage step**, including connecting SharePoint, OneDrive or Google Drive and the guided alternative | 14 | The reassurance screen |
| 16 | **The extraction agent** reading wordings into products, covers, exclusions, waiting periods, premiums and claim documents, with sources and confidence | 15 | The biggest missing piece |
| 17 | **The "here is what I found" confirm screen**, then publish | 16 | Setup in minutes |
| 18 | **Managed WhatsApp number flow** ported from CIP: request, support queue, code by SMS or voice, enter code, register | none | "Ask us to set up your number" |
| 19 | **Claim by WhatsApp polish and the identity check** (a code, like CIP's) | none | A strong claims demo |
| 20 | **Security pack**: the one-pager, an architecture diagram, answers to the usual questionnaire | none | The trust slide |
| 21 | **Rehearsal** on a clean workspace for each scenario, with fallbacks | all | A demo that works on the day |

Items 1, 3, 14 and 18 have no dependencies and can start together.

## 16. Updated questions

1. Confirm the Eugene facts in 14.8, or give the teammate's real details (with their agreement).
2. For route B (a daily file), is that how Mavuno-style banks usually extract data? Please tell me what the real banks have said about their systems, because it decides how real the first pilot is.
3. Who at the bank approves a rule: marketing, compliance, or the bancassurance lead? That is the "approved by" in 14.5.
4. For bancassurance, do we record commission rates in IIP, or only show conversions and let the bank apply its own rates?
