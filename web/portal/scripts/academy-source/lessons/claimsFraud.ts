import type { Module, Question } from "../courses";

// Course: AI-Powered Claims Management & Fraud Intelligence. All examples are synthetic. The service standards
// are those used in the hands-on claims course; confirm the current figures with the Insurance Regulatory
// Authority and your insurer.

export const CLAIMS_FRAUD_MODULES: Module[] = [
  {
    id: "claims-journey",
    title: "The claims journey and where AI helps",
    goal: "See the whole path from notice to payment and the steps where waiting, re-keying and chasing documents cost the most time.",
    outcomes: ["Name the steps from notice to payment", "Find where time is lost", "Mark where AI prepares and where a person decides"],
    flow: ["Notice of loss", "Registration", "Documents", "Assessment", "Offer or repudiation", "Payment"],
    flowNote: "In bancassurance the bank often takes the notice and supports the customer. The insurer decides and pays.",
    screen: { file: "claims-list.png", caption: "The work queue: every open claim, its status, its documents and its service clock.", callouts: ["Filters for what needs attention", "Status, documents and clock per claim", "Open a claim for its detail"] },
    example: {
      title: "Where does a motor claim wait?",
      steps: ["Count the days between each step on five closed claims.", "The longest gaps are usually waiting for documents and waiting for a reply.", "Those are the steps where AI preparation saves the most time."],
    },
    tryIt: "Open Claims and pick one claim. Say which step it is at and what it is waiting for.",
    takeaways: ["Most delay is waiting, not deciding.", "AI shortens the preparation, not the decision.", "Every claim should show its current step."],
  },
  {
    id: "claims-intake",
    title: "Intake from WhatsApp, branch and email",
    goal: "Capture the same facts however the customer reports the loss, and link every message and document to one claim.",
    outcomes: ["List the facts every claim needs at intake", "Link messages and documents to one claim", "Hand over from the assistant to a person"],
    flow: ["Customer reports", "Assistant or staff asks the key questions", "Facts captured once", "Claim opened", "Documents requested", "Person takes over"],
    flowNote: "On WhatsApp the assistant can collect the facts in conversation and send a secure link for documents. A person can step in at any time.",
    screen: { file: "claim-detail.png", caption: "A claim: its summary, status, documents and the steps that need a person.", callouts: ["Summary and status", "Documents received", "Actions that need a person"] },
    example: {
      title: "A customer writes on WhatsApp at night",
      steps: ["The assistant asks what happened, when, and which policy, and records the answers.", "It opens the claim and sends a secure link to upload the documents.", "In the morning a claims officer sees the claim with everything already attached."],
    },
    tryIt: "Open a claim and list the facts you would need if it had arrived as a voice note.",
    takeaways: ["Capture the facts once, in one record.", "Every message and document belongs to a claim.", "A person can take over at any moment."],
  },
  {
    id: "document-intelligence",
    title: "Document intelligence",
    goal: "Classify each document, read its fields with a confidence the reviewer can see, and list exactly what is still missing.",
    outcomes: ["Read a document's type and confidence", "Know what a person must still confirm", "List what is missing"],
    flow: ["Document arrives", "Type classified", "Fields read with a confidence", "Person checks what the AI read", "Missing items listed"],
    flowNote: "Anything the AI is unsure about is treated as still missing until a person confirms it.",
    screen: { file: "documents.png", caption: "The document library: each document with its type, status and the confidence of what was read.", callouts: ["Type and status", "Confidence per document", "Review opens the document"] },
    example: {
      title: "A discharge note is unclear",
      steps: ["The system reads the note and marks the date of discharge with medium confidence.", "A person opens the document, sees the date is smudged and corrects it.", "The correction is recorded and improves how often the classifier matches a person."],
    },
    tryIt: "Open the document library and find one document with medium confidence. Say what a person should check.",
    takeaways: ["Confidence tells the reviewer where to look.", "Unsure means still missing, never assumed.", "Corrections are recorded."],
  },
  {
    id: "assessment-support",
    title: "Assessment support",
    goal: "Prepare a summary of what happened, what the file holds, what is missing and what to watch, each line tied to its source, for a person to decide on.",
    outcomes: ["Write the four parts of a claim summary", "Tie each line to a source", "Leave the decision to the assessor"],
    flow: ["What happened", "What we have", "What is missing", "What to watch", "Assessor decides"],
    flowNote: "A summary is a draft for the assessor. It never approves, rejects or sets an amount.",
    screen: { file: "claim-detail.png", caption: "The claim page puts what needs a decision first, with the documents and checks behind it.", callouts: ["What needs a decision", "Documents against documents required", "Where the reviewer records the decision"] },
    example: {
      title: "A four-line summary",
      steps: ["What happened: a rear-end collision reported on the day, photographs attached.", "What we have and what is missing: police report present, repair estimate missing.", "What to watch: the estimate is above the usual range for this model, to be checked by the assessor."],
    },
    tryIt: "Take one claim and write the four-line summary, naming the source for each line.",
    takeaways: ["Four parts: happened, have, missing, watch.", "Every line points to its source.", "The assessor decides."],
  },
  {
    id: "fraud-intelligence",
    title: "Fraud intelligence",
    goal: "Understand signals such as inconsistent dates, repeated details and identity mismatches, and how a person disposes of each one with a recorded reason.",
    outcomes: ["Name common fraud signals", "Explain why a signal is a question, not a finding", "Record a disposition with a reason"],
    flow: ["Signal raised", "Person reviews the facts", "Dismiss, monitor or escalate", "Reason recorded", "Audit trail"],
    flowNote: "A signal is a reason to look closer. The system does not accuse anyone of fraud. Only a person, following the insurer's process, can reach a finding.",
    screen: { file: "fraud.png", caption: "Fraud signals with the facts behind each one, and a place for the reviewer to record what was decided and why.", callouts: ["The signal", "The facts it rests on", "The reviewer's decision and reason"] },
    example: {
      title: "Two claims, one phone number",
      steps: ["The system flags that two unrelated customers gave the same phone number.", "A reviewer checks both files: they are a mother and son who share a phone.", "The signal is dismissed with that reason, so it does not come back unexplained."],
    },
    tryIt: "Open Fraud and read one signal. Say what facts you would check before deciding.",
    takeaways: ["A signal is a question, not a verdict.", "Check the facts before deciding.", "Record the reason, including when you dismiss."],
  },
  {
    id: "service-clocks",
    title: "Workflow automation and the service clocks",
    goal: "Use the regulator's standards (acknowledge in 3 working days, payment in 10, 15 or 20 working days by amount) to see which claims are about to breach.",
    outcomes: ["State the main service standards", "Work out which clock is running", "Act before a standard is missed"],
    flow: ["Acknowledge: 3 working days", "Assessor: 3 days (5 if complex)", "Offer or repudiation: 5 days after the final report", "Discharge voucher signed", "Payment: 10, 15 or 20 working days"],
    flowNote: "Payment is 10 working days up to UGX 10M, 15 above 10M up to 50M, and 20 above 50M. The clock runs from the signed discharge voucher. Confirm current figures with the regulator.",
    screen: { file: "claims-list.png", caption: "Each claim shows its service clock, so the ones near a deadline can be handled first.", callouts: ["The clock per claim", "Filter to claims needing attention", "Open one to act"] },
    example: {
      title: "Which claim first?",
      steps: ["Three claims: one near the acknowledgement deadline, one waiting on an assessor, one awaiting payment.", "Name the clock on each and count the working days left.", "Act on the one with the fewest days left, not the largest amount."],
    },
    tryIt: "From the work queue, choose the three claims you would act on first and say which clock is running on each.",
    takeaways: ["Every claim has one running clock.", "Urgency is the nearest deadline.", "Automation reminds. A person acts."],
  },
  {
    id: "settlement-communication",
    title: "Settlement and customer communication",
    goal: "Draft clear, honest updates in the customer's language, keep a person's approval on everything that is sent, and record what was said.",
    outcomes: ["Draft a clear update", "Keep a person's approval on what is sent", "Record what was sent"],
    flow: ["Event on the claim", "Draft prepared", "Person edits and approves", "Sent in the customer's language", "Recorded"],
    flowNote: "Nothing about an offer, a repudiation or a payment goes to the customer without a named person approving it.",
    screen: { file: "outreach.png", caption: "Drafted messages wait for a person to read, edit and approve. Nothing goes out on its own.", callouts: ["The draft", "Edit", "Approve and send"] },
    example: {
      title: "A payment update",
      steps: ["The claim reaches the payment step. The system drafts a short message with the amount and the date.", "The officer checks the amount, softens a sentence and approves.", "The message and who approved it are recorded on the claim."],
    },
    tryIt: "Open Outreach and find where a person must approve before a message can go out.",
    takeaways: ["Plain words, the customer's language.", "A person approves every update.", "What was sent is recorded."],
  },
  {
    id: "measuring-claims",
    title: "Measuring a claims operation",
    goal: "Choose turnaround, completeness and complaint measures, set a baseline first, and report only what was measured.",
    outcomes: ["Choose three measures", "Set a baseline", "Report what was measured"],
    flow: ["Choose measures", "Baseline", "Change one thing", "Measure again", "Report honestly"],
    flowNote: "Targets are targets. Report results you measured, with the period and the number of claims behind them.",
    screen: { file: "executive.png", caption: "Insights: trends over time drawn from the workspace's own records.", callouts: ["A measure over time", "Comparison with an earlier period", "Drawn from the records"] },
    example: {
      title: "Three measures",
      steps: ["Days from notice to acknowledgement, share of files complete on first submission, complaints per hundred claims.", "Record this month's figures as the baseline.", "After a change, compare the same measures over the same length of time."],
    },
    tryIt: "Pick one measure for your claims team and write how you would get its baseline.",
    takeaways: ["Few measures, measured the same way.", "Baseline before change.", "Never report a result you did not measure."],
  },
];

export const CLAIMS_FRAUD_QUIZ: Question[] = [
  { q: "Where is the most time usually lost in a claim?", options: ["The decision itself", "Waiting for documents and replies", "Printing"], answer: 1, why: "Most delay is waiting. That is where AI preparation helps most." },
  { q: "When a document's confidence is medium, what should happen?", options: ["It is accepted", "A person checks what the AI read", "It is deleted"], answer: 1, why: "Anything the AI is unsure about is treated as still missing until a person confirms it." },
  { q: "What is a fraud signal?", options: ["A finding of fraud", "A reason for a person to look closer", "A reason to refuse a claim"], answer: 1, why: "A signal is a question. Only a person following the insurer's process can reach a finding." },
  { q: "What starts the payment clock?", options: ["The date of the loss", "The date the claim was registered", "The date the discharge voucher is signed"], answer: 2, why: "The payment standards are counted from the signed discharge voucher." },
  { q: "A drafted message about a payment is ready. What happens next?", options: ["It is sent at once", "A named person edits and approves it", "It is held for a week"], answer: 1, why: "Nothing goes to the customer about an offer, repudiation or payment without a named person approving it." },
];
