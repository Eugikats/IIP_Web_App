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
  name: string;
  initials: string;
  modules: string[];
  admin: string[];
  landing: string;
}

export const ALL_MODULES = [
  "/app",
  "/app/claims",
  "/app/channels",
  "/app/customers",
  "/app/documents",
  "/app/acquisition",
  "/app/policy",
  "/app/fraud",
  "/app/underwriting",
  "/app/workflows",
  "/app/communications",
  "/app/knowledge",
  "/app/executive",
  "/app/integrations",
  "/app/audit",
  "/app/support",
];
export const ALL_ADMIN = ["/app/admin", "/app/admin/agents", "/app/admin/billing", "/app/admin/tenant-policy", "/app/admin/onboarding", "/app/settings"];

export const ROLES: Record<Role, RoleDef> = {
  Admin: {
    key: "Admin", label: "Admin", title: "Workspace Administrator", name: "Timothy Okello", initials: "TO",
    modules: ALL_MODULES, admin: ALL_ADMIN, landing: "/app",
  },
  ClaimsOfficer: {
    key: "ClaimsOfficer", label: "Claims Officer", title: "Senior Claims Officer", name: "Sarah Achieng", initials: "SA",
    modules: ["/app", "/app/claims", "/app/channels", "/app/customers", "/app/documents", "/app/communications", "/app/policy", "/app/knowledge"], admin: [], landing: "/app/claims",
  },
  ClaimsSupervisor: {
    key: "ClaimsSupervisor", label: "Claims Supervisor", title: "Claims Supervisor", name: "Joseph Mugisha", initials: "JM",
    modules: ["/app", "/app/claims", "/app/workflows", "/app/communications", "/app/fraud", "/app/executive", "/app/policy", "/app/customers"], admin: [], landing: "/app/workflows",
  },
  CustomerService: {
    key: "CustomerService", label: "Customer Service", title: "Customer Care Lead", name: "Rita Naluyima", initials: "RN",
    modules: ["/app", "/app/channels", "/app/communications", "/app/acquisition", "/app/customers", "/app/knowledge"], admin: [], landing: "/app/channels",
  },
  Broker: {
    key: "Broker", label: "Broker", title: "Broker · Kato Partners", name: "Michael Kato", initials: "MK",
    modules: ["/app", "/app/acquisition", "/app/claims"], admin: [], landing: "/app/acquisition",
  },
  Compliance: {
    key: "Compliance", label: "Compliance", title: "Compliance Officer", name: "Lydia Byaruhanga", initials: "LB",
    modules: ["/app", "/app/audit", "/app/communications", "/app/executive", "/app/policy"], admin: [], landing: "/app/audit",
  },
  KnowledgeManager: {
    key: "KnowledgeManager", label: "Knowledge Manager", title: "Knowledge Manager", name: "Grace Wanjiku", initials: "GW",
    modules: ["/app", "/app/knowledge", "/app/documents", "/app/policy"], admin: [], landing: "/app/knowledge",
  },
  ITAdmin: {
    key: "ITAdmin", label: "IT Admin", title: "IT Administrator", name: "David Mensah", initials: "DM",
    modules: ["/app", "/app/integrations", "/app/support", "/app/audit"], admin: ["/app/admin", "/app/settings"], landing: "/app/support",
  },
  PublisherSupport: {
    key: "PublisherSupport", label: "Publisher Support", title: "Otic Support Engineer", name: "Peter Kagwa", initials: "PK",
    modules: ["/app", "/app/integrations", "/app/support", "/app/audit", "/app/executive"], admin: ["/app/admin", "/app/admin/agents", "/app/admin/onboarding"], landing: "/app/support",
  },
};

export function canAccess(role: Role, path: string): boolean {
  const def = ROLES[role];
  const all = [...def.modules, ...def.admin];
  return all.some((m) => path === m || path.startsWith(m + "/"));
}
