"use client";

import { Loader2, Search } from "lucide-react";
import { Badge, GlassCard } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import type { FraudAgentFinding, FraudAgentReview } from "@/lib/lovable-parity/data/fraudAgent";

const SEVERITY_TONE: Record<FraudAgentFinding["severity"], "error" | "warning" | "violet"> = {
  RequiresInvestigation: "error",
  PotentialAnomaly: "warning",
  AdvisorySignal: "violet",
};

/**
 * The fraud agent's review of this claim: what it read the claim against, what it found, and the facts
 * behind each finding. Findings are leads for a person, never conclusions, and every one opens onto the
 * claim facts it rests on.
 */
export default function FraudAgentCard({
  review,
  loading,
  error,
  running,
  onRun,
}: {
  review: FraudAgentReview | null;
  loading: boolean;
  error: string | null;
  running: boolean;
  onRun: () => void;
}) {
  const canRun = review?.agentEnabled !== false && !loading;
  const ran = review?.ran === true;

  return (
    <GlassCard>
      <div className="mb-3 flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Search className="h-4 w-4 text-dls-text-secondary" aria-hidden="true" />
          <div className="text-[14px] font-bold text-dls-text-primary">Fraud agent review</div>
        </div>
        {canRun ? (
          <Button variant="secondary" onClick={onRun} disabled={running} icon={running ? <Loader2 className="animate-spin" /> : undefined}>
            {running ? "Reading the claim" : ran ? "Run again" : "Run review"}
          </Button>
        ) : null}
      </div>

      {loading ? (
        <p className="m-0 text-[12.5px] text-dls-text-secondary" role="status">Loading the review.</p>
      ) : null}

      {error ? (
        <p role="alert" className="m-0 mb-2 text-[12.5px] text-dls-danger-text">{error}</p>
      ) : null}

      {!loading && review && review.agentEnabled === false ? (
        <p className="m-0 text-[12.5px] leading-5 text-dls-text-secondary">
          The fraud agent is switched off for this workspace. The fixed rules above still run. A workspace administrator can switch the agent on under Admin, Agents.
        </p>
      ) : null}

      {!loading && review && review.agentEnabled && !ran ? (
        <p className="m-0 text-[12.5px] leading-5 text-dls-text-secondary">
          The fraud agent has not read this claim yet. It reads the claim against a written playbook of how insurance fraud shows up in Uganda and lists what a person should check, with the facts behind each point. The fixed rules above run on their own.
        </p>
      ) : null}

      {ran && review?.stale ? (
        <p role="status" className="m-0 mb-3 rounded-dls-control border border-dls-border-subtle bg-dls-surface-sunken px-3 py-2 text-[12px] leading-5 text-dls-text-secondary">
          The claim has changed since this review was written. Run it again to bring it up to date.
        </p>
      ) : null}

      {ran && review && review.findings.length === 0 ? (
        <p className="m-0 text-[12.5px] leading-5 text-dls-text-secondary">
          The fraud agent read the claim and found nothing in the facts that points to a known pattern. This is not a clearance. A person still decides.
        </p>
      ) : null}

      {ran && review && review.findings.length > 0 ? (
        <div className="space-y-3">
          {review.findings.map((finding, index) => (
            <div key={`${finding.indicatorCode}-${index}`} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="text-[13px] font-semibold text-dls-text-primary">{finding.title}</div>
                  <div className="mt-0.5 text-[11px] text-dls-text-muted">{finding.line}: {finding.indicatorLabel}</div>
                </div>
                <Badge tone={SEVERITY_TONE[finding.severity]}>{finding.severityLabel}</Badge>
              </div>
              <p className="mb-0 mt-2 text-[12px] leading-snug text-dls-text-secondary">{finding.observation}</p>
              <p className="mb-0 mt-1.5 text-[12px] leading-snug text-dls-text-primary">
                <span className="font-semibold">To check: </span>
                {finding.whatToCheck}
              </p>
              {finding.facts.length > 0 ? (
                <details className="mt-2 text-[11.5px] text-dls-text-muted">
                  <summary className="cursor-pointer select-none rounded-dls-tight dls-focus-outline">
                    Based on {finding.facts.length} fact{finding.facts.length === 1 ? "" : "s"} from the claim
                  </summary>
                  <ul className="m-0 mt-1.5 list-none space-y-1 p-0">
                    {finding.facts.map((fact) => (
                      <li key={fact.id} className="leading-snug">
                        <span className="font-semibold text-dls-text-secondary">{fact.id}</span> {fact.text}
                      </li>
                    ))}
                  </ul>
                </details>
              ) : null}
            </div>
          ))}
        </div>
      ) : null}

      {ran && review ? (
        <p className="m-0 mt-3 text-[11px] leading-snug text-dls-text-muted">
          Read {review.generatedAt ? new Date(review.generatedAt).toLocaleString() : ""}
          {review.isMock ? " by a test model, not a real one" : ""}, against playbook {review.playbookVersion}.
          {review.leftOut > 0
            ? ` ${review.leftOut} point${review.leftOut === 1 ? " was" : "s were"} left out because ${review.leftOut === 1 ? "it" : "they"} did not rest on the claim's facts or used wording that is not allowed.`
            : ""}
        </p>
      ) : null}
    </GlassCard>
  );
}
