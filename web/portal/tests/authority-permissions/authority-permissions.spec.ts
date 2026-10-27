import { expect, test } from "@playwright/test";
import {
  PREVIEW_PERMISSIONS,
  ROLES,
  ROUTE_ACCESS,
  STAFF_ROLES,
  canAccess,
  canAccessWithPermissions,
  canPreviewPermission,
  previewLandingRoute,
  searchPreviewGuidance,
  type Permission,
  type Role,
} from "../../lib/roles";

const expectedLabels: Record<Role, string> = {
  Admin: "Workspace Administrator",
  ClaimsOfficer: "Claims Officer",
  ClaimsSupervisor: "Claims Supervisor",
  CustomerService: "Customer Service",
  Broker: "Broker",
  Compliance: "Compliance Officer",
  KnowledgeManager: "Knowledge Manager",
  ITAdmin: "IT Administrator",
  PublisherSupport: "Publisher Support Engineer",
};

const consequentialGrants: Record<Permission, Role[]> = {
  ApproveSettlementWorkflowStep: ["Admin", "ClaimsSupervisor", "Compliance"],
  OverrideAiRecommendation: ["Admin", "ClaimsSupervisor", "Compliance"],
  ExportEvidencePack: ["Admin", "ClaimsSupervisor", "Compliance", "ITAdmin"],
  SendCustomerUpdate: ["Admin", "ClaimsOfficer", "ClaimsSupervisor", "CustomerService"],
  ManageAgentWorkflows: ["Admin", "ITAdmin"],
  ManageTenantOnboarding: ["PublisherSupport"],
} as Record<Permission, Role[]>;

test("every staff preview uses one full operational label and a permitted landing route", () => {
  expect(STAFF_ROLES).toEqual(Object.keys(expectedLabels));
  for (const role of STAFF_ROLES) {
    expect(ROLES[role].label).toBe(expectedLabels[role]);
    expect(ROLES[role].title).toBe(expectedLabels[role]);
    expect(previewLandingRoute(role)).toBe(ROLES[role].landing);
    expect(canAccess(role, previewLandingRoute(role))).toBe(true);
  }
});

test("preview navigation is derived from the same permission rules used for protected presentation", () => {
  for (const role of STAFF_ROLES) {
    for (const rule of ROUTE_ACCESS) {
      expect(canAccess(role, rule.path), `${role}: ${rule.path}`).toBe(
        canAccessWithPermissions(PREVIEW_PERMISSIONS[role], rule.path),
      );
    }
  }
});

test("consequential preview controls match the current server role contract for every staff role", () => {
  for (const [permission, grantedRoles] of Object.entries(consequentialGrants) as Array<[Permission, Role[]]>) {
    for (const role of STAFF_ROLES) {
      expect(canPreviewPermission(role, permission), `${role}: ${permission}`).toBe(grantedRoles.includes(role));
    }
  }
});

test("disabled preview search guidance names the selected experience and recovery", () => {
  for (const role of STAFF_ROLES) {
    const guidance = searchPreviewGuidance(role, true);
    expect(guidance).toContain(ROLES[role].label);
    expect(guidance).toContain("synthetic review mode");
  }
});
