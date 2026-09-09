"use client";

import { useEffect, useId, useState } from "react";
import { toast } from "sonner";
import { AlertTriangle, Inbox, Link2, Loader2, Mail, MailPlus, PlayCircle, RefreshCcw } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Field as UIField } from "@/components/ui/Input";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
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
  const displayNameId = useId();
  const mailboxAddressId = useId();
  const productLineId = useId();

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
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : loadError ? (
        <GlassCard><ErrorStateSurface title="Email integration status could not be loaded" message={loadError} /></GlassCard>
      ) : (
        <div className="space-y-4">
          <GlassCard>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-3">
                <div className={`grid h-11 w-11 shrink-0 place-items-center rounded-dls-chip ${health?.overallStatus === "healthy" ? "bg-dls-success-tint text-dls-success-text" : "bg-dls-warning-tint text-dls-warning-text"}`}>
                  <Mail className="h-5 w-5" aria-hidden="true" />
                </div>
                <div>
                  <div className="text-[14px] font-bold text-dls-text-primary">{health?.displayName ?? "Microsoft Graph mail"}</div>
                  <div className="mt-1 text-[12px] text-dls-text-secondary">
                    {health?.providers.map((provider) => `${provider.name}: ${provider.status}`).join(" · ") || "No providers reported."}
                  </div>
                </div>
              </div>
              {canRunTestIngestion ? (
                <Button
                  variant="secondary"
                  onClick={onRunTestIngestion}
                  disabled={runningTestIngestion}
                  icon={runningTestIngestion ? <Loader2 className="animate-spin" aria-hidden="true" /> : <PlayCircle aria-hidden="true" />}
                >
                  Run test ingestion
                </Button>
              ) : null}
              <Button variant="secondary" onClick={() => void load()} icon={<RefreshCcw aria-hidden="true" />}>
                Refresh
              </Button>
            </div>
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center justify-between gap-3">
              <div className="text-[14px] font-bold text-dls-text-primary">Configured mailboxes</div>
              <Button variant="secondary" onClick={() => setMailboxDialogOpen(true)} icon={<MailPlus aria-hidden="true" />}>
                Add mailbox
              </Button>
            </div>
            {mailboxes.length === 0 ? (
              <div className="rounded-dls-control border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-secondary">
                No mailbox is configured for this workspace yet. Add one to start receiving claim emails.
              </div>
            ) : (
              <div className="grid gap-2 sm:grid-cols-2">
                {mailboxes.map((mailbox) => (
                  <div key={mailbox.mailboxConnectionId} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
                    <div className="text-[13px] font-semibold text-dls-text-primary">{mailbox.displayName}</div>
                    <div className="text-[11.5px] text-dls-text-muted">{mailbox.mailboxAddress}</div>
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
              <Inbox className="h-4 w-4 text-dls-info-text" aria-hidden="true" />
              <div className="text-[14px] font-bold text-dls-text-primary">Inbound messages</div>
            </div>
            {messages.length === 0 ? (
              <div className="rounded-dls-control border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-secondary">
                No inbound email has been received for this workspace yet.
              </div>
            ) : (
              <Table label="Inbound email messages">
                <TableHead>
                  <TableRow>
                    <TableHeaderCell>Sender</TableHeaderCell>
                    <TableHeaderCell>Subject</TableHeaderCell>
                    <TableHeaderCell>Received</TableHeaderCell>
                    <TableHeaderCell>Status</TableHeaderCell>
                    <TableHeaderCell>Action</TableHeaderCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {messages.map((message) => (
                    <TableRow key={message.messageId} className="align-top">
                      <TableCell className="text-dls-text-primary">{message.sender}</TableCell>
                      <TableCell className="max-w-64 truncate text-dls-text-secondary">{message.subject}</TableCell>
                      <TableCell className="whitespace-nowrap text-dls-text-muted">{new Date(message.receivedAt).toLocaleString()}</TableCell>
                      <TableCell><Badge tone={STATUS_TONE[message.processingStatus] ?? "neutral"}>{STATUS_LABEL[message.processingStatus] ?? message.processingStatus}</Badge></TableCell>
                      <TableCell>
                        {message.linkedCaseId ? (
                          <Link to={`/app/claims/${message.linkedCaseId}`} className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-control font-semibold text-dls-primary hover:underline">
                            <Link2 className="h-3.5 w-3.5" aria-hidden="true" /> Open claim
                          </Link>
                        ) : (
                          <button
                            type="button"
                            onClick={() => setProcessingMessage(message)}
                            className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-control border border-dls-border-input bg-dls-surface px-2.5 py-1.5 text-dls-text-primary hover:bg-dls-bg-page"
                          >
                            Process
                          </button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
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
            <UIField label="Display name" htmlFor={displayNameId}>
              <Input
                id={displayNameId}
                value={mailboxForm.displayName}
                onChange={(event) => setMailboxForm((current) => ({ ...current, displayName: event.target.value }))}
                placeholder="e.g. Claims Inbox"
              />
            </UIField>
            <UIField label="Mailbox address" htmlFor={mailboxAddressId}>
              <Input
                id={mailboxAddressId}
                value={mailboxForm.mailboxAddress}
                onChange={(event) => setMailboxForm((current) => ({ ...current, mailboxAddress: event.target.value }))}
                placeholder="claims@yourcompany.com"
              />
            </UIField>
            <UIField label="Product line (optional)" htmlFor={productLineId}>
              <Input
                id={productLineId}
                value={mailboxForm.productLine}
                onChange={(event) => setMailboxForm((current) => ({ ...current, productLine: event.target.value }))}
                placeholder="e.g. Motor"
              />
            </UIField>
            <label className="flex items-center gap-2 text-[13px] font-semibold text-dls-text-primary">
              <input
                type="checkbox"
                checked={mailboxForm.ingestionEnabled}
                onChange={(event) => setMailboxForm((current) => ({ ...current, ingestionEnabled: event.target.checked }))}
                className="h-4 w-4 rounded border-dls-border-input"
              />
              Enable ingestion immediately
            </label>
            {mailboxFormError ? (
              <div className="flex items-start gap-2 rounded-dls-control bg-dls-danger-tint p-3 text-[12px] text-dls-danger-text">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> {mailboxFormError}
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
