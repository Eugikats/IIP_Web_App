import { expect, test } from "@playwright/test";
import { syntheticWorkspaceSnapshot } from "../../lib/lovable-parity/syntheticWorkspaceSnapshot";
import { buildSyntheticCommandCenterAnswer } from "../../lib/lovable-parity/syntheticCommandCenterAnswer";
import { formatProviderStatusLabel, isProviderHealthy } from "../../lib/providerHealth";

test("synthetic claims, reports, and Command Center metrics share one snapshot", () => {
  const snapshot = syntheticWorkspaceSnapshot;
  const answer = buildSyntheticCommandCenterAnswer("What needs attention?");
  const metricBlock = answer.blocks.find((block) => block.type === "metric_pills");
  const metricItems = metricBlock && "items" in metricBlock && Array.isArray(metricBlock.items)
    ? metricBlock.items as Array<{ label: string; value: string }>
    : [];

  expect(snapshot.source).toBe("synthetic");
  expect(snapshot.limitations.length).toBeGreaterThan(0);
  expect(Number.isNaN(Date.parse(snapshot.dataAsOf))).toBe(false);
  expect(snapshot.operationsSummary.openClaims).toBe(snapshot.claims.filter((claim) => claim.status !== "Closed").length);
  expect(snapshot.operationsSummary.claimsByStatus.reduce((total, item) => total + item.count, 0)).toBe(snapshot.claims.length);
  expect(metricItems.length).toBeGreaterThan(0);
  const values = Object.fromEntries(metricItems.map((item) => [item.label, item.value]));
  expect(values["Open claims"]).toBe(String(snapshot.operationsSummary.openClaims));
  expect(values["SLA at risk"]).toBe(String(snapshot.operationsSummary.slaAtRisk));
  expect(answer.snapshotId).toBe(snapshot.snapshotId);
  expect(answer.dataAsOf).toBe(snapshot.dataAsOf);
});

test("only an exact healthy provider response is labelled operational", () => {
  for (const provider of syntheticWorkspaceSnapshot.providerHealth.providers) {
    const label = formatProviderStatusLabel(provider.status);
    if (label === "Operational") expect(provider.status).toBe("healthy");
    if (["missing", "unknown", "degraded", "disabled", "not_configured"].includes(provider.status)) {
      expect(label).not.toBe("Operational");
      expect(isProviderHealthy(label)).toBe(false);
    }
  }
});
