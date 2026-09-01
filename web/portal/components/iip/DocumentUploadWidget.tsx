"use client";

import { Loader2, Upload } from "lucide-react";
import { useRef, useState } from "react";
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
export function DocumentUploadWidget({ caseId, role, onUploaded }: DocumentUploadWidgetProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState<number | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleFiles(fileList: FileList | null) {
    if (!fileList || fileList.length === 0) {
      return;
    }

    setError(null);
    setStatus(null);

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
          let message = `Upload failed with HTTP ${xhr.status}.`;
          try {
            const body = JSON.parse(xhr.responseText) as {
              message?: string;
              errors?: Record<string, string[]>;
              title?: string;
              detail?: string;
            };
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
      setError(err instanceof Error ? err.message : "Upload failed.");
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
      <button
        type="button"
        disabled={uploading}
        onClick={() => inputRef.current?.click()}
        className="inline-flex min-h-11 items-center gap-2 rounded-2xl border border-cloud bg-white px-4 py-3 text-[13px] font-medium text-navy hover-lift disabled:opacity-50 focus-ring"
      >
        {uploading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
        {uploading ? "Uploading…" : "Upload photo or document"}
      </button>
      <div className="text-[11.5px] text-stone">
        Photos are compressed on-device for slow networks. PDF and text files upload as-is. Max 25 MB per file.
      </div>
      {progress !== null ? (
        <div className="h-1.5 overflow-hidden rounded-full bg-cloud">
          <div
            className="h-full rounded-full bg-info transition-[width]"
            style={{ width: `${progress}%` }}
          />
        </div>
      ) : null}
      {status ? <div className="text-[12px] text-navy">{status}</div> : null}
      {error ? <div className="text-[12px] text-[hsl(var(--danger))]">{error}</div> : null}
    </div>
  );
}
