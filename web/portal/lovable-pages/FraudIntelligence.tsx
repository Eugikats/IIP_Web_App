"use client";
import { useState } from "react";
import { toast } from "sonner";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { AlertTriangle, ShieldAlert } from "lucide-react";
import {
  useFraudSignals, OUTCOME_LABEL, SELECTABLE_OUTCOMES, type FraudSeverity, type FraudSignalView,
} from "@/lib/lovable-parity/data/fraud";

const badgeTone: Record<FraudSeverity, "error" | "warning" | "violet"> = {
  Investigation: "error",
  Anomaly: "warning",
  Advisory: "violet",
};

const badgeLabel: Record<FraudSeverity, string> = {
  Investigation: "Requires investigation",
  Anomaly: "Potential anomaly",
  Advisory: "Advisory signal",
};

const STATUS_LABEL: Record<string, string> = {
  NotTriaged: "Not yet triaged",
  Triaged: "Triaged",
  UnderInvestigation: "Under investigation",
  OutcomeRecorded: "Outcome recorded",
  Closed: "Closed",
};

const STATUS_TONE: Record<string, "neutral" | "info" | "warning" | "success"> = {
  NotTriaged: "neutral",
  Triaged: "info",
  UnderInvestigation: "warning",
  OutcomeRecorded: "info",
  Closed: "success",
};

type PendingAction =
  | { kind: "triage"; signal: FraudSignalView }
  | { kind: "assign"; signal: FraudSignalView; assignedToUserId: string }
  | { kind: "outcome"; signal: FraudSignalView; outcome: string; notes: string }
  | { kind: "close"; signal: FraudSignalView };

export default function FraudIntelligence() {
  const { signals, loading, error, counts, triage, assign, recordOutcome, close } = useFraudSignals();
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [assigneeDraft, setAssigneeDraft] = useState<Record<string, string>>({});
  const [outcomeDraft, setOutcomeDraft] = useState<Record<string, { outcome: string; notes: string }>>({});

  function allows(signal: FraudSignalView, action: string) {
    return signal.disposition.allowedActions.some((a) => a.action === action);
  }
  function blockedReason(signal: FraudSignalView, action: string) {
    return signal.disposition.blockedActions.find((a) => a.action === action)?.reason ?? null;
  }

  async function handleConfirm(auditReason: string) {
    if (!pending) return;
    const result = pending.kind === "triage" ? await triage(pending.signal, auditReason)
      : pending.kind === "assign" ? await assign(pending.signal.signalId, pending.assignedToUserId, auditReason)
      : pending.kind === "outcome" ? await recordOutcome(pending.signal.signalId, pending.outcome, pending.notes, auditReason)
      : await close(pending.signal.signalId, auditReason);
    if (result.ok) {
      toast.success(
        pending.kind === "triage" ? "Signal triaged." :
        pending.kind === "assign" ? "Signal assigned for investigation." :
        pending.kind === "outcome" ? "Outcome recorded." : "Signal closed.",
      );
    } else {
      toast.error(result.message ?? `${pending.kind} failed.`);
    }
    setPending(null);
  }

  return (
    <>
      <SectionHeader eyebrow="Fraud Intelligence" title="Advisory risk signals" sub="Signals are advisory and require human investigation. IIP does not accuse customers of fraud or make fraud determinations." />
      <div className="mb-4"><GuardrailBanner text="Use safer wording: Fraud signal detected · Requires investigation · Potential anomaly · Advisory signal. Never: fraud confirmed." /></div>

      <div className="grid md:grid-cols-4 gap-3 mb-6">
        {[
          { l: "Total advisory signals", v: counts.total, t: "violet" as const },
          { l: "Requires investigation", v: counts.investigation, t: "error" as const },
          { l: "Potential anomalies", v: counts.anomaly, t: "warning" as const },
          { l: "Advisory only", v: counts.advisory, t: "info" as const },
        ].map(k => (
          <div key={k.l} className="glass-subtle p-4">
            <div className="text-[11.5px] text-muted">{k.l}</div>
            <div className="mt-1 text-[26px] leading-none font-semibold text-ink">{k.v}</div>
            <Badge tone={k.t} className="mt-2">Advisory</Badge>
          </div>
        ))}
      </div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-muted">Computing advisory signals…</div></GlassCard>
      ) : signals.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <ShieldAlert className="h-6 w-6 text-muted" />
            <div className="text-ink font-medium">No advisory signals</div>
            <div className="text-[12.5px] text-muted max-w-md">
              {error
                ? error
                : "The rule engine found no advisory patterns in the current tenant-scoped claims. No signal is shown without a matching rule result and evidence reference."}
            </div>
          </div>
        </GlassCard>
      ) : (
        <div className="grid md:grid-cols-2 gap-3">
          {signals.map((s) => {
            const status = s.disposition.status;
            return (
            <GlassCard key={s.id} className="hover-lift">
              <div className="flex items-start gap-3">
                <div className="h-10 w-10 rounded-xl bg-violet/10 text-violet grid place-items-center"><AlertTriangle className="h-4 w-4" /></div>
                <div className="flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <div className="text-[14px] font-semibold text-ink">{s.title}</div>
                    <Badge tone={badgeTone[s.severity]}>{badgeLabel[s.severity]}</Badge>
                    <Badge tone={STATUS_TONE[status] ?? "neutral"}>{STATUS_LABEL[status] ?? status}</Badge>
                  </div>
                  <div className="text-[12.5px] text-ink-soft mt-1">{s.detail}</div>
                  <div className="mt-2 text-[11.5px] text-muted">Linked: {s.linked}</div>
                  {s.evidence.length > 0 ? (
                    <div className="mt-1.5 flex flex-wrap gap-1">
                      {s.evidence.slice(0, 3).map((e, idx) => (
                        <span key={idx} className="rounded-full bg-white/70 border border-line px-2 py-0.5 text-[10.5px] text-muted">{e}</span>
                      ))}
                    </div>
                  ) : null}
                  {s.disposition.assignedToUserId && (
                    <div className="mt-1.5 text-[11px] text-muted">Assigned to: {s.disposition.assignedToUserId}</div>
                  )}
                  {s.disposition.outcome && (
                    <div className="mt-1 text-[11px] text-muted">Outcome: {OUTCOME_LABEL[s.disposition.outcome] ?? s.disposition.outcome}{s.disposition.outcomeNotes ? ` — ${s.disposition.outcomeNotes}` : ""}</div>
                  )}
                </div>
              </div>
              <div className="mt-3 flex items-center justify-between flex-wrap gap-2">
                <div className="inline-flex items-center gap-1.5 text-[11.5px] text-[hsl(38_92%_36%)]"><AlertTriangle className="h-3 w-3" /> Advisory only · human review required</div>
                <div className="flex items-center gap-1.5 flex-wrap">
                  {allows(s, "triageFraudSignal") && (
                    <button onClick={() => setPending({ kind: "triage", signal: s })} className="rounded-lg border border-line bg-white px-2.5 py-1.5 text-[11.5px] font-medium text-ink hover:bg-line/40">Triage</button>
                  )}
                  {allows(s, "assignFraudSignal") && (
                    <div className="flex items-center gap-1">
                      <input
                        value={assigneeDraft[s.id] ?? ""}
                        onChange={(e) => setAssigneeDraft((cur) => ({ ...cur, [s.id]: e.target.value }))}
                        placeholder="Assignee user ID"
                        className="rounded-lg border border-line bg-white px-2 py-1.5 text-[11px] text-ink w-32"
                      />
                      <button
                        disabled={!assigneeDraft[s.id]?.trim()}
                        onClick={() => setPending({ kind: "assign", signal: s, assignedToUserId: assigneeDraft[s.id].trim() })}
                        className="rounded-lg border border-line bg-white px-2.5 py-1.5 text-[11.5px] font-medium text-ink hover:bg-line/40 disabled:opacity-50"
                      >
                        Assign
                      </button>
                    </div>
                  )}
                  {allows(s, "recordFraudDispositionOutcome") && (
                    <div className="flex items-center gap-1">
                      <select
                        value={outcomeDraft[s.id]?.outcome ?? ""}
                        onChange={(e) => setOutcomeDraft((cur) => ({ ...cur, [s.id]: { outcome: e.target.value, notes: cur[s.id]?.notes ?? "" } }))}
                        className="rounded-lg border border-line bg-white px-2 py-1.5 text-[11px] text-ink"
                      >
                        <option value="">Outcome…</option>
                        {SELECTABLE_OUTCOMES.map((o) => <option key={o} value={o}>{OUTCOME_LABEL[o]}</option>)}
                      </select>
                      <button
                        disabled={!outcomeDraft[s.id]?.outcome}
                        onClick={() => setPending({ kind: "outcome", signal: s, outcome: outcomeDraft[s.id].outcome, notes: outcomeDraft[s.id]?.notes ?? "" })}
                        className="rounded-lg border border-line bg-white px-2.5 py-1.5 text-[11.5px] font-medium text-ink hover:bg-line/40 disabled:opacity-50"
                      >
                        Record outcome
                      </button>
                    </div>
                  )}
                  {allows(s, "closeFraudSignal") && (
                    <button onClick={() => setPending({ kind: "close", signal: s })} className="rounded-lg gradient-primary px-2.5 py-1.5 text-[11.5px] font-semibold text-white">Close signal</button>
                  )}
                  {!allows(s, "triageFraudSignal") && !allows(s, "assignFraudSignal") && !allows(s, "recordFraudDispositionOutcome") && !allows(s, "closeFraudSignal") && (
                    <span className="text-[11px] text-muted" title={blockedReason(s, "triageFraudSignal") ?? blockedReason(s, "closeFraudSignal") ?? undefined}>
                      {status === "Closed" ? "Closed" : "No action available"}
                    </span>
                  )}
                </div>
              </div>
            </GlassCard>
          );})}
        </div>
      )}

      <GovernedActionDialog
        open={!!pending}
        title={
          pending?.kind === "triage" ? "Triage fraud signal" :
          pending?.kind === "assign" ? "Assign for investigation" :
          pending?.kind === "outcome" ? "Record disposition outcome" : "Close fraud signal"
        }
        target={pending?.signal.title ?? ""}
        impact={
          pending?.kind === "triage" ? "This acknowledges the signal has been reviewed and begins its disposition workflow." :
          pending?.kind === "assign" ? `This assigns the signal to ${pending.assignedToUserId} for investigation.` :
          pending?.kind === "outcome" ? `This records "${OUTCOME_LABEL[pending.outcome] ?? pending.outcome}" as the investigation outcome. This is a process outcome, never a fraud determination.` :
          "This closes the signal after its outcome has been recorded."
        }
        auditNotice="This action is written to the audit trail with your reason and cannot be undone silently."
        confirmLabel={
          pending?.kind === "triage" ? "Triage" :
          pending?.kind === "assign" ? "Assign" :
          pending?.kind === "outcome" ? "Record outcome" : "Close"
        }
        tone="primary"
        onClose={() => setPending(null)}
        onConfirm={handleConfirm}
      />
    </>
  );
}
