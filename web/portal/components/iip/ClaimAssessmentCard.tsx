"use client";

import { useCallback, useEffect, useState } from "react";
import { Link } from "@/components/iip/NextRouterCompat";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type Source = { kind: string; id: string; label: string };
type Factor = {
  code: string;
  source: string;
  sourceLabel: string;
  strength: "Light" | "Moderate" | "Strong";
  reason: string;
  reference: Source;
  reading: string;
  readingLabel: string;
  counts: boolean;
};
type Assessment = {
  caseId: string;
  claimReference: string;
  score: { value: number; band: "Low" | "Moderate" | "Elevated" | "High"; bandLabel: string; factors: Factor[] };
  recommendation: {
    kind: "ReadyForDecision" | "RequestInformation" | "ReviewFlaggedItems" | "ReferForInvestigation";
    label: string;
    headline: string;
    reasons: { text: string; source: Source }[];
    suggestedRequests: string[];
  };
  narrative: string | null;
  narrativeProvider: string | null;
  narrativeIsMock: boolean;
  prepared: boolean;
  stale: boolean;
  generatedAt: string | null;
  generatedBy: string | null;
  response: { kind: "Agree" | "Disagree"; note: string | null; byUserId: string | null; at: string | null } | null;
  notice: string;
};

const BAND_TONE: Record<Assessment["score"]["band"], PillTone> = { Low: "success", Moderate: "info", Elevated: "warning", High: "danger" };
const STRENGTH_TONE: Record<Factor["strength"], PillTone> = { Light: "neutral", Moderate: "warning", Strong: "danger" };

/** Where a person can open the thing a statement rests on. Sources with nowhere to go are shown as text. */
function SourceLink({ source }: { source: Source }) {
  const label = `Source: ${source.label}`;
  if (source.kind === "Document" || source.kind === "Picture") {
    return <a href={`#document-${source.id}`} className="dls-focus-outline text-[11.5px] text-dls-text-secondary underline underline-offset-2 hover:text-dls-text-primary">{label}</a>;
  }
  if (source.kind === "Signal") {
    return <Link to="/app/fraud" className="dls-focus-outline text-[11.5px] text-dls-text-secondary underline underline-offset-2 hover:text-dls-text-primary">{label}</Link>;
  }
  return <span className="text-[11.5px] text-dls-text-muted">{label}</span>;
}

type ClaimAssessmentCardProps = { caseId: string; role: string; canRespond: boolean };

/**
 * The review score and recommendation for a claim (build-loop item 18). Two separate containers: what the
 * platform prepared (score, the reasons behind it, the recommended next step and a model's short
 * explanation) and the person's own response. Everything in the first is advice with its sources; nothing
 * on this card changes the claim.
 */
export function ClaimAssessmentCard({ caseId, role, canRespond }: ClaimAssessmentCardProps) {
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [choice, setChoice] = useState("Agree");
  const [note, setNote] = useState("");

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/assessment`, { headers: scopedHeaders(role, ""), cache: "no-store" });
      if (!response.ok) return setLoadError("The review score could not be loaded.");
      setAssessment((await response.json()) as Assessment);
    } catch {
      setLoadError("The service could not be reached. Check your connection and try again.");
    }
  }, [caseId, role]);

  useEffect(() => { void load(); }, [load]);

  async function post(path: string, body?: unknown) {
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/assessment/${path}`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      if (!response.ok) {
        const payload = (await response.json().catch(() => ({}))) as { message?: string; errors?: Record<string, string[]> };
        setError(payload.message ?? Object.values(payload.errors ?? {}).flat()[0] ?? "That could not be saved.");
        return;
      }
      setAssessment((await response.json()) as Assessment);
      setNote("");
    } catch {
      setError("The service could not be reached. Check your connection and try again.");
    } finally {
      setBusy(false);
    }
  }

  if (loadError) return <ErrorStateSurface title="The review score could not be loaded" message={loadError} onRetry={load} />;
  if (!assessment) return <LoadingSkeleton rows={3} />;

  const counted = assessment.score.factors.filter((factor) => factor.counts);
  const setAside = assessment.score.factors.filter((factor) => !factor.counts);

  return (
    <div data-testid="claim-assessment" className="space-y-4">
      <div>
        <div className="text-[14px] font-bold text-dls-text-primary">Review score and recommendation</div>
        <p className="mt-1 text-[12px] text-dls-text-secondary">{assessment.notice}</p>
      </div>

      <section aria-label="Prepared by the platform" className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4" data-testid="assessment-prepared">
        <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Prepared for you</div>

        <div className="mt-3 flex flex-wrap items-center gap-3">
          <div className="text-[28px] font-bold leading-none text-dls-text-primary" data-testid="assessment-score">{assessment.score.value}</div>
          <div className="text-[12px] text-dls-text-secondary">out of 100</div>
          <StatusPill tone={BAND_TONE[assessment.score.band]} label={assessment.score.bandLabel} />
        </div>
        <p className="mt-2 text-[12px] text-dls-text-secondary">
          How much a person should look at this claim, from the reasons below. It is not a chance that anyone has done wrong.
        </p>

        {counted.length === 0 ? (
          <p className="mt-3 text-[12.5px] text-dls-text-secondary" data-testid="assessment-no-factors">Nothing has been flagged on this claim.</p>
        ) : (
          <ul className="mt-3 space-y-2" data-testid="assessment-factors">
            {counted.map((factor) => (
              <li key={factor.code} className="rounded-dls-tight border border-dls-border-subtle p-2.5" data-testid="assessment-factor">
                <div className="flex flex-wrap items-center gap-2">
                  <StatusPill tone={STRENGTH_TONE[factor.strength]} label={factor.strength} />
                  <span className="text-[11.5px] text-dls-text-muted">{factor.sourceLabel}</span>
                  <span className="text-[11.5px] text-dls-text-muted">{factor.readingLabel}</span>
                </div>
                <p className="mt-1.5 text-[12.5px] leading-5 text-dls-text-primary">{factor.reason}</p>
                <div className="mt-1"><SourceLink source={factor.reference} /></div>
              </li>
            ))}
          </ul>
        )}
        {setAside.length > 0 && (
          <p className="mt-2 text-[11.5px] text-dls-text-muted">{setAside.length} {setAside.length === 1 ? "reason was" : "reasons were"} set aside by a person and no longer count.</p>
        )}

        <div className="mt-4 border-t border-dls-border-subtle pt-3" data-testid="assessment-recommendation">
          <div className="flex flex-wrap items-center gap-2">
            <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Recommended next step</div>
            <StatusPill tone={assessment.recommendation.kind === "ReadyForDecision" ? "success" : assessment.recommendation.kind === "ReferForInvestigation" ? "danger" : "warning"} label={assessment.recommendation.label} />
          </div>
          <p className="mt-1.5 text-[13px] font-semibold text-dls-text-primary">{assessment.recommendation.headline}</p>
          <ul className="mt-2 space-y-1.5">
            {assessment.recommendation.reasons.map((reason, index) => (
              <li key={`${reason.source.kind}-${reason.source.id}-${index}`} className="text-[12.5px] leading-5 text-dls-text-secondary" data-testid="assessment-reason">
                {reason.text} <SourceLink source={reason.source} />
              </li>
            ))}
          </ul>
          {assessment.recommendation.suggestedRequests.length > 0 && (
            <ul className="mt-2 list-disc space-y-0.5 pl-5 text-[12.5px] text-dls-text-secondary" data-testid="assessment-requests">
              {assessment.recommendation.suggestedRequests.map((request) => <li key={request}>{request}</li>)}
            </ul>
          )}
        </div>

        <div className="mt-4 border-t border-dls-border-subtle pt-3" data-testid="assessment-narrative">
          <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">In words</div>
          {assessment.narrative ? (
            <p className="mt-1.5 text-[12.5px] leading-5 text-dls-text-secondary">{assessment.narrative}</p>
          ) : (
            <p className="mt-1.5 text-[12.5px] text-dls-text-muted">
              {assessment.stale ? "The facts have changed since the last explanation was written." : assessment.prepared ? "No written explanation is available." : "No written explanation has been prepared yet."}
            </p>
          )}
          <div className="mt-2 flex flex-wrap items-center gap-3">
            <Button variant="secondary" disabled={busy} onClick={() => void post("refresh")}>
              {assessment.narrative ? "Write it again" : "Prepare the explanation"}
            </Button>
            {assessment.generatedAt && (
              <span className="text-[11.5px] text-dls-text-muted">Prepared {new Date(assessment.generatedAt).toLocaleString()}{assessment.narrativeIsMock ? " (test model)" : ""}</span>
            )}
          </div>
        </div>
      </section>

      <section aria-label="Your response" className="rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-4" data-testid="assessment-response">
        <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Your response</div>
        {assessment.response ? (
          <p className="mt-2 text-[12.5px] text-dls-text-primary" data-testid="assessment-response-recorded">
            {assessment.response.kind === "Agree" ? "Agreed" : "Disagreed"} by {assessment.response.byUserId}
            {assessment.response.note ? `: ${assessment.response.note}` : "."}
          </p>
        ) : (
          <p className="mt-2 text-[12.5px] text-dls-text-secondary">
            {assessment.stale ? "Anything you said earlier was about facts that have since changed. " : ""}Nobody has responded to this recommendation yet.
          </p>
        )}
        {canRespond ? (
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Select aria-label="Your response to the recommendation" value={choice} onChange={(event) => setChoice(event.target.value)}>
              <option value="Agree">I agree</option>
              <option value="Disagree">I disagree</option>
            </Select>
            <Input aria-label="Why, if you disagree" placeholder={choice === "Disagree" ? "Why you disagree (required)" : "A note (optional)"} value={note} onChange={(event) => setNote(event.target.value)} className="min-w-[10rem] flex-1" />
            <Button disabled={busy} onClick={() => void post("response", { response: choice, note })}>Record response</Button>
          </div>
        ) : (
          <p className="mt-2 text-[11.5px] text-dls-text-muted">Your role can read this but not respond to it.</p>
        )}
        {error && <p role="alert" className="mt-2 text-[12.5px] text-dls-danger-text">{error}</p>}
      </section>
    </div>
  );
}
