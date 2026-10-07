"use client";
// Ported from the CIP web app (apps/web/components/dashboard/v2/acquisition) so IIP and CIP share one look.

import { useEffect, useRef, useState, type ReactNode } from "react";
import { Search, SlidersHorizontal, X } from "lucide-react";

export type AcquisitionFilterField = {
  key: string;
  label: string;
  kind: "select" | "text" | "boolean";
  value: string;
  options?: Array<{ label: string; value: string }>;
  placeholder?: string;
};

export type AcquisitionActiveChip = {
  key: string;
  label: string;
};

function FieldControl({
  field,
  onChange,
}: {
  field: AcquisitionFilterField;
  onChange: (key: string, value: string) => void;
}) {
  if (field.kind === "text") {
    return (
      <label className="block">
        <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-slate-400">
          {field.label}
        </span>
        <input
          type="text"
          value={field.value}
          placeholder={field.placeholder}
          onChange={(e) => onChange(field.key, e.target.value)}
          className="w-full rounded-lg border border-slate-200 bg-white px-2.5 py-2 text-[13px] text-[#0B1640] outline-none focus:border-[#1769FF]/40"
        />
      </label>
    );
  }

  if (field.kind === "boolean") {
    return (
      <label className="block">
        <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-slate-400">
          {field.label}
        </span>
        <select
          value={field.value}
          onChange={(e) => onChange(field.key, e.target.value)}
          className="w-full rounded-lg border border-slate-200 bg-white px-2.5 py-2 text-[13px] text-[#0B1640] outline-none focus:border-[#1769FF]/40"
        >
          <option value="all">All</option>
          <option value="true">Yes</option>
          <option value="false">No</option>
        </select>
      </label>
    );
  }

  return (
    <label className="block">
      <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-slate-400">
        {field.label}
      </span>
      <select
        value={field.value}
        onChange={(e) => onChange(field.key, e.target.value)}
        className="w-full rounded-lg border border-slate-200 bg-white px-2.5 py-2 text-[13px] text-[#0B1640] outline-none focus:border-[#1769FF]/40"
      >
        {(field.options ?? []).map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
    </label>
  );
}

export default function AcquisitionFilterToolbar({
  fields,
  activeChips,
  activeCount,
  search,
  onSearchChange,
  onFieldChange,
  onClearChip,
  onClearAll,
  searchPlaceholder = "Search prospect or masked contact...",
}: {
  fields: AcquisitionFilterField[];
  activeChips: AcquisitionActiveChip[];
  activeCount: number;
  search: string;
  onSearchChange: (value: string) => void;
  onFieldChange: (key: string, value: string) => void;
  onClearChip: (key: string) => void;
  onClearAll: () => void;
  searchPlaceholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [mobileSheet, setMobileSheet] = useState(false);
  const popRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDoc = (e: MouseEvent) => {
      if (!popRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [open]);

  useEffect(() => {
    if (!mobileSheet) return;
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = prev;
    };
  }, [mobileSheet]);

  const filterPanel: ReactNode = (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
      {fields.map((field) => (
        <FieldControl key={field.key} field={field} onChange={onFieldChange} />
      ))}
    </div>
  );

  return (
    <div className="space-y-2.5">
      <div className="flex items-center gap-2">
        <div className="relative min-w-0 flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="search"
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder={searchPlaceholder}
            className="h-10 w-full rounded-xl border border-slate-200 bg-white py-2 pl-9 pr-3 text-[13px] text-[#0B1640] outline-none placeholder:text-slate-400 focus:border-[#1769FF]/40"
            aria-label="Search prospects"
          />
        </div>

        {/* Desktop filters — same SlidersHorizontal control as WhatsApp inbox */}
        <div className="relative hidden shrink-0 lg:block" ref={popRef}>
          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            className={`inline-flex h-10 w-10 items-center justify-center rounded-xl border shadow-sm ${
              activeCount > 0
                ? "border-[#1769FF] bg-[#EEF4FF] text-[#1769FF]"
                : "border-slate-200 bg-white text-slate-500 hover:bg-slate-50"
            }`}
            aria-expanded={open}
            aria-label={activeCount > 0 ? `Filters (${activeCount} active)` : "Filters"}
          >
            <SlidersHorizontal className="h-3.5 w-3.5" strokeWidth={2} />
            {activeCount > 0 ? (
              <span className="absolute -right-1 -top-1 inline-flex h-4 min-w-[1rem] items-center justify-center rounded-full bg-[#1769FF] px-1 text-[9px] font-bold text-white">
                {activeCount}
              </span>
            ) : null}
          </button>
          {open ? (
            <div className="absolute right-0 z-30 mt-1.5 w-[min(36rem,calc(100vw-2rem))] rounded-xl border border-slate-200 bg-white p-4 shadow-[0_16px_40px_rgba(15,23,42,0.14)]">
              {filterPanel}
              <div className="mt-3 flex justify-end gap-2 border-t border-slate-100 pt-3">
                <button
                  type="button"
                  onClick={onClearAll}
                  className="rounded-lg px-3 py-1.5 text-[12px] font-medium text-slate-600 hover:bg-slate-50"
                >
                  Clear all
                </button>
                <button
                  type="button"
                  onClick={() => setOpen(false)}
                  className="rounded-lg bg-[#1769FF] px-3 py-1.5 text-[12px] font-semibold text-white hover:bg-[#1258d6]"
                >
                  Done
                </button>
              </div>
            </div>
          ) : null}
        </div>

        {/* Mobile filters */}
        <button
          type="button"
          onClick={() => setMobileSheet(true)}
          className={`relative inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border shadow-sm lg:hidden ${
            activeCount > 0
              ? "border-[#1769FF] bg-[#EEF4FF] text-[#1769FF]"
              : "border-slate-200 bg-white text-slate-500"
          }`}
          aria-label={activeCount > 0 ? `Filters (${activeCount} active)` : "Filters"}
        >
          <SlidersHorizontal className="h-3.5 w-3.5" strokeWidth={2} />
          {activeCount > 0 ? (
            <span className="absolute -right-1 -top-1 inline-flex h-4 min-w-[1rem] items-center justify-center rounded-full bg-[#1769FF] px-1 text-[9px] font-bold text-white">
              {activeCount}
            </span>
          ) : null}
        </button>
      </div>

      {activeChips.length > 0 ? (
        <div className="flex items-center gap-2 overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
          {activeChips.map((chip) => (
            <button
              key={chip.key}
              type="button"
              onClick={() => onClearChip(chip.key)}
              className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-slate-200 bg-white px-2.5 py-1 text-[12px] font-medium text-slate-700 hover:bg-slate-50"
            >
              {chip.label}
              <X className="h-3 w-3 text-slate-400" />
            </button>
          ))}
          <button
            type="button"
            onClick={onClearAll}
            className="shrink-0 px-1.5 text-[12px] font-medium text-slate-500 hover:text-slate-800"
          >
            Clear all
          </button>
        </div>
      ) : null}

      {mobileSheet ? (
        <div className="fixed inset-0 z-50 lg:hidden">
          <button
            type="button"
            className="absolute inset-0 bg-slate-900/30"
            aria-label="Close filters"
            onClick={() => setMobileSheet(false)}
          />
          <div className="absolute inset-x-0 bottom-0 max-h-[85dvh] overflow-y-auto rounded-t-2xl bg-white px-4 pb-[max(1rem,env(safe-area-inset-bottom))] pt-3 shadow-2xl">
            <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-slate-200" />
            <div className="mb-3 flex items-center justify-between">
              <h2 className="text-[15px] font-semibold text-[#0B1640]">Filters</h2>
              <button
                type="button"
                onClick={() => setMobileSheet(false)}
                className="rounded-lg p-2 text-slate-500 hover:bg-slate-50"
                aria-label="Close"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            {filterPanel}
            <div className="mt-4 flex gap-2">
              <button
                type="button"
                onClick={onClearAll}
                className="h-11 flex-1 rounded-xl border border-slate-200 text-[13px] font-medium text-slate-700"
              >
                Clear all
              </button>
              <button
                type="button"
                onClick={() => setMobileSheet(false)}
                className="h-11 flex-1 rounded-xl bg-[#1769FF] text-[13px] font-semibold text-white"
              >
                Apply filters
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
