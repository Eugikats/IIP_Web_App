"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { AlertTriangle, Loader2, ShieldCheck } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { redeemMagicLink, storeSession } from "@/lib/clientPortal";

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
      router.replace("/access/claim");
    })();
  }, [token, router]);

  return (
    <main className="grid min-h-screen place-items-center bg-white px-6 text-ink">
      <div className="w-full max-w-sm text-center">
        <span className="mx-auto grid h-10 w-10 place-items-center rounded-md border border-line bg-white">
          <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
        </span>

        {error ? (
          <>
            <AlertTriangle className="mx-auto mt-6 h-6 w-6 text-danger" />
            <h1 className="mt-4 text-xl font-semibold text-ink">This link didn&apos;t work</h1>
            <p className="mt-2 text-sm leading-6 text-muted">{error}</p>
            <a
              href="/access"
              className="mt-6 inline-flex min-h-11 items-center justify-center rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-l2 hover:bg-primary/90 focus-ring"
            >
              Request a new link
            </a>
          </>
        ) : (
          <>
            <ShieldCheck className="mx-auto mt-6 h-6 w-6 text-success" />
            <h1 className="mt-4 text-xl font-semibold text-ink">Verifying your access link</h1>
            <p className="mt-2 flex items-center justify-center gap-2 text-sm leading-6 text-muted">
              <Loader2 className="h-4 w-4 animate-spin" /> One moment while we confirm your details.
            </p>
          </>
        )}
      </div>
    </main>
  );
}
