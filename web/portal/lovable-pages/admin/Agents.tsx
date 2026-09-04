"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
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
      return <Badge tone="violet">AI agent (model-backed)</Badge>;
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
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading agent registry…</div></GlassCard>
      ) : agents.length === 0 ? (
        <GlassCard><div className="p-10 text-center text-stone"><div className="text-navy font-medium">No agents registered</div><div className="text-[12.5px] mt-1">{error ?? "The agent catalog is empty for this workspace."}</div></div></GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
                <th className="p-3">Agent</th><th className="p-3">Purpose</th><th className="p-3">Implementation</th><th className="p-3">Risk</th><th className="p-3">Human approval</th><th className="p-3">State</th><th className="p-3 text-right">Kill switch</th>
              </tr>
            </thead>
            <tbody>
              {agents.map((a) => {
                const implemented = a.implementationKind !== "NotImplemented";
                return (
                <tr key={a.key} className="border-t border-cloud/70 hover:bg-white/50">
                  <td className="p-3"><div className="text-navy font-medium">{a.name}</div><div className="text-[10.5px] text-stone">{a.key}</div></td>
                  <td className="p-3 text-slate2 max-w-[320px]">
                    {a.purpose}
                    {a.tools.length > 0 ? (
                      <div className="mt-1 text-[10.5px] text-stone">Tools: {a.tools.join(", ")}</div>
                    ) : null}
                  </td>
                  <td className="p-3"><ImplementationPill kind={a.implementationKind} /></td>
                  <td className="p-3"><RiskPill r={a.riskLevel} /></td>
                  <td className="p-3">{a.humanApprovalRequired ? <Badge tone="violet">Required</Badge> : <Badge tone="neutral">Not required</Badge>}</td>
                  <td className="p-3">{a.enabled ? <Badge tone="success">Enabled</Badge> : <Badge tone="warning">Disabled</Badge>}</td>
                  <td className="p-3 text-right">
                    {implemented ? (
                      <button
                        onClick={() => toggle(a.key, a.name, a.enabled)}
                        className={`text-[11.5px] rounded-lg px-3 py-1.5 border ${a.enabled ? "border-error/30 bg-white text-error" : "border-success/30 bg-white text-success"}`}
                      >
                        {a.enabled ? "Disable" : "Enable"}
                      </button>
                    ) : (
                      <span className="text-[11.5px] text-stone" title="No code path in the backend invokes this agent — there is nothing for a switch to control.">
                        Not implemented
                      </span>
                    )}
                  </td>
                </tr>
                );
              })}
            </tbody>
          </table>
        </GlassCard>
      )}
      <div className="mt-3 text-[11.5px] text-stone">Kill-switch changes require a reason and are written to the audit trail. A disabled agent fails closed at invocation.</div>
    </>
  );
}
