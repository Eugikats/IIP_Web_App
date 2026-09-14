"use client";
import { usePeople } from "@/lib/people";
import { useState } from "react";
import { toast } from "sonner";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { AlertTriangle, ShieldAlert } from "lucide-react";
import {
  useFraudSignals, OUTCOME_LABEL, SELECTABLE_OUTCOMES, type FraudSeverity, type FraudSignalView,
} from "@/lib/lovable-parity/data/fraud";

const badgeTone: Record<FraudSeverity, "error" | "warning" | "info"> = {
  Investigation: "error",
  Anomaly: "warning",
  Advisory: "info",
};

const badgeLabel: Record<FraudSeverity, string> = {
  Investigation: "Requires investigation",
  Anomaly: "Potential anomaly",
  Advisory: "Advisory signal",
};

const STATUS_LABEL: Record<string, string> = {
  NotTriaged: "Not yet reviewed",
  Triaged: "Reviewed",
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
  const { describe } = usePeople();
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
        pending.kind === "triage" ? "Signal marked as reviewed." :
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

      {/* Not StatTile (§8.9): every tile here needs its own "Advisory" badge underneath the
          figure — a safety-relevant reinforcement (AGENTS.md: never let an AI-flagged signal read
          as a finding) StatTile's fixed label+value body has no slot for. No tile links anywhere
          either — there is no per-severity filtered sub-view to link to, and inventing one is out
          of scope for a presentation-only migration (same accepted, documented §8.9 partial as
          screen 6/Documents.tsx). */}
      <div className="mb-6 grid gap-3 md:grid-cols-4">
        {[
          { l: "Total advisory signals", v: counts.total, tone: "text-dls-primary" },
          { l: "Requires investigation", v: counts.investigation, tone: "text-dls-danger-text" },
          { l: "Potential anomalies", v: counts.anomaly, tone: "text-dls-warning-text" },
          { l: "Advisory only", v: counts.advisory, tone: "text-dls-text-primary" },
        ].map((k) => (
          <div key={k.l} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
            <div className="text-[13.5px] text-dls-text-secondary">{k.l}</div>
            <div className={`mt-2 text-[30px] font-bold leading-[1.1] tabular-nums tracking-[-0.5px] ${k.tone}`}>{k.v}</div>
            <Badge tone="info" className="mt-2">Advisory</Badge>
          </div>
        ))}
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Advisory signals could not be loaded" message={error} /></GlassCard>
      ) : signals.length === 0 ? (
        <GlassCard>
          <EmptyStateSurface
            title="No advisory signals"
            message="Nothing unusual has been picked up in this workspace’s claims. Nothing is flagged here unless a specific check matched and can show you what it matched on."
          />
        </GlassCard>
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {signals.map((s) => {
            const status = s.disposition.status;
            return (
            <GlassCard key={s.id}>
              <div className="flex items-start gap-3">
                <div className="grid h-10 w-10 shrink-0 place-items-center rounded-dls-chip bg-dls-warning-tint text-dls-warning-text"><AlertTriangle className="h-4 w-4" aria-hidden="true" /></div>
                <div className="flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="text-[14px] font-bold text-dls-text-primary">{s.title}</div>
                    <Badge tone={badgeTone[s.severity]}>{badgeLabel[s.severity]}</Badge>
                    <Badge tone={STATUS_TONE[status] ?? "neutral"}>{STATUS_LABEL[status] ?? status}</Badge>
                  </div>
                  <div className="mt-1 text-[12.5px] text-dls-text-secondary">{s.detail}</div>
                  <div className="mt-2 text-[11.5px] text-dls-text-muted">Linked: {s.linked}</div>
                  {s.evidence.length > 0 ? (
                    <div className="mt-1.5 flex flex-wrap gap-1">
                      {s.evidence.slice(0, 3).map((e, idx) => (
                        <span key={idx} className="rounded-dls-pill border border-dls-border-subtle bg-dls-surface px-2 py-0.5 text-[10.5px] text-dls-text-muted">{e}</span>
                      ))}
                    </div>
                  ) : null}
                  {s.disposition.assignedToUserId && (
                    <div className="mt-1.5 text-[11px] text-dls-text-muted">Assigned to {describe(s.disposition.assignedToUserId)}</div>
                  )}
                  {s.disposition.outcome && (
                    <div className="mt-1 text-[11px] text-dls-text-muted">Outcome: {OUTCOME_LABEL[s.disposition.outcome] ?? s.disposition.outcome}{s.disposition.outcomeNotes ? ` — ${s.disposition.outcomeNotes}` : ""}</div>
                  )}
                </div>
              </div>
              <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
                <div className="inline-flex items-center gap-1.5 text-[11.5px] font-semibold text-dls-warning-text"><AlertTriangle className="h-3 w-3" aria-hidden="true" /> Advisory only · human review required</div>
                <div className="flex flex-wrap items-center gap-1.5">
                  {allows(s, "triageFraudSignal") && (
                    <Button variant="secondary" onClick={() => setPending({ kind: "triage", signal: s })}>Mark reviewed</Button>
                  )}
                  {allows(s, "assignFraudSignal") && (
                    <div className="flex items-center gap-1.5">
                      <Input
                        value={assigneeDraft[s.id] ?? ""}
                        onChange={(e) => setAssigneeDraft((cur) => ({ ...cur, [s.id]: e.target.value }))}
                        placeholder="Assignee user ID"
                        aria-label={`Assignee for ${s.title}`}
                        className="w-32"
                      />
                      <Button
                        variant="secondary"
                        disabled={!assigneeDraft[s.id]?.trim()}
                        onClick={() => setPending({ kind: "assign", signal: s, assignedToUserId: assigneeDraft[s.id].trim() })}
                      >
                        Assign
                      </Button>
                    </div>
                  )}
                  {allows(s, "recordFraudDispositionOutcome") && (
                    <div className="flex items-center gap-1.5">
                      <Select
                        value={outcomeDraft[s.id]?.outcome ?? ""}
                        onChange={(e) => setOutcomeDraft((cur) => ({ ...cur, [s.id]: { outcome: e.target.value, notes: cur[s.id]?.notes ?? "" } }))}
                        aria-label={`Outcome for ${s.title}`}
                      >
                        <option value="">Outcome…</option>
                        {SELECTABLE_OUTCOMES.map((o) => <option key={o} value={o}>{OUTCOME_LABEL[o]}</option>)}
                      </Select>
                      <Button
                        variant="secondary"
                        disabled={!outcomeDraft[s.id]?.outcome}
                        onClick={() => setPending({ kind: "outcome", signal: s, outcome: outcomeDraft[s.id].outcome, notes: outcomeDraft[s.id]?.notes ?? "" })}
                      >
                        Record outcome
                      </Button>
                    </div>
                  )}
                  {allows(s, "closeFraudSignal") && (
                    <Button variant="primary" onClick={() => setPending({ kind: "close", signal: s })}>Close signal</Button>
                  )}
                  {!allows(s, "triageFraudSignal") && !allows(s, "assignFraudSignal") && !allows(s, "recordFraudDispositionOutcome") && !allows(s, "closeFraudSignal") && (
                    <span className="text-[11px] text-dls-text-muted" title={blockedReason(s, "triageFraudSignal") ?? blockedReason(s, "closeFraudSignal") ?? undefined}>
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
          pending?.kind === "triage" ? "Mark this fraud signal reviewed" :
          pending?.kind === "assign" ? "Assign for investigation" :
          pending?.kind === "outcome" ? "Record disposition outcome" : "Close fraud signal"
        }
        target={pending?.signal.title ?? ""}
        impact={
          pending?.kind === "triage" ? "This records that someone has looked at the signal, and moves it into the investigation process." :
          pending?.kind === "assign" ? `This assigns the signal to ${pending.assignedToUserId} for investigation.` :
          pending?.kind === "outcome" ? `This records "${OUTCOME_LABEL[pending.outcome] ?? pending.outcome}" as the investigation outcome. This is a process outcome, never a fraud determination.` :
          "This closes the signal after its outcome has been recorded."
        }
        auditNotice="This action is written to the audit trail with your reason and cannot be undone silently."
        confirmLabel={
          pending?.kind === "triage" ? "Mark reviewed" :
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
