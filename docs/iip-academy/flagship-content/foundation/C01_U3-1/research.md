# Source research pack

3.1 — What makes a system agentic

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

