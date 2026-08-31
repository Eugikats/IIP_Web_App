import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const rel = {
  master: "docs/03-design-architecture/iip_end_to_end_user_flow_architecture.md",
  journeys: "docs/03-design-architecture/iip_release_1_role_journey_maps.md",
  blueprint: "docs/03-design-architecture/iip_release_1_intelligence_layer_service_blueprint.md",
  catalog: "docs/03-design-architecture/iip_release_1_task_flow_exception_state_catalog.md",
  interactions: "docs/03-design-architecture/iip_release_1_interaction_authority_and_inventory.md",
  authority: "docs/03-design-architecture/iip_release_1_action_purpose_human_ai_authority_contracts.md",
  surfaces: "docs/03-design-architecture/iip_release_1_surface_control_interaction_contracts.md",
  recovery: "docs/03-design-architecture/iip_release_1_state_exception_recovery_contracts.md",
  lowFi: "docs/03-design-architecture/iip_release_1_low_fidelity_wireframe_state_inventory.md",
  responsive: "docs/03-design-architecture/iip_release_1_responsive_adaptive_reflow_contract.md",
  complexPatterns: "docs/03-design-architecture/iip_release_1_complex_operational_pattern_contracts.md",
  accessibility: "docs/03-design-architecture/iip_release_1_interaction_accessibility_and_runtime_parity.md",
  accessibilityEvidence: "docs/07-qa/release-1-interaction-accessibility-and-runtime-parity-verification-2026-08-08.md",
  interactionTrace: "docs/01-strategy-requirements-roadmap/iip_release_1_interaction_requirements_traceability.md",
  ia: "docs/03-design-architecture/iip_information_architecture_navigation_and_screen_inventory.md",
  baseline: "docs/01-strategy-requirements-roadmap/iip_release_1_requirements_baseline.md",
  acceptance: "docs/01-strategy-requirements-roadmap/iip_mvp_acceptance_criteria_register.md",
  rtm: "docs/01-strategy-requirements-roadmap/iip_requirements_traceability_matrix.md",
  uat: "docs/07-qa/uat-script.md",
};

const failures = [];
const docs = Object.fromEntries(Object.entries(rel).map(([key, value]) => {
  const fullPath = path.join(repoRoot, value);
  if (!fs.existsSync(fullPath)) failures.push(`Missing artifact: ${value}`);
  return [key, fs.existsSync(fullPath) ? fs.readFileSync(fullPath, "utf8") : ""];
}));

function tableIds(text, pattern) {
  return [...text.matchAll(/^\|\s*`?([^|`]+)`?\s*\|/gm)]
    .map((match) => match[1].trim())
    .filter((id) => pattern.test(id));
}

function assertUnique(label, ids) {
  const seen = new Set();
  const duplicates = new Set();
  for (const id of ids) (seen.has(id) ? duplicates : seen).add(id);
  if (duplicates.size) failures.push(`${label} duplicate IDs: ${[...duplicates].join(", ")}`);
}

function section(text, start, end) {
  const startIndex = text.indexOf(start);
  const endIndex = end ? text.indexOf(end, startIndex + start.length) : text.length;
  if (startIndex < 0 || endIndex < 0) return "";
  return text.slice(startIndex, endIndex);
}

const baselineIds = tableIds(docs.baseline, /^R1-(?:BUS|FUN|WA|AI|DAT|SEC|PRI|UX|A11Y|L10N|PERF|RES|OPS|REL)-\d{3}$/);
const rtmIds = tableIds(docs.rtm, /^R1-(?:BUS|FUN|WA|AI|DAT|SEC|PRI|UX|A11Y|L10N|PERF|RES|OPS|REL)-\d{3}$/);
assertUnique("Baseline", baselineIds);
assertUnique("RTM", rtmIds);

const baselineSet = new Set(baselineIds);
const rtmSet = new Set(rtmIds);
const missingRtm = [...baselineSet].filter((id) => !rtmSet.has(id));
const extraRtm = [...rtmSet].filter((id) => !baselineSet.has(id));
if (missingRtm.length) failures.push(`RTM missing requirements: ${missingRtm.join(", ")}`);
if (extraRtm.length) failures.push(`RTM has non-baseline requirements: ${extraRtm.join(", ")}`);
if (!docs.rtm.includes(`${baselineIds.length} expected`)) failures.push(`RTM coverage metric must state ${baselineIds.length} expected`);

const flowRegistry = section(docs.catalog, "## 2. Canonical flow registry", "## 3. Detailed task transitions");
const flowIds = tableIds(flowRegistry, /^FLW-R1-[A-Z]+-\d{2}$/);
assertUnique("Flow registry", flowIds);
if (flowIds.length !== 10) failures.push(`Expected 10 canonical flows, found ${flowIds.length}`);
for (const id of flowIds) {
  if (!docs.master.includes(`\`${id}\``)) failures.push(`Master flow registry missing ${id}`);
  if (!docs.catalog.includes(`### \`${id}\``)) failures.push(`Detailed task transition missing ${id}`);
}

const requiredEvidenceConditions = [
  "Missing", "Unverified", "Low-confidence", "Conflicting/contradicted", "Stale",
  "Superseded", "Unauthorized", "Unsupported language", "Duplicate", "Corrupt/unsafe",
];
for (const condition of requiredEvidenceConditions) {
  if (!docs.catalog.includes(`| ${condition} |`)) failures.push(`Evidence decision table missing ${condition}`);
}

const stateCodes = [
  "LOADING", "EMPTY", "POPULATED", "PARTIAL", "STALE", "CONFLICT", "UNAUTHORIZED",
  "RESTRICTED", "NOT-CONFIGURED", "UNAVAILABLE", "TIMEOUT", "RETRYING", "FAILED",
  "SUCCESS", "INTERRUPTED", "REAUTHENTICATED", "RECOVERY", "HIGH-VOLUME",
];
for (const code of stateCodes) {
  if (!docs.catalog.includes(`| \`${code}\` |`)) failures.push(`UI state registry missing ${code}`);
}

const interactionRegistry = section(docs.interactions, "## 5. Critical interaction inventory", "## 6. Cross-cutting interaction decisions");
const interactionIds = tableIds(interactionRegistry, /^ACT-R1-[A-Z]+-\d{2}$/);
assertUnique("Interaction inventory", interactionIds);
if (interactionIds.length !== 20) failures.push(`Expected 20 critical interactions, found ${interactionIds.length}`);
for (const id of interactionIds) {
  if (!interactionRegistry.includes(`| \`${id}\` |`)) failures.push(`Interaction inventory missing row ${id}`);
  if (!docs.authority.includes(`| \`${id}\` |`)) failures.push(`Purpose/human-AI authority contract missing ${id}`);
  if (!docs.surfaces.includes(`| \`${id}\` |`)) failures.push(`Surface/control coverage missing ${id}`);
  if (!docs.recovery.includes(`| \`${id}\` |`)) failures.push(`Action recovery coverage missing ${id}`);
  if (!docs.accessibility.includes(`| \`${id}\` |`)) failures.push(`Interaction accessibility coverage missing ${id}`);
  if (!docs.interactionTrace.includes(`| \`${id}\` |`)) failures.push(`Interaction requirements traceability missing ${id}`);
}
const interactionTraceIds = tableIds(docs.interactionTrace, /^ACT-R1-[A-Z]+-\d{2}$/);
assertUnique("Interaction requirements traceability", interactionTraceIds);
if (interactionTraceIds.length !== interactionIds.length) failures.push(`Expected ${interactionIds.length} interaction traceability rows, found ${interactionTraceIds.length}`);
for (const column of ["Requirements", "Acceptance", "Current permission and enforcement", "Current audit action or gap", "Flow, screen, region/state", "API/implementation seam", "Test/UAT target", "Readiness/gaps"]) {
  if (!docs.interactionTrace.includes(column)) failures.push(`Interaction traceability missing column ${column}`);
}
for (const classification of ["Evidence", "Assumption", "Constraint", "Decision"]) {
  if (!docs.interactions.includes(`| ${classification} |`)) failures.push(`Interaction ledger missing ${classification} classification`);
}
for (const dimension of ["Design", "API/Application", "UI control", "Local evidence", "Protected evidence"]) {
  if (!docs.interactions.includes(`| ${dimension} |`)) failures.push(`Interaction parity model missing ${dimension}`);
}
for (const heading of ["Action purpose", "User intent", "Required feedback", "Authority", "Attention demand", "Human/AI boundary"]) {
  if (!docs.authority.includes(heading)) failures.push(`Purpose/human-AI authority contracts missing ${heading}`);
}
for (const authorityClass of ["AUT-0 Observe", "AUT-1 Prepare", "AUT-2 Route", "AUT-3 Human commit", "AUT-4 External/consequential", "AUT-5 Safety control"]) {
  if (!docs.authority.includes(`\`${authorityClass}\``)) failures.push(`Authority model missing ${authorityClass}`);
}
for (const attentionLevel of ["ATT-0 Ambient", "ATT-1 Routine", "ATT-2 Focused review", "ATT-3 Deliberate decision", "ATT-4 Incident"]) {
  if (!docs.authority.includes(`\`${attentionLevel}\``)) failures.push(`Attention model missing ${attentionLevel}`);
}
const controlStates = ["default", "hover", "focus", "pressed", "disabled", "loading", "success", "error", "retry", "reduced-motion"];
for (const controlState of controlStates) {
  if (!docs.surfaces.includes(`| \`${controlState}\` |`)) failures.push(`Control-state contract missing ${controlState}`);
}
const surfaceIds = ["SUR-R1-CUES", "SUR-R1-EVIDENCE", "SUR-R1-APPROVAL", "SUR-R1-DIALOG", "SUR-R1-PANEL", "SUR-R1-TABLE", "SUR-R1-STATUS"];
for (const surfaceId of surfaceIds) {
  if (!docs.surfaces.includes(`\`${surfaceId}\``)) failures.push(`Surface registry missing ${surfaceId}`);
}
const surfaceControlGapCount = 8;
for (let index = 1; index <= surfaceControlGapCount; index += 1) {
  const id = `SUR-GAP-${String(index).padStart(2, "0")}`;
  if (!docs.surfaces.includes(`\`${id}\``)) failures.push(`Surface/control gap register missing ${id}`);
}
const requiredRecoveryStates = ["LOADING", "EMPTY", "PARTIAL", "FAILED", "UNAUTHORIZED", "RESTRICTED", "UNAVAILABLE", "NOT-CONFIGURED", "SUCCESS", "RECOVERY"];
for (const recoveryState of requiredRecoveryStates) {
  if (!docs.recovery.includes(`\`${recoveryState}\``)) failures.push(`Recovery contract missing required state ${recoveryState}`);
}
const regionIds = tableIds(docs.recovery, /^REG-R1-[A-Z]+-[A-Z]+$/);
assertUnique("Region inventory", regionIds);
if (regionIds.length !== 20) failures.push(`Expected 20 independently changing regions, found ${regionIds.length}`);
for (const topic of ["Confirmation policy", "Cancellation, undo, rollback, and compensation", "Duplicate prevention and idempotence", "Interruption, session expiry, and resume", "Stale data and conflict"]) {
  if (!docs.recovery.includes(topic)) failures.push(`Recovery contract missing topic ${topic}`);
}
const recoveryGapCount = 8;
for (let index = 1; index <= recoveryGapCount; index += 1) {
  const id = `REC-GAP-${String(index).padStart(2, "0")}`;
  if (!docs.recovery.includes(`\`${id}\``)) failures.push(`Recovery gap register missing ${id}`);
}
for (let index = 1; index <= 9; index += 1) {
  const id = `INT-GAP-${String(index).padStart(2, "0")}`;
  if (!docs.interactions.includes(`\`${id}\``)) failures.push(`Interaction gap register missing ${id}`);
}
for (const concern of ["Keyboard", "Focus entry/return", "Touch/pointer", "Name, role, state", "Announcement", "Contrast and non-color", "Reduced motion", "Zoom/reflow", "Timeout/recovery"]) {
  if (!docs.accessibility.includes(`| ${concern} |`)) failures.push(`Universal interaction accessibility contract missing ${concern}`);
}
for (const id of ["CP-R1-QUEUE", "CP-R1-EVIDENCE", "CP-R1-APPROVAL", "CP-R1-TABLE", "CP-R1-PANEL", "CP-R1-DIALOG"]) {
  if (!docs.accessibility.includes(`| \`${id}\` |`)) failures.push(`Accessibility application matrix missing complex pattern ${id}`);
}
for (const state of ["LOADING", "EMPTY", "ERROR", "PERMISSION", "PARTIAL", "SUCCESS", "RECOVERY"]) {
  if (!docs.accessibility.includes(`| \`${state}\` |`)) failures.push(`Accessibility seven-state application missing ${state}`);
}
for (const criterion of ["2.1.1 Keyboard", "2.1.2 No Keyboard Trap", "2.4.3 Focus Order", "2.4.7 Focus Visible", "2.4.11 Focus Not Obscured", "4.1.2 Name, Role, Value", "4.1.3 Status Messages", "2.5.8 Target Size", "2.3.3 Animation from Interactions"]) {
  if (!docs.accessibility.includes(criterion)) failures.push(`Accessibility criteria mapping missing ${criterion}`);
}
for (const evidenceLayer of ["Design evidence", "Implementation evidence", "User evidence"]) {
  if (!docs.accessibility.includes(`| ${evidenceLayer} |`)) failures.push(`Accessibility review limits missing ${evidenceLayer}`);
}
if (!docs.accessibility.includes("https://www.w3.org/TR/WCAG22/")) failures.push("Accessibility contract missing WCAG 2.2 normative baseline link");
if (!docs.accessibility.includes("https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/")) failures.push("Accessibility contract missing modal-dialog pattern source");
for (const runtimeConcern of ["Current client seam", "API/Application authority", "Provider/external effect", "Concurrency", "Audit", "Idempotency/duplicate control"]) {
  if (!docs.accessibility.includes(runtimeConcern)) failures.push(`Client/runtime reconciliation missing ${runtimeConcern}`);
}
for (let index = 1; index <= 8; index += 1) {
  const id = `A11Y-GAP-${String(index).padStart(2, "0")}`;
  if (!docs.accessibility.includes(`\`${id}\``)) failures.push(`Accessibility gap register missing ${id}`);
}
for (let index = 1; index <= 3; index += 1) {
  const id = `PAR-GAP-${String(index).padStart(2, "0")}`;
  if (!docs.accessibility.includes(`\`${id}\``)) failures.push(`Runtime parity gap register missing ${id}`);
}

const iaScreenIds = tableIds(docs.ia, /^IA-SCR-\d{3}$/);
const lowFiScreenSection = section(docs.lowFi, "## 4. Screen inventory matrix", "## 5. Family-level state transitions");
const lowFiScreenIds = tableIds(lowFiScreenSection, /^IA-SCR-\d{3}$/);
assertUnique("IA screen inventory", iaScreenIds);
assertUnique("Low-fidelity screen inventory", lowFiScreenIds);
for (const id of iaScreenIds) {
  if (!lowFiScreenIds.includes(id)) failures.push(`Low-fidelity state inventory missing ${id}`);
  const row = lowFiScreenSection.split("\n").find((line) => line.startsWith(`| \`${id}\` |`)) ?? "";
  if (!row.includes("`ALL-7`")) failures.push(`Low-fidelity screen ${id} does not require ALL-7 states`);
}
for (const id of lowFiScreenIds) {
  if (!iaScreenIds.includes(id)) failures.push(`Low-fidelity state inventory references unknown screen ${id}`);
}
for (const state of ["LOADING", "EMPTY", "ERROR", "PERMISSION", "PARTIAL", "SUCCESS", "RECOVERY"]) {
  if (!docs.lowFi.includes(`| \`${state}\` |`)) failures.push(`Low-fidelity seven-state contract missing ${state}`);
}
for (let index = 1; index <= 5; index += 1) {
  const id = `LFW-GAP-${String(index).padStart(2, "0")}`;
  if (!docs.lowFi.includes(`\`${id}\``)) failures.push(`Low-fidelity gap register missing ${id}`);
}
for (const context of ["RSP-R1-COMPACT", "RSP-R1-INTERMEDIATE", "RSP-R1-WIDE"]) {
  if (!docs.responsive.includes(`\`${context}\``)) failures.push(`Responsive context matrix missing ${context}`);
}
for (const heading of ["Navigation transformations", "Layout-family transformations", "Complex component transformations", "Input and capability adaptations", "State preservation across reflow", "Per-state reflow rules", "Responsive test scenarios"]) {
  if (!docs.responsive.includes(heading)) failures.push(`Responsive contract missing ${heading}`);
}
for (let index = 1; index <= 12; index += 1) {
  const id = `RSP-TST-${String(index).padStart(2, "0")}`;
  if (!docs.responsive.includes(`\`${id}\``)) failures.push(`Responsive test matrix missing ${id}`);
}
for (let index = 1; index <= 6; index += 1) {
  const id = `RSP-GAP-${String(index).padStart(2, "0")}`;
  if (!docs.responsive.includes(`\`${id}\``)) failures.push(`Responsive gap register missing ${id}`);
}
const complexPatternIds = ["CP-R1-QUEUE", "CP-R1-EVIDENCE", "CP-R1-APPROVAL", "CP-R1-TABLE", "CP-R1-PANEL", "CP-R1-DIALOG"];
for (const id of complexPatternIds) {
  if (!docs.complexPatterns.includes(`\`${id}\``)) failures.push(`Complex operational pattern contract missing ${id}`);
}
for (const heading of ["Shared seven-state coordination", "Cross-pattern handoffs", "Data/content contract", "Accessibility and responsive checklist", "Gaps and validation order"]) {
  if (!docs.complexPatterns.includes(heading)) failures.push(`Complex operational pattern contract missing ${heading}`);
}
const complexPatternTestIds = [
  "CP-TST-QUEUE-01", "CP-TST-QUEUE-02", "CP-TST-QUEUE-03",
  "CP-TST-EVIDENCE-01", "CP-TST-EVIDENCE-02", "CP-TST-EVIDENCE-03",
  "CP-TST-APPROVAL-01", "CP-TST-APPROVAL-02", "CP-TST-APPROVAL-03",
  "CP-TST-TABLE-01", "CP-TST-TABLE-02", "CP-TST-TABLE-03",
  "CP-TST-PANEL-01", "CP-TST-PANEL-02", "CP-TST-PANEL-03",
  "CP-TST-DIALOG-01", "CP-TST-DIALOG-02", "CP-TST-DIALOG-03",
];
for (const id of complexPatternTestIds) {
  if (!docs.complexPatterns.includes(`\`${id}\``)) failures.push(`Complex operational pattern test matrix missing ${id}`);
}
for (let index = 1; index <= 7; index += 1) {
  const id = `CP-GAP-${String(index).padStart(2, "0")}`;
  if (!docs.complexPatterns.includes(`\`${id}\``)) failures.push(`Complex operational pattern gap register missing ${id}`);
}
if (!docs.surfaces.includes("iip_release_1_complex_operational_pattern_contracts.md")) failures.push("Surface/control contract missing complex-pattern companion link");
for (const evidenceTerm of ["Dialog keyboard entry", "Dialog focus containment", "Escape and focus return", "Target size", "Names/labels", "Contrast", "Reduced motion"]) {
  if (!docs.accessibilityEvidence.includes(`| ${evidenceTerm} |`)) failures.push(`Accessibility evidence missing ${evidenceTerm}`);
}

const acceptanceIds = tableIds(docs.acceptance, /^AC-[A-Z0-9]+-\d{2}$/);
const uatIds = tableIds(docs.uat, /^UAT-[A-Z0-9]+-\d{3}$/);
assertUnique("Acceptance", acceptanceIds);
assertUnique("UAT", uatIds);
for (const id of ["AC-FLOW-01", "AC-LFW-01", "AC-RSP-01", "AC-CPLX-01", "AC-EVD-01", "AC-SOD-01", "AC-INT-01", "AC-PRE-01", "AC-ACT-01", "AC-CON-01", "AC-AUD-01", "AC-A11Y-02", "AC-A11Y-03", "AC-A11Y-04", "AC-A11Y-05", "AC-A11Y-06"]) {
  if (!acceptanceIds.includes(id)) failures.push(`Acceptance register missing ${id}`);
}
for (const id of ["R1-FUN-019", "R1-DAT-009", "R1-DAT-010", "R1-DAT-011", "R1-SEC-005", "R1-UX-005", "R1-UX-006", "R1-UX-007", "R1-A11Y-005", "R1-A11Y-006", "R1-A11Y-007", "R1-A11Y-008", "R1-A11Y-009", "R1-REL-004"]) {
  if (!baselineSet.has(id)) failures.push(`Baseline missing derived requirement ${id}`);
}
for (const id of ["UAT-UX-001", "UAT-UX-002", "UAT-A11Y-001", "UAT-A11Y-002"]) {
  if (!uatIds.includes(id)) failures.push(`UAT script missing ${id}`);
}

const traceRequirementIds = new Set(docs.interactionTrace.match(/\bR1-(?:BUS|FUN|WA|AI|DAT|SEC|PRI|UX|A11Y|L10N|PERF|RES|OPS|REL)-\d{3}\b/g) ?? []);
const traceAcceptanceIds = new Set(docs.interactionTrace.match(/\bAC-[A-Z0-9]+-\d{2}\b/g) ?? []);
const traceUatIds = new Set(docs.interactionTrace.match(/\bUAT-[A-Z0-9]+-\d{3}\b/g) ?? []);
const traceFlowIds = new Set(docs.interactionTrace.match(/\bFLW-R1-[A-Z]+-\d{2}\b/g) ?? []);
const traceScreenIds = new Set(docs.interactionTrace.match(/\bIA-SCR-\d{3}\b/g) ?? []);
const traceRegionIds = new Set(docs.interactionTrace.match(/\bREG-R1-[A-Z]+-[A-Z]+\b/g) ?? []);
const traceStateIds = new Set(docs.interactionTrace.match(/\bST-R1-(?:MYWORK|CASE|EVIDENCE|APPROVAL|CHANNEL|PROVIDER)-[A-Z-]+\b/g) ?? []);
for (const id of traceRequirementIds) if (!baselineSet.has(id)) failures.push(`Interaction traceability references unknown requirement ${id}`);
for (const id of traceAcceptanceIds) if (!acceptanceIds.includes(id)) failures.push(`Interaction traceability references unknown acceptance criterion ${id}`);
for (const id of traceUatIds) if (!uatIds.includes(id)) failures.push(`Interaction traceability references unknown UAT scenario ${id}`);
for (const id of traceFlowIds) if (!flowIds.includes(id)) failures.push(`Interaction traceability references unknown flow ${id}`);
for (const id of traceScreenIds) if (!docs.ia.includes(`| ${id} |`)) failures.push(`Interaction traceability references unknown screen ${id}`);
for (const id of traceRegionIds) if (!regionIds.includes(id)) failures.push(`Interaction traceability references unknown region ${id}`);
for (const id of traceStateIds) {
  const code = stateCodes.find((candidate) => id.endsWith(`-${candidate}`));
  if (!code) failures.push(`Interaction traceability references unknown state ${id}`);
}

const expectedGapIds = [
  "PERM-GAP-01", "PERM-GAP-02", "PERM-GAP-03", "PERM-GAP-04", "PERM-GAP-05",
  "AUD-GAP-01", "AUD-GAP-02", "AUD-GAP-03", "AUD-GAP-04", "AUD-GAP-05",
];
for (const id of expectedGapIds) {
  if (!docs.catalog.includes(`\`${id}\``)) failures.push(`Gap register missing ${id}`);
}

const permissionSource = fs.readFileSync(path.join(repoRoot, "src/InsuranceIntelligence.Domain/Administration/Permission.cs"), "utf8");
const roleMapSource = fs.readFileSync(path.join(repoRoot, "src/InsuranceIntelligence.Domain/Administration/RolePermissionMap.cs"), "utf8");
const auditSource = [
  "src/InsuranceIntelligence.Domain/Audit/AuditEvent.cs",
  "src/InsuranceIntelligence.Application/WhatsApp/WhatsAppMessagingService.cs",
  "src/InsuranceIntelligence.Api/Integrations/EmailIntegrationEndpoints.cs",
  "src/InsuranceIntelligence.Application/CommandCenter/CommandCenterConversationService.cs",
  "src/InsuranceIntelligence.Application/Agents/AgentRegistryService.cs",
].map((value) => fs.readFileSync(path.join(repoRoot, value), "utf8")).join("\n");

const expectedPermissions = [
  "ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAllClaims", "ViewAssignedClaims",
  "ApproveSettlementWorkflowStep", "ViewAuditLog", "ViewAiOutputs", "OverrideAiRecommendation",
  "ManageCustomerCommunications", "SendCustomerUpdate", "ViewChannels", "ReviewAgentWorkItems",
  "ManageAgents",
];
for (const permission of expectedPermissions) {
  if (!new RegExp(`\\b${permission}\\b`).test(permissionSource)) failures.push(`Documented permission missing from enum: ${permission}`);
}

const expectedAuditActions = [
  "case.created", "case.assigned", "case.escalated", "document.uploaded", "document.processed",
  "document.reclassified", "document.extraction.approved", "document.extraction.field_corrected",
  "case.completeness.updated", "workflow.evaluated", "workflow.task.created", "workflow.task.completed",
  "workflow.task.assigned", "approval.created", "approval.approved", "approval.rejected", "approval.overridden", "approval.escalated",
  "approval.next_step.executed", "claim.review_requested", "claim.review_acknowledged",
  "claim.review_resolved", "whatsapp.inbound.received", "whatsapp.outbound.attempted",
  "whatsapp.delivery.reconciled", "email.received", "email.case_linked", "email.case_created",
  "conversation.viewed", "conversation.renamed", "conversation.archived", "conversation.restored",
  "customer_update.drafted", "customer_update.approved", "customer_update.rejected", "customer_update.sent",
  "evidence_pack.exported", "agent.killswitch.enabled", "agent.killswitch.disabled",
];
for (const action of expectedAuditActions) {
  if (!auditSource.includes(`"${action}"`)) failures.push(`Documented audit action missing from current source: ${action}`);
}

const complianceMap = section(roleMapSource, "[PlatformRole.Compliance]", "[PlatformRole.Client]");
for (const permission of ["ApproveSettlementWorkflowStep", "OverrideAiRecommendation"]) {
  if (!complianceMap.includes(`Permission.${permission}`)) failures.push(`PERM-GAP-01 no longer matches current Compliance map: ${permission}`);
}
const claimsOfficerMap = section(roleMapSource, "[PlatformRole.ClaimsOfficer]", "[PlatformRole.ClaimsSupervisor]");
const customerServiceMap = section(roleMapSource, "[PlatformRole.CustomerService]", "[PlatformRole.Broker]");
for (const [label, source] of [["ClaimsOfficer", claimsOfficerMap], ["CustomerService", customerServiceMap]]) {
  for (const permission of ["RunAgentWorkflows", "ReviewAgentWorkItems"]) {
    if (!source.includes(`Permission.${permission}`)) failures.push(`PERM-GAP-02 no longer matches current ${label} map: ${permission}`);
  }
}

if (failures.length) {
  console.error(JSON.stringify({ status: "failed", failures }, null, 2));
  process.exitCode = 1;
} else {
  console.log(JSON.stringify({
    status: "passed",
    artifacts: Object.keys(rel).length,
    requirements: baselineIds.length,
    flows: flowIds.length,
    interactions: interactionIds.length,
    interactionTraceabilityRows: interactionTraceIds.length,
    accessibilityCoveredInteractions: interactionIds.length,
    accessibilityCoveredComplexPatterns: complexPatternIds.length,
    accessibilityCoveredLowFidelityStates: 7,
    accessibilityGaps: 8,
    runtimeParityGaps: 3,
    controlStates: controlStates.length,
    coordinatedSurfaces: surfaceIds.length,
    surfaceControlGaps: surfaceControlGapCount,
    independentlyChangingRegions: regionIds.length,
    requiredRecoveryStates: requiredRecoveryStates.length,
    recoveryGaps: recoveryGapCount,
    lowFidelityScreens: lowFiScreenIds.length,
    requiredLowFidelityStatesPerScreen: 7,
    responsiveContexts: 3,
    responsiveTestScenarios: 12,
    complexOperationalPatterns: complexPatternIds.length,
    complexPatternTestScenarios: complexPatternTestIds.length,
    acceptanceCriteria: acceptanceIds.length,
    uatScenarios: uatIds.length,
    stateCodes: stateCodes.length,
    permissionAuditGaps: expectedGapIds.length,
    permissionsChecked: expectedPermissions.length,
    auditActionsChecked: expectedAuditActions.length,
  }, null, 2));
}
