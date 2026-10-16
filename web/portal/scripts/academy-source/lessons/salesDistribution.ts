import type { Module, Question } from "../courses";

// Course: AI for Insurance Sales, Distribution & Customer Experience. All examples are synthetic. Market figures
// and platform terms are attributed and dated; legal and regulatory points are to be confirmed with legal and
// compliance.

export const SALES_DISTRIBUTION_MODULES: Module[] = [
  {
    id: "customer-journey",
    title: "The customer journey on WhatsApp and digital channels",
    goal: "Map how customers actually reach an insurer, and where an assistant helps and where a person must step in.",
    outcomes: ["Map a customer's journey on WhatsApp", "Mark where the assistant helps", "Mark where a person steps in"],
    flow: ["Customer writes", "Assistant greets and asks", "Needs understood", "Product matched", "Person takes over to apply", "Follow-up"],
    flowNote: "In 2024 Uganda's insurance premiums grew about 10 per cent to UGX 1.76 trillion, while penetration stayed under 1 per cent of GDP (Insurance Regulatory Authority). Most people are still not reached.",
    screen: { file: "acquisition.png", caption: "The acquisition dashboard: people who might take cover, from WhatsApp conversations, bank lists and the team.", callouts: ["Counts by kind of prospect", "The prospect queue", "Tabs for scores, outreach and channels"] },
    example: {
      title: "A boutique owner writes at lunchtime",
      steps: ["The customer says they own a boutique and asks what cover exists.", "The assistant asks what they sell and where, and records what the customer said.", "A person is offered the lead with those facts, ready to follow up."],
    },
    tryIt: "Open Acquisition and look at the five counts across the top. Say which kind of customer you would speak to first, and why.",
    takeaways: ["Meet customers where they already are.", "The assistant gathers facts; a person closes.", "Reach is the opportunity: most people are not yet insured."],
  },
  {
    id: "lead-qualification",
    title: "Lead capture and qualification",
    goal: "Collect the details that matter, classify the need, score interest, and route the lead to the right person.",
    outcomes: ["List the details worth capturing", "Say how interest is classified", "Route a lead to the right person"],
    flow: ["Message received", "Facts captured", "Need classified", "Interest scored", "Routed to an officer", "Follow-up agreed"],
    flowNote: "Each fact is stored with who said it and when, so an officer can see what is the customer's own word.",
    screen: { file: "prospect-record.png", caption: "A lead's record: relationship to the insurer, stage, whether follow-up was agreed, product interest, and what the customer said and when.", callouts: ["Stage and follow-up agreed", "Product interest", "Facts with who said them and when"] },
    example: {
      title: "A warm lead",
      steps: ["The customer asked about cover for stock and agreed to be contacted.", "The record shows the product of interest and that follow-up was agreed.", "An officer is assigned and opens the conversation to continue."],
    },
    tryIt: "Open a prospect and find where it says whether follow-up was agreed. Say why that matters before you contact them.",
    takeaways: ["Capture facts as the customer said them.", "Record whether follow-up was agreed.", "Route to a named person."],
  },
  {
    id: "policy-enquiries",
    title: "Policy and product enquiries",
    goal: "Answer from approved sources, cite them, and say clearly when the answer needs a person.",
    outcomes: ["Answer from approved wording", "Cite the source", "Say when a person must confirm"],
    flow: ["Question", "Approved sources searched", "Passage found", "Answer with citation", "Not found: hand to a person"],
    flowNote: "An assistant must not answer about cover from memory. If it cannot cite an approved source, it says a person will confirm.",
    screen: { file: "data-hub.png", caption: "The Data Hub holds the approved sources. An answer about cover is grounded in them and logged with its sources.", callouts: ["Approved sources only", "Ask a policy question", "Answers logged with sources"] },
    example: {
      title: "Is stock covered against fire?",
      steps: ["The assistant searches the approved product wording in force.", "It finds the clause and explains in plain words, citing it.", "For an exclusion it cannot find, it says a colleague will confirm."],
    },
    tryIt: "Write three questions a customer might ask about a product, and where in the approved wording each answer would be.",
    takeaways: ["Answers about cover cite approved wording.", "Wording in force on the date matters.", "No source means a person confirms."],
  },
  {
    id: "quotes-applications",
    title: "Quotes and applications in conversation",
    goal: "Guide a customer through an application and prepare it for review, while binding cover and final premiums stay with a person.",
    outcomes: ["Guide an application in conversation", "Prepare it for review", "Leave binding to a person"],
    flow: ["Customer wants to apply", "Details collected", "Documents requested", "File prepared", "Person reviews", "Person binds cover"],
    flowNote: "The assistant never binds a policy or sets a final premium. It prepares the application so a person can.",
    screen: { file: "scores.png", caption: "A score with its reasons, quoted from the record, so an officer can see why a lead is ready.", callouts: ["The score", "The reasons", "The facts they come from"] },
    example: {
      title: "Ready to apply",
      steps: ["A customer says they want the shop cover and sends the details asked for.", "The record shows the lead as ready to apply, with the reasons.", "An officer reviews, confirms the premium and binds the cover."],
    },
    tryIt: "Open Scores and read the reasons for one lead. Say whether you would contact them and why.",
    takeaways: ["Prepare the file; a person binds.", "Show the reasons for readiness.", "A quote is advice until a person confirms it."],
  },
  {
    id: "renewals-retention",
    title: "Renewals, cross-selling and retention",
    goal: "Time reminders, suggest relevant cover, and respect consent and opt-out in every message.",
    outcomes: ["Plan a renewal reminder", "Suggest relevant cover", "Respect consent and opt-out"],
    flow: ["Renewal date approaching", "Reminder drafted", "Person approves", "Sent in the customer's language", "Reply handled", "Outcome recorded"],
    flowNote: "Only contact people who agreed to be contacted, and make it easy to stop. A person approves outreach before it is sent.",
    screen: { file: "outreach.png", caption: "Drafted messages wait for a person to read, edit and approve. Nothing goes out on its own.", callouts: ["The draft", "Edit", "Approve and send"] },
    example: {
      title: "A renewal reminder",
      steps: ["A customer's cover renews in three weeks and they agreed to follow-up.", "A reminder is drafted with the date and what to do.", "An officer approves it and it is sent; the reply comes back to the same conversation."],
    },
    tryIt: "Open Outreach and find where a person approves a draft. Say what you would check before approving.",
    takeaways: ["Consent first, every time.", "A person approves outreach.", "Make stopping easy."],
  },
  {
    id: "language-experience",
    title: "Language, tone and experience",
    goal: "Reply in the customer's language, keep the tone of the bank or insurer, and label machine translation honestly.",
    outcomes: ["Reply in the customer's language", "Keep the brand's tone", "Label machine translation honestly"],
    flow: ["Customer writes in their language", "Language recognised", "Reply in the same language", "Machine translation labelled", "Person can take over"],
    flowNote: "A reply that was translated by a computer and has not been checked by a native speaker should say so to the staff who read it.",
    screen: { missing: "A screenshot of a customer conversation with a translation note needs real conversation data, which this demonstration workspace does not contain. IIP records the customer's language on each conversation, and a note when a reply was translated by a computer." },
    example: {
      title: "A message in Kiswahili",
      steps: ["The customer writes in Kiswahili about stock for a shop.", "The assistant replies in Kiswahili and notes it was translated by a computer.", "A staff member reads the note and can check or take over."],
    },
    tryIt: "Pick the two languages your customers use most. Write who in your team could check a reply in each.",
    takeaways: ["Answer in the customer's language.", "Say when a translation is unchecked.", "Keep the bank's tone."],
  },
  {
    id: "channel-rules",
    title: "Rules of the channel",
    goal: "Respect consent, data protection and Meta's WhatsApp Business terms, which since 15 January 2026 do not allow general-purpose AI assistants on the platform but do allow customer-service use; confirm details with legal and compliance.",
    outcomes: ["Name the consent and data rules", "Explain the WhatsApp Business policy on AI", "Know who confirms the details"],
    flow: ["Consent recorded", "Data kept to what is needed", "Channel terms respected", "Person approves outreach", "Audit trail kept"],
    flowNote: "WhatsApp Business terms changed in October 2025, effective 15 January 2026: businesses whose main product is a general-purpose AI assistant cannot use the API to deliver it. Customer-service and operational uses remain allowed. Which data protection rules apply, including Uganda's Data Protection and Privacy Act, 2019, is to be confirmed with legal and compliance.",
    screen: { file: "settings.png", caption: "Settings: the workspace, people and roles, so access to customer data follows the job.", callouts: ["People", "Roles", "Workspace"] },
    example: {
      title: "Before a campaign",
      steps: ["List everyone on the contact list and whether each agreed to be contacted.", "Remove anyone who did not agree or who asked to stop.", "Have compliance read the message before it is approved."],
    },
    tryIt: "Write the three questions you would ask legal before sending a renewal campaign over WhatsApp.",
    takeaways: ["Consent and opt-out come first.", "Channel terms are part of the rules.", "Legal confirms; do not assume."],
  },
  {
    id: "measuring-sales",
    title: "Measuring sales and service",
    goal: "Track response time, conversion and satisfaction against a baseline, and report only what was measured.",
    outcomes: ["Choose three measures", "Set a baseline", "Report what was measured"],
    flow: ["Choose measures", "Baseline", "Change one thing", "Measure again", "Report honestly"],
    flowNote: "Response time, share of leads that become applications, and customer satisfaction are enough to start.",
    screen: { file: "executive.png", caption: "Insights: trends over time from the workspace's own records.", callouts: ["A measure over time", "Comparison with an earlier period", "Drawn from the records"] },
    example: {
      title: "A first month",
      steps: ["Record how long leads wait for a first reply today.", "Run the assistant for a month with officers following up.", "Measure again and report both numbers with the period."],
    },
    tryIt: "Pick one measure for your sales team and write how you would get its baseline.",
    takeaways: ["Few measures, measured the same way.", "Baseline before change.", "Report what you measured."],
  },
];

export const SALES_DISTRIBUTION_QUIZ: Question[] = [
  { q: "Who closes the sale and binds cover?", options: ["The assistant", "A person", "The customer"], answer: 1, why: "The assistant prepares the application. A person confirms the premium and binds the cover." },
  { q: "An assistant cannot find a clause on an exclusion. What should it do?", options: ["Guess", "Say a person will confirm", "Say it is not covered"], answer: 1, why: "Without an approved source it says a person will confirm." },
  { q: "Before a renewal campaign, what comes first?", options: ["Writing the message", "Checking who agreed to be contacted", "Choosing a time"], answer: 1, why: "Consent comes first. Only contact people who agreed, and make it easy to stop." },
  { q: "A reply was translated by a computer. What should staff see?", options: ["Nothing", "A note that it has not been checked by a native speaker", "A different customer"], answer: 1, why: "Say so honestly, so staff can check or take over." },
  { q: "What do Meta's WhatsApp Business terms say about AI from 15 January 2026?", options: ["All AI is banned", "General-purpose AI assistants are not allowed, customer-service use is", "Nothing changed"], answer: 1, why: "Businesses whose main product is a general-purpose AI assistant cannot use the API to deliver it. Customer-service uses remain allowed. Confirm details with legal." },
];
