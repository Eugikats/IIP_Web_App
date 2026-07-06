import { cn } from "@/lib/utils";

const STAGES = [
  "FNOL captured",
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

export default function MotorLifecycle({ current = 6 }: { current?: number }) {
  return (
    <div className="glass-subtle p-4">
      <div className="flex items-center justify-between mb-3">
        <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Motor claim lifecycle</div>
        <div className="text-[11px] text-stone">Stage {current + 1} of {STAGES.length}</div>
      </div>
      <div className="flex items-center gap-1 overflow-x-auto pb-1">
        {STAGES.map((s, i) => (
          <div key={s} className="flex items-center gap-1 shrink-0">
            <div
              className={cn(
                "text-[10.5px] rounded-full px-2.5 py-1 whitespace-nowrap ring-1 ring-inset",
                i < current && "bg-[hsl(var(--success)/0.10)] text-[hsl(var(--success))] ring-[hsl(var(--success)/0.22)]",
                i === current && "bg-[hsl(var(--info)/0.10)] text-info ring-[hsl(var(--info)/0.22)] font-medium",
                i > current && "bg-white text-stone ring-cloud"
              )}
            >
              {i + 1}. {s}
            </div>
            {i < STAGES.length - 1 && <div className="w-3 h-px bg-cloud shrink-0" />}
          </div>
        ))}
      </div>
    </div>
  );
}
