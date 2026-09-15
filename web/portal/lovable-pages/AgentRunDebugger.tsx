"use client";

import { businessReference, humanizeIdentifier } from "@/lib/format";
import Link from "next/link";
import { useParams } from "next/navigation";
import { Activity, Clock3, Database, RefreshCw, ShieldCheck, TerminalSquare } from "lucide-react";
import { Badge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import WorkflowCanvas from "@/components/iip/WorkflowCanvas";
import { useAgentWorkflowRun } from "@/lib/agentWorkflows";

export default function AgentRunDebugger() {
  const params = useParams<{ id: string }>();
  const { run, definition, loading, error, refresh } = useAgentWorkflowRun(params.id);

  if (loading) return <GlassCard><LoadingSkeleton rows={5} /></GlassCard>;
  if (!run || !definition) return <GlassCard><ErrorStateSurface title="Run unavailable" message={error ?? "The run or its immutable workflow definition was not found."} /></GlassCard>;

  return (
    <>
      <SectionHeader
        eyebrow="Agent Operations · Run Debugger"
        title={definition.name}
        sub={`${humanizeIdentifier(run.businessReferenceType)} · ${businessReference(run.businessReferenceType, run.businessReferenceId)}`}
        action={
          <Button variant="secondary" onClick={() => void refresh()} icon={<RefreshCw aria-hidden="true" />}>
            Refresh state
          </Button>
        }
      />

      <div className="mb-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <GlassCard className="!p-4">
          <div className="flex items-center gap-2 text-[10.5px] font-bold uppercase tracking-[0.12em] text-dls-text-muted"><Activity className="h-4 w-4 text-dls-info-text" aria-hidden="true" /> State</div>
          <div className="mt-3"><StatusBadge status={run.status} /></div>
        </GlassCard>
        <GlassCard className="!p-4">
          <div className="flex items-center gap-2 text-[10.5px] font-bold uppercase tracking-[0.12em] text-dls-text-muted"><Clock3 className="h-4 w-4 text-dls-info-text" aria-hidden="true" /> Updated</div>
          <div className="mt-3 text-[12.5px] font-semibold text-dls-text-primary">{new Date(run.updatedAt).toLocaleString()}</div>
        </GlassCard>
        <GlassCard className="!p-4">
          <div className="flex items-center gap-2 text-[10.5px] font-bold uppercase tracking-[0.12em] text-dls-text-muted"><TerminalSquare className="h-4 w-4 text-dls-text-secondary" aria-hidden="true" /> Support reference</div>
          <div className="mt-3 break-all text-[10.5px] text-dls-text-secondary">{run.correlationId}</div>
          <div className="mt-1 text-[11px] text-dls-text-muted">Quote this if you contact support about this run.</div>
        </GlassCard>
        <GlassCard className="!p-4">
          <div className="flex items-center gap-2 text-[10.5px] font-bold uppercase tracking-[0.12em] text-dls-text-muted"><ShieldCheck className="h-4 w-4 text-dls-success-text" aria-hidden="true" /> Authority</div>
          <div className="mt-3 text-[12px] font-semibold text-dls-text-primary">Preparation only · human reviewed</div>
        </GlassCard>
      </div>

      <WorkflowCanvas definition={definition} nodeRuns={run.nodeRuns} className="min-h-[500px]" />

      <div className="mt-4 grid gap-4 xl:grid-cols-[minmax(0,1fr)_340px]">
        <GlassCard className="!p-0 overflow-hidden">
          <div className="border-b border-dls-border-subtle px-4 py-3 text-[10.5px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Node execution trace</div>
          <div className="divide-y divide-dls-border-subtle">
            {definition.nodes.map((node) => {
              const trace = run.nodeRuns.find((candidate) => candidate.nodeKey === node.key);
              return (
                <div key={node.key} className="grid gap-3 p-4 md:grid-cols-[190px_minmax(0,1fr)_170px]">
                  <div>
                    <div className="text-[12.5px] font-semibold text-dls-text-primary">{node.name}</div>
                    <div className="mt-1 font-mono text-[10px] text-dls-text-muted">{node.key}</div>
                  </div>
                  <div>
                    <div className="text-[11.5px] leading-5 text-dls-text-secondary">{trace?.outputSummary ?? "No bounded output summary recorded."}</div>
                    {trace?.errorCode ? <div className="mt-1 text-[11px] text-dls-danger-text">{trace.errorCode}</div> : null}
                  </div>
                  <div className="space-y-1.5 text-right">
                    <StatusBadge status={trace?.status ?? "Pending"} />
                    <div className="text-[10.5px] text-dls-text-muted">Attempt {trace?.attemptCount ?? 0}</div>
                    <div className="text-[10.5px] text-dls-text-muted">{trace?.provider ?? "Provider not recorded"}</div>
                  </div>
                </div>
              );
            })}
          </div>
        </GlassCard>

        <div className="space-y-4">
          <GlassCard className="!p-4">
            <div className="flex items-center gap-2 text-[10.5px] font-bold uppercase tracking-[0.14em] text-dls-text-muted"><Database className="h-4 w-4 text-dls-info-text" aria-hidden="true" /> Evidence ledger</div>
            <div className="mt-3 space-y-2">
              {run.nodeRuns.flatMap((node) => node.evidence.map((evidence) => ({ node: node.nodeKey, evidence }))).map(({ node, evidence }, index) => (
                <div key={`${node}-${evidence.sourceId}-${index}`} className="rounded-dls-control border border-dls-border-subtle bg-dls-surface p-3">
                  <div className="text-[11.5px] font-semibold text-dls-text-primary">{evidence.displayLabel ?? evidence.sourceId}</div>
                  <div className="mt-1 break-all font-mono text-[9.5px] text-dls-text-muted">{node} · {evidence.sourceType} · {evidence.sourceId}</div>
                  {typeof evidence.confidence === "number" ? (
                    <div className="mt-2"><Badge tone={evidence.confidence >= 0.85 ? "success" : "warning"}>{Math.round(evidence.confidence * 100)}% confidence</Badge></div>
                  ) : null}
                </div>
              ))}
              {run.nodeRuns.every((node) => node.evidence.length === 0) ? (
                <div className="rounded-dls-control border border-dashed border-dls-border-subtle p-5 text-center text-[11.5px] text-dls-text-muted">No evidence references have been recorded yet.</div>
              ) : null}
            </div>
          </GlassCard>
          <GlassCard className="!p-4">
            <div className="text-[10.5px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Safe observability</div>
            <div className="mt-3 space-y-2 text-[11.5px] text-dls-text-secondary">
              <div className="flex justify-between"><span>Workflow version</span><span className="font-semibold text-dls-text-primary">v{run.workflowVersion}</span></div>
              <div className="flex justify-between"><span>Raw prompts</span><Badge tone="success">Not exposed</Badge></div>
              <div className="flex justify-between"><span>Raw OCR/body text</span><Badge tone="success">Not persisted</Badge></div>
              <div className="flex justify-between"><span>Failure</span><span className="font-semibold text-dls-text-primary">{run.failureCode ?? "None"}</span></div>
            </div>
          </GlassCard>
          <Link
            href="/app/work-items"
            className="dls-focus-outline block rounded-dls-control bg-dls-primary px-4 py-[9px] text-center text-[13px] font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover"
          >
            Open human review queue
          </Link>
        </div>
      </div>
    </>
  );
}
