"use client";
import { useParams, Link } from "@/components/iip/NextRouterCompat";
import { toast } from "sonner";
import { ArrowLeft, FileText, Sparkles, CheckCircle2, AlertTriangle, ShieldCheck, Download, Send, MessageSquareText } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge, SLABadge, RiskBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import MotorLifecycle from "@/components/iip/MotorLifecycle";
import CompletenessCard from "@/components/iip/CompletenessCard";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { claims } from "@/lib/lovable-parity/mock";

export default function ClaimReview() {
  const { id } = useParams();
  const c = claims.find(x => x.id === id) ?? claims[0];

  const timeline = [
    { t: "Today 09:42", who: "AI Copilot", text: "Extracted 14 fields from police report (avg conf 0.86)." },
    { t: "Today 09:31", who: "AI Copilot", text: "Advisory: repair quotation 22% above regional median." },
    { t: "Today 08:04", who: c.customer, text: "Submitted vehicle photos via WhatsApp (5 images)." },
    { t: "Yesterday 17:22", who: "T. Okello", text: "Assigned claim to self." },
    { t: "Yesterday 16:41", who: "System", text: "FNOL received via WhatsApp channel." },
  ];

  const missingDocs = ["Driving permit (front)", "Traffic officer signature"];
  const documents = [
    { name: "FNOL form", status: "Extracted", conf: 0.94 },
    { name: "Police report", status: "Extracted", conf: 0.86 },
    { name: "National ID", status: "Extracted", conf: 0.98 },
    { name: "Vehicle photos (5)", status: "Classified", conf: 0.91 },
    { name: "Repair quotation", status: "Advisory flag", conf: 0.72 },
    { name: "Policy schedule", status: "Extracted", conf: 0.95 },
  ];

  return (
    <>
      <div className="flex items-center gap-2 text-[12px] text-stone mb-4">
        <Link to="/app/claims" className="inline-flex items-center gap-1 hover:text-navy"><ArrowLeft className="h-3.5 w-3.5" /> Work Queue</Link>
        <span>/</span><span className="text-navy">{c.id}</span>
      </div>

      <SectionHeader
        eyebrow={`Case Review · ${c.policy}`}
        title={`${c.id} · ${c.customer}`}
        sub={c.incident}
        action={
          <div className="flex items-center gap-2">
            <StatusBadge status={c.status} />
            <SLABadge sla={c.sla} />
            <RiskBadge risk={c.risk} />
          </div>
        }
      />

      <div className="mb-4"><GuardrailBanner text="AI prepares this claim's work — extractions, summaries, drafts, and advisory signals. Settlement, denial, repudiation, payment, and fraud determination remain with the reviewing officer." /></div>

      <div className="mb-4"><MotorLifecycle current={6} /></div>

      <div className="grid lg:grid-cols-3 gap-4">
        {/* Left column */}
        <div className="lg:col-span-2 space-y-4">
          {/* AI summary */}
          <GlassCard level={3}>
            <div className="flex items-center justify-between mb-3">
              <div className="flex items-center gap-2">
                <div className="h-8 w-8 rounded-xl gradient-primary grid place-items-center shadow-l2"><Sparkles className="h-4 w-4 text-white" /></div>
                <div>
                  <div className="text-[11px] uppercase tracking-[0.14em] text-stone">AI-generated claim summary</div>
                  <div className="text-[14px] font-semibold text-navy">Prepared for human review</div>
                </div>
              </div>
              <ConfidenceBadge value={0.86} />
            </div>
            <p className="text-[13.5px] leading-[21px] text-slate2">
              Policyholder <b>{c.customer}</b> reports a <b>rear-end collision</b> on Kampala Road at approx. 17:40 involving one third-party vehicle.
              Police report reference <b>UPF/KLA/2098</b> attached. Vehicle photos indicate rear bumper and boot deformation, consistent with statement.
              Repair quotation of <b>UGX 4,200,000</b> is 22% above the regional median for the make/model — Copilot recommends secondary assessor review before settlement.
              Coverage under policy <b>{c.policy}</b> (Motor Comprehensive) appears applicable to the loss event, subject to human confirmation.
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              <Badge tone="info">Evidence · 3 links</Badge>
              <Badge tone="violet">Policy clause 4.2 · 4.7</Badge>
              <Badge tone="warning">Advisory · quotation variance</Badge>
            </div>
          </GlassCard>

          {/* Human decision area */}
          <GlassCard>
            <div className="flex items-center justify-between mb-3">
              <div className="text-[14px] font-semibold text-navy">Human review actions</div>
              <div className="text-[11.5px] text-stone">AI prepares work. You make the decision.</div>
            </div>
            <div className="grid sm:grid-cols-2 gap-2.5">
              {[
                { label: "Prepare recommendation", icon: FileText, tone: "primary" as const },
                { label: "Request supervisor review", icon: ShieldCheck, tone: "secondary" as const },
                { label: "Draft customer update", icon: MessageSquareText, tone: "secondary" as const },
                { label: "Mark evidence reviewed", icon: CheckCircle2, tone: "secondary" as const },
                { label: "Send document follow-up", icon: Send, tone: "secondary" as const },
                { label: "Export claim pack", icon: Download, tone: "secondary" as const },
              ].map(a => (
                <button
                  key={a.label}
                  onClick={() => toast.success(`${a.label} · queued for review`, { description: "This is a prototype — no backend action performed." })}
                  className={`inline-flex items-center gap-2 rounded-2xl px-4 py-3 text-[13px] font-medium hover-lift ${
                    a.tone === "primary"
                      ? "gradient-primary text-white shadow-l2"
                      : "bg-white border border-cloud text-navy"
                  }`}>
                  <a.icon className="h-4 w-4" /> {a.label}
                </button>
              ))}
            </div>
          </GlassCard>

          {/* Timeline */}
          <GlassCard>
            <div className="text-[14px] font-semibold text-navy mb-4">Incident & activity timeline</div>
            <div className="relative pl-5">
              <div className="absolute left-1.5 top-1 bottom-1 w-px bg-cloud" />
              {timeline.map((t, i) => (
                <div key={i} className="relative pb-4">
                  <div className="absolute -left-[9px] top-1 h-2 w-2 rounded-full bg-info ring-2 ring-white" />
                  <div className="text-[12px] text-stone">{t.t} · {t.who}</div>
                  <div className="text-[13px] text-navy mt-0.5">{t.text}</div>
                </div>
              ))}
            </div>
          </GlassCard>

          {/* Documents */}
          <GlassCard>
            <div className="flex items-center justify-between mb-3">
              <div className="text-[14px] font-semibold text-navy">Documents received</div>
              <Link to="/app/claims/CLM-1048/extraction" className="text-[12px] text-info hover:underline">Open extraction review →</Link>
            </div>
            <div className="grid sm:grid-cols-2 gap-2">
              {documents.map(d => (
                <div key={d.name} className="rounded-xl border border-cloud bg-white/80 p-3 flex items-center gap-3">
                  <div className="h-9 w-9 rounded-lg bg-[hsl(var(--info)/0.08)] grid place-items-center text-info"><FileText className="h-4 w-4" /></div>
                  <div className="flex-1 min-w-0">
                    <div className="text-[13px] font-medium text-navy truncate">{d.name}</div>
                    <div className="text-[11px] text-stone">{d.status}</div>
                  </div>
                  <ConfidenceBadge value={d.conf} />
                </div>
              ))}
            </div>
          </GlassCard>
        </div>

        {/* Right column */}
        <div className="space-y-4">
          <GlassCard>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Policy & customer</div>
            <div className="mt-2 space-y-2 text-[13px]">
              <Row k="Customer" v={c.customer} />
              <Row k="Policy" v={`${c.policy} · Motor Comprehensive`} />
              <Row k="Sum insured" v="UGX 42,000,000" />
              <Row k="Excess" v="UGX 250,000" />
              <Row k="Renewal" v="12 Aug 2026" />
              <Row k="Channel" v={c.channel} />
              <Row k="Region" v={c.region} />
            </div>
          </GlassCard>

          <CompletenessCard
            score={0.78}
            items={[
              { label: "Claim form", present: true },
              { label: "Police report", present: true },
              { label: "National ID", present: true },
              { label: "Driving permit", present: false },
              { label: "Vehicle photos", present: true },
              { label: "Repair quotation", present: true, lowConfidence: true },
              { label: "Policy schedule", present: true },
              { label: "Traffic officer signature", present: false },
            ]}
          />



          <div className="rounded-2xl border border-[hsl(var(--warning)/0.28)] bg-[hsl(var(--warning)/0.08)] p-4">
            <div className="flex items-center gap-2 mb-1"><AlertTriangle className="h-3.5 w-3.5 text-warning" />
              <div className="text-[11px] uppercase tracking-[0.14em] text-[hsl(38_92%_36%)]">Advisory fraud signal</div>
            </div>
            <p className="text-[13px] text-slate2 leading-snug">
              Repair quotation from provider <b>SwiftFix Auto Ltd</b> exceeds regional median by 22%. Provider appears on 3 other active claims this month.
              Advisory only — requires human investigation. Not a fraud determination.
            </p>
          </div>

          <GlassCard>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Audit trail (last 5)</div>
            <div className="mt-2 space-y-1.5 text-[12px]">
              {["Field extraction · run_9f2a","Advisory signal · run_9f04","Human view · T. Okello","Draft customer update · run_9f19","FNOL received · WhatsApp"].map((x,i) => (
                <div key={i} className="flex items-center justify-between text-slate2"><span>{x}</span><span className="text-stone">EV-{88291 - i * 21}</span></div>
              ))}
            </div>
          </GlassCard>
        </div>
      </div>
    </>
  );
}
function Row({ k, v }: { k: string; v: string }) {
  return <div className="flex items-baseline justify-between gap-2"><span className="text-stone text-[11.5px]">{k}</span><span className="text-navy text-right">{v}</span></div>;
}
