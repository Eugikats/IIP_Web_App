"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { toast } from "sonner";
import { useAgents } from "@/lib/lovable-parity/data/agents";

function RiskPill({ r }: { r: string }) {
  const tone = r === "High" ? "error" : r === "Medium" ? "warning" : "success";
  return <Badge tone={tone as any}>{r}</Badge>;
}

// WO-11: honest labels for what an agent actually is, verified against real backend call sites —
// never implies every catalog entry is a working AI agent.
function ImplementationPill({ kind }: { kind: string }) {
  switch (kind) {
    case "ModelBacked":
      return <Badge tone="info">AI agent (model-backed)</Badge>;
    case "AiServiceBacked":
      return <Badge tone="info">AI service (non-chat)</Badge>;
    case "RuleBased":
      return <Badge tone="neutral">Rule-based (no AI call)</Badge>;
    default:
      return <Badge tone="error">Not implemented</Badge>;
  }
}

export default function Agents() {
  const { agents, loading, error, setKillSwitch } = useAgents();

  async function toggle(key: string, name: string, currentlyEnabled: boolean) {
    const action = currentlyEnabled ? "disable" : "enable";
    const reason = window.prompt(`Reason to ${action} "${name}" (recorded in the audit trail):`);
    if (!reason || !reason.trim()) return;
    const ok = await setKillSwitch(key, !currentlyEnabled, reason.trim());
    toast[ok ? "success" : "error"](ok ? `Agent ${action}d — recorded in audit` : "Kill-switch update failed");
  }

  return (
    <>
      <SectionHeader
        eyebrow="Admin · AI & Agent Management"
        title="Agent registry"
        sub="Every agent is versioned, permissioned, and auditable. Human approval remains required for regulated actions."
      />
      <div className="mb-4"><GuardrailBanner text="Disabled agents cannot be invoked. The kill switch is a governed, audited control — it constrains agents; it never grants them autonomy over a decision." /></div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={5} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Agent registry could not be loaded" message={error} /></GlassCard>
      ) : agents.length === 0 ? (
        <GlassCard><EmptyStateSurface title="No agents registered" message="The agent catalog is empty for this workspace." /></GlassCard>
      ) : (
        <Table label="Agent registry">
          <TableHead>
            <TableRow>
              <TableHeaderCell>Agent</TableHeaderCell>
              <TableHeaderCell>Purpose</TableHeaderCell>
              <TableHeaderCell>Implementation</TableHeaderCell>
              <TableHeaderCell>Risk</TableHeaderCell>
              <TableHeaderCell>Human approval</TableHeaderCell>
              <TableHeaderCell>State</TableHeaderCell>
              <TableHeaderCell numeric>Kill switch</TableHeaderCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {agents.map((a) => {
              const implemented = a.implementationKind !== "NotImplemented";
              return (
                <TableRow key={a.key}>
                  <TableCell>
                    <div className="font-semibold text-dls-text-primary">{a.name}</div>
                    <div className="text-[11px] text-dls-text-muted">{a.key}</div>
                  </TableCell>
                  <TableCell className="max-w-[320px] text-dls-text-secondary">
                    {a.purpose}
                    {a.tools.length > 0 ? (
                      <div className="mt-1 text-[11px] text-dls-text-muted">Tools: {a.tools.join(", ")}</div>
                    ) : null}
                  </TableCell>
                  <TableCell><ImplementationPill kind={a.implementationKind} /></TableCell>
                  <TableCell><RiskPill r={a.riskLevel} /></TableCell>
                  <TableCell>{a.humanApprovalRequired ? <Badge tone="info">Required</Badge> : <Badge tone="neutral">Not required</Badge>}</TableCell>
                  <TableCell>{a.enabled ? <Badge tone="success">Enabled</Badge> : <Badge tone="warning">Disabled</Badge>}</TableCell>
                  <TableCell numeric>
                    {implemented ? (
                      <button
                        type="button"
                        onClick={() => toggle(a.key, a.name, a.enabled)}
                        className={`dls-focus-outline rounded-dls-control border px-3 py-1.5 text-[11.5px] font-bold transition-colors duration-dls-fast ease-dls ${a.enabled ? "border-dls-border-input bg-dls-surface text-dls-danger-text hover:bg-dls-bg-page" : "border-dls-border-input bg-dls-surface text-dls-success-text hover:bg-dls-bg-page"}`}
                      >
                        {a.enabled ? "Disable" : "Enable"}
                      </button>
                    ) : (
                      <span className="text-[11.5px] text-dls-text-muted" title="No code path in the backend invokes this agent — there is nothing for a switch to control.">
                        Not implemented
                      </span>
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}
      <div className="mt-3 text-[11.5px] text-dls-text-muted">Kill-switch changes require a reason and are written to the audit trail. A disabled agent fails closed at invocation.</div>
    </>
  );
}
