"use client";
import { useState, useMemo } from "react";
import { Link } from "@/components/iip/NextRouterCompat";
import { toast } from "sonner";
import { AlertTriangle, ArrowRight, ClipboardList, Filter, Loader2, Search, UserCheck, X } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge, SLABadge, RiskBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { useClaims } from "@/lib/lovable-parity/data/claims";
import type { ClaimRowView } from "@/lib/lovable-parity/data/claims";
import { useRuntimeContext } from "@/lib/runtimeContext";

const quickFilters = [
  "All",
  "My queue",
  "Unassigned",
  "Missing documents",
  "Pending approval",
  "Escalated",
  "SLA attention",
] as const;
type QuickFilter = (typeof quickFilters)[number];

export default function ClaimsQueue() {
  const { claims, loadState, refresh, assignCase, escalateCase } = useClaims();
  const loading = loadState.status === "loading";
  const error = "error" in loadState ? loadState.error : null;
  const snapshotAvailable = loadState.status === "success" || loadState.status === "empty";
  const { runtimeContext } = useRuntimeContext();
  const [f, setF] = useState<QuickFilter>("All");
  const [q, setQ] = useState("");
  const [productFilter, setProductFilter] = useState("All");
  const [riskFilter, setRiskFilter] = useState("All");
  const [channelFilter, setChannelFilter] = useState("All");
  const [busyCaseId, setBusyCaseId] = useState<string | null>(null);

  const productOptions = useMemo(() => uniqueOptions(claims.map((claim) => claim.productLine)), [claims]);
  const riskOptions = useMemo(() => uniqueOptions(claims.map((claim) => claim.risk)), [claims]);
  const channelOptions = useMemo(() => uniqueOptions(claims.map((claim) => claim.channel)), [claims]);

  const quickCounts = useMemo(() => {
    const counts = new Map<QuickFilter, number>();
    for (const filter of quickFilters) {
      counts.set(filter, claims.filter((claim) => matchesQuickFilter(claim, filter, runtimeContext?.userId ?? null)).length);
    }
    return counts;
  }, [claims, runtimeContext?.userId]);

  const rows = useMemo(() => claims.filter(c => {
    if (!matchesQuickFilter(c, f, runtimeContext?.userId ?? null)) return false;
    if (productFilter !== "All" && c.productLine !== productFilter) return false;
    if (riskFilter !== "All" && c.risk !== riskFilter) return false;
    if (channelFilter !== "All" && c.channel !== channelFilter) return false;
    if (q && !`${c.id} ${c.customer} ${c.policy} ${c.productLine} ${c.claimType} ${c.status} ${c.assignedUserId ?? ""} ${c.assignedTeam ?? ""}`.toLowerCase().includes(q.toLowerCase())) return false;
    return true;
  }), [channelFilter, claims, f, productFilter, q, riskFilter, runtimeContext?.userId]);

  const activeStructuredFilters = [productFilter, riskFilter, channelFilter].filter((value) => value !== "All").length;

  function clearFilters() {
    setF("All");
    setQ("");
    setProductFilter("All");
    setRiskFilter("All");
    setChannelFilter("All");
  }

  async function assignToMe(caseId: string) {
    if (!runtimeContext?.userId) {
      toast.error("User context is not available.");
      return;
    }

    setBusyCaseId(caseId);
    try {
      const result = await assignCase(caseId, runtimeContext.userId);
      if (result.ok) toast.success("Case assigned", { description: "The work queue was refreshed from the backend." });
      else toast.error(result.message ?? "Assignment failed.");
    } finally {
      setBusyCaseId(null);
    }
  }

  async function escalate(caseId: string) {
    setBusyCaseId(caseId);
    try {
      const result = await escalateCase(caseId, "Escalated from claims work queue for supervisor review.");
      if (result.ok) toast.success("Case escalated", { description: "The case is now assigned to Claims Supervision." });
      else toast.error(result.message ?? "Escalation failed.");
    } finally {
      setBusyCaseId(null);
    }
  }

  return (
    <>
      <SectionHeader
        eyebrow="Claims Intelligence"
        title="Work Queue"
        sub="Claims requiring officer attention across Motor Operations. AI has pre-processed extractions, evidence, and risk signals — officers decide."
        action={<Link to="/app/claims/new" className="inline-flex min-h-11 items-center rounded-2xl gradient-primary px-4 py-2.5 text-[13px] font-medium text-white shadow-l2 hover-lift focus-ring">+ New Claim (FNOL)</Link>}
      />

      <div className="glass-subtle p-3 mb-4 space-y-3">
        <div className="flex flex-wrap items-center gap-2">
        <label htmlFor="claim-search" className="flex min-h-11 w-full min-w-0 flex-1 items-center gap-2 rounded-xl border border-line bg-white/80 px-3 py-2 sm:min-w-[280px] sm:max-w-md">
          <Search className="h-3.5 w-3.5 text-muted" />
          <span className="sr-only">Search claims</span>
          <input id="claim-search" name="claim-search" aria-label="Search claims" autoComplete="off" value={q} onChange={e => setQ(e.target.value)} placeholder="Search claim, customer, policy, assignment…" className="min-w-0 flex-1 bg-transparent text-[13px] text-ink outline-none placeholder:text-muted" />
        </label>
        <div className="flex flex-wrap gap-1.5">
          {quickFilters.map(x => (
            <button key={x} onClick={() => setF(x)} className={`min-h-11 rounded-full border px-3 py-2 text-[12px] transition-colors ${f === x ? "border-info/30 bg-info/8 text-info" : "border-line bg-white/70 text-ink-soft hover:bg-white"}`}>
              {x} <span className="ml-1 text-[10px] opacity-70">{snapshotAvailable ? quickCounts.get(x) ?? 0 : "—"}</span>
            </button>
          ))}
        </div>
        <button
          type="button"
          onClick={clearFilters}
          disabled={f === "All" && q.length === 0 && activeStructuredFilters === 0}
          className="ml-auto inline-flex min-h-11 items-center gap-1.5 rounded-xl border border-line bg-white/70 px-3 py-2 text-[12px] text-ink-soft disabled:cursor-not-allowed disabled:opacity-45"
        >
          <X className="h-3.5 w-3.5" />
          Clear
        </button>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Filter className="h-3.5 w-3.5 text-muted" />
          <QueueSelect label="Product" value={productFilter} options={productOptions} onChange={setProductFilter} />
          <QueueSelect label="Risk" value={riskFilter} options={riskOptions} onChange={setRiskFilter} />
          <QueueSelect label="Channel" value={channelFilter} options={channelOptions} onChange={setChannelFilter} />
          <div className="ml-auto text-[12px] text-muted">Assignment and escalation execute through audited API actions.</div>
        </div>
      </div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-muted">Loading claims…</div></GlassCard>
      ) : error ? (
        <GlassCard>
          <div className="flex flex-col items-center gap-2 p-10 text-center">
            <ClipboardList className="h-6 w-6 text-muted" />
            <div className="font-medium text-ink">Claims could not be loaded</div>
            <div className="max-w-md text-[12.5px] text-muted">{error.message}</div>
            <div className="max-w-md text-[12px] text-muted">{error.recovery}</div>
            {error.correlationId ? <div className="font-mono text-[11px] text-muted">Reference {error.correlationId}</div> : null}
            <div className="text-[11px] text-muted">Attempted {new Date(error.occurredAt).toLocaleString()}</div>
            {error.retryable ? <button type="button" onClick={() => void refresh()} className="mt-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring">Try again</button> : <Link to="/app" className="mt-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring">Return to Command Center</Link>}
          </div>
        </GlassCard>
      ) : claims.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <ClipboardList className="h-6 w-6 text-muted" />
            <div className="text-ink font-medium">No claims in this workspace</div>
            <div className="text-[12.5px] text-muted max-w-md">Claims appear here once created from FNOL intake or channel conversations.</div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <div data-testid="claims-card-list" className="divide-y divide-line/70 md:hidden">
            {rows.map((claim) => (
              <article key={claim.id} data-testid="claim-card" className="space-y-3 p-4">
                <div className="flex min-w-0 items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h2 className="break-words text-[14px] font-semibold text-ink">{claim.id}</h2>
                    <p className="mt-0.5 break-words text-[12.5px] text-ink-soft">{claim.customer} · {claim.policy}</p>
                  </div>
                  <StatusBadge status={claim.status} />
                </div>
                <div className="flex flex-wrap gap-2">{claim.requiresConfirmation ? <Badge tone="warning">Needs confirmation</Badge> : null}<SLABadge sla={claim.sla} /><RiskBadge risk={claim.risk} /></div>
                <dl className="grid gap-2 text-[12px] text-ink-soft min-[360px]:grid-cols-2">
                  <div><dt className="text-muted">Evidence</dt><dd>{claim.complete ? "Documents complete" : "Documents incomplete"}</dd></div>
                  <div><dt className="text-muted">Owner</dt><dd className="break-words">{claim.assignedUserId ?? claim.assignedTeam ?? "Unassigned"}</dd></div>
                  <div><dt className="text-muted">Product</dt><dd>{claim.productLine} · {claim.claimType}</dd></div>
                  <div><dt className="text-muted">Updated</dt><dd>{claim.updated}</dd></div>
                </dl>
                <ClaimActions claim={claim} busy={busyCaseId === claim.id} onAssign={assignToMe} onEscalate={escalate} compact />
              </article>
            ))}
          </div>

          <div data-testid="claims-tablet-table" className="hidden overflow-x-auto md:block xl:hidden">
            <table className="w-full text-[13px]">
              <caption className="sr-only">Claims priority queue with expandable details</caption>
              <thead><tr className="border-b border-line text-left text-[11px] uppercase tracking-[0.12em] text-muted"><th className="px-4 py-3">Claim & details</th><th className="px-3 py-3">Status</th><th className="px-3 py-3">SLA</th><th className="px-3 py-3">Owner</th><th className="px-3 py-3">Action</th></tr></thead>
              <tbody>{rows.map((claim) => <tr key={claim.id} className="border-b border-line/60 align-top">
                <td className="px-4 py-3"><div className="flex items-center gap-2"><span className="font-medium text-ink">{claim.id}</span>{claim.requiresConfirmation ? <Badge tone="warning">Needs confirmation</Badge> : null}</div><div className="text-[11px] text-muted">{claim.customer}</div><details className="mt-2"><summary className="min-h-6 cursor-pointer text-[11.5px] text-info focus-ring">More details</summary><dl className="mt-2 grid grid-cols-2 gap-2 text-[11px] text-ink-soft"><div><dt className="text-muted">Evidence</dt><dd>{claim.complete ? "Complete" : "Incomplete"}</dd></div><div><dt className="text-muted">Risk</dt><dd>{claim.risk}</dd></div><div><dt className="text-muted">Product</dt><dd>{claim.productLine} · {claim.claimType}</dd></div><div><dt className="text-muted">Updated</dt><dd>{claim.updated}</dd></div></dl></details></td>
                <td className="px-3 py-3"><StatusBadge status={claim.status} /></td><td className="px-3 py-3"><SLABadge sla={claim.sla} /></td><td className="max-w-40 break-words px-3 py-3 text-ink-soft">{claim.assignedUserId ?? claim.assignedTeam ?? "Unassigned"}</td><td className="px-3 py-3"><Link to={`/app/claims/${claim.id}`} className="inline-flex min-h-11 items-center gap-1 rounded-xl border border-line bg-white px-3 py-2 text-info focus-ring">Review <ArrowRight className="h-3.5 w-3.5" /></Link></td>
              </tr>)}</tbody>
            </table>
          </div>

          <div data-testid="claims-desktop-table" className="hidden overflow-x-auto xl:block">
            <table className="w-full text-[13px]">
              <caption className="sr-only">Complete claims work queue</caption>
              <thead>
                <tr className="text-left text-[11px] uppercase tracking-[0.12em] text-muted border-b border-line">
                  {["Claim","Customer","Product","Status","Docs","SLA","Risk","Confidence","Assigned","Updated","Actions"].map(h => (
                    <th key={h} className="px-4 py-3 font-medium">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map(c => (
                  <tr key={c.id} className="border-b border-line/60 hover:bg-white/70 transition cursor-pointer group">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <span className="font-medium text-ink">{c.id}</span>
                        {c.requiresConfirmation ? <Badge tone="warning">Needs confirmation</Badge> : null}
                      </div>
                      <div className="text-[11px] text-muted">{c.policy}</div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="text-ink">{c.customer}</div>
                      <div className="text-[11px] text-muted">{c.channel}</div>
                    </td>
                    <td className="px-4 py-3 text-ink-soft">{c.productLine} · {c.claimType}</td>
                    <td className="px-4 py-3"><StatusBadge status={c.status} /></td>
                    <td className="px-4 py-3">{c.complete ? <Badge tone="success">Complete</Badge> : <Badge tone="warning">Incomplete</Badge>}</td>
                    <td className="px-4 py-3"><SLABadge sla={c.sla} /></td>
                    <td className="px-4 py-3"><RiskBadge risk={c.risk} /></td>
                    <td className="px-4 py-3"><ConfidenceBadge value={c.confidence} /></td>
                    <td className="px-4 py-3 text-ink-soft whitespace-nowrap">
                      {c.assignedUserId ? c.assignedUserId : c.assignedTeam ?? "Unassigned"}
                    </td>
                    <td className="px-4 py-3 text-muted whitespace-nowrap">{c.updated}</td>
                    <td className="px-4 py-3">
                      <ClaimActions claim={c} busy={busyCaseId === c.id} onAssign={assignToMe} onEscalate={escalate} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between px-4 py-3 text-[12px] text-muted border-t border-line">
            <div>Showing {rows.length} of {claims.length} claims</div>
          </div>
        </GlassCard>
      )}
    </>
  );
}

function uniqueOptions(values: string[]) {
  return Array.from(new Set(values.filter(Boolean))).sort((a, b) => a.localeCompare(b));
}

function normalized(value: string) {
  return value.replace(/[\s_-]/g, "").toLowerCase();
}

function matchesQuickFilter(claim: { status: string; sla: string; complete: boolean; assignedUserId: string | null; assignedTeam: string | null }, filter: QuickFilter, userId: string | null) {
  const status = normalized(claim.status);
  const sla = normalized(claim.sla);
  switch (filter) {
    case "All":
      return true;
    case "My queue":
      return Boolean(userId && claim.assignedUserId === userId);
    case "Unassigned":
      return !claim.assignedUserId && !claim.assignedTeam;
    case "Missing documents":
      return !claim.complete || status.includes("missingdocuments");
    case "Pending approval":
      return status.includes("pendingapproval");
    case "Escalated":
      return status.includes("escalated");
    case "SLA attention":
      return sla !== "normal";
  }
}

function QueueSelect({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: string;
  options: string[];
  onChange: (value: string) => void;
}) {
  const id = `queue-filter-${label.toLowerCase()}`;
  return (
    <label htmlFor={id} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-line bg-white/70 px-3 py-2 text-[12px] text-ink-soft">
      <span className="text-muted">{label}</span>
      <select
        id={id}
        aria-label={`Filter by ${label.toLowerCase()}`}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="bg-transparent text-ink outline-none"
      >
        <option value="All">All</option>
        {options.map((option) => (
          <option key={option} value={option}>{option}</option>
        ))}
      </select>
    </label>
  );
}

function ClaimActions({ claim, busy, onAssign, onEscalate, compact = false }: { claim: ClaimRowView; busy: boolean; onAssign: (id: string) => Promise<void>; onEscalate: (id: string) => Promise<void>; compact?: boolean }) {
  return <div className={`flex flex-wrap items-center gap-2 ${compact ? "border-t border-line/70 pt-3" : ""}`}>
    <Link to={`/app/claims/${claim.id}`} className="inline-flex min-h-11 items-center gap-1 rounded-xl border border-line bg-white px-3 py-2 text-[12px] font-medium text-info focus-ring">Review <ArrowRight className="h-3.5 w-3.5" /></Link>
    <button type="button" onClick={() => void onAssign(claim.id)} disabled={busy} className="inline-flex min-h-11 items-center gap-1 rounded-xl border border-line bg-white px-3 py-2 text-[12px] text-ink focus-ring disabled:opacity-50">{busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <UserCheck className="h-3.5 w-3.5" />}Assign</button>
    <button type="button" onClick={() => void onEscalate(claim.id)} disabled={busy || claim.status === "Escalated"} className="inline-flex min-h-11 items-center gap-1 rounded-xl border border-warning/32 bg-warning/8 px-3 py-2 text-[12px] text-[hsl(38_92%_36%)] focus-ring disabled:opacity-50"><AlertTriangle className="h-3.5 w-3.5" />Escalate</button>
  </div>;
}
