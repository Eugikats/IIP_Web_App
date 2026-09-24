"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { AlertTriangle, Loader2, ShieldCheck } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { redeemMagicLink, storeSession } from "@/lib/clientPortal";

// Claimant-facing (tier 6, stricter rules). This screen renders only the redeem-result error
// message (already sanitised at the source, see AGENTS.md) or a generic "verifying" state,
// nothing staff-only is ever in scope here.
export default function ClaimAccessVerify() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token");
  const [error, setError] = useState<string | null>(null);
  const attempted = useRef(false);

  useEffect(() => {
    if (attempted.current) return;
    attempted.current = true;

    if (!token) {
      setError("This link is missing its access token. Request a new one.");
      return;
    }

    void (async () => {
      const result = await redeemMagicLink(token);
      if (!result.ok) {
        setError(result.error);
        return;
      }
      storeSession(result.session);
      router.replace(result.session.caseId ? "/access/claim" : "/access/new-claim");
    })();
  }, [token, router]);

  return (
    <main className="grid min-h-screen place-items-center bg-dls-surface px-6 text-dls-text-primary">
      <div className="w-full max-w-sm text-center">
        <span className="mx-auto grid h-10 w-10 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
          <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
        </span>

        {error ? (
          <>
            <AlertTriangle className="mx-auto mt-6 h-6 w-6 text-dls-danger-text" aria-hidden="true" />
            <h1 className="mt-4 text-xl font-bold text-dls-text-primary">This link didn&apos;t work</h1>
            <p className="mt-2 text-sm leading-6 text-dls-text-secondary">{error}</p>
            <a
              href="/access"
              className="dls-focus-outline mt-6 inline-flex min-h-11 items-center justify-center rounded-dls-control bg-dls-primary px-5 py-2.5 text-sm font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover"
            >
              Request a new link
            </a>
          </>
        ) : (
          <>
            <ShieldCheck className="mx-auto mt-6 h-6 w-6 text-dls-success-text" aria-hidden="true" />
            <h1 className="mt-4 text-xl font-bold text-dls-text-primary">Verifying your access link</h1>
            <p className="mt-2 flex items-center justify-center gap-2 text-sm leading-6 text-dls-text-secondary">
              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> One moment while we confirm your details.
            </p>
          </>
        )}
      </div>
    </main>
  );
}
