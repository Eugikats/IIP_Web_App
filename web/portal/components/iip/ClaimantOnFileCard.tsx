"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import type { CustomerMemory } from "@/lib/customerMemory";

type ClaimantOnFileCardProps = {
  memory: CustomerMemory;
  busy: boolean;
  error: string | null;
  notice: string | null;
  onUseDetails: (keys: string[]) => void;
  onUseDocument: (documentId: string, documentTypeKey: string) => void;
};

/**
 * What the customer already gave on an earlier claim (build-loop item 26): details they can accept instead of typing again, and
 * documents they can reuse instead of sending again. Nothing is filled in or copied until they press a button, and every line
 * says which claim it came from.
 */
export function ClaimantOnFileCard({ memory, busy, error, notice, onUseDetails, onUseDocument }: ClaimantOnFileCardProps) {
  const [chosen, setChosen] = useState<Record<string, boolean>>({});
  if (memory.values.length === 0 && memory.documents.length === 0 && !notice) return null;
  const keys = memory.values.filter((value) => chosen[value.key] ?? true).map((value) => value.key);

  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6" data-testid="on-file-card">
      <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">What we already have</div>
      <p className="mt-1 text-xs leading-5 text-dls-text-muted">From your earlier claims with us. Use them to save time, or send new ones. Nothing is used until you choose.</p>

      {memory.values.length > 0 && (
        <div className="mt-3" data-testid="on-file-details">
          <ul className="space-y-2">
            {memory.values.map((value) => (
              <li key={value.key}>
                <label className="flex items-start gap-2 text-sm text-dls-text-primary">
                  <input type="checkbox" className="mt-1 h-4 w-4" checked={chosen[value.key] ?? true} onChange={(event) => setChosen({ ...chosen, [value.key]: event.target.checked })} />
                  <span>
                    {value.label}: <span className="font-semibold">{value.value}</span>
                    <span className="block text-xs text-dls-text-muted">from claim {value.fromClaim}</span>
                  </span>
                </label>
              </li>
            ))}
          </ul>
          <div className="mt-2">
            <Button variant="secondary" disabled={busy || keys.length === 0} onClick={() => onUseDetails(keys)}>Use these details</Button>
          </div>
        </div>
      )}

      {memory.documents.length > 0 && (
        <ul className="mt-4 space-y-3" data-testid="on-file-documents">
          {memory.documents.map((document) => (
            <li key={document.documentId} className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-sm text-dls-text-primary">
                {document.label}
                <span className="block text-xs text-dls-text-muted">{document.fileName}, from claim {document.fromClaim}</span>
              </span>
              <Button variant="secondary" disabled={busy} onClick={() => onUseDocument(document.documentId, document.documentTypeKey)} aria-label={`Use the one on file for ${document.label}`}>Use the one on file</Button>
            </li>
          ))}
        </ul>
      )}

      {notice && <p role="status" className="mt-3 text-xs leading-5 text-dls-text-secondary">{notice}</p>}
      {error && <p role="alert" className="mt-3 text-xs leading-5 text-dls-danger-text">{error}</p>}
    </div>
  );
}
