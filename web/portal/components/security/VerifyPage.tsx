"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { LockKeyhole } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { getMfaStatus, MfaError, signOutMfa, type MfaStatus } from "@/lib/security/mfa";
import { signOut } from "@/lib/entraAuth";
import VerifyForm from "./VerifyForm";

// The page a signed-in person lands on when their organisation requires a second step and they have not done it yet.
// It is outside the app, so it works before the app's screens are allowed to load.

function safeNext(value: string | null) {
  // Only a path inside this site: never an address somebody else could send a person to.
  return value && value.startsWith("/") && !value.startsWith("//") && !value.startsWith("/security/verify") ? value : "/app";
}

export default function VerifyPage() {
  const params = useSearchParams();
  const next = safeNext(params?.get("next") ?? null);
  const [status, setStatus] = useState<MfaStatus | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void getMfaStatus()
      .then((result) => {
        if (!result.mustVerify) window.location.replace(next);
        else setStatus(result);
      })
      .catch((failure) => setError(failure instanceof MfaError ? failure.message : "Your status could not be loaded. Sign in again."));
  }, [next]);

  return (
    <main className="grid min-h-screen place-items-center bg-dls-bg-page px-6 py-10 text-dls-text-primary">
      <div className="w-full max-w-md rounded-2xl border border-dls-border-subtle bg-dls-surface p-7 shadow-sm">
        <div className="flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-dls-control bg-dls-primary-soft text-dls-primary"><LockKeyhole className="h-5 w-5" aria-hidden /></span>
          <div>
            <h1 className="m-0 text-[18px] font-bold">Verify it is you</h1>
            <p className="m-0 text-[12.5px] text-dls-text-muted">Your organisation asks for a second step after you sign in.</p>
          </div>
        </div>
        <div className="mt-5">
          {error ? (
            <p role="alert" className="m-0 text-[13px] text-dls-danger-text">{error}</p>
          ) : status ? (
            <VerifyForm status={status} onVerified={() => window.location.replace(next)} />
          ) : (
            <LoadingSkeleton rows={2} />
          )}
        </div>
        <div className="mt-6 border-t border-dls-border-subtle pt-4">
          <Button
            variant="ghost"
            onClick={() => {
              void signOutMfa().catch(() => undefined).finally(() => void signOut());
            }}
          >
            Sign out
          </Button>
        </div>
      </div>
    </main>
  );
}
