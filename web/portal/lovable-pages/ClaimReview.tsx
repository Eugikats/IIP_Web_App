"use client";

import { toast } from "sonner";
import {
  TriangleAlert,
  ArrowLeft,
  CheckCircle2,
  FileText,
  MessageSquareText,
  ShieldAlert,
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
import ClaimProcessStages from "@/components/iip/ClaimProcessStages";
import ClaimDecisionCard, { type DecisionAction } from "@/components/iip/ClaimDecisionCard";
import { DocumentUploadWidget } from "@/components/iip/DocumentUploadWidget";
import { MarkdownText } from "@/components/iip/MarkdownText";
import { useClaimReview, type ConfirmClaimCorrections } from "@/lib/lovable-parity/data/claimReview";
import { useClaimsSummary } from "@/lib/lovable-parity/data/claimsSummary";
import { useClaimChecklist } from "@/lib/lovable-parity/data/claimChecklist";
import { useFraudSignals, type FraudSeverity } from "@/lib/lovable-parity/data/fraud";
import { useCaseReview } from "@/lib/lovable-parity/data/caseReview";
import type { ChannelSendAvailability } from "@/lib/providerHealth";
import { useUI } from "@/context/UIStateContext";
import { extractionHref } from "@/lib/workItemLinks";
import { documentContractLabel } from "@/lib/documentContracts";
import { canPreviewPermission, hasPermission } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { channelLabel, claimReference, documentTypeLabel, formatDate, humanizeIdentifier } from "@/lib/format";
import { usePeople } from "@/lib/people";
import AssignClaimControl from "@/components/iip/AssignClaimControl";
import { getFileConnectionStatus, type FileConnectionStatus } from "@/lib/files/fileConnections";
import { ClaimDetailsCard } from "@/components/iip/ClaimDetailsCard";
import { PrivacyConsentLine } from "@/components/iip/PrivacyConsentLine";
import { FieldProposalsCard } from "@/components/iip/FieldProposalsCard";
import { AuthenticityCard } from "@/components/iip/AuthenticityCard";
import { ClaimAssessmentCard } from "@/components/iip/ClaimAssessmentCard";
import { ClaimStatusChange } from "@/components/iip/ClaimStatusChange";
import { SendClaimLink } from "@/components/iip/SendClaimLink";
import { ClaimsPackDownload } from "@/components/iip/ClaimsPackDownload";

const POLICY_SOURCE_LABEL: Record<string, string> = {
  Manual: "Entered manually",
  FileImport: "Loaded from file",
  PartnerApi: "Partner API",
  ExternalLookup: "External lookup",
};

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

const LANGUAGE_NAMES: Record<string, string> = { lg: "Luganda", sw: "Kiswahili" };

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
    resolving,
    recordingSatisfaction,
    draftCustomerUpdate,
    draftDocumentFollowUp,
    draftDecisionOutcomeUpdate,
    approveCustomerUpdate,
    sendCustomerUpdate,
    acknowledgeReviewRequest,
    resolveReviewRequest,
    exportEvidencePack,
    downloadExportedAuditPack,
    saveExportedAuditPack,
    requestSupervisorReview,
    confirmClaim,
    resolveClaim,
    recordSatisfactionResponse,
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
  const { checklist, error: checklistError } = useClaimChecklist(claimId, roleDef.key, detail?.documents.length);
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
  // switcher, a role without UploadDocument (e.g. ITAdmin, Compliance) must not see a button
  // that the server will refuse with 403.
  const canUploadDocuments = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "UploadDocument")
    : hasPermission(runtimeContext?.permissions ?? [], "UploadDocument");
  // "Draft customer update" and "Draft document follow-up" both call
  // POST /cases/{id}/customer-updates/draft, which requires ManageCustomerCommunications,
  // a role that can view the claim (e.g. Compliance) does not necessarily hold it.
  const canDraftCustomerUpdates = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "ManageCustomerCommunications")
    : hasPermission(runtimeContext?.permissions ?? [], "ManageCustomerCommunications");
  // "Export claim pack" calls POST /compliance/cases/{id}/audit-pack/export, which requires
  // ExportEvidencePack, most claim-viewing roles (ClaimsOfficer, CustomerService, Broker) do
  // not hold it; only ClaimsSupervisor and Compliance do.
  // Editing the answers to a claim's questions needs the same permission as filing a claim.
  const [detailsVersion, setDetailsVersion] = useState(0);
  const [statusNotice, setStatusNotice] = useState<string | null>(null);
  const canEditClaimDetails = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "CreateClaim")
    : hasPermission(runtimeContext?.permissions ?? [], "CreateClaim");
  // The review score and recommendation are limited to the roles that already see fraud signals, and
  // responding to the recommendation is the permission that overrides an AI recommendation.
  const canViewAssessment = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "ViewFraudSignals")
    : hasPermission(runtimeContext?.permissions ?? [], "ViewFraudSignals");
  const canRespondToAssessment = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "OverrideAiRecommendation")
    : hasPermission(runtimeContext?.permissions ?? [], "OverrideAiRecommendation");
  // Sending the customer a link to their claim is messaging the customer, the same permission as sending an update.
  const canSendClaimLink = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "SendCustomerUpdate")
    : hasPermission(runtimeContext?.permissions ?? [], "SendCustomerUpdate");
  const canExportEvidencePack = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "ExportEvidencePack")
    : hasPermission(runtimeContext?.permissions ?? [], "ExportEvidencePack");
  // CustomerUpdatePanel's allowedActions/blockedActions reflect the update's workflow state
  // only (see CaseActions.cs, it does not know the caller's permission); approve/send must be
  // gated here too, on the same permissions POST /customer-updates/{id}/approve and /send
  // actually require server-side.
  const canApproveCustomerUpdate = canDraftCustomerUpdates; // both require ManageCustomerCommunications
  const canSendCustomerUpdate = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "SendCustomerUpdate")
    : hasPermission(runtimeContext?.permissions ?? [], "SendCustomerUpdate");
  // "Resolve claim" calls POST /cases/{id}/resolve, which requires ApproveSettlementWorkflowStep,
  // the same authority as executing an approved workflow step (outcome O).
  const canResolveClaim = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "ApproveSettlementWorkflowStep")
    : hasPermission(runtimeContext?.permissions ?? [], "ApproveSettlementWorkflowStep");
  const [pendingCustomerUpdateAction, setPendingCustomerUpdateAction] = useState<PendingCustomerUpdateAction | null>(null);
  const [exportDialogOpen, setExportDialogOpen] = useState(false);
  // Where the person also wants a copy of the pack kept, in their own storage. Never chosen for them.
  const [saveTo, setSaveTo] = useState<"none" | "google" | "microsoft">("none");
  const [fileStatus, setFileStatus] = useState<FileConnectionStatus[] | null>(null);
  const [reviewBusyId, setReviewBusyId] = useState<string | null>(null);
  const [confirmDialogOpen, setConfirmDialogOpen] = useState(false);
  const [confirmCorrections, setConfirmCorrections] = useState<ConfirmClaimCorrections>({});
  const [resolveDialogOpen, setResolveDialogOpen] = useState(false);
  const [resolution, setResolution] = useState<"Settled" | "Closed">("Settled");
  const [satisfactionOpen, setSatisfactionOpen] = useState(false);
  const [satisfactionRating, setSatisfactionRating] = useState<"Good" | "Neutral" | "Poor">("Good");
  const [satisfactionNotes, setSatisfactionNotes] = useState("");
  const [satisfactionResult, setSatisfactionResult] = useState<string | null>(null);

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
        description: "The draft tells the customer to expect contact, it never states the decision itself. Review it below before approval or send.",
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

  async function handleResolveClaim(auditReason: string) {
    const res = await resolveClaim(resolution, auditReason);
    if (res.ok) {
      setResolveDialogOpen(false);
      toast.success(resolution === "Settled" ? "Claim recorded as settled" : "Claim closed", {
        description: "A satisfaction question has been drafted for approval.",
      });
    } else {
      toast.error(res.message ?? "Could not record the claim resolution");
    }
  }

  async function handleRecordSatisfaction() {
    const res = await recordSatisfactionResponse(satisfactionRating, satisfactionNotes);
    if (res.ok) {
      setSatisfactionOpen(false);
      setSatisfactionNotes("");
      setSatisfactionResult(satisfactionRating);
      toast.success("Satisfaction answer recorded", {
        description: res.serviceRecoveryTaskId ? "A service recovery task was opened for follow-up." : undefined,
      });
    } else {
      toast.error(res.message ?? "Could not record the satisfaction answer");
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
          description: "Workflow state only, this does not settle, deny, or pay the claim.",
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

  useEffect(() => {
    if (!exportDialogOpen) return;
    setSaveTo("none");
    getFileConnectionStatus(roleDef.key).then(setFileStatus).catch(() => setFileStatus([]));
  }, [exportDialogOpen, roleDef.key]);

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

    // The person chose, in the export dialog, to also keep a copy in their own storage.
    if (saveTo !== "none") {
      const saved = await saveExportedAuditPack(res.objectKey, saveTo);
      const where = saveTo === "google" ? "Google Drive" : "OneDrive";
      if (saved.ok) {
        toast.success(`Saved to your ${where}`, { description: saved.name ? `${saved.name}, in the folder "IIP exports".` : 'In the folder "IIP exports".' });
      } else {
        toast.error(`Not saved to your ${where}`, { description: saved.message ?? "The pack could not be saved." });
      }
    }
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
          <ErrorStateSurface
            title={error?.kind === "terminal_failure" ? "Claim unavailable" : undefined}
            message={error?.message ?? "No claim found for this workspace."}
            correlationId={error?.correlationId}
            onRetry={error?.retryable ? () => void reload() : undefined}
          />
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

  // Outcome H: one primary action, everything else behind a More menu, not a wall of equally-
  // weighted buttons. The first visible entry in this list becomes the primary action, ordered by
  // what the claim's own state actually needs next.
  const documentFollowUpAction: DecisionAction = {
    key: "document-follow-up",
    label: "Request missing documents",
    onClick: () => void handleDraftDocumentFollowUp(),
    disabled: drafting,
    visible: canDraftCustomerUpdates && detail.status === "MissingDocuments",
  };
  // A settled or closed claim cannot start a new approval request (CaseActions.CanCreateApproval
  // on the backend refuses it); this used to stay visible and clickable anyway, so the only
  // feedback was a generic failure toast on click. Hidden here instead, on the same terminal-
  // status check the resolve action below uses (demo-readiness pass).
  const claimIsTerminal = detail.status === "Settled" || detail.status === "Closed";
  const supervisorReviewAction: DecisionAction = {
    key: "supervisor-review",
    label: "Request supervisor review",
    onClick: () => void handleSupervisorReview(),
    disabled: requestingApproval,
    visible: !claimIsTerminal,
  };
  const draftCustomerUpdateAction: DecisionAction = {
    key: "draft-customer-update",
    label: "Draft customer update",
    onClick: () => void handleDraftCustomerUpdate(),
    disabled: drafting,
    visible: canDraftCustomerUpdates,
  };
  const draftDecisionOutcomeAction: DecisionAction = {
    key: "draft-decision-outcome",
    label: "Draft decision outcome update",
    onClick: () => void handleDraftDecisionOutcomeUpdate(),
    disabled: drafting,
    visible: canDraftCustomerUpdates,
  };
  const exportClaimPackAction: DecisionAction = {
    key: "export-claim-pack",
    label: "Export claim pack",
    onClick: () => setExportDialogOpen(true),
    disabled: exporting,
    visible: canExportEvidencePack,
  };
  const markEvidenceReviewedAction: DecisionAction = {
    key: "mark-evidence-reviewed",
    label: "Mark evidence reviewed",
    onClick: () => unavailable("Mark evidence reviewed"),
    visible: true,
  };
  // Outcome O: the terminal step after ExecuteApprovedNextStep's ReadyForExport hand-off, or from
  // Escalated for an exception-handled claim. Recording it here is what makes "ask one
  // satisfaction question on close" real rather than a hook with nothing that ever calls it.
  // Previously only visible from ReadyForExport/Escalated, so a claim anywhere earlier in the
  // pipeline had no visible way to be completed at all. Widened to any non-terminal status: a
  // claims officer or admin can resolve a claim directly, still behind the same permission and
  // the same reasoned Settled/Closed dialog (demo-readiness pass).
  const resolveClaimAction: DecisionAction = {
    key: "resolve-claim",
    label: "Record claim resolution",
    onClick: () => setResolveDialogOpen(true),
    disabled: resolving,
    visible: canResolveClaim && !claimIsTerminal,
  };
  const decisionActions: DecisionAction[] = detail.status === "MissingDocuments"
    ? [documentFollowUpAction, supervisorReviewAction, draftCustomerUpdateAction, draftDecisionOutcomeAction, exportClaimPackAction, markEvidenceReviewedAction, resolveClaimAction]
    : [resolveClaimAction, supervisorReviewAction, draftCustomerUpdateAction, draftDecisionOutcomeAction, exportClaimPackAction, markEvidenceReviewedAction, documentFollowUpAction];

  const missingBlock = (
      <div>
        <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">What is missing</div>
        {checklist === null ? (
          <p className="text-[12.5px] text-dls-text-muted">{checklistError ?? "Checking what is still needed."}</p>
        ) : (
          <>
            <p className="mb-1.5 text-[12.5px] font-semibold text-dls-text-primary">
              {Math.round(checklist.completenessScore)}% complete: documents {checklist.requiredReceived} of {checklist.requiredTotal}
              {checklist.details && checklist.details.total > 0 ? `, details ${checklist.details.found} of ${checklist.details.total}` : ""}
            </p>
            {checklist.missingRequiredDocuments.length === 0 && !checklist.details?.missing.length && !checklist.details?.unreadDocuments.length ? (
              <p className="text-[12.5px] text-dls-text-muted">Nothing outstanding.</p>
            ) : (
              <ul className="space-y-1 text-[12.5px] text-dls-text-secondary">
                {checklist.missingRequiredDocuments.map((item) => (
                  <li key={`document-${item.documentType}`}>{item.label}</li>
                ))}
                {(checklist.details?.missing ?? []).map((item) => (
                  <li key={`detail-${item.documentLabel}-${item.detail}`}>{item.detail}, on the {item.documentLabel.toLowerCase()}</li>
                ))}
                {(checklist.details?.unreadDocuments ?? []).map((label) => (
                  <li key={`unread-${label}`}>{label}: could not be read, a person needs to check it</li>
                ))}
              </ul>
            )}
          </>
        )}
      </div>
  );

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
          <div className="space-y-4">
            <div>
              <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">What happened</div>
              <MarkdownText className="text-[13px] text-dls-text-secondary leading-relaxed" content={displayedSummary.summary} />
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">What we have</div>
                {documentItems.filter((item) => item.present).length === 0 ? (
                  <p className="text-[12.5px] text-dls-text-muted">No documents received yet.</p>
                ) : (
                  <ul className="space-y-1 text-[12.5px] text-dls-text-secondary">
                    {documentItems.filter((item) => item.present).map((item) => (
                      <li key={item.label} className="flex items-center gap-1.5">
                        <CheckCircle2 className="h-3 w-3 shrink-0 text-dls-success-dot" aria-hidden="true" /> {item.label}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
              {missingBlock}
            </div>

            {(displayedSummary.openQuestions.length > 0 || displayedSummary.riskSignals?.length > 0) ? (
              <div>
                <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">What to watch</div>
                {displayedSummary.riskSignals?.length > 0 ? (
                  <ul className="mb-2 space-y-2">
                    {displayedSummary.riskSignals.map((signal, i) => (
                      <li key={i} className="flex items-start gap-2 text-[12.5px] text-dls-text-secondary">
                        <Badge tone={signal.category === "Fraud" ? "warning" : "neutral"} className="mt-0.5 shrink-0">
                          {signal.category === "Fraud" ? "Fraud signal" : "Underwriting"}
                        </Badge>
                        <span>{signal.note}</span>
                      </li>
                    ))}
                  </ul>
                ) : null}
                {displayedSummary.openQuestions.length > 0 ? (
                  <ul className="space-y-1 text-[12.5px] text-dls-text-secondary">
                    {displayedSummary.openQuestions.map((question) => (
                      <li key={question}>- {question}</li>
                    ))}
                  </ul>
                ) : null}
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
            <div className="mt-3">{missingBlock}</div>
          </div>
        )}
      </GlassCard>
      </div>

      <section aria-label="Claim at a glance" className="mb-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <FactTile
          label="Claimed"
          value={detail.claimedAmount != null ? `UGX ${detail.claimedAmount.toLocaleString("en-UG")}` : "Not stated"}
          sub={humanizeIdentifier(detail.claimType)}
        />
        <FactTile label="Happened" value={formatDate(detail.incidentDate)} sub={`Reported through ${channelLabel(detail.channel)}`} />
        <FactTile
          label="Cover"
          value={detail.policyNumber}
          sub={detail.policyEffectiveFrom && detail.policyEffectiveTo
            ? `${formatDate(detail.policyEffectiveFrom)} to ${formatDate(detail.policyEffectiveTo)}`
            : "Cover period not loaded"}
        />
        <FactTile
          label="Owner"
          value={detail.assignedUserId ? describe(detail.assignedUserId) : "Unassigned"}
          sub={detail.slaDueAt ? `Due ${formatDate(detail.slaDueAt)}` : "No due date set"}
        />
      </section>

      {detail.originalIncidentText ? (
        <div className="mb-4 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
          <div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">
            Customer&apos;s own words{detail.originalIncidentLanguage ? ` (${LANGUAGE_NAMES[detail.originalIncidentLanguage] ?? detail.originalIncidentLanguage})` : ""}
          </div>
          <p className="mt-1.5 whitespace-pre-line text-[13px] leading-relaxed text-dls-text-primary">{detail.originalIncidentText}</p>
          <p className="mt-2 text-[11.5px] leading-5 text-dls-text-muted">
            The description in the heading is a computer translation of this. If it matters, read the customer&apos;s own words or ask a
            speaker of the language.
          </p>
        </div>
      ) : null}

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
                  a best-effort guess from the raw message, review and correct them, then confirm to start the SLA
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
        <ClaimProcessStages claimId={claimId} role={roleDef.key} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="space-y-4 lg:col-span-2">
          <ClaimDecisionCard
            detail={detail}
            openReviewRequestCount={reviewRequests.filter((request) => request.status === "Open" || request.status === "Acknowledged").length}
            documentsReceivedCount={checklist?.requiredReceived ?? documentItems.filter((item) => item.present).length}
            documentsRequiredCount={checklist?.requiredTotal ?? documentItems.length}
            actions={decisionActions}
          />

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
                  Acknowledge or resolve contestability requests. This updates workflow state only, it does not settle, deny, or pay the claim.
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
                  <div key={document.documentId} id={`document-${document.documentId}`} className="flex items-start gap-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
                    <div className="grid h-9 w-9 shrink-0 place-items-center rounded-dls-control bg-dls-info-tint text-dls-info-text">
                      <FileText className="h-4 w-4" aria-hidden="true" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="break-words text-[13px] font-semibold text-dls-text-primary">{documentContractLabel(document.customerDocumentTypeKey ?? document.documentType)}</div>
                      <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1">
                        <span className="text-[11px] text-dls-text-muted">{document.processingStatus}</span>
                        {document.classificationConfidence > 0 ? (
                          <ConfidenceBadge value={document.classificationConfidence > 1 ? document.classificationConfidence / 100 : document.classificationConfidence} />
                        ) : (
                          <Badge tone="neutral">Not scored</Badge>
                        )}
                      </div>
                      {document.processingCapability === "DocumentProcessing" ? (
                        <div className="mt-2 text-[11.5px] text-dls-warning-text">Document processing is not configured. No fields were extracted.</div>
                      ) : null}
                    </div>
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
            <div className="mb-3 text-[14px] font-bold text-dls-text-primary">What was reported</div>
            <p className="m-0 whitespace-pre-line text-[13px] leading-relaxed text-dls-text-primary">{detail.incidentDescription}</p>
            <div className="mt-3 grid gap-2 sm:grid-cols-2">
              <DetailTile label="Date of incident" value={formatDate(detail.incidentDate)} />
              <DetailTile label="Reported through" value={channelLabel(detail.channel)} />
              <DetailTile label="Customer email" value={detail.customerEmail} />
              <DetailTile label="Customer phone" value={detail.customerPhone} />
              {detail.motorFnolDetails ? (
                <>
                  <DetailTile label="Vehicle registration" value={detail.motorFnolDetails.vehicleRegistration} />
                  <DetailTile label="Accident time" value={detail.motorFnolDetails.accidentTime} />
                  <DetailTile label="Accident location" value={detail.motorFnolDetails.accidentLocation} />
                  <DetailTile label="Driver name" value={detail.motorFnolDetails.driverName} />
                  <DetailTile label="Driving permit" value={detail.motorFnolDetails.drivingPermitNumber} />
                  <DetailTile label="Third party" value={detail.motorFnolDetails.thirdPartyInvolved} />
                  <DetailTile label="Injury indicator" value={detail.motorFnolDetails.injuryIndicator} />
                  <DetailTile label="Police report" value={detail.motorFnolDetails.policeReportStatus} />
                </>
              ) : null}
              {detail.medicalFnolDetails ? (
                <>
                  <DetailTile label="Patient" value={detail.medicalFnolDetails.patientName} />
                  <DetailTile label="Relationship to policyholder" value={detail.medicalFnolDetails.relationshipToPolicyholder} />
                  <DetailTile label="Treatment date" value={detail.medicalFnolDetails.treatmentDate} />
                  <DetailTile label="Provider" value={detail.medicalFnolDetails.providerName} />
                  <DetailTile label="Admission" value={detail.medicalFnolDetails.admissionType} />
                  <DetailTile label="Treating doctor" value={detail.medicalFnolDetails.treatingDoctor} />
                  <DetailTile label="Diagnosis summary" value={detail.medicalFnolDetails.diagnosisSummary} />
                </>
              ) : null}
            </div>
            {!detail.motorFnolDetails && !detail.medicalFnolDetails ? (
              <p className="mb-0 mt-3 text-[11.5px] text-dls-text-muted">
                No structured details were captured for this {detail.productLine} claim beyond the description above. Ask the customer for what is missing.
              </p>
            ) : null}
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
              <div className="space-y-4">
                <div>
                  <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">What happened</div>
                  <MarkdownText className="text-[13px] text-dls-text-secondary leading-relaxed" content={displayedSummary.summary} />
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">What we have</div>
                    {documentItems.filter((item) => item.present).length === 0 ? (
                      <p className="text-[12.5px] text-dls-text-muted">No documents received yet.</p>
                    ) : (
                      <ul className="space-y-1 text-[12.5px] text-dls-text-secondary">
                        {documentItems.filter((item) => item.present).map((item) => (
                          <li key={item.label} className="flex items-center gap-1.5">
                            <CheckCircle2 className="h-3 w-3 shrink-0 text-dls-success-dot" aria-hidden="true" /> {item.label}
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                  <div>
                    <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">What is missing</div>
                    {documentItems.filter((item) => !item.present).length === 0 ? (
                      <p className="text-[12.5px] text-dls-text-muted">Nothing outstanding.</p>
                    ) : (
                      <ul className="space-y-1 text-[12.5px] text-dls-text-secondary">
                        {documentItems.filter((item) => !item.present).map((item) => (
                          <li key={item.label}>{item.label}</li>
                        ))}
                      </ul>
                    )}
                  </div>
                </div>

                {(displayedSummary.openQuestions.length > 0 || displayedSummary.riskSignals?.length > 0) ? (
                  <div>
                    <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">What to watch</div>
                    {displayedSummary.riskSignals?.length > 0 ? (
                      <ul className="mb-2 space-y-2">
                        {displayedSummary.riskSignals.map((signal, i) => (
                          <li key={i} className="flex items-start gap-2 text-[12.5px] text-dls-text-secondary">
                            <Badge tone={signal.category === "Fraud" ? "warning" : "neutral"} className="mt-0.5 shrink-0">
                              {signal.category === "Fraud" ? "Fraud signal" : "Underwriting"}
                            </Badge>
                            <span>{signal.note}</span>
                          </li>
                        ))}
                      </ul>
                    ) : null}
                    {displayedSummary.openQuestions.length > 0 ? (
                      <ul className="space-y-1 text-[12.5px] text-dls-text-secondary">
                        {displayedSummary.openQuestions.map((question) => (
                          <li key={question}>- {question}</li>
                        ))}
                      </ul>
                    ) : null}
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

          {canViewAssessment && (
            <GlassCard>
              <ClaimAssessmentCard caseId={claimId} role={roleDef.key} canRespond={canRespondToAssessment} />
            </GlassCard>
          )}

          <GlassCard>
            <ClaimDetailsCard key={detailsVersion} caseId={claimId} role={roleDef.key} canEdit={canEditClaimDetails} />
            <div className="mt-4 border-t border-dls-border-subtle pt-4">
              <AuthenticityCard caseId={claimId} role={roleDef.key} canDecide={canEditClaimDetails} />
            </div>
            <div className="mt-4 border-t border-dls-border-subtle pt-4">
              <FieldProposalsCard caseId={claimId} role={roleDef.key} canDecide={canEditClaimDetails} onAccepted={() => setDetailsVersion((value) => value + 1)} />
            </div>
            <div className="mt-4 border-t border-dls-border-subtle pt-3">
              <PrivacyConsentLine caseId={claimId} role={roleDef.key} />
            </div>
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
              <Row
                k="Cover period"
                v={
                  detail.policyEffectiveFrom && detail.policyEffectiveTo
                    ? `${formatDate(detail.policyEffectiveFrom)} – ${formatDate(detail.policyEffectiveTo)}`
                    : "Not loaded from a policy record"
                }
              />
              {detail.policyRecordSource && (
                <Row
                  k="Policy source"
                  v={
                    (POLICY_SOURCE_LABEL[detail.policyRecordSource] ?? detail.policyRecordSource) +
                    (detail.policyLastRefreshedAt ? ` · refreshed ${formatDate(detail.policyLastRefreshedAt)}` : "")
                  }
                />
              )}
              <Row k="Claim type" v={detail.claimType} />
              <Row k="Channel" v={channelLabel(detail.channel)} />
              <Row k="Incident date" v={formatDate(detail.incidentDate)} />
              <Row k="Assigned to" v={describe(detail.assignedUserId, "Nobody yet")} />
              <AssignClaimControl caseId={detail.claimId} currentAssigneeId={detail.assignedUserId} onAssigned={reload} />
              {detail.customerEmail ? <Row k="Email" v={detail.customerEmail} /> : null}
              {detail.customerPhone ? <Row k="Phone" v={detail.customerPhone} /> : null}
            </div>
          </GlassCard>

          <GlassCard>
            <ClaimStatusChange caseId={detail.claimId} role={roleDef.key} status={detail.status} notice={statusNotice} onChanged={(confirmation) => { setStatusNotice(confirmation); void reload(); }} />
          </GlassCard>

          {canExportEvidencePack && (
            <GlassCard>
              <ClaimsPackDownload caseId={detail.claimId} role={roleDef.key} />
            </GlassCard>
          )}

          {canSendClaimLink && (
            <GlassCard>
              <SendClaimLink caseId={detail.claimId} role={roleDef.key} />
            </GlassCard>
          )}

          {(detail.status === "Settled" || detail.status === "Closed") && (
            <GlassCard>
              <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Satisfaction</div>
              {satisfactionResult ? (
                <p className="mt-2 text-[13px] text-dls-text-secondary">
                  Recorded as <span className="font-bold text-dls-text-primary">{satisfactionResult}</span>.
                </p>
              ) : satisfactionOpen ? (
                <div className="mt-2 space-y-2">
                  <div className="flex gap-2">
                    {(["Good", "Neutral", "Poor"] as const).map((rating) => (
                      <button
                        key={rating}
                        type="button"
                        onClick={() => setSatisfactionRating(rating)}
                        className={`dls-focus-outline rounded-dls-control border px-3 py-1.5 text-[12.5px] font-semibold ${
                          satisfactionRating === rating
                            ? "border-dls-primary bg-dls-primary-soft text-dls-primary"
                            : "border-dls-border-input text-dls-text-secondary hover:bg-dls-bg-page"
                        }`}
                      >
                        {rating}
                      </button>
                    ))}
                  </div>
                  <textarea
                    aria-label="Satisfaction notes"
                    placeholder="What did the customer say? (optional)"
                    className="min-h-16 w-full resize-y rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-sm text-dls-text-primary outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                    value={satisfactionNotes}
                    onChange={(event) => setSatisfactionNotes(event.target.value)}
                  />
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" onClick={() => setSatisfactionOpen(false)} disabled={recordingSatisfaction}>Cancel</Button>
                    <Button variant="primary" onClick={handleRecordSatisfaction} disabled={recordingSatisfaction}>
                      {recordingSatisfaction ? "Recording…" : "Record answer"}
                    </Button>
                  </div>
                </div>
              ) : (
                <div className="mt-2">
                  <p className="text-[12.5px] text-dls-text-muted">
                    A satisfaction question was drafted for approval when this claim was resolved. Record the
                    customer&apos;s answer here once you hear back.
                  </p>
                  <Button variant="secondary" className="mt-2" onClick={() => setSatisfactionOpen(true)}>
                    Record satisfaction answer
                  </Button>
                </div>
              )}
            </GlassCard>
          )}

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
            <fieldset className="mt-4 border-t border-dls-border-subtle pt-3">
              <legend className="text-[12.5px] font-semibold text-dls-text-primary">Also keep a copy in your own storage</legend>
              <div className="mt-2 flex flex-col gap-1.5">
                {([["none", "Download only", true, null], ["microsoft", "Save to my OneDrive", fileStatus?.find((s) => s.provider === "microsoft")?.connected ?? false, "microsoft"], ["google", "Save to my Google Drive", fileStatus?.find((s) => s.provider === "google")?.connected ?? false, "google"]] as const).map(([value, label, available]) => (
                  <label key={value} className={`flex items-center gap-2 text-[13px] ${available ? "text-dls-text-primary" : "text-dls-text-muted"}`}>
                    <input type="radio" name="save-to" value={value} checked={saveTo === value} disabled={!available} onChange={() => setSaveTo(value)} />
                    {label}
                    {!available && fileStatus !== null ? <span className="text-[12px]">(connect your account in Settings, Integrations first)</span> : null}
                  </label>
                ))}
              </div>
              <p className="m-0 mt-2 text-[12px] text-dls-text-muted">It goes to the folder &quot;IIP exports&quot; in your own account. The export is recorded on this claim with where it was sent, never its contents.</p>
            </fieldset>
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
      <GovernedActionDialog
        open={resolveDialogOpen}
        title="Record claim resolution"
        target={`Claim ${claimReference(detail.claimId)}`}
        impact="Records what happened after this claim's evidence pack was prepared for the insurer's own settlement or payment system. This does not itself release any payment. A satisfaction question is then drafted for approval, never auto sent."
        auditNotice="Recording a claim resolution is a consequential action, kept in the audit trail with both the status before and after."
        confirmLabel={resolution === "Settled" ? "Record as settled" : "Record as closed"}
        tone="primary"
        evidence={
          <div className="space-y-2 rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-4">
            <div className="text-sm font-semibold text-dls-text-primary">What happened to this claim?</div>
            <label className="flex items-center gap-2 text-sm text-dls-text-secondary">
              <input type="radio" name="resolution" checked={resolution === "Settled"} onChange={() => setResolution("Settled")} />
              Settled - the customer was paid
            </label>
            <label className="flex items-center gap-2 text-sm text-dls-text-secondary">
              <input type="radio" name="resolution" checked={resolution === "Closed"} onChange={() => setResolution("Closed")} />
              Closed - resolved without a payment
            </label>
          </div>
        }
        onClose={() => setResolveDialogOpen(false)}
        onConfirm={handleResolveClaim}
      />
    </>
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

function FactTile({ label, value, sub }: { label: string; value: string; sub: string }) {
  return (
    <div className="min-w-0 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
      <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{label}</div>
      <div className="mt-1 truncate text-[16px] font-semibold text-dls-text-primary" title={value}>{value}</div>
      <div className="mt-0.5 truncate text-[12px] text-dls-text-secondary" title={sub}>{sub}</div>
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
