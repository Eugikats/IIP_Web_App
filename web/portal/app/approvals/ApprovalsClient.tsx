"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApprovalDecisionPanel } from "@/components/ApprovalDecisionPanel";
import { RiskBadge, StatusBadge } from "@/components/Badges";
import { DataTable } from "@/components/DataTable";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { SideDrawer } from "@/components/SideDrawer";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { allowsAction, hasActionState, type ActionStateContract } from "@/lib/actionState";
import { motion } from "framer-motion";

type Approval = ActionStateContract & {
  approvalId: string;
  caseId: string;
  approvalType: string;
  status: string;
  riskLevel: string;
  evidenceSummary: string;
  sourceReferences: string[];
  requestedByUserId: string;
  reviewerUserId: string | null;
  comments: string | null;
  createdAt: string;
};

type ApprovalDecisionName = "approve" | "reject" | "override" | "escalate";

type PendingApprovalDecision = {
  approval: Approval;
  decision: ApprovalDecisionName;
  comments: string;
};

const approvalDecisionActions = {
  approve: "approveApproval",
  reject: "rejectApproval",
  override: "overrideApproval",
  escalate: "escalateApproval"
} as const;

export function ApprovalsClient() {
  const { role, userId, can } = useRole();
  const [approvals, setApprovals] = useState<Approval[]>([]);
  const [selected, setSelected] = useState<Approval | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [pendingDecision, setPendingDecision] = useState<PendingApprovalDecision | null>(null);

  const load = useCallback(async () => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/approvals`, {
        headers: { ...scopedHeaders(role, userId), "Content-Type": "application/json" }
      });
      if (!response.ok) throw new Error(`Approvals failed with HTTP ${response.status}.`);
      const payload: unknown = await response.json();
      if (!Array.isArray(payload) || !payload.every(hasActionState)) {
        throw new Error("Approval response did not include required backend decision authorization state.");
      }

      const data = payload as Approval[];
      setApprovals(data);
      setSelected((current) => current && data.some((item) => item.approvalId === current.approvalId) ? data.find((item) => item.approvalId === current.approvalId) ?? current : current ?? data[0] ?? null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Approvals failed.");
    } finally {
      setLoading(false);
    }
  }, [role, userId]);

  useEffect(() => {
    load();
  }, [load]);

  async function decide(approval: Approval, decision: ApprovalDecisionName, comments: string, auditReason: string) {
    if (!allowsAction(approval, approvalDecisionActions[decision])) {
      setError("This approval action is not currently allowed by the backend workflow state.");
      return;
    }

    const response = await fetch(`${apiBaseUrl}/api/v1/approvals/${approval.approvalId}/${decision}`, {
      method: "POST",
      headers: {
        ...scopedHeaders(role, userId),
        "Content-Type": "application/json",
        "X-Audit-Reason": auditReason
      },
      body: JSON.stringify({ comments })
    });
    if (!response.ok) {
      setError(await readDecisionError(response));
      return;
    }
    const updatedPayload: unknown = await response.json();
    if (!hasActionState(updatedPayload)) {
      setApprovals([]);
      setSelected(null);
      setPendingDecision(null);
      setError("Decision was accepted, but the response omitted required backend authorization state.");
      return;
    }

    setPendingDecision(null);
    await load();
  }

  if (error) return <ErrorState message={error} />;
  if (loading) return <LoadingState label="Loading approvals workbench..." />;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }}>
      <PageHeader eyebrow="Human review" title="Approvals" description="Clear human decision boundary for workflow approvals. These actions do not settle claims, deny claims, or authorize payments." />
      <SectionCard eyebrow="Queue" title="Approval queue" description="Pending and completed workflow approval requests from the current tenant workspace.">
        {approvals.length === 0 ? (
          <EmptyState title="No approvals yet" description="Create one from Case Review or evaluate workflow for high-risk or escalated cases." />
        ) : (
          <DataTable>
            <thead className="bg-mist text-left text-[10px] font-bold uppercase tracking-[0.2em] text-muted">
              <tr>
                <th className="px-4 py-3 border-b border-line">Approval</th>
                <th className="px-4 py-3 border-b border-line">Case</th>
                <th className="px-4 py-3 border-b border-line">Status</th>
                <th className="px-4 py-3 border-b border-line">Risk</th>
                <th className="px-4 py-3 border-b border-line">Requested</th>
                <th className="px-4 py-3 border-b border-line">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line bg-surface/50">
              {approvals.map((approval) => (
                <tr key={approval.approvalId} className="hover:bg-mist/70 transition-colors">
                  <td className="px-4 py-4">
                    <div className="font-semibold text-ink">{approval.approvalType}</div>
                    <div className="mt-1 text-sm text-muted line-clamp-2 max-w-sm">{approval.evidenceSummary}</div>
                  </td>
                  <td className="px-4 py-4">
                    <span className="font-mono text-xs text-muted">{approval.caseId.slice(0, 8)}</span>
                  </td>
                  <td className="px-4 py-4"><StatusBadge label={approval.status} /></td>
                  <td className="px-4 py-4"><RiskBadge label={approval.riskLevel} /></td>
                  <td className="px-4 py-4 text-sm text-muted font-mono">{new Date(approval.createdAt).toLocaleString()}</td>
                  <td className="px-4 py-4">
                    <button className="rounded-full border border-accent/40 bg-accent/10 px-3 py-1.5 text-xs font-semibold text-accent hover:bg-accent hover:text-white transition-colors" onClick={() => setSelected(approval)}>
                      Review
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </DataTable>
        )}
      </SectionCard>

      <SideDrawer
        open={selected !== null}
        title={selected ? selected.approvalType : "Approval"}
        subtitle={selected ? "Approval decision • Human-reviewed workflow only" : undefined}
        onClose={() => {
          setSelected(null);
          setPendingDecision(null);
        }}
        footer={
          selected ? (
            <div className="flex flex-wrap gap-2">
              <Link className="rounded-full bg-azure border border-azure/40 px-5 py-2.5 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all" href={`/cases/${selected.caseId}`}>
                Open full case
              </Link>
            </div>
          ) : null
        }
      >
        {selected ? (
          <div className="space-y-5">
            <div className="rounded-xl border border-line bg-mist p-4">
              <div className="flex flex-wrap gap-2">
                <StatusBadge label={selected.status} />
                <RiskBadge label={selected.riskLevel} />
              </div>
              <p className="mt-4 text-sm leading-6 text-ink font-mono">{selected.evidenceSummary}</p>
              <p className="mt-4 text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Requested by {selected.requestedByUserId} on {new Date(selected.createdAt).toLocaleString()}</p>
            </div>
            <div className="hidden md:block">
              <SectionCard eyebrow="Action" title="Decision panel" description="Every workflow decision requires human reviewer comments and an audit reason." className="shadow-none !bg-transparent !p-0 !border-0">
                <ApprovalDecisionPanel
                  disabled={!approvalAllowsAny(selected) || (!can("ApproveSettlementWorkflowStep") && !can("OverrideAiRecommendation"))}
                  allowedDecisions={{
                    approve: can("ApproveSettlementWorkflowStep") && allowsAction(selected, "approveApproval"),
                    reject: can("ApproveSettlementWorkflowStep") && allowsAction(selected, "rejectApproval"),
                    escalate: can("ApproveSettlementWorkflowStep") && allowsAction(selected, "escalateApproval"),
                    override: can("OverrideAiRecommendation") && allowsAction(selected, "overrideApproval")
                  }}
                  onDecision={async (decision, comments) => {
                    setPendingDecision({ approval: selected, decision, comments });
                  }}
                />
              </SectionCard>
            </div>
            <div role="note" className="rounded-xl border border-warning/30 bg-warning/10 p-4 text-sm leading-6 text-warning md:hidden">
              Workflow decision controls are available on tablet or desktop after full case review.
            </div>
            <SectionCard eyebrow="References" title="Evidence" className="shadow-none !bg-transparent !p-0 !border-0">
              {selected.sourceReferences.length > 0 ? (
                <ul className="space-y-2 text-sm text-muted">
                  {selected.sourceReferences.map((source) => (
                    <li key={source} className="rounded-xl border border-line bg-mist px-4 py-3 font-mono text-ink text-xs">{source}</li>
                  ))}
                </ul>
              ) : (
                <EmptyState title="No source references" description="This approval record does not include separate source references yet." />
              )}
            </SectionCard>
          </div>
        ) : null}
      </SideDrawer>

      {pendingDecision ? (
        <GovernedActionDialog
          open={pendingDecision !== null}
          title={approvalDecisionTitle(pendingDecision.decision)}
          target={`${pendingDecision.approval.approvalType} (${pendingDecision.approval.approvalId})`}
          impact={approvalDecisionImpact(pendingDecision.decision)}
          auditNotice="This records a human review-step decision for the workflow queue only. It does not settle, deny, repudiate, authorize payment, or close the claim."
          confirmLabel={approvalDecisionConfirmLabel(pendingDecision.decision)}
          tone={pendingDecision.decision === "reject" || pendingDecision.decision === "override" ? "danger" : "primary"}
          evidence={<ApprovalDecisionEvidence pendingDecision={pendingDecision} />}
          onClose={() => setPendingDecision(null)}
          onConfirm={async (auditReason) => {
            await decide(pendingDecision.approval, pendingDecision.decision, pendingDecision.comments, auditReason);
          }}
        />
      ) : null}
    </motion.div>
  );
}

function ApprovalDecisionEvidence({ pendingDecision }: { pendingDecision: PendingApprovalDecision }) {
  const approval = pendingDecision.approval;

  return (
    <div className="rounded-xl border border-line bg-mist p-4 text-sm leading-6 text-muted">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em]">Evidence context</p>
      <dl className="mt-3 grid gap-2 sm:grid-cols-2">
        <EvidenceItem label="Status" value={approval.status} />
        <EvidenceItem label="Risk" value={approval.riskLevel} />
        <EvidenceItem label="Requested by" value={approval.requestedByUserId} />
        <EvidenceItem label="Source references" value={`${approval.sourceReferences.length}`} />
      </dl>
      <div className="mt-3 rounded-lg border border-line bg-surface/70 p-3">
        <p className="text-[10px] font-bold uppercase tracking-[0.18em] opacity-70">Evidence summary</p>
        <p className="mt-1 text-ink">{approval.evidenceSummary}</p>
      </div>
      <div className="mt-3 rounded-lg border border-line bg-surface/70 p-3">
        <p className="text-[10px] font-bold uppercase tracking-[0.18em] opacity-70">Reviewer comments</p>
        <p className="mt-1 text-ink">{pendingDecision.comments}</p>
      </div>
    </div>
  );
}

function EvidenceItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-[10px] font-bold uppercase tracking-[0.18em] opacity-70">{label}</dt>
      <dd className="mt-1 break-words font-mono font-semibold text-ink">{value}</dd>
    </div>
  );
}

function approvalDecisionTitle(decision: ApprovalDecisionName) {
  switch (decision) {
    case "approve":
      return "Approve workflow review step";
    case "reject":
      return "Reject workflow review step";
    case "override":
      return "Record human override";
    case "escalate":
      return "Escalate workflow review step";
  }
}

function approvalDecisionImpact(decision: ApprovalDecisionName) {
  switch (decision) {
    case "approve":
      return "Marks this workflow review request as approved for progression while preserving the human reviewer comments and audit trail.";
    case "reject":
      return "Rejects this workflow review request. This is not a claim denial or repudiation and does not close the case.";
    case "override":
      return "Records that a human reviewer overrode the AI recommendation for this workflow step. It does not authorize payment or settlement.";
    case "escalate":
      return "Escalates this workflow review request for further human review and preserves the reviewer comments.";
  }
}

function approvalDecisionConfirmLabel(decision: ApprovalDecisionName) {
  switch (decision) {
    case "approve":
      return "Approve step";
    case "reject":
      return "Reject step";
    case "override":
      return "Record override";
    case "escalate":
      return "Escalate step";
  }
}

function approvalAllowsAny(approval: Approval) {
  return approval.allowedActions.length > 0;
}

async function readDecisionError(response: Response) {
  try {
    const body = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const validationMessage = body.errors
      ? Object.values(body.errors).flat().find((message) => message.trim().length > 0)
      : null;

    return body.message ?? validationMessage ?? `Decision failed with HTTP ${response.status}.`;
  } catch {
    return `Decision failed with HTTP ${response.status}.`;
  }
}
