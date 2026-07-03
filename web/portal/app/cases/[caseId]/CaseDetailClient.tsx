"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { CompletenessIndicator } from "@/components/CompletenessIndicator";
import { CustomerUpdatePanel, customerUpdateAllows, type CustomerUpdate } from "@/components/CustomerUpdatePanel";
import { CustomerUpdateGovernedDialog, type PendingCustomerUpdateAction } from "@/components/CustomerUpdateGovernedDialog";
import { PageHeader } from "@/components/PageHeader";
import { RiskBadge, SlaBadge, StatusBadge } from "@/components/Badges";
import { SectionCard } from "@/components/SectionCard";
import { SideDrawer } from "@/components/SideDrawer";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { SourceCitationList, type SourceCitation } from "@/components/SourceCitationList";
import { BlockedActionPanel, ConfirmationAuditNotice, GovernedButton, StatusChip } from "@/components/GovernedUi";
import type { BlockedAction } from "@/components/GovernedUi";

function toBlockedActions(actions: DocumentActionState[] | undefined): BlockedAction[] {
  return (actions ?? []).map((action) => ({
    action: action.action,
    reasonCode: action.requiredPermission,
    message: action.reason ?? action.label
  }));
}
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, canUseLocalTestDoubles, scopedHeaders } from "@/lib/config";
import { allowsAction, blockedActionReason, hasActionState, type ActionStateContract } from "@/lib/actionState";
import { documentContractLabel, type DocumentItem, type DocumentActionState } from "@/lib/documentContracts";
import {
  customerUpdateSendAvailability,
  type ChannelSendAvailability,
  type ProviderHealth
} from "@/lib/providerHealth";
import { motion } from "framer-motion";
import { Bot, FileText, CheckCircle, AlertTriangle, Clock, Link as LinkIcon, Cpu, ClipboardCheck, FileCheck2, Send } from "lucide-react";

type CaseDetail = ActionStateContract & {
  caseId: string;
  customerName: string;
  customerEmail?: string | null;
  customerPhone?: string | null;
  policyNumber: string;
  productLine: string;
  claimType: string;
  status: string;
  riskLevel: string;
  priority: string;
  incidentDate?: string | null;
  incidentDescription?: string | null;
  assignedUserId?: string | null;
  assignedTeam?: string | null;
  assignedUserName?: string | null;
  slaDueAt?: string | null;
  completenessScore: number;
  createdAt?: string | null;
  updatedAt?: string | null;
  summary?: string | null;
  audit?: {
    lastActionName: string | null;
  };
  allowedActions?: string[];
  blockedActions?: DocumentActionState[];
};

type ChecklistItem = {
  documentType: string;
  label: string;
  received: boolean;
};

type Checklist = {
  completenessScore: number;
  requiredReceived: number;
  requiredTotal: number;
  requiredDocuments: ChecklistItem[];
  optionalDocuments: ChecklistItem[];
  receivedDocuments: ChecklistItem[];
  missingRequiredDocuments: ChecklistItem[];
};

type RecommendedAction = {
  actionId?: string;
  label?: string;
  title: string;
  summary?: string;
  rationale?: string;
  priority?: string;
  actionType?: string;
  requiresHumanApproval?: boolean;
  evidence?: string[];
};

type WorkflowTask = {
  taskId: string;
  caseId: string;
  taskType: string;
  status: string;
  assignedToUserId?: string | null;
  assignedToName?: string | null;
  title?: string | null;
  description?: string | null;
  dueAt?: string | null;
};

type TimelineEvent = {
  eventId: string;
  eventType: string;
  actor: string;
  timestamp: string;
  summary: string;
  beforeStatus: string | null;
  afterStatus: string | null;
};

type AssistantAction = {
  answer: string;
  confidence: string;
  sources: SourceCitation[];
};

type Communication = {
  communicationId: string;
  channel: string;
  direction?: string;
  messageBody: string;
  deliveryStatus: string;
  createdAt: string;
};

type Approval = {
  approvalId: string;
  caseId: string;
  approvalType: string;
  status: string;
  riskLevel: string;
  evidenceSummary: string;
  sourceReferences: string[];
  reviewerUserId?: string | null;
  decision?: string | null;
  comments?: string | null;
  createdAt: string;
  updatedAt: string;
  decidedAt?: string | null;
};

type CaseReviewTab = "Overview" | "Documents" | "Policy & Coverage" | "Timeline" | "Tasks" | "Approvals" | "Communications" | "Audit";

type DrawerState =
  | { type: "document"; document: DocumentItem }
  | { type: "citation"; source: SourceCitation }
  | { type: "timeline"; event: TimelineEvent }
  | { type: "update"; update: CustomerUpdate }
  | { type: "communication"; communication: Communication }
  | { type: "task"; task: WorkflowTask }
  | { type: "approval"; approval: Approval }
  | { type: "governedAction"; action: GovernedCaseAction }
  | null;

type GovernedCaseAction = {
  title: string;
  affectedObject: string;
  impact: string;
  submitLabel: string;
  path: string;
  body: Record<string, unknown>;
};

const caseReviewTabs: CaseReviewTab[] = ["Overview", "Documents", "Policy & Coverage", "Timeline", "Tasks", "Approvals", "Communications", "Audit"];

export function CaseDetailClient({ caseId }: { caseId: string }) {
  const { role, userId, can } = useRole();
  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [checklist, setChecklist] = useState<Checklist | null>(null);
  const [recommendedActions, setRecommendedActions] = useState<RecommendedAction[]>([]);
  const [tasks, setTasks] = useState<WorkflowTask[]>([]);
  const [timeline, setTimeline] = useState<TimelineEvent[]>([]);
  const [updates, setUpdates] = useState<CustomerUpdate[]>([]);
  const [communications, setCommunications] = useState<Communication[]>([]);
  const [approvals, setApprovals] = useState<Approval[]>([]);
  const [providerHealth, setProviderHealth] = useState<ProviderHealth | null>(null);
  const [assistantQuestion, setAssistantQuestion] = useState("What should the assigned officer review next?");
  const [assistantAnswer, setAssistantAnswer] = useState<AssistantAction | null>(null);
  const [activeTab, setActiveTab] = useState<CaseReviewTab>("Overview");
  const [governedReason, setGovernedReason] = useState("");
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [drawer, setDrawer] = useState<DrawerState>(null);
  const [pendingCustomerUpdateAction, setPendingCustomerUpdateAction] = useState<PendingCustomerUpdateAction | null>(null);
  const canManageCustomerCommunications = can("ManageCustomerCommunications");
  const canSendCustomerUpdates = can("SendCustomerUpdate");
  const canReviewWorkflow = can("ApproveSettlementWorkflowStep");
  const canUseGroundedAssistant = can("ViewAiOutputs");
  const canUploadDocuments = can("UploadDocument");

  const headers = useMemo(
    () => scopedHeaders(role, userId),
    [role, userId]
  );

  const jsonHeaders = useMemo(
    () => ({
      ...headers,
      "Content-Type": "application/json"
    }),
    [headers]
  );

  const load = useCallback(async () => {
    setError(null);
    setStatus("Loading case review...");

    try {
      const [
        caseResponse,
        documentsResponse,
        checklistResponse,
        actionsResponse,
        tasksResponse,
        timelineResponse,
        updatesResponse,
        assistantResponse,
        communicationsResponse,
        approvalsResponse,
        providerResponse
      ] = await Promise.all([
        fetch(`${apiBaseUrl}/api/v1/cases/${caseId}`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/documents`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/document-checklist`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/recommended-actions`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/workflow/tasks`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/timeline`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/customer-updates`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/assistant/actions/${caseId}`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/communications`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/approvals`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/provider-status`, { headers })
      ]);

      if (!caseResponse.ok) {
        throw new Error(`Case review failed with HTTP ${caseResponse.status}.`);
      }

      const casePayload: unknown = await caseResponse.json();
      if (!hasActionState(casePayload)) {
        throw new Error("Case response did not include required backend action authorization state.");
      }

      setCaseDetail(casePayload as CaseDetail);
      setDocuments(documentsResponse.ok ? ((await documentsResponse.json()) as DocumentItem[]) : []);
      setChecklist(checklistResponse.ok ? ((await checklistResponse.json()) as Checklist) : null);
      setRecommendedActions(actionsResponse.ok ? ((await actionsResponse.json()) as RecommendedAction[]) : []);

      const workflowTasks = tasksResponse.ok ? ((await tasksResponse.json()) as WorkflowTask[]) : [];
      setTasks(workflowTasks.filter((task) => task.caseId === caseId && task.status !== "Completed"));
      setTimeline(timelineResponse.ok ? ((await timelineResponse.json()) as TimelineEvent[]) : []);
      setUpdates(updatesResponse.ok ? ((await updatesResponse.json()) as CustomerUpdate[]) : []);
      setCommunications(communicationsResponse.ok ? ((await communicationsResponse.json()) as Communication[]) : []);
      const approvalItems = approvalsResponse.ok ? ((await approvalsResponse.json()) as Approval[]) : [];
      setApprovals(approvalItems.filter((approval) => approval.caseId === caseId));
      setProviderHealth(providerResponse.ok ? ((await providerResponse.json()) as ProviderHealth) : null);
      setAssistantAnswer(assistantResponse.ok ? ((await assistantResponse.json()) as AssistantAction) : null);
      setStatus(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Case review failed.");
      setStatus(null);
    }
  }, [caseId, headers]);

  useEffect(() => {
    void load();
  }, [load]);

  const pendingApprovals = approvals.filter(
    (approval) => approval.status !== "Approved" && approval.status !== "Rejected"
  );

  async function post(path: string, body: object = {}, auditReason?: string) {
    setError(null);
    setStatus("Submitting workflow action...");

    try {
      const response = await fetch(`${apiBaseUrl}${path}`, {
        method: "POST",
        headers: auditReason ? { ...jsonHeaders, "X-Audit-Reason": auditReason } : jsonHeaders,
        body: JSON.stringify(body)
      });

      if (!response.ok) {
        throw new Error(await readActionError(response));
      }

      await load();
      setStatus("Action completed.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Action failed.");
      setStatus(null);
    }
  }

  async function submitGovernedAction(action: GovernedCaseAction) {
    if (!governedReason.trim()) {
      setStatus("A reason is required before this governed action can be recorded.");
      return;
    }

    await post(action.path, { ...action.body, comments: governedReason.trim() }, governedReason.trim());
    setGovernedReason("");
    setDrawer(null);
  }

  async function askAssistant() {
    if (!caseDetail || !assistantQuestion.trim() || !canUseGroundedAssistant) {
      return;
    }

    setError(null);
    setStatus("Generating grounded assistant answer...");

    try {
      // Grounded, case-scoped policy Q&A via the kept policy-questions endpoint.
      // The multi-agent /api/v1/assistant/query endpoint was dropped in the v2 migration.
      const response = await fetch(`${apiBaseUrl}/api/v1/assistant/policy-questions`, {
        method: "POST",
        headers: jsonHeaders,
        body: JSON.stringify({
          caseId,
          question: assistantQuestion,
          productLine: caseDetail.productLine
        })
      });

      if (!response.ok) {
        throw new Error(`Assistant answer failed with HTTP ${response.status}.`);
      }

      setAssistantAnswer((await response.json()) as AssistantAction);
      setStatus("Assistant answer refreshed from approved knowledge.");
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Assistant answer failed.");
      setStatus(null);
    }
  }

  function resolveCustomerUpdateSendContext(update: CustomerUpdate) {
    if (!caseDetail) {
      return null;
    }

    const availability = customerUpdateSendAvailability(update.channel, providerHealth, canUseLocalTestDoubles);
    if (!availability.enabled) {
      setError(availability.reason);
      return null;
    }

    const recipient = availability.recipientField === "phone"
      ? caseDetail.customerPhone?.trim()
      : availability.recipientField === "email"
        ? caseDetail.customerEmail?.trim()
        : null;

    if (availability.recipientField && !recipient) {
      setError(`Cannot send: the customer's ${availability.recipientField} contact is not configured.`);
      return null;
    }

    return { availability, recipient };
  }

  function openCustomerUpdateConfirmation(action: "approve" | "send", update: CustomerUpdate) {
    setError(null);

    if (action === "approve") {
      if (!canManageCustomerCommunications || !customerUpdateAllows(update, "approveCustomerUpdate")) {
        setError("You do not have permission to approve customer updates.");
        return;
      }

      setPendingCustomerUpdateAction({ action, update });
      return;
    }

    if (!canSendCustomerUpdates || !customerUpdateAllows(update, "sendCustomerUpdate")) {
      setError("You do not have permission to send customer updates.");
      return;
    }

    const sendContext = resolveCustomerUpdateSendContext(update);
    if (!sendContext) {
      return;
    }

    setPendingCustomerUpdateAction({
      action,
      update,
      availability: sendContext.availability,
      recipient: sendContext.recipient
    });
  }

  async function approveCustomerUpdate(update: CustomerUpdate, auditReason: string) {
    if (!canManageCustomerCommunications || !customerUpdateAllows(update, "approveCustomerUpdate")) {
      setError("You do not have permission to approve customer updates.");
      return;
    }

    await post(`/api/v1/customer-updates/${update.updateId}/approve`, { userId }, auditReason);
    setPendingCustomerUpdateAction(null);
  }

  async function sendCustomerUpdate(update: CustomerUpdate, auditReason: string) {
    if (!canSendCustomerUpdates || !customerUpdateAllows(update, "sendCustomerUpdate")) {
      setError("You do not have permission to send customer updates.");
      return;
    }

    const sendContext = resolveCustomerUpdateSendContext(update);
    if (!sendContext) {
      return;
    }

    await post(`/api/v1/customer-updates/${update.updateId}/send`, {
      userId,
      recipient: sendContext.recipient
    }, auditReason);
    setPendingCustomerUpdateAction(null);
  }

  if (error && !caseDetail) {
    return <ErrorState message={error} />;
  }

  if (!caseDetail) {
    return <LoadingState label="Loading case review..." />;
  }

  const openTask = tasks[0] ?? null;
  const checklistMissing = checklist?.missingRequiredDocuments ?? [];
  const documentCompleteness = checklist?.completenessScore ?? caseDetail.completenessScore;
  const canEvaluateCaseWorkflow = canReviewWorkflow && allowsAction(caseDetail, "evaluateCaseWorkflow");
  const canCreateApprovalRequest = canReviewWorkflow && allowsAction(caseDetail, "createApprovalRequest");
  const canDraftCaseCustomerUpdate = canManageCustomerCommunications && allowsAction(caseDetail, "draftCustomerUpdate");
  const workflowBlockedReason = blockedActionReason(caseDetail, "evaluateCaseWorkflow");
  const approvalBlockedReason = blockedActionReason(caseDetail, "createApprovalRequest");
  const updateBlockedReason = blockedActionReason(caseDetail, "draftCustomerUpdate");
  const blockedCaseActions = caseDetail.blockedActions.filter((action) =>
    ["evaluateCaseWorkflow", "createApprovalRequest", "draftCustomerUpdate"].includes(action.action)
  );

  function slaFromDue(due: string | null | undefined) {
    if (!due) return "No SLA";
    const now = new Date();
    const d = new Date(due);
    if (d < now) return "Breached";
    const diff = d.getTime() - now.getTime();
    if (diff < 1000 * 60 * 60 * 24) return "At Risk";
    return "On Track";
  }

  return (
    <>
      <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }} className="space-y-6">
        <PageHeader
          eyebrow="Case Review"
          title={caseDetail.customerName}
          description="Human-supervised claim handling with grounded policy support, workflow controls, customer update drafting, and auditable communications."
          actions={
            <div className="flex gap-3">
              {canUploadDocuments ? <Link className="rounded-full border border-line bg-surface px-5 py-2.5 text-sm font-semibold text-muted hover:border-accent hover:text-accent hover:shadow-glow-cyan transition-all" href={`/claims/${caseId}/documents`}>
                Manage documents
              </Link> : null}
              {canReviewWorkflow ? <button
                className="rounded-full border border-accent/40 bg-accent/10 px-5 py-2.5 text-sm font-semibold text-accent hover:bg-accent hover:text-white shadow-glow-cyan transition-all disabled:cursor-not-allowed disabled:opacity-50"
                disabled={!canEvaluateCaseWorkflow}
                title={workflowBlockedReason ?? undefined}
                onClick={() => post(`/api/v1/cases/${caseId}/workflow/evaluate`, {})}
              >
                Evaluate workflow
              </button> : null}
              {canManageCustomerCommunications ? <button
                className="rounded-full bg-azure border border-azure/50 px-5 py-2.5 text-sm font-semibold text-white shadow-glow-cyan transition-all hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50"
                disabled={!canDraftCaseCustomerUpdate}
                title={updateBlockedReason ?? undefined}
                onClick={() => post(`/api/v1/cases/${caseId}/customer-updates/draft`, { reasonCategory: checklistMissing.length ? "MissingDocuments" : "StatusUpdate", channel: "Email", createdByUserId: userId })}
              >
                Draft customer update
              </button> : null}
            </div>
          }
        />

        {error ? <ErrorState message={error} /> : null}
        {status ? <div className="rounded-xl border border-line bg-surface px-4 py-3 font-mono text-sm text-accent shadow-panel">{status}</div> : null}

        <section className="grid gap-4 grid-cols-2 lg:grid-cols-3">
          <Metric label="Status" value={<StatusBadge label={caseDetail.status} />} />
          <Metric label="Risk" value={<RiskBadge label={caseDetail.riskLevel} />} />
          <Metric label="Priority" value={<span className="font-mono text-accent">{caseDetail.priority}</span>} />
          <Metric label="SLA" value={<SlaBadge label={slaFromDue(caseDetail.slaDueAt)} />} />
          <Metric label="Completeness" value={<span className="font-mono text-accent">{documentCompleteness}%</span>} />
          <Metric label="Assigned" value={caseDetail.assignedUserName ?? caseDetail.assignedUserId ?? "Unassigned"} />
        </section>

        <div className="flex flex-wrap gap-2" role="tablist" aria-label="Case review sections">
          {caseReviewTabs.map((tab) => (
            <button
              key={tab}
              type="button"
              role="tab"
              aria-selected={activeTab === tab}
              className={`min-h-11 rounded-xl border px-4 py-2 text-sm font-semibold transition-colors ${activeTab === tab ? "border-accent bg-accent text-white" : "border-line bg-surface text-muted hover:border-accent/50 hover:text-ink"}`}
              onClick={() => setActiveTab(tab)}
            >
              {tab}
            </button>
          ))}
        </div>

        <BlockedActionPanel actions={toBlockedActions(caseDetail.blockedActions)} title="Case-level blocked actions" />

        <div className="grid gap-6 xl:grid-cols-[minmax(0,1.45fr)_minmax(320px,0.85fr)]">
          <div className="space-y-6">
            {activeTab === "Overview" ? (
              <SectionCard
                eyebrow="Intelligence"
                title="Case summary"
                description="AI-prepared review summary. It informs the officer, but does not approve or reject claims."
                actions={<StatusBadge label="Human review required" />}
              >
                <div className="grid gap-5 lg:grid-cols-[1.4fr_0.9fr]">
                  <div className="space-y-4">
                    <p className="text-sm leading-7 text-muted/90">
                      {caseDetail.summary ??
                        `${caseDetail.claimType} claim on policy ${caseDetail.policyNumber}. Review uploaded evidence, missing-document checklist, recommended actions, and customer communications before any consequential human decision.`}
                    </p>
                    <div className="grid gap-3 md:grid-cols-2">
                      <InfoBlock label="Claim ID" value={caseDetail.caseId} />
                      <InfoBlock label="Policy" value={caseDetail.policyNumber} />
                      <InfoBlock label="Customer" value={caseDetail.customerName} />
                      <InfoBlock label="Masked contact" value={maskedContact(caseDetail.customerEmail, caseDetail.customerPhone)} />
                      <InfoBlock label="Product line" value={caseDetail.productLine} />
                      <InfoBlock label="Claim type" value={caseDetail.claimType} />
                      <InfoBlock label="Incident date" value={caseDetail.incidentDate ?? "Not captured"} />
                      <InfoBlock label="Submitted" value={caseDetail.createdAt ? new Date(caseDetail.createdAt).toLocaleString() : "Not captured"} />
                      <InfoBlock label="Claimed amount" value="Not captured" />
                      <InfoBlock label="Current queue" value={caseDetail.assignedTeam ?? "Claims team"} />
                      <InfoBlock label="Last updated" value={caseDetail.updatedAt ? new Date(caseDetail.updatedAt).toLocaleString() : "Not captured"} />
                      <InfoBlock label="Latest audit" value={caseDetail.audit?.lastActionName ?? "No audit action recorded"} />
                    </div>
                  </div>
                  <div className="rounded-xl border border-warning/30 bg-warning/10 p-4">
                    <p className="flex items-center gap-2 text-[10px] font-bold uppercase tracking-[0.12em] text-warning"><AlertTriangle className="h-3 w-3" /> Control notes</p>
                    <ul className="mt-3 space-y-2 text-sm leading-6 text-muted">
                      <li>Production identity and permissions must be derived server-side.</li>
                      <li>Deployed runtimes require real configured providers.</li>
                      <li>Consequential actions still require a human decision boundary.</li>
                      <li>Audit history remains tenant and workspace scoped.</li>
                    </ul>
                    <div className="mt-4 flex flex-wrap gap-2">
                      <StatusChip label={`${caseDetail.allowedActions?.length ?? 0} available actions`} tone="success" />
                      <StatusChip label={(caseDetail.blockedActions?.length ?? 0) > 0 ? "Action blocked" : "No case block"} tone={(caseDetail.blockedActions?.length ?? 0) > 0 ? "danger" : "neutral"} />
                    </div>
                  </div>
                </div>
              </SectionCard>
            ) : null}

            {activeTab === "Documents" ? (
            <SectionCard
              eyebrow="Evidence"
              title="Documents and completeness"
              description="Click a document to inspect extraction, warnings, and provider details in the drawer."
              actions={canUploadDocuments ? <Link className="rounded-full border border-line bg-surface px-4 py-2 text-sm font-semibold text-ink hover:text-accent hover:border-accent transition-all" href={`/claims/${caseId}/documents`}>Open full document flow</Link> : null}
            >
              <div className="grid gap-6 lg:grid-cols-[minmax(0,1.2fr)_320px]">
                <div className="space-y-3">
                  {documents.length === 0 ? (
                    <EmptyState title="No documents received" description="Upload a claim form, invoice, report, or image to begin document review." />
                  ) : (
                    documents.map((document) => (
                      <button
                        key={document.documentId}
                        className="w-full rounded-xl border border-line bg-mist p-4 text-left transition hover:border-accent/50 group"
                        onClick={() => setDrawer({ type: "document", document })}
                      >
                        <div className="flex flex-wrap items-start justify-between gap-3">
                          <div className="flex items-center gap-3">
                            <FileText className="w-5 h-5 text-accent group-hover:text-azure transition-colors" />
                            <div>
                              <p className="font-semibold text-ink group-hover:text-accent transition-colors">{document.originalFileName}</p>
                              <p className="mt-1 text-sm text-muted">
                                {document.documentType} • {document.processingStatus} • uploaded {new Date(document.uploadedAt).toLocaleString()}
                              </p>
                            </div>
                          </div>
                          <div className="flex flex-wrap gap-2">
                            <StatusBadge label={document.processingStatus} />
                            <span className="font-mono text-[10px] text-accent bg-accent/10 border border-accent/20 px-2 py-1 rounded">{Math.round(document.confidenceScore * 100)}%</span>
                          </div>
                        </div>
                      </button>
                    ))
                  )}
                </div>
                <div className="space-y-4 rounded-xl border border-line bg-mist p-4 relative overflow-hidden">
                  <div className="absolute top-0 right-0 w-32 h-32 bg-accent/5 rounded-bl-full pointer-events-none" />
                  <CompletenessIndicator value={documentCompleteness} />
                  <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-1">
                    <SummaryPill label="Required received" value={`${checklist?.requiredReceived ?? 0}/${checklist?.requiredTotal ?? 0}`} />
                    <SummaryPill label="Missing required" value={`${checklistMissing.length}`} />
                  </div>
                  <div>
                    <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Missing now</p>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {checklistMissing.length === 0 ? (
                        <span className="rounded-full border border-success/30 bg-success/10 px-3 py-1 text-xs font-semibold text-success shadow-glow-success">No missing required documents</span>
                      ) : (
                        checklistMissing.map((item) => (
                          <span key={item.documentType} className="rounded-full border border-danger/30 bg-danger/10 px-3 py-1 text-xs font-semibold text-danger shadow-glow-danger">
                            {item.label}
                          </span>
                        ))
                      )}
                    </div>
                  </div>
                </div>
              </div>
            </SectionCard>
            ) : null}

            {activeTab === "Policy & Coverage" ? (
            <SectionCard
              eyebrow="Knowledge"
              title="Grounded policy references"
              description="Source citations are drawn from approved tenant knowledge only."
            >
              {assistantAnswer?.sources?.length ? (
                <div className="space-y-3">
                  {assistantAnswer.sources.map((source) => (
                    <button
                      key={source.chunkId}
                      className="w-full rounded-xl border border-line bg-mist p-4 text-left transition hover:border-accent/50 group"
                      onClick={() => setDrawer({ type: "citation", source })}
                    >
                      <p className="font-semibold text-ink group-hover:text-accent transition-colors flex items-center gap-2"><LinkIcon className="w-4 h-4" /> {source.label}</p>
                      <p className="mt-2 text-sm leading-6 text-muted border-l-2 border-accent/30 pl-3">{source.excerpt}</p>
                    </button>
                  ))}
                </div>
              ) : (
                <SourceCitationList sources={[]} />
              )}
            </SectionCard>
            ) : null}

            {activeTab === "Timeline" || activeTab === "Audit" ? (
            <SectionCard
              eyebrow="History"
              title="Audit timeline"
              description="Every workflow step stays visible and exportable for compliance-safe review."
            >
              {timeline.length === 0 ? (
                <EmptyState title="No audit events yet" description="Timeline events will appear after workflow actions and customer communications are recorded." />
              ) : (
                <div className="space-y-3">
                  {timeline.map((event) => (
                    <button
                      key={event.eventId}
                      className="w-full rounded-xl border border-line bg-mist p-4 text-left transition hover:border-accent/50 group"
                      onClick={() => setDrawer({ type: "timeline", event })}
                    >
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <p className="font-semibold text-ink group-hover:text-accent transition-colors">{event.summary}</p>
                          <p className="mt-1 text-sm text-muted flex items-center gap-1">
                            <Clock className="w-3 h-3" /> {event.eventType} • {event.actor}
                          </p>
                        </div>
                        <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted font-mono">{new Date(event.timestamp).toLocaleString()}</p>
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </SectionCard>
            ) : null}

            {activeTab === "Communications" ? (
            <SectionCard
              eyebrow="Outbound"
              title="Customer updates & communication"
              description="Drafts remain human-reviewed by design. Delivery requires a configured provider in deployed runtimes."
            >
              <div className="grid gap-6 lg:grid-cols-[minmax(0,1.05fr)_minmax(0,0.95fr)]">
                <div className="space-y-4">
                  <CustomerUpdatePanel
                    updates={updates}
                    onApprove={canManageCustomerCommunications ? (update) => openCustomerUpdateConfirmation("approve", update) : undefined}
                    onSend={canSendCustomerUpdates ? (update) => openCustomerUpdateConfirmation("send", update) : undefined}
                    getSendAvailability={(update) => customerUpdateSendAvailability(update.channel, providerHealth, canUseLocalTestDoubles)}
                  />
                </div>
                <div className="space-y-3">
                  {communications.length === 0 ? (
                    <EmptyState title="No communications yet" description="Case-linked email and SMS history will appear here after approved sending through a configured provider." />
                  ) : (
                    communications.map((communication) => (
                      <button
                        key={communication.communicationId}
                        className="w-full rounded-xl border border-line bg-mist p-4 text-left transition hover:border-accent/50 group"
                        onClick={() => setDrawer({ type: "communication", communication })}
                      >
                        <div className="flex flex-wrap items-start justify-between gap-3">
                          <div>
                            <p className="font-semibold text-ink group-hover:text-accent transition-colors">
                              {communication.channel} • <StatusBadge label={communication.deliveryStatus} />
                            </p>
                            <p className="mt-2 line-clamp-2 text-sm leading-6 text-muted border-l-2 border-line pl-2">{communication.messageBody}</p>
                          </div>
                          <p className="text-[10px] uppercase font-mono tracking-[0.18em] text-muted">{new Date(communication.createdAt).toLocaleString()}</p>
                        </div>
                      </button>
                    ))
                  )}
                </div>
              </div>
            </SectionCard>
            ) : null}
          </div>

          <div className="space-y-6">
            {activeTab === "Tasks" ? (
            <SectionCard
              eyebrow="Operations"
              title="Workflow command deck"
              description="Use these actions to create review tasks and approval steps without bypassing human accountability."
              actions={<StatusBadge label={openTask ? openTask.status : "No open task"} />}
            >
              <div className="space-y-4">
                <div className="rounded-xl border border-line bg-mist p-4 relative overflow-hidden">
                  <div className="absolute top-0 right-0 p-3"><Bot className="w-8 h-8 text-surface" /></div>
                  <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Open task</p>
                  {openTask ? (
                    <button className="mt-3 w-full rounded-xl border border-accent/40 bg-surface/80 p-4 text-left group transition-all hover:border-accent hover:shadow-glow-cyan" onClick={() => setDrawer({ type: "task", task: openTask })}>
                      <p className="font-semibold text-ink group-hover:text-accent">{openTask.title ?? openTask.taskType}</p>
                      <p className="mt-1 text-sm text-muted">{openTask.description ?? "Workflow task awaiting a claims officer or supervisor decision."}</p>
                    </button>
                  ) : (
                    <p className="mt-3 text-sm text-muted">No open workflow task is currently attached to this case.</p>
                  )}
                </div>

                {blockedCaseActions.length > 0 ? (
                  <div className="space-y-2">
                    {blockedCaseActions.map((action) => (
                      <p key={action.action} className="rounded-xl border border-warning/30 bg-warning/10 px-3 py-2 text-xs font-semibold text-warning">
                        {action.reason ?? `${action.label} is not currently available.`}
                      </p>
                    ))}
                  </div>
                ) : null}

                <button
                  className="w-full rounded-full bg-accent/10 border border-accent/40 px-4 py-3 text-sm font-semibold text-accent shadow-glow-cyan hover:bg-accent hover:text-white transition-all disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={!canCreateApprovalRequest}
                  title={approvalBlockedReason ?? undefined}
                  onClick={() => post(`/api/v1/cases/${caseId}/approvals`, {})}
                >
                  Create approval request
                </button>
                <button
                  className="w-full rounded-full border border-line bg-surface px-4 py-3 text-sm font-semibold text-ink hover:text-accent hover:border-accent transition-all disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={!canEvaluateCaseWorkflow}
                  title={workflowBlockedReason ?? undefined}
                  onClick={() => post(`/api/v1/cases/${caseId}/workflow/evaluate`, {})}
                >
                  Send to supervisor workflow
                </button>
                <button
                  className="w-full rounded-full border border-success/30 bg-success/10 px-4 py-3 text-sm font-semibold text-success shadow-glow-success disabled:cursor-not-allowed disabled:opacity-50 hover:bg-success hover:text-white transition-all"
                  disabled={!openTask || !canReviewWorkflow}
                  onClick={() => openTask && post(`/api/v1/workflow/tasks/${openTask.taskId}/complete`, {})}
                >
                  Complete active task
                </button>
              </div>
            </SectionCard>
            ) : null}

            {activeTab === "Tasks" || activeTab === "Policy & Coverage" ? (
            <SectionCard eyebrow="AI Guidance" title="Recommended next actions" description="AI can prepare the work, but a human still owns the decision.">
              {recommendedActions.length === 0 ? (
                <EmptyState title="No recommendations yet" description="Run workflow evaluation to refresh case guidance." />
              ) : (
                <div className="space-y-3">
                  {recommendedActions.map((action, index) => (
                    <div key={action.actionId ?? `${action.title}-${index}`} className="rounded-xl border border-line bg-mist p-4">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div className="flex gap-3">
                          <CheckCircle className="w-5 h-5 text-accent mt-0.5" />
                          <div>
                            <p className="font-semibold text-ink">{action.label ?? action.title}</p>
                            <p className="mt-1 text-sm leading-6 text-muted/80">{action.summary ?? action.rationale ?? "No additional summary was returned for this recommendation."}</p>
                            {action.requiresHumanApproval ? <p className="mt-2 text-xs font-semibold text-warning">Human approval required before relying on this action.</p> : null}
                          </div>
                        </div>
                        <StatusBadge label={action.priority ?? "Planned"} />
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </SectionCard>
            ) : null}

            {activeTab === "Approvals" ? (
            <SectionCard
              eyebrow="Human review"
              title="Approvals"
              description="Approval records are review steps only. They do not settle, deny, repudiate or authorise payment."
              actions={<StatusChip label={`${pendingApprovals.length} pending`} tone={pendingApprovals.length > 0 ? "warning" : "success"} />}
            >
              {approvals.length === 0 ? (
                <EmptyState title="No approvals yet" description="Create a supervisor review step when evidence and workflow state require human review." />
              ) : (
                <div className="space-y-3">
                  {approvals.map((approval) => (
                    <button key={approval.approvalId} className="w-full rounded-xl border border-line bg-mist p-4 text-left transition hover:border-accent/50" onClick={() => setDrawer({ type: "approval", approval })}>
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <p className="font-semibold text-ink">{approval.approvalType}</p>
                          <p className="mt-1 text-sm leading-6 text-muted">{approval.evidenceSummary}</p>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <StatusBadge label={approval.status} />
                          <RiskBadge label={approval.riskLevel} />
                        </div>
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </SectionCard>
            ) : null}

            {activeTab === "Policy & Coverage" ? (
            <SectionCard
              eyebrow="Assistant"
              title="IIP copilot for this case"
              description="Grounded internal assistant only. It must cite sources and escalate uncertainty."
              actions={<StatusBadge label="Grounded retrieval" />}
            >
              <div className="space-y-4">
                <textarea
                  className="min-h-[120px] w-full rounded-xl border border-line bg-shell px-4 py-3 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono"
                  value={assistantQuestion}
                  onChange={(event) => setAssistantQuestion(event.target.value)}
                />
                <button className="rounded-full bg-azure border border-azure/40 px-4 py-2 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all w-full flex justify-center gap-2 items-center disabled:cursor-not-allowed disabled:opacity-50" disabled={!canUseGroundedAssistant} onClick={askAssistant}>
                  <Bot className="w-4 h-4" /> Ask grounded assistant
                </button>
                <div className="rounded-xl border border-line bg-mist p-4">
                  <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted flex items-center gap-2"><Cpu className="w-3 h-3" /> Latest answer</p>
                  <motion.p key={assistantAnswer?.answer ?? "empty"} initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="mt-3 text-sm leading-7 text-muted/90">
                    {assistantAnswer?.answer ?? "No grounded answer yet. Ask a case-specific policy or workflow question."}
                  </motion.p>
                  {assistantAnswer?.confidence ? <p className="mt-3 text-[10px] font-bold uppercase tracking-[0.18em] text-accent font-mono bg-accent/10 inline-block px-2 py-1 rounded border border-accent/20">Confidence: {assistantAnswer.confidence}</p> : null}
                </div>
              </div>
            </SectionCard>
            ) : null}
          </div>
        </div>
      </motion.div>

      <SideDrawer
        open={drawer !== null}
        title={drawerTitle(drawer)}
        subtitle={drawerSubtitle(drawer)}
        onClose={() => {
          setDrawer(null);
          setPendingCustomerUpdateAction(null);
        }}
        footer={drawerFooter(
          drawer,
          caseId,
          post,
          userId,
          openCustomerUpdateConfirmation,
          canManageCustomerCommunications,
          canSendCustomerUpdates,
          canReviewWorkflow,
          canUploadDocuments,
          (update) => customerUpdateSendAvailability(update.channel, providerHealth, canUseLocalTestDoubles)
        )}
      >
        {drawerBody(drawer, governedReason, setGovernedReason, submitGovernedAction)}
      </SideDrawer>

      <CustomerUpdateGovernedDialog
        pendingAction={pendingCustomerUpdateAction}
        onClose={() => setPendingCustomerUpdateAction(null)}
        onConfirm={async (auditReason) => {
          if (!pendingCustomerUpdateAction) {
            return;
          }

          if (pendingCustomerUpdateAction.action === "approve") {
            await approveCustomerUpdate(pendingCustomerUpdateAction.update, auditReason);
            return;
          }

          await sendCustomerUpdate(pendingCustomerUpdateAction.update, auditReason);
        }}
      />
    </>
  );
}

function Metric({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4 shadow-panel flex flex-col justify-between">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</p>
      <div className="mt-3 text-lg font-bold text-ink">{value}</div>
    </div>
  );
}

function InfoBlock({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-surface/50 p-3">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</p>
      <p className="mt-2 text-sm font-semibold text-ink font-mono">{value}</p>
    </div>
  );
}

function SummaryPill({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-surface/50 p-3">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</p>
      <p className="mt-2 text-lg font-bold text-ink font-mono">{value}</p>
    </div>
  );
}

function maskedContact(email?: string | null, phone?: string | null) {
  if (phone) {
    return phone;
  }

  if (!email) {
    return "Not provided";
  }

  const [localPart, domain] = email.split("@");
  if (!domain) {
    return "Contact on file";
  }

  return `${localPart.slice(0, 2)}***@${domain}`;
}

function drawerTitle(drawer: DrawerState) {
  if (!drawer) return "";
  if (drawer.type === "document") return drawer.document.originalFileName;
  if (drawer.type === "citation") return drawer.source.documentName;
  if (drawer.type === "timeline") return drawer.event.summary;
  if (drawer.type === "update") return `${drawer.update.reasonCategory} update`;
  if (drawer.type === "communication") return `${drawer.communication.channel} communication`;
  if (drawer.type === "approval") return `${drawer.approval.approvalType} approval`;
  if (drawer.type === "governedAction") return drawer.action.title;
  return drawer.task.title ?? drawer.task.taskType;
}

function drawerSubtitle(drawer: DrawerState) {
  if (!drawer) return "";
  if (drawer.type === "document") return "Document details, extraction, validation warnings, and provider context.";
  if (drawer.type === "citation") return "Approved knowledge citation used for grounded internal assistance.";
  if (drawer.type === "timeline") return "Audit-safe timeline detail for case review and compliance.";
  if (drawer.type === "update") return "Customer update preview. Delivery requires a configured provider in deployed runtimes.";
  if (drawer.type === "communication") return "Case-linked communication history preview.";
  if (drawer.type === "approval") return "Human review-step record with evidence summary and source references.";
  if (drawer.type === "governedAction") return "Reason and audit confirmation are required before submitting.";
  return "Workflow task preview with explicit human completion control.";
}

function drawerBody(
  drawer: DrawerState,
  governedReason: string,
  setGovernedReason: (value: string) => void,
  submitGovernedAction: (action: GovernedCaseAction) => Promise<void>
) {
  if (!drawer) {
    return null;
  }

  if (drawer.type === "document") {
    return (
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-2">
          <InfoBlock label="Document type" value={drawer.document.documentType} />
          <InfoBlock label="Processing" value={drawer.document.processingStatus} />
          <InfoBlock label="Confidence" value={`${Math.round(drawer.document.confidenceScore * 100)}%`} />
          <InfoBlock label="Uploaded" value={new Date(drawer.document.uploadedAt).toLocaleString()} />
        </div>
        <div>
          <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Extracted fields</p>
          <div className="mt-3 space-y-2 rounded-xl border border-line bg-mist p-4">
            {drawer.document.fields.length === 0 ? (
              <p className="text-sm text-muted">No extracted fields were returned for this document yet.</p>
            ) : (
              drawer.document.fields.map((field) => (
                <div key={field.name} className="rounded-lg border border-line/60 bg-surface/70 p-3 text-sm">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <span className="text-muted">{field.name}</span>
                    <span className="font-semibold text-ink font-mono">{field.effectiveValue}</span>
                  </div>
                  <div className="mt-2 flex flex-wrap gap-2 text-[10px] font-bold uppercase tracking-[0.12em] text-muted">
                    <span>{documentContractLabel(field.verificationStatus)}</span>
                    <span>{field.sensitivity}</span>
                    <span>{field.confidenceScore == null ? "No confidence" : `${Math.round(field.confidenceScore * 100)}% confidence`}</span>
                    {field.correctedValue != null ? <span className="text-warning">Human corrected</span> : null}
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
        <div>
          <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Validation warnings</p>
          <div className="mt-3 space-y-2">
            {drawer.document.validationWarnings.length === 0 ? (
              <div className="rounded-xl border border-success/30 bg-success/5 p-4 text-sm text-success shadow-glow-success">No validation warnings.</div>
            ) : (
              drawer.document.validationWarnings.map((warning) => (
                <div key={warning.code} className="rounded-xl border border-danger/30 bg-danger/5 p-4 text-sm text-danger shadow-glow-danger">
                  <p className="font-bold">{warning.code}</p>
                  <p className="mt-1 text-danger/80">{warning.message}</p>
                </div>
              ))
            )}
          </div>
        </div>
        <BlockedActionPanel actions={toBlockedActions(drawer.document.blockedActions)} title="Document blocked actions" />
      </div>
    );
  }

  if (drawer.type === "citation") {
    return (
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-2">
          <InfoBlock label="Product line" value={drawer.source.productLine} />
          <InfoBlock label="Version" value={drawer.source.version} />
        </div>
        <SourceCitationList sources={[drawer.source]} />
        <div className="rounded-xl border border-line bg-mist p-4 text-sm leading-7 text-muted">
          This citation supports internal claim handling only. It should not be treated as a final claims decision or a replacement for human review.
        </div>
      </div>
    );
  }

  if (drawer.type === "timeline") {
    return (
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-2">
          <InfoBlock label="Event type" value={drawer.event.eventType} />
          <InfoBlock label="Actor" value={drawer.event.actor} />
          <InfoBlock label="Before status" value={drawer.event.beforeStatus ?? "None"} />
          <InfoBlock label="After status" value={drawer.event.afterStatus ?? "None"} />
        </div>
        <div className="rounded-xl border border-line bg-mist p-4 text-sm leading-7 text-muted font-mono">
          {new Date(drawer.event.timestamp).toLocaleString()}
        </div>
      </div>
    );
  }

  if (drawer.type === "update") {
    return (
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-2">
          <InfoBlock label="Channel" value={drawer.update.channel} />
          <InfoBlock label="Status" value={drawer.update.status} />
          <InfoBlock label="Approval required" value={drawer.update.requiresApproval ? "Yes" : "No"} />
          <InfoBlock label="Delivery" value={drawer.update.deliveryStatus ?? "Not sent"} />
        </div>
        <div className="rounded-xl border border-line bg-mist p-4 text-sm leading-7 text-muted">{drawer.update.messageBody}</div>
      </div>
    );
  }

  if (drawer.type === "communication") {
    return (
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-2">
          <InfoBlock label="Channel" value={drawer.communication.channel} />
          <InfoBlock label="Direction" value={drawer.communication.direction ?? "Outbound"} />
          <InfoBlock label="Status" value={drawer.communication.deliveryStatus} />
          <InfoBlock label="Created" value={new Date(drawer.communication.createdAt).toLocaleString()} />
        </div>
        <div className="rounded-xl border border-line bg-mist p-4 text-sm leading-7 text-muted">{drawer.communication.messageBody}</div>
      </div>
    );
  }

  if (drawer.type === "approval") {
    return (
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-2">
          <InfoBlock label="Status" value={drawer.approval.status} />
          <InfoBlock label="Risk" value={drawer.approval.riskLevel} />
          <InfoBlock label="Updated" value={new Date(drawer.approval.updatedAt).toLocaleString()} />
        </div>
        <div className="rounded-xl border border-line bg-mist p-4 text-sm leading-7 text-muted">{drawer.approval.evidenceSummary}</div>
        {drawer.approval.sourceReferences.length > 0 ? (
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[0.12em] text-muted">Source references</p>
            <div className="mt-2 flex flex-wrap gap-2">
              {drawer.approval.sourceReferences.map((reference) => (
                <StatusChip key={reference} label={reference} tone="info" />
              ))}
            </div>
          </div>
        ) : null}
      </div>
    );
  }

  if (drawer.type === "governedAction") {
    return (
      <ConfirmationAuditNotice action={drawer.action.title} affectedObject={drawer.action.affectedObject} impact={drawer.action.impact}>
        <label className="block text-sm font-medium text-muted">
          Reason
          <textarea
            className="mt-2 min-h-28 w-full rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink outline-none transition-colors focus:border-accent focus:ring-1 focus:ring-accent"
            value={governedReason}
            onChange={(event) => setGovernedReason(event.target.value)}
            placeholder="Record the operational reason for this review action."
          />
        </label>
        <div className="mt-4">
          <GovernedButton variant="primary" icon={<ClipboardCheck className="h-4 w-4" />} disabledReason={governedReason.trim() ? undefined : "A reason is required before this action can be recorded."} onClick={() => submitGovernedAction(drawer.action)}>
            {drawer.action.submitLabel}
          </GovernedButton>
        </div>
      </ConfirmationAuditNotice>
    );
  }

  return (
    <div className="space-y-5">
      <div className="grid gap-3 sm:grid-cols-2">
        <InfoBlock label="Task type" value={drawer.task.taskType} />
        <InfoBlock label="Status" value={drawer.task.status} />
        <InfoBlock label="Assignee" value={drawer.task.assignedToName ?? drawer.task.assignedToUserId ?? "Unassigned"} />
        <InfoBlock label="Due" value={drawer.task.dueAt ? new Date(drawer.task.dueAt).toLocaleString() : "Not set"} />
      </div>
      <div className="rounded-xl border border-line bg-mist p-4 text-sm leading-7 text-muted">
        {drawer.task.description ?? "Workflow detail is limited, but the task remains auditable and human completed."}
      </div>
    </div>
  );
}

function drawerFooter(
  drawer: DrawerState,
  caseId: string,
  post: (path: string, body?: object) => Promise<void>,
  userId: string,
  openCustomerUpdateConfirmation: (action: "approve" | "send", update: CustomerUpdate) => void,
  canManageCustomerCommunications: boolean,
  canSendCustomerUpdates: boolean,
  canReviewWorkflow: boolean,
  canUploadDocuments: boolean,
  getSendAvailability: (update: CustomerUpdate) => ChannelSendAvailability
) {
  if (!drawer) {
    return null;
  }

  if (drawer.type === "update") {
    const sendAvailability = getSendAvailability(drawer.update);

    return (
      <div className="flex flex-wrap gap-2">
        {canManageCustomerCommunications && customerUpdateAllows(drawer.update, "approveCustomerUpdate") ? (
          <button className="rounded-full bg-azure border border-azure/50 px-5 py-2.5 text-sm font-semibold text-white shadow-glow-cyan transition-all hover:brightness-110" onClick={() => openCustomerUpdateConfirmation("approve", drawer.update)}>
            Approve update
          </button>
        ) : null}
        {canSendCustomerUpdates && customerUpdateAllows(drawer.update, "sendCustomerUpdate") ? (
          <button
            className="rounded-full border border-line bg-surface px-5 py-2.5 text-sm font-semibold text-ink hover:text-accent hover:border-accent transition-all disabled:cursor-not-allowed disabled:opacity-50"
            disabled={!sendAvailability.enabled}
            title={sendAvailability.reason || undefined}
            onClick={() => openCustomerUpdateConfirmation("send", drawer.update)}
          >
            Send through configured provider
          </button>
        ) : null}
        {!sendAvailability.enabled ? (
          <p className="basis-full rounded-xl border border-warning/30 bg-warning/10 px-3 py-2 text-xs font-semibold text-warning">
            {sendAvailability.reason}
          </p>
        ) : null}
      </div>
    );
  }

  if (drawer.type === "task" && drawer.task.status !== "Completed" && canReviewWorkflow) {
    return (
      <div className="flex flex-wrap gap-2">
        <button className="rounded-full bg-azure border border-azure/50 px-5 py-2.5 text-sm font-semibold text-white shadow-glow-cyan transition-all hover:brightness-110" onClick={() => post(`/api/v1/workflow/tasks/${drawer.task.taskId}/complete`, {})}>
          Complete task
        </button>
        <Link className="rounded-full border border-line bg-surface px-5 py-2.5 text-sm font-semibold text-ink hover:text-accent hover:border-accent transition-all" href={`/cases/${caseId}`}>
          Open full case
        </Link>
      </div>
    );
  }

  if (drawer.type === "document" && canUploadDocuments) {
    return (
      <Link className="rounded-full border border-line bg-surface px-5 py-2.5 text-sm font-semibold text-ink hover:text-accent hover:border-accent transition-all" href={`/claims/${caseId}/documents`}>
        Open full document flow
      </Link>
    );
  }

  if (drawer.type === "governedAction") {
    return null;
  }

  return null;
}

async function readActionError(response: Response) {
  try {
    const body = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const validationMessage = body.errors
      ? Object.values(body.errors).flat().find((message) => message.trim().length > 0)
      : null;

    return body.message ?? validationMessage ?? `Action failed with HTTP ${response.status}.`;
  } catch {
    return `Action failed with HTTP ${response.status}.`;
  }
}

function slaFromDue(dueAt?: string | null) {
  if (!dueAt) {
    return "OnTrack";
  }

  const dueTime = new Date(dueAt).getTime();
  const now = Date.now();

  if (Number.isNaN(dueTime)) {
    return "OnTrack";
  }

  if (dueTime <= now) {
    return "Breached";
  }

  if (dueTime - now < 12 * 60 * 60 * 1000) {
    return "AtRisk";
  }

  return "OnTrack";
}
