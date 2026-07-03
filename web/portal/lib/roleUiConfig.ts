export const roles = [
  "Admin",
  "ClaimsOfficer",
  "ClaimsSupervisor",
  "CustomerService",
  "Broker",
  "Compliance",
  "Client",
  "KnowledgeManager",
  "ITAdmin",
  "PublisherSupport"
] as const;

export type Role = (typeof roles)[number];

export const roleLabels: Record<Role, string> = {
  Admin: "Tenant Admin",
  ClaimsOfficer: "Claims Officer",
  ClaimsSupervisor: "Claims Supervisor",
  CustomerService: "Customer Service",
  Broker: "Broker",
  Compliance: "Compliance Officer",
  Client: "Policyholder",
  KnowledgeManager: "Knowledge Manager",
  ITAdmin: "IT Admin",
  PublisherSupport: "Publisher Support"
};

export const rolePermissions: Record<Role, string[]> = {
  Admin: ["all"],
  ITAdmin: ["ViewCommandCenter", "ViewAuditLog", "ConfigureIntegrations", "ManageTenantSettings"],
  ClaimsOfficer: ["ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAssignedClaims", "ViewAiOutputs", "ManageCustomerCommunications", "SendCustomerUpdate"],
  ClaimsSupervisor: ["ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAllClaims", "ViewAssignedClaims", "ApproveSettlementWorkflowStep", "ViewAuditLog", "ViewAiOutputs", "OverrideAiRecommendation", "ManageCustomerCommunications", "SendCustomerUpdate"],
  CustomerService: ["ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAssignedClaims", "ViewAiOutputs", "ManageCustomerCommunications", "SendCustomerUpdate"],
  Broker: ["CreateClaim", "UploadDocument", "ViewAssignedClaims", "ViewAiOutputs"],
  Compliance: ["ViewCommandCenter", "ViewAllClaims", "ViewAuditLog", "ManageKnowledgeBase", "ViewAiOutputs", "ApproveSettlementWorkflowStep", "OverrideAiRecommendation"],
  Client: ["ViewClientPortal"],
  KnowledgeManager: ["ManageKnowledgeBase", "ViewAiOutputs"],
  PublisherSupport: ["ViewCommandCenter", "ViewAuditLog"]
};

export type NavIconKey =
  | "messageSquare"
  | "users"
  | "target"
  | "clipboardCheck"
  | "filePlus"
  | "fileSearch"
  | "bot"
  | "headset"
  | "workflow"
  | "bookOpen"
  | "network"
  | "barChart"
  | "shield"
  | "lock"
  | "lifeBuoy";

export type NavigationItem = {
  label: string;
  href: string;
  permission: string;
  icon: NavIconKey;
  exact?: boolean;
  mobile?: boolean;
};

export type NavigationGroup = {
  title: string;
  items: NavigationItem[];
};

export type RouteAccessRule = {
  path: string;
  permission: string;
  exact?: boolean;
};

// Only built modules are linked in Gate 1; future modules stay out of the
// shell until their backend contracts and routes exist.
export const navigationGroups: NavigationGroup[] = [
  {
    title: "Operations",
    items: [
      { label: "Command Center", href: "/", permission: "ViewCommandCenter", icon: "messageSquare", exact: true },
      { label: "Channels", href: "/channels", permission: "ViewCommandCenter", icon: "network", mobile: false },
      { label: "Customers", href: "/customers", permission: "ViewAssignedClaims", icon: "users" },
      { label: "Sales & Leads", href: "/sales-leads", permission: "ViewCommandCenter", icon: "target", mobile: false },
      { label: "My Claims", href: "/client-portal", permission: "ViewClientPortal", icon: "clipboardCheck" },
      { label: "Claims Intelligence", href: "/claims", permission: "ViewAssignedClaims", icon: "clipboardCheck" },
      { label: "New Claim", href: "/cases/new", permission: "CreateClaim", icon: "filePlus" },
      { label: "Document Checker", href: "/document-checker", permission: "UploadDocument", icon: "fileSearch" },
      { label: "Policy Intelligence", href: "/policy-intelligence", permission: "ViewAiOutputs", icon: "bookOpen" },
      { label: "AI Conversations", href: "/ai-assistant", permission: "ViewAiOutputs", icon: "bot" },
      { label: "Customer Service", href: "/customer-service", permission: "ViewAssignedClaims", icon: "headset" },
      { label: "Workflow & Approvals", href: "/approvals", permission: "ApproveSettlementWorkflowStep", icon: "workflow" },
      { label: "Knowledge Hub", href: "/knowledge-base", permission: "ManageKnowledgeBase", icon: "bookOpen" },
      { label: "Integrations & Channels", href: "/integrations", permission: "ConfigureIntegrations", icon: "network", mobile: false }
    ]
  },
  {
    title: "Governance",
    items: [
      { label: "Reports & Audit", href: "/reports", permission: "ViewCommandCenter", icon: "barChart" },
      { label: "Compliance", href: "/compliance", permission: "ViewAuditLog", icon: "shield" },
      { label: "Tenant Onboarding", href: "/tenant-onboarding", permission: "ManageTenantSettings", icon: "clipboardCheck", mobile: false },
      { label: "Admin Console", href: "/admin-settings", permission: "ManageTenantSettings", icon: "lock", mobile: false },
      { label: "Support & Health", href: "/support-health", permission: "ViewCommandCenter", icon: "lifeBuoy" }
    ]
  }
];

export const routeAccessRules: RouteAccessRule[] = [
  { path: "/", permission: "ViewCommandCenter", exact: true },
  { path: "/channels", permission: "ViewCommandCenter" },
  { path: "/customers", permission: "ViewAssignedClaims" },
  { path: "/sales-leads", permission: "ViewCommandCenter" },
  { path: "/client-portal", permission: "ViewClientPortal" },
  { path: "/claims", permission: "ViewAssignedClaims" },
  { path: "/cases/new", permission: "CreateClaim", exact: true },
  { path: "/cases/", permission: "ViewAssignedClaims" },
  { path: "/document-checker", permission: "UploadDocument" },
  { path: "/policy-intelligence", permission: "ViewAiOutputs" },
  { path: "/ai-assistant", permission: "ViewAiOutputs" },
  { path: "/customer-service", permission: "ViewAssignedClaims" },
  { path: "/approvals", permission: "ApproveSettlementWorkflowStep" },
  { path: "/knowledge-base", permission: "ManageKnowledgeBase" },
  { path: "/integrations", permission: "ConfigureIntegrations" },
  { path: "/reports", permission: "ViewCommandCenter" },
  { path: "/compliance", permission: "ViewAuditLog" },
  { path: "/tenant-onboarding", permission: "ManageTenantSettings" },
  { path: "/admin-settings", permission: "ManageTenantSettings" },
  { path: "/support-health", permission: "ViewCommandCenter" }
];

export function hasPermission(permissions: string[], permission: string) {
  return permissions.includes("all") || permissions.includes(permission);
}

export function requiredPermissionForPath(pathname: string) {
  const rule = routeAccessRules
    .filter((item) => (item.exact ? pathname === item.path : pathname === item.path || pathname.startsWith(item.path)))
    .sort((a, b) => b.path.length - a.path.length)[0];

  return rule?.permission ?? null;
}

export const commandCenterSuggestions: Partial<Record<Role, string[]>> = {
  ClaimsSupervisor: [
    "Summarize SLA risk and pending approvals.",
    "Which operational areas need human review?",
    "Summarize incomplete claims and customer update backlog.",
    "What should my team handle first?"
  ],
  CustomerService: [
    "Summarize customer update backlog.",
    "Which claims need customer communication?",
    "What is the current open claims picture?",
    "Which items need human review before a customer update?"
  ],
  Compliance: [
    "Summarize approvals and human-review pressure.",
    "Which operational areas need audit attention?",
    "Summarize assistant escalations.",
    "What metrics should I inspect before exporting an audit pack?"
  ],
  Admin: [
    "Summarize current operational health.",
    "What are the biggest workflow risks?",
    "Summarize provider-related limits honestly.",
    "Which queues need attention?"
  ],
  ITAdmin: [
    "Summarize provider-related limits honestly.",
    "What operational metrics depend on configured providers?",
    "Which queues need attention?",
    "Summarize current operational health."
  ]
};

export const defaultCommandCenterSuggestions = [
  "Summarize current operational health.",
  "Summarize SLA risk and pending approvals.",
  "Summarize incomplete claims and customer update backlog.",
  "What should I inspect first?"
];

export function isRole(value: string): value is Role {
  return roles.includes(value as Role);
}
