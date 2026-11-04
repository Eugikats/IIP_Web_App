"use client";

import { useEffect, useState } from "react";
import { ArrowRight, AlertTriangle, Check, Loader2, LockKeyhole, ShieldCheck } from "lucide-react";
import { useNavigate } from "@/components/iip/NextRouterCompat";
import iipLogo from "@/assets/iip-logo";
import { Button } from "@/components/ui/Button";
import { canUseEntraBrowserAuth, canUseLocalTestDoubles, runtimeProfile } from "@/lib/config";
import { PasswordSignIn } from "@/components/iip/PasswordSignIn";
import { describeSignInError, getEntraApiAccessToken } from "@/lib/entraAuth";
import { loadCapabilities, type AuthCapabilities } from "@/lib/platformAuth";

function environmentLabel() {
  if (runtimeProfile.toLowerCase() === "managedapplicationreviewer") return "Reviewer environment";
  return `${runtimeProfile} environment`;
}

export default function Login() {
  const navigate = useNavigate();
  const [signingIn, setSigningIn] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [capabilities, setCapabilities] = useState<AuthCapabilities | null>(null);

  useEffect(() => {
    void loadCapabilities().then(setCapabilities);
  }, []);

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

  useEffect(() => {
    if (canUseLocalTestDoubles || !canUseEntraBrowserAuth) return;
    if (!/[#&?](code|error)=/.test(window.location.hash)) return;
    try {
      if (window.sessionStorage.getItem("iip.signin.finish") === window.location.hash) {
        setError("Sign-in could not be completed. Try again.");
        return;
      }
      window.sessionStorage.setItem("iip.signin.finish", window.location.hash);
    } catch {
      // Storage can be blocked; finishing sign-in once is still correct.
    }
    void signIn();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const passwordAvailable = capabilities?.passwordSignIn === true;
  const microsoftAvailable = canUseEntraBrowserAuth || canUseLocalTestDoubles;

  return (
    <main className="grid min-h-screen bg-dls-bg-page text-dls-text-primary lg:grid-cols-[minmax(0,1.08fr)_minmax(420px,0.92fr)]">
      <section className="hidden min-h-screen flex-col justify-between border-r border-dls-border-subtle bg-dls-surface px-12 py-10 lg:flex xl:px-16 xl:py-12">
        <div className="flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
            <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
          </span>
          <div>
            <div className="text-sm font-semibold">IIP Operations</div>
            <div className="mt-0.5 text-xs text-dls-text-muted">Insurance Intelligence Platform</div>
          </div>
        </div>

        <div className="max-w-2xl">
          <p className="mb-5 text-sm font-medium text-dls-text-secondary">Secure insurance operations workspace</p>
          <h1 className="max-w-xl text-4xl font-semibold leading-tight xl:text-5xl">
            Prepare insurance work with evidence, controls, and human accountability.
          </h1>
          <p className="mt-6 max-w-lg text-base leading-7 text-dls-text-secondary">
            Review claims, documents, approvals, communications, and audit evidence from one governed staff workspace.
          </p>
          <div className="mt-10 grid max-w-xl gap-4 border-t border-dls-border-subtle pt-7 sm:grid-cols-3">
            {["Server-resolved access", "Evidence-linked work", "Human-reviewed decisions"].map((item) => (
              <div key={item} className="flex items-start gap-2 text-sm leading-5 text-dls-text-secondary">
                <Check className="mt-0.5 h-4 w-4 shrink-0 text-dls-success-text" aria-hidden="true" />
                <span>{item}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="text-xs text-dls-text-muted">Authorized insurer staff only</div>
      </section>

      <section className="flex min-h-screen items-center justify-center px-6 py-10 sm:px-10 lg:px-14">
        <div className="w-full max-w-md">
          <div className="mb-12 flex items-center gap-3 lg:hidden">
            <span className="grid h-10 w-10 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
              <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
            </span>
            <div>
              <div className="text-sm font-semibold">IIP Operations</div>
              <div className="text-xs text-dls-text-muted">Insurance Intelligence Platform</div>
            </div>
          </div>

          <div className="mb-8 flex items-center justify-between gap-4 border-b border-dls-border-subtle pb-4">
            <span className="text-sm font-medium text-dls-text-secondary">Staff access</span>
            <span className="text-xs text-dls-text-muted">{environmentLabel()}</span>
          </div>
          <h2 className="text-3xl font-semibold leading-tight">Sign in to IIP Operations</h2>
          <p className="mt-3 text-sm leading-6 text-dls-text-secondary">
            Use your organization account. Your tenant, workspace, role, and permissions are resolved securely after sign-in.
          </p>

          {(microsoftAvailable || !passwordAvailable) && (
            <>
              <div className="mt-8 flex items-start gap-3 border-y border-dls-border-subtle py-4">
                <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-dls-success-text" aria-hidden="true" />
                <div>
                  <div className="text-sm font-medium">Microsoft Entra ID</div>
                  <div className="mt-1 text-xs leading-5 text-dls-text-secondary">Single sign-on and your organization&apos;s access policies apply.</div>
                </div>
              </div>

              <Button variant="primary" onClick={signIn} disabled={signingIn || !microsoftAvailable} className="mt-8 min-h-12 w-full gap-3 py-3 text-sm">
                {signingIn ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <LockKeyhole className="h-4 w-4" aria-hidden="true" />}
                <span>{signingIn ? "Connecting to Microsoft" : canUseLocalTestDoubles ? "Enter local workspace" : "Continue with Microsoft"}</span>
                {!signingIn && <ArrowRight className="h-4 w-4" aria-hidden="true" />}
              </Button>
            </>
          )}

          {!microsoftAvailable && !passwordAvailable && (
            <div className="mt-5 flex items-start gap-2 rounded-dls-control border border-dls-border-subtle bg-dls-surface px-3 py-3 text-xs leading-5 text-dls-text-secondary">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-dls-warning-text" aria-hidden="true" />
              <span>Microsoft sign-in is not configured for this environment.</span>
            </div>
          )}
          {error && (
            <div role="alert" className="mt-5 flex items-start gap-2 rounded-dls-control border border-dls-danger-border bg-dls-danger-tint px-3 py-3 text-xs leading-5 text-dls-danger-text">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
              <span>{error}</span>
            </div>
          )}
          {passwordAvailable && <PasswordSignIn onSignedIn={() => navigate("/app")} allowSignUp={capabilities?.signUp === true} />}
          <p className="mt-8 text-xs leading-5 text-dls-text-muted">
            Access is limited to authorized staff. Consequential insurance decisions remain with accountable human reviewers.
          </p>
        </div>
      </section>
    </main>
  );
}
