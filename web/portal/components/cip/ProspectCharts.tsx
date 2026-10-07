"use client";

import { useMemo, type ReactNode } from "react";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import DashboardCard from "./DashboardCard";
import { EmptyStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import type { SalesLeadView } from "@/lib/lovable-parity/data/salesLeads";
import { dailyCounts, dayLabels } from "./series";

// The Charts tab: the same prospects as the queue, drawn four ways. Every figure is counted from the
// records on screen; nothing is estimated.

const BLUE = "#1769FF";
const PALETTE = ["#1769FF", "#38BDF8", "#10B981", "#F59E0B", "#8B5CF6", "#94A3B8"];
const AXIS = { fontSize: 11, fill: "#94A3B8" };

const STAGE_ORDER: { stage: string; label: string }[] = [
  { stage: "New", label: "New" },
  { stage: "GettingToKnow", label: "Getting to know them" },
  { stage: "Interested", label: "Interested" },
  { stage: "Considering", label: "Considering" },
  { stage: "ReadyToApply", label: "Ready to apply" },
  { stage: "Applied", label: "Applied" },
  { stage: "WithAPerson", label: "With a person" },
  { stage: "NotNow", label: "Not now" },
];

function tally<T extends string>(items: T[]): { name: T; value: number }[] {
  const counts = new Map<T, number>();
  for (const item of items) counts.set(item, (counts.get(item) ?? 0) + 1);
  return [...counts.entries()].map(([name, value]) => ({ name, value })).sort((a, b) => b.value - a.value);
}

function ChartCard({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <DashboardCard padding="none" className="overflow-hidden">
      <div className="border-b border-slate-100 px-4 py-3">
        <h2 className="m-0 text-[14px] font-bold text-[#070B3F]">{title}</h2>
        {subtitle ? <p className="m-0 mt-0.5 text-[11.5px] text-slate-500">{subtitle}</p> : null}
      </div>
      <div className="p-4">{children}</div>
    </DashboardCard>
  );
}

function Donut({ data }: { data: { name: string; value: number }[] }) {
  const total = data.reduce((sum, item) => sum + item.value, 0);
  return (
    <div className="flex flex-col items-center gap-4 sm:flex-row">
      <div className="relative h-44 w-44 shrink-0">
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie data={data} dataKey="value" nameKey="name" innerRadius={52} outerRadius={78} paddingAngle={2} stroke="none">
              {data.map((item, index) => (
                <Cell key={item.name} fill={PALETTE[index % PALETTE.length]} />
              ))}
            </Pie>
            <Tooltip />
          </PieChart>
        </ResponsiveContainer>
        <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
          <span className="text-[22px] font-bold leading-none text-[#0B1640]">{total}</span>
          <span className="mt-1 text-[10px] font-semibold uppercase tracking-wide text-slate-400">Prospects</span>
        </div>
      </div>
      <ul className="m-0 w-full min-w-0 space-y-1.5 p-0">
        {data.map((item, index) => (
          <li key={item.name} className="flex list-none items-center gap-2 text-[12px] text-slate-600">
            <span className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ background: PALETTE[index % PALETTE.length] }} aria-hidden />
            <span className="min-w-0 flex-1 truncate">{item.name}</span>
            <span className="font-semibold tabular-nums text-[#0B1640]">{item.value}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function HorizontalBars({ data, height }: { data: { name: string; value: number }[]; height: number }) {
  return (
    <div style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} layout="vertical" margin={{ left: 4, right: 16, top: 4, bottom: 4 }}>
          <CartesianGrid horizontal={false} stroke="#EEF2F7" />
          <XAxis type="number" allowDecimals={false} tick={AXIS} axisLine={false} tickLine={false} />
          <YAxis type="category" dataKey="name" width={150} tick={{ ...AXIS, fill: "#475569" }} axisLine={false} tickLine={false} />
          <Tooltip cursor={{ fill: "rgba(23,105,255,0.06)" }} />
          <Bar dataKey="value" fill={BLUE} radius={[0, 6, 6, 0]} barSize={14} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

export default function ProspectCharts({ leads, loading }: { leads: SalesLeadView[]; loading: boolean }) {
  const perDay = useMemo(() => {
    const labels = dayLabels();
    const counts = dailyCounts(leads.map((lead) => lead.createdAt));
    return labels.map((label, index) => ({ label, value: counts[index] }));
  }, [leads]);

  const byStage = useMemo(
    () => STAGE_ORDER.map(({ stage, label }) => ({ name: label, value: leads.filter((lead) => lead.stage === stage).length })).filter((item) => item.value > 0),
    [leads],
  );
  const byRelationship = useMemo(() => tally(leads.map((lead) => lead.relationshipLabel)), [leads]);
  const byChannel = useMemo(() => tally(leads.map((lead) => lead.primaryChannel ?? "Manual")), [leads]);
  const byProduct = useMemo(
    () => tally(leads.map((lead) => lead.productInterest).filter((name): name is string => !!name)).slice(0, 7),
    [leads],
  );

  if (loading) {
    return <DashboardCard><LoadingSkeleton rows={6} /></DashboardCard>;
  }
  if (leads.length === 0) {
    return (
      <DashboardCard>
        <EmptyStateSurface title="Nothing to chart yet" message="Charts fill in as prospects arrive from WhatsApp, bank lists and your team." />
      </DashboardCard>
    );
  }

  const noRecentMovement = perDay.every((point) => point.value === 0);

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <div className="lg:col-span-2">
        <ChartCard title="New prospects, last 14 days" subtitle="Counted from the day each prospect was first recorded.">
          {noRecentMovement ? (
            <p className="m-0 py-10 text-center text-[12.5px] text-slate-500">No new prospects in the last 14 days.</p>
          ) : (
            <div className="h-56">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={perDay} margin={{ left: -16, right: 8, top: 8, bottom: 0 }}>
                  <defs>
                    <linearGradient id="prospects-area" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor={BLUE} stopOpacity={0.22} />
                      <stop offset="100%" stopColor={BLUE} stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid vertical={false} stroke="#EEF2F7" />
                  <XAxis dataKey="label" tick={AXIS} axisLine={false} tickLine={false} interval={1} />
                  <YAxis allowDecimals={false} tick={AXIS} axisLine={false} tickLine={false} />
                  <Tooltip />
                  <Area type="monotone" dataKey="value" name="New prospects" stroke={BLUE} strokeWidth={2} fill="url(#prospects-area)" />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
        </ChartCard>
      </div>

      <ChartCard title="Where they are in the journey" subtitle="Prospects by stage.">
        <HorizontalBars data={byStage} height={Math.max(180, byStage.length * 34)} />
      </ChartCard>

      <ChartCard title="Who they are" subtitle="Prospects by relationship with the insurer.">
        <Donut data={byRelationship} />
      </ChartCard>

      <ChartCard title="What they are interested in" subtitle="Covers named most often.">
        {byProduct.length === 0 ? (
          <p className="m-0 py-10 text-center text-[12.5px] text-slate-500">No cover has been named yet.</p>
        ) : (
          <HorizontalBars data={byProduct} height={Math.max(180, byProduct.length * 34)} />
        )}
      </ChartCard>

      <ChartCard title="How they reached us" subtitle="Prospects by channel.">
        <Donut data={byChannel} />
      </ChartCard>
    </div>
  );
}
