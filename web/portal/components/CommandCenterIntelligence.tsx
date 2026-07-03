"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { useRole } from "./RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import {
  AlertTriangle,
  BookOpen,
  Bot,
  ChevronRight,
  FileText,
  MessageSquareText,
  Settings,
  ShieldCheck,
  UserCheck,
} from "lucide-react";

type Metrics = {
  averageCompletenessScore: number;
  knowledgeDocumentsApproved: number;
  assistantQuestionsAnswered: number;
  escalationsRequired: number;
  openClaims: number;
  newClaimsToday: number;
  incompleteClaims: number;
  pendingApprovals: number;
  openTasks: number;
  customerUpdatesPending: number;
  slaAtRisk: number;
  slaBreached: number;
};

/**
 * Command Center "Intelligence" rail content. Fetches its own metrics so it can
 * live in the AppShell's screen-edge right panel independent of the chat surface.
 * Honest "Not connected yet" states stand in for data the backend lacks.
 */
export function CommandCenterIntelligence() {
  const { role, userId, isLoadingContext } = useRole();
  const [metrics, setMetrics] = useState<Metrics | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let active = true;
    setError(false);
    setMetrics(null);

    if (isLoadingContext) {
      return () => {
        active = false;
      };
    }

    fetch(`${apiBaseUrl}/api/v1/command-center/metrics`, { headers: scopedHeaders(role, userId) })
      .then((r) => (r.ok ? r.json() : Promise.reject()))
      .then((data) => active && setMetrics(data as Metrics))
      .catch(() => active && setError(true));
    return () => {
      active = false;
    };
  }, [isLoadingContext, role, userId]);

  if (error) {
    return (
      <p className="rounded-2xl border border-danger-border bg-danger-soft px-3 py-2 text-xs text-danger-text">
        Could not load Command Center context.
      </p>
    );
  }

  if (!metrics) {
    return (
      <div className="space-y-3">
        {[0, 1, 2].map((i) => (
          <div key={i} className="h-24 animate-pulse rounded-2xl bg-surface-muted" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <ProviderContext metrics={metrics} />
      <OwnerLinks metrics={metrics} />
      <HumanBoundary />
      <CapabilityGaps />
    </div>
  );
}

function ProviderContext({ metrics }: { metrics: Metrics }) {
  return (
    <RailCard title="Provider And Evidence Context">
      <div className="space-y-2">
        <StatusRow icon={<Bot className="h-4 w-4 text-ai" />} label="Command metrics API" status="Available" />
        <StatusRow icon={<BookOpen className="h-4 w-4 text-primary" />} label="Approved knowledge" status={`${metrics.knowledgeDocumentsApproved} documents`} />
        <StatusRow icon={<ShieldCheck className="h-4 w-4 text-primary" />} label="Decision mode" status="Preparation only" />
      </div>
      <RailNote>
        The chat may summarize workflow pressure, but cited evidence and action provenance remain visible after a query.
      </RailNote>
    </RailCard>
  );
}

function OwnerLinks({ metrics }: { metrics: Metrics }) {
  return (
    <RailCard title="Owner Screens">
      <div className="space-y-2">
        <ActionRow icon={<UserCheck className="h-4 w-4 text-primary" />} title="Pending approvals" count={metrics.pendingApprovals} href="/approvals" />
        <ActionRow icon={<FileText className="h-4 w-4 text-warning-text" />} title="Incomplete documents" count={metrics.incompleteClaims} href="/document-checker" />
        <ActionRow icon={<AlertTriangle className="h-4 w-4 text-danger-text" />} title="SLA risk queue" count={metrics.slaAtRisk} href="/claims" />
        <ActionRow icon={<MessageSquareText className="h-4 w-4 text-primary" />} title="Customer updates" count={metrics.customerUpdatesPending} href="/customer-service" />
      </div>
      <RailNote>Open the owner screen for governed actions, reason capture, and workflow updates.</RailNote>
    </RailCard>
  );
}

function HumanBoundary() {
  return (
    <RailCard title="Human Boundary">
      <div className="rounded-2xl border border-line bg-surface px-3 py-3">
        <div className="flex items-center gap-2">
          <ShieldCheck className="h-4 w-4 text-primary" />
          <p className="text-sm font-semibold text-ink">Command Center prepares work</p>
        </div>
        <p className="mt-2 text-xs leading-5 text-muted">
          It does not approve, deny, settle, pay, accuse fraud, or issue a final complaint response. Those actions stay in governed human workflows.
        </p>
      </div>
    </RailCard>
  );
}

function CapabilityGaps() {
  return (
    <RailCard title="Configuration Gaps">
      <div className="space-y-2">
        <StatusRow icon={<Settings className="h-4 w-4 text-muted" />} label="Cycle-time analytics" status="Not configured" muted />
        <StatusRow icon={<Settings className="h-4 w-4 text-muted" />} label="Review completion rate" status="Not configured" muted />
      </div>
      <RailNote>Missing capabilities are shown as unavailable. The UI must not simulate connected providers.</RailNote>
    </RailCard>
  );
}

/* ── Primitives ─────────────────────────────────────────────────── */

function RailCard({ title, badge, children }: { title: string; badge?: ReactNode; children: ReactNode }) {
  return (
    <section className="rounded-2xl border border-line bg-surface-soft">
      <div className="flex items-center justify-between gap-2 border-b border-line px-4 py-2.5">
        <h3 className="text-[13px] font-semibold text-ink">{title}</h3>
        {badge}
      </div>
      <div className="space-y-2.5 p-3">{children}</div>
    </section>
  );
}

function RailNote({ children }: { children: ReactNode }) {
  return <p className="text-[11px] leading-relaxed text-muted">{children}</p>;
}

function ActionRow({ icon, title, count, href }: { icon: ReactNode; title: string; count: number; href: string }) {
  return (
    <Link href={href} className="group flex items-center gap-3 rounded-xl border border-line bg-surface p-2.5 transition-colors hover:bg-mist">
      <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-lg border border-line bg-mist">{icon}</div>
      <p className="min-w-0 flex-1 truncate text-sm font-semibold text-ink">{title}</p>
      <span className="flex-shrink-0 rounded-full border border-primary-border bg-primary-soft px-2 py-0.5 font-mono text-[10px] font-bold text-primary">{count}</span>
      <ChevronRight className="h-4 w-4 flex-shrink-0 text-muted transition-transform group-hover:translate-x-0.5" />
    </Link>
  );
}

function StatusRow({ icon, label, status, muted = false }: { icon: ReactNode; label: string; status: string; muted?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-xl border border-line bg-surface px-3 py-2">
      <div className="flex items-center gap-2.5">
        {icon}
        <span className={`min-w-0 text-sm ${muted ? "text-muted" : "text-ink"}`}>{label}</span>
      </div>
      <span className={`flex-shrink-0 text-right text-[11px] font-semibold ${muted ? "text-muted" : "text-ink"}`}>{status}</span>
    </div>
  );
}
