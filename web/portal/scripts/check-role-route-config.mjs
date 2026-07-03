import { readFileSync } from "node:fs";
import { join } from "node:path";

const source = readFileSync(join(process.cwd(), "lib", "roleUiConfig.ts"), "utf8");

const navHrefMatches = [...source.matchAll(/href:\s*"([^"]+)"/g)].map((match) => match[1]);
const routeRuleMatches = [...source.matchAll(/\{\s*path:\s*"([^"]+)",\s*permission:\s*"([^"]+)"/g)].map((match) => ({
  path: match[1],
  permission: match[2]
}));
const permissionMatches = [...source.matchAll(/"([A-Za-z]+(?:[A-Za-z0-9]+)*)"/g)]
  .map((match) => match[1])
  .filter((value) => /^(View|Create|Upload|Approve|Manage|Configure|Override)/.test(value));

const configuredPermissions = new Set(permissionMatches);
const routePaths = routeRuleMatches.map((rule) => rule.path);
const findings = [];

for (const href of navHrefMatches) {
  const covered = routeRuleMatches.some((rule) => href === rule.path || href.startsWith(rule.path));
  if (!covered) {
    findings.push(`Navigation href "${href}" has no route access rule.`);
  }
}

for (const rule of routeRuleMatches) {
  if (!configuredPermissions.has(rule.permission)) {
    findings.push(`Route access rule "${rule.path}" uses unknown permission "${rule.permission}".`);
  }
}

const duplicates = routePaths.filter((path, index) => routePaths.indexOf(path) !== index);
for (const path of new Set(duplicates)) {
  findings.push(`Route access rule "${path}" is duplicated.`);
}

if (!/Client:\s*\[\s*"ViewClientPortal"\s*\]/.test(source)) {
  findings.push("Client role must only be granted ViewClientPortal.");
}

if (findings.length > 0) {
  console.error("Role route configuration check failed:");
  for (const finding of findings) {
    console.error(`- ${finding}`);
  }
  process.exit(1);
}

console.log("Role route configuration check passed.");
