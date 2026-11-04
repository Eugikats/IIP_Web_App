"use client";

import { useState } from "react";
import { Download } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type ClaimsPackDownloadProps = { caseId: string; role: string };

/**
 * "Download claims pack" (build-loop item 24): one zip with a summary of the claim as a PDF, the customer's
 * documents renamed after what they are, the evidence exactly as received and a manifest of SHA-256 hashes.
 * Anything the platform prepared inside it is labelled advisory. Each download is recorded in the audit log.
 */
export function ClaimsPackDownload({ caseId, role }: ClaimsPackDownloadProps) {
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; text: string } | null>(null);

  async function download() {
    setBusy(true);
    setResult(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/compliance/cases/${caseId}/claims-pack`, { headers: scopedHeaders(role, ""), cache: "no-store" });
      if (!response.ok) {
        const payload = (await response.json().catch(() => ({}))) as { message?: string };
        setResult({ ok: false, text: payload.message ?? (response.status === 403 ? "You do not have permission to export a claims pack." : "The pack could not be made. Try again shortly.") });
        return;
      }

      const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(response.headers.get("content-disposition") ?? "")?.[1] ?? "ClaimsPack.zip";
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = decodeURIComponent(name);
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      const files = response.headers.get("x-pack-files");
      setResult({ ok: true, text: `Downloaded ${decodeURIComponent(name)}${files ? ` (${files} files)` : ""}, ${(blob.size / 1024).toFixed(0)} KB.` });
    } catch {
      setResult({ ok: false, text: "The service could not be reached. Check your connection and try again." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div data-testid="claims-pack">
      <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Claims pack</div>
      <p className="mt-1 text-[12.5px] text-dls-text-secondary">A zip with a summary of the claim, the documents renamed, the evidence as received and a list of file fingerprints. Parts prepared by the platform are marked advisory.</p>
      <div className="mt-2">
        <Button variant="secondary" icon={<Download aria-hidden="true" />} disabled={busy} onClick={() => void download()}>Download claims pack</Button>
      </div>
      {result && <p role={result.ok ? "status" : "alert"} className={`mt-2 text-[12.5px] ${result.ok ? "text-dls-text-secondary" : "text-dls-danger-text"}`}>{result.text}</p>}
    </div>
  );
}
