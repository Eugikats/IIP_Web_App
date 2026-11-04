"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { AlertTriangle, Download, Loader2, ShieldCheck } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { Button } from "@/components/ui/Button";
import { apiBaseUrl } from "@/lib/config";

/**
 * The page a secure link to a claims pack opens (build-loop item 25). Nothing is downloaded until the person presses the
 * button, so a mail scanner that only opens links does not use up the link's downloads. It says plainly when a link has
 * expired or run out, and never shows anything about the claim itself.
 */
export default function SharedClaimsPack() {
  const token = useSearchParams().get("token");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(token ? null : "This link is missing its token. Ask the sender for a new one.");
  const [done, setDone] = useState(false);

  async function download() {
    if (!token) return;
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/shared/claims-packs/${encodeURIComponent(token)}`, { cache: "no-store" });
      if (!response.ok) {
        const payload = (await response.json().catch(() => ({}))) as { message?: string };
        setError(payload.message ?? "This link has expired or is no longer valid. Ask the sender for a new one.");
        return;
      }

      const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(response.headers.get("content-disposition") ?? "")?.[1] ?? "ClaimsPack.zip";
      const url = URL.createObjectURL(await response.blob());
      const link = document.createElement("a");
      link.href = url;
      link.download = decodeURIComponent(name);
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      setDone(true);
    } catch {
      setError("The download could not be reached. Check your connection and try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="grid min-h-screen place-items-center bg-dls-surface px-6 text-dls-text-primary">
      <div className="w-full max-w-sm text-center">
        <span className="mx-auto grid h-10 w-10 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
          <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
        </span>
        <ShieldCheck className="mx-auto mt-6 h-6 w-6 text-dls-text-muted" aria-hidden="true" />
        <h1 className="mt-4 text-xl font-bold">Claims pack</h1>
        <p className="mt-2 text-sm leading-6 text-dls-text-secondary">
          A claims pack was shared with you. It holds a summary of a claim, the customer&apos;s documents and a list of file fingerprints, and contains personal information. Keep it secure.
        </p>
        <div className="mt-6">
          <Button icon={busy ? <Loader2 className="animate-spin" aria-hidden="true" /> : <Download aria-hidden="true" />} disabled={busy || !token} onClick={() => void download()}>
            Download the pack
          </Button>
        </div>
        {done && <p role="status" className="mt-4 text-sm text-dls-text-secondary" data-testid="shared-pack-done">The pack was downloaded.</p>}
        {error && (
          <p role="alert" className="mt-4 flex items-start justify-center gap-2 text-sm leading-6 text-dls-danger-text" data-testid="shared-pack-error">
            <AlertTriangle className="mt-1 h-4 w-4 shrink-0" aria-hidden="true" />
            <span>{error}</span>
          </p>
        )}
      </div>
    </main>
  );
}
