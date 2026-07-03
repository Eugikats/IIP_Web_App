export function CompletenessIndicator({ value }: { value: number }) {
  const width = `${Math.min(100, Math.max(0, value))}%`;
  return (
    <div>
      <div className="flex items-center justify-between gap-3 text-[10px] font-bold uppercase tracking-[0.2em] text-muted">
        <span>Completeness</span>
        <span className="font-mono text-ink">{value}%</span>
      </div>
      <div className="mt-3 h-2 rounded-full bg-mist overflow-hidden border border-line">
        <div className="h-full rounded-full bg-success transition-all duration-500 shadow-glow-success" style={{ width }} />
      </div>
    </div>
  );
}
