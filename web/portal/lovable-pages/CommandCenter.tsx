"use client";
import { useState } from "react";
import { ArrowRight, Sparkles, MessagesSquare, ShieldCheck, Loader2 } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge, SLABadge, RiskBadge } from "@/components/iip/Primitives";
import { activityStream } from "@/lib/lovable-parity/mock";
import { Link } from "@/components/iip/NextRouterCompat";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { useOperationsSummary } from "@/lib/lovable-parity/data/reports";
import { useClaims } from "@/lib/lovable-parity/data/claims";
import { useCommandCenterQuery } from "@/lib/lovable-parity/data/commandCenterQuery";
import CommandCenterAnswerView from "@/components/iip/CommandCenterAnswer";

const parity = LOVABLE_VISUAL_PARITY_MODE;

export default function CommandCenter() {
  const { summary } = useOperationsSummary();
  const { claims } = useClaims();
  const { ask, answer, loading, error } = useCommandCenterQuery();
  const [question, setQuestion] = useState("");
  const submit = () => ask(question);

  const pills = summary ? [
    { label: "Open claims", value: summary.openClaims },
    { label: "New claims today", value: summary.newClaimsToday },
    { label: "Incomplete claims", value: summary.incompleteClaims },
    { label: "SLA at risk", value: summary.slaAtRisk },
    { label: "Pending approvals", value: summary.pendingApprovals },
    { label: "Customer updates", value: summary.customerUpdatesPending },
    { label: "AI questions answered", value: summary.assistantQuestionsAnswered },
    { label: "Escalations required", value: summary.escalationsRequired },
  ] : [];

  return (
    <>
      <SectionHeader
        eyebrow="Command Center"
        title="Operations command view"
        sub="What needs attention across Motor Claims and Customer Operations. AI prepares work — humans decide."
      />

      {/* AI Command Bar — wired to POST /api/v1/command-center/query (governed, human decides) */}
      <div className="glass-elevated p-5 mb-8">
        <div className="flex items-center gap-3">
          <div className="h-9 w-9 rounded-xl gradient-primary grid place-items-center shadow-l2"><Sparkles className="h-4 w-4 text-white" /></div>
          <input
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter") submit(); }}
            placeholder="Ask IIP about claims, documents, SLA, approvals, or missing information…"
            className="flex-1 bg-transparent outline-none text-[14.5px] placeholder:text-stone text-navy"
          />
          <button
            onClick={submit}
            disabled={loading || !question.trim()}
            className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2.5 shadow-l2 hover-lift disabled:opacity-50"
          >
            {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <>Ask <ArrowRight className="h-4 w-4" /></>}
          </button>
        </div>
        <div className="mt-3 text-[11px] text-stone">Grounded on this workspace's real data via <span className="font-mono">/api/v1/command-center/query</span>. AI prepares — humans decide.</div>
      </div>

      {error && (
        <div className="mb-8 rounded-xl border border-red-200 bg-red-50 p-3 text-[12.5px] text-red-800">{error}</div>
      )}
      {answer && <CommandCenterAnswerView answer={answer} />}

      {/* Metrics — real operations summary */}
      {pills.length > 0 ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-8">
          {pills.map((m) => (
            <div key={m.label} className="glass-subtle p-4 hover-lift">
              <div className="text-[11.5px] text-stone">{m.label}</div>
              <div className="mt-1.5 text-[26px] leading-none font-semibold text-navy tracking-tight">{m.value.toLocaleString()}</div>
            </div>
          ))}
        </div>
      ) : (
        <GlassCard className="mb-8"><div className="p-6 text-center text-[12.5px] text-stone">Operational metrics load from the reports service for this workspace.</div></GlassCard>
      )}

      {/* Recommended actions — Copilot-prepared (parity only until assistant is wired) */}
      {parity && (
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
              "Reassign aging claims to balance workload.",
              "Send bulk document-follow-up on WhatsApp threads awaiting police report.",
              "Escalate CLM-1064 to Supervisor · exceeds authority threshold.",
            ].map((t, i) => (
              <div key={i} className="flex items-center justify-between gap-4 rounded-2xl border border-cloud bg-white/70 px-4 py-3 hover-lift">
                <div className="flex items-start gap-3"><div className="mt-0.5 h-6 w-6 rounded-lg bg-[hsl(var(--info)/0.08)] text-info grid place-items-center text-[11px] font-semibold">{i + 1}</div><div className="text-[13.5px] text-slate2">{t}</div></div>
                <button className="text-[12px] text-info hover:underline shrink-0">Review →</button>
              </div>
            ))}
          </div>
        </GlassCard>
      )}

      {/* Priority claims (real) + activity (parity only) */}
      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 glass-subtle p-5">
          <div className="flex items-center justify-between mb-4">
            <div>
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Priority claims</div>
              <div className="text-[15px] font-semibold text-navy mt-0.5">Requiring attention</div>
            </div>
            <Link to="/app/claims" className="text-[12px] text-info hover:underline">Open work queue →</Link>
          </div>
          {claims.length === 0 ? (
            <div className="py-8 text-center text-[12.5px] text-stone">No claims in this workspace yet.</div>
          ) : (
            <div className="divide-y divide-cloud/80">
              {claims.slice(0, 5).map((c) => (
                <div key={c.id} className="py-3 flex items-center gap-4">
                  <div className="w-28 shrink-0"><div className="text-[12.5px] font-medium text-navy truncate">{c.id}</div><div className="text-[11px] text-stone">{c.policy}</div></div>
                  <div className="flex-1 min-w-0"><div className="text-[13px] text-navy truncate">{c.customer}</div><div className="text-[11.5px] text-stone truncate">{c.productLine} · {c.claimType}</div></div>
                  <div className="hidden md:flex items-center gap-1.5"><StatusBadge status={c.status} /></div>
                  <div className="hidden lg:flex items-center gap-1.5"><SLABadge sla={c.sla} /></div>
                  <div className="hidden lg:flex items-center gap-1.5"><RiskBadge risk={c.risk} /></div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="glass-subtle p-5">
          <div className="flex items-center gap-2 mb-4"><MessagesSquare className="h-3.5 w-3.5 text-info" /><div className="text-[11px] uppercase tracking-[0.14em] text-stone">Activity stream</div></div>
          {parity ? (
            <div className="space-y-3">
              {activityStream.map((a, i) => (
                <div key={i} className="flex gap-3">
                  <div className={`mt-1 h-1.5 w-1.5 rounded-full shrink-0 ${a.type === "ai" ? "bg-violet2" : a.type === "human" ? "bg-info" : "bg-stone"}`} />
                  <div className="min-w-0"><div className="text-[12.5px] text-navy leading-snug">{a.text}</div><div className="text-[10.5px] text-stone mt-0.5">{a.time} · {a.actor}</div></div>
                </div>
              ))}
            </div>
          ) : (
            <div className="flex items-center gap-2 text-[12px] text-stone"><ShieldCheck className="h-4 w-4" /> Live activity stream appears once the audit/activity feed is wired here.</div>
          )}
        </div>
      </div>
    </>
  );
}
