"use client";

import { useMemo, useState } from "react";
import { ArrowDown, ArrowUp, Bot, CheckCircle2, CircleDot, GitBranch, Loader2, Plus, Save, ShieldCheck, Trash2, Wrench } from "lucide-react";
import { toast } from "sonner";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import WorkflowCanvas from "@/components/iip/WorkflowCanvas";
import { useAgentWorkflows, type WorkflowDefinition, type WorkflowNode, type WorkflowNodeType } from "@/lib/agentWorkflows";
import { useAgents } from "@/lib/lovable-parity/data/agents";

const toolOptions = ["claims.read", "documents.read", "policies.read", "knowledge.retrieve", "reports.read"];

const initialNodes: WorkflowNode[] = [
  { key: "trigger", name: "Business event", description: "Start from a real tenant-scoped business record reference.", type: "Trigger", sideEffect: "ReadOnly", position: { x: 60, y: 180 }, requiresHumanReview: false, configuration: {} },
  { key: "complete", name: "Prepared work complete", description: "End without a consequential insurance decision or external send.", type: "Complete", sideEffect: "ReadOnly", position: { x: 620, y: 180 }, requiresHumanReview: false, configuration: {} },
];

function layout(nodes: WorkflowNode[]) {
  return nodes.map((node, index) => ({ ...node, position: { x: 60 + index * 280, y: index % 2 === 0 ? 150 : 250 } }));
}

function edgesFor(nodes: WorkflowNode[]) {
  return nodes.slice(0, -1).map((node, index) => ({ sourceNodeKey: node.key, targetNodeKey: nodes[index + 1].key, outcome: null, label: null }));
}

function typeIcon(type: WorkflowNodeType) {
  return { Trigger: CircleDot, Agent: Bot, Tool: Wrench, Decision: GitBranch, HumanReview: ShieldCheck, Complete: CheckCircle2 }[type];
}

export default function AgentFlowBuilder() {
  const { saveDraft, publish } = useAgentWorkflows();
  const { agents } = useAgents();
  const [definitionId, setDefinitionId] = useState<string | null>(null);
  const [key, setKey] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [references, setReferences] = useState("case, claim");
  const [nodes, setNodes] = useState<WorkflowNode[]>(initialNodes);
  const [selectedKey, setSelectedKey] = useState("trigger");
  const [saving, setSaving] = useState(false);

  const laidOut = useMemo(() => layout(nodes), [nodes]);
  const selected = laidOut.find((node) => node.key === selectedKey) ?? laidOut[0];
  const preview: WorkflowDefinition = {
    definitionId: definitionId ?? "draft",
    key: key || "new-governed-flow",
    name: name || "New governed workflow",
    description: description || "Draft workflow",
    release: 0,
    version: 1,
    status: "Draft",
    isSystemDefinition: false,
    businessReferenceTypes: references.split(",").map((item) => item.trim()).filter(Boolean),
    nodes: laidOut,
    edges: edgesFor(laidOut),
  };

  function addNode(type: WorkflowNodeType) {
    if (type === "Trigger" || type === "Complete") return;
    const id = `${type.replace(/([A-Z])/g, "-$1").toLowerCase().replace(/^-/, "")}-${crypto.randomUUID().slice(0, 6)}`;
    const firstAgent = agents[0]?.key ?? "claims-summary";
    const node: WorkflowNode = {
      key: id,
      name: type === "HumanReview" ? "Human review" : type === "Agent" ? "Agent task" : type === "Tool" ? "Grounding tool" : "Decision gate",
      description: type === "HumanReview" ? "Review prepared work before any downstream operational use." : "Configure this bounded workflow step.",
      type,
      agentKey: type === "Agent" ? firstAgent : null,
      toolKey: type === "Tool" || type === "Decision" ? toolOptions[0] : null,
      sideEffect: type === "HumanReview" ? "ApprovalRequired" : "ReadOnly",
      position: { x: 0, y: 0 },
      requiresHumanReview: type === "HumanReview",
      configuration: type === "HumanReview" ? { correctionTarget: nodes.findLast((item) => item.type === "Agent")?.key ?? "trigger" } : {},
    };
    setNodes((current) => [...current.slice(0, -1), node, current[current.length - 1]]);
    setSelectedKey(id);
  }

  function updateSelected(change: Partial<WorkflowNode>) {
    setNodes((current) => current.map((node) => node.key === selected.key ? { ...node, ...change } : node));
  }

  function moveSelected(offset: -1 | 1) {
    const index = nodes.findIndex((node) => node.key === selected.key);
    const target = index + offset;
    if (index <= 0 || index >= nodes.length - 1 || target <= 0 || target >= nodes.length - 1) return;
    const next = [...nodes];
    [next[index], next[target]] = [next[target], next[index]];
    setNodes(next);
  }

  function removeSelected() {
    if (selected.type === "Trigger" || selected.type === "Complete") return;
    setNodes((current) => current.filter((node) => node.key !== selected.key));
    setSelectedKey("trigger");
  }

  async function save(shouldPublish: boolean) {
    const businessReferenceTypes = references.split(",").map((item) => item.trim().toLowerCase()).filter(Boolean);
    setSaving(true);
    const saved = await saveDraft({
      definitionId,
      key: key.trim(),
      name: name.trim(),
      description: description.trim(),
      businessReferenceTypes,
      nodes: laidOut,
      edges: edgesFor(laidOut),
    });
    if (!saved.ok) {
      setSaving(false); toast.error(saved.message); return;
    }
    setDefinitionId(saved.data.definitionId);
    if (shouldPublish) {
      const published = await publish(saved.data.key);
      setSaving(false);
      if (!published.ok) { toast.error(published.message); return; }
      toast.success("Workflow published as an immutable governed version.");
      window.location.assign("/app/flows");
      return;
    }
    setSaving(false);
    toast.success("Draft saved to the tenant workflow catalog.");
  }

  return (
    <>
      <SectionHeader eyebrow="Automation Studio · Flow Builder" title="Create a governed workflow" sub="Compose bounded agent, tool, decision, and human-review nodes. Published versions are immutable and auditable." />
      <div className="mb-5"><GuardrailBanner text="External-send nodes are deliberately unavailable. Consequential claim, payment, policy, underwriting, complaint, fraud, and filing decisions cannot be added to this canvas." /></div>

      <div className="grid gap-4 xl:grid-cols-[260px_minmax(0,1fr)_310px]">
        <div className="space-y-4">
          <GlassCard className="!p-4">
            <div className="text-[10.5px] uppercase tracking-[0.14em] text-stone">Node palette</div>
            <div className="mt-3 grid gap-2">
              {(["Agent", "Tool", "Decision", "HumanReview"] as WorkflowNodeType[]).map((type) => {
                const Icon = typeIcon(type);
                return <button key={type} type="button" onClick={() => addNode(type)} className="flex items-center gap-3 rounded-xl border border-cloud bg-white/70 p-3 text-left text-[12px] font-medium text-navy hover:border-violet/30 hover:bg-violet/5 focus-ring"><span className="grid h-8 w-8 place-items-center rounded-xl bg-gradient-secondary text-white"><Icon className="h-4 w-4" /></span><span className="flex-1">{type.replace(/([A-Z])/g, " $1").trim()}</span><Plus className="h-3.5 w-3.5 text-stone" /></button>;
              })}
            </div>
          </GlassCard>
          <GlassCard className="!p-4">
            <div className="text-[10.5px] uppercase tracking-[0.14em] text-stone">Publish gate</div>
            <div className="mt-3 space-y-2 text-[11.5px] text-slate2">
              <div className="flex justify-between"><span>Trigger</span><Badge tone="success">1</Badge></div>
              <div className="flex justify-between"><span>Human review</span><Badge tone={nodes.some((node) => node.type === "HumanReview") ? "success" : "warning"}>{nodes.filter((node) => node.type === "HumanReview").length}</Badge></div>
              <div className="flex justify-between"><span>External sends</span><Badge tone="success">Blocked</Badge></div>
              <div className="flex justify-between"><span>Version policy</span><Badge tone="violet">Immutable</Badge></div>
            </div>
          </GlassCard>
        </div>

        <div className="min-w-0 space-y-4">
          <GlassCard className="!p-4">
            <div className="grid gap-3 md:grid-cols-2">
              <label className="text-[11.5px] text-stone">Workflow name<input value={name} onChange={(event) => setName(event.target.value)} className="mt-1.5 w-full rounded-xl border border-cloud bg-white px-3 py-2.5 text-[12.5px] text-navy focus-ring" placeholder="Claims evidence preparation" /></label>
              <label className="text-[11.5px] text-stone">Workflow key<input value={key} onChange={(event) => setKey(event.target.value.toLowerCase().replace(/[^a-z0-9-]/g, "-"))} className="mt-1.5 w-full rounded-xl border border-cloud bg-white px-3 py-2.5 font-mono text-[12px] text-navy focus-ring" placeholder="claims-evidence-preparation" /></label>
              <label className="text-[11.5px] text-stone md:col-span-2">Purpose<textarea value={description} onChange={(event) => setDescription(event.target.value)} className="mt-1.5 min-h-20 w-full rounded-xl border border-cloud bg-white px-3 py-2.5 text-[12.5px] text-navy focus-ring" placeholder="What prepared work this flow produces and where human accountability applies." /></label>
              <label className="text-[11.5px] text-stone md:col-span-2">Allowed business reference types<input value={references} onChange={(event) => setReferences(event.target.value)} className="mt-1.5 w-full rounded-xl border border-cloud bg-white px-3 py-2.5 text-[12.5px] text-navy focus-ring" placeholder="case, claim, document" /></label>
            </div>
          </GlassCard>
          <WorkflowCanvas definition={preview} selectedNodeKey={selected.key} onSelectNode={(node) => setSelectedKey(node.key)} className="min-h-[520px]" />
          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" disabled={saving} onClick={() => void save(false)} className="inline-flex items-center gap-2 rounded-xl border border-cloud bg-white px-4 py-2.5 text-[12px] font-medium text-navy disabled:opacity-50 focus-ring">{saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />} Save draft</button>
            <button type="button" disabled={saving} onClick={() => void save(true)} className="inline-flex items-center gap-2 rounded-xl gradient-primary px-4 py-2.5 text-[12px] font-medium text-white disabled:opacity-50 focus-ring"><ShieldCheck className="h-4 w-4" /> Save & publish</button>
          </div>
        </div>

        <GlassCard className="!p-4 self-start">
          <div className="flex items-center justify-between gap-2"><div className="text-[10.5px] uppercase tracking-[0.14em] text-stone">Node configuration</div><Badge tone="neutral">{selected.type}</Badge></div>
          <div className="mt-4 space-y-3">
            <label className="block text-[11.5px] text-stone">Name<input value={selected.name} onChange={(event) => updateSelected({ name: event.target.value })} className="mt-1.5 w-full rounded-xl border border-cloud bg-white px-3 py-2 text-[12px] text-navy focus-ring" /></label>
            <label className="block text-[11.5px] text-stone">Key<input disabled={selected.type === "Trigger" || selected.type === "Complete"} value={selected.key} onChange={(event) => { const next = event.target.value.toLowerCase().replace(/[^a-z0-9-]/g, "-"); setNodes((current) => current.map((node) => node.key === selected.key ? { ...node, key: next } : node)); setSelectedKey(next); }} className="mt-1.5 w-full rounded-xl border border-cloud bg-white px-3 py-2 font-mono text-[11.5px] text-navy disabled:bg-cloud/40 focus-ring" /></label>
            <label className="block text-[11.5px] text-stone">Instructions<textarea value={selected.description} onChange={(event) => updateSelected({ description: event.target.value })} className="mt-1.5 min-h-24 w-full rounded-xl border border-cloud bg-white px-3 py-2 text-[12px] text-navy focus-ring" /></label>
            {selected.type === "Agent" ? <label className="block text-[11.5px] text-stone">Canonical agent<select value={selected.agentKey ?? ""} onChange={(event) => updateSelected({ agentKey: event.target.value })} className="mt-1.5 w-full rounded-xl border border-cloud bg-white px-3 py-2 text-[12px] text-navy focus-ring">{agents.map((agent) => <option key={agent.key} value={agent.key}>{agent.name}</option>)}</select></label> : null}
            {selected.type === "Tool" || selected.type === "Decision" ? <label className="block text-[11.5px] text-stone">Typed tool<select value={selected.toolKey ?? ""} onChange={(event) => updateSelected({ toolKey: event.target.value })} className="mt-1.5 w-full rounded-xl border border-cloud bg-white px-3 py-2 text-[12px] text-navy focus-ring">{toolOptions.map((tool) => <option key={tool}>{tool}</option>)}</select></label> : null}
            {selected.type !== "Trigger" && selected.type !== "Complete" && selected.type !== "HumanReview" ? <label className="block text-[11.5px] text-stone">Side-effect class<select value={selected.sideEffect} onChange={(event) => updateSelected({ sideEffect: event.target.value as WorkflowNode["sideEffect"] })} className="mt-1.5 w-full rounded-xl border border-cloud bg-white px-3 py-2 text-[12px] text-navy focus-ring"><option value="ReadOnly">Read only</option><option value="DraftOnly">Draft only</option><option value="InternalWrite">Internal write</option><option value="ApprovalRequired">Approval required</option></select></label> : null}
          </div>
          <div className="mt-5 flex gap-2 border-t border-cloud pt-4">
            <button type="button" onClick={() => moveSelected(-1)} className="grid h-9 w-9 place-items-center rounded-xl border border-cloud bg-white text-slate2 focus-ring" aria-label="Move node earlier"><ArrowUp className="h-4 w-4" /></button>
            <button type="button" onClick={() => moveSelected(1)} className="grid h-9 w-9 place-items-center rounded-xl border border-cloud bg-white text-slate2 focus-ring" aria-label="Move node later"><ArrowDown className="h-4 w-4" /></button>
            <button type="button" disabled={selected.type === "Trigger" || selected.type === "Complete"} onClick={removeSelected} className="ml-auto inline-flex items-center gap-2 rounded-xl border border-error/20 bg-white px-3 py-2 text-[11.5px] text-error disabled:opacity-40 focus-ring"><Trash2 className="h-3.5 w-3.5" /> Remove</button>
          </div>
        </GlassCard>
      </div>
    </>
  );
}

