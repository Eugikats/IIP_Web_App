"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { ArrowRight, ClipboardCheck, Clock3, Inbox, RefreshCw, Users } from "lucide-react";
import { Badge, GlassCard, StatusBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Field, Select } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { useMyWork, type MyWorkItem, type MyWorkSourceType, type MyWorkState } from "@/lib/lovable-parity/data/myWork";

type OwnershipFilter = "mine" | "unassigned" | "all";

const SOURCE_LABELS: Record<MyWorkSourceType, string> = {
  WorkflowTask: "Workflow task",
  Approval: "Approval",
  DocumentReview: "Document review",
  CommunicationReview: "Communication review",
  AgentWorkItem: "Prepared work",
};

export default function UniversalMyWork({ onOpenSpecialist }: { onOpenSpecialist: (view: "prepared" | "approvals") => void }) {
  const { runtimeContext } = useRuntimeContext();
  const canViewAll = (runtimeContext?.permissions ?? []).includes("ViewAllClaims");
  const [ownership, setOwnership] = useState<OwnershipFilter>("mine");
  const [sourceType, setSourceType] = useState<MyWorkSourceType | undefined>();
  const [state, setState] = useState<MyWorkState | undefined>();
  const { response, loadState, refresh } = useMyWork({ ownership, sourceType, state });
  const loading = loadState.status === "loading";
  const error = "error" in loadState ? loadState.error : null;
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const items = useMemo(() => response?.items ?? [], [response]);

  useEffect(() => {
    if (!selectedId || !items.some((item) => item.id === selectedId)) setSelectedId(items[0]?.id ?? null);
  }, [items, selectedId]);

  useEffect(() => {
    if (!canViewAll && ownership !== "mine") setOwnership("mine");
  }, [canViewAll, ownership]);

  const selected = items.find((item) => item.id === selectedId) ?? null;
  const summary = response?.summary;
  const hasFilters = ownership !== "mine" || Boolean(sourceType) || Boolean(state);

  function clearFilters() {
    setOwnership("mine");
    setSourceType(undefined);
    setState(undefined);
  }

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Summary label="Visible work" value={error ? "—" : summary?.total ?? "—"} icon={<Inbox className="h-4 w-4" aria-hidden="true" />} />
        <Summary label="Overdue" value={error ? "—" : summary?.overdue ?? "—"} icon={<Clock3 className="h-4 w-4" aria-hidden="true" />} tone="warning" />
        <Summary label="Unassigned" value={error ? "—" : summary?.unassigned ?? "—"} icon={<Users className="h-4 w-4" aria-hidden="true" />} />
        <Summary label="Needs approval" value={error ? "—" : summary?.approvalRequired ?? "—"} icon={<ClipboardCheck className="h-4 w-4" aria-hidden="true" />} tone="warning" />
      </div>

      <GlassCard className="!p-4">
        <div className="flex flex-wrap items-end gap-3">
          <Field label="Ownership" htmlFor="filter-ownership">
            <Select id="filter-ownership" value={ownership} onChange={(event) => setOwnership(event.target.value as OwnershipFilter)}>
              <option value="mine">Mine</option>
              {canViewAll ? <option value="unassigned">Unassigned</option> : null}
              {canViewAll ? <option value="all">All visible work</option> : null}
            </Select>
          </Field>
          <Field label="Work type" htmlFor="filter-work-type">
            <Select id="filter-work-type" value={sourceType ?? ""} onChange={(event) => setSourceType((event.target.value || undefined) as MyWorkSourceType | undefined)}>
              <option value="">All types</option>
              {Object.entries(SOURCE_LABELS).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </Select>
          </Field>
          <Field label="State" htmlFor="filter-state">
            <Select id="filter-state" value={state ?? ""} onChange={(event) => setState((event.target.value || undefined) as MyWorkState | undefined)}>
              <option value="">All states</option>
              <option value="Ready">Ready</option><option value="InProgress">In progress</option><option value="Escalated">Escalated</option><option value="Completed">Completed</option><option value="Closed">Closed</option>
            </Select>
          </Field>
          <Button variant="secondary" icon={<RefreshCw />} onClick={() => void refresh()} className="ml-auto">
            Refresh
          </Button>
        </div>
      </GlassCard>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : error ? (
        <GlassCard>
          <ErrorStateSurface title="My Work could not be loaded" message={`${error.message} ${error.recovery}`} correlationId={error.correlationId} onRetry={error.retryable ? () => void refresh() : undefined} />
          {!error.retryable && (
            <div className="px-5 pb-4">
              <Link href="/app" className="dls-focus-outline inline-flex items-center justify-center gap-1.5 rounded-dls-control border border-dls-border-input bg-dls-surface px-4 py-[9px] text-[13px] font-bold text-dls-text-primary hover:bg-dls-bg-page">
                Return to Command Center
              </Link>
            </div>
          )}
          <p className="px-5 pb-4 text-[11px] text-dls-text-muted">Attempted {new Date(error.occurredAt).toLocaleString()}</p>
        </GlassCard>
      ) : items.length === 0 ? (
        <GlassCard>
          {hasFilters ? (
            <div className="flex flex-col items-center gap-3 px-5 py-10 text-center">
              <p className="text-[13.5px] text-dls-text-secondary">No items match these filters.</p>
              <Button variant="ghost" onClick={clearFilters}>Clear filters</Button>
            </div>
          ) : (
            <EmptyStateSurface message="Nothing authorized matches your ownership, type, and state filters right now." />
          )}
        </GlassCard>
      ) : (
        <div className="grid min-h-[560px] gap-4 xl:grid-cols-[390px_minmax(0,1fr)]">
          <GlassCard className="overflow-hidden !p-2">
            <div className="px-3 py-2 text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Current projection · {items.length}{response?.hasMore ? "+" : ""}</div>
            <div className="max-h-[620px] space-y-1 overflow-y-auto">
              {items.map((item) => <WorkRow key={item.id} item={item} selected={item.id === selectedId} onSelect={() => setSelectedId(item.id)} />)}
            </div>
          </GlassCard>
          {selected ? <WorkDetail item={selected} onOpenSpecialist={onOpenSpecialist} /> : null}
        </div>
      )}
    </div>
  );
}

function Summary({ label, value, icon, tone = "neutral" }: { label: string; value: number | string; icon: React.ReactNode; tone?: "neutral" | "warning" }) {
  const color = tone === "warning" ? "text-dls-warning-text bg-dls-warning-tint" : "text-dls-info-text bg-dls-info-tint";
  return (
    <GlassCard className="!p-4">
      <div className="flex items-center justify-between">
        <div>
          <div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{label}</div>
          <div className="mt-1 text-2xl font-bold tabular-nums text-dls-text-primary">{value}</div>
        </div>
        <span className={`grid h-9 w-9 place-items-center rounded-dls-control ${color}`}>{icon}</span>
      </div>
    </GlassCard>
  );
}

function WorkRow({ item, selected, onSelect }: { item: MyWorkItem; selected: boolean; onSelect: () => void }) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={`dls-focus-outline w-full rounded-dls-card border p-3 text-left transition-colors duration-dls-fast ease-dls ${selected ? "border-dls-primary-border bg-dls-primary-soft-alt" : "border-transparent hover:border-dls-border-subtle hover:bg-dls-bg-page"}`}
    >
      <div className="flex items-start justify-between gap-2">
        <span className="text-[12.5px] font-bold leading-4 text-dls-text-primary">{item.title}</span>
        <StatusBadge status={item.sourceStatus} />
      </div>
      <div className="mt-2 flex flex-wrap gap-1.5">
        <Badge tone="neutral">{SOURCE_LABELS[item.sourceType]}</Badge>
        {item.dueState === "Overdue" ? <Badge tone="warning">Overdue</Badge> : null}
        {item.evidenceReadiness === "Gaps" ? <Badge tone="warning">Evidence gaps</Badge> : null}
      </div>
      <div className="mt-2 text-[10.5px] text-dls-text-muted">{item.ownerUserId ?? "Unassigned"} · {new Date(item.updatedAt).toLocaleString()}</div>
    </button>
  );
}

function WorkDetail({ item, onOpenSpecialist }: { item: MyWorkItem; onOpenSpecialist: (view: "prepared" | "approvals") => void }) {
  const specialist = item.sourceType === "Approval" ? "approvals" : item.sourceType === "AgentWorkItem" ? "prepared" : null;
  return (
    <GlassCard className="!p-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex flex-wrap gap-2"><Badge tone="neutral">{SOURCE_LABELS[item.sourceType]}</Badge><StatusBadge status={item.sourceStatus} /></div>
          <h2 className="mt-3 text-xl font-bold text-dls-text-primary">{item.title}</h2>
          <div className="mt-1 text-[11px] text-dls-text-muted">Source {item.sourceId}{item.caseId ? ` · Case ${item.caseId}` : ""}</div>
        </div>
        {item.approvalRequired ? <Badge tone="warning">Human approval required</Badge> : null}
      </div>
      <div className="mt-5 rounded-dls-card border border-dls-primary-border bg-dls-primary-soft-alt p-4">
        <div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-primary">Required action</div>
        <p className="mt-2 text-[13px] text-dls-text-primary">{item.requiredAction}</p>
      </div>
      <dl className="mt-4 grid gap-3 sm:grid-cols-2">
        <Fact label="Owner" value={item.ownerUserId ?? "Unassigned"} /><Fact label="Owner basis" value={item.ownerBasis} />
        <Fact label="Priority" value={item.priority} /><Fact label="Due state" value={item.dueState} />
        <Fact label="Evidence readiness" value={item.evidenceReadiness} /><Fact label="Team" value={item.ownerTeam ?? "No team assignment"} />
      </dl>
      <div className="mt-5 flex justify-end">
        {specialist ? (
          <Button variant="primary" icon={<ArrowRight />} onClick={() => onOpenSpecialist(specialist)}>Open source workspace</Button>
        ) : (
          <Link href={item.href} className="dls-focus-outline inline-flex items-center justify-center gap-1.5 rounded-dls-control bg-dls-primary px-4 py-[9px] text-[13px] font-bold text-dls-text-inverse shadow-dls-primary hover:bg-dls-primary-hover">
            Open source work <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" />
          </Link>
        )}
      </div>
    </GlassCard>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
      <dt className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{label}</dt>
      <dd className="mt-2 text-[12.5px] font-semibold text-dls-text-primary">{value}</dd>
    </div>
  );
}
