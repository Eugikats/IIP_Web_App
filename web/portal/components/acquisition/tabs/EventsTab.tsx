"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Plus, Upload } from "lucide-react";
import CipStatusPill from "@/components/cip/CipStatusPill";
import { Button } from "@/components/ui/Button";
import { Field, Input, Select } from "@/components/ui/Input";
import { useUI } from "@/context/UIStateContext";
import {
  importEventFile,
  listEvents,
  listEventTypes,
  recordEvent,
  type BankEvent,
  type BankEventType,
  type EventImportOutcome,
} from "@/lib/bankEvents";
import { KpiRow, ReportState, TableCard, shortWhen, th } from "./ReportParts";

const WHEN: { value: string; label: string; days: number }[] = [
  { value: "0", label: "Today", days: 0 },
  { value: "1", label: "Yesterday", days: 1 },
  { value: "2", label: "2 days ago", days: 2 },
  { value: "3", label: "3 days ago", days: 3 },
  { value: "7", label: "A week ago", days: 7 },
  { value: "14", label: "Two weeks ago", days: 14 },
];

const SOURCE: Record<BankEvent["source"], string> = { Recorded: "Entered by hand", FileImport: "From a file", Feed: "From the bank's system" };

function money(value: string | undefined): string | null {
  const number = Number(value);
  return value && Number.isFinite(number) ? `UGX ${number.toLocaleString("en-UG")}` : null;
}

function detailsOf(event: BankEvent): string {
  const parts = [money(event.details.amount), event.details.term_months ? `${event.details.term_months} months` : null, event.details.branch ?? null].filter(Boolean);
  return parts.length > 0 ? parts.join(" · ") : "None";
}

/** What the bank has told IIP about its customers. A rule reads these to decide whom to offer cover. Nothing here sends a
 *  message: an event is a fact, and what to do about it is decided by the rules a person has approved. */
export default function EventsTab() {
  const { roleDef } = useUI();
  const role = roleDef.key;

  const [events, setEvents] = useState<BankEvent[] | null>(null);
  const [types, setTypes] = useState<BankEventType[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [formOpen, setFormOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [imported, setImported] = useState<{ fileName: string; outcome: EventImportOutcome } | null>(null);
  const [form, setForm] = useState({ eventType: "loan_disbursed", phone: "", when: "2", amount: "", termMonths: "", branch: "" });
  const fileInput = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const [list, kinds] = await Promise.all([listEvents(role), listEventTypes(role)]);
    setLoading(false);
    if (!list.ok) {
      setForbidden(list.forbidden);
      setError(list.forbidden ? null : list.error);
      return;
    }
    setEvents(list.value);
    if (kinds.ok) setTypes(kinds.value);
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const chosenType = types.find((type) => type.code === form.eventType);
  const pending = (events ?? []).filter((event) => event.state === "Pending").length;
  const week = (events ?? []).filter((event) => Date.now() - new Date(event.occurredAt).getTime() <= 7 * 24 * 3600 * 1000).length;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFormError(null);
    setNotice(null);
    const days = WHEN.find((option) => option.value === form.when)?.days ?? 0;
    const occurredAt = new Date(Date.now() - days * 24 * 3600 * 1000 - 60_000).toISOString();
    const result = await recordEvent(role, {
      eventType: form.eventType,
      phone: form.phone,
      occurredAt,
      amount: form.amount ? Number(form.amount.replace(/,/g, "")) : undefined,
      termMonths: form.termMonths ? Number(form.termMonths) : undefined,
      branch: form.branch.trim() || undefined,
    });
    setBusy(false);
    if (!result.ok) {
      setFormError(result.error);
      return;
    }
    setNotice(result.value.created ? "Event recorded. It is waiting for the rules to look at it." : "That event was already recorded, so nothing changed.");
    setForm({ ...form, phone: "", amount: "", termMonths: "", branch: "" });
    setFormOpen(false);
    await load();
  }

  async function onFile(file: File | undefined) {
    if (!file) return;
    setNotice(null);
    setImported(null);
    setBusy(true);
    const result = await importEventFile(role, file.name, await file.text());
    setBusy(false);
    if (fileInput.current) fileInput.current.value = "";
    if (!result.ok) {
      setNotice(result.error);
      return;
    }
    setImported({ fileName: file.name, outcome: result.value });
    await load();
  }

  const rows = useMemo(() => events ?? [], [events]);

  return (
    <div className="space-y-4">
      <KpiRow
        items={[
          { label: "Events recorded", value: String(rows.length) },
          { label: "Waiting for the rules", value: String(pending) },
          { label: "Happened in the last 7 days", value: String(week) },
        ]}
      />

      {notice ? <p className="m-0 rounded-xl border border-slate-200 bg-white px-4 py-3 text-[13px] text-slate-600" role="status">{notice}</p> : null}

      {imported ? (
        <section className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-[13px] text-slate-600" aria-label="File result">
          <p className="m-0 font-semibold text-[#0B1640]">
            {imported.fileName}: {imported.outcome.rowsRead} rows read, {imported.outcome.created} new, {imported.outcome.duplicates} already known, {imported.outcome.rejected.length} refused.
          </p>
          {imported.outcome.ignoredColumns.length > 0 ? (
            <p className="m-0 mt-1.5">
              Left out on purpose: {imported.outcome.ignoredColumns.join(", ")}. IIP takes only the customer&apos;s phone, what happened, when, and a few safe details, never balances or account numbers.
            </p>
          ) : null}
          {imported.outcome.rejected.length > 0 ? (
            <ul className="m-0 mt-1.5 list-disc pl-5">
              {imported.outcome.rejected.slice(0, 8).map((problem) => (
                <li key={problem.row}>Row {problem.row}: {problem.reason}</li>
              ))}
              {imported.outcome.rejected.length > 8 ? <li>and {imported.outcome.rejected.length - 8} more.</li> : null}
            </ul>
          ) : null}
        </section>
      ) : null}

      <TableCard
        title="Bank events"
        subtitle="What has happened to a customer at the bank. The rules read these to decide whom to offer cover."
        actions={
          <>
            <input ref={fileInput} type="file" accept=".csv,text/csv" className="sr-only" aria-label="Load a file of events" onChange={(event) => void onFile(event.target.files?.[0])} />
            <Button variant="secondary" disabled={busy} icon={<Upload className="h-4 w-4" aria-hidden="true" />} onClick={() => fileInput.current?.click()}>
              Load a file
            </Button>
            <Button variant="primary" disabled={busy} icon={<Plus className="h-4 w-4" aria-hidden="true" />} onClick={() => setFormOpen((open) => !open)}>
              Record an event
            </Button>
          </>
        }
      >
        {formOpen ? (
          <form onSubmit={submit} className="grid gap-3 border-b border-slate-100 px-4 py-4 sm:grid-cols-2 sm:px-5" aria-label="Record a bank event">
            <Field label="What happened" hint={chosenType ? `${chosenType.reason} Suggests: ${chosenType.suggestedOffer}.` : undefined} htmlFor="ev-type">
              <Select id="ev-type" value={form.eventType} onChange={(event) => setForm({ ...form, eventType: event.target.value })}>
                {types.map((type) => (
                  <option key={type.code} value={type.code}>{type.label}</option>
                ))}
              </Select>
            </Field>
            <Field label="Customer's phone" hint="With the country code, for example +256 7XX XXX XXX." htmlFor="ev-phone">
              <Input id="ev-phone" inputMode="tel" required value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} />
            </Field>
            <Field label="When it happened" hint="Choose an earlier day to rehearse a message that is due a few days after." htmlFor="ev-when">
              <Select id="ev-when" value={form.when} onChange={(event) => setForm({ ...form, when: event.target.value })}>
                {WHEN.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </Select>
            </Field>
            <Field label="Amount (UGX)" hint="For a loan, how much was lent. Not a balance." htmlFor="ev-amount">
              <Input id="ev-amount" inputMode="numeric" value={form.amount} onChange={(event) => setForm({ ...form, amount: event.target.value })} />
            </Field>
            <Field label="Term in months" htmlFor="ev-term">
              <Input id="ev-term" inputMode="numeric" value={form.termMonths} onChange={(event) => setForm({ ...form, termMonths: event.target.value })} />
            </Field>
            <Field label="Branch" htmlFor="ev-branch">
              <Input id="ev-branch" value={form.branch} onChange={(event) => setForm({ ...form, branch: event.target.value })} />
            </Field>
            {formError ? <p className="m-0 text-[13px] text-dls-danger-text sm:col-span-2" role="alert">{formError}</p> : null}
            <div className="flex gap-2 sm:col-span-2">
              <Button type="submit" variant="primary" disabled={busy || form.phone.trim().length === 0}>Save the event</Button>
              <Button variant="ghost" disabled={busy} onClick={() => setFormOpen(false)}>Cancel</Button>
            </div>
          </form>
        ) : null}

        <ReportState
          loading={loading}
          error={error}
          forbidden={forbidden}
          onRetry={() => void load()}
          permission="ManageSalesLeads"
          empty={
            events && events.length === 0
              ? { title: "No events yet", message: "When the bank tells IIP what happened to a customer, it shows here. Record one by hand, or load a file the bank sends." }
              : null
          }
        >
          <div className="overflow-x-auto">
            <table className="w-full min-w-[40rem] text-left text-[13px]">
              <thead className="border-b border-slate-100 text-[11px] uppercase tracking-wide text-slate-500">
                <tr>
                  <th className={th}>Happened</th>
                  <th className={th}>Event</th>
                  <th className={th}>Customer</th>
                  <th className={th}>Details</th>
                  <th className={th}>Came from</th>
                  <th className={th}>Rules</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((event) => (
                  <tr key={event.eventId} className="border-b border-slate-50 last:border-0">
                    <td className="px-3 py-3 text-slate-600">{shortWhen(event.occurredAt)}</td>
                    <td className="px-3 py-3 font-medium text-[#0B1640]">{event.label}</td>
                    <td className="px-3 py-3 font-mono text-[12px] text-slate-600">{event.phone}</td>
                    <td className="px-3 py-3 text-slate-600">{detailsOf(event)}</td>
                    <td className="px-3 py-3 text-slate-600">{SOURCE[event.source]}</td>
                    <td className="px-3 py-3">
                      <CipStatusPill tone={event.state === "Processed" ? "success" : "neutral"}>{event.state === "Processed" ? "Looked at" : "Waiting"}</CipStatusPill>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ReportState>
      </TableCard>
    </div>
  );
}
