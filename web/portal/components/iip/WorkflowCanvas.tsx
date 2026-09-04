"use client";

import { Bot, CheckCircle2, CircleDot, GitBranch, ShieldCheck, Wrench } from "lucide-react";
import { cn } from "@/lib/utils";
import type { WorkflowDefinition, WorkflowNode, WorkflowNodeRun } from "@/lib/agentWorkflows";

const nodeIcons = {
  Trigger: CircleDot,
  Agent: Bot,
  Tool: Wrench,
  Decision: GitBranch,
  HumanReview: ShieldCheck,
  Complete: CheckCircle2,
} as const;

const statusStyles: Record<string, string> = {
  Pending: "border-line bg-white/90",
  Queued: "border-info/40 bg-info/5 shadow-glow-cyan",
  Running: "border-violet/40 bg-violet/5 shadow-glow-cyan",
  WaitingForHuman: "border-warning/50 bg-warning/10 shadow-glow-warning",
  Completed: "border-success/40 bg-success/10 shadow-glow-success",
  Failed: "border-danger/45 bg-danger/10 shadow-glow-danger",
  Skipped: "border-line bg-line/60 opacity-70",
};

function WorkflowNodeCard({ node, run, selected, onSelect }: {
  node: WorkflowNode;
  run?: WorkflowNodeRun;
  selected: boolean;
  onSelect?: (node: WorkflowNode) => void;
}) {
  const Icon = nodeIcons[node.type];
  return (
    <button
      type="button"
      onClick={() => onSelect?.(node)}
      className={cn(
        "absolute z-10 w-[210px] rounded-2xl border p-3 text-left backdrop-blur transition-all focus-ring",
        statusStyles[run?.status ?? "Pending"],
        selected && "ring-2 ring-violet/40 border-violet/50",
      )}
      style={{ left: node.position.x, top: node.position.y }}
    >
      <div className="flex items-start gap-2.5">
        <span className="grid h-8 w-8 shrink-0 place-items-center rounded-xl bg-gradient-secondary text-white shadow-l1">
          <Icon className="h-4 w-4" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block truncate text-[12.5px] font-semibold text-ink">{node.name}</span>
          <span className="mt-0.5 block truncate text-[10.5px] uppercase tracking-[0.1em] text-muted">
            {node.type.replace(/([A-Z])/g, " $1").trim()}{run ? ` · ${run.status}` : ""}
          </span>
        </span>
      </div>
      <span className="mt-2 line-clamp-2 block text-[11.5px] leading-4 text-ink-soft">{run?.outputSummary ?? node.description}</span>
    </button>
  );
}

export default function WorkflowCanvas({ definition, nodeRuns = [], selectedNodeKey, onSelectNode, className }: {
  definition: WorkflowDefinition;
  nodeRuns?: WorkflowNodeRun[];
  selectedNodeKey?: string | null;
  onSelectNode?: (node: WorkflowNode) => void;
  className?: string;
}) {
  const width = Math.max(760, ...definition.nodes.map((node) => node.position.x + 260));
  const height = Math.max(430, ...definition.nodes.map((node) => node.position.y + 150));
  const runByNode = new Map(nodeRuns.map((run) => [run.nodeKey, run]));
  const nodeByKey = new Map(definition.nodes.map((node) => [node.key, node]));

  return (
    <div className={cn("relative overflow-auto rounded-2xl border border-line/80 bg-white/45", className)}>
      <div
        className="relative min-h-[430px]"
        style={{
          width,
          height,
          backgroundImage: "radial-gradient(circle, rgba(11,15,59,0.12) 1px, transparent 1px)",
          backgroundSize: "22px 22px",
        }}
      >
        <svg className="absolute inset-0 h-full w-full" viewBox={`0 0 ${width} ${height}`} aria-hidden="true">
          <defs>
            <linearGradient id="flow-line" x1="0" x2="1">
              <stop offset="0" stopColor="#2563FF" stopOpacity="0.45" />
              <stop offset="1" stopColor="#7C4DFF" stopOpacity="0.65" />
            </linearGradient>
            <marker id="flow-arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
              <path d="M0,0 L8,4 L0,8 z" fill="#7C4DFF" fillOpacity="0.65" />
            </marker>
          </defs>
          {definition.edges.map((edge, index) => {
            const source = nodeByKey.get(edge.sourceNodeKey);
            const target = nodeByKey.get(edge.targetNodeKey);
            if (!source || !target) return null;
            const x1 = source.position.x + 210;
            const y1 = source.position.y + 48;
            const x2 = target.position.x;
            const y2 = target.position.y + 48;
            const bend = Math.max(40, Math.abs(x2 - x1) * 0.45);
            return (
              <g key={`${edge.sourceNodeKey}-${edge.targetNodeKey}-${index}`}>
                <path
                  d={`M ${x1} ${y1} C ${x1 + bend} ${y1}, ${x2 - bend} ${y2}, ${x2} ${y2}`}
                  fill="none"
                  stroke="url(#flow-line)"
                  strokeWidth="2"
                  markerEnd="url(#flow-arrow)"
                />
                {edge.label ? <text x={(x1 + x2) / 2} y={(y1 + y2) / 2 - 8} textAnchor="middle" className="fill-muted text-[10px]">{edge.label}</text> : null}
              </g>
            );
          })}
        </svg>
        {definition.nodes.map((node) => (
          <WorkflowNodeCard
            key={node.key}
            node={node}
            run={runByNode.get(node.key)}
            selected={selectedNodeKey === node.key}
            onSelect={onSelectNode}
          />
        ))}
      </div>
    </div>
  );
}

