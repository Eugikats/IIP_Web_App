"use client";

import { useEffect, useState } from "react";
import { DataTable } from "@/components/DataTable";
import { MetricCard } from "@/components/MetricCard";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/Badges";
import { ErrorState, LoadingState } from "@/components/States";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { motion } from "framer-motion";

type Summary = {
  openClaims: number;
  newClaimsToday: number;
  incompleteClaims: number;
  averageCompletenessScore: number;
  pendingApprovals: number;
  openTasks: number;
  customerUpdatesPending: number;
  assistantQuestionsAnswered: number;
  escalationsRequired: number;
  slaAtRisk: number;
  slaBreached: number;
  claimsByStatus: Array<{ label: string; count: number }>;
  workQueueSummary: Array<{ label: string; count: number }>;
};

export function ReportsClient() {
  const { role, userId } = useRole();
  const [summary, setSummary] = useState<Summary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const response = await fetch(`${apiBaseUrl}/api/v1/reports/operations-summary`, {
          headers: scopedHeaders(role, userId)
        });
        if (!response.ok) throw new Error(`Operations summary failed with HTTP ${response.status}.`);
        setSummary((await response.json()) as Summary);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Operations summary failed.");
      }
    }
    load();
  }, [role, userId]);

  if (error) return <ErrorState message={error} />;
  if (!summary) return <LoadingState label="Loading operations summary..." />;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }}>
      <PageHeader
        eyebrow="Reports"
        title="Operations summary"
        description="Workspace-scoped workflow reporting. Power BI and deeper external analytics remain future integrations and are not implied by this view."
      />
      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard label="Open claims" value={summary.openClaims} />
        <MetricCard label="Incomplete claims" value={summary.incompleteClaims} tone={summary.incompleteClaims > 0 ? "warning" : "success"} />
        <MetricCard label="Open tasks" value={summary.openTasks} />
        <MetricCard label="SLA breached" value={summary.slaBreached} tone={summary.slaBreached > 0 ? "danger" : "success"} />
      </section>
      <div className="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard label="Pending approvals" value={summary.pendingApprovals} tone={summary.pendingApprovals > 0 ? "warning" : "success"} />
        <MetricCard label="Customer updates pending" value={summary.customerUpdatesPending} />
        <MetricCard label="Assistant answers" value={summary.assistantQuestionsAnswered} />
        <MetricCard label="Average completeness" value={`${summary.averageCompletenessScore}%`} tone={summary.averageCompletenessScore >= 80 ? "success" : "warning"} />
      </div>
      <div className="mt-5 grid gap-5 lg:grid-cols-2">
        <StatusTable title="Claims by status" rows={summary.claimsByStatus} />
        <StatusTable title="Work queue summary" rows={summary.workQueueSummary} />
      </div>
      <div className="mt-5 grid gap-5 lg:grid-cols-[1.2fr_0.8fr]">
        <SectionCard eyebrow="Insights" title="Operational reading" description="This interpretation is based on backend report aggregates rather than external BI telemetry.">
          <div className="grid gap-3 md:grid-cols-2">
            <ReadingRow label="New claims today" value={summary.newClaimsToday} />
            <ReadingRow label="SLA at risk" value={summary.slaAtRisk} tone={summary.slaAtRisk > 0 ? "warning" : "success"} />
            <ReadingRow label="Escalations required" value={summary.escalationsRequired} tone={summary.escalationsRequired > 0 ? "warning" : "success"} />
            <ReadingRow label="Open approvals" value={summary.pendingApprovals} tone={summary.pendingApprovals > 0 ? "warning" : "success"} />
          </div>
        </SectionCard>
        <SectionCard eyebrow="Compliance" title="Provider honesty" description="Keep report interpretation aligned with the current platform mode.">
          <div className="space-y-3 text-sm leading-6 text-muted">
            <div className="flex items-center justify-between gap-3 rounded-xl border border-line bg-mist p-4">
              <span className="font-mono text-xs text-ink">Storage and workflow state</span>
              <StatusBadge label="Tenant workflow data" />
            </div>
            <div className="flex items-center justify-between gap-3 rounded-xl border border-line bg-mist p-4">
              <span className="font-mono text-xs text-ink">AI and retrieval</span>
              <StatusBadge label="Provider abstracted" />
            </div>
            <div className="flex items-center justify-between gap-3 rounded-xl border border-line bg-mist p-4">
              <span className="font-mono text-xs text-ink">External analytics</span>
              <StatusBadge label="No live Power BI yet" />
            </div>
          </div>
        </SectionCard>
      </div>
      <div className="mt-5">
        <SectionCard
          eyebrow="Exports"
          title="Basic export states"
          description="Exports are represented truthfully. The portal does not imply generated files, live Power BI, or customer-facing extracts when those services are unavailable."
        >
          <div className="grid gap-3 md:grid-cols-3">
            <ExportState
              label="Operations CSV"
              status="Not configured"
              description="Requires an approved export endpoint and retention policy."
            />
            <ExportState
              label="Power BI"
              status="Not configured"
              description="Embedded analytics are unavailable until a tenant report workspace is connected."
            />
            <ExportState
              label="Audit packs"
              status="Available in Compliance"
              description="Case evidence packs are previewed from compliance-safe audit APIs."
            />
          </div>
        </SectionCard>
      </div>
    </motion.div>
  );
}

function StatusTable({ title, rows }: { title: string; rows: Array<{ label: string; count: number }> }) {
  return (
    <SectionCard title={title}>
      <DataTable>
        <thead className="bg-mist text-left text-[10px] font-bold uppercase tracking-[0.2em] text-muted">
          <tr>
            <th className="px-4 py-3 border-b border-line">Label</th>
            <th className="px-4 py-3 text-right border-b border-line">Count</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line bg-surface/50">
          {rows.length === 0 ? (
            <tr><td className="px-4 py-4 text-sm text-muted" colSpan={2}>No data yet.</td></tr>
          ) : rows.map((row) => (
            <tr key={row.label} className="hover:bg-mist/70 transition-colors">
              <td className="px-4 py-3 text-sm font-semibold text-ink">{row.label}</td>
              <td className="px-4 py-3 text-right text-sm font-semibold text-ink font-mono">{row.count}</td>
            </tr>
          ))}
        </tbody>
      </DataTable>
    </SectionCard>
  );
}

function ReadingRow({ label, value, tone = "info" }: { label: string; value: number; tone?: "info" | "warning" | "success" }) {
  const toneClass = {
    info: "border-accent/30 bg-accent/10 text-accent shadow-glow-cyan",
    warning: "border-warning/30 bg-warning/10 text-warning shadow-glow-warning",
    success: "border-success/30 bg-success/10 text-success shadow-glow-success"
  }[tone];

  return (
    <div className={`rounded-xl border p-4 ${toneClass}`}>
      <p className="text-[10px] font-bold uppercase tracking-[0.2em]">{label}</p>
      <p className="mt-3 text-2xl font-semibold font-mono">{value}</p>
    </div>
  );
}

function ExportState({ label, status, description }: { label: string; status: string; description: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm font-semibold text-ink">{label}</p>
        <StatusBadge label={status} />
      </div>
      <p className="mt-3 text-xs leading-5 text-muted">{description}</p>
    </div>
  );
}
