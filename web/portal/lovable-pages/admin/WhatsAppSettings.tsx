"use client";

import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, ExternalLink, Loader2, MessageCircle, PlugZap, RefreshCcw, ShieldCheck, Unplug } from "lucide-react";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { useUI } from "@/context/UIStateContext";
import {
  type WhatsAppConfigurationStatus,
  deleteWhatsAppConfiguration,
  getWhatsAppConfigurationStatus,
  saveWhatsAppConfiguration,
  testWhatsAppConfiguration,
} from "@/lib/whatsappSettings";

const FIELD_GUIDE: { key: keyof FormState; label: string; placeholder: string; help: string; secret: boolean }[] = [
  { key: "phoneNumberId", label: "Phone number ID", placeholder: "109876543210987", help: "Meta Business Settings → WhatsApp Accounts → your number → Phone number ID.", secret: false },
  { key: "accessToken", label: "Permanent access token", placeholder: "EAAG...", help: "System Users → your app's system user → Generate token (permanent, whatsapp_business_messaging scope).", secret: true },
  { key: "appSecret", label: "App secret", placeholder: "App secret from Meta App Settings", help: "App Settings → Basic → App secret. Used to verify that inbound webhooks really came from Meta.", secret: true },
  { key: "verifyToken", label: "Webhook verify token", placeholder: "Choose any string, e.g. a random password", help: "You choose this value yourself, then paste the same value into Meta's webhook subscription screen.", secret: true },
];

type FormState = { phoneNumberId: string; accessToken: string; appSecret: string; verifyToken: string; businessDisplayName: string };
const EMPTY_FORM: FormState = { phoneNumberId: "", accessToken: "", appSecret: "", verifyToken: "", businessDisplayName: "" };

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
      accessToken: form.accessToken.trim(),
      appSecret: form.appSecret.trim(),
      verifyToken: form.verifyToken.trim(),
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

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Channels"
        title="WhatsApp Business"
        sub="Connect your WhatsApp Business number so claimants can message you directly. Credentials are validated against Meta before anything is saved, and encrypted at rest."
      />

      {loading && (
        <GlassCard><div className="flex items-center justify-center gap-2 py-10 text-[13px] text-muted"><Loader2 className="h-4 w-4 animate-spin" /> Loading connection status…</div></GlassCard>
      )}

      {!loading && loadError && (
        <GlassCard><div className="py-6 text-center text-[13px] text-danger">{loadError}</div></GlassCard>
      )}

      {!loading && !loadError && status && (
        <div className="space-y-4">
          <GlassCard>
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="flex items-start gap-3">
                <div className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl ${status.connected ? "bg-success/10 text-success" : "bg-line/60 text-muted"}`}>
                  <MessageCircle className="h-5 w-5" />
                </div>
                <div>
                  <div className="flex items-center gap-2">
                    <div className="text-[14px] font-semibold text-ink">{status.connected ? status.businessDisplayName || "WhatsApp Business" : "Not connected"}</div>
                    {status.connected ? <Badge tone={status.lastValidationStatus === "Success" ? "success" : "warning"}>{status.lastValidationStatus === "Success" ? "Verified" : "Needs attention"}</Badge> : null}
                  </div>
                  {status.connected ? (
                    <div className="mt-1 text-[12px] text-muted">
                      Phone number ID {status.phoneNumberId} · configured by {status.configuredByUserId}
                      {status.configuredAt ? ` on ${new Date(status.configuredAt).toLocaleDateString()}` : ""}
                    </div>
                  ) : (
                    <div className="mt-1 text-[12px] text-muted">No WhatsApp Business number is connected for this workspace yet.</div>
                  )}
                </div>
              </div>
              {status.connected && (
                <div className="flex gap-2">
                  <button onClick={onTest} disabled={testing} className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-2 text-[12px] font-medium text-ink focus-ring disabled:opacity-50">
                    {testing ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <RefreshCcw className="h-3.5 w-3.5" />} Test connection
                  </button>
                  <button onClick={() => setDisconnectDialogOpen(true)} className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-danger/30 bg-white px-3 py-2 text-[12px] font-medium text-danger focus-ring">
                    <Unplug className="h-3.5 w-3.5" /> Disconnect
                  </button>
                </div>
              )}
            </div>

            {testResult && (
              <div className={`mt-4 flex items-start gap-2 rounded-xl border p-3 text-[12px] ${testResult.success ? "border-success/30 bg-success/5 text-success" : "border-danger/25 bg-danger/7 text-danger"}`}>
                {testResult.success ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" /> : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />}
                <span>{testResult.success ? "Meta confirmed this connection is working." : testResult.message}</span>
              </div>
            )}

            {status.connected && status.lastValidationStatus === "Failed" && status.lastValidationError && (
              <div className="mt-4 flex items-start gap-2 rounded-xl border border-danger/25 bg-danger/7 p-3 text-[12px] text-danger">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /> {status.lastValidationError}
              </div>
            )}

            {status.connected && status.requiresChannelProviderDeploymentChange && (
              <div className="mt-4 flex items-start gap-2 rounded-xl border border-warning/30 bg-warning/8 p-3 text-[12px] text-ink-soft">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-warning" />
                <span>Credentials are saved and verified, but this environment isn&apos;t yet routing WhatsApp traffic through them. Ask whoever manages your deployment to set <code className="rounded bg-mist px-1 py-0.5">INSURANCEINTELLIGENCE_CHANNEL_PROVIDER=WhatsAppCloudApi</code> to go live — a one-time deployment setting, not something this screen can change.</span>
              </div>
            )}
          </GlassCard>

          {!status.connected && (
            <GlassCard>
              <div className="flex items-center gap-2 mb-1">
                <PlugZap className="h-4 w-4 text-info" />
                <div className="text-[14px] font-semibold text-ink">Connect your WhatsApp Business number</div>
              </div>
              <p className="mb-4 text-[12px] leading-5 text-muted">
                You&apos;ll need a Meta Business app with the WhatsApp product added. Each field below says exactly where to find it.
              </p>

              <div className="space-y-4">
                {FIELD_GUIDE.map((field) => (
                  <label key={field.key} className="block text-[13px] font-medium text-ink">
                    {field.label}
                    <input
                      type={field.secret ? "password" : "text"}
                      value={form[field.key]}
                      onChange={(event) => setForm((current) => ({ ...current, [field.key]: event.target.value }))}
                      placeholder={field.placeholder}
                      autoComplete="off"
                      className="mt-1.5 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-[13px] text-ink placeholder:text-muted focus-ring"
                    />
                    <span className="mt-1 block text-[11px] font-normal leading-4 text-muted">{field.help}</span>
                  </label>
                ))}
                <label className="block text-[13px] font-medium text-ink">
                  Display name <span className="font-normal text-muted">(optional — we&apos;ll use Meta&apos;s verified name if you leave this blank)</span>
                  <input
                    value={form.businessDisplayName}
                    onChange={(event) => setForm((current) => ({ ...current, businessDisplayName: event.target.value }))}
                    placeholder="e.g. Acme Insurance"
                    className="mt-1.5 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-[13px] text-ink placeholder:text-muted focus-ring"
                  />
                </label>
              </div>

              {formError && (
                <div className="mt-4 flex items-start gap-2 rounded-xl border border-danger/25 bg-danger/7 p-3 text-[12px] text-danger">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /> {formError}
                </div>
              )}

              <button
                onClick={() => setConnectDialogOpen(true)}
                disabled={!canSubmitConnect}
                className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-md bg-primary px-5 py-2.5 text-[13px] font-semibold text-white shadow-l2 hover:bg-primary/90 focus-ring disabled:cursor-not-allowed disabled:opacity-50"
              >
                <ShieldCheck className="h-4 w-4" /> Validate and connect
              </button>
            </GlassCard>
          )}
        </div>
      )}

      <GovernedActionDialog
        open={connectDialogOpen}
        title="Connect WhatsApp Business"
        target={form.phoneNumberId || "New WhatsApp connection"}
        impact="IIP will call Meta's Graph API to confirm these credentials work, then encrypt and store them. This does not send any message to customers."
        auditNotice="Connecting a messaging channel is a governed action — record why this number is being connected now."
        confirmLabel="Validate and save"
        onClose={() => setConnectDialogOpen(false)}
        onConfirm={onConnect}
        evidence={
          <div className="rounded-xl border border-line bg-mist/60 p-3 text-[12px] text-ink-soft">
            <div>Phone number ID: <span className="font-medium text-ink">{form.phoneNumberId || "—"}</span></div>
            {form.businessDisplayName && <div>Display name: <span className="font-medium text-ink">{form.businessDisplayName}</span></div>}
          </div>
        }
      />

      <GovernedActionDialog
        open={disconnectDialogOpen}
        title="Disconnect WhatsApp Business"
        target={status?.businessDisplayName || status?.phoneNumberId || "This WhatsApp connection"}
        impact="Inbound and outbound WhatsApp messages will stop working immediately for this workspace until a connection is configured again."
        auditNotice="Disconnecting a live messaging channel is a governed action — record why."
        confirmLabel="Disconnect"
        tone="danger"
        onClose={() => setDisconnectDialogOpen(false)}
        onConfirm={onDisconnect}
      />

      <div className="mt-6 flex items-center gap-1.5 text-[11px] text-muted">
        <ExternalLink className="h-3 w-3" /> Meta Business Settings is where phone numbers, tokens, and app secrets are managed on Meta&apos;s side.
      </div>
    </>
  );
}
