"use client";
import { useMemo, useState } from "react";
import { SectionHeader, GlassCard, Badge, RiskBadge } from "@/components/iip/Primitives";
import { toast } from "sonner";
import { useApprovals } from "@/lib/lovable-parity/data/approvals";
import GuardrailBanner from "@/components/iip/GuardrailBanner";

export default function Workflows() {
  const { approvals, loading, error, parityMode } = useApprovals();
  const [tab, setTab] = useState<"Task inbox" | "Approval queue" | "Escalations">("Approval queue");

  const filtered = useMemo(() =>
    tab === "Escalations" ? approvals.filter((a) => a.risk === "High")
    : tab === "Task inbox" ? approvals.filter((a) => a.risk !== "High")
    : approvals, [approvals, tab]);

  const gov = (label: string) => (parityMode ? toast.success(label) : toast("Recorded — advances workflow state only; audited."));

  return (
    <>
      <SectionHeader eyebrow="Workflow & Approvals" title="Human review queue" sub="Every step below advances a workflow — regulated outcomes remain with a named human reviewer." />
      <div className="mb-4"><GuardrailBanner text="Approval actions update workflow state only. Final regulated decisions — settle, deny, pay, bind, accuse fraud — require an explicit human owner." /></div>

      <div className="flex flex-wrap gap-1.5 mb-4">
        {(["Task inbox","Approval queue","Escalations"] as const).map((t) => (
          <button key={t} onClick={() => setTab(t)} className={`text-[12px] rounded-full px-3 py-1.5 border ${tab === t ? "bg-navy text-white border-navy" : "bg-white text-slate2 border-cloud"}`}>{t}</button>
        ))}
      </div>

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
              </div>
              <div className="col-span-2 text-[13px] text-slate2">{q.requester}</div>
              <div className="col-span-2 text-[13px] text-slate2">{q.reviewer}</div>
              <div className="col-span-1"><RiskBadge risk={q.risk} /></div>
              <div className="col-span-1 text-[12px] text-stone">{q.ageHrs}h</div>
              <div className="col-span-2 flex justify-end gap-1.5 flex-wrap">
                <button onClick={() => gov("Advanced to next workflow step")} className="text-[11.5px] rounded-lg gradient-primary text-white px-2.5 py-1.5">Approve</button>
                <button onClick={() => gov("Requested more information")} className="text-[11.5px] rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-navy">Request info</button>
                <button onClick={() => gov("Reassigned")} className="text-[11.5px] rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-navy">Reassign</button>
                <button onClick={() => gov("Rejected")} className="text-[11.5px] rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-error">Reject</button>
              </div>
            </div>
          ))}
        </GlassCard>
      )}
    </>
  );
}
