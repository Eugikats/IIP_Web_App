import { providerHealth } from "@/lib/lovable-parity/mock";
import { Badge } from "./Primitives";
import { Activity } from "lucide-react";

export default function ProviderHealthGrid() {
  return (
    <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-3">
      {providerHealth.map((p) => {
        const tone = p.status === "Operational" ? "success" : p.status === "Degraded" ? "warning" : "error";
        return (
          <div key={p.name} className="glass-subtle p-4 hover-lift">
            <div className="flex items-center justify-between mb-2">
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone">{p.category}</div>
              <Badge tone={tone as any}>{p.status}</Badge>
            </div>
            <div className="text-[13.5px] font-medium text-navy leading-snug">{p.name}</div>
            <div className="mt-2 flex items-center justify-between text-[11.5px] text-stone">
              <span className="inline-flex items-center gap-1"><Activity className="h-3 w-3" />{p.latency}</span>
              <span>Checked {p.lastCheck}</span>
            </div>
          </div>
        );
      })}
    </div>
  );
}
