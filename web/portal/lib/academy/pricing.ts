"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { COURSES } from "@/lib/academy/courses";

// What each IIP Academy course costs, set in Settings. Prices are shown to learners. Collecting payment
// is not connected, so nobody is charged: the pages say so.

export type PriceMap = Record<string, number>;

const SEED_PRICE = 500_000;
const SEED_IDS = COURSES.map((course) => course.id);

export function formatPrice(amount: number, currency = "UGX"): string {
  if (amount === 0) return "Free";
  // Shillings have no cents; other currencies show them only when the amount has some.
  const digits = currency === "UGX" || Number.isInteger(amount) ? 0 : 2;
  return new Intl.NumberFormat("en-US", { style: "currency", currency, currencyDisplay: "code", minimumFractionDigits: digits, maximumFractionDigits: digits }).format(amount).replace(/ /g, " ");
}

export function useAcademyPricing() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [prices, setPrices] = useState<PriceMap | null>(LOVABLE_VISUAL_PARITY_MODE ? Object.fromEntries(SEED_IDS.map((id) => [id, SEED_PRICE])) : null);
  const [currency, setCurrency] = useState("UGX");
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/academy/pricing`, { headers: scopedHeaders(role, ""), cache: "no-store" });
      if (!response.ok) throw new Error("Prices could not be loaded.");
      const body = (await response.json()) as { currency: string; prices: { courseId: string; amount: number }[] };
      setCurrency(body.currency);
      setPrices(Object.fromEntries(body.prices.map((price) => [price.courseId, price.amount])));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Prices could not be loaded.");
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  const save = useCallback(async (next: PriceMap): Promise<{ ok: boolean; message?: string }> => {
    if (LOVABLE_VISUAL_PARITY_MODE) return { ok: false, message: "Not available in the visual preview." };
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/academy/pricing`, {
        method: "PUT",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ prices: Object.entries(next).map(([courseId, amount]) => ({ courseId, amount })) }),
        cache: "no-store",
      });
      if (response.status === 403) return { ok: false, message: "You do not have permission to change prices." };
      if (!response.ok) {
        const body = (await response.json().catch(() => ({}))) as { errors?: Record<string, string[]> };
        return { ok: false, message: Object.values(body.errors ?? {})[0]?.[0] ?? "The prices could not be saved." };
      }
      await load();
      return { ok: true };
    } catch {
      return { ok: false, message: "The prices could not be saved." };
    }
  }, [role, load]);

  return { prices, currency, loading, error, reload: load, save };
}
