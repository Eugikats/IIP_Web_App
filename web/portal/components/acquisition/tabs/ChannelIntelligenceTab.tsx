"use client";

import { useState } from "react";
import { Donut } from "@/components/cip/ProspectCharts";
import DashboardCard from "@/components/cip/DashboardCard";
import { useChannelIntelligence } from "@/lib/lovable-parity/data/acquisitionReports";
import { AgentRowsTable } from "./AgentsTab";
import { KpiRow, ReportState, TableCard, th } from "./ReportParts";

// CIP's Channel Intelligence: a channel overview and agent performance. Cost per qualified lead and
// average response time are not measured by IIP, so they say so instead of showing a number.

const VIEWS = [
  { id: "channels", label: "Channel overview" },
  { id: "agents", label: "Agent performance" },
] as const;

export default function ChannelIntelligenceTab() {
  const { data, loading, error, forbidden, reload } = useChannelIntelligence();
  const [view, setView] = useState<(typeof VIEWS)[number]["id"]>("channels");
  const percent = (value: number | null | undefined) => (value === null || value === undefined ? "None yet" : `${value}%`);

  return (
    <div className="space-y-4">
      <div className="inline-flex rounded-lg border border-slate-200/90 bg-white p-0.5" role="tablist" aria-label="Channel intelligence views">
        {VIEWS.map((item) => (
          <button
            key={item.id}
            type="button"
            role="tab"
            aria-selected={view === item.id}
            onClick={() => setView(item.id)}
            className={`rounded-md px-3 py-1.5 text-[12px] font-medium ${view === item.id ? "bg-[#1769FF]/12 text-[#1769FF]" : "text-slate-500 hover:bg-slate-50"}`}
          >
            {item.label}
          </button>
        ))}
      </div>

      <KpiRow
        items={[
          { label: "Total interactions", value: String(data?.totalInteractions ?? 0), helper: "Customer messages" },
          { label: "New leads captured", value: String(data?.newLeads ?? 0) },
          { label: "Qualified leads", value: String(data?.qualified ?? 0), helper: "Scored Qualified or above" },
          { label: "Conversion rate", value: percent(data?.conversionPercent), helper: "Applied of all leads" },
          { label: "Cost per qualified lead", value: "Not configured", helper: "No spend is recorded" },
          { label: "Average response time", value: "Not measured", helper: "Replies are not timed yet" },
        ]}
      />

      {view === "channels" ? (
        <div className="grid gap-4 2xl:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
          <TableCard title="Channel performance overview">
            <ReportState loading={loading} error={error} forbidden={forbidden} onRetry={() => void reload()} permission="View sales leads" empty={!loading && !error && !forbidden && (data?.channels.length ?? 0) === 0 ? { title: "No channel activity yet", message: "Messages and leads are counted here once people start writing in." } : null}>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[520px] text-left">
                  <thead>
                    <tr className="border-b border-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
                      <th className="px-5 py-3 font-semibold">Channel</th>
                      <th className={th}>Interactions</th>
                      <th className={th}>New leads</th>
                      <th className={th}>Qualified</th>
                      <th className={`${th} pr-4`}>Conversion</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-50">
                    {(data?.channels ?? []).map((row) => (
                      <tr key={row.channel} className="hover:bg-slate-50/70">
                        <td className="px-5 py-3.5 text-[13px] font-semibold text-[#0B1640]">{row.channel}</td>
                        <td className="px-3 py-3.5 text-[12px] tabular-nums text-slate-700">{row.interactions}</td>
                        <td className="px-3 py-3.5 text-[12px] tabular-nums text-slate-700">{row.newLeads}</td>
                        <td className="px-3 py-3.5 text-[12px] tabular-nums text-slate-700">{row.qualified}</td>
                        <td className="px-3 py-3.5 pr-4 text-[12px] tabular-nums text-slate-700">{percent(row.conversionPercent)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </ReportState>
          </TableCard>
          <DashboardCard padding="none" className="overflow-hidden">
            <div className="border-b border-slate-100 px-4 py-3">
              <h2 className="m-0 text-[14px] font-bold text-[#070B3F]">Channel mix</h2>
              <p className="m-0 mt-0.5 text-[11.5px] text-slate-500">Where new leads came from</p>
            </div>
            <div className="p-4">
              {(data?.channels ?? []).some((row) => row.newLeads > 0) ? (
                <Donut unit="Leads" data={(data?.channels ?? []).filter((row) => row.newLeads > 0).map((row) => ({ name: row.channel, value: row.newLeads }))} />
              ) : (
                <p className="m-0 py-8 text-center text-[12.5px] text-slate-500">{loading ? "Loading" : "No leads to show yet."}</p>
              )}
            </div>
          </DashboardCard>
        </div>
      ) : (
        <TableCard title="Agent performance" subtitle="The agents behind acquisition, counted from their recorded runs.">
          <ReportState loading={loading} error={error} forbidden={forbidden} onRetry={() => void reload()} permission="View sales leads" empty={null}>
            <AgentRowsTable rows={data?.agents ?? []} />
          </ReportState>
        </TableCard>
      )}
    </div>
  );
}
