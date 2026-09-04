"use client";
import { useMemo, useState } from "react";
import { Link } from "@/components/iip/NextRouterCompat";
import { Search, Users, ArrowRight } from "lucide-react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { useCustomersList } from "@/lib/lovable-parity/data/customers";

export default function Customers() {
  const { customers, loading, error } = useCustomersList();
  const [q, setQ] = useState("");

  const rows = useMemo(() => customers.filter((c) => {
    if (!q) return true;
    return `${c.id} ${c.name} ${c.contact}`.toLowerCase().includes(q.toLowerCase());
  }), [customers, q]);

  return (
    <>
      <SectionHeader
        eyebrow="Customer 360"
        title="Customers"
        sub="Internal staff workspace. Customers do not see this view — sensitive fields are hidden from customer-facing surfaces."
      />
      <GlassCard className="!p-0 overflow-hidden">
        <div className="p-3 border-b border-line/70 flex items-center gap-2 rounded-xl text-[12.5px] text-muted">
          <Search className="h-3.5 w-3.5" />
          <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search customer, contact…" className="flex-1 bg-transparent outline-none text-ink placeholder:text-muted" />
        </div>

        {loading ? (
          <div className="p-8 text-center text-[12.5px] text-muted">Loading customers…</div>
        ) : rows.length === 0 ? (
          <div className="p-10 text-center text-muted flex flex-col items-center gap-2">
            <Users className="h-6 w-6" />
            <div className="text-ink font-medium">No customers yet</div>
            <div className="text-[12.5px]">{error ? error : "Customers created from channel conversations or FNOL intake appear here."}</div>
          </div>
        ) : (
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-muted">
                <th className="p-3">Customer</th><th className="p-3">Contact</th><th className="p-3">Active claims</th><th className="p-3"></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((c) => (
                <tr key={c.id} className="border-t border-line/70 hover:bg-white/50">
                  <td className="p-3">
                    <div className="text-ink font-medium">{c.name}</div>
                    <div className="text-[11px] text-muted">{c.id}</div>
                  </td>
                  <td className="p-3 text-ink-soft">{c.contact}</td>
                  <td className="p-3 text-ink">{c.activeClaims > 0 ? <Badge tone="info">{c.activeClaims}</Badge> : <span className="text-muted">0</span>}</td>
                  <td className="p-3"><Link to={`/app/customers/${c.id}`} className="inline-flex items-center gap-1 text-info text-[12.5px] hover:underline">Open 360 <ArrowRight className="h-3 w-3" /></Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </GlassCard>
    </>
  );
}
