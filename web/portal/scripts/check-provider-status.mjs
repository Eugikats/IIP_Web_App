import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { join } from "node:path";
import { runInNewContext } from "node:vm";

const require = createRequire(import.meta.url);
const ts = require("typescript");
const source = readFileSync(join(process.cwd(), "lib/providerHealth.ts"), "utf8");
const { outputText } = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
});
const loadedModule = { exports: {} };

runInNewContext(outputText, {
  exports: loadedModule.exports,
  module: loadedModule
});

const { isProviderHealthy, providerStatusTone, formatProviderStatusLabel } = loadedModule.exports;

// "Configured" is a genuinely good state, not a lesser one, the backend's own overall-status
// aggregate (AdminEndpoints.BuildProviderHealth in the API) only treats "missing"/"not_configured"
// as degraded, and several real providers (database, blobStorage, documentIntelligence, aiSearch)
// report "configured" rather than "healthy" because they have no active health ping. This script
// previously asserted the opposite, grouping "Configured" with genuinely bad states like
// "Degraded"/"Missing"/"Unavailable", which is exactly the bug that shipped live as "9 of 9
// providers need attention" next to a panel showing every provider as Configured.
for (const status of ["Healthy", "Operational", "Configured"]) {
  assert.equal(providerStatusTone(status), "success", status);
  assert.equal(isProviderHealthy(status), true, status);
}

for (const status of [
  "Connected",
  "Degraded",
  "Disabled",
  "Missing",
  "Mock",
  "Not configured",
  "not_configured",
  "not-configured",
  "Pending",
  "OK",
  "Ready",
  "Unavailable",
  "Unconfigured",
  "unknown-provider-state"
]) {
  assert.equal(providerStatusTone(status), "warning", status);
  assert.equal(isProviderHealthy(status), false, status);
}

assert.equal(formatProviderStatusLabel("not_configured"), "Not configured");
assert.equal(formatProviderStatusLabel("Not configured"), "Not configured");
assert.equal(formatProviderStatusLabel("configured"), "Configured");

assert.equal(formatProviderStatusLabel("mock"), "Mock");
assert.equal(providerStatusTone("mock"), "warning");
