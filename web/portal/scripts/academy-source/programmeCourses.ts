// The IIP Academy programme: five courses for insurers and bancassurance teams. Each is described here; its
// lessons and check questions are in lessons/. A course with no lessons yet would carry an outline and be listed
// as not startable (see isReady in courses.ts).
//
// The same honesty rules as the hands-on courses apply: only features IIP really has are described as IIP
// features; figures about the market are attributed and dated; forecasts are called forecasts; what a law or a
// regulator requires is "to be confirmed with legal and compliance"; all examples are synthetic; the
// certificate is a certificate of completion, not an accredited qualification.

import type { Course } from "./courses";
import { AI_AGENTIC_MODULES, AI_AGENTIC_QUIZ } from "./lessons/aiAgentic";
import { CLAIMS_FRAUD_MODULES, CLAIMS_FRAUD_QUIZ } from "./lessons/claimsFraud";
import { UNDERWRITING_RISK_MODULES, UNDERWRITING_RISK_QUIZ } from "./lessons/underwritingRisk";
import { SALES_DISTRIBUTION_MODULES, SALES_DISTRIBUTION_QUIZ } from "./lessons/salesDistribution";
import { AGENTIC_SYSTEMS_MODULES, AGENTIC_SYSTEMS_QUIZ } from "./lessons/agenticSystems";

export const PROGRAMME_COURSES: Course[] = [
  {
    id: "ai-agentic-insurance",
    number: 0,
    category: "ai-foundations",
    title: "AI & Agentic AI for Insurance",
    short: "Foundations of AI, generative AI and agentic AI across the insurance value chain, taught practically through IIP.",
    audience: "Insurance and bancassurance leaders, heads of claims, underwriting and distribution, compliance and risk officers, and anyone who must judge AI before buying or running it",
    duration: "2 days (proposed)",
    aim: "Understand what AI, generative AI and agentic AI each do, where each belongs in the insurance value chain, and how a human-supervised system keeps a person in charge of every consequential decision. Learn to tell what works today from what is still a forecast, using IIP as the worked example.",
    iipAreas: ["Command Center", "Inbox", "Claims", "Data Hub", "Audit"],
    criteria: ["Complete every lesson", "Pass the check questions"],
    outcomes: [
      "Explain AI, generative AI and agentic AI in plain words, and say what each is good and bad at",
      "Place AI on the insurance value chain: acquire, underwrite, serve, claim and retain",
      "Describe how an AI agent works: a model, tools it may use, and a person who stays in charge",
      "Explain why answers must be grounded in approved sources and carry a citation",
      "Name the main ways an agent fails, using the OWASP Top 10 for LLM Applications (2025)",
      "Choose a first use case and say how its results will be measured honestly",
    ],
    modules: AI_AGENTIC_MODULES,
    quiz: AI_AGENTIC_QUIZ,
  },
  {
    id: "ai-claims-fraud",
    number: 0,
    category: "claims-fraud",
    title: "AI-Powered Claims Management & Fraud Intelligence",
    short: "Claims intake, document intelligence, assessment, fraud detection, workflow automation, settlement and customer communication.",
    audience: "Claims managers and handlers, bancassurance claims coordinators, fraud and special-investigation teams, and insurer operations leaders",
    duration: "2 days (proposed)",
    aim: "Follow a claim from first notice to payment and see where AI shortens the work without taking the decision: intake across channels, reading documents, preparing an assessment, raising fraud signals as questions for a person, tracking the regulator's service standards, and drafting customer updates a person approves.",
    iipAreas: ["Claims", "Inbox", "Documents", "Fraud", "Insights", "Audit"],
    criteria: ["Complete every lesson", "Pass the check questions"],
    outcomes: [
      "Map a claim from loss to payment and say who owns each step",
      "Take claims in from WhatsApp, the branch and email into one record",
      "Use document intelligence to classify documents, read the fields and list what is missing",
      "Prepare an evidence-linked assessment summary for a human assessor to decide on",
      "Treat fraud signals as reasons to look closer, never as an accusation",
      "Track each claim against the regulator's service standards and act before one is missed",
      "Draft customer updates that a person reviews and approves before they are sent",
    ],
    modules: CLAIMS_FRAUD_MODULES,
    quiz: CLAIMS_FRAUD_QUIZ,
  },
  {
    id: "ai-underwriting-risk",
    number: 0,
    category: "underwriting-risk",
    title: "AI for Underwriting & Risk Intelligence",
    short: "AI-assisted underwriting, risk profiling, pricing intelligence, portfolio analysis and decision support.",
    audience: "Underwriters, actuarial and pricing analysts, credit and relationship officers who prepare applications, portfolio and risk managers",
    duration: "2 days (proposed)",
    aim: "See how AI prepares an application so that the underwriter receives a file that is ready to decide: documents read, gaps listed, risk profile drawn from the record, portfolio context shown. Learn what AI may inform in pricing and what only a person may set, and how fairness and explanation are checked.",
    iipAreas: ["Underwriting", "Documents", "Insights", "Data Hub", "Audit"],
    criteria: ["Complete every lesson", "Pass the check questions"],
    outcomes: [
      "Describe the underwriting cycle and the points where AI saves time",
      "Triage submissions: read the documents and list what is missing before referral",
      "Build a risk profile from the record, with the facts behind each point",
      "Say what AI may inform in pricing and what must be set by a person",
      "Read portfolio views for concentration and trend",
      "Refer to an underwriter with a complete file, and record the decision and its reason",
      "Check a model for fairness and for drift, and explain a decision to the customer",
    ],
    modules: UNDERWRITING_RISK_MODULES,
    quiz: UNDERWRITING_RISK_QUIZ,
  },
  {
    id: "ai-sales-distribution",
    number: 0,
    category: "sales-experience",
    title: "AI for Insurance Sales, Distribution & Customer Experience",
    short: "Using AI across WhatsApp and digital channels for customer acquisition, policy enquiries, lead qualification, renewals, cross-selling and retention.",
    audience: "Bancassurance and branch teams, agency and distribution managers, digital and marketing teams, customer service leads",
    duration: "2 days (proposed)",
    aim: "Uganda's insurance premiums grew about 10 per cent to UGX 1.76 trillion in 2024, yet penetration is still under 1 per cent of GDP (Insurance Regulatory Authority). Learn how a WhatsApp and digital assistant reaches more people at lower cost: answering enquiries from approved sources, qualifying leads, helping with applications, supporting renewals and retention, always with consent and with a person ready to take over.",
    iipAreas: ["Inbox", "Acquisition", "Outreach", "Command Center", "Data Hub"],
    criteria: ["Complete every lesson", "Pass the check questions"],
    outcomes: [
      "Design a customer journey that starts on WhatsApp and hands over cleanly to a person",
      "Capture and qualify leads with the facts the sales team needs",
      "Answer policy and product questions from approved sources, with a citation",
      "Support quotes and applications in conversation, leaving binding to a person",
      "Plan renewals, cross-selling and retention with consent and an easy way to opt out",
      "Serve customers in their own language, and say when a translation has not been checked",
      "Measure conversion and service quality without overstating results",
    ],
    modules: SALES_DISTRIBUTION_MODULES,
    quiz: SALES_DISTRIBUTION_QUIZ,
  },
  {
    id: "agentic-systems",
    number: 0,
    category: "build-govern",
    title: "Building Agentic AI Systems for Insurance",
    short: "A technical course covering AI agents, RAG, workflow orchestration, APIs, insurance-system integration, security, governance and production deployment using IIP as the practical environment.",
    audience: "Software and data engineers, solution architects, integration and platform teams, technical leads and security staff at insurers, banks and their vendors",
    duration: "3 days (proposed)",
    aim: "Build and run an agentic system the way a regulated insurer needs it: an agent that uses tools, retrieval over approved knowledge, durable workflows, integrations with core systems, and strong security, with governance designed in from the start. IIP is the working reference for each part.",
    iipAreas: ["Command Center", "Data Hub", "Audit", "Settings", "Inbox"],
    criteria: ["Complete every lesson", "Pass the check questions"],
    outcomes: [
      "Describe an agent's parts: model, tools, loop and state, and set the limits on each",
      "Build retrieval over approved documents, with chunking, an index and citations",
      "Run work as durable, retryable and idempotent workflows",
      "Integrate policy, claims, messaging and email systems through APIs",
      "Secure the system: tenant isolation, secrets, least-privilege tools and defences against prompt injection",
      "Put human approval, evidence and audit into the design, not on top of it",
      "Test and monitor agents, and deploy and operate them in production",
    ],
    modules: AGENTIC_SYSTEMS_MODULES,
    quiz: AGENTIC_SYSTEMS_QUIZ,
  },
];
