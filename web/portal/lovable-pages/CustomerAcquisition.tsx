"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { motion } from "framer-motion";
import { ArrowDown, ArrowUp, ChevronRight, Loader2, MoreHorizontal, Plus, Upload } from "lucide-react";
import { toast } from "sonner";
import AcquisitionFilterToolbar, { type AcquisitionActiveChip, type AcquisitionFilterField } from "@/components/cip/AcquisitionFilterToolbar";
import AcquisitionKpiStrip from "@/components/cip/AcquisitionKpiStrip";
import AcquisitionTabs from "@/components/cip/AcquisitionTabs";
import CipStatusPill from "@/components/cip/CipStatusPill";
import ProspectCharts from "@/components/cip/ProspectCharts";
import AgentsTab from "@/components/acquisition/tabs/AgentsTab";
import ChannelIntelligenceTab from "@/components/acquisition/tabs/ChannelIntelligenceTab";
import EscalationsTab from "@/components/acquisition/tabs/EscalationsTab";
import OutreachTab from "@/components/acquisition/tabs/OutreachTab";
import EventsTab from "@/components/acquisition/tabs/EventsTab";
import RulesTab from "@/components/acquisition/tabs/RulesTab";
import ConversionsTab from "@/components/acquisition/tabs/ConversionsTab";
import ScoresTab from "@/components/acquisition/tabs/ScoresTab";
import CountUp from "@/components/cip/CountUp";
import { dailyCounts, trendDirection } from "@/components/cip/series";
import { presentationMode } from "@/lib/config";
import { useLiveProspects } from "@/lib/lovable-parity/useLiveProspects";
import { Input } from "@/components/ui/Input";
import { EmptyStateSurface, ErrorStateSurface, LoadingSkeleton, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";
import { LeadCustomerModal, initialsOf } from "@/components/acquisition/LeadCustomerModal";
import { useSalesLeads, type SalesLeadView } from "@/lib/lovable-parity/data/salesLeads";
import { formatDateTime } from "@/lib/format";

/** "6 Oct, 08:20": short enough for a table cell; the full date is in the record. */
function shortWhen(value: string | null): string {
  if (!value) return "Not recorded";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Not recorded"
    : date.toLocaleString("en-GB", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
}

// The acquisition dashboard in the same layout as CIP's: tabs, KPI cards with the trend beside the
// number, a filter button with a count badge and chips, a compact queue table with paging, and a
// Charts tab. A trend is drawn only from real creation dates; otherwise the card says "No trend".

const PAGE_SIZE = 12;
const TABS = [
  { id: "prospects", label: "Prospects" },
  { id: "escalations", label: "Escalations" },
  { id: "scores", label: "Scores" },
  { id: "events", label: "Bank events" },
  { id: "rules", label: "Rules" },
  { id: "conversions", label: "Conversions" },
  { id: "outreach", label: "Outreach" },
  { id: "agents", label: "Agents" },
  { id: "channels", label: "Channel Intelligence" },
  { id: "charts", label: "Charts" },
] as const;
type TabId = (typeof TABS)[number]["id"];

const STAGE_OPTIONS = [
  { label: "All", value: "all" },
  { label: "New", value: "New" },
  { label: "Getting to know them", value: "GettingToKnow" },
  { label: "Interested", value: "Interested" },
  { label: "Considering", value: "Considering" },
  { label: "Ready to apply", value: "ReadyToApply" },
  { label: "Applied", value: "Applied" },
  { label: "With a person", value: "WithAPerson" },
  { label: "Not now", value: "NotNow" },
];
const RELATIONSHIP_OPTIONS = [
  { label: "All", value: "all" },
  { label: "Existing customer", value: "ExistingCustomer" },
  { label: "Registered", value: "Registered" },
  { label: "New to insurer", value: "NewToInsurer" },
];
const BANK_OPTIONS = [
  { label: "All", value: "all" },
  { label: "Yes", value: "yes" },
  { label: "No", value: "no" },
  { label: "Not known", value: "unknown" },
];
const CONSENT_OPTIONS = [
  { label: "All", value: "all" },
  { label: "Agreed", value: "Agreed" },
  { label: "Declined", value: "Declined" },
  { label: "Withdrawn", value: "Withdrawn" },
  { label: "Not recorded", value: "NotRecorded" },
];

const DEFAULT_FILTERS = { stage: "all", relationship: "all", bank: "all", consent: "all", channel: "all", officer: "all", product: "", search: "" };
type Filters = typeof DEFAULT_FILTERS;

type Tone = "success" | "warning" | "danger" | "info" | "neutral" | "violet";
function stageTone(stage: string): Tone {
  switch (stage) {
    case "ReadyToApply":
    case "Applied":
      return "success";
    case "Considering":
      return "warning";
    case "Interested":
    case "GettingToKnow":
      return "info";
    case "WithAPerson":
      return "violet";
    default:
      return "neutral";
  }
}
function relationshipTone(relationship: string): Tone {
  return relationship === "ExistingCustomer" ? "success" : relationship === "Registered" ? "info" : "neutral";
}

function optionLabel(options: { label: string; value: string }[], value: string) {
  return options.find((option) => option.value === value)?.label ?? value;
}

export default function CustomerAcquisition() {
  const { leads, loading, error, forbidden, creating, reload, createLead, getLeadDetail, addNote, assign, refreshInsights, runTriage, refresh, draftOutreach, draftFollowUps, editOutreach, sendOutreach, sendOutreachMany, dismissOutreach } = useSalesLeads();
  const live = useLiveProspects(leads, refresh, true, presentationMode ? 3000 : 15000);
  const [tab, setTab] = useState<TabId>("prospects");
  const [filters, setFilters] = useState<Filters>(DEFAULT_FILTERS);
  const [newestFirst, setNewestFirst] = useState(true);
  const [page, setPage] = useState(1);
  const [openLeadId, setOpenLeadId] = useState<string | null>(null);
  const [capturing, setCapturing] = useState(false);

  const patchFilter = useCallback((key: string, value: string) => {
    setPage(1);
    setFilters((current) => ({ ...current, [key]: value }));
  }, []);

  const officers = useMemo(
    () => [...new Set(leads.map((lead) => lead.assignedOfficerName).filter((name): name is string => !!name))].sort(),
    [leads],
  );
  const channels = useMemo(
    () => [...new Set(leads.map((lead) => lead.primaryChannel).filter((name): name is string => !!name))].sort(),
    [leads],
  );

  const rows = useMemo(() => {
    const q = filters.search.trim().toLowerCase();
    const product = filters.product.trim().toLowerCase();
    return leads
      .filter((lead) => {
        if (filters.stage !== "all" && lead.stage !== filters.stage) return false;
        if (filters.relationship !== "all" && lead.relationship !== filters.relationship) return false;
        if (filters.bank === "yes" && lead.bankCustomer !== true) return false;
        if (filters.bank === "no" && lead.bankCustomer !== false) return false;
        if (filters.bank === "unknown" && lead.bankCustomer !== null) return false;
        if (filters.consent !== "all" && lead.consentStatus !== filters.consent) return false;
        if (filters.channel !== "all" && lead.primaryChannel !== filters.channel) return false;
        if (filters.officer !== "all" && lead.assignedOfficerName !== filters.officer) return false;
        if (product && !(lead.productInterest ?? "").toLowerCase().includes(product)) return false;
        if (q && !`${lead.customerName} ${lead.maskedContact} ${lead.occupation ?? ""}`.toLowerCase().includes(q)) return false;
        return true;
      })
      .sort((a, b) => {
        const diff = (b.lastInteractionAt ?? "").localeCompare(a.lastInteractionAt ?? "");
        return newestFirst ? diff : -diff;
      });
  }, [leads, filters, newestFirst]);

  const pageCount = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const safePage = Math.min(page, pageCount);
  const pagedRows = rows.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);
  const rangeStart = rows.length === 0 ? 0 : (safePage - 1) * PAGE_SIZE + 1;
  const rangeEnd = Math.min(rows.length, safePage * PAGE_SIZE);

  const filterFields: AcquisitionFilterField[] = [
    { key: "stage", label: "Stage", kind: "select", value: filters.stage, options: STAGE_OPTIONS },
    { key: "relationship", label: "Relationship", kind: "select", value: filters.relationship, options: RELATIONSHIP_OPTIONS },
    { key: "bank", label: "Bank customer", kind: "select", value: filters.bank, options: BANK_OPTIONS },
    { key: "consent", label: "Agreed to follow-up", kind: "select", value: filters.consent, options: CONSENT_OPTIONS },
    { key: "channel", label: "Channel", kind: "select", value: filters.channel, options: [{ label: "All channels", value: "all" }, ...channels.map((c) => ({ label: c, value: c }))] },
    { key: "officer", label: "Assigned officer", kind: "select", value: filters.officer, options: [{ label: "All officers", value: "all" }, ...officers.map((o) => ({ label: o, value: o }))] },
    { key: "product", label: "Product interest", kind: "text", value: filters.product, placeholder: "For example Shop cover" },
  ];

  const activeChips: AcquisitionActiveChip[] = [];
  if (filters.stage !== "all") activeChips.push({ key: "stage", label: `Stage: ${optionLabel(STAGE_OPTIONS, filters.stage)}` });
  if (filters.relationship !== "all") activeChips.push({ key: "relationship", label: optionLabel(RELATIONSHIP_OPTIONS, filters.relationship) });
  if (filters.bank !== "all") activeChips.push({ key: "bank", label: `Bank customer: ${optionLabel(BANK_OPTIONS, filters.bank)}` });
  if (filters.consent !== "all") activeChips.push({ key: "consent", label: `Follow-up: ${optionLabel(CONSENT_OPTIONS, filters.consent)}` });
  if (filters.channel !== "all") activeChips.push({ key: "channel", label: filters.channel });
  if (filters.officer !== "all") activeChips.push({ key: "officer", label: filters.officer });
  if (filters.product.trim()) activeChips.push({ key: "product", label: filters.product.trim() });

  const clearChip = (key: string) => patchFilter(key, key === "product" ? "" : "all");
  const clearAll = () => {
    setPage(1);
    setFilters((current) => ({ ...DEFAULT_FILTERS, search: current.search }));
  };

  const kpiRows = useMemo(() => ({
    total: leads,
    existing: leads.filter((lead) => lead.relationship === "ExistingCustomer"),
    newToInsurer: leads.filter((lead) => lead.relationship === "NewToInsurer"),
    bank: leads.filter((lead) => lead.bankCustomer === true),
    ready: leads.filter((lead) => lead.stage === "ReadyToApply"),
  }), [leads]);
  const kpi = (label: string, subset: SalesLeadView[]) => {
    const series = dailyCounts(subset.map((lead) => lead.createdAt));
    // "Down" only means fewer new prospects than earlier in the fortnight, which is not bad news, so the
    // line is drawn in the neutral colour rather than the warning one.
    const direction = trendDirection(series);
    return { label, value: <CountUp value={subset.length} />, sparklineData: series, trendDirection: direction === "down" ? ("flat" as const) : direction };
  };

  return (
    <div className="cip-acq space-y-4">
      <div>
        <h1 className="m-0 text-[20px] font-semibold tracking-tight text-[#0B1640]">Acquisition dashboard</h1>
        <p className="m-0 mt-0.5 text-[12.5px] text-slate-500">People who might take cover, from WhatsApp conversations, bank lists and your team.</p>
        <div className="mt-2 flex flex-wrap items-center gap-2" aria-live="polite">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-100 bg-emerald-50 px-2.5 py-1 text-[11px] font-semibold text-emerald-700">
            <span className="iip-live-dot h-1.5 w-1.5 rounded-full bg-emerald-500" aria-hidden />
            Live
          </span>
          {live.feed.length === 0 ? (
            <span className="text-[12px] text-slate-500">Watching for new messages. New people and score changes appear here as they happen.</span>
          ) : null}
        </div>
        {live.feed.length > 0 ? (
          <ul className="m-0 mt-1.5 space-y-0.5 p-0">
            {live.feed.slice(0, 3).map((event, i) => (
              <li key={event.id} className={`list-none text-[12px] ${i === 0 ? "iip-flash rounded px-1 font-medium text-[#0B1640]" : "px-1 text-slate-500"}`}>{event.text}</li>
            ))}
          </ul>
        ) : null}
      </div>

      <AcquisitionTabs tabs={[...TABS]} active={tab} onChange={setTab} chatHref="/app/channels" />

      {tab === "prospects" || tab === "charts" ? (
      <AcquisitionKpiStrip
        items={[
          kpi("Total prospects", kpiRows.total),
          kpi("Existing customers", kpiRows.existing),
          kpi("New to the insurer", kpiRows.newToInsurer),
          kpi("Bank customers", kpiRows.bank),
          kpi("Ready to apply", kpiRows.ready),
        ]}
      />
      ) : null}

      {tab === "charts" ? (
        <ProspectCharts leads={leads} loading={loading} />
      ) : tab === "escalations" ? (
        <EscalationsTab onOpenLead={setOpenLeadId} />
      ) : tab === "scores" ? (
        <ScoresTab leads={leads} loading={loading} error={error} forbidden={forbidden} reload={reload} runTriage={runTriage} onOpenLead={setOpenLeadId} />
      ) : tab === "events" ? (
        <EventsTab />
      ) : tab === "rules" ? (
        <RulesTab />
      ) : tab === "conversions" ? (
        <ConversionsTab />
      ) : tab === "outreach" ? (
        <OutreachTab leads={leads} loading={loading} error={error} forbidden={forbidden} reload={reload} draftOutreach={draftOutreach} draftFollowUps={draftFollowUps} editOutreach={editOutreach} sendOutreach={sendOutreach} sendOutreachMany={sendOutreachMany} dismissOutreach={dismissOutreach} />
      ) : tab === "agents" ? (
        <AgentsTab runTriage={runTriage} />
      ) : tab === "channels" ? (
        <ChannelIntelligenceTab />
      ) : (
        <>
          <div className="flex items-start gap-2">
            <div className="min-w-0 flex-1">
              <AcquisitionFilterToolbar
                fields={filterFields}
                activeChips={activeChips}
                activeCount={activeChips.length}
                search={filters.search}
                onSearchChange={(value) => patchFilter("search", value)}
                onFieldChange={patchFilter}
                onClearChip={clearChip}
                onClearAll={clearAll}
              />
            </div>
            <button
              type="button"
              onClick={() => setCapturing((value) => !value)}
              className="inline-flex h-10 shrink-0 items-center gap-1.5 rounded-xl bg-[#1769FF] px-3.5 text-[13px] font-semibold text-white hover:bg-[#1258d6]"
            >
              <Plus className="h-4 w-4" aria-hidden />
              New lead
            </button>
            <Link href="/app/acquisition/import" className="inline-flex h-10 shrink-0 items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3.5 text-[13px] font-semibold text-slate-700 hover:bg-slate-50">
              <Upload className="h-4 w-4" aria-hidden />
              Import list
            </Link>
          </div>

          {capturing ? (
            <CaptureLead
              creating={creating}
              onCancel={() => setCapturing(false)}
              onSave={async (input) => {
                const result = await createLead(input);
                if (result.ok) {
                  toast.success("Lead captured");
                  setCapturing(false);
                } else {
                  toast.error(result.message ?? "The lead could not be saved.");
                }
              }}
            />
          ) : null}

          <section className="overflow-hidden rounded-xl border border-slate-200/90 bg-white shadow-[0_1px_2px_rgba(15,23,42,0.03)]">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-4 py-3.5 sm:px-5">
              <h2 className="m-0 text-[15px] font-semibold text-[#0B1640]">Prospect queue</h2>
              <button
                type="button"
                onClick={() => {
                  setPage(1);
                  setNewestFirst((value) => !value);
                }}
                className="inline-flex h-8 items-center gap-1 rounded-md border border-slate-200 bg-white px-2 text-[12px] font-medium text-slate-600 hover:bg-slate-50"
                aria-label={newestFirst ? "Sorted by last interaction, newest first. Click to reverse." : "Sorted by last interaction, oldest first. Click to reverse."}
              >
                <span>Sort</span>
                <span className="inline-flex flex-col leading-none text-[#1769FF]" aria-hidden>
                  <ArrowUp className={`h-2.5 w-2.5 ${newestFirst ? "opacity-35" : "opacity-100"}`} strokeWidth={2.5} />
                  <ArrowDown className={`-mt-0.5 h-2.5 w-2.5 ${newestFirst ? "opacity-100" : "opacity-35"}`} strokeWidth={2.5} />
                </span>
              </button>
            </div>

            {loading ? (
              <div className="p-5"><LoadingSkeleton rows={6} /></div>
            ) : forbidden ? (
              <div className="p-5"><NoPermissionStateSurface permission="View sales leads" /></div>
            ) : error ? (
              <div className="p-5"><ErrorStateSurface title="Prospects unavailable" message={error} onRetry={() => void reload()} /></div>
            ) : leads.length === 0 ? (
              <div className="p-5">
                <EmptyStateSurface title="No prospects yet" message="Prospects appear here when someone asks Kato about a cover on WhatsApp, or when your team captures a lead." />
              </div>
            ) : rows.length === 0 ? (
              <div className="p-5">
                <EmptyStateSurface title="No prospects match these filters" message="Clear a filter or change the search to see more." action={{ label: "Clear filters", onClick: () => { clearAll(); patchFilter("search", ""); } }} />
              </div>
            ) : (
              <>
                {/* Desktop table */}
                <div className="hidden overflow-x-auto lg:block">
                  <table className="w-full min-w-[1080px] text-left">
                    <thead>
                      <tr className="border-b border-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
                        <th className="px-5 py-3 font-semibold">Customer</th>
                        <th className="hidden 2xl:table-cell px-3 py-3 font-semibold">Contact</th>
                        <th className="hidden 2xl:table-cell px-3 py-3 font-semibold">Bank customer</th>
                        <th className="hidden 2xl:table-cell px-3 py-3 font-semibold">Relationship</th>
                        <th className="px-3 py-3 font-semibold">Product interest</th>
                        <th className="px-3 py-3 font-semibold">Stage</th>
                        <th className="px-3 py-3 font-semibold">Score</th>
                        <th className="px-3 py-3 font-semibold">Message</th>
                        <th className="px-3 py-3 font-semibold">Assigned officer</th>
                        <th className="px-3 py-3 font-semibold">Last interaction</th>
                        <th className="px-3 py-3 pr-4 font-semibold">Action</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-50">
                      {pagedRows.map((row) => (
                        <motion.tr key={row.leadId} layout="position" transition={{ type: "spring", stiffness: 260, damping: 30 }} className={`cursor-pointer hover:bg-slate-50/70 ${live.active(row.leadId)?.kind === "new" ? "iip-row-in" : live.active(row.leadId)?.kind === "message" ? "iip-flash" : ""}`} onClick={() => setOpenLeadId(row.leadId)}>
                          <td className="min-w-[8.5rem] px-5 py-3.5">
                            <div className="text-[13px] font-semibold text-[#0B1640]">{row.customerName}</div>
                            <div className="mt-0.5 text-[11px] text-slate-500">{row.occupation ?? "Work not known yet"}</div>
                          </td>
                          <td className="hidden 2xl:table-cell px-3 py-3.5">
                            <div className="font-mono text-[12px] text-slate-600">{row.maskedContact}</div>
                            <div className="mt-0.5 text-[11px] text-slate-500">{row.primaryChannel ?? "Manual"}</div>
                          </td>
                          <td className="hidden 2xl:table-cell px-3 py-3.5">
                            <CipStatusPill tone={row.bankCustomer === true ? "success" : "neutral"}>{row.bankCustomerLabel}</CipStatusPill>
                          </td>
                          <td className="hidden 2xl:table-cell px-3 py-3.5">
                            <CipStatusPill tone={relationshipTone(row.relationship)}>{row.relationshipLabel}</CipStatusPill>
                          </td>
                          <td className={`min-w-[9rem] px-3 py-3.5 text-[12px] text-slate-700 ${live.active(row.leadId)?.kind === "interest" ? "iip-flash" : ""}`}>{row.productInterest ?? "Not yet known"}</td>
                          <td className="whitespace-nowrap px-3 py-3.5">
                            <CipStatusPill tone={stageTone(row.stage)}>{row.stageLabel}</CipStatusPill>
                          </td>
                          <td className={`whitespace-nowrap px-3 py-3.5 text-[12px] text-slate-700 ${live.active(row.leadId)?.kind === "scored" ? "iip-flash" : ""}`}>
                            {live.scoring(row) ? (
                              <span className="inline-flex items-center gap-1.5 text-slate-500"><span className="iip-shimmer h-2 w-10 rounded-full" aria-hidden />Scoring</span>
                            ) : row.triage ? (
                              <span className="font-semibold tabular-nums text-[#0B1640]"><CountUp value={row.triage.score} /><span className="ml-1.5 font-normal text-slate-500">{row.triage.bandLabel}</span></span>
                            ) : (
                              <span className="text-slate-400">Not scored</span>
                            )}
                          </td>
                          <td className="whitespace-nowrap px-3 py-3.5 text-[12px] text-slate-700">
                            {row.latestOutreach ? <CipStatusPill tone={row.latestOutreach.status === "Sent" ? "success" : row.latestOutreach.status === "Blocked" ? "danger" : row.latestOutreach.status === "DemoSent" ? "info" : "warning"}>{row.latestOutreach.status === "DemoSent" ? "Demo sent" : row.latestOutreach.status === "Blocked" ? "Could not send" : row.latestOutreach.status === "Sent" ? "Sent" : "Ready to review"}</CipStatusPill> : <span className="text-slate-400">None</span>}
                          </td>
                          <td className="px-3 py-3.5 text-[12px] text-slate-700">{row.assignedOfficerName ?? "Unassigned"}</td>
                          <td className="whitespace-nowrap px-3 py-3.5 text-[12px] text-slate-600">{shortWhen(row.lastInteractionAt)}</td>
                          <td className="px-3 py-3.5 pr-4" onClick={(event) => event.stopPropagation()}>
                            <div className="flex items-center gap-1.5">
                              <button
                                type="button"
                                onClick={() => setOpenLeadId(row.leadId)}
                                aria-label={`View ${row.customerName}`}
                                className="inline-flex h-8 items-center rounded-lg border border-slate-200 bg-white px-3 text-[12px] font-semibold text-slate-700 hover:bg-slate-50"
                              >
                                View
                              </button>
                              <RowMenu row={row} onView={() => setOpenLeadId(row.leadId)} />
                            </div>
                          </td>
                        </motion.tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Mobile list */}
                <ul className="m-0 divide-y divide-slate-100 p-0 lg:hidden">
                  {pagedRows.map((row) => (
                    <li key={row.leadId} className="list-none">
                      <button type="button" onClick={() => setOpenLeadId(row.leadId)} className="flex w-full items-start gap-3 px-4 py-3.5 text-left transition-colors hover:bg-slate-50/80 active:bg-slate-50">
                        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-slate-100 text-[12px] font-semibold text-[#0B1640]">{initialsOf(row.customerName)}</div>
                        <div className="min-w-0 flex-1">
                          <div className="flex items-start justify-between gap-2">
                            <div className="min-w-0">
                              <p className="m-0 truncate text-[14px] font-semibold text-[#0B1640]">{row.customerName}</p>
                              <p className="m-0 mt-0.5 text-[11px] text-slate-500">{row.occupation ?? "Work not known yet"}</p>
                            </div>
                            <ChevronRight className="mt-1 h-4 w-4 shrink-0 text-slate-300" />
                          </div>
                          <p className="m-0 mt-1.5 text-[12px] text-slate-600">
                            <span className="font-mono">{row.maskedContact}</span>
                            <span className="text-slate-300"> · </span>
                            {row.primaryChannel ?? "Manual"}
                          </p>
                          <div className="mt-2 flex flex-wrap items-center gap-2">
                            <CipStatusPill tone={stageTone(row.stage)}>{row.stageLabel}</CipStatusPill>
                            <span className="truncate text-[11px] text-slate-500">{row.productInterest ?? "Not yet known"}</span>
                          </div>
                          <p className="m-0 mt-1.5 text-[11px] text-slate-400">Last interaction: {formatDateTime(row.lastInteractionAt, "Not recorded")}</p>
                        </div>
                      </button>
                    </li>
                  ))}
                </ul>
              </>
            )}

            {!loading && !error && !forbidden && rows.length > 0 ? (
              <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 px-4 py-3 sm:px-5">
                <p className="m-0 text-[12px] text-slate-500">
                  Showing {rangeStart}–{rangeEnd} of {rows.length.toLocaleString()} prospects
                </p>
                <div className="flex items-center gap-1.5">
                  <button
                    type="button"
                    disabled={safePage <= 1}
                    onClick={() => setPage(Math.max(1, safePage - 1))}
                    className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                    aria-label="Previous page"
                  >
                    ‹
                  </button>
                  <span className="inline-flex h-8 min-w-[2rem] items-center justify-center rounded-lg bg-[#1769FF] px-2 text-[12px] font-semibold text-white">{safePage}</span>
                  <button
                    type="button"
                    disabled={safePage >= pageCount}
                    onClick={() => setPage(Math.min(pageCount, safePage + 1))}
                    className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                    aria-label="Next page"
                  >
                    ›
                  </button>
                </div>
              </div>
            ) : null}
          </section>
        </>
      )}

      <LeadCustomerModal
        leadId={openLeadId}
        onClose={() => setOpenLeadId(null)}
        load={getLeadDetail}
        addNote={addNote}
        assign={assign}
        refreshInsights={refreshInsights}
      />
    </div>
  );
}

/** The "..." menu on a row, as in CIP: view the record, open the conversation when there is one. */
function RowMenu({ row, onView }: { row: SalesLeadView; onView: () => void }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const onDoc = (event: MouseEvent) => {
      if (!ref.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [open]);
  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`More actions for ${row.customerName}`}
        className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-slate-50"
      >
        <MoreHorizontal className="h-4 w-4" aria-hidden />
      </button>
      {open ? (
        <div role="menu" className="absolute right-0 z-20 mt-1 w-44 rounded-xl border border-slate-200 bg-white p-1 shadow-[0_16px_40px_rgba(15,23,42,0.14)]">
          <button type="button" role="menuitem" onClick={() => { setOpen(false); onView(); }} className="block w-full rounded-lg px-3 py-2 text-left text-[12.5px] font-medium text-slate-700 hover:bg-slate-50">
            View details
          </button>
          {row.conversationId ? (
            <Link role="menuitem" href={`/app/channels?thread=${encodeURIComponent(row.conversationId)}`} className="block rounded-lg px-3 py-2 text-[12.5px] font-medium text-slate-700 hover:bg-slate-50">
              Open conversation
            </Link>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function CaptureLead({ creating, onCancel, onSave }: {
  creating: boolean;
  onCancel: () => void;
  onSave: (input: { customerName: string; leadSource: string; customerNeed: string; productFit: string; contactInfo: string; bankCustomer: boolean | null }) => Promise<void>;
}) {
  const [form, setForm] = useState<{ customerName: string; leadSource: string; customerNeed: string; productFit: string; contactInfo: string; bankCustomer: boolean | null }>(
    { customerName: "", leadSource: "Manual capture", customerNeed: "", productFit: "", contactInfo: "", bankCustomer: null });
  return (
    <section className="rounded-xl border border-slate-200/90 bg-white p-4 shadow-[0_1px_2px_rgba(15,23,42,0.03)] sm:p-5">
      <h2 className="m-0 text-[15px] font-semibold text-[#0B1640]">Capture a lead</h2>
      <div className="mt-3 grid gap-3 md:grid-cols-2">
        <Field label="Customer name" required value={form.customerName} onChange={(value) => setForm({ ...form, customerName: value })} />
        <Field label="Phone or email" value={form.contactInfo} onChange={(value) => setForm({ ...form, contactInfo: value })} />
        <Field label="What does the customer need?" required value={form.customerNeed} onChange={(value) => setForm({ ...form, customerNeed: value })} className="md:col-span-2" />
        <Field label="Cover they asked about" value={form.productFit} onChange={(value) => setForm({ ...form, productFit: value })} />
        <Field label="Where the lead came from" value={form.leadSource} onChange={(value) => setForm({ ...form, leadSource: value })} />
        <label className="block">
          <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-slate-400">Banks with the partner bank?</span>
          <select
            value={form.bankCustomer === null ? "" : form.bankCustomer ? "yes" : "no"}
            onChange={(event) => setForm({ ...form, bankCustomer: event.target.value === "" ? null : event.target.value === "yes" })}
            className="w-full rounded-lg border border-slate-200 bg-white px-2.5 py-2 text-[13px] text-[#0B1640] outline-none focus:border-[#1769FF]/40"
          >
            <option value="">Not known</option>
            <option value="yes">Yes</option>
            <option value="no">No</option>
          </select>
        </label>
      </div>
      <div className="mt-4 flex justify-end gap-2 border-t border-slate-100 pt-3">
        <button type="button" onClick={onCancel} className="rounded-lg px-3 py-1.5 text-[12px] font-medium text-slate-600 hover:bg-slate-50">Cancel</button>
        <button
          type="button"
          onClick={() => void onSave(form)}
          disabled={creating || !form.customerName.trim() || !form.customerNeed.trim()}
          className="inline-flex items-center gap-1.5 rounded-lg bg-[#1769FF] px-3 py-1.5 text-[12px] font-semibold text-white hover:bg-[#1258d6] disabled:cursor-not-allowed disabled:opacity-50"
        >
          {creating ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : null}
          Save lead
        </button>
      </div>
    </section>
  );
}

function Field({ label, value, onChange, className, required }: { label: string; value: string; onChange: (value: string) => void; className?: string; required?: boolean }) {
  return (
    <label className={`block ${className ?? ""}`}>
      <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-slate-400">
        {label}
        {required ? " (required)" : ""}
      </span>
      <Input value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}
