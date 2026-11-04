"use client";

import type { ChangeEvent } from "react";
import { Camera, Loader2, Paperclip, RefreshCw, Trash2, UploadCloud } from "lucide-react";
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

const IMAGE_FORMATS = ["jpg", "jpeg", "png", "heic", "heif", "webp"];

type SlotPickerProps = {
  formats: string[];
  multiple: boolean;
  /** Pages of one piece of paperwork (as opposed to a set of evidence photos). */
  pages?: boolean;
  uploaded: boolean;
  /** A multi-file slot that already holds as many files as it may. */
  full: boolean;
  busy: boolean;
  locked: boolean;
  onPick: (event: ChangeEvent<HTMLInputElement>) => void;
};

/**
 * The two ways to put a file in a slot (build-loop item 10): "Take photo" opens the camera on a
 * phone (and the normal file chooser on a computer), "Choose file" opens the chooser. A slot that
 * takes only PDFs offers no camera. The camera takes one picture at a time, so a multi-file slot
 * is filled by taking several, or by choosing several at once.
 */
function SlotPicker({ formats, multiple, pages = false, uploaded, full, busy, locked, onPick }: SlotPickerProps) {
  const canCapture = formats.length === 0 || formats.some((format) => IMAGE_FORMATS.includes(format.toLowerCase()));
  const accept = formats.length > 0 ? formats.map((format) => `.${format}`).join(",") : "image/*,application/pdf";
  const chooseText = pages
    ? (uploaded ? "Add another page" : "Choose files")
    : multiple ? (uploaded ? "Add more files" : "Choose files") : uploaded ? "Replace file" : "Choose file";
  const disabled = locked || full;
  const buttonClass = `dls-focus-outline focus-within:ring-2 focus-within:ring-dls-primary inline-flex min-h-9 items-center gap-1.5 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-1.5 text-xs font-semibold text-dls-text-secondary ${disabled ? "opacity-60" : "cursor-pointer hover:bg-dls-bg-page"}`;
  return (
    <>
      {canCapture && (
        <label className={buttonClass}>
          {busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" /> : <Camera className="h-3.5 w-3.5" aria-hidden="true" />}
          Take photo
          <input type="file" accept="image/*" capture="environment" data-upload="capture" className="sr-only" disabled={disabled} onChange={onPick} />
        </label>
      )}
      <label className={buttonClass}>
        {uploaded && !multiple ? <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" /> : <UploadCloud className="h-3.5 w-3.5" aria-hidden="true" />}
        {chooseText}
        <input type="file" accept={accept} multiple={multiple} data-upload="choose" className="sr-only" disabled={disabled} onChange={onPick} />
      </label>
    </>
  );
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
  /** Take one page out of a multi-page document. */
  onRemovePage?: (documentTypeKey: string, documentId: string, label: string) => void;
  /** Move a page one place earlier (-1) or later (1). */
  onMovePage?: (documentTypeKey: string, documentId: string, delta: -1 | 1) => void;
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
export function ClaimantDocumentsCard({ documents, busyKey, notice, noticeFailed = false, requiredSummary, requiredComplete = false, onUpload, onRemove, onRetry, onRemovePage, onMovePage, previewOnly = false, uploadsBlockedReason = null }: ClaimantDocumentsCardProps) {
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

            {doc.kind !== "Evidence" && doc.allowsMultipleFiles && (doc.files?.length ?? 0) > 0 && (
              <div className="mt-2 ml-5.5" data-testid="page-list">
                <ol className="space-y-1" aria-label={`Pages of ${doc.label}`}>
                  {doc.files!.map((file, index) => (
                    <li key={file.documentId} className="flex items-center justify-between gap-2 rounded-dls-control bg-dls-bg-page px-2.5 py-1.5 text-xs text-dls-text-secondary">
                      <span className="min-w-0 truncate">Page {index + 1}: {file.fileName}</span>
                      <span className="flex shrink-0 gap-1">
                        <Button variant="ghost" onClick={() => onMovePage?.(doc.documentTypeKey, file.documentId, -1)} disabled={locked || index === 0 || !onMovePage} aria-label={`Move page ${index + 1} of ${doc.label} earlier`}>Up</Button>
                        <Button variant="ghost" onClick={() => onMovePage?.(doc.documentTypeKey, file.documentId, 1)} disabled={locked || index === doc.files!.length - 1 || !onMovePage} aria-label={`Move page ${index + 1} of ${doc.label} later`}>Down</Button>
                        <Button variant="ghost" onClick={() => onRemovePage?.(doc.documentTypeKey, file.documentId, doc.label)} disabled={locked || !onRemovePage} aria-label={`Remove page ${index + 1} of ${doc.label}`}>Remove</Button>
                      </span>
                    </li>
                  ))}
                </ol>
                {(doc.pageCount ?? 0) > 0 && (
                  <p className="mt-1 text-[11px] leading-4 text-dls-text-muted">These pages are sent as one PDF of {doc.pageCount} {doc.pageCount === 1 ? "page" : "pages"}, in this order.</p>
                )}
              </div>
            )}

            <div className="mt-2 ml-5.5 flex flex-wrap items-center gap-2">
              <SlotPicker
                formats={doc.acceptedFormats ?? []}
                multiple={doc.allowsMultipleFiles === true}
                pages={doc.kind !== "Evidence" && doc.allowsMultipleFiles === true}
                uploaded={doc.uploaded}
                full={doc.allowsMultipleFiles === true && (doc.fileCount ?? 0) >= (doc.maxFiles ?? 1)}
                busy={busyKey === doc.documentTypeKey}
                locked={locked}
                onPick={(event) => onUpload(event, doc.documentTypeKey, doc.label)}
              />
              {doc.canRetry && (
                <Button variant="secondary" icon={<RefreshCw aria-hidden="true" />} onClick={() => onRetry(doc.documentTypeKey, doc.label)} disabled={locked}>
                  Try again
                </Button>
              )}
              {doc.uploaded && (
                <Button variant="ghost" icon={<Trash2 aria-hidden="true" />} onClick={() => onRemove(doc.documentTypeKey, doc.label)} disabled={locked}>
                  {doc.kind !== "Evidence" && doc.allowsMultipleFiles && (doc.fileCount ?? 0) > 1 ? "Remove all pages" : "Remove"}
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
            <div className="flex flex-wrap items-center gap-2">
              <SlotPicker
                formats={[]}
                multiple
                uploaded={false}
                full={false}
                busy={busyKey === SOMETHING_ELSE_KEY}
                locked={locked}
                onPick={(event) => onUpload(event, SOMETHING_ELSE_KEY, "Document")}
              />
            </div>
          </div>
        </li>
      </ul>
      {previewOnly && <p className="mt-3 text-xs leading-5 text-dls-text-muted">Preview only. Nothing can be sent from here.</p>}
      {uploadsBlockedReason && !previewOnly && <p role="status" className="mt-3 text-xs leading-5 text-dls-text-muted">{uploadsBlockedReason}</p>}
      {notice && <p role={noticeFailed ? "alert" : "status"} aria-live="polite" className={`mt-3 text-xs leading-5 ${noticeFailed ? "font-semibold text-dls-danger-text" : "text-dls-text-muted"}`}>{notice}</p>}
    </div>
  );
}
