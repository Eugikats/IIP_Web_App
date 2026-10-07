"use client";
// Ported from the CIP web app (AcquisitionKpiStrip, sparkline variant): label, then the number with its
// trend line beside it. A trend is drawn only from a real series, otherwise the card says "No trend".

import type { ReactNode } from "react";
import CipMetricCard, { type TrendDirection } from "./CipMetricCard";

export type AcquisitionKpiItem = {
  label: string;
  value: ReactNode;
  sparklineData?: number[];
  trendDirection?: TrendDirection;
  helper?: string;
};

export default function AcquisitionKpiStrip({ items }: { items: AcquisitionKpiItem[] }) {
  return (
    <div className="-mx-1 flex gap-2.5 overflow-x-auto px-1 pb-1 [scrollbar-width:none] lg:mx-0 lg:grid lg:overflow-visible lg:px-0 lg:pb-0 lg:[grid-template-columns:repeat(auto-fit,minmax(11rem,1fr))] [&::-webkit-scrollbar]:hidden">
      {items.map((item) => (
        <div key={item.label} className="min-w-[11rem] flex-1 lg:min-w-0">
          <CipMetricCard
            compact
            label={item.label}
            value={item.value}
            sparklineData={item.sparklineData ?? []}
            trendDirection={item.trendDirection ?? "up"}
            helperText={item.helper}
            className="h-full border-[#1769FF]/15 shadow-[0_1px_2px_rgba(15,23,42,0.03)]"
          />
        </div>
      ))}
    </div>
  );
}
