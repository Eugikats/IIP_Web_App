"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { StatusPill } from "@/components/ui/StatusPill";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import type { DocumentItem } from "@/lib/documentContracts";

type Slot = { key: string; label: string };

type DocumentFilingPanelProps = {
  document: DocumentItem;
  role: string;
  /** Called after the document was filed, so the page can reload its list. */
  onFiled: () => void;
};

/**
 * What the recogniser made of a document and, when it needs a person, a way to say where it
 * belongs (build-loop item 14). The recogniser only advises: nothing here changes the file, only
 * which of the claim's documents it counts as.
 */
export function DocumentFilingPanel({ document, role, onFiled }: DocumentFilingPanelProps) {
  const [slots, setSlots] = useState<Slot[]>([]);
  const [chosen, setChosen] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const needsPerson = Boolean(document.needsReview);

  useEffect(() => {
    if (!needsPerson && !document.classifierDisagrees) return;
    let cancelled = false;
    fetch(`${apiBaseUrl}/api/v1/cases/${document.caseId}/document-checklist`, { headers: scopedHeaders(role, ""), cache: "no-store" })
      .then(async (response) => {
        if (cancelled || !response.ok) return;
        const payload = (await response.json()) as { slots?: { key: string; label: string }[] };
        setSlots((payload.slots ?? []).map((slot) => ({ key: slot.key, label: slot.label })));
      })
      .catch(() => undefined);
    return () => { cancelled = true; };
  }, [document.caseId, document.documentId, needsPerson, document.classifierDisagrees, role]);

  async function fileIt() {
    if (!chosen) return;
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${document.caseId}/documents/${document.documentId}/file-into`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ documentTypeKey: chosen }),
      });
      if (!response.ok) {
        const payload = (await response.json().catch(() => ({}))) as { message?: string; errors?: Record<string, string[]> };
        setError(payload.message ?? Object.values(payload.errors ?? {}).flat()[0] ?? "It could not be filed.");
        return;
      }
      onFiled();
    } catch {
      setError("The service could not be reached. Check your connection and try again.");
    } finally {
      setBusy(false);
    }
  }

  if (!document.recognitionReason && !document.displayName && !needsPerson) return null;

  return (
    <div className="mb-3 rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-3 text-[12px] leading-5 text-dls-text-secondary" data-testid="document-recognition">
      {document.displayName && (
        <div>
          Shown as <span className="font-semibold text-dls-text-primary" data-testid="document-display-name">{document.displayName}</span>
          {document.displayName !== document.originalFileName && <> (sent as {document.originalFileName})</>}
        </div>
      )}
      {document.recognitionReason && (
        <div className="mt-1">
          Looks like: {document.recognisedTypeKey ?? "not sure"}
          {typeof document.recognitionConfidence === "number" && document.recognisedTypeKey ? ` (${Math.round(document.recognitionConfidence * 100)}% sure)` : ""}. {document.recognitionReason}
        </div>
      )}
      {document.filedByRecognition && <div className="mt-1">Filed automatically because the match was clear. Move it if that is wrong.</div>}
      {needsPerson && (
        <div className="mt-2">
          <StatusPill tone="warning" label="Needs a person" />
          <p className="mt-1">{document.reviewReason}</p>
        </div>
      )}
      {(needsPerson || document.classifierDisagrees) && slots.length > 0 && (
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <label htmlFor={`file-into-${document.documentId}`} className="font-semibold text-dls-text-primary">Put it in</label>
          <Select id={`file-into-${document.documentId}`} value={chosen} onChange={(event) => setChosen(event.target.value)}>
            <option value="">Choose a document</option>
            {slots.map((slot) => <option key={slot.key} value={slot.key}>{slot.label}</option>)}
          </Select>
          <Button variant="secondary" onClick={() => void fileIt()} disabled={!chosen || busy}>{busy ? "Filing..." : "Put it there"}</Button>
        </div>
      )}
      {error && <p role="alert" className="mt-2 text-dls-danger-text">{error}</p>}
    </div>
  );
}
