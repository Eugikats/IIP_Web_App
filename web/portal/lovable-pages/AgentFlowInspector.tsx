"use client";

import { useParams } from "next/navigation";
import { Loader2, LockKeyhole, ShieldCheck } from "lucide-react";
import { Badge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import WorkflowCanvas from "@/components/iip/WorkflowCanvas";
import { useAgentWorkflows } from "@/lib/agentWorkflows";

export default function AgentFlowInspector() {
  const params = useParams<{ key: string }>();
  const { definitions, runs, loading, error } = useAgentWorkflows();
  const definition = definitions.find((item) => item.key === decodeURIComponent(params.key));
  if (loading) return <GlassCard><div className="flex items-center justify-center gap-2 py-16 text-sm text-muted"><Loader2 className="h-4 w-4 animate-spin" /> Loading immutable definition…</div></GlassCard>;
  if (!definition) return <GlassCard><div className="py-16 text-center"><div className="font-medium text-ink">Workflow definition unavailable</div><div className="mt-1 text-[12.5px] text-muted">{error ?? "The requested workflow is not available in this workspace."}</div></div></GlassCard>;
  return (
    <>
      <SectionHeader eyebrow="Automation Studio · Definition Inspector" title={definition.name} sub={definition.description} />
      <div className="mb-4 flex flex-wrap items-center gap-2"><StatusBadge status={definition.status} /><Badge tone={definition.isSystemDefinition ? "violet" : "info"}>{definition.isSystemDefinition ? "Platform governed" : "Tenant governed"}</Badge><Badge tone="neutral">Version {definition.version}</Badge><Badge tone="neutral">{runs.filter((run) => run.workflowKey === definition.key).length} recorded runs</Badge></div>
      <WorkflowCanvas definition={definition} className="min-h-[520px]" />
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <GlassCard className="!p-4"><div className="flex items-center gap-2 text-[10.5px] uppercase tracking-[0.14em] text-muted"><LockKeyhole className="h-4 w-4 text-violet" /> Version boundary</div><p className="mt-3 text-[12.5px] leading-5 text-ink-soft">Published versions are immutable. Changes require a new tenant workflow key or a governed platform release, preserving reproducible run history.</p></GlassCard>
        <GlassCard className="!p-4"><div className="flex items-center gap-2 text-[10.5px] uppercase tracking-[0.14em] text-muted"><ShieldCheck className="h-4 w-4 text-success" /> Side-effect boundary</div><p className="mt-3 text-[12.5px] leading-5 text-ink-soft">This definition contains no external-send node. Approval-required work must reach a human-review node before completion.</p></GlassCard>
      </div>
    </>
  );
}

