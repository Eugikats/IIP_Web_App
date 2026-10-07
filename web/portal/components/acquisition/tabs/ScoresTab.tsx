"use client";

import { useMemo, useState } from "react";
import { ArrowDown, ArrowUp, Download, Loader2, Sparkles } from "lucide-react";
import { toast } from "sonner";
import CipStatusPill from "@/components/cip/CipStatusPill";
import type { SalesLeadView } from "@/lib/lovable-parity/data/salesLeads";
import { KpiRow, ReportState, TableCard, downloadCsv, th } from "./ReportParts";

type Tone = "success" | "warning" | "danger" | "info" | "neutral" | "violet";

function bandTone(band: string): Tone {
  return band === "HighPriority" ? "danger" : band === "Qualified" ? "success" : band === "Warm" ? "warning" : band === "Early" ? "info" : "neutral";
}

function ScoreMeter({ score }: { score: number }) {
  return (
    <div className="flex items-center gap-2">
      <span className="w-7 text-right text-[13px] font-bold tabular-nums text-[#0B1640]">{score}</span>
      <span className="hidden h-1.5 w-16 overflow-hidden rounded-full bg-slate-100 lg:block" aria-hidden>
        <span className="block h-full rounded-full bg-[#1769FF]" style={{ width: `${Math.min(100, score)}%` }} />
      </span>
    </div>
  );
}

export default function ScoresTab({
  leads,
  loading,
  error,
  forbidden,
  reload,
  runTriage,
  onOpenLead,
}: {
  leads: SalesLeadView[];
  loading: boolean;
  error: string | null;
  forbidden: boolean;
  reload: () => Promise<void>;
  runTriage: (leadId?: string) => Promise<{ ok: boolean; message?: string; scored?: number; skipped?: number }>;
  onOpenLead: (leadId: string) => void;
}) {
  const [qualifiedOnly, setQualifiedOnly] = useState(false);
  const [newestFirst, setNewestFirst] = useState(true);
  const [running, setRunning] = useState(false);

  const scored = useMemo(() => leads.filter((lead) => lead.triage !== null), [leads]);
  const rows = useMemo(() => {
    const list = qualifiedOnly ? scored.filter((lead) => lead.triage!.band === "Qualified" || lead.triage!.band === "HighPriority") : scored;
    return [...list].sort((a, b) => (newestFirst ? b.triage!.score - a.triage!.score : a.triage!.score - b.triage!.score));
  }, [scored, qualifiedOnly, newestFirst]);

  const count = (band: string) => scored.filter((lead) => lead.triage!.band === band).length;
  const average = scored.length === 0 ? "None yet" : String(Math.round(scored.reduce((sum, lead) => sum + lead.triage!.score, 0) / scored.length));

  const run = async () => {
    setRunning(true);
    const result = await runTriage();
    setRunning(false);
    if (result.ok) toast.success(`Scored ${result.scored} prospects`, { description: result.skipped ? `${result.skipped} have too little recorded to rank yet.` : undefined });
    else toast.error(result.message ?? "The prospects could not be scored.");
  };

  const unscored = leads.length - scored.length;

  return (
    <div className="space-y-4">
      <KpiRow
        items={[
          { label: "Total prospects", value: String(leads.length) },
          { label: "Scored", value: String(scored.length) },
          { label: "Qualified", value: String(count("Qualified") + count("HighPriority")) },
          { label: "High priority", value: String(count("HighPriority")) },
          { label: "Warm", value: String(count("Warm")) },
          { label: "Average score", value: average },
        ]}
      />
      <TableCard
        title="Prospect scores"
        subtitle="Each score is built from what is recorded about the person. It orders who to contact first; it never decides cover."
        actions={
          <>
            <label className="inline-flex items-center gap-1.5 text-[12px] text-slate-600">
              <input type="checkbox" checked={qualifiedOnly} onChange={(event) => setQualifiedOnly(event.target.checked)} className="h-3.5 w-3.5 accent-[#1769FF]" />
              Qualified only
            </label>
            <button
              type="button"
              disabled={rows.length === 0}
              onClick={() => downloadCsv("prospect-scores.csv", ["Prospect", "Channel", "Score", "Band", "Confidence", "Best fit", "Second fit", "Scored"], rows.map((lead) => [lead.customerName, lead.primaryChannel ?? "Manual", lead.triage!.score, lead.triage!.bandLabel, lead.triage!.confidence, lead.triage!.fits[0]?.product ?? "", lead.triage!.fits[1]?.product ?? "", lead.triage!.scoredAt]))}
              className="inline-flex h-8 items-center gap-1.5 rounded-md border border-slate-200 bg-white px-2.5 text-[12px] font-medium text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <Download className="h-3.5 w-3.5" aria-hidden />
              Export
            </button>
            <button
              type="button"
              onClick={() => setNewestFirst((value) => !value)}
              aria-label={newestFirst ? "Highest score first. Click to reverse." : "Lowest score first. Click to reverse."}
              className="inline-flex h-8 items-center gap-1 rounded-md border border-slate-200 bg-white px-2 text-[12px] font-medium text-slate-600 hover:bg-slate-50"
            >
              <span>Score</span>
              <span className="inline-flex flex-col leading-none text-[#1769FF]" aria-hidden>
                <ArrowUp className={`h-2.5 w-2.5 ${newestFirst ? "opacity-35" : ""}`} strokeWidth={2.5} />
                <ArrowDown className={`-mt-0.5 h-2.5 w-2.5 ${newestFirst ? "" : "opacity-35"}`} strokeWidth={2.5} />
              </span>
            </button>
            <button
              type="button"
              onClick={() => void run()}
              disabled={running || leads.length === 0}
              className="inline-flex h-8 items-center gap-1.5 rounded-md bg-[#1769FF] px-3 text-[12px] font-semibold text-white hover:bg-[#1258d6] disabled:cursor-not-allowed disabled:opacity-50"
            >
              {running ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <Sparkles className="h-3.5 w-3.5" aria-hidden />}
              {scored.length === 0 ? "Score prospects" : "Score again"}
            </button>
          </>
        }
      >
        <ReportState
          loading={loading}
          error={error}
          forbidden={forbidden}
          onRetry={() => void reload()}
          permission="View sales leads"
          empty={
            leads.length === 0
              ? { title: "No prospects to score", message: "Prospects appear when someone asks Kato about a cover, or when a bank list is imported." }
              : scored.length === 0
                ? { title: "Nobody has been scored yet", message: `Choose Score prospects to rank the ${leads.length} people here and see the two covers that fit each.` }
                : rows.length === 0
                  ? { title: "No qualified prospects yet", message: "Untick Qualified only to see everyone who has been scored." }
                  : null
          }
        >
          <div className="overflow-x-auto">
            <table className="w-full min-w-[0px] text-left">
              <thead>
                <tr className="border-b border-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
                  <th className="px-5 py-3 font-semibold">Prospect</th>
                  <th className={`${th} hidden 2xl:table-cell`}>Channel</th>
                  <th className={th}>Score</th>
                  <th className={th}>Band</th>
                  <th className={`${th} hidden xl:table-cell`}>Recommended covers</th>
                  <th className={`${th} hidden 2xl:table-cell`}>Assigned officer</th>
                  <th className={`${th} pr-4`}>Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-50">
                {rows.map((lead) => {
                  const triage = lead.triage!;
                  return (
                    <tr key={lead.leadId} className="cursor-pointer align-top hover:bg-slate-50/70" onClick={() => onOpenLead(lead.leadId)}>
                      <td className="min-w-[8.5rem] px-5 py-3.5">
                        <div className="text-[13px] font-semibold text-[#0B1640]">{lead.customerName}</div>
                        <div className="mt-0.5 text-[11px] text-slate-500">{lead.occupation ?? "Work not known yet"}</div>
                        {triage.fits[0] ? <div className="mt-1 text-[11px] font-medium text-[#1769FF] xl:hidden">Best fit: {triage.fits[0].product.replace(/^Nile Shield |^Mpingo |^Kifaru /, "")}</div> : null}
                      </td>
                      <td className="hidden 2xl:table-cell px-3 py-3.5 text-[12px] text-slate-700">{lead.primaryChannel ?? "Manual"}</td>
                      <td className="px-2 py-3.5"><ScoreMeter score={triage.score} /></td>
                      <td className="whitespace-nowrap px-2 py-3.5">
                        <CipStatusPill tone={bandTone(triage.band)} dot>{triage.bandLabel}</CipStatusPill>
                        <div className="mt-1 text-[10.5px] text-slate-400">{triage.confidence} confidence</div>
                      </td>
                      <td className="hidden min-w-[14rem] max-w-[22rem] px-3 py-3.5 xl:table-cell">
                        {triage.fits.length === 0 ? (
                          <span className="text-[12px] text-slate-400">Too little recorded to recommend a cover</span>
                        ) : (
                          <ol className="m-0 list-none space-y-1.5 p-0">
                            {triage.fits.map((fit, index) => (
                              <li key={fit.product} className="text-[12px]">
                                <span className="font-semibold text-[#0B1640]">{index + 1}. {fit.product}</span>
                                <span className="block text-[11px] text-slate-400">{fit.insurer}</span>
                                <span className="mt-0.5 line-clamp-2 block text-[11px] leading-snug text-slate-500">{fit.reasons.slice(0, 2).join(". ")}</span>
                              </li>
                            ))}
                          </ol>
                        )}
                      </td>
                      <td className="hidden 2xl:table-cell px-3 py-3.5 text-[12px] text-slate-700">{lead.assignedOfficerName ?? "Unassigned"}</td>
                      <td className="px-2 py-3.5 pr-3" onClick={(event) => event.stopPropagation()}>
                        <button type="button" onClick={() => onOpenLead(lead.leadId)} className="inline-flex h-8 items-center rounded-lg border border-slate-200 bg-white px-3 text-[12px] font-semibold text-slate-700 hover:bg-slate-50">
                          Review
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          {unscored > 0 && scored.length > 0 ? (
            <p className="m-0 border-t border-slate-100 px-5 py-3 text-[12px] text-slate-500">{unscored} {unscored === 1 ? "prospect has" : "prospects have"} not been scored yet. Choose Score again to include them.</p>
          ) : null}
        </ReportState>
      </TableCard>
    </div>
  );
}
