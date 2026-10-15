# Course 1 · Module 4 — Responsible AI Governance in Insurance

## Research packs for Units 4.1–4.3

---

## Unit 4.1 — AI risks specific to insurance

**Learning outcome:** Identify fairness, explainability, security, third-party and conduct risks in an AI use case.

**Video length target:** 11–12 minutes.

### Core concepts and terminology

Insurance is built on differentiating between risks, which makes the line between legitimate risk assessment and unfair discrimination central to AI governance. Six risk families matter most.

**Fairness and bias.** Models learn from historical data, which may reflect past unfair practices or under-representation of some groups. **Proxy discrimination** occurs when a variable that seems neutral — a location, a phone type, a mobile-money pattern — closely tracks a characteristic that should not drive decisions. **Explainability.** Customers declined, loaded or delayed are entitled to understand why; complex models can make this hard, and the IAIS treats transparency and explainability as a core supervisory topic (S07). **Robustness and security.** Models can fail on unusual inputs, degrade as conditions change (**model drift**), be manipulated (prompt injection, Unit 3.3), or leak sensitive data. **Third-party risk.** Most insurers will use vendor models and cloud platforms; the insurer remains accountable for outcomes, so contracts, data terms, audit rights and exit plans matter. **Conduct and customer outcomes.** AI that pushes products customers do not need, misstates cover, or makes claims harder to pursue creates conduct risk. **Over-reliance.** Staff may lose skills or stop challenging outputs (automation bias, Unit 2.2).

### Sources

S07 (IAIS — five topics including fairness, ethics and redress), S08 (NIST — trustworthy AI characteristics, *verify*), S09 (EU AI Act — life and health insurance pricing as high-risk), S15 (DPPA gaps on profiling), S17 (*verify*).

### Regulatory and operational considerations

The EU AI Act classifies AI used for risk assessment and pricing of natural persons in life and health insurance as high-risk (S09); although not binding in Uganda, it signals where international expectations are heading and will apply to Ugandan firms serving EU-linked groups or clients. In Uganda, DPPA obligations apply to profiling and automated decisions (S05).

### Realistic Ugandan scenarios

A micro-health product uses an ML model to price cover. The model learns that customers in certain districts have higher claims; investigation shows those districts have better hospital access, so people there actually use their cover. Pricing on this pattern would penalise access to care rather than risk.

A vendor's chatbot for a broker stores conversations on overseas servers for "model improvement"; the contract does not address deletion or DPPA compliance.

*Validate through S16.*

### Common professional mistakes

Assuming removing a protected attribute removes bias; accepting "the model is proprietary" as an answer to explainability questions; not monitoring after go-live; signing vendor terms without data and audit clauses.

### Recommended techniques

Ask of every use case: Who could be treated unfairly? Can we explain the outcome to the customer? What happens if it fails? Who is the vendor and what do they do with our data? How will we know if it drifts?

### Teaching examples and storyboard pointers

Six-tile risk map. Animated proxy-discrimination example (the district pricing case). Vendor-contract checklist slide.

### Assessment questions (10)

1. **Proxy discrimination occurs when:**
   A) A broker acts on behalf of a client B) A seemingly neutral variable closely tracks a characteristic that should not drive decisions C) A model is too small D) A premium is paid late
   **Answer: B.**

2. **A model raises micro-health prices in districts with better hospital access because claims are higher there. What is the concern?**
   A) None — higher claims justify higher prices B) The model may be penalising access to care rather than true risk, creating an unfair outcome C) The model is too accurate D) The districts are too small
   **Answer: B.**

3. **Model drift means:**
   A) The model moves to another server B) Model performance changes over time as real-world conditions change C) A model is copied by a competitor D) The model's name changes
   **Answer: B.**

4. **A vendor refuses to explain how its model declines applications, citing proprietary rights. The insurer should:**
   A) Accept this B) Recognise it remains accountable and require sufficient explanation, testing access or alternatives C) Stop explaining declines to customers D) Rely on the vendor's reputation
   **Answer: B.**

5. **Which IAIS supervisory topic most directly relates to customers understanding why AI affected them?**
   A) Proportionality B) Transparency and explainability C) Capital adequacy D) Reinsurance
   **Answer: B.** (S07)

6. **Removing gender from a dataset guarantees the model will not discriminate by gender. True or false?**
   A) True B) False — other variables may act as proxies
   **Answer: B.**

7. **Which contract clause is most important when a chatbot vendor processes customer conversations?**
   A) Logo usage B) Data use, retention, deletion, location and audit rights C) Office hours D) Font choice
   **Answer: B.**

8. **Under the EU AI Act, AI used for risk assessment and pricing of individuals in life and health insurance is classified as:**
   A) Prohibited B) High-risk C) Minimal risk D) Unregulated
   **Answer: B.** (S09)

9. **An AI sales assistant consistently recommends the highest-commission product regardless of need. Which risk family is this?**
   A) Conduct and customer outcomes B) Model drift C) Prompt injection D) Data minimisation
   **Answer: A.**

10. **Which practice best addresses over-reliance on AI?**
    A) Removing human review B) Keeping staff trained to challenge outputs and sampling AI work regularly C) Using more AI tools D) Hiding AI use from staff
    **Answer: B.**

### Sources, dates and reuse

S05 (PD), S07 (Quote), S08 (*verify*), S09 (Facts), S15 (Facts), S17 (*verify*). Original synthesis. Expert verification: pending.

---

## Unit 4.2 — The governance landscape: IAIS, NIST, EU AI Act and Uganda

**Learning outcome:** Explain what the main frameworks and Ugandan laws expect from an insurer using AI.

**Video length target:** 12 minutes.

### Core concepts and terminology

Four layers of guidance shape AI governance for a Ugandan insurer.

**International insurance supervision — IAIS.** In July 2025 the IAIS published its Application Paper on the supervision of artificial intelligence after public consultation (S07). It organises supervisory expectations around five topics: risk-based supervision and proportionality; governance and accountability; robustness, safety and security; transparency and explainability; and fairness, ethics and redress (S07). Application Papers do not create new requirements; they explain how existing Insurance Core Principles apply (S07). The IRA, as an IAIS member supervisor (*verify membership status*), is likely to draw on this paper. In 2026 the IAIS also issued a members-only supervisory question bank on AI governance and risk management building on the paper (S07 digital innovation page).

**Practical risk-management framework — NIST AI RMF.** The US NIST framework organises AI risk management into four functions: **Govern** (policies, roles, culture), **Map** (understand context and risks of each use case), **Measure** (test and track performance and risks), **Manage** (prioritise and act on risks) (S08, *verify*). Its Generative AI Profile adds GenAI-specific risks such as confabulation and information integrity (S08). It is voluntary and widely used as a practical toolkit.

**Reference regulation — EU AI Act.** The EU AI Act takes a risk-based approach and lists AI for risk assessment and pricing of natural persons in life and health insurance as high-risk (S09). Under a provisional agreement reached on 7 May 2026, obligations for these stand-alone high-risk systems were deferred from 2 August 2026 to 2 December 2027 (S09). Transparency obligations under Article 50 were largely unaffected (S09). It matters to Ugandan firms with EU parents, partners or clients, and as a benchmark.

**Uganda.** There is no standalone AI law. The Data Protection and Privacy Act 2019 and Regulations 2021 are the most important instruments for AI that processes personal data, enforced by the PDPO (S05, S06). The Insurance Act 2017 and IRA regulations govern conduct, claims and intermediaries (S04, *verify*). The Ministry of ICT and National Guidance is developing a National Emerging Technologies Strategy covering AI, blockchain, cloud, big data and quantum computing, initiated by a Cabinet directive for an AI policy (S12); Uganda has publicly emphasised transparency, accountability and human oversight (S12). *Recheck strategy publication status before recording.*

### Sources

S04, S05, S06, S07, S08, S09, S12.

### Regulatory and operational considerations

Because Ugandan AI regulation is principles-based and still developing, insurers that align with IAIS topics and use NIST-style processes will be well placed when the IRA or PDPO issue more specific expectations.

### Realistic Ugandan scenarios

A Ugandan subsidiary of a pan-African insurer receives a group AI policy written to EU AI Act standards; the local compliance officer maps it to DPPA requirements and IRA expectations. A local broker with no group support uses the IAIS five topics as a simple checklist for its first AI tool.

*Validate through S16.*

### Common professional mistakes

Concluding that "there is no AI law in Uganda, so there are no obligations"; treating the EU AI Act as directly binding in Uganda; quoting outdated EU deadlines (2 August 2026) for high-risk obligations; treating frameworks as paperwork rather than operating practice.

### Recommended techniques

Use the IAIS five topics as the "what" and the NIST four functions as the "how"; map each to DPPA and Insurance Act obligations.

### Teaching examples and storyboard pointers

Four-layer pyramid (international supervision, practical frameworks, reference regulation, Ugandan law). Two-column mapping slide: IAIS topics × NIST functions. Timeline graphic of EU AI Act dates including the deferral.

### Assessment questions (10)

1. **Which of the following is one of the five topics in the IAIS AI Application Paper?**
   A) Investment returns B) Fairness, ethics and redress C) Branch expansion D) Agent commission
   **Answer: B.**

2. **IAIS Application Papers:**
   A) Create new binding requirements B) Explain how existing Insurance Core Principles apply, without new requirements C) Replace national law D) Apply only in Europe
   **Answer: B.**

3. **The four functions of the NIST AI RMF are:**
   A) Plan, Do, Check, Act B) Govern, Map, Measure, Manage C) Identify, Protect, Detect, Respond D) Collect, Store, Share, Delete
   **Answer: B.**

4. **Which statement about AI law in Uganda in 2026 is most accurate?**
   A) Uganda has a comprehensive AI Act B) There is no standalone AI law, but the DPPA, Insurance Act and IRA rules apply, and a national emerging technologies strategy is in development C) AI is unregulated D) AI is banned in financial services
   **Answer: B.**

5. **Under the May 2026 provisional agreement, EU AI Act obligations for stand-alone high-risk systems such as life and health insurance pricing apply from:**
   A) 2 August 2026 B) 2 December 2027 C) 1 January 2030 D) They were cancelled
   **Answer: B.** (S09)

6. **Is the EU AI Act directly binding on a purely Ugandan broker serving only Ugandan clients?**
   A) Yes B) No, but it is a useful benchmark and may affect firms with EU links
   **Answer: B.**

7. **Which Ugandan instrument is most important for AI systems processing personal data?**
   A) Traffic and Road Safety Act B) Data Protection and Privacy Act 2019 and its Regulations C) Companies Act only D) Income Tax Act
   **Answer: B.**

8. **In the NIST framework, testing a model's error rate on Ugandan document samples falls under:**
   A) Govern B) Map C) Measure D) Manage
   **Answer: C.**

9. **Appointing an accountable owner and approving an AI policy falls under which NIST function?**
   A) Govern B) Map C) Measure D) Manage
   **Answer: A.**

10. **A compliance officer says, "There's no AI law, so we don't need AI governance." The best response is:**
    A) Agree B) Existing laws and supervisory principles already apply to AI use, and aligning with IAIS and NIST prepares the firm for future rules C) Wait for the strategy D) Use only EU law
    **Answer: B.**

### Sources, dates and reuse

S04 (*verify*), S05 (PD), S06, S09, S12 (Facts), S07 (Quote), S08 (PD, *verify*). Original synthesis. Expert verification: pending — recommend legal review.

---

## Unit 4.3 — Assessing an AI use case end to end

**Learning outcome:** Complete an AI use-case assessment canvas and recommend whether and how to proceed.

**Video length target:** 12 minutes (worked example).

### Core concepts and terminology

The **AI Use-Case Assessment Canvas** pulls together everything in the course into one page that a professional can bring to their manager, risk team or board. It has ten boxes: (1) **Problem and baseline** — what is going wrong today, in numbers; (2) **Proposed AI solution** — category (rules, ML, GenAI, agent) and what it does; (3) **Users and affected people** — staff who use it, customers affected; (4) **Data** — what data, whose, where it is processed, DPPA basis; (5) **Autonomy and decision rights** — level per action, what is never automated; (6) **Value** — expected benefit and how it will be measured; (7) **Risks** — fairness, explainability, security, third-party, conduct, over-reliance; (8) **Controls** — verification steps, escalation triggers, audit trail, monitoring, stop mechanism; (9) **Governance mapping** — IAIS topics and NIST functions addressed; (10) **Recommendation and pilot plan** — proceed, proceed with conditions, or do not proceed; scope, duration, success criteria, owner, review date.

A use case is not "approved" by filling in the canvas; the canvas makes the reasoning visible so the right people can decide. Proportionality applies: a GenAI tool drafting internal notes needs a short canvas; an agent interacting with customers needs a full one.

### Sources

All prior sources; principally S07 (proportionality; governance and accountability), S08 (Map–Measure–Manage), S05.

### Regulatory and operational considerations

The canvas supports DPPA accountability (including any impact assessment the Regulations require for high-risk processing, *verify*) and demonstrates governance to the IRA. It should be stored with the approval decision and revisited at the review date.

### Worked example (for video)

Use case: WhatsApp claims-intake agent at a Ugandan general insurer. Problem: files take too long to complete because documents are missing; baseline average days from notification to complete file recorded over three months. Solution: agent at Level 2–3 for checklists and reminders, Level 0 for decisions. Data: names, phone numbers, policy numbers, vehicle registration; processed by a vendor — data location and terms reviewed with DPO. Risks: misreading documents, prompt injection via uploaded files, tone with distressed customers. Controls: handwritten documents routed to staff; no payment or decline authority; escalation on death claims, complaints, and threshold amounts; weekly 5% sample review; kill switch held by claims manager. Recommendation: proceed with a three-month pilot in one branch; success = fewer days to complete file with no rise in complaints.

### Common professional mistakes

Leaving the baseline blank, so benefits cannot be shown; listing risks without matching controls; no named owner; no review date; treating the canvas as a one-off form.

### Teaching examples and storyboard pointers

Build the canvas box by box on screen for the worked example. End with a downloadable blank canvas (Academy-designed) that learners will use in the final task.

### Assessment questions (10)

1. **Why does the canvas begin with "Problem and baseline"?**
   A) To fill space B) So that the benefit of the AI solution can be measured against today's performance C) Because regulators require it first D) To describe the vendor
   **Answer: B.**

2. **In the worked example, which action is set at Level 0 for the intake agent?**
   A) Sending document checklists B) Sending reminders C) Deciding whether to pay or decline a claim D) Creating a claim file
   **Answer: C.**

3. **Which control addresses the risk of the agent misreading handwritten documents?**
   A) A larger model B) Routing handwritten documents to staff C) More reminders D) Removing escalation
   **Answer: B.**

4. **A canvas lists "bias" as a risk but no corresponding control. What is the problem?**
   A) None B) Risks without matching controls give decision-makers no assurance C) Bias is not relevant to insurance D) Controls are optional
   **Answer: B.**

5. **Which recommendation option is appropriate when benefits are clear but data terms with the vendor are not yet confirmed?**
   A) Proceed immediately B) Proceed with conditions — confirm data terms before go-live C) Never proceed D) Proceed and review later
   **Answer: B.**

6. **Proportionality in the canvas means:**
   A) Every use case needs the same 40-page assessment B) The depth of assessment matches the potential impact of the use case C) Only large insurers need assessments D) Assessments are optional
   **Answer: B.**

7. **Which pilot success criterion is best?**
   A) "Staff liked it" B) Fewer days from notification to complete file, with no increase in complaints, over three months C) "The vendor was happy" D) Number of messages sent
   **Answer: B.**

8. **Who should hold the stop mechanism ("kill switch") in the worked example?**
   A) The vendor only B) A named business owner, such as the claims manager C) Any customer D) No one
   **Answer: B.**

9. **The "Governance mapping" box connects the use case to:**
   A) The marketing plan B) IAIS topics and NIST functions C) Staff leave schedules D) Premium tables
   **Answer: B.**

10. **After approval, the canvas should be:**
    A) Deleted B) Stored with the decision and revisited at the review date C) Sent to customers D) Published on social media
    **Answer: B.**

### Sources, dates and reuse

S05 (PD), S07 (Quote), S08 (*verify*). Canvas is an original Academy framework. Expert verification: pending.
