"use client";

import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type Finding = { code: string; severity: "Info" | "Low" | "Medium" | "High"; reason: string; relatedClaimReference: string | null; relatedIsThisClaim: boolean };
type Report = {
  documentId: string;
  documentName: string | null;
  originalFileName: string;
  findings: Finding[];
  disposition: "NotReviewed" | "NoConcern" | "NeedsFollowUp" | "ConcernRaised";
  dispositionNote: string | null;
  dispositionByUserId: string | null;
  needsLook: boolean;
};

const SEVERITY_TONE: Record<Finding["severity"], PillTone> = { Info: "info", Low: "neutral", Medium: "warning", High: "danger" };
const DISPOSITION_LABEL: Record<Report["disposition"], string> = {
  NotReviewed: "Not looked at yet",
  NoConcern: "No concern",
  NeedsFollowUp: "Needs follow-up",
  ConcernRaised: "Concern raised",
};

type AuthenticityCardProps = { caseId: string; role: string; canDecide: boolean };

/**
 * What the authenticity checks noticed about the claim's pictures (build-loop item 16): missing or
 * odd camera details, signs of editing, and pictures used before. Each finding says why. These are
 * prompts to look, not verdicts; the reading below each picture is a person's and is recorded.
 */
export function AuthenticityCard({ caseId, role, canDecide }: AuthenticityCardProps) {
  const [reports, setReports] = useState<Report[] | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [choice, setChoice] = useState<Record<string, string>>({});
  const [note, setNote] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoadError(false);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/authenticity`, { headers: scopedHeaders(role, ""), cache: "no-store" });
      if (!response.ok) return setLoadError(true);
      setReports((await response.json()) as Report[]);
    } catch {
      setLoadError(true);
    }
  }, [caseId, role]);

  useEffect(() => { void load(); }, [load]);

  async function save(report: Report) {
    setBusy(report.documentId);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/documents/${report.documentId}/authenticity/disposition`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ disposition: choice[report.documentId] ?? "NoConcern", note: note[report.documentId] ?? "" }),
      });
      if (!response.ok) {
        const payload = (await response.json().catch(() => ({}))) as { message?: string; errors?: Record<string, string[]> };
        setError(payload.message ?? Object.values(payload.errors ?? {}).flat()[0] ?? "That could not be saved.");
        return;
      }
      await load();
    } catch {
      setError("The service could not be reached. Check your connection and try again.");
    } finally {
      setBusy(null);
    }
  }

  if (loadError) return <p role="alert" className="text-[12.5px] text-dls-danger-text">The authenticity checks could not be loaded.</p>;
  if (!reports || reports.length === 0) return null;
  const waiting = reports.filter((report) => report.needsLook).length;

  return (
    <div data-testid="authenticity-card">
      <div className="mb-1 flex flex-wrap items-center gap-2">
        <div className="text-[14px] font-bold text-dls-text-primary">Picture checks</div>
        {waiting > 0 && <StatusPill tone="warning" label={`${waiting} ${waiting === 1 ? "picture needs" : "pictures need"} a look`} />}
      </div>
      <p className="mb-3 text-[12px] text-dls-text-secondary">These are reasons to look at a picture, not a finding that anything is wrong. Many are harmless, such as a photo sent through a messaging app.</p>
      <ul className="space-y-3">
        {reports.map((report) => (
          <li key={report.documentId} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3" data-testid="authenticity-report">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="min-w-0 text-[13px] font-semibold text-dls-text-primary">{report.documentName ?? report.originalFileName}</div>
              <StatusPill tone={report.disposition === "ConcernRaised" ? "danger" : report.disposition === "NeedsFollowUp" ? "warning" : report.disposition === "NoConcern" ? "success" : "neutral"} label={DISPOSITION_LABEL[report.disposition]} />
            </div>
            {report.findings.length === 0 ? (
              <p className="mt-2 text-[12px] text-dls-text-secondary">Nothing unusual was noticed.</p>
            ) : (
              <ul className="mt-2 space-y-1.5">
                {report.findings.map((finding, index) => (
                  <li key={`${finding.code}-${index}`} className="flex items-start gap-2 text-[12px] leading-5 text-dls-text-secondary">
                    <StatusPill tone={SEVERITY_TONE[finding.severity]} label={finding.severity === "Info" ? "For information" : finding.severity} className="shrink-0" />
                    <span>{finding.reason}{finding.relatedClaimReference && !finding.relatedIsThisClaim ? ` (${finding.relatedClaimReference})` : ""}</span>
                  </li>
                ))}
              </ul>
            )}
            {report.disposition !== "NotReviewed" && (
              <p className="mt-2 text-[11.5px] text-dls-text-muted">
                {DISPOSITION_LABEL[report.disposition]} by {report.dispositionByUserId}{report.dispositionNote ? `: ${report.dispositionNote}` : ""}
              </p>
            )}
            {canDecide && (
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <Select aria-label={`Your reading of ${report.documentName ?? report.originalFileName}`} value={choice[report.documentId] ?? "NoConcern"} onChange={(event) => setChoice((current) => ({ ...current, [report.documentId]: event.target.value }))}>
                  <option value="NoConcern">No concern</option>
                  <option value="NeedsFollowUp">Needs follow-up</option>
                  <option value="ConcernRaised">Concern raised</option>
                </Select>
                <Input
                  aria-label={`Note about ${report.documentName ?? report.originalFileName}`}
                  placeholder="What you saw or need checked"
                  value={note[report.documentId] ?? ""}
                  onChange={(event) => setNote((current) => ({ ...current, [report.documentId]: event.target.value }))}
                  className="min-w-[10rem] flex-1"
                />
                <Button variant="secondary" disabled={busy === report.documentId} onClick={() => void save(report)} aria-label={`Save reading of ${report.documentName ?? report.originalFileName}`}>Save</Button>
              </div>
            )}
          </li>
        ))}
      </ul>
      {error && <p role="alert" className="mt-2 text-[12.5px] text-dls-danger-text">{error}</p>}
    </div>
  );
}
