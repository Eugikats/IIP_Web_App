import { ReactNode } from "react";

const toneStyles = {
  neutral: "border-line bg-surface text-ink",
  warning: "border-warning-border bg-warning-soft text-warning-text",
  danger:  "border-danger-border bg-danger-soft text-danger-text",
  success: "border-success-border bg-success-soft text-success-text",
};

export function MetricCard({
  label,
  value,
  detail,
  tone = "neutral",
  eyebrow,
}: {
  label: string;
  value: string | number;
  detail?: string;
  tone?: "neutral" | "warning" | "danger" | "success";
  eyebrow?: ReactNode;
}) {
  const cardStyle = toneStyles[tone];
  const valueColor = tone === "neutral" ? "text-ink" : toneStyles[tone].split(" ").pop(); // Extract text color

  return (
    <div className={`flex min-h-[124px] min-w-0 flex-col rounded-xl border p-5 shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-card cursor-default ${cardStyle}`}>
      {eyebrow && (
        <p className="mb-1 truncate text-[11px] font-bold uppercase tracking-wider text-muted">
          {eyebrow}
        </p>
      )}

      <p className="text-sm font-semibold leading-snug text-muted">
        {label}
      </p>

      <p className={`mt-2 text-3xl font-bold leading-none tracking-tight font-mono tabular-nums break-words ${valueColor}`}>
        {value}
      </p>

      {detail && (
        <p className="mt-auto pt-1.5 text-xs leading-relaxed text-muted line-clamp-2">{detail}</p>
      )}
    </div>
  );
}
