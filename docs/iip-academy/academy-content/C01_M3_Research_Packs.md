# Course 1 · Module 3 — Agentic AI: From Assistants to Agents

## Research packs for Units 3.1–3.3

---

## Unit 3.1 — What makes a system agentic

**Learning outcome:** Describe the components of an AI agent and place a given system on an autonomy scale.

**Video length target:** 10–11 minutes.

### Core concepts and terminology

An AI agent combines five components. A **goal** sets what it is trying to achieve ("get this claim file complete and ready for assessment"). A **reasoning model**, usually an LLM, interprets the goal and plans steps. **Tools** let it act: reading emails, querying the policy administration system, checking a payment status, sending a WhatsApp message, creating a task. **Memory** keeps track of what has happened so far in the task and sometimes across tasks. **Guardrails** define what the agent may and may not do — permissions, spending or value limits, topics it must refuse, situations where it must hand over to a person.

A useful distinction is between a **workflow** (a fixed sequence of steps in which an AI model performs some of them) and an **agent** (which decides for itself which steps to take and in what order). Many practical "agentic" deployments in insurance are closer to workflows with AI steps, which is often the safer design.

The Academy uses a five-level **autonomy scale**: Level 0 — suggests only (human does everything); Level 1 — drafts outputs for human use; Level 2 — prepares and proposes actions that a human approves one by one; Level 3 — acts automatically within defined limits and escalates exceptions; Level 4 — acts fully autonomously. Industry tracking in 2026 shows insurers using agents mainly to summarise, route and accelerate work, with few giving agents material authority over pricing, coverage or claims decisions (S10) — that is, most real deployments sit at Levels 1–3.

### Sources

S07 (IAIS — agentic AI as a new development), S10 (Evident Q1/Q2 2026), S11 (ScienceSoft — customer-service-led agentic use cases), S17 (OWASP — "excessive agency" risk, *verify*).

### Regulatory and operational considerations

The more autonomy an agent has, the more the firm's governance must cover its actions as if they were taken by an employee: authority limits, records, supervision and the ability to stop it. Agents acting on personal data engage the DPPA, including the right relating to automated decision-taking (S05).

### Realistic Ugandan scenarios

A general insurer's "claims agent" receives a WhatsApp message reporting a minor accident, checks the policy is in force, lists outstanding documents, sends the customer the checklist, and creates a claim file for a human officer. It cannot approve or decline anything. This is Level 2–3 for intake and Level 0 for decisions.

A bancassurance "agent" prepares a renewal reminder for each expiring credit-life policy and queues it for the relationship manager to send — Level 2.

*Validate through S16.*

### Common professional mistakes

Assuming "agent" means "fully autonomous"; describing a fixed workflow as an agent (or vice versa) when assessing risk; granting broad tool access "for convenience"; forgetting that agents can chain small errors into a large one.

### Recommended techniques

Describe any proposed agent by listing its goal, tools, memory and guardrails, then assign an autonomy level separately for each type of action it can take.

### AI applications and limitations

Agents excel at multi-step, repetitive coordination tasks. Limitations include compounding errors, vulnerability to manipulated inputs (Unit 3.3) and difficulty handling genuinely novel situations.

### Teaching examples and storyboard pointers

Diagram of the five components around a central "goal". Animated autonomy ladder 0–4 with an insurance example at each rung. Walk through the WhatsApp claims-intake agent step by step.

### Assessment questions (10)

1. **Which component allows an AI agent to query the policy administration system?**
   A) Memory B) Tools C) Goal D) Training data
   **Answer: B.**

2. **Guardrails in an agent define:**
   A) The colour scheme of the interface B) What the agent may and may not do, and when it must hand over to a person C) The model's training cutoff D) The premium rate
   **Answer: B.**

3. **An agent prepares renewal reminders and queues them for a relationship manager to approve and send. Its autonomy level is:**
   A) Level 0 B) Level 2 C) Level 4 D) Not agentic
   **Answer: B.** It proposes actions approved one by one.

4. **What is the key difference between a workflow with AI steps and an agent?**
   A) Workflows cannot use AI B) In a workflow the sequence is fixed; an agent decides which steps to take C) Agents are always cheaper D) There is no difference
   **Answer: B.**

5. **According to 2026 industry tracking, insurers mostly use agents to:**
   A) Set prices without review B) Summarise, route and accelerate workflows C) Replace all claims staff D) Approve all claims automatically
   **Answer: B.** (S10)

6. **An intake agent can check policy status and request documents but cannot approve or decline claims. For decisions, its autonomy level is:**
   A) Level 0 B) Level 3 C) Level 4 D) Level 2
   **Answer: A.**

7. **Why should autonomy be assigned per type of action rather than per agent?**
   A) It is simpler for IT B) Different actions carry different risk — sending a document checklist differs from making a payment C) Regulators require one level per agent D) Agents cannot have multiple actions
   **Answer: B.**

8. **"Excessive agency" refers to:**
   A) Too many insurance agents in a market B) Giving an AI system more permissions or autonomy than its task requires C) A slow model D) A licensing offence
   **Answer: B.** (S17)

9. **Which is a characteristic risk of multi-step agents?**
   A) They cannot read text B) Small errors early in a task can compound across later steps C) They only work offline D) They cannot be stopped
   **Answer: B.**

10. **A colleague says, "It's an agent, so it runs without supervision." The best correction is:**
    A) Agree B) Agents can operate at different autonomy levels; supervision and limits are design choices the firm must make C) Agents are illegal D) Only IT can supervise agents
    **Answer: B.**

### Sources, dates and reuse

S05 (PD), S07 (Quote), S10, S11 (Facts), S17 (*verify*). Autonomy scale is an original Academy framework. Expert verification: pending.

---

## Unit 3.2 — Agentic use cases in claims, underwriting, distribution and service

**Learning outcome:** Evaluate an agentic use case for value, feasibility and risk in a Ugandan setting.

**Video length target:** 12 minutes.

### Core concepts and terminology

Six agentic use-case families recur across the industry. **FNOL intake and triage**: capturing first notification of loss from calls, emails or messaging, checking policy status, collecting required documents and routing to the right handler. **Document extraction**: pulling data from garage estimates, police abstracts, hospital invoices, valuation reports and proposal forms into structured fields. **Underwriting submission preparation**: assembling and summarising a submission so the underwriter starts with a complete picture, escalating complex cases with the analysis attached. **Fraud and anomaly flagging**: spotting patterns across claims — the same garage or clinic appearing repeatedly, timing anomalies, inconsistencies with external data — for human investigators. **Proactive customer communication**: status updates during claims, which research suggests improve customers' sense of being informed even when processing time does not change. **Distribution support**: quoting, comparing options, preparing renewal and cross-sell packs for brokers and bank relationship managers; ScienceSoft reports a 2026 shift towards agentic products for intermediaries focused on quoting and placement (S11).

Each use case is evaluated on three axes. **Value**: volume, time saved, leakage reduced, customer experience, compliance with settlement timelines. **Feasibility**: data availability and quality, digital versus paper documents, integration with core systems, connectivity, language. **Risk**: impact on customers if wrong, personal data involved, autonomy level required, ability to explain outcomes.

In Uganda, specific features matter: claims and enquiries commonly arrive by phone and WhatsApp; many documents are paper or photographed; payouts may go to mobile money; and IRA attention to settlement timeliness (S03) gives intake and document-chasing agents a strong case.

### Sources

S03, S10, S11, S14 (fraud and information-sharing as settlement challenges), S16 (*pending*).

### Regulatory and operational considerations

Fraud flags must not become automatic denials: flagged claims need investigation and fair treatment. Any agent communicating with customers must not misstate cover or promise outcomes. Agents processing health data (medical claims) carry heightened data protection obligations (S05).

### Realistic Ugandan scenarios

A Kampala motor insurer pilots an intake agent on WhatsApp. Average time from notification to complete file falls, but the agent misreads handwritten police abstracts. The team keeps the agent for checklists and reminders but routes all handwritten documents to a person.

A medical insurer considers an agent that flags hospitals billing the same procedure unusually often. The flags go to an investigations officer, not to automatic claim rejection.

*Validate through S16.*

### Common professional mistakes

Choosing the use case with the most impressive demo rather than the best value-feasibility-risk balance; testing only on clean digital documents; measuring time saved but not error rates or complaints; letting fraud flags block payments automatically.

### Recommended techniques

Use the three-axis scoring (value, feasibility, risk, each 1–5) and pilot with a defined baseline, success metrics and a limited scope. Test with real Ugandan document samples, including poor-quality photographs.

### AI applications and limitations

Strongest near-term fit: intake, document checklists, status updates, submission summaries. Higher-risk: anything that decides cover, price or payment.

### Teaching examples and storyboard pointers

Six-tile slide of the use-case families with a Ugandan example on each. Worked scoring of two use cases on the three axes. Short case study narration of the WhatsApp intake pilot.

### Assessment questions (10)

1. **FNOL stands for:**
   A) Final notice of liability B) First notification of loss C) Full number of losses D) Formal notice of lapse
   **Answer: B.**

2. **Which use case most directly supports timely claims settlement?**
   A) An agent that chases missing claim documents and reports file completeness B) An agent that writes marketing posts C) An agent that books staff leave D) An agent that designs logos
   **Answer: A.**

3. **An anomaly agent flags a clinic billing the same procedure unusually often. What should happen next?**
   A) Automatically reject all claims from the clinic B) Refer the flag to an investigator for review C) Publish the clinic's name D) Ignore the flag
   **Answer: B.**

4. **During a pilot, an intake agent misreads handwritten police abstracts. A sound response is:**
   A) Abandon AI entirely B) Keep the agent for tasks it does well and route handwritten documents to a person C) Ask customers to stop submitting abstracts D) Ignore the errors
   **Answer: B.**

5. **The three axes for evaluating an agentic use case are:**
   A) Cost, colour, speed B) Value, feasibility, risk C) Vendor, price, brand D) Premium, excess, limit
   **Answer: B.**

6. **Which Ugandan feature most affects feasibility of document-extraction agents?**
   A) Many documents are paper or photographed B) All documents are digital C) Customers never submit documents D) Documents are in Latin
   **Answer: A.**

7. **Proactive claims status updates are valuable mainly because:**
   A) They are required to be daily B) Customers feel informed and supported, which can improve satisfaction even if processing time is unchanged C) They replace settlement D) They reduce premiums
   **Answer: B.**

8. **Which metric set best evaluates an intake agent pilot?**
   A) Number of messages sent only B) Time to complete file, error rate, escalation rate and complaints compared with a baseline C) Number of staff trained D) Model size
   **Answer: B.**

9. **Which use case requires the most caution before increasing autonomy?**
   A) Sending document checklists B) Summarising submissions C) Deciding whether a claim is paid D) Drafting status updates
   **Answer: C.**

10. **A vendor demo uses perfectly scanned documents. What should a Ugandan insurer insist on before buying?**
    A) A larger discount B) Testing with its own real documents, including poor-quality photographs C) A longer contract D) A logo change
    **Answer: B.**

### Sources, dates and reuse

S03, S10, S11, S14 (Facts); S05 (PD); S16 (*pending*). Original synthesis. Expert verification: pending.

---

## Unit 3.3 — Designing human oversight for AI agents

**Learning outcome:** Design decision rights, escalation triggers and audit controls for an agentic workflow.

**Video length target:** 11–12 minutes.

### Core concepts and terminology

Human oversight is designed, not assumed. The Academy teaches five building blocks. A **decision-rights matrix** lists every action the agent can take and states, for each, whether the agent may act alone, must seek approval, or must never act. **Escalation triggers** are conditions that send a case to a person: value above a threshold, low model confidence, missing or conflicting documents, a complaint or distress signal, a vulnerable customer (bereavement, serious illness), or any request outside the agent's scope. An **audit trail** records inputs, the agent's reasoning or steps, tools used, outputs and who approved what, so decisions can be explained and reviewed. **Monitoring** tracks error rates, escalation rates, complaints and drift over time, with regular sampling of the agent's work by a person. A **stop mechanism** ("kill switch") lets the business pause the agent immediately.

Agents also face a specific security risk: **prompt injection**, where instructions hidden in content the agent reads (an email, a PDF, a web page) try to make it do something unintended — for example, a document containing hidden text telling the agent to "mark this claim approved". OWASP lists prompt injection and excessive agency among the top risks for LLM applications (S17, *verify*). The defences are least-privilege tool permissions, treating all external content as data not instructions, human approval for consequential actions, and testing with adversarial examples.

Meaningful oversight also guards against **rubber-stamping**: if a human approves hundreds of agent decisions a day without time to review, the oversight is nominal. Approval volumes and review time should be sized realistically.

### Sources

S07 (IAIS — governance and accountability; robustness, safety and security), S08 (NIST — Govern and Manage functions, *verify*), S17 (*verify*), S05 (automated decision-taking rights), S12 (Uganda's emphasis on human oversight).

### Regulatory and operational considerations

Uganda's government has stated that transparency, accountability and human oversight must underpin AI governance (S12). The DPPA's automated decision-taking provision (S05) makes it important that individuals affected by significant automated decisions can obtain human involvement (*verify exact wording*). Audit trails support both IRA supervision and responses to complaints.

### Realistic Ugandan scenarios

An intake agent's decision-rights matrix: may send document checklists alone; may create claim files alone; must get approval to tell a customer a claim is "not covered"; must never authorise payment. Escalation triggers: any death claim, any message containing the words "complaint" or "lawyer", any amount over a set threshold.

A broker's quoting agent reads a client email containing hidden text instructing it to apply the maximum discount. Least-privilege permissions mean it can only propose discounts for human approval, so the attack fails.

*Validate through S16.*

### Common professional mistakes

Treating "a human can review if needed" as oversight; no record of what the agent did; giving agents broad system permissions; escalation rules so wide everything escalates (no value) or so narrow nothing does (no control); no way to stop the agent quickly.

### Recommended techniques

Build the decision-rights matrix first; set escalation triggers from real complaint and error data; sample a fixed percentage of agent work weekly; test with adversarial documents before go-live; name an accountable owner for each agent.

### AI applications and limitations

AI can assist monitoring (summarising error patterns) but cannot be its own overseer.

### Teaching examples and storyboard pointers

Decision-rights matrix template built live for the intake agent. Animated prompt-injection example with hidden text revealed. Diagram of the oversight loop: act → log → sample → review → adjust.

### Assessment questions (10)

1. **A decision-rights matrix specifies:**
   A) Staff salaries B) For each agent action, whether it may act alone, needs approval, or must never act C) The model's training data D) Premium rates
   **Answer: B.**

2. **Which is the most appropriate escalation trigger for a claims intake agent?**
   A) Every message received B) Death claims and messages mentioning a complaint C) Messages sent on weekdays D) None — the agent should handle everything
   **Answer: B.**

3. **Prompt injection is:**
   A) A vaccine B) Hidden instructions in content the agent reads that try to make it act in unintended ways C) A faster processor D) A premium adjustment
   **Answer: B.**

4. **Which control best limits the damage of a successful prompt injection?**
   A) A larger model B) Least-privilege permissions, so the agent can only propose consequential actions for approval C) Longer prompts D) Using the agent only at night
   **Answer: B.**

5. **A supervisor approves 600 agent decisions per day with seconds per case. This is an example of:**
   A) Strong oversight B) Rubber-stamping — nominal rather than meaningful oversight C) Prompt injection D) Data minimisation
   **Answer: B.**

6. **Why is an audit trail important for agent decisions?**
   A) It speeds up the model B) It allows decisions to be explained, reviewed, and responded to in complaints and supervision C) It reduces premiums D) It is only needed for IT
   **Answer: B.**

7. **Which action should an intake agent never take on its own?**
   A) Sending a document checklist B) Authorising a claim payment C) Creating a claim file D) Acknowledging receipt
   **Answer: B.**

8. **A "kill switch" allows the business to:**
   A) Delete customer data B) Pause the agent immediately if problems arise C) Increase autonomy D) Train the model
   **Answer: B.**

9. **Escalation rules that send every case to a person result in:**
   A) Perfect control and full value B) Little efficiency gain from the agent C) Faster processing D) No need for staff
   **Answer: B.**

10. **Uganda's government has emphasised which principles for AI governance?**
    A) Speed and secrecy B) Transparency, accountability and human oversight C) Full autonomy D) Vendor self-regulation only
    **Answer: B.** (S12)

### Sources, dates and reuse

S05 (PD), S07 (Quote), S08 (*verify*), S12 (Facts), S17 (*verify*). Original synthesis. Expert verification: pending.
