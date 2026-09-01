"use client";
import { AlertTriangle, ShieldCheck } from "lucide-react";
import { useEnvironment } from "@/lib/lovable-parity/data/environment";

/**
 * A slim, always-visible strip that tells the operator which review mode they are in.
 * Its whole job is to make synthetic demo data impossible to mistake for live customer
 * data — so it is loud (amber) whenever data is synthetic, silent in Production, and
 * fails closed to a cautious "unverified" state if it cannot confirm the environment.
 */
export default function EnvironmentBanner() {
  const { environment, error } = useEnvironment();

  // Production with live data: no banner — a calm, unbadged workspace is the signal.
  if (environment && environment.reviewMode === "Production" && !environment.syntheticData) {
    return null;
  }

  if (environment?.syntheticData) {
    return (
      <div
        role="status"
        className="flex items-center justify-center gap-2 px-4 py-1.5 text-[12px] font-medium bg-amber-100 text-amber-900 border-b border-amber-300"
      >
        <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
        <span className="uppercase tracking-[0.12em]">Synthetic demo data</span>
        <span className="text-amber-950 normal-case tracking-normal">
          {environment.name} — not production. {environment.dataClassification}
        </span>
      </div>
    );
  }

  if (environment && environment.reviewMode === "LocalDevelopment") {
    return (
      <div className="flex items-center justify-center gap-2 px-4 py-1.5 text-[12px] font-medium bg-slate-100 text-slate-700 border-b border-slate-300">
        <ShieldCheck className="h-3.5 w-3.5 shrink-0" />
        <span>Local development — {environment.dataClassification || "no synthetic data"}</span>
      </div>
    );
  }

  // environment === null: could not confirm the runtime. Warn rather than imply live.
  return (
    <div
      role="status"
      className="flex items-center justify-center gap-2 px-4 py-1.5 text-[12px] font-medium bg-amber-50 text-amber-800 border-b border-amber-200"
    >
      <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
      <span>
        Environment unverified — treat all data with caution until the runtime confirms its mode.
        {error ? ` ${error.message}` : ""}
        {error?.correlationId ? ` Reference ${error.correlationId}.` : ""}
      </span>
    </div>
  );
}
