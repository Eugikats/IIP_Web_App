"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { Bot, Braces, History, Loader2, Play, Plus, ShieldCheck } from "lucide-react";
import { toast } from "sonner";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Badge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import WorkflowCanvas from "@/components/iip/WorkflowCanvas";
import { useAgentWorkflows, type WorkflowDefinition, type WorkflowNode } from "@/lib/agentWorkflows";
import { useUI } from "@/context/UIStateContext";
import { canPreviewPermission } from "@/lib/roles";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { canUseLocalTestDoubles } from "@/lib/config";

function releaseLabel(flow: WorkflowDefinition) {
  return flow.release > 0 ? `Release ${flow.release}` : "Custom";
}

export default function AgentFlows() {
  const { definitions, runs, loading, error, startRun } = useAgentWorkflows();
  const { roleDef } = useUI();
  const { runtimeContext, parityMode } = useRuntimeContext();
  const canManage = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "ManageAgentWorkflows")
    : (runtimeContext?.permissions ?? []).includes("ManageAgentWorkflows");
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [selectedNode, setSelectedNode] = useState<WorkflowNode | null>(null);
  const [referenceType, setReferenceType] = useState("");
  const [referenceId, setReferenceId] = useState("");
  const [starting, setStarting] = useState(false);

  useEffect(() => {
    if (!selectedKey && definitions.length > 0) setSelectedKey(definitions[0].key);
  }, [definitions, selectedKey]);

  const selected = useMemo(
    () => definitions.find((flow) => flow.key === selectedKey) ?? definitions[0] ?? null,
    [definitions, selectedKey],
  );

  useEffect(() => {
    setSelectedNode(selected?.nodes[0] ?? null);
    setReferenceType(selected?.businessReferenceTypes[0] ?? "");
    setReferenceId("");
  }, [selected?.key]);

  async function runSelected() {
    if (!selected || !referenceType || !referenceId.trim()) {
      toast.error("Choose a reference type and enter a real business record reference.");
      return;
    }
    setStarting(true);
    const result = await startRun(selected.key, referenceType, referenceId.trim());
    setStarting(false);
    if (result.ok) {
      toast.success("Workflow run queued with a durable audit trail.");
      window.location.assign(`/app/runs/${result.data.runId}`);
    } else {
      toast.error(result.message);
    }
  }

  return (
    <>
      <SectionHeader
        eyebrow="Agent Operations · Automation Studio"
        title="Governed insurance workflows"
        sub="Design and operate reusable multi-agent flows with evidence, human handoffs, durable run state, and protected side-effect boundaries."
        action={canManage ?
          <Link
            href="/app/flows/new"
            className="dls-focus-outline inline-flex min-h-11 items-center justify-center gap-1.5 rounded-dls-control bg-dls-primary px-4 py-[9px] text-[13px] font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover"
          >
            <Plus className="h-4 w-4" aria-hidden="true" /> Create governed flow
          </Link>
        : undefined}
      />
      <div className="mb-5">
        <GuardrailBanner text="Flows prepare insurance work. They cannot approve or reject claims, bind cover, set premiums, release payment, accuse fraud, or send externally without the existing named-human approval services." />
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={5} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Workflow control plane could not be loaded" message={error} /></GlassCard>
      ) : definitions.length === 0 ? (
        <GlassCard><ErrorStateSurface title="No workflow definitions available" message="The release catalog could not be loaded for this workspace." /></GlassCard>
      ) : (
        <div className="grid gap-4 xl:grid-cols-[280px_minmax(0,1fr)_300px]">
          <GlassCard className="!p-3 xl:max-h-[680px] xl:overflow-y-auto">
            <div className="px-2 pb-3 text-[10.5px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Release catalog</div>
            <div className="space-y-2">
              {definitions.map((flow) => {
                const flowRuns = runs.filter((run) => run.workflowKey === flow.key);
                const active = flow.key === selected?.key;
                return (
                  <button
                    key={flow.definitionId}
                    type="button"
                    onClick={() => setSelectedKey(flow.key)}
                    className={`dls-focus-outline w-full rounded-dls-card border p-3 text-left transition-colors duration-dls-fast ease-dls ${active ? "border-dls-primary-border bg-dls-primary-soft-alt" : "border-transparent hover:border-dls-border-subtle hover:bg-dls-bg-page"}`}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge tone={flow.release > 0 ? "info" : "neutral"}>{releaseLabel(flow)}</Badge>
                      <span className="text-[10.5px] text-dls-text-muted">v{flow.version}</span>
                    </div>
                    <div className="mt-2 text-[12.5px] font-semibold leading-4 text-dls-text-primary">{flow.name.replace(/^Release \d+ · /, "")}</div>
                    <div className="mt-2 flex items-center gap-3 text-[10.5px] text-dls-text-muted">
                      <span>{flow.nodes.length} nodes</span><span>{flowRuns.length} runs</span>
                    </div>
                  </button>
                );
              })}
            </div>
          </GlassCard>

          {selected ? (
            <div className="min-w-0 space-y-4">
              <GlassCard className="!p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge tone="info">{releaseLabel(selected)}</Badge>
                      <StatusBadge status={selected.status} />
                      {selected.isSystemDefinition ? <Badge tone="neutral">Platform governed</Badge> : <Badge tone="info">Tenant flow</Badge>}
                    </div>
                    <h2 className="mt-3 text-lg font-bold text-dls-text-primary">{selected.name}</h2>
                    <p className="mt-1 max-w-3xl text-[12.5px] leading-5 text-dls-text-secondary">{selected.description}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Link href={`/app/flows/${selected.key}`} className="dls-focus-outline inline-flex min-h-11 items-center gap-1.5 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[11.5px] font-semibold text-dls-text-primary hover:bg-dls-bg-page">
                      <Braces className="h-3.5 w-3.5" aria-hidden="true" /> Inspect definition
                    </Link>
                    <Link href="/app/work-items" className="dls-focus-outline inline-flex min-h-11 items-center gap-1.5 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[11.5px] font-semibold text-dls-text-primary hover:bg-dls-bg-page">
                      <ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" /> Review queue
                    </Link>
                  </div>
                </div>
              </GlassCard>
              <WorkflowCanvas definition={selected} selectedNodeKey={selectedNode?.key} onSelectNode={setSelectedNode} className="min-h-[460px]" />
              <GlassCard className="!p-4">
                <div className="flex items-center gap-2 text-[12.5px] font-bold text-dls-text-primary"><Play className="h-4 w-4 text-dls-info-text" aria-hidden="true" /> Start against a real business record</div>
                <div className="mt-3 grid gap-3 md:grid-cols-[180px_minmax(0,1fr)_auto]">
                  <Select value={referenceType} onChange={(event) => setReferenceType(event.target.value)} aria-label="Reference type">
                    {selected.businessReferenceTypes.map((type) => <option key={type} value={type}>{type}</option>)}
                  </Select>
                  <Input value={referenceId} onChange={(event) => setReferenceId(event.target.value)} placeholder="Record ID or authoritative business reference" aria-label="Business record reference" />
                  <Button
                    type="button"
                    variant="primary"
                    onClick={() => void runSelected()}
                    disabled={starting}
                    icon={starting ? <Loader2 className="animate-spin" aria-hidden="true" /> : <Play aria-hidden="true" />}
                  >
                    Queue run
                  </Button>
                </div>
              </GlassCard>
            </div>
          ) : null}

          <div className="space-y-4">
            <GlassCard className="!p-4">
              <div className="flex items-center gap-2 text-[11px] font-bold uppercase tracking-[0.13em] text-dls-text-muted"><Bot className="h-4 w-4 text-dls-primary" aria-hidden="true" /> Node inspector</div>
              {selectedNode ? (
                <div className="mt-4">
                  <div className="text-sm font-bold text-dls-text-primary">{selectedNode.name}</div>
                  <div className="mt-1 text-[11px] font-bold uppercase tracking-[0.1em] text-dls-text-muted">{selectedNode.type} · {selectedNode.key}</div>
                  <p className="mt-3 text-[12px] leading-5 text-dls-text-secondary">{selectedNode.description}</p>
                  <div className="mt-4 space-y-2 border-t border-dls-border-subtle pt-3 text-[11.5px]">
                    <div className="flex justify-between gap-3"><span className="text-dls-text-muted">Agent</span><span className="text-right font-semibold text-dls-text-primary">{selectedNode.agentKey ?? "Not applicable"}</span></div>
                    <div className="flex justify-between gap-3"><span className="text-dls-text-muted">Tool</span><span className="text-right font-semibold text-dls-text-primary">{selectedNode.toolKey ?? "Not applicable"}</span></div>
                    <div className="flex justify-between gap-3"><span className="text-dls-text-muted">Side effect</span><Badge tone={selectedNode.sideEffect === "ApprovalRequired" ? "warning" : "neutral"}>{selectedNode.sideEffect}</Badge></div>
                    <div className="flex justify-between gap-3"><span className="text-dls-text-muted">Human review</span><Badge tone={selectedNode.requiresHumanReview ? "info" : "success"}>{selectedNode.requiresHumanReview ? "Required" : "Bounded"}</Badge></div>
                  </div>
                </div>
              ) : <div className="mt-4 text-[12px] text-dls-text-secondary">Select a node to inspect its authority and handoff.</div>}
            </GlassCard>
            <GlassCard className="!p-4">
              <div className="flex items-center gap-2 text-[11px] font-bold uppercase tracking-[0.13em] text-dls-text-muted"><History className="h-4 w-4 text-dls-info-text" aria-hidden="true" /> Recent runs</div>
              <div className="mt-3 space-y-2">
                {runs.filter((run) => run.workflowKey === selected?.key).slice(0, 5).map((run) => (
                  <Link key={run.runId} href={`/app/runs/${run.runId}`} className="dls-focus-outline block rounded-dls-control border border-dls-border-subtle bg-dls-surface p-3 transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page">
                    <div className="flex items-center justify-between gap-2"><span className="truncate text-[11.5px] font-semibold text-dls-text-primary">{run.businessReferenceId}</span><StatusBadge status={run.status} /></div>
                    <div className="mt-1 truncate font-mono text-[10px] text-dls-text-muted">{run.correlationId}</div>
                  </Link>
                ))}
                {runs.filter((run) => run.workflowKey === selected?.key).length === 0 ? (
                  <div className="rounded-dls-control border border-dashed border-dls-border-subtle p-4 text-center text-[11.5px] text-dls-text-muted">No runs recorded for this flow.</div>
                ) : null}
              </div>
            </GlassCard>
          </div>
        </div>
      )}
    </>
  );
}
