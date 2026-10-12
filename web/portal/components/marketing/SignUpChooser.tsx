"use client";

// Sign-up starts here: the person chooses IIP for Insurance or IIP for Bancassurance. Both lead into the same
// platform for now; the choice is kept so the organisation can be created with it after sign-in.

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { ArrowRight, Building2, Check, ShieldCheck } from "lucide-react";
import MarketingNav from "./MarketingNav";
import { MarketingContainer } from "./motion";

export type SignUpProduct = "insurance" | "bancassurance";
export const SIGNUP_PRODUCT_KEY = "iip-signup-product";

const OPTIONS: { id: SignUpProduct; name: string; who: string; icon: typeof ShieldCheck; points: string[] }[] = [
  {
    id: "insurance",
    name: "IIP for Insurance",
    who: "Insurers, brokers and claims teams",
    icon: ShieldCheck,
    points: ["Claims on WhatsApp in local languages", "Document checks and service standards", "Fraud signals and a full audit trail"],
  },
  {
    id: "bancassurance",
    name: "IIP for Bancassurance",
    who: "Banks that sell insurance to their customers",
    icon: Building2,
    points: ["Prospects scored with recommended covers", "Approved personal outreach on WhatsApp", "Follow-ups and policies about to end"],
  },
];

function readChoice(value: string | null): SignUpProduct | null {
  return value === "insurance" || value === "bancassurance" ? value : null;
}

export default function SignUpChooser() {
  const router = useRouter();
  const params = useSearchParams();
  const [choice, setChoice] = useState<SignUpProduct | null>(readChoice(params.get("product")));

  const proceed = () => {
    if (!choice) return;
    try {
      window.localStorage.setItem(SIGNUP_PRODUCT_KEY, choice);
    } catch {
      // Storage can be blocked; the choice can be made again after sign-in.
    }
    router.push(`/login?signup=${choice}`);
  };

  return (
    <div className="iip-marketing min-h-dvh bg-[var(--mkt-muted-bg)]">
      <MarketingNav />
      <main className="py-14 lg:py-20">
        <MarketingContainer>
          <div className="mx-auto max-w-3xl text-center">
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.14em] text-[var(--mkt-brand)]">Create your account</p>
            <h1 className="m-0 mt-3 text-balance text-3xl font-bold tracking-tight text-[var(--mkt-ink)] sm:text-4xl">Which product is for you?</h1>
            <p className="m-0 mt-4 text-base leading-7 text-[var(--mkt-muted)]">You can invite your team once your organisation is set up.</p>
          </div>

          <div role="radiogroup" aria-label="Choose a product" className="mx-auto mt-10 grid max-w-4xl gap-4 md:grid-cols-2">
            {OPTIONS.map((option) => {
              const selected = choice === option.id;
              return (
                <button
                  key={option.id}
                  type="button"
                  role="radio"
                  aria-checked={selected}
                  onClick={() => setChoice(option.id)}
                  className={`flex h-full flex-col rounded-2xl border bg-white p-7 text-left transition-colors ${
                    selected ? "border-[var(--mkt-brand)] ring-2 ring-[var(--mkt-brand)]/20" : "border-[var(--mkt-border)] hover:border-[var(--mkt-brand)]/40"
                  }`}
                >
                  <span className="flex w-full items-start justify-between">
                    <span className="inline-flex h-11 w-11 items-center justify-center rounded-xl bg-[var(--mkt-brand-soft)] text-[var(--mkt-brand)]">
                      <option.icon className="h-5 w-5" aria-hidden />
                    </span>
                    <span
                      className={`inline-flex h-6 w-6 items-center justify-center rounded-full border ${selected ? "border-[var(--mkt-brand)] bg-[var(--mkt-brand)] text-white" : "border-[var(--mkt-border)]"}`}
                      aria-hidden
                    >
                      {selected ? <Check className="h-3.5 w-3.5" /> : null}
                    </span>
                  </span>
                  <span className="mt-5 text-xl font-bold tracking-tight text-[var(--mkt-fg)]">{option.name}</span>
                  <span className="mt-1 text-sm font-medium text-[var(--mkt-brand)]">{option.who}</span>
                  <span className="mt-4 space-y-2">
                    {option.points.map((point) => (
                      <span key={point} className="flex gap-2 text-sm text-[var(--mkt-muted)]">
                        <Check className="mt-0.5 h-4 w-4 shrink-0 text-[var(--mkt-brand)]" aria-hidden />
                        {point}
                      </span>
                    ))}
                  </span>
                </button>
              );
            })}
          </div>

          <div className="mx-auto mt-8 flex max-w-4xl flex-col items-center gap-3">
            <button
              type="button"
              disabled={!choice}
              onClick={proceed}
              className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-[6px] bg-[var(--mkt-brand)] px-8 text-sm font-semibold text-white transition-colors hover:bg-[#1258d6] disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto"
            >
              {choice ? `Continue with ${OPTIONS.find((option) => option.id === choice)!.name}` : "Choose a product to continue"}
              <ArrowRight className="h-4 w-4" aria-hidden />
            </button>
            <p className="m-0 text-sm text-[var(--mkt-muted)]">
              Already have an account?{" "}
              <Link href="/login" className="font-semibold text-[var(--mkt-brand)] hover:underline">Sign in</Link>
            </p>
            <p className="m-0 text-xs text-[var(--mkt-muted)]">
              By creating an account you agree to the{" "}
              <Link href="/terms" className="font-medium text-[var(--mkt-brand)] hover:underline">Terms and Conditions</Link>
              {" "}and the{" "}
              <Link href="/privacy" className="font-medium text-[var(--mkt-brand)] hover:underline">Privacy Policy</Link>.
            </p>
          </div>
        </MarketingContainer>
      </main>
    </div>
  );
}
