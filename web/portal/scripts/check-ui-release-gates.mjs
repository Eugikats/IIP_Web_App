import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.cwd();
const findings = [];

function read(path) {
  const fullPath = join(root, path);
  if (!existsSync(fullPath)) {
    findings.push(`${path}: file is missing.`);
    return "";
  }

  return readFileSync(fullPath, "utf8");
}

function requireText(path, text, message) {
  const source = read(path);
  if (!source.includes(text)) {
    findings.push(`${path}: ${message}`);
  }
}

function rejectPattern(path, pattern, message) {
  const source = read(path);
  if (pattern.test(source)) {
    findings.push(`${path}: ${message}`);
  }
}

function requirePattern(path, pattern, message) {
  const source = read(path);
  if (!pattern.test(source)) {
    findings.push(`${path}: ${message}`);
  }
}

requireText("app/app/layout.tsx", "@/components/iip/AppShell", "/app routes must render inside the governed IIP shell.");
requireText("components/iip/AppShell.tsx", "RuntimeContextProvider", "must resolve runtime identity through the shared runtime context provider.");
requireText("components/iip/AppShell.tsx", "canAccessWithPermissions(runtimeContext?.permissions", "protected /app presentation must use server-returned effective permissions.");
requireText("components/iip/AppShell.tsx", "firstAccessibleRoute(runtimeContext?.permissions", "denied routes must fall back to an actually permitted destination.");
requireText("components/iip/AppShell.tsx", "<Sidebar />", "must render role-aware left navigation.");
requireText("components/iip/AppShell.tsx", "<CopilotPanel />", "must render the right evidence/context rail.");
requireText("components/iip/AppShell.tsx", "<StatusBar />", "must render the environment/status footer.");
requireText("components/iip/AppShell.tsx", "!runtimeContext", "must withhold protected shell content until /api/v1/me resolves.");
requireText("components/iip/AppShell.tsx", "Verifying your access", "must render a bounded authentication loading state.");
requireText("lovable-pages/Login.tsx", "runtimeProfile", "login environment labeling must come from runtime configuration.");
requireText("lovable-pages/Login.tsx", "Microsoft Entra ID", "login must identify the configured organization identity provider.");
rejectPattern("lovable-pages/Login.tsx", /Authority tenant|Resolved after sign-in|href="#"+|glass-elevated|err instanceof Error/, "login must not expose implementation details, dead links, prototype card composition, or raw auth errors.");

requireText("lib/runtimeContext.tsx", "/api/v1/me", "runtime context must come from the governed /api/v1/me endpoint.");
requireText("lib/runtimeContext.tsx", "permissions: (body.permissions", "runtime context must retain server-returned effective permissions.");
requireText("lib/runtimeContext.tsx", "LovableVisualParity", "visual parity identity must stay explicitly marked when the dev fixture mode is enabled.");
requireText("lib/runtimeContext.tsx", "UiServiceError.fromUnknown", "runtime context failures must not expose raw browser or provider errors.");
requireText("next.config.ts", "NEXT_PUBLIC_API_BASE_URL is required for protected portal builds", "protected builds must fail closed when the API origin is missing.");
requireText("next.config.ts", "if (protectedRuntime) return [];", "protected runtimes must not proxy missing API configuration to localhost.");
rejectPattern("lib/runtimeContext.tsx", /headers:\s*\{[\s\S]*?(X-Tenant-Id|X-Workspace-Id|X-User-Id)/, "runtime context fetch must not send browser-owned authority headers.");

requireText("components/iip/Sidebar.tsx", "canAccessWithPermissions(runtimeContext?.permissions", "protected navigation must be effective-permission-filtered.");
requireText("components/iip/Sidebar.tsx", "usePreviewRole", "browser role preview must remain explicitly separated from protected navigation.");
requireText("components/iip/Sidebar.tsx", "runtimeContext?.availableWorkspaces", "workspace label must use backend runtime context.");
requireText("components/iip/Sidebar.tsx", "Workspace not configured", "missing workspace must render an honest state.");

requireText("components/iip/CopilotPanel.tsx", "useProviderHealth", "evidence rail must render backend-owned provider health.");
requireText("components/iip/CopilotPanel.tsx", "Not configured", "provider gaps must render an honest not-configured state.");
requireText("lib/lovable-parity/data/providerHealth.ts", "/api/v1/provider-status", "provider health must come from the governed backend endpoint.");
requireText("lib/lovable-parity/mode.ts", "process.env.NODE_ENV !== \"production\"", "visual parity fixtures must be disabled in production builds.");
requireText("components/iip/ProviderHealthGrid.tsx", "providerStatusTone", "provider cards must use the shared fail-closed status classifier.");
requireText("components/iip/StatusBar.tsx", "isProviderHealthy", "the status bar must use the shared fail-closed status classifier.");
requireText("components/iip/CopilotPanel.tsx", "isProviderHealthy", "the evidence rail must use the shared fail-closed status classifier.");
requireText("lib/providerHealth.ts", "return \"warning\";", "unknown provider states must fail closed as warnings.");

requireText("lib/lovable-parity/data/commandCenterQuery.ts", "/api/v1/command-center/query", "Command Center must use the versioned governed typed-block query endpoint.");
requireText("components/iip/CommandCenterAnswer.tsx", "case \"not_configured\"", "Command Center must render not-configured blocks explicitly.");
requireText("components/iip/CommandCenterAnswer.tsx", "evidence_list", "Command Center must render evidence-list blocks.");
rejectPattern("lib/lovable-parity/data/commandCenterQuery.ts", /\/api\/assistant\/query/, "Command Center must not use the removed multi-agent assistant query endpoint.");

requireText("lovable-pages/FNOL.tsx", "/api/v1/cases", "new claim intake must use the governed case creation endpoint.");
rejectPattern("lovable-pages/FNOL.tsx", /createdByUserId\s*:|tenantId\s*:|workspaceId\s*:/, "new claim intake must not send browser-owned creator or tenant/workspace authority.");
requireText("lib/lovable-parity/data/claims.ts", "/api/v1/claims", "claims queue must load from the governed claims endpoint.");
requireText("lib/lovable-parity/data/claims.ts", "/api/v1/cases/${caseId}/assign", "claims assignment must use the governed backend endpoint.");
requireText("lib/lovable-parity/data/claims.ts", "/api/v1/cases/${caseId}/escalate", "claims escalation must use the governed backend endpoint.");
rejectPattern("lib/lovable-parity/data/claims.ts", /tenantId\s*:|workspaceId\s*:|createdByUserId\s*:/, "claims queue actions must not send browser-owned tenant/workspace/creator authority.");

requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/claims/${claimId}", "claim review must load claim details from the governed endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "LOVABLE_VISUAL_PARITY_MODE", "development parity claim rows must open a matching parity detail without calling the live-only endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "syntheticWorkspaceSnapshot.claims.find", "parity claim detail must resolve the selected shared-snapshot claim rather than substituting another record.");
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/cases/${claimId}/timeline", "claim review must load timeline from the governed endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/cases/${claimId}/customer-updates/draft", "customer-update drafting must use the governed backend endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/customer-updates/${updateId}/approve", "customer-update approval must use the governed backend endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/customer-updates/${update.updateId}/send", "customer-update send must use the governed backend endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/compliance/cases/${claimId}/audit-pack/export", "evidence-pack export must use the governed backend endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "\"X-Audit-Reason\": auditReason", "governed claim-review mutations must include an audit reason header.");
rejectPattern("lib/lovable-parity/data/claimReview.ts", /JSON\.stringify\(\{[\s\S]*?(requestedByUserId|completedByUserId|createdByUserId|reviewerUserId|tenantId|workspaceId)\s*:/, "claim-review actions must not send browser-owned actor or tenant/workspace authority.");

requireText("lib/lovable-parity/data/approvals.ts", "/api/v1/approvals", "approval queue must use the governed approvals endpoint.");
requireText("lib/lovable-parity/data/approvals.ts", "\"X-Audit-Reason\"", "approval decisions must send governed audit reasons.");
rejectPattern("lib/lovable-parity/data/approvals.ts", /JSON\.stringify\(\{[\s\S]*?(reviewerUserId|tenantId|workspaceId)\s*:/, "approval decisions must not send browser-owned reviewer or tenant/workspace authority.");

requireText("lib/lovable-parity/data/channels.ts", "/api/v1/channels/overview", "Channels must read from the tenant-scoped channels endpoint.");
requireText("lovable-pages/Channels.tsx", "Not configured", "unimplemented channel write actions must render not-configured states.");

requireText("lib/lovable-parity/data/knowledge.ts", "/api/v1/knowledge-documents", "Knowledge Hub must read from the governed knowledge endpoint.");
requireText("lovable-pages/Knowledge.tsx", "approved sources", "Knowledge Hub must state approved-source grounding.");
rejectPattern("lib/lovable-parity/data/knowledge.ts", /uploadedByUserId\s*:|tenantId\s*:|workspaceId\s*:/, "knowledge requests must not send browser-owned uploader or tenant/workspace authority.");

requireText("lib/lovable-parity/data/approvals.ts", "execute-next-step", "approved workflow next steps must execute through the backend.");
requireText("lib/providerHealth.ts", "not configured", "communication/provider utilities must preserve honest not-configured states.");
requireText("lib/uiLoadState.ts", "export type UiLoadState<T>", "shared UI load states must distinguish loading, success, empty, authority, configuration, offline, retryable, and terminal outcomes.");
requireText("lib/uiLoadState.ts", "export class UiServiceError", "service failures must use a safe structured UI error model.");
requireText("lovable-pages/UniversalMyWork.tsx", 'value={error ? "—"', "My Work summaries must not render numeric zero when the request failed.");
requireText("next.config.ts", 'phase === PHASE_DEVELOPMENT_SERVER ? ".next-dev" : ".next"', "development and production servers must use isolated Next.js output directories.");
requireText("package.json", '"dev": "node scripts/run-next-dev.mjs"', "all development servers must use the shared compiler lock.");
requireText("package.json", '"build": "node scripts/run-next-build.mjs"', "all production builds must use fail-closed configuration validation.");
requireText("next.config.ts", "NEXT_PUBLIC_RUNTIME_PROFILE and NEXT_PUBLIC_AUTH_MODE are required", "production builds must fail closed when runtime authority configuration is missing.");
requireText("scripts/run-runtime-recovery-production.mjs", 'NEXT_PUBLIC_API_BASE_URL: "http://127.0.0.1:5167"', "the production recovery gate must set its API origin explicitly at build time.");
requireText("lib/lovable-parity/syntheticWorkspaceSnapshot.ts", "satisfies SyntheticWorkspaceSnapshot", "development parity data must declare one typed synthetic workspace snapshot.");
requireText("lib/lovable-parity/data/reports.ts", "syntheticWorkspaceSnapshot.operationsSummary", "reports parity metrics must consume the shared synthetic snapshot.");
requireText("lib/lovable-parity/data/claims.ts", "syntheticWorkspaceSnapshot.claims", "claims parity rows must consume the shared synthetic snapshot.");
requireText("lib/lovable-parity/data/providerHealth.ts", "syntheticWorkspaceSnapshot.providerHealth.providers", "provider parity status must consume the shared synthetic snapshot.");
requireText("lib/lovable-parity/syntheticCommandCenterAnswer.ts", "snapshot.operationsSummary.openClaims", "Command Center parity metrics must consume the shared synthetic snapshot.");
rejectPattern("lib/lovable-parity/data/reports.ts", /openClaims:\s*148/, "reports must not retain contradictory standalone parity metrics.");
rejectPattern("lib/lovable-parity/data/commandCenterQuery.ts", /Open claims[\s\S]*value:\s*"24"/, "Command Center must not retain contradictory standalone parity metrics.");
requirePattern("next.config.ts", /outputFileTracingExcludes[\s\S]*node_modules\/sharp\/\*\*\//, "standalone builds must exclude unused native image optimization packages.");

if (findings.length > 0) {
  console.error("UI release gate check failed:");
  for (const finding of findings) {
    console.error(`- ${finding}`);
  }
  process.exit(1);
}

console.log("UI release gate check passed.");
