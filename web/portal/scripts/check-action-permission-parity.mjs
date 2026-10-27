import { readFileSync } from "node:fs";
import { join } from "node:path";

// WO-05: the portal's PREVIEW_PERMISSIONS (lib/roles.ts) is a hand-maintained mirror of the
// backend's authoritative RolePermissionMap (src/InsuranceIntelligence.Domain/Administration).
// Action controls gate their visibility on this mirror in local-test/parity mode (see
// canPreviewPermission usages in ClaimReview.tsx), if the mirror drifts from the real map, a
// role could see a button the server will actually refuse (the exact bug this work order fixed:
// ITAdmin/Compliance saw "Upload document" despite lacking UploadDocument server-side). This
// script fails the build the moment the two tables diverge, in either direction.
const portalRoot = process.cwd();
const repoRoot = join(portalRoot, "..", "..");

const rolesSource = readFileSync(join(portalRoot, "lib", "roles.ts"), "utf8");
const backendSource = readFileSync(
  join(repoRoot, "src", "InsuranceIntelligence.Domain", "Administration", "RolePermissionMap.cs"),
  "utf8",
);
const permissionEnumSource = readFileSync(
  join(repoRoot, "src", "InsuranceIntelligence.Domain", "Administration", "Permission.cs"),
  "utf8",
);

const findings = [];

// --- Parse the frontend mirror: PREVIEW_PERMISSIONS = { Role: [...], ... } ---
const previewBlock = rolesSource.match(/export const PREVIEW_PERMISSIONS:[\s\S]*?\n\};/)?.[0] ?? "";
if (!previewBlock) findings.push("Could not find PREVIEW_PERMISSIONS in lib/roles.ts.");

const frontendPermissionsByRole = new Map();
for (const match of previewBlock.matchAll(/^\s{2}(\w+):\s*(ALL_PERMISSIONS(?:\.filter\(\(permission\) => permission !== "ManageTenantOnboarding"\))?|\[([^\]]*)\])/gm)) {
  const [, role, allOrList, list] = match;
  if (allOrList.startsWith("ALL_PERMISSIONS")) {
    frontendPermissionsByRole.set(role, allOrList.includes(".filter") ? "ALL_EXCEPT_PLATFORM" : "ALL");
  } else {
    const permissions = [...(list ?? "").matchAll(/"([A-Za-z]+)"/g)].map((m) => m[1]);
    frontendPermissionsByRole.set(role, new Set(permissions));
  }
}

// --- Parse the backend map: [PlatformRole.X] = [ Permission.A, Permission.B, ... ] ---
const backendPermissionsByRole = new Map();
const allBackendPermissions = [...permissionEnumSource.matchAll(/^\s*([A-Za-z]+),?\s*$/gm)]
  .map((m) => m[1])
  .filter((name) => name !== "Permission"); // guard against matching the enum declaration line itself

for (const match of backendSource.matchAll(
  /\[PlatformRole\.(\w+)\]\s*=\s*(Enum\.GetValues<Permission>\(\)(?:\s*\.Where\([^\n]+?\)\.ToArray\(\))?|\[([\s\S]*?)\])/g,
)) {
  const [, role, allOrList, list] = match;
  if (allOrList.startsWith("Enum.GetValues")) {
    backendPermissionsByRole.set(role, allOrList.includes(".Where") ? "ALL_EXCEPT_PLATFORM" : "ALL");
  } else {
    const permissions = [...(list ?? "").matchAll(/Permission\.(\w+)/g)].map((m) => m[1]);
    backendPermissionsByRole.set(role, new Set(permissions));
  }
}

if (backendPermissionsByRole.size === 0) findings.push("Could not parse any role from RolePermissionMap.cs.");
if (allBackendPermissions.length === 0) findings.push("Could not parse the Permission enum from Permission.cs.");

// --- Compare ---
// PlatformRole.Client is the client-portal (policyholder-facing) role, it never appears in the
// staff Role type or PREVIEW_PERMISSIONS (see lib/roles.ts STAFF_ROLES / ROLES), because it is
// not a staff preview experience at all; it is handled entirely by ClientPortalEndpoints.cs on a
// separate surface. Deliberate, not drift.
const backendOnlyRoles = new Set(["Client", "Integration"]);

const roles = new Set(
  [...frontendPermissionsByRole.keys(), ...backendPermissionsByRole.keys()].filter(
    (role) => !backendOnlyRoles.has(role),
  ),
);
for (const role of roles) {
  const frontend = frontendPermissionsByRole.get(role);
  const backend = backendPermissionsByRole.get(role);

  if (frontend === undefined) {
    findings.push(`Role "${role}" exists in the backend RolePermissionMap but has no PREVIEW_PERMISSIONS entry.`);
    continue;
  }
  if (backend === undefined) {
    findings.push(`Role "${role}" has a PREVIEW_PERMISSIONS entry but does not exist in the backend RolePermissionMap.`);
    continue;
  }

  const frontendSet = frontend === "ALL" ? new Set(allBackendPermissions)
    : frontend === "ALL_EXCEPT_PLATFORM" ? new Set(allBackendPermissions.filter((permission) => permission !== "ManageTenantOnboarding")) : frontend;
  const backendSet = backend === "ALL" ? new Set(allBackendPermissions)
    : backend === "ALL_EXCEPT_PLATFORM" ? new Set(allBackendPermissions.filter((permission) => permission !== "ManageTenantOnboarding")) : backend;

  for (const permission of backendSet) {
    if (!frontendSet.has(permission)) {
      findings.push(
        `Role "${role}" has "${permission}" server-side (RolePermissionMap.cs) but PREVIEW_PERMISSIONS in lib/roles.ts is missing it, a preview/parity user could see an action control this permission does not gate that the server would actually allow, or (more dangerously) the reverse: an action control gated on this permission would be hidden even though the server grants it.`,
      );
    }
  }
  for (const permission of frontendSet) {
    if (!backendSet.has(permission)) {
      findings.push(
        `Role "${role}" has "${permission}" in PREVIEW_PERMISSIONS (lib/roles.ts) but RolePermissionMap.cs does not grant it server-side, an action control gated on this permission would render in preview/parity mode and then be refused with 403 by the real server, exactly the class of bug WO-05 fixed for UploadDocument.`,
      );
    }
  }
}

if (findings.length > 0) {
  console.error("Action permission parity check failed:");
  for (const finding of findings) console.error(`- ${finding}`);
  process.exit(1);
}

console.log(`Action permission parity check passed (${roles.size} roles, ${allBackendPermissions.length} permissions in sync).`);
