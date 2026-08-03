"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { Bot, Braces, GitBranch, History, Loader2, Play, Plus, ShieldCheck } from "lucide-react";
import { toast } from "sonner";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Badge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import WorkflowCanvas from "@/components/iip/WorkflowCanvas";
import { useAgentWorkflows, type WorkflowDefinition, type WorkflowNode } from "@/lib/agentWorkflows";
import { useUI } from "@/context/UIStateContext";

function releaseLabel(flow: WorkflowDefinition) {
  return flow.release > 0 ? `Release ${flow.release}` : "Custom";
}

export default function AgentFlows() {
  const { definitions, runs, loading, error, startRun } = useAgentWorkflows();
  const { roleDef } = useUI();
  const canManage = roleDef.key === "Admin" || roleDef.key === "ITAdmin";
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
          <Link href="/app/flows/new" className="inline-flex items-center gap-2 rounded-2xl gradient-primary px-4 py-2.5 text-[12.5px] font-medium text-white shadow-l2 focus-ring">
            <Plus className="h-4 w-4" /> Create governed flow
          </Link>
        : undefined}
      />
      <div className="mb-5">
        <GuardrailBanner text="Flows prepare insurance work. They cannot approve or reject claims, bind cover, set premiums, release payment, accuse fraud, or send externally without the existing named-human approval services." />
      </div>

      {loading ? (
        <GlassCard><div className="flex items-center justify-center gap-2 py-12 text-sm text-stone"><Loader2 className="h-4 w-4 animate-spin" /> Loading workflow control plane…</div></GlassCard>
      ) : definitions.length === 0 ? (
        <GlassCard><div className="py-14 text-center"><div className="font-medium text-navy">No workflow definitions available</div><div className="mt-1 text-[12.5px] text-stone">{error ?? "The release catalog could not be loaded for this workspace."}</div></div></GlassCard>
      ) : (
        <div className="grid gap-4 xl:grid-cols-[280px_minmax(0,1fr)_300px]">
          <GlassCard className="!p-3 xl:max-h-[680px] xl:overflow-y-auto">
            <div className="px-2 pb-3 text-[10.5px] uppercase tracking-[0.14em] text-stone">Release catalog</div>
            <div className="space-y-2">
              {definitions.map((flow) => {
                const flowRuns = runs.filter((run) => run.workflowKey === flow.key);
                const active = flow.key === selected?.key;
                return (
                  <button
                    key={flow.definitionId}
                    type="button"
                    onClick={() => setSelectedKey(flow.key)}
                    className={`w-full rounded-2xl border p-3 text-left transition-all focus-ring ${active ? "border-violet/30 bg-violet/10 shadow-l1" : "border-cloud/80 bg-white/65 hover:bg-white"}`}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge tone={flow.release > 0 ? "violet" : "info"}>{releaseLabel(flow)}</Badge>
                      <span className="text-[10.5px] text-stone">v{flow.version}</span>
                    </div>
                    <div className="mt-2 text-[12.5px] font-semibold leading-4 text-navy">{flow.name.replace(/^Release \d+ · /, "")}</div>
                    <div className="mt-2 flex items-center gap-3 text-[10.5px] text-stone">
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
                      <Badge tone="violet">{releaseLabel(selected)}</Badge>
                      <StatusBadge status={selected.status} />
                      {selected.isSystemDefinition ? <Badge tone="neutral">Platform governed</Badge> : <Badge tone="info">Tenant flow</Badge>}
                    </div>
                    <h2 className="mt-3 text-lg font-semibold text-navy">{selected.name}</h2>
                    <p className="mt-1 max-w-3xl text-[12.5px] leading-5 text-slate2">{selected.description}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Link href={`/app/flows/${selected.key}`} className="inline-flex items-center gap-1.5 rounded-xl border border-cloud bg-white px-3 py-2 text-[11.5px] font-medium text-navy focus-ring">
                      <Braces className="h-3.5 w-3.5" /> Inspect definition
                    </Link>
                    <Link href="/app/work-items" className="inline-flex items-center gap-1.5 rounded-xl border border-cloud bg-white px-3 py-2 text-[11.5px] font-medium text-navy focus-ring">
                      <ShieldCheck className="h-3.5 w-3.5" /> Review queue
                    </Link>
                  </div>
                </div>
              </GlassCard>
              <WorkflowCanvas definition={selected} selectedNodeKey={selectedNode?.key} onSelectNode={setSelectedNode} className="min-h-[460px]" />
              <GlassCard className="!p-4">
                <div className="flex items-center gap-2 text-[12.5px] font-semibold text-navy"><Play className="h-4 w-4 text-info" /> Start against a real business record</div>
                <div className="mt-3 grid gap-3 md:grid-cols-[180px_minmax(0,1fr)_auto]">
                  <select value={referenceType} onChange={(event) => setReferenceType(event.target.value)} className="rounded-xl border border-cloud bg-white px-3 py-2.5 text-[12.5px] text-navy focus-ring">
                    {selected.businessReferenceTypes.map((type) => <option key={type} value={type}>{type}</option>)}
                  </select>
                  <input value={referenceId} onChange={(event) => setReferenceId(event.target.value)} placeholder="Record ID or authoritative business reference" className="rounded-xl border border-cloud bg-white px-3 py-2.5 text-[12.5px] text-navy placeholder:text-stone focus-ring" />
                  <button type="button" onClick={() => void runSelected()} disabled={starting} className="inline-flex items-center justify-center gap-2 rounded-xl gradient-primary px-4 py-2.5 text-[12.5px] font-medium text-white disabled:opacity-50 focus-ring">
                    {starting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Play className="h-4 w-4" />} Queue run
                  </button>
                </div>
              </GlassCard>
            </div>
          ) : null}

          <div className="space-y-4">
            <GlassCard className="!p-4">
              <div className="flex items-center gap-2 text-[11px] uppercase tracking-[0.13em] text-stone"><Bot className="h-4 w-4 text-violet" /> Node inspector</div>
              {selectedNode ? (
                <div className="mt-4">
                  <div className="text-sm font-semibold text-navy">{selectedNode.name}</div>
                  <div className="mt-1 text-[11px] uppercase tracking-[0.1em] text-stone">{selectedNode.type} · {selectedNode.key}</div>
                  <p className="mt-3 text-[12px] leading-5 text-slate2">{selectedNode.description}</p>
                  <div className="mt-4 space-y-2 border-t border-cloud pt-3 text-[11.5px]">
                    <div className="flex justify-between gap-3"><span className="text-stone">Agent</span><span className="text-right font-medium text-navy">{selectedNode.agentKey ?? "Not applicable"}</span></div>
                    <div className="flex justify-between gap-3"><span className="text-stone">Tool</span><span className="text-right font-medium text-navy">{selectedNode.toolKey ?? "Not applicable"}</span></div>
                    <div className="flex justify-between gap-3"><span className="text-stone">Side effect</span><Badge tone={selectedNode.sideEffect === "ApprovalRequired" ? "warning" : "neutral"}>{selectedNode.sideEffect}</Badge></div>
                    <div className="flex justify-between gap-3"><span className="text-stone">Human review</span><Badge tone={selectedNode.requiresHumanReview ? "violet" : "success"}>{selectedNode.requiresHumanReview ? "Required" : "Bounded"}</Badge></div>
                  </div>
                </div>
              ) : <div className="mt-4 text-[12px] text-stone">Select a node to inspect its authority and handoff.</div>}
            </GlassCard>
            <GlassCard className="!p-4">
              <div className="flex items-center gap-2 text-[11px] uppercase tracking-[0.13em] text-stone"><History className="h-4 w-4 text-info" /> Recent runs</div>
              <div className="mt-3 space-y-2">
                {runs.filter((run) => run.workflowKey === selected?.key).slice(0, 5).map((run) => (
                  <Link key={run.runId} href={`/app/runs/${run.runId}`} className="block rounded-xl border border-cloud bg-white/65 p-3 hover:bg-white focus-ring">
                    <div className="flex items-center justify-between gap-2"><span className="truncate text-[11.5px] font-medium text-navy">{run.businessReferenceId}</span><StatusBadge status={run.status} /></div>
                    <div className="mt-1 truncate font-mono text-[10px] text-stone">{run.correlationId}</div>
                  </Link>
                ))}
                {runs.filter((run) => run.workflowKey === selected?.key).length === 0 ? <div className="rounded-xl border border-dashed border-cloud p-4 text-center text-[11.5px] text-stone">No runs recorded for this flow.</div> : null}
              </div>
            </GlassCard>
          </div>
        </div>
      )}
    </>
  );
}
