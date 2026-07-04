"use client";
import { useState } from "react";
import { SectionHeader, GlassCard, Badge, RiskBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { submissions } from "@/lib/lovable-parity/mock";
import { Sparkles } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { toast } from "sonner";

export default function Underwriting() {
  const [active, setActive] = useState(submissions[0]);
  return (
    <>
      <SectionHeader eyebrow="Underwriting Intelligence" title="Submission queue" sub="Broker and direct submissions summarised for underwriters. AI drafts questions and highlights gaps — underwriters decide." />
      <div className="mb-4"><GuardrailBanner text="AI does not make final underwriting acceptance or rejection. It summarises, recommends, drafts questions, and escalates." /></div>

      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 space-y-3">
          {submissions.map(s => (
            <GlassCard key={s.id} className={`hover-lift cursor-pointer ${active.id === s.id ? "ring-1 ring-info/30" : ""}`} >
              <div onClick={() => setActive(s)} className="flex items-start justify-between gap-4">
                <div>
                  <div className="text-[11.5px] text-stone">{s.broker}</div>
                  <div className="text-[15px] font-semibold text-navy">{s.id} · {s.applicant}</div>
                  <div className="text-[12px] text-stone mt-0.5">{s.product} · Sum insured {s.sumInsured}</div>
                  <div className="mt-2 flex flex-wrap gap-2">
                    <RiskBadge risk={s.risk} />
                    <Badge tone={s.missing ? "warning" : "success"}>{s.missing ? `${s.missing} missing` : "Complete"}</Badge>
                    <ConfidenceBadge value={s.confidence} />
                    <Badge tone="neutral">{s.ageHrs}h in queue</Badge>
                  </div>
                </div>
                <div className="flex flex-col gap-2">
                  <button onClick={() => toast.success("Question drafted for broker")} className="text-[12.5px] rounded-xl gradient-primary text-white px-3 py-1.5">Draft questions</button>
                  <button onClick={() => toast("Escalated for underwriter review")} className="text-[12.5px] rounded-xl border border-cloud bg-white px-3 py-1.5 text-navy">Escalate for review</button>
                </div>
              </div>
            </GlassCard>
          ))}
        </div>

        <GlassCard>
          <div className="flex items-center gap-2 mb-3"><Sparkles className="h-3.5 w-3.5 text-info" /><div className="text-[11px] uppercase tracking-[0.14em] text-stone">Risk summary · {active.id}</div></div>
          <ul className="text-[13px] text-slate2 space-y-1.5 list-disc pl-4">
            <li>{active.applicant} via {active.broker}.</li>
            <li>{active.product}, sum insured {active.sumInsured}.</li>
            <li>{active.missing} missing information item{active.missing === 1 ? "" : "s"}.</li>
            <li>Confidence in extracted fields: {(active.confidence * 100).toFixed(0)}%.</li>
          </ul>
          <div className="mt-3 rounded-xl border border-cloud bg-white/80 p-3 text-[12.5px] text-slate2">
            <div className="text-[11px] text-stone mb-1">Draft question to broker</div>
            Please share the outstanding NCD certificate and a recent valuation report before we can present terms. This is a draft — send only after underwriter approval.
          </div>
        </GlassCard>
      </div>
    </>
  );
}
