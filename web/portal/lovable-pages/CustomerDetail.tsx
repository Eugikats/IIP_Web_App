"use client";
import { useParams, Link } from "@/components/iip/NextRouterCompat";
import { ArrowLeft, Sparkles, FileText, MessageSquare, Shield, ClipboardList } from "lucide-react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { LoadingSkeleton, ErrorStateSurface, NotConfiguredStateSurface } from "@/components/ui/StateSurfaces";
import { useCustomerProfile } from "@/lib/lovable-parity/data/customers";

function DictRows({ dict }: { dict: Record<string, string> }) {
  const entries = Object.entries(dict);
  if (entries.length === 0) return <div className="text-[12.5px] text-dls-text-muted">Not recorded.</div>;
  return (
    <div className="space-y-1 text-[12.5px]">
      {entries.map(([k, v]) => (
        <div key={k} className="flex justify-between gap-3">
          <span className="text-dls-text-muted">{k}</span>
          <span className="truncate text-right text-dls-text-primary">{v}</span>
        </div>
      ))}
    </div>
  );
}

function DictCardList({ items, empty }: { items: Record<string, string>[]; empty: string }) {
  if (items.length === 0) return <div className="text-[12.5px] text-dls-text-muted">{empty}</div>;
  return (
    <div className="space-y-2">
      {items.map((item, i) => (
        <div key={i} className="rounded-dls-control border border-dls-border-subtle bg-dls-surface p-3"><DictRows dict={item} /></div>
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
      <div className="mb-4 flex items-center gap-2 text-[13px] text-dls-text-muted">
        <Link to="/app/customers" className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-control hover:text-dls-text-primary">
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden="true" /> Customers
        </Link>
        <span aria-hidden="true">›</span>
        <span className="font-semibold text-dls-text-primary">{String(id ?? "")}</span>
      </div>

      <SectionHeader eyebrow="Customer 360" title={name} sub={contact} />

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : !profile ? (
        <GlassCard><ErrorStateSurface title="Customer not found" message={error ?? "No profile in this workspace for this customer."} /></GlassCard>
      ) : (
        <div className="grid gap-4 lg:grid-cols-3">
          <div className="space-y-4 lg:col-span-2">
            <GlassCard>
              <div className="mb-2 flex items-center gap-2">
                <div className="grid h-8 w-8 place-items-center rounded-dls-chip bg-dls-primary-soft text-dls-primary">
                  <Sparkles className="h-4 w-4" aria-hidden="true" />
                </div>
                <div>
                  <div className="text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">AI customer summary</div>
                  <div className="text-[14px] font-bold text-dls-text-primary">Prepared for staff review</div>
                </div>
              </div>
              <NotConfiguredStateSurface what="A grounded, cited customer summary appears here once the customer-summary service is configured. IIP does not summarise from model memory — without approved source grounding this stays not configured rather than guessing." />
            </GlassCard>

            <GlassCard>
              <div className="mb-3 inline-flex items-center gap-2 text-[14px] font-bold text-dls-text-primary"><FileText className="h-4 w-4 text-dls-info-text" aria-hidden="true" /> Claims</div>
              <DictCardList items={profile.claims} empty="No claims on file." />
            </GlassCard>

            <GlassCard>
              <div className="mb-3 inline-flex items-center gap-2 text-[14px] font-bold text-dls-text-primary"><MessageSquare className="h-4 w-4 text-dls-info-text" aria-hidden="true" /> Interactions</div>
              <DictCardList items={profile.interactions} empty="No customer interactions yet." />
            </GlassCard>
          </div>

          <div className="space-y-4">
            <GlassCard>
              <div className="mb-2 inline-flex items-center gap-1 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted"><ClipboardList className="h-3.5 w-3.5" aria-hidden="true" /> Policies</div>
              <DictCardList items={profile.policies} empty="No policies recorded." />
            </GlassCard>

            <GlassCard>
              <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Documents on file</div>
              <DictCardList items={profile.documents} empty="No documents." />
            </GlassCard>

            <GlassCard>
              <div className="mb-2 inline-flex items-center gap-1 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted"><Shield className="h-3.5 w-3.5" aria-hidden="true" /> Consent &amp; privacy</div>
              {Object.keys(profile.consent ?? {}).length > 0 ? (
                <DictRows dict={profile.consent} />
              ) : (
                <NotConfiguredStateSurface what="Consent log not configured. When wired, each captured consent (purpose, timestamp, channel) is listed here." />
              )}
            </GlassCard>
          </div>
        </div>
      )}
    </>
  );
}
