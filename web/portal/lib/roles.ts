export type Role =
  | "Admin"
  | "ClaimsOfficer"
  | "ClaimsSupervisor"
  | "CustomerService"
  | "Broker"
  | "Compliance"
  | "KnowledgeManager"
  | "ITAdmin"
  | "PublisherSupport";

export interface RoleDef {
  key: Role;
  label: string;
  title: string;
  landing: string;
}

export type Permission =
  | "ViewCommandCenter"
  | "CreateClaim"
  | "UploadDocument"
  | "ViewAllClaims"
  | "ViewAssignedClaims"
  | "ApproveSettlementWorkflowStep"
  | "ViewAuditLog"
  | "ExportEvidencePack"
  | "ManageKnowledgeBase"
  | "ConfigureIntegrations"
  | "ViewAiOutputs"
  | "OverrideAiRecommendation"
  | "ManageCustomerCommunications"
  | "SendCustomerUpdate"
  | "ManageTenantSettings"
  | "ViewClientPortal"
  | "ViewChannels"
  | "ViewCustomers"
  | "ViewSalesLeads"
  | "ManageSalesLeads"
  | "ViewFraudSignals"
  | "ViewUnderwriting"
  | "ManageAgents"
  | "ViewAgentWorkflows"
  | "RunAgentWorkflows"
  | "ManageAgentWorkflows"
  | "ReviewAgentWorkItems"
  | "ViewBilling"
  | "ManageTenantOnboarding"
  | "ManageProducts";

export const STAFF_ROLES: Role[] = [
  "Admin", "ClaimsOfficer", "ClaimsSupervisor", "CustomerService", "Broker",
  "Compliance", "KnowledgeManager", "ITAdmin", "PublisherSupport",
];

export const ALL_PERMISSIONS: Permission[] = [
  "ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAllClaims", "ViewAssignedClaims",
  "ApproveSettlementWorkflowStep", "ViewAuditLog", "ExportEvidencePack", "ManageKnowledgeBase",
  "ConfigureIntegrations", "ViewAiOutputs", "OverrideAiRecommendation",
  "ManageCustomerCommunications", "SendCustomerUpdate", "ManageTenantSettings", "ViewClientPortal",
  "ViewChannels", "ViewCustomers", "ViewSalesLeads", "ManageSalesLeads", "ViewFraudSignals",
  "ViewUnderwriting", "ManageAgents", "ViewAgentWorkflows", "RunAgentWorkflows",
  "ManageAgentWorkflows", "ReviewAgentWorkItems", "ViewBilling", "ManageTenantOnboarding",
  "ManageProducts",
];

// Development presentation contract only. Protected runtimes use server-resolved permissions.
// Keep this aligned with RolePermissionMap and test the alignment at both boundaries.
export const PREVIEW_PERMISSIONS: Record<Role, Permission[]> = {
  Admin: ALL_PERMISSIONS,
  ITAdmin: [
    "ViewCommandCenter", "ViewAuditLog", "ExportEvidencePack", "ConfigureIntegrations",
    "ManageTenantSettings", "ManageAgents", "ViewAgentWorkflows", "RunAgentWorkflows",
    "ManageAgentWorkflows", "ReviewAgentWorkItems", "ViewBilling", "ManageProducts",
  ],
  ClaimsOfficer: [
    "ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAssignedClaims", "ViewAiOutputs",
    "ManageCustomerCommunications", "SendCustomerUpdate", "ViewChannels", "ViewCustomers",
    "ViewAgentWorkflows", "RunAgentWorkflows", "ReviewAgentWorkItems",
  ],
  ClaimsSupervisor: [
    "ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAllClaims", "ViewAssignedClaims",
    "ApproveSettlementWorkflowStep", "ViewAuditLog", "ExportEvidencePack", "ViewAiOutputs",
    "OverrideAiRecommendation", "ManageCustomerCommunications", "SendCustomerUpdate", "ViewChannels",
    "ViewCustomers", "ViewFraudSignals", "ViewUnderwriting", "ViewAgentWorkflows",
    "RunAgentWorkflows", "ReviewAgentWorkItems", "ManageProducts",
  ],
  CustomerService: [
    "ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAssignedClaims", "ViewAiOutputs",
    "ManageCustomerCommunications", "SendCustomerUpdate", "ViewChannels", "ViewCustomers",
    "ViewSalesLeads", "ManageSalesLeads", "ViewAgentWorkflows", "RunAgentWorkflows",
    "ReviewAgentWorkItems",
  ],
  Broker: ["CreateClaim", "UploadDocument", "ViewAssignedClaims", "ViewAiOutputs"],
  Compliance: [
    "ViewCommandCenter", "ViewAllClaims", "ViewAuditLog", "ExportEvidencePack", "ManageKnowledgeBase",
    "ViewAiOutputs", "ApproveSettlementWorkflowStep", "OverrideAiRecommendation", "ViewCustomers",
    "ViewFraudSignals", "ViewUnderwriting", "ViewAgentWorkflows", "RunAgentWorkflows",
    "ReviewAgentWorkItems",
  ],
  KnowledgeManager: ["ManageKnowledgeBase", "ViewAiOutputs"],
  PublisherSupport: ["ViewCommandCenter", "ViewAuditLog", "ManageTenantOnboarding"],
};

type RouteAccessRule = {
  path: string;
  requiredAny: Permission[];
  exact?: boolean;
};

// Presentation visibility only. The API remains authoritative for every read and action.
// Put specific child routes before their parent prefix.
export const ROUTE_ACCESS: RouteAccessRule[] = [
  // Training is open to everyone who can sign in.
  { path: "/app/academy", requiredAny: [] },
  { path: "/app/conversations", requiredAny: ["ViewCommandCenter"] },
  { path: "/app/search", requiredAny: ["ViewAssignedClaims", "ViewAllClaims"] },
  { path: "/app/claims/new", requiredAny: ["CreateClaim"], exact: true },
  { path: "/app/claims", requiredAny: ["ViewAssignedClaims", "ViewAllClaims"] },
  { path: "/app/channels", requiredAny: ["ViewChannels"] },
  { path: "/app/customers", requiredAny: ["ViewCustomers"] },
  { path: "/app/documents", requiredAny: ["ViewAllClaims"] },
  { path: "/app/communications", requiredAny: ["ViewAssignedClaims", "ViewAllClaims"] },
  { path: "/app/acquisition", requiredAny: ["ViewSalesLeads"] },
  { path: "/app/policy", requiredAny: ["ViewAiOutputs", "ManageKnowledgeBase"] },
  { path: "/app/retention", requiredAny: ["ViewCustomers"] },
  { path: "/app/fraud", requiredAny: ["ViewFraudSignals"] },
  { path: "/app/underwriting", requiredAny: ["ViewUnderwriting"] },
  { path: "/app/knowledge", requiredAny: ["ManageKnowledgeBase"] },
  { path: "/app/executive", requiredAny: ["ViewCommandCenter"] },
  { path: "/app/work-items", requiredAny: ["ViewAssignedClaims", "ViewAllClaims", "ReviewAgentWorkItems", "ApproveSettlementWorkflowStep", "ManageCustomerCommunications"] },
  { path: "/app/workflows", requiredAny: ["ReviewAgentWorkItems", "ApproveSettlementWorkflowStep"] },
  { path: "/app/flows", requiredAny: ["ViewAgentWorkflows"] },
  { path: "/app/runs", requiredAny: ["ViewAgentWorkflows"] },
  { path: "/app/integrations", requiredAny: ["ConfigureIntegrations", "ManageTenantSettings"] },
  { path: "/app/support", requiredAny: ["ConfigureIntegrations", "ManageTenantSettings"] },
  { path: "/app/audit", requiredAny: ["ViewAuditLog"] },
  { path: "/app/admin/users", requiredAny: ["ManageTenantSettings"] },
  // Prefix-matched below covers /app/admin/agents/{key}/behaviour too, no separate entry needed.
  { path: "/app/admin/agents", requiredAny: ["ManageAgents"] },
  { path: "/app/admin/products", requiredAny: ["ManageProducts"] },
  { path: "/app/admin/policy-import", requiredAny: ["ManageProducts"] },
  { path: "/app/admin/billing", requiredAny: ["ViewBilling"] },
  { path: "/app/admin/onboarding", requiredAny: ["ManageTenantOnboarding"] },
  { path: "/app/admin/tenant-policy", requiredAny: ["ManageTenantSettings"] },
  { path: "/app/admin/whatsapp", requiredAny: ["ConfigureIntegrations"] },
  { path: "/app/admin/email", requiredAny: ["ConfigureIntegrations"] },
  { path: "/app/admin/connections", requiredAny: ["ConfigureIntegrations"] },
  { path: "/app/admin", requiredAny: ["ManageTenantSettings"], exact: true },
  // Every authenticated user reaches their own profile/settings, it has no admin-only content.
  { path: "/app/settings", requiredAny: [] },
  // Where Google or Microsoft returns a person after they connect their own files.
  { path: "/app/connect", requiredAny: [] },
  { path: "/app/assistant", requiredAny: ["ViewCommandCenter"] },
  { path: "/app/brief", requiredAny: ["ViewCommandCenter"] },
  { path: "/app", requiredAny: ["ViewCommandCenter"], exact: true },
];

export function canAccessWithPermissions(permissions: readonly string[], path: string): boolean {
  const rule = ROUTE_ACCESS.find((candidate) =>
    candidate.exact ? path === candidate.path : path === candidate.path || path.startsWith(candidate.path + "/")
  );
  if (!rule) return false;
  return rule.requiredAny.length === 0 || rule.requiredAny.some((permission) => permissions.includes(permission));
}

export function firstAccessibleRoute(permissions: readonly string[]): string | null {
  const preferred = [
    "/app", "/app/claims", "/app/channels", "/app/work-items", "/app/customers",
    "/app/documents", "/app/communications", "/app/knowledge", "/app/audit",
    "/app/integrations", "/app/admin",
  ];
  return preferred.find((path) => canAccessWithPermissions(permissions, path)) ?? null;
}

export const ROLES: Record<Role, RoleDef> = {
  Admin: {
    key: "Admin", label: "Workspace Administrator", title: "Workspace Administrator", landing: "/app",
  },
  ClaimsOfficer: {
    key: "ClaimsOfficer", label: "Claims Officer", title: "Claims Officer", landing: "/app/claims",
  },
  ClaimsSupervisor: {
    key: "ClaimsSupervisor", label: "Claims Supervisor", title: "Claims Supervisor",
    landing: "/app/work-items",
  },
  CustomerService: {
    key: "CustomerService", label: "Customer Service", title: "Customer Service", landing: "/app/channels",
  },
  Broker: {
    key: "Broker", label: "Broker", title: "Broker", landing: "/app/claims",
  },
  Compliance: {
    key: "Compliance", label: "Compliance Officer", title: "Compliance Officer", landing: "/app/audit",
  },
  KnowledgeManager: {
    key: "KnowledgeManager", label: "Knowledge Manager", title: "Knowledge Manager", landing: "/app/knowledge",
  },
  ITAdmin: {
    key: "ITAdmin", label: "IT Administrator", title: "IT Administrator", landing: "/app/integrations",
  },
  PublisherSupport: {
    key: "PublisherSupport", label: "Publisher Support Engineer", title: "Publisher Support Engineer", landing: "/app",
  },
};

export function canAccess(role: Role, path: string): boolean {
  return canAccessWithPermissions(PREVIEW_PERMISSIONS[role], path);
}

export function canUseGlobalSearchPreview(role: Role): boolean {
  return canPreviewPermission(role, "ViewAssignedClaims") || canPreviewPermission(role, "ViewAllClaims");
}

export function canPreviewPermission(role: Role, permission: Permission): boolean {
  return PREVIEW_PERMISSIONS[role].includes(permission);
}

// Gates a single action control (a button, not a whole route) on the signed-in user's REAL
// server-resolved permissions. Never gate an action on the preview role switcher, it changes
// what a user sees, never their actual authority (RoleSwitcher.tsx). Callers follow the same
// usePreviewRole pattern as canAccess/canAccessWithPermissions for route gating: in local-test
// or visual-parity mode, check canPreviewPermission(role, permission) instead, that mirrors
// what the backend's x-demo-role header would actually grant in that mode.
export function hasPermission(permissions: readonly string[], permission: Permission): boolean {
  return permissions.includes(permission);
}

export function previewLandingRoute(role: Role): string {
  const configured = ROLES[role].landing;
  return canAccess(role, configured) ? configured : firstAccessibleRoute(PREVIEW_PERMISSIONS[role]) ?? "/unauthorized";
}

export function searchPreviewGuidance(role: Role, syntheticReviewMode: boolean): string {
  const mode = syntheticReviewMode ? " in synthetic review mode" : "";
  return `${ROLES[role].label} does not include global search${mode}. Choose an experience with claims access to preview search.`;
}
