"use client";

import { useProviderHealth } from "@/lib/lovable-parity/data/providerHealth";
import { isProviderHealthy } from "@/lib/providerHealth";
import { runtimeProfile } from "@/lib/config";
import { useRuntimeContext } from "@/lib/runtimeContext";

export default function StatusBar() {
  const { providers, loading, error, source } = useProviderHealth();
  const { runtimeContext, loading: runtimeLoading } = useRuntimeContext();
  const attentionCount = providers.filter((provider) => !isProviderHealthy(provider.status)).length;

  const providerLabel = loading
    ? "Checking provider status"
    : error
      ? "Provider status unavailable"
      : providers.length === 0
        ? "Providers not configured"
        : attentionCount === 0
          ? `${providers.length} providers configured`
          : `${attentionCount} providers need attention`;
  const sourceLabel = source === "synthetic" && !loading ? "Preview data. " : "";
  const providerTone = loading || attentionCount > 0 || error || providers.length === 0
    ? "bg-dls-warning-text"
    : "bg-dls-success-text";
  const roleLabel = runtimeContext?.roleLabel ?? (runtimeLoading ? "Resolving access" : "Access unavailable");

  return (
    <footer aria-label="System status" className="flex min-h-8 shrink-0 items-center gap-3 border-t border-dls-border-subtle bg-dls-surface px-3 py-1 text-[11px] text-dls-text-secondary sm:px-5">
      <span aria-live="polite" className="inline-flex min-w-0 items-center gap-1.5">
        <span aria-hidden="true" className={`h-1.5 w-1.5 shrink-0 rounded-full ${providerTone}`} />
        <span className="truncate">{sourceLabel}{providerLabel}</span>
      </span>
      <span className="hidden shrink-0 sm:inline">Environment: {runtimeProfile}</span>
      <span className="hidden shrink-0 sm:inline">Signed in as {roleLabel}</span>
      <span className="ml-auto shrink-0">AI prepares work. People decide claims.</span>
    </footer>
  );
}
