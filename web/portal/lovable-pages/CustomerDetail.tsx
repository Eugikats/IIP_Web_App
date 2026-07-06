"use client";
import { useParams, Link } from "@/components/iip/NextRouterCompat";
import { ArrowLeft, Sparkles, FileText, MessageSquare, Shield, ClipboardList } from "lucide-react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { useCustomerProfile } from "@/lib/lovable-parity/data/customers";

function DictRows({ dict }: { dict: Record<string, string> }) {
  const entries = Object.entries(dict);
  if (entries.length === 0) return <div className="text-[12.5px] text-stone">Not recorded.</div>;
  return (
    <div className="space-y-1 text-[12.5px]">
      {entries.map(([k, v]) => (
        <div key={k} className="flex justify-between gap-3">
          <span className="text-stone">{k}</span>
          <span className="text-navy text-right truncate">{v}</span>
        </div>
      ))}
    </div>
  );
}

function DictCardList({ items, empty }: { items: Record<string, string>[]; empty: string }) {
  if (items.length === 0) return <div className="text-[12.5px] text-stone">{empty}</div>;
  return (
    <div className="space-y-2">
      {items.map((item, i) => (
        <div key={i} className="rounded-xl border border-cloud bg-white/70 p-3"><DictRows dict={item} /></div>
      ))}
    </div>
  );
}

export default function CustomerDetail() {
  const { id } = useParams();
  const { profile, loading, error } = useCustomerProfile(String(id ?? ""));

  const name = profile?.identity?.fullName ?? profile?.identity?.name ?? String(id ?? "");
  const contact = profile?.identity?.contact ?? profile?.identity?.email ?? profile?.identity?.phone ?? "";

  return (
    <>
      <div className="flex items-center gap-2 text-[12px] text-stone mb-4">
        <Link to="/app/customers" className="inline-flex items-center gap-1 hover:text-navy"><ArrowLeft className="h-3.5 w-3.5" /> Customers</Link>
        <span>/</span><span className="text-navy">{String(id ?? "")}</span>
      </div>

      <SectionHeader eyebrow="Customer 360" title={name} sub={contact} />

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading customer profile…</div></GlassCard>
      ) : !profile ? (
        <GlassCard>
          <div className="p-10 text-center text-stone">
            <div className="text-navy font-medium">Customer not found</div>
            <div className="text-[12.5px] mt-1">{error ?? "No profile in this workspace for this customer."}</div>
          </div>
        </GlassCard>
      ) : (
        <div className="grid lg:grid-cols-3 gap-4">
          <div className="lg:col-span-2 space-y-4">
            <GlassCard level={3}>
              <div className="flex items-center gap-2 mb-2">
                <div className="h-8 w-8 rounded-xl gradient-primary grid place-items-center shadow-l2"><Sparkles className="h-4 w-4 text-white" /></div>
                <div>
                  <div className="text-[11px] uppercase tracking-[0.14em] text-stone">AI customer summary</div>
                  <div className="text-[14px] font-semibold text-navy">Prepared for staff review</div>
                </div>
              </div>
              <p className="text-[12.5px] leading-[20px] text-slate2">
                A grounded, cited customer summary appears here once the customer-summary service is configured. IIP does not summarise from model memory — without approved source grounding this stays <span className="font-medium">Not configured</span> rather than guessing.
              </p>
            </GlassCard>

            <GlassCard>
              <div className="text-[14px] font-semibold text-navy mb-3 inline-flex items-center gap-2"><FileText className="h-4 w-4 text-info" /> Claims</div>
              <DictCardList items={profile.claims} empty="No claims on file." />
            </GlassCard>

            <GlassCard>
              <div className="text-[14px] font-semibold text-navy mb-3 inline-flex items-center gap-2"><MessageSquare className="h-4 w-4 text-info" /> Interactions</div>
              <DictCardList items={profile.interactions} empty="No customer interactions yet." />
            </GlassCard>
          </div>

          <div className="space-y-4">
            <GlassCard>
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2 inline-flex items-center gap-1"><ClipboardList className="h-3.5 w-3.5" /> Policies</div>
              <DictCardList items={profile.policies} empty="No policies recorded." />
            </GlassCard>

            <GlassCard>
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2">Documents on file</div>
              <DictCardList items={profile.documents} empty="No documents." />
            </GlassCard>

            <GlassCard>
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2 inline-flex items-center gap-1"><Shield className="h-3.5 w-3.5" /> Consent & privacy</div>
              {Object.keys(profile.consent ?? {}).length > 0
                ? <DictRows dict={profile.consent} />
                : <div className="text-[12.5px] text-stone">Consent log not configured. When wired, each captured consent (purpose, timestamp, channel) is listed here.</div>}
            </GlassCard>
          </div>
        </div>
      )}
    </>
  );
}
