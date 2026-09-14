"use client";

import { Bot, CheckCircle2, CircleDot, GitBranch, ShieldCheck, Wrench } from "lucide-react";
import { cn } from "@/lib/utils";
import type { WorkflowDefinition, WorkflowNode, WorkflowNodeRun } from "@/lib/agentWorkflows";

// Design language v2 migration (ui-migration-ledger.md, tier 4). Foundation-level shared
// dependency, not a per-screen patch, used by AgentFlows.tsx, AgentFlowInspector.tsx,
// AgentFlowBuilder.tsx, and AgentRunDebugger.tsx (4 of tier 4's 6 screens), so migrated once here
// before any of them, same precedent as the earlier chat-surface and Primitives.tsx fixes.
//
// Removed outright: backdrop-blur node cards (glassmorphism), the bg-gradient-secondary icon
// tile (gradient outside the two permitted uses), and every shadow-glow-* status treatment, a
// glowing node on state change is exactly the "neon accent / ambient glow" pattern craft
// guardrails §2 item 3 catalogues. Status is now border + tint background only, matching how
// every other status surface in the product signals state.

const nodeIcons = {
  Trigger: CircleDot,
  Agent: Bot,
  Tool: Wrench,
  Decision: GitBranch,
  HumanReview: ShieldCheck,
  Complete: CheckCircle2,
} as const;

const statusStyles: Record<string, string> = {
  Pending: "border-dls-border-subtle bg-dls-surface",
  Queued: "border-dls-info-text bg-dls-info-tint",
  Running: "border-dls-primary bg-dls-primary-soft-alt",
  WaitingForHuman: "border-dls-warning-text bg-dls-warning-tint",
  Completed: "border-dls-success-text bg-dls-success-tint",
  Failed: "border-dls-danger-text bg-dls-danger-tint",
  Skipped: "border-dls-border-subtle bg-dls-surface-sunken opacity-70",
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
        "dls-focus-outline absolute z-10 w-[210px] rounded-dls-overlay border p-3 text-left transition-colors duration-dls-fast ease-dls",
        statusStyles[run?.status ?? "Pending"],
        selected && "ring-2 ring-dls-primary/40 border-dls-primary",
      )}
      style={{ left: node.position.x, top: node.position.y }}
    >
      <div className="flex items-start gap-2.5">
        <span className="grid h-8 w-8 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary">
          <Icon className="h-4 w-4" aria-hidden="true" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block truncate text-[12.5px] font-semibold text-dls-text-primary">{node.name}</span>
          <span className="mt-0.5 block truncate text-[10.5px] font-bold uppercase tracking-[0.1em] text-dls-text-muted">
            {node.type.replace(/([A-Z])/g, " $1").trim()}{run ? ` · ${run.status}` : ""}
          </span>
        </span>
      </div>
      <span className="mt-2 line-clamp-2 block text-[11.5px] leading-4 text-dls-text-secondary">{run?.outputSummary ?? node.description}</span>
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
    <div className={cn("relative overflow-auto rounded-dls-card border border-dls-border-subtle bg-dls-surface", className)}>
      <div
        className="relative min-h-[430px]"
        style={{
          width,
          height,
          // A dot-grid canvas texture, not a brand gradient, kept on a design-system colour
          // (border-strong) at low alpha rather than the previous literal rgba(11,15,59,…).
          backgroundImage: "radial-gradient(circle, rgb(var(--color-border-strong) / 0.5) 1px, transparent 1px)",
          backgroundSize: "22px 22px",
        }}
      >
        <svg className="absolute inset-0 h-full w-full" viewBox={`0 0 ${width} ${height}`} aria-hidden="true">
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
                {/* Flat structural colour, not the two named gradient exceptions (§4.5), edges
                    are connective tissue, not a call to action. */}
                <path
                  d={`M ${x1} ${y1} C ${x1 + bend} ${y1}, ${x2 - bend} ${y2}, ${x2} ${y2}`}
                  fill="none"
                  stroke="rgb(var(--color-border-strong))"
                  strokeWidth="2"
                  markerEnd={`url(#flow-arrow-${definition.definitionId})`}
                />
                {edge.label ? (
                  <text x={(x1 + x2) / 2} y={(y1 + y2) / 2 - 8} textAnchor="middle" className="fill-dls-text-muted text-[10px]">{edge.label}</text>
                ) : null}
              </g>
            );
          })}
          <defs>
            <marker id={`flow-arrow-${definition.definitionId}`} markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
              <path d="M0,0 L8,4 L0,8 z" fill="rgb(var(--color-border-strong))" />
            </marker>
          </defs>
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
