"use client";
import { useParams, Link } from "@/components/iip/NextRouterCompat";
import { ArrowLeft, Sparkles, FileText, MessageSquare, Shield, ClipboardList } from "lucide-react";
import { GlassCard, SectionHeader, Badge, RiskBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { customers, claims, documentLibrary, communications } from "@/lib/lovable-parity/mock";

export default function CustomerDetail() {
  const { id } = useParams();
  const c = customers.find((x) => x.id === id) ?? customers[0];
  const cust = c.name;
  const cClaims = claims.filter((x) => x.customer === cust);
  const cDocs = documentLibrary.filter((x) => x.customer === cust);
  const cComms = communications.filter((x) => x.customer === cust);

  return (
    <>
      <div className="flex items-center gap-2 text-[12px] text-stone mb-4">
        <Link to="/app/customers" className="inline-flex items-center gap-1 hover:text-navy"><ArrowLeft className="h-3.5 w-3.5" /> Customers</Link>
        <span>/</span><span className="text-navy">{c.id}</span>
      </div>
      <SectionHeader
        eyebrow={`Customer 360 · ${c.id}`}
        title={c.name}
        sub={c.contact}
        action={<div className="flex items-center gap-2"><Badge tone="info">{c.region}</Badge><RiskBadge risk={c.risk} />{c.consent ? <Badge tone="success">Consent on file</Badge> : <Badge tone="warning">Consent missing</Badge>}</div>}
      />

      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 space-y-4">
          <GlassCard level={3}>
            <div className="flex items-center gap-2 mb-2">
              <div className="h-8 w-8 rounded-xl gradient-primary grid place-items-center shadow-l2"><Sparkles className="h-4 w-4 text-white" /></div>
              <div>
                <div className="text-[11px] uppercase tracking-[0.14em] text-stone">AI customer summary</div>
                <div className="text-[14px] font-semibold text-navy">Prepared for staff review</div>
              </div>
              <div className="ml-auto"><ConfidenceBadge value={0.84} /></div>
            </div>
            <p className="text-[13.5px] leading-[21px] text-slate2">
              {c.name} holds {c.policies.length} active policy ({c.policies.join(", ")}) and has {c.activeClaims} open claim. Interaction history shows strong preference for {c.channelPref}. No adverse regulatory flags detected in workspace records. Missing fields shown below are not guessed — they remain marked unknown until confirmed.
            </p>
            <div className="mt-3 rounded-xl border border-cloud bg-white/70 p-3 text-[12.5px] text-slate2">
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-1">Suggested next action</div>
              Draft a customer-safe update for open claim, then request supervisor review before send.
            </div>
          </GlassCard>

          <GlassCard>
            <div className="text-[14px] font-semibold text-navy mb-3 inline-flex items-center gap-2"><FileText className="h-4 w-4 text-info" /> Claims</div>
            <div className="space-y-2">
              {cClaims.map((cl) => (
                <Link key={cl.id} to={`/app/claims/${cl.id}`} className="block rounded-xl border border-cloud bg-white/70 p-3 hover-lift">
                  <div className="flex items-center justify-between">
                    <div className="text-[13px] font-medium text-navy">{cl.id} · {cl.claimType}</div>
                    <Badge tone="neutral">{cl.status}</Badge>
                  </div>
                  <div className="text-[12px] text-stone">{cl.incident} · {cl.updated}</div>
                </Link>
              ))}
              {cClaims.length === 0 && <div className="text-[12.5px] text-stone">No claims on file.</div>}
            </div>
          </GlassCard>

          <GlassCard>
            <div className="text-[14px] font-semibold text-navy mb-3 inline-flex items-center gap-2"><MessageSquare className="h-4 w-4 text-info" /> Recent communications</div>
            <div className="space-y-2">
              {cComms.map((m) => (
                <div key={m.id} className="rounded-xl border border-cloud bg-white/70 p-3">
                  <div className="flex items-center justify-between">
                    <div className="text-[13px] text-navy">{m.subject}</div>
                    <Badge tone="neutral">{m.status}</Badge>
                  </div>
                  <div className="text-[12px] text-stone">{m.channel} · {m.drafted}</div>
                </div>
              ))}
              {cComms.length === 0 && <div className="text-[12.5px] text-stone">No customer messages yet.</div>}
            </div>
          </GlassCard>
        </div>

        <div className="space-y-4">
          <GlassCard>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2 inline-flex items-center gap-1"><ClipboardList className="h-3.5 w-3.5" /> Policies</div>
            <div className="space-y-1.5 text-[13px]">
              {c.policies.map((p) => <div key={p} className="flex justify-between"><span className="text-navy">{p}</span><Badge tone="success">Active</Badge></div>)}
            </div>
          </GlassCard>

          <GlassCard>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2">Documents on file</div>
            <div className="space-y-1.5 text-[12.5px]">
              {cDocs.map((d) => (
                <div key={d.id} className="flex justify-between text-slate2"><span className="truncate mr-2">{d.name}</span><Badge tone="neutral">{d.status}</Badge></div>
              ))}
              {cDocs.length === 0 && <div className="text-stone">No documents.</div>}
            </div>
          </GlassCard>

          <GlassCard>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2 inline-flex items-center gap-1"><Shield className="h-3.5 w-3.5" /> Consent & privacy</div>
            <div className="text-[12.5px] text-slate2 space-y-1">
              <div>Purpose: Claims processing · Marketing (opt-out)</div>
              <div>Captured: Onboarding · logged</div>
              <div>Retention: 7 years post-policy</div>
            </div>
          </GlassCard>
        </div>
      </div>
    </>
  );
}
