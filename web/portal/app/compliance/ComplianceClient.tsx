"use client";

import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { DataTable } from "@/components/DataTable";
import { SideDrawer } from "@/components/SideDrawer";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { StatusBadge } from "@/components/Badges";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { motion } from "framer-motion";

type AuditEvent = {
  eventId: string;
  caseId?: string;
  userId?: string;
  actorType: string;
  actionName: string;
  timestamp: string;
  validationResult: string;
  correlationId: string;
};

type AuditPack = {
  caseId: string;
  status: string;
  auditEvents: AuditEvent[];
  documents: { documentId: string; originalFileName: string; documentType: string; processingStatus: string }[];
  assistantActions: { agentActionLogId: string; agentName: string; riskFlag: string; createdAt: string }[];
  approvals: { approvalId: string; approvalType: string; status: string; riskLevel: string }[];
  customerUpdates: { updateId: string; channel: string; status: string; deliveryStatus?: string }[];
};

export function ComplianceClient() {
  const { role, userId } = useRole();
  const [events, setEvents] = useState<AuditEvent[]>([]);
  const [caseFilter, setCaseFilter] = useState("");
  const [eventFilter, setEventFilter] = useState("");
  const [actorFilter, setActorFilter] = useState("");
  const [auditPack, setAuditPack] = useState<AuditPack | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      if (caseFilter) params.set("caseId", caseFilter);
      if (eventFilter) params.set("eventType", eventFilter);
      if (actorFilter) params.set("actor", actorFilter);
      const response = await fetch(`${apiBaseUrl}/api/v1/compliance/audit-events?${params.toString()}`, {
        headers: scopedHeaders(role, userId)
      });

      if (!response.ok) {
        throw new Error("Compliance view requires Compliance, Admin, ClaimsSupervisor, or ITAdmin access.");
      }

      setEvents((await response.json()) as AuditEvent[]);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Audit events failed.");
    } finally {
      setLoading(false);
    }
  }, [actorFilter, caseFilter, eventFilter, role, userId]);

  useEffect(() => {
    load();
  }, [load]);

  async function openPack(caseId: string) {
    const response = await fetch(`${apiBaseUrl}/api/v1/compliance/cases/${caseId}/audit-pack`, {
      headers: scopedHeaders(role, userId)
    });
    if (response.ok) {
      setAuditPack((await response.json()) as AuditPack);
    }
  }

  if (error) return <ErrorState message={error} />;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }}>
      <PageHeader eyebrow="Reports & Audit" title="Compliance & Audit" description="Read-only evidence views, audit packs, and authorised provenance references for claims operations." />

      <SectionCard
        eyebrow="Access model"
        title="Read-only audit support"
        description="This surface supports review and evidence preview only. It does not approve claims, change customer messages, or expose model request text, credentials, internal reserves, or staff deliberations."
      >
        <div className="grid gap-3 md:grid-cols-3">
          <AuditState label="Audit event table" status="Read only" description="Workspace-scoped events are retrieved through compliance APIs." />
          <AuditState label="Case audit pack" status="Preview only" description="Case packs summarise evidence counts and customer-update records." />
          <AuditState label="AI provenance" status="References only" description="Authorised staff see provenance references and counts, not model request text or draft message bodies." />
        </div>
      </SectionCard>

      <div className="mt-5">
        <SectionCard eyebrow="Filters" title="Audit filters" description="Filter by case, event type, or actor without changing audit contracts.">
        <div className="grid gap-3 md:grid-cols-[1fr_1fr_1fr_auto]">
          <input className="rounded-xl border border-line bg-shell px-4 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono" placeholder="Case ID" value={caseFilter} onChange={(event) => setCaseFilter(event.target.value)} />
          <input className="rounded-xl border border-line bg-shell px-4 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono" placeholder="Event type" value={eventFilter} onChange={(event) => setEventFilter(event.target.value)} />
          <input className="rounded-xl border border-line bg-shell px-4 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono" placeholder="Actor" value={actorFilter} onChange={(event) => setActorFilter(event.target.value)} />
          <button className="rounded-full bg-azure border border-azure/40 px-6 py-2 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all" onClick={load}>
            Apply
          </button>
        </div>
        </SectionCard>
      </div>

      <div className="mt-5">
        <SectionCard eyebrow="Evidence" title="Audit events" description="Restricted internal fields are excluded from this read-only view.">
          {loading ? (
            <LoadingState label="Loading audit events..." />
          ) : events.length === 0 ? (
            <EmptyState title="No audit events" description="Run claims, workflow, or communications activity to generate audit events." />
          ) : (
            <DataTable>
              <thead className="bg-mist text-left text-[10px] font-bold uppercase tracking-[0.2em] text-muted">
                <tr>
                  <th className="px-4 py-3 border-b border-line">Event</th>
                  <th className="px-4 py-3 border-b border-line">Case</th>
                  <th className="px-4 py-3 border-b border-line">Actor</th>
                  <th className="px-4 py-3 border-b border-line">Result</th>
                  <th className="px-4 py-3 border-b border-line">Time</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line bg-surface/50">
                {events.map((event) => {
                  const caseId = event.caseId;

                  return (
                  <tr key={event.eventId} className="hover:bg-mist/70 transition-colors">
                    <td className="px-4 py-3 font-semibold text-ink text-sm">{event.actionName}</td>
                    <td className="px-4 py-3 text-sm text-muted">
                      {caseId ? (
                        <button className="rounded-full border border-accent/40 bg-accent/10 px-3 py-1 text-xs font-semibold text-accent hover:bg-accent hover:text-white transition-colors" onClick={() => openPack(caseId)}>
                          Preview pack
                        </button>
                      ) : (
                        <span className="font-mono text-xs opacity-50">Workspace</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-sm text-muted font-mono">{event.userId ?? event.actorType}</td>
                    <td className="px-4 py-3 text-sm text-muted">{event.validationResult}</td>
                    <td className="px-4 py-3 text-xs text-muted font-mono">{new Date(event.timestamp).toLocaleString()}</td>
                  </tr>
                  );
                })}
              </tbody>
            </DataTable>
          )}
        </SectionCard>
      </div>

      <SideDrawer
        open={auditPack !== null}
        title={auditPack ? `Audit pack ${auditPack.caseId.slice(0, 8)}` : "Audit pack"}
        subtitle={auditPack ? `${auditPack.status} - Compliance-safe preview` : undefined}
        onClose={() => setAuditPack(null)}
      >
        {auditPack ? (
          <div className="space-y-5">
            <div className="grid gap-3 md:grid-cols-2">
              <Metric label="Audit events" value={auditPack.auditEvents.length} />
              <Metric label="Documents" value={auditPack.documents.length} />
              <Metric label="Assistant actions" value={auditPack.assistantActions.length} />
              <Metric label="Approvals" value={auditPack.approvals.length} />
            </div>
            <SectionCard title="Export state" className="shadow-none !bg-transparent !p-0 !border-0">
              <div className="rounded-xl border border-line bg-mist px-4 py-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-ink">Audit pack file export</p>
                  <StatusBadge label="Preview only" />
                </div>
                <p className="mt-2 text-xs leading-5 text-muted">
                  File generation is unavailable in this UI gate. Compliance staff can review the safe pack summary without creating a downloadable customer or regulator extract.
                </p>
              </div>
            </SectionCard>
            <SectionCard title="Documents" className="shadow-none !bg-transparent !p-0 !border-0">
              {auditPack.documents.length === 0 ? (
                <EmptyState title="No documents" description="No case documents are attached to this audit pack yet." />
              ) : (
                <div className="space-y-2">
                  {auditPack.documents.map((document) => (
                    <div key={document.documentId} className="rounded-xl border border-line bg-mist px-4 py-3">
                      <p className="font-semibold text-ink font-mono text-sm">{document.originalFileName}</p>
                      <p className="mt-2 text-xs font-bold uppercase tracking-[0.1em] text-muted">{document.documentType} / {document.processingStatus}</p>
                    </div>
                  ))}
                </div>
              )}
            </SectionCard>
            <SectionCard title="Customer updates" className="shadow-none !bg-transparent !p-0 !border-0">
              {auditPack.customerUpdates.length === 0 ? (
                <EmptyState title="No customer updates" description="No customer update records are present in this pack." />
              ) : (
                <div className="space-y-2">
                  {auditPack.customerUpdates.map((item) => (
                    <div key={item.updateId} className="rounded-xl border border-line bg-mist px-4 py-3">
                      <p className="font-semibold text-ink">{item.channel}</p>
                      <p className="mt-2 text-xs font-bold uppercase tracking-[0.1em] text-muted">{item.status} / {item.deliveryStatus ?? "No delivery status"}</p>
                    </div>
                  ))}
                </div>
              )}
            </SectionCard>
          </div>
        ) : null}
      </SideDrawer>
    </motion.div>
  );
}

function AuditState({ label, status, description }: { label: string; status: string; description: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm font-semibold text-ink">{label}</p>
        <StatusBadge label={status} />
      </div>
      <p className="mt-3 text-xs leading-5 text-muted">{description}</p>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4 flex flex-col justify-between">
      <p className="text-2xl font-semibold text-ink font-mono">{value}</p>
      <p className="mt-2 text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</p>
    </div>
  );
}
