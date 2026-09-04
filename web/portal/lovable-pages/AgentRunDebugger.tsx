"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { Activity, Clock3, Database, Loader2, RefreshCw, ShieldCheck, TerminalSquare } from "lucide-react";
import { Badge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import WorkflowCanvas from "@/components/iip/WorkflowCanvas";
import { useAgentWorkflowRun } from "@/lib/agentWorkflows";

export default function AgentRunDebugger() {
  const params = useParams<{ id: string }>();
  const { run, definition, loading, error, refresh } = useAgentWorkflowRun(params.id);

  if (loading) return <GlassCard><div className="flex items-center justify-center gap-2 py-16 text-sm text-muted"><Loader2 className="h-4 w-4 animate-spin" /> Loading durable run state…</div></GlassCard>;
  if (!run || !definition) return <GlassCard><div className="py-16 text-center"><div className="font-medium text-ink">Run unavailable</div><div className="mt-1 text-[12.5px] text-muted">{error ?? "The run or its immutable workflow definition was not found."}</div></div></GlassCard>;

  return (
    <>
      <SectionHeader
        eyebrow="Agent Operations · Run Debugger"
        title={definition.name}
        sub={`${run.businessReferenceType} · ${run.businessReferenceId}`}
        action={<button type="button" onClick={() => void refresh()} className="inline-flex items-center gap-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring"><RefreshCw className="h-3.5 w-3.5" /> Refresh state</button>}
      />

      <div className="mb-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <GlassCard className="!p-4"><div className="flex items-center gap-2 text-[10.5px] uppercase tracking-[0.12em] text-muted"><Activity className="h-4 w-4 text-info" /> State</div><div className="mt-3"><StatusBadge status={run.status} /></div></GlassCard>
        <GlassCard className="!p-4"><div className="flex items-center gap-2 text-[10.5px] uppercase tracking-[0.12em] text-muted"><Clock3 className="h-4 w-4 text-info" /> Updated</div><div className="mt-3 text-[12.5px] font-medium text-ink">{new Date(run.updatedAt).toLocaleString()}</div></GlassCard>
        <GlassCard className="!p-4"><div className="flex items-center gap-2 text-[10.5px] uppercase tracking-[0.12em] text-muted"><TerminalSquare className="h-4 w-4 text-violet" /> Correlation</div><div className="mt-3 truncate font-mono text-[10.5px] text-ink" title={run.correlationId}>{run.correlationId}</div></GlassCard>
        <GlassCard className="!p-4"><div className="flex items-center gap-2 text-[10.5px] uppercase tracking-[0.12em] text-muted"><ShieldCheck className="h-4 w-4 text-success" /> Authority</div><div className="mt-3 text-[12px] font-medium text-ink">Preparation only · human reviewed</div></GlassCard>
      </div>

      <WorkflowCanvas definition={definition} nodeRuns={run.nodeRuns} className="min-h-[500px]" />

      <div className="mt-4 grid gap-4 xl:grid-cols-[minmax(0,1fr)_340px]">
        <GlassCard className="!p-0 overflow-hidden">
          <div className="border-b border-line px-4 py-3 text-[10.5px] uppercase tracking-[0.14em] text-muted">Node execution trace</div>
          <div className="divide-y divide-line/70">
            {definition.nodes.map((node) => {
              const trace = run.nodeRuns.find((candidate) => candidate.nodeKey === node.key);
              return (
                <div key={node.key} className="grid gap-3 p-4 md:grid-cols-[190px_minmax(0,1fr)_170px]">
                  <div><div className="text-[12.5px] font-medium text-ink">{node.name}</div><div className="mt-1 font-mono text-[10px] text-muted">{node.key}</div></div>
                  <div><div className="text-[11.5px] leading-5 text-ink-soft">{trace?.outputSummary ?? "No bounded output summary recorded."}</div>{trace?.errorCode ? <div className="mt-1 text-[11px] text-danger">{trace.errorCode}</div> : null}</div>
                  <div className="space-y-1.5 text-right"><StatusBadge status={trace?.status ?? "Pending"} /><div className="text-[10.5px] text-muted">Attempt {trace?.attemptCount ?? 0}</div><div className="text-[10.5px] text-muted">{trace?.provider ?? "Provider not recorded"}</div></div>
                </div>
              );
            })}
          </div>
        </GlassCard>

        <div className="space-y-4">
          <GlassCard className="!p-4">
            <div className="flex items-center gap-2 text-[10.5px] uppercase tracking-[0.14em] text-muted"><Database className="h-4 w-4 text-info" /> Evidence ledger</div>
            <div className="mt-3 space-y-2">
              {run.nodeRuns.flatMap((node) => node.evidence.map((evidence) => ({ node: node.nodeKey, evidence }))).map(({ node, evidence }, index) => (
                <div key={`${node}-${evidence.sourceId}-${index}`} className="rounded-xl border border-line bg-white/65 p-3"><div className="text-[11.5px] font-medium text-ink">{evidence.displayLabel ?? evidence.sourceId}</div><div className="mt-1 break-all font-mono text-[9.5px] text-muted">{node} · {evidence.sourceType} · {evidence.sourceId}</div>{typeof evidence.confidence === "number" ? <div className="mt-2"><Badge tone={evidence.confidence >= 0.85 ? "success" : "warning"}>{Math.round(evidence.confidence * 100)}% confidence</Badge></div> : null}</div>
              ))}
              {run.nodeRuns.every((node) => node.evidence.length === 0) ? <div className="rounded-xl border border-dashed border-line p-5 text-center text-[11.5px] text-muted">No evidence references have been recorded yet.</div> : null}
            </div>
          </GlassCard>
          <GlassCard className="!p-4">
            <div className="text-[10.5px] uppercase tracking-[0.14em] text-muted">Safe observability</div>
            <div className="mt-3 space-y-2 text-[11.5px] text-ink-soft"><div className="flex justify-between"><span>Workflow version</span><span className="font-medium text-ink">v{run.workflowVersion}</span></div><div className="flex justify-between"><span>Raw prompts</span><Badge tone="success">Not exposed</Badge></div><div className="flex justify-between"><span>Raw OCR/body text</span><Badge tone="success">Not persisted</Badge></div><div className="flex justify-between"><span>Failure</span><span className="font-medium text-ink">{run.failureCode ?? "None"}</span></div></div>
          </GlassCard>
          <Link href="/app/work-items" className="block rounded-2xl gradient-primary px-4 py-3 text-center text-[12px] font-medium text-white shadow-l2 focus-ring">Open human review queue</Link>
        </div>
      </div>
    </>
  );
}

