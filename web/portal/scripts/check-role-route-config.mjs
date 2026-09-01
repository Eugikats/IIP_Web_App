import { readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.cwd();
const source = readFileSync(join(root, "lib", "roles.ts"), "utf8");
const sidebar = readFileSync(join(root, "components", "iip", "Sidebar.tsx"), "utf8");
const roleBlock = source.match(/export const ROLES:[\s\S]*?\n\};/)?.[0] ?? "";
const roleKeys = [...roleBlock.matchAll(/^\s{2}([A-Za-z0-9]+):\s*\{/gm)].map((match) => match[1]);
const permissionRouteBlock = source.match(/export const ROUTE_ACCESS[\s\S]*?\n\];/)?.[0] ?? "";
const permissionPaths = [...permissionRouteBlock.matchAll(/path:\s*"([^"]+)"/g)].map((match) => match[1]);
const sidebarPaths = [...sidebar.matchAll(/to:\s*"([^"]+)"/g)].map((match) => match[1]);
const expectedRoles = [
  "Admin", "ClaimsOfficer", "ClaimsSupervisor", "CustomerService", "Broker",
  "Compliance", "KnowledgeManager", "ITAdmin", "PublisherSupport",
];
const findings = [];

for (const role of expectedRoles) {
  if (!roleKeys.includes(role)) findings.push(`Expected role "${role}" is missing from ROLES.`);
  if (!source.includes(`${role}:`)) findings.push(`Expected role "${role}" is missing from PREVIEW_PERMISSIONS.`);
}

for (const path of permissionPaths) {
  if (path !== "/app" && !path.startsWith("/app/")) findings.push(`Role route "${path}" must stay under /app.`);
}

for (const href of sidebarPaths) {
  if (!permissionPaths.includes(href)) findings.push(`Sidebar route "${href}" has no permission visibility rule.`);
}

for (const role of roleKeys) {
  const definition = roleBlock.match(new RegExp(`${role}:\\s*\\{[\\s\\S]*?landing:\\s*"([^"]+)"`));
  const landing = definition?.[1];
  if (!landing) findings.push(`Role "${role}" has no landing route.`);
  else if (!permissionPaths.includes(landing)) findings.push(`Role "${role}" landing route "${landing}" has no permission rule.`);
}

if (!source.includes("canAccessWithPermissions") || !source.includes("requiredAny")) findings.push("Permission-derived route visibility is missing.");
if (!source.includes("canAccessWithPermissions(PREVIEW_PERMISSIONS[role], path)")) findings.push("Preview navigation must derive from the shared permission route rules.");
if (!sidebar.includes("runtimeContext?.permissions") || !sidebar.includes("canAccessWithPermissions")) findings.push("Protected navigation must consume server-returned effective permissions.");

if (findings.length > 0) {
  console.error("Role route configuration check failed:");
  for (const finding of findings) console.error(`- ${finding}`);
  process.exit(1);
}

console.log("Role route configuration check passed.");
