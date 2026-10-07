"use client";

import { Fragment, useState } from "react";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";
import CipStatusPill from "@/components/cip/CipStatusPill";
import type { AgentOperationRow } from "@/lib/lovable-parity/data/acquisitionReports";
import { useAgentOperations } from "@/lib/lovable-parity/data/acquisitionReports";
import { KpiRow, ReportState, TableCard, shortWhen, th } from "./ReportParts";

type Tone = "success" | "warning" | "danger" | "info" | "neutral" | "violet";

export function healthTone(health: string): Tone {
  return health === "Healthy" ? "success" : health === "Degraded" ? "warning" : health === "Errors" ? "danger" : "neutral";
}

export function AgentRowsTable({ rows, onRun, running }: { rows: AgentOperationRow[]; onRun?: (key: string) => void; running?: string | null }) {
  const [openKey, setOpenKey] = useState<string | null>(null);
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[620px] text-left">
        <thead>
          <tr className="border-b border-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
            <th className="px-5 py-3 font-semibold">Agent</th>
            <th className={th}>Status</th>
            <th className={th}>Health</th>
            <th className={th}>Runs</th>
            <th className={th}>Errors</th>
            <th className={th}>Last run</th>
            <th className={`${th} hidden 2xl:table-cell`}>Last OK</th>
            <th className={`${th} hidden 2xl:table-cell`}>Error rate</th>
            <th className={`${th} pr-4`}>Action</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-50">
          {rows.map((row) => (
            <Fragment key={row.key}>
              <tr className="hover:bg-slate-50/70">
                <td className="px-5 py-3.5">
                  <div className="text-[13px] font-semibold text-[#0B1640]">{row.name}</div>
                  <div className="mt-0.5 text-[11px] text-slate-500">{row.kind === "ModelBacked" ? "Uses a language model" : "Fixed rules"}</div>
                </td>
                <td className="px-3 py-3.5"><CipStatusPill tone={row.enabled ? "success" : "neutral"} dot>{row.enabled ? "Live" : "Switched off"}</CipStatusPill></td>
                <td className="whitespace-nowrap px-3 py-3.5"><CipStatusPill tone={healthTone(row.health)}>{row.health}</CipStatusPill></td>
                <td className="px-3 py-3.5 text-[12px] tabular-nums text-slate-700">{row.runs}</td>
                <td className="px-3 py-3.5 text-[12px] tabular-nums text-slate-700">{row.errors}</td>
                <td className="whitespace-nowrap px-3 py-3.5 text-[12px] text-slate-600">{row.lastRunAt ? shortWhen(row.lastRunAt) : "Never"}</td>
                <td className="hidden 2xl:table-cell whitespace-nowrap px-3 py-3.5 text-[12px] text-slate-600">{row.lastOkAt ? shortWhen(row.lastOkAt) : "Never"}</td>
                <td className="hidden 2xl:table-cell px-3 py-3.5 text-[12px] tabular-nums text-slate-700">{row.runs === 0 ? "None yet" : `${row.errorRatePercent}%`}</td>
                <td className="px-3 py-3.5 pr-4">
                  <div className="flex items-center gap-1.5">
                    <button type="button" onClick={() => setOpenKey(openKey === row.key ? null : row.key)} aria-expanded={openKey === row.key} className="inline-flex h-8 items-center rounded-lg border border-slate-200 bg-white px-3 text-[12px] font-semibold text-slate-700 hover:bg-slate-50">
                      View
                    </button>
                    {onRun && row.canRunNow ? (
                      <button type="button" onClick={() => onRun(row.key)} disabled={running === row.key} className="inline-flex h-8 items-center gap-1.5 rounded-lg bg-[#1769FF] px-3 text-[12px] font-semibold text-white hover:bg-[#1258d6] disabled:opacity-50">
                        {running === row.key ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : null}
                        Run
                      </button>
                    ) : null}
                  </div>
                </td>
              </tr>
              {openKey === row.key ? (
                <tr className="bg-[#F7F9FC]">
                  <td colSpan={9} className="px-5 py-3 text-[12.5px] leading-relaxed text-slate-600">{row.purpose}</td>
                </tr>
              ) : null}
            </Fragment>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default function AgentsTab({ runTriage }: { runTriage: () => Promise<{ ok: boolean; message?: string; scored?: number }> }) {
  const { data, loading, error, forbidden, reload } = useAgentOperations();
  const [running, setRunning] = useState<string | null>(null);

  const run = async (key: string) => {
    if (key !== "prospect-triage") return;
    setRunning(key);
    const result = await runTriage();
    setRunning(null);
    if (result.ok) {
      toast.success(`Scored ${result.scored} prospects`);
      void reload();
    } else toast.error(result.message ?? "The agent could not run.");
  };

  return (
    <div className="space-y-4">
      <KpiRow
        items={[
          { label: "Total agents", value: String(data?.total ?? 0) },
          { label: "Live and healthy", value: String(data?.live ?? 0) },
          { label: "With errors", value: String(data?.errors ?? 0) },
          { label: "No runs yet", value: String(data?.noRuns ?? 0) },
        ]}
      />
      <TableCard title="Agents" subtitle="The agents that work on acquisition. They prepare and rank; people decide and send.">
        <ReportState loading={loading} error={error} forbidden={forbidden} onRetry={() => void reload()} permission="View sales leads" empty={!loading && !error && !forbidden && (data?.rows.length ?? 0) === 0 ? { title: "No agents", message: "No acquisition agents are registered for this workspace." } : null}>
          <AgentRowsTable rows={data?.rows ?? []} onRun={(key) => void run(key)} running={running} />
        </ReportState>
      </TableCard>
    </div>
  );
}
