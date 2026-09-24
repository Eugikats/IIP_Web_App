import { CheckCircle2, Circle, TriangleAlert } from "lucide-react";

interface Item { label: string; present: boolean; lowConfidence?: boolean }

export default function CompletenessCard({
  items,
  score,
}: {
  items: Item[];
  score: number;
}) {
  const pct = Math.round(score * 100);
  const receivedCount = items.filter((item) => item.present).length;
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
      <div className="mb-3 flex items-center justify-between">
        <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Completeness</div>
        {/* Workflow legibility contract §7: the percentage never turns red at a low number,
            urgency comes from SLA state, not completeness. Neutral text at any value. */}
        <span className="text-[13px] font-bold text-dls-text-primary">{pct}% · {receivedCount} of {items.length}</span>
      </div>
      <ul className="space-y-1.5 text-[13px]">
        {items.map((it) => (
          <li key={it.label} className="flex items-center gap-2 text-dls-text-secondary">
            {it.present ? (
              <CheckCircle2 className="h-3.5 w-3.5 text-dls-success-dot" aria-hidden="true" />
            ) : it.lowConfidence ? (
              <TriangleAlert className="h-3.5 w-3.5 text-dls-warning-dot" aria-hidden="true" />
            ) : (
              <Circle className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" />
            )}
            <span className={it.present ? undefined : "text-dls-text-primary"}>{it.label}</span>
            {it.lowConfidence && <span className="ml-auto text-[10.5px] text-dls-warning-text">low confidence</span>}
            {!it.present && !it.lowConfidence && <span className="ml-auto text-[10.5px] text-dls-text-muted">missing</span>}
          </li>
        ))}
      </ul>
      <div className="mt-3 text-[10.5px] leading-4 text-dls-text-muted">Completeness is not a claim decision. Low-confidence fields count as incomplete until reviewed. Use the claim&apos;s governed document-follow-up action to prepare a customer request.</div>
    </div>
  );
}
