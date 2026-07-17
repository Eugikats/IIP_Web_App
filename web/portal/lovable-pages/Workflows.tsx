"use client";
import { useMemo, useState } from "react";
import { SectionHeader, GlassCard, Badge, RiskBadge, StatusBadge } from "@/components/iip/Primitives";
import { toast } from "sonner";
import { useApprovals } from "@/lib/lovable-parity/data/approvals";
import GuardrailBanner from "@/components/iip/GuardrailBanner";

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

      <div className="flex flex-wrap gap-1.5 mb-4">
        {(["All approvals","High risk","Completed"] as const).map((t) => (
          <button key={t} onClick={() => setTab(t)} className={`text-[12px] rounded-full px-3 py-1.5 border ${tab === t ? "bg-navy text-white border-navy" : "bg-white text-slate2 border-cloud"}`}>{t}</button>
        ))}
      </div>

      <label className="mb-4 block rounded-2xl border border-cloud bg-white/70 p-4 text-[11.5px] font-medium text-navy">Decision reason
        <textarea value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Record the evidence checked and the reason for approving, rejecting, or executing the next governed step." className="mt-2 min-h-16 w-full rounded-xl border border-cloud bg-white px-3 py-2.5 text-[12px] font-normal text-navy placeholder:text-stone focus-ring" />
      </label>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading approvals…</div></GlassCard>
      ) : approvals.length === 0 ? (
        <GlassCard><div className="p-10 text-center text-stone"><div className="text-navy font-medium">No approvals in the queue</div><div className="text-[12.5px] mt-1">{error ? error : "Approval tasks appear here when AI or staff route work for human review."}</div></div></GlassCard>
      ) : (
        <GlassCard className="p-0 overflow-hidden">
          <div className="grid grid-cols-12 text-[11px] uppercase tracking-[0.12em] text-stone px-4 py-3 border-b border-cloud">
            <div className="col-span-4">Task</div>
            <div className="col-span-2">Requester</div>
            <div className="col-span-2">Reviewer</div>
            <div className="col-span-1">Risk</div>
            <div className="col-span-1">Age</div>
            <div className="col-span-2 text-right">Actions</div>
          </div>
          {filtered.map(q => (
            <div key={q.id} className="grid grid-cols-12 items-start px-4 py-3 border-b border-cloud/60 hover:bg-white/70">
              <div className="col-span-4">
                <div className="text-[13px] text-navy font-medium">{q.title}</div>
                <div className="text-[11.5px] text-stone">{q.id}{q.claim ? ` · ${q.claim}` : ""}</div>
                <div className="text-[12px] text-slate2 mt-1">{q.summary}</div>
                <div className="mt-2 flex flex-wrap items-center gap-1.5">
                  <StatusBadge status={q.status} />
                  {q.executedAt ? <Badge tone="success">Executed</Badge> : null}
                </div>
              </div>
              <div className="col-span-2 text-[13px] text-slate2">{q.requester}</div>
              <div className="col-span-2 text-[13px] text-slate2">{q.reviewer}</div>
              <div className="col-span-1"><RiskBadge risk={q.risk} /></div>
              <div className="col-span-1 text-[12px] text-stone">{q.ageHrs}h</div>
              <div className="col-span-2 flex justify-end gap-1.5 flex-wrap">
                {can(q.allowedActions, "approveApproval") ? <button
                  onClick={() => run("Approved workflow step", q.id, () => approve(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="text-[11.5px] rounded-lg gradient-primary text-white px-2.5 py-1.5 disabled:opacity-50"
                >
                  Approve
                </button> : null}
                {can(q.allowedActions, "executeApprovedNextStep") ? <button
                  onClick={() => run("Executed approved next step", q.id, () => executeNextStep(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="text-[11.5px] rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-navy disabled:opacity-50"
                >
                  Execute
                </button> : null}
                {can(q.allowedActions, "rejectApproval") ? <button
                  onClick={() => run("Rejected workflow step", q.id, () => reject(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="text-[11.5px] rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-error disabled:opacity-50"
                >
                  Reject
                </button> : null}
                {can(q.allowedActions, "escalateApproval") ? <button
                  onClick={() => run("Escalated workflow step", q.id, () => escalate(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="text-[11.5px] rounded-lg border border-warning/30 bg-white px-2.5 py-1.5 text-warning-text disabled:opacity-50"
                >
                  Escalate
                </button> : null}
                {can(q.allowedActions, "overrideApproval") ? <button
                  onClick={() => run("Recorded human override", q.id, () => override(q.id, reason.trim()))}
                  disabled={busyId === q.id || reason.trim().length < 3}
                  className="text-[11.5px] rounded-lg border border-violet/30 bg-white px-2.5 py-1.5 text-violet disabled:opacity-50"
                >
                  Override AI
                </button> : null}
                {!q.allowedActions.length ? <span className="text-[11px] text-stone">No action available</span> : null}
              </div>
            </div>
          ))}
        </GlassCard>
      )}
    </>
  );
}
