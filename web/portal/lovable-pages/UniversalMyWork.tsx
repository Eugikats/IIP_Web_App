"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, ArrowRight, ClipboardCheck, Clock3, FileCheck2, Inbox, Loader2, RefreshCw, Users } from "lucide-react";
import { Badge, GlassCard, StatusBadge } from "@/components/iip/Primitives";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { useMyWork, type MyWorkItem, type MyWorkOwnership, type MyWorkSourceType, type MyWorkState } from "@/lib/lovable-parity/data/myWork";

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

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Summary label="Visible work" value={error ? "—" : summary?.total ?? "—"} icon={<Inbox className="h-4 w-4" />} />
        <Summary label="Overdue" value={error ? "—" : summary?.overdue ?? "—"} icon={<Clock3 className="h-4 w-4" />} tone="warning" />
        <Summary label="Unassigned" value={error ? "—" : summary?.unassigned ?? "—"} icon={<Users className="h-4 w-4" />} />
        <Summary label="Needs approval" value={error ? "—" : summary?.approvalRequired ?? "—"} icon={<ClipboardCheck className="h-4 w-4" />} tone="violet" />
      </div>

      <GlassCard className="!p-4">
        <div className="flex flex-wrap items-end gap-3">
          <label className="text-[11px] font-medium text-ink">Ownership
            <select value={ownership} onChange={(event) => setOwnership(event.target.value as OwnershipFilter)} className="mt-1 block rounded-xl border border-line bg-white px-3 py-2 text-[12px] focus-ring">
              <option value="mine">Mine</option>
              {canViewAll ? <option value="unassigned">Unassigned</option> : null}
              {canViewAll ? <option value="all">All visible work</option> : null}
            </select>
          </label>
          <label className="text-[11px] font-medium text-ink">Work type
            <select value={sourceType ?? ""} onChange={(event) => setSourceType((event.target.value || undefined) as MyWorkSourceType | undefined)} className="mt-1 block rounded-xl border border-line bg-white px-3 py-2 text-[12px] focus-ring">
              <option value="">All types</option>
              {Object.entries(SOURCE_LABELS).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </select>
          </label>
          <label className="text-[11px] font-medium text-ink">State
            <select value={state ?? ""} onChange={(event) => setState((event.target.value || undefined) as MyWorkState | undefined)} className="mt-1 block rounded-xl border border-line bg-white px-3 py-2 text-[12px] focus-ring">
              <option value="">All states</option>
              <option value="Ready">Ready</option><option value="InProgress">In progress</option><option value="Escalated">Escalated</option><option value="Completed">Completed</option><option value="Closed">Closed</option>
            </select>
          </label>
          <button type="button" onClick={() => void refresh()} className="ml-auto inline-flex items-center gap-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring"><RefreshCw className="h-3.5 w-3.5" /> Refresh</button>
        </div>
      </GlassCard>

      {loading ? <GlassCard><div className="flex items-center justify-center gap-2 py-16 text-sm text-muted"><Loader2 className="h-4 w-4 animate-spin" /> Loading current work…</div></GlassCard>
      : error ? <GlassCard><div className="py-14 text-center"><AlertTriangle className="mx-auto h-6 w-6 text-warning" /><div className="mt-3 font-medium text-ink">My Work could not be loaded</div><div className="mt-1 text-[12px] text-muted">{error.message}</div><div className="mt-1 text-[12px] text-muted">{error.recovery}</div>{error.correlationId ? <div className="mt-2 font-mono text-[11px] text-muted">Reference {error.correlationId}</div> : null}<div className="mt-1 text-[11px] text-muted">Attempted {new Date(error.occurredAt).toLocaleString()}</div>{error.retryable ? <button type="button" onClick={() => void refresh()} className="mt-4 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring">Try again</button> : <Link href="/app" className="mt-4 inline-flex rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring">Return to Command Center</Link>}</div></GlassCard>
      : items.length === 0 ? <GlassCard><div className="py-16 text-center"><FileCheck2 className="mx-auto h-7 w-7 text-success" /><div className="mt-3 font-medium text-ink">No matching work</div><div className="mt-1 text-[12px] text-muted">Nothing authorized matches these ownership, type, and state filters.</div></div></GlassCard>
      : <div className="grid min-h-[560px] gap-4 xl:grid-cols-[390px_minmax(0,1fr)]">
          <GlassCard className="!p-2 overflow-hidden">
            <div className="px-3 py-2 text-[10.5px] uppercase tracking-[0.14em] text-muted">Current projection · {items.length}{response?.hasMore ? "+" : ""}</div>
            <div className="max-h-[620px] space-y-1 overflow-y-auto">
              {items.map((item) => <WorkRow key={item.id} item={item} selected={item.id === selectedId} onSelect={() => setSelectedId(item.id)} />)}
            </div>
          </GlassCard>
          {selected ? <WorkDetail item={selected} onOpenSpecialist={onOpenSpecialist} /> : null}
        </div>}
    </div>
  );
}

function Summary({ label, value, icon, tone = "neutral" }: { label: string; value: number | string; icon: React.ReactNode; tone?: "neutral" | "warning" | "violet" }) {
  const color = tone === "warning" ? "text-warning-text bg-warning/10" : tone === "violet" ? "text-violet bg-violet/10" : "text-info bg-info/10";
  return <GlassCard className="!p-4"><div className="flex items-center justify-between"><div><div className="text-[10.5px] uppercase tracking-[0.12em] text-muted">{label}</div><div className="mt-1 text-2xl font-semibold text-ink">{value}</div></div><span className={`grid h-9 w-9 place-items-center rounded-xl ${color}`}>{icon}</span></div></GlassCard>;
}

function WorkRow({ item, selected, onSelect }: { item: MyWorkItem; selected: boolean; onSelect: () => void }) {
  return <button type="button" onClick={onSelect} className={`w-full rounded-2xl border p-3 text-left focus-ring ${selected ? "border-violet/30 bg-violet/10" : "border-transparent hover:border-line hover:bg-white/70"}`}>
    <div className="flex items-start justify-between gap-2"><span className="text-[12.5px] font-semibold leading-4 text-ink">{item.title}</span><StatusBadge status={item.sourceStatus} /></div>
    <div className="mt-2 flex flex-wrap gap-1.5"><Badge tone="neutral">{SOURCE_LABELS[item.sourceType]}</Badge>{item.dueState === "Overdue" ? <Badge tone="warning">Overdue</Badge> : null}{item.evidenceReadiness === "Gaps" ? <Badge tone="warning">Evidence gaps</Badge> : null}</div>
    <div className="mt-2 text-[10.5px] text-muted">{item.ownerUserId ?? "Unassigned"} · {new Date(item.updatedAt).toLocaleString()}</div>
  </button>;
}

function WorkDetail({ item, onOpenSpecialist }: { item: MyWorkItem; onOpenSpecialist: (view: "prepared" | "approvals") => void }) {
  const specialist = item.sourceType === "Approval" ? "approvals" : item.sourceType === "AgentWorkItem" ? "prepared" : null;
  return <GlassCard className="!p-5">
    <div className="flex flex-wrap items-start justify-between gap-4"><div><div className="flex flex-wrap gap-2"><Badge tone="violet">{SOURCE_LABELS[item.sourceType]}</Badge><StatusBadge status={item.sourceStatus} /></div><h2 className="mt-3 text-xl font-semibold text-ink">{item.title}</h2><div className="mt-1 text-[11px] text-muted">Source {item.sourceId}{item.caseId ? ` · Case ${item.caseId}` : ""}</div></div>{item.approvalRequired ? <Badge tone="warning">Human approval required</Badge> : null}</div>
    <div className="mt-5 rounded-2xl border border-violet/20 bg-violet/5 p-4"><div className="text-[10.5px] uppercase tracking-[0.14em] text-violet">Required action</div><p className="mt-2 text-[13px] text-ink">{item.requiredAction}</p></div>
    <dl className="mt-4 grid gap-3 sm:grid-cols-2">
      <Fact label="Owner" value={item.ownerUserId ?? "Unassigned"} /><Fact label="Owner basis" value={item.ownerBasis} />
      <Fact label="Priority" value={item.priority} /><Fact label="Due state" value={item.dueState} />
      <Fact label="Evidence readiness" value={item.evidenceReadiness} /><Fact label="Team" value={item.ownerTeam ?? "No team assignment"} />
    </dl>
    <div className="mt-5 flex justify-end">{specialist
      ? <button type="button" onClick={() => onOpenSpecialist(specialist)} className="inline-flex items-center gap-2 rounded-xl gradient-primary px-4 py-2.5 text-[11.5px] font-medium text-white focus-ring">Open source workspace <ArrowRight className="h-4 w-4" /></button>
      : <Link href={item.href} className="inline-flex items-center gap-2 rounded-xl gradient-primary px-4 py-2.5 text-[11.5px] font-medium text-white focus-ring">Open source work <ArrowRight className="h-4 w-4" /></Link>}
    </div>
  </GlassCard>;
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div className="rounded-2xl border border-line bg-white/60 p-4"><dt className="text-[10.5px] uppercase tracking-[0.12em] text-muted">{label}</dt><dd className="mt-2 text-[12.5px] font-medium text-ink">{value}</dd></div>;
}
