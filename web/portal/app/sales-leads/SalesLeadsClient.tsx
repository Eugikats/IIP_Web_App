"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { motion } from "framer-motion";
import { ArrowRight, Target } from "lucide-react";
import { StatusBadge } from "@/components/Badges";
import { DataTable } from "@/components/DataTable";
import { PageHeader } from "@/components/PageHeader";
import { useRole } from "@/components/RoleContext";
import { SectionCard } from "@/components/SectionCard";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type InboundMessage = {
  messageId: string;
  sender: string;
  subject: string;
  bodyPreview?: string;
  receivedAt: string;
  processingStatus: string | number;
};

export function SalesLeadsClient() {
  const { role, userId } = useRole();
  const [messages, setMessages] = useState<InboundMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/messages`, {
        headers: scopedHeaders(role, userId),
      });
      if (response.ok) setMessages((await response.json()) as InboundMessage[]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Sales & Leads intake failed to load.");
    } finally {
      setLoading(false);
    }
  }, [role, userId]);

  useEffect(() => {
    load();
  }, [load]);

  const prospectiveIntakes = useMemo(
    () => messages.filter((message) => /quote|renewal|cover|policy|lead|proposal/i.test(`${message.subject} ${message.bodyPreview ?? ""}`)),
    [messages]
  );

  if (loading) return <LoadingState label="Loading acquisition intake state..." />;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.35 }}>
      <PageHeader
        eyebrow="Roadmap module"
        title="Sales & Leads"
        description="The acquisition pipeline is visible in the information architecture, but no lead, prospect, quote, or underwriting workflow backend is configured yet."
      />
      {error ? <div className="mb-4"><ErrorState message={error} /></div> : null}

      <SectionCard title="Acquisition pipeline" description="No fabricated pipeline is shown. Connect lead and quote systems before this can operate as a sales workspace.">
        <div className="flex items-start gap-4 rounded-xl border border-warning/30 bg-warning/10 p-4">
          <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg border border-warning/30 bg-surface text-warning">
            <Target className="h-5 w-5" />
          </div>
          <div>
            <StatusBadge label="Not configured" />
            <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
              Lead capture, prospect records, quote readiness, underwriting referral, and bind workflows are not implemented in the MVP backend. Channel-originated enquiries can be inspected below when they exist, but they are not presented as a working pipeline.
            </p>
          </div>
        </div>
      </SectionCard>

      <div className="mt-5">
        <SectionCard eyebrow="Channel signals" title="Prospective intake messages" description="Read-only signals from real inbound channel messages; not a lead system of record.">
          {prospectiveIntakes.length === 0 ? (
            <EmptyState title="No prospective intakes" description="No inbound messages currently look like acquisition or quote enquiries." />
          ) : (
            <DataTable label="Prospective intake messages">
              <thead className="bg-mist text-left text-xs uppercase tracking-wider text-muted">
                <tr>
                  <th className="px-4 py-3">Sender</th>
                  <th className="px-4 py-3">Subject</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Next</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {prospectiveIntakes.map((message) => (
                  <tr key={message.messageId}>
                    <td className="px-4 py-3 font-mono text-xs text-ink">{message.sender}</td>
                    <td className="px-4 py-3">
                      <p className="text-sm font-semibold text-ink">{message.subject}</p>
                      {message.bodyPreview ? <p className="mt-1 line-clamp-1 text-xs text-muted">{message.bodyPreview}</p> : null}
                    </td>
                    <td className="px-4 py-3"><StatusBadge label={String(message.processingStatus)} /></td>
                    <td className="px-4 py-3">
                      <Link href="/channels" className="inline-flex items-center gap-1 text-sm font-semibold text-ai">
                        Review in Channels <ArrowRight className="h-3.5 w-3.5" />
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          )}
        </SectionCard>
      </div>
    </motion.div>
  );
}
