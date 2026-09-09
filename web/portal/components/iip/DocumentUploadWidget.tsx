"use client";

import { Loader2, Upload } from "lucide-react";
import { useRef, useState } from "react";
import { Button } from "@/components/ui/Button";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { compressImageForUpload, offlineUploadMessage } from "@/lib/compressImageForUpload";

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
// ProviderNotConfiguredExceptionHandler returns (see AGENTS.md I9 — never let a raw provider
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
  const [error, setError] = useState<string | null>(null);
  const [notConfiguredCapability, setNotConfiguredCapability] = useState<string | null>(null);

  async function handleFiles(fileList: FileList | null) {
    if (!fileList || fileList.length === 0) {
      return;
    }

    setError(null);
    setStatus(null);
    setNotConfiguredCapability(null);

    if (typeof navigator !== "undefined" && navigator.onLine === false) {
      setError(offlineUploadMessage());
      return;
    }

    setUploading(true);
    setProgress(0);

    try {
      const form = new FormData();
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
            // returns (see src/InsuranceIntelligence.Api/Local) — render it as an honest
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

      setStatus("Documents uploaded. Extraction will run on the server.");
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
      <Button variant="secondary" icon={uploading ? <Loader2 className="animate-spin" /> : <Upload />} disabled={uploading} onClick={() => inputRef.current?.click()}>
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
      {status ? <div className="text-[12px] text-dls-text-primary">{status}</div> : null}
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
