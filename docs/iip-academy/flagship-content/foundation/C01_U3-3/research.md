# Source research pack

3.3 — Designing human oversight for AI agents

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
