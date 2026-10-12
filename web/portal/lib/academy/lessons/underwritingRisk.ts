import type { Module, Question } from "@/lib/academy/courses";

// Course: AI for Underwriting & Risk Intelligence. All examples are synthetic. What IIP does today is stated
// plainly: it reviews and summarises submissions and drafts questions, it shows operational portfolio counts, and
// it does not set premiums or make underwriting decisions. Pricing models and loss-ratio analysis are taught as
// concepts, not as IIP features.

export const UNDERWRITING_RISK_MODULES: Module[] = [
  {
    id: "underwriting-and-ai",
    title: "How underwriting works and where AI fits",
    goal: "Follow an application from submission to decision, and mark the steps that are reading and chasing, not judgement.",
    outcomes: ["Name the steps from submission to decision", "Separate reading and chasing from judgement", "Say what AI may prepare"],
    flow: ["Submission", "Read and extract", "Gaps chased", "Risk assessed", "Terms and price set", "Decision"],
    flowNote: "AI helps with the first three steps. Assessing the risk, setting terms and price, and the decision belong to the underwriter.",
    screen: { file: "underwriting.png", caption: "Submission reviews: AI summarises submissions, drafts questions and highlights gaps. Underwriters decide acceptance, rejection and premium.", callouts: ["A statement of what AI does not do", "The queue of submissions", "Guardrail: acceptance and pricing are human"] },
    example: {
      title: "A boda-boda fleet application",
      steps: ["The application arrives by email with three attachments.", "The documents are read and the facts listed: number of riders, licence status, sum insured.", "The underwriter receives the file with the facts and the gaps already listed."],
    },
    tryIt: "Open Underwriting and read the line that says what AI does not do. Say which steps in your own process that rule protects.",
    takeaways: ["Most underwriting time is reading and chasing.", "Judgement, terms and price stay with the underwriter.", "A file that arrives ready is decided sooner."],
  },
  {
    id: "submission-triage",
    title: "Submission triage",
    goal: "Read the documents, extract the facts, list what is missing and ask for exactly that, before the file reaches an underwriter.",
    outcomes: ["Read facts from submission documents", "List what is missing", "Ask for exactly the missing items"],
    flow: ["Documents received", "Type classified", "Facts extracted", "Gaps listed", "Request drafted", "Person sends"],
    flowNote: "A person checks what the AI read. Anything the AI is unsure of counts as still missing.",
    screen: { file: "documents.png", caption: "The document library: type, status and the confidence of what was read.", callouts: ["Type and status", "Confidence", "Review a document"] },
    example: {
      title: "A missing licence",
      steps: ["The file has the application form and a vehicle registration, but no driving licence.", "The gap is listed and a request is drafted for that one document.", "A person reads the request and sends it."],
    },
    tryIt: "Open the document library. For one claim, list the documents present and the documents you would still ask for.",
    takeaways: ["Triage before referral saves the underwriter's time.", "Ask for the exact missing item.", "A person confirms what the AI read."],
  },
  {
    id: "risk-profiling",
    title: "Risk profiling from the record",
    goal: "Draw a profile from the customer's own record, with every point linked to the fact behind it.",
    outcomes: ["Build a profile from the record", "Link each point to its fact", "Spot a point that has no fact behind it"],
    flow: ["Customer record", "Facts selected", "Profile drawn", "Each point cites its fact", "Underwriter checks"],
    flowNote: "A profile point with no fact behind it is an opinion. Remove it or find the fact.",
    screen: { file: "scores.png", caption: "A score with the reasons behind it, quoted from the record.", callouts: ["The score", "The reasons", "The facts they come from"] },
    example: {
      title: "Weak versus good",
      steps: ["Weak: 'This customer looks risky.'", "Good: 'Two claims in the last twelve months, both for motor, both settled.'", "A good point can be checked against the record by someone who was not there."],
    },
    tryIt: "Open Scores and find one reason. Say what record fact it rests on.",
    takeaways: ["Every point cites its fact.", "A score without reasons cannot be challenged.", "Use only facts you may lawfully use."],
  },
  {
    id: "pricing-intelligence",
    title: "Pricing intelligence",
    goal: "Use data and models to inform a price, and be clear that setting the premium, and approving a rate, stays with a person.",
    outcomes: ["Say what data can inform a price", "Say what must be set by a person", "Explain why a model's price is advice"],
    flow: ["Data and experience", "Model suggests a range", "Underwriter or actuary reviews", "Premium set by a person", "Reason recorded"],
    flowNote: "IIP does not set premiums. This lesson teaches the principle: a model may inform a price, and a named person sets it and records why.",
    screen: { missing: "IIP does not price policies, so there is no pricing screen to show. Pricing is taught here as a principle that applies to any tool you use." },
    example: {
      title: "A suggested range",
      steps: ["A model suggests a range based on past claims for similar risks.", "The underwriter sees the customer has added a security device and moves to the lower end.", "The premium is set by the underwriter and the reason is written down."],
    },
    tryIt: "Write down who in your organisation may approve a rate, and where their reason is recorded.",
    takeaways: ["A model's price is advice.", "A person sets the premium and records why.", "Pricing must be explainable to the customer and the regulator."],
  },
  {
    id: "portfolio-analysis",
    title: "Portfolio analysis",
    goal: "Read concentration by product, region and channel, and spot trends early using the Insights views.",
    outcomes: ["Read a portfolio view", "Spot a concentration", "Say what a view does not show"],
    flow: ["Records aggregated", "Counts by status and product", "Trend over time", "Concentration spotted", "Action decided by a person"],
    flowNote: "Insights shows operational counts from the workspace. Loss ratios and financial measures are omitted rather than estimated when they are not available.",
    screen: { file: "executive.png", caption: "The management operations view: open claims, document completeness, SLA at risk and claims by status, with the time of the snapshot.", callouts: ["Snapshot time and its limit", "Counts and completeness", "Claims by status"] },
    example: {
      title: "Too many claims in one status",
      steps: ["The chart shows many claims waiting in the same status.", "The reader asks which product and which insurer they belong to.", "A person decides what to change and checks the chart again next week."],
    },
    tryIt: "Open Insights and read the line under the headline. Say what the view will not show you.",
    takeaways: ["A view is a snapshot, with a time.", "Counts are not causes.", "Missing measures are omitted, not guessed."],
  },
  {
    id: "decision-support",
    title: "Decision support, not decisions",
    goal: "Refer to the underwriter with a file ready to decide, record the decision and the reason, and keep the AI's part and the person's part apart in the audit trail.",
    outcomes: ["Prepare a referral file", "Record the decision and reason", "Keep the AI's part and the person's apart"],
    flow: ["File prepared", "Summary and gaps shown", "Underwriter decides", "Reason recorded", "Audit trail"],
    flowNote: "AI does not make final underwriting acceptance or rejection. It summarises, recommends, drafts questions and escalates.",
    screen: { file: "audit.png", caption: "The audit log: who acted, what changed, and when.", callouts: ["Filters", "One entry", "Who, what and when"] },
    example: {
      title: "A referral",
      steps: ["The file arrives with a summary, the facts and the gaps already listed.", "The underwriter accepts with a condition and writes the reason.", "The audit log shows the AI's summary and the person's decision as separate entries."],
    },
    tryIt: "Open Audit and find one decision. Say who made it and where the reason is.",
    takeaways: ["Referral with a ready file.", "The decision and the reason are recorded together.", "Two voices, two entries."],
  },
  {
    id: "fairness-regulation",
    title: "Fairness, explanation and regulation",
    goal: "Test for unfair patterns, explain an outcome in plain words, and know the references, each to be confirmed with legal and compliance for your case.",
    outcomes: ["Name a fairness check", "Explain an outcome in plain words", "Name the main references"],
    flow: ["Define groups to compare", "Compare outcomes", "Find a gap", "Investigate the cause", "Fix or justify", "Record"],
    flowNote: "References: the NAIC model bulletin on the use of AI by insurers (United States, 2023); the IAIS application paper on supervising AI (July 2025); Uganda's Data Protection and Privacy Act, 2019. Which of them applies to you, and what each requires, is to be confirmed with legal and compliance.",
    screen: { file: "settings.png", caption: "Settings: the workspace, people and what each role can do, which supports least-privilege access to data.", callouts: ["People", "Roles", "Workspace"] },
    example: {
      title: "A pattern by region",
      steps: ["Refusals are higher in one region than in others.", "The team checks whether the cause is the risk itself or a data gap in that region.", "The finding and the action are recorded and reviewed by compliance."],
    },
    tryIt: "Pick one decision your team makes. Write how you would explain it to the customer in two sentences.",
    takeaways: ["Compare outcomes across groups, regularly.", "An explanation must be understandable by the customer.", "Which law applies is confirmed, not assumed."],
  },
  {
    id: "model-monitoring",
    title: "Keeping a model honest over time",
    goal: "Monitor accuracy and drift, review overrides, and decide when a model or a rule is retired.",
    outcomes: ["Say what drift is", "Track overrides", "Decide when to retire a model"],
    flow: ["Model in use", "Accuracy checked", "Overrides reviewed", "Drift found", "Retrain, adjust or retire"],
    flowNote: "A model that was right last year may be wrong now. Check it on a schedule, and keep a record of every override.",
    screen: { file: "documents.png", caption: "The classifier's accuracy is learned from every reclassification and correction recorded by people.", callouts: ["Classification accuracy", "Corrections recorded", "Confidence per document"] },
    example: {
      title: "A rising override rate",
      steps: ["Underwriters override the suggested referral more often each month.", "The team checks whether the market or the data has changed.", "They adjust the rule and record why."],
    },
    tryIt: "Choose one automated rule in your process. Write how often you would check it and who owns the check.",
    takeaways: ["Models drift.", "Overrides are information.", "Retire what no longer works."],
  },
];

export const UNDERWRITING_RISK_QUIZ: Question[] = [
  { q: "Which underwriting steps does AI mainly help with today?", options: ["Reading, extracting and chasing gaps", "Setting the premium", "Deciding acceptance"], answer: 0, why: "AI prepares the file. Price and decision stay with the underwriter." },
  { q: "A profile says a customer 'looks risky'. What is wrong with that?", options: ["Nothing", "No fact from the record is behind it", "It is too short"], answer: 1, why: "Every profile point must cite the fact behind it, so a reviewer can check it." },
  { q: "Who sets the premium when a model suggests a price range?", options: ["The model", "A named person, who records why", "The customer"], answer: 1, why: "A model's price is advice. A person sets the premium and records the reason." },
  { q: "An Insights view omits a loss ratio. Why?", options: ["It is broken", "It was not available, so it is omitted rather than estimated", "Loss ratios are secret"], answer: 1, why: "A made-up figure is worse than none. Unavailable measures are omitted, not guessed." },
  { q: "Underwriters override a model more each month. What does that suggest?", options: ["Nothing", "The model or the data may have drifted", "Underwriters are careless"], answer: 1, why: "A rising override rate is a signal to check for drift and review the rule." },
];
