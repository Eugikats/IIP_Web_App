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
requireText("components/iip/AppShell.tsx", "canAccess(roleDef.key, loc.pathname)", "must guard /app routes through the role access config.");
requireText("components/iip/AppShell.tsx", "<Navigate to={roleDef.landing} replace />", "must redirect denied routes to the role landing route.");
requireText("components/iip/AppShell.tsx", "<Sidebar />", "must render role-aware left navigation.");
requireText("components/iip/AppShell.tsx", "<CopilotPanel />", "must render the right evidence/context rail.");
requireText("components/iip/AppShell.tsx", "<StatusBar />", "must render the environment/status footer.");
requireText("components/iip/AppShell.tsx", "!runtimeContext", "must withhold protected shell content until /api/v1/me resolves.");
requireText("components/iip/AppShell.tsx", "Verifying your access", "must render a bounded authentication loading state.");
requireText("lovable-pages/Login.tsx", "runtimeProfile", "login environment labeling must come from runtime configuration.");
requireText("lovable-pages/Login.tsx", "Microsoft Entra ID", "login must identify the configured organization identity provider.");
rejectPattern("lovable-pages/Login.tsx", /Authority tenant|Resolved after sign-in|href="#"+|glass-elevated|err instanceof Error/, "login must not expose implementation details, dead links, prototype card composition, or raw auth errors.");

requireText("lib/runtimeContext.tsx", "/api/v1/me", "runtime context must come from the governed /api/v1/me endpoint.");
requireText("lib/runtimeContext.tsx", "LovableVisualParity", "visual parity identity must stay explicitly marked when the dev fixture mode is enabled.");
rejectPattern("lib/runtimeContext.tsx", /headers:\s*\{[\s\S]*?(X-Tenant-Id|X-Workspace-Id|X-User-Id)/, "runtime context fetch must not send browser-owned authority headers.");

requireText("components/iip/Sidebar.tsx", "group.items.filter((item) => roleDef.modules.includes(item.to))", "grouped module navigation must be role-filtered.");
requireText("components/iip/Sidebar.tsx", "roleDef.admin.includes(m.to)", "admin navigation must be role-filtered.");
requireText("components/iip/Sidebar.tsx", "runtimeContext?.availableWorkspaces", "workspace label must use backend runtime context.");
requireText("components/iip/Sidebar.tsx", "Workspace not configured", "missing workspace must render an honest state.");

requireText("components/iip/CopilotPanel.tsx", "useProviderHealth", "evidence rail must render backend-owned provider health.");
requireText("components/iip/CopilotPanel.tsx", "Not configured", "provider gaps must render an honest not-configured state.");
requireText("lib/lovable-parity/data/providerHealth.ts", "/api/v1/admin/provider-health", "provider health must come from the governed backend endpoint.");
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
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/cases/${claimId}/timeline", "claim review must load timeline from the governed endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/cases/${claimId}/customer-updates/draft", "customer-update drafting must use the governed backend endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/customer-updates/${updateId}/approve", "customer-update approval must use the governed backend endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/customer-updates/${update.updateId}/send", "customer-update send must use the governed backend endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "/api/v1/compliance/cases/${claimId}/audit-pack/export", "evidence-pack export must use the governed backend endpoint.");
requireText("lib/lovable-parity/data/claimReview.ts", "\"X-Audit-Reason\": auditReason", "governed claim-review mutations must include an audit reason header.");
rejectPattern("lib/lovable-parity/data/claimReview.ts", /requestedByUserId\s*:|completedByUserId\s*:|createdByUserId\s*:|reviewerUserId\s*:|tenantId\s*:|workspaceId\s*:/, "claim-review actions must not send browser-owned actor or tenant/workspace authority.");

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
requirePattern("next.config.ts", /outputFileTracingExcludes[\s\S]*node_modules\/sharp\/\*\*\//, "standalone builds must exclude unused native image optimization packages.");

if (findings.length > 0) {
  console.error("UI release gate check failed:");
  for (const finding of findings) {
    console.error(`- ${finding}`);
  }
  process.exit(1);
}

console.log("UI release gate check passed.");
