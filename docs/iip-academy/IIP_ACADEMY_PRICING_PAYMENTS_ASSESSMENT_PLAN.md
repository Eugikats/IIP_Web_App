# IIP Academy: dollar pricing, Flutterwave payments, assessment rules, Zenith limits

Written 2026-10-17 from the owner's notes. This is a plan, not a build. Nothing in it is live yet.

## What you decided

| # | Decision |
|---|---|
| 1 | Every course costs **USD 85** (replaces UGX 500,000). |
| 2 | Unit quiz: 10 questions. Module assessment: 15 questions. End of course task: pass mark 60 of 100. |
| 3 | Zenith is hidden during the end of course task and during quizzes. |
| 4 | Payments use **Flutterwave**, built the same way as in oticlms. |
| 5 | The Discord link is set in the admin screen. Already built. |
| 6 | The hero photo is AI generated. No rights question. |
| 7 | The seven design-only courses stay out. Already the case. |
| 8 | Zenith: **10 explanations a person a day, 1,000,000 tokens a month** for everyone. |

## What is already true today

- **Discord link:** set on the Academy team screen, Community tab. The buttons stay hidden until it is saved.
- **Seven extra courses:** not listed anywhere. Nothing to do.
- **Course 1 (AI & Agentic AI for Insurance):** already exactly 10 questions per unit quiz, 15 per module assessment, pass mark 70% for both, and a final task marked out of 100 with pass mark 60.
- **Zenith during assessments:** the floating Zenith button is not drawn on any quiz, module assessment, course check or final task page. A browser test proves it for quizzes.
- **The other eight courses** have no unit quizzes, no module assessments and no final task. Each ends with one short "check" of 5 questions (four AI courses) or 3 questions (four hands-on courses), pass mark 60.

## Question for you: the other eight courses

I read "check understanding can be less questions" as: **keep the short end-of-course checks short** for the eight courses, and apply the full 10 / 15 / final task structure only to Course 1 for now. That needs no new content.

If you meant every course should have 10 per unit and 15 per module, that is about **1,820 new questions** (the four AI courses: 640 unit questions and 480 module questions; the four hands-on courses: 400 and 300), plus a final task and rubric for each. Those questions have to be written from each course's lessons and checked by a person before learners see them. Tell me which you meant before I build anything here.

## Part A. Dollar pricing

**Change:** prices become USD, default 85 for all nine courses, editable per course on the Academy team screen, Prices tab.

- Stored prices that were saved in shillings are not carried over as dollars. A saved price without a USD marker is treated as unset and shows 85. (The live site has never had prices saved, so nothing is lost.)
- The course page, dashboard cards and checkout show **USD 85**.
- The owner can set a price of 0 for a free course. A free course enrols without payment.

**Decision that needs you: mobile money.** Uganda mobile money (MTN, Airtel) charges in shillings, cards can charge in dollars. So checkout offers two choices:

1. **Card, USD 85.**
2. **Mobile money, shillings at a rate shown to the learner** ("UGX 350,000 at 4,118 per dollar").

The rate is a setting on the Academy team screen (Prices tab), so you control it. Your note "350k = 85 dollars" implies 4,118. Check that against the rate you want to use before launch. The shilling amount is worked out on the server and rounded to the nearest 1,000.

## Part B. Flutterwave payments

### What oticlms does that we keep

I read `oticlms/backend/courses/payments_views.py`. We keep its shape:

- The server decides the price. The browser never sends an amount.
- Flutterwave's hosted payment page (mobile money, card, USSD, bank transfer). No card details touch IIP.
- Payment is never trusted from the redirect alone. The server asks Flutterwave to verify the transaction with the secret key, and only then enrols.
- The redirect callback and the webhook can both arrive; enrolment is created once whichever lands first.
- A payment record is kept for the admin to see.

### What we do better than oticlms

| In oticlms | In IIP |
|---|---|
| The webhook secret is optional: if not set it logs a warning and accepts anything | Required. With no secret configured the webhook rejects everything |
| Verification does not compare the amount or currency to what was asked | The order stores what we asked for. A payment that is less than the order, or in another currency, is refused and flagged |
| A verified payment is trusted for whatever user and course are in its metadata | The order is created by us before payment, keyed by a reference only we hold. Payment is matched to the order, not to metadata |
| There is a leftover test endpoint that marks any payment successful | Not carried over |

### How it works in IIP

1. Learner presses Enrol on a priced course. The checkout dialog (already in the design, with payment option cards) asks card or mobile money.
2. `POST /api/v1/academy/courses/{id}/checkout`: the server works out the price, creates an **order** (kind `order` in the Academy table: reference, person, course, amount asked, currency, status pending), calls Flutterwave's payments API and returns the hosted page link.
3. Learner pays on Flutterwave. Flutterwave sends them back to `/academy/payment/callback`.
4. The callback page calls `POST /api/v1/academy/payments/confirm`. The server verifies with Flutterwave, checks the amount and currency against the order, marks it paid and enrols. The page then shows "You are enrolled" and goes to the course.
5. `POST /api/v1/academy/payments/webhook` does the same from Flutterwave's side, with the `verif-hash` header checked in constant time.
6. **Reconcile sweep:** a background job re-checks orders that stayed pending for more than 5 minutes by reference (Flutterwave "verify by reference"). A learner who paid and closed the tab is enrolled anyway.
7. A person is notified in the Academy when payment succeeds, and an order that cannot be matched appears on the admin screen for a person to resolve.

### Where the code sits

- The Flutterwave calls sit behind an interface (`IPaymentGateway`) with a Flutterwave adapter and a fake one for tests, as with every other provider in IIP. Domain rules (order states, amount checks, idempotent enrolment) are provider free.
- Settings: `Flutterwave__SecretKey`, `Flutterwave__PublicKey`, `Flutterwave__SecretHash`, `Flutterwave__Mode` (test or live). The secrets go in Key Vault `kv-iip-dev-zcxolwov`. They are Otic's own platform credentials, not an insurer's, so they are not per-organisation connections.
- `/api/v1/academy/payments/webhook` is added to the few routes that do not need a sign-in (like the WhatsApp webhook), protected by the secret hash. The confirm route needs a sign-in and only works for the person who owns the order.
- New admin tab **Payments**: list of orders (person, course, amount, currency, status), a "check with Flutterwave again" button for a pending order, and a "mark refunded" note. Refunds are made in the Flutterwave dashboard; IIP records that it happened and revokes access if you choose.
- Until live keys are set, the site behaves as it does now: enrolment is open and charges nothing, with the notice. **Once keys are present, a priced course needs payment**, except when an Academy operator enrols a person by hand.

### Two things to check on the Flutterwave side

1. **Webhook address.** I believe a Flutterwave account has one webhook address, and oticlms may already be using it. If so, IIP cannot have its own on the same account. Step 6 (the reconcile sweep) makes payments safe without a webhook, but a separate Flutterwave business account for IIP is cleaner. Please confirm which you want.
2. **Dollar collection.** The Flutterwave account must be allowed to take card payments in USD and to settle them. Check that in the Flutterwave dashboard.

### A security problem found in oticlms

`oticlms/FLUTTERWAVE_INTEGRATION.md` contains the **live Flutterwave secret key, public key and encryption key, and an email account password**, written in plain text and committed. Anyone with the repository can take payments and read the account. I have not copied any of it. I recommend you **rotate those keys and the email password now**, remove them from that file, and keep them only in the deployment environment. If IIP is to use the same Flutterwave account, use the new keys.

## Part C. Assessment rules

| Item | Rule | State |
|---|---|---|
| Unit quiz | 10 questions, pass 70%, unlimited attempts, a pass completes the lesson | Done for Course 1 |
| Module assessment | 15 questions, pass 70%, 3 attempts | Done for Course 1 |
| End of course task | Pass 60 of 100, marked by a person against the rubric | Done for Course 1 |
| Other eight courses | Short end check, pass 60% | As today, unless you want the full structure (see the question above) |
| Zenith hidden | On every assessment page and the final task page | Done in the page. See the hardening below |

**Hardening for "Zenith is hidden".** Hiding the button does not stop a learner opening Zenith in another tab. So:

1. **Quizzes and assessments:** when a learner starts one, the server marks them as sitting it. Until they submit, or 90 minutes pass, Zenith's ask and practice endpoints answer "Zenith is switched off while you are in an assessment." The Zenith page and the dashboard box show the same message.
2. **End of course task:** the task page sends a short signal while it is open. Zenith is switched off while the page is open and for 5 minutes after it is closed. (A person can still write the task for days; Zenith comes back when they leave the page.)
3. Both are tested: ask Zenith in a second tab during a quiz and get refused.

## Part D. Zenith limits

- Daily allowance: **10** written explanations per person (was 30).
- Monthly allowance: **1,000,000** tokens for everyone (was 2,000,000).
- Both become settings on the Academy team screen (a Zenith tab), with the month's usage shown, so you can change them without a deploy. Recommendations, plans and practice questions stay free and are not counted.

## Order of work

1. **Pricing in dollars** (domain, API, screens, tests). Prices tab gets the exchange rate.
2. **Zenith limits and the assessment lock** (server and screens, tests). Zenith tab on the admin screen.
3. **Payment core**: order records, `IPaymentGateway`, the fake gateway and all the amount, replay, wrong-person and signature tests.
4. **Flutterwave adapter** against Flutterwave test keys; checkout dialog with card and mobile money; callback page; Payments admin tab; reconcile sweep.
5. **Deploy gate**: API, portal, and Flutterwave **test mode** keys in Key Vault. Pay with a Flutterwave test card and test mobile money number; check enrolment, the admin list and a refund note.
6. **Live gate** (needs you): live keys in Key Vault, `Flutterwave__Mode` set to live, webhook address set in the Flutterwave dashboard if you have a separate account, one real small payment, then the first real sale.
7. If you want it: the eight other courses' full question banks, written and reviewed.

## What I need from you

1. The answer to "the other eight courses" above.
2. Card plus mobile money at the checkout, with a shilling rate: yes or no, and the rate.
3. Same Flutterwave account as oticlms or a separate one.
4. Is USD 85 the price including any tax (VAT), or is tax added?
5. Rotate the oticlms keys.
6. Flutterwave test keys, put into Key Vault by you (see `OWNER_SETUP_GOOGLE_MICROSOFT.md` for how to store a secret without typing it anywhere else).
