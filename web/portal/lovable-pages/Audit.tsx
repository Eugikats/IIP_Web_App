"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Download, Sparkles, User, Cog, FileClock, ShieldCheck, Timer, MapPin, Database } from "lucide-react";
import { toast } from "sonner";
import { useAuditEvents, useCompliancePosture, toCsv } from "@/lib/lovable-parity/data/audit";

const icon = { "AI Copilot": Sparkles, "System": Cog } as Record<string, any>;

const TYPE_TONE: Record<string, "info" | "violet" | "warning" | "success" | "neutral" | "error"> = {
  claim: "info", document: "info", approval: "success", customer: "info",
  communication: "violet", integration: "warning", report: "neutral",
  knowledge_document: "success", agent_policy: "violet",
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
          <button onClick={exportCsv} className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2.5 shadow-l2">
            <Download className="h-4 w-4" /> Export CSV
          </button>
        }
      />

      <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4 mb-5">
        <GlassCard className="p-4">
          <div className="flex items-start justify-between gap-3">
            <div>
              <div className="text-[11px] uppercase tracking-[0.12em] text-muted">Retention</div>
              <div className="mt-2 text-xl font-semibold text-ink">
                {postureLoading ? "..." : posture ? `${posture.retention.auditRetentionDays} days` : "Not configured"}
              </div>
            </div>
            <ShieldCheck className="h-4 w-4 text-info" />
          </div>
          <div className="mt-3">
            <Badge tone={posture?.retention.meetsMinimum ? "success" : "warning"}>
              {posture?.retention.meetsMinimum ? "Minimum met" : "Needs review"}
            </Badge>
          </div>
        </GlassCard>

        <GlassCard className="p-4">
          <div className="flex items-start justify-between gap-3">
            <div>
              <div className="text-[11px] uppercase tracking-[0.12em] text-muted">Consent</div>
              <div className="mt-2 text-xl font-semibold text-ink">
                {postureLoading ? "..." : posture ? `${posture.consent.consentedRecords}/${posture.consent.onboardingRecords}` : "Not configured"}
              </div>
            </div>
            <Database className="h-4 w-4 text-violet" />
          </div>
          <div className="mt-3">
            <Badge tone={posture?.consent.blockedConsent ? "error" : posture?.consent.pendingConsent ? "warning" : "success"}>
              {posture?.consent.blockedConsent ? "Blocked" : posture?.consent.pendingConsent ? "Pending" : "Consented"}
            </Badge>
          </div>
        </GlassCard>

        <GlassCard className="p-4">
          <div className="flex items-start justify-between gap-3">
            <div>
              <div className="text-[11px] uppercase tracking-[0.12em] text-muted">SLA posture</div>
              <div className="mt-2 text-xl font-semibold text-ink">
                {postureLoading ? "..." : posture ? `${posture.sla.atRisk + posture.sla.breached} attention` : "Not configured"}
              </div>
            </div>
            <Timer className="h-4 w-4 text-warning" />
          </div>
          <div className="mt-3 flex flex-wrap gap-2">
            <Badge tone={posture?.sla.breached ? "error" : "neutral"}>{posture?.sla.breached ?? 0} breached</Badge>
            <Badge tone={posture?.sla.atRisk ? "warning" : "neutral"}>{posture?.sla.atRisk ?? 0} at risk</Badge>
          </div>
        </GlassCard>

        <GlassCard className="p-4">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="text-[11px] uppercase tracking-[0.12em] text-muted">Data residency</div>
              <div className="mt-2 text-xl font-semibold text-ink">
                {postureLoading ? "..." : posture?.dataResidency.azureRegion ?? "Not configured"}
              </div>
            </div>
            <MapPin className={"h-4 w-4 " + (posture?.dataResidency.status === "Compliant" ? "text-success" : posture?.dataResidency.status === "Mismatch" ? "text-danger" : "text-muted")} />
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Badge tone={posture?.dataResidency.status === "Compliant" ? "success" : posture?.dataResidency.status === "Mismatch" ? "error" : "neutral"}>
              {posture?.dataResidency.status ?? "Not configured"}
            </Badge>
            <span className="text-[12px] text-muted">
              {posture ? posture.dataResidency.residencyCountry + " / " + posture.dataResidency.azureGeography : postureError ?? "Residency policy not configured"}
            </span>
          </div>
          <div className="mt-2 break-words text-[11.5px] text-muted">
            AI processing: {posture ? posture.dataResidency.azureOpenAiDeploymentType + " (" + posture.dataResidency.mode + ")" : "Not configured"}
          </div>
          <div className="mt-2 break-words text-[11.5px] text-muted">
            Approval: {posture?.dataResidency.approvalReference ?? "Not configured"}
          </div>
        </GlassCard>
      </div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-muted">Loading audit trail…</div></GlassCard>
      ) : rows.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <FileClock className="h-6 w-6 text-muted" />
            <div className="text-ink font-medium">No audit events</div>
            <div className="text-[12.5px] text-muted max-w-md">
              {error ? error : "Audit events appear here as AI actions, human decisions, and integration events are recorded for this workspace."}
            </div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard className="p-0 overflow-hidden">
          <div className="grid grid-cols-12 text-[11px] uppercase tracking-[0.12em] text-muted px-4 py-3 border-b border-line">
            <div className="col-span-2">Time</div>
            <div className="col-span-2">Actor</div>
            <div className="col-span-3">Action</div>
            <div className="col-span-3">Target</div>
            <div className="col-span-1">Type</div>
            <div className="col-span-1">Correlation</div>
          </div>
          {rows.map((e) => {
            const Icon = icon[e.actor] ?? User;
            const knownType = TYPE_TONE[e.targetType] !== undefined;
            const tone = TYPE_TONE[e.targetType] ?? "neutral";
            return (
              <div key={e.id} className="grid grid-cols-12 items-center px-4 py-3 border-b border-line/60 hover:bg-white/70 text-[13px]">
                <div className="col-span-2 text-muted">{e.time}</div>
                <div className="col-span-2 inline-flex items-center gap-2 text-ink"><Icon className="h-3.5 w-3.5 text-info" />{e.actor}</div>
                <div className="col-span-3 text-ink-soft truncate">{e.action}</div>
                <div className="col-span-3 text-ink-soft truncate">{e.target}</div>
                <div className="col-span-1"><Badge tone={tone}>{knownType ? e.targetType : "Other"}</Badge></div>
                <div className="col-span-1 text-[11.5px] text-muted truncate">{e.correlation}</div>
              </div>
            );
          })}
        </GlassCard>
      )}
      <div className="mt-3 text-[11.5px] text-muted">Model / prompt metadata is recorded per AI run. Logs never contain secrets or unnecessary PII.</div>
    </>
  );
}
