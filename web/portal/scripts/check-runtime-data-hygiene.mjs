import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const root = process.cwd();
const scanRoots = ["app", "components", "lib"];
const allowedDevelopmentFixtureRoots = new Set([
  "lib\\lovable-parity",
  "lib/lovable-parity"
]);
const banned = [
  "tenant-demo",
  "workspace-claims",
  "local-user",
  "local-admin",
  "local-supervisor",
  "local-service-agent",
  "Amina Okello",
  "amina@example.test",
  "POL-1001",
  "Minor collision",
  "customer@example.com",
  "+15551234567",
  "teams-channel-webhook",
  "Your claim update has been processed.",
  "sendSimulated",
  "Simulate Email",
  "Simulate SMS",
  "Simulate Teams",
  "Seed Demo Data",
  "Local/mock",
  "local mock",
  "SimulatedSent"
];

const allowedExtensions = new Set([".js", ".jsx", ".ts", ".tsx"]);
const findings = [];

function extensionOf(fileName) {
  const index = fileName.lastIndexOf(".");
  return index === -1 ? "" : fileName.slice(index);
}

function walk(directory) {
  const relativeDirectory = relative(root, directory);
  if (allowedDevelopmentFixtureRoots.has(relativeDirectory)) return;

  for (const entry of readdirSync(directory)) {
    const path = join(directory, entry);
    const stats = statSync(path);

    if (stats.isDirectory()) {
      if (entry === "node_modules" || entry === ".next") continue;
      walk(path);
      continue;
    }

    if (!allowedExtensions.has(extensionOf(entry))) continue;

    const content = readFileSync(path, "utf8");
    for (const term of banned) {
      if (content.includes(term)) {
        findings.push(`${relative(root, path)} contains banned runtime data term: ${term}`);
      }
    }
  }
}

for (const scanRoot of scanRoots) {
  walk(join(root, scanRoot));
}

if (findings.length > 0) {
  console.error("Frontend runtime data hygiene check failed:");
  for (const finding of findings) {
    console.error(`- ${finding}`);
  }
  process.exit(1);
}

console.log("Frontend runtime data hygiene check passed.");
