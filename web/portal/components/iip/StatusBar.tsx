"use client";
import { useEffect, useState } from "react";
import { Activity, CheckCircle2, AlertTriangle, ShieldCheck, X } from "lucide-react";
import { useProviderHealth } from "@/lib/lovable-parity/data/providerHealth";
import { isProviderHealthy } from "@/lib/providerHealth";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { presentationMode } from "@/lib/config";

const DISMISS_KEY = "iip.statusbar.providerWarningDismissed";

export default function StatusBar() {
  const { providers, loading, source } = useProviderHealth();
  // The rail's avatar button is icon-only, this is the only always-visible statement of the
  // real signed-in authority, independent of whatever role the preview switcher is showing.
  const { runtimeContext, loading: runtimeLoading } = useRuntimeContext();
  const roleLabel = runtimeContext?.roleLabel ?? (runtimeLoading ? "Authority resolving" : "Authority unavailable");
  const degraded = providers.filter((p) => !isProviderHealthy(p.status));
  const ok = providers.length > 0 && degraded.length === 0;

  // Per-tab, not permanent: a real provider gap is still true and still worth fixing, this only
  // lets a viewer stop being reminded of it in the current browser tab (e.g. mid-demo). A fresh
  // tab, or reloading tomorrow, shows the honest state again rather than hiding it for good.
  const [dismissed, setDismissed] = useState(false);
  useEffect(() => {
    try {
      setDismissed(window.sessionStorage.getItem(DISMISS_KEY) === "1");
    } catch {
      // Storage unavailable: default to showing the real status, the safer default.
    }
  }, []);
  function dismiss() {
    setDismissed(true);
    try {
      window.sessionStorage.setItem(DISMISS_KEY, "1");
    } catch {
      // Best effort; the dismissal just won't survive a reload in this tab.
    }
  }

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
  const showProviderStatus = !presentationMode && (!dismissed || ok || loading);

  return (
    <footer aria-label="System status" className="min-h-8 shrink-0 border-t border-line/70 bg-white/60 px-2 py-1 backdrop-blur-xl sm:px-4 flex items-center gap-2 sm:gap-5 text-[10px] sm:text-[11px] text-muted">
      {showProviderStatus ? (
        <span role="status" className="inline-flex min-w-0 items-center gap-1.5">
          {loading ? <Activity className="h-3 w-3" /> : ok ? <CheckCircle2 className="h-3 w-3 text-success" /> : <AlertTriangle className="h-3 w-3 text-warning" />}
          <span className="truncate">{summary}</span>
          {!ok && !loading ? (
            <button
              type="button"
              onClick={dismiss}
              aria-label="Hide this notice for now"
              title="Hide for now (still visible in Admin > Connections)"
              className="dls-focus-outline ml-0.5 shrink-0 rounded-dls-tight p-0.5 text-muted hover:text-dls-text-primary"
            >
              <X className="h-3 w-3" aria-hidden="true" />
            </button>
          ) : null}
        </span>
      ) : presentationMode ? null : (
        <span role="status" className="inline-flex min-w-0 items-center gap-1.5 text-muted">
          <ShieldCheck className="h-3 w-3" aria-hidden="true" />
          <span className="truncate">System status hidden for this session</span>
        </span>
      )}
      <span className="hidden shrink-0 truncate sm:inline">Signed in as {roleLabel}</span>
      <span className="ml-auto inline-flex shrink-0 items-center gap-1.5">
        <ShieldCheck className="h-3 w-3 text-violet" /> <span className="sm:hidden">Human authority</span><span className="hidden sm:inline">AI prepares work · humans approve regulated decisions</span>
      </span>
    </footer>
  );
}
