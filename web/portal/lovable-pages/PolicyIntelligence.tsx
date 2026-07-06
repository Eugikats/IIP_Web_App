"use client";
import { SectionHeader, GlassCard, Badge, ConfidenceBadge } from "@/components/iip/Primitives";
import { Search, ShieldCheck, BookOpen, Sparkles, AlertTriangle } from "lucide-react";

const clauses = [
  { id: "MTR-COMP · 4.2", title: "Own damage — vehicle collision", src: "Motor Comprehensive Policy Wording v3.2 · p.12", conf: 0.94 },
  { id: "MTR-COMP · 4.7", title: "Third-party property damage", src: "Motor Comprehensive Policy Wording v3.2 · p.18", conf: 0.91 },
  { id: "MTR-COMP · 6.1", title: "Excess and betterment", src: "Motor Comprehensive Policy Wording v3.2 · p.24", conf: 0.88 },
  { id: "MTR-COMP · 9.3", title: "Exclusions — unlicensed driver", src: "Motor Comprehensive Policy Wording v3.2 · p.31", conf: 0.96 },
];

export default function PolicyIntelligence() {
  return (
    <>
      <SectionHeader eyebrow="Policy Intelligence" title="Approved policy lookup & coverage Q&A" sub="Source-grounded answers from your approved policy library. Every response cites the underlying clause." />

      <div className="glass-elevated p-4 mb-6">
        <div className="flex items-center gap-3">
          <Search className="h-4 w-4 text-stone" />
          <input placeholder="Is rear-end collision covered under MTR-8821? What is the excess?" className="flex-1 bg-transparent outline-none text-[14px] placeholder:text-stone text-navy" />
          <button className="rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2 shadow-l2">Ask</button>
        </div>
      </div>

      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 space-y-4">
          <GlassCard level={3}>
            <div className="flex items-center gap-2 mb-2">
              <div className="h-8 w-8 rounded-xl gradient-primary grid place-items-center"><Sparkles className="h-4 w-4 text-white" /></div>
              <div>
                <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Coverage explanation</div>
                <div className="text-[14px] font-semibold text-navy">Rear-end collision under MTR-8821</div>
              </div>
              <div className="ml-auto"><ConfidenceBadge value={0.88} /></div>
            </div>
            <p className="text-[13.5px] leading-[21px] text-slate2">
              Under Motor Comprehensive Policy <b>MTR-8821</b>, own-damage arising from a collision with a third-party vehicle is covered subject to clause 4.2.
              A standard excess of <b>UGX 250,000</b> applies (clause 6.1). Third-party property damage is covered under clause 4.7 up to the sum insured.
              Coverage is void if the driver at the time of the incident is unlicensed (exclusion 9.3).
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              {clauses.slice(0,3).map(c => <Badge key={c.id} tone="info">{c.id}</Badge>)}
            </div>
          </GlassCard>

          <GlassCard>
            <div className="text-[14px] font-semibold text-navy mb-3">Cited clauses</div>
            <div className="space-y-2">
              {clauses.map(c => (
                <div key={c.id} className="rounded-xl border border-cloud bg-white/80 p-3 flex items-center gap-3">
                  <div className="h-9 w-9 rounded-lg bg-[hsl(var(--violet)/0.10)] text-violet2 grid place-items-center"><BookOpen className="h-4 w-4" /></div>
                  <div className="flex-1 min-w-0">
                    <div className="text-[13px] font-medium text-navy">{c.title}</div>
                    <div className="text-[11.5px] text-stone truncate">{c.id} · {c.src}</div>
                  </div>
                  <ConfidenceBadge value={c.conf} />
                </div>
              ))}
            </div>
          </GlassCard>
        </div>

        <div className="space-y-4">
          <div className="rounded-2xl border border-[hsl(var(--warning)/0.28)] bg-[hsl(var(--warning)/0.08)] p-4">
            <div className="flex items-center gap-2 mb-1"><AlertTriangle className="h-3.5 w-3.5 text-warning" /><div className="text-[11px] uppercase tracking-[0.14em] text-[hsl(38_92%_36%)]">Not a legal interpretation</div></div>
            <p className="text-[13px] text-slate2 leading-snug">Policy Intelligence provides source-grounded guidance to prepare human review. It is not a binding legal or claims interpretation.</p>
          </div>

          <GlassCard>
            <div className="flex items-center gap-2 mb-2"><ShieldCheck className="h-3.5 w-3.5 text-info" /><div className="text-[11px] uppercase tracking-[0.14em] text-stone">Related policies</div></div>
            <div className="space-y-1.5 text-[13px] text-slate2">
              <div>MTR-COMM · Commercial Motor Wording v2.4</div>
              <div>MTR-TP · Third-party Wording v1.7</div>
              <div>REG · IRA Motor Guidelines 2024</div>
            </div>
          </GlassCard>

          <button className="w-full rounded-2xl border border-cloud bg-white text-navy text-[13px] font-medium py-2.5 hover-lift">Escalate to human policy expert</button>
        </div>
      </div>
    </>
  );
}
