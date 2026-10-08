"use client";

import { useState } from "react";
import { ArrowRight, AlertTriangle, Check, Loader2, LockKeyhole, ShieldCheck } from "lucide-react";
import { useNavigate } from "@/components/iip/NextRouterCompat";
import iipLogo from "@/assets/iip-logo";
import { canUseEntraBrowserAuth, canUseLocalTestDoubles, runtimeProfile } from "@/lib/config";
import { describeSignInError, getEntraApiAccessToken } from "@/lib/entraAuth";

function environmentLabel() {
  if (runtimeProfile.toLowerCase() === "managedapplicationreviewer") return "Reviewer environment";
  return `${runtimeProfile} environment`;
}

export default function Login() {
  const navigate = useNavigate();
  const [signingIn, setSigningIn] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function signIn() {
    setSigningIn(true);
    setError(null);
    try {
      if (canUseLocalTestDoubles) {
        navigate("/app");
        return;
      }
      const token = await getEntraApiAccessToken();
      if (token) navigate("/app");
    } catch (failure) {
      setError(describeSignInError(failure));
      setSigningIn(false);
    }
  }

  const signInAvailable = canUseEntraBrowserAuth || canUseLocalTestDoubles;

  return (
    <main className="grid min-h-screen bg-white text-ink lg:grid-cols-[minmax(0,1.08fr)_minmax(420px,0.92fr)]">
      <section className="relative hidden min-h-screen flex-col justify-between overflow-hidden bg-ink px-12 py-10 text-white lg:flex xl:px-16 xl:py-12">
        <div className="absolute inset-y-0 right-0 w-px bg-white/15" />
        <div className="relative flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-md bg-white">
            <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
          </span>
          <div>
            <div className="text-sm font-semibold">IIP Operations</div>
            <div className="mt-0.5 text-xs text-white/65">Insurance Intelligence Platform</div>
          </div>
        </div>

        <div className="relative max-w-2xl">
          <p className="mb-5 text-sm font-medium text-white/65">Secure insurance operations workspace</p>
          <h1 className="max-w-xl text-4xl font-semibold leading-tight text-white xl:text-5xl">
            Prepare insurance work with evidence, controls, and human accountability.
          </h1>
          <p className="mt-6 max-w-lg text-base leading-7 text-white/70">
            Review claims, documents, approvals, communications, and audit evidence from one governed staff workspace.
          </p>
          <div className="mt-10 grid max-w-xl gap-4 border-t border-white/15 pt-7 sm:grid-cols-3">
            {["Server-resolved access", "Evidence-linked work", "Human-reviewed decisions"].map((item) => (
              <div key={item} className="flex items-start gap-2 text-sm leading-5 text-white/75">
                <Check className="mt-0.5 h-4 w-4 shrink-0 text-[#7dd3fc]" />
                <span>{item}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="relative text-xs text-white/55">Authorized insurer staff only</div>
      </section>

      <section className="flex min-h-screen items-center justify-center px-6 py-10 sm:px-10 lg:px-14">
        <div className="w-full max-w-md">
          <div className="mb-12 flex items-center gap-3 lg:hidden">
            <span className="grid h-10 w-10 place-items-center rounded-md border border-line bg-white">
              <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
            </span>
            <div>
              <div className="text-sm font-semibold">IIP Operations</div>
              <div className="text-xs text-muted">Insurance Intelligence Platform</div>
            </div>
          </div>

          <div className="mb-8 flex items-center justify-between gap-4 border-b border-line pb-4">
            <span className="text-sm font-medium text-ink-soft">Staff access</span>
            <span className="text-xs text-muted">{environmentLabel()}</span>
          </div>
          <h2 className="text-3xl font-semibold leading-tight text-ink">Sign in to IIP Operations</h2>
          <p className="mt-3 text-sm leading-6 text-muted">
            Use your organization account. Your tenant, workspace, role, and permissions are resolved securely after sign-in.
          </p>

          <div className="mt-8 flex items-start gap-3 border-y border-line py-4">
            <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-success" />
            <div>
              <div className="text-sm font-medium text-ink">Microsoft Entra ID</div>
              <div className="mt-1 text-xs leading-5 text-muted">Single sign-on and your organization&apos;s access policies apply.</div>
            </div>
          </div>

          <button
            type="button"
            onClick={signIn}
            disabled={signingIn || !signInAvailable}
            className="mt-8 inline-flex min-h-12 w-full items-center justify-center gap-3 rounded-md bg-primary px-5 py-3 text-sm font-semibold text-white shadow-l2 transition-colors hover:bg-primary/90 focus-ring disabled:cursor-not-allowed disabled:opacity-55"
          >
            {signingIn ? <Loader2 className="h-4 w-4 animate-spin" /> : <LockKeyhole className="h-4 w-4" />}
            <span>{signingIn ? "Connecting to Microsoft" : canUseLocalTestDoubles ? "Enter local workspace" : "Continue with Microsoft"}</span>
            {!signingIn && <ArrowRight className="h-4 w-4" />}
          </button>

          {!signInAvailable && (
            <div className="mt-5 flex items-start gap-2 rounded-md border border-warning/30 bg-warning/8 px-3 py-3 text-xs leading-5 text-ink-soft">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-warning" />
              <span>Microsoft sign-in is not configured for this environment.</span>
            </div>
          )}
          {error && (
            <div role="alert" className="mt-5 flex items-start gap-2 rounded-md border border-danger/25 bg-danger/7 px-3 py-3 text-xs leading-5 text-danger">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}
          <p className="mt-8 text-xs leading-5 text-muted">
            Access is limited to authorized staff. Consequential insurance decisions remain with accountable human reviewers.
          </p>
        </div>
      </section>
    </main>
  );
}