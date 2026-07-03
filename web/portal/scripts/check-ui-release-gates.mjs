import { readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.cwd();
const findings = [];

function read(path) {
  return readFileSync(join(root, path), "utf8");
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

requireText("components/AppShell.tsx", "<RouteAccessGuard>{children}</RouteAccessGuard>", "must wrap app content in RouteAccessGuard.");
requireText("components/AppShell.tsx", "<ProviderStatusStrip />", "must render backend-owned provider status in the shell.");
requireText("components/AppShell.tsx", "<MobileNav />", "must keep mobile role-aware navigation available.");

requireText("components/ProviderStatusStrip.tsx", "/api/v1/provider-status", "must fetch provider status from the backend.");
requireText("components/ProviderStatusStrip.tsx", "can(\"ViewCommandCenter\")", "must hide provider status from unauthorized roles.");
requireText("components/ProviderStatusStrip.tsx", "aria-label={`${item.label}: ${statusLabel(status)}`}", "must expose accessible provider status labels.");
requireText("app/integrations/PortalIntegrationsClient.tsx", "/api/v1/integrations/email/health", "must fetch backend-owned email integration action state.");
requireText("app/integrations/PortalIntegrationsClient.tsx", "setPendingIntegrationAction({ type: \"testIngestion\" })", "test ingestion must use governed confirmation before mutation.");
requireText("app/integrations/PortalIntegrationsClient.tsx", "\"X-Audit-Reason\": auditReason", "integration mutations must send the governed audit reason.");
rejectPattern("app/integrations/PortalIntegrationsClient.tsx", /defaultWorkspaceId\s*:/, "mailbox configuration must not send browser-owned workspace identity.");
rejectPattern("app/integrations/PortalIntegrationsClient.tsx", /processedByUserId\s*:/, "email processing must not send browser-owned actor identity.");
requireText("app/document-checker/DocumentCheckerClient.tsx", "/correct-field", "document corrections must use the governed backend endpoint.");
requireText("app/document-checker/DocumentCheckerClient.tsx", "document.previewUrl", "document preview must use the authorized backend content route.");
rejectPattern("app/document-checker/DocumentCheckerClient.tsx", /\bobjectKey\b/, "document workbench must not expose internal storage object keys.");
requireText("app/claims/ClaimsInboxClient.tsx", "payload.every(hasActionState)", "claims inbox must require backend action authorization state.");
requireText("app/cases/[caseId]/CaseDetailClient.tsx", "hasActionState(casePayload)", "case review must require backend action authorization state.");
requireText("app/cases/new/NewCaseForm.tsx", "hasActionState(responsePayload)", "new claim intake must require backend action authorization state.");
requireText("app/knowledge-base/KnowledgeBaseClient.tsx", "payload.every(hasActionState)", "knowledge hub must require backend lifecycle action state.");
requireText("app/admin-settings/AdminSettingsClient.tsx", "usersPayload.every(hasActionState)", "admin role controls must require backend role-change authorization state.");
requireText("app/approvals/ApprovalsClient.tsx", "payload.every(hasActionState)", "approval decisions must require backend authorization state.");
rejectPattern("app/approvals/ApprovalsClient.tsx", /JSON\.stringify\(\{\s*reviewerUserId\s*:/, "approval decisions must not send browser-owned reviewer identity.");
rejectPattern("app/cases/[caseId]/CaseDetailClient.tsx", /requestedByUserId\s*:/, "case workflow actions must not send browser-owned requester identity.");
rejectPattern("app/cases/[caseId]/CaseDetailClient.tsx", /completedByUserId\s*:/, "workflow task completion must not send browser-owned actor identity.");
requireText("app/customer-service/CustomerServiceClient.tsx", "updatesPayload.every(hasActionState)", "customer update controls must require backend action authorization state.");
rejectPattern("app/customer-service/CustomerServiceClient.tsx", /createdByUserId\s*:/, "customer update drafting must not send browser-owned creator identity.");
rejectPattern("app/customer-service/CustomerServiceClient.tsx", /JSON\.stringify\(\{\s*userId\s*:/, "customer update decisions must not send browser-owned actor identity.");
rejectPattern("components/CustomerUpdatePanel.tsx", /update\.status\s*===/, "customer update actions must not infer authorization from frontend status.");
rejectPattern("app/knowledge-base/KnowledgeBaseClient.tsx", /uploadedByUserId/, "knowledge upload must not send browser-owned uploader identity.");
rejectPattern("app/knowledge-base/KnowledgeBaseClient.tsx", /\bobjectKey\b/, "knowledge hub must not expose internal storage object keys.");
rejectPattern("app/cases/new/NewCaseForm.tsx", /createdByUserId\s*:/, "new claim intake must not send browser-owned creator identity.");
rejectPattern("app/claims/ClaimsInboxClient.tsx", /return action === ["']openCaseReview["']/, "claims inbox must not infer action authority in the frontend.");
rejectPattern("app/cases/[caseId]/CaseDetailClient.tsx", /caseDetail\.status !== ["']Closed["']/, "case review must not infer action authority from status.");
requireText("components/AssistantChat.tsx", "Prepared by {response.modelProvider}", "assistant answers must render backend model provenance.");
requireText("components/AssistantChat.tsx", "Preparation only", "assistant answers must state that they cannot trigger decisions.");
requireText("components/AssistantChat.tsx", "response.guardrailMessage", "assistant answers must render backend guardrail guidance.");
requireText("components/AssistantChat.tsx", "value.canTriggerDecision !== false", "assistant responses must fail closed unless the backend marks them as non-decision output.");
requireText("components/AssistantChat.tsx", "aria-label=\"Approved sources\"", "assistant answers must render approved source citations.");
rejectPattern("components/AssistantChat.tsx", /\/api\/assistant\/query/, "runtime chat must not use the multi-agent query endpoint until its tenant, visibility, provenance, and audit contracts are complete.");
requireText("hooks/useCommandCenterQuery.tsx", "/api/v1/command-center/query", "Command Center chat must use the versioned governed typed-block query endpoint.");
requireText("hooks/useCommandCenterQuery.tsx", "parseCommandCenterResponse", "Command Center query responses must pass through the strict typed-block parser.");
rejectPattern("app/CommandCenterClient.tsx", /\/api\/assistant\/query/, "Command Center must not bypass the governed operational assistant endpoint.");
requireText("app/ai-assistant/AiAssistantClient.tsx", "/api/v1/assistant/policy-questions", "AI Conversations must use the grounded policy-question endpoint.");
rejectPattern("app/ai-assistant/AiAssistantClient.tsx", /setAudience|<select[\s\S]*?Customer/, "staff AI Conversations must not let the browser select a customer-safe response audience.");
requireText("app/policy-intelligence/PolicyIntelligenceClient.tsx", "/api/v1/assistant/policy-questions", "Policy Intelligence must use the grounded policy-question endpoint.");
requireText("app/policy-intelligence/PolicyIntelligenceClient.tsx", "response.coverageState", "Policy Intelligence must render backend-owned coverage state.");
requireText("app/policy-intelligence/PolicyIntelligenceClient.tsx", "response.sources.map", "Policy Intelligence must render approved source citations.");
requireText("app/policy-intelligence/PolicyIntelligenceClient.tsx", "response.canTriggerDecision === false", "Policy Intelligence must reject responses that could trigger decisions.");
rejectPattern("app/policy-intelligence/PolicyIntelligenceClient.tsx", /tenantId\s*:/, "Policy Intelligence must not send browser-owned tenant authority.");
rejectPattern("app/policy-intelligence/PolicyIntelligenceClient.tsx", /workspaceId\s*:/, "Policy Intelligence must not send browser-owned workspace authority.");
rejectPattern("app/policy-intelligence/PolicyIntelligenceClient.tsx", /userId\s*:/, "Policy Intelligence must not send browser-owned user authority.");
requireText("app/tenant-onboarding/TenantOnboardingClient.tsx", "/api/v1/admin/tenant-configuration", "tenant onboarding must use the governed tenant configuration endpoint.");
requireText("app/tenant-onboarding/TenantOnboardingClient.tsx", "\"X-Audit-Reason\": auditReason", "tenant onboarding changes must include the governed audit reason.");
requireText("app/tenant-onboarding/TenantOnboardingClient.tsx", "approvalThresholds: currentConfiguration.approvalThresholds", "tenant onboarding must preserve backend approval thresholds when saving profile changes.");
requireText("app/tenant-onboarding/TenantOnboardingClient.tsx", "checklists: currentConfiguration.checklists", "tenant onboarding must preserve backend document checklists when saving profile changes.");
rejectPattern("app/tenant-onboarding/TenantOnboardingClient.tsx", /tenantId\s*:/, "tenant onboarding must not send browser-owned tenant authority.");
rejectPattern("app/tenant-onboarding/TenantOnboardingClient.tsx", /workspaceId\s*:/, "tenant onboarding must not send browser-owned workspace authority.");
rejectPattern("app/tenant-onboarding/TenantOnboardingClient.tsx", /body:\s*JSON\.stringify\(\{[\s\S]*?\buserId\s*:/, "tenant onboarding must not send browser-owned user authority.");
rejectPattern("app/tenant-onboarding/TenantOnboardingClient.tsx", /providerModes\s*:/, "tenant onboarding must not mutate provider modes through the tenant profile request.");

requireText("app/client-portal/ClientPortalClient.tsx", "/api/v1/client-portal/claims/", "client portal must use dedicated customer-safe API routes.");
requireText("app/client-portal/ClientPortalClient.tsx", "aria-label=\"Open customer claim\"", "claim lookup form must have an accessible name.");
requireText("app/client-portal/ClientPortalClient.tsx", "role=\"status\"", "customer portal status messages must announce updates.");
requireText("app/client-portal/ClientPortalClient.tsx", "Customer assistant endpoint not configured yet", "customer assistant must remain not configured until a safe endpoint exists.");
requireText("app/client-portal/ClientPortalClient.tsx", "aria-label=\"Upload claim documents\"", "document upload input must have an accessible name.");

rejectPattern(
  "app/client-portal/ClientPortalClient.tsx",
  /\/api\/(?:v1\/)?(cases|claims|admin|command-center|provider-status|audit|integrations)\b/,
  "client portal must not call staff, provider, audit, integration, or command-center APIs."
);

requireText("components/MobileNav.tsx", "aria-label={item.label}", "mobile navigation links must expose full accessible labels.");
requireText("next.config.ts", "outputFileTracingExcludes", "standalone builds must exclude unused native image optimization packages.");
requireText("next.config.ts", "node_modules/sharp/**/*", "standalone builds must exclude unused Sharp packages.");

if (findings.length > 0) {
  console.error("UI release gate check failed:");
  for (const finding of findings) {
    console.error(`- ${finding}`);
  }
  process.exit(1);
}

console.log("UI release gate check passed.");
