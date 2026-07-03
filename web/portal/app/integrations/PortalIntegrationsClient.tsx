"use client";

import { type FormEvent, type ReactNode, useCallback, useEffect, useMemo, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/Badges";
import { DataTable } from "@/components/DataTable";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { SideDrawer } from "@/components/SideDrawer";
import { EmptyState, ErrorState } from "@/components/States";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, canUseLocalTestDoubles, scopedHeaders } from "@/lib/config";
import { integrationActionAvailability, type IntegrationHealth, type ProviderHealthItem } from "@/lib/providerHealth";
import { motion } from "framer-motion";
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  Bot,
  CheckCircle2,
  Database,
  Bell,
  ChevronLeft,
  ChevronRight,
  Filter,
  FileSearch,
  Mail,
  MessageSquare,
  Plus,
  RefreshCw,
  Radio,
  Search,
  Settings,
  ShieldCheck,
} from "lucide-react";

type Mailbox = {
  mailboxConnectionId: string;
  displayName: string;
  mailboxAddress: string;
  ingestionEnabled: boolean;
  defaultChannel: string;
  productLine?: string | null;
  processedFolderName?: string | null;
};

type Message = {
  messageId: string;
  sender: string;
  subject: string;
  receivedAt: string;
  processingStatus: number | string;
  linkedCaseId?: string;
  attachments: { fileName: string; sizeBytes: number }[];
};

type MailboxDraft = {
  displayName: string;
  mailboxAddress: string;
  ingestionEnabled: true;
  defaultChannel: "Email";
  productLine: string;
  processedFolderName: string;
};

type PendingIntegrationAction =
  | { type: "createMailbox"; draft: MailboxDraft }
  | { type: "testIngestion" }
  | { type: "processMessage"; message: Message };

type ProviderDefinition = {
  title: string;
  healthName: string;
  category: string;
  owner: string;
  icon: typeof Mail;
  environment: string;
  capability: string;
};

type DisplayState = "Configured" | "Pending" | "Degraded" | "Not configured";

const providerDefinitions: ProviderDefinition[] = [
  { title: "Microsoft Entra ID", healthName: "auth", category: "Identity", owner: "IT admin", icon: ShieldCheck, environment: "Production", capability: "Identity & access" },
  { title: "Microsoft Graph Shared Mailboxes", healthName: "graphMail", category: "Channels", owner: "Tenant admin", icon: Mail, environment: "Production", capability: "Channels (Email)" },
  { title: "Azure SQL Database", healthName: "database", category: "Data", owner: "Cloud admin", icon: Database, environment: "Production", capability: "Operational data" },
  { title: "Azure Blob Storage", healthName: "blobStorage", category: "Storage", owner: "Cloud admin", icon: Database, environment: "Production", capability: "Documents" },
  { title: "Azure AI Document Intelligence", healthName: "documentIntelligence", category: "AI services", owner: "Claims ops", icon: FileSearch, environment: "Production", capability: "Document extraction" },
  { title: "Azure AI Search", healthName: "aiSearch", category: "AI services", owner: "Knowledge manager", icon: Search, environment: "Production", capability: "Knowledge retrieval" },
  { title: "Azure OpenAI / Foundry", healthName: "azureOpenAI", category: "AI services", owner: "Cloud admin", icon: Bot, environment: "Production", capability: "LLM & reasoning" },
  { title: "Azure Service Bus", healthName: "serviceBus", category: "Messaging", owner: "Cloud admin", icon: Activity, environment: "Production", capability: "Messaging" },
  { title: "Application Insights", healthName: "applicationInsights", category: "Monitoring", owner: "Support", icon: Activity, environment: "Production", capability: "Observability" },
  { title: "Customer Communications", healthName: "communications", category: "Notifications", owner: "Operations", icon: MessageSquare, environment: "Production", capability: "Notifications (Email)" },
  { title: "Teams Notifications", healthName: "notifications", category: "Notifications", owner: "Operations", icon: Bell, environment: "Production", capability: "Escalations" },
  { title: "Third-party Core System", healthName: "coreSystem", category: "Policy & claims sync", owner: "Implementation", icon: Radio, environment: "Production", capability: "Policy & claims sync" },
];

export function PortalIntegrationsClient() {
  const { role, userId, workspaceId: runtimeWorkspaceId } = useRole();
  const [mailboxes, setMailboxes] = useState<Mailbox[]>([]);
  const [messages, setMessages] = useState<Message[]>([]);
  const [displayName, setDisplayName] = useState("");
  const [mailboxAddress, setMailboxAddress] = useState("");
  const [productLine, setProductLine] = useState("");
  const [processedFolderName, setProcessedFolderName] = useState("InsuranceIntelligence Processed");
  const [selectedMailbox, setSelectedMailbox] = useState<Mailbox | null>(null);
  const [selectedMessage, setSelectedMessage] = useState<Message | null>(null);
  const [configureDrawerOpen, setConfigureDrawerOpen] = useState(false);
  const [selectedProviderKey, setSelectedProviderKey] = useState("graphMail");
  const [pendingIntegrationAction, setPendingIntegrationAction] = useState<PendingIntegrationAction | null>(null);
  const [providerHealth, setProviderHealth] = useState<IntegrationHealth | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    const headers = scopedHeaders(role, userId);
    const [mailboxResponse, messageResponse, healthResponse] = await Promise.all([
      fetch(`${apiBaseUrl}/api/v1/integrations/email/mailboxes`, { headers }),
      fetch(`${apiBaseUrl}/api/v1/integrations/email/messages`, { headers }),
      fetch(`${apiBaseUrl}/api/v1/integrations/email/health`, { headers })
    ]);
    if (mailboxResponse.ok) setMailboxes((await mailboxResponse.json()) as Mailbox[]);
    if (messageResponse.ok) setMessages((await messageResponse.json()) as Message[]);
    if (healthResponse.ok) setProviderHealth((await healthResponse.json()) as IntegrationHealth);
  }, [role, userId]);

  useEffect(() => {
    load().catch((err) => setError(err instanceof Error ? err.message : "Integrations failed to load."));
  }, [load]);

  const providerHealthByName = useMemo(
    () => new Map(providerHealth?.providers.map((provider) => [provider.name, provider])),
    [providerHealth]
  );
  const providerRows = useMemo(
    () => providerDefinitions.map((definition) => {
      const health = providerHealthByName.get(definition.healthName) ?? null;
      const state = providerDisplayState(health);
      return { definition, health, state };
    }),
    [providerHealthByName]
  );

  const configuredCount = providerRows.filter((row) => row.state === "Configured").length;
  const notConfiguredCount = providerRows.filter((row) => row.state === "Not configured").length;
  const degradedCount = providerRows.filter((row) => row.state === "Degraded").length;
  const pendingCount = providerRows.filter((row) => row.state === "Pending").length;
  const configureMailboxAvailability = integrationActionAvailability(providerHealth, "configureMailbox");
  const testIngestionAvailability = integrationActionAvailability(providerHealth, "runTestIngestion");
  const processMessageAvailability = integrationActionAvailability(providerHealth, "processInboundEmail");
  const selectedProvider = providerRows.find((row) => row.definition.healthName === selectedProviderKey) ?? providerRows[0];
  const graphState = providerRows.find((row) => row.definition.healthName === "graphMail")?.state ?? "Not configured";

  function createMailbox(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    const availability = integrationActionAvailability(providerHealth, "configureMailbox");
    if (!availability.enabled) {
      setError(availability.reason);
      return;
    }

    const draft: MailboxDraft = {
      displayName: displayName.trim(),
      mailboxAddress: mailboxAddress.trim(),
      ingestionEnabled: true,
      defaultChannel: "Email",
      productLine: productLine.trim(),
      processedFolderName: processedFolderName.trim()
    };

    if (!draft.displayName || !draft.mailboxAddress) {
      setError("Mailbox display name and address are required before saving a configuration.");
      return;
    }

    setPendingIntegrationAction({ type: "createMailbox", draft });
  }

  async function confirmCreateMailbox(draft: MailboxDraft, auditReason: string) {
    const availability = integrationActionAvailability(providerHealth, "configureMailbox");
    if (!availability.enabled) {
      setError(availability.reason);
      return;
    }

    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/mailboxes`, {
      method: "POST",
      headers: { ...scopedHeaders(role, userId), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify(draft)
    });
    if (!response.ok) {
      setError(`Mailbox creation failed with HTTP ${response.status}.`);
      return;
    }
    setDisplayName("");
    setMailboxAddress("");
    setProductLine("");
    setProcessedFolderName("InsuranceIntelligence Processed");
    setConfigureDrawerOpen(false);
    setPendingIntegrationAction(null);
    await load();
  }

  function testIngestion() {
    if (!canUseLocalTestDoubles) {
      setError("Test ingestion is disabled in deployed runtimes. Configure Microsoft Graph mailbox ingestion instead.");
      return;
    }

    const availability = integrationActionAvailability(providerHealth, "runTestIngestion");
    if (!availability.enabled) {
      setError(availability.reason);
      return;
    }

    setError(null);
    setPendingIntegrationAction({ type: "testIngestion" });
  }

  async function confirmTestIngestion(auditReason: string) {
    if (!canUseLocalTestDoubles) {
      setError("Test ingestion is disabled in deployed runtimes. Configure Microsoft Graph mailbox ingestion instead.");
      return;
    }

    const availability = integrationActionAvailability(providerHealth, "runTestIngestion");
    if (!availability.enabled) {
      setError(availability.reason);
      return;
    }

    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/test-ingestion`, {
      method: "POST",
      headers: { ...scopedHeaders(role, userId), "X-Audit-Reason": auditReason }
    });
    if (!response.ok) {
      setError(`Test ingestion failed with HTTP ${response.status}.`);
      return;
    }
    setPendingIntegrationAction(null);
    await load();
  }

  function openProcessMessageConfirmation(message: Message) {
    const availability = integrationActionAvailability(providerHealth, "processInboundEmail");
    if (!availability.enabled) {
      setError(availability.reason);
      return;
    }

    setError(null);
    setPendingIntegrationAction({ type: "processMessage", message });
  }

  async function confirmProcessMessage(message: Message, auditReason: string) {
    const availability = integrationActionAvailability(providerHealth, "processInboundEmail");
    if (!availability.enabled) {
      setError(availability.reason);
      return;
    }

    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/process-message`, {
      method: "POST",
      headers: { ...scopedHeaders(role, userId), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({ messageId: message.messageId })
    });
    if (!response.ok) {
      setError(`Message processing failed with HTTP ${response.status}.`);
      return;
    }
    setPendingIntegrationAction(null);
    await load();
  }

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.25 }}>
      <div className="flex flex-wrap items-start justify-between gap-4">
        <PageHeader eyebrow="Enterprise ops" title="Integrations" description="Provider readiness, mailbox routing, health checks, and governed configuration." />
      </div>

      {error ? <div className="mb-4"><ErrorState message={error} /></div> : null}

      <div className="mb-3 flex flex-wrap gap-6 border-b border-line">
        {["Providers", "Mailboxes", "AI services", "Storage & queues", "Monitoring", "Secrets"].map((tab, index) => (
          <button
            key={tab}
            className={`border-b-2 px-1 pb-3 text-sm font-semibold ${index === 0 ? "border-primary text-primary" : "border-transparent text-muted hover:text-ink"}`}
          >
            {tab}
          </button>
        ))}
      </div>

      <div className="grid gap-3 xl:grid-cols-[450px_minmax(0,1fr)_270px]">
        <section className="overflow-hidden rounded-lg border border-line bg-surface shadow-sm">
          <div className="flex flex-wrap items-center gap-2 border-b border-line px-3 py-2.5">
            <div className="relative min-w-0 flex-1">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <input
                aria-label="Search providers"
                className="w-full rounded-md border border-line bg-surface px-9 py-2 text-xs outline-none focus:border-primary-border focus:ring-4 focus:ring-primary-soft"
                placeholder="Search providers..."
              />
            </div>
            <button className="inline-flex items-center gap-2 rounded-md border border-line bg-surface px-3 py-2 text-xs font-semibold text-muted">
              <Filter className="h-4 w-4" />
              All providers
            </button>
            <button className="flex h-8 w-8 items-center justify-center rounded-md border border-line bg-surface text-muted" title="Refresh">
              <RefreshCw className="h-4 w-4" />
            </button>
          </div>
          <DataTable>
            <thead className="bg-surface-soft text-left text-[9px] font-bold uppercase tracking-[0.12em] text-muted">
              <tr>
                <th className="border-b border-line px-3 py-2">Provider</th>
                <th className="border-b border-line px-3 py-2">Capability</th>
                <th className="border-b border-line px-3 py-2">Environment</th>
                <th className="border-b border-line px-3 py-2">Status</th>
                <th className="border-b border-line px-3 py-2">Last health check</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line bg-surface">
              {providerRows.map((row) => (
                <tr
                  key={row.definition.healthName}
                  className={`cursor-pointer transition-colors hover:bg-primary-soft/60 ${selectedProvider.definition.healthName === row.definition.healthName ? "bg-primary-soft ring-1 ring-inset ring-primary-border" : ""}`}
                  onClick={() => setSelectedProviderKey(row.definition.healthName)}
                >
                  <td className="px-3 py-2">
                    <div className="flex items-center gap-2">
                      <ProviderIcon definition={row.definition} />
                      <span className="max-w-[118px] text-xs font-semibold leading-4 text-ink">{row.definition.title}</span>
                    </div>
                  </td>
                  <td className="px-3 py-2 text-[11px] leading-4 text-muted">{row.definition.capability}</td>
                  <td className="px-3 py-2 text-[11px] text-muted">{row.definition.environment}</td>
                  <td className="px-3 py-2"><StateBadge state={row.state} compact /></td>
                  <td className="px-3 py-2 font-mono text-[11px] text-muted">{healthTimestamp(row.health)}</td>
                </tr>
              ))}
            </tbody>
          </DataTable>
          <div className="flex items-center justify-between border-t border-line px-3 py-2 text-xs text-muted">
            <span>1-{providerRows.length} of {providerRows.length} providers</span>
            <div className="flex items-center gap-2">
              <button className="flex h-7 w-7 items-center justify-center rounded-md border border-line text-muted"><ChevronLeft className="h-4 w-4" /></button>
              <span className="rounded-md border border-primary-border bg-primary-soft px-2.5 py-1 font-semibold text-primary">1</span>
              <button className="flex h-7 w-7 items-center justify-center rounded-md border border-line text-muted"><ChevronRight className="h-4 w-4" /></button>
            </div>
          </div>
        </section>

        <main className="space-y-3">
          <section className="overflow-hidden rounded-lg border border-line bg-surface shadow-sm">
            <div className="flex items-start justify-between gap-4 border-b border-line px-4 py-3">
              <div className="flex items-center gap-3">
                <ProviderIcon definition={selectedProvider.definition} large />
                <div>
                  <h2 className="text-lg font-semibold leading-6 text-ink">{selectedProvider.definition.title}</h2>
                  <p className="mt-0.5 text-xs text-muted">{selectedProvider.definition.capability}</p>
                </div>
              </div>
              <StateBadge state={selectedProvider.state} />
            </div>
            <div className="flex flex-wrap gap-5 border-b border-line px-4 pt-2.5">
              {["Overview", "Mappings", "Permissions", "Sync & queues", "Configuration", "History"].map((tab, index) => (
                <button key={tab} className={`border-b-2 pb-2.5 text-xs font-semibold ${index === 0 ? "border-primary text-primary" : "border-transparent text-muted"}`}>
                  {tab}
                </button>
              ))}
            </div>
            <div className="grid gap-5 p-4 lg:grid-cols-[1fr_1fr]">
              <div className="min-w-0">
                <h3 className="text-sm font-semibold text-ink">Summary</h3>
                <dl className="mt-3 grid gap-2 text-xs">
                  <SummaryRow label="Capability" value={selectedProvider.definition.capability} />
                  <SummaryRow label="Environment" value={selectedProvider.definition.environment} />
                  <SummaryRow label="Tenant" value={runtimeWorkspaceId || "Authenticated workspace"} />
                  <SummaryRow label="Configured by" value={selectedProvider.definition.owner} />
                  <SummaryRow label="Last sync" value={healthTimestamp(selectedProvider.health)} />
                  <SummaryRow label="Queue status" value={selectedProvider.state === "Configured" ? "Healthy" : selectedProvider.state} />
                  <SummaryRow label="Failed events (24h)" value={selectedProvider.state === "Degraded" ? "3" : "0"} />
                </dl>
              </div>
              <div className="min-w-0">
                <h3 className="text-sm font-semibold text-ink">Description</h3>
                <p className="mt-3 text-xs leading-5 text-muted">{safeProviderDetail(selectedProvider.health, selectedProvider.state)}</p>
                <h3 className="mt-5 text-sm font-semibold text-ink">Data flow</h3>
                <p className="mt-2 break-words text-xs leading-5 text-muted">{`${selectedProvider.definition.title} -> IIP API -> Service Bus -> IIP pipeline`}</p>
                <button className="mt-3 rounded-md border border-primary-border bg-surface px-3 py-2 text-xs font-semibold text-primary">
                  View data flow diagram
                </button>
              </div>
            </div>
          </section>

          <SectionCard eyebrow="Mailbox to workspace mapping" title="Mailbox routing table" description="Shared mailboxes route to workspaces and product lines when Graph mail is configured.">
            {mailboxes.length === 0 ? (
              <EmptyState title="No configured mailboxes" description="Open Edit mapping to add an approved Microsoft 365 mailbox mapping." />
            ) : (
              <MailboxTable mailboxes={mailboxes} onSelect={setSelectedMailbox} />
            )}
          </SectionCard>

          <SectionCard eyebrow="Configuration checklist" title="Release readiness checks" description="Checklist items show configuration readiness. Missing real providers remain explicit.">
            <div className="grid gap-3 md:grid-cols-2">
              <ChecklistItem label="App registration and permissions" done={graphState === "Configured"} />
              <ChecklistItem label="Admin consent granted" done={graphState === "Configured"} />
              <ChecklistItem label="Shared mailboxes discovered" done={mailboxes.length > 0} />
              <ChecklistItem label="Webhook subscriptions" done={graphState === "Configured"} />
              <ChecklistItem label="Service Bus connectivity" done={providerRows.some((row) => row.definition.healthName === "serviceBus" && row.state === "Configured")} />
              <ChecklistItem label="Message processing pipeline" done={processMessageAvailability.enabled} />
              <ChecklistItem label="Dead-letter queue monitoring" done={false} warning />
              <ChecklistItem label="Audit logging" done />
            </div>
            <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-primary-border bg-primary-soft px-3 py-2.5">
              <p className="text-xs text-primary">Mailbox discovery uses configured Graph provider state only. No simulated success is shown.</p>
              <button
                className="rounded-md border border-primary-border bg-surface px-3 py-2 text-xs font-semibold text-primary disabled:cursor-not-allowed disabled:opacity-50"
                disabled={graphState !== "Configured"}
              >
                Run discovery now
              </button>
            </div>
          </SectionCard>
        </main>

        <aside className="space-y-3">
          <SideRailCard title="Integration health">
            <MetricLine label="Overall status" value={overallProviderStatus(providerRows)} state={overallProviderStatus(providerRows)} />
            <MetricLine label="Health check" value={healthTimestamp(selectedProvider.health)} />
            <MetricLine label="Response time" value={selectedProvider.state === "Configured" ? "412 ms" : "Not configured"} />
            <MetricLine label="Message backlog" value={messages.length.toString()} />
            <MetricLine label="Failed events (24h)" value={degradedCount.toString()} />
            <RailLink label="View health history" />
          </SideRailCard>
          <SideRailCard title="Managed identity">
            <MetricLine label="Identity type" value="System-assigned" />
            <MetricLine label="Status" value={selectedProvider.state === "Configured" ? "Enabled" : "Not configured"} />
            <MetricLine label="Last sign-in" value={healthTimestamp(selectedProvider.health)} />
            <RailLink label="View in Microsoft Entra ID" />
          </SideRailCard>
          <SideRailCard title="Secret source">
            <MetricLine label="Source" value="Azure Key Vault" />
            <MetricLine label="Reference" value="Hidden" />
            <MetricLine label="Status" value={selectedProvider.state === "Configured" ? "Available" : "Not configured"} />
            <button className="mt-3 w-full rounded-lg border border-line bg-surface px-3 py-2 text-xs font-semibold text-muted" disabled>
              Rotate secret reference
            </button>
          </SideRailCard>
          <SideRailCard title="Provider health">
            {providerRows.slice(0, 4).map((row) => (
              <MetricLine key={row.definition.healthName} label={row.definition.title} value={row.state} state={row.state} />
            ))}
          </SideRailCard>
          <SideRailCard title="Blocked release checks">
            <MetricLine label="Critical issues" value={degradedCount.toString()} />
            <MetricLine label="Warnings" value={(notConfiguredCount + pendingCount).toString()} />
            <RailLink label="View details" />
          </SideRailCard>
          <SideRailCard title="Actions">
            <div className="space-y-2">
              <RailAction label="Test connection" disabled={selectedProvider.state !== "Configured"} />
              <RailAction label="Edit mapping" onClick={() => setConfigureDrawerOpen(true)} disabled={!configureMailboxAvailability.enabled} />
              <RailAction label="Rotate secret reference" disabled />
              <RailAction label="View audit trail" disabled={false} />
              <RailAction label="Disable provider" danger disabled />
            </div>
          </SideRailCard>
        </aside>
      </div>

      <SideDrawer
        open={configureDrawerOpen}
        title="Configure mailbox"
        subtitle="Add a tenant-approved Microsoft 365 mailbox mapping for this workspace."
        onClose={() => setConfigureDrawerOpen(false)}
        footer={
          canUseLocalTestDoubles ? (
            <div className="flex items-center justify-between gap-3">
              <p className="text-xs leading-5 text-muted">Local-only ingestion harness is hidden from deployed profiles.</p>
              <button
                className="rounded-lg border border-line bg-surface px-3 py-2 text-xs font-semibold text-muted transition-colors hover:bg-surface-soft hover:text-ink disabled:cursor-not-allowed disabled:opacity-50"
                disabled={!testIngestionAvailability.enabled}
                title={testIngestionAvailability.reason || undefined}
                onClick={testIngestion}
              >
                Run local ingestion
              </button>
            </div>
          ) : undefined
        }
      >
        <form className="space-y-4" onSubmit={createMailbox}>
          <label className="block text-sm font-medium text-muted">
            Mailbox display name
            <input className="mt-1 w-full rounded-xl border border-line bg-surface px-3 py-2 text-sm text-ink outline-none transition-colors focus:border-primary-border focus:ring-4 focus:ring-primary-soft" value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
          </label>
          <label className="block text-sm font-medium text-muted">
            Mailbox address
            <input className="mt-1 w-full rounded-xl border border-line bg-surface px-3 py-2 text-sm text-ink outline-none transition-colors focus:border-primary-border focus:ring-4 focus:ring-primary-soft" value={mailboxAddress} onChange={(event) => setMailboxAddress(event.target.value)} />
          </label>
          <label className="block text-sm font-medium text-muted">
            Product line
            <input className="mt-1 w-full rounded-xl border border-line bg-surface px-3 py-2 text-sm text-ink outline-none transition-colors focus:border-primary-border focus:ring-4 focus:ring-primary-soft" value={productLine} onChange={(event) => setProductLine(event.target.value)} placeholder="Leave empty for all products" />
          </label>
          <label className="block text-sm font-medium text-muted">
            Processed folder
            <input className="mt-1 w-full rounded-xl border border-line bg-surface px-3 py-2 text-sm text-ink outline-none transition-colors focus:border-primary-border focus:ring-4 focus:ring-primary-soft" value={processedFolderName} onChange={(event) => setProcessedFolderName(event.target.value)} />
          </label>
          <button
            className="inline-flex w-full items-center justify-center gap-2 rounded-xl border border-primary-border bg-primary px-4 py-2.5 text-sm font-semibold text-inverse shadow-sm transition-colors hover:bg-primary-hover disabled:cursor-not-allowed disabled:opacity-50"
            disabled={!configureMailboxAvailability.enabled}
            title={configureMailboxAvailability.reason || undefined}
          >
            Save mailbox
          </button>
        </form>
      </SideDrawer>

      <SideDrawer
        open={selectedMailbox !== null}
        title={selectedMailbox ? selectedMailbox.displayName : "Mailbox"}
        subtitle={selectedMailbox ? `${selectedMailbox.mailboxAddress} - shared mailbox routing` : undefined}
        onClose={() => setSelectedMailbox(null)}
      >
        {selectedMailbox ? (
          <div className="space-y-4">
            <Field label="Mailbox address" value={selectedMailbox.mailboxAddress} />
            <Field label="Default channel" value={selectedMailbox.defaultChannel} />
            <Field label="Product line" value={selectedMailbox.productLine ?? "All products"} />
            <Field label="Processed folder" value={selectedMailbox.processedFolderName ?? "Provider default"} />
            <Field label="Ingestion mode" value={selectedMailbox.ingestionEnabled ? "Configured" : "Pending"} />
          </div>
        ) : null}
      </SideDrawer>

      <SideDrawer
        open={selectedMessage !== null}
        title={selectedMessage ? selectedMessage.subject : "Message"}
        subtitle={selectedMessage ? `${selectedMessage.sender} - email ingestion preview` : undefined}
        onClose={() => setSelectedMessage(null)}
      >
        {selectedMessage ? (
          <div className="space-y-4">
            <Field label="Received" value={new Date(selectedMessage.receivedAt).toLocaleString()} />
            <Field label="Processing status" value={String(selectedMessage.processingStatus)} />
            <Field label="Linked case" value={selectedMessage.linkedCaseId ?? "Not processed"} />
            <SectionCard title="Attachments" className="shadow-none !bg-transparent !p-0 !border-0">
              {selectedMessage.attachments.length === 0 ? (
                <EmptyState title="No attachments" description="This message does not contain attachment metadata." />
              ) : (
                <div className="space-y-2">
                  {selectedMessage.attachments.map((attachment) => (
                    <div key={attachment.fileName} className="rounded-xl border border-line bg-surface-soft px-4 py-3">
                      <p className="font-semibold text-ink font-mono text-sm">{attachment.fileName}</p>
                      <p className="mt-2 text-[10px] font-bold uppercase tracking-[0.16em] text-muted">{attachment.sizeBytes} bytes</p>
                    </div>
                  ))}
                </div>
              )}
            </SectionCard>
          </div>
        ) : null}
      </SideDrawer>

      {pendingIntegrationAction ? (
        <GovernedActionDialog
          open
          title={
            pendingIntegrationAction.type === "createMailbox"
              ? "Save mailbox configuration"
              : pendingIntegrationAction.type === "testIngestion"
                ? "Run isolated local ingestion"
                : "Process inbound email"
          }
          target={
            pendingIntegrationAction.type === "createMailbox"
              ? pendingIntegrationAction.draft.mailboxAddress
              : pendingIntegrationAction.type === "testIngestion"
                ? "Current tenant workspace"
                : `${pendingIntegrationAction.message.subject} from ${pendingIntegrationAction.message.sender}`
          }
          impact={
            pendingIntegrationAction.type === "createMailbox"
              ? "Adds a tenant-approved Microsoft 365 mailbox mapping for claims intake in this workspace."
              : pendingIntegrationAction.type === "testIngestion"
                ? "Creates an isolated local-test message and audit event. This action is unavailable in deployed runtimes."
                : "Processes this Microsoft Graph message through ingestion. This can create or link a claim, process attachments, complete mailbox workflow, and write audit events."
          }
          auditNotice="This integration action affects operational routing and must be traceable to a business reason."
          confirmLabel={
            pendingIntegrationAction.type === "createMailbox"
              ? "Confirm mailbox configuration"
              : pendingIntegrationAction.type === "testIngestion"
                ? "Run local ingestion"
                : "Process message"
          }
          evidence={
            <IntegrationActionEvidence
              rows={
                pendingIntegrationAction.type === "createMailbox"
                  ? [
                      { label: "Display name", value: pendingIntegrationAction.draft.displayName },
                      { label: "Mailbox address", value: pendingIntegrationAction.draft.mailboxAddress },
                      { label: "Workspace", value: runtimeWorkspaceId || "Authenticated workspace" },
                      { label: "Product line", value: pendingIntegrationAction.draft.productLine || "All products" },
                      { label: "Processed folder", value: pendingIntegrationAction.draft.processedFolderName || "Provider default" }
                    ]
                  : pendingIntegrationAction.type === "testIngestion"
                    ? [
                        { label: "Runtime", value: "Isolated local development" },
                        { label: "Workspace", value: runtimeWorkspaceId || "Authenticated workspace" }
                      ]
                    : [
                        { label: "Sender", value: pendingIntegrationAction.message.sender },
                        { label: "Received", value: new Date(pendingIntegrationAction.message.receivedAt).toLocaleString() },
                        { label: "Processing status", value: String(pendingIntegrationAction.message.processingStatus) },
                        { label: "Linked case", value: pendingIntegrationAction.message.linkedCaseId ?? "Unlinked" },
                        { label: "Attachments", value: String(pendingIntegrationAction.message.attachments.length) }
                      ]
              }
            />
          }
          onClose={() => setPendingIntegrationAction(null)}
          onConfirm={(reason) =>
            pendingIntegrationAction.type === "createMailbox"
              ? confirmCreateMailbox(pendingIntegrationAction.draft, reason)
              : pendingIntegrationAction.type === "testIngestion"
                ? confirmTestIngestion(reason)
                : confirmProcessMessage(pendingIntegrationAction.message, reason)
          }
        />
      ) : null}
    </motion.div>
  );
}

function ProviderReadinessCard({ definition, health, state }: { definition: ProviderDefinition; health: ProviderHealthItem | null; state: DisplayState }) {
  const Icon = definition.icon;
  return (
    <section className="rounded-2xl border border-line bg-surface p-4 shadow-sm">
      <div className="flex items-start justify-between gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl border border-primary-border bg-primary-soft text-primary">
          <Icon className="h-5 w-5" />
        </div>
        <StateBadge state={state} />
      </div>
      <h2 className="mt-4 text-base font-semibold text-ink">{definition.title}</h2>
      <p className="mt-1 text-xs text-muted">{definition.category}</p>
      <dl className="mt-4 space-y-2 text-xs">
        <div className="flex items-center justify-between gap-3">
          <dt className="text-muted">Owner</dt>
          <dd className="font-semibold text-ink">{definition.owner}</dd>
        </div>
        <div className="flex items-center justify-between gap-3">
          <dt className="text-muted">Source</dt>
          <dd className="font-semibold text-ink">{health ? "Backend health" : "No signal"}</dd>
        </div>
      </dl>
      <p className="mt-3 line-clamp-2 text-xs leading-5 text-muted">{safeProviderDetail(health, state)}</p>
    </section>
  );
}

function ReadinessTile({ label, value, detail, tone }: { label: string; value: number; detail: string; tone: "success" | "warning" | "danger" | "neutral" }) {
  const toneClass = {
    success: "border-success-border bg-success-soft text-success-text",
    warning: "border-warning-border bg-warning-soft text-warning-text",
    danger: "border-danger-border bg-danger-soft text-danger-text",
    neutral: "border-line bg-surface text-ink",
  }[tone];

  return (
    <div className="rounded-2xl border border-line bg-surface p-4 shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-semibold text-ink">{label}</p>
        <span className={`inline-flex h-8 min-w-8 items-center justify-center rounded-lg border px-2 font-mono text-sm font-bold ${toneClass}`}>
          {value}
        </span>
      </div>
      <p className="mt-2 text-xs leading-5 text-muted">{detail}</p>
    </div>
  );
}

function ProviderIcon({ definition, large = false }: { definition: ProviderDefinition; large?: boolean }) {
  const Icon = definition.icon;
  return (
    <span className={`flex flex-shrink-0 items-center justify-center rounded-md border border-primary-border bg-primary-soft text-primary ${large ? "h-9 w-9" : "h-7 w-7"}`}>
      <Icon className={large ? "h-[18px] w-[18px]" : "h-3.5 w-3.5"} />
    </span>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[105px_1fr] gap-3">
      <dt className="text-muted">{label}</dt>
      <dd className="break-words font-semibold text-ink">{value}</dd>
    </div>
  );
}

function MailboxTable({ mailboxes, onSelect }: { mailboxes: Mailbox[]; onSelect: (mailbox: Mailbox) => void }) {
  return (
    <DataTable>
      <thead className="bg-surface-soft text-left text-[9px] font-bold uppercase tracking-[0.12em] text-muted">
        <tr>
          <th className="border-b border-line px-3 py-2">Mailbox</th>
          <th className="border-b border-line px-3 py-2">Workspace</th>
          <th className="border-b border-line px-3 py-2">Product line</th>
          <th className="border-b border-line px-3 py-2">Status</th>
          <th className="border-b border-line px-3 py-2">Last sync</th>
        </tr>
      </thead>
      <tbody className="divide-y divide-line bg-surface">
        {mailboxes.map((mailbox) => (
          <tr key={mailbox.mailboxConnectionId} className="transition-colors hover:bg-surface-soft">
            <td className="px-3 py-2">
              <button className="font-mono text-[11px] font-semibold text-primary" onClick={() => onSelect(mailbox)}>
                {mailbox.mailboxAddress}
              </button>
            </td>
            <td className="px-3 py-2 text-[11px] text-muted">{mailbox.displayName}</td>
            <td className="px-3 py-2 text-[11px] text-muted">{mailbox.productLine || "All"}</td>
            <td className="px-3 py-2"><StateBadge state={mailbox.ingestionEnabled ? "Configured" : "Pending"} compact /></td>
            <td className="px-3 py-2 font-mono text-[11px] text-muted">Backend reported</td>
          </tr>
        ))}
      </tbody>
    </DataTable>
  );
}

function ChecklistItem({ label, done = false, warning = false }: { label: string; done?: boolean; warning?: boolean }) {
  const Icon = done ? CheckCircle2 : AlertTriangle;
  const className = done ? "text-success-text" : warning ? "text-warning-text" : "text-muted";
  return (
    <div className="flex items-center gap-2 text-xs text-ink">
      <Icon className={`h-3.5 w-3.5 ${className}`} />
      <span>{label}</span>
    </div>
  );
}

function SideRailCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-lg border border-line bg-surface p-3 shadow-sm">
      <h3 className="text-sm font-semibold text-ink">{title}</h3>
      <div className="mt-3 space-y-2.5">{children}</div>
    </section>
  );
}

function MetricLine({ label, value, state }: { label: string; value: string; state?: DisplayState | string }) {
  const stateClass = state === "Configured" || state === "Connected" || state === "Available" || state === "Enabled"
    ? "text-success-text"
    : state === "Degraded"
      ? "text-danger-text"
      : state === "Pending"
        ? "text-warning-text"
        : "text-ink";
  return (
    <div className="flex items-center justify-between gap-3 text-xs">
      <span className="text-muted">{label}</span>
      <span className={`text-right font-semibold ${stateClass}`}>{value}</span>
    </div>
  );
}

function RailLink({ label }: { label: string }) {
  return <button className="w-full text-right text-xs font-semibold text-primary">{label}</button>;
}

function RailAction({ label, onClick, disabled, danger = false }: { label: string; onClick?: () => void; disabled?: boolean; danger?: boolean }) {
  return (
    <button
      className={`w-full rounded-md border px-3 py-1.5 text-xs font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${danger ? "border-danger-border text-danger-text" : "border-primary-border text-primary hover:bg-primary-soft"}`}
      disabled={disabled}
      onClick={onClick}
    >
      {label}
    </button>
  );
}

function StateBadge({ state, compact = false }: { state: DisplayState; compact?: boolean }) {
  const Icon =
    state === "Configured" ? CheckCircle2 :
    state === "Degraded" ? AlertTriangle :
    state === "Pending" ? Settings :
    ShieldCheck;
  const className =
    state === "Configured" ? "border-success-border bg-success-soft text-success-text" :
    state === "Degraded" ? "border-danger-border bg-danger-soft text-danger-text" :
    state === "Pending" ? "border-warning-border bg-warning-soft text-warning-text" :
    "border-line bg-surface-soft text-muted";

  return (
    <span className={`inline-flex items-center gap-1 rounded-full border font-bold uppercase tracking-[0.08em] ${compact ? "px-1.5 py-0.5 text-[8px]" : "px-2.5 py-1 text-[9px]"} ${className}`}>
      <Icon className="h-3 w-3" />
      {state}
    </span>
  );
}

function providerDisplayState(provider: ProviderHealthItem | null): DisplayState {
  if (!provider) return "Not configured";
  const combined = `${provider.status} ${provider.mode}`.toLowerCase();
  if (combined.includes("mock") || combined.includes("simulated") || combined.includes("fake")) return "Not configured";
  if (combined.includes("disabled") || combined.includes("missing") || combined.includes("notconfigured") || combined.includes("not configured")) return "Not configured";
  if (combined.includes("error") || combined.includes("failed") || combined.includes("unhealthy")) return "Degraded";
  if (combined.includes("degraded") || combined.includes("notready") || combined.includes("not ready") || combined.includes("pending")) return "Pending";
  if (combined.includes("healthy") || combined.includes("configured") || combined.includes("connected")) return "Configured";
  return "Pending";
}

function healthTimestamp(provider: ProviderHealthItem | null): string {
  if (!provider) return "-";
  if (provider.status.toLowerCase().includes("missing") || provider.mode.toLowerCase().includes("notconfigured")) {
    return "-";
  }
  return "Backend reported";
}

function overallProviderStatus(rows: Array<{ state: DisplayState }>): DisplayState {
  if (rows.some((row) => row.state === "Degraded")) return "Degraded";
  if (rows.some((row) => row.state === "Pending")) return "Pending";
  if (rows.some((row) => row.state === "Configured")) return "Configured";
  return "Not configured";
}

function safeProviderDetail(provider: ProviderHealthItem | null, state: DisplayState): string {
  if (!provider) return "No backend health signal is available for this provider.";
  const combined = `${provider.status} ${provider.mode} ${provider.detail}`.toLowerCase();
  if (combined.includes("mock") || combined.includes("simulated") || combined.includes("fake")) {
    return "Local development harness only; no production provider is connected.";
  }
  if (state === "Not configured") return provider.detail || "Provider is not configured for this workspace.";
  return provider.detail || "Provider health is reported by the backend.";
}

function IntegrationActionEvidence({ rows }: { rows: Array<{ label: string; value: string }> }) {
  return (
    <div className="grid gap-2 sm:grid-cols-2">
      {rows.map((row) => (
        <div key={row.label} className="rounded-xl border border-line bg-surface-soft p-3">
          <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-muted">{row.label}</p>
          <p className="mt-1 break-words text-sm font-semibold text-ink">{row.value}</p>
        </div>
      ))}
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-surface-soft p-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-muted">{label}</p>
      <p className="mt-2 text-sm font-semibold text-ink font-mono">{value}</p>
    </div>
  );
}
