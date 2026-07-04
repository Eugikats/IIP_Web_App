"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { AlertTriangle, Copy, User, Calendar, IdCard, Building2, TrendingUp } from "lucide-react";

const signals = [
  { icon: Copy, title: "Duplicate claim pattern", detail: "3 claims from same vehicle registration in 45 days", risk: "High", claim: "CLM-1071" },
  { icon: User, title: "Mismatched claimant names", detail: "Claim form vs National ID discrepancy", risk: "Medium", claim: "CLM-1052" },
  { icon: Calendar, title: "Mismatched incident dates", detail: "Police report time differs by 12 min from FNOL", risk: "Advisory Signal", claim: "CLM-1048" },
  { icon: IdCard, title: "Expired driving permit", detail: "Permit expired 8 months before incident date", risk: "High", claim: "CLM-1064" },
  { icon: Building2, title: "Repeated provider", detail: "SwiftFix Auto Ltd appears on 5 claims this month", risk: "Advisory Signal", claim: "Multiple" },
  { icon: TrendingUp, title: "Inflated quotation indicator", detail: "Quotation 22% above regional median", risk: "Medium", claim: "CLM-1048" },
];

export default function FraudIntelligence() {
  return (
    <>
      <SectionHeader eyebrow="Fraud Intelligence" title="Advisory risk signals" sub="Signals are advisory and require human investigation. IIP does not accuse customers of fraud or make fraud determinations." />
      <div className="mb-4"><GuardrailBanner text="Use safer wording: Fraud signal detected · Requires investigation · Potential anomaly · Advisory signal. Never: fraud confirmed." /></div>

      <div className="grid md:grid-cols-4 gap-3 mb-6">
        {[
          { l: "Advisory signals (7d)", v: 24, t: "violet" as const },
          { l: "Requires investigation", v: 6, t: "warning" as const },
          { l: "Under human review", v: 4, t: "info" as const },
          { l: "Closed — cleared", v: 12, t: "success" as const },
        ].map(k => (
          <div key={k.l} className="glass-subtle p-4">
            <div className="text-[11.5px] text-stone">{k.l}</div>
            <div className="mt-1 text-[26px] leading-none font-semibold text-navy">{k.v}</div>
            <Badge tone={k.t} className="mt-2">Advisory</Badge>
          </div>
        ))}
      </div>

      <div className="grid md:grid-cols-2 gap-3">
        {signals.map((s, i) => (
          <GlassCard key={i} className="hover-lift">
            <div className="flex items-start gap-3">
              <div className="h-10 w-10 rounded-xl bg-[hsl(var(--violet)/0.10)] text-violet2 grid place-items-center"><s.icon className="h-4 w-4" /></div>
              <div className="flex-1">
                <div className="flex items-center gap-2">
                  <div className="text-[14px] font-semibold text-navy">{s.title}</div>
                  <Badge tone={s.risk === "High" ? "error" : s.risk === "Medium" ? "warning" : "violet"}>{s.risk}</Badge>
                </div>
                <div className="text-[12.5px] text-slate2 mt-1">{s.detail}</div>
                <div className="mt-2 text-[11.5px] text-stone">Linked: {s.claim}</div>
              </div>
            </div>
            <div className="mt-3 flex items-center justify-between">
              <div className="inline-flex items-center gap-1.5 text-[11.5px] text-[hsl(38_92%_36%)]"><AlertTriangle className="h-3 w-3" /> Advisory only · human review required</div>
              <button className="text-[12px] text-info hover:underline">Open investigation →</button>
            </div>
          </GlassCard>
        ))}
      </div>
    </>
  );
}
