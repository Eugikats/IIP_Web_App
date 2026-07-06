"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { AlertTriangle, ShieldAlert } from "lucide-react";
import { useFraudSignals, type FraudSeverity } from "@/lib/lovable-parity/data/fraud";

const badgeTone: Record<FraudSeverity, "error" | "warning" | "violet"> = {
  Investigation: "error",
  Anomaly: "warning",
  Advisory: "violet",
};

const badgeLabel: Record<FraudSeverity, string> = {
  Investigation: "Requires investigation",
  Anomaly: "Potential anomaly",
  Advisory: "Advisory signal",
};

export default function FraudIntelligence() {
  const { signals, loading, error, counts } = useFraudSignals();

  return (
    <>
      <SectionHeader eyebrow="Fraud Intelligence" title="Advisory risk signals" sub="Signals are advisory and require human investigation. IIP does not accuse customers of fraud or make fraud determinations." />
      <div className="mb-4"><GuardrailBanner text="Use safer wording: Fraud signal detected · Requires investigation · Potential anomaly · Advisory signal. Never: fraud confirmed." /></div>

      <div className="grid md:grid-cols-4 gap-3 mb-6">
        {[
          { l: "Total advisory signals", v: counts.total, t: "violet" as const },
          { l: "Requires investigation", v: counts.investigation, t: "error" as const },
          { l: "Potential anomalies", v: counts.anomaly, t: "warning" as const },
          { l: "Advisory only", v: counts.advisory, t: "info" as const },
        ].map(k => (
          <div key={k.l} className="glass-subtle p-4">
            <div className="text-[11.5px] text-stone">{k.l}</div>
            <div className="mt-1 text-[26px] leading-none font-semibold text-navy">{k.v}</div>
            <Badge tone={k.t} className="mt-2">Advisory</Badge>
          </div>
        ))}
      </div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Computing advisory signals…</div></GlassCard>
      ) : signals.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <ShieldAlert className="h-6 w-6 text-stone" />
            <div className="text-navy font-medium">No advisory signals</div>
            <div className="text-[12.5px] text-stone max-w-md">
              {error
                ? error
                : "The rule engine found no advisory patterns in the current claims for this workspace. Signals appear here as claims accumulate — nothing is fabricated."}
            </div>
          </div>
        </GlassCard>
      ) : (
        <div className="grid md:grid-cols-2 gap-3">
          {signals.map((s) => (
            <GlassCard key={s.id} className="hover-lift">
              <div className="flex items-start gap-3">
                <div className="h-10 w-10 rounded-xl bg-[hsl(var(--violet)/0.10)] text-violet2 grid place-items-center"><AlertTriangle className="h-4 w-4" /></div>
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <div className="text-[14px] font-semibold text-navy">{s.title}</div>
                    <Badge tone={badgeTone[s.severity]}>{badgeLabel[s.severity]}</Badge>
                  </div>
                  <div className="text-[12.5px] text-slate2 mt-1">{s.detail}</div>
                  <div className="mt-2 text-[11.5px] text-stone">Linked: {s.linked}</div>
                  {s.evidence.length > 0 ? (
                    <div className="mt-1.5 flex flex-wrap gap-1">
                      {s.evidence.slice(0, 3).map((e, idx) => (
                        <span key={idx} className="rounded-full bg-white/70 border border-cloud px-2 py-0.5 text-[10.5px] text-stone">{e}</span>
                      ))}
                    </div>
                  ) : null}
                </div>
              </div>
              <div className="mt-3 flex items-center justify-between">
                <div className="inline-flex items-center gap-1.5 text-[11.5px] text-[hsl(38_92%_36%)]"><AlertTriangle className="h-3 w-3" /> Advisory only · human review required</div>
                <button className="text-[12px] text-info hover:underline">Open investigation →</button>
              </div>
            </GlassCard>
          ))}
        </div>
      )}
    </>
  );
}
