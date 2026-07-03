"use client";

import type { ReactNode } from "react";
import { useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { AssistantChat } from "@/components/AssistantChat";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, defaultTenantId, defaultWorkspaceId, scopedHeaders } from "@/lib/config";
import { Bot, FileSearch, MessageSquareWarning, Sparkles } from "lucide-react";

const SUGGESTED_PROMPTS = [
  "What does the approved policy wording say about this claim?",
  "What are the exclusions for this product line?",
  "What documents are required to progress this claim?",
];

export function AiAssistantClient({
  initialCaseId = "",
  initialProductLine = "",
}: {
  initialCaseId?: string;
  initialProductLine?: string;
}) {
  const { role, userId } = useRole();
  const [caseId, setCaseId] = useState(initialCaseId);
  const [productLine, setProductLine] = useState(initialProductLine);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        eyebrow="AI Conversations"
        title="Grounded insurance assistant"
        description="Ask policy and claim-context questions against approved tenant knowledge. Answers include citations and provenance or refuse and escalate when evidence is insufficient."
      />

      <div className="grid gap-6 xl:grid-cols-[320px_minmax(0,1fr)]">
        <aside className="space-y-6">
          <SectionCard eyebrow="Configuration" title="Request scope" description="These values ground every question you ask and appear in the composer's context window.">
            <div className="space-y-4">
              <Input label="Case ID (optional)" value={caseId} onChange={setCaseId} placeholder="e.g. a case GUID" />
              <Input label="Product line (optional)" value={productLine} onChange={setProductLine} placeholder="Product line" />
            </div>
          </SectionCard>

          <SectionCard eyebrow="Guardrails" title="Decision posture" description="Provider availability and evidence state are reported by the backend.">
            <div className="space-y-3 text-sm leading-6">
              <Boundary label="Knowledge source" value="Approved tenant knowledge only" />
              <Boundary label="May do" value="Explain, cite, summarize, escalate" />
              <Boundary label="May not do" value="Deny claims, bind policies, accuse fraud, bypass review" />
              <Boundary label="Uncertainty" value="Escalates and flags for human review" />
            </div>
          </SectionCard>

          <SectionCard eyebrow="Modes" title="Available conversation modes" description="Only configured and governed modes are exposed as usable interactions.">
            <div className="space-y-3">
              <Mode icon={<Bot className="h-4 w-4" />} label="Policy Q&A" state="Available" />
              <Mode icon={<FileSearch className="h-4 w-4" />} label="Document reasoning" state="Use Document Checker" />
              <Mode icon={<MessageSquareWarning className="h-4 w-4" />} label="Customer update drafting" state="Use Case Review" />
            </div>
          </SectionCard>
        </aside>

        {/* Chat surface */}
        <section className="flex flex-col rounded-3xl border border-line bg-surface shadow-card">
          <div className="flex items-center justify-between gap-3 border-b border-line px-5 py-4">
            <div className="flex items-center gap-2">
              <div className="flex h-7 w-7 items-center justify-center rounded-xl bg-brand-gradient text-white">
                <Sparkles className="h-4 w-4" />
              </div>
              <h2 className="text-base font-semibold text-ink">Conversation session</h2>
            </div>
            <span className="rounded-full border border-violet/30 bg-violet/10 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-violet">
              Human supervised
            </span>
          </div>
          <AssistantChat
            className="flex-1"
            productLine={productLine.trim() || null}
            caseId={caseId.trim() || null}
            endpointPath="/api/v1/assistant/policy-questions"
            suggestions={SUGGESTED_PROMPTS}
            emptyTitle="Ask a governed AI question"
            emptyHint="The assistant cites approved sources when available and escalates anything unsupported by tenant knowledge."
          />
        </section>
      </div>
    </div>
  );
}

function Input({ label, value, onChange, placeholder }: { label: string; value: string; onChange: (v: string) => void; placeholder?: string }) {
  return (
    <label className="block text-sm font-medium text-muted">
      {label}
      <input
        className="mt-1 w-full rounded-2xl border border-line bg-shell px-3 py-2.5 text-sm text-ink outline-none transition-all focus:border-violet/40 focus:ring-4 focus:ring-violet/10"
        value={value}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
      />
    </label>
  );
}

function Boundary({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-line bg-mist px-4 py-3">
      <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className="mt-1.5 text-sm font-semibold text-ink">{value}</p>
    </div>
  );
}

function Mode({ icon, label, state }: { icon: ReactNode; label: string; state: string }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-2xl border border-line bg-mist px-4 py-3">
      <div className="flex items-center gap-2 text-sm font-semibold text-ink">
        <span className="text-violet">{icon}</span>
        {label}
      </div>
      <span className="rounded-full border border-line bg-surface px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-muted">{state}</span>
    </div>
  );
}
