"use client";
import { Link } from "@/components/iip/NextRouterCompat";
import { Search, Users, ArrowRight } from "lucide-react";
import { GlassCard, SectionHeader, Badge, RiskBadge } from "@/components/iip/Primitives";
import { customers } from "@/lib/lovable-parity/mock";

export default function Customers() {
  return (
    <>
      <SectionHeader
        eyebrow="Customer 360"
        title="Customers"
        sub="Internal staff workspace. Customers do not see this view — sensitive fields are hidden from customer-facing surfaces."
      />
      <GlassCard className="!p-0 overflow-hidden">
        <div className="p-3 border-b border-cloud/70 flex items-center gap-2 rounded-xl text-[12.5px] text-stone">
          <Search className="h-3.5 w-3.5" /><span>Search customer, phone, policy…</span>
        </div>
        <table className="w-full text-[13px]">
          <thead className="bg-white/60">
            <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
              <th className="p-3">Customer</th><th className="p-3">Contact</th><th className="p-3">Policies</th><th className="p-3">Active claims</th><th className="p-3">Preferred channel</th><th className="p-3">Consent</th><th className="p-3">Risk</th><th className="p-3"></th>
            </tr>
          </thead>
          <tbody>
            {customers.map((c) => (
              <tr key={c.id} className="border-t border-cloud/70 hover:bg-white/50">
                <td className="p-3">
                  <div className="text-navy font-medium">{c.name}</div>
                  <div className="text-[11px] text-stone">{c.id} · {c.region}</div>
                </td>
                <td className="p-3 text-slate2">{c.contact}</td>
                <td className="p-3"><div className="flex flex-wrap gap-1">{c.policies.map((p) => <Badge key={p} tone="info">{p}</Badge>)}</div></td>
                <td className="p-3 text-navy">{c.activeClaims}</td>
                <td className="p-3 text-slate2">{c.channelPref}</td>
                <td className="p-3">{c.consent ? <Badge tone="success">On file</Badge> : <Badge tone="warning">Missing</Badge>}</td>
                <td className="p-3"><RiskBadge risk={c.risk} /></td>
                <td className="p-3"><Link to={`/app/customers/${c.id}`} className="inline-flex items-center gap-1 text-info text-[12.5px] hover:underline">Open <ArrowRight className="h-3 w-3" /></Link></td>
              </tr>
            ))}
          </tbody>
        </table>
        {customers.length === 0 && (
          <div className="p-10 text-center text-stone flex flex-col items-center gap-2">
            <Users className="h-6 w-6" />
            <div className="text-navy font-medium">No customers yet</div>
            <div className="text-[12.5px]">Customers created from channel conversations will appear here.</div>
          </div>
        )}
      </GlassCard>
    </>
  );
}
