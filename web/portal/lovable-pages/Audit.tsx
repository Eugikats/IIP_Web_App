"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import { Download, Sparkles, User, Cog, ShieldCheck, Timer, MapPin, Database } from "lucide-react";
import { toast } from "sonner";
import { useAuditEvents, useCompliancePosture, toCsv } from "@/lib/lovable-parity/data/audit";

const icon = { "AI Copilot": Sparkles, "System": Cog } as Record<string, any>;

const TYPE_TONE: Record<string, "info" | "warning" | "success" | "neutral" | "error"> = {
  claim: "info", document: "info", approval: "success", customer: "info",
  communication: "info", integration: "warning", report: "neutral",
  knowledge_document: "success", agent_policy: "info",
};

export default function Audit() {
  const { rows, loading, error } = useAuditEvents();
  const { posture, loading: postureLoading, error: postureError } = useCompliancePosture();

  function exportCsv() {
    if (rows.length === 0) {
      toast("Nothing to export");
      return;
    }
    const blob = new Blob([toCsv(rows)], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `iip-audit-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    toast.success("Audit CSV exported");
  }

  return (
    <>
      <SectionHeader
        eyebrow="Reports & Audit"
        title="Audit trail"
        sub="Every AI action, human decision, integration event, and communication — evidence-linked and regulator-ready."
        action={
          <Button variant="primary" onClick={exportCsv} icon={<Download aria-hidden="true" />}>
            Export CSV
          </Button>
        }
      />

      <div className="mb-5 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
        <GlassCard>
          <div className="flex items-start justify-between gap-3">
            <div>
              <div className="text-[11px] font-bold uppercase tracking-[0.12em] text-dls-text-muted">Retention</div>
              <div className="mt-2 text-xl font-bold text-dls-text-primary">
                {postureLoading ? "…" : posture ? `${posture.retention.auditRetentionDays} days` : "Not configured"}
              </div>
            </div>
            <ShieldCheck className="h-4 w-4 text-dls-info-text" aria-hidden="true" />
          </div>
          <div className="mt-3">
            <Badge tone={posture?.retention.meetsMinimum ? "success" : "warning"}>
              {posture?.retention.meetsMinimum ? "Minimum met" : "Needs review"}
            </Badge>
          </div>
        </GlassCard>

        <GlassCard>
          <div className="flex items-start justify-between gap-3">
            <div>
              <div className="text-[11px] font-bold uppercase tracking-[0.12em] text-dls-text-muted">Consent</div>
              <div className="mt-2 text-xl font-bold text-dls-text-primary">
                {postureLoading ? "…" : posture ? `${posture.consent.consentedRecords}/${posture.consent.onboardingRecords}` : "Not configured"}
              </div>
            </div>
            <Database className="h-4 w-4 text-dls-primary" aria-hidden="true" />
          </div>
          <div className="mt-3">
            <Badge tone={posture?.consent.blockedConsent ? "error" : posture?.consent.pendingConsent ? "warning" : "success"}>
              {posture?.consent.blockedConsent ? "Blocked" : posture?.consent.pendingConsent ? "Pending" : "Consented"}
            </Badge>
          </div>
        </GlassCard>

        <GlassCard>
          <div className="flex items-start justify-between gap-3">
            <div>
              <div className="text-[11px] font-bold uppercase tracking-[0.12em] text-dls-text-muted">SLA posture</div>
              <div className="mt-2 text-xl font-bold text-dls-text-primary">
                {postureLoading ? "…" : posture ? `${posture.sla.atRisk + posture.sla.breached} attention` : "Not configured"}
              </div>
            </div>
            <Timer className="h-4 w-4 text-dls-warning-text" aria-hidden="true" />
          </div>
          <div className="mt-3 flex flex-wrap gap-2">
            <Badge tone={posture?.sla.breached ? "error" : "neutral"}>{posture?.sla.breached ?? 0} breached</Badge>
            <Badge tone={posture?.sla.atRisk ? "warning" : "neutral"}>{posture?.sla.atRisk ?? 0} at risk</Badge>
          </div>
        </GlassCard>

        <GlassCard>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="text-[11px] font-bold uppercase tracking-[0.12em] text-dls-text-muted">Data residency</div>
              <div className="mt-2 text-xl font-bold text-dls-text-primary">
                {postureLoading ? "…" : posture?.dataResidency.azureRegion ?? "Not configured"}
              </div>
            </div>
            <MapPin
              className={"h-4 w-4 " + (posture?.dataResidency.status === "Compliant" ? "text-dls-success-text" : posture?.dataResidency.status === "Mismatch" ? "text-dls-danger-text" : "text-dls-text-muted")}
              aria-hidden="true"
            />
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Badge tone={posture?.dataResidency.status === "Compliant" ? "success" : posture?.dataResidency.status === "Mismatch" ? "error" : "neutral"}>
              {posture?.dataResidency.status ?? "Not configured"}
            </Badge>
            <span className="text-[12px] text-dls-text-secondary">
              {posture ? posture.dataResidency.residencyCountry + " / " + posture.dataResidency.azureGeography : postureError ?? "Residency policy not configured"}
            </span>
          </div>
          <div className="mt-2 break-words text-[11.5px] text-dls-text-muted">
            AI processing: {posture ? posture.dataResidency.azureOpenAiDeploymentType + " (" + posture.dataResidency.mode + ")" : "Not configured"}
          </div>
          <div className="mt-2 break-words text-[11.5px] text-dls-text-muted">
            Approval: {posture?.dataResidency.approvalReference ?? "Not configured"}
          </div>
        </GlassCard>
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={6} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Audit trail could not be loaded" message={error} /></GlassCard>
      ) : rows.length === 0 ? (
        <GlassCard><EmptyStateSurface title="No audit events" message="Audit events appear here as AI actions, human decisions, and integration events are recorded for this workspace." /></GlassCard>
      ) : (
        <Table label="Audit trail">
          <TableHead>
            <TableRow>
              <TableHeaderCell>Time</TableHeaderCell>
              <TableHeaderCell>Actor</TableHeaderCell>
              <TableHeaderCell>Action</TableHeaderCell>
              <TableHeaderCell>Target</TableHeaderCell>
              <TableHeaderCell>Type</TableHeaderCell>
              <TableHeaderCell>Correlation</TableHeaderCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((e) => {
              const Icon = icon[e.actor] ?? User;
              const knownType = TYPE_TONE[e.targetType] !== undefined;
              const tone = TYPE_TONE[e.targetType] ?? "neutral";
              return (
                <TableRow key={e.id}>
                  <TableCell className="text-dls-text-muted">{e.time}</TableCell>
                  <TableCell>
                    <span className="inline-flex items-center gap-2 text-dls-text-primary"><Icon className="h-3.5 w-3.5 text-dls-info-text" aria-hidden="true" />{e.actor}</span>
                  </TableCell>
                  <TableCell className="max-w-[220px] truncate text-dls-text-secondary">{e.action}</TableCell>
                  <TableCell className="max-w-[220px] truncate text-dls-text-secondary">{e.target}</TableCell>
                  <TableCell><Badge tone={tone}>{knownType ? e.targetType : "Other"}</Badge></TableCell>
                  <TableCell className="max-w-[120px] truncate text-[11.5px] text-dls-text-muted">{e.correlation}</TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}
      <div className="mt-3 text-[11.5px] text-dls-text-muted">Model / prompt metadata is recorded per AI run. Logs never contain secrets or unnecessary PII.</div>
    </>
  );
}
