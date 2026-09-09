"use client";

import { useParams } from "next/navigation";
import { LockKeyhole, ShieldCheck } from "lucide-react";
import { Badge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import WorkflowCanvas from "@/components/iip/WorkflowCanvas";
import { useAgentWorkflows } from "@/lib/agentWorkflows";

export default function AgentFlowInspector() {
  const params = useParams<{ key: string }>();
  const { definitions, runs, loading, error } = useAgentWorkflows();
  const definition = definitions.find((item) => item.key === decodeURIComponent(params.key));
  if (loading) return <GlassCard><LoadingSkeleton rows={5} /></GlassCard>;
  if (!definition) return <GlassCard><ErrorStateSurface title="Workflow definition unavailable" message={error ?? "The requested workflow is not available in this workspace."} /></GlassCard>;
  return (
    <>
      <SectionHeader eyebrow="Automation Studio · Definition Inspector" title={definition.name} sub={definition.description} />
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <StatusBadge status={definition.status} />
        <Badge tone="info">{definition.isSystemDefinition ? "Platform governed" : "Tenant governed"}</Badge>
        <Badge tone="neutral">Version {definition.version}</Badge>
        <Badge tone="neutral">{runs.filter((run) => run.workflowKey === definition.key).length} recorded runs</Badge>
      </div>
      <WorkflowCanvas definition={definition} className="min-h-[520px]" />
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <GlassCard className="!p-4">
          <div className="flex items-center gap-2 text-[10.5px] font-bold uppercase tracking-[0.14em] text-dls-text-muted"><LockKeyhole className="h-4 w-4 text-dls-primary" aria-hidden="true" /> Version boundary</div>
          <p className="mt-3 text-[12.5px] leading-5 text-dls-text-secondary">Published versions are immutable. Changes require a new tenant workflow key or a governed platform release, preserving reproducible run history.</p>
        </GlassCard>
        <GlassCard className="!p-4">
          <div className="flex items-center gap-2 text-[10.5px] font-bold uppercase tracking-[0.14em] text-dls-text-muted"><ShieldCheck className="h-4 w-4 text-dls-success-text" aria-hidden="true" /> Side-effect boundary</div>
          <p className="mt-3 text-[12.5px] leading-5 text-dls-text-secondary">This definition contains no external-send node. Approval-required work must reach a human-review node before completion.</p>
        </GlassCard>
      </div>
    </>
  );
}
