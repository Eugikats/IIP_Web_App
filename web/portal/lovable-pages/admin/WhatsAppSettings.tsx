"use client";

import { useEffect, useId, useState } from "react";
import { AlertTriangle, CheckCircle2, Copy, ExternalLink, Loader2, MessageCircle, PlugZap, RefreshCcw, ShieldCheck, Unplug } from "lucide-react";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Field as UIField } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import {
  type WhatsAppConfigurationStatus,
  deleteWhatsAppConfiguration,
  getWhatsAppConfigurationStatus,
  saveWhatsAppConfiguration,
  testWhatsAppConfiguration,
} from "@/lib/whatsappSettings";

// No verify-token field: IIP generates that itself and registers it with Meta directly (blueprint
// §4.3) — the operator never types or stores it.
const FIELD_GUIDE: { key: keyof FormState; label: string; placeholder: string; help: string; secret: boolean }[] = [
  { key: "phoneNumberId", label: "Phone number ID", placeholder: "109876543210987", help: "Meta Business Settings → WhatsApp Accounts → your number → Phone number ID.", secret: false },
  { key: "wabaId", label: "WhatsApp Business Account ID", placeholder: "102290129340398", help: "Meta Business Settings → WhatsApp Accounts → shown next to the phone number. Used to register the webhook automatically.", secret: false },
  { key: "accessToken", label: "Permanent access token", placeholder: "EAAG...", help: "System Users → your app's system user → Generate token (permanent, whatsapp_business_messaging + whatsapp_business_management scopes).", secret: true },
  { key: "appSecret", label: "App secret", placeholder: "App secret from Meta App Settings", help: "App Settings → Basic → App secret. Used to verify that inbound webhooks really came from Meta.", secret: true },
];

type FormState = { phoneNumberId: string; wabaId: string; accessToken: string; appSecret: string; businessDisplayName: string };
const EMPTY_FORM: FormState = { phoneNumberId: "", wabaId: "", accessToken: "", appSecret: "", businessDisplayName: "" };

const WEBHOOK_STATUS_LABEL: Record<string, string> = {
  Registered: "Webhook registered with Meta",
  RegistrationFailed: "Webhook registration failed — manual setup needed",
  ManualRequired: "Webhook needs manual setup",
};

export default function WhatsAppSettings() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [status, setStatus] = useState<WhatsAppConfigurationStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [connectDialogOpen, setConnectDialogOpen] = useState(false);
  const [disconnectDialogOpen, setDisconnectDialogOpen] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string | null } | null>(null);
  const [copied, setCopied] = useState(false);
  const displayNameId = useId();

  async function load() {
    setLoading(true);
    setLoadError(null);
    const result = await getWhatsAppConfigurationStatus(role);
    setLoading(false);
    if (!result.ok) {
      setLoadError(result.error);
      return;
    }
    setStatus(result.value);
  }

  useEffect(() => { void load(); }, [role]);

  const canSubmitConnect = FIELD_GUIDE.every((field) => form[field.key].trim().length > 0);

  async function onConnect(reason: string) {
    setFormError(null);
    const result = await saveWhatsAppConfiguration(role, {
      phoneNumberId: form.phoneNumberId.trim(),
      wabaId: form.wabaId.trim(),
      accessToken: form.accessToken.trim(),
      appSecret: form.appSecret.trim(),
      businessDisplayName: form.businessDisplayName.trim() || undefined,
    }, reason);
    if (!result.ok) {
      setFormError(result.error);
      throw new Error(result.error);
    }
    setStatus(result.value);
    setForm(EMPTY_FORM);
    setConnectDialogOpen(false);
  }

  async function onDisconnect(reason: string) {
    const result = await deleteWhatsAppConfiguration(role, reason);
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
    const result = await testWhatsAppConfiguration(role);
    setTesting(false);
    if (!result.ok) {
      setTestResult({ success: false, message: result.error });
      return;
    }
    setTestResult({ success: result.value.success, message: result.value.message });
    void load();
  }

  async function copyCallbackUrl(url: string) {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard access can fail (permissions, insecure context) — the URL is still selectable text.
    }
  }

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Channels"
        title="WhatsApp Business"
        sub="Connect your WhatsApp Business number so claimants can message you directly. Credentials are validated against Meta before anything is saved, encrypted at rest, and the webhook is registered automatically."
      />

      {loading && (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      )}

      {!loading && loadError && (
        <GlassCard><ErrorStateSurface title="Connection status could not be loaded" message={loadError} /></GlassCard>
      )}

      {!loading && !loadError && status && (
        <div className="space-y-4">
          <GlassCard>
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="flex items-start gap-3">
                <div className={`grid h-11 w-11 shrink-0 place-items-center rounded-dls-chip ${status.connected ? "bg-dls-success-tint text-dls-success-text" : "bg-dls-bg-page text-dls-text-muted"}`}>
                  <MessageCircle className="h-5 w-5" aria-hidden="true" />
                </div>
                <div>
                  <div className="flex items-center gap-2">
                    <div className="text-[14px] font-bold text-dls-text-primary">{status.connected ? status.businessDisplayName || "WhatsApp Business" : "Not connected"}</div>
                    {status.connected ? <Badge tone={status.lastValidationStatus === "Success" ? "success" : "warning"}>{status.lastValidationStatus === "Success" ? "Verified" : "Needs attention"}</Badge> : null}
                  </div>
                  {status.connected ? (
                    <div className="mt-1 text-[12px] text-dls-text-secondary">
                      Phone number ID {status.phoneNumberId} · configured by {status.configuredByUserId}
                      {status.configuredAt ? ` on ${new Date(status.configuredAt).toLocaleDateString()}` : ""}
                    </div>
                  ) : (
                    <div className="mt-1 text-[12px] text-dls-text-secondary">No WhatsApp Business number is connected for this workspace yet.</div>
                  )}
                </div>
              </div>
              {status.connected && (
                <div className="flex gap-2">
                  <Button variant="secondary" onClick={onTest} disabled={testing} icon={testing ? <Loader2 className="animate-spin" aria-hidden="true" /> : <RefreshCcw aria-hidden="true" />}>
                    Test connection
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
                <span>{testResult.success ? "Meta confirmed this connection is working." : testResult.message}</span>
              </div>
            )}

            {status.connected && status.lastValidationStatus === "Failed" && status.lastValidationError && (
              <div className="mt-4 flex items-start gap-2 rounded-dls-control bg-dls-danger-tint p-3 text-[12px] text-dls-danger-text">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> {status.lastValidationError}
              </div>
            )}

            {status.connected && status.webhookRegistrationStatus && (
              <div className={`mt-4 rounded-dls-control p-3 text-[12px] ${status.webhookRegistrationStatus === "Registered" ? "bg-dls-success-tint text-dls-success-text" : "bg-dls-warning-tint text-dls-text-secondary"}`}>
                <div className="flex items-start gap-2">
                  {status.webhookRegistrationStatus === "Registered"
                    ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                    : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-dls-warning-text" aria-hidden="true" />}
                  <span className="font-semibold">{WEBHOOK_STATUS_LABEL[status.webhookRegistrationStatus] ?? status.webhookRegistrationStatus}</span>
                </div>
                {status.webhookRegistrationError && (
                  <p className="mt-1 text-dls-text-secondary">{status.webhookRegistrationError}</p>
                )}
                {status.webhookRegistrationStatus !== "Registered" && status.webhookCallbackUrl && (
                  <div className="mt-2 flex items-center gap-2">
                    <code className="flex-1 truncate rounded bg-dls-surface-sunken px-2 py-1 font-mono text-[11px] text-dls-text-primary">{status.webhookCallbackUrl}</code>
                    <button
                      type="button"
                      onClick={() => copyCallbackUrl(status.webhookCallbackUrl!)}
                      className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-control border border-dls-border-input bg-dls-surface px-2 py-1 text-[11px] font-semibold text-dls-text-secondary hover:bg-dls-bg-page"
                    >
                      <Copy className="h-3 w-3" aria-hidden="true" /> {copied ? "Copied" : "Copy"}
                    </button>
                  </div>
                )}
                {status.webhookRegistrationStatus !== "Registered" && (
                  <p className="mt-2 text-dls-text-secondary">Paste this URL into Meta&apos;s webhook subscription screen if you need to register it manually.</p>
                )}
              </div>
            )}
          </GlassCard>

          {!status.connected && (
            <GlassCard>
              <div className="mb-1 flex items-center gap-2">
                <PlugZap className="h-4 w-4 text-dls-info-text" aria-hidden="true" />
                <div className="text-[14px] font-bold text-dls-text-primary">Connect your WhatsApp Business number</div>
              </div>
              <p className="mb-4 text-[12px] leading-5 text-dls-text-secondary">
                You&apos;ll need a Meta Business app with the WhatsApp product added. Each field below says exactly where to find it — the webhook is registered automatically once these validate.
              </p>

              <div className="space-y-4">
                {FIELD_GUIDE.map((field) => (
                  <WhatsAppField key={field.key} label={field.label} help={field.help}>
                    {(id) => (
                      <Input
                        id={id}
                        type={field.secret ? "password" : "text"}
                        value={form[field.key]}
                        onChange={(event) => setForm((current) => ({ ...current, [field.key]: event.target.value }))}
                        placeholder={field.placeholder}
                        autoComplete="off"
                      />
                    )}
                  </WhatsAppField>
                ))}
                <UIField label="Display name (optional — we'll use Meta's verified name if you leave this blank)" htmlFor={displayNameId}>
                  <Input
                    id={displayNameId}
                    value={form.businessDisplayName}
                    onChange={(event) => setForm((current) => ({ ...current, businessDisplayName: event.target.value }))}
                    placeholder="e.g. Acme Insurance"
                  />
                </UIField>
              </div>

              {formError && (
                <div className="mt-4 flex items-start gap-2 rounded-dls-control bg-dls-danger-tint p-3 text-[12px] text-dls-danger-text">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> {formError}
                </div>
              )}

              <div className="mt-5">
                <Button variant="primary" onClick={() => setConnectDialogOpen(true)} disabled={!canSubmitConnect} icon={<ShieldCheck aria-hidden="true" />}>
                  Validate and connect
                </Button>
              </div>
            </GlassCard>
          )}
        </div>
      )}

      <GovernedActionDialog
        open={connectDialogOpen}
        title="Connect WhatsApp Business"
        target={form.phoneNumberId || "New WhatsApp connection"}
        impact="IIP will call Meta's Graph API to confirm these credentials work, then encrypt and store them, and register a webhook so Meta can deliver messages here. This does not send any message to customers."
        auditNotice="Connecting a channel is recorded in the audit trail with your name against it — note why you are connecting this number."
        confirmLabel="Validate and save"
        onClose={() => setConnectDialogOpen(false)}
        onConfirm={onConnect}
        evidence={
          <div className="rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-3 text-[12px] text-dls-text-secondary">
            <div>Phone number ID: <span className="font-semibold text-dls-text-primary">{form.phoneNumberId || "—"}</span></div>
            <div>WABA ID: <span className="font-semibold text-dls-text-primary">{form.wabaId || "—"}</span></div>
            {form.businessDisplayName && <div>Display name: <span className="font-semibold text-dls-text-primary">{form.businessDisplayName}</span></div>}
          </div>
        }
      />

      <GovernedActionDialog
        open={disconnectDialogOpen}
        title="Disconnect WhatsApp Business"
        target={status?.businessDisplayName || status?.phoneNumberId || "This WhatsApp connection"}
        impact="Inbound and outbound WhatsApp messages will stop working immediately for this workspace until a connection is configured again."
        auditNotice="Disconnecting a live channel is recorded in the audit trail with your name against it — note why."
        confirmLabel="Disconnect"
        tone="danger"
        onClose={() => setDisconnectDialogOpen(false)}
        onConfirm={onDisconnect}
      />

      <div className="mt-6 flex items-center gap-1.5 text-[11px] text-dls-text-muted">
        <ExternalLink className="h-3 w-3" aria-hidden="true" /> Meta Business Settings is where phone numbers, tokens, and app secrets are managed on Meta&apos;s side.
      </div>
    </>
  );
}

function WhatsAppField({ label, help, children }: { label: string; help: string; children: (id: string) => React.ReactNode }) {
  const id = useId();
  return (
    <div>
      <UIField label={label} hint={help} htmlFor={id}>{children(id)}</UIField>
    </div>
  );
}
