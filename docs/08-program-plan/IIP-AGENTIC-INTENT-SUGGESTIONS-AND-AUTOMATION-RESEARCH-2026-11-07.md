# Agentic AI with intent for IIP: suggestions, automations and how to add them

Status: Research and proposal, not a contract. Nothing here is built. Last reviewed 2026-11-07.
Audience: the product owner and the engineers who would build it.
Decisions needed from the owner are listed in section 9.

## 1. The short version

IIP already has most of the safe foundations: sixteen governed agents, a kill switch, typed tools with side-effect levels, a graph workflow engine that forces a human review before any consequential step, an approve-once standing rule model for outreach, a durable background queue, and a chat that turns instructions into proposals. What it lacks is the connective layer that makes it feel agentic:

1. **Intent is narrow.** The Command Center understands three action instructions (draft a customer message, assign a claim, summarise a claim). Everything else a person might want to say gets "I did not understand that instruction."
2. **Suggestions are not a thing the system keeps.** Proposals exist only inside a chat answer. Nothing watches the workspace, notices that a claim is stuck, and leaves a suggestion waiting that a person can accept, dismiss or snooze, with a record of what happened.
3. **Automations start from a person, not from an event.** Workflow triggers begin "from a real reference" a person picks. Only two things run by themselves: a fixed follow-through timer every six hours, and approved outreach rules for sales events.

The recommendation is one new spine, not new frameworks:

- an **intent registry** (data, like the agent catalog) that maps what a person says to a typed action and its side-effect level;
- a **suggestion store** fed by **signals** (events and schedules), with accept, dismiss, snooze, expiry and acceptance-rate tracking;
- **standing instructions** that generalise the existing outreach rule (approve the rule once, it runs within limits) to internal and customer-safe actions;
- an **autonomy ladder** of three tiers (suggest, confirm, run within an approved rule), with a fixed list of things no tier can ever do;
- **server-side preflight checks** at the moment of execution, which is what Claude Code calls hooks.

All of it reuses the workflow engine, the background queue and the approval guard. None of it needs a free-roaming multi-agent runtime, which ADR-0002 deliberately declined.

## 2. What "intent" means here

The phrase can mean three things, and the design needs all three:

| Direction | Meaning | Example |
|---|---|---|
| Intent in | A person states a goal in words and the system works out the action. | "Chase the Kampala motor claims missing police abstracts" |
| Suggestions out | The system notices something and offers an action with its reasons. | "Claim CLM-1a2b3c4d has waited 5 days for an estimate. Send the repairer a reminder?" |
| Standing intent | A person states a durable rule once and the system applies it within limits. | "When a document is missing for 3 days, remind the customer once, in office hours" |

If you meant something narrower (for example only the chat), sections 5.1 and 7 (phase 1) stand alone.

## 3. What IIP has today, and what is missing

Verified against `main` on 2026-11-07. File names are where to look.

| Capability | State | Where |
|---|---|---|
| Governed agents | 16 in the catalog. Each declares risk, human-approval need, implementation and tools. Disable per agent or tenant | `AgentRegistry.cs`, `AgentGate.cs` |
| Tool side-effect levels | `read`, `draft`, `write`, `external_send`, `approval_required`. No generic SQL tool | handbook 4.4 |
| Human boundary | `ApprovalDecisionGuard.EnsureHumanForConsequentialDecision` is the backstop whatever a rule or setting says | `Domain/Approvals` |
| Intent handling | Deterministic classifier first, model only as fallback. Three action intents: `DraftCustomerMessage`, `AssignClaim`, `SummarizeClaim`. Ambiguous names return candidates and ask | `CommandCenterActionService.cs` |
| Proposals | `ActionProposalBlock` and `ProposedAction` exist as chat response blocks. **Not stored**: close the chat and the proposal is gone | `CommandCenterQueryContracts.cs` |
| Daily surface | The Brief lists items needing the person and a "handled for you" list. My Work ranks tasks | `BriefService.cs`, `MyWorkService.cs` |
| Standing rules | `OutreachRule`: trigger event type, conditions, wait, allowed products, template, hours, per-person and daily limits, status Draft, Approved, Paused. Approval is tied to a fingerprint of the exact content, and editing removes it. A decision log records each decision. Sales outreach only | `Rules/OutreachRule.cs`, `RuleEngine.cs`, `DecisionLog.cs` |
| Workflow engine | Graph of Trigger, Agent, Tool, Decision, HumanReview, Complete nodes, each queued as its own message. Publish-time check rejects any path from a consequential or external-send node that skips a HumanReview node. Only Release 1 (`document-to-decision`) executes | `AgentWorkflowService.cs`, `AgentWorkflowHumanReviewInvariant.cs` |
| Scheduled work | Case follow-through every 6 hours (customer reminder for missing documents, officer SLA warning), retention enforcement, mailbox ingestion | `Functions/*TimerFunction.cs` |
| Durable background queue | Lease, retry, dead letter, visibility, used in every environment | `BackgroundJobs` (handbook 4.5) |
| Learning from outcomes | Conversation insights record "a suggested next step" on prospects. Claim recommendation lets a person Agree or Disagree with the facts shown | `conversation-insights`, `claim-recommendation` |
| Measure of usefulness | None found: no acceptance rate for any suggestion | n/a |

**The gaps, in plain words:**

1. No registry of intents, so each new instruction means new parsing code.
2. No stored suggestion with a lifecycle (open, accepted, dismissed, snoozed, expired).
3. No event triggers: nothing says "when X happens, start this".
4. Standing rules exist only for outreach, and the follow-up timer's rules are fixed in code, not set by the customer's administrator.
5. Reminders and follow-ups are recorded but "a stored reminder will not send itself" (the 2026-11-07 gap review): the scheduling worker, template checks and duplicate prevention are not finished. Any automation that sends must wait for that.
6. No place for a person to see everything automated for them, what it did, and to undo or pause it. The Brief's "handled for you" is the seed of this.

## 4. What other tools do, and the lesson for IIP

Sources are listed in section 10 with how much weight each deserves. Where a source was a blog or a search result rather than the vendor's own page, it says so.

### 4.1 Claude Code (Anthropic)

| Idea | How it works | Lesson for IIP |
|---|---|---|
| Hooks | Deterministic commands run at lifecycle points: before a tool runs, after it, when it needs permission, when a session starts or stops, when a task completes. A pre-tool hook can allow, deny (with a reason fed back to the agent) or ask. Allow does not override enterprise deny rules. Hooks can also be a model call for judgment, but deterministic ones are the guardrails | Put the guardrails in **code that runs at execution time**, not in the prompt. IIP's equivalent is a preflight step that every automated or confirmed action passes through: consent, opt-out, messaging window, duplicates, daily limits, role, tenant. A refusal returns a reason the agent and the person can see |
| Permission modes | `default` asks for each edit. `plan` reads and proposes, edits blocked until the plan is approved. `acceptEdits` allows in-scope edits. `auto` lets a classifier approve routine actions and blocks risky ones, with a fixed deny list (force push, production deploys, sending secrets). After 3 consecutive blocks or 20 total, auto mode pauses and asks. `dontAsk` denies instead of asking, for locked-down scripts. `bypassPermissions` is for isolated machines only | The **autonomy ladder** (section 5.4). Plan-then-confirm for multi-step intents. A fixed deny list that no mode can lift. A **circuit breaker** that drops an automation back to suggest-only after repeated blocks or failures. No bypass mode exists in IIP, and none should |
| Subagents | A markdown file with a name, a `description` that tells the main agent when to delegate, an allowlist of tools, a model, a turn limit and a permission mode. Own context, only a summary returns. Cheaper models for simple work. Descriptions are budgeted because they cost context every time | IIP's agent catalog already has the same shape. Two takeaways: write each agent's description as "when to use me", and route cheap classification to a small model. Do **not** copy the nesting and concurrency: ADR-0002 keeps a registry of constraints, not a free multi-agent runtime |
| Skills and plugins | Reusable instructions loaded on demand, bundled with hooks and tools for sharing | Maps to **automation templates** a tenant can switch on (section 5.3), shipped by the platform, reviewed once, configured by the tenant administrator |
| Tool design | Few, well-scoped tools that finish a job, namespaced, returning readable names rather than raw ids, with errors that say how to fix the call | IIP's typed tools follow this. Keep adding intent actions as **one tool per business outcome** (for example "request missing documents"), not per table or endpoint |

### 4.2 Anthropic's guidance on building agents

- **Workflows versus agents.** Workflows follow predefined code paths. Agents decide their own steps. Start with the simplest thing that works and add autonomy only where steps cannot be predicted. IIP's insurance processes are mostly predictable, so **most automations should be workflows with a model at specific steps**, not open-ended agents.
- **Named patterns:** prompt chaining, routing, parallelisation, orchestrator-workers, evaluator-optimiser. Routing (classify, then send to a specialist) is already what the Command Center and customer assistant do. Evaluator-optimiser fits the draft-and-check loop for customer messages (the translation quality judge is an example).
- **Human checkpoints and stopping conditions.** Agents pause for human feedback at checkpoints and have iteration limits. IIP's `HumanReview` node and the publish-time invariant are this.
- **Make planning visible.** Show the plan before acting. This is the "preview" in section 5.1.

### 4.3 Microsoft Copilot Studio (autonomous agents)

Microsoft's own page is the most useful enterprise reference found:

- Agents act on **triggers** (events), not only prompts, with instructions, tools and safeguards. "Event-driven execution doesn't mean the agent runs continuously."
- Define scope narrowly, test in a sandbox, **roll out gradually** and watch decisions closely at first, then expand responsibility in small steps.
- **Least privilege**, and "don't rely on instructions" for limits. Enforce them outside the prompt.
- **Verify the trigger is authentic.** For an agent reacting to email, check the sender. This matters directly for IIP: inbound WhatsApp, email and uploaded documents are untrusted, and a message must never be able to trigger an action by what it says.
- Full audit of triggers received, decisions made and actions taken, with alerts on unusual access.
- A roadmap item (RM570434, third-party roadmap tracker, availability unconfirmed) describes a per-tool "pause and ask a person to approve, approve for the session, or deny" toggle. The same three-way choice suits IIP's confirm tier.

### 4.4 Salesforce Agentforce

Only search-result summaries were read, not the vendor pages. The structure is: **topics** (a scope with rules and which actions are allowed), **actions** (the executable tools, such as flows and API calls) and **guardrails** (natural-language limits plus a trust layer that masks personal data, screens outputs and logs generations). The useful idea is the topic: a named scope that bounds what the agent may do for that kind of request. IIP's equivalent is an intent's registry entry (section 5.1). The note about natural-language guardrails is the point to avoid copying: IIP's limits should be code.

### 4.5 Approval and suggestion interface patterns

From practitioner articles, not studies (treat as opinion):

- **Approval fatigue** is the main failure. People rubber-stamp when asked too often. Gate by **risk and reversibility**, not everything.
- A review queue should be organised around **decisions**, with filters for risk, age and agent, and a diff when a draft was changed.
- Show people **their own acceptance rate** and let them tune how often the agent asks.
- Keep the **delegation chain** in each record: who asked, which agent proposed, on what evidence.
- No rigorous research was found on how unprompted suggestions differ from agent approval requests. IIP should measure this itself (section 5.2).

## 5. Proposed design

### 5.1 Intent registry

A catalogue in code, beside `AgentCatalog`, one entry per intent:

| Field | Purpose |
|---|---|
| `key` and `label` | for example `request-missing-documents`, "Ask for missing documents" |
| `examples` | phrases used by the deterministic classifier and as test cases |
| `parameters` | typed, each resolved against real records (claim reference, customer, person to assign) with the same ambiguity handling that exists today |
| `action` | the typed tool or workflow it runs |
| `sideEffect` | `read`, `draft`, `write`, `external_send`, `approval_required` |
| `requiredPermission` | what the person must hold; checked again at execution |
| `preview` | what the person sees before anything happens (the plan) |
| `reversible` | whether it can be undone, and how |
| `maxAutonomy` | the highest tier the intent may ever run at (section 5.4) |

Behaviour:

1. Deterministic classifier first, as now. The model fallback may **only choose an intent from the registry and fill its typed parameters**. It cannot invent an action or a tool.
2. Show the plan. For `draft` and above, the answer is an action proposal naming exactly what will change and for whom, with the evidence. Multi-step intents ("chase all motor claims missing abstracts") show the list and let the person deselect items.
3. Ambiguity asks. Unknown or low-confidence intent says what it understood and offers the nearest intents. It never guesses on `write` or `external_send`.
4. Start with a small set that matches real daily work, drawn from what staff already do: request missing documents, send a reminder, assign or reassign, set a follow-up date, summarise a claim or customer, draft a customer update, list claims at SLA risk, prepare a briefing. The registry makes adding one a data change plus a tool.

### 5.2 Suggestions

**Signals** are events and schedules the system already knows: case opened, document extracted, document still missing after N days, SLA approaching, customer replied, customer silent for N days, policy approaching renewal, claim stuck in a status, extraction low confidence, recommendation disagreed with.

A **suggestion** is a stored record:

| Field | Notes |
|---|---|
| id, workspace, created by (an agent key or `rule:...`), created at | tenant and workspace scoped like every record |
| kind | from a fixed list; each kind names the intent it would run |
| subject | the claim, customer or policy it is about |
| why | a short plain reason and the evidence ids it cites. A suggestion with no evidence is not created |
| proposed action | a registry intent with its parameters already filled |
| risk class | from the intent's side-effect level |
| dedupe key | one open suggestion per kind and subject |
| status | Open, Accepted, Dismissed (with an optional reason), Snoozed until, Expired, Superseded |
| expires at | a stale suggestion is removed, not left to mislead |
| outcome | what happened after acceptance (sent, failed, undone) |

Rules for generating them:

- **Rules decide, a model only explains**, the pattern already used for the claim recommendation. Signals are computed from facts on the record. A model may phrase the reason, and its text is dropped if it contains decision or accusation wording (`AdvisoryLanguage`).
- **Limits against noise:** a daily cap per person, one open suggestion per subject and kind, a snooze, and automatic quieting of a kind that is dismissed repeatedly.
- **Measure every suggestion:** shown, accepted, dismissed, snoozed, ignored until expiry, and what accepting led to. Report acceptance rate per kind to the administrator. A kind below a threshold is switched off by default, which is the practical cure for approval fatigue.

Where they appear: the Brief (a section for suggestions, with "handled for you" beside it), My Work ranking (as a source), inline on the claim or customer they concern, and in the Command Center ("what should I do next?"). Accepting runs the intent through the normal confirm and preflight path. It never bypasses it.

### 5.3 Automations (standing instructions)

Generalise `OutreachRule` rather than build a second mechanism. A standing instruction is: **trigger** (an event type, or a schedule), **conditions**, **wait**, **action** (a registry intent or an approved template), **hours**, **limits**, and a **status** of Draft, Approved or Paused, with the approval tied to a fingerprint of the exact content. Editing any part removes the approval. This already exists, is tested, and is the right shape.

What changes:

- Triggers extend beyond sales events to the signals in 5.2, and the workflow engine's `Trigger` node gains two kinds, **Event** and **Schedule**, beside today's "start from a reference". This reuses the engine, the queue and the invariant instead of adding a runtime.
- Actions extend to internal and customer-safe ones: remind an officer, request missing documents, set a follow-up, assign by a stated rule, prepare a briefing.
- **Platform templates.** IIP ships reviewed automation templates (for example "Remind the customer once about missing documents", "Warn the officer 24 hours before an SLA breach", "Prepare the Monday briefing"). A tenant administrator switches one on and sets its numbers. This is the "skills and plugins" idea, with the platform as the reviewer. Tenants configure the numbers and wording within bounds; they do not author arbitrary actions in the first release.
- Every run writes a decision log line (already the pattern): trigger received, conditions checked, decision, action, outcome.
- A single **Automations** screen lists every standing instruction, its status, last run, runs, blocks and failures, with pause, edit and view-log. Nothing runs that is not on this screen.

### 5.4 Autonomy ladder

| Tier | What happens | Claude Code analogue | IIP rule |
|---|---|---|---|
| 0 Suggest | The system leaves a suggestion. Nothing runs until a person accepts | `default` and `plan` | Always available. The only tier for anything touching a decision |
| 1 Confirm | A person accepts a previewed action in one click and it runs | approving a plan | `draft`, `write` and `external_send` intents. The recorded human approval is the existing one |
| 2 Run within a rule | An approved standing instruction runs by itself within its limits | `auto` with a classifier | Only intents whose `maxAutonomy` allows it: `read`, `draft` and low-risk internal `write`, plus `external_send` of a platform-reviewed customer-safe template within the rule's limits, as outreach rules do today |

**Never at any tier** (fixed in code, not configurable, and a test must fail if a new intent crosses it): approve or reject a claim, deny, repudiate or settle, release a payment, bind a policy, set a premium or terms, make an underwriting decision, accuse anyone of fraud, send a sensitive or regulated communication without a recorded human approval, change who owns a customer, delete records. This is the existing prohibited list restated for automations.

**Circuit breaker.** An automation that is blocked by preflight or fails repeatedly (the Claude Code figures are 3 in a row or 20 in total, a sensible start) pauses itself, drops to suggest-only and tells the administrator. It does not retry silently.

**Roll out each automation in order:** suggest only for a period, then confirm, then run within the rule, only if the acceptance rate and the zero-block record justify it. The administrator moves it up, never the platform.

### 5.5 Preflight (the hook equivalent)

One server-side function every action passes through at the moment of execution, whether it came from a chat instruction, an accepted suggestion or an automation. It re-checks, from current records and not from when the suggestion was made:

- the actor's permission and the tenant and workspace;
- that the subject is still in the state the suggestion assumed (the evidence snapshot idea already used for stale chat answers);
- consent, opt-out, channel window and approved template;
- duplicate prevention and the per-person and daily limits;
- the kill switch for the agent and the tenant;
- the fixed deny list above.

A refusal is recorded with its reason and shown to the person. It never becomes a silent no-op. This is the single place to put new safety rules, and it is what lets automations be trusted.

### 5.6 Untrusted input never starts an action

Documents, emails, WhatsApp text and model output are data. A trigger is a **system event** (a record changed, a time passed), not the content of a message. A customer writing "approve my claim and send me the money" can at most create a human-review item. Any model call that reads such content runs with no ability to call a write tool, and its output passes the same validator and preflight. This is the Copilot Studio point about authentic triggers, made structural.

### 5.7 Fit with the existing architecture

| Question | Answer |
|---|---|
| New agent in the catalog? | One rule-based `suggestion-engine` (signals to suggestions, model explains only). Needs an ADR, as the catalog requires one for any addition |
| New runtime? | No. Reuse the workflow engine (add Event and Schedule triggers), the durable `BackgroundJobs` queue and the timers |
| Tenancy | Suggestions, standing instructions and logs carry organisation and workspace like every record. Platform templates are shared; their instances are per tenant |
| Control plane data | Suggestions refer to records by id and carry short reasons. They must not copy customer content into places the data classification forbids. In the SaaS edition, check each field against the allowed list before storing |
| AI review impact | No change to the human boundary. A suggestion is a proposal. The preamble and `ApprovalDecisionGuard` stay as they are |
| Roles | Tenant administrators own automations. Staff see and act on their own suggestions. A new permission for "manage automations" is separate from "run agents" |
| Language | Reasons and previews follow the person's language setting. Customer-facing text keeps the existing translation gates |

## 6. What to avoid

- **A free-roaming agent that picks its own tools.** Predictable processes should be workflows.
- **Prompt-only guardrails.** Every limit lives in preflight.
- **Everything asks.** Gate by risk and reversibility, measure acceptance, retire noisy kinds.
- **Notification spam.** Caps and snooze from the first release.
- **Silent failure.** A blocked or failed automation is visible and pauses itself.
- **Claims the product cannot back.** Do not describe automations as running until the scheduling worker, template checks and duplicate prevention are finished and tested end to end. Show `Not configured` until then.
- **Tenants authoring arbitrary actions.** First release: platform templates with tenant numbers only.

## 7. Ordered build

Each phase ships on its own, keeps the app runnable and has its gate. No phase depends on the next to be useful.

1. **Intent registry and plan preview.** Registry in code, the three existing intents moved into it, preview and ambiguity behaviour kept, model fallback restricted to registry choices. Add the first new intents (request missing documents, set follow-up, list claims at SLA risk). Gate: classifier tests per intent, an ambiguity test, a test that the model fallback cannot name an action outside the registry, permission re-check at execution.
2. **Preflight.** Extract the shared checks into one function used by every action path, including the existing draft, assign and outreach sends. Gate: tests for each refusal reason, and a test that no action path bypasses it.
3. **Suggestion store and Brief section.** Entity, repository, signals for missing documents, SLA approach and silent customer, dedupe, caps, expiry, accept and dismiss and snooze, outcome recording, acceptance metrics. Gate: tenant isolation tests, no suggestion without evidence, accepted suggestion goes through preflight, stale suggestion expires.
4. **Event and Schedule triggers in the workflow engine.** Extend `Trigger` nodes. Gate: the human-review invariant still rejects bypass paths, an event trigger cannot be fired by message content, trigger authenticity tests.
5. **Standing instructions beyond outreach, and the Automations screen.** Generalise `OutreachRule`, platform templates, decision log, pause, edit, circuit breaker. Needs the reminder and follow-up sending path finished first. Gate: fingerprint invalidation, limits, hours, circuit breaker, no automation runs while Draft or Paused.
6. **Measure and tune.** Administrator report of acceptance per kind and per automation, with the switch-off default for low acceptance. Gate: metrics are computed from stored outcomes, not estimated.
7. **Optional later:** model-written reasons, richer intents from voice or WhatsApp for staff, tenant-composed automations from the same building blocks, after the first release shows what people actually use.

## 8. Risks and honest limits

- **Regulatory and customer-facing risk** is the reason tier 2 is limited to reviewed templates within limits. The IRA and data protection context in the readiness blueprint applies to anything that sends.
- **Trust takes time.** The acceptance data is how you decide whether to raise a tier, and it will not exist until phase 3 ships.
- **Sources are uneven.** Anthropic's and Microsoft's pages are primary. The interface-pattern claims are practitioner opinion, and the Agentforce notes come from search summaries. Nothing here was tested against IIP users.
- **Model quality for local languages** is unproven, so suggestions and previews in Luganda or Kiswahili need review before they are relied on.
- **This is a design, not an estimate.** Sequence and gates are given. Effort is not guessed here.

## 9. Decisions needed from the owner

1. Is the three-direction reading of "intent" (section 2) right, or is the chat the main thing?
2. Which five intents and which three suggestion kinds matter most for the next demo? Suggested starting set: missing documents, SLA risk, silent customer.
3. Should tier 2 (runs by itself) be limited to reviewed platform templates at first? This document recommends yes.
4. Who may switch an automation on: the tenant administrator only, or also a team lead?
5. Does the first release need the reminder and follow-up sending finished before any automation is shown? This document says yes, and that until then the screen says `Not configured`.

## 10. Sources and how much weight each deserves

Primary vendor documentation (read in full or in large part on 2026-11-07):

- Anthropic, [Building effective agents](https://www.anthropic.com/engineering/building-effective-agents)
- Anthropic, [Writing tools for agents](https://www.anthropic.com/engineering/writing-tools-for-agents)
- Claude Code documentation: [hooks guide](https://code.claude.com/docs/en/hooks-guide), [subagents](https://code.claude.com/docs/en/sub-agents), [permission modes](https://code.claude.com/docs/en/permission-modes). The last two were read up to the first 100,000 characters of each page.
- Microsoft, [Design autonomous agent capabilities (Copilot Studio)](https://learn.microsoft.com/en-us/microsoft-copilot-studio/guidance/autonomous-agents)

Search results only, not opened in full:

- Salesforce Trailhead, [Explore Agentforce guardrails and trust patterns](https://trailhead.salesforce.com/content/learn/modules/trusted-agentic-ai/explore-agentforce-guardrails-and-trust-patterns), and third-party summaries of topics, actions and the trust layer. No detail on how proactive triggers are configured was found.
- Roadmap tracker entry [RM570434](https://mc.merill.net/message/RM570434) for per-tool human approval in Copilot Studio. A third-party tracker, with availability listed as in development.

Practitioner opinion (blogs and course pages, not studies):

- [Human-in-the-loop UX patterns for production AI agents](https://agentinterface-co.pages.dev/blog/human-in-the-loop-ux-patterns-production-ai-agents.html)
- [Human-in-the-Loop Approval Interfaces for AI Agent Actions](https://www.gravitee.io/corpus/gen-1980/human-computer-interaction/human-in-the-loop-approval-interfaces-for-ai-agent-actions.html)
- [Copilot Studio human-in-the-loop governance](https://articles.firstaimovers.com/articles/copilot-studio-human-in-loop-governance-smes-2026/)

IIP facts come from the code and the handbook sections 4.4 to 4.6 on `main`, and from the 2026-11-07 usability and agent gap review.
