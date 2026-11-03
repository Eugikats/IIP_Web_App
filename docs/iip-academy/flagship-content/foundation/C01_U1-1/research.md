# Source research pack

1.1 — What AI, machine learning, generative AI and agentic AI actually are

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

