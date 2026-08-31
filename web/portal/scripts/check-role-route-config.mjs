import { readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.cwd();
const source = readFileSync(join(root, "lib", "roles.ts"), "utf8");
const sidebar = readFileSync(join(root, "components", "iip", "Sidebar.tsx"), "utf8");

const rolePaths = [...source.matchAll(/"\/app(?:\/[^"]*)?"/g)].map((match) => match[0].slice(1, -1));
const sidebarPaths = [...sidebar.matchAll(/to:\s*"([^"]+)"/g)].map((match) => match[1]);
const roleKeys = [...source.matchAll(/^\s{2}([A-Za-z0-9]+):\s*\{/gm)].map((match) => match[1]);
const moduleList = source.match(/export const ALL_MODULES = \[([\s\S]*?)\];/)?.[1] ?? "";
const adminList = source.match(/export const ALL_ADMIN = \[([\s\S]*?)\];/)?.[1] ?? "";
const canonicalPaths = [...`${moduleList}\n${adminList}`.matchAll(/"\/app(?:\/[^"]*)?"/g)].map((match) => match[0].slice(1, -1));
const permissionRouteBlock = source.match(/export const ROUTE_ACCESS[\s\S]*?\n\];/)?.[0] ?? "";
const permissionPaths = [...permissionRouteBlock.matchAll(/path:\s*"([^\"]+)"/g)].map((match) => match[1]);
const expectedRoles = [
  "Admin",
  "ClaimsOfficer",
  "ClaimsSupervisor",
  "CustomerService",
  "Broker",
  "Compliance",
  "KnowledgeManager",
  "ITAdmin",
  "PublisherSupport",
];
const findings = [];

for (const role of expectedRoles) {
  if (!roleKeys.includes(role)) {
    findings.push(`Expected role "${role}" is missing from ROLES.`);
  }
}

for (const path of rolePaths) {
  if (path !== "/app" && !path.startsWith("/app/")) {
    findings.push(`Role route "${path}" must stay under /app.`);
  }
}

for (const href of sidebarPaths) {
  const covered = rolePaths.includes(href);
  if (!covered) {
    findings.push(`Sidebar route "${href}" is not present in role access config.`);
  }
  if (!permissionPaths.includes(href)) {
    findings.push(`Sidebar route "${href}" has no server-permission visibility rule.`);
  }
}

if (!source.includes("canAccessWithPermissions") || !source.includes("requiredAny")) {
  findings.push("Permission-derived route visibility is missing.");
}

if (!sidebar.includes("runtimeContext?.permissions") || !sidebar.includes("canAccessWithPermissions")) {
  findings.push("Protected navigation must consume server-returned effective permissions.");
}

for (const role of roleKeys) {
  const roleBlock = source.match(new RegExp(`${role}:\\s*\\{[\\s\\S]*?landing:\\s*"([^"]+)"`));
  const landing = roleBlock?.[1];
  if (!landing) {
    findings.push(`Role "${role}" has no landing route.`);
  } else if (!rolePaths.includes(landing)) {
    findings.push(`Role "${role}" landing route "${landing}" is not in role access config.`);
  }
}

const duplicates = canonicalPaths.filter((path, index) => canonicalPaths.indexOf(path) !== index);
for (const path of new Set(duplicates)) {
  findings.push(`Role route "${path}" is duplicated.`);
}

if (!source.includes("canonicalPath === m") || !source.includes("canonicalPath.startsWith(m + \"/\")")) {
  findings.push("canAccess must cover nested /app route children through prefix matching.");
}

if (!source.includes('m !== "/app"') || !source.includes('m !== "/app/admin"')) {
  findings.push("Root /app and /app/admin grants must remain exact so they do not authorize every nested route.");
}

if (findings.length > 0) {
  console.error("Role route configuration check failed:");
  for (const finding of findings) {
    console.error(`- ${finding}`);
  }
  process.exit(1);
}

console.log("Role route configuration check passed.");
