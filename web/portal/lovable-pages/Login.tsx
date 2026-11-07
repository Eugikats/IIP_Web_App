"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { AlertTriangle } from "lucide-react";
import { useNavigate } from "@/components/iip/NextRouterCompat";
import { GoogleIcon, MicrosoftIcon } from "@/components/brand/ProviderIcons";
import MarketingNav from "@/components/marketing/MarketingNav";
import { PasswordSignIn } from "@/components/iip/PasswordSignIn";
import { canUseEntraBrowserAuth, canUseExternalIdSignIn, canUseLocalTestDoubles, runtimeProfile } from "@/lib/config";
import { describeSignInError, finishReturningSignIn, startSignIn, type SignInMethod } from "@/lib/entraAuth";
import { loadCapabilities, type AuthCapabilities } from "@/lib/platformAuth";

// The layout, copy shape and styling follow CIP's sign-in page (CIP_Web_App apps/web/components/auth/LoginScreen.tsx),
// so the two products look like one family. The words are IIP's own and say only what the platform does.

function environmentLabel() {
  if (runtimeProfile.toLowerCase() === "managedapplicationreviewer") return "Reviewer environment";
  return `${runtimeProfile} environment`;
}

const BODY_COPY =
  "IIP prepares claims, documents and customer conversations with the evidence attached, and leaves every consequential decision to an accountable person.";

const EYE_BROW = "Prepared by the platform. Decided by people.";

const VALUE_PROPS = [
  { title: "Work With Evidence", body: "Every answer shows its source." },
  { title: "Keep People In Charge", body: "Humans approve what matters." },
  { title: "Stay Auditable", body: "Every action is recorded." },
] as const;

function ValuePropsFooter({ className = "" }: { className?: string }) {
  return (
    <div className={`login-value-row grid w-full max-w-lg grid-cols-3 gap-0 ${className}`}>
      {VALUE_PROPS.map((item, index) => (
        <div key={item.title} className={`px-2 text-center ${index > 0 ? "border-l border-slate-200" : ""}`}>
          <p className="text-[10px] font-bold uppercase tracking-[0.06em] text-[#0B1B3A] sm:text-[11px]">{item.title}</p>
          <p className="mt-1 text-[10px] leading-snug text-slate-500 sm:text-[11px]">{item.body}</p>
        </div>
      ))}
    </div>
  );
}

export default function Login() {
  const navigate = useNavigate();
  const [signingIn, setSigningIn] = useState<SignInMethod | null>(null);
  const [finishing, setFinishing] = useState(canUseEntraBrowserAuth);
  const [error, setError] = useState<string | null>(null);
  const [capabilities, setCapabilities] = useState<AuthCapabilities | null>(null);

  useEffect(() => {
    void loadCapabilities().then(setCapabilities);
  }, []);

  // Microsoft has just returned the person to this page with their sign-in waiting to be finished: finish it and go in.
  useEffect(() => {
    if (!canUseEntraBrowserAuth) return;
    let cancelled = false;
    void finishReturningSignIn().then((outcome) => {
      if (cancelled) return;
      if (outcome.signedIn) {
        window.location.replace("/app");
        return;
      }
      if (outcome.error) setError(outcome.error);
      setFinishing(false);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  // Arriving from the sign-up page carries the product the person chose.
  const signingUp = typeof window !== "undefined" ? new URLSearchParams(window.location.search).get("signup") : null;
  const signUpProduct = signingUp === "bancassurance" ? "IIP for Bancassurance" : signingUp === "insurance" ? "IIP for Insurance" : null;

  async function signIn(method: SignInMethod) {
    setSigningIn(method);
    setError(null);
    try {
      if (canUseLocalTestDoubles) {
        navigate("/app");
        return;
      }
      await startSignIn(method);
    } catch (failure) {
      setError(describeSignInError(failure));
      setSigningIn(null);
    }
  }

  const passwordAvailable = capabilities?.passwordSignIn === true;
  const microsoftAvailable = canUseEntraBrowserAuth || canUseLocalTestDoubles;
  const googleAvailable = canUseLocalTestDoubles ? false : canUseExternalIdSignIn;
  const busy = signingIn !== null || finishing;
  const year = new Date().getFullYear();

  return (
    <div className="iip-marketing min-h-dvh bg-white">
      <MarketingNav />
      <div className="login-page relative min-h-[calc(100dvh-4rem)]">
        <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden="true">
          <div className="login-mist" />
          <div className="login-glow login-glow-a" />
          <div className="login-glow login-glow-b" />
        </div>

        <div className="relative z-10 mx-auto flex min-h-[calc(100dvh-4rem)] w-full max-w-6xl flex-col px-5 py-8 sm:px-8 lg:px-12 lg:py-12">
          <header className="flex flex-col items-center text-center lg:hidden">
            <h1 className="login-headline max-w-md text-[clamp(1.65rem,6.5vw,2.15rem)] font-bold leading-[1.15] tracking-tight text-[#0B1B3A]">
              One workspace for <span className="login-accent">evidence-led insurance work.</span>
            </h1>
            <p className="mt-4 max-w-md text-[14px] leading-relaxed text-slate-500">{BODY_COPY}</p>
          </header>

          <div className="mt-8 flex flex-1 flex-col lg:mt-0 lg:grid lg:grid-cols-[minmax(0,1.05fr)_minmax(320px,400px)] lg:items-center lg:gap-12 xl:gap-16">
            <section className="hidden lg:flex lg:min-h-[min(70vh,640px)] lg:flex-col lg:justify-between lg:py-4">
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-slate-400">{EYE_BROW}</p>
                <h1 className="login-headline mt-5 max-w-xl text-[clamp(2.4rem,3.6vw,3.35rem)] font-bold leading-[1.08] tracking-tight text-[#0B1B3A]">
                  One workspace for <span className="login-accent">evidence-led insurance work.</span>
                </h1>
                <p className="mt-5 max-w-lg text-[15px] leading-relaxed text-slate-500">{BODY_COPY}</p>
              </div>
              <div className="mt-16 max-w-lg">
                <ValuePropsFooter />
              </div>
            </section>

            <section className="flex w-full flex-col items-center lg:items-end">
              <div className="w-full max-w-[400px]">
                <div className="login-signin-card w-full rounded-2xl border border-slate-200/80 bg-white px-6 py-7 shadow-[0_18px_50px_-20px_rgba(15,40,90,0.22)] sm:px-8 sm:py-8">
                  <div className="flex items-baseline justify-between gap-3">
                    <h2 className="text-[22px] font-bold tracking-tight text-[#0B1B3A]">{signUpProduct ? "Create your account" : "Sign in"}</h2>
                    <span className="text-[11px] text-slate-400">{environmentLabel()}</span>
                  </div>
                  {signUpProduct && (
                    <p className="mt-2 text-[13px] leading-relaxed text-slate-500">
                      You chose {signUpProduct}. Sign in with Microsoft, Google or an email code, and then name your organisation.
                    </p>
                  )}

                  <div className="mt-6">
                    {!microsoftAvailable && !passwordAvailable && (
                      <div className="mb-5 flex items-start gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-[12px] leading-relaxed text-slate-700">
                        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden="true" />
                        <span>Sign-in is not configured for this environment.</span>
                      </div>
                    )}

                    {error && (
                      <div role="alert" className="mb-5 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-[12px] leading-relaxed text-red-800">
                        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                        <span>{error}</span>
                      </div>
                    )}

                    <button
                      type="button"
                      disabled={busy || !microsoftAvailable}
                      onClick={() => void signIn("microsoft")}
                      className="login-ms-btn flex w-full items-center justify-center gap-3 rounded-xl bg-[#0B1B3A] px-5 py-3.5 text-[14px] font-semibold text-white transition hover:bg-[#132a52] disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      <MicrosoftIcon className="h-[18px] w-[18px] shrink-0" />
                      <span>
                        {signingIn === "microsoft" || finishing
                          ? "Redirecting to Microsoft..."
                          : canUseLocalTestDoubles
                            ? "Enter local workspace"
                            : "Continue with Microsoft"}
                      </span>
                    </button>

                    {!canUseLocalTestDoubles && (
                      <>
                        <p className="mt-2 text-center text-[11px] leading-relaxed text-slate-400">
                          Work or school account, checked by Microsoft Entra ID.
                        </p>
                        <button
                          type="button"
                          disabled={busy || !googleAvailable}
                          onClick={() => void signIn("google")}
                          className="mt-3 flex w-full items-center justify-center gap-3 rounded-xl border border-slate-200 bg-white px-5 py-3.5 text-[14px] font-semibold text-[#0B1B3A] transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                          <GoogleIcon className="h-[18px] w-[18px] shrink-0" />
                          <span>{signingIn === "google" ? "Redirecting to Google..." : "Continue with Google"}</span>
                        </button>

                        <div className="my-5 flex items-center gap-3">
                          <div className="h-px flex-1 bg-slate-200" />
                          <span className="text-[12px] text-slate-400">or</span>
                          <div className="h-px flex-1 bg-slate-200" />
                        </div>
                        <button
                          type="button"
                          disabled={busy || !googleAvailable}
                          onClick={() => void signIn("email")}
                          className="mx-auto block text-[14px] font-semibold text-[#2563EB] transition hover:text-[#1d4ed8] disabled:cursor-not-allowed disabled:opacity-50"
                        >
                          {signingIn === "email" ? "Redirecting..." : "Sign in with email"}
                        </button>
                        {!googleAvailable && (
                          <p className="mt-3 text-center text-[11px] leading-relaxed text-slate-400">
                            Google and email sign-in are not set up for this environment.
                          </p>
                        )}
                      </>
                    )}

                    {passwordAvailable && (
                      <div className="mt-6 border-t border-slate-200 pt-5">
                        <PasswordSignIn onSignedIn={() => navigate("/app")} allowSignUp={capabilities?.signUp === true} />
                      </div>
                    )}
                  </div>

                  <p className="mt-7 text-center text-[11px] leading-relaxed text-slate-400">
                    By signing in, you agree to our{" "}
                    <Link href="/terms" className="font-medium text-[#2563EB] hover:underline">
                      Terms and Conditions
                    </Link>{" "}
                    and{" "}
                    <Link href="/privacy" className="font-medium text-[#2563EB] hover:underline">
                      Privacy Policy
                    </Link>
                    .
                  </p>
                </div>

                <p className="mt-4 text-center text-[11px] text-slate-400">&copy; {year} Otic Group. All rights reserved.</p>
              </div>
            </section>
          </div>

          <footer className="mt-10 flex flex-col items-center lg:hidden">
            <ValuePropsFooter />
          </footer>
        </div>
      </div>
    </div>
  );
}
