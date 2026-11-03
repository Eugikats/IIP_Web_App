# Enterprise Agentic AI Program — Course Reference

Everything below was pulled from the **live production database** via the backend
API on **1 September 2026**, not from marketing copy or assumption.

- Source: `GET https://y6yk9gp7zb.us-east-1.awsapprunner.com/api/agentic/courses/faa47f0f-d5e6-42a4-979f-a1453e2ca58a/`
- Course last updated in the database: **2026-09-01T12:43:52Z**

If someone asks something not answered here, the answer is not in the course
record and should be treated as undecided rather than improvised.

> **Read the "Where the landing page and the course disagree" section before
> answering questions.** Several things the landing page promises do not match
> what is actually built. That is the single biggest cause of questions you
> cannot answer.

---

## 1. The short answers

| Question | Answer |
|---|---|
| Official title in the system | **Enterprise Agentic AI Program** |
| Price | **UGX 500,000** (`500000.00`, platform currency UGX) |
| Free? | No |
| Structure | **12 modules, 68 lessons** |
| Stated duration | **24 hours** of estimated effort |
| Difficulty level | **Beginner** |
| Prerequisites | **None.** "You do not need a technical background." |
| Instructor of record | Otic Institute of Emerging Technologies |
| Category | Agent Builder |
| Published | Yes |
| Videos | **None.** 0 of 68 lessons have video. It is a written course plus practical sandbox work. |
| AI tutor | Enabled on **all 68 lessons** |
| Certificate | Yes, issued on completion |
| Ratings so far | 0 reviews |

**Is it organised in weeks?** **No.** There is no "Week 1 / Week 2 / Week 3" in
the course. It is Course Orientation followed by Module 1 through Module 11.
See §5.

---

## 2. What the learner actually builds

One continuous project called the **Professional Copilot**, carried across the
whole course. In the course's own words:

> You will create a Professional Copilot and evolve it across the course. It can
> serve finance, marketing, engineering, education, operations, public service,
> entrepreneurship, or another field. By the end, it should accept a meaningful
> input, reason within defined boundaries, use knowledge or tools where
> appropriate, return a structured output, handle failures safely, and pass a
> documented test set.

Crucially, this is **not a set of disconnected labs**:

> This is one continuous build, not a collection of unrelated labs. In Module 1
> you create one sandbox project. Every later module opens that same project,
> adds the component just taught, tests the new behavior without breaking
> earlier behavior, and saves a numbered checkpoint.

Every practical exercise follows the same five steps:

1. **Open your current build** at the latest successful checkpoint.
2. **Add the new component** — only the one taught in that module.
3. **Run focused tests** — new cases plus regression cases for earlier behaviour.
4. **Fix and explain** — inspect the trace, improve, record what changed.
5. **Save the checkpoint** — becomes the starting point for the next module.

---

## 3. Stated learning outcomes

Verbatim from the orientation module. By the end a learner should be able to:

- Explain what makes a system agentic.
- Translate a real problem into an agent goal and scope.
- Write clear system instructions and output contracts.
- Manage context, knowledge, and memory deliberately.
- Design sequential, conditional, and iterative workflows.
- Connect tools and external information safely.
- Build and debug a multi-step agent.
- Evaluate quality, safety, reliability, latency, and cost.
- Specialize an agent for a practical field.
- Document and demonstrate a completed solution.

The course also states it is deliberately vendor-neutral:

> You will not be trained to depend on one vendor, interface, or model. Products
> change quickly; the underlying components change much more slowly.

The components taught: goals, instructions, models, context, memory, knowledge,
tools, workflows, guardrails, evaluation, observability, human oversight.

---

## 4. What exact tools are taught

This question has **two accurate answers**, and they differ. Use both.

### 4a. What the curriculum says (Lesson 8.2, "Safe built-in learning tools")

The lesson states the sandbox *can provide*:

- Calculator
- Date and time utility
- Text extractor for approved file types
- Local knowledge search
- Mock web search over a curated dataset
- Mock spreadsheet read and append
- Mock email draft and send preview
- HTTP request to an allowlisted teaching endpoint
- Human approval

with the caveat: "All write-like actions should be simulated in the initial release."

### 4b. What is actually implemented in the sandbox today

Ten tools are registered in the agent sandbox (`agent_sandbox/tools.py`):

| Tool | What it does |
|---|---|
| `calculator` | Evaluate a basic arithmetic expression (`+ - * /` and parentheses) |
| `datetime` | Return the current UTC time in ISO-8601 |
| `set_variable` | Store a named value in conversation memory |
| `clear_variable` | Remove a named value from conversation memory |
| `redirect_topic` | Jump to another topic start in the same turn |
| `http_request` | HTTP request, restricted to an allowlisted host |
| `expense_log` | Append an expense entry to memory and workspace |
| `list_expenses` | List recent expenses from memory or workspace |
| `send_email` | Email the authenticated learner (their account address only) |
| `schedule_reminder` | Schedule a reminder |

**Safe phrasing when asked:** "The course teaches tool-calling as a concept —
how to define a tool, describe it, validate arguments, and handle tool errors.
The sandbox ships ten working tools including a calculator, date/time, memory
variables, an allowlisted HTTP request, expense logging, email to yourself, and
reminders. Write-like actions are simulated by design, so nothing touches real
systems."

**What is taught conceptually about tools** (Lesson 8.1): a tool is a function
with a name, description, input schema, and result; the model *proposes* a call
and the application validates authorization and arguments before executing.
Explicitly: "Never let a model directly construct unrestricted database queries
or shell commands in a learner sandbox."

---

## 5. The exact order, module by module

This is the authoritative sequence as stored in the database. Numbers in
brackets are lesson counts. Every module after Orientation ends with a
**Cumulative Sandbox Build**.

### Course Orientation [5 lessons]
1. Welcome
2. What you will build
3. How the cumulative sandbox works
4. Learning outcomes
5. Before you begin

### Module 1: Understanding Agentic AI [7]
1.1 From prediction to action · 1.2 Assistant, automation, workflow, and agent ·
1.3 The agent loop · 1.4 Core components · 1.5 Good and poor use cases ·
Reflection · **Cumulative Sandbox Build 1: Create and inspect your project**

### Module 2: Vibe Coding and Problem Definition [5]
2.1 Natural language as an interface to building · 2.2 Begin with the problem,
not the agent · 2.3 Scope and non-goals · 2.4 Convert an idea into a system ·
**Cumulative Sandbox Build 2: Give the agent a defined job**

### Module 3: Agent Architecture Basics [6]
3.1 Inputs and validation · 3.2 Decision logic · 3.3 Actions and outputs ·
3.4 A reference architecture · 3.5 Failure paths ·
**Cumulative Sandbox Build 3: Build the controlled skeleton**

### Module 4: Prompt Engineering for Agents [5]
4.1 Prompts are operating instructions · 4.2 Write observable instructions ·
4.3 Structured outputs · 4.4 Injection-resistant prompting ·
**Cumulative Sandbox Build 4: Replace vague instructions**

### Module 5: Context, Knowledge, and Memory [6]
5.1 Three concepts that are often confused · 5.2 Working memory ·
5.3 Long-term memory · 5.4 Retrieval-augmented generation ·
5.5 Context window strategy ·
**Cumulative Sandbox Build 5: Add knowledge, context, and memory**

### Module 6: Agent Behavior and Human Control [5]
6.1 Behavior is policy, not decoration · 6.2 Confidence and uncertainty ·
6.3 Human-in-the-loop patterns · 6.4 Least privilege ·
**Cumulative Sandbox Build 6: Add behavior rules and human control**

### Module 7: Workflow Design and Automation [6]
7.1 Sequential workflows · 7.2 Conditional workflows · 7.3 Iteration and loops ·
7.4 Triggers and actions · 7.5 State machines ·
**Cumulative Sandbox Build 7: Turn the agent into a workflow**

### Module 8: Tools, APIs, Documents, and Data [6]
8.1 Tool calling · 8.2 Safe built-in learning tools · 8.3 API fundamentals ·
8.4 Files and spreadsheets · 8.5 Tool errors ·
**Cumulative Sandbox Build 8: Add controlled tools**

### Module 9: Multi-Step Agents [5]
9.1 Planner and executor · 9.2 Single-agent versus multi-agent ·
9.3 Reflection and verification · 9.4 Stop conditions and budgets ·
**Cumulative Sandbox Build 9: Add bounded planning**

### Module 10: Testing, Evaluation, and Debugging [6]
10.1 Evaluation begins before deployment · 10.2 What to measure ·
10.3 Build an evaluation set · 10.4 Debug from traces · 10.5 Red-team testing ·
**Cumulative Sandbox Build 10: Evaluate and debug the agent**

### Module 11: Responsible Deployment and Operations [6]
11.1 Risk classification · 11.2 Privacy and security · 11.3 Transparency ·
11.4 Observability · 11.5 Release lifecycle ·
**Cumulative Sandbox Build 11: Make the project release-ready**

---

## 6. Assessment and progression rules

Identical across every module, straight from the database:

| Rule | Value |
|---|---|
| Must complete all lessons in a module | **Yes** (all 12 modules) |
| Module quiz required | **Yes** (all 12 modules) |
| AI simulation required | **Yes** (all except Course Orientation) |
| Quiz questions per module | **3** |
| Quiz pass mark | **70%** |
| Quiz time limit | **20 minutes** |
| Quiz attempts | **Up to 3** |
| AI simulation questions | **3** |
| AI simulation pass mark | **70%** |
| Anti-cheat | Enabled |

The AI simulation is a facilitated conversation, not a multiple-choice test. Its
persona is set to:

> You are an expert Agent Builder facilitator helping the learner complete their
> cumulative Professional Copilot sandbox build. Ask probing questions about
> traces, tests, and checkpoints. Never request real secrets or production data.

There is **no final mega-quiz** configured — assessment is per module.

---

## 7. Cost and payment

| | |
|---|---|
| Price | **UGX 500,000** |
| Stored value | `500000.00` |
| Currency | UGX (platform default) |
| Free tier | No |
| Payment provider | Flutterwave (production keys active) |
| Payment methods | Card and mobile money |
| Access model | Enrolment is created on confirmed payment; access does not expire |
| Refunds | **Not defined anywhere in the system.** Do not promise a refund policy. |

For context, the other two published courses are the same price:

| Course | Price | Lessons |
|---|---|---|
| Enterprise Agentic AI Program | UGX 500,000 | 68 |
| Engineering Design and Innovation with Autonomous AI | UGX 500,000 | 13 |
| Agentic Marketing Strategist | UGX 500,000 | 13 |

This one has **roughly five times the lesson count** of the other two at the same
price, which is a genuinely strong selling point.

---

## 8. Where the landing page and the course disagree

These are real mismatches between
`https://oticlearn.com/programs/enterprise-agentic-ai` and the course in the
database. **This is most likely why you keep getting questions you cannot answer.**

| Topic | Landing page says | The course actually is |
|---|---|---|
| **Title** | "Executive Programme in Enterprise Agentic AI" | "Enterprise Agentic AI Program" |
| **Structure** | "Week 1 / Week 2 / Week 3" | 12 modules, no weeks anywhere |
| **Duration** | "Three weeks, self-paced" | 24 estimated hours, no schedule stored |
| **Week 1 content** | "Foundations of Agentic AI … closes with an introduction to vibe coding" | Vibe coding is **Module 2**, the second module |
| **Week 2 content** | "Designing Agent Workflows" | Workflows are **Module 7** of 12 |
| **Week 3 content** | "Applying Agentic AI to Your Organisation" | No such module. Course ends with Testing/Evaluation (10) and Responsible Deployment (11) |
| **Deliverable** | "an AI agent concept mapped to a real workflow" | A **working, tested Professional Copilot**, which is considerably stronger |
| **Cohorts** | "Reserve your place in the next cohort" | No cohort, intake, or start date exists in the system. Enrolment is immediate on payment |

### How to answer the "are you sure about Week 1, 2, 3?" question

**You are not sure, because the course is not organised that way.** The honest,
confident answer:

> "The programme is structured as 12 modules rather than fixed weeks — an
> orientation plus eleven modules, taken in order. It is self-paced, and we
> estimate about 24 hours of work in total, which most people spread over about
> three weeks. You build one project, a Professional Copilot, continuously
> across all of them."

That is accurate, keeps the three-week framing as guidance rather than a
promise, and does not contradict the product.

### Two things worth fixing

1. **The landing page week-by-week section is inaccurate.** It describes a
   three-week structure that does not exist and places vibe coding and workflows
   in the wrong sequence.
2. **The title mismatch is an actual bug**, not just copy. Two places key off the
   exact string `'Executive Programme in Enterprise Agentic AI'`:
   - `CoursesPage.jsx` → `CUSTOM_LANDING_PAGES`, which decides whether the course
     card links to the landing page
   - `EnterpriseAgenticAILandingPage.jsx` → `COURSE_TITLE`, used to resolve the
     course ID for the "Reserve Your Place" button and the payment email

   The real title is `'Enterprise Agentic AI Program'`, so **neither lookup
   matches**. The course card does not link to the landing page, and the ID
   lookup silently falls back to its hardcoded default. It works today only
   because that fallback ID happens to be correct.

---

## 9. Content volume, stated plainly

So you are not caught out: the written lesson text totals roughly **5,400 words**
(~27 minutes of reading). The 24-hour estimate is therefore **overwhelmingly
practical work** — the eleven cumulative sandbox builds, the quizzes, and the AI
simulations — not reading.

If asked "is it just 27 minutes of reading?", the accurate framing is:

> "The reading is deliberately concise. The bulk of the time is hands-on: eleven
> cumulative builds where you extend the same agent, test it, debug from traces,
> and save a checkpoint, plus a quiz and an AI-facilitated review per module."

---

## 10. Things genuinely not defined anywhere

Do not invent answers to these. They are absent from the course record:

- Refund or cancellation policy
- Cohort dates, intakes, or enrolment deadlines
- Any deadline or expiry on course access
- Live sessions, office hours, or scheduled contact time
- Named human instructors (the instructor of record is the institute itself)
- Accreditation body for the certificate
- Support response times