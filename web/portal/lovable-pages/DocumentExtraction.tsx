"use client";
import { useState } from "react";
import { toast } from "sonner";
import { FileText, ArrowLeft, ArrowRight, ZoomIn, RotateCw, Sparkles } from "lucide-react";
import { GlassCard, SectionHeader, Badge, ConfidenceBadge } from "@/components/iip/Primitives";

const initial = [
  { key: "Claim ID", value: "CLM-1048", conf: 0.99 },
  { key: "Policy Number", value: "MTR-8821", conf: 0.98 },
  { key: "Claimant Name", value: "Aisha Nakato", conf: 0.96 },
  { key: "National ID", value: "CM8412700ABC12", conf: 0.94 },
  { key: "Incident Date", value: "2026-07-03", conf: 0.88 },
  { key: "Incident Time", value: "17:40", conf: 0.62 },
  { key: "Location", value: "Kampala Road, Kampala", conf: 0.83 },
  { key: "Police Report Ref", value: "UPF/KLA/2098", conf: 0.91 },
  { key: "Vehicle Reg.", value: "UAX 456K", conf: 0.97 },
  { key: "Estimated Damage (UGX)", value: "4,200,000", conf: 0.72 },
  { key: "Officer Name", value: "PC J. Wamala", conf: 0.68 },
  { key: "Signature Present", value: "No", conf: 0.55 },
];

export default function DocumentExtraction() {
  const [fields, setFields] = useState(initial);

  return (
    <>
      <SectionHeader eyebrow="Document Intelligence" title="Extraction Review · Police Report" sub="CLM-1048 · Aisha Nakato · Confirm AI-extracted fields against source. Field-level confidence shown." action={
        <div className="flex items-center gap-2">
          <Badge tone="info">Doc classification · Police Report · 0.98</Badge>
          <Badge tone="warning">1 duplicate warning</Badge>
        </div>
      } />

      <div className="grid lg:grid-cols-5 gap-4">
        {/* Preview */}
        <div className="lg:col-span-3">
          <GlassCard className="p-4">
            <div className="flex items-center justify-between mb-3">
              <div className="flex items-center gap-2 text-[13px] text-navy"><FileText className="h-4 w-4 text-info" /> police_report_UPF_KLA_2098.pdf</div>
              <div className="flex items-center gap-1">
                <IconBtn><ArrowLeft className="h-3.5 w-3.5" /></IconBtn>
                <span className="text-[12px] text-stone px-2">1 / 3</span>
                <IconBtn><ArrowRight className="h-3.5 w-3.5" /></IconBtn>
                <IconBtn><ZoomIn className="h-3.5 w-3.5" /></IconBtn>
                <IconBtn><RotateCw className="h-3.5 w-3.5" /></IconBtn>
              </div>
            </div>
            <div className="aspect-[4/5] rounded-2xl border border-cloud bg-gradient-to-br from-mist to-white p-8 relative overflow-hidden">
              <div className="text-center text-[10.5px] uppercase tracking-[0.2em] text-stone">Uganda Police Force</div>
              <div className="text-center text-[14px] font-semibold text-navy mt-1">Traffic Incident Report</div>
              <div className="mt-4 text-[11.5px] text-slate2 space-y-1.5">
                <div>Reference: <span className="bg-[hsl(var(--info)/0.14)] px-1 rounded">UPF/KLA/2098</span></div>
                <div>Date: <span className="bg-[hsl(var(--info)/0.14)] px-1 rounded">03 Jul 2026</span> · Time: <span className="bg-[hsl(var(--warning)/0.18)] px-1 rounded">17:52</span></div>
                <div>Location: <span className="bg-[hsl(var(--info)/0.14)] px-1 rounded">Kampala Road, Kampala</span></div>
                <div>Complainant: <span className="bg-[hsl(var(--info)/0.14)] px-1 rounded">Aisha Nakato</span></div>
                <div>Vehicle: <span className="bg-[hsl(var(--info)/0.14)] px-1 rounded">UAX 456K</span> — Toyota Vitz, White</div>
                <div>Officer: <span className="bg-[hsl(var(--warning)/0.18)] px-1 rounded">PC J. Wamala</span></div>
                <div className="mt-4 leading-relaxed text-[11px] text-stone">Complainant vehicle was stationary at signalised intersection when third-party vehicle (registration UBK 189C) impacted the rear. No injuries. Both vehicles remained drivable. Scene attended 17:52…</div>
              </div>
              <div className="absolute bottom-6 right-6 rounded-lg border border-dashed border-warning bg-[hsl(var(--warning)/0.08)] px-2.5 py-1.5 text-[10.5px] text-[hsl(38_92%_36%)]">
                No officer signature detected
              </div>
            </div>
          </GlassCard>
        </div>

        {/* Fields */}
        <div className="lg:col-span-2 space-y-3">
          <GlassCard>
            <div className="flex items-center gap-2 mb-3">
              <Sparkles className="h-3.5 w-3.5 text-info" />
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Extracted fields · 12</div>
            </div>
            <div className="space-y-2.5">
              {fields.map((f, i) => (
                <div key={f.key} className="rounded-xl border border-cloud bg-white/80 px-3 py-2.5">
                  <div className="flex items-center justify-between mb-1">
                    <div className="text-[11px] text-stone">{f.key}</div>
                    <ConfidenceBadge value={f.conf} />
                  </div>
                  <input
                    value={f.value}
                    onChange={e => setFields(fs => fs.map((x, j) => j === i ? { ...x, value: e.target.value } : x))}
                    className="w-full bg-transparent outline-none text-[13.5px] text-navy"
                  />
                </div>
              ))}
            </div>
          </GlassCard>

          <div className="flex flex-col gap-2">
            <button onClick={() => toast.success("Extraction approved", { description: "Written to claim CLM-1048. Human approval logged." })} className="rounded-2xl gradient-primary text-white text-[13px] font-medium py-3 shadow-l2 hover-lift">
              Approve extraction
            </button>
            <button onClick={() => toast("Marked for re-upload", { description: "Customer will receive a WhatsApp request." })} className="rounded-2xl border border-cloud bg-white text-navy text-[13px] font-medium py-2.5 hover-lift">
              Send back for re-upload
            </button>
            <button onClick={() => toast("Flagged as low confidence", { description: "Escalated to Document Intelligence review." })} className="rounded-2xl border border-cloud bg-white text-navy text-[13px] font-medium py-2.5 hover-lift">
              Mark as low confidence
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
function IconBtn({ children }: { children: any }) {
  return <button className="h-8 w-8 grid place-items-center rounded-lg border border-cloud bg-white/80 text-slate2 hover:bg-white">{children}</button>;
}
