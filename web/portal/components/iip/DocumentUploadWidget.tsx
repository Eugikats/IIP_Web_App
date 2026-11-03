"use client";

import { Loader2, Upload } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { Link } from "@/components/iip/NextRouterCompat";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { compressImageForUpload, offlineUploadMessage } from "@/lib/compressImageForUpload";
import { listDocumentUploadOptions, type DocumentTypeUploadOption } from "@/lib/documentTypes";
import { extractionHref } from "@/lib/workItemLinks";

type DocumentUploadWidgetProps = {
  caseId: string;
  role: string;
  onUploaded: () => void | Promise<void>;
};

/**
 * Low-bandwidth claim document upload: camera/file picker, client JPEG downscale for photos,
 * progress via XHR, honest offline/error states (no fake queued success).
 */
// Human-readable labels for the stable capability identifiers the API's
// ProviderNotConfiguredExceptionHandler returns (see AGENTS.md I9, never let a raw provider
// error reach a user; render an honest, actionable not-configured state instead).
const CAPABILITY_LABELS: Record<string, string> = {
  DocumentStorage: "Document storage",
  DocumentProcessing: "Document processing",
};

class NotConfiguredError extends Error {
  constructor(public readonly capability: string) {
    super(`${capability} is not configured.`);
  }
}

export function DocumentUploadWidget({ caseId, role, onUploaded }: DocumentUploadWidgetProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState<number | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [documentTypeKey, setDocumentTypeKey] = useState("");
  const [documentTypeOptions, setDocumentTypeOptions] = useState<DocumentTypeUploadOption[]>([]);
  const [documentTypeOptionsLoading, setDocumentTypeOptionsLoading] = useState(true);
  const [documentTypeOptionsError, setDocumentTypeOptionsError] = useState<string | null>(null);
  const [reviewDocumentId, setReviewDocumentId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notConfiguredCapability, setNotConfiguredCapability] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    void listDocumentUploadOptions(role).then((result) => {
      if (!active) return;
      if (result.ok) {
        setDocumentTypeOptions(result.value);
        setDocumentTypeOptionsError(null);
      } else {
        setDocumentTypeOptions([]);
        setDocumentTypeOptionsError(result.error);
      }
      setDocumentTypeOptionsLoading(false);
    });
    return () => {
      active = false;
    };
  }, [role]);

  async function handleFiles(fileList: FileList | null) {
    if (!fileList || fileList.length === 0) {
      return;
    }

    setError(null);
    setStatus(null);
    setNotConfiguredCapability(null);
    setReviewDocumentId(null);

    if (!documentTypeKey) {
      setError("Choose what kind of claim document you are uploading.");
      return;
    }

    if (typeof navigator !== "undefined" && navigator.onLine === false) {
      setError(offlineUploadMessage());
      return;
    }

    setUploading(true);
    setProgress(0);
    let processingCapability: string | null = null;

    try {
      const form = new FormData();
      form.append("documentTypeKey", documentTypeKey);
      for (const original of Array.from(fileList)) {
        const prepared = await compressImageForUpload(original);
        form.append("files", prepared, prepared.name);
      }

      await new Promise<void>((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open("POST", `${apiBaseUrl}/api/v1/cases/${caseId}/documents`);
        const headers = scopedHeaders(role, "");
        for (const [key, value] of Object.entries(headers)) {
          xhr.setRequestHeader(key, value);
        }

        xhr.upload.onprogress = (event) => {
          if (event.lengthComputable && event.total > 0) {
            setProgress(Math.round((event.loaded / event.total) * 100));
          }
        };

        xhr.onload = () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            try {
              const body = JSON.parse(xhr.responseText) as {
                documents?: Array<{ documentId?: string }>;
                processingCapability?: string | null;
              };
              processingCapability = typeof body.processingCapability === "string" ? body.processingCapability : null;
              const firstDocumentId = body.documents?.[0]?.documentId;
              if (!processingCapability && typeof firstDocumentId === "string") setReviewDocumentId(firstDocumentId);
            } catch {
              // Upload success remains usable when an older server omits its response body.
            }
            resolve();
            return;
          }
          let message = `Upload failed.`;
          try {
            const body = JSON.parse(xhr.responseText) as {
              message?: string;
              errors?: Record<string, string[]>;
              title?: string;
              detail?: string;
              capability?: string;
            };
            // 503 + capability is the structured shape ProviderNotConfiguredExceptionHandler
            // returns (see src/InsuranceIntelligence.Api/Local), render it as an honest
            // not-configured state, distinct from an ordinary upload failure.
            if (xhr.status === 503 && typeof body.capability === "string" && body.capability.trim()) {
              reject(new NotConfiguredError(body.capability));
              return;
            }
            if (typeof body.message === "string" && body.message.trim()) {
              message = body.message;
            } else if (body.errors) {
              const first = Object.values(body.errors).flat()[0];
              if (first) message = first;
            } else if (typeof body.detail === "string" && body.detail.trim()) {
              message = body.detail;
            }
          } catch {
            // Keep the HTTP fallback when the body is not JSON.
          }
          reject(new Error(message));
        };

        xhr.onerror = () => {
          reject(
            new Error(
              typeof navigator !== "undefined" && navigator.onLine === false
                ? offlineUploadMessage()
                : "Upload failed. Check your connection and try again.",
            ),
          );
        };

        xhr.send(form);
      });

      if (processingCapability === "DocumentProcessing") {
        setStatus("Upload saved. Document processing is not configured, so no fields were extracted.");
        setNotConfiguredCapability(processingCapability);
      } else {
        setStatus("Upload complete. Extraction results are ready for review.");
      }
      setProgress(100);
      await onUploaded();
    } catch (err) {
      if (err instanceof NotConfiguredError) {
        setNotConfiguredCapability(err.capability);
      } else {
        setError(err instanceof Error ? err.message : "Upload failed.");
      }
      setProgress(null);
    } finally {
      setUploading(false);
      if (inputRef.current) {
        inputRef.current.value = "";
      }
    }
  }

  return (
    <div className="mt-3 space-y-2">
      <label className="block space-y-1.5 text-[11.5px] font-semibold text-dls-text-secondary">
        <span>Document type</span>
        <Select aria-label="Document type for upload" value={documentTypeKey} onChange={(event) => setDocumentTypeKey(event.target.value)} disabled={uploading || documentTypeOptionsLoading || documentTypeOptions.length === 0}>
          <option value="">{documentTypeOptionsLoading ? "Loading document types…" : "Choose a document type"}</option>
          {documentTypeOptions.map((option) => <option key={option.key} value={option.key}>{option.customerLabel}</option>)}
        </Select>
      </label>
      <div className="text-[11.5px] text-dls-text-muted">All files selected together will be assigned to this document type. Upload another type separately.</div>
      {documentTypeOptionsError ? <div className="text-[12px] text-dls-danger-text">{documentTypeOptionsError}</div> : null}
      <input
        ref={inputRef}
        type="file"
        aria-label="Upload claim photos or documents"
        accept="image/*,.pdf,.tif,.tiff,.txt,image/jpeg,image/png,application/pdf"
        capture="environment"
        multiple
        className="sr-only"
        disabled={uploading}
        onChange={(event) => {
          void handleFiles(event.target.files);
        }}
      />
      <Button variant="secondary" icon={uploading ? <Loader2 className="animate-spin" /> : <Upload />} disabled={uploading || documentTypeOptionsLoading || documentTypeOptions.length === 0 || !documentTypeKey} onClick={() => inputRef.current?.click()}>
        {uploading ? "Uploading…" : "Upload photo or document"}
      </Button>
      <div className="text-[11.5px] text-dls-text-muted">
        Photos are compressed on-device for slow networks. PDF and text files upload as-is. Max 25 MB per file.
      </div>
      {progress !== null ? (
        <div className="h-1.5 overflow-hidden rounded-dls-pill bg-dls-border-subtle">
          <div
            className="h-full rounded-dls-pill bg-dls-primary transition-[width] duration-dls-base ease-dls"
            style={{ width: `${progress}%` }}
          />
        </div>
      ) : null}
      {status ? (
        <div aria-live="polite" className="space-y-1 text-[12px] text-dls-text-primary">
          <div>{status}</div>
          {reviewDocumentId && extractionHref(caseId, reviewDocumentId) ? (
            <Link to={extractionHref(caseId, reviewDocumentId)!} className="font-semibold text-dls-primary hover:underline">Review extracted details</Link>
          ) : null}
        </div>
      ) : null}
      {notConfiguredCapability ? (
        <div className="text-[12px] text-dls-warning-text">
          {CAPABILITY_LABELS[notConfiguredCapability] ?? notConfiguredCapability} is not configured for this workspace. Ask a workspace administrator to configure it.
        </div>
      ) : error ? (
        <div className="text-[12px] text-dls-danger-text">{error}</div>
      ) : null}
    </div>
  );
}
