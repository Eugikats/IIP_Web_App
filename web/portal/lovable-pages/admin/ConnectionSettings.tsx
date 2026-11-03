"use client";

import { useEffect, useId, useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2, Languages, Loader2, Mail, MessageSquare, MessageCircle, PlugZap, RefreshCcw, ShieldCheck, Unplug, Camera, Send, AtSign, Video, Pencil } from "lucide-react";
import { TranslationTryPanel } from "@/components/iip/TranslationTryPanel";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Select, Field as UIField } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import {
  type ConnectionStatus,
  type ConnectionType,
  EMAIL_PROVIDER_FIELDS,
  POLICY_SYSTEM_PROVIDER_FIELDS,
  SMS_PROVIDER_FIELDS,
  SOCIAL_PROVIDER_FIELDS,
  TRANSLATION_PROVIDER_FIELDS,
  deleteConnection,
  getConnectionProviders,
  getConnectionStatus,
  saveConnection,
  testConnection,
} from "@/lib/connections";

// Every connection type this screen manages. Adding a type means adding one entry here plus a
// field guide in lib/connections.ts, the screen, dialogs, and API calls stay the same.
const CONNECTION_TYPES: { type: ConnectionType; label: string; icon: typeof MessageSquare; description: string }[] = [
  { type: "sms", label: "SMS", icon: MessageSquare, description: "Send text messages to customers, claim status, magic links, decision outcomes." },
  { type: "email", label: "Email", icon: Mail, description: "Send email to customers over any SMTP-speaking mail system, no Azure tenant needed." },
  { type: "translation", label: "Translation", icon: Languages, description: "Translate customer messages and replies for the WhatsApp assistant, e.g. Sunbird AI." },
  { type: "policysystem", label: "Policy system", icon: PlugZap, description: "Connect a read-only policy lookup endpoint. Claims and document synchronization still require an insurer-specific mapping and source setup." },
  { type: "instagram", label: "Instagram", icon: Camera, description: "Connect an Instagram professional account for customer initiated DMs and staff approved replies." },
  { type: "messenger", label: "Facebook Messenger", icon: MessageCircle, description: "Connect a Facebook Page for customer initiated messages and staff approved replies." },
  { type: "tiktok", label: "TikTok", icon: Video, description: "Verify a TikTok business account. Business Messaging API access and eligibility are required before messaging can be enabled." },
  { type: "x", label: "X", icon: AtSign, description: "Connect X Direct Messages with an OAuth 2.0 user token. X developer access and DM scopes are required." },
  { type: "telegram", label: "Telegram", icon: Send, description: "Receive private messages and send staff approved replies through your bot." },
];

const FIELD_GUIDE_BY_TYPE: Record<ConnectionType, Record<string, import("@/lib/connections").ConnectionFieldSpec[]>> = {
  sms: SMS_PROVIDER_FIELDS,
  email: EMAIL_PROVIDER_FIELDS,
  translation: TRANSLATION_PROVIDER_FIELDS,
  policysystem: POLICY_SYSTEM_PROVIDER_FIELDS,
  ...SOCIAL_PROVIDER_FIELDS,
};

const SOCIAL_TYPES: ConnectionType[] = ["instagram", "messenger", "tiktok", "x", "telegram"];

export default function ConnectionSettings() {
  return (
    <>
      <SectionHeader
        eyebrow="Admin · Channels"
        title="Connections"
        sub="Manage workspace provider connections and credentials. Policy-system setup enables read-only policy lookup; it does not yet synchronize claims or documents."
      />
      <div className="space-y-4">
        {CONNECTION_TYPES.map((entry) => (
          <ConnectionCard key={entry.type} type={entry.type} label={entry.label} icon={entry.icon} description={entry.description} />
        ))}
      </div>
    </>
  );
}

function ConnectionCard({
  type, label, icon: Icon, description,
}: { type: ConnectionType; label: string; icon: typeof MessageSquare; description: string }) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const social = SOCIAL_TYPES.includes(type);
  const policySystem = type === "policysystem";
  const [status, setStatus] = useState<ConnectionStatus | null>(null);
  const [availableProviders, setAvailableProviders] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [providerError, setProviderError] = useState<string | null>(null);
  const [selectedProvider, setSelectedProvider] = useState<string>("");
  const [fieldValues, setFieldValues] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [connectDialogOpen, setConnectDialogOpen] = useState(false);
  const [disconnectDialogOpen, setDisconnectDialogOpen] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string | null } | null>(null);
  const [editing, setEditing] = useState(false);
  const messagingStatus = status?.lastValidationDiscovered?.messagingStatus ?? (type === "telegram" ? "Webhook setup required" : "Messaging not configured");
  const metaMessaging = type === "instagram" || type === "messenger";
  const metaAccountId = status?.lastValidationDiscovered?.accountId;
  const metaWebhookPath = metaMessaging && metaAccountId
    ? `/api/v1/integrations/meta/${type}/${encodeURIComponent(metaAccountId)}/webhook`
    : null;
  const xWebhookPath = type === "x" && metaAccountId
    ? `/api/v1/integrations/x/${encodeURIComponent(metaAccountId)}/webhook`
    : null;

  async function load() {
    setLoading(true);
    setLoadError(null);
    setProviderError(null);
    const [statusResult, providersResult] = await Promise.all([
      getConnectionStatus(role, type),
      getConnectionProviders(role, type),
    ]);
    setLoading(false);
    if (!statusResult.ok) {
      setLoadError(statusResult.error);
      return;
    }
    setStatus(statusResult.value);
    if (providersResult.ok) {
      setAvailableProviders(providersResult.value);
      if (!selectedProvider && providersResult.value.length > 0) setSelectedProvider(providersResult.value[0]);
    } else {
      setAvailableProviders([]);
      setProviderError(providersResult.error);
    }
  }

  useEffect(() => { void load(); }, [role, type]);

  const fieldGuide = useMemo(
    () => FIELD_GUIDE_BY_TYPE[type][selectedProvider] ?? [],
    [type, selectedProvider]
  );

  const requiredFields = fieldGuide.filter((f) => !f.optional);
  const canSubmitConnect = selectedProvider.length > 0 && requiredFields.every((f) => (fieldValues[f.key] ?? "").trim().length > 0);

  async function onConnect(reason: string) {
    setFormError(null);
    const secretFields: Record<string, string> = {};
    const nonSecretFields: Record<string, string> = {};
    for (const field of fieldGuide) {
      const value = (fieldValues[field.key] ?? "").trim();
      if (!value) continue;
      if (field.secret) secretFields[field.key] = value;
      else nonSecretFields[field.key] = value;
    }
    const result = await saveConnection(role, type, { providerKey: selectedProvider, secretFields, nonSecretFields }, reason);
    if (!result.ok) {
      setFormError(result.error);
      throw new Error(result.error);
    }
    setStatus(result.value);
    setFieldValues({});
    setEditing(false);
    setConnectDialogOpen(false);
  }

  async function onDisconnect(reason: string) {
    const result = await deleteConnection(role, type, reason);
    if (!result.ok) {
      throw new Error(result.error);
    }
    setDisconnectDialogOpen(false);
    setTestResult(null);
    void load();
  }

  async function onTest() {
    setTesting(true);
    setTestResult(null);
    const result = await testConnection(role, type);
    setTesting(false);
    if (!result.ok) {
      setTestResult({ success: false, message: result.error });
      return;
    }
    setTestResult({ success: result.value.success, message: result.value.message });
    void load();
  }

  return (
    <GlassCard className="mb-4">
      {loading && <LoadingSkeleton rows={3} />}

      {!loading && loadError && <ErrorStateSurface title={`${label} status could not be loaded`} message={loadError} onRetry={() => void load()} />}

      {!loading && !loadError && status && (
        <div>
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="flex items-start gap-3">
              <div className={`grid h-11 w-11 shrink-0 place-items-center rounded-dls-chip ${status.connected ? "bg-dls-success-tint text-dls-success-text" : "bg-dls-bg-page text-dls-text-muted"}`}>
                <Icon className="h-5 w-5" aria-hidden="true" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <div className="text-[14px] font-bold text-dls-text-primary">{status.connected ? `${label} · ${status.providerKey}` : label}</div>
                  {status.connected ? <Badge tone={status.lastValidationStatus === "Success" ? "success" : "warning"}>{status.lastValidationStatus === "Success" ? (social ? "Account verified" : policySystem ? "Settings validated" : "Verified") : "Needs attention"}</Badge> : null}
                  {social ? <Badge tone={messagingStatus === "Webhook active" || messagingStatus === "Human replies" ? "success" : "warning"}>{messagingStatus}</Badge> : null}
                </div>
                {status.connected ? (
                  <div className="mt-1 text-[12px] text-dls-text-secondary">
                    {status.nonSecretSettings?.senderId ? `Sender ID ${status.nonSecretSettings.senderId} · ` : ""}
                    configured by {status.configuredByUserId}
                    {status.configuredAt ? ` on ${new Date(status.configuredAt).toLocaleDateString()}` : ""}
                  </div>
                ) : (
                  <div className="mt-1 text-[12px] text-dls-text-secondary">{description}</div>
                )}
                {status.connected && status.lastValidationDiscovered && Object.keys(status.lastValidationDiscovered).length > 0 && (
                  <div className="mt-1 text-[11px] text-dls-text-muted">
                    {Object.entries(status.lastValidationDiscovered).filter(([k]) => k !== "messagingStatus").map(([k, v]) => `${k}: ${v}`).join(" · ")}
                  </div>
                )}
                {status.connected && metaWebhookPath && (
                  <div className="mt-3 max-w-3xl rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-3 text-[12px] text-dls-text-secondary">
                    <div className="font-semibold text-dls-text-primary">Finish webhook setup in Meta</div>
                    <p className="mt-1">Set the callback URL to your public API address followed by this path, then use the saved verification token and subscribe to message events:</p>
                    <code className="mt-1 block break-all text-[11px]">{metaWebhookPath}</code>
                    <p className="mt-1">After a signed event arrives, the status changes to webhook active. Customer initiated conversations can receive staff approved replies after they arrive.</p>
                  </div>
                )}
                {status.connected && xWebhookPath && (
                  <div className="mt-3 max-w-3xl rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-3 text-[12px] text-dls-text-secondary">
                    <div className="font-semibold text-dls-text-primary">Finish X webhook setup</div>
                    <p className="mt-1">Register this HTTPS callback with the X v2 Webhooks API, complete its CRC check, and subscribe the account to the X Activity API <code>dm.received</code> event. The client secret saved above validates CRC and signed webhook deliveries.</p>
                    <code className="mt-1 block break-all text-[11px]">{xWebhookPath}</code>
                    <p className="mt-1">Webhook status becomes active after a valid event arrives. Account verification alone does not confirm Direct Message entitlement or webhook subscription.</p>
                  </div>
                )}
              </div>
            </div>
            {status.connected && (
              <div className="flex gap-2">
                {social && <Button variant="secondary" onClick={() => { setEditing((current) => !current); setFormError(null); }} icon={<Pencil aria-hidden="true" />}>{editing ? "Cancel changes" : "Change credentials"}</Button>}
                <Button variant="secondary" onClick={onTest} disabled={testing} icon={testing ? <Loader2 className="animate-spin" aria-hidden="true" /> : <RefreshCcw aria-hidden="true" />}>
                  {social ? "Recheck account" : policySystem ? "Validate settings" : "Test connection"}
                </Button>
                <Button variant="secondary" onClick={() => setDisconnectDialogOpen(true)} icon={<Unplug aria-hidden="true" />} className="!text-dls-danger-text">
                  Disconnect
                </Button>
              </div>
            )}
          </div>

          {testResult && (
            <div className={`mt-4 flex items-start gap-2 rounded-dls-control p-3 text-[12px] ${testResult.success ? "bg-dls-success-tint text-dls-success-text" : "bg-dls-danger-tint text-dls-danger-text"}`}>
              {testResult.success ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />}
              <span>{testResult.success ? (social ? `The provider confirmed account access. ${messagingStatus}.` : policySystem ? "The HTTPS address and field settings passed validation. A real policy response is checked when a user performs a policy lookup." : "The provider confirmed this connection is working.") : testResult.message}</span>
            </div>
          )}

          {status.connected && status.lastValidationStatus === "Failed" && status.lastValidationError && (
            <div className="mt-4 flex items-start gap-2 rounded-dls-control bg-dls-danger-tint p-3 text-[12px] text-dls-danger-text">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> {status.lastValidationError}
            </div>
          )}

          {status.connected && type === "translation" && <TranslationTryPanel role={role} />}

          {(!status.connected || editing) && (
            <div className="mt-4">
              <div className="mb-1 flex items-center gap-2">
                <PlugZap className="h-4 w-4 text-dls-info-text" aria-hidden="true" />
                <div className="text-[14px] font-bold text-dls-text-primary">{social ? (editing ? `Replace ${label} credentials` : `Verify ${label} account`) : `Connect ${label.toLowerCase()}`}</div>
              </div>

              {providerError && <ErrorStateSurface title="Provider list could not be loaded" message={providerError} onRetry={() => void load()} />}
              {!providerError && availableProviders.length === 0 && <div className="mb-3 text-[12px] text-dls-text-secondary">Not configured. No provider is available for this connection in the current deployment.</div>}

              {availableProviders.length > 1 && (
                <div className="mb-3">
                  <UIField label="Provider" htmlFor="provider-select">
                    <Select
                      id="provider-select"
                      value={selectedProvider}
                      onChange={(e) => { setSelectedProvider(e.target.value); setFieldValues({}); }}
                    >
                      {availableProviders.map((p) => <option key={p} value={p}>{p}</option>)}
                    </Select>
                  </UIField>
                </div>
              )}

              <div className="space-y-4">
                {fieldGuide.map((field) => (
                  <ConnectionField key={field.key} label={field.label} help={field.help}>
                    {(id) => (
                      <Input
                        id={id}
                        type={field.secret ? "password" : "text"}
                        value={fieldValues[field.key] ?? ""}
                        onChange={(event) => setFieldValues((current) => ({ ...current, [field.key]: event.target.value }))}
                        placeholder={field.placeholder}
                        autoComplete="off"
                      />
                    )}
                  </ConnectionField>
                ))}
              </div>

              {formError && (
                <div className="mt-4 flex items-start gap-2 rounded-dls-control bg-dls-danger-tint p-3 text-[12px] text-dls-danger-text">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> {formError}
                </div>
              )}

              <div className="mt-5">
                <Button variant="primary" onClick={() => setConnectDialogOpen(true)} disabled={!canSubmitConnect} icon={<ShieldCheck aria-hidden="true" />}>
                  {social ? "Validate and save account" : "Validate and connect"}
                </Button>
              </div>
            </div>
          )}
        </div>
      )}

      <GovernedActionDialog
        open={connectDialogOpen}
        title={social ? `Verify ${label} account` : `Connect ${label}`}
        target={selectedProvider || `New ${label} connection`}
        impact={policySystem
          ? "IIP will validate the HTTPS address and field settings, then encrypt and store the authentication value. It will not call the external system until a staff member performs a policy lookup."
          : `IIP will call ${selectedProvider}'s API to confirm account access, then encrypt and store the credentials. ${social ? "Messaging remains not configured." : "This does not send any message to customers."}`}
        auditNotice={`Connecting a ${label.toLowerCase()} channel is a governed action, record why this is being connected now.`}
        confirmLabel="Validate and save"
        onClose={() => setConnectDialogOpen(false)}
        onConfirm={onConnect}
        evidence={
          <div className="rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-3 text-[12px] text-dls-text-secondary">
            <div>Provider: <span className="font-semibold text-dls-text-primary">{selectedProvider || "-"}</span></div>
          </div>
        }
      />

      <GovernedActionDialog
        open={disconnectDialogOpen}
        title={`Disconnect ${label}`}
        target={status?.providerKey || `This ${label.toLowerCase()} connection`}
        impact={social ? `The saved ${label} account credentials will be removed from this workspace.` : `Outbound ${label.toLowerCase()} will stop working immediately for this workspace until a connection is configured again.`}
        auditNotice={`Disconnecting a live ${label.toLowerCase()} channel is a governed action, record why.`}
        confirmLabel="Disconnect"
        tone="danger"
        onClose={() => setDisconnectDialogOpen(false)}
        onConfirm={onDisconnect}
      />
    </GlassCard>
  );
}

function ConnectionField({ label, help, children }: { label: string; help: string; children: (id: string) => React.ReactNode }) {
  const id = useId();
  return (
    <div>
      <UIField label={label} hint={help} htmlFor={id}>{children(id)}</UIField>
    </div>
  );
}
