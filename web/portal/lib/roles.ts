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
  "/app/work-items",
  "/app/flows",
  "/app/runs",
  "/app/communications",
  "/app/knowledge",
  "/app/executive",
  "/app/integrations",
  "/app/audit",
];
export const ALL_ADMIN = ["/app/admin", "/app/admin/agents", "/app/admin/billing", "/app/admin/tenant-policy", "/app/admin/onboarding"];

export const ROLES: Record<Role, RoleDef> = {
  Admin: {
    key: "Admin", label: "Admin", title: "Workspace Administrator",
    modules: ALL_MODULES, admin: ALL_ADMIN, landing: "/app",
  },
  ClaimsOfficer: {
    key: "ClaimsOfficer", label: "Claims Officer", title: "Senior Claims Officer",
    modules: ["/app", "/app/claims", "/app/channels", "/app/customers", "/app/documents", "/app/communications", "/app/policy", "/app/knowledge", "/app/work-items", "/app/flows", "/app/runs"], admin: [], landing: "/app/claims",
  },
  ClaimsSupervisor: {
    key: "ClaimsSupervisor", label: "Claims Supervisor", title: "Claims Supervisor",
    modules: ["/app", "/app/claims", "/app/work-items", "/app/flows", "/app/runs", "/app/communications", "/app/fraud", "/app/executive", "/app/policy", "/app/customers"], admin: [], landing: "/app/work-items",
  },
  CustomerService: {
    key: "CustomerService", label: "Customer Service", title: "Customer Care Lead",
    modules: ["/app", "/app/channels", "/app/communications", "/app/acquisition", "/app/customers", "/app/knowledge", "/app/work-items", "/app/flows", "/app/runs"], admin: [], landing: "/app/channels",
  },
  Broker: {
    key: "Broker", label: "Broker", title: "Broker",
    modules: ["/app", "/app/acquisition", "/app/claims"], admin: [], landing: "/app/acquisition",
  },
  Compliance: {
    key: "Compliance", label: "Compliance", title: "Compliance Officer",
    modules: ["/app", "/app/audit", "/app/communications", "/app/executive", "/app/policy", "/app/work-items", "/app/flows", "/app/runs"], admin: [], landing: "/app/audit",
  },
  KnowledgeManager: {
    key: "KnowledgeManager", label: "Knowledge Manager", title: "Knowledge Manager",
    modules: ["/app", "/app/knowledge", "/app/documents", "/app/policy"], admin: [], landing: "/app/knowledge",
  },
  ITAdmin: {
    key: "ITAdmin", label: "IT Admin", title: "IT Administrator",
    modules: ["/app", "/app/integrations", "/app/audit", "/app/work-items", "/app/flows", "/app/runs"], admin: ["/app/admin"], landing: "/app/integrations",
  },
  PublisherSupport: {
    key: "PublisherSupport", label: "Publisher Support", title: "Publisher Support Engineer",
    modules: ["/app", "/app/integrations", "/app/audit", "/app/executive"], admin: ["/app/admin", "/app/admin/agents", "/app/admin/onboarding"], landing: "/app/integrations",
  },
};

export function canAccess(role: Role, path: string): boolean {
  const def = ROLES[role];
  const all = [...def.modules, ...def.admin];
  const canonicalPath = path === "/app/workflows" || path.startsWith("/app/workflows/")
    ? path.replace("/app/workflows", "/app/work-items")
    : path === "/app/support" || path.startsWith("/app/support/")
      ? path.replace("/app/support", "/app/integrations")
      : path === "/app/settings" || path.startsWith("/app/settings/")
        ? "/app/admin"
        : path;
  return all.some((m) =>
    canonicalPath === m
    || (m !== "/app" && m !== "/app/admin" && canonicalPath.startsWith(m + "/"))
  );
}
