// IIP Academy course content. Written from docs/iip-training-courses-brief.md and kept short on purpose:
// each module is one page with the idea, a flow, a real screen from IIP and a worked example.
//
// Honesty rules from the brief: only features IIP really has are described; a screen IIP does not have is
// shown as a labelled placeholder, never drawn; the certificate is a certificate of completion, not an
// accredited qualification; percentages are targets, never measured results; obligations under law or
// regulation are "to be confirmed with legal and compliance". All examples are synthetic.

import { PROGRAMME_COURSES } from "./programmeCourses";

export type Screen =
  | { file: string; caption: string; callouts: string[] }
  | { missing: string };

export type Module = {
  id: string;
  title: string;
  goal: string;
  outcomes: string[];
  /** The idea in one picture: a left-to-right flow, optionally with a branch note. */
  flow: string[];
  flowNote?: string;
  screen: Screen;
  example: { title: string; steps: string[] };
  tryIt: string;
  takeaways: string[];
};

export type Question = { q: string; options: string[]; answer: number; why: string };

export type Course = {
  id: string;
  number: number;
  title: string;
  short: string;
  audience: string;
  duration: string;
  aim: string;
  iipAreas: string[];
  criteria: string[];
  /** The category the catalogue files the course under (see categories.ts). */
  category: string;
  /** What a learner can do at the end, in the course's own words. Shown on the course page. */
  outcomes?: string[];
  /** The planned lessons, shown on the course page while the lessons themselves are being written. A course
   * with an outline and no modules is listed honestly as such: it cannot be started. */
  outline?: { title: string; goal: string }[];
  modules: Module[];
  quiz: Question[];
};

/** A course whose lessons are written and can be taken. */
export const isReady = (course: Course) => course.modules.length > 0;

// The four hands-on courses taught against the real IIP screens.
const HANDS_ON: Omit<Course, "category">[] = [
  {
    id: "claims",
    number: 2,
    title: "AI-assisted claims for bancassurance",
    short: "Track every claim against the regulator's service standards and get files right first time.",
    audience: "Claims coordinators, branch staff who act as the claims contact, bancassurance managers",
    duration: "1 day (proposed)",
    aim: "See how IIP tracks each claim against the Insurance Regulatory Authority (IRA) service standards, so the bank knows which claims are about to breach and what to do next.",
    iipAreas: ["Claims", "Documents", "Insights", "Audit"],
    criteria: ["Complete every module", "Pass the check questions"],
    modules: [
      {
        id: "life-of-a-claim",
        title: "The life of a bancassurance claim",
        goal: "Know who does what between the bank, the insurer, the assessor and the reinsurer.",
        outcomes: ["Name the steps from loss to payment", "Say who owns each step", "Find a claim's current step in IIP"],
        flow: ["Loss event", "Bank takes the claim", "Insurer registers it", "Documents and assessor", "Offer or repudiation", "Payment"],
        flowNote: "In bancassurance the bank sells and supports. The insurer decides and pays.",
        screen: { file: "claims-list.png", caption: "The claims list: every open claim, its status and what needs attention.", callouts: ["Search and filters", "Status of each claim", "Open a claim for its detail"] },
        example: {
          title: "A loan-linked claim arrives at the branch",
          steps: ["The customer reports the loss at the branch.", "The branch records the claim in IIP and passes it to the insurer.", "IIP shows the claim in the list with its status, so anyone can see where it is."],
        },
        tryIt: "Open Claims. Pick any claim and say, in one sentence, which step it is at and who owns that step.",
        takeaways: ["The bank is the intake point. The insurer decides and pays.", "Every claim has a visible status in IIP.", "You never need to wait for an insurer to tell you where a claim is."],
      },
      {
        id: "ira-clocks",
        title: "The IRA clocks",
        goal: "Read a claim's timeline and say which clock is running and how long is left.",
        outcomes: ["State the IRA milestones", "State the three payment tiers", "Work out a payment deadline from the discharge voucher date"],
        flow: ["Acknowledge: 3 working days", "Assessor: 3 days (5 if complex)", "Offer or repudiation: 5 days after final report", "Discharge voucher signed", "Payment: 10, 15 or 20 working days"],
        flowNote: "Payment tiers by amount: up to UGX 10M is 10 working days, above 10M up to 50M is 15 working days, above 50M is 20 working days or on the reinsurer's cash call. All are counted from the signed discharge voucher.",
        screen: { file: "claim-detail.png", caption: "A claim in IIP: its status, documents and the people and steps around it.", callouts: ["Claim summary and status", "Documents and evidence", "Actions that need a person"] },
        example: {
          title: "Which clock is running?",
          steps: ["A UGX 30M claim has been assessed and the customer signed the discharge voucher on Monday.", "30M sits in the middle tier: payment is due within 15 working days of Monday.", "Count 15 working days forward, skipping weekends and public holidays. That is the deadline to chase."],
        },
        tryIt: "Open a claim. Find the date of its last step, name the clock that is running, and work out the next deadline.",
        takeaways: ["The payment clock starts at the signed discharge voucher.", "Three tiers: 10, 15 or 20 working days by amount.", "A repudiation needs a written reason."],
      },
      {
        id: "complete-files",
        title: "Complete files first time",
        goal: "Check a claim file for missing documents before it goes to the insurer.",
        outcomes: ["List the documents a claim type needs", "Use IIP's document checks", "Ask the customer for exactly what is missing"],
        flow: ["Claim opened", "Documents uploaded", "IIP reads each document", "Missing items listed", "Customer asked for the gaps", "File complete"],
        screen: { file: "claim-detail.png", caption: "'What needs a decision' shows how many required documents have arrived, and what is still open.", callouts: ["Documents received against documents required", "Service standard status", "The next action for a person"] },
        example: {
          title: "A discharge note is missing",
          steps: ["IIP reads the claim file and lists the documents it has.", "The hospital discharge note is not there, so IIP shows it as missing.", "The branch asks the customer for that one document instead of sending the file back and forth."],
        },
        tryIt: "Open a claim with documents. List what is present, and what you would still ask for.",
        takeaways: ["A complete file is the fastest way to a paid claim.", "IIP reads documents; a person confirms what it read.", "Ask for the exact missing item, not 'more documents'."],
      },
      {
        id: "claims-at-risk",
        title: "Claims at risk",
        goal: "Find claims close to breaching a service standard and decide the next action.",
        outcomes: ["Sort the list by urgency", "Name the running clock per claim", "Choose the next action for each"],
        flow: ["Open claims list", "Filter by status", "Spot the nearest deadline", "Choose: chase, collect documents or update customer"],
        screen: { file: "claims-list.png", caption: "Filter the list to see which claims need attention first.", callouts: ["Filters", "Claims needing attention", "Next action"] },
        example: {
          title: "Five claims, one morning",
          steps: ["List the claims by how close their next deadline is.", "Name the clock each is on: acknowledgement, assessor, offer or payment.", "Write the next action beside each: chase the insurer, request a document, or update the customer."],
        },
        tryIt: "From the claims list, pick the three claims you would act on first and say why.",
        takeaways: ["Urgency is the nearest deadline, not the biggest amount.", "Each claim has one running clock.", "Every claim leaves your list with a next action."],
      },
      {
        id: "comparing-insurers",
        title: "Comparing insurers and escalating",
        goal: "Spot a slow partner and prepare an escalation, and know how disputes are handled.",
        outcomes: ["Compare turnaround across insurers", "Prepare an escalation", "Explain the route for a disputed repudiation"],
        flow: ["Repudiation with written reason", "Customer disputes", "IRA Complaints Bureau", "Insurance Tribunal"],
        flowNote: "A repudiation needs a written reason. Disputes go to the IRA Complaints Bureau, then the Insurance Tribunal.",
        screen: { missing: "An insurer comparison view is proposed for IIP but not built yet. Today, use the claims list filters and the Insights page for turnaround." },
        example: {
          title: "One insurer is slow",
          steps: ["Collect the claims waiting on that insurer and the days each has waited.", "List which standard each has passed or is near.", "Send the list to the insurer's contact with the dates, and note the escalation in IIP."],
        },
        tryIt: "Using the claims list, list the claims waiting longest on one insurer and draft the escalation note.",
        takeaways: ["Escalate with dates and standards, not complaints.", "A repudiation always carries a written reason.", "Disputes go to the IRA Complaints Bureau, then the Insurance Tribunal."],
      },
    ],
    quiz: [
      { q: "Which date starts the payment clock?", options: ["The date of the loss", "The date the claim was registered", "The date the discharge voucher is signed"], answer: 2, why: "The payment clocks are counted from the signed discharge voucher." },
      { q: "What is the payment deadline for a UGX 30M claim?", options: ["10 working days", "15 working days", "20 working days"], answer: 1, why: "Above UGX 10M and up to UGX 50M the tier is 15 working days." },
      { q: "What must accompany a repudiation?", options: ["A phone call", "A written reason", "A new claim form"], answer: 1, why: "A repudiation needs a written reason, which also supports any later dispute." },
    ],
  },
  {
    id: "underwriting",
    number: 1,
    title: "Agentic AI in SME and credit-linked underwriting",
    short: "Pre-screen applications and refer to the insurer with a file that is ready to decide.",
    audience: "Bancassurance team, credit officers, relationship managers who prepare insurance applications",
    duration: "1 to 1.5 days (proposed)",
    aim: "See how an AI agent gathers data, checks it against rules, scores it and prepares a file, with a person making every decision. The partner insurer underwrites; IIP improves the bank's pre-screening and referrals.",
    iipAreas: ["Acquisition", "Underwriting", "Audit"],
    criteria: ["Complete every module", "Pass the check questions"],
    modules: [
      {
        id: "how-an-agent-works",
        title: "How an underwriting agent works",
        goal: "Explain what the agent does at each step and where a person decides.",
        outcomes: ["Name the steps", "Say where the agent recommends", "Say where a person decides"],
        flow: ["Application and loan data", "Agent checks the data", "Rules and scoring", "Score with reasons", "Person decides", "Recorded in the audit log"],
        flowNote: "The agent prepares and recommends. A person always decides.",
        screen: { file: "underwriting.png", caption: "The underwriting workspace in IIP. Submission intake is not connected in this demo, so the queue is empty here: it says so instead of showing made-up cases.", callouts: ["The queue of submissions for review", "The guardrail: AI does not accept or reject", "The place a person decides"] },
        example: {
          title: "From application to a prepared file",
          steps: ["The application and loan details arrive.", "The agent checks they are complete and consistent.", "It scores the case and lists the reasons, then hands a prepared file to a person."],
        },
        tryIt: "Open Underwriting. Point to the place where the agent's work ends and a person's decision begins.",
        takeaways: ["The agent prepares; a person decides.", "A score always comes with reasons.", "Every decision is recorded."],
      },
      {
        id: "complete-application",
        title: "Capturing a complete application",
        goal: "Capture the details a good decision needs, once, correctly.",
        outcomes: ["List the details SME and loan-linked cover needs", "Spot a gap before submission", "Explain why data quality drives the score"],
        flow: ["Applicant details", "Loan details", "Business or asset details", "Gaps flagged", "Ready to assess"],
        screen: { file: "prospect-record.png", caption: "A person's record in IIP: what is known, and what is still unknown.", callouts: ["What is known", "Where it came from", "What is still unknown"] },
        example: {
          title: "What is still unknown?",
          steps: ["Open a person's record and read what is recorded.", "Read the 'Still unknown' list: those are the gaps.", "Ask for the gaps before the file goes to the insurer."],
        },
        tryIt: "Open a prospect. List three things still unknown, and say how each would change the assessment.",
        takeaways: ["A gap in the data is a gap in the score.", "Every fact shows where it came from.", "Ask once, early."],
      },
      {
        id: "reading-the-score",
        title: "Reading the score",
        goal: "Read a score and its reasons, and judge whether the reasons are sound.",
        outcomes: ["Read the score and band", "Check each reason against the facts", "Explain what a high confidence score still does not tell you"],
        flow: ["Facts recorded", "Rules applied", "Score 0 to 100", "Band and confidence", "Reasons quoted from the record", "Person challenges or accepts"],
        flowNote: "In IIP, the prospect score is built from five parts: need, fit of the cover, capacity to pay, relationship with the bank and insurer, and whether the person can be contacted.",
        screen: { file: "scores.png", caption: "Scores in IIP: each person's score, band and the two covers that fit, with reasons.", callouts: ["Score and band", "Recommended covers", "Reasons quoted from the record"] },
        example: {
          title: "Does the explanation match the facts?",
          steps: ["Read the score and the band.", "Read each reason, then find that fact in the person's record.", "If a reason is not supported by the record, do not accept the score. Record why."],
        },
        tryIt: "Open Scores, pick a person, and check each reason against their record. Say whether you accept the score.",
        takeaways: ["A score orders the work. It does not decide cover.", "Check every reason against the facts.", "High confidence is still not a decision."],
      },
      {
        id: "accept-refer-decline",
        title: "Accept, refer or decline",
        goal: "Choose between accepting the recommendation, referring, or asking for more information.",
        outcomes: ["Use thresholds and referral rules", "Prepare a referral", "Know who has the final word"],
        flow: ["Score and reasons", "Within limits?", "Recommend, person confirms", "Or refer with the file prepared", "Insurer decides"],
        screen: { missing: "A referral queue for loan-linked cover is proposed for IIP but not built yet. Today, use the Outreach and Escalations views in Acquisition to see what is waiting for a person." },
        example: {
          title: "Borderline case",
          steps: ["A score sits just under the usual comfort level.", "The reasons are sound but one fact is missing.", "Ask for the fact and re-score, or refer with a note saying exactly what is uncertain."],
        },
        tryIt: "Take a borderline person from Scores and decide: accept, refer, or ask for more. Write the reason in one sentence.",
        takeaways: ["Refer with a prepared file, not a bare form.", "Say exactly what is uncertain.", "The partner insurer makes the underwriting decision."],
      },
      {
        id: "recording-decisions",
        title: "Recording decisions",
        goal: "Record a decision and reason so that it can be audited.",
        outcomes: ["Write an audit-ready reason", "Find an entry in the audit log", "Tell an AI recommendation from a person's decision"],
        flow: ["Decision made", "Reason written", "Recorded with time and person", "Visible in the audit log"],
        screen: { file: "audit.png", caption: "The audit log: who did what, and when.", callouts: ["Filters", "The entry", "Who and when"] },
        example: {
          title: "An audit-ready reason",
          steps: ["Weak: 'Looks fine.'", "Better: 'Accepted: income and loan term are consistent with the application; no gaps open.'", "A good reason names the facts it relied on."],
        },
        tryIt: "Open Audit and find one entry. Say who acted, what they did and when.",
        takeaways: ["Write reasons a stranger could follow.", "Name the facts you relied on.", "The log shows who decided, not just what."],
      },
    ],
    quiz: [
      { q: "What does a high-confidence score still not tell you?", options: ["Nothing, it is a decision", "Whether the reasons are sound and a person agrees", "The customer's name"], answer: 1, why: "A score orders the work. A person still checks the reasons and decides." },
      { q: "In bancassurance, who makes the underwriting decision?", options: ["The bank's AI", "The partner insurer", "The customer"], answer: 1, why: "It is an agency model: the bank sells and supports, the insurer underwrites and pays." },
      { q: "What makes a recorded reason audit-ready?", options: ["It is short", "It names the facts relied on", "It says the AI agreed"], answer: 1, why: "A reason that names the facts lets a reviewer follow the decision." },
    ],
  },
  {
    id: "fraud-identity",
    number: 3,
    title: "AI for fraud and identity in credit-linked cover",
    short: "Read an alert, weigh the evidence and decide without accusing anyone.",
    audience: "Bancassurance, risk, compliance and claims staff",
    duration: "1 day (proposed)",
    aim: "See how IIP surfaces suspicious patterns with reasons a person can review, and how to record a fair outcome. An alert is a reason to look closer, never a finding.",
    iipAreas: ["Fraud", "Claims", "Audit"],
    criteria: ["Complete every module", "Pass the check questions"],
    modules: [
      {
        id: "where-risk-sits",
        title: "Where fraud and identity risk sit",
        goal: "Know the stages at which risk appears: application, claim and payout.",
        outcomes: ["Name the three stages", "Say what is checked at each", "Find the Fraud workspace"],
        flow: ["Claim or application", "Identity check", "Pattern checks", "Alert with a reason", "Human review", "Outcome recorded"],
        screen: { file: "fraud.png", caption: "The Fraud workspace: signals raised, with severity and status.", callouts: ["Signals and severity", "Why it was raised", "Status of the review"] },
        example: {
          title: "Three stages",
          steps: ["At application, check the details are consistent.", "At claim, check the claimant matches the policyholder or named beneficiary.", "At payout, check the right person is paid."],
        },
        tryIt: "Open Fraud. Name the severity levels and what each asks a person to do.",
        takeaways: ["Risk appears at application, claim and payout.", "Each signal carries a reason.", "A person reviews every signal."],
      },
      {
        id: "identity-checks",
        title: "Identity checks and national ID",
        goal: "Explain what an identity check does and what a mismatch means.",
        outcomes: ["Describe a match and a mismatch", "Know a national ID number (NIN) is 14 characters", "Use test data only in training"],
        flow: ["Claimant details", "Compared with the policy record", "Match: proceed", "Mismatch: alert with reason"],
        flowNote: "A NIN is a 14-character alphanumeric number issued by NIRA. Financial institutions can verify it digitally where they have access. Whether and how this bank and insurer have access is to be confirmed.",
        screen: { missing: "A dedicated identity check result screen is not built in IIP yet. In training, use only clearly fake identity values." },
        example: {
          title: "A mismatch is a question",
          steps: ["The claimant's ID details differ from the next of kin on file.", "This raises an alert with the reason stated.", "A person asks the claimant to clarify before anything else."],
        },
        tryIt: "Write what you would ask a claimant whose ID details do not match the record, in respectful words.",
        takeaways: ["A mismatch raises a question, not an accusation.", "Never use real ID numbers in training.", "Access to ID verification is to be confirmed."],
      },
      {
        id: "reading-an-alert",
        title: "Reading an alert",
        goal: "Read an alert, its evidence and its reasons, and weigh each piece.",
        outcomes: ["Read severity and reasons", "Weigh each piece of evidence", "Say why an alert is not a finding"],
        flow: ["Alert raised", "Read the reasons", "Check each against the file", "Weigh together", "Decide"],
        screen: { file: "fraud.png", caption: "A signal and the reasons behind it.", callouts: ["Severity", "Reasons and evidence", "Actions allowed to a person"] },
        example: {
          title: "Two pieces of evidence",
          steps: ["The claimant's ID details differ from the next of kin on file.", "The same phone number appears on another claim.", "Each is a reason to look closer. Together they justify an investigation, not a conclusion."],
        },
        tryIt: "Pick a signal in Fraud. List each piece of evidence and say how strong it is on its own.",
        takeaways: ["An alert is a reason to look closer.", "Weigh each piece of evidence.", "Say what you do not know."],
      },
      {
        id: "patterns",
        title: "Patterns across claims",
        goal: "Recognise rules-first patterns and why simple rules come before models.",
        outcomes: ["Name common patterns", "Explain why rules come first", "Know patterns are examples, not a proven list"],
        flow: ["Repeated phone or ID", "Claim soon after start", "Incomplete or reused documents", "Repeat claims on the same loan"],
        flowNote: "These are examples of patterns. They are not a complete or proven list.",
        screen: { file: "fraud.png", caption: "Signals grouped by severity help show where patterns appear.", callouts: ["Severity groups", "Counts", "Open a signal"] },
        example: {
          title: "Why rules first",
          steps: ["A simple rule is easy to explain to the customer and the insurer.", "Models improve with data over time.", "Start with what you can explain."],
        },
        tryIt: "Name two patterns and say how you would explain each to a colleague in one sentence.",
        takeaways: ["Rules first, models later.", "Patterns are examples, not proof.", "Explainable beats clever."],
      },
      {
        id: "deciding-recording",
        title: "Deciding and recording",
        goal: "Clear, investigate or escalate, and record the reason.",
        outcomes: ["Choose an outcome", "Write the reason", "Speak to a customer fairly about a flagged case"],
        flow: ["Review", "Cleared", "Investigate: ask for more", "Escalate: insurer and compliance", "Reason recorded"],
        screen: { file: "fraud.png", caption: "The actions a person can take on a signal, each recorded.", callouts: ["Review", "Record an outcome", "Close"] },
        example: {
          title: "A false alarm",
          steps: ["The phone number matched because two relatives share a phone.", "Clear the alert and write why.", "Do not tell the customer they were flagged. Recording the outcome helps the system improve."],
        },
        tryIt: "Choose an outcome for one signal and write a one-sentence reason.",
        takeaways: ["Record every outcome and why.", "Clear false alarms promptly.", "Never accuse; ask."],
      },
    ],
    quiz: [
      { q: "Why is an alert not a finding?", options: ["It is always wrong", "It is a reason to look closer, not proof", "Only managers can see it"], answer: 1, why: "An alert says something needs a look. Only a review can find out what it means." },
      { q: "What should you do before telling a customer their claim is flagged?", options: ["Tell them straight away", "Review the evidence and decide whether to say anything", "Delete the alert"], answer: 1, why: "A false alarm can hurt a customer. Review first, then decide what to say, respectfully." },
      { q: "Why are rules explained before models?", options: ["Rules are easier to explain and check", "Models are illegal", "Rules are always more accurate"], answer: 0, why: "Simple rules can be explained to customers, insurers and auditors. Models come later." },
    ],
  },
  {
    id: "governance",
    number: 4,
    title: "AI governance, explainability and compliance",
    short: "Review any AI-assisted decision with a checklist, and know where people must decide.",
    audience: "Leadership, compliance, internal audit, risk and course leads",
    duration: "Half to 1 day (proposed)",
    aim: "The foundation for the other courses: explanations a non-technical reviewer can read, an audit trail of who did what, and human oversight that is recorded. Legal obligations are to be confirmed with legal and compliance.",
    iipAreas: ["Audit", "Settings", "Insights"],
    criteria: ["Complete every module", "Pass the check questions"],
    modules: [
      {
        id: "why-governance",
        title: "Why governance matters in insurance AI",
        goal: "Understand the stakes: money, customers and regulators.",
        outcomes: ["Name who is affected", "Say why people stay in the loop", "Know that IIP prepares and humans approve"],
        flow: ["AI prepares", "Human reviews", "Decision recorded", "Available on request"],
        screen: { file: "executive.png", caption: "Insights: the workspace at a glance, with AI work shown as preparation, not decisions.", callouts: ["Summary", "AI preparation", "Human review"] },
        example: {
          title: "The line IIP draws",
          steps: ["AI may summarise, classify, extract, recommend and draft.", "AI may not approve or reject a claim, bind a policy, set a final premium or accuse anyone of fraud.", "A named person decides, and it is recorded."],
        },
        tryIt: "Find where IIP says that AI prepares work and humans approve regulated decisions.",
        takeaways: ["AI prepares. People decide.", "Every consequential decision is recorded.", "Obligations under law are to be confirmed with legal and compliance."],
      },
      {
        id: "explainability",
        title: "Explainability in practice",
        goal: "Judge whether an explanation is good enough.",
        outcomes: ["Read reasons and factors", "Spot a weak explanation", "Know the limits of an explanation"],
        flow: ["Score or alert", "Reasons", "Facts behind each reason", "Reviewer checks", "Accept or challenge"],
        screen: { file: "scores.png", caption: "A score with the reasons behind it, quoted from the record.", callouts: ["The score", "The reasons", "Facts they come from"] },
        example: {
          title: "Weak versus good",
          steps: ["Weak: 'The model rated this high.'", "Good: 'Works as a boda rider, which this cover is for; said they want it.'", "A good explanation can be checked against the record by someone who was not there."],
        },
        tryIt: "Open Scores and decide whether one explanation is good enough. Say why.",
        takeaways: ["A good reason can be checked.", "'The AI said so' is never a reason.", "Explanations have limits: say what they do not cover."],
      },
      {
        id: "audit-trails",
        title: "Audit trails",
        goal: "Find a past decision and reconstruct who decided what and why.",
        outcomes: ["Use audit filters", "Read an entry", "Tell the AI's part from the person's"],
        flow: ["AI recommendation logged", "Human review", "Agree: decision logged", "Disagree: override logged with reason", "Audit trail"],
        screen: { file: "audit.png", caption: "The audit log with filters and entries.", callouts: ["Filters", "An entry", "Who, what and when"] },
        example: {
          title: "Reconstruct a decision",
          steps: ["Filter the log to the day and the type of action.", "Open the entry and read who acted and what changed.", "Find the reason recorded with it."],
        },
        tryIt: "Find one entry in Audit and say who did what, when, and why.",
        takeaways: ["A log says what happened. An explanation says why.", "Both are needed.", "Entries are available to insurers, auditors and regulators on request."],
      },
      {
        id: "human-oversight",
        title: "Human oversight and overrides",
        goal: "Know where review is mandatory and how an override is recorded.",
        outcomes: ["Name review points", "Record an override with a reason", "Know who needs to see it"],
        flow: ["AI drafts", "Person reviews", "Approve: sent", "Edit: sent as edited", "Dismiss: recorded"],
        screen: { file: "outreach.png", caption: "Drafted messages wait for a person to read, edit and approve. Nothing goes out on its own.", callouts: ["The draft", "Edit", "Approve and send"] },
        example: {
          title: "Edited before sending",
          steps: ["The agent drafts a message.", "A person changes the wording and approves it.", "What was approved is what is sent, and the approval is recorded."],
        },
        tryIt: "Open Outreach. Find where a person must approve before a message can go out.",
        takeaways: ["No message goes out without a named person approving it.", "Overrides carry a reason.", "Review points are part of the design."],
      },
      {
        id: "data-protection",
        title: "Data protection and access",
        goal: "Describe how roles, permissions and tenant isolation protect customer data.",
        outcomes: ["Explain roles and permissions", "Explain tenant isolation in plain words", "List open legal questions"],
        flow: ["User signs in", "Role decides what they can see", "Workspace keeps one customer's data apart from another's", "Access is logged"],
        flowNote: "Which laws and regulator expectations apply, and what each requires, is to be confirmed with legal and compliance. This course teaches the process of checking.",
        screen: { file: "settings.png", caption: "Settings: the workspace, people and what each can do.", callouts: ["People", "Roles", "Workspace"] },
        example: {
          title: "Who could see this?",
          steps: ["Ask which role needed to see the data.", "Check the person had that role.", "If not, raise it."],
        },
        tryIt: "Open Settings and find where roles are shown.",
        takeaways: ["Least access that does the job.", "Workspaces are kept apart.", "Legal obligations are confirmed, not assumed."],
      },
    ],
    quiz: [
      { q: "What is the difference between a log and an explanation?", options: ["None", "A log says what happened; an explanation says why", "A log is only for IT"], answer: 1, why: "You need both: the record of events and the reasons behind them." },
      { q: "Who needs to see an override reason?", options: ["Nobody", "Reviewers, insurers and auditors who ask", "Only the customer"], answer: 1, why: "An override with no visible reason cannot be reviewed." },
      { q: "Why is 'the AI said so' not an acceptable reason?", options: ["It is rude", "It cannot be checked against the facts", "It is too short"], answer: 1, why: "A reason must be checkable by someone who was not there." },
    ],
  },
];

const HANDS_ON_CATEGORY: Record<string, string> = {
  claims: "claims-fraud",
  "fraud-identity": "claims-fraud",
  underwriting: "underwriting-risk",
  governance: "build-govern",
};

// The programme courses first, then the hands-on ones, numbered in the order the catalogue shows them.
export const COURSES: Course[] = [
  ...PROGRAMME_COURSES,
  ...HANDS_ON.map((course) => ({ ...course, category: HANDS_ON_CATEGORY[course.id] ?? "build-govern" })),
].map((course, index) => ({ ...course, number: index + 1 }));

export const getCourse = (id: string) => COURSES.find((course) => course.id === id) ?? null;
