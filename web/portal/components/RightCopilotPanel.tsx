"use client";

import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { ChevronLeft, ChevronRight, ShieldCheck } from "lucide-react";
import { CommandCenterIntelligence } from "./CommandCenterIntelligence";
import { EvidenceListBlock } from "./blocks/EvidenceListBlock";
import { NotConfiguredBlock } from "./blocks/NotConfiguredBlock";
import { useRole } from "./RoleContext";
import { hasPermission } from "@/lib/roleUiConfig";
import {
  useCommandCenterLastTurn,
  type CompletedAssistantTurn,
} from "@/hooks/useCommandCenterQuery";

/**
 * Screen-edge right rail. Today only the Command Center has real, contextual
 * content to show here, so the rail renders there and is hidden elsewhere.
 *
 * Once an AI query has completed, the rail becomes the Context & Evidence Rail:
 * it reflects the latest turn's cited sources, honest capability gaps, and the
 * governed provenance backing the answer. Before any query, it falls back to the
 * default operational Intelligence content.
 */
export function RightCopilotPanel() {
  const pathname = usePathname();
  const { isLoadingContext, permissions } = useRole();
  const [collapsed, setCollapsed] = useState(false);
  const lastTurn = useCommandCenterLastTurn();

  useEffect(() => {
    if (window.localStorage.getItem("iip-rightpanel-collapsed") === "1") setCollapsed(true);
  }, []);

  function toggle() {
    setCollapsed((c) => {
      const next = !c;
      window.localStorage.setItem("iip-rightpanel-collapsed", next ? "1" : "0");
      return next;
    });
  }

  if (pathname !== "/" || isLoadingContext || !hasPermission(permissions, "ViewCommandCenter")) return null;

  const label = lastTurn ? "Context & Evidence" : "Context";

  return (
    <aside
      aria-label="Command center context and evidence"
      className={`hidden h-screen flex-shrink-0 flex-col border-l border-line bg-surface transition-all duration-300 xl:flex ${
        collapsed ? "xl:w-[56px]" : "xl:w-[360px]"
      }`}
    >
      <div className="flex items-center justify-between border-b border-line px-3 py-3">
        {!collapsed && <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-muted-soft">{label}</p>}
        <button
          onClick={toggle}
          title={collapsed ? "Expand panel" : "Collapse panel"}
          aria-label={collapsed ? "Expand context panel" : "Collapse context panel"}
          className={`flex h-8 w-8 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-soft hover:text-ink ${collapsed ? "mx-auto" : ""}`}
        >
          {collapsed ? <ChevronLeft className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
        </button>
      </div>
      {!collapsed && (
        <div className="flex-1 overflow-y-auto p-4 custom-scrollbar">
          {lastTurn ? <ContextEvidenceRail turn={lastTurn} /> : <CommandCenterIntelligence />}
        </div>
      )}
    </aside>
  );
}

/**
 * Live rail content reflecting the most recent completed AI query turn:
 * cited evidence, honest "not configured" capability gaps, and governed
 * provenance. Nothing here is a decision — the answer prepares work for a human.
 */
function ContextEvidenceRail({ turn }: { turn: CompletedAssistantTurn }) {
  const evidenceBlocks = turn.blocks.filter(
    (block): block is Extract<typeof block, { type: "evidence_list" }> => block.type === "evidence_list"
  );
  const notConfiguredBlocks = turn.blocks.filter(
    (block): block is Extract<typeof block, { type: "not_configured" }> => block.type === "not_configured"
  );
  const { modelProvider, promptVersion, agentActionLogId } = turn.response;

  return (
    <div className="space-y-5">
      <section aria-label="Evidence" className="space-y-3">
        <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted-soft">Evidence</p>
        {evidenceBlocks.length > 0 ? (
          evidenceBlocks.map((block, idx) => <EvidenceListBlock key={`evidence-${idx}`} block={block} />)
        ) : (
          <p className="rounded-xl border border-dashed border-line bg-mist px-3 py-3 text-xs text-muted">
            No sources cited for this answer.
          </p>
        )}
      </section>

      {notConfiguredBlocks.length > 0 && (
        <section aria-label="Not configured" className="space-y-3">
          <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted-soft">Capability gaps</p>
          {notConfiguredBlocks.map((block, idx) => (
            <NotConfiguredBlock key={`not-configured-${idx}`} block={block} />
          ))}
        </section>
      )}

      <section aria-label="Provenance" className="rounded-2xl border border-line bg-mist p-3">
        <div className="flex items-center gap-1.5">
          <ShieldCheck className="h-3.5 w-3.5 text-violet" aria-hidden="true" />
          <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-muted-soft">
            Preparation only — not a decision
          </p>
        </div>
        <dl className="mt-2.5 space-y-1.5 text-[11px]">
          <div className="flex items-center justify-between gap-2">
            <dt className="text-muted">Model</dt>
            <dd className="font-mono text-ink">{modelProvider}</dd>
          </div>
          <div className="flex items-center justify-between gap-2">
            <dt className="text-muted">Prompt</dt>
            <dd className="truncate font-mono text-ink" title={promptVersion}>{truncate(promptVersion, 22)}</dd>
          </div>
          <div className="flex items-center justify-between gap-2">
            <dt className="text-muted">Audit log</dt>
            <dd className="font-mono text-ink" title={agentActionLogId}>{agentActionLogId.slice(0, 8)}</dd>
          </div>
        </dl>
      </section>
    </div>
  );
}

function truncate(value: string, max: number): string {
  return value.length > max ? `${value.slice(0, max - 1)}…` : value;
}
