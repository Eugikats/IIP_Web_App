"use client";

import type { ChangeEvent } from "react";
import { Loader2, Paperclip, RefreshCw, Trash2, UploadCloud } from "lucide-react";
import { Button } from "@/components/ui/Button";
import type { ClientPortalClaim } from "@/lib/clientPortal";

export const SOMETHING_ELSE_KEY = "__something_else__";

export type ClaimantDocumentRow = ClientPortalClaim["requiredDocuments"][number];

function formatDate(value: string | null) {
  if (!value) return null;
  try {
    return new Date(value).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  } catch {
    return value;
  }
}

type ClaimantDocumentsCardProps = {
  documents: ClaimantDocumentRow[];
  busyKey: string | null;
  notice: string | null;
  noticeFailed?: boolean;
  requiredSummary?: string | null;
  requiredComplete?: boolean;
  onUpload: (event: ChangeEvent<HTMLInputElement>, documentTypeKey: string, label: string) => void;
  onRemove: (documentTypeKey: string, label: string) => void;
  onRetry: (documentTypeKey: string, label: string) => void;
  /** Used by "Preview as customer": the card looks the same but nothing can be sent. */
  previewOnly?: boolean;
  /** Why sending is not possible yet (for example the privacy notice is not agreed). */
  uploadsBlockedReason?: string | null;
};

/**
 * The customer's list of documents to send, one row per slot. Shared by the claim page and by
 * "Preview as customer", so what an administrator previews is the same component the customer
 * sees, not a copy that can drift.
 */
export function ClaimantDocumentsCard({ documents, busyKey, notice, noticeFailed = false, requiredSummary, requiredComplete = false, onUpload, onRemove, onRetry, previewOnly = false, uploadsBlockedReason = null }: ClaimantDocumentsCardProps) {
  const locked = previewOnly || busyKey !== null || uploadsBlockedReason !== null;
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6" data-testid="claimant-documents">
      <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">Documents</div>
      <p className="mt-1 text-xs leading-5 text-dls-text-muted">Send each document below. You can replace or remove anything you send.</p>
      {requiredSummary && !previewOnly && (
        <p className={`mt-3 rounded-dls-control px-3 py-2 text-xs font-semibold leading-5 ${requiredComplete ? "bg-dls-success-tint text-dls-success-text" : "bg-dls-warning-tint text-dls-warning-text"}`}>{requiredSummary}</p>
      )}
      <ul className="mt-3 divide-y divide-dls-border-subtle">
        {documents.map((doc) => (
          <li key={doc.documentTypeKey} className="py-3" data-testid="claimant-document-row">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="flex items-start gap-2">
                <Paperclip className="mt-0.5 h-3.5 w-3.5 shrink-0 text-dls-text-muted" aria-hidden="true" />
                <div>
                  <div className="text-sm font-semibold text-dls-text-primary">
                    {doc.label}
                    {!doc.required && <span className="ml-1.5 text-xs font-normal text-dls-text-muted">(optional)</span>}
                  </div>
                  <div className="text-xs leading-5 text-dls-text-muted">{doc.description}</div>
                </div>
              </div>
              <span className={`shrink-0 text-xs font-semibold ${doc.canRetry ? "text-dls-danger-text" : doc.uploaded ? "text-dls-success-text" : "text-dls-warning-text"}`}>{doc.status}</span>
            </div>

            {doc.uploaded && (
              // The classifier disagreeing with the customer's own choice
              // (doc.disagreesWithClassifier) is for staff to see, never shown to the
              // customer as an error (claim intake build prompt outcome B).
              <div className="mt-2 ml-5.5 text-xs text-dls-text-muted">
                <span className="truncate">{doc.fileName}{doc.uploadedAt ? ` · ${formatDate(doc.uploadedAt)}` : ""}</span>
              </div>
            )}

            {doc.kind === "Evidence" && (
              <div className="mt-1 ml-5.5 text-[11px] leading-4 text-dls-text-muted">
                These photos are kept exactly as you take them.
              </div>
            )}
            {doc.allowsMultipleFiles && (doc.fileCount ?? 0) > 0 && (
              <div className="mt-1 ml-5.5 text-[11px] leading-4 text-dls-text-muted">
                {doc.fileCount} of {doc.maxFiles} sent
              </div>
            )}

            <div className="mt-2 ml-5.5 flex items-center gap-2">
              <label className={`dls-focus-outline inline-flex min-h-8 items-center gap-1.5 rounded-dls-control border border-dls-border-input bg-dls-surface px-2.5 py-1 text-xs font-semibold text-dls-text-secondary ${previewOnly ? "opacity-60" : "cursor-pointer hover:bg-dls-bg-page"}`}>
                {busyKey === doc.documentTypeKey ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />
                ) : doc.uploaded && !doc.allowsMultipleFiles ? (
                  <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" />
                ) : (
                  <UploadCloud className="h-3.5 w-3.5" aria-hidden="true" />
                )}
                {doc.allowsMultipleFiles ? (doc.uploaded ? "Add more" : "Upload") : doc.uploaded ? "Replace" : "Upload"}
                <input
                  type="file"
                  accept={doc.acceptedFormats?.length ? doc.acceptedFormats.map((format) => `.${format}`).join(",") : "image/*,application/pdf"}
                  multiple={doc.allowsMultipleFiles === true}
                  className="hidden"
                  disabled={locked || (doc.allowsMultipleFiles === true && (doc.fileCount ?? 0) >= (doc.maxFiles ?? 1))}
                  onChange={(event) => onUpload(event, doc.documentTypeKey, doc.label)}
                />
              </label>
              {doc.canRetry && (
                <Button variant="secondary" icon={<RefreshCw aria-hidden="true" />} onClick={() => onRetry(doc.documentTypeKey, doc.label)} disabled={locked}>
                  Try again
                </Button>
              )}
              {doc.uploaded && (
                <Button variant="ghost" icon={<Trash2 aria-hidden="true" />} onClick={() => onRemove(doc.documentTypeKey, doc.label)} disabled={locked}>
                  Remove
                </Button>
              )}
            </div>
          </li>
        ))}
        <li className="py-3">
          <div className="flex items-start gap-2">
            <Paperclip className="mt-0.5 h-3.5 w-3.5 shrink-0 text-dls-text-muted" aria-hidden="true" />
            <div>
              <div className="text-sm font-semibold text-dls-text-primary">Something else</div>
              <div className="text-xs leading-5 text-dls-text-muted">Anything else that supports your claim.</div>
            </div>
          </div>
          <div className="mt-2 ml-5.5">
            <label className={`dls-focus-outline inline-flex min-h-8 items-center gap-1.5 rounded-dls-control border border-dls-border-input bg-dls-surface px-2.5 py-1 text-xs font-semibold text-dls-text-secondary ${previewOnly ? "opacity-60" : "cursor-pointer hover:bg-dls-bg-page"}`}>
              {busyKey === SOMETHING_ELSE_KEY ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" /> : <UploadCloud className="h-3.5 w-3.5" aria-hidden="true" />}
              Upload
              <input
                type="file"
                multiple
                accept="image/*,application/pdf"
                className="hidden"
                disabled={locked}
                onChange={(event) => onUpload(event, SOMETHING_ELSE_KEY, "Document")}
              />
            </label>
          </div>
        </li>
      </ul>
      {previewOnly && <p className="mt-3 text-xs leading-5 text-dls-text-muted">Preview only. Nothing can be sent from here.</p>}
      {uploadsBlockedReason && !previewOnly && <p role="status" className="mt-3 text-xs leading-5 text-dls-text-muted">{uploadsBlockedReason}</p>}
      {notice && <p role={noticeFailed ? "alert" : "status"} aria-live="polite" className={`mt-3 text-xs leading-5 ${noticeFailed ? "font-semibold text-dls-danger-text" : "text-dls-text-muted"}`}>{notice}</p>}
    </div>
  );
}
