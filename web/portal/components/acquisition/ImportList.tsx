"use client";

import { useMemo, useRef, useState } from "react";
import Link from "next/link";
import { ArrowLeft, Cloud, FileSpreadsheet, Mail, Upload } from "lucide-react";
import CipStatusPill from "@/components/cip/CipStatusPill";

// Bringing a customer list into IIP. Choosing a file and mapping its columns works in the browser, so a
// person can see exactly what would be imported. Loading the list into the workspace, and reading a
// list from Drive or Outlook, are not connected yet, and the page says so instead of pretending.

const FIELDS = [
  { key: "name", label: "Full name", required: true, hints: ["name", "customer", "full"] },
  { key: "phone", label: "Phone number", required: true, hints: ["phone", "mobile", "msisdn", "contact", "tel"] },
  { key: "occupation", label: "Work", required: false, hints: ["occupation", "work", "job", "profession"] },
  { key: "area", label: "Area", required: false, hints: ["area", "town", "location", "district"] },
  { key: "branch", label: "Bank branch", required: false, hints: ["branch"] },
  { key: "bank_products", label: "Products held at the bank", required: false, hints: ["product", "account", "loan"] },
  { key: "consent", label: "Agreed to be contacted", required: false, hints: ["consent", "opt", "agree", "marketing"] },
] as const;

const SOURCES = [
  { id: "file", label: "Upload a file", note: "CSV from your computer", icon: Upload, available: true, reason: "" },
  { id: "excel", label: "Excel workbook", note: "Save as CSV first", icon: FileSpreadsheet, available: false, reason: "Excel files are not read yet. Save the sheet as CSV and upload that." },
  { id: "drive", label: "Google Drive", note: "Pick a sheet from Drive", icon: Cloud, available: false, reason: "Not connected. A Drive connection has not been set up for this workspace." },
  { id: "onedrive", label: "OneDrive", note: "Pick a workbook from OneDrive", icon: Cloud, available: false, reason: "Not connected. A OneDrive connection has not been set up for this workspace." },
  { id: "outlook", label: "Outlook", note: "Read a list sent by email", icon: Mail, available: false, reason: "Not connected. Email is not configured for this workspace." },
] as const;

function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = "";
  let quoted = false;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (quoted) {
      if (ch === '"' && text[i + 1] === '"') { cell += '"'; i++; }
      else if (ch === '"') quoted = false;
      else cell += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ",") { row.push(cell); cell = ""; }
    else if (ch === "\n" || ch === "\r") {
      if (ch === "\r" && text[i + 1] === "\n") i++;
      row.push(cell); cell = "";
      if (row.some((value) => value.trim() !== "")) rows.push(row);
      row = [];
    } else cell += ch;
  }
  row.push(cell);
  if (row.some((value) => value.trim() !== "")) rows.push(row);
  return rows;
}

export default function ImportList() {
  const input = useRef<HTMLInputElement>(null);
  const [fileName, setFileName] = useState<string | null>(null);
  const [rows, setRows] = useState<string[][]>([]);
  const [mapping, setMapping] = useState<Record<string, number>>({});
  const [problem, setProblem] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const headers = rows[0] ?? [];
  const body = useMemo(() => rows.slice(1), [rows]);

  const load = async (file: File) => {
    setProblem(null);
    setNotice(null);
    if (!/\.csv$/i.test(file.name)) {
      setProblem("Only CSV files can be read here. Save your sheet as CSV and try again.");
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setProblem("That file is larger than 5 MB. Split the list and upload it in parts.");
      return;
    }
    const parsed = parseCsv(await file.text());
    if (parsed.length < 2) {
      setProblem("The file has no rows under its headings.");
      return;
    }
    const guess: Record<string, number> = {};
    FIELDS.forEach((field) => {
      const at = parsed[0].findIndex((heading) => field.hints.some((hint) => heading.toLowerCase().includes(hint)));
      if (at >= 0) guess[field.key] = at;
    });
    setRows(parsed);
    setFileName(file.name);
    setMapping(guess);
  };

  const missingRequired = FIELDS.filter((field) => field.required && mapping[field.key] === undefined);
  const preview = useMemo(() => body.slice(0, 5), [body]);
  const mapped = FIELDS.filter((field) => mapping[field.key] !== undefined);

  return (
    <div className="cip-acq space-y-4">
      <div>
        <Link href="/app/acquisition" className="inline-flex items-center gap-1 text-[12px] font-semibold text-slate-500 hover:text-[#1769FF]">
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
          Acquisition dashboard
        </Link>
        <h1 className="m-0 mt-1 text-[20px] font-semibold tracking-tight text-[#0B1640]">Import a customer list</h1>
        <p className="m-0 mt-0.5 max-w-3xl text-[12.5px] text-slate-500">
          Bring in a list of the bank&apos;s customers. IIP scores each person, recommends two covers and drafts a message for review. Only people who agreed to be contacted can be messaged.
        </p>
      </div>

      <section className="rounded-xl border border-slate-200/90 bg-white p-4 sm:p-5">
        <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">Where is the list?</h2>
        <ul className="m-0 mt-3 grid list-none gap-2.5 p-0 sm:grid-cols-2 xl:grid-cols-5">
          {SOURCES.map((source) => (
            <li key={source.id}>
              <button
                type="button"
                onClick={() => (source.available ? input.current?.click() : setNotice(source.reason))}
                className={`flex h-full w-full flex-col items-start gap-1.5 rounded-xl border p-3.5 text-left ${source.available ? "border-[#1769FF]/40 bg-[#F5F8FF] hover:bg-[#EEF4FF]" : "border-slate-200 bg-white hover:bg-slate-50"}`}
              >
                <span className="inline-flex w-full items-center justify-between">
                  <source.icon className={`h-5 w-5 ${source.available ? "text-[#1769FF]" : "text-slate-400"}`} aria-hidden />
                  {source.available ? null : <CipStatusPill tone="neutral">Not connected</CipStatusPill>}
                </span>
                <span className="text-[13px] font-semibold text-[#0B1640]">{source.label}</span>
                <span className="text-[11.5px] text-slate-500">{source.note}</span>
              </button>
            </li>
          ))}
        </ul>
        <input
          ref={input}
          type="file"
          accept=".csv,text/csv"
          className="sr-only"
          aria-label="Choose a CSV file"
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) void load(file);
            event.target.value = "";
          }}
        />
        {notice ? <p role="status" className="m-0 mt-3 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-[12.5px] text-slate-600">{notice}</p> : null}
        {problem ? <p role="alert" className="m-0 mt-3 rounded-lg border border-red-100 bg-red-50 px-3 py-2 text-[12.5px] text-red-700">{problem}</p> : null}
      </section>

      {fileName ? (
        <section className="rounded-xl border border-slate-200/90 bg-white p-4 sm:p-5">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">Match the columns</h2>
            <p className="m-0 text-[12px] text-slate-500">{fileName}: {body.length.toLocaleString()} people, {headers.length} columns</p>
          </div>
          <div className="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {FIELDS.map((field) => (
              <label key={field.key} className="block">
                <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-slate-400">{field.label}{field.required ? " (required)" : ""}</span>
                <select
                  value={mapping[field.key] ?? ""}
                  onChange={(event) => setMapping((current) => {
                    const next = { ...current };
                    if (event.target.value === "") delete next[field.key];
                    else next[field.key] = Number(event.target.value);
                    return next;
                  })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-2.5 py-2 text-[13px] text-[#0B1640] outline-none focus:border-[#1769FF]/40"
                >
                  <option value="">Not in this file</option>
                  {headers.map((heading, index) => <option key={`${heading}-${index}`} value={index}>{heading || `Column ${index + 1}`}</option>)}
                </select>
              </label>
            ))}
          </div>

          <h3 className="m-0 mt-5 text-[12px] font-bold uppercase tracking-wide text-slate-400">First {preview.length} people, as they would be read</h3>
          <div className="mt-2 overflow-x-auto rounded-lg border border-slate-100">
            <table className="w-full min-w-[560px] text-left text-[12px]">
              <thead>
                <tr className="border-b border-slate-100 text-[10.5px] uppercase tracking-wide text-slate-400">
                  {mapped.map((field) => <th key={field.key} className="px-3 py-2 font-semibold">{field.label}</th>)}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-50">
                {preview.map((row, i) => (
                  <tr key={i}>
                    {mapped.map((field) => <td key={field.key} className="px-3 py-2 text-slate-700">{row[mapping[field.key]] ?? ""}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {missingRequired.length > 0 ? (
            <p className="m-0 mt-3 text-[12.5px] text-amber-700">Match {missingRequired.map((field) => field.label.toLowerCase()).join(" and ")} to continue.</p>
          ) : null}
          <div className="mt-4 flex flex-wrap items-center gap-3 border-t border-slate-100 pt-4">
            <button type="button" disabled className="inline-flex h-9 cursor-not-allowed items-center rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white opacity-50">
              Import {body.length.toLocaleString()} people
            </button>
            <p className="m-0 text-[12px] text-slate-500">Loading a list into the workspace is not connected yet. Nothing has been imported or sent.</p>
          </div>
        </section>
      ) : null}

      <section className="rounded-xl border border-slate-200/90 bg-white p-4 text-[12.5px] leading-relaxed text-slate-600 sm:p-5">
        <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">What happens to an imported list</h2>
        <ol className="m-0 mt-2 list-decimal space-y-1 pl-5">
          <li>Each person becomes a prospect, marked as coming from the list.</li>
          <li>Anyone without a recorded agreement to be contacted is kept, but cannot be messaged.</li>
          <li>The triage agent scores each person and recommends two covers, with reasons from what the list says.</li>
          <li>The outreach agent drafts a personal message. A person reads, changes and approves each one.</li>
        </ol>
      </section>
    </div>
  );
}
