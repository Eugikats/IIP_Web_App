"use client";
import { useState } from "react";
import { SectionHeader, GlassCard, Badge, RiskBadge } from "@/components/iip/Primitives";
import { Workflow } from "lucide-react";
import { toast } from "sonner";
import { approvals } from "@/lib/lovable-parity/mock";
import GuardrailBanner from "@/components/iip/GuardrailBanner";

export default function Workflows() {
  const [tab, setTab] = useState<"Task inbox" | "Approval queue" | "Escalations">("Approval queue");
  const filtered =
    tab === "Escalations" ? approvals.filter((a) => a.risk === "High")
    : tab === "Task inbox" ? approvals.filter((a) => a.risk !== "High")
    : approvals;

  return (
    <>
      <SectionHeader eyebrow="Workflow & Approvals" title="Human review queue" sub="Every step below advances a workflow — regulated outcomes remain with a named human reviewer." />
      <div className="mb-4"><GuardrailBanner text="Approval actions update workflow state only. Final regulated decisions — settle, deny, pay, bind, accuse fraud — require an explicit human owner." /></div>

      <div className="flex flex-wrap gap-1.5 mb-4">
        {(["Task inbox","Approval queue","Escalations"] as const).map((t) => (
          <button key={t} onClick={() => setTab(t)} className={`text-[12px] rounded-full px-3 py-1.5 border ${tab === t ? "bg-navy text-white border-navy" : "bg-white text-slate2 border-cloud"}`}>{t}</button>
        ))}
      </div>

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
              <button onClick={() => toast.success("Advanced to next workflow step")} className="text-[11.5px] rounded-lg gradient-primary text-white px-2.5 py-1.5">Approve</button>
              <button onClick={() => toast("Requested more information")} className="text-[11.5px] rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-navy">Request info</button>
              <button onClick={() => toast("Reassigned")} className="text-[11.5px] rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-navy">Reassign</button>
              <button onClick={() => toast("Rejected")} className="text-[11.5px] rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-error">Reject</button>
            </div>
          </div>
        ))}
      </GlassCard>

      <div className="mt-6 grid md:grid-cols-2 gap-4">
        <GlassCard>
          <div className="flex items-center gap-2 mb-3"><Workflow className="h-3.5 w-3.5 text-info" /><div className="text-[11px] uppercase tracking-[0.14em] text-stone">Decision trail · CLM-1064</div></div>
          <div className="relative pl-5">
            <div className="absolute left-1.5 top-1 bottom-1 w-px bg-cloud" />
            {[
              { t: "FNOL received", who: "System" },
              { t: "AI extraction complete", who: "Copilot" },
              { t: "Officer review", who: "S. Achieng · advanced next step" },
              { t: "Escalated to Supervisor", who: "S. Achieng" },
              { t: "Awaiting supervisor review", who: "J. Mugisha" },
            ].map((x, i, arr) => (
              <div key={i} className="relative pb-4">
                <div className={`absolute -left-[9px] top-1 h-2 w-2 rounded-full ring-2 ring-white ${i === arr.length - 1 ? "bg-warning" : "bg-info"}`} />
                <div className="text-[13px] text-navy">{x.t}</div>
                <div className="text-[11.5px] text-stone">{x.who}</div>
              </div>
            ))}
          </div>
        </GlassCard>

        <GlassCard>
          <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Escalations & workload</div>
          <div className="mt-2 text-[13px] text-slate2 space-y-1.5">
            <p>3 tasks currently escalated to Supervisor. Average time-to-decision this week: <b>4h 12m</b> (target 6h).</p>
            <p className="text-stone text-[12px]">J. Mugisha over capacity by ~18%. Reassignment recommended.</p>
            <div className="flex flex-wrap gap-1.5 pt-2">
              <Badge tone="warning">3 escalations</Badge>
              <Badge tone="info">9 approvals pending</Badge>
              <Badge tone="success">SLA on-track · 92%</Badge>
            </div>
          </div>
        </GlassCard>
      </div>
    </>
  );
}
