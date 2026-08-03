"use client";
import { Badge } from "./Primitives";
import { Activity } from "lucide-react";
import { useProviderHealth } from "@/lib/lovable-parity/data/providerHealth";
import { providerStatusTone } from "@/lib/providerHealth";

export default function ProviderHealthGrid() {
  const { providers, loading, error } = useProviderHealth();

  if (loading) {
    return <div className="glass-subtle p-6 text-center text-[12.5px] text-stone">Loading provider health…</div>;
  }

  if (providers.length === 0) {
    return (
      <div className="glass-subtle p-6 text-center text-[12.5px] text-stone">
        {error ? error : "No provider health reported. Providers appear here once configured — empty data is not fabricated."}
      </div>
    );
  }

  return (
    <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-3">
      {providers.map((p) => {
        const tone = providerStatusTone(p.status);
        return (
          <div key={p.name} className="glass-subtle p-4 hover-lift">
            <div className="flex items-center justify-between mb-2">
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone">{p.category}</div>
              <Badge tone={tone}>{p.status}</Badge>
            </div>
            <div className="text-[13.5px] font-medium text-navy leading-snug">{p.name}</div>
            <div className="mt-2 flex items-center justify-between text-[11.5px] text-stone">
              <span className="inline-flex items-center gap-1"><Activity className="h-3 w-3" />{p.latency}</span>
              <span>{p.lastCheck}</span>
            </div>
          </div>
        );
      })}
    </div>
  );
}
