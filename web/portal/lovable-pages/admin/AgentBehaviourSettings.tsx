"use client";

import { useEffect, useId, useState } from "react";
import { useParams } from "next/navigation";
import { AlertTriangle, CheckCircle2, History, Loader2, PlayCircle, ShieldCheck } from "lucide-react";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { MarkdownText } from "@/components/iip/MarkdownText";
import { Button } from "@/components/ui/Button";
import { Input, Field as UIField } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import {
  type AgentBehaviourStatus,
  type AgentBehaviourVersion,
  getAgentBehaviour,
  getAgentBehaviourVersions,
  saveAgentBehaviour,
  testAgentBehaviour,
} from "@/lib/agentBehaviour";

const AGENT_LABELS: Record<string, string> = {
  "command-center": "Command Center Agent",
  "claims-summary": "Claims Summary Agent",
  "document-checker": "Document Checker Agent",
  "policy-intelligence": "Policy Intelligence Agent",
  "risk-fraud-signal": "Risk & Fraud Signal Agent",
  "customer-update-drafting": "Customer Update Drafting Agent",
  routing: "Routing Agent",
  "knowledge-assistant": "Knowledge Assistant",
  "report-insight": "Report Insight Agent",
  "underwriting-intelligence": "Underwriting Intelligence Agent",
};

type FormState = {
  systemPromptOverride: string;
  persona: string;
  language: string;
  refusalTopics: string;
  escalationMessage: string;
  maxResponseCharacters: string;
  requireCitation: boolean;
};

function toForm(status: AgentBehaviourStatus): FormState {
  const v = status.currentVersion;
  return {
    systemPromptOverride: v?.systemPromptOverride ?? "",
    persona: v?.persona ?? "",
    language: v?.language ?? "en",
    refusalTopics: v?.refusalTopics.join(", ") ?? "",
    escalationMessage: v?.escalationMessage ?? "",
    maxResponseCharacters: v?.maxResponseCharacters ? String(v.maxResponseCharacters) : "",
    requireCitation: v?.requireCitation ?? true,
  };
}

export default function AgentBehaviourSettings() {
  const params = useParams<{ key: string }>();
  const agentKey = decodeURIComponent(params.key);
  const { roleDef } = useUI();
  const role = roleDef.key;
  const label = AGENT_LABELS[agentKey] ?? agentKey;

  const [status, setStatus] = useState<AgentBehaviourStatus | null>(null);
  const [versions, setVersions] = useState<AgentBehaviourVersion[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [form, setForm] = useState<FormState | null>(null);
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [showHistory, setShowHistory] = useState(false);
  const [testQuestion, setTestQuestion] = useState("");
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ ok: boolean; message: string; citations?: { title: string }[] } | null>(null);
  const isCitationLocked = agentKey === "policy-intelligence";

  async function load() {
    setLoading(true);
    setLoadError(null);
    const [statusResult, versionsResult] = await Promise.all([
      getAgentBehaviour(role, agentKey),
      getAgentBehaviourVersions(role, agentKey),
    ]);
    setLoading(false);
    if (!statusResult.ok) {
      setLoadError(statusResult.error);
      return;
    }
    setStatus(statusResult.value);
    setForm(toForm(statusResult.value));
    if (versionsResult.ok) setVersions(versionsResult.value);
  }

  useEffect(() => { void load(); }, [role, agentKey]);

  async function onSave(reason: string) {
    if (!form) return;
    setSaveError(null);
    const result = await saveAgentBehaviour(role, agentKey, {
      systemPromptOverride: form.systemPromptOverride.trim() || null,
      persona: form.persona.trim() || null,
      language: form.language.trim() || "en",
      allowedTopics: [],
      refusalTopics: form.refusalTopics.split(",").map((t) => t.trim()).filter(Boolean),
      escalationMessage: form.escalationMessage.trim() || null,
      maxResponseCharacters: form.maxResponseCharacters.trim() ? Number(form.maxResponseCharacters.trim()) : null,
      requireCitation: isCitationLocked ? true : form.requireCitation,
    }, reason);
    if (!result.ok) {
      setSaveError(result.error);
      throw new Error(result.error);
    }
    setStatus(result.value);
    setForm(toForm(result.value));
    setSaveDialogOpen(false);
    void load();
  }

  async function onTest() {
    if (!testQuestion.trim()) return;
    setTesting(true);
    setTestResult(null);
    const result = await testAgentBehaviour(role, agentKey, testQuestion.trim());
    setTesting(false);
    if (!result.ok) {
      setTestResult({ ok: false, message: result.error });
      return;
    }
    if (!result.value.tested) {
      setTestResult({ ok: false, message: result.value.message ?? "This agent cannot be tested here." });
      return;
    }
    setTestResult({ ok: true, message: result.value.answer ?? "", citations: result.value.citations });
  }

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Agents"
        title={`${label} · Behaviour`}
        sub="Shape this agent's voice and scope. The platform's decision boundary below is never editable — a tenant can change how the agent talks, never what it is allowed to decide."
      />

      {loading && <GlassCard><LoadingSkeleton rows={5} /></GlassCard>}
      {!loading && loadError && <GlassCard><ErrorStateSurface title="This agent's behaviour could not be loaded" message={loadError} /></GlassCard>}

      {!loading && !loadError && status && form && (
        <div className="space-y-4">
          <GlassCard>
            <div className="mb-2 flex items-center gap-2">
              <ShieldCheck className="h-4 w-4 text-dls-info-text" aria-hidden="true" />
              <div className="text-[14px] font-bold text-dls-text-primary">Effective prompt</div>
              {status.isCustomized ? <Badge tone="info">Customized · v{status.currentVersion?.version}</Badge> : <Badge tone="neutral">Platform default</Badge>}
            </div>
            <p className="mb-3 text-[12px] text-dls-text-secondary">This is exactly what the model receives — the immutable section first, then what this workspace configured below.</p>
            <div className="rounded-dls-control border border-dls-warning-dot bg-dls-warning-tint p-3 text-[12px] leading-5 text-dls-text-primary">
              <div className="mb-1 text-[10.5px] font-bold uppercase tracking-[0.08em] text-dls-warning-text">Platform preamble · not editable</div>
              {status.effectivePreamble}
            </div>
            <div className="mt-2 rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-3 text-[12px] leading-5 text-dls-text-primary">
              <div className="mb-1 text-[10.5px] font-bold uppercase tracking-[0.08em] text-dls-text-muted">This workspace&apos;s configuration</div>
              {status.effectiveTenantSection}
            </div>
          </GlassCard>

          <GlassCard>
            <div className="mb-3 text-[14px] font-bold text-dls-text-primary">Configure</div>
            <div className="space-y-4">
              <FormField label="System prompt override (optional — leave blank to use the platform default shown above)">
                {(id) => (
                  <textarea
                    id={id}
                    className="min-h-24 w-full resize-y rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-sm text-dls-text-primary outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                    value={form.systemPromptOverride}
                    onChange={(e) => setForm({ ...form, systemPromptOverride: e.target.value })}
                    placeholder="e.g. Answer in plain, everyday English. Keep replies under four sentences."
                  />
                )}
              </FormField>
              <div className="grid gap-3 sm:grid-cols-2">
                <FormField label="Tone / persona (optional)">
                  {(id) => <Input id={id} value={form.persona} onChange={(e) => setForm({ ...form, persona: e.target.value })} placeholder="e.g. Formal, plain-language" />}
                </FormField>
                <FormField label="Response language">
                  {(id) => <Input id={id} value={form.language} onChange={(e) => setForm({ ...form, language: e.target.value })} placeholder="en" />}
                </FormField>
              </div>
              <FormField label="Refusal topics (comma-separated, optional)" hint="If asked about any of these, the agent declines and uses the escalation message below instead of answering.">
                {(id) => <Input id={id} value={form.refusalTopics} onChange={(e) => setForm({ ...form, refusalTopics: e.target.value })} placeholder="e.g. premium pricing, legal advice" />}
              </FormField>
              <FormField label="Escalation message">
                {(id) => <Input id={id} value={form.escalationMessage} onChange={(e) => setForm({ ...form, escalationMessage: e.target.value })} placeholder="I'm not able to help with that directly — let me connect you with a member of our team." />}
              </FormField>
              <div className="grid gap-3 sm:grid-cols-2 sm:items-end">
                <FormField label="Max response length in characters (optional — for SMS/WhatsApp)">
                  {(id) => <Input id={id} value={form.maxResponseCharacters} onChange={(e) => setForm({ ...form, maxResponseCharacters: e.target.value.replace(/[^0-9]/g, "") })} placeholder="e.g. 320" />}
                </FormField>
                <CitationToggle
                  checked={isCitationLocked ? true : form.requireCitation}
                  locked={isCitationLocked}
                  onChange={(checked) => setForm({ ...form, requireCitation: checked })}
                />
              </div>
            </div>

            {saveError && (
              <div className="mt-4 flex items-start gap-2 rounded-dls-control bg-dls-danger-tint p-3 text-[12px] text-dls-danger-text">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> {saveError}
              </div>
            )}

            <div className="mt-5 flex items-center gap-2">
              <Button variant="primary" onClick={() => setSaveDialogOpen(true)}>Save configuration</Button>
              <Button variant="secondary" icon={<History aria-hidden="true" />} onClick={() => setShowHistory((v) => !v)}>
                {showHistory ? "Hide" : "Show"} version history ({versions.length})
              </Button>
            </div>
          </GlassCard>

          {showHistory && (
            <GlassCard>
              <div className="mb-3 text-[14px] font-bold text-dls-text-primary">Version history</div>
              {versions.length === 0 ? (
                <p className="text-[12px] text-dls-text-secondary">No changes have been saved yet — this agent is running on the platform default.</p>
              ) : (
                <div className="space-y-2">
                  {versions.map((v) => (
                    <div key={v.versionId} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-[12.5px] font-bold text-dls-text-primary">v{v.version}</span>
                        <span className="text-[11px] text-dls-text-muted">{v.updatedByUserId} · {new Date(v.updatedAt).toLocaleString()}</span>
                      </div>
                      <p className="mt-1 text-[12px] text-dls-text-secondary">{v.changeReason}</p>
                    </div>
                  ))}
                </div>
              )}
            </GlassCard>
          )}

          <GlassCard>
            <div className="mb-1 flex items-center gap-2">
              <PlayCircle className="h-4 w-4 text-dls-info-text" aria-hidden="true" />
              <div className="text-[14px] font-bold text-dls-text-primary">Test console</div>
            </div>
            <p className="mb-3 text-[12px] text-dls-text-secondary">Ask a sample question and see the drafted answer with citations. Sends nothing to a customer.</p>
            <div className="flex gap-2">
              <Input value={testQuestion} onChange={(e) => setTestQuestion(e.target.value)} placeholder="e.g. Is windscreen replacement covered under a comprehensive motor policy?" className="flex-1" />
              <Button variant="secondary" onClick={onTest} disabled={testing || !testQuestion.trim()} icon={testing ? <Loader2 className="animate-spin" aria-hidden="true" /> : <PlayCircle aria-hidden="true" />}>
                Test
              </Button>
            </div>
            {testResult && (
              <div className={`mt-3 rounded-dls-control p-3 text-[12px] ${testResult.ok ? "bg-dls-success-tint text-dls-success-text" : "bg-dls-warning-tint text-dls-text-secondary"}`}>
                <div className="flex items-start gap-2">
                  {testResult.ok ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />}
                  {/* Only the success branch is model output — render its markdown the same way
                      every other assistant surface does. The failure branch is our own error copy,
                      which stays literal so it is never reinterpreted as formatting. */}
                  {testResult.ok
                    ? <MarkdownText className="min-w-0 flex-1" content={testResult.message} />
                    : <span className="whitespace-pre-wrap">{testResult.message}</span>}
                </div>
                {testResult.citations && testResult.citations.length > 0 && (
                  <div className="mt-2 flex flex-wrap gap-1.5">
                    {testResult.citations.map((c, i) => <Badge key={i} tone="neutral">{c.title}</Badge>)}
                  </div>
                )}
              </div>
            )}
          </GlassCard>
        </div>
      )}

      <GovernedActionDialog
        open={saveDialogOpen}
        title={`Change ${label}'s behaviour`}
        target={label}
        impact="This changes what the agent says and how, for every request in this workspace from now on. It cannot change what the agent is allowed to decide."
        auditNotice="Changing an agent's configured behaviour is a governed action — record why."
        confirmLabel="Save"
        onClose={() => setSaveDialogOpen(false)}
        onConfirm={onSave}
      />
    </>
  );
}

function FormField({ label, hint, children }: { label: string; hint?: string; children: (id: string) => React.ReactNode }) {
  const id = useId();
  return (
    <UIField label={label} hint={hint} htmlFor={id}>
      {children(id)}
    </UIField>
  );
}

function CitationToggle({ checked, locked, onChange }: { checked: boolean; locked: boolean; onChange: (checked: boolean) => void }) {
  const id = useId();
  return (
    <label htmlFor={id} className="flex min-h-11 items-center gap-2 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[12.5px] text-dls-text-primary">
      <input
        id={id}
        type="checkbox"
        checked={checked}
        disabled={locked}
        onChange={(e) => onChange(e.target.checked)}
        className="h-4 w-4"
      />
      Require a citation for every factual claim
      {locked && <span className="text-[11px] text-dls-text-muted">(locked on for this agent)</span>}
    </label>
  );
}
