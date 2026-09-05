"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { AlertTriangle, Inbox, Link2, Loader2, Mail, MailPlus, PlayCircle, RefreshCcw } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { useUI } from "@/context/UIStateContext";
import {
  type CreateMailboxInput,
  type EmailIntegrationHealth,
  type InboundEmailMessage,
  type MailboxConnection,
  createMailbox,
  getEmailHealth,
  listMailboxes,
  listMessages,
  processMessage,
  runTestIngestion,
} from "@/lib/emailIntegration";

// EmailProcessingStatus has no string-enum converter server-side, so it comes back as its raw
// ordinal (0-3) — see InboundEmailMessage.processingStatus in lib/emailIntegration.ts.
const STATUS_TONE: Record<number, "neutral" | "info" | "success" | "warning"> = {
  0: "neutral",
  1: "info",
  2: "success",
  3: "warning",
};

const STATUS_LABEL: Record<number, string> = {
  0: "Received — not yet processed",
  1: "Linked to existing claim",
  2: "Claim created",
  3: "Processing failed",
};

type MailboxFormState = { displayName: string; mailboxAddress: string; productLine: string; ingestionEnabled: boolean };
const EMPTY_MAILBOX_FORM: MailboxFormState = { displayName: "", mailboxAddress: "", productLine: "", ingestionEnabled: true };

export default function EmailInbox() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [health, setHealth] = useState<EmailIntegrationHealth | null>(null);
  const [mailboxes, setMailboxes] = useState<MailboxConnection[]>([]);
  const [messages, setMessages] = useState<InboundEmailMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [mailboxForm, setMailboxForm] = useState<MailboxFormState>(EMPTY_MAILBOX_FORM);
  const [mailboxDialogOpen, setMailboxDialogOpen] = useState(false);
  const [mailboxFormError, setMailboxFormError] = useState<string | null>(null);
  const [runningTestIngestion, setRunningTestIngestion] = useState(false);
  const [processingMessage, setProcessingMessage] = useState<InboundEmailMessage | null>(null);

  async function load() {
    setLoading(true);
    setLoadError(null);
    const [healthResult, mailboxResult, messageResult] = await Promise.all([
      getEmailHealth(role),
      listMailboxes(role),
      listMessages(role),
    ]);
    setLoading(false);
    if (!healthResult.ok) {
      setLoadError(healthResult.error);
      return;
    }
    setHealth(healthResult.value);
    setMailboxes(mailboxResult.ok ? mailboxResult.value : []);
    setMessages(messageResult.ok ? [...messageResult.value].sort((a, b) => Date.parse(b.receivedAt) - Date.parse(a.receivedAt)) : []);
  }

  useEffect(() => { void load(); }, [role]);

  const canRunTestIngestion = health?.allowedActions.some((action) => action.action === "runTestIngestion") ?? false;
  const canSubmitMailbox = mailboxForm.displayName.trim().length > 0 && mailboxForm.mailboxAddress.trim().length > 0;

  async function onCreateMailbox(reason: string) {
    setMailboxFormError(null);
    const input: CreateMailboxInput = {
      displayName: mailboxForm.displayName.trim(),
      mailboxAddress: mailboxForm.mailboxAddress.trim(),
      ingestionEnabled: mailboxForm.ingestionEnabled,
      defaultChannel: "Email",
      productLine: mailboxForm.productLine.trim() || undefined,
    };
    const result = await createMailbox(role, input, reason);
    if (!result.ok) {
      setMailboxFormError(result.error);
      throw new Error(result.error);
    }
    setMailboxForm(EMPTY_MAILBOX_FORM);
    setMailboxDialogOpen(false);
    toast.success("Mailbox added", { description: `${result.value.mailboxAddress} is now configured for this workspace.` });
    void load();
  }

  async function onRunTestIngestion() {
    setRunningTestIngestion(true);
    const result = await runTestIngestion(role, "Local test ingestion run from the Email Mailboxes screen.");
    setRunningTestIngestion(false);
    if (!result.ok) {
      toast.error(result.error);
      return;
    }
    toast.success("Test message ingested", { description: "A synthetic inbound email was added below — process it to see claim intake end to end." });
    void load();
  }

  async function onProcessMessage(reason: string) {
    if (!processingMessage) return;
    const result = await processMessage(role, processingMessage.messageId, reason);
    if (!result.ok) {
      throw new Error(result.error);
    }
    setProcessingMessage(null);
    toast.success(
      result.value.matchReason === "already_processed" ? "Already processed" : "Message processed",
      { description: `Matched via "${result.value.matchReason}". Case ${result.value.caseId.slice(0, 8)} is ready for review.` }
    );
    void load();
  }

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Channels"
        title="Email Mailboxes"
        sub="Configure inbound claim mailboxes and process received email into real claims. Sender identity is verified against customer records before a claim is created."
      />

      {loading ? (
        <GlassCard><div className="flex items-center justify-center gap-2 py-10 text-[13px] text-muted"><Loader2 className="h-4 w-4 animate-spin" /> Loading email integration status…</div></GlassCard>
      ) : loadError ? (
        <GlassCard><div className="py-6 text-center text-[13px] text-danger">{loadError}</div></GlassCard>
      ) : (
        <div className="space-y-4">
          <GlassCard>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-3">
                <div className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl ${health?.overallStatus === "healthy" ? "bg-success/10 text-success" : "bg-warning/10 text-warning"}`}>
                  <Mail className="h-5 w-5" />
                </div>
                <div>
                  <div className="text-[14px] font-semibold text-ink">{health?.displayName ?? "Microsoft Graph mail"}</div>
                  <div className="mt-1 text-[12px] text-muted">
                    {health?.providers.map((provider) => `${provider.name}: ${provider.status}`).join(" · ") || "No providers reported."}
                  </div>
                </div>
              </div>
              {canRunTestIngestion ? (
                <button
                  onClick={onRunTestIngestion}
                  disabled={runningTestIngestion}
                  className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-2 text-[12px] font-medium text-ink focus-ring disabled:opacity-50"
                >
                  {runningTestIngestion ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <PlayCircle className="h-3.5 w-3.5" />} Run test ingestion
                </button>
              ) : null}
              <button onClick={() => void load()} className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-2 text-[12px] font-medium text-ink focus-ring">
                <RefreshCcw className="h-3.5 w-3.5" /> Refresh
              </button>
            </div>
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-3">
              <div className="text-[14px] font-semibold text-ink">Configured mailboxes</div>
              <button
                onClick={() => setMailboxDialogOpen(true)}
                className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-2 text-[12px] font-medium text-ink focus-ring"
              >
                <MailPlus className="h-3.5 w-3.5" /> Add mailbox
              </button>
            </div>
            {mailboxes.length === 0 ? (
              <div className="rounded-xl border border-line bg-white/70 p-4 text-[12.5px] text-muted">
                No mailbox is configured for this workspace yet. Add one to start receiving claim emails.
              </div>
            ) : (
              <div className="grid gap-2 sm:grid-cols-2">
                {mailboxes.map((mailbox) => (
                  <div key={mailbox.mailboxConnectionId} className="rounded-xl border border-line bg-white/80 p-3">
                    <div className="text-[13px] font-medium text-ink">{mailbox.displayName}</div>
                    <div className="text-[11.5px] text-muted">{mailbox.mailboxAddress}</div>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                      <Badge tone={mailbox.ingestionEnabled ? "success" : "neutral"}>{mailbox.ingestionEnabled ? "Ingestion on" : "Ingestion off"}</Badge>
                      {mailbox.productLine ? <Badge tone="info">{mailbox.productLine}</Badge> : null}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center gap-2">
              <Inbox className="h-4 w-4 text-info" />
              <div className="text-[14px] font-semibold text-ink">Inbound messages</div>
            </div>
            {messages.length === 0 ? (
              <div className="rounded-xl border border-line bg-white/70 p-4 text-[12.5px] text-muted">
                No inbound email has been received for this workspace yet.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-[12.5px]">
                  <thead>
                    <tr className="border-b border-line text-left text-[10.5px] uppercase tracking-[0.12em] text-muted">
                      <th className="px-2 py-2">Sender</th>
                      <th className="px-2 py-2">Subject</th>
                      <th className="px-2 py-2">Received</th>
                      <th className="px-2 py-2">Status</th>
                      <th className="px-2 py-2">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {messages.map((message) => (
                      <tr key={message.messageId} className="border-b border-line/60 align-top">
                        <td className="px-2 py-2 text-ink">{message.sender}</td>
                        <td className="max-w-64 truncate px-2 py-2 text-ink-soft">{message.subject}</td>
                        <td className="whitespace-nowrap px-2 py-2 text-muted">{new Date(message.receivedAt).toLocaleString()}</td>
                        <td className="px-2 py-2"><Badge tone={STATUS_TONE[message.processingStatus] ?? "neutral"}>{STATUS_LABEL[message.processingStatus] ?? message.processingStatus}</Badge></td>
                        <td className="px-2 py-2">
                          {message.linkedCaseId ? (
                            <Link to={`/app/claims/${message.linkedCaseId}`} className="inline-flex items-center gap-1 text-info hover:underline">
                              <Link2 className="h-3.5 w-3.5" /> Open claim
                            </Link>
                          ) : (
                            <button
                              onClick={() => setProcessingMessage(message)}
                              className="inline-flex items-center gap-1 rounded-lg border border-line bg-white px-2.5 py-1.5 text-ink focus-ring"
                            >
                              Process
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </GlassCard>
        </div>
      )}

      <GovernedActionDialog
        open={mailboxDialogOpen}
        title="Add a claim mailbox"
        target={mailboxForm.mailboxAddress || "New mailbox"}
        impact="Email sent to this address will be pulled in for claim intake once ingestion is enabled."
        auditNotice="Configuring a mailbox is a governed action — record why this mailbox is being added now."
        confirmLabel="Add mailbox"
        onClose={() => setMailboxDialogOpen(false)}
        onConfirm={onCreateMailbox}
        evidence={
          <div className="space-y-3">
            <label className="block text-[13px] font-medium text-ink">
              Display name
              <input
                value={mailboxForm.displayName}
                onChange={(event) => setMailboxForm((current) => ({ ...current, displayName: event.target.value }))}
                placeholder="e.g. Claims Inbox"
                className="mt-1.5 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-[13px] text-ink placeholder:text-muted focus-ring"
              />
            </label>
            <label className="block text-[13px] font-medium text-ink">
              Mailbox address
              <input
                value={mailboxForm.mailboxAddress}
                onChange={(event) => setMailboxForm((current) => ({ ...current, mailboxAddress: event.target.value }))}
                placeholder="claims@yourcompany.com"
                className="mt-1.5 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-[13px] text-ink placeholder:text-muted focus-ring"
              />
            </label>
            <label className="block text-[13px] font-medium text-ink">
              Product line <span className="font-normal text-muted">(optional)</span>
              <input
                value={mailboxForm.productLine}
                onChange={(event) => setMailboxForm((current) => ({ ...current, productLine: event.target.value }))}
                placeholder="e.g. Motor"
                className="mt-1.5 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-[13px] text-ink placeholder:text-muted focus-ring"
              />
            </label>
            <label className="flex items-center gap-2 text-[13px] font-medium text-ink">
              <input
                type="checkbox"
                checked={mailboxForm.ingestionEnabled}
                onChange={(event) => setMailboxForm((current) => ({ ...current, ingestionEnabled: event.target.checked }))}
                className="h-4 w-4 rounded border-line"
              />
              Enable ingestion immediately
            </label>
            {mailboxFormError ? (
              <div className="flex items-start gap-2 rounded-xl border border-danger/25 bg-danger/7 p-3 text-[12px] text-danger">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /> {mailboxFormError}
              </div>
            ) : null}
          </div>
        }
      />

      <GovernedActionDialog
        open={processingMessage !== null}
        title="Process inbound email"
        target={processingMessage ? `${processingMessage.subject} — ${processingMessage.sender}` : "Inbound email"}
        impact="IIP will verify the sender against customer and policy records, then link this email to a matching claim or create a new one. An unidentified sender's claim still gets created, but flagged as needing confirmation before its SLA starts."
        auditNotice="Processing an inbound email into a claim is a governed action — record why."
        confirmLabel="Process message"
        onClose={() => setProcessingMessage(null)}
        onConfirm={onProcessMessage}
      />
    </>
  );
}
