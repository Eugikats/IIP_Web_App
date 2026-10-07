"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { ArrowDown, ArrowUp, Download } from "lucide-react";
import CipStatusPill from "@/components/cip/CipStatusPill";
import { useEscalations, type EscalationRow } from "@/lib/lovable-parity/data/acquisitionReports";
import { KpiRow, ReportState, TableCard, downloadCsv, shortWhen, th } from "./ReportParts";

type Tone = "success" | "warning" | "danger" | "info" | "neutral" | "violet";
const STATUS_FILTERS = ["Open", "Pending customer", "With a person", "Closed", "All"] as const;

function statusTone(status: EscalationRow["status"]): Tone {
  return status === "Open" ? "warning" : status === "Closed" ? "success" : status === "Pending customer" ? "info" : "violet";
}
function priorityTone(priority: EscalationRow["priority"]): Tone {
  return priority === "High" ? "danger" : priority === "Medium" ? "warning" : "neutral";
}

export default function EscalationsTab({ onOpenLead }: { onOpenLead: (leadId: string) => void }) {
  const { data, loading, error, forbidden, reload } = useEscalations();
  const [status, setStatus] = useState<(typeof STATUS_FILTERS)[number]>("Open");
  const [newestFirst, setNewestFirst] = useState(true);

  const rows = useMemo(() => {
    const list = (data?.rows ?? []).filter((row) => status === "All" || row.status === status);
    return [...list].sort((a, b) => (newestFirst ? b.updatedAt.localeCompare(a.updatedAt) : a.updatedAt.localeCompare(b.updatedAt)));
  }, [data, status, newestFirst]);

  const none = !loading && !error && !forbidden && (data?.rows.length ?? 0) === 0;

  return (
    <div className="space-y-4">
      <KpiRow
        items={[
          { label: "Open escalations", value: String(data?.open ?? 0) },
          { label: "High priority", value: String(data?.highPriority ?? 0) },
          { label: "Pending customer", value: String(data?.pendingCustomer ?? 0) },
          { label: "New to bank", value: String(data?.newToBank ?? 0) },
          { label: "Unassigned", value: String(data?.unassigned ?? 0) },
        ]}
      />
      <TableCard
        title="Escalations"
        subtitle="Conversations Kato handed to your team, or where a reply is waiting for approval."
        actions={
          <>
            <label className="inline-flex items-center gap-1.5 text-[12px] text-slate-600">
              <span className="font-medium">Status</span>
              <select
                value={status}
                onChange={(event) => setStatus(event.target.value as (typeof STATUS_FILTERS)[number])}
                className="h-8 rounded-md border border-slate-200 bg-white px-2 text-[12px] text-[#0B1640] outline-none focus:border-[#1769FF]/40"
              >
                {STATUS_FILTERS.map((option) => <option key={option}>{option}</option>)}
              </select>
            </label>
            <button
              type="button"
              disabled={rows.length === 0}
              onClick={() => downloadCsv("escalations.csv", ["Customer", "Contact", "Type", "Status", "Priority", "Assignee", "Raised", "Updated"], rows.map((row) => [row.customer, row.contact, row.type, row.status, row.priority, row.assigneeName ?? "Unassigned", row.raisedAt, row.updatedAt]))}
              className="inline-flex h-8 items-center gap-1.5 rounded-md border border-slate-200 bg-white px-2.5 text-[12px] font-medium text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <Download className="h-3.5 w-3.5" aria-hidden />
              Export
            </button>
            <button
              type="button"
              onClick={() => setNewestFirst((value) => !value)}
              aria-label={newestFirst ? "Newest first. Click to reverse." : "Oldest first. Click to reverse."}
              className="inline-flex h-8 items-center gap-1 rounded-md border border-slate-200 bg-white px-2 text-[12px] font-medium text-slate-600 hover:bg-slate-50"
            >
              <span>Sort</span>
              <span className="inline-flex flex-col leading-none text-[#1769FF]" aria-hidden>
                <ArrowUp className={`h-2.5 w-2.5 ${newestFirst ? "opacity-35" : ""}`} strokeWidth={2.5} />
                <ArrowDown className={`-mt-0.5 h-2.5 w-2.5 ${newestFirst ? "" : "opacity-35"}`} strokeWidth={2.5} />
              </span>
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
          empty={none ? { title: "No escalations yet", message: "When Kato hands a conversation to your team, or a reply needs approval, it is listed here." } : rows.length === 0 ? { title: `No ${status.toLowerCase()} escalations`, message: "Choose another status to see the rest." } : null}
        >
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-left">
              <thead>
                <tr className="border-b border-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
                  <th className="px-5 py-3 font-semibold">Customer</th>
                  <th className={`${th} hidden 2xl:table-cell`}>Contact</th>
                  <th className={th}>Type</th>
                  <th className={th}>Status</th>
                  <th className={th}>Priority</th>
                  <th className={th}>Assignee</th>
                  <th className={`${th} hidden 2xl:table-cell`}>Due</th>
                  <th className={th}>Updated</th>
                  <th className={`${th} pr-4`}>Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-50">
                {rows.map((row) => (
                  <tr key={row.escalationId} className="hover:bg-slate-50/70">
                    <td className="px-5 py-3.5 text-[13px] font-semibold text-[#0B1640]">{row.customer}</td>
                    <td className="hidden 2xl:table-cell px-3 py-3.5 font-mono text-[12px] text-slate-600">{row.contact}</td>
                    <td className="min-w-[7rem] px-3 py-3.5 text-[12px] text-slate-700">{row.type}</td>
                    <td className="whitespace-nowrap px-3 py-3.5"><CipStatusPill tone={statusTone(row.status)} dot>{row.status}</CipStatusPill></td>
                    <td className="px-3 py-3.5"><CipStatusPill tone={priorityTone(row.priority)}>{row.priority}</CipStatusPill></td>
                    <td className="px-3 py-3.5 text-[12px] text-slate-700">{row.assigneeName ?? "Unassigned"}</td>
                    <td className="hidden 2xl:table-cell whitespace-nowrap px-3 py-3.5 text-[12px] text-slate-400" title="No response target has been configured for this workspace">Not configured</td>
                    <td className="whitespace-nowrap px-3 py-3.5 text-[12px] text-slate-600">{shortWhen(row.updatedAt)}</td>
                    <td className="px-3 py-3.5 pr-4">
                      <div className="flex items-center gap-1.5">
                        {row.conversationId ? (
                          <Link href={`/app/channels?thread=${encodeURIComponent(row.conversationId)}`} className="inline-flex h-8 items-center rounded-lg border border-slate-200 bg-white px-3 text-[12px] font-semibold text-slate-700 hover:bg-slate-50">
                            Open chat
                          </Link>
                        ) : null}
                        {row.leadId ? (
                          <button type="button" onClick={() => onOpenLead(row.leadId!)} className="inline-flex h-8 items-center rounded-lg border border-slate-200 bg-white px-3 text-[12px] font-semibold text-slate-700 hover:bg-slate-50">
                            View
                          </button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ReportState>
      </TableCard>
    </div>
  );
}
