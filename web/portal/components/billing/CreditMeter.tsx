"use client";

import { useCallback, useEffect, useState } from "react";
import { motion } from "framer-motion";
import { Bar, BarChart, ResponsiveContainer, Tooltip, XAxis } from "recharts";
import { RefreshCw } from "lucide-react";
import { StatusPill } from "@/components/ui/StatusPill";
import { LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { getCreditsSummary, type CreditsResult, type CreditsSummary } from "@/lib/billing/credits";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

const STATE_TEXT: Record<CreditsSummary["state"], { label: string; tone: "success" | "warning" | "danger" | "neutral" }> = {
  ok: { label: "Within your plan", tone: "success" },
  near_limit: { label: "Nearly used up", tone: "warning" },
  exhausted: { label: "Used up", tone: "danger" },
  not_metered: { label: "No plan limit", tone: "neutral" },
};

const BAR_COLOUR: Record<CreditsSummary["state"], string> = {
  ok: "#1769FF",
  near_limit: "#d97706",
  exhausted: "#dc2626",
  not_metered: "#94a3b8",
};

function daysLeftInMonth(): number {
  const now = new Date();
  const end = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1));
  return Math.max(0, Math.ceil((end.getTime() - now.getTime()) / 86_400_000));
}

const fmt = (value: number) => value.toLocaleString();

/** The AI credit meter: credits used this month against the plan, where they went, and what to do when they run out. */
export default function CreditMeter() {
  const { roleDef } = useUI();
  const [result, setResult] = useState<CreditsResult | null>(null);

  const load = useCallback(async () => {
    setResult(null);
    if (LOVABLE_VISUAL_PARITY_MODE) {
      setResult({ ok: false, kind: "unavailable", message: "AI credits are not shown in the visual preview." });
      return;
    }
    setResult(await getCreditsSummary(roleDef.key));
  }, [roleDef.key]);

  useEffect(() => { void load(); }, [load]);

  if (result === null) {
    return (
      <section aria-label="AI credits" className="mb-5 rounded-xl border border-gray-200 bg-white p-4">
        <LoadingSkeleton rows={3} />
      </section>
    );
  }

  if (!result.ok) {
    return (
      <section aria-label="AI credits" className="mb-5 rounded-xl border border-gray-200 bg-white p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="m-0 text-[14px] font-bold text-slate-900">AI credits</h3>
          <StatusPill tone={result.kind === "no_permission" ? "neutral" : "warning"} label={result.kind === "no_permission" ? "No permission" : "Could not check"} />
        </div>
        <p className="m-0 mt-1.5 text-[13px] leading-5 text-slate-500">{result.message}</p>
        {result.kind === "unavailable" ? (
          <button type="button" onClick={() => void load()} className="mt-3 inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-gray-200 bg-white px-3 text-[13px] font-semibold text-slate-700 transition-colors hover:bg-slate-50">
            <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" /> Try again
          </button>
        ) : null}
      </section>
    );
  }

  const s = result.summary;
  const pill = STATE_TEXT[s.state];
  const topFeature = s.byFeature[0]?.credits ?? 0;

  return (
    <section aria-label="AI credits" className="mb-5 rounded-xl border border-gray-200 bg-white p-4 sm:p-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="m-0 text-[14px] font-bold text-slate-900">AI credits</h3>
          <p className="m-0 mt-0.5 text-[12.5px] text-slate-500">{s.metered ? `${s.planName} plan, this month` : "This month"}</p>
        </div>
        <StatusPill tone={pill.tone} label={pill.label} />
      </div>

      <div className="mt-4 flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
        <span className="text-[26px] font-bold tabular-nums leading-none text-slate-900">{fmt(s.used)}</span>
        <span className="text-[13px] text-slate-500">{s.metered ? `of ${fmt(s.included)} credits used` : "credits used (no plan limit applies to this workspace)"}</span>
      </div>

      {s.metered ? (
        <>
          <div
            className="mt-3 h-2.5 w-full overflow-hidden rounded-full bg-slate-100"
            role="progressbar"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={s.percentUsed}
            aria-label={`${s.percentUsed} percent of this month's credits used`}
          >
            <motion.div
              className="h-full rounded-full"
              style={{ backgroundColor: BAR_COLOUR[s.state] }}
              initial={{ width: 0 }}
              animate={{ width: `${Math.max(2, s.percentUsed)}%` }}
              transition={{ duration: 0.6, ease: [0.22, 1, 0.36, 1] }}
            />
          </div>
          <p className="m-0 mt-2 text-[12.5px] text-slate-500">
            {fmt(s.remaining)} left, {daysLeftInMonth()} {daysLeftInMonth() === 1 ? "day" : "days"} until the month resets.
          </p>
        </>
      ) : null}

      {s.state === "exhausted" ? (
        <div role="alert" className="mt-4 rounded-lg border border-red-100 bg-red-50 px-3.5 py-3 text-[13px] leading-5 text-red-800">
          <p className="m-0 font-semibold">This month&apos;s AI credits are used up.</p>
          <p className="m-0 mt-0.5">
            Kato falls back to plain replies, and questions that need the AI cannot be answered. Counting questions in the Command Center still work.
            {s.canAddCredits ? " Add credits to turn the AI back on." : " Ask your administrator to add credits."}
          </p>
        </div>
      ) : null}

      {s.canAddCredits && (s.state === "exhausted" || s.state === "near_limit") ? (
        <div className="mt-3">
          <a
            href="mailto:info@oticgroup.net?subject=Add%20AI%20credits"
            className="inline-flex min-h-9 items-center rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white transition-colors hover:bg-[#1258d6]"
          >
            Request more credits
          </a>
          <p className="m-0 mt-1.5 text-[12px] text-slate-400">Online payment is not connected yet, so credits are added by Otic.</p>
        </div>
      ) : null}

      {s.byFeature.length === 0 ? (
        <p className="m-0 mt-4 border-t border-slate-100 pt-3 text-[13px] text-slate-500">No AI has been used yet this month.</p>
      ) : (
        <div className="mt-4 grid gap-5 border-t border-slate-100 pt-4 md:grid-cols-2">
          <div>
            <h4 className="m-0 text-[11px] font-semibold uppercase tracking-wide text-slate-400">Where they went</h4>
            <ul className="m-0 mt-2 list-none space-y-2.5 p-0">
              {s.byFeature.map((item) => (
                <li key={item.feature}>
                  <div className="flex items-baseline justify-between gap-3 text-[13px]">
                    <span className="min-w-0 truncate text-slate-700">{item.label}</span>
                    <span className="shrink-0 tabular-nums font-semibold text-slate-900">{fmt(item.credits)}</span>
                  </div>
                  <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-slate-100">
                    <div className="h-full rounded-full bg-[#1769FF]/70" style={{ width: `${topFeature > 0 ? Math.max(4, (item.credits / topFeature) * 100) : 0}%` }} />
                  </div>
                  <div className="mt-0.5 text-[11.5px] text-slate-400">{fmt(item.calls)} {item.calls === 1 ? "call" : "calls"}</div>
                </li>
              ))}
            </ul>
          </div>
          <div>
            <h4 className="m-0 text-[11px] font-semibold uppercase tracking-wide text-slate-400">Each day this month</h4>
            <div className="mt-2 h-32 w-full" role="img" aria-label={`Credits used each day this month. Total ${fmt(s.used)}.`}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={s.byDay} margin={{ top: 4, right: 0, bottom: 0, left: 0 }}>
                  <XAxis dataKey="day" tick={{ fontSize: 10, fill: "#94a3b8" }} axisLine={false} tickLine={false} interval="preserveStartEnd" />
                  <Tooltip cursor={{ fill: "#f1f5f9" }} formatter={(value) => [`${value} credits`, ""]} labelFormatter={(label) => String(label)} />
                  <Bar dataKey="credits" fill="#1769FF" radius={[3, 3, 0, 0]} animationDuration={600} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>
        </div>
      )}

      {s.costUsd !== null ? (
        <p className="m-0 mt-4 border-t border-slate-100 pt-3 text-[12.5px] text-slate-500">
          Cost to Otic this month for these {fmt(s.calls)} calls: <span className="font-semibold text-slate-700">US$ {s.costUsd.toFixed(2)}</span>. Shown to people who can see billing only.
        </p>
      ) : null}
    </section>
  );
}
