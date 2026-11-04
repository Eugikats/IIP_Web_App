"use client";

import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type Proposal = {
  proposalId: string;
  fieldKey: string;
  fieldLabel: string;
  value: string;
  currentValue: string | null;
  documentName: string | null;
  page: number | null;
  confidence: number;
  status: "Proposed" | "Accepted" | "Rejected";
  conflictsWithConfirmed: boolean;
  warning: string | null;
  acceptedValue: string | null;
};

type FieldProposalsCardProps = {
  caseId: string;
  role: string;
  canDecide: boolean;
  /** Called after an answer was put on the claim, so the details card can refresh. */
  onAccepted: () => void;
};

const TONE: Record<Proposal["status"], PillTone> = { Proposed: "warning", Accepted: "success", Rejected: "neutral" };

/**
 * What the claim's documents say, offered for the claim's questions (build-loop item 15). Each
 * suggestion names its document, page and how sure the reading was. Nothing is on the claim until an
 * officer presses Accept; a different answer already on the claim is replaced only by choosing to.
 */
export function FieldProposalsCard({ caseId, role, canDecide, onAccepted }: FieldProposalsCardProps) {
  const [proposals, setProposals] = useState<Proposal[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [edits, setEdits] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/field-proposals`, { headers: scopedHeaders(role, ""), cache: "no-store" });
      if (!response.ok) {
        setLoadError("The suggestions from the documents could not be loaded.");
        return;
      }
      setProposals((await response.json()) as Proposal[]);
    } catch {
      setLoadError("The suggestions from the documents could not be loaded.");
    }
  }, [caseId, role]);

  useEffect(() => { void load(); }, [load]);

  async function decide(proposal: Proposal, action: "accept" | "reject", replaceConfirmed = false) {
    setBusyId(proposal.proposalId);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/field-proposals/${proposal.proposalId}/${action}`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify(action === "accept" ? { value: edits[proposal.proposalId]?.trim() || undefined, replaceConfirmed } : {}),
      });
      if (!response.ok) {
        const payload = (await response.json().catch(() => ({}))) as { message?: string; errors?: Record<string, string[]> };
        setError(payload.message ?? Object.values(payload.errors ?? {}).flat()[0] ?? "That could not be saved.");
        return;
      }
      await load();
      if (action === "accept") onAccepted();
    } catch {
      setError("The service could not be reached. Check your connection and try again.");
    } finally {
      setBusyId(null);
    }
  }

  if (loadError) return <p role="alert" className="text-[12.5px] text-dls-danger-text">{loadError}</p>;
  if (!proposals || proposals.length === 0) return null;

  return (
    <div data-testid="field-proposals">
      <div className="mb-1 text-[14px] font-bold text-dls-text-primary">Suggested from the documents</div>
      <p className="mb-3 text-[12px] text-dls-text-secondary">Read from what the customer sent. Nothing is added to the claim until you accept it, and you can correct it first.</p>
      <ul className="space-y-2">
        {proposals.map((proposal) => (
          <li key={proposal.proposalId} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3" data-testid="field-proposal">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="text-[11px] font-semibold uppercase tracking-[0.08em] text-dls-text-muted">{proposal.fieldLabel}</div>
                <div className="mt-0.5 text-[13px] font-semibold text-dls-text-primary">{proposal.status === "Accepted" ? proposal.acceptedValue : proposal.value}</div>
                <div className="mt-0.5 text-[11.5px] text-dls-text-muted">
                  From {proposal.documentName ?? "a document"}{proposal.page ? `, page ${proposal.page}` : ""}, {Math.round(proposal.confidence * 100)}% sure
                </div>
              </div>
              <StatusPill tone={TONE[proposal.status]} label={proposal.status === "Proposed" ? "Needs a decision" : proposal.status} />
            </div>
            {proposal.status === "Proposed" && proposal.conflictsWithConfirmed && (
              <p className="mt-2 text-[12px] text-dls-text-secondary">The claim already says <span className="font-semibold">{proposal.currentValue}</span>, put there by a person. Accepting only replaces it if you choose to.</p>
            )}
            {proposal.status === "Proposed" && proposal.warning && <p className="mt-2 text-[12px] text-dls-danger-text">{proposal.warning} Correct it before accepting.</p>}
            {proposal.status === "Proposed" && canDecide && (
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <Input
                  aria-label={`Correct ${proposal.fieldLabel}`}
                  placeholder="Correct it here if needed"
                  value={edits[proposal.proposalId] ?? ""}
                  onChange={(event) => setEdits((current) => ({ ...current, [proposal.proposalId]: event.target.value }))}
                  className="min-w-[10rem] flex-1"
                />
                <Button variant="secondary" disabled={busyId === proposal.proposalId} onClick={() => void decide(proposal, "accept", proposal.conflictsWithConfirmed)} aria-label={`${proposal.conflictsWithConfirmed ? "Replace with" : "Accept"} ${proposal.fieldLabel}`}>
                  {proposal.conflictsWithConfirmed ? "Replace" : "Accept"}
                </Button>
                <Button variant="ghost" disabled={busyId === proposal.proposalId} onClick={() => void decide(proposal, "reject")} aria-label={`Reject ${proposal.fieldLabel}`}>Reject</Button>
              </div>
            )}
          </li>
        ))}
      </ul>
      {error && <p role="alert" className="mt-2 text-[12.5px] text-dls-danger-text">{error}</p>}
    </div>
  );
}
