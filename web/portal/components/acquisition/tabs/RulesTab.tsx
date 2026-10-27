"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Play, Plus } from "lucide-react";
import CipStatusPill from "@/components/cip/CipStatusPill";
import { Button } from "@/components/ui/Button";
import { Field, Input, Select } from "@/components/ui/Input";
import { useUI } from "@/context/UIStateContext";
import {
  approveRule,
  getDecisions,
  getOutcomes,
  getRules,
  markOutcomeDone,
  pauseRule,
  resumeRule,
  ruleSentence,
  runRules,
  saveApprovalSettings,
  saveRule,
  type OfferOutcomeView,
  type OutreachRule,
  type RuleDecision,
  type RuleForm,
  type RulesOverview,
} from "@/lib/rules";
import { KpiRow, ReportState, TableCard, shortWhen, th } from "./ReportParts";

const OUTCOME: Record<RuleDecision["outcome"], { label: string; tone: "success" | "info" | "neutral" | "warning" | "danger" }> = {
  Sent: { label: "Sent", tone: "success" },
  DemoSent: { label: "Shown as sent, not delivered", tone: "info" },
  Skipped: { label: "Not sent", tone: "neutral" },
  Scheduled: { label: "Waiting", tone: "warning" },
  Blocked: { label: "Could not send", tone: "danger" },
};

const OUTCOME_KIND: Record<OfferOutcomeView["kind"], string> = {
  Replied: "Replied",
  Interested: "Asked for details",
  NotNow: "Said not now",
  Declined: "Said no thanks",
  RenewalRequested: "Asked to renew",
  Stopped: "Said stop. No more messages will be sent",
  Applied: "Took the cover",
};

const EMPTY: RuleForm = {
  name: "",
  eventType: "loan_disbursed",
  minAmount: null,
  minTermMonths: null,
  waitDays: 2,
  allowedProductLines: [],
  templateName: "bank_cover_intro",
  language: "en",
  fromHour: 9,
  toHour: 18,
  onePerPersonDays: 30,
  sameCoverDays: 90,
  dailyLimit: 50,
};

const toForm = (rule: OutreachRule): RuleForm => ({
  name: rule.name,
  eventType: rule.eventType,
  minAmount: rule.minAmount,
  minTermMonths: rule.minTermMonths,
  waitDays: rule.waitDays,
  allowedProductLines: rule.allowedProductLines,
  templateName: rule.templateName,
  language: rule.language,
  fromHour: rule.fromHour,
  toHour: rule.toHour,
  onePerPersonDays: rule.onePerPersonDays,
  sameCoverDays: rule.sameCoverDays,
  dailyLimit: rule.dailyLimit,
  followUpAfterDays: rule.followUpAfterDays,
  followUpTemplate: rule.followUpTemplate,
});

const numberOrNull = (value: string) => (value.trim() === "" ? null : Number(value.replace(/,/g, "")));

/** The standing instructions: when this happens, offer this, in these words, within these limits. A person approves a rule once;
 *  the rule then runs by itself, and every decision it makes, to send or not, is written to the log below. */
export default function RulesTab() {
  const { roleDef } = useUI();
  const role = roleDef.key;

  const [overview, setOverview] = useState<RulesOverview | null>(null);
  const [decisions, setDecisions] = useState<RuleDecision[]>([]);
  const [outcomes, setOutcomes] = useState<OfferOutcomeView[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<{ id: string | null; form: RuleForm } | null>(null);
  const [roles, setRoles] = useState<string | null>(null);
  const [outcomeFilter, setOutcomeFilter] = useState("");
  const [openDecision, setOpenDecision] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const [rules, log, replies] = await Promise.all([getRules(role), getDecisions(role, outcomeFilter || undefined), getOutcomes(role)]);
    setLoading(false);
    if (!rules.ok) {
      setForbidden(rules.forbidden);
      setError(rules.forbidden ? null : rules.error);
      return;
    }
    setOverview(rules.value);
    if (log.ok) setDecisions(log.value);
    if (replies.ok) setOutcomes(replies.value);
  }, [role, outcomeFilter]);

  useEffect(() => {
    void load();
  }, [load]);

  const coverNames = useMemo(() => Object.fromEntries((overview?.covers ?? []).map((cover) => [cover.line, cover.name])), [overview]);
  const live = (overview?.rules ?? []).filter((rule) => rule.isLive).length;
  const waiting = (overview?.rules ?? []).filter((rule) => rule.status === "Draft").length;

  async function act(work: () => Promise<{ ok: true } | { ok: false; error: string }>, done: string) {
    setBusy(true);
    setActionError(null);
    setNotice(null);
    const result = await work();
    setBusy(false);
    if (!result.ok) {
      setActionError(result.error);
      return false;
    }
    if (done) setNotice(done);
    await load();
    return true;
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!editing) return;
    if (await act(() => saveRule(role, editing.id, editing.form), "Saved as a draft. It does nothing until someone approves it.")) setEditing(null);
  }

  const form = editing?.form;
  const patch = (changes: Partial<RuleForm>) => editing && setEditing({ ...editing, form: { ...editing.form, ...changes } });
  const toggleCover = (line: string) =>
    patch({ allowedProductLines: form?.allowedProductLines.includes(line) ? form.allowedProductLines.filter((item) => item !== line) : [...(form?.allowedProductLines ?? []), line] });

  return (
    <div className="space-y-4">
      <KpiRow
        items={[
          { label: "Rules running", value: String(live) },
          { label: "Drafts waiting for approval", value: String(waiting) },
          { label: "Offers sent", value: String(decisions.filter((item) => item.outcome === "Sent" || item.outcome === "DemoSent").length) },
          { label: "Considered and not sent", value: String(decisions.filter((item) => item.outcome === "Skipped").length) },
          { label: "Replies waiting for a person", value: String(outcomes.filter((item) => item.needsPerson && !item.done).length) },
        ]}
      />

      {notice ? <p className="m-0 rounded-xl border border-slate-200 bg-white px-4 py-3 text-[13px] text-slate-600" role="status">{notice}</p> : null}
      {actionError ? <p className="m-0 rounded-xl border border-slate-200 bg-white px-4 py-3 text-[13px] text-dls-danger-text" role="alert">{actionError}</p> : null}

      <TableCard
        title="Rules"
        subtitle="Approve a rule once and it runs by itself within its limits. Change any part of it and it needs approving again."
        actions={
          <>
            <Button variant="secondary" disabled={busy} icon={<Play className="h-4 w-4" aria-hidden="true" />} onClick={() => void act(async () => {
              const result = await runRules(role);
              if (result.ok) setNotice(`Looked at ${result.value.considered}: ${result.value.sent + result.value.demoSent} sent, ${result.value.skipped} not sent, ${result.value.scheduled} waiting, ${result.value.blocked} could not send.`);
              return result.ok ? { ok: true as const } : result;
            }, "")}>
              Run the rules now
            </Button>
            <Button variant="primary" disabled={busy} icon={<Plus className="h-4 w-4" aria-hidden="true" />} onClick={() => setEditing({ id: null, form: { ...EMPTY } })}>
              New rule
            </Button>
          </>
        }
      >
        {overview ? (
          <div className="border-b border-slate-100 px-4 py-3 text-[13px] text-slate-600 sm:px-5">
            <p className="m-0">
              Who may approve a rule here: <strong className="text-[#0B1640]">{overview.approval.allowedRoles.join(", ")}</strong>
              {overview.approval.requireDifferentApprover ? ", and not the person who wrote it" : ""}.{" "}
              {overview.approval.youMayApprove ? "You may approve." : "You may not approve."}
            </p>
            {roles === null ? (
              <button type="button" className="mt-1 text-[12.5px] font-semibold text-[#1769FF] underline" onClick={() => setRoles(overview.approval.allowedRoles.join(", "))}>
                Change who may approve
              </button>
            ) : (
              <div className="mt-2 flex flex-wrap items-end gap-2">
                <Field label="Roles, separated by commas" htmlFor="approver-roles">
                  <Input id="approver-roles" value={roles} onChange={(event) => setRoles(event.target.value)} />
                </Field>
                <Button variant="secondary" disabled={busy} onClick={() => void act(async () => {
                  const result = await saveApprovalSettings(role, roles.split(",").map((part) => part.trim()).filter(Boolean), overview.approval.requireDifferentApprover);
                  if (result.ok) setRoles(null);
                  return result.ok ? { ok: true as const } : result;
                }, "Who may approve was changed.")}>
                  Save
                </Button>
                <Button variant="ghost" onClick={() => setRoles(null)}>Cancel</Button>
              </div>
            )}
          </div>
        ) : null}

        {editing && form ? (
          <form onSubmit={submit} className="grid gap-3 border-b border-slate-100 px-4 py-4 sm:grid-cols-2 sm:px-5" aria-label="Write a rule">
            <div className="sm:col-span-2">
              <Field label="Name" htmlFor="rule-name">
                <Input id="rule-name" required value={form.name} onChange={(event) => patch({ name: event.target.value })} />
              </Field>
            </div>
            <Field label="When this happens" htmlFor="rule-event">
              <Select id="rule-event" value={form.eventType} onChange={(event) => patch({ eventType: event.target.value })}>
                {(overview?.events ?? []).map((item) => <option key={item.code} value={item.code}>{item.label}</option>)}
              </Select>
            </Field>
            <Field label="Wait (days)" hint="0 sends as soon as the rules next run." htmlFor="rule-wait">
              <Input id="rule-wait" inputMode="numeric" value={form.waitDays} onChange={(event) => patch({ waitDays: Number(event.target.value) || 0 })} />
            </Field>
            <Field label="Only if the amount is at least (UGX)" htmlFor="rule-amount">
              <Input id="rule-amount" inputMode="numeric" value={form.minAmount ?? ""} onChange={(event) => patch({ minAmount: numberOrNull(event.target.value) })} />
            </Field>
            <Field label="Only if the term is at least (months)" htmlFor="rule-term">
              <Input id="rule-term" inputMode="numeric" value={form.minTermMonths ?? ""} onChange={(event) => patch({ minTermMonths: numberOrNull(event.target.value) })} />
            </Field>
            <fieldset className="sm:col-span-2">
              <legend className="mb-1.5 text-[13.5px] font-bold text-dls-text-primary">Covers it may offer (the best fit is chosen)</legend>
              <div className="flex flex-wrap gap-x-5 gap-y-2">
                {(overview?.covers ?? []).map((cover) => (
                  <label key={cover.line} className="flex items-center gap-2 text-[13px] text-slate-700">
                    <input type="checkbox" checked={form.allowedProductLines.includes(cover.line)} onChange={() => toggleCover(cover.line)} />
                    {cover.name}
                  </label>
                ))}
              </div>
            </fieldset>
            <Field label="Opening message (template)" hint="Only an approved template can be sent to someone who has not written in." htmlFor="rule-template">
              <Select id="rule-template" value={form.templateName} onChange={(event) => patch({ templateName: event.target.value })}>
                {(overview?.templates ?? []).map((template) => (
                  <option key={`${template.name}:${template.language}`} value={template.name}>{template.name} ({template.status === "Approved" ? "approved" : "not approved yet"})</option>
                ))}
              </Select>
            </Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="From hour" htmlFor="rule-from">
                <Input id="rule-from" inputMode="numeric" value={form.fromHour} onChange={(event) => patch({ fromHour: Number(event.target.value) || 0 })} />
              </Field>
              <Field label="Until hour" htmlFor="rule-to">
                <Input id="rule-to" inputMode="numeric" value={form.toHour} onChange={(event) => patch({ toHour: Number(event.target.value) || 0 })} />
              </Field>
            </div>
            <Field label="One offer per person every (days)" htmlFor="rule-one">
              <Input id="rule-one" inputMode="numeric" value={form.onePerPersonDays} onChange={(event) => patch({ onePerPersonDays: Number(event.target.value) || 0 })} />
            </Field>
            <Field label="The same cover only once every (days)" htmlFor="rule-same">
              <Input id="rule-same" inputMode="numeric" value={form.sameCoverDays} onChange={(event) => patch({ sameCoverDays: Number(event.target.value) || 0 })} />
            </Field>
            <Field label="People a day, at most" htmlFor="rule-daily">
              <Input id="rule-daily" inputMode="numeric" value={form.dailyLimit} onChange={(event) => patch({ dailyLimit: Number(event.target.value) || 0 })} />
            </Field>
            <div className="flex gap-2 sm:col-span-2">
              <Button type="submit" variant="primary" disabled={busy || form.name.trim().length === 0}>Save as a draft</Button>
              <Button variant="ghost" disabled={busy} onClick={() => setEditing(null)}>Cancel</Button>
            </div>
          </form>
        ) : null}

        <ReportState
          loading={loading}
          error={error}
          forbidden={forbidden}
          onRetry={() => void load()}
          permission="ManageSalesLeads"
          empty={overview && overview.rules.length === 0 ? { title: "No rules yet", message: "A rule says when to reach out and what to offer. Write one, approve it once, and it runs by itself within its limits." } : null}
        >
          <ul className="m-0 list-none divide-y divide-slate-100 p-0" aria-label="Rules">
            {(overview?.rules ?? []).map((rule) => (
              <li key={rule.ruleId} className="px-4 py-4 sm:px-5">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="m-0 text-[14px] font-semibold text-[#0B1640]">{rule.name}</h3>
                    <CipStatusPill tone={rule.isLive ? "success" : rule.status === "Paused" ? "warning" : "neutral"}>
                      {rule.isLive ? "Running" : rule.status === "Paused" ? "Paused" : "Draft, not approved"}
                    </CipStatusPill>
                    <span className="text-[12px] text-slate-400">version {rule.version}</span>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {rule.status === "Draft" && overview?.approval.youMayApprove ? (
                      <Button variant="primary" disabled={busy} onClick={() => void act(() => approveRule(role, rule.ruleId), `Approved. “${rule.name}” runs by itself from now on.`)}>Approve this rule</Button>
                    ) : null}
                    {rule.status === "Approved" ? <Button variant="secondary" disabled={busy} onClick={() => void act(() => pauseRule(role, rule.ruleId), `Paused “${rule.name}”.`)}>Pause</Button> : null}
                    {rule.status === "Paused" ? <Button variant="secondary" disabled={busy} onClick={() => void act(() => resumeRule(role, rule.ruleId), `Resumed “${rule.name}”.`)}>Resume</Button> : null}
                    <Button variant="ghost" disabled={busy} onClick={() => setEditing({ id: rule.ruleId, form: toForm(rule) })}>Edit</Button>
                  </div>
                </div>
                <p className="m-0 mt-1.5 text-[13px] leading-relaxed text-slate-600">{ruleSentence(rule, coverNames)}</p>
                <p className="m-0 mt-1 text-[12px] text-slate-400">
                  {rule.approvedBy ? `Approved by ${rule.approvedBy} (${rule.approvedByRole}) on ${shortWhen(rule.approvedAt)}.` : "Not approved. Changing a rule takes its approval away."}
                </p>
              </li>
            ))}
          </ul>
        </ReportState>
      </TableCard>

      <TableCard title="Replies" subtitle="How people answered an offer. A person who asked for details or to renew is waiting for someone on the team.">
        {outcomes.length === 0 ? (
          <p className="m-0 px-5 py-8 text-center text-[13px] text-slate-500">No one has answered an offer yet.</p>
        ) : (
          <ul className="m-0 list-none divide-y divide-slate-100 p-0" aria-label="Replies">
            {outcomes.map((item) => (
              <li key={item.outcomeId} className="flex flex-wrap items-start justify-between gap-3 px-4 py-3 sm:px-5">
                <div className="min-w-0">
                  <p className="m-0 text-[13.5px] font-semibold text-[#0B1640]">
                    {item.customerName ?? "Unknown"} <span className="font-mono text-[11.5px] font-normal text-slate-400">{item.phone}</span>
                  </p>
                  <p className="m-0 mt-0.5 text-[13px] text-slate-600">
                    {OUTCOME_KIND[item.kind]}. Offer: {item.cover} ({item.insurer}), from the rule {item.ruleName}.
                  </p>
                  {item.reply ? <p className="m-0 mt-0.5 text-[12.5px] text-slate-500">They wrote: “{item.reply}” on {shortWhen(item.at)}.</p> : null}
                  <p className="m-0 mt-0.5 text-[12px] text-slate-400">Opened with: {item.openingText}</p>
                </div>
                {item.needsPerson ? (
                  item.done ? (
                    <CipStatusPill tone="success">Followed up</CipStatusPill>
                  ) : (
                    <Button variant="secondary" disabled={busy} onClick={() => void act(() => markOutcomeDone(role, item.outcomeId), "Marked as followed up.")}>Mark as followed up</Button>
                  )
                ) : (
                  <CipStatusPill tone="neutral">Nothing to do</CipStatusPill>
                )}
              </li>
            ))}
          </ul>
        )}
      </TableCard>

      <TableCard
        title="Decision log"
        subtitle="Every time a rule looked at someone, what it decided and why, whether it sent or not."
        actions={
          <Select aria-label="Filter the log by outcome" value={outcomeFilter} onChange={(event) => setOutcomeFilter(event.target.value)}>
            <option value="">Everything</option>
            <option value="Sent">Sent</option>
            <option value="DemoSent">Shown as sent</option>
            <option value="Skipped">Not sent</option>
            <option value="Scheduled">Waiting</option>
            <option value="Blocked">Could not send</option>
          </Select>
        }
      >
        {decisions.length === 0 ? (
          <p className="m-0 px-5 py-8 text-center text-[13px] text-slate-500">
            {outcomeFilter ? "Nothing in the log has this outcome." : "Nothing yet. When an event arrives and a rule looks at it, the decision appears here."}
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[40rem] text-left text-[13px]">
              <thead className="border-b border-slate-100 text-[11px] uppercase tracking-wide text-slate-500">
                <tr>
                  <th className={th}>When</th>
                  <th className={th}>Customer</th>
                  <th className={th}>Rule</th>
                  <th className={th}>Outcome</th>
                  <th className={th}>Why</th>
                </tr>
              </thead>
              <tbody>
                {decisions.map((decision) => (
                  <tr key={decision.decisionId} className="border-b border-slate-50 align-top last:border-0">
                    <td className="px-3 py-3 text-slate-600">{shortWhen(decision.decidedAt)}</td>
                    <td className="px-3 py-3 text-slate-700">{decision.customerName ?? "Unknown"}<span className="block font-mono text-[11.5px] text-slate-400">{decision.phone}</span></td>
                    <td className="px-3 py-3 text-slate-600">{decision.ruleName}</td>
                    <td className="px-3 py-3"><CipStatusPill tone={OUTCOME[decision.outcome].tone}>{OUTCOME[decision.outcome].label}</CipStatusPill></td>
                    <td className="px-3 py-3 text-slate-600">
                      {decision.reason}
                      <button type="button" className="ml-2 text-[12px] font-semibold text-[#1769FF] underline" aria-expanded={openDecision === decision.decisionId} onClick={() => setOpenDecision(openDecision === decision.decisionId ? null : decision.decisionId)}>
                        {openDecision === decision.decisionId ? "Hide the checks" : "Show the checks"}
                      </button>
                      {openDecision === decision.decisionId ? (
                        <ul className="m-0 mt-2 list-none space-y-1 p-0">
                          {decision.gates.map((gate) => (
                            <li key={gate.name} className="text-[12.5px]">
                              <span className="font-semibold text-[#0B1640]">{gate.name}: {gate.passed ? "passed" : "stopped here"}.</span> {gate.detail}
                            </li>
                          ))}
                        </ul>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </TableCard>
    </div>
  );
}
