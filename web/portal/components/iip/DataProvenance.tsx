import { cn } from "@/lib/utils";

type DataProvenanceProps = {
  source: "live" | "synthetic" | "historical";
  dataAsOf: string;
  snapshotId: string;
  limitations: string;
  className?: string;
};

function formatTimestamp(value: string) {
  const timestamp = Date.parse(value);
  return Number.isNaN(timestamp) ? value : new Date(timestamp).toLocaleString();
}

export default function DataProvenance({ source, dataAsOf, snapshotId, limitations, className }: DataProvenanceProps) {
  const sourceLabel = source === "synthetic" ? "Synthetic snapshot" : source === "historical" ? "Historical response" : "Current workspace snapshot";
  return (
    <div className={cn("rounded-dls-card border border-dls-border-subtle bg-dls-surface px-3 py-2 text-[11px] leading-4 text-dls-text-muted", className)} role="note" data-snapshot-id={snapshotId}>
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <span className="inline-flex items-center gap-1.5 font-bold text-dls-text-primary">
          {source === "synthetic" && <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-dls-warning-dot" aria-hidden="true" />}
          {sourceLabel}
        </span>
        <span>Data as of {formatTimestamp(dataAsOf)}</span>
      </div>
      <div className="mt-1">Limit: {limitations}</div>
    </div>
  );
}
