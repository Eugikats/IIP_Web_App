"use client";

import Link from "next/link";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { ArrowLeft, ArrowRight, Check, ExternalLink, Plus, Save, Trash2 } from "lucide-react";
import { StatusBadge } from "@/components/Badges";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { ErrorState, LoadingState } from "@/components/States";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type NamedToggle = { name: string; enabled: boolean };
type ApprovalThreshold = { productLine: string; amount: number; requiredRole: string };
type Checklist = { productLine: string; requiredDocuments: string[] };

type TenantConfiguration = {
  insurerName: string;
  logoUrl?: string | null;
  country: string;
  region: string;
  businessLines: NamedToggle[];
  channels: NamedToggle[];
  approvalThresholds: ApprovalThreshold[];
  checklists: Checklist[];
  auditRetentionDays: number;
};

type AdminUser = {
  userId: string;
  name: string;
  email: string;
  role: string;
  status: string;
};

type IntegrationHealth = {
  overallStatus: string;
  providers: { name: string; mode: string; status: string; detail: string }[];
};

type KnowledgeDocument = {
  knowledgeDocumentId: string;
  approvalStatus: string;
  indexedStatus: string;
};

const steps = ["Company", "Operating scope", "Readiness", "Review"] as const;

export function TenantOnboardingClient() {
  const { role, userId, can } = useRole();
  const headers = useMemo(() => scopedHeaders(role, userId), [role, userId]);
  const [configuration, setConfiguration] = useState<TenantConfiguration | null>(null);
  const [originalConfiguration, setOriginalConfiguration] = useState<TenantConfiguration | null>(null);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [integrationHealth, setIntegrationHealth] = useState<IntegrationHealth | null>(null);
  const [knowledgeDocuments, setKnowledgeDocuments] = useState<KnowledgeDocument[] | null>(null);
  const [step, setStep] = useState(0);
  const [newBusinessLine, setNewBusinessLine] = useState("");
  const [newChannel, setNewChannel] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [confirmSave, setConfirmSave] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setError(null);
      try {
        const [configurationResponse, usersResponse, integrationResponse, knowledgeResponse] = await Promise.all([
          fetch(`${apiBaseUrl}/api/v1/admin/tenant-configuration`, { headers }),
          fetch(`${apiBaseUrl}/api/v1/admin/users`, { headers }),
          can("ConfigureIntegrations")
            ? fetch(`${apiBaseUrl}/api/v1/integrations/email/health`, { headers })
            : Promise.resolve(null),
          can("ManageKnowledgeBase")
            ? fetch(`${apiBaseUrl}/api/v1/knowledge-documents`, { headers })
            : Promise.resolve(null)
        ]);

        if (!configurationResponse.ok || !usersResponse.ok) {
          throw new Error("Tenant onboarding requires tenant administration access.");
        }

        const configurationPayload: unknown = await configurationResponse.json();
        const usersPayload: unknown = await usersResponse.json();
        if (!isTenantConfiguration(configurationPayload)) {
          throw new Error("Tenant configuration response is incomplete.");
        }
        if (!Array.isArray(usersPayload) || !usersPayload.every(isAdminUser)) {
          throw new Error("Tenant user response is incomplete.");
        }

        let nextIntegrationHealth: IntegrationHealth | null = null;
        if (integrationResponse) {
          if (!integrationResponse.ok) {
            throw new Error(`Integration readiness failed with HTTP ${integrationResponse.status}.`);
          }
          const integrationPayload: unknown = await integrationResponse.json();
          if (!isIntegrationHealth(integrationPayload)) {
            throw new Error("Integration readiness response is incomplete.");
          }
          nextIntegrationHealth = integrationPayload;
        }

        let nextKnowledgeDocuments: KnowledgeDocument[] | null = null;
        if (knowledgeResponse) {
          if (!knowledgeResponse.ok) {
            throw new Error(`Knowledge readiness failed with HTTP ${knowledgeResponse.status}.`);
          }
          const knowledgePayload: unknown = await knowledgeResponse.json();
          if (!Array.isArray(knowledgePayload) || !knowledgePayload.every(isKnowledgeDocument)) {
            throw new Error("Knowledge readiness response is incomplete.");
          }
          nextKnowledgeDocuments = knowledgePayload;
        }

        if (cancelled) return;
        const normalized = normalizeConfiguration(configurationPayload);
        setConfiguration(normalized);
        setOriginalConfiguration(normalized);
        setUsers(usersPayload);
        setIntegrationHealth(nextIntegrationHealth);
        setKnowledgeDocuments(nextKnowledgeDocuments);
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Tenant onboarding failed to load.");
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [can, headers]);

  if (error && !configuration) return <ErrorState message={error} />;
  if (!configuration || !originalConfiguration) return <LoadingState label="Loading tenant onboarding..." />;

  const currentConfiguration = configuration;
  const hasChanges = stableConfiguration(configuration) !== stableConfiguration(originalConfiguration);
  const approvedKnowledge = knowledgeDocuments?.filter(
    (document) => document.approvalStatus === "Approved" && document.indexedStatus === "Indexed"
  ).length ?? 0;
  const activeUsers = users.filter((user) => user.status.toLowerCase() === "active").length;
  const readiness = [
    Boolean(configuration.insurerName.trim() && configuration.country.trim() && configuration.region.trim()),
    configuration.businessLines.some((line) => line.enabled),
    activeUsers > 0,
    integrationHealth?.overallStatus.toLowerCase() === "healthy" || integrationHealth?.overallStatus.toLowerCase() === "configured",
    knowledgeDocuments === null ? false : approvedKnowledge > 0
  ];
  const readyCount = readiness.filter(Boolean).length;

  function updateField<K extends keyof TenantConfiguration>(key: K, value: TenantConfiguration[K]) {
    setConfiguration((current) => current ? { ...current, [key]: value } : current);
    setStatus(null);
  }

  function addNamedItem(event: FormEvent, key: "businessLines" | "channels", value: string, clear: () => void) {
    event.preventDefault();
    const name = value.trim();
    if (!name) return;
    if (currentConfiguration[key].some((item) => item.name.toLowerCase() === name.toLowerCase())) {
      setError(`${name} is already present.`);
      return;
    }
    updateField(key, [...currentConfiguration[key], { name, enabled: true }]);
    clear();
    setError(null);
  }

  function updateNamedItem(key: "businessLines" | "channels", name: string, enabled: boolean) {
    updateField(key, currentConfiguration[key].map((item) => item.name === name ? { ...item, enabled } : item));
  }

  function removeNamedItem(key: "businessLines" | "channels", name: string) {
    updateField(key, currentConfiguration[key].filter((item) => item.name !== name));
  }

  async function saveConfiguration(auditReason: string) {
    setError(null);
    setStatus("Saving tenant setup...");
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/tenant-configuration`, {
      method: "PUT",
      headers: {
        ...headers,
        "Content-Type": "application/json",
        "X-Audit-Reason": auditReason
      },
      body: JSON.stringify({
        insurerName: currentConfiguration.insurerName.trim(),
        logoUrl: currentConfiguration.logoUrl?.trim() || null,
        country: currentConfiguration.country.trim(),
        region: currentConfiguration.region.trim(),
        businessLines: currentConfiguration.businessLines,
        channels: currentConfiguration.channels,
        approvalThresholds: currentConfiguration.approvalThresholds,
        checklists: currentConfiguration.checklists,
        auditRetentionDays: currentConfiguration.auditRetentionDays
      })
    });

    if (!response.ok) {
      setStatus(null);
      setError(await readFailure(response));
      return;
    }

    const payload: unknown = await response.json();
    if (!isTenantConfiguration(payload)) {
      setStatus(null);
      setError("Saved tenant configuration response is incomplete.");
      return;
    }

    const normalized = normalizeConfiguration(payload);
    setConfiguration(normalized);
    setOriginalConfiguration(normalized);
    setConfirmSave(false);
    setStatus("Tenant setup saved and recorded in the audit trail.");
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Tenant onboarding"
        title="Insurer setup workspace"
        description="Configure the tenant profile and operating scope, then review backend-reported users, knowledge, workflow, and provider readiness."
        actions={<StatusBadge label={`${readyCount} of ${readiness.length} readiness checks`} />}
      />

      <nav className="grid grid-cols-4 overflow-hidden rounded-lg border border-line bg-surface" aria-label="Onboarding steps">
        {steps.map((label, index) => (
          <button
            key={label}
            className={`min-w-0 border-r border-line px-3 py-3 text-left text-xs font-semibold last:border-r-0 ${index === step ? "bg-accent text-white" : "text-muted hover:bg-mist"}`}
            onClick={() => setStep(index)}
            aria-current={index === step ? "step" : undefined}
          >
            <span className="mr-2 font-mono">{index + 1}</span>
            <span className="hidden sm:inline">{label}</span>
          </button>
        ))}
      </nav>

      {error ? <ErrorState message={error} /> : null}
      {status ? <div className="rounded-lg border border-success/30 bg-success/5 px-4 py-3 text-sm text-success" role="status">{status}</div> : null}

      {step === 0 ? (
        <SectionCard eyebrow="Company profile" title="Insurer identity" description="Profile values are stored in the tenant configuration record.">
          <div className="grid gap-5 md:grid-cols-2">
            <TextField label="Insurer name" value={configuration.insurerName} onChange={(value) => updateField("insurerName", value)} required />
            <TextField label="Logo URL" value={configuration.logoUrl ?? ""} onChange={(value) => updateField("logoUrl", value)} placeholder="https://..." type="url" />
            <TextField label="Country" value={configuration.country} onChange={(value) => updateField("country", value)} required />
            <TextField label="Region" value={configuration.region} onChange={(value) => updateField("region", value)} required />
            <label className="block text-sm font-medium text-ink">
              Audit retention days
              <input
                aria-label="Audit retention days"
                className="mt-1 w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-2 focus:ring-accent/15"
                type="number"
                min={90}
                step={1}
                value={configuration.auditRetentionDays}
                onChange={(event) => updateField("auditRetentionDays", Number(event.target.value))}
              />
            </label>
          </div>
        </SectionCard>
      ) : null}

      {step === 1 ? (
        <div className="grid gap-6 lg:grid-cols-2">
          <NamedConfigurationList
            title="Business lines"
            description="Enable only product lines this workspace will operate."
            label="Business line name"
            items={configuration.businessLines}
            newValue={newBusinessLine}
            setNewValue={setNewBusinessLine}
            onAdd={(event) => addNamedItem(event, "businessLines", newBusinessLine, () => setNewBusinessLine(""))}
            onToggle={(name, enabled) => updateNamedItem("businessLines", name, enabled)}
            onRemove={(name) => removeNamedItem("businessLines", name)}
          />
          <NamedConfigurationList
            title="Communication channels"
            description="Channel enablement does not replace provider configuration or health checks."
            label="Channel name"
            items={configuration.channels}
            newValue={newChannel}
            setNewValue={setNewChannel}
            onAdd={(event) => addNamedItem(event, "channels", newChannel, () => setNewChannel(""))}
            onToggle={(name, enabled) => updateNamedItem("channels", name, enabled)}
            onRemove={(name) => removeNamedItem("channels", name)}
          />
        </div>
      ) : null}

      {step === 2 ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <ReadinessItem title="Company profile" status={readiness[0] ? "Configured" : "Not configured"} detail={configuration.insurerName || "Insurer profile is incomplete."} />
          <ReadinessItem title="Users and roles" status={activeUsers > 0 ? "Configured" : "Not configured"} detail={`${activeUsers} active user${activeUsers === 1 ? "" : "s"}`} href="/admin-settings" linkLabel="Manage users" />
          <ReadinessItem title="Provider connections" status={integrationHealth?.overallStatus ?? "Restricted"} detail={integrationHealth ? `${integrationHealth.providers.length} provider dependencies reported by the backend.` : "Current role cannot inspect integration health."} href="/integrations" linkLabel="Open integrations" />
          <ReadinessItem title="Approved knowledge" status={knowledgeDocuments === null ? "Restricted" : approvedKnowledge > 0 ? "Configured" : "Not configured"} detail={knowledgeDocuments === null ? "Current role cannot inspect knowledge readiness." : `${approvedKnowledge} approved and indexed source${approvedKnowledge === 1 ? "" : "s"}`} href={can("ManageKnowledgeBase") ? "/knowledge-base" : undefined} linkLabel="Open Knowledge Hub" />
          <ReadinessItem title="Approval thresholds" status={configuration.approvalThresholds.length > 0 ? "Configured" : "Not configured"} detail={`${configuration.approvalThresholds.length} threshold${configuration.approvalThresholds.length === 1 ? "" : "s"} stored for this workspace.`} href="/admin-settings" linkLabel="Review settings" />
          <ReadinessItem title="Document checklists" status={configuration.checklists.length > 0 ? "Configured" : "Not configured"} detail={`${configuration.checklists.length} checklist${configuration.checklists.length === 1 ? "" : "s"} stored for this workspace.`} href="/admin-settings" linkLabel="Review settings" />
        </div>
      ) : null}

      {step === 3 ? (
        <SectionCard eyebrow="Review" title="Tenant setup summary" description="Saving changes updates tenant-scoped configuration and writes an audit event naming changed configuration categories.">
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Summary label="Insurer" value={configuration.insurerName || "Not configured"} />
            <Summary label="Location" value={[configuration.country, configuration.region].filter(Boolean).join(" / ") || "Not configured"} />
            <Summary label="Audit retention" value={`${configuration.auditRetentionDays} days`} />
            <Summary label="Enabled business lines" value={`${configuration.businessLines.filter((item) => item.enabled).length}`} />
            <Summary label="Enabled channels" value={`${configuration.channels.filter((item) => item.enabled).length}`} />
            <Summary label="Backend readiness checks" value={`${readyCount} of ${readiness.length}`} />
          </dl>
          <div className="mt-6 flex items-center justify-between gap-4 border-t border-line pt-5">
            <p className="text-xs text-muted">{hasChanges ? "Unsaved tenant configuration changes are ready for review." : "No tenant configuration changes are pending."}</p>
            <button
              className="inline-flex items-center gap-2 rounded-lg bg-accent px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-hover disabled:cursor-not-allowed disabled:opacity-50"
              disabled={!hasChanges}
              onClick={() => setConfirmSave(true)}
            >
              <Save className="h-4 w-4" />
              Review and save
            </button>
          </div>
        </SectionCard>
      ) : null}

      <div className="flex items-center justify-between gap-3">
        <button className="inline-flex items-center gap-2 rounded-lg border border-line bg-surface px-4 py-2 text-sm font-semibold text-ink disabled:opacity-40" disabled={step === 0} onClick={() => setStep((current) => Math.max(0, current - 1))}>
          <ArrowLeft className="h-4 w-4" /> Previous
        </button>
        {step < steps.length - 1 ? (
          <button className="inline-flex items-center gap-2 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white hover:bg-brand-hover" onClick={() => setStep((current) => Math.min(steps.length - 1, current + 1))}>
            Continue <ArrowRight className="h-4 w-4" />
          </button>
        ) : null}
      </div>

      <GovernedActionDialog
        open={confirmSave}
        title="Save tenant onboarding configuration"
        target={configuration.insurerName || "Current tenant workspace"}
        impact="Updates insurer profile, operating scope, and retained governance configuration for this tenant workspace. Provider connections are not changed by this action."
        auditNotice="The backend derives the administrator and tenant scope from runtime identity and records the changed configuration categories with your reason."
        confirmLabel="Save tenant setup"
        evidence={<p className="text-xs text-muted">{configuration.businessLines.filter((item) => item.enabled).length} enabled business lines and {configuration.channels.filter((item) => item.enabled).length} enabled channels.</p>}
        onClose={() => setConfirmSave(false)}
        onConfirm={saveConfiguration}
      />
    </div>
  );
}

function TextField({ label, value, onChange, placeholder, type = "text", required = false }: { label: string; value: string; onChange: (value: string) => void; placeholder?: string; type?: string; required?: boolean }) {
  return (
    <label className="block text-sm font-medium text-ink">
      {label}
      <input aria-label={label} className="mt-1 w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-2 focus:ring-accent/15" type={type} value={value} placeholder={placeholder} required={required} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function NamedConfigurationList({ title, description, label, items, newValue, setNewValue, onAdd, onToggle, onRemove }: { title: string; description: string; label: string; items: NamedToggle[]; newValue: string; setNewValue: (value: string) => void; onAdd: (event: FormEvent) => void; onToggle: (name: string, enabled: boolean) => void; onRemove: (name: string) => void }) {
  return (
    <SectionCard title={title} description={description}>
      <form className="mb-4 flex gap-2" onSubmit={onAdd}>
        <input aria-label={label} className="min-w-0 flex-1 rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-2 focus:ring-accent/15" value={newValue} onChange={(event) => setNewValue(event.target.value)} />
        <button className="inline-flex h-10 w-10 flex-none items-center justify-center rounded-lg border border-line bg-surface text-ink hover:border-accent" type="submit" aria-label={`Add ${label.toLowerCase()}`} title={`Add ${label.toLowerCase()}`}><Plus className="h-4 w-4" /></button>
      </form>
      {items.length === 0 ? <p className="rounded-lg border border-dashed border-line p-4 text-sm text-muted">No entries configured.</p> : (
        <div className="divide-y divide-line rounded-lg border border-line">
          {items.map((item) => (
            <div className="flex items-center gap-3 px-3 py-3" key={item.name}>
              <input type="checkbox" aria-label={`Enable ${item.name}`} checked={item.enabled} onChange={(event) => onToggle(item.name, event.target.checked)} />
              <span className="min-w-0 flex-1 break-words text-sm font-medium text-ink">{item.name}</span>
              <StatusBadge label={item.enabled ? "Enabled" : "Disabled"} />
              <button className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-muted hover:bg-danger/10 hover:text-danger" onClick={() => onRemove(item.name)} aria-label={`Remove ${item.name}`} title={`Remove ${item.name}`}><Trash2 className="h-4 w-4" /></button>
            </div>
          ))}
        </div>
      )}
    </SectionCard>
  );
}

function ReadinessItem({ title, status, detail, href, linkLabel }: { title: string; status: string; detail: string; href?: string; linkLabel?: string }) {
  return (
    <SectionCard title={title} actions={<StatusBadge label={status} />}>
      <p className="min-h-10 text-sm leading-5 text-muted">{detail}</p>
      {href && linkLabel ? <Link className="mt-4 inline-flex items-center gap-2 text-sm font-semibold text-accent hover:underline" href={href}>{linkLabel}<ExternalLink className="h-3.5 w-3.5" /></Link> : null}
    </SectionCard>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return <div className="rounded-lg border border-line bg-mist p-4"><dt className="text-xs font-medium text-muted">{label}</dt><dd className="mt-2 break-words text-sm font-semibold text-ink">{value}</dd></div>;
}

function normalizeConfiguration(configuration: TenantConfiguration): TenantConfiguration {
  return {
    ...configuration,
    logoUrl: configuration.logoUrl ?? null,
    businessLines: configuration.businessLines.map((item) => ({ ...item })),
    channels: configuration.channels.map((item) => ({ ...item })),
    approvalThresholds: configuration.approvalThresholds.map((item) => ({ ...item })),
    checklists: configuration.checklists.map((item) => ({ ...item, requiredDocuments: [...item.requiredDocuments] }))
  };
}

function stableConfiguration(configuration: TenantConfiguration) {
  return JSON.stringify(configuration);
}

function isTenantConfiguration(value: unknown): value is TenantConfiguration {
  if (!value || typeof value !== "object") return false;
  const configuration = value as Partial<TenantConfiguration>;
  return typeof configuration.insurerName === "string"
    && (configuration.logoUrl === null || configuration.logoUrl === undefined || typeof configuration.logoUrl === "string")
    && typeof configuration.country === "string"
    && typeof configuration.region === "string"
    && typeof configuration.auditRetentionDays === "number"
    && Array.isArray(configuration.businessLines)
    && configuration.businessLines.every(isNamedToggle)
    && Array.isArray(configuration.channels)
    && configuration.channels.every(isNamedToggle)
    && Array.isArray(configuration.approvalThresholds)
    && configuration.approvalThresholds.every(isApprovalThreshold)
    && Array.isArray(configuration.checklists)
    && configuration.checklists.every(isChecklist);
}

function isNamedToggle(value: unknown): value is NamedToggle {
  if (!value || typeof value !== "object") return false;
  const item = value as Partial<NamedToggle>;
  return typeof item.name === "string" && typeof item.enabled === "boolean";
}

function isApprovalThreshold(value: unknown): value is ApprovalThreshold {
  if (!value || typeof value !== "object") return false;
  const threshold = value as Partial<ApprovalThreshold>;
  return typeof threshold.productLine === "string"
    && typeof threshold.amount === "number"
    && typeof threshold.requiredRole === "string";
}

function isChecklist(value: unknown): value is Checklist {
  if (!value || typeof value !== "object") return false;
  const checklist = value as Partial<Checklist>;
  return typeof checklist.productLine === "string"
    && Array.isArray(checklist.requiredDocuments)
    && checklist.requiredDocuments.every((document) => typeof document === "string");
}

function isAdminUser(value: unknown): value is AdminUser {
  if (!value || typeof value !== "object") return false;
  const user = value as Partial<AdminUser>;
  return typeof user.userId === "string" && typeof user.name === "string" && typeof user.email === "string" && typeof user.role === "string" && typeof user.status === "string";
}

function isIntegrationHealth(value: unknown): value is IntegrationHealth {
  if (!value || typeof value !== "object") return false;
  const health = value as Partial<IntegrationHealth>;
  return typeof health.overallStatus === "string" && Array.isArray(health.providers);
}

function isKnowledgeDocument(value: unknown): value is KnowledgeDocument {
  if (!value || typeof value !== "object") return false;
  const document = value as Partial<KnowledgeDocument>;
  return typeof document.knowledgeDocumentId === "string" && typeof document.approvalStatus === "string" && typeof document.indexedStatus === "string";
}

async function readFailure(response: Response) {
  try {
    const body = await response.json() as { detail?: string; message?: string; errors?: Record<string, string[]> };
    return (body.errors ? Object.values(body.errors).flat()[0] : null) ?? body.detail ?? body.message ?? `Tenant setup failed with HTTP ${response.status}.`;
  } catch {
    return `Tenant setup failed with HTTP ${response.status}.`;
  }
}
