import { CheckCircle2, Circle, AlertTriangle } from "lucide-react";
import { Badge } from "./Primitives";

interface Item { label: string; present: boolean; lowConfidence?: boolean }

export default function CompletenessCard({
  items,
  score,
}: {
  items: Item[];
  score: number;
}) {
  const pct = Math.round(score * 100);
  const tone = pct >= 85 ? "success" : pct >= 60 ? "warning" : "error";
  return (
    <div className="glass-subtle p-4">
      <div className="flex items-center justify-between mb-3">
        <div className="text-[11px] uppercase tracking-[0.14em] text-muted">Completeness</div>
        <Badge tone={tone as any}>{pct}% complete</Badge>
      </div>
      <ul className="space-y-1.5 text-[13px]">
        {items.map((it) => (
          <li key={it.label} className="flex items-center gap-2 text-ink-soft">
            {it.present ? (
              <CheckCircle2 className="h-3.5 w-3.5 text-success" />
            ) : it.lowConfidence ? (
              <AlertTriangle className="h-3.5 w-3.5 text-warning" />
            ) : (
              <Circle className="h-3.5 w-3.5 text-muted" />
            )}
            <span className={it.present ? "" : "text-ink"}>{it.label}</span>
            {it.lowConfidence && <span className="ml-auto text-[10.5px] text-warning">low confidence</span>}
            {!it.present && !it.lowConfidence && <span className="ml-auto text-[10.5px] text-muted">missing</span>}
          </li>
        ))}
      </ul>
      <div className="mt-3 text-[10.5px] leading-4 text-muted">Completeness is not a claim decision. Low-confidence fields count as incomplete until reviewed. Use the claim&apos;s governed document-follow-up action to prepare a customer request.</div>
    </div>
  );
}
