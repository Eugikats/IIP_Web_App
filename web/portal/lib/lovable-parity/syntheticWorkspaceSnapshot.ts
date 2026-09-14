// One development-only source for the synthetic workspace shown in visual parity mode.
// Protected runtimes cannot enable parity mode and must never import these values as fallback data.
export type ClaimStatus = "New" | "Assigned" | "Awaiting Documents" | "In Review" | "Missing Documents" | "Human Review Required" | "Pending Approval" | "Escalated" | "Approved" | "Rejected" | "Ready for Export" | "Closed";
export type SLAState = "Normal" | "At Risk" | "Breached";
export type RiskState = "Low" | "Medium" | "High" | "Advisory Signal";

export interface Claim {
  id: string;
  customer: string;
  policy: string;
  productLine: string;
  claimType: string;
  incident: string;
  status: ClaimStatus;
  missingDocs: number;
  sla: SLAState;
  risk: RiskState;
  officer: string;
  channel: "WhatsApp" | "Email" | "Web" | "Broker" | "Branch" | "Mobile" | "SMS";
  updated: string;
  confidence: number;
  amount: string;
  region: string;
}

export type SyntheticProviderStatus = "healthy" | "degraded" | "missing" | "not_configured" | "disabled" | "unknown";
type SyntheticWorkspaceSnapshot = {
  source: "synthetic";
  snapshotId: string;
  dataAsOf: string;
  limitations: string;
  claims: Claim[];
  operationsSummary: {
    openClaims: number;
    newClaimsToday: number;
    incompleteClaims: number;
    averageCompletenessScore: number;
    pendingApprovals: number;
    customerUpdatesPending: number;
    assistantQuestionsAnswered: number;
    escalationsRequired: number;
    slaAtRisk: number;
    slaBreached: number;
    closedClaimsWithSlaBreach: number;
    claimsByStatus: { label: string; count: number }[];
  };
  providerHealth: {
    overallStatus: "healthy" | "degraded";
    providers: Array<{
      name: string;
      displayName: string;
      category: string;
      mode: string;
      status: SyntheticProviderStatus;
      detail: string;
      latency: string;
    }>;
  };
};

const claims: Claim[] = [
  { id: "CLM-1048", customer: "Aisha Nakato", policy: "MTR-8821", productLine: "Motor", claimType: "Collision", incident: "Rear-end collision, Kampala Rd", status: "Human Review Required", missingDocs: 0, sla: "At Risk", risk: "Medium", officer: "T. Okello", channel: "WhatsApp", updated: "12 min ago", confidence: 0.86, amount: "UGX 4.2M", region: "Central" },
  { id: "CLM-1052", customer: "David Mensah", policy: "MTR-1930", productLine: "Motor", claimType: "Collision", incident: "Side-swipe, parked vehicle", status: "Missing Documents", missingDocs: 3, sla: "Normal", risk: "Low", officer: "S. Achieng", channel: "Email", updated: "1 hr ago", confidence: 0.72, amount: "UGX 1.8M", region: "Eastern" },
  { id: "CLM-1061", customer: "Grace Wanjiku", policy: "MTR-7712", productLine: "Motor", claimType: "Windscreen", incident: "Windscreen damage, hail", status: "In Review", missingDocs: 1, sla: "Normal", risk: "Low", officer: "T. Okello", channel: "Web", updated: "3 hr ago", confidence: 0.91, amount: "UGX 620K", region: "Central" },
  { id: "CLM-1064", customer: "Samuel Otieno", policy: "MTR-4402", productLine: "Motor", claimType: "Third-party BI", incident: "Third-party bodily injury", status: "Escalated", missingDocs: 2, sla: "Breached", risk: "High", officer: "J. Mugisha", channel: "Broker", updated: "5 hr ago", confidence: 0.64, amount: "UGX 12.4M", region: "Western" },
  { id: "CLM-1071", customer: "Fatima Suleiman", policy: "MTR-5591", productLine: "Motor", claimType: "Total loss", incident: "Total loss, fire", status: "New", missingDocs: 4, sla: "Normal", risk: "Advisory Signal", officer: "Unassigned", channel: "WhatsApp", updated: "just now", confidence: 0.58, amount: "UGX 22.0M", region: "Northern" },
  { id: "CLM-1074", customer: "Peter Kagwa", policy: "MTR-9902", productLine: "Motor", claimType: "Collision", incident: "Minor collision, taxi park", status: "In Review", missingDocs: 0, sla: "Normal", risk: "Low", officer: "S. Achieng", channel: "Branch", updated: "yesterday", confidence: 0.94, amount: "UGX 780K", region: "Central" },
  { id: "CLM-1082", customer: "Lydia Namutebi", policy: "MTR-3308", productLine: "Motor", claimType: "Collision", incident: "Boda-boda impact", status: "Awaiting Documents", missingDocs: 2, sla: "At Risk", risk: "Medium", officer: "T. Okello", channel: "WhatsApp", updated: "2 hr ago", confidence: 0.77, amount: "UGX 1.1M", region: "Central" },
  { id: "CLM-1090", customer: "John Ekakoro", policy: "MTR-6650", productLine: "Motor", claimType: "Vandalism", incident: "Vandalism", status: "Ready for Export", missingDocs: 0, sla: "Normal", risk: "Low", officer: "J. Mugisha", channel: "Email", updated: "yesterday", confidence: 0.96, amount: "UGX 2.3M", region: "Eastern" },
  { id: "CLM-2025-00928", customer: "Rachel Auma", policy: "MTR-2210", productLine: "Motor", claimType: "Collision", incident: "Front-end collision, Jinja Rd", status: "Pending Approval", missingDocs: 1, sla: "At Risk", risk: "Medium", officer: "S. Achieng", channel: "WhatsApp", updated: "35 min ago", confidence: 0.83, amount: "UGX 6.8M", region: "Central" },
];

const claimsByStatus = Array.from(
  claims.reduce((counts, claim) => counts.set(claim.status, (counts.get(claim.status) ?? 0) + 1), new Map<string, number>()),
  ([label, count]) => ({ label, count }),
);

export const syntheticWorkspaceSnapshot = {
  source: "synthetic",
  snapshotId: "iip-r1-synthetic-2026-08-31",
  dataAsOf: "2026-08-31T12:00:00.000Z",
  limitations: "Development-only illustrative data. It is not customer, provider, staging, or production evidence.",
  claims,
  operationsSummary: {
    openClaims: claims.filter((claim) => claim.status !== "Closed").length,
    newClaimsToday: claims.filter((claim) => claim.status === "New").length,
    incompleteClaims: claims.filter((claim) => claim.missingDocs > 0).length,
    averageCompletenessScore: Math.round(claims.reduce((total, claim) => total + claim.confidence * 100, 0) / claims.length),
    pendingApprovals: claims.filter((claim) => claim.status === "Pending Approval").length,
    customerUpdatesPending: 1,
    assistantQuestionsAnswered: claims.length,
    escalationsRequired: claims.filter((claim) => claim.risk === "High" || claim.risk === "Advisory Signal").length,
    slaAtRisk: claims.filter((claim) => claim.sla === "At Risk" && claim.status !== "Closed").length,
    slaBreached: claims.filter((claim) => claim.sla === "Breached" && claim.status !== "Closed").length,
    closedClaimsWithSlaBreach: claims.filter((claim) => claim.sla === "Breached" && claim.status === "Closed").length,
    claimsByStatus,
  },
  providerHealth: {
    overallStatus: "degraded",
    providers: [
      { name: "azureOpenAI", displayName: "Azure OpenAI · GPT-4o", category: "AI", mode: "AzureOpenAI", status: "healthy", detail: "Synthetic healthy response for development UI verification only.", latency: "412ms" },
      { name: "documentIntelligence", displayName: "Azure AI Document Intelligence", category: "Document", mode: "AzureDocumentIntelligence", status: "healthy", detail: "Synthetic healthy response for development UI verification only.", latency: "1.1s" },
      { name: "aiSearch", displayName: "Azure AI Search (RAG)", category: "AI", mode: "NotConfigured", status: "not_configured", detail: "Not configured in this synthetic workspace.", latency: "-" },
      { name: "whatsApp", displayName: "WhatsApp Cloud API", category: "Channel", mode: "NotConfigured", status: "not_configured", detail: "Not configured in this synthetic workspace.", latency: "-" },
      { name: "graphMail", displayName: "Microsoft Graph / Outlook", category: "Channel", mode: "Graph", status: "degraded", detail: "Synthetic degraded response for recovery-state verification.", latency: "2.4s" },
      { name: "sms", displayName: "SMS Gateway", category: "Channel", mode: "NotConfigured", status: "missing", detail: "No SMS provider is configured in this synthetic workspace.", latency: "-" },
    ],
  },
} satisfies SyntheticWorkspaceSnapshot;
