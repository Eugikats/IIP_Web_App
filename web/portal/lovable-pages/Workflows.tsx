"use client";
import { useMemo, useState } from "react";
import { SectionHeader, GlassCard, Badge, RiskBadge, StatusBadge } from "@/components/iip/Primitives";
import { toast } from "sonner";
import { useApprovals } from "@/lib/lovable-parity/data/approvals";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Field } from "@/components/ui/Input";
import { LoadingSkeleton, EmptyStateSurface } from "@/components/ui/StateSurfaces";

export default function Workflows({ embedded = false }: { embedded?: boolean }) {
  const { approvals, loading, error, parityMode, approve, reject, escalate, override, executeNextStep } = useApprovals();
  const [tab, setTab] = useState<"All approvals" | "High risk" | "Completed">("All approvals");
  const [busyId, setBusyId] = useState<string | null>(null);
  const [reason, setReason] = useState("");

  const filtered = useMemo(() =>
    tab === "High risk" ? approvals.filter((a) => a.risk === "High" && !a.executedAt)
    : tab === "Completed" ? approvals.filter((a) => Boolean(a.executedAt) || ["Approved", "Rejected", "Completed"].includes(a.status))
    : approvals, [approvals, tab]);

  async function run(label: string, approvalId: string, action: () => Promise<{ ok: boolean; message?: string }>) {
    if (reason.trim().length < 3) { toast.error("Record what you checked and why before taking an approval action."); return; }
    setBusyId(approvalId);
    try {
      const result = await action();
      if (result.ok) { toast.success(label); setReason(""); }
      else toast.error(result.message ?? `${label} failed.`);
    } finally {
      setBusyId(null);
    }
  }

  const can = (actions: { action: string }[], action: string) =>
    parityMode || actions.some((candidate) => candidate.action === action);

  return (
    <>
      {!embedded ? <>
        <SectionHeader eyebrow="Workflow & Approvals" title="Human review queue" sub="Every step below advances a workflow — regulated outcomes remain with a named human reviewer." />
        <div className="mb-4"><GuardrailBanner text="Approval actions update workflow state only. Final regulated decisions — settle, deny, pay, bind, accuse fraud — require an explicit human owner." /></div>
      </> : null}

      <div className="mb-4 flex flex-wrap gap-1.5">
        {(["All approvals", "High risk", "Completed"] as const).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => setTab(t)}
            className={`dls-focus-outline rounded-dls-chip border px-3 py-1.5 text-[12px] font-semibold transition-colors duration-dls-fast ease-dls ${tab === t ? "border-dls-primary bg-dls-primary text-dls-text-inverse" : "border-dls-border-input bg-dls-surface text-dls-text-secondary hover:border-dls-primary"}`}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="mb-4">
        <Field label="Decision reason">
          <textarea
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Record the evidence checked and the reason for approving, rejecting, or executing the next governed step."
            className="min-h-16 w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2.5 text-[12px] text-dls-text-primary placeholder:text-dls-text-muted focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
          />
        </Field>
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : approvals.length === 0 ? (
        <GlassCard><EmptyStateSurface message={error ?? "Approval tasks appear here when AI or staff route work for human review."} /></GlassCard>
      ) : (
        <GlassCard className="overflow-hidden !p-0">
          <div className="grid grid-cols-12 border-b border-dls-border-subtle px-4 py-3 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">
            <div className="col-span-4">Task</div>
            <div className="col-span-2">Requester</div>
            <div className="col-span-2">Reviewer</div>
            <div className="col-span-1">Risk</div>
            <div className="col-span-1">Age</div>
            <div className="col-span-2 text-right">Actions</div>
          </div>
          {filtered.map(q => (
            <div key={q.id} className="grid grid-cols-12 items-start border-b border-dls-border-subtle px-4 py-3 hover:bg-dls-bg-page">
              <div className="col-span-4">
                <div className="text-[13px] font-semibold text-dls-text-primary">{q.title}</div>
                <div className="text-[11.5px] text-dls-text-muted">{q.id}{q.claim ? ` · ${q.claim}` : ""}</div>
                <div className="mt-1 text-[12px] text-dls-text-secondary">{q.summary}</div>
                <div className="mt-2 flex flex-wrap items-center gap-1.5">
                  <StatusBadge status={q.status} />
                  {q.executedAt ? <Badge tone="success">Executed</Badge> : null}
                </div>
              </div>
              <div className="col-span-2 text-[13px] text-dls-text-secondary">{q.requester}</div>
              <div className="col-span-2 text-[13px] text-dls-text-secondary">{q.reviewer}</div>
              <div className="col-span-1"><RiskBadge risk={q.risk} /></div>
              <div className="col-span-1 text-[12px] text-dls-text-muted">{q.ageHrs}h</div>
              <div className="col-span-2 flex flex-wrap justify-end gap-1.5">
                {can(q.allowedActions, "approveApproval") ? <button
                  type="button"
                  onClick={() => run("Approved workflow step", q.id, () => approve(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="dls-focus-outline rounded-dls-control bg-dls-primary px-2.5 py-1.5 text-[11.5px] font-bold text-dls-text-inverse disabled:opacity-50"
                >
                  Approve
                </button> : null}
                {can(q.allowedActions, "executeApprovedNextStep") ? <button
                  type="button"
                  onClick={() => run("Executed approved next step", q.id, () => executeNextStep(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="dls-focus-outline rounded-dls-control border border-dls-border-input bg-dls-surface px-2.5 py-1.5 text-[11.5px] font-bold text-dls-text-primary disabled:opacity-50"
                >
                  Execute
                </button> : null}
                {can(q.allowedActions, "rejectApproval") ? <button
                  type="button"
                  onClick={() => run("Rejected workflow step", q.id, () => reject(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="dls-focus-outline rounded-dls-control px-2.5 py-1.5 text-[11.5px] font-bold text-dls-danger-text hover:underline disabled:opacity-50"
                >
                  Reject
                </button> : null}
                {can(q.allowedActions, "escalateApproval") ? <button
                  type="button"
                  onClick={() => run("Escalated workflow step", q.id, () => escalate(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="dls-focus-outline rounded-dls-control border border-dls-warning-dot bg-dls-surface px-2.5 py-1.5 text-[11.5px] font-bold text-dls-warning-text disabled:opacity-50"
                >
                  Escalate
                </button> : null}
                {can(q.allowedActions, "overrideApproval") ? <button
                  type="button"
                  onClick={() => run("Recorded human override", q.id, () => override(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="dls-focus-outline rounded-dls-control border border-dls-border-input bg-dls-surface px-2.5 py-1.5 text-[11.5px] font-bold text-dls-text-primary disabled:opacity-50"
                >
                  Override AI
                </button> : null}
                {!q.allowedActions.length ? <span className="text-[11px] text-dls-text-muted">No action available</span> : null}
              </div>
            </div>
          ))}
        </GlassCard>
      )}
    </>
  );
}
