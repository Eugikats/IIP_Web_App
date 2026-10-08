"use client";

// The first screen after someone signs in for the first time and belongs to no organisation yet: they name
// their organisation and confirm the product. The API makes them its administrator. Both products open the
// same workspace for now; the product is kept on the organisation.

import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import MarketingNav from "./MarketingNav";
import { MarketingContainer } from "./motion";
import { SIGNUP_PRODUCT_KEY, type SignUpProduct } from "./SignUpChooser";

const TYPES = [
  { id: "Insurer", label: "An insurance company" },
  { id: "Broker", label: "A broker or agency" },
] as const;

export default function CreateOrganisation() {
  const [name, setName] = useState("");
  const [product, setProduct] = useState<SignUpProduct>("insurance");
  const [type, setType] = useState<(typeof TYPES)[number]["id"]>("Insurer");
  const [saving, setSaving] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  useEffect(() => {
    try {
      const stored = window.localStorage.getItem(SIGNUP_PRODUCT_KEY);
      if (stored === "bancassurance" || stored === "insurance") setProduct(stored);
    } catch {
      // Storage can be blocked; the person chooses below.
    }
  }, []);

  const valid = name.trim().length >= 2 && name.trim().length <= 100;

  const create = async () => {
    setSaving(true);
    setProblem(null);
    try {
      const token = await getEntraApiAccessToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/organisations`, {
        method: "POST",
        headers: { ...scopedHeaders("Admin", ""), "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        body: JSON.stringify({
          name: name.trim(),
          // A bank selling insurance is set up as the insurer side of its own pipeline.
          type: product === "bancassurance" ? "Insurer" : type,
          product: product === "bancassurance" ? "Bancassurance" : "Insurance",
        }),
      });
      if (response.status === 401) {
        setProblem("Your sign-in has expired. Sign in again to continue.");
        return;
      }
      if (!response.ok) {
        const body = (await response.json().catch(() => ({}))) as { message?: string; errors?: Record<string, string[]> };
        setProblem(body.message ?? Object.values(body.errors ?? {})[0]?.[0] ?? "Your organisation could not be created. Try again.");
        return;
      }
      try {
        window.localStorage.removeItem(SIGNUP_PRODUCT_KEY);
      } catch {
        // Nothing to clean up.
      }
      window.location.assign("/app");
    } catch {
      setProblem("Your organisation could not be created. Check your connection and try again.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="iip-marketing min-h-dvh bg-[var(--mkt-muted-bg)]">
      <MarketingNav />
      <main className="py-14 lg:py-20">
        <MarketingContainer>
          <div className="mx-auto max-w-xl rounded-2xl border border-[var(--mkt-border)] bg-white p-7 sm:p-9">
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.14em] text-[var(--mkt-brand)]">Almost there</p>
            <h1 className="m-0 mt-3 text-2xl font-bold tracking-tight text-[var(--mkt-ink)] sm:text-3xl">Set up your organisation</h1>
            <p className="m-0 mt-3 text-sm leading-6 text-[var(--mkt-muted)]">You will be its administrator and can invite your team once it is set up.</p>

            <label className="mt-7 block">
              <span className="mb-1.5 block text-sm font-semibold text-[var(--mkt-fg)]">Organisation name</span>
              <input
                value={name}
                onChange={(event) => setName(event.target.value)}
                maxLength={100}
                autoFocus
                placeholder="For example, Kampala Assurance"
                className="w-full rounded-lg border border-[var(--mkt-border)] px-3.5 py-2.5 text-[15px] text-[var(--mkt-fg)] outline-none focus:border-[var(--mkt-brand)]"
              />
            </label>

            <fieldset className="mt-6 border-0 p-0">
              <legend className="mb-2 text-sm font-semibold text-[var(--mkt-fg)]">Product</legend>
              <div className="grid gap-2 sm:grid-cols-2">
                {(["insurance", "bancassurance"] as const).map((option) => (
                  <label key={option} className={`flex cursor-pointer items-center gap-2.5 rounded-lg border px-3.5 py-3 text-sm ${product === option ? "border-[var(--mkt-brand)] bg-[var(--mkt-brand-soft)]" : "border-[var(--mkt-border)]"}`}>
                    <input type="radio" name="product" checked={product === option} onChange={() => setProduct(option)} className="accent-[#1769ff]" />
                    <span className="font-medium text-[var(--mkt-fg)]">{option === "insurance" ? "IIP for Insurance" : "IIP for Bancassurance"}</span>
                  </label>
                ))}
              </div>
            </fieldset>

            {product === "insurance" ? (
              <fieldset className="mt-6 border-0 p-0">
                <legend className="mb-2 text-sm font-semibold text-[var(--mkt-fg)]">Your organisation is</legend>
                <div className="grid gap-2 sm:grid-cols-2">
                  {TYPES.map((option) => (
                    <label key={option.id} className={`flex cursor-pointer items-center gap-2.5 rounded-lg border px-3.5 py-3 text-sm ${type === option.id ? "border-[var(--mkt-brand)] bg-[var(--mkt-brand-soft)]" : "border-[var(--mkt-border)]"}`}>
                      <input type="radio" name="type" checked={type === option.id} onChange={() => setType(option.id)} className="accent-[#1769ff]" />
                      <span className="font-medium text-[var(--mkt-fg)]">{option.label}</span>
                    </label>
                  ))}
                </div>
              </fieldset>
            ) : null}

            {problem ? <p role="alert" className="m-0 mt-5 rounded-lg border border-red-100 bg-red-50 px-3.5 py-2.5 text-sm text-red-700">{problem}</p> : null}

            <button
              type="button"
              disabled={!valid || saving}
              onClick={() => void create()}
              className="mt-7 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-[6px] bg-[var(--mkt-brand)] px-6 text-sm font-semibold text-white transition-colors hover:bg-[#1258d6] disabled:cursor-not-allowed disabled:opacity-50"
            >
              {saving ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
              Create organisation and open IIP
            </button>
          </div>
        </MarketingContainer>
      </main>
    </div>
  );
}
