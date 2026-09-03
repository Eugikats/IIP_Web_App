"use client";

import { toast } from "sonner";
import {
  AlertTriangle,
  ArrowLeft,
  CheckCircle2,
  Download,
  FileText,
  Loader2,
  MessageSquareText,
  Send,
  ShieldCheck,
} from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "@/components/iip/NextRouterCompat";
import { CustomerUpdateGovernedDialog, type PendingCustomerUpdateAction } from "@/components/CustomerUpdateGovernedDialog";
import { CustomerUpdatePanel, type CustomerUpdate } from "@/components/CustomerUpdatePanel";
import { ClaimReviewRequestsPanel } from "@/components/ClaimReviewRequestsPanel";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { Badge, ConfidenceBadge, GlassCard, RiskBadge, SectionHeader, SLABadge, StatusBadge } from "@/components/iip/Primitives";
import CompletenessCard from "@/components/iip/CompletenessCard";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import MotorLifecycle from "@/components/iip/MotorLifecycle";
import { DocumentUploadWidget } from "@/components/iip/DocumentUploadWidget";
import { MarkdownText } from "@/components/iip/MarkdownText";
import { useClaimReview } from "@/lib/lovable-parity/data/claimReview";
import { useClaimsSummary } from "@/lib/lovable-parity/data/claimsSummary";
import type { ChannelSendAvailability } from "@/lib/providerHealth";
import { useUI } from "@/context/UIStateContext";
import { extractionHref } from "@/lib/workItemLinks";
import { canPreviewPermission, hasPermission } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { useRuntimeContext } from "@/lib/runtimeContext";

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
    draftCustomerUpdate,
    draftDocumentFollowUp,
    approveCustomerUpdate,
    sendCustomerUpdate,
    acknowledgeReviewRequest,
    resolveReviewRequest,
    exportEvidencePack,
    requestSupervisorReview,
    reload,
  } = useClaimReview(claimId);
  const loading = loadState.status === "loading";
  const error = "error" in loadState ? loadState.error : null;
  const { roleDef } = useUI();
  const { summary: claimsSummary, loading: summarizing, error: summaryError, generate: generateSummary } = useClaimsSummary(claimId, roleDef.key);
  const { runtimeContext, parityMode } = useRuntimeContext();
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
    if (res.ok) {
      toast.success("Claim pack exported", {
        description: res.objectKey ? `Stored as ${res.objectKey}` : "Evidence pack artifact stored.",
      });
      setExportDialogOpen(false);
    } else {
      toast.error(res.message ?? "Evidence-pack export failed");
    }
  }

  const crumb = (
    <div className="flex items-center gap-2 text-[12px] text-stone mb-4">
      <Link to="/app/claims" className="inline-flex items-center gap-1 hover:text-navy">
        <ArrowLeft className="h-3.5 w-3.5" /> Claims
      </Link>
      <span>/</span>
      <span className="text-navy">{claimId}</span>
    </div>
  );

  if (loading) {
    return (
      <>
        {crumb}
        <GlassCard>
          <div className="inline-flex items-center gap-2 p-10 text-center text-[13px] text-stone">
            <Loader2 className="h-4 w-4 animate-spin" /> Loading claim workspace...
          </div>
        </GlassCard>
      </>
    );
  }

  if (error || !detail) {
    return (
      <>
        {crumb}
        <GlassCard>
          <div className="flex flex-col items-center gap-2 p-10 text-center">
            <FileText className="h-6 w-6 text-stone" />
            <div className="font-medium text-navy">Claim unavailable</div>
            <div className="max-w-md text-[12.5px] text-stone">{error?.message ?? "No claim found for this workspace."}</div>
            {error?.recovery ? <div className="max-w-md text-[12px] text-stone">{error.recovery}</div> : null}
            {error ? <div className="text-[11px] text-stone">Attempted {new Date(error.occurredAt).toLocaleString()}</div> : null}
            {error?.correlationId ? <div className="font-mono text-[11px] text-stone">Reference {error.correlationId}</div> : null}
            <div className="mt-2 flex flex-wrap justify-center gap-2">
              {error?.retryable ? <button type="button" onClick={() => void reload()} className="rounded-xl border border-cloud bg-white px-3 py-2 text-[11.5px] font-medium text-navy focus-ring">Try again</button> : null}
              <Link to="/app/claims" className="rounded-xl border border-cloud bg-white px-3 py-2 text-[11.5px] font-medium text-navy focus-ring">Return to claims</Link>
            </div>
          </div>
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
        title={`${detail.claimId.slice(0, 8)} · ${detail.customerName}`}
        sub={detail.incidentDescription}
        action={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={detail.status} />
            <SLABadge sla={sla} />
            <RiskBadge risk={detail.riskLevel} />
          </div>
        }
      />

      <div className="mb-4">
        <GuardrailBanner text="AI prepares this claim's work: extractions, summaries, drafts, and advisory signals. Settlement, denial, repudiation, payment, and fraud determination remain with the reviewing officer." />
      </div>

      <div className="mb-4">
        <MotorLifecycle status={detail.status} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="space-y-4 lg:col-span-2">
          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-4">
              <div className="text-[14px] font-semibold text-navy">Human review actions</div>
              <div className="text-[11.5px] text-stone">Only wired actions execute. Unwired actions report Not configured.</div>
            </div>
            <div className="grid gap-2.5 sm:grid-cols-2">
              {canDraftCustomerUpdates ? (
                <button
                  type="button"
                  onClick={handleDraftCustomerUpdate}
                  disabled={drafting}
                  className="inline-flex items-center gap-2 rounded-2xl border border-cloud bg-white px-4 py-3 text-[13px] font-medium text-navy hover-lift disabled:opacity-50"
                >
                  {drafting ? <Loader2 className="h-4 w-4 animate-spin" /> : <MessageSquareText className="h-4 w-4" />}
                  Draft customer update
                </button>
              ) : null}
              <button
                type="button"
                onClick={handleSupervisorReview}
                disabled={requestingApproval}
                className="inline-flex items-center gap-2 rounded-2xl border border-cloud bg-white px-4 py-3 text-[13px] font-medium text-navy hover-lift disabled:opacity-50"
              >
                {requestingApproval ? <Loader2 className="h-4 w-4 animate-spin" /> : <ShieldCheck className="h-4 w-4" />}
                Request supervisor review
              </button>
              <UnavailableAction icon={CheckCircle2} label="Mark evidence reviewed" onClick={unavailable} />
              {canDraftCustomerUpdates ? (
                <button
                  type="button"
                  onClick={handleDraftDocumentFollowUp}
                  disabled={drafting}
                  className="inline-flex items-center gap-2 rounded-2xl border border-cloud bg-white px-4 py-3 text-[13px] font-medium text-navy hover-lift disabled:opacity-50"
                >
                  {drafting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
                  Draft document follow-up
                </button>
              ) : null}
              {canExportEvidencePack ? (
                <button
                  type="button"
                  onClick={() => setExportDialogOpen(true)}
                  disabled={exporting}
                  className="inline-flex items-center gap-2 rounded-2xl border border-cloud bg-white px-4 py-3 text-[13px] font-medium text-navy hover-lift disabled:opacity-50"
                >
                  {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
                  Export claim pack
                </button>
              ) : null}
            </div>
            {!canDraftCustomerUpdates || !canExportEvidencePack ? (
              <div className="mt-2.5 text-[11.5px] text-stone">
                {!canDraftCustomerUpdates && !canExportEvidencePack
                  ? "Your role does not include drafting customer updates or exporting the claim pack."
                  : !canDraftCustomerUpdates
                    ? "Your role does not include drafting customer updates."
                    : "Your role does not include exporting the claim pack."}
              </div>
            ) : null}
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-4">
              <div>
                <div className="text-[14px] font-semibold text-navy">Customer updates</div>
                <div className="mt-1 text-[11.5px] text-stone">
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
                <div className="text-[14px] font-semibold text-navy">Claimant re-review requests</div>
                <div className="mt-1 text-[11.5px] text-stone">
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
              <div className="text-[14px] font-semibold text-navy">Documents received</div>
              {firstExtractionHref ? <Link to={firstExtractionHref} className="text-[12px] text-info hover:underline">
                Open extraction review
              </Link> : null}
            </div>
            {detail.documents.length === 0 ? (
              <div className="rounded-xl border border-cloud bg-white/70 p-4 text-[12.5px] text-stone">
                No documents uploaded to this claim yet.
              </div>
            ) : (
              <div className="grid gap-2 sm:grid-cols-2">
                {detail.documents.map((document) => (
                  <div key={document.documentId} className="flex items-center gap-3 rounded-xl border border-cloud bg-white/80 p-3">
                    <div className="grid h-9 w-9 place-items-center rounded-lg bg-[hsl(var(--info)/0.08)] text-info">
                      <FileText className="h-4 w-4" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-[13px] font-medium text-navy">{document.documentType}</div>
                      <div className="text-[11px] text-stone">{document.processingStatus}</div>
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
              <div className="mt-3 text-[11.5px] text-stone">
                Your role does not include document upload.
              </div>
            )}
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-4">
              <div>
                <div className="text-[14px] font-semibold text-navy">Claim summary</div>
                <div className="mt-1 text-[11.5px] text-stone">
                  Prepared from this claim&apos;s facts, documents, policy, and approved knowledge. Required before decision use — not a decision itself.
                </div>
              </div>
              <button
                type="button"
                onClick={() => { void generateSummary(); }}
                disabled={summarizing}
                className="inline-flex shrink-0 items-center gap-2 rounded-2xl border border-cloud bg-white px-4 py-2.5 text-[13px] font-medium text-navy hover-lift disabled:opacity-50"
              >
                {summarizing ? <Loader2 className="h-4 w-4 animate-spin" /> : <MessageSquareText className="h-4 w-4" />}
                {claimsSummary ? "Regenerate summary" : "Generate summary"}
              </button>
            </div>
            {summaryError ? (
              <div className="flex items-start gap-2 rounded-xl border border-warning/30 bg-warning/10 px-3 py-2 text-[12.5px] text-[hsl(38_92%_36%)]">
                <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                <span>{summaryError}</span>
              </div>
            ) : claimsSummary ? (
              <div className="space-y-3">
                <MarkdownText className="text-[13px] text-slate2 leading-relaxed" content={claimsSummary.summary} />
                {claimsSummary.openQuestions.length > 0 ? (
                  <div>
                    <div className="mb-1 text-[11px] uppercase tracking-[0.14em] text-stone">Open questions</div>
                    <ul className="space-y-1 text-[12.5px] text-slate2">
                      {claimsSummary.openQuestions.map((question) => (
                        <li key={question}>- {question}</li>
                      ))}
                    </ul>
                  </div>
                ) : null}
                {claimsSummary.knowledgeSources.length > 0 ? (
                  <div>
                    <div className="mb-1 text-[11px] uppercase tracking-[0.14em] text-stone">Sources</div>
                    <div className="space-y-1">
                      {claimsSummary.knowledgeSources.map((source) => (
                        <div key={source.chunkId} className="flex items-center gap-2 text-[12px] text-stone">
                          <FileText className="h-3.5 w-3.5 shrink-0" />
                          {source.label}: {source.documentName}
                        </div>
                      ))}
                    </div>
                  </div>
                ) : null}
              </div>
            ) : (
              <div className="rounded-xl border border-cloud bg-white/70 p-4 text-[12.5px] text-stone">
                No summary generated yet.
              </div>
            )}
          </GlassCard>

          <GlassCard>
            <div className="mb-3 text-[14px] font-semibold text-navy">Motor FNOL details</div>
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
              <div className="rounded-xl border border-cloud bg-white/70 p-4 text-[12.5px] text-stone">
                This claim has no structured Motor FNOL details yet.
              </div>
            )}
          </GlassCard>

          <GlassCard>
            <div className="mb-4 text-[14px] font-semibold text-navy">Activity timeline</div>
            {timeline.length === 0 ? (
              <div className="rounded-xl border border-cloud bg-white/70 p-4 text-[12.5px] text-stone">
                No recorded activity yet. Governed actions and AI runs appear here after they are written by the backend.
              </div>
            ) : (
              <div className="relative pl-5">
                <div className="absolute bottom-1 left-1.5 top-1 w-px bg-cloud" />
                {timeline.map((event) => (
                  <div key={event.eventId} className="relative pb-4">
                    <div className="absolute -left-[9px] top-1 h-2 w-2 rounded-full bg-info ring-2 ring-white" />
                    <div className="text-[12px] text-stone">
                      {new Date(event.timestamp).toLocaleString()} · {event.actor}
                    </div>
                    <div className="mt-0.5 text-[13px] text-navy">{event.summary || event.eventType}</div>
                  </div>
                ))}
              </div>
            )}
          </GlassCard>
        </div>

        <div className="space-y-4">
          <GlassCard>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Policy &amp; customer</div>
            <div className="mt-2 space-y-2 text-[13px]">
              <Row k="Customer" v={detail.customerName} />
              <Row k="Policy" v={`${detail.policyNumber} · ${detail.productLine}`} />
              <Row k="Claim type" v={detail.claimType} />
              <Row k="Channel" v={detail.channel} />
              <Row k="Incident date" v={detail.incidentDate} />
              {detail.assignedUserId ? <Row k="Assigned" v={detail.assignedUserId} /> : null}
              {detail.customerEmail ? <Row k="Email" v={detail.customerEmail} /> : null}
              {detail.customerPhone ? <Row k="Phone" v={detail.customerPhone} /> : null}
            </div>
          </GlassCard>

          <CompletenessCard
            score={completeness}
            items={documentItems.length > 0 ? documentItems : [{ label: "Claim documents", present: false }]}
          />

          <div className="rounded-2xl border border-[hsl(var(--warning)/0.28)] bg-[hsl(var(--warning)/0.08)] p-4">
            <div className="mb-1 flex items-center gap-2">
              <AlertTriangle className="h-3.5 w-3.5 text-warning" />
              <div className="text-[11px] uppercase tracking-[0.14em] text-[hsl(38_92%_36%)]">Advisory boundary</div>
            </div>
            <p className="text-[13px] leading-snug text-slate2">
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
        target={`Case ${detail.claimId}`}
        impact="Creates a durable JSON evidence-pack artifact from sanitized audit, document, assistant-action, approval, and customer-update records for this claim."
        auditNotice="Evidence-pack export is a compliance-sensitive action. The exported artifact is stored through the configured evidence storage provider and the export event is written to the audit trail."
        confirmLabel="Export claim pack"
        evidence={
          <div className="rounded-xl border border-line bg-mist p-4 text-sm leading-6 text-muted">
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
    <button
      type="button"
      onClick={() => onClick(label)}
      className="inline-flex items-center gap-2 rounded-2xl border border-cloud bg-white px-4 py-3 text-[13px] font-medium text-navy hover-lift"
    >
      <Icon className="h-4 w-4" />
      {label}
    </button>
  );
}

function Row({ k, v }: { k: string; v: string }) {
  return (
    <div className="flex items-baseline justify-between gap-2">
      <span className="text-[11.5px] text-stone">{k}</span>
      <span className="text-right text-navy">{v}</span>
    </div>
  );
}

function DetailTile({ label, value }: { label: string; value: string | null }) {
  return (
    <div className="rounded-xl border border-cloud bg-white/70 p-3">
      <div className="text-[11px] uppercase tracking-[0.12em] text-stone">{label}</div>
      <div className="mt-1 text-[13px] text-navy">{value || "Not captured"}</div>
    </div>
  );
}

function ExportEvidenceItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-[10px] font-bold uppercase tracking-[0.18em] opacity-70">{label}</dt>
      <dd className="mt-1 break-words font-mono font-semibold text-ink">{value}</dd>
    </div>
  );
}
