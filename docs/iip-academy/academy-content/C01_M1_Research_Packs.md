# Course 1 · Module 1 — Foundations: AI in Ugandan Insurance

## Research packs for Units 1.1–1.3

Source IDs (S01–S18) refer to the Master Research Register. Items marked *verify* must be checked against the primary source before narration is recorded.

---

## Unit 1.1 — What AI, machine learning, generative AI and agentic AI actually are

**Learning outcome:** Distinguish rules-based automation, predictive machine learning, generative AI and agentic AI, and classify an insurance tool correctly.

**Video length target:** 9–10 minutes.

### Core concepts and terminology

"Artificial intelligence" is used loosely in the industry, and most of the confusion professionals face comes from treating very different technologies as one thing. The unit therefore teaches four categories that learners can use to classify any tool a vendor, manager or colleague describes.

Rules-based automation follows instructions written explicitly by people: "if the vehicle is older than fifteen years, refer the proposal to a senior underwriter." It is predictable and easy to audit, but it cannot handle situations its authors did not anticipate and it does not learn. Robotic process automation (RPA) is a common form of this, where software repeats the clicks and keystrokes a person would make across systems.

Machine learning (ML) builds a model from historical data rather than from hand-written rules. A model trained on several years of motor claims can produce a score estimating how likely a new claim is to need investigation. ML is predictive: it classifies, scores or forecasts. Its quality depends entirely on the data it learned from, so gaps or biases in historical records carry forward into its predictions.

Generative AI (GenAI) produces new content — text, images, summaries, translations, code — based on patterns learned from very large datasets. Large language models (LLMs) are the generative systems most professionals now meet, through assistants that draft letters, summarise reports or explain policy wording. Their output is fluent but probabilistic, which means it can be confidently wrong.

Agentic AI uses a generative model as a "reasoning engine" that can plan several steps towards a goal, use tools (search a policy database, read an email, call a pricing system, send a message) and adjust its plan based on results. The difference from a chatbot is action: a chatbot answers, an agent does things. The IAIS notes that agentic AI is one of the new developments accelerating AI adoption in insurance (S07).

| Term | Plain-language definition | Insurance example |
|------|---------------------------|-------------------|
| Rules-based automation | Fixed if-then instructions written by people | Auto-referral of high-value proposals |
| RPA | Software that mimics human clicks across systems | Copying claim details from email into the core system |
| Machine learning | A model that learns patterns from historical data to predict or classify | Fraud-likelihood score on new claims |
| Generative AI / LLM | A model that produces new text or images | Drafting a claims acknowledgement letter |
| Agentic AI | A system that plans, uses tools and takes multi-step actions towards a goal | An agent that receives a WhatsApp claim notification, checks the policy, requests missing documents and opens a claim file |
| Model | The learned mathematical representation produced by training | — |
| Human in the loop | A person reviews or approves before an AI output takes effect | Claims officer approves agent's settlement recommendation |

### Sources

S07 (IAIS Application Paper, introduction — agentic AI as a new development), S10 (Evident Q2 2026 — agents used to summarise, route and accelerate workflows), S11 (ScienceSoft — distinction between agentic and non-agentic GenAI adoption), S08 (NIST AI RMF definitions, *verify*).

### Regulatory and operational considerations

No Ugandan law defines these categories, but the distinction matters in practice because the risks differ. Rules are transparent; ML scores may be hard to explain to a customer whose claim is delayed; generative output may contain fabricated facts; agents can take actions that bind the company. The Data Protection and Privacy Act 2019 contains a right relating to automated decision-taking (S05, *verify section*), which becomes relevant as soon as an ML model or agent makes or heavily shapes a decision about an individual.

### Realistic Ugandan scenarios

A bancassurance relationship manager at a Kampala bank is told by a vendor that their new "AI underwriting engine" will speed up credit-life approvals. On questioning, it turns out to be a rules table — useful, but not AI that learns, and it will need manual updating whenever the insurer's underwriting guidelines change.

A medical insurer's claims team adopts a GenAI assistant to summarise hospital invoices. One summary lists a procedure that does not appear on the invoice. The team realises the tool generates plausible text rather than extracting facts, and adds a check against the source document.

*Validate both scenarios through S16 interviews.*

### Common professional mistakes

Calling every automated system "AI", which hides important differences in risk; assuming GenAI "looks up" answers rather than generating them; assuming an agent is safe because each individual step seems harmless; and believing AI will replace professional judgment, when current practice shows insurers keep humans in charge of material decisions (S10).

### Recommended techniques

Teach the "four-question classifier": Does it follow fixed rules written by people? Does it learn from historical data to predict? Does it create new content? Does it take actions using tools? The first "yes" from the top gives the main category, and a tool may combine several.

### AI applications and limitations

Applications span every function, but each category has a characteristic limitation: rules are brittle, ML inherits data bias, GenAI fabricates, agents compound errors across steps.

### Teaching examples and storyboard pointers

Open with a vendor pitch slide full of buzzwords and ask, "Which of these is actually AI?" Use one diagram showing the four categories as a ladder of increasing capability and increasing need for oversight. Demonstrate by classifying five real-sounding Ugandan insurance tools live.

### Assessment questions (10)

1. **Which statement best describes generative AI?**
   A) It follows if-then rules written by programmers B) It produces new content such as text based on patterns learned from data C) It copies data between systems by mimicking clicks D) It stores policyholder records securely
   **Answer: B.** Generative AI creates new content; A describes rules-based automation, C describes RPA.

2. **A motor system automatically refers any claim from a policyholder with more than three claims in twelve months. This is best described as:**
   A) Machine learning B) Rules-based automation C) Generative AI D) Agentic AI
   **Answer: B.** The threshold was written by a person; the system does not learn.

3. **An insurer trains a model on five years of claims to score new claims for likely fraud. This is:**
   A) Predictive machine learning B) RPA C) Generative AI D) A rules table
   **Answer: A.** It learns patterns from historical data to produce a prediction.

4. **What most clearly distinguishes agentic AI from a chatbot?**
   A) It uses a larger model B) It can plan multiple steps and use tools to take actions towards a goal C) It speaks local languages D) It never makes mistakes
   **Answer: B.** Agents act; chatbots respond.

5. **A broker tells a client, "AI will soon approve all claims without anyone checking." Which response is most accurate?**
   A) Agree — agents already do this widely B) Explain that most insurers currently use agents to summarise, route and speed up work while keeping humans responsible for coverage and claims decisions C) Say AI cannot be used in claims at all D) Say only regulators may use AI
   **Answer: B.** Industry tracking in 2026 shows few insurers give agents material autonomy over claims decisions (S10).

6. **Which task is least appropriate to hand to a GenAI tool without human review?**
   A) Drafting a first version of a customer newsletter B) Summarising a meeting transcript for internal notes C) Issuing the final decision to repudiate a claim D) Suggesting headings for a training manual
   **Answer: C.** A repudiation affects the customer's rights and must rest on verified facts and accountable human judgment.

7. **A vendor's "AI underwriting engine" turns out to be a table of fixed referral rules. What is the main practical implication?**
   A) It will improve automatically over time B) It must be updated manually whenever underwriting guidelines change C) It will fabricate answers D) It requires DPPA registration while ML does not
   **Answer: B.** Rules do not learn; someone must maintain them.

8. **Why does the distinction between AI categories matter for an insurance professional?**
   A) Each category carries different risks and needs different controls B) Only agentic AI is legal in Uganda C) Rules-based systems are always more accurate D) It affects only IT staff
   **Answer: A.** Transparency, bias, fabrication and action risks differ by category.

9. **A claims assistant summarises a hospital invoice and lists a procedure that is not on the invoice. The most likely explanation is that:**
   A) The hospital changed the invoice B) The generative model produced plausible but unsupported text C) The rules table is outdated D) The agent sent the wrong email
   **Answer: B.** Generative models can fabricate details; summaries must be checked against the source.

10. **Which statement about AI output is true?**
    A) AI output is always deterministic and correct B) Confident wording from AI indicates accuracy C) AI output is probabilistic and can be wrong while sounding confident D) AI output cannot contain errors if the vendor is reputable
    **Answer: C.** Fluency is not evidence of accuracy.

### Sources, dates and reuse

S07 (Jul 2025, Quote), S08 (2023/2024, PD, *verify*), S10 (2026, Facts), S11 (2026, Facts). All explanatory text above is original synthesis. Expert verification: pending.

---

## Unit 1.2 — How large language models work, and why they get things wrong

**Learning outcome:** Explain in plain terms how an LLM produces answers and predict the situations in which it is likely to be unreliable.

**Video length target:** 10–11 minutes.

### Core concepts and terminology

An LLM is trained on a very large body of text to predict the next piece of text — a "token", roughly a word or part of a word — given everything before it. By repeating this prediction many times it produces sentences, paragraphs and documents. It does not consult a database of verified facts unless it has been connected to one; it produces the most plausible continuation based on patterns in its training.

Several practical consequences follow. **Hallucination** (NIST uses the term "confabulation", *verify* S08) is the production of fluent, confident content that is false or unsupported — a non-existent IRA circular, an invented clause number, a wrong premium calculation. The **knowledge cutoff** means the model's built-in knowledge stops at a training date, so recent regulation, new product rules or this year's tariff changes may be missing unless the tool can search or the user provides the text. The **context window** is how much text the model can consider at once; very long documents may be truncated or details in the middle may receive less attention. **Non-determinism** means the same prompt can give different answers on different attempts. **Arithmetic and counting** are weak points for language models working without a calculator tool. **Language coverage** varies: performance in English is usually stronger than in Luganda, Runyankore or Swahili, so translations of policy wording need native-speaker review.

Tools connected to search, document retrieval or company systems ("grounding" or "retrieval") reduce but do not eliminate these problems. A tool that claims to have "checked the IRA website" when it has no browsing capability is generating text, not reporting an action.

### Sources

S08 (NIST AI 600-1 — confabulation and information-integrity risks, *verify*), S07 (IAIS — robustness and transparency topics), S15 (gaps in Ugandan law on algorithmic accountability).

### Regulatory and operational considerations

An insurer remains accountable for what it tells customers regardless of whether a person or a model drafted it. Misstatements about cover, exclusions or claim timelines can amount to poor market conduct. The IAIS paper's transparency and explainability topic (S07) expects firms to understand and be able to explain AI-driven outcomes.

### Realistic Ugandan scenarios

An underwriting assistant asks an AI tool for "the current IRA requirements for motor third-party insurance" and receives a confident answer citing a circular number that cannot be found. The assistant learns to ask for the source text and to check the IRA website directly.

A customer service officer asks a tool to translate a funeral-cover exclusion into Luganda for a customer; a colleague who speaks Luganda notices the translation reverses the meaning of "not covered".

*Validate through S16.*

### Common professional mistakes

Treating the AI as a search engine; trusting citations without opening them; using AI-computed premiums, sums insured or settlement figures without recalculating; pasting a 150-page policy and assuming every clause was considered; assuming a polished answer means a correct one.

### Recommended techniques

Provide the source text and ask the model to answer only from it and to quote the clause it relied on; ask the model to state what it is unsure of; recalculate every number independently; break long documents into sections; use native-speaker review for any customer-facing translation.

### AI applications and limitations

Strong at drafting, restructuring, summarising provided text, generating options and explaining concepts. Weak at recent facts, exact citations, arithmetic without tools, and rare or local knowledge.

### Teaching examples and storyboard pointers

Animate next-token prediction with a sentence completing itself ("The claim was declined because the policy…"). Show a side-by-side of a hallucinated citation versus a grounded answer quoting the provided clause. Demonstrate asking the same question three times and getting three different wordings.

### Assessment questions (10)

1. **In simple terms, how does an LLM produce text?**
   A) It retrieves verified answers from a central database B) It predicts the most likely next piece of text, repeatedly C) It copies whole paragraphs from websites D) It runs the user's question through a rules table
   **Answer: B.**

2. **An AI tool cites "IRA Circular 14 of 2025" which cannot be found anywhere. This is an example of:**
   A) Knowledge cutoff B) Hallucination/confabulation C) Context window overflow D) Encryption failure
   **Answer: B.** The tool generated a plausible but non-existent reference.

3. **Why might an AI assistant give outdated information about a regulation issued last month?**
   A) Its knowledge stops at a training cutoff unless it can search or is given the text B) Regulators block AI tools C) LLMs cannot read English legal text D) The regulation is encrypted
   **Answer: A.**

4. **A colleague pastes a 200-page reinsurance treaty and asks for "all exclusions". What is the main risk?**
   A) The tool will refuse B) Some clauses may be missed or given less attention because of context limits C) The treaty will be published online D) The output will be in the wrong language
   **Answer: B.** Break the document into sections and check each.

5. **An AI tool calculates a pro-rata refund on cancellation. What should you do before sending it to the customer?**
   A) Send it — AI is good at maths B) Recalculate independently C) Ask the AI to confirm it is correct D) Round it up to be safe
   **Answer: B.** LLMs are unreliable at arithmetic without tools; asking the same model to "confirm" is not verification.

6. **Which prompt is most likely to reduce hallucination when asking about a policy exclusion?**
   A) "What does the policy exclude?" B) "Using only the clause text pasted below, state whether flood damage is excluded and quote the sentence you relied on." C) "Be accurate." D) "Answer like an expert underwriter."
   **Answer: B.** Grounding in provided text and requiring a quotation makes errors visible.

7. **The same question asked three times gives three different wordings. This reflects:**
   A) A software fault B) The non-deterministic nature of generative models C) Hacking D) A context window error
   **Answer: B.**

8. **An AI tool without internet access says, "I have checked the IRA website and confirmed this." How should you treat this?**
   A) As confirmation B) As generated text that does not prove any check took place C) As a legal opinion D) As a regulatory filing
   **Answer: B.**

9. **A Luganda translation of an exclusion is going to a customer. What is the best practice?**
   A) Send it as generated B) Have a fluent Luganda speaker review it against the English original C) Translate it back into English with the same tool and send if it matches D) Avoid informing the customer
   **Answer: B.** Back-translation by the same tool can hide errors.

10. **Which statement about grounding (connecting AI to search or documents) is correct?**
    A) It eliminates all errors B) It reduces errors but outputs still need checking C) It is illegal in Uganda D) It makes the model deterministic
    **Answer: B.**

### Sources, dates and reuse

S07 (Jul 2025, Quote), S08 (Jul 2024, PD, *verify*), S15 (Facts). Original synthesis. Expert verification: pending.

---

## Unit 1.3 — The AI opportunity across the insurance value chain

**Learning outcome:** Map AI applications to each stage of the insurance value chain and judge which are realistic for the Ugandan market today.

**Video length target:** 11–12 minutes.

### Core concepts and terminology

The insurance value chain runs from product design and pricing, through distribution and sales, underwriting, policy administration and servicing, to claims, fraud management and recoveries, with finance, compliance and reporting running alongside. AI can contribute at each stage, but value is concentrated where work is high-volume, document-heavy and repetitive, and where faster or more consistent decisions directly affect cost or customer experience.

Globally, the evidence for 2026 points the same way. Evident's tracking of thirty large insurers found that in Q1 2026 underwriting and pricing together with claims management made up the majority of newly announced use cases, with agentic deployments concentrated in claims and specialty underwriting (S10). In Q2 2026 the same group announced 37 new use cases, spreading across distribution, customer engagement and claims, with agentic AI accounting for 6 of the 37 (16%) and few insurers granting agents real authority over pricing, coverage or claims decisions (S10). ScienceSoft reports that customer service led agentic use cases in Q1 2026 (S11).

The Ugandan market context shapes what is realistic. Gross written premium exceeded UGX 2 trillion for the first time in 2025, reaching UGX 2.024 trillion — growth of 14.72% on 2024 (S01, S02). Life business drove much of the growth, rising 39.21% from UGX 702.2 billion to UGX 977.6 billion (S01). Insurers paid UGX 934.55 billion in gross claims, 46.2% of premiums (S02). Bancassurance is expanding, with the IRA licensing 22 financial institutions, up from 19 (S02). At the same time, penetration remains low relative to regional and global averages (S02), and the IRA has urged insurers to automate claims processing so that settlements are timely, while insurers such as AAR point to high technology costs and network challenges (S03). The IRA projects premium growth of about 10% in 2026, citing digital innovation and expanding distribution channels among the drivers (S01).

| Value-chain stage | Example AI application | Ugandan readiness note |
|-------------------|------------------------|------------------------|
| Product design | Analysing claims and enquiry patterns to design microinsurance or funeral products | Data often fragmented; start with internal claims data |
| Distribution & sales | GenAI drafting product explanations for bank customers; lead prioritisation | Strong fit with bancassurance growth |
| Underwriting | Summarising proposal documents, valuation reports and medical reports for the underwriter | Paper and scanned documents common; extraction quality must be tested |
| Policy servicing | Answering policy queries via WhatsApp with human escalation | High demand; language coverage needs testing |
| Claims | First notification of loss (FNOL) intake, document checklists, status updates, settlement drafts | Directly linked to IRA's timeliness expectations |
| Fraud | Flagging unusual patterns, repeat garages or providers | Requires clean historical data and human investigation |
| Compliance & reporting | Drafting regulatory returns narratives; monitoring complaints | Outputs must be verified line by line |

### Sources

S01, S02, S03, S10, S11, S14 (fraud and information-sharing challenges in claims settlement).

### Regulatory and operational considerations

The legal requirement to settle claims within a set period after complete documentation (reported as 60 days, S03; *verify section in S04*) gives claims automation a clear compliance motive. Any use case processing personal data must comply with DPPA 2019 (S05). Distribution use cases through banks involve two regulated parties and both sets of conduct expectations.

### Realistic Ugandan scenarios

A mid-sized general insurer receives most motor claim notifications through WhatsApp and phone calls. Missing documents are the main cause of delay. An AI-assisted intake checklist that tells the customer exactly what is still missing is a low-risk, high-value first use case.

A life insurer growing through bancassurance wants relationship managers to explain products consistently. A GenAI tool drafting plain-language explanations from approved product sheets, reviewed by compliance, reduces mis-selling risk.

*Validate through S16.*

### Common professional mistakes

Starting with the most impressive use case rather than the most valuable and safest; copying a foreign insurer's use case without checking local data quality and connectivity; measuring success by "using AI" rather than by cycle time, cost, complaint rates or customer satisfaction; ignoring the cost of change management and training.

### Recommended techniques

Prioritise use cases with a simple screen: high volume, repetitive, document-heavy, measurable, low harm if wrong, and data available. Start where AI assists a person rather than decides.

### AI applications and limitations

Applications are broad; limitations in Uganda include connectivity, cost, paper-based records, language diversity and limited historical data for model training.

### Teaching examples and storyboard pointers

Animated value-chain diagram lighting up stage by stage. One chart of Ugandan 2025 premium and claims figures (build our own chart from S01/S02 data; do not reuse news graphics). Close with the prioritisation screen applied to two candidate use cases.

### Assessment questions (10)

1. **According to 2026 industry tracking, which two functions accounted for most new AI use cases announced by large insurers in Q1 2026?**
   A) HR and finance B) Underwriting & pricing and claims management C) Marketing and legal D) Investments and reinsurance
   **Answer: B.** (S10)

2. **Uganda's insurance gross written premium in 2025 was approximately:**
   A) UGX 1.06 trillion B) UGX 1.76 trillion C) UGX 2.02 trillion D) UGX 9.3 trillion
   **Answer: C.** UGX 2.024 trillion (S01, S02).

3. **Gross claims paid in 2025 were 46.2% of gross written premium. What does this ratio indicate most directly?**
   A) The industry's profit margin B) The share of premium income paid back to policyholders as claims C) Insurance penetration D) The cost of reinsurance
   **Answer: B.** It is not a profit measure; expenses, reserves and investment income are separate.

4. **Which first AI use case best fits the prioritisation screen for a Ugandan motor insurer?**
   A) Fully automated claim repudiation B) An intake assistant that tells customers which documents are still missing C) A model that sets premiums with no human review D) Replacing all loss assessors
   **Answer: B.** High volume, measurable, low harm, assists rather than decides.

5. **The IRA has urged insurers to automate claims processing mainly to:**
   A) Reduce staff numbers B) Ensure timely settlement of claims C) Replace brokers D) Increase premiums
   **Answer: B.** (S03)

6. **Which local constraint was cited by a Ugandan health insurer as a challenge to automation?**
   A) Lack of customers B) High technology costs and network issues C) Prohibition by law D) Excess staff
   **Answer: B.** (S03)

7. **A bancassurance team wants consistent product explanations across bank branches. The most appropriate AI application is:**
   A) An agent that sells policies without staff involvement B) A GenAI tool drafting plain-language explanations from approved product sheets, reviewed by compliance C) A fraud model D) An RPA bot for premium collection
   **Answer: B.**

8. **Why might copying a foreign insurer's AI use case directly fail in Uganda?**
   A) Foreign AI is illegal B) Differences in data availability, document formats, connectivity and language C) Ugandan customers do not make claims D) Foreign insurers do not use AI
   **Answer: B.**

9. **Which is the best measure of success for a claims AI pilot?**
   A) Number of staff who tried the tool B) Reduction in average days from last document to settlement, with complaint rates monitored C) How advanced the model is D) Number of prompts written
   **Answer: B.** Measure business outcomes, not activity.

10. **In Q2 2026, agentic AI accounted for about what share of newly announced insurer use cases tracked by Evident?**
    A) 2% B) 16% C) 50% D) 90%
    **Answer: B.** 6 of 37 (S10).

### Sources, dates and reuse

S01 (26 Jun 2026, Facts), S02 (2026, Facts), S03 (2025, Facts; legal requirement *verify* in S04), S10 (2026, Facts), S11 (2026, Facts), S14 (2017, Facts). Figures recomputed (see register §5). Expert verification: pending.
