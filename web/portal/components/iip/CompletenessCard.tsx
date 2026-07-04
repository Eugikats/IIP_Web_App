import { CheckCircle2, Circle, AlertTriangle } from "lucide-react";
import { Badge } from "./Primitives";
import { toast } from "sonner";

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
        <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Completeness</div>
        <Badge tone={tone as any}>{pct}% complete</Badge>
      </div>
      <ul className="space-y-1.5 text-[13px]">
        {items.map((it) => (
          <li key={it.label} className="flex items-center gap-2 text-slate2">
            {it.present ? (
              <CheckCircle2 className="h-3.5 w-3.5 text-success" />
            ) : it.lowConfidence ? (
              <AlertTriangle className="h-3.5 w-3.5 text-warning" />
            ) : (
              <Circle className="h-3.5 w-3.5 text-stone" />
            )}
            <span className={it.present ? "" : "text-navy"}>{it.label}</span>
            {it.lowConfidence && <span className="ml-auto text-[10.5px] text-warning">low confidence</span>}
            {!it.present && !it.lowConfidence && <span className="ml-auto text-[10.5px] text-stone">missing</span>}
          </li>
        ))}
      </ul>
      <button
        onClick={() => toast.success("Customer-safe document request drafted", { description: "Draft added to Communications for officer approval." })}
        className="mt-3 w-full text-[12.5px] rounded-xl border border-cloud bg-white py-2 text-navy hover:bg-white/80"
      >
        Draft request for missing documents
      </button>
      <div className="mt-2 text-[10.5px] text-stone">Completeness is not a claim decision. Low-confidence fields count as incomplete until reviewed.</div>
    </div>
  );
}
