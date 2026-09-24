"use client";

import { useId, useMemo, useState } from "react";
import { ArrowDown, ArrowUp, Bot, CheckCircle2, CircleDot, GitBranch, Loader2, Plus, Save, ShieldCheck, Trash2, Wrench } from "lucide-react";
import { toast } from "sonner";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button, IconButton } from "@/components/ui/Button";
import { Input, Select, Textarea, Field as UIField } from "@/components/ui/Input";
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

// Delegates to components/ui/Input.tsx's Field/Input/Textarea/Select, same precedent as
// FNOL.tsx/Knowledge.tsx. Kept local: this screen's ~10 call sites don't warrant promoting it.
function Field({ label, className, children }: { label: string; className?: string; children: (id: string) => React.ReactNode }) {
  const id = useId();
  return (
    <div className={className}>
      <UIField label={label} htmlFor={id}>{children(id)}</UIField>
    </div>
  );
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
            <div className="text-[10.5px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Node palette</div>
            <div className="mt-3 grid gap-2">
              {(["Agent", "Tool", "Decision", "HumanReview"] as WorkflowNodeType[]).map((type) => {
                const Icon = typeIcon(type);
                return (
                  <button
                    key={type}
                    type="button"
                    onClick={() => addNode(type)}
                    className="dls-focus-outline flex items-center gap-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3 text-left text-[12px] font-semibold text-dls-text-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page"
                  >
                    <span className="grid h-8 w-8 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><Icon className="h-4 w-4" aria-hidden="true" /></span>
                    <span className="flex-1">{type.replace(/([A-Z])/g, " $1").trim()}</span>
                    <Plus className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" />
                  </button>
                );
              })}
            </div>
          </GlassCard>
          <GlassCard className="!p-4">
            <div className="text-[10.5px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Publish gate</div>
            <div className="mt-3 space-y-2 text-[11.5px] text-dls-text-secondary">
              <div className="flex justify-between"><span>Trigger</span><Badge tone="success">1</Badge></div>
              <div className="flex justify-between"><span>Human review</span><Badge tone={nodes.some((node) => node.type === "HumanReview") ? "success" : "warning"}>{nodes.filter((node) => node.type === "HumanReview").length}</Badge></div>
              <div className="flex justify-between"><span>External sends</span><Badge tone="success">Blocked</Badge></div>
              <div className="flex justify-between"><span>Version policy</span><Badge tone="info">Immutable</Badge></div>
            </div>
          </GlassCard>
        </div>

        <div className="min-w-0 space-y-4">
          <GlassCard className="!p-4">
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="Workflow name">{(id) => <Input id={id} value={name} onChange={(event) => setName(event.target.value)} placeholder="Claims evidence preparation" />}</Field>
              <Field label="Workflow key">{(id) => <Input id={id} value={key} onChange={(event) => setKey(event.target.value.toLowerCase().replace(/[^a-z0-9-]/g, "-"))} className="font-mono" placeholder="claims-evidence-preparation" />}</Field>
              <Field label="Purpose" className="md:col-span-2">{(id) => <Textarea id={id} value={description} onChange={(event) => setDescription(event.target.value)} placeholder="What prepared work this flow produces and where human accountability applies." />}</Field>
              <Field label="Allowed business reference types" className="md:col-span-2">{(id) => <Input id={id} value={references} onChange={(event) => setReferences(event.target.value)} placeholder="case, claim, document" />}</Field>
            </div>
          </GlassCard>
          <WorkflowCanvas definition={preview} selectedNodeKey={selected.key} onSelectNode={(node) => setSelectedKey(node.key)} className="min-h-[520px]" />
          <div className="flex flex-wrap justify-end gap-2">
            <Button variant="secondary" disabled={saving} onClick={() => void save(false)} icon={saving ? <Loader2 className="animate-spin" aria-hidden="true" /> : <Save aria-hidden="true" />}>
              Save draft
            </Button>
            <Button variant="primary" disabled={saving} onClick={() => void save(true)} icon={<ShieldCheck aria-hidden="true" />}>
              Save &amp; publish
            </Button>
          </div>
        </div>

        <GlassCard className="!p-4 self-start">
          <div className="flex items-center justify-between gap-2">
            <div className="text-[10.5px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Node configuration</div>
            <Badge tone="neutral">{selected.type}</Badge>
          </div>
          <div className="mt-4 space-y-3">
            <Field label="Name">{(id) => <Input id={id} value={selected.name} onChange={(event) => updateSelected({ name: event.target.value })} />}</Field>
            <Field label="Key">
              {(id) => (
                <Input
                  id={id}
                  disabled={selected.type === "Trigger" || selected.type === "Complete"}
                  value={selected.key}
                  onChange={(event) => {
                    const next = event.target.value.toLowerCase().replace(/[^a-z0-9-]/g, "-");
                    setNodes((current) => current.map((node) => node.key === selected.key ? { ...node, key: next } : node));
                    setSelectedKey(next);
                  }}
                  className="font-mono"
                />
              )}
            </Field>
            <Field label="Instructions">{(id) => <Textarea id={id} value={selected.description} onChange={(event) => updateSelected({ description: event.target.value })} />}</Field>
            {selected.type === "Agent" ? (
              <Field label="Canonical agent">
                {(id) => (
                  <Select id={id} value={selected.agentKey ?? ""} onChange={(event) => updateSelected({ agentKey: event.target.value })} className="w-full">
                    {agents.map((agent) => <option key={agent.key} value={agent.key}>{agent.name}</option>)}
                  </Select>
                )}
              </Field>
            ) : null}
            {selected.type === "Tool" || selected.type === "Decision" ? (
              <Field label="Typed tool">
                {(id) => (
                  <Select id={id} value={selected.toolKey ?? ""} onChange={(event) => updateSelected({ toolKey: event.target.value })} className="w-full">
                    {toolOptions.map((tool) => <option key={tool}>{tool}</option>)}
                  </Select>
                )}
              </Field>
            ) : null}
            {selected.type !== "Trigger" && selected.type !== "Complete" && selected.type !== "HumanReview" ? (
              <Field label="Side-effect class">
                {(id) => (
                  <Select id={id} value={selected.sideEffect} onChange={(event) => updateSelected({ sideEffect: event.target.value as WorkflowNode["sideEffect"] })} className="w-full">
                    <option value="ReadOnly">Read only</option>
                    <option value="DraftOnly">Draft only</option>
                    <option value="InternalWrite">Internal write</option>
                    <option value="ApprovalRequired">Approval required</option>
                  </Select>
                )}
              </Field>
            ) : null}
          </div>
          <div className="mt-5 flex gap-2 border-t border-dls-border-subtle pt-4">
            <IconButton onClick={() => moveSelected(-1)} aria-label="Move node earlier"><ArrowUp className="h-4 w-4" aria-hidden="true" /></IconButton>
            <IconButton onClick={() => moveSelected(1)} aria-label="Move node later"><ArrowDown className="h-4 w-4" aria-hidden="true" /></IconButton>
            <button
              type="button"
              disabled={selected.type === "Trigger" || selected.type === "Complete"}
              onClick={removeSelected}
              className="dls-focus-outline ml-auto inline-flex min-h-11 items-center gap-1.5 rounded-dls-control px-3 text-[11.5px] font-bold text-dls-danger-text transition-colors duration-dls-fast ease-dls hover:underline disabled:opacity-40"
            >
              <Trash2 className="h-3.5 w-3.5" aria-hidden="true" /> Remove
            </button>
          </div>
        </GlassCard>
      </div>
    </>
  );
}
