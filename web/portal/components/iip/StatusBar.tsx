"use client";
import { Activity, CheckCircle2, AlertTriangle, ShieldCheck } from "lucide-react";
import { useProviderHealth } from "@/lib/lovable-parity/data/providerHealth";
import { isProviderHealthy } from "@/lib/providerHealth";

export default function StatusBar() {
  const { providers, loading, source } = useProviderHealth();
  const degraded = providers.filter((p) => !isProviderHealthy(p.status));
  const ok = providers.length > 0 && degraded.length === 0;

  const providerSummary = loading
    ? "Checking provider health…"
    : providers.length === 0
      ? "Provider health · Not configured"
      : degraded.length === 0
        ? `All ${providers.length} providers healthy`
        : `${degraded.length} of ${providers.length} providers need attention`;
  const summary = source === "synthetic" && !loading
    ? `Synthetic snapshot · ${providerSummary}`
    : providerSummary;

  return (
    <footer aria-label="System status" className="min-h-8 shrink-0 border-t border-cloud/70 bg-white/60 px-2 py-1 backdrop-blur-xl sm:px-4 flex items-center gap-2 sm:gap-5 text-[10px] sm:text-[11px] text-stone">
      <span role="status" className="inline-flex min-w-0 items-center gap-1.5">
        {loading ? <Activity className="h-3 w-3" /> : ok ? <CheckCircle2 className="h-3 w-3 text-success" /> : <AlertTriangle className="h-3 w-3 text-warning" />}
        <span className="truncate">{summary}</span>
      </span>
      <span className="ml-auto inline-flex shrink-0 items-center gap-1.5">
        <ShieldCheck className="h-3 w-3 text-violet2" /> <span className="sm:hidden">Human authority</span><span className="hidden sm:inline">AI prepares work · humans approve regulated decisions</span>
      </span>
    </footer>
  );
}
