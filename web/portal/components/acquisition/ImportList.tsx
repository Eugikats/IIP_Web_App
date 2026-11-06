"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { Loader2 } from "lucide-react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import { useUI } from "@/context/UIStateContext";
import { readListFile } from "@/lib/acquisition/readSpreadsheet";
import { guessMapping, IMPORT_FIELDS, LAST_NAME_KEY, nameOf, type ColumnMapping } from "@/lib/acquisition/guessMapping";
import { downloadFile, FileConnectionError, getFileConnectionStatus, startFileConnection, type FileConnectionStatus, type FileProviderKey } from "@/lib/files/fileConnections";
import { OneDrivePicker, pickFromGoogleDrive } from "@/components/files/FilePicker";
import Link from "next/link";
import { ArrowLeft, Cloud, FileSpreadsheet, Mail, Upload } from "lucide-react";
import CipStatusPill from "@/components/cip/CipStatusPill";

// Bringing a customer list into IIP. A CSV or Excel file is read and its columns are matched in the browser,
// so a person sees exactly what would be imported before anything is sent. Pressing Import loads the people
// into the workspace as prospects. Reading a list from Drive or Outlook is not connected yet, and the page
// says so instead of pretending.

type ImportResult = {
  listName: string;
  created: number;
  withConsent: number;
  alreadyKnown: number;
  invalid: number;
  problems: { row: number; reason: string }[];
};

const FIELDS = IMPORT_FIELDS;

const SOURCES = [
  { id: "file", label: "Upload a CSV file", note: "From your computer", icon: Upload, available: true, reason: "" },
  { id: "excel", label: "Excel workbook", note: "An .xlsx file from your computer", icon: FileSpreadsheet, available: true, reason: "" },
  { id: "drive", label: "Google Drive", note: "Pick a sheet from your Drive", icon: Cloud, available: false, reason: "" },
  { id: "onedrive", label: "OneDrive", note: "Pick a workbook from your OneDrive", icon: Cloud, available: false, reason: "" },
  { id: "outlook", label: "Outlook", note: "Read a list sent by email", icon: Mail, available: false, reason: "Not connected. Email is not configured for this workspace." },
] as const;

export default function ImportList() {
  const input = useRef<HTMLInputElement>(null);
  const [fileName, setFileName] = useState<string | null>(null);
  const [rows, setRows] = useState<string[][]>([]);
  const [mapping, setMapping] = useState<ColumnMapping>({});
  const [problem, setProblem] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const { roleDef } = useUI();
  const [importing, setImporting] = useState(false);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [fileStates, setFileStates] = useState<Record<string, FileConnectionStatus> | null>(null);
  const [oneDriveOpen, setOneDriveOpen] = useState(false);
  const [busySource, setBusySource] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    void getFileConnectionStatus(roleDef.key)
      .then((list) => { if (!cancelled) setFileStates(Object.fromEntries(list.map((item) => [item.provider, item]))); })
      .catch(() => { if (!cancelled) setFileStates({}); });
    return () => { cancelled = true; };
  }, [roleDef.key]);

  const cloudSource = async (provider: FileProviderKey) => {
    const status = fileStates?.[provider];
    const name = provider === "google" ? "Google Drive" : "OneDrive";
    setNotice(null);
    if (!status) {
      setNotice(`${name} could not be checked just now. Upload the file from your computer instead.`);
      return;
    }
    if (!status.configured) {
      setNotice(`Not connected. ${status.reason ?? `${name} sign-in has not been set up for IIP yet.`} Upload a CSV or Excel file from your computer in the meantime.`);
      return;
    }
    if (!status.connected) {
      setBusySource(provider);
      try {
        await startFileConnection(provider, "/app/acquisition/import", roleDef.key);
      } catch (reason) {
        setProblem(reason instanceof FileConnectionError ? reason.message : "The connection could not be started.");
        setBusySource(null);
      }
      return;
    }
    if (provider === "microsoft") {
      setOneDriveOpen(true);
      return;
    }
    setBusySource(provider);
    try {
      const fileId = await pickFromGoogleDrive(roleDef.key);
      if (fileId) await load(await downloadFile("google", fileId, roleDef.key));
    } catch (reason) {
      setProblem(reason instanceof Error ? reason.message : "The file could not be read from Google Drive.");
    } finally {
      setBusySource(null);
    }
  };

  const headers = rows[0] ?? [];
  const body = useMemo(() => rows.slice(1), [rows]);

  const load = async (file: File) => {
    setProblem(null);
    setNotice(null);
    setResult(null);
    if (file.size > 5 * 1024 * 1024) {
      setProblem("That file is larger than 5 MB. Split the list and upload it in parts.");
      return;
    }
    let parsed: string[][];
    try {
      parsed = await readListFile(file);
    } catch (reason) {
      setProblem(reason instanceof Error ? reason.message : "That file could not be read.");
      return;
    }
    if (parsed.length > 5001) {
      setProblem("A list can hold up to 5,000 people at a time. Split it and upload it in parts.");
      return;
    }
    if (parsed.length < 2) {
      setProblem("The file has no rows under its headings.");
      return;
    }
    setRows(parsed);
    setFileName(file.name);
    setMapping(guessMapping(parsed[0]));
  };

  const missingRequired = FIELDS.filter((field) => field.required && mapping[field.key] === undefined);
  const preview = useMemo(() => body.slice(0, 5), [body]);
  const mapped = FIELDS.filter((field) => mapping[field.key] !== undefined);

  const runImport = async () => {
    setImporting(true);
    setProblem(null);
    try {
      const token = await getEntraApiAccessToken();
      const cell = (row: string[], key: string) => {
        if (key === "name") return nameOf(row, mapping) || undefined;
        const at = mapping[key as keyof ColumnMapping];
        return at === undefined ? undefined : row[at]?.trim() || undefined;
      };
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads/import`, {
        method: "POST",
        headers: { ...scopedHeaders(roleDef.key, ""), "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        body: JSON.stringify({
          listName: (fileName ?? "Uploaded list").replace(/\.[^.]+$/, ""),
          rows: body.map((row) => ({
            name: cell(row, "name"),
            phone: cell(row, "phone"),
            occupation: cell(row, "occupation"),
            area: cell(row, "area"),
            branch: cell(row, "branch"),
            bankProducts: cell(row, "bank_products"),
            consent: cell(row, "consent"),
          })),
        }),
      });
      if (response.status === 401) { setProblem("Your sign-in has expired. Sign in again to continue."); return; }
      if (response.status === 403) { setProblem("Your role cannot import customer lists."); return; }
      if (!response.ok) {
        const data = (await response.json().catch(() => ({}))) as { errors?: Record<string, string[]>; message?: string };
        setProblem(Object.values(data.errors ?? {})[0]?.[0] ?? data.message ?? "The list could not be imported. Nothing was changed.");
        return;
      }
      setResult((await response.json()) as ImportResult);
    } catch {
      setProblem("The list could not be imported. Check your connection and try again. Nothing was changed.");
    } finally {
      setImporting(false);
    }
  };

  const cloudConnected = (id: string) =>
    (id === "drive" && fileStates?.google?.connected === true) || (id === "onedrive" && fileStates?.microsoft?.connected === true);

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
                onClick={() => {
                  if (source.id === "drive") void cloudSource("google");
                  else if (source.id === "onedrive") void cloudSource("microsoft");
                  else if (source.available) input.current?.click();
                  else setNotice(source.reason);
                }}
                className={`flex h-full w-full flex-col items-start gap-1.5 rounded-xl border p-3.5 text-left transition-colors ${source.available || cloudConnected(source.id) ? "border-[#1769FF]/40 bg-[#F5F8FF] hover:bg-[#EEF4FF]" : "border-slate-200 bg-white hover:bg-slate-50"}`}
              >
                <span className="inline-flex w-full items-center justify-between">
                  <source.icon className={`h-5 w-5 ${source.available || cloudConnected(source.id) ? "text-[#1769FF]" : "text-slate-400"}`} aria-hidden />
                  {source.available || cloudConnected(source.id) ? null : <CipStatusPill tone="neutral">Not connected</CipStatusPill>}
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
          accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          className="sr-only"
          aria-label="Choose a CSV or Excel file"
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
                    // A person who picks the name column by hand has decided; stop joining a last-name column.
                    if (field.key === "name") delete next[LAST_NAME_KEY];
                    return next;
                  })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-2.5 py-2 text-[13px] text-[#0B1640] outline-none focus:border-[#1769FF]/40"
                >
                  <option value="">Not in this file</option>
                  {headers.map((heading, index) => <option key={`${heading}-${index}`} value={index}>{heading || `Column ${index + 1}`}</option>)}
                </select>
                {field.key === "name" && mapping[LAST_NAME_KEY] !== undefined ? (
                  <span className="mt-1 block text-[11px] text-slate-500">Joined with {headers[mapping[LAST_NAME_KEY]!] || `Column ${mapping[LAST_NAME_KEY]! + 1}`} as the last name.</span>
                ) : null}
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
                    {mapped.map((field) => (
                      <td key={field.key} className="px-3 py-2 text-slate-700">
                        {field.key === "name" ? nameOf(row, mapping) : row[mapping[field.key]!] ?? ""}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {missingRequired.length > 0 ? (
            <p className="m-0 mt-3 text-[12.5px] text-amber-700">Match {missingRequired.map((field) => field.label.toLowerCase()).join(" and ")} to continue.</p>
          ) : null}
          <div className="mt-4 flex flex-wrap items-center gap-3 border-t border-slate-100 pt-4">
            <button
              type="button"
              disabled={missingRequired.length > 0 || importing || result !== null}
              onClick={() => void runImport()}
              className="inline-flex h-9 items-center gap-2 rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white transition-colors hover:bg-[#1258d6] disabled:cursor-not-allowed disabled:opacity-50"
            >
              {importing ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
              Import {body.length.toLocaleString()} people
            </button>
            <p className="m-0 text-[12px] text-slate-500">
              {result ? "Done. Nothing has been sent to anyone." : "Nothing is sent to anyone. People are added as prospects."}
            </p>
          </div>
        </section>
      ) : null}

      {result ? (
        <section role="status" className="rounded-xl border border-emerald-100 bg-emerald-50/60 p-4 text-[13px] text-slate-700 sm:p-5">
          <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">{result.created.toLocaleString()} {result.created === 1 ? "person" : "people"} added from {result.listName}</h2>
          <ul className="m-0 mt-2 list-disc space-y-0.5 pl-5">
            <li>{result.withConsent.toLocaleString()} agreed to be contacted, so they can be messaged. Everyone else is kept but cannot be messaged.</li>
            {result.alreadyKnown > 0 ? <li>{result.alreadyKnown.toLocaleString()} {result.alreadyKnown === 1 ? "was" : "were"} already on record or listed twice, and {result.alreadyKnown === 1 ? "was" : "were"} left as {result.alreadyKnown === 1 ? "it is" : "they are"}.</li> : null}
            {result.invalid > 0 ? <li>{result.invalid.toLocaleString()} {result.invalid === 1 ? "row" : "rows"} could not be read (no name or no usable phone number).</li> : null}
          </ul>
          {result.problems.length > 0 ? (
            <details className="mt-3">
              <summary className="cursor-pointer text-[12.5px] font-semibold text-slate-600">See the rows that were skipped</summary>
              <ul className="m-0 mt-2 list-none space-y-0.5 p-0 text-[12.5px] text-slate-600">
                {result.problems.map((item) => <li key={`${item.row}-${item.reason}`}>Row {item.row}: {item.reason}</li>)}
              </ul>
            </details>
          ) : null}
          <div className="mt-4">
            <Link href="/app/acquisition" className="inline-flex h-9 items-center rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white transition-colors hover:bg-[#1258d6]">
              Open the acquisition dashboard
            </Link>
          </div>
        </section>
      ) : null}

      <section className="rounded-xl border border-slate-200/90 bg-white p-4 text-[12.5px] leading-relaxed text-slate-600 sm:p-5">
        <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">What happens to an imported list</h2>
        <ol className="m-0 mt-2 list-decimal space-y-1 pl-5">
          <li>Each person becomes a prospect, marked as coming from the list.</li>
          <li>Anyone without a recorded agreement to be contacted is kept, but cannot be messaged.</li>
          <li>Run the triage agent from the acquisition dashboard to score each person and recommend two covers, with reasons from what the list says.</li>
          <li>The outreach agent drafts a personal message. A person reads, changes and approves each one.</li>
        </ol>
      </section>
      {oneDriveOpen ? (
        <OneDrivePicker
          onClose={() => setOneDriveOpen(false)}
          onPicked={(file) => {
            setOneDriveOpen(false);
            void load(file);
          }}
        />
      ) : null}
    </div>
  );
}
