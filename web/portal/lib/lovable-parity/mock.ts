// Development-only LovableVisualParity fixtures.
// Do not use this module from staging, production, ManagedApplicationReviewer,
// or customer-facing deployments. Replace with /api/v1 contracts as each slice
// is backend-backed.

import { syntheticWorkspaceSnapshot } from "@/lib/lovable-parity/syntheticWorkspaceSnapshot";
import type { RiskState } from "@/lib/lovable-parity/syntheticWorkspaceSnapshot";
export type { Claim, ClaimStatus, SLAState, RiskState } from "@/lib/lovable-parity/syntheticWorkspaceSnapshot";
export const claims = syntheticWorkspaceSnapshot.claims;

export const activityStream = [
  { time: "09:42", actor: "AI Copilot", text: "Prepared claim summary for CLM-1048 — 3 evidence links, medium-risk advisory.", type: "ai" },
  { time: "09:31", actor: "T. Okello", text: "Marked evidence reviewed on CLM-1061.", type: "human" },
  { time: "09:14", actor: "AI Copilot", text: "Extracted 12 fields from repair quotation on CLM-1082 (avg. confidence 0.87).", type: "ai" },
  { time: "08:58", actor: "System", text: "WhatsApp connector re-connected. Backlog cleared.", type: "system" },
  { time: "08:41", actor: "S. Achieng", text: "Requested supervisor review on CLM-1064.", type: "human" },
];

export const leads = [
  { id: "LD-2201", name: "Brian Ssemakula", source: "WhatsApp", product: "Motor Comprehensive", score: 0.88, status: "Qualified", officer: "M. Kato" },
  { id: "LD-2205", name: "Rita Naluyima", source: "Web", product: "Third-party Motor", score: 0.61, status: "New", officer: "Unassigned" },
  { id: "LD-2211", name: "Charles Odongo", source: "Broker", product: "Fleet", score: 0.79, status: "In Conversation", officer: "M. Kato" },
  { id: "LD-2214", name: "Amina Yusuf", source: "Email", product: "Motor Comprehensive", score: 0.44, status: "Needs KYC", officer: "L. Byaruhanga" },
];

export const auditEvents = [
  { time: "10:12", user: "AI Copilot", action: "Field extraction", target: "CLM-1048 / Police report", targetType: "document", severity: "info", evidence: "EV-88291", run: "run_9f2a", corr: "corr_a12b", model: "gpt-4o · prompt v3.2" },
  { time: "10:05", user: "T. Okello", action: "Human override", target: "CLM-1064 / Risk state → High", targetType: "claim", severity: "warning", evidence: "EV-88240", run: "-", corr: "corr_a12a", model: "-" },
  { time: "09:58", user: "AI Copilot", action: "Draft customer update", target: "CLM-1082", targetType: "communication", severity: "info", evidence: "EV-88221", run: "run_9f19", corr: "corr_a129", model: "gpt-4o · prompt v2.9" },
  { time: "09:41", user: "J. Mugisha", action: "Approved next workflow step", target: "CLM-1090", targetType: "approval", severity: "success", evidence: "EV-88190", run: "-", corr: "corr_a128", model: "-" },
  { time: "09:22", user: "AI Copilot", action: "Advisory signal raised", target: "CLM-1071 / duplicate claimant pattern", targetType: "claim", severity: "warning", evidence: "EV-88170", run: "run_9f04", corr: "corr_a127", model: "gpt-4o · prompt v1.8" },
  { time: "09:03", user: "G. Wanjiku", action: "Knowledge document approved", target: "Motor Claims SOP v4.3", targetType: "knowledge_document", severity: "success", evidence: "EV-88160", run: "-", corr: "corr_a126", model: "-" },
  { time: "08:47", user: "System", action: "Integration event", target: "Microsoft Graph · degraded (2.4s)", targetType: "integration", severity: "warning", evidence: "EV-88155", run: "-", corr: "corr_a125", model: "-" },
  { time: "08:31", user: "T. Okello", action: "Agent policy updated", target: "Customer Update Agent · v1.4", targetType: "agent_policy", severity: "info", evidence: "EV-88140", run: "-", corr: "corr_a124", model: "-" },
];

export const trendData = [
  { m: "Jan", tat: 6.8, sla: 12, ai: 420 },
  { m: "Feb", tat: 6.2, sla: 14, ai: 610 },
  { m: "Mar", tat: 5.7, sla: 11, ai: 780 },
  { m: "Apr", tat: 5.1, sla: 9, ai: 940 },
  { m: "May", tat: 4.6, sla: 8, ai: 1120 },
  { m: "Jun", tat: 4.1, sla: 6, ai: 1284 },
];

// ---------- Channels ----------
export type Channel = "WhatsApp" | "Email" | "Web" | "SMS" | "Broker" | "Teams";
export interface ChannelConversation {
  id: string;
  channel: Channel;
  customer: string;
  matched: boolean;
  snippet: string;
  updated: string;
  unread: number;
  needsApproval: boolean;
  intent: "New claim" | "Claim update" | "Quote request" | "Complaint" | "General";
}
export const conversations: ChannelConversation[] = [
  { id: "CN-9001", channel: "WhatsApp", customer: "Aisha Nakato", matched: true, snippet: "Uploading the police report photos now, one moment.", updated: "2 min ago", unread: 2, needsApproval: false, intent: "Claim update" },
  { id: "CN-9002", channel: "Email", customer: "David Mensah", matched: true, snippet: "Reattaching driving permit as requested.", updated: "18 min ago", unread: 1, needsApproval: true, intent: "Claim update" },
  { id: "CN-9003", channel: "Web", customer: "Unknown (walk-in)", matched: false, snippet: "Hi, I want to know how to add my new car to my policy.", updated: "34 min ago", unread: 1, needsApproval: false, intent: "Quote request" },
  { id: "CN-9004", channel: "WhatsApp", customer: "Fatima Suleiman", matched: true, snippet: "Please tell me the status of my claim.", updated: "1 hr ago", unread: 0, needsApproval: true, intent: "Claim update" },
  { id: "CN-9005", channel: "SMS", customer: "+256 772 118 402", matched: false, snippet: "Requesting call back re: motor policy.", updated: "2 hr ago", unread: 0, needsApproval: false, intent: "General" },
  { id: "CN-9006", channel: "Broker", customer: "Kato Partners · Ivan K.", matched: true, snippet: "Submitting three motor renewal quote requests.", updated: "yesterday", unread: 0, needsApproval: false, intent: "Quote request" },
];

// ---------- Customers ----------
export interface Customer {
  id: string;
  name: string;
  contact: string;
  policies: string[];
  activeClaims: number;
  region: string;
  channelPref: Channel;
  consent: boolean;
  risk: "Low" | "Medium" | "High";
}
export const customers: Customer[] = [
  { id: "CU-4401", name: "Aisha Nakato", contact: "aisha.n@example.co · +256 772 118 001", policies: ["MTR-8821"], activeClaims: 1, region: "Central", channelPref: "WhatsApp", consent: true, risk: "Medium" },
  { id: "CU-4402", name: "David Mensah", contact: "d.mensah@example.co · +256 772 118 002", policies: ["MTR-1930"], activeClaims: 1, region: "Eastern", channelPref: "Email", consent: true, risk: "Low" },
  { id: "CU-4403", name: "Grace Wanjiku", contact: "grace.w@example.co · +256 772 118 003", policies: ["MTR-7712", "HOME-2210"], activeClaims: 1, region: "Central", channelPref: "Web", consent: true, risk: "Low" },
  { id: "CU-4404", name: "Samuel Otieno", contact: "s.otieno@example.co", policies: ["MTR-4402"], activeClaims: 1, region: "Western", channelPref: "Broker", consent: false, risk: "High" },
  { id: "CU-4405", name: "Fatima Suleiman", contact: "fatima.s@example.co · +256 772 118 005", policies: ["MTR-5591"], activeClaims: 1, region: "Northern", channelPref: "WhatsApp", consent: true, risk: "Medium" },
  { id: "CU-4406", name: "Peter Kagwa", contact: "p.kagwa@example.co", policies: ["MTR-9902"], activeClaims: 1, region: "Central", channelPref: "Branch" as Channel, consent: true, risk: "Low" },
];

// ---------- Documents library ----------
export type DocStatus = "Uploaded" | "Extraction Pending" | "Extracted" | "Review Required" | "Reviewed" | "Rejected" | "Failed";
export interface DocRecord {
  id: string;
  name: string;
  type: string;
  claim?: string;
  customer?: string;
  status: DocStatus;
  confidence?: number;
  evidence: string;
  uploaded: string;
}
export const documentLibrary: DocRecord[] = [
  { id: "DOC-77201", name: "Police report — UPF/KLA/2098.pdf", type: "Police report", claim: "CLM-1048", customer: "Aisha Nakato", status: "Reviewed", confidence: 0.86, evidence: "EV-88291", uploaded: "2 hr ago" },
  { id: "DOC-77202", name: "Repair quotation — SwiftFix.pdf", type: "Repair quotation", claim: "CLM-1048", customer: "Aisha Nakato", status: "Review Required", confidence: 0.72, evidence: "EV-88280", uploaded: "3 hr ago" },
  { id: "DOC-77203", name: "Driving permit.jpg", type: "Driving permit", claim: "CLM-1052", customer: "David Mensah", status: "Extraction Pending", evidence: "EV-88270", uploaded: "1 hr ago" },
  { id: "DOC-77204", name: "Vehicle photos (5).zip", type: "Vehicle photos", claim: "CLM-1061", customer: "Grace Wanjiku", status: "Extracted", confidence: 0.91, evidence: "EV-88260", uploaded: "yesterday" },
  { id: "DOC-77205", name: "Assessor report.pdf", type: "Assessor report", claim: "CLM-1064", customer: "Samuel Otieno", status: "Failed", evidence: "EV-88250", uploaded: "yesterday" },
  { id: "DOC-77206", name: "National ID — front.jpg", type: "National ID", claim: "CLM-1071", customer: "Fatima Suleiman", status: "Extracted", confidence: 0.98, evidence: "EV-88240", uploaded: "20 min ago" },
  { id: "DOC-77207", name: "Policy schedule.pdf", type: "Policy schedule", claim: "CLM-1090", customer: "John Ekakoro", status: "Reviewed", confidence: 0.95, evidence: "EV-88230", uploaded: "2 days ago" },
  { id: "DOC-77208", name: "Random attachment.docx", type: "Unknown", customer: "Unknown", status: "Rejected", evidence: "EV-88220", uploaded: "3 days ago" },
];

// ---------- Communications ----------
export type CommStatus = "Pending Approval" | "Approved" | "Sent" | "Send Failed" | "Rejected";
export interface Communication {
  id: string;
  claim?: string;
  customer: string;
  channel: Channel;
  subject: string;
  preview: string;
  status: CommStatus;
  drafted: string;
  reviewer?: string;
}
export const communications: Communication[] = [
  { id: "CM-5501", claim: "CLM-1082", customer: "Lydia Namutebi", channel: "WhatsApp", subject: "Follow-up: repair quotation", preview: "Thank you for your patience. To progress your claim we require the repair quotation…", status: "Pending Approval", drafted: "8 min ago" },
  { id: "CM-5502", claim: "CLM-2025-00928", customer: "Rachel Auma", channel: "WhatsApp", subject: "Update: assessment in progress", preview: "Our team is currently reviewing your claim. We will share the outcome within 48 hours…", status: "Pending Approval", drafted: "22 min ago" },
  { id: "CM-5503", claim: "CLM-1090", customer: "John Ekakoro", channel: "Email", subject: "Claim pack ready for export", preview: "Please find attached the final claim documentation for your records…", status: "Approved", drafted: "1 hr ago", reviewer: "J. Mugisha" },
  { id: "CM-5504", claim: "CLM-1061", customer: "Grace Wanjiku", channel: "Email", subject: "Windscreen claim acknowledged", preview: "We have received your claim and required documentation…", status: "Sent", drafted: "yesterday", reviewer: "T. Okello" },
  { id: "CM-5505", claim: "CLM-1052", customer: "David Mensah", channel: "SMS", subject: "Document request", preview: "Please share a clearer image of your driving permit…", status: "Send Failed", drafted: "2 hr ago", reviewer: "S. Achieng" },
  { id: "CM-5506", claim: "CLM-1071", customer: "Fatima Suleiman", channel: "WhatsApp", subject: "Draft with reserve figure", preview: "Rejected — draft referenced internal reserve figures.", status: "Rejected", drafted: "3 hr ago", reviewer: "L. Byaruhanga" },
];

// ---------- Providers / health ----------
export interface ProviderHealth {
  name: string;
  category: "AI" | "Document" | "Channel" | "Storage" | "Data" | "Messaging" | "Directory";
  status: "Operational" | "Degraded" | "Down";
  latency: string;
  lastCheck: string;
}
export const providerHealth: ProviderHealth[] = [
  { name: "Azure OpenAI · GPT-4o", category: "AI", status: "Operational", latency: "412ms", lastCheck: "1 min ago" },
  { name: "Azure AI Document Intelligence", category: "Document", status: "Operational", latency: "1.1s", lastCheck: "2 min ago" },
  { name: "Azure AI Search (RAG)", category: "AI", status: "Operational", latency: "220ms", lastCheck: "1 min ago" },
  { name: "WhatsApp Cloud API", category: "Channel", status: "Operational", latency: "220ms", lastCheck: "just now" },
  { name: "Microsoft Graph / Outlook", category: "Channel", status: "Degraded", latency: "2.4s", lastCheck: "1 min ago" },
  { name: "SMS Gateway (Twilio)", category: "Channel", status: "Operational", latency: "310ms", lastCheck: "3 min ago" },
  { name: "Website Chat Widget", category: "Channel", status: "Operational", latency: "180ms", lastCheck: "2 min ago" },
  { name: "Microsoft Teams", category: "Channel", status: "Operational", latency: "260ms", lastCheck: "4 min ago" },
  { name: "Azure Blob Storage", category: "Storage", status: "Operational", latency: "94ms", lastCheck: "1 min ago" },
  { name: "Azure SQL", category: "Data", status: "Operational", latency: "62ms", lastCheck: "1 min ago" },
  { name: "Azure Service Bus / Event Grid", category: "Messaging", status: "Operational", latency: "88ms", lastCheck: "2 min ago" },
  { name: "Entra ID (Directory)", category: "Directory", status: "Operational", latency: "140ms", lastCheck: "5 min ago" },
];

// ---------- Agents ----------
export type AgentStatus = "Active" | "Disabled" | "Testing" | "Deprecated";
export type AgentRisk = "Low" | "Medium" | "High";
export interface AgentDef {
  id: string;
  name: string;
  purpose: string;
  status: AgentStatus;
  version: string;
  owner: string;
  risk: AgentRisk;
  modules: string[];
  tools: string[];
  knowledge: string[];
  humanApproval: string;
  confidenceThreshold: number;
  lastUpdated: string;
  lastRun: string;
  runsToday: number;
  costEstimate: string;
  promptVersion: string;
  model: string;
}
export const agents: AgentDef[] = [
  { id: "AG-01", name: "Orchestrator Agent", purpose: "Routes work between agents and human queues", status: "Active", version: "v2.1", owner: "AI Governance", risk: "Medium", modules: ["All"], tools: ["route_task", "read_workflow"], knowledge: ["Approved SOPs"], humanApproval: "Not required (routing only)", confidenceThreshold: 0.75, lastUpdated: "Today", lastRun: "1 min ago", runsToday: 412, costEstimate: "US$ 8.20", promptVersion: "orch-v2.1", model: "gpt-4o" },
  { id: "AG-02", name: "FNOL Intake Agent", purpose: "Structures FNOL data from customer intake", status: "Active", version: "v1.7", owner: "Claims", risk: "Low", modules: ["Claims", "Channels"], tools: ["parse_intake", "create_claim_draft"], knowledge: ["FNOL SOP"], humanApproval: "Required to submit FNOL", confidenceThreshold: 0.8, lastUpdated: "2 days ago", lastRun: "3 min ago", runsToday: 88, costEstimate: "US$ 2.10", promptVersion: "fnol-v1.7", model: "gpt-4o-mini" },
  { id: "AG-03", name: "Document Intelligence Agent", purpose: "Extracts and classifies uploaded documents", status: "Active", version: "v3.2", owner: "Documents", risk: "Low", modules: ["Documents", "Claims"], tools: ["ocr", "field_extract"], knowledge: ["Document Type Rules"], humanApproval: "Required below 0.80", confidenceThreshold: 0.8, lastUpdated: "Today", lastRun: "just now", runsToday: 213, costEstimate: "US$ 6.40", promptVersion: "doc-v3.2", model: "Azure DocIntel + gpt-4o" },
  { id: "AG-04", name: "Missing Information Agent", purpose: "Detects checklist gaps and low-confidence fields", status: "Active", version: "v1.4", owner: "Claims", risk: "Low", modules: ["Claims"], tools: ["checklist_compare"], knowledge: ["Motor Checklist v2"], humanApproval: "Not required (advisory)", confidenceThreshold: 0.75, lastUpdated: "1 week ago", lastRun: "9 min ago", runsToday: 154, costEstimate: "US$ 1.10", promptVersion: "miss-v1.4", model: "gpt-4o-mini" },
  { id: "AG-05", name: "Claims Summary Agent", purpose: "Prepares claim summaries with evidence citations", status: "Active", version: "v2.0", owner: "Claims", risk: "Medium", modules: ["Claims"], tools: ["read_claim", "cite_evidence"], knowledge: ["Approved SOPs", "Policy Schedules"], humanApproval: "Required before customer share", confidenceThreshold: 0.8, lastUpdated: "Today", lastRun: "12 min ago", runsToday: 68, costEstimate: "US$ 3.90", promptVersion: "sum-v2.0", model: "gpt-4o" },
  { id: "AG-06", name: "Fraud Signal Agent", purpose: "Raises advisory fraud signals with evidence", status: "Active", version: "v1.2", owner: "Risk", risk: "High", modules: ["Fraud", "Claims"], tools: ["pattern_match", "graph_lookup"], knowledge: ["Fraud Signal Playbook"], humanApproval: "Required for any action", confidenceThreshold: 0.85, lastUpdated: "3 days ago", lastRun: "22 min ago", runsToday: 41, costEstimate: "US$ 1.80", promptVersion: "fraud-v1.2", model: "gpt-4o" },
  { id: "AG-07", name: "Policy Intelligence Agent", purpose: "Answers policy questions grounded in approved sources", status: "Active", version: "v1.9", owner: "Knowledge", risk: "Medium", modules: ["Policy", "Knowledge"], tools: ["rag_search"], knowledge: ["Approved Policy Docs"], humanApproval: "Required for coverage confirmation", confidenceThreshold: 0.8, lastUpdated: "Today", lastRun: "4 min ago", runsToday: 129, costEstimate: "US$ 4.60", promptVersion: "pol-v1.9", model: "gpt-4o" },
  { id: "AG-08", name: "Customer Update Agent", purpose: "Drafts customer-safe communications", status: "Active", version: "v1.4", owner: "Customer Service", risk: "High", modules: ["Communications", "Claims"], tools: ["template_render"], knowledge: ["Comms Templates"], humanApproval: "Required before send", confidenceThreshold: 0.8, lastUpdated: "Today", lastRun: "2 min ago", runsToday: 92, costEstimate: "US$ 2.80", promptVersion: "comm-v1.4", model: "gpt-4o-mini" },
  { id: "AG-09", name: "Multilingual Agent", purpose: "Translates customer messages and drafts", status: "Active", version: "v1.1", owner: "Customer Service", risk: "Low", modules: ["Channels", "Communications"], tools: ["translate"], knowledge: ["Glossary"], humanApproval: "Not required (assist)", confidenceThreshold: 0.75, lastUpdated: "2 weeks ago", lastRun: "18 min ago", runsToday: 34, costEstimate: "US$ 0.70", promptVersion: "ml-v1.1", model: "gpt-4o-mini" },
  { id: "AG-10", name: "Review and Audit Agent", purpose: "Assembles evidence bundles for reviewers and auditors", status: "Active", version: "v1.0", owner: "Compliance", risk: "Medium", modules: ["Audit", "Claims"], tools: ["bundle_evidence"], knowledge: ["Audit Rules"], humanApproval: "Not required (read-only)", confidenceThreshold: 0.8, lastUpdated: "1 week ago", lastRun: "30 min ago", runsToday: 22, costEstimate: "US$ 1.20", promptVersion: "aud-v1.0", model: "gpt-4o-mini" },
  { id: "AG-11", name: "Underwriting Support Agent", purpose: "Summarizes submissions and drafts underwriter questions", status: "Testing", version: "v0.4", owner: "Underwriting", risk: "Medium", modules: ["Underwriting"], tools: ["read_submission"], knowledge: ["Underwriting Guidelines"], humanApproval: "Required for any recommendation", confidenceThreshold: 0.85, lastUpdated: "Yesterday", lastRun: "1 hr ago", runsToday: 6, costEstimate: "US$ 0.40", promptVersion: "uw-v0.4", model: "gpt-4o" },
  { id: "AG-12", name: "Early Warning Agent", purpose: "Surfaces operational anomalies to executives", status: "Testing", version: "v0.3", owner: "Executive", risk: "Medium", modules: ["Executive"], tools: ["metric_watch"], knowledge: ["Metric Playbook"], humanApproval: "Not required (advisory)", confidenceThreshold: 0.75, lastUpdated: "Yesterday", lastRun: "2 hr ago", runsToday: 4, costEstimate: "US$ 0.30", promptVersion: "ew-v0.3", model: "gpt-4o-mini" },
  { id: "AG-13", name: "Broker Assistant Agent", purpose: "Assists brokers with safe status information", status: "Disabled", version: "v0.2", owner: "Sales", risk: "Medium", modules: ["Acquisition"], tools: ["broker_lookup"], knowledge: ["Broker Playbook"], humanApproval: "Required for any outbound", confidenceThreshold: 0.85, lastUpdated: "1 month ago", lastRun: "—", runsToday: 0, costEstimate: "US$ 0.00", promptVersion: "brk-v0.2", model: "gpt-4o-mini" },
];

// ---------- Knowledge ----------
export type KnowledgeSensitivity = "approved" | "customer_confidential" | "staff_only" | "contains_secrets" | "contains_unapproved_pii" | "unknown";
export interface KnowledgeDoc {
  id: string;
  title: string;
  category: string;
  sensitivity: KnowledgeSensitivity;
  status: "Approved" | "Pending" | "Needs re-index";
  retrievalEligible: boolean;
  sourceQuality: "High" | "Medium" | "Low";
  updated: string;
  owner: string;
}
export const knowledgeDocs: KnowledgeDoc[] = [
  { id: "KD-2201", title: "Motor Claims SOP v4.3", category: "SOP", sensitivity: "approved", status: "Approved", retrievalEligible: true, sourceQuality: "High", updated: "Today", owner: "G. Wanjiku" },
  { id: "KD-2202", title: "Comprehensive Motor Wording v3.2", category: "Policy Wording", sensitivity: "approved", status: "Approved", retrievalEligible: true, sourceQuality: "High", updated: "2 days ago", owner: "G. Wanjiku" },
  { id: "KD-2203", title: "Fraud Signal Playbook", category: "Playbook", sensitivity: "staff_only", status: "Approved", retrievalEligible: true, sourceQuality: "High", updated: "1 week ago", owner: "L. Byaruhanga" },
  { id: "KD-2204", title: "Customer Comms Templates", category: "Templates", sensitivity: "approved", status: "Approved", retrievalEligible: true, sourceQuality: "High", updated: "Today", owner: "R. Naluyima" },
  { id: "KD-2205", title: "Draft: Home Contents Wording v0.6", category: "Policy Wording", sensitivity: "unknown", status: "Pending", retrievalEligible: false, sourceQuality: "Medium", updated: "3 days ago", owner: "G. Wanjiku" },
  { id: "KD-2206", title: "Legacy Broker Notes (2019)", category: "Notes", sensitivity: "contains_unapproved_pii", status: "Needs re-index", retrievalEligible: false, sourceQuality: "Low", updated: "6 months ago", owner: "Archive" },
  { id: "KD-2207", title: "Internal fraud casebook (redacted)", category: "Reference", sensitivity: "contains_secrets", status: "Approved", retrievalEligible: false, sourceQuality: "High", updated: "1 month ago", owner: "L. Byaruhanga" },
];

// ---------- Approvals / workflow ----------
export interface ApprovalTask {
  id: string;
  title: string;
  claim?: string;
  requester: string;
  reviewer: string;
  risk: RiskState;
  ageHrs: number;
  summary: string;
  status: "Pending" | "Approved" | "Rejected" | "Escalated";
}
export const approvals: ApprovalTask[] = [
  { id: "AP-3301", title: "Route CLM-1064 to supervisor", claim: "CLM-1064", requester: "AI Copilot", reviewer: "J. Mugisha", risk: "High", ageHrs: 4, summary: "Third-party bodily injury exceeds officer threshold. Requires supervisor review before next workflow step.", status: "Pending" },
  { id: "AP-3302", title: "Send customer update — CLM-2025-00928", claim: "CLM-2025-00928", requester: "S. Achieng", reviewer: "T. Okello", risk: "Medium", ageHrs: 0.5, summary: "Draft prepared by Customer Update Agent v1.4. No reserves or internal notes included.", status: "Pending" },
  { id: "AP-3303", title: "Approve knowledge document upload", requester: "G. Wanjiku", reviewer: "T. Okello", risk: "Low", ageHrs: 2, summary: "Home Contents Wording v0.6 requires sensitivity classification before retrieval eligibility.", status: "Pending" },
  { id: "AP-3304", title: "Reassess repair quotation — CLM-1048", claim: "CLM-1048", requester: "AI Copilot", reviewer: "T. Okello", risk: "Medium", ageHrs: 1, summary: "Quotation 22% above regional median. Advisory only — requires reviewer decision.", status: "Pending" },
];

// ---------- Underwriting ----------
export interface Submission {
  id: string;
  applicant: string;
  broker: string;
  product: string;
  sumInsured: string;
  missing: number;
  risk: RiskState;
  confidence: number;
  ageHrs: number;
}
export const submissions: Submission[] = [
  { id: "SUB-7701", applicant: "Katonga Logistics Ltd", broker: "Kato Partners", product: "Fleet Comprehensive", sumInsured: "UGX 420M", missing: 2, risk: "Medium", confidence: 0.78, ageHrs: 6 },
  { id: "SUB-7702", applicant: "Amina Yusuf", broker: "Direct", product: "Motor Comprehensive", sumInsured: "UGX 65M", missing: 0, risk: "Low", confidence: 0.92, ageHrs: 2 },
  { id: "SUB-7703", applicant: "Nile Cargo SA", broker: "Kato Partners", product: "Marine Cargo", sumInsured: "USD 1.2M", missing: 3, risk: "High", confidence: 0.61, ageHrs: 22 },
];

// ---------- Fraud signals ----------
export interface FraudSignal {
  id: string;
  claim: string;
  pattern: string;
  detail: string;
  severity: "Advisory" | "Elevated" | "Investigation";
  evidence: string;
}
export const fraudSignals: FraudSignal[] = [
  { id: "FS-901", claim: "CLM-1048", pattern: "Repeated repair vendor", detail: "SwiftFix Auto Ltd appears on 3 other active claims this month.", severity: "Advisory", evidence: "EV-88280" },
  { id: "FS-902", claim: "CLM-1071", pattern: "Duplicate claimant", detail: "Claimant name matches an earlier fire claim by phonetic similarity.", severity: "Elevated", evidence: "EV-88170" },
  { id: "FS-903", claim: "CLM-1064", pattern: "Photo/quotation mismatch", detail: "Damage geometry in photos inconsistent with itemised quotation lines.", severity: "Investigation", evidence: "EV-88250" },
  { id: "FS-904", claim: "CLM-1052", pattern: "Expired permit", detail: "Driving permit reported expired 22 days before incident.", severity: "Elevated", evidence: "EV-88270" },
];

// ---------- Usage metering ----------
export const usage = {
  package: "Enterprise · Insurance",
  seatsUsed: 42, seatsLimit: 60,
  aiRunsUsed: 12840, aiRunsLimit: 25000,
  docsProcessed: 3120, docsLimit: 6000,
  channelMessages: 7860, channelLimit: 15000,
  storageGB: 128, storageLimit: 500,
  billingPeriod: "Nov 1 – Nov 30, 2026",
  supportTier: "Priority · 24×7",
};

// Kill switch levels
export const killSwitchLevels = [
  "Disable one agent",
  "Disable one agent in one workspace",
  "Disable all customer-facing drafts",
  "Disable all fraud signals",
  "Disable all AI drafting",
  "Disable AI provider",
  "Disable AI for tenant",
  "Emergency read-only mode",
];
