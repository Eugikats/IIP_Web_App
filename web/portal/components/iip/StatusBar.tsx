"use client";
import { Activity, CheckCircle2, AlertTriangle, ShieldCheck } from "lucide-react";
import { useProviderHealth } from "@/lib/lovable-parity/data/providerHealth";

const HEALTHY = /oper|health|config|ready|ok/i;

export default function StatusBar() {
  const { providers, loading } = useProviderHealth();
  const degraded = providers.filter((p) => !HEALTHY.test(p.status));
  const ok = providers.length > 0 && degraded.length === 0;

  const summary = loading
    ? "Checking provider health…"
    : providers.length === 0
      ? "Provider health · Not configured"
      : degraded.length === 0
        ? `All ${providers.length} providers healthy`
        : `${degraded.length} of ${providers.length} providers need attention`;

  return (
    <div className="h-8 shrink-0 border-t border-cloud/70 bg-white/60 backdrop-blur-xl px-4 flex items-center gap-5 text-[11px] text-stone">
      <span className="inline-flex items-center gap-1.5">
        {loading ? <Activity className="h-3 w-3" /> : ok ? <CheckCircle2 className="h-3 w-3 text-success" /> : <AlertTriangle className="h-3 w-3 text-warning" />}
        {summary}
      </span>
      <span className="ml-auto inline-flex items-center gap-1.5">
        <ShieldCheck className="h-3 w-3 text-violet2" /> AI prepares work · humans approve regulated decisions
      </span>
    </div>
  );
}
