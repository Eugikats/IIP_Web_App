# IIP Marketplace: one WhatsApp number, every insurer's cover

Status: proposal for discussion. Nothing in this document is built. Written 2026-10-21.

## 1. The idea in one paragraph

Today IIP serves one organisation at a time: an insurer, or a bank selling its partner insurers' covers. This proposal adds a second kind of customer. **Many insurers sign up to IIP, load their products and policy wordings, and connect their claims and customer data. Members of the public then message one shared WhatsApp number. Kato, IIP's assistant, finds out what the person does and what they need, compares the approved covers from every participating insurer, and helps them choose, explaining each cover in plain words from the insurers' own wordings.** The insurer that is chosen receives the application and serves the customer from there.

The customer gets one place to ask. The insurers get a channel that brings people to them already understood. IIP becomes the neutral layer between them.

## 2. Who is involved

| Who | What they do | What they get |
|---|---|---|
| **Insurer** | Signs up, loads products and policy wordings, sets eligibility and prices, receives applications and leads | A new channel, qualified prospects with the conversation behind them, fewer unclear enquiries |
| **Customer** | Messages one number in English, Luganda or Kiswahili, by text or voice note | Plain-language advice across all insurers, no need to know who sells what |
| **Operator (Otic)** | Runs the shared number, vets insurers, sets the rules of the marketplace | A fee per qualified referral or sale (see section 8) |
| **Regulator (IRA)** | Licenses intermediaries and insurers | Clear records of who advised what, and why |
| **Human adviser** | Licensed person at Otic or at a partner broker who takes over when needed | Full context, no re-asking |

## 3. The customer journey

1. The customer writes to the shared number, for example "I ride a boda boda and I want something in case I have an accident."
2. Kato greets them, takes their name from their WhatsApp profile or asks for it, and asks one or two short questions about their work, family, what they would lose, and what they could pay a month. Nothing is asked as a form.
3. Kato searches the **approved covers of every participating insurer** and picks up to three that fit. For each one it says in plain words:
   - who provides it and who pays the claims;
   - what it pays in a situation the customer described, from the insurer's wording;
   - the monthly price and any waiting period or exclusion that matters to them;
   - why it was chosen, and the one thing the customer should check.
4. If the customer wants one, Kato collects the details that insurer needs and **sends an application to that insurer**. The insurer's own team confirms, issues the policy and explains payment. Kato never says a cover is approved or a claim will be paid.
5. If the customer wants a person, a licensed adviser takes over the same conversation.
6. After the sale the customer can keep using the same number for their policy questions and for reporting a claim, which Kato routes to the right insurer.

## 4. How it fits what IIP already does

Most of the pieces exist. The new work is mainly in how organisations are arranged and how the assistant chooses across them.

| Existing capability | How the marketplace uses it |
|---|---|
| Kato on WhatsApp with text and voice, in English, Luganda and Kiswahili | The customer front door, unchanged |
| Bancassurance mode: Kato already recommends across several partner insurers and names the insurer on every cover | The same behaviour, but over a changing set of insurers instead of a fixed list |
| Data Hub: policy wordings are uploaded, classified by a person, approved, and only then used to answer | Each insurer's wordings, with the same approval gate, never used until approved |
| Grounded answers with citations, and "never invent a price or a rule" | Every cover claim cites the insurer's own document |
| Prospect records, scoring and a staff acquisition screen | Each insurer sees the prospects routed to it, not other insurers' |
| One tenant per organisation, with workspace isolation | One tenant per insurer, so one insurer can never see another's data |
| Human approval for anything consequential | Applications and claim decisions stay with the insurer's people |

## 5. What has to be built

### 5.1 A marketplace layer above the tenants

IIP currently treats each organisation as a separate world. The marketplace needs a thin shared layer that sits above them and holds only what must be shared:

- **The shared number and its conversations.** A customer's messages are not owned by any one insurer until they choose one. They live in a marketplace workspace owned by Otic.
- **A published catalogue.** Each insurer chooses which products to publish to the marketplace. Only published, approved products enter the catalogue.
- **Referrals.** A record of which customer was introduced to which insurer, when, and what was sent.

Everything else stays inside each insurer's own tenant, as today.

### 5.2 Insurer onboarding

1. The insurer signs up and Otic **vets** them: IRA licence, contact person, agreement signed, the lines of business they may sell.
2. They load products, and for each: the policy wording, the schedule of benefits, who it is for, what it excludes, waiting periods, pricing rules, what is needed to apply and to claim.
3. A person at the insurer **approves** each document, as the Data Hub already requires. Otic reviews the first products of a new insurer before they go live.
4. The insurer sets how referrals reach them: the staff screen, an email, or an API into their policy system.

### 5.3 A normalised product catalogue

Insurers describe products differently. To compare fairly, each product is also described in a common form, filled in by the insurer and **checked against the wording**:

- line of business (accident, funeral, health, motor, stock and shop, maternity, school fees, and so on);
- who it is for (occupation types, age range, location, whether it covers a family);
- what it pays, with the amount and the trigger;
- price: monthly, by level, with what changes the price;
- waiting periods, main exclusions, how a claim is made, how fast claims are paid;
- eligibility rules in a machine-readable form (for example "age 18 to 65", "owns the motorcycle").

The wording remains the authority. If the common form and the wording disagree, the wording wins and the product is flagged for the insurer to fix.

### 5.4 The matching and advice engine

Kato's choice of covers is made in two steps, so it can be explained and audited:

1. **Rules filter first.** Remove products the person is not eligible for, or that do not cover what they described. This is plain code, not a model.
2. **Fit scoring second.** Rank what is left on how well it matches their situation (work, dependants, budget, what worries them), using the insurer's own wording as evidence. The model writes the explanation; it does not invent the facts.

Every recommendation stores: what the customer said, which products were considered, which were removed and why, the ranking, and the wording passages used. That record is the answer to "why did you suggest this?" for the customer, the insurer and the regulator.

### 5.5 Fairness rules built into the assistant

The marketplace only works if customers can trust the advice. These are product rules, not guidelines:

- **Advise on fit, not on payment.** Ranking never uses commission, referral fee or which insurer is a bigger partner. The ranking code cannot see them.
- **Show what you are paid.** The customer can ask who pays Otic and how, and Kato answers truthfully.
- **Show the alternatives.** Up to three covers, with the honest differences, including when a cheaper one fits worse.
- **Say when nothing fits.** If no approved cover suits the person, Kato says so and offers a person.
- **No pressure and no urgency tricks**, consistent with Kato's existing rules.
- **No sensitive profiling.** Religion, tribe, health conditions, disability and politics are not asked, scored or stored, as today.

### 5.6 Applications and handover

- The application goes to the chosen insurer with the customer's consent, only the details that insurer needs, and a summary of the conversation.
- The customer is told exactly what was shared and with whom.
- The insurer confirms or declines through its own process. Kato reports the outcome back to the customer in plain words.
- Payment of premiums stays between the customer and the insurer. IIP does not hold customers' money.

### 5.7 After the sale

Once a customer holds a policy, the shared number can recognise them and route to the right insurer: policy questions answered from that insurer's wording, claims reported through the secure upload link and sent to that insurer's claims team. The insurer sees only its own customers.

## 6. Data and privacy

Insurance conversations are personal data under the Data Protection and Privacy Act, 2019, and some of it is sensitive. The design principles:

- **Data stays where it belongs.** Each insurer's products, customers and claims live in that insurer's tenant. The marketplace holds the conversation and the referral record, and nothing about an insurer's policyholders.
- **Share only on a choice.** A customer's details go to an insurer only after the customer picks that insurer and agrees. Before that, no insurer sees the conversation.
- **No selling or trading of customer data**, and no sharing between insurers.
- **Retention and deletion** rules apply to the shared conversations, and a customer can ask for theirs to be deleted.
- **Voice notes and messages** are processed only to answer the customer, as today.

## 7. Regulation and licensing

This is the most important thing to settle before any build. In Uganda, advising people on which insurance to buy and being paid for introducing them is regulated intermediary activity. The proposal must not be launched on a guess. Questions for the IRA and for counsel:

1. **What licence does the operator need?** A broker, an agent, a referral or technology service provider, or a new category. The answer changes who is responsible for the advice.
2. **Who gives the advice?** Kato is software. Which licensed person or entity is accountable for what Kato says, and what records must be kept?
3. **How must commissions and fees be disclosed** to the customer?
4. **Can an automated assistant take an application,** or must a licensed person confirm each one? (The design above already has the insurer confirm.)
5. **What are the rules for selling microinsurance,** and does the existing microinsurance framework help here?
6. **What does the regulator expect on complaints, record keeping and customer disclosures** for a digital channel?
7. **Is a shared WhatsApp number acceptable,** and how must the business be identified to customers?

Until these are answered the safe launch posture is: Kato **explains and compares**, a licensed person at Otic or a partner broker is the named adviser, and every application is confirmed by the insurer.

## 8. How the marketplace earns money

Options, in rough order of how easy they are to defend:

| Model | How it works | Watch out for |
|---|---|---|
| **Platform subscription per insurer** | A monthly fee for being on the marketplace and using the Data Hub and prospect screen | Insurers may want proof of leads before paying |
| **Fee per qualified referral** | A fixed fee when a customer chooses the insurer and a complete application is sent | Must not influence ranking (section 5.5) and may be restricted by the licensing answer |
| **Commission on policies sold** | A share of first-year premium | The most sensitive for fairness and for regulation. Needs the licence answer first |
| **Insurer-side AI credits** | Insurers pay for the AI work on their own enquiries, as IIP already meters | Easy and clean, since IIP already records AI usage per organisation |

A sensible first step is the subscription plus AI credits, with referral fees added only after the licensing answer.

## 9. Risks and how to handle them

| Risk | What could go wrong | Response |
|---|---|---|
| Biased advice | Customers steered to whoever pays most | Ranking cannot see fees. Recommendations are logged and sampled in review |
| Out-of-date wording | A cover is described from an old document | Only approved, current-dated wordings are used. Insurers get expiry reminders. Superseded wordings are removed from answers |
| Mis-selling | A customer buys a cover that does not fit | Eligibility rules first, plain explanation of exclusions and waiting periods, insurer confirmation, a cooling-off statement where the law gives one |
| Insurers distrust each other | Fear of seeing competitors' prices | Insurers never see each other's data. The catalogue shows customers, not competitors, the comparison |
| Cold start | Few insurers means little choice | Start with a small set of partners in one segment, and say honestly which insurers are included |
| Wrong claims answers | Kato answers a claims question wrongly | Claims decisions stay with the insurer. Kato only reports, tracks and explains |
| Privacy breach | One insurer sees another's customer | Tenant isolation, which IIP already tests at the database level, plus marketplace-specific tests |
| Customer confusion about who they bought from | They think the policy is from Otic | Every message names the insurer. Policy documents come from the insurer |

## 10. A phased plan

### Phase 0: settle the rules (weeks, no code)
- Get the licensing and disclosure answers from the IRA and counsel.
- Pick the first segment, for example boda boda riders, market traders and small shops, where the covers are simple.
- Sign a first small group of insurers, ideally three to five, with agreements that cover data, referral fees and conduct.

### Phase 1: prove the matching with a fixed partner list
- Use the existing bancassurance mode as it is: a fixed list of partner insurers, a human adviser on every sale.
- Add the common product form, the recommendation record, and the "why this cover" explanation.
- Measure: how often customers choose the top-ranked cover, how often a person changes the recommendation, and customer feedback.

### Phase 2: real multi-insurer onboarding
- Self-service insurer sign-up with vetting.
- The published catalogue, referral records and the insurer's referral inbox.
- A marketplace workspace for the shared conversations, separate from every insurer's tenant.

### Phase 3: applications and the after-sale loop
- Structured applications sent to insurers, with consent screens and outcome tracking.
- Recognising existing customers on the shared number, routing policy questions and claims to the right insurer.

### Phase 4: scale and widen
- More insurers and lines of business, broader language support, an API into insurers' policy systems, and agent or branch partners.

## 11. How we will know it works

- **Customer side:** the share of conversations that end with a chosen cover, time from first message to application, how many customers say the advice was clear, complaints per hundred sales.
- **Insurer side:** applications received, completion rate, cost per sale compared with their other channels, the number of insurers who stay after three months.
- **Trust side:** the share of recommendations where a reviewer agrees the ranking was fair, the number of times a customer asked about fees, disclosure complaints.
- **Cost side:** AI cost per completed application, so the unit economics are known before scaling.

## 12. Open questions

1. Which licence will the operator hold, and who is the accountable adviser? (Section 7.)
2. Which first segment and which first insurers?
3. Does a shared marketplace number belong to Otic, or should each insurer also keep its own number?
4. Will insurers publish prices openly to customers, or only quote after a few questions?
5. How are claims handled in the first phase: Kato only reports and tracks, or also takes the first notification for each insurer?
6. Who pays for the AI, the insurers, Otic, or a split?
7. Do we need a customer-facing page that lists the participating insurers and how the marketplace is paid, so the disclosure is public and not only in chat?

## 13. Recommendation

Do not start building a new platform. **Run Phase 0 now, then Phase 1 with the capabilities IIP already has**, because the hard parts (the WhatsApp assistant, grounded answers from approved wordings, multi-insurer recommendations in bancassurance mode, tenant isolation, human approval) already exist and are running. The questions that decide whether this can be a business are regulatory and commercial, not technical, so answer those first.
