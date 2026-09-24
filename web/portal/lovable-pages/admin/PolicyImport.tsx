"use client";

import { useId, useMemo, useState } from "react";
import { UploadCloud, ArrowLeft, CheckCircle2, AlertTriangle } from "lucide-react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Select, Field } from "@/components/ui/Input";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { useUI } from "@/context/UIStateContext";
import {
  parsePolicyImportFile,
  commitPolicyImport,
  type PolicyImportParseResult,
  type PolicyImportCommitResult,
} from "@/lib/policyImport";

type Step = "upload" | "mapping" | "result";

const NOT_MAPPED = "";

export default function PolicyImport() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const fileFieldId = useId();

  const [step, setStep] = useState<Step>("upload");
  const [file, setFile] = useState<File | null>(null);
  const [parsing, setParsing] = useState(false);
  const [parseError, setParseError] = useState<string | null>(null);
  const [parsed, setParsed] = useState<PolicyImportParseResult | null>(null);
  const [mapping, setMapping] = useState<Record<string, string>>({});
  const [committing, setCommitting] = useState(false);
  const [commitError, setCommitError] = useState<string | null>(null);
  const [result, setResult] = useState<PolicyImportCommitResult | null>(null);

  const requiredFields = useMemo(() => parsed?.availableFields.filter((f) => f.required) ?? [], [parsed]);
  const mappedFieldSet = useMemo(() => new Set(Object.values(mapping).filter(Boolean)), [mapping]);
  const missingRequired = requiredFields.filter((f) => !mappedFieldSet.has(f.field));

  async function onParse() {
    if (!file) {
      setParseError("Choose a .csv or .xlsx file first.");
      return;
    }
    setParsing(true);
    setParseError(null);
    const outcome = await parsePolicyImportFile(role, file);
    setParsing(false);
    if (!outcome.ok) {
      setParseError(outcome.error);
      return;
    }
    setParsed(outcome.value);
    setMapping(outcome.value.suggestedMapping);
    setStep("mapping");
  }

  async function onCommit() {
    if (!parsed) return;
    setCommitting(true);
    setCommitError(null);
    const outcome = await commitPolicyImport(role, { headers: parsed.headers, columnMapping: mapping, rows: parsed.rows });
    setCommitting(false);
    if (!outcome.ok) {
      setCommitError(outcome.error);
      return;
    }
    setResult(outcome.value);
    setStep("result");
  }

  function startOver() {
    setStep("upload");
    setFile(null);
    setParseError(null);
    setParsed(null);
    setMapping({});
    setCommitError(null);
    setResult(null);
  }

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Policy Import"
        title="Policy Import"
        sub="Load policies from a CSV or Excel export. Map columns, then import: every accepted row is marked where it came from and when it was last refreshed."
      />

      {step === "upload" && (
        <GlassCard>
          <div className="mb-3 text-[15px] font-bold text-dls-text-primary">Upload a file</div>
          <Field label="Policy export (CSV or Excel) *" htmlFor={fileFieldId}>
            <input
              id={fileFieldId}
              type="file"
              accept=".csv,.xlsx"
              onChange={(e) => {
                setFile(e.target.files?.[0] ?? null);
                setParseError(null);
              }}
              className="w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-[9px] text-sm text-dls-text-primary file:mr-3 file:rounded-dls-control file:border-0 file:bg-dls-bg-page file:px-3 file:py-1.5 file:text-[12.5px] file:font-semibold file:text-dls-text-secondary focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
            />
          </Field>
          {parseError && (
            <p className="mt-3 text-[12.5px] text-dls-danger-text" role="alert">
              {parseError}
            </p>
          )}
          <div className="mt-4 flex justify-end">
            <Button variant="primary" icon={<UploadCloud aria-hidden="true" />} onClick={onParse} disabled={parsing}>
              {parsing ? "Reading file…" : "Read file"}
            </Button>
          </div>
        </GlassCard>
      )}

      {step === "mapping" && parsed && (
        <GlassCard>
          <div className="flex items-start justify-between gap-3">
            <div>
              <div className="text-[15px] font-bold text-dls-text-primary">Map columns</div>
              <p className="mt-1 text-[12.5px] text-dls-text-secondary">
                {parsed.rows.length} row{parsed.rows.length === 1 ? "" : "s"} found
                {parsed.truncated ? ", truncated to the first 5,000" : ""}. Confirm what each column means before importing.
              </p>
            </div>
            <Button variant="ghost" icon={<ArrowLeft aria-hidden="true" />} onClick={startOver}>
              Start over
            </Button>
          </div>

          <div className="mt-4 overflow-x-auto">
            <table className="w-full min-w-[560px] text-left text-[12.5px]">
              <thead>
                <tr className="border-b border-dls-border-subtle text-dls-text-muted">
                  <th className="py-2 pr-3 font-bold">Column</th>
                  <th className="py-2 pr-3 font-bold">Sample value</th>
                  <th className="py-2 font-bold">Maps to</th>
                </tr>
              </thead>
              <tbody>
                {parsed.headers.map((header, index) => (
                  <tr key={header + index} className="border-b border-dls-border-subtle last:border-0">
                    <td className="py-2 pr-3 font-semibold text-dls-text-primary">{header}</td>
                    <td className="py-2 pr-3 text-dls-text-secondary">{parsed.rows[0]?.[index] || "—"}</td>
                    <td className="py-2">
                      <Select value={mapping[header] ?? NOT_MAPPED} onChange={(e) => setMapping((m) => ({ ...m, [header]: e.target.value }))}>
                        <option value={NOT_MAPPED}>Do not import</option>
                        {parsed.availableFields.map((f) => (
                          <option key={f.field} value={f.field}>
                            {f.label}
                            {f.required ? " *" : ""}
                          </option>
                        ))}
                      </Select>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {missingRequired.length > 0 && (
            <p className="mt-3 text-[12.5px] text-dls-danger-text" role="alert">
              Map {missingRequired.map((f) => f.label).join(", ")} to a column before importing.
            </p>
          )}
          {commitError && (
            <p className="mt-3 text-[12.5px] text-dls-danger-text" role="alert">
              {commitError}
            </p>
          )}

          <div className="mt-4 flex justify-end">
            <Button variant="primary" onClick={onCommit} disabled={committing || missingRequired.length > 0}>
              {committing ? "Importing…" : `Import ${parsed.rows.length} row${parsed.rows.length === 1 ? "" : "s"}`}
            </Button>
          </div>
        </GlassCard>
      )}

      {step === "result" && result && (
        <>
          <div className="mb-4 grid gap-3 sm:grid-cols-3">
            <SummaryStat label="Total rows" value={result.totalRows} />
            <SummaryStat label="Imported" value={result.importedCount} tone="success" />
            <SummaryStat label="Failed" value={result.failedCount} tone={result.failedCount > 0 ? "danger" : "neutral"} />
          </div>

          <GlassCard>
            <div className="flex items-center gap-2">
              {result.failedCount === 0 ? (
                <CheckCircle2 className="h-4 w-4 text-dls-success-text" aria-hidden="true" />
              ) : (
                <AlertTriangle className="h-4 w-4 text-dls-warning-text" aria-hidden="true" />
              )}
              <div className="text-[15px] font-bold text-dls-text-primary">Validation report</div>
            </div>

            {result.errors.length === 0 ? (
              <p className="mt-3 text-[12.5px] text-dls-text-secondary">Every row imported without errors.</p>
            ) : (
              <div className="mt-3">
                <Table label="Rows that could not be imported">
                  <TableHead>
                    <TableRow>
                      <TableHeaderCell numeric>Row</TableHeaderCell>
                      <TableHeaderCell>What went wrong</TableHeaderCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {result.errors.map((rowError) => (
                      <TableRow key={rowError.rowNumber}>
                        <TableCell numeric>{rowError.rowNumber}</TableCell>
                        <TableCell className="text-dls-text-secondary">{rowError.errors.join(" ")}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}

            <div className="mt-4 flex justify-end">
              <Button variant="secondary" onClick={startOver}>
                Import another file
              </Button>
            </div>
          </GlassCard>
        </>
      )}
    </>
  );
}

function SummaryStat({ label, value, tone = "neutral" }: { label: string; value: number; tone?: "neutral" | "success" | "danger" }) {
  const toneClass = tone === "success" ? "text-dls-success-text" : tone === "danger" ? "text-dls-danger-text" : "text-dls-text-primary";
  return (
    <GlassCard>
      <div className="text-[11px] font-bold uppercase tracking-[0.1em] text-dls-text-muted">{label}</div>
      <div className={`mt-1 text-[26px] font-extrabold ${toneClass}`}>{value}</div>
    </GlassCard>
  );
}
