import type { CommandCenterAnswer } from "@/lib/lovable-parity/data/commandCenterQuery";
import { syntheticWorkspaceSnapshot } from "@/lib/lovable-parity/syntheticWorkspaceSnapshot";

export function buildSyntheticCommandCenterAnswer(question: string): CommandCenterAnswer {
  const snapshot = syntheticWorkspaceSnapshot;
  return {
    question,
    canTriggerDecision: false,
    generatedAt: snapshot.dataAsOf,
    dataAsOf: snapshot.dataAsOf,
    snapshotId: snapshot.snapshotId,
    source: snapshot.source,
    limitations: snapshot.limitations,
    blocks: [
      { type: "text", markdown: "Synthetic workspace briefing for interaction testing. Verify live workspace data before operational use." },
      { type: "metric_pills", items: [
        { label: "Open claims", value: String(snapshot.operationsSummary.openClaims), tone: "info" },
        { label: "SLA at risk", value: String(snapshot.operationsSummary.slaAtRisk), tone: "warning" },
        { label: "Pending approvals", value: String(snapshot.operationsSummary.pendingApprovals), tone: "info" },
      ] },
    ],
  };
}
