"use client";

import { useCallback, useEffect, useState } from "react";
import { ShieldAlert } from "lucide-react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { Button } from "@/components/ui/Button";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { useUI } from "@/context/UIStateContext";
import {
  approveAndSendRetentionReminder,
  draftRetentionReminder,
  getRetentionWatch,
  type RetentionReminder,
  type RetentionWatchItem,
  type RetentionWatchSummary,
} from "@/lib/retention";

const BUCKET_TONE: Record<number, PillTone> = { 14: "danger", 30: "warning", 60: "neutral" };

const REMINDER_STATUS_LABEL: Record<string, string> = {
  PendingApproval: "Pending approval",
  Approved: "Approved",
  Sent: "Sent",
  ConsentRequired: "Consent required",
  ConfigurationRequired: "Not configured",
  Failed: "Failed to send",
};

export default function Retention() {
  const { roleDef } = useUI();
  const role = roleDef.key;

  const [summary, setSummary] = useState<RetentionWatchSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reminders, setReminders] = useState<Record<string, RetentionReminder>>({});
  const [busyPolicyId, setBusyPolicyId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const result = await getRetentionWatch(role);
    setLoading(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setSummary(result.value);
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  async function onDraft(item: RetentionWatchItem) {
    setBusyPolicyId(item.policyId);
    setActionError(null);
    const result = await draftRetentionReminder(role, item.policyId);
    setBusyPolicyId(null);
    if (!result.ok) {
      setActionError(result.error);
      return;
    }
    setReminders((prev) => ({ ...prev, [item.policyId]: result.value }));
  }

  async function onApproveAndSend(item: RetentionWatchItem, reminderId: string) {
    setBusyPolicyId(item.policyId);
    setActionError(null);
    const result = await approveAndSendRetentionReminder(role, reminderId);
    setBusyPolicyId(null);
    if (!result.ok) {
      setActionError(result.error);
      return;
    }
    setReminders((prev) => ({ ...prev, [item.policyId]: result.value }));
  }

  function effectiveReminder(item: RetentionWatchItem): { status: string; reminderId: string } | null {
    const local = reminders[item.policyId];
    if (local) return { status: local.status, reminderId: local.reminderId };
    if (item.latestReminderStatus && item.latestReminderId) {
      return { status: item.latestReminderStatus, reminderId: item.latestReminderId };
    }
    return null;
  }

  return (
    <>
      <SectionHeader
        eyebrow="Retention Watch"
        title="Retention Watch"
        sub="Policies whose cover ends in the next 60 days, ranked by claim experience and time since last contact. Reminders are always drafted for approval, never auto sent."
      />

      {loading && <GlassCard><LoadingSkeleton rows={5} /></GlassCard>}
      {!loading && error && <GlassCard><ErrorStateSurface title="Retention watch could not be loaded" message={error} onRetry={load} /></GlassCard>}

      {!loading && !error && summary && summary.items.length === 0 && (
        <GlassCard>
          <EmptyStateSurface
            title="No policies are due for renewal soon"
            message="Policies whose cover ends within 60 days will appear here, ranked by attention needed."
          />
        </GlassCard>
      )}

      {!loading && !error && summary && summary.items.length > 0 && (
        <>
          <div className="mb-4 grid gap-3 sm:grid-cols-2">
            <GlassCard>
              <div className="text-[11px] font-bold uppercase tracking-[0.1em] text-dls-text-muted">Policies at risk</div>
              <div className="mt-1 text-[26px] font-extrabold text-dls-text-primary">{summary.policiesAtRisk}</div>
            </GlassCard>
            <GlassCard>
              <div className="text-[11px] font-bold uppercase tracking-[0.1em] text-dls-text-muted">Premium at risk</div>
              <div className="mt-1 text-[26px] font-extrabold text-dls-text-primary">
                {summary.premiumAtRiskCurrency} {summary.premiumAtRisk.toLocaleString()}
              </div>
            </GlassCard>
          </div>

          {actionError && (
            <div className="mb-3 flex items-center gap-2 rounded-dls-card border border-dls-danger-text/30 bg-dls-surface p-3 text-[12.5px] text-dls-danger-text">
              <ShieldAlert className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
              {actionError}
            </div>
          )}

          <Table label="Retention watch">
            <TableHead>
              <TableRow>
                <TableHeaderCell>Customer</TableHeaderCell>
                <TableHeaderCell>Policy</TableHeaderCell>
                <TableHeaderCell>Cover ends</TableHeaderCell>
                <TableHeaderCell numeric>Claims (12m)</TableHeaderCell>
                <TableHeaderCell>Last contact</TableHeaderCell>
                <TableHeaderCell>Reminder</TableHeaderCell>
                <TableHeaderCell>Action</TableHeaderCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {summary.items.map((item) => {
                const reminder = effectiveReminder(item);
                const status = reminder?.status ?? null;
                const busy = busyPolicyId === item.policyId;
                return (
                  <TableRow key={item.policyId}>
                    <TableCell>
                      <div className="font-semibold text-dls-text-primary">{item.customerName}</div>
                      <div className="text-[11.5px] text-dls-text-muted">{item.productLine}</div>
                    </TableCell>
                    <TableCell className="text-dls-text-secondary">{item.policyNumber}</TableCell>
                    <TableCell>
                      <StatusPill tone={BUCKET_TONE[item.expiryBucketDays] ?? "neutral"} label={`${item.daysToExpiry} days`} />
                    </TableCell>
                    <TableCell numeric>{item.claimCountLast12Months}</TableCell>
                    <TableCell className="text-dls-text-muted">
                      {item.daysSinceLastContact === null ? "No contact on record" : `${item.daysSinceLastContact} days ago`}
                    </TableCell>
                    <TableCell>
                      {status ? (
                        <span className="text-[12.5px] text-dls-text-secondary">{REMINDER_STATUS_LABEL[status] ?? status}</span>
                      ) : (
                        <span className="text-[12.5px] text-dls-text-muted">Not drafted</span>
                      )}
                    </TableCell>
                    <TableCell>
                      {!status && (
                        <Button variant="secondary" onClick={() => onDraft(item)} disabled={busy}>
                          {busy ? "Drafting…" : "Draft reminder"}
                        </Button>
                      )}
                      {status === "PendingApproval" && reminder && (
                        <Button variant="primary" onClick={() => onApproveAndSend(item, reminder.reminderId)} disabled={busy}>
                          {busy ? "Sending…" : "Approve & send"}
                        </Button>
                      )}
                      {status && status !== "PendingApproval" && (
                        <span className="text-[12.5px] text-dls-text-muted">{REMINDER_STATUS_LABEL[status] ?? status}</span>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </>
      )}
    </>
  );
}
