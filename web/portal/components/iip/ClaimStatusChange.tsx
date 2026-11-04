"use client";

import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type Option = { status: string; label: string; requiresSupervisor: boolean; allowedForYou: boolean };
type Other = { status: string; label: string; howItIsReached: string };
type Options = { currentStatus: string; currentLabel: string; options: Option[]; others: Other[] };

type ClaimStatusChangeProps = {
  caseId: string;
  role: string;
  status: string;
  /** Called with the confirmation once the status has changed. The page reloads the claim, which remounts this
   *  panel, so the page keeps the confirmation and hands it back as <c>notice</c>. */
  onChanged: (confirmation: string) => void;
  notice?: string | null;
};

/**
 * The one "Change status" action (build-loop item 20). It offers only the moves an officer is allowed to
 * decide, asks for a reason that goes on the record, and says where every other status comes from (Assign,
 * Escalate, the Approvals queue, Resolve) instead of hiding it. Moves that need a supervisor are shown and
 * disabled for anyone else.
 */
export function ClaimStatusChange({ caseId, role, status, onChanged, notice = null }: ClaimStatusChangeProps) {
  const [options, setOptions] = useState<Options | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [target, setTarget] = useState("");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoadError(false);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/status-options`, { headers: scopedHeaders(role, ""), cache: "no-store" });
      if (!response.ok) return setLoadError(true);
      setOptions((await response.json()) as Options);
    } catch {
      setLoadError(true);
    }
  }, [caseId, role]);

  // The status shown on the page changes when the claim moves by any route, so the choices are read again.
  useEffect(() => { void load(); }, [load, status]);

  async function change() {
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/status`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ toStatus: target, reason }),
      });
      const payload = (await response.json().catch(() => ({}))) as { message?: string; label?: string; errors?: Record<string, string[]> };
      if (!response.ok) {
        setError(payload.message ?? Object.values(payload.errors ?? {}).flat()[0] ?? "The status could not be changed.");
        return;
      }
      setTarget("");
      setReason("");
      onChanged(`The claim is now ${(payload.label ?? "updated").toLowerCase()}.`);
      await load();
    } catch {
      setError("The service could not be reached. Check your connection and try again.");
    } finally {
      setBusy(false);
    }
  }

  if (loadError) return <p role="alert" className="text-[12.5px] text-dls-danger-text">The status choices could not be loaded.</p>;
  if (!options) return null;

  return (
    <div data-testid="claim-status-change">
      <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Change status</div>
      <p className="mt-1 text-[12.5px] text-dls-text-secondary">Now: <span className="font-semibold text-dls-text-primary" data-testid="current-status">{options.currentLabel}</span></p>
      {options.options.length === 0 ? (
        <p className="mt-2 text-[12.5px] text-dls-text-muted">No status can be chosen by hand from here. See below for how this claim moves on.</p>
      ) : (
        <div className="mt-2 space-y-2">
          <Select aria-label="Move the claim to" value={target} onChange={(event) => setTarget(event.target.value)} className="w-full">
            <option value="">Choose a status</option>
            {options.options.map((option) => (
              <option key={option.status} value={option.status} disabled={!option.allowedForYou}>
                {option.label}{option.requiresSupervisor ? (option.allowedForYou ? " (supervisor)" : " (needs a supervisor)") : ""}
              </option>
            ))}
          </Select>
          <Input aria-label="Why you are changing the status" placeholder="Why (required, kept on the record)" value={reason} onChange={(event) => setReason(event.target.value)} maxLength={500} className="w-full" />
          <Button variant="secondary" disabled={busy || !target || !reason.trim()} onClick={() => void change()}>Change status</Button>
        </div>
      )}
      {notice && <p role="status" className="mt-2 text-[12.5px] text-dls-text-secondary">{notice}</p>}
      {error && <p role="alert" className="mt-2 text-[12.5px] text-dls-danger-text">{error}</p>}
      <details className="mt-3 text-[12px] text-dls-text-secondary">
        <summary className="dls-focus-outline cursor-pointer rounded-dls-tight font-semibold text-dls-text-primary">How the other statuses are reached</summary>
        <ul className="mt-2 space-y-1.5" data-testid="other-statuses">
          {options.others.map((other) => (
            <li key={other.status}><span className="font-semibold text-dls-text-primary">{other.label}.</span> {other.howItIsReached}</li>
          ))}
        </ul>
      </details>
    </div>
  );
}
