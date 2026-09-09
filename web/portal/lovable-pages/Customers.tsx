"use client";
import { useMemo, useState } from "react";
import { Link } from "@/components/iip/NextRouterCompat";
import { Search, ArrowRight } from "lucide-react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { Input } from "@/components/ui/Input";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface, EmptyFilteredStateSurface } from "@/components/ui/StateSurfaces";
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
      <div className="mb-4 max-w-md">
        <Input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="Search customer, contact…"
          leadingIcon={<Search aria-hidden="true" />}
          aria-label="Search customers"
        />
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={5} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Customers could not be loaded" message={error} /></GlassCard>
      ) : customers.length === 0 ? (
        <GlassCard><EmptyStateSurface message="Customers created from channel conversations or FNOL intake appear here." /></GlassCard>
      ) : rows.length === 0 ? (
        <GlassCard><EmptyFilteredStateSurface onClear={() => setQ("")} /></GlassCard>
      ) : (
        <Table label="Customers">
          <TableHead>
            <TableRow>
              <TableHeaderCell>Customer</TableHeaderCell>
              <TableHeaderCell>Contact</TableHeaderCell>
              <TableHeaderCell numeric>Active claims</TableHeaderCell>
              <TableHeaderCell aria-label="Actions" />
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((c) => (
              <TableRow key={c.id}>
                <TableCell>
                  <div className="font-semibold text-dls-text-primary">{c.name}</div>
                  <div className="text-[11px] text-dls-text-muted">{c.id}</div>
                </TableCell>
                <TableCell className="text-dls-text-secondary">{c.contact}</TableCell>
                <TableCell numeric>{c.activeClaims > 0 ? <Badge tone="info">{c.activeClaims}</Badge> : <span className="text-dls-text-muted">0</span>}</TableCell>
                <TableCell>
                  <Link to={`/app/customers/${c.id}`} className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-control text-[12.5px] font-semibold text-dls-primary hover:underline">
                    Open 360 <ArrowRight className="h-3 w-3" aria-hidden="true" />
                  </Link>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </>
  );
}
