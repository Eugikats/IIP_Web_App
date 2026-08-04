#!/usr/bin/env node

import { randomUUID } from "node:crypto";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { createServer } from "node:http";
import process from "node:process";
import { performance } from "node:perf_hooks";

function usage() {
  console.log([
    "Usage:",
    "  node tools/qa/Invoke-IipPerformanceSmoke.mjs --base-url <url> [options]",
    "  node tools/qa/Invoke-IipPerformanceSmoke.mjs --self-test [options]",
    "",
    "Options:",
    "  --requests <n>              Requests per scenario (default: 50)",
    "  --concurrency <n>           Concurrent workers (default: 5)",
    "  --include-authenticated     Test claims routes using IIP_UAT_ACCESS_TOKEN",
    "  --include-provider-health   Also test /api/v1/provider-status",
    "  --include-compliance        Also test /api/v1/compliance/posture",
    "  --claim-id <id>             Also test /api/v1/claims/{id}",
    "  --allow-remote              Required for non-loopback targets",
    "  --output <path>             Write the JSON result to a file",
    "  --self-test                 Run against an internal localhost fixture server",
    "  --help                      Show this help",
    "",
    "Protected-route tokens are read only from IIP_UAT_ACCESS_TOKEN."
  ].join("\n"));
}

function parsePositiveInteger(value, name) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new Error(name + " must be a positive integer.");
  }

  return parsed;
}

function parseArgs(argv) {
  const options = {
    allowRemote: false,
    baseUrl: "",
    claimId: "",
    concurrency: 5,
    help: false,
    includeAuthenticated: false,
    includeCompliance: false,
    includeProviderHealth: false,
    output: "",
    requests: 50,
    selfTest: false
  };

  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--allow-remote") options.allowRemote = true;
    else if (arg === "--include-authenticated") options.includeAuthenticated = true;
    else if (arg === "--include-compliance") options.includeCompliance = true;
    else if (arg === "--include-provider-health") options.includeProviderHealth = true;
    else if (arg === "--self-test") options.selfTest = true;
    else if (arg === "--help" || arg === "-h") options.help = true;
    else if (arg === "--base-url") options.baseUrl = argv[++index] ?? "";
    else if (arg === "--claim-id") options.claimId = argv[++index] ?? "";
    else if (arg === "--output") options.output = argv[++index] ?? "";
    else if (arg === "--requests") options.requests = parsePositiveInteger(argv[++index], "--requests");
    else if (arg === "--concurrency") options.concurrency = parsePositiveInteger(argv[++index], "--concurrency");
    else throw new Error("Unknown argument: " + arg);
  }

  return options;
}

function percentile(sorted, ratio) {
  if (sorted.length === 0) return 0;
  return sorted[Math.min(sorted.length - 1, Math.ceil(sorted.length * ratio) - 1)];
}

function isLoopback(url) {
  return ["localhost", "127.0.0.1", "::1"].includes(url.hostname);
}

async function requestOnce(baseUrl, scenario, token) {
  const started = performance.now();
  try {
    const response = await fetch(new URL(scenario.path, baseUrl), {
      signal: AbortSignal.timeout(Math.max(5000, scenario.targetP95Ms * 10)),
      headers: {
        Accept: "application/json",
        "X-Correlation-ID": randomUUID(),
        ...(scenario.authenticated ? { Authorization: "Bearer " + token } : {})
      }
    });
    await response.arrayBuffer();
    return {
      durationMs: performance.now() - started,
      ok: scenario.expectedStatuses.includes(response.status),
      status: response.status
    };
  } catch (error) {
    return {
      durationMs: performance.now() - started,
      error: error instanceof Error ? error.message : String(error),
      ok: false,
      status: 0
    };
  }
}

async function runScenario(baseUrl, scenario, requests, concurrency, token) {
  await requestOnce(baseUrl, scenario, token);
  const results = new Array(requests);
  let nextIndex = 0;

  async function worker() {
    while (nextIndex < requests) {
      const current = nextIndex;
      nextIndex += 1;
      results[current] = await requestOnce(baseUrl, scenario, token);
    }
  }

  const started = performance.now();
  await Promise.all(Array.from({ length: Math.min(concurrency, requests) }, () => worker()));
  const elapsedMs = performance.now() - started;
  const durations = results.map((result) => result.durationMs).sort((left, right) => left - right);
  const failures = results.filter((result) => !result.ok);
  const statuses = [...new Set(results.map((result) => result.status))].sort((left, right) => left - right);
  const statusCounts = Object.fromEntries(
    statuses.map((status) => [status, results.filter((result) => result.status === status).length])
  );
  const p95Ms = percentile(durations, 0.95);

  return {
    id: scenario.id,
    path: scenario.path,
    requests,
    concurrency,
    elapsedMs: Math.round(elapsedMs),
    requestsPerSecond: Number((requests / (elapsedMs / 1000)).toFixed(2)),
    errorRate: Number((failures.length / requests).toFixed(4)),
    p50Ms: Math.round(percentile(durations, 0.5)),
    p95Ms: Math.round(p95Ms),
    p99Ms: Math.round(percentile(durations, 0.99)),
    targetP95Ms: scenario.targetP95Ms,
    statusCounts,
    passed: failures.length === 0 && p95Ms <= scenario.targetP95Ms,
    errors: failures.slice(0, 5).map((failure) => failure.error ?? "HTTP " + failure.status)
  };
}

async function startSelfTestServer() {
  const server = createServer((request, response) => {
    const authenticated = request.headers.authorization === "Bearer self-test-token";
    if (request.url === "/health/ready") {
      response.writeHead(200, { "content-type": "application/json" });
      response.end('{"status":"ready"}');
      return;
    }

    if (request.url?.startsWith("/api/v1/claims") && authenticated) {
      response.writeHead(200, { "content-type": "application/json" });
      response.end("[]");
      return;
    }

    if (request.url === "/api/v1/provider-status" && authenticated) {
      response.writeHead(200, { "content-type": "application/json" });
      response.end('{"overallStatus":"healthy","providers":[]}');
      return;
    }

    if (request.url === "/api/v1/compliance/posture" && authenticated) {
      response.writeHead(200, { "content-type": "application/json" });
      response.end('{"retention":{"auditRetentionDays":365},"providers":{"seedDemoData":false}}');
      return;
    }

    response.writeHead(401, { "content-type": "application/json" });
    response.end('{"error":"unauthorized"}');
  });

  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const address = server.address();
  if (!address || typeof address === "string") {
    throw new Error("Self-test server did not expose a TCP port.");
  }

  return { baseUrl: "http://127.0.0.1:" + address.port, server };
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  if (options.help) {
    usage();
    return;
  }

  let fixture;
  if (options.selfTest) {
    fixture = await startSelfTestServer();
    options.baseUrl = fixture.baseUrl;
    options.includeAuthenticated = true;
    options.claimId = "self-test-claim";
  }

  try {
    if (!options.baseUrl) throw new Error("--base-url is required unless --self-test is used.");
    const baseUrl = new URL(options.baseUrl);
    if (!["http:", "https:"].includes(baseUrl.protocol)) {
      throw new Error("--base-url must use http or https.");
    }
    if (!isLoopback(baseUrl) && !options.allowRemote) {
      throw new Error("Remote performance targets require explicit --allow-remote approval.");
    }

    const token = options.selfTest ? "self-test-token" : process.env.IIP_UAT_ACCESS_TOKEN ?? "";
    if (options.includeAuthenticated && !token) {
      throw new Error("IIP_UAT_ACCESS_TOKEN is required for --include-authenticated.");
    }

    const scenarios = [
      {
        id: "PERF-HEALTH-001",
        path: "/health/ready",
        authenticated: false,
        expectedStatuses: [200],
        targetP95Ms: 300
      }
    ];
    if (options.includeAuthenticated) {
      scenarios.push({
        id: "PERF-API-001",
        path: "/api/v1/claims",
        authenticated: true,
        expectedStatuses: [200],
        targetP95Ms: 800
      });
    }
    if (options.includeAuthenticated && options.includeProviderHealth) {
      scenarios.push({
        id: "PERF-ADMIN-001",
        path: "/api/v1/provider-status",
        authenticated: true,
        expectedStatuses: [200],
        targetP95Ms: 800
      });
    }
    if (options.includeAuthenticated && options.includeCompliance) {
      scenarios.push({
        id: "PERF-COMPLIANCE-001",
        path: "/api/v1/compliance/posture",
        authenticated: true,
        expectedStatuses: [200],
        targetP95Ms: 1200
      });
    }
    if (options.includeAuthenticated && options.claimId) {
      scenarios.push({
        id: "PERF-API-002",
        path: "/api/v1/claims/" + encodeURIComponent(options.claimId),
        authenticated: true,
        expectedStatuses: [200],
        targetP95Ms: 1200
      });
    }

    const results = [];
    for (const scenario of scenarios) {
      results.push(await runScenario(baseUrl, scenario, options.requests, options.concurrency, token));
    }

    const report = {
      generatedAt: new Date().toISOString(),
      baseUrl: baseUrl.origin,
      mode: options.selfTest ? "self-test" : "runtime",
      requests: options.requests,
      concurrency: options.concurrency,
      results,
      thresholdFailures: results.filter((result) => !result.passed).map((result) => ({
        id: result.id,
        path: result.path,
        p95Ms: result.p95Ms,
        targetP95Ms: result.targetP95Ms,
        errorRate: result.errorRate
      })),
      passed: results.every((result) => result.passed)
    };
    console.table(results.map(({ errors, statusCounts, ...result }) => result));
    if (options.output) {
      await mkdir(path.dirname(options.output), { recursive: true });
      await writeFile(options.output, JSON.stringify(report, null, 2) + "\n", "utf8");
    }
    if (!report.passed) process.exitCode = 1;
  } finally {
    if (fixture) {
      await new Promise((resolve, reject) => {
        fixture.server.close((error) => (error ? reject(error) : resolve()));
      });
    }
  }
}

main().catch((error) => {
  console.error("Performance smoke failed: " + (error instanceof Error ? error.message : String(error)));
  process.exitCode = 1;
});
