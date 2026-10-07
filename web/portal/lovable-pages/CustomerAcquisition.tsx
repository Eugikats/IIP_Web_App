"use client";

import { useCallback, useMemo, useState } from "react";
import { motion, useReducedMotion } from "framer-motion";
import { ChevronLeft, ChevronRight, Loader2, MessageCircle, Plus, Search, UserRound } from "lucide-react";
import { toast } from "sonner";
import { SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { StatusPill } from "@/components/ui/StatusPill";
import {
  EmptyFilteredStateSurface,
  EmptyStateSurface,
  ErrorStateSurface,
  LoadingSkeleton,
  NoPermissionStateSurface,
} from "@/components/ui/StateSurfaces";
import { LeadCustomerModal, relationshipTone, stageTone } from "@/components/acquisition/LeadCustomerModal";
import { useSalesLeads, type SalesLeadView } from "@/lib/lovable-parity/data/salesLeads";
import { formatDateTime } from "@/lib/format";
import { cn } from "@/lib/utils";

// The prospect queue: everyone who might buy cover, from WhatsApp conversations and staff capture.
// Counts and trends are computed from the records on screen; a trend line is drawn only from real
// daily counts, and a tile with no movement in the window says "No trend" rather than inventing one.

const PAGE_SIZE = 20;
const TREND_DAYS = 14;

type StageFilter = "All" | "Ready" | "Talking" | "WithAPerson" | "Applied" | "NotNow";
type RelationshipFilter = "All" | "NewToInsurer" | "Registered" | "ExistingCustomer";
type BankFilter = "All" | "Yes" | "No" | "Unknown";
type SortKey = "recent" | "name" | "stage";

const STAGE_FILTERS: { key: StageFilter; label: string; stages: string[] }[] = [
  { key: "All", label: "All stages", stages: [] },
  { key: "Ready", label: "Ready to apply", stages: ["ReadyToApply"] },
  { key: "Talking", label: "In conversation", stages: ["New", "GettingToKnow", "Interested", "Considering"] },
  { key: "WithAPerson", label: "With a person", stages: ["WithAPerson"] },
  { key: "Applied", label: "Applied", stages: ["Applied"] },
  { key: "NotNow", label: "Not now", stages: ["NotNow"] },
];

const STAGE_ORDER = ["ReadyToApply", "WithAPerson", "Considering", "Interested", "GettingToKnow", "New", "Applied", "NotNow"];

export default function CustomerAcquisition() {
  const { leads, loading, error, forbidden, creating, reload, createLead, getLeadDetail, addNote, assign, refreshInsights } = useSalesLeads();
  const [query, setQuery] = useState("");
  const [stageFilter, setStageFilter] = useState<StageFilter>("All");
  const [relationshipFilter, setRelationshipFilter] = useState<RelationshipFilter>("All");
  const [bankFilter, setBankFilter] = useState<BankFilter>("All");
  const [sort, setSort] = useState<SortKey>("recent");
  const [page, setPage] = useState(0);
  const [openLeadId, setOpenLeadId] = useState<string | null>(null);
  const [capturing, setCapturing] = useState(false);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const stages = STAGE_FILTERS.find((item) => item.key === stageFilter)?.stages ?? [];
    const rows = leads.filter((lead) => {
      if (stages.length > 0 && !stages.includes(lead.stage)) return false;
      if (relationshipFilter !== "All" && lead.relationship !== relationshipFilter) return false;
      if (bankFilter === "Yes" && lead.bankCustomer !== true) return false;
      if (bankFilter === "No" && lead.bankCustomer !== false) return false;
      if (bankFilter === "Unknown" && lead.bankCustomer !== null) return false;
      if (q && !`${lead.customerName} ${lead.maskedContact} ${lead.productInterest ?? ""} ${lead.occupation ?? ""}`.toLowerCase().includes(q)) return false;
      return true;
    });
    return [...rows].sort((a, b) => {
      if (sort === "name") return a.customerName.localeCompare(b.customerName);
      if (sort === "stage") return STAGE_ORDER.indexOf(a.stage) - STAGE_ORDER.indexOf(b.stage);
      return (b.lastInteractionAt ?? "").localeCompare(a.lastInteractionAt ?? "");
    });
  }, [leads, query, stageFilter, relationshipFilter, bankFilter, sort]);

  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const currentPage = Math.min(page, pageCount - 1);
  const visible = filtered.slice(currentPage * PAGE_SIZE, currentPage * PAGE_SIZE + PAGE_SIZE);
  const filtersActive = query.length > 0 || stageFilter !== "All" || relationshipFilter !== "All" || bankFilter !== "All";

  const clearFilters = useCallback(() => {
    setQuery("");
    setStageFilter("All");
    setRelationshipFilter("All");
    setBankFilter("All");
    setPage(0);
  }, []);

  return (
    <>
      <SectionHeader
        eyebrow="Customer acquisition"
        title="Prospects"
        sub="Everyone who might take cover: people who wrote to Kato on WhatsApp and leads your team captured. Open a prospect to see what they told us and what the agent made of it."
      />

      <KpiStrip leads={leads} loading={loading} />

      <div className="mt-5 flex flex-col gap-3">
        <label className="relative block w-full">
          <span className="sr-only">Search prospects</span>
          <Input
            leadingIcon={<Search aria-hidden="true" />}
            value={query}
            onChange={(event) => {
              setQuery(event.target.value);
              setPage(0);
            }}
            placeholder="Search name, masked contact, work or cover"
          />
        </label>
        <div className="flex flex-wrap items-center gap-2">
          <Select label="Stage" value={stageFilter} onChange={(value) => { setStageFilter(value as StageFilter); setPage(0); }}
            options={STAGE_FILTERS.map((item) => ({ value: item.key, label: item.label }))} />
          <Select label="Relationship" value={relationshipFilter} onChange={(value) => { setRelationshipFilter(value as RelationshipFilter); setPage(0); }}
            options={[
              { value: "All", label: "Any relationship" },
              { value: "NewToInsurer", label: "New to insurer" },
              { value: "Registered", label: "Registered" },
              { value: "ExistingCustomer", label: "Existing customer" },
            ]} />
          <Select label="Bank customer" value={bankFilter} onChange={(value) => { setBankFilter(value as BankFilter); setPage(0); }}
            options={[
              { value: "All", label: "Bank customer: any" },
              { value: "Yes", label: "Bank customer: yes" },
              { value: "No", label: "Bank customer: no" },
              { value: "Unknown", label: "Bank customer: not known" },
            ]} />
          <Select label="Sort" value={sort} onChange={(value) => setSort(value as SortKey)}
            options={[
              { value: "recent", label: "Most recent" },
              { value: "stage", label: "Closest to buying" },
              { value: "name", label: "Name" },
            ]} />
          <Button variant="primary" onClick={() => setCapturing((value) => !value)} icon={<Plus aria-hidden="true" />}>
            New lead
          </Button>
        </div>
      </div>

      {capturing ? <CaptureLead creating={creating} onCancel={() => setCapturing(false)} onSave={async (input) => {
        const result = await createLead(input);
        if (result.ok) {
          toast.success("Lead captured");
          setCapturing(false);
        } else {
          toast.error(result.message ?? "The lead could not be saved.");
        }
      }} /> : null}

      <section className="mt-5 overflow-hidden rounded-dls-card border border-dls-border-subtle bg-dls-surface" aria-labelledby="prospect-queue-title">
        <div className="flex items-center justify-between gap-3 border-b border-dls-border-subtle px-5 py-4">
          <h2 id="prospect-queue-title" className="m-0 text-[16px] font-bold text-dls-text-primary">Prospect queue</h2>
          {!loading && !error && !forbidden ? (
            <span className="text-[12.5px] text-dls-text-muted">{filtered.length} of {leads.length}</span>
          ) : null}
        </div>

        {loading ? (
          <div className="p-5"><LoadingSkeleton rows={6} /></div>
        ) : forbidden ? (
          <div className="p-5"><NoPermissionStateSurface permission="View sales leads" /></div>
        ) : error ? (
          <div className="p-5"><ErrorStateSurface title="Prospects unavailable" message={error} onRetry={() => void reload()} /></div>
        ) : leads.length === 0 ? (
          <div className="p-5">
            <EmptyStateSurface
              title="No prospects yet"
              message="Prospects appear here when someone writes to Kato on WhatsApp about cover, or when your team captures a lead."
            />
          </div>
        ) : filtered.length === 0 ? (
          <div className="p-5"><EmptyFilteredStateSurface onClear={clearFilters} /></div>
        ) : (
          <>
            <ProspectTable rows={visible} onOpen={setOpenLeadId} />
            {pageCount > 1 ? (
              <div className="flex items-center justify-between gap-3 border-t border-dls-border-subtle px-5 py-3 text-[13px] text-dls-text-secondary">
                <span>Page {currentPage + 1} of {pageCount}</span>
                <div className="flex gap-2">
                  <Button variant="secondary" onClick={() => setPage(currentPage - 1)} disabled={currentPage === 0} icon={<ChevronLeft aria-hidden="true" />}>Previous</Button>
                  <Button variant="secondary" onClick={() => setPage(currentPage + 1)} disabled={currentPage >= pageCount - 1} icon={<ChevronRight aria-hidden="true" />}>Next</Button>
                </div>
              </div>
            ) : null}
          </>
        )}
        {filtersActive && !loading && filtered.length > 0 ? (
          <div className="border-t border-dls-border-subtle px-5 py-3">
            <button type="button" onClick={clearFilters} className="dls-focus-outline text-[13px] font-semibold text-dls-primary-accent hover:underline">Clear filters</button>
          </div>
        ) : null}
      </section>

      <LeadCustomerModal
        leadId={openLeadId}
        onClose={() => setOpenLeadId(null)}
        load={getLeadDetail}
        addNote={addNote}
        assign={assign}
        refreshInsights={refreshInsights}
      />
    </>
  );
}

function ProspectTable({ rows, onOpen }: { rows: SalesLeadView[]; onOpen: (leadId: string) => void }) {
  const reduceMotion = useReducedMotion();
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[1320px] border-collapse text-left">
        <thead>
          <tr className="border-b border-dls-border-subtle">
            {["Customer", "Masked contact", "Bank customer", "Relationship", "Channel", "Product interest", "Stage", "Assigned officer", "Last interaction", ""].map((heading) => (
              <th key={heading || "action"} scope="col" className="px-5 py-3 text-[11.5px] font-semibold uppercase tracking-[0.06em] text-dls-text-muted">
                {heading || <span className="sr-only">Action</span>}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((lead, index) => (
            <motion.tr
              key={lead.leadId}
              className="border-b border-dls-border-subtle last:border-b-0 hover:bg-dls-bg-page"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              transition={{ duration: reduceMotion ? 0 : 0.15, delay: reduceMotion ? 0 : Math.min(index, 10) * 0.02 }}
            >
              <td className="min-w-[10rem] px-5 py-3.5">
                <button type="button" onClick={() => onOpen(lead.leadId)} className="dls-focus-outline text-left">
                  <div className="text-[14px] font-semibold text-dls-text-primary hover:underline">{lead.customerName}</div>
                  <div className="text-[12.5px] text-dls-text-muted">{lead.occupation ?? "Work not known yet"}</div>
                </button>
              </td>
              <td className="px-5 py-3.5 text-[13.5px] tabular-nums text-dls-text-secondary">{lead.maskedContact}</td>
              <td className="px-5 py-3.5"><StatusPill tone={lead.bankCustomer === true ? "success" : "neutral"} label={lead.bankCustomerLabel} /></td>
              <td className="px-5 py-3.5"><StatusPill tone={relationshipTone(lead.relationship)} label={lead.relationshipLabel} /></td>
              <td className="px-5 py-3.5 text-[13.5px] text-dls-text-secondary">
                <span className="inline-flex items-center gap-1.5">
                  {lead.primaryChannel === "WhatsApp" ? <MessageCircle className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" /> : <UserRound className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" />}
                  {lead.primaryChannel ?? "Manual"}
                </span>
              </td>
              <td className="min-w-[13rem] px-5 py-3.5 text-[13.5px] text-dls-text-primary">{lead.productInterest ?? <span className="text-dls-text-muted">Not yet known</span>}</td>
              <td className="px-5 py-3.5"><StatusPill tone={stageTone(lead.stage)} label={lead.stageLabel} /></td>
              <td className="px-5 py-3.5 text-[13.5px] text-dls-text-secondary">{lead.assignedOfficerName ?? "Unassigned"}</td>
              <td className="px-5 py-3.5 text-[13px] text-dls-text-secondary">{formatDateTime(lead.lastInteractionAt, "Not recorded")}</td>
              <td className="px-5 py-3.5 text-right">
                <Button variant="secondary" onClick={() => onOpen(lead.leadId)} aria-label={`View ${lead.customerName}`}>View</Button>
              </td>
            </motion.tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function KpiStrip({ leads, loading }: { leads: SalesLeadView[]; loading: boolean }) {
  const tiles = useMemo(() => [
    { label: "Total prospects", rows: leads },
    { label: "Existing customers", rows: leads.filter((lead) => lead.relationship === "ExistingCustomer") },
    { label: "New to the insurer", rows: leads.filter((lead) => lead.relationship === "NewToInsurer") },
    { label: "Ready to apply", rows: leads.filter((lead) => lead.stage === "ReadyToApply") },
    { label: "Unassigned", rows: leads.filter((lead) => !lead.assignedSalesOfficer) },
  ], [leads]);

  return (
    <div className="grid grid-cols-[repeat(auto-fit,minmax(112px,1fr))] gap-3">
      {tiles.map((tile) => (
        <div key={tile.label} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface px-4 py-4">
          <div className="text-[13px] font-semibold text-dls-text-secondary">{tile.label}</div>
          <div className="mt-2 flex flex-col gap-2">
            {loading ? (
              <Loader2 className="h-5 w-5 animate-spin text-dls-text-muted" aria-label="Loading" />
            ) : (
              <span className="text-[28px] font-bold leading-none tabular-nums text-dls-text-primary">{tile.rows.length}</span>
            )}
            {!loading ? <Trend rows={tile.rows} label={tile.label} /> : null}
          </div>
        </div>
      ))}
    </div>
  );
}

/** New prospects per day over the last 14 days, from when each record was created. */
function Trend({ rows, label }: { rows: SalesLeadView[]; label: string }) {
  const counts = useMemo(() => {
    const days = Array.from({ length: TREND_DAYS }, () => 0);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    for (const row of rows) {
      if (!row.createdAt) continue;
      const created = new Date(row.createdAt);
      created.setHours(0, 0, 0, 0);
      const age = Math.round((today.getTime() - created.getTime()) / 86_400_000);
      if (age >= 0 && age < TREND_DAYS) days[TREND_DAYS - 1 - age] += 1;
    }
    return days;
  }, [rows]);

  const max = Math.max(...counts);
  if (max === 0) {
    return (
      <span className="flex w-full flex-col items-start gap-1" title={`No new ${label.toLowerCase()} in the last ${TREND_DAYS} days`}>
        <span className="block w-full border-t border-dashed border-dls-border-strong" aria-hidden="true" />
        <span className="text-[11px] text-dls-text-muted">No trend</span>
      </span>
    );
  }

  const width = 120;
  const height = 32;
  const points = counts.map((count, index) => `${(index / (TREND_DAYS - 1)) * width},${height - 2 - (count / max) * (height - 4)}`).join(" ");
  return (
    <svg className="h-8 w-full" preserveAspectRatio="none" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`New per day over the last ${TREND_DAYS} days: ${counts.join(", ")}`}>
      <polyline points={points} fill="none" stroke="rgb(var(--color-primary))" strokeWidth="1.75" strokeLinejoin="round" strokeLinecap="round" vectorEffect="non-scaling-stroke" />
    </svg>
  );
}

function Select({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: { value: string; label: string }[] }) {
  return (
    <label className="inline-flex min-h-10 items-center rounded-dls-control border border-dls-border-input bg-dls-surface px-3 text-[13px] text-dls-text-primary focus-within:ring-2 focus-within:ring-dls-primary-border">
      <span className="sr-only">{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)} className={cn("cursor-pointer bg-transparent outline-none")}>
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </label>
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
    <div className="mt-4 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-5">
      <h3 className="m-0 text-[15px] font-bold text-dls-text-primary">Capture a lead</h3>
      <div className="mt-3 grid gap-3 md:grid-cols-2">
        <Field label="Customer name" required value={form.customerName} onChange={(value) => setForm({ ...form, customerName: value })} />
        <Field label="Phone or email" value={form.contactInfo} onChange={(value) => setForm({ ...form, contactInfo: value })} />
        <Field label="What does the customer need?" required value={form.customerNeed} onChange={(value) => setForm({ ...form, customerNeed: value })} className="md:col-span-2" />
        <Field label="Cover they asked about" value={form.productFit} onChange={(value) => setForm({ ...form, productFit: value })} />
        <Field label="Where the lead came from" value={form.leadSource} onChange={(value) => setForm({ ...form, leadSource: value })} />
        <label className="block">
          <span className="text-[12px] font-semibold text-dls-text-secondary">Banks with the partner bank?</span>
          <div className="mt-1">
            <Select label="Banks with the partner bank" value={form.bankCustomer === null ? "" : form.bankCustomer ? "yes" : "no"}
              onChange={(value) => setForm({ ...form, bankCustomer: value === "" ? null : value === "yes" })}
              options={[{ value: "", label: "Not known" }, { value: "yes", label: "Yes" }, { value: "no", label: "No" }]} />
          </div>
        </label>
      </div>
      <div className="mt-4 flex justify-end gap-2">
        <Button variant="ghost" onClick={onCancel}>Cancel</Button>
        <Button
          variant="primary"
          onClick={() => void onSave(form)}
          disabled={creating || !form.customerName.trim() || !form.customerNeed.trim()}
          icon={creating ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}
        >
          Save lead
        </Button>
      </div>
    </div>
  );
}

function Field({ label, value, onChange, className, required }: { label: string; value: string; onChange: (value: string) => void; className?: string; required?: boolean }) {
  return (
    <label className={cn("block", className)}>
      <span className="text-[12px] font-semibold text-dls-text-secondary">{label}{required ? <span className="text-dls-text-muted"> (required)</span> : null}</span>
      <div className="mt-1"><Input value={value} onChange={(event) => onChange(event.target.value)} /></div>
    </label>
  );
}
