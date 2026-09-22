"use client";

import { useClaimProcess } from "@/lib/lovable-parity/data/claimProcess";

/// The claim's progress (build prompt outcome H): the pinned product's own configured claim
/// process (outcome A), never a fabricated or claim-type-hardcoded stage list (design language
/// v2, workflow legibility contract §4: "every displayed state maps to a real backend enum
/// value"). This intentionally does not mark a "current" stage: nothing in the backend today
/// tracks which of a product's configured stages a claim is actually at, and guessing one from
/// CaseStatus would be exactly the kind of UI-only, unverified state that contract prohibits.
export default function ClaimProcessStages({ claimId, role }: { claimId: string; role: string }) {
  const { process, loading, error, retry } = useClaimProcess(claimId, role);

  if (loading) {
    return (
      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
        <div className="h-4 w-48 animate-pulse rounded-dls-tight bg-dls-surface-sunken" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-between gap-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">
        <span>{error}</span>
        <button type="button" onClick={() => void retry()} className="dls-focus-outline shrink-0 rounded-dls-tight text-[12px] font-semibold text-dls-primary hover:underline">
          Retry
        </button>
      </div>
    );
  }

  if (!process.configured || process.stages.length === 0) {
    return (
      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">
        This claim&apos;s product has no configured claim process. Not configured.
      </div>
    );
  }

  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
      <div className="mb-3 flex items-center justify-between">
        <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">
          {process.productName}&apos;s claim process
        </div>
        <div className="text-[11px] text-dls-text-muted">{process.stages.length} stages</div>
      </div>
      <div tabIndex={0} aria-label="Configured claim process stages" className="dls-focus-outline flex items-stretch gap-1 overflow-x-auto pb-1">
        {process.stages.map((stage, index) => (
          <div key={stage.stageKey} className="flex shrink-0 items-center gap-1">
            <div className="whitespace-nowrap rounded-dls-pill border border-dls-border-subtle bg-dls-bg-page px-2.5 py-1 text-[10.5px] font-semibold text-dls-text-secondary">
              {index + 1}. {stage.name}
              <span className="ml-1.5 font-normal text-dls-text-muted">
                · {stage.ownerRole}
                {stage.serviceStandardWorkingDays !== null ? ` · ${stage.serviceStandardWorkingDays}d` : ""}
              </span>
            </div>
            {index < process.stages.length - 1 && <div className="h-px w-3 shrink-0 bg-dls-border-subtle" />}
          </div>
        ))}
      </div>
    </div>
  );
}
