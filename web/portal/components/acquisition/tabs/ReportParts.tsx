"use client";

import type { ReactNode } from "react";
import DashboardCard from "@/components/cip/DashboardCard";
import { EmptyStateSurface, ErrorStateSurface, LoadingSkeleton, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";

// Shared pieces of the four report tabs: the KPI row (numbers only, no invented trend), the card that
// holds a table, and the loading / error / no-permission / empty states every report must have.

export type Kpi = { label: string; value: string; helper?: string };

export function KpiRow({ items }: { items: Kpi[] }) {
  return (
    <div className="-mx-1 flex gap-2.5 overflow-x-auto px-1 pb-1 [scrollbar-width:none] lg:mx-0 lg:grid lg:overflow-visible lg:px-0 lg:pb-0 lg:[grid-template-columns:repeat(auto-fit,minmax(11rem,1fr))] [&::-webkit-scrollbar]:hidden">
      {items.map((item) => (
        <div key={item.label} className="min-w-[11rem] flex-1 lg:min-w-0">
          <DashboardCard padding="sm" className="flex h-full min-h-[5.5rem] flex-col justify-center border-[#1769FF]/15">
            <p className="m-0 text-[11px] font-semibold leading-4 text-slate-500">{item.label}</p>
            <p className="m-0 mt-1.5 text-[20px] font-bold leading-none tracking-tight text-[#0B1640]">{item.value}</p>
            {item.helper ? <p className="m-0 mt-1.5 text-[10.5px] leading-snug text-slate-400">{item.helper}</p> : null}
          </DashboardCard>
        </div>
      ))}
    </div>
  );
}

export function TableCard({ title, subtitle, actions, children }: { title: string; subtitle?: string; actions?: ReactNode; children: ReactNode }) {
  return (
    <section className="overflow-hidden rounded-xl border border-slate-200/90 bg-white shadow-[0_1px_2px_rgba(15,23,42,0.03)]">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-4 py-3.5 sm:px-5">
        <div>
          <h2 className="m-0 text-[15px] font-semibold text-[#0B1640]">{title}</h2>
          {subtitle ? <p className="m-0 mt-0.5 text-[12px] text-slate-500">{subtitle}</p> : null}
        </div>
        {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
      </div>
      {children}
    </section>
  );
}

export function ReportState({
  loading,
  error,
  forbidden,
  onRetry,
  permission,
  empty,
  children,
}: {
  loading: boolean;
  error: string | null;
  forbidden: boolean;
  onRetry: () => void;
  permission: string;
  empty?: { title: string; message: string } | null;
  children: ReactNode;
}) {
  if (loading) return <div className="p-5"><LoadingSkeleton rows={5} /></div>;
  if (forbidden) return <div className="p-5"><NoPermissionStateSurface permission={permission} /></div>;
  if (error) return <div className="p-5"><ErrorStateSurface title="This view is unavailable" message={error} onRetry={onRetry} /></div>;
  if (empty) return <div className="p-5"><EmptyStateSurface title={empty.title} message={empty.message} /></div>;
  return <>{children}</>;
}

export const th = "px-3 py-3 font-semibold";

export function shortWhen(value: string | null | undefined): string {
  if (!value) return "Not recorded";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Not recorded"
    : date.toLocaleString("en-GB", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
}

/** A CSV download of exactly the rows on screen, so "Export" never exports something different. */
export function downloadCsv(filename: string, header: string[], rows: (string | number | null)[][]) {
  const cell = (value: string | number | null) => `"${String(value ?? "").replace(/"/g, '""')}"`;
  const body = [header, ...rows].map((row) => row.map(cell).join(",")).join("\r\n");
  const url = URL.createObjectURL(new Blob([body], { type: "text/csv;charset=utf-8" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}
