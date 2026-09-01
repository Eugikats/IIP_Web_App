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

export default function DataProvenance({ source, dataAsOf, snapshotId, limitations, className = "" }: DataProvenanceProps) {
  const sourceLabel = source === "synthetic" ? "Synthetic snapshot" : source === "historical" ? "Historical response" : "Current workspace snapshot";
  return (
    <div className={`rounded-xl border border-cloud bg-white/65 px-3 py-2 text-[11px] leading-4 text-stone ${className}`} role="note">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <span className={source === "synthetic" ? "font-semibold text-warning" : "font-medium text-navy"}>{sourceLabel}</span>
        <span>Data as of {formatTimestamp(dataAsOf)}</span>
        <span className="font-mono text-[10px]">Snapshot {snapshotId}</span>
      </div>
      <div className="mt-1">Limit: {limitations}</div>
    </div>
  );
}
