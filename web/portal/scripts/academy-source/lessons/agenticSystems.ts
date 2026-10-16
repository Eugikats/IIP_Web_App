import type { Module, Question } from "../courses";

// Course: Building Agentic AI Systems for Insurance. A technical course. IIP is used as the working reference:
// every example describes how IIP is built, with synthetic data. The security list is the OWASP Top 10 for LLM
// Applications (2025). Legal and regulatory points are to be confirmed with legal and compliance.

export const AGENTIC_SYSTEMS_MODULES: Module[] = [
  {
    id: "agent-anatomy",
    title: "Anatomy of an agent",
    goal: "Model, tools, loop and state: how a request becomes a sequence of tool calls, and where to put hard limits.",
    outcomes: ["Name the four parts of an agent", "Describe the loop", "Place hard limits on steps, tools and data"],
    flow: ["Request", "Model chooses a tool", "Tool runs with the caller's permissions", "Result returned to the model", "Repeat up to a limit", "Answer"],
    flowNote: "Limit the number of steps, the tools offered and the data each tool can reach. The limits live in code, not in the prompt.",
    screen: { file: "command-center.png", caption: "An agent's answer built from tool results: counts from the records, a link to the page behind them, and the snapshot time.", callouts: ["Figures from a counting tool", "A link to the source page", "Snapshot time and limit"] },
    example: {
      title: "A counting agent",
      steps: ["A small set of tools is offered: count records, show a daily series, list a few items. All are read-only.", "The loop stops after a fixed number of steps even if the model asks for more.", "Each tool checks the caller's permission before it reads anything."],
    },
    tryIt: "Sketch an agent for one task. List its tools, mark each read-only or not, and write its step limit.",
    takeaways: ["An agent is a model, tools, a loop and state.", "Cap steps, tools and data.", "Tools enforce permissions, not the prompt."],
  },
  {
    id: "retrieval",
    title: "Retrieval-augmented generation",
    goal: "Chunk and index approved knowledge, retrieve with filters (product, effective date, tenant), and return every answer with its sources.",
    outcomes: ["Chunk a document sensibly", "Retrieve with filters", "Return answers with sources"],
    flow: ["Approved document", "Classified and versioned", "Chunked", "Indexed", "Query with filters", "Answer with citations"],
    flowNote: "Filters matter as much as the model: the wording in force on the date, for the right product and the right tenant, and nothing whose sensitivity is unknown.",
    screen: { file: "data-hub.png", caption: "The Data Hub: approved sources, classified and versioned. Unknown sensitivity is blocked from retrieval by default.", callouts: ["Approved sources only", "Sensitivity must be set", "Answers logged with sources"] },
    example: {
      title: "Which wording applies?",
      steps: ["A claim happened in March. Two versions of the motor wording exist.", "Retrieval filters to the version in force on the date of loss.", "The answer cites that version's clause, so a reviewer can open it."],
    },
    tryIt: "Choose a document you would index. Write the filters its answers would need.",
    takeaways: ["Chunk, index, filter, cite.", "Filter by tenant, product and effective date.", "No source means a person confirms."],
  },
  {
    id: "orchestration",
    title: "Workflow orchestration",
    goal: "Use queues, retries and idempotency so a webhook delivered twice does not answer twice or lose a message.",
    outcomes: ["Explain why work goes through a queue", "Make a handler idempotent", "Handle a message that is delivered twice"],
    flow: ["Webhook received", "Message saved first", "Acknowledge at once", "Work queued", "Handler runs with retries", "Duplicate recognised"],
    flowNote: "Save before you act, answer the sender quickly, and treat a repeat as already handled. A message that is saved but never answered must still be visible to a person.",
    screen: { file: "audit.png", caption: "The audit log: every consequential step recorded with who or what did it.", callouts: ["Filters", "One entry", "Who, what and when"] },
    example: {
      title: "A WhatsApp message arrives twice",
      steps: ["The first delivery is saved and queued for the assistant.", "The second delivery is recognised by its message id and not acted on again.", "The customer gets one reply, and the message is on record either way."],
    },
    tryIt: "Take one integration you know. Write what happens if its message is delivered twice, and what happens if your handler crashes halfway.",
    takeaways: ["Save first, acknowledge, then work.", "Idempotency keys stop double actions.", "Never drop a customer's message."],
  },
  {
    id: "integrations",
    title: "APIs and integrations",
    goal: "Connect policy administration, claims, WhatsApp Cloud API and email behind interfaces, with synthetic providers for development.",
    outcomes: ["Put each provider behind an interface", "Use a synthetic provider for development", "Switch providers by configuration"],
    flow: ["Domain logic", "Interface", "Real provider or synthetic provider", "Configured per environment", "Health reported honestly"],
    flowNote: "Keep domain rules apart from provider details. When a real provider is not configured, show Not configured. Never display an unconfigured service as working.",
    screen: { file: "settings.png", caption: "Settings: the workspace, its people and its connections, each shown as connected or not.", callouts: ["People", "Roles", "Connections"] },
    example: {
      title: "WhatsApp behind an interface",
      steps: ["The assistant calls an outbound interface. It does not know which provider sits behind it.", "In development the interface is a synthetic provider; in production it is the WhatsApp Cloud API.", "Health shows ready only when the provider is really configured."],
    },
    tryIt: "Pick an integration you need. Write its interface in three methods and what its synthetic version would return.",
    takeaways: ["Interfaces keep domain logic clean.", "Synthetic providers for development and tests.", "Report health honestly."],
  },
  {
    id: "security",
    title: "Security",
    goal: "Isolate tenants, keep secrets out of code, give each tool the least authority, and defend against the risks in the OWASP Top 10 for LLM Applications (2025), including prompt injection and excessive agency.",
    outcomes: ["Isolate tenants end to end", "Store secrets safely", "Defend against prompt injection and excessive agency"],
    flow: ["Authenticate the caller", "Resolve tenant and role on the server", "Tools check permission", "Secrets encrypted, write-only", "Inputs treated as data", "Everything logged"],
    flowNote: "The 2025 list includes prompt injection, sensitive information disclosure, system prompt leakage, excessive agency and unbounded consumption. Each has a control in the design, not in the wording of a prompt.",
    screen: { file: "settings.png", caption: "Roles decide what each person can see and do. The server resolves the tenant and role, not the browser.", callouts: ["People", "Roles", "Workspace"] },
    example: {
      title: "A credential you can save but not read back",
      steps: ["An administrator saves a provider's access token. It is validated first, then stored encrypted.", "The screen can show that a token exists, never the token itself.", "A webhook is accepted only if its signature matches the secret for that number."],
    },
    tryIt: "List every secret your system holds. Next to each, write where it is stored and who can read it.",
    takeaways: ["Tenant and role are resolved on the server.", "Secrets are encrypted and write-only.", "Limits in the system beat promises in the prompt."],
  },
  {
    id: "governance-by-design",
    title: "Governance by design",
    goal: "Add approval gates, evidence links and an audit trail to every consequential action, so the record of who decided what is complete.",
    outcomes: ["Put an approval gate on a consequential action", "Link evidence to an output", "Keep a complete audit trail"],
    flow: ["AI prepares", "Evidence attached", "Approval gate", "Named person decides", "Reason recorded", "Audit trail"],
    flowNote: "The platform's preamble carries the human-decision boundary and is not editable by a tenant. A tenant may set an agent's voice and scope, never its authority.",
    screen: { file: "audit.png", caption: "The audit trail: who acted, what changed, and when, with the AI's part apart from the person's.", callouts: ["Filters by day and action", "One entry", "Who, what and when"] },
    example: {
      title: "A customer message needs approval",
      steps: ["The assistant drafts a message about a claim outcome.", "The gate holds it until a named person edits and approves.", "What was sent, who approved it and when are recorded."],
    },
    tryIt: "Pick a consequential action in your system. Write who approves it and where that is recorded.",
    takeaways: ["Gates in the design, not on top.", "Evidence links make outputs checkable.", "Voice and scope are configurable; authority is not."],
  },
  {
    id: "evaluation-testing",
    title: "Evaluation and testing",
    goal: "Build fixed test sets, regression tests and red-team cases, and measure answers against their sources.",
    outcomes: ["Build a fixed test set", "Write a regression test for a failure", "Run red-team cases"],
    flow: ["Collect real cases", "Fix the expected answer and its source", "Run on every change", "Failures become tests", "Red-team the edges"],
    flowNote: "A bug found in production becomes a test that fails before the fix and passes after it.",
    screen: { file: "documents.png", caption: "Classification accuracy is learned from every correction people record, a live source of test cases.", callouts: ["Accuracy per type", "Corrections recorded", "Confidence per document"] },
    example: {
      title: "A dropped message becomes a test",
      steps: ["A webhook returned success but saved nothing for plain text messages.", "A test is written that sends a signed text message and expects it saved and handed to the assistant.", "The test fails on the old code and passes on the fix, and stays in the suite."],
    },
    tryIt: "Take a failure you have seen. Write the test that would have caught it.",
    takeaways: ["Fixed test sets with known answers.", "Every failure becomes a test.", "Test the edges: wrong signature, unknown number, duplicate."],
  },
  {
    id: "production-operations",
    title: "Production operations",
    goal: "Observe, control cost, handle incidents and roll out changes safely, using IIP's own deployment as the reference.",
    outcomes: ["Choose what to observe", "Control cost per feature", "Handle an incident and roll out safely"],
    flow: ["Health and logs", "Usage and cost per feature", "Alert on failure", "Incident: contain, fix, test", "Roll out and verify"],
    flowNote: "Measure usage per feature so cost is visible. After any deployment, verify the real path end to end, not just that the service is up.",
    screen: { file: "executive.png", caption: "Insights: operational counts with the time of the snapshot, the kind of view an operations team watches.", callouts: ["Snapshot time and limit", "Counts and completeness", "Claims by status"] },
    example: {
      title: "After a deployment",
      steps: ["Check that the service reports ready.", "Send one real message through the channel and confirm it is saved and answered.", "Record what was checked and the time."],
    },
    tryIt: "Write the three checks you would run after every deployment of your system.",
    takeaways: ["Observe usage, errors and cost.", "Verify the real path after a change.", "Write down what you checked."],
  },
];

export const AGENTIC_SYSTEMS_QUIZ: Question[] = [
  { q: "Where should an agent's limits on tools and steps live?", options: ["In the prompt", "In code, enforced by the system", "In the documentation"], answer: 1, why: "Limits in the prompt can be talked around. Limits in code cannot." },
  { q: "A webhook is delivered twice. What is the right behaviour?", options: ["Answer twice", "Recognise the repeat and act once, with the message on record", "Ignore both"], answer: 1, why: "Save first, acknowledge, and use an idempotency key so a repeat is not acted on again." },
  { q: "Retrieval for a claim must use which wording?", options: ["The latest version", "The version in force on the date of loss", "Any version"], answer: 1, why: "Filter by effective date as well as product and tenant, and cite the clause." },
  { q: "How should a provider's access token be handled on screen?", options: ["Shown to administrators", "Stored encrypted and never shown back", "Kept in the page source"], answer: 1, why: "Secrets are validated, stored encrypted and write-only. A screen shows that one exists, never its value." },
  { q: "A failure reaches production. What is the best next step after the fix?", options: ["Hope it does not recur", "Add a test that fails before the fix and passes after", "Remove the feature"], answer: 1, why: "Every failure becomes a regression test, so it cannot return unnoticed." },
];
