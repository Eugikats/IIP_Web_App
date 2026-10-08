"use client";

import { useEffect, useRef, useState } from "react";
import { motion } from "framer-motion";
import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChevronDown, Download, FileSpreadsheet, FileText, Image as ImageIcon, Loader2 } from "lucide-react";
import { exportPdf, exportPng, exportXlsx } from "@/lib/command-center/exportAnswer";

export type ChartBlockData = {
  title: string;
  kind: string;
  points: { label: string; value: number }[];
  unit: string;
  summary?: string | null;
};

const BLUE = "#1769FF";

/** A chart drawn from counted records, with an Export menu (picture, PDF, Excel) as in CIP. */
export default function ChartCard({ block }: { block: ChartBlockData }) {
  const cardRef = useRef<HTMLDivElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  useEffect(() => {
    if (!menuOpen) return;
    const close = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) setMenuOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [menuOpen]);

  const rows = block.points.map((point) => ({ label: point.label, value: point.value }));
  const empty = rows.length === 0 || rows.every((row) => row.value === 0);

  const run = async (kind: "png" | "pdf" | "xlsx") => {
    setMenuOpen(false);
    setProblem(null);
    setBusy(kind);
    try {
      if (kind === "xlsx") exportXlsx(block.title, rows, block.unit);
      else if (cardRef.current && kind === "png") await exportPng(cardRef.current, block.title);
      else if (cardRef.current) await exportPdf(cardRef.current, block.title, rows, block.unit);
    } catch (reason) {
      setProblem(reason instanceof Error ? reason.message : "The file could not be saved.");
    } finally {
      setBusy(null);
    }
  };

  const items = [
    { key: "png" as const, label: "Picture (PNG)", icon: ImageIcon },
    { key: "pdf" as const, label: "PDF", icon: FileText },
    { key: "xlsx" as const, label: "Excel", icon: FileSpreadsheet },
  ];

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35, ease: [0.22, 1, 0.36, 1] }}
      className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-[0_8px_24px_rgba(15,23,42,0.06)] transition-shadow hover:shadow-[0_10px_28px_rgba(15,23,42,0.1)]"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="m-0 text-[13px] font-semibold text-[#0B1640]">{block.title}</p>
          {block.summary ? <p className="m-0 mt-0.5 text-[12px] text-slate-500">{block.summary}</p> : null}
        </div>
        <div ref={menuRef} className="relative shrink-0">
          <button
            type="button"
            onClick={() => setMenuOpen((open) => !open)}
            aria-haspopup="menu"
            aria-expanded={menuOpen}
            disabled={empty || busy !== null}
            className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-[12px] font-semibold text-slate-700 transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <Download className="h-3.5 w-3.5" aria-hidden />}
            Export
            <ChevronDown className="h-3 w-3" aria-hidden />
          </button>
          {menuOpen ? (
            <div role="menu" className="absolute right-0 top-full z-40 mt-1 w-44 rounded-xl border border-slate-200 bg-white p-1 shadow-lg">
              {items.map(({ key, label, icon: Icon }) => (
                <button
                  key={key}
                  type="button"
                  role="menuitem"
                  onClick={() => void run(key)}
                  className="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-[12px] text-slate-700 transition-colors hover:bg-slate-50"
                >
                  <Icon className="h-3.5 w-3.5" aria-hidden />
                  {label}
                </button>
              ))}
            </div>
          ) : null}
        </div>
      </div>

      {/* The element exported as a picture or PDF: title, caption and the chart. */}
      <div ref={cardRef} data-export-title={block.title} data-export-caption={block.summary ?? ""} className="mt-3 bg-white">
        {empty ? (
          <p className="m-0 py-10 text-center text-[12.5px] text-slate-500">Nothing to chart: no records fall in this period.</p>
        ) : (
          <div className="h-56 w-full" role="img" aria-label={`${block.title}. ${rows.map((row) => `${row.label}: ${row.value}`).join(", ")}`}>
            <ResponsiveContainer width="100%" height="100%">
              {block.kind === "bar" ? (
                <BarChart data={rows} layout="vertical" margin={{ top: 4, right: 16, bottom: 4, left: 8 }}>
                  <CartesianGrid horizontal={false} stroke="#eef2f7" />
                  <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11, fill: "#64748b" }} axisLine={false} tickLine={false} />
                  <YAxis type="category" dataKey="label" width={110} tick={{ fontSize: 11, fill: "#475569" }} axisLine={false} tickLine={false} />
                  <Tooltip cursor={{ fill: "#f1f5f9" }} formatter={(value) => [String(value), block.unit]} />
                  <Bar dataKey="value" fill={BLUE} radius={[0, 6, 6, 0]} barSize={16} animationDuration={700} />
                </BarChart>
              ) : (
                <LineChart data={rows} margin={{ top: 8, right: 16, bottom: 4, left: -16 }}>
                  <CartesianGrid vertical={false} stroke="#eef2f7" />
                  <XAxis dataKey="label" tick={{ fontSize: 11, fill: "#64748b" }} axisLine={false} tickLine={false} interval="preserveStartEnd" />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "#64748b" }} axisLine={false} tickLine={false} />
                  <Tooltip formatter={(value) => [String(value), block.unit]} />
                  <Line type="monotone" dataKey="value" stroke={BLUE} strokeWidth={2.5} dot={{ r: 3, fill: BLUE }} activeDot={{ r: 5 }} animationDuration={800} />
                </LineChart>
              )}
            </ResponsiveContainer>
          </div>
        )}
      </div>
      {problem ? <p role="alert" className="m-0 mt-2 text-[12px] text-red-700">{problem}</p> : null}
    </motion.div>
  );
}
