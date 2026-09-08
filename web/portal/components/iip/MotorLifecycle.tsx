import { cn } from "@/lib/utils";

const STAGES = [
  "FNOL captured",
  "Assigned",
  "Documents uploaded",
  "Classified & extracted",
  "Completeness checked",
  "Claim summary prepared",
  "Fraud signals reviewed (advisory)",
  "Officer review",
  "Supervisor approval",
  "Customer update drafted",
  "Human approval",
  "Audit stored",
  "Claim pack exported",
];

const STATUS_STAGE: Record<string, number> = {
  New: 0,
  Assigned: 1,
  MissingDocuments: 4,
  ReadyForReview: 7,
  InReview: 7,
  PendingApproval: 8,
  Escalated: 8,
  ReadyForExport: 12,
  Settled: 12,
  Closed: 12,
};

export default function MotorLifecycle({ current, status }: { current?: number; status?: string }) {
  const stage = current ?? STATUS_STAGE[status ?? ""] ?? 0;

  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
      <div className="mb-3 flex items-center justify-between">
        <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Motor claim lifecycle</div>
        <div className="text-[11px] text-dls-text-muted">Stage {stage + 1} of {STAGES.length}</div>
      </div>
      <div tabIndex={0} aria-label="Motor claim lifecycle stages" className="dls-focus-outline flex items-center gap-1 overflow-x-auto pb-1">
        {STAGES.map((s, i) => (
          <div key={s} className="flex shrink-0 items-center gap-1">
            <div
              className={cn(
                "whitespace-nowrap rounded-dls-pill px-2.5 py-1 text-[10.5px] font-semibold",
                i < stage && "bg-dls-success-tint text-dls-success-text",
                i === stage && "bg-dls-info-tint text-dls-info-text",
                i > stage && "bg-dls-surface text-dls-text-muted border border-dls-border-subtle",
              )}
            >
              {i + 1}. {s}
            </div>
            {i < STAGES.length - 1 && <div className="h-px w-3 shrink-0 bg-dls-border-subtle" />}
          </div>
        ))}
      </div>
    </div>
  );
}
