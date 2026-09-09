"use client";

import { useCallback, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { buildSyntheticCommandCenterAnswer } from "@/lib/lovable-parity/syntheticCommandCenterAnswer";

// Mirrors src/InsuranceIntelligence.Application/CommandCenter/CommandCenterQueryContracts.cs.
// Blocks are a discriminated union keyed by `type` (frozen snake_case contract). Unknown
// block types must render a safe fallback, never crash or dump raw JSON.
export type CommandCenterBlock =
  | { type: "text"; markdown: string }
  | { type: "metric_pills"; items: { label: string; value: string; tone: string; detail?: string }[] }
  | { type: "claims_table"; rows: { caseId: string; customer: string; productLine: string; status: string; slaStatus: string; completeness: number; assignedTo: string; href: string }[] }
  | { type: "sla_summary"; items: { caseId: string; customer: string; slaStatus: string; dueLabel: string }[] }
  | { type: "document_gap_summary"; items: { caseId: string; customer: string; missing: string[] }[] }
  | { type: "approval_queue"; items: { approvalId: string; caseId: string; title: string; status: string; href: string }[] }
  | { type: "draft_communication"; draftId: string; preview: string; requiresApproval: boolean }
  | { type: "management_summary"; title: string; sections: { heading: string; body: string }[] }
  | { type: "action_proposal"; actions: { label: string; description: string; targetHref: string; requiresHumanApproval: boolean }[] }
  | { type: "evidence_list"; sources: { label: string; documentName: string; ref: string }[] }
  | { type: "not_configured"; provider: string; adminAction: string }
  | { type: "error"; code: string; safeMessage: string; correlationId: string }
  | { type: string; [k: string]: unknown };

export type CommandCenterAnswer = {
  question: string;
  blocks: CommandCenterBlock[];
  canTriggerDecision: boolean;
  generatedAt: string;
  dataAsOf: string;
  snapshotId: string;
  source: "live" | "synthetic" | "historical";
  limitations: string;
  conversationId?: string;
  turnId?: string;
};

type QueryResponseDto = {
  blocks: CommandCenterBlock[];
  canTriggerDecision: boolean;
  generatedAt: string;
  conversationId?: string;
  turnId?: string;
  dataAsOf?: string;
  snapshotId?: string;
};

export type CommandCenterQueryResult =
  | { ok: true; answer: CommandCenterAnswer }
  | { ok: false; kind: "unauthorized" | "reconcile"; message: string };

export function useCommandCenterQuery() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [answer, setAnswer] = useState<CommandCenterAnswer | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const ask = useCallback(async (question: string, conversationId?: string, clientRequestId?: string): Promise<CommandCenterQueryResult> => {
    const q = question.trim();
    if (!q) return { ok: false, kind: "reconcile", message: "Enter a question before sending." };
    setLoading(true);
    setError(null);
    if (LOVABLE_VISUAL_PARITY_MODE) {
      const parity = buildSyntheticCommandCenterAnswer(q);
      setAnswer(parity);
      setLoading(false);
      return { ok: true, answer: parity };
    }
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/command-center/query`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ question: q, conversationId: conversationId || null, clientRequestId: clientRequestId || null }),
      });
      if (response.status === 403) {
        const message = "You do not have permission to query the Command Center.";
        setError(message);
        return { ok: false, kind: "unauthorized", message };
      }
      if (!response.ok) throw new Error(`Command Center query failed.`);
      const dto = (await response.json()) as QueryResponseDto;
      const result: CommandCenterAnswer = {
        question: q,
        blocks: dto.blocks ?? [],
        canTriggerDecision: Boolean(dto.canTriggerDecision),
        generatedAt: dto.generatedAt,
        dataAsOf: dto.dataAsOf ?? dto.generatedAt,
        snapshotId: dto.snapshotId ?? `command-center-${dto.generatedAt}`,
        source: "live",
        limitations: "Prepared from the current tenant-scoped snapshot. Confirm cited evidence before consequential use.",
        conversationId: dto.conversationId,
        turnId: dto.turnId,
      };
      setAnswer(result);
      return { ok: true, answer: result };
    } catch (err) {
      const message = err instanceof Error ? err.message : "Command Center query failed.";
      setError(message);
      setAnswer(null);
      return { ok: false, kind: "reconcile", message: "IIP could not confirm whether this request was saved. Retry safely to reconcile it before sending another request." };
    } finally {
      setLoading(false);
    }
  }, [role]);

  const reset = useCallback(() => { setAnswer(null); setError(null); }, []);

  return { ask, reset, answer, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
