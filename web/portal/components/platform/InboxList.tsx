"use client";

import { useState } from "react";
import Link from "next/link";
import { Check } from "lucide-react";
import { DashboardCard } from "./ui";
import { StatusPill } from "./ui";
import { PlatformEmptyState, PlatformErrorState, PlatformLoadingState } from "./ui";
import { actionLabel, formatWhen, humanize, INBOX_TABS, severityTone } from "@/lib/platform/format";
import type { InboxCounts, InboxItem } from "@/lib/platform/types";
import { usePlatformMutation, usePlatformQuery } from "@/lib/platform/use-platform";
import { Button, InlineError, SegmentedTabs } from "./ui";

type InboxTab = (typeof INBOX_TABS)[number]["key"];

export default function InboxList({
  workspaceId,
  status,
  onStatusChange,
}: {
  workspaceId?: string | null;
  status: string;
  onStatusChange: (status: string) => void;
}) {
  const ws = workspaceId ? `workspaceId=${workspaceId}` : "";
  const query = [status ? `status=${status}` : "", ws].filter(Boolean).join("&");
  const items = usePlatformQuery<InboxItem[]>(`/inbox${query ? `?${query}` : ""}`);
  const counts = usePlatformQuery<InboxCounts>(`/inbox/counts${ws ? `?${ws}` : ""}`);
  const mutate = usePlatformMutation();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const c = counts.data;
  const tabCount: Record<string, number | undefined> = c
    ? { "": c.total, needs_attention: c.needsAttention, awaiting_approval: c.awaitingApproval, recommendation: c.recommendations, monitoring: c.monitoring }
    : {};
  const tabs = INBOX_TABS.map((t) => ({ ...t, count: tabCount[t.key] }));

  const act = async (item: InboxItem, action: string) => {
    setBusy(`${item.id}:${action}`);
    setError(null);
    try {
      await mutate(`/inbox/${encodeURIComponent(item.id)}/${action}`, "POST", {});
    } catch (err) {
      setError(err instanceof Error ? err.message : "That did not work.");
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="space-y-4">
      <SegmentedTabs<InboxTab> tabs={tabs} value={(status as InboxTab) ?? ""} onChange={(k) => onStatusChange(k)} label="Inbox filters" />
      <InlineError message={error} />
      {items.loading ? (
        <PlatformLoadingState title="Loading the inbox…" />
      ) : items.error ? (
        <PlatformErrorState title="The inbox could not load" description={items.error} />
      ) : (items.data ?? []).length === 0 ? (
        <PlatformEmptyState
          title={status === "resolved" ? "Nothing resolved yet" : "Nothing needs you right now"}
          description="Active automations and high-severity channel insights put their findings here. Approvals prepared by the agent appear here too."
        />
      ) : (
        <div className="space-y-2.5">
          {(items.data ?? []).map((item) => (
            <DashboardCard key={item.id} padding="md">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-1.5">
                    <StatusPill tone={item.status === "awaiting_approval" ? "warning" : item.status === "monitoring" ? "info" : severityTone(item.severity)} dot>
                      {item.status === "awaiting_approval" ? "Awaiting approval" : item.status === "monitoring" ? "Monitoring" : humanize(item.severity)}
                    </StatusPill>
                    {item.status === "acknowledged" ? <StatusPill tone="neutral">Acknowledged</StatusPill> : null}
                    {item.status === "resolved" || item.status === "dismissed" ? <StatusPill tone="success">{humanize(item.status)}</StatusPill> : null}
                    {item.occurrences > 1 ? <span className="text-[11px] font-medium text-dls-text-muted">seen {item.occurrences}×</span> : null}
                    <span className="text-[11px] text-dls-text-muted">· {formatWhen(item.updatedAt)}</span>
                  </div>
                  <p className="mt-1.5 text-[14px] font-semibold text-dls-text-primary">{item.title}</p>
                  {item.body ? <p className="mt-0.5 text-[13px] leading-snug text-dls-text-secondary">{item.body}</p> : null}
                  <p className="mt-1.5 text-[11px] text-dls-text-muted">
                    {[item.workspaceName, item.automationName, item.source === "channel_insight" ? "Channel insight" : item.source === "agent_action" ? "Prepared by the agent" : null]
                      .filter(Boolean)
                      .join(" · ")}
                    {item.href ? (
                      <>
                        {" · "}
                        <Link href={item.href} className="font-semibold text-dls-primary hover:underline">
                          Open
                        </Link>
                      </>
                    ) : null}
                  </p>
                  {item.resolutionNote ? <p className="mt-1 text-[12px] italic text-dls-text-secondary">“{item.resolutionNote}”</p> : null}
                </div>
                {item.actions.length > 0 ? (
                  <div className="flex shrink-0 flex-wrap gap-2">
                    {item.actions.map((action) => (
                      <Button
                        key={action}
                        variant={action === "approve" || action === "resolve" ? "primary" : "secondary"}
                        className="px-3 py-1.5 text-[12px]"
                        disabled={busy !== null}
                        onClick={() => void act(item, action)}
                      >
                        {action === "resolve" || action === "approve" ? <Check className="h-3.5 w-3.5" /> : null}
                        {busy === `${item.id}:${action}` ? "…" : actionLabel(action)}
                      </Button>
                    ))}
                  </div>
                ) : null}
              </div>
            </DashboardCard>
          ))}
        </div>
      )}
    </div>
  );
}
