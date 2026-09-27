"use client";
import { claimReference, humanizeIdentifier, shortReference } from "@/lib/format";
import { Link } from "@/components/iip/NextRouterCompat";
import { useMemo, useState } from "react";
import { SectionHeader, GlassCard, Badge, StatusBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { FilterTab } from "@/components/ui/FilterTab";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface, EmptyFilteredStateSurface } from "@/components/ui/StateSurfaces";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { ExternalLink } from "lucide-react";
import { toast } from "sonner";
import { useCustomerUpdateQueue, type CustomerUpdateQueueItem } from "@/lib/lovable-parity/data/communications";

type PendingAction = { kind: "approve" | "reject" | "send"; item: CustomerUpdateQueueItem };

const TABS = ["Pending Approval", "Approved", "Sent", "Rejected", "All"] as const;

export default function Communications() {
  const { items, loading, error, parityMode, approve, reject, send } = useCustomerUpdateQueue();
  const [tab, setTab] = useState<(typeof TABS)[number]>("Pending Approval");
  const [pending, setPending] = useState<PendingAction | null>(null);
  const filtered = useMemo(
    () => (tab === "All" ? items : items.filter((item) => item.status === tab || (tab === "Sent" && item.status === "Send Failed"))),
    [items, tab],
  );
  const tabCounts = useMemo(() => {
    const counts = new Map<(typeof TABS)[number], number>();
    for (const t of TABS) {
      counts.set(t, t === "All" ? items.length : items.filter((item) => item.status === t || (t === "Sent" && item.status === "Send Failed")).length);
    }
    return counts;
  }, [items]);

  function allows(item: CustomerUpdateQueueItem, action: string) {
    return item.allowedActions.some((a) => a.action === action);
  }

  function blockedReason(item: CustomerUpdateQueueItem, action: string) {
    return item.blockedActions.find((a) => a.action === action)?.reason ?? null;
  }

  async function handleConfirm(auditReason: string) {
    if (!pending) return;
    const { kind, item } = pending;
    const result = kind === "approve" ? await approve(item.updateId, auditReason)
      : kind === "reject" ? await reject(item.updateId, auditReason)
      : await send(item, auditReason);
    if (result.ok) {
      toast.success(kind === "approve" ? "Update approved." : kind === "reject" ? "Update rejected." : "Update sent.");
    } else {
      toast.error(result.message ?? `${kind} failed.`);
    }
    setPending(null);
  }

  return (
    <>
      <SectionHeader
        eyebrow="Customer Communications"
        title="Customer update queue"
        sub="AI drafts customer-safe messages. Every send requires named human approval, no sensitive message is dispatched automatically."
      />
      <div className="mb-4"><GuardrailBanner text="Drafts must not promise settlement, coverage, or payment. Approved drafts are dispatched via the configured provider and logged in Audit." /></div>

      <div className="mb-4 flex flex-wrap gap-1.5">
        {TABS.map((t) => (
          <FilterTab key={t} active={tab === t} onClick={() => setTab(t)} count={tabCounts.get(t)}>
            {t}
          </FilterTab>
        ))}
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Customer updates could not be loaded" message={error} /></GlassCard>
      ) : items.length === 0 ? (
        <GlassCard><EmptyStateSurface title="No customer updates" message="Customer-update drafts appear here once created from a claim." /></GlassCard>
      ) : filtered.length === 0 ? (
        <GlassCard><EmptyFilteredStateSurface title="No drafts in this state" onClear={() => setTab("All")} /></GlassCard>
      ) : (
        <Table label="Customer update queue">
          <TableHead>
            <TableRow>
              <TableHeaderCell>Draft</TableHeaderCell>
              <TableHeaderCell>Channel</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Updated</TableHeaderCell>
              <TableHeaderCell numeric>Actions</TableHeaderCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {filtered.map((item) => {
              const approveBlocked = blockedReason(item, "approveCustomerUpdate");
              const sendBlocked = blockedReason(item, "sendCustomerUpdate");
              return (
                <TableRow key={item.updateId} className="align-top">
                  <TableCell>
                    <div className="font-semibold text-dls-text-primary">{humanizeIdentifier(item.reasonCategory)} · {item.customerName}</div>
                    <div className="max-w-[420px] truncate text-[11.5px] text-dls-text-secondary">{item.messageBody}</div>
                    <Link to={`/app/claims/${item.caseId}`} className="dls-focus-outline mt-0.5 inline-flex items-center gap-1 rounded-dls-control text-[11px] font-semibold text-dls-primary hover:underline">
                      <ExternalLink className="h-3 w-3" aria-hidden="true" /> Claim {claimReference(item.caseId)}
                    </Link>
                  </TableCell>
                  <TableCell><Badge tone="info">{item.channel}</Badge></TableCell>
                  <TableCell><StatusBadge status={item.status} /></TableCell>
                  <TableCell className="text-dls-text-muted">{parityMode ? item.updatedAt : new Date(item.updatedAt).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}</TableCell>
                  <TableCell numeric>
                    <div className="flex flex-wrap justify-end gap-1.5">
                      {allows(item, "approveCustomerUpdate") && (
                        <Button variant="secondary" onClick={() => setPending({ kind: "approve", item })}>Approve</Button>
                      )}
                      {allows(item, "approveCustomerUpdate") && (
                        // Button's destructive variant is zero-padding by design (§8.4, a text
                        // link, not a row action); hand-sized here to match its bordered/filled
                        // siblings in this row, same treatment Workflows.tsx already established.
                        <button
                          type="button"
                          onClick={() => setPending({ kind: "reject", item })}
                          className="dls-focus-outline inline-flex min-h-11 items-center justify-center rounded-dls-control px-4 py-[9px] text-[13px] font-bold text-dls-danger-text transition-colors duration-dls-fast ease-dls hover:underline"
                        >
                          Reject
                        </button>
                      )}
                      {allows(item, "sendCustomerUpdate") && (
                        <Button variant="primary" onClick={() => setPending({ kind: "send", item })}>Send</Button>
                      )}
                      {!allows(item, "approveCustomerUpdate") && !allows(item, "sendCustomerUpdate") && (
                        <span className="text-[11px] text-dls-text-muted" title={approveBlocked ?? sendBlocked ?? undefined}>{approveBlocked ?? sendBlocked ?? "No action available"}</span>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}

      <GovernedActionDialog
        open={!!pending}
        title={pending?.kind === "approve" ? "Approve customer update" : pending?.kind === "reject" ? "Reject customer update" : "Send customer update"}
        target={pending ? `${pending.item.customerName} · ${pending.item.channel} · ${pending.item.reasonCategory}` : ""}
        impact={
          pending?.kind === "send"
            ? "This dispatches the message to the customer through the configured provider immediately. This cannot be undone."
            : pending?.kind === "approve"
              ? "This marks the draft as approved and makes it eligible to send."
              : "This marks the draft as rejected. It will not be sendable."
        }
        evidence={
          pending ? (
            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-3">
              <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-dls-text-muted">Message</p>
              <p className="mt-2 whitespace-pre-wrap text-sm text-dls-text-primary">{pending.item.messageBody}</p>
            </div>
          ) : undefined
        }
        auditNotice="This action is written to the audit trail and cannot be undone silently."
        confirmLabel={pending?.kind === "approve" ? "Approve" : pending?.kind === "reject" ? "Reject" : "Send"}
        tone={pending?.kind === "reject" ? "danger" : "primary"}
        requireReason={false}
        onClose={() => setPending(null)}
        onConfirm={handleConfirm}
      />
    </>
  );
}
