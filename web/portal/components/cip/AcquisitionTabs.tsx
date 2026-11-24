"use client";
// Ported from the CIP web app (AcquisitionView): the segmented tab bar and the green Chat pill.

import Link from "next/link";
import { MessageCircle } from "lucide-react";

export type AcquisitionTab<T extends string> = { id: T; label: string };

export default function AcquisitionTabs<T extends string>({
  tabs,
  active,
  onChange,
  chatHref,
}: {
  tabs: AcquisitionTab<T>[];
  active: T;
  onChange: (id: T) => void;
  chatHref: string;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <div
        className="inline-flex max-w-full rounded-lg border border-slate-200/90 bg-white/90 p-0.5 max-md:w-full max-md:overflow-x-auto md:flex-wrap"
        role="tablist"
        aria-label="Acquisition views"
      >
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={active === tab.id}
            ref={(node) => { if (node && active === tab.id) node.scrollIntoView?.({ inline: "nearest", block: "nearest" }); }}
            onClick={() => onChange(tab.id)}
            className={`shrink-0 whitespace-nowrap rounded-md px-3 py-1.5 max-md:min-h-10 text-[12px] font-medium transition-colors ${
              active === tab.id
                ? "bg-[#1769FF]/12 text-[#1769FF] shadow-[0_0_0_1px_rgba(23,105,255,0.35),0_0_12px_rgba(23,105,255,0.25)]"
                : "text-slate-500 hover:bg-slate-50 hover:text-slate-700"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <Link
        href={chatHref}
        className="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-[12px] font-semibold text-emerald-700 hover:bg-emerald-100"
      >
        <MessageCircle className="h-3.5 w-3.5" aria-hidden />
        Chat
      </Link>
    </div>
  );
}
