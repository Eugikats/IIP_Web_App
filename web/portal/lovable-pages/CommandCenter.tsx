"use client";
import { ArrowRight, Sparkles, ArrowUpRight, ArrowDownRight, MessagesSquare, FileSearch, ShieldCheck, AlertTriangle } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge, SLABadge, RiskBadge } from "@/components/iip/Primitives";
import { metrics, claims, activityStream } from "@/lib/lovable-parity/mock";
import { Link } from "@/components/iip/NextRouterCompat";

export default function CommandCenter() {
  return (
    <>
      <SectionHeader
        eyebrow="Command Center"
        title="Good morning, Timothy."
        sub="Here is what needs attention across Motor Claims and Customer Operations today."
        action={
          <div className="hidden md:flex items-center gap-2 text-[11.5px] text-stone">
            <span className="h-2 w-2 rounded-full bg-success" /> All systems healthy · updated 12s ago
          </div>
        }
      />

      {/* AI Command Bar */}
      <div className="glass-elevated p-5 mb-8">
        <div className="flex items-center gap-3">
          <div className="h-9 w-9 rounded-xl gradient-primary grid place-items-center shadow-l2">
            <Sparkles className="h-4 w-4 text-white" />
          </div>
          <input
            placeholder="Ask IIP about claims, documents, customers, policies, or workflows…"
            className="flex-1 bg-transparent outline-none text-[14.5px] placeholder:text-stone text-navy"
          />
          <button className="hidden md:inline-flex items-center gap-1.5 rounded-xl border border-cloud bg-white/70 px-3 py-1.5 text-[12px] text-slate2">CLM-1048</button>
          <button className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2.5 shadow-l2 hover-lift">
            Ask <ArrowRight className="h-4 w-4" />
          </button>
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          {[
            "Summarize CLM-1048 with evidence",
            "Which claims will breach SLA today?",
            "Draft a customer update for CLM-1082",
            "List advisory fraud signals this week",
          ].map((s) => (
            <button key={s} className="text-[12px] text-slate2 rounded-full border border-cloud bg-white/70 px-3 py-1.5 hover:bg-white transition">
              {s}
            </button>
          ))}
        </div>
      </div>

      {/* Metrics */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-8">
        {metrics.map((m) => {
          const up = m.delta.startsWith("+");
          const flat = m.delta === "0";
          return (
            <div key={m.label} className="glass-subtle p-4 hover-lift">
              <div className="text-[11.5px] text-stone">{m.label}</div>
              <div className="mt-1.5 flex items-end justify-between">
                <div className="text-[26px] leading-none font-semibold text-navy tracking-tight">{m.value.toLocaleString()}</div>
                {!flat && (
                  <div className={`text-[11px] inline-flex items-center gap-0.5 ${up ? "text-success" : "text-error"}`}>
                    {up ? <ArrowUpRight className="h-3 w-3" /> : <ArrowDownRight className="h-3 w-3" />} {m.delta}
                  </div>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* Priority work */}
      <div className="grid lg:grid-cols-3 gap-4 mb-8">
        <PriorityCard
          icon={FileSearch}
          tone="violet"
          eyebrow="Claims requiring review"
          count={7}
          items={[
            { title: "CLM-1048 · Aisha Nakato", meta: "Rear-end collision · SLA at risk in 3h 12m" },
            { title: "CLM-1064 · Samuel Otieno", meta: "Third-party injury · Escalated" },
            { title: "CLM-1082 · Lydia Namutebi", meta: "Boda-boda impact · 2 docs missing" },
          ]}
        />
        <PriorityCard
          icon={AlertTriangle}
          tone="warning"
          eyebrow="Missing document follow-ups"
          count={14}
          items={[
            { title: "CLM-1052", meta: "Police report · Driving permit · Repair quote" },
            { title: "CLM-1071", meta: "National ID · Policy schedule + 2" },
            { title: "CLM-1082", meta: "Vehicle photos (rear, plate)" },
          ]}
        />
        <PriorityCard
          icon={ShieldCheck}
          tone="info"
          eyebrow="Approval queue"
          count={9}
          items={[
            { title: "CLM-1090 · Ready for export", meta: "Approve next workflow step" },
            { title: "CLM-1074 · Draft customer update", meta: "Awaiting supervisor" },
            { title: "CLM-1061 · Extraction pack", meta: "12 fields · avg conf. 91%" },
          ]}
        />
      </div>

      {/* Recommended actions */}
      <GlassCard className="mb-8">
        <div className="flex items-center justify-between mb-4">
          <div>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Recommended next actions</div>
            <div className="text-[15px] font-semibold text-navy mt-0.5">Prepared by Copilot · human review required</div>
          </div>
          <Badge tone="violet"><Sparkles className="h-3 w-3" /> AI-prepared</Badge>
        </div>
        <div className="space-y-2.5">
          {[
            "Reassign 4 aging claims from J. Mugisha to S. Achieng — balances workload by ~18%.",
            "Send bulk document-follow-up on 6 WhatsApp threads awaiting police report.",
            "Escalate CLM-1064 to Supervisor · third-party injury exceeds authority threshold.",
            "Approve document extraction pack on CLM-1090 · 12/12 fields · avg conf. 96%.",
          ].map((t, i) => (
            <div key={i} className="flex items-center justify-between gap-4 rounded-2xl border border-cloud bg-white/70 px-4 py-3 hover-lift">
              <div className="flex items-start gap-3">
                <div className="mt-0.5 h-6 w-6 rounded-lg bg-[hsl(var(--info)/0.08)] text-info grid place-items-center text-[11px] font-semibold">{i + 1}</div>
                <div className="text-[13.5px] text-slate2">{t}</div>
              </div>
              <button className="text-[12px] text-info hover:underline shrink-0">Review →</button>
            </div>
          ))}
        </div>
      </GlassCard>

      {/* Activity + priority list */}
      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 glass-subtle p-5">
          <div className="flex items-center justify-between mb-4">
            <div>
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Priority claims</div>
              <div className="text-[15px] font-semibold text-navy mt-0.5">Requiring your attention</div>
            </div>
            <Link to="/app/claims" className="text-[12px] text-info hover:underline">Open work queue →</Link>
          </div>
          <div className="divide-y divide-cloud/80">
            {claims.slice(0, 5).map((c) => (
              <div key={c.id} className="py-3 flex items-center gap-4">
                <div className="w-24 shrink-0">
                  <div className="text-[12.5px] font-medium text-navy">{c.id}</div>
                  <div className="text-[11px] text-stone">{c.policy}</div>
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-[13px] text-navy truncate">{c.customer}</div>
                  <div className="text-[11.5px] text-stone truncate">{c.incident}</div>
                </div>
                <div className="hidden md:flex items-center gap-1.5"><StatusBadge status={c.status} /></div>
                <div className="hidden lg:flex items-center gap-1.5"><SLABadge sla={c.sla} /></div>
                <div className="hidden lg:flex items-center gap-1.5"><RiskBadge risk={c.risk} /></div>
              </div>
            ))}
          </div>
        </div>

        <div className="glass-subtle p-5">
          <div className="flex items-center gap-2 mb-4">
            <MessagesSquare className="h-3.5 w-3.5 text-info" />
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Activity stream</div>
          </div>
          <div className="space-y-3">
            {activityStream.map((a, i) => (
              <div key={i} className="flex gap-3">
                <div className={`mt-1 h-1.5 w-1.5 rounded-full shrink-0 ${a.type === "ai" ? "bg-violet2" : a.type === "human" ? "bg-info" : "bg-stone"}`} />
                <div className="min-w-0">
                  <div className="text-[12.5px] text-navy leading-snug">{a.text}</div>
                  <div className="text-[10.5px] text-stone mt-0.5">{a.time} · {a.actor}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </>
  );
}

function PriorityCard({ icon: Icon, eyebrow, count, items, tone }: { icon: any; eyebrow: string; count: number; items: { title: string; meta: string }[]; tone: "violet" | "warning" | "info" }) {
  const toneMap = {
    violet: "text-violet2 bg-[hsl(var(--violet)/0.10)]",
    warning: "text-[hsl(38_92%_38%)] bg-[hsl(var(--warning)/0.12)]",
    info: "text-info bg-[hsl(var(--info)/0.08)]",
  };
  return (
    <div className="glass p-5 hover-lift">
      <div className="flex items-start justify-between mb-3">
        <div className="flex items-center gap-2">
          <div className={`h-8 w-8 rounded-xl grid place-items-center ${toneMap[tone]}`}>
            <Icon className="h-4 w-4" />
          </div>
          <div>
            <div className="text-[10.5px] uppercase tracking-[0.14em] text-stone">{eyebrow}</div>
            <div className="text-[22px] font-semibold text-navy leading-none mt-1">{count}</div>
          </div>
        </div>
        <button className="text-[11.5px] text-info hover:underline">Open →</button>
      </div>
      <div className="space-y-2 mt-2">
        {items.map((it, i) => (
          <div key={i} className="rounded-xl bg-white/70 border border-cloud/80 px-3 py-2">
            <div className="text-[12.5px] font-medium text-navy truncate">{it.title}</div>
            <div className="text-[11px] text-stone truncate">{it.meta}</div>
          </div>
        ))}
      </div>
    </div>
  );
}
