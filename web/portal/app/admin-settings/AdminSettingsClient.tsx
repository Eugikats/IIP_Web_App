"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/Badges";
import { StatusChip } from "@/components/GovernedUi";
import { SideDrawer } from "@/components/SideDrawer";
import { ErrorState, LoadingState } from "@/components/States";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { allowsAction, blockedActionReason, hasActionState, type ActionStateContract } from "@/lib/actionState";
import { isRole, roleLabels, roles, type Role } from "@/lib/roleUiConfig";
import { motion } from "framer-motion";

type ProviderModes = {
  storageProvider: string;
  authMode: string;
  aiProvider: string;
  documentProvider: string;
  retrievalProvider: string;
  emailProvider: string;
  communicationProvider: string;
  notificationProvider: string;
  seedDemoData: boolean;
};

type ProviderHealthItem = {
  name: string;
  mode: string;
  status: string;
  detail: string;
};

type ProviderHealth = {
  overallStatus?: string;
  status?: string;
  providers: ProviderHealthItem[];
};

type TenantConfig = {
  insurerName: string;
  country: string;
  region: string;
  auditRetentionDays: number;
  businessLines: { name: string; enabled: boolean }[];
  channels: { name: string; enabled: boolean }[];
};

type User = ActionStateContract & {
  userId: string;
  name: string;
  email: string;
  role: string;
  branch?: string;
  department?: string;
};

type ProviderSetting = {
  label: string;
  value: string;
  healthName?: string;
};

type PendingRoleChange = {
  user: User;
  nextRole: Role;
};

export function AdminSettingsClient() {
  const { role, userId } = useRole();
  const [providerModes, setProviderModes] = useState<ProviderModes | null>(null);
  const [providerHealth, setProviderHealth] = useState<ProviderHealth | null>(null);
  const [tenantConfig, setTenantConfig] = useState<TenantConfig | null>(null);
  const [users, setUsers] = useState<User[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [selectedProvider, setSelectedProvider] = useState<[string, string] | null>(null);
  const [showTenantPreview, setShowTenantPreview] = useState(false);
  const [selectedUser, setSelectedUser] = useState<User | null>(null);
  const [pendingRoleChange, setPendingRoleChange] = useState<PendingRoleChange | null>(null);
  const [status, setStatus] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const headers = scopedHeaders(role, userId);
        const [providerResponse, providerHealthResponse, configResponse, usersResponse] = await Promise.all([
          fetch(`${apiBaseUrl}/api/v1/admin/provider-modes`, { headers }),
          fetch(`${apiBaseUrl}/api/v1/admin/provider-health`, { headers }),
          fetch(`${apiBaseUrl}/api/v1/admin/tenant-configuration`, { headers }),
          fetch(`${apiBaseUrl}/api/v1/admin/users`, { headers })
        ]);

        if (!providerResponse.ok || !providerHealthResponse.ok || !configResponse.ok || !usersResponse.ok) {
          throw new Error("Admin settings require Admin or ITAdmin access.");
        }

        const usersPayload: unknown = await usersResponse.json();
        if (!Array.isArray(usersPayload) || !usersPayload.every(hasActionState)) {
          throw new Error("Admin user response did not include required backend role-change authorization state.");
        }

        setProviderModes((await providerResponse.json()) as ProviderModes);
        setProviderHealth((await providerHealthResponse.json()) as ProviderHealth);
        setTenantConfig((await configResponse.json()) as TenantConfig);
        setUsers(usersPayload as User[]);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Admin settings failed.");
      }
    }

    load();
  }, [role, userId]);

  async function changeRole(targetUser: User, nextRole: Role, auditReason: string) {
    setError(null);
    setStatus("Submitting role change for server authorization...");
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/users/${targetUser.userId}/role`, {
      method: "PUT",
      headers: {
        ...scopedHeaders(role, userId),
        "Content-Type": "application/json",
        "X-Audit-Reason": auditReason
      },
      body: JSON.stringify({ role: nextRole })
    });

    if (!response.ok) {
      setStatus(null);
      setError(`Role change failed with HTTP ${response.status}.`);
      return false;
    }

    const updatedPayload: unknown = await response.json();
    if (!hasActionState(updatedPayload)) {
      setUsers([]);
      setSelectedUser(null);
      setStatus("Role change accepted, but role controls were disabled pending a valid authorization refresh.");
      setError("Role change response did not include required backend authorization state.");
      return true;
    }

    const updated = updatedPayload as User;
    const usersResponse = await fetch(`${apiBaseUrl}/api/v1/admin/users`, { headers: scopedHeaders(role, userId) });
    if (!usersResponse.ok) {
      setUsers([]);
      setSelectedUser(null);
      setStatus("Role change accepted, but role controls were disabled pending refresh.");
      setError(`Role changed, but refreshed authorization state failed with HTTP ${usersResponse.status}.`);
      return true;
    }

    const usersPayload: unknown = await usersResponse.json();
    if (!Array.isArray(usersPayload) || !usersPayload.every(hasActionState)) {
      setUsers([]);
      setSelectedUser(null);
      setStatus("Role change accepted, but role controls were disabled pending a valid authorization refresh.");
      setError("Role changed, but refreshed users did not include required backend authorization state.");
      return true;
    }

    const refreshedUsers = usersPayload as User[];
    const refreshedUser = refreshedUsers.find((item) => item.userId === updated.userId) ?? updated;
    setUsers(refreshedUsers);
    setSelectedUser((current) => current && current.userId === refreshedUser.userId ? refreshedUser : current);
    setStatus("Role change accepted by the backend.");
    return true;
  }

  async function confirmRoleChange(reason: string) {
    if (!pendingRoleChange) return;

    const success = await changeRole(pendingRoleChange.user, pendingRoleChange.nextRole, reason);
    if (success) {
      setPendingRoleChange(null);
    }
  }

  function requestRoleChange(targetUser: User, nextRoleValue: string) {
    if (!isRole(nextRoleValue) || targetUser.role === nextRoleValue) {
      return;
    }

    setPendingRoleChange({ user: targetUser, nextRole: nextRoleValue });
  }

  if (error) {
    return <ErrorState message={error} />;
  }

  if (!providerModes || !providerHealth || !tenantConfig) {
    return <LoadingState label="Loading admin settings..." />;
  }

  const settings: ProviderSetting[] = [
    { label: "Storage Provider", value: providerModes.storageProvider, healthName: "database" },
    { label: "Blob Provider", value: providerModes.storageProvider, healthName: "blobStorage" },
    { label: "Auth Mode", value: providerModes.authMode },
    { label: "AI Provider", value: providerModes.aiProvider, healthName: "azureOpenAI" },
    { label: "Retrieval Provider", value: providerModes.retrievalProvider, healthName: "aiSearch" },
    { label: "Document Provider", value: providerModes.documentProvider, healthName: "documentIntelligence" },
    { label: "Email Provider", value: providerModes.emailProvider, healthName: "graphMail" },
    { label: "Communication Provider", value: providerModes.communicationProvider, healthName: "communications" },
    { label: "Notification Provider", value: providerModes.notificationProvider, healthName: "notifications" },
    { label: "Seeded Business Data", value: providerModes.seedDemoData ? "Enabled" : "Disabled" }
  ];
  const providerHealthByName = new Map(providerHealth.providers.map((provider) => [provider.name, provider]));

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }} className="space-y-6">
      <PageHeader
        eyebrow="Admin Settings"
        title="Tenant controls and provider modes"
        description="Tenant configuration, provider status, and user role controls are scoped to the authenticated runtime context."
      />

      <div className="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
        <SectionCard eyebrow="Platform" title="Provider modes" description="Click a provider row to inspect what is configured, unavailable, or unhealthy in this environment.">
          <div className="mb-4 flex items-center justify-between rounded-xl border border-line bg-surface/50 px-4 py-3 shadow-sm">
            <div>
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Provider health</p>
              <p className="text-xs text-ink font-mono mt-1">Configuration status only. Secret values are not surfaced.</p>
            </div>
            <StatusBadge label={providerHealth.overallStatus ?? providerHealth.status ?? "Not configured"} />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            {settings.map((setting) => (
              <button key={setting.label} className="flex min-w-0 flex-col items-start gap-2 rounded-xl border border-line bg-mist p-4 text-left transition hover:-translate-y-0.5 hover:border-accent hover:shadow-glow-cyan focus:outline-none" onClick={() => setSelectedProvider([setting.label, setting.healthName ?? setting.value])}>
                <span className="w-full break-words text-sm font-semibold text-ink font-mono">{setting.label}</span>
                <StatusBadge label={providerHealthByName.get(setting.healthName ?? "")?.status ?? setting.value} />
              </button>
            ))}
          </div>
        </SectionCard>

        <SectionCard eyebrow="Tenant" title="Tenant configuration" description="Workspace-safe insurer settings and enabled operating lines." actions={<button className="rounded-full border border-line bg-surface px-4 py-2 text-sm font-semibold text-muted hover:text-ink hover:border-accent/50 transition-all shadow-sm" onClick={() => setShowTenantPreview(true)}>Preview tenant</button>}>
          <div className="grid gap-3 md:grid-cols-2">
            <ConfigRow label="Insurer" value={tenantConfig.insurerName} />
            <ConfigRow label="Country" value={tenantConfig.country} />
            <ConfigRow label="Region" value={tenantConfig.region} />
            <ConfigRow label="Audit retention" value={`${tenantConfig.auditRetentionDays} days`} />
          </div>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <List title="Business lines" items={tenantConfig.businessLines.map((item) => `${item.name} ${item.enabled ? "enabled" : "disabled"}`)} />
            <List title="Channels" items={tenantConfig.channels.map((item) => `${item.name} ${item.enabled ? "enabled" : "disabled"}`)} />
          </div>
        </SectionCard>
      </div>

      <SectionCard eyebrow="Access" title="Users and roles" description="Role updates are accepted only when the backend authorizes the current runtime identity for this action.">
        {status ? <div className="mb-4 rounded-xl border border-accent/30 bg-accent/10 px-4 py-3 text-sm text-accent shadow-glow-cyan">{status}</div> : null}
        <div className="grid gap-3">
          {users.map((user) => (
            <div key={user.userId} className="grid gap-4 rounded-xl border border-line bg-mist p-4 md:grid-cols-[1fr_180px] md:items-center transition-colors hover:border-accent/40 hover:bg-surface/50">
              <button className="text-left focus:outline-none" onClick={() => setSelectedUser(user)}>
                <p className="font-semibold text-ink">{user.name}</p>
                <p className="text-sm text-muted font-mono mt-1">{user.email}</p>
                <p className="mt-2 text-[10px] font-bold uppercase tracking-[0.2em] text-muted">
                  {user.department ?? "Unassigned"} / {user.branch ?? "Head Office"}
                </p>
              </button>
              <select
                className="rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all disabled:cursor-not-allowed disabled:opacity-60 cursor-pointer font-mono"
                value={user.role}
                disabled={!allowsAction(user, "updateUserRole")}
                title={blockedActionReason(user, "updateUserRole") ?? "Change tenant role"}
                onChange={(event) => requestRoleChange(user, event.target.value)}
              >
                {roles.map((item) => (
                  <option className="bg-shell text-ink" key={item} value={item}>
                    {roleLabels[item]}
                  </option>
                ))}
              </select>
            </div>
          ))}
        </div>
      </SectionCard>

      <SideDrawer
        open={selectedProvider !== null || showTenantPreview || selectedUser !== null}
        title={selectedProvider ? selectedProvider[0] : showTenantPreview ? tenantConfig.insurerName : selectedUser?.name ?? ""}
        subtitle={
          selectedProvider
            ? "Provider mode detail. This view should be read literally and not as proof of unavailable live integration."
            : showTenantPreview
              ? "Tenant and workspace configuration preview."
              : "User and role assignment preview."
        }
        onClose={() => {
          setSelectedProvider(null);
          setShowTenantPreview(false);
          setSelectedUser(null);
        }}
      >
        {selectedProvider ? (
          <div className="space-y-5">
            {providerHealthByName.get(selectedProvider[1]) ? (
              [providerHealthByName.get(selectedProvider[1])!].map((provider) => (
                <div key={provider.name} className="rounded-xl border border-line bg-mist p-5 shadow-sm">
                  <div className="flex items-center justify-between gap-3">
                    <p className="text-sm font-semibold text-ink font-mono">{provider.name}</p>
                    <StatusChip label={providerState(provider)} tone={providerTone(providerState(provider))} />
                  </div>
                  <ConfigRow label="Mode" value={provider.mode} />
                  <p className="mt-4 text-sm leading-6 text-muted">{provider.detail}</p>
                </div>
              ))
            ) : (
              <ConfigRow label="Mode" value={selectedProvider[1]} />
            )}
            <div className="rounded-xl border border-warning/30 bg-warning/10 p-5 text-sm leading-7 text-warning shadow-glow-warning font-mono">
              {selectedProvider[0].includes("AI")
                ? "AI capabilities stay provider-abstracted. Unconfigured providers must not be presented as live Azure OpenAI or Foundry usage."
                : selectedProvider[0].includes("Document")
                  ? "Document processing is available only when a live document provider is configured and healthy."
                  : selectedProvider[0].includes("Communication") || selectedProvider[0].includes("Email") || selectedProvider[0].includes("Notification")
                    ? "Communications require configured providers. No live Outlook, Graph, SMS, WhatsApp, or Teams sending is implied when the provider is unavailable."
                    : "This provider setting reflects environment configuration, not an autonomous system capability."}
            </div>
          </div>
        ) : showTenantPreview ? (
          <div className="space-y-5">
            <div className="grid gap-3 sm:grid-cols-2">
              <ConfigRow label="Insurer" value={tenantConfig.insurerName} />
              <ConfigRow label="Country" value={tenantConfig.country} />
              <ConfigRow label="Region" value={tenantConfig.region} />
              <ConfigRow label="Retention" value={`${tenantConfig.auditRetentionDays} days`} />
            </div>
            <List title="Business lines" items={tenantConfig.businessLines.map((item) => `${item.name}: ${item.enabled ? "enabled" : "disabled"}`)} />
            <List title="Channels" items={tenantConfig.channels.map((item) => `${item.name}: ${item.enabled ? "enabled" : "disabled"}`)} />
          </div>
        ) : selectedUser ? (
          <div className="space-y-5">
            <ConfigRow label="Email" value={selectedUser.email} />
            <ConfigRow label="Role" value={selectedUser.role} />
            <ConfigRow label="Department" value={selectedUser.department ?? "Unassigned"} />
            <ConfigRow label="Branch" value={selectedUser.branch ?? "Head Office"} />
            <div className="rounded-xl border border-warning/30 bg-warning/10 p-5 text-sm leading-7 text-warning shadow-glow-warning">
              Role changes must be backed by server-side authorization and tenant-scoped identity claims before they are accepted.
            </div>
          </div>
        ) : null}
      </SideDrawer>
      <GovernedActionDialog
        open={pendingRoleChange !== null}
        title="Confirm tenant role change"
        target={pendingRoleChange ? `${pendingRoleChange.user.name} (${pendingRoleChange.user.email})` : ""}
        impact={pendingRoleChange ? `Change role from ${roleLabel(pendingRoleChange.user.role)} to ${roleLabel(pendingRoleChange.nextRole)}. This changes what the user can view or administer after the backend accepts the request.` : ""}
        auditNotice="Role changes are tenant-scoped administrative actions. The backend must authorize the current identity, and the reason is sent with the request for audit-supporting context."
        confirmLabel="Submit role change"
        evidence={pendingRoleChange ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <ConfigRow label="Current role" value={roleLabel(pendingRoleChange.user.role)} />
            <ConfigRow label="Requested role" value={roleLabel(pendingRoleChange.nextRole)} />
            <ConfigRow label="Department" value={pendingRoleChange.user.department ?? "Unassigned"} />
            <ConfigRow label="Branch" value={pendingRoleChange.user.branch ?? "Head Office"} />
          </div>
        ) : null}
        onClose={() => setPendingRoleChange(null)}
        onConfirm={confirmRoleChange}
      />
    </motion.div>
  );
}

function roleLabel(value: string) {
  return isRole(value) ? roleLabels[value] : value;
}

function ConfigRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</p>
      <p className="mt-2 text-sm font-semibold text-ink font-mono">{value}</p>
    </div>
  );
}

function AuthorityCard({ title, value, description }: { title: string; value: string; description: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{title}</p>
      <p className="mt-2 font-mono text-sm font-semibold text-ink">{value}</p>
      <p className="mt-2 text-sm leading-6 text-muted">{description}</p>
    </div>
  );
}

function List({ title, items }: { title: string; items: string[] }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{title}</p>
      <div className="mt-3 flex flex-wrap gap-2">
        {items.map((item) => (
          <span key={item} className="rounded-full border border-line bg-shell px-3 py-1.5 text-[10px] font-bold uppercase tracking-[0.1em] text-ink shadow-sm">
            {item}
          </span>
        ))}
      </div>
    </div>
  );
}

function providerState(provider: ProviderHealthItem): "Connected" | "Needs Configuration" | "Not Configured" | "Error" | "Disabled by Policy" {
  const combined = `${provider.status} ${provider.mode}`.toLowerCase();
  if (combined.includes("disabled")) return "Disabled by Policy";
  if (combined.includes("error") || combined.includes("failed") || combined.includes("unhealthy")) return "Error";
  if (combined.includes("missing") || combined.includes("notconfigured") || combined.includes("not configured")) return "Not Configured";
  if (combined.includes("degraded") || combined.includes("notready") || combined.includes("not ready")) return "Needs Configuration";
  if (combined.includes("healthy") || combined.includes("configured") || combined.includes("connected")) return "Connected";
  return "Needs Configuration";
}

function providerTone(state: ReturnType<typeof providerState>): "neutral" | "success" | "warning" | "danger" {
  if (state === "Connected") return "success";
  if (state === "Error") return "danger";
  if (state === "Not Configured" || state === "Disabled by Policy") return "neutral";
  return "warning";
}
