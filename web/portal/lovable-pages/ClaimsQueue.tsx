"use client";
import { useState, useMemo } from "react";
import { Link } from "@/components/iip/NextRouterCompat";
import { toast } from "sonner";
import { TriangleAlert, ArrowRight, ClipboardList, Filter, Search, UserCheck, X } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge, SLABadge, RiskBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface, EmptyFilteredStateSurface } from "@/components/ui/StateSurfaces";
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
  const hasActiveFilters = f !== "All" || q.length > 0 || activeStructuredFilters > 0;

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
        action={
          <Link to="/app/claims/new" className="dls-focus-outline inline-flex min-h-11 items-center justify-center gap-1.5 rounded-dls-control bg-dls-primary px-4 py-[9px] text-[13px] font-bold text-dls-text-inverse shadow-dls-primary hover:bg-dls-primary-hover">
            + New Claim (FNOL)
          </Link>
        }
      />

      <div className="mb-4 space-y-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
        <div className="flex flex-wrap items-center gap-2">
          <label htmlFor="claim-search" className="flex min-h-11 w-full min-w-0 flex-1 items-center gap-2 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 focus-within:border-dls-primary focus-within:ring-[3px] focus-within:ring-dls-primary/[0.12] sm:min-w-[280px] sm:max-w-md">
            <Search className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" />
            <span className="sr-only">Search claims</span>
            <input id="claim-search" name="claim-search" aria-label="Search claims" autoComplete="off" value={q} onChange={e => setQ(e.target.value)} placeholder="Search claim, customer, policy, assignment…" className="min-w-0 flex-1 bg-transparent text-[13px] text-dls-text-primary outline-none placeholder:text-dls-text-muted" />
          </label>
          <div className="flex flex-wrap gap-1.5">
            {quickFilters.map(x => (
              <button
                key={x}
                type="button"
                onClick={() => setF(x)}
                className={`dls-focus-outline min-h-11 rounded-dls-chip border px-3 py-2 text-[12px] font-semibold transition-colors duration-dls-fast ease-dls ${f === x ? "border-dls-primary bg-dls-primary text-dls-text-inverse" : "border-dls-border-input bg-dls-surface text-dls-text-secondary hover:border-dls-primary"}`}
              >
                {x} <span className="ml-1 text-[10px] opacity-80">{snapshotAvailable ? quickCounts.get(x) ?? 0 : "—"}</span>
              </button>
            ))}
          </div>
          <Button variant="ghost" icon={<X />} onClick={clearFilters} disabled={!hasActiveFilters} className="ml-auto">
            Clear
          </Button>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Filter className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" />
          <QueueSelect label="Product" value={productFilter} options={productOptions} onChange={setProductFilter} />
          <QueueSelect label="Risk" value={riskFilter} options={riskOptions} onChange={setRiskFilter} />
          <QueueSelect label="Channel" value={channelFilter} options={channelOptions} onChange={setChannelFilter} />
          <div className="ml-auto text-[12px] text-dls-text-muted">Assignment and escalation execute through audited API actions.</div>
        </div>
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={6} /></GlassCard>
      ) : error ? (
        <GlassCard>
          <ErrorStateSurface message={`${error.message} ${error.recovery}`} correlationId={error.correlationId} onRetry={error.retryable ? () => void refresh() : undefined} />
          {!error.retryable && (
            <div className="px-5 pb-4">
              <Link to="/app" className="dls-focus-outline inline-flex items-center justify-center gap-1.5 rounded-dls-control border border-dls-border-input bg-dls-surface px-4 py-[9px] text-[13px] font-bold text-dls-text-primary hover:bg-dls-bg-page">
                Return to Command Center
              </Link>
            </div>
          )}
          <p className="px-5 pb-4 text-[11px] text-dls-text-muted">Attempted {new Date(error.occurredAt).toLocaleString()}</p>
        </GlassCard>
      ) : claims.length === 0 ? (
        <GlassCard>
          <div className="flex flex-col items-center gap-2 p-10 text-center">
            <ClipboardList className="h-6 w-6 text-dls-text-muted" aria-hidden="true" />
            <EmptyStateSurface message="Claims appear here once created from FNOL intake or channel conversations." />
          </div>
        </GlassCard>
      ) : rows.length === 0 ? (
        <GlassCard><EmptyFilteredStateSurface onClear={clearFilters} /></GlassCard>
      ) : (
        <GlassCard className="overflow-hidden !p-0">
          <div data-testid="claims-card-list" className="divide-y divide-dls-border-subtle md:hidden">
            {rows.map((claim) => (
              <article key={claim.id} data-testid="claim-card" className="space-y-3 p-4">
                <div className="flex min-w-0 items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h2 className="break-words text-[14px] font-bold text-dls-text-primary">{claim.id}</h2>
                    <p className="mt-0.5 break-words text-[12.5px] text-dls-text-secondary">{claim.customer} · {claim.policy}</p>
                  </div>
                  <StatusBadge status={claim.status} />
                </div>
                <div className="flex flex-wrap gap-2">{claim.requiresConfirmation ? <Badge tone="warning">Needs confirmation</Badge> : null}<SLABadge sla={claim.sla} /><RiskBadge risk={claim.risk} /></div>
                <dl className="grid gap-2 text-[12px] text-dls-text-secondary min-[360px]:grid-cols-2">
                  <div><dt className="text-dls-text-muted">Evidence</dt><dd>{claim.complete ? "Documents complete" : "Documents incomplete"}</dd></div>
                  <div><dt className="text-dls-text-muted">Owner</dt><dd className="break-words">{claim.assignedUserId ?? claim.assignedTeam ?? "Unassigned"}</dd></div>
                  <div><dt className="text-dls-text-muted">Product</dt><dd>{claim.productLine} · {claim.claimType}</dd></div>
                  <div><dt className="text-dls-text-muted">Updated</dt><dd>{claim.updated}</dd></div>
                </dl>
                <ClaimActions claim={claim} busy={busyCaseId === claim.id} onAssign={assignToMe} onEscalate={escalate} compact />
              </article>
            ))}
          </div>

          <div data-testid="claims-tablet-table" className="hidden md:block xl:hidden">
            <Table label="Claims priority queue with expandable details">
              <TableHead>
                <TableRow><TableHeaderCell>Claim & details</TableHeaderCell><TableHeaderCell>Status</TableHeaderCell><TableHeaderCell>SLA</TableHeaderCell><TableHeaderCell>Owner</TableHeaderCell><TableHeaderCell>Action</TableHeaderCell></TableRow>
              </TableHead>
              <TableBody>
                {rows.map((claim) => (
                  <TableRow key={claim.id}>
                    <TableCell>
                      <div className="flex items-center gap-2"><span className="font-semibold text-dls-text-primary">{claim.id}</span>{claim.requiresConfirmation ? <Badge tone="warning">Needs confirmation</Badge> : null}</div>
                      <div className="text-[11px] text-dls-text-muted">{claim.customer}</div>
                      <details className="mt-2">
                        <summary className="dls-focus-outline min-h-6 cursor-pointer text-[11.5px] font-semibold text-dls-primary">More details</summary>
                        <dl className="mt-2 grid grid-cols-2 gap-2 text-[11px] text-dls-text-secondary">
                          <div><dt className="text-dls-text-muted">Evidence</dt><dd>{claim.complete ? "Complete" : "Incomplete"}</dd></div>
                          <div><dt className="text-dls-text-muted">Risk</dt><dd>{claim.risk}</dd></div>
                          <div><dt className="text-dls-text-muted">Product</dt><dd>{claim.productLine} · {claim.claimType}</dd></div>
                          <div><dt className="text-dls-text-muted">Updated</dt><dd>{claim.updated}</dd></div>
                        </dl>
                      </details>
                    </TableCell>
                    <TableCell><StatusBadge status={claim.status} /></TableCell>
                    <TableCell><SLABadge sla={claim.sla} /></TableCell>
                    <TableCell className="max-w-40 break-words text-dls-text-secondary">{claim.assignedUserId ?? claim.assignedTeam ?? "Unassigned"}</TableCell>
                    <TableCell>
                      <Link to={`/app/claims/${claim.id}`} className="dls-focus-outline inline-flex min-h-11 items-center gap-1 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 font-semibold text-dls-primary hover:bg-dls-bg-page">Review <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" /></Link>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <div data-testid="claims-desktop-table" className="hidden xl:block">
            <Table label="Complete claims work queue">
              <TableHead>
                <TableRow>
                  {["Claim", "Customer", "Product", "Status", "Docs", "SLA", "Risk", "Confidence", "Assigned", "Updated", "Actions"].map(h => (
                    <TableHeaderCell key={h}>{h}</TableHeaderCell>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map(c => (
                  <TableRow key={c.id}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-dls-text-primary">{c.id}</span>
                        {c.requiresConfirmation ? <Badge tone="warning">Needs confirmation</Badge> : null}
                      </div>
                      <div className="text-[11px] text-dls-text-muted">{c.policy}</div>
                    </TableCell>
                    <TableCell>
                      <div className="text-dls-text-primary">{c.customer}</div>
                      <div className="text-[11px] text-dls-text-muted">{c.channel}</div>
                    </TableCell>
                    <TableCell className="text-dls-text-secondary">{c.productLine} · {c.claimType}</TableCell>
                    <TableCell><StatusBadge status={c.status} /></TableCell>
                    <TableCell>{c.complete ? <Badge tone="success">Complete</Badge> : <Badge tone="warning">Incomplete</Badge>}</TableCell>
                    <TableCell><SLABadge sla={c.sla} /></TableCell>
                    <TableCell><RiskBadge risk={c.risk} /></TableCell>
                    <TableCell><ConfidenceBadge value={c.confidence} /></TableCell>
                    <TableCell className="whitespace-nowrap text-dls-text-secondary">
                      {c.assignedUserId ? c.assignedUserId : c.assignedTeam ?? "Unassigned"}
                    </TableCell>
                    <TableCell className="whitespace-nowrap text-dls-text-muted">{c.updated}</TableCell>
                    <TableCell>
                      <ClaimActions claim={c} busy={busyCaseId === c.id} onAssign={assignToMe} onEscalate={escalate} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
          <div className="flex items-center justify-between border-t border-dls-border-subtle px-4 py-3 text-[12px] text-dls-text-muted">
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
    <div className="inline-flex min-h-11 items-center gap-2 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2">
      <label htmlFor={id} className="text-[12px] text-dls-text-muted">{label}</label>
      <Select
        id={id}
        aria-label={`Filter by ${label.toLowerCase()}`}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="border-0 bg-transparent p-0 text-[12px] text-dls-text-primary focus:ring-0"
      >
        <option value="All">All</option>
        {options.map((option) => (
          <option key={option} value={option}>{option}</option>
        ))}
      </Select>
    </div>
  );
}

function ClaimActions({ claim, busy, onAssign, onEscalate, compact = false }: { claim: ClaimRowView; busy: boolean; onAssign: (id: string) => Promise<void>; onEscalate: (id: string) => Promise<void>; compact?: boolean }) {
  return (
    <div className={`flex flex-wrap items-center gap-2 ${compact ? "border-t border-dls-border-subtle pt-3" : ""}`}>
      <Link to={`/app/claims/${claim.id}`} className="dls-focus-outline inline-flex min-h-11 items-center gap-1 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[12px] font-semibold text-dls-primary hover:bg-dls-bg-page">Review <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" /></Link>
      <button type="button" onClick={() => void onAssign(claim.id)} disabled={busy} className="dls-focus-outline inline-flex min-h-11 items-center gap-1 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[12px] font-semibold text-dls-text-primary hover:bg-dls-bg-page disabled:opacity-50">
        <UserCheck className="h-3.5 w-3.5" aria-hidden="true" />Assign
      </button>
      <button type="button" onClick={() => void onEscalate(claim.id)} disabled={busy || claim.status === "Escalated"} className="dls-focus-outline inline-flex min-h-11 items-center gap-1 rounded-dls-control border border-dls-warning-dot bg-dls-surface px-3 py-2 text-[12px] font-semibold text-dls-warning-text disabled:opacity-50">
        <TriangleAlert className="h-3.5 w-3.5" aria-hidden="true" />Escalate
      </button>
    </div>
  );
}
