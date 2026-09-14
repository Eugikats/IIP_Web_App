"use client";

import { toast } from "sonner";
import {
  TriangleAlert,
  ArrowLeft,
  CheckCircle2,
  Download,
  FileText,
  Gavel,
  MessageSquareText,
  Send,
  ShieldAlert,
  ShieldCheck,
} from "lucide-react";
import { useState, useEffect, useRef } from "react";
import { Link, useParams } from "@/components/iip/NextRouterCompat";
import { CustomerUpdateGovernedDialog, type PendingCustomerUpdateAction } from "@/components/CustomerUpdateGovernedDialog";
import { CustomerUpdatePanel, type CustomerUpdate } from "@/components/CustomerUpdatePanel";
import { ClaimReviewRequestsPanel } from "@/components/ClaimReviewRequestsPanel";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import PolicyQuestionPanel from "@/components/PolicyQuestionPanel";
import { Badge, ConfidenceBadge, GlassCard, RiskBadge, SectionHeader, SLABadge, StatusBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import CompletenessCard from "@/components/iip/CompletenessCard";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import MotorLifecycle from "@/components/iip/MotorLifecycle";
import { DocumentUploadWidget } from "@/components/iip/DocumentUploadWidget";
import { MarkdownText } from "@/components/iip/MarkdownText";
import { useClaimReview, type ConfirmClaimCorrections } from "@/lib/lovable-parity/data/claimReview";
import { useClaimsSummary } from "@/lib/lovable-parity/data/claimsSummary";
import { useFraudSignals, type FraudSeverity } from "@/lib/lovable-parity/data/fraud";
import { useCaseReview } from "@/lib/lovable-parity/data/caseReview";
import type { ChannelSendAvailability } from "@/lib/providerHealth";
import { useUI } from "@/context/UIStateContext";
import { extractionHref } from "@/lib/workItemLinks";
import { canPreviewPermission, hasPermission } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { claimReference } from "@/lib/format";
import { usePeople } from "@/lib/people";
import AssignClaimControl from "@/components/iip/AssignClaimControl";

const FRAUD_SEVERITY_TONE: Record<FraudSeverity, "error" | "warning" | "violet"> = {
  Investigation: "error",
  Anomaly: "warning",
  Advisory: "violet",
};

const FRAUD_SEVERITY_LABEL: Record<FraudSeverity, string> = {
  Investigation: "Requires investigation",
  Anomaly: "Potential anomaly",
  Advisory: "Advisory signal",
};

const FRAUD_STATUS_LABEL: Record<string, string> = {
  NotTriaged: "Not yet reviewed",
  Triaged: "Reviewed",
  UnderInvestigation: "Under investigation",
  OutcomeRecorded: "Outcome recorded",
  Closed: "Closed",
};

export default function ClaimReview() {
  const { id } = useParams();
  return <ClaimReviewLive claimId={String(id ?? "")} />;
}

function ClaimReviewLive({ claimId }: { claimId: string }) {
  const {
    detail,
    timeline,
    customerUpdates,
    reviewRequests,
    loadState,
    drafting,
    requestingApproval,
    exporting,
    confirming,
    draftCustomerUpdate,
    draftDocumentFollowUp,
    draftDecisionOutcomeUpdate,
    approveCustomerUpdate,
    sendCustomerUpdate,
    acknowledgeReviewRequest,
    resolveReviewRequest,
    exportEvidencePack,
    downloadExportedAuditPack,
    requestSupervisorReview,
    confirmClaim,
    reload,
  } = useClaimReview(claimId);
  const loading = loadState.status === "loading";
  const error = "error" in loadState ? loadState.error : null;
  const { roleDef } = useUI();
  const { summary: claimsSummary, loading: summarizing, error: summaryError, generate: generateSummary } = useClaimsSummary(claimId, roleDef.key);
  const { signals: fraudSignals, loading: fraudLoading, error: fraudError } = useFraudSignals();
  const caseFraudSignals = fraudSignals.filter((signal) => signal.relatedCaseIds.includes(claimId));
  const { review: persistedReview, loading: reviewLoading, load: loadReview } = useCaseReview(claimId, roleDef.key);
  useEffect(() => { if (claimId) void loadReview(); }, [claimId, loadReview]);
  // Freshly generated summary takes precedence; fall back to the persisted one loaded on mount.
  const displayedSummary = claimsSummary ?? persistedReview;
  const summaryIsLoading = summarizing || reviewLoading;
  const { runtimeContext, parityMode } = useRuntimeContext();
  const { describe } = usePeople();

  // Every new claim is reviewed by the claims agent in the background the moment it is submitted.
  // If the page opens before that review lands, check back for a minute rather than looking empty.
  const [awaitingAutomaticReview, setAwaitingAutomaticReview] = useState(false);
  const pollAttempts = useRef(0);
  const hasReview = Boolean(displayedSummary);
  const claimStatus = detail?.status;
  useEffect(() => { pollAttempts.current = 0; }, [claimId]);
  useEffect(() => {
    if (!claimId || hasReview || parityMode) {
      setAwaitingAutomaticReview(false);
      return;
    }
    if (claimStatus && !["New", "Assigned"].includes(claimStatus)) return;
    setAwaitingAutomaticReview(true);
    const timer = window.setInterval(() => {
      pollAttempts.current += 1;
      if (pollAttempts.current > 12) {
        window.clearInterval(timer);
        setAwaitingAutomaticReview(false);
        return;
      }
      void loadReview();
    }, 6000);
    return () => window.clearInterval(timer);
  }, [claimId, hasReview, parityMode, claimStatus, loadReview]);
  // Gates the upload control on the signed-in user's real permission, never on the preview role
  // switcher — a role without UploadDocument (e.g. ITAdmin, Compliance) must not see a button
  // that the server will refuse with 403.
  const canUploadDocuments = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "UploadDocument")
    : hasPermission(runtimeContext?.permissions ?? [], "UploadDocument");
  // "Draft customer update" and "Draft document follow-up" both call
  // POST /cases/{id}/customer-updates/draft, which requires ManageCustomerCommunications —
  // a role that can view the claim (e.g. Compliance) does not necessarily hold it.
  const canDraftCustomerUpdates = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "ManageCustomerCommunications")
    : hasPermission(runtimeContext?.permissions ?? [], "ManageCustomerCommunications");
  // "Export claim pack" calls POST /compliance/cases/{id}/audit-pack/export, which requires
  // ExportEvidencePack — most claim-viewing roles (ClaimsOfficer, CustomerService, Broker) do
  // not hold it; only ClaimsSupervisor and Compliance do.
  const canExportEvidencePack = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "ExportEvidencePack")
    : hasPermission(runtimeContext?.permissions ?? [], "ExportEvidencePack");
  // CustomerUpdatePanel's allowedActions/blockedActions reflect the update's workflow state
  // only (see CaseActions.cs — it does not know the caller's permission); approve/send must be
  // gated here too, on the same permissions POST /customer-updates/{id}/approve and /send
  // actually require server-side.
  const canApproveCustomerUpdate = canDraftCustomerUpdates; // both require ManageCustomerCommunications
  const canSendCustomerUpdate = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "SendCustomerUpdate")
    : hasPermission(runtimeContext?.permissions ?? [], "SendCustomerUpdate");
  const [pendingCustomerUpdateAction, setPendingCustomerUpdateAction] = useState<PendingCustomerUpdateAction | null>(null);
  const [exportDialogOpen, setExportDialogOpen] = useState(false);
  const [reviewBusyId, setReviewBusyId] = useState<string | null>(null);
  const [confirmDialogOpen, setConfirmDialogOpen] = useState(false);
  const [confirmCorrections, setConfirmCorrections] = useState<ConfirmClaimCorrections>({});

  async function handleDraftCustomerUpdate() {
    const res = await draftCustomerUpdate();
    if (res.ok) {
      toast.success("Customer update drafted", {
        description: "The draft is pending human approval before send.",
      });
    } else {
      toast.error(res.message ?? "Could not draft update");
    }
  }

  async function handleDraftDocumentFollowUp() {
    const res = await draftDocumentFollowUp();
    if (res.ok) {
      toast.success("Document follow-up drafted", {
        description: "Review the customer update below before approval or send.",
      });
    } else {
      toast.error(res.message ?? "Could not draft document follow-up");
    }
  }

  async function handleDraftDecisionOutcomeUpdate() {
    const res = await draftDecisionOutcomeUpdate();
    if (res.ok) {
      toast.success("Decision-outcome update drafted", {
        description: "The draft tells the customer to expect contact — it never states the decision itself. Review it below before approval or send.",
      });
    } else {
      toast.error(res.message ?? "Could not draft decision-outcome update");
    }
  }

  function unavailable(label: string) {
    toast("Not configured", {
      description: `${label} is not wired to a backend workflow yet.`,
    });
  }

  async function handleSupervisorReview() {
    const res = await requestSupervisorReview();
    if (res.ok) {
      toast.success("Supervisor review requested", {
        description: "The approval is now in the human review queue.",
      });
    } else {
      toast.error(res.message ?? "Could not request supervisor review");
    }
  }

  function sendAvailability(update: CustomerUpdate): ChannelSendAvailability {
    const channel = update.channel.toLowerCase();
    if (channel === "email" && !detail?.customerEmail) {
      return { enabled: false, reason: "This claim has no customer email recipient.", recipientField: "email" };
    }

    if ((channel === "sms" || channel === "whatsapp") && !detail?.customerPhone) {
      return { enabled: false, reason: "This claim has no customer phone recipient.", recipientField: "phone" };
    }

    return { enabled: true, reason: "", recipientField: channel === "email" ? "email" : channel === "sms" || channel === "whatsapp" ? "phone" : null };
  }

  async function handleCustomerUpdateConfirm(auditReason: string) {
    if (!pendingCustomerUpdateAction) return;

    const action = pendingCustomerUpdateAction;
    const res = action.action === "approve"
      ? await approveCustomerUpdate(action.update.updateId, auditReason)
      : await sendCustomerUpdate(action.update, auditReason);

    if (res.ok) {
      toast.success(action.action === "approve" ? "Customer update approved" : "Customer update sent");
      setPendingCustomerUpdateAction(null);
    } else {
      toast.error(res.message ?? "Customer update action failed");
    }
  }

  async function handleAcknowledgeReviewRequest(requestId: string) {
    setReviewBusyId(requestId);
    try {
      const res = await acknowledgeReviewRequest(requestId);
      if (res.ok) {
        toast.success("Re-review request acknowledged", {
          description: "Workflow state only — this does not settle, deny, or pay the claim.",
        });
      } else {
        toast.error(res.message ?? "Could not acknowledge re-review request");
      }
    } finally {
      setReviewBusyId(null);
    }
  }

  async function handleResolveReviewRequest(requestId: string, resolutionNote: string) {
    if (resolutionNote.trim().length < 3) {
      toast.error("Record a short resolution note before closing the re-review request.");
      return;
    }
    setReviewBusyId(requestId);
    try {
      const res = await resolveReviewRequest(requestId, resolutionNote);
      if (res.ok) {
        toast.success("Re-review request resolved", {
          description: "Human workflow state recorded. Claim outcome still requires a separate governed decision.",
        });
      } else {
        toast.error(res.message ?? "Could not resolve re-review request");
      }
    } finally {
      setReviewBusyId(null);
    }
  }

  async function handleExportEvidencePack(auditReason: string) {
    const res = await exportEvidencePack(auditReason);
    if (!res.ok) {
      toast.error(res.message ?? "Evidence-pack export failed");
      return;
    }
    setExportDialogOpen(false);
    if (!res.objectKey) {
      toast.success("Claim pack exported", { description: "Evidence pack artifact stored." });
      return;
    }
    const downloadResult = await downloadExportedAuditPack(res.objectKey);
    toast.success("Claim pack exported", {
      description: downloadResult.ok
        ? "Download started."
        : `Stored as ${res.objectKey}, but the download could not start: ${downloadResult.message ?? "unknown error"}.`,
    });
  }

  function openConfirmDialog() {
    if (!detail) return;
    setConfirmCorrections({
      productLine: detail.productLine,
      claimType: detail.claimType,
      incidentDate: detail.incidentDate,
      incidentDescription: detail.incidentDescription,
    });
    setConfirmDialogOpen(true);
  }

  async function handleConfirmClaim(auditReason: string) {
    const res = await confirmClaim(confirmCorrections, auditReason);
    if (res.ok) {
      toast.success("Claim confirmed", {
        description: "The SLA clock has started and this claim now counts in officer metrics.",
      });
      setConfirmDialogOpen(false);
    } else {
      toast.error(res.message ?? "Could not confirm this claim");
    }
  }

  const crumb = (
    // text-dls-text-muted measures 4.47:1 on this page's bg-dls-bg-page at 12px (gate-17);
    // text-dls-text-secondary is the platform's existing token for labels/captions and clears
    // 6.23:1 here, so it fits a breadcrumb without introducing a new color.
    <div className="mb-4 flex items-center gap-2 text-[12px] text-dls-text-secondary">
      <Link to="/app/claims" className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-tight hover:text-dls-text-primary">
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden="true" /> Claims
      </Link>
      <span>/</span>
      <span className="text-dls-text-primary">{claimReference(claimId)}</span>
    </div>
  );

  if (loading) {
    return (
      <>
        {crumb}
        <GlassCard><LoadingSkeleton rows={5} /></GlassCard>
      </>
    );
  }

  if (error || !detail) {
    return (
      <>
        {crumb}
        <GlassCard>
          <ErrorStateSurface message={error?.message ?? "No claim found for this workspace."} correlationId={error?.correlationId} onRetry={error?.retryable ? () => void reload() : undefined} />
          <div className="flex flex-wrap justify-center gap-2 px-5 pb-4">
            <Link to="/app/claims" className="dls-focus-outline inline-flex items-center justify-center gap-1.5 rounded-dls-control border border-dls-border-input bg-dls-surface px-4 py-[9px] text-[13px] font-bold text-dls-text-primary hover:bg-dls-bg-page">
              Return to claims
            </Link>
          </div>
          {error ? <p className="px-5 pb-4 text-center text-[11px] text-dls-text-muted">Attempted {new Date(error.occurredAt).toLocaleString()}</p> : null}
        </GlassCard>
      </>
    );
  }

  const completeness = detail.completenessScore > 1 ? detail.completenessScore / 100 : detail.completenessScore;
  const sla = detail.slaDueAt ? (new Date(detail.slaDueAt).getTime() < Date.now() ? "Breached" : "Normal") : "Normal";
  const documentItems = detail.documents.map((document) => ({
    label: document.documentType,
    present: document.processingStatus !== "Missing" && document.processingStatus !== "MissingDocuments",
    lowConfidence: document.classificationConfidence > 0 && document.classificationConfidence < 0.75,
  }));
  const firstExtractionHref = extractionHref(claimId, detail.documents[0]?.documentId);

  return (
    <>
      {crumb}
      <SectionHeader
        eyebrow={`Case Review · ${detail.policyNumber}`}
        eyebrowClassName="text-dls-text-secondary"
        title={`${claimReference(detail.claimId)} · ${detail.customerName}`}
        sub={detail.incidentDescription}
        action={
          <div className="flex flex-wrap items-center gap-2">
            {detail.requiresConfirmation ? <Badge tone="warning">Needs confirmation</Badge> : null}
            <StatusBadge status={detail.status} />
            <SLABadge sla={sla} />
            <RiskBadge risk={detail.riskLevel} />
          </div>
        }
      />

      <div className="mb-4">
        <GuardrailBanner text="AI prepares this claim's work: extractions, summaries, drafts, and advisory signals. Settlement, denial, repudiation, payment, and fraud determination remain with the reviewing officer." />
      </div>

      {detail.requiresConfirmation ? (
        <div className="mb-4 rounded-dls-card border border-dls-warning-dot bg-dls-warning-tint p-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="flex items-start gap-2">
              <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-dls-warning-dot" aria-hidden="true" />
              <div>
                <div className="text-[13px] font-bold text-dls-text-primary">This claim needs confirmation</div>
                <p className="mt-1 max-w-2xl text-[12.5px] leading-snug text-dls-text-secondary">
                  IIP created this claim automatically from an identified {detail.channel} sender. The details below are
                  a best-effort guess from the raw message — review and correct them, then confirm to start the SLA
                  clock. Until confirmed, this claim is excluded from SLA and attention metrics.
                </p>
              </div>
            </div>
            <Button variant="secondary" icon={<CheckCircle2 />} disabled={confirming} onClick={openConfirmDialog} className="shrink-0">
              Confirm claim
            </Button>
          </div>
        </div>
      ) : null}

      <div className="mb-4">
        <MotorLifecycle status={detail.status} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="space-y-4 lg:col-span-2">
          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-4">
              <div className="text-[14px] font-bold text-dls-text-primary">Human review actions</div>
              <div className="text-[11.5px] text-dls-text-muted">Only wired actions execute. Unwired actions report Not configured.</div>
            </div>
            <div className="grid gap-2.5 sm:grid-cols-2">
              {canDraftCustomerUpdates ? (
                <Button variant="secondary" icon={<MessageSquareText />} disabled={drafting} onClick={handleDraftCustomerUpdate}>
                  Draft customer update
                </Button>
              ) : null}
              <Button variant="secondary" icon={<ShieldCheck />} disabled={requestingApproval} onClick={handleSupervisorReview}>
                Request supervisor review
              </Button>
              <UnavailableAction icon={CheckCircle2} label="Mark evidence reviewed" onClick={unavailable} />
              {canDraftCustomerUpdates ? (
                <Button variant="secondary" icon={<Send />} disabled={drafting} onClick={handleDraftDocumentFollowUp}>
                  Draft document follow-up
                </Button>
              ) : null}
              {canDraftCustomerUpdates ? (
                <Button variant="secondary" icon={<Gavel />} disabled={drafting} onClick={handleDraftDecisionOutcomeUpdate}>
                  Draft decision outcome update
                </Button>
              ) : null}
              {canExportEvidencePack ? (
                <Button variant="secondary" icon={<Download />} disabled={exporting} onClick={() => setExportDialogOpen(true)}>
                  Export claim pack
                </Button>
              ) : null}
            </div>
            {!canDraftCustomerUpdates || !canExportEvidencePack ? (
              <div className="mt-2.5 text-[11.5px] text-dls-text-muted">
                {!canDraftCustomerUpdates && !canExportEvidencePack
                  ? "Your role does not include drafting customer updates or exporting the claim pack."
                  : !canDraftCustomerUpdates
                    ? "Your role does not include drafting customer updates."
                    : "Your role does not include exporting the claim pack."}
              </div>
            ) : null}
          </GlassCard>

          <PolicyQuestionPanel
            caseId={detail.claimId}
            productLine={detail.productLine}
            title="Ask a policy question about this claim"
            sub={`Answers are grounded in approved ${detail.productLine} knowledge sources only, and every question is logged against this claim.`}
          />

          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-4">
              <div>
                <div className="text-[14px] font-bold text-dls-text-primary">Customer updates</div>
                <div className="mt-1 text-[11.5px] text-dls-text-muted">
                  Backend action states control approval and send availability.
                </div>
              </div>
            </div>
            <CustomerUpdatePanel
              updates={customerUpdates}
              onApprove={canApproveCustomerUpdate ? (update) => setPendingCustomerUpdateAction({ action: "approve", update }) : undefined}
              onSend={canSendCustomerUpdate ? (update) => {
                const availability = sendAvailability(update);
                setPendingCustomerUpdateAction({
                  action: "send",
                  update,
                  recipient: availability.recipientField === "email" ? detail.customerEmail : availability.recipientField === "phone" ? detail.customerPhone : null,
                  availability,
                });
              } : undefined}
              getSendAvailability={sendAvailability}
            />
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-4">
              <div>
                <div className="text-[14px] font-bold text-dls-text-primary">Claimant re-review requests</div>
                <div className="mt-1 text-[11.5px] text-dls-text-muted">
                  Acknowledge or resolve contestability requests. This updates workflow state only — it does not settle, deny, or pay the claim.
                </div>
              </div>
              {reviewRequests.some((request) => request.status === "Open" || request.status === "Acknowledged") ? (
                <Badge tone="warning">
                  {reviewRequests.filter((request) => request.status === "Open" || request.status === "Acknowledged").length} open
                </Badge>
              ) : null}
            </div>
            <ClaimReviewRequestsPanel
              requests={reviewRequests}
              busyId={reviewBusyId}
              onAcknowledge={handleAcknowledgeReviewRequest}
              onResolve={handleResolveReviewRequest}
            />
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-4">
              <div className="text-[14px] font-bold text-dls-text-primary">Documents received</div>
              {firstExtractionHref ? <Link to={firstExtractionHref} className="dls-focus-outline rounded-dls-tight text-[12px] font-semibold text-dls-primary hover:underline">
                Open extraction review
              </Link> : null}
            </div>
            {detail.documents.length === 0 ? (
              <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">
                No documents uploaded to this claim yet.
              </div>
            ) : (
              <div className="grid gap-2 sm:grid-cols-2">
                {detail.documents.map((document) => (
                  <div key={document.documentId} className="flex items-center gap-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
                    <div className="grid h-9 w-9 place-items-center rounded-dls-control bg-dls-info-tint text-dls-info-text">
                      <FileText className="h-4 w-4" aria-hidden="true" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-[13px] font-semibold text-dls-text-primary">{document.documentType}</div>
                      <div className="text-[11px] text-dls-text-muted">{document.processingStatus}</div>
                    </div>
                    {document.classificationConfidence > 0 ? (
                      <ConfidenceBadge value={document.classificationConfidence > 1 ? document.classificationConfidence / 100 : document.classificationConfidence} />
                    ) : (
                      <Badge tone="neutral">Not scored</Badge>
                    )}
                  </div>
                ))}
              </div>
            )}
            {canUploadDocuments ? (
              <DocumentUploadWidget caseId={claimId} role={roleDef.key} onUploaded={reload} />
            ) : (
              <div className="mt-3 text-[11.5px] text-dls-text-muted">
                Your role does not include document upload.
              </div>
            )}
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-4">
              <div>
                <div className="text-[14px] font-bold text-dls-text-primary">Claim summary</div>
                <div className="mt-1 text-[11.5px] text-dls-text-muted">
                  Prepared by the claims agent from this claim&apos;s facts, documents, policy and approved knowledge. It prepares the claim for a decision and is not a decision itself.
                </div>
              </div>
              <Button variant="secondary" icon={<MessageSquareText />} disabled={summarizing} onClick={() => { void generateSummary(); }} className="shrink-0">
                {displayedSummary ? "Regenerate summary" : "Generate summary"}
              </Button>
            </div>
            {summaryError ? (
              <div className="flex items-start gap-2 rounded-dls-control border border-dls-warning-dot bg-dls-warning-tint px-3 py-2 text-[12.5px] text-dls-warning-text">
                <TriangleAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                <span>{summaryError}</span>
              </div>
            ) : summaryIsLoading && !displayedSummary && !awaitingAutomaticReview ? (
              <LoadingSkeleton rows={3} />
            ) : displayedSummary ? (
              <div className="space-y-3">
                <MarkdownText className="text-[13px] text-dls-text-secondary leading-relaxed" content={displayedSummary.summary} />
                {displayedSummary.openQuestions.length > 0 ? (
                  <div>
                    <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Open questions</div>
                    <ul className="space-y-1 text-[12.5px] text-dls-text-secondary">
                      {displayedSummary.openQuestions.map((question) => (
                        <li key={question}>- {question}</li>
                      ))}
                    </ul>
                  </div>
                ) : null}
                {displayedSummary.riskSignals?.length > 0 ? (
                  <div>
                    <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Risk signals</div>
                    <ul className="space-y-2">
                      {displayedSummary.riskSignals.map((signal, i) => (
                        <li key={i} className="flex items-start gap-2 text-[12.5px] text-dls-text-secondary">
                          <Badge tone={signal.category === "Fraud" ? "warning" : "neutral"} className="mt-0.5 shrink-0">
                            {signal.category === "Fraud" ? "Fraud signal" : "Underwriting"}
                          </Badge>
                          <span>{signal.note}</span>
                        </li>
                      ))}
                    </ul>
                    <div className="mt-2 text-[11px] text-dls-text-muted">
                      Advisory observations only. A person decides what, if anything, to do.
                    </div>
                  </div>
                ) : null}
                {displayedSummary.knowledgeSources.length > 0 ? (
                  <div>
                    <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Sources</div>
                    <div className="space-y-1">
                      {displayedSummary.knowledgeSources.map((source) => (
                        <div key={source.chunkId} className="flex items-center gap-2 text-[12px] text-dls-text-muted">
                          <FileText className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                          {source.label}: {source.documentName}
                        </div>
                      ))}
                    </div>
                  </div>
                ) : null}
              </div>
            ) : (
              <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">
                {awaitingAutomaticReview
                  ? "The claims agent reviews every new claim as soon as it arrives. Its review usually appears here within a minute."
                  : "No review yet. Use Generate summary to ask the claims agent for one."}
              </div>
            )}
          </GlassCard>

          <GlassCard>
            <div className="mb-3 text-[14px] font-bold text-dls-text-primary">Motor FNOL details</div>
            {detail.motorFnolDetails ? (
              <div className="grid gap-2 sm:grid-cols-2">
                <DetailTile label="Vehicle registration" value={detail.motorFnolDetails.vehicleRegistration} />
                <DetailTile label="Accident time" value={detail.motorFnolDetails.accidentTime} />
                <DetailTile label="Accident location" value={detail.motorFnolDetails.accidentLocation} />
                <DetailTile label="Driver name" value={detail.motorFnolDetails.driverName} />
                <DetailTile label="Driving permit" value={detail.motorFnolDetails.drivingPermitNumber} />
                <DetailTile label="Third party" value={detail.motorFnolDetails.thirdPartyInvolved} />
                <DetailTile label="Injury indicator" value={detail.motorFnolDetails.injuryIndicator} />
                <DetailTile label="Police report" value={detail.motorFnolDetails.policeReportStatus} />
                <DetailTile label="Preferred channel" value={detail.motorFnolDetails.preferredCommunicationChannel} />
                <DetailTile label="Document notes" value={detail.motorFnolDetails.uploadedDocuments} />
              </div>
            ) : (
              <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">
                This claim has no structured Motor FNOL details yet.
              </div>
            )}
          </GlassCard>

          <GlassCard>
            <div className="mb-4 text-[14px] font-bold text-dls-text-primary">Activity timeline</div>
            {timeline.length === 0 ? (
              <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">
                No recorded activity yet. Governed actions and AI runs appear here after they are written by the backend.
              </div>
            ) : (
              <div className="relative pl-5">
                <div className="absolute bottom-1 left-1.5 top-1 w-px bg-dls-border-subtle" />
                {timeline.map((event) => (
                  <div key={event.eventId} className="relative pb-4">
                    <div className="absolute -left-[9px] top-1 h-2 w-2 rounded-full bg-dls-info-dot ring-2 ring-dls-surface" />
                    <div className="text-[12px] text-dls-text-muted">
                      {new Date(event.timestamp).toLocaleString()} · {event.actor}
                    </div>
                    <div className="mt-0.5 text-[13px] text-dls-text-primary">{event.summary || event.eventType}</div>
                  </div>
                ))}
              </div>
            )}
          </GlassCard>
        </div>

        <div className="space-y-4">
          <GlassCard>
            <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Policy &amp; customer</div>
            <div className="mt-2 space-y-2 text-[13px]">
              <Row k="Customer" v={detail.customerName} />
              <Row k="Policy" v={`${detail.policyNumber} · ${detail.productLine}`} />
              <Row k="Claim type" v={detail.claimType} />
              <Row k="Channel" v={detail.channel} />
              <Row k="Incident date" v={detail.incidentDate} />
              <Row k="Assigned to" v={describe(detail.assignedUserId, "Nobody yet")} />
              <AssignClaimControl caseId={detail.claimId} currentAssigneeId={detail.assignedUserId} onAssigned={reload} />
              {detail.customerEmail ? <Row k="Email" v={detail.customerEmail} /> : null}
              {detail.customerPhone ? <Row k="Phone" v={detail.customerPhone} /> : null}
            </div>
          </GlassCard>

          <CompletenessCard
            score={completeness}
            items={documentItems.length > 0 ? documentItems : [{ label: "Claim documents", present: false }]}
          />

          {!fraudLoading && !fraudError && caseFraudSignals.length > 0 ? (
            <GlassCard>
              <div className="mb-3 flex items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <ShieldAlert className="h-4 w-4 text-dls-warning-dot" aria-hidden="true" />
                  <div className="text-[14px] font-bold text-dls-text-primary">Fraud &amp; underwriting signals</div>
                </div>
                <Badge tone="warning">{caseFraudSignals.length}</Badge>
              </div>
              <div className="space-y-3">
                {caseFraudSignals.map((signal) => (
                  <div key={signal.signalId} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
                    <div className="flex items-start justify-between gap-2">
                      <div className="text-[13px] font-semibold text-dls-text-primary">{signal.title}</div>
                      <Badge tone={FRAUD_SEVERITY_TONE[signal.severity]}>{FRAUD_SEVERITY_LABEL[signal.severity]}</Badge>
                    </div>
                    <p className="mt-1 text-[12px] leading-snug text-dls-text-secondary">{signal.detail}</p>
                    {signal.evidence.length > 0 ? (
                      <ul className="mt-2 space-y-0.5 text-[11px] text-dls-text-muted">
                        {signal.evidence.slice(0, 3).map((item) => (
                          <li key={item}>· {item}</li>
                        ))}
                      </ul>
                    ) : null}
                    <div className="mt-2 flex items-center justify-between gap-2">
                      <span className="text-[11px] text-dls-text-muted">{FRAUD_STATUS_LABEL[signal.disposition.status] ?? signal.disposition.status}</span>
                      <Link to="/app/fraud" className="dls-focus-outline rounded-dls-tight text-[11.5px] font-semibold text-dls-primary hover:underline">
                        Open in Fraud Intelligence →
                      </Link>
                    </div>
                  </div>
                ))}
              </div>
            </GlassCard>
          ) : null}

          <div className="rounded-dls-card border border-dls-warning-dot bg-dls-warning-tint p-4">
            <div className="mb-1 flex items-center gap-2">
              <TriangleAlert className="h-3.5 w-3.5 text-dls-warning-dot" aria-hidden="true" />
              <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-warning-text">Advisory boundary</div>
            </div>
            <p className="text-[13px] leading-snug text-dls-text-secondary">
              Fraud and risk outputs are advisory only. The platform must show backend evidence before any human investigation step proceeds.
            </p>
          </div>
        </div>
      </div>
      <CustomerUpdateGovernedDialog
        pendingAction={pendingCustomerUpdateAction}
        onClose={() => setPendingCustomerUpdateAction(null)}
        onConfirm={handleCustomerUpdateConfirm}
      />
      <GovernedActionDialog
        open={exportDialogOpen}
        title="Export claim evidence pack"
        target={`Claim ${claimReference(detail.claimId)}`}
        impact="Creates a durable JSON evidence-pack artifact from sanitized audit, document, assistant-action, approval, and customer-update records for this claim."
        auditNotice="Evidence-pack export is a compliance-sensitive action. The exported artifact is stored through the configured evidence storage provider and the export event is written to the audit trail."
        confirmLabel="Export claim pack"
        evidence={
          <div className="rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-4 text-sm leading-6 text-dls-text-muted">
            <dl className="grid gap-2 sm:grid-cols-2">
              <ExportEvidenceItem label="Claim status" value={detail.status} />
              <ExportEvidenceItem label="Documents" value={String(detail.documents.length)} />
              <ExportEvidenceItem label="Customer updates" value={String(customerUpdates.length)} />
              <ExportEvidenceItem label="Timeline events" value={String(timeline.length)} />
            </dl>
          </div>
        }
        onClose={() => setExportDialogOpen(false)}
        onConfirm={handleExportEvidencePack}
      />
      <GovernedActionDialog
        open={confirmDialogOpen}
        title="Confirm claim"
        target={`Claim ${claimReference(detail.claimId)}`}
        impact="Starts this claim's SLA clock and includes it in officer SLA and attention metrics. Correct any details IIP guessed from the raw channel message before confirming."
        auditNotice="Confirming this claim is recorded in the audit trail. Both the version before you confirmed it and the version after are kept."
        confirmLabel="Confirm claim"
        tone="primary"
        evidence={
          <div className="space-y-3 rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-4">
            <label htmlFor="confirm-product-line" className="block text-sm font-semibold text-dls-text-primary">
              Product line
              <input
                id="confirm-product-line"
                aria-label="Product line"
                className="mt-1 w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-sm text-dls-text-primary outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                value={confirmCorrections.productLine ?? ""}
                onChange={(event) => setConfirmCorrections((prev) => ({ ...prev, productLine: event.target.value }))}
              />
            </label>
            <label htmlFor="confirm-claim-type" className="block text-sm font-semibold text-dls-text-primary">
              Claim type
              <input
                id="confirm-claim-type"
                aria-label="Claim type"
                className="mt-1 w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-sm text-dls-text-primary outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                value={confirmCorrections.claimType ?? ""}
                onChange={(event) => setConfirmCorrections((prev) => ({ ...prev, claimType: event.target.value }))}
              />
            </label>
            <label htmlFor="confirm-incident-description" className="block text-sm font-semibold text-dls-text-primary">
              Incident description
              <textarea
                id="confirm-incident-description"
                aria-label="Incident description"
                className="mt-1 min-h-20 w-full resize-y rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-sm text-dls-text-primary outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                value={confirmCorrections.incidentDescription ?? ""}
                onChange={(event) => setConfirmCorrections((prev) => ({ ...prev, incidentDescription: event.target.value }))}
              />
            </label>
          </div>
        }
        onClose={() => setConfirmDialogOpen(false)}
        onConfirm={handleConfirmClaim}
      />
    </>
  );
}

function UnavailableAction({
  icon: Icon,
  label,
  onClick,
}: {
  icon: typeof ShieldCheck;
  label: string;
  onClick: (label: string) => void;
}) {
  return (
    <Button variant="secondary" icon={<Icon />} onClick={() => onClick(label)}>
      {label}
    </Button>
  );
}

function Row({ k, v }: { k: string; v: string }) {
  return (
    <div className="flex items-baseline justify-between gap-2">
      <span className="text-[11.5px] text-dls-text-muted">{k}</span>
      <span className="text-right text-dls-text-primary">{v}</span>
    </div>
  );
}

function DetailTile({ label, value }: { label: string; value: string | null }) {
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
      <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{label}</div>
      <div className="mt-1 text-[13px] text-dls-text-primary">{value || "Not captured"}</div>
    </div>
  );
}

function ExportEvidenceItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-[10px] font-bold uppercase tracking-[0.18em] text-dls-text-muted">{label}</dt>
      <dd className="mt-1 break-words font-mono font-semibold text-dls-text-primary">{value}</dd>
    </div>
  );
}
