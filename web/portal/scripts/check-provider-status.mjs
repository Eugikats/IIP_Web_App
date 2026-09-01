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

for (const status of ["Healthy", "Operational"]) {
  assert.equal(providerStatusTone(status), "success", status);
  assert.equal(isProviderHealthy(status), true, status);
}

for (const status of [
  "Configured",
  "Connected",
  "Degraded",
  "Disabled",
  "Missing",
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
