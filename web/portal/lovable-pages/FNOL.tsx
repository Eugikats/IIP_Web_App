"use client";
import { useState } from "react";
import { toast } from "sonner";
import { Check, ChevronRight, Sparkles, Upload, FileText } from "lucide-react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";

const steps = ["Customer", "Policy", "Incident", "Vehicle & Driver", "Documents", "Review"];
const checklist = ["Claim form", "Police report", "National ID", "Driving permit", "Vehicle photos", "Repair quotation", "Policy schedule"];

export default function FNOL() {
  const [step, setStep] = useState(2);
  const completeness = Math.round(((step + 1) / steps.length) * 100);

  return (
    <>
      <SectionHeader eyebrow="Claims Intelligence" title="New Claim · First Notice of Loss" sub="Structured intake for motor claims. AI assists with extraction and completeness; you confirm every field." />

      {/* Stepper */}
      <div className="glass-subtle p-4 mb-6">
        <div className="flex items-center gap-2 flex-wrap">
          {steps.map((s, i) => (
            <div key={s} className="flex items-center gap-2">
              <button onClick={() => setStep(i)} className={`inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-[12px] transition ${i === step ? "gradient-primary text-white shadow-l2" : i < step ? "bg-white border border-cloud text-navy" : "text-stone"}`}>
                {i < step ? <Check className="h-3 w-3" /> : <span className="h-4 w-4 grid place-items-center rounded-full border border-current text-[10px]">{i+1}</span>}
                {s}
              </button>
              {i < steps.length - 1 && <ChevronRight className="h-3 w-3 text-stone" />}
            </div>
          ))}
          <div className="ml-auto text-[12px] text-stone">Completeness · <span className="text-navy font-medium">{completeness}%</span></div>
        </div>
        <div className="mt-3 h-1.5 rounded-full bg-cloud overflow-hidden"><div className="h-full gradient-primary" style={{ width: `${completeness}%` }} /></div>
      </div>

      <div className="grid lg:grid-cols-3 gap-4">
        <GlassCard className="lg:col-span-2">
          <div className="text-[14px] font-semibold text-navy mb-4">Incident details</div>
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Date of incident" value="2026-07-03" />
            <Field label="Time" value="17:40" />
            <Field label="Location" value="Kampala Road, Kampala" className="sm:col-span-2" />
            <Field label="Incident type" value="Rear-end collision" />
            <Field label="Third party involved" value="Yes · 1 vehicle" />
            <Field label="Police report reference" value="UPF/KLA/2098" />
            <Field label="Estimated damage" value="UGX 4,200,000" />
            <div className="sm:col-span-2">
              <label className="text-[11.5px] font-medium text-slate2">Narrative</label>
              <textarea rows={4} defaultValue="Vehicle was stationary at traffic light on Kampala Road when a third-party vehicle failed to stop and impacted the rear. No injuries reported. Police attended scene."
                className="mt-1 w-full rounded-2xl border border-cloud bg-white/80 px-4 py-3 text-[13.5px] text-navy outline-none focus:ring-2 focus:ring-info/40" />
            </div>
          </div>

          <div className="mt-6 flex items-center justify-between">
            <button onClick={() => setStep(Math.max(0, step - 1))} className="text-[13px] rounded-2xl border border-cloud bg-white px-4 py-2.5 text-navy">Back</button>
            <div className="flex gap-2">
              <button onClick={() => toast("Draft saved", { description: "You can resume this intake anytime." })} className="text-[13px] rounded-2xl border border-cloud bg-white px-4 py-2.5 text-navy">Save draft</button>
              <button onClick={() => setStep(Math.min(steps.length - 1, step + 1))} className="text-[13px] rounded-2xl gradient-primary text-white px-4 py-2.5 shadow-l2">Continue</button>
            </div>
          </div>
        </GlassCard>

        <div className="space-y-4">
          <GlassCard>
            <div className="flex items-center gap-2 mb-3">
              <Sparkles className="h-3.5 w-3.5 text-info" />
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Copilot assist</div>
            </div>
            <div className="text-[13px] text-slate2 space-y-2">
              <div>• Extracted 6 fields from FNOL form (avg conf 0.91).</div>
              <div>• Location cross-checks with GPS metadata on photo 3.</div>
              <div>• Potential inconsistency: time on police report is 17:52.</div>
            </div>
          </GlassCard>

          <GlassCard>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2">Required documents</div>
            <ul className="space-y-1.5">
              {checklist.map((d, i) => {
                const done = i < 4;
                return (
                  <li key={d} className="flex items-center justify-between text-[13px]">
                    <span className="text-slate2">{d}</span>
                    {done ? <Badge tone="success">Received</Badge> : <Badge tone="warning">Missing</Badge>}
                  </li>
                );
              })}
            </ul>
            <button className="mt-3 w-full inline-flex items-center justify-center gap-2 text-[12.5px] rounded-xl border border-cloud bg-white py-2 text-navy hover:bg-white/80">
              <Upload className="h-3.5 w-3.5" /> Upload document
            </button>
          </GlassCard>

          <GlassCard>
            <div className="flex items-center gap-2 mb-2"><FileText className="h-3.5 w-3.5 text-stone" /><div className="text-[11px] uppercase tracking-[0.14em] text-stone">Next required action</div></div>
            <div className="text-[13px] text-slate2">Request driving permit (front) from customer via WhatsApp before submitting for review.</div>
          </GlassCard>
        </div>
      </div>
    </>
  );
}

function Field({ label, value, className = "" }: { label: string; value: string; className?: string }) {
  return (
    <div className={className}>
      <label className="text-[11.5px] font-medium text-slate2">{label}</label>
      <input defaultValue={value} className="mt-1 w-full rounded-2xl border border-cloud bg-white/80 px-4 py-3 text-[13.5px] text-navy outline-none focus:ring-2 focus:ring-info/40" />
    </div>
  );
}
