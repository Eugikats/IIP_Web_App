"use client";

import Link from "next/link";
import { Suspense, useCallback, useEffect, useMemo, useState, type ComponentType } from "react";
import { usePathname } from "next/navigation";
import { AnimatePresence, motion, useReducedMotion } from "framer-motion";
import {
  ArrowLeft, Bot, CreditCard, ShieldCheck, FolderOpen, HardDrive, Languages, Mail, MessageCircle, Package, Plug, Search, Settings, Smartphone, User, Users, X, Bell, Building2, Database, Gauge, Scale, type LucideIcon,
} from "lucide-react";
import { Modal } from "@/components/ui/Modal";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { Input } from "@/components/ui/Input";
import { LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { formatDateTime } from "@/lib/format";
import { cn } from "@/lib/utils";
import { disconnectFileProvider, FileConnectionError, getFileConnectionStatus, startFileConnection, type FileConnectionStatus, type FileProviderKey } from "@/lib/files/fileConnections";
import CreditMeter from "@/components/billing/CreditMeter";
import SecuritySection from "@/components/security/SecuritySection";
import TestSend from "@/components/settings/TestSend";
import { IntegrationBrandIcon } from "@/components/brand/ProviderIcons";
import ProfileSettings from "@/lovable-pages/Settings";
import AgentsPanel from "@/lovable-pages/admin/Agents";
import BillingPanel from "@/lovable-pages/admin/Billing";
import ConnectionsPanel from "@/lovable-pages/admin/ConnectionSettings";
import EmailInboxPanel from "@/lovable-pages/admin/EmailInbox";
import PolicyImportPanel from "@/lovable-pages/admin/PolicyImport";
import ProductsPanel from "@/lovable-pages/admin/Products";
import TenantPolicyPanel from "@/lovable-pages/admin/TenantPolicy";
import UsersPanel from "@/lovable-pages/admin/Users";
import WhatsAppPanel from "@/lovable-pages/admin/WhatsAppSettings";
import NotificationRoutingSettings from "@/lovable-pages/admin/NotificationRoutingSettings";
import TemplatesPanel from "@/lovable-pages/admin/Templates";
import OnboardingPanel from "@/lovable-pages/admin/Onboarding";

// Settings as a window over whatever screen the person is on, laid out as in CIP: a list of sections on
// the left, the section's title and description on top, and the content beneath. Every settings screen
// lives here, so nothing opens as a full page. Integrations come first: what the platform supports, what
// is connected and what is not. A card is "Connected" only when the provider validated the saved
// connection; file sources that cannot be connected yet say "Not connected" and why.

export type SettingsSection = "integrations" | "dataHub" | "agents" | "security" | "notifications" | "usage" | "billing" | "seats" | "organisation" | "profile" | "governance";

const SECTIONS: { key: SettingsSection; label: string; title: string; description: string; icon: LucideIcon }[] = [
  { key: "integrations", label: "Integrations", title: "Integrations", description: "Manage insurer channels and connected systems.", icon: Plug },
  { key: "dataHub", label: "Data Hub", title: "Data Hub", description: "Review insurer knowledge, products and source documents.", icon: Database },
  { key: "agents", label: "Agent rules", title: "Agent rules", description: "Review the approved instructions and proposals that govern agents.", icon: Bot },
  { key: "security", label: "Security & Login", title: "Security & Login", description: "Manage supported IIP security controls alongside Microsoft sign-in.", icon: ShieldCheck },
  { key: "notifications", label: "Notifications", title: "Notifications", description: "Choose who receives operational alerts and where they are delivered.", icon: Bell },
  { key: "usage", label: "Usage", title: "Usage", description: "Review actual AI credit usage and metering for this workspace.", icon: Gauge },
  { key: "billing", label: "Billing", title: "Billing", description: "Review the plan and billing records available to this workspace.", icon: CreditCard },
  { key: "seats", label: "Account seats", title: "Account seats", description: "Manage workspace members and their assigned access.", icon: Users },
  { key: "organisation", label: "Organisation", title: "Organisation", description: "Review insurer identity and onboarding records.", icon: Building2 },
  { key: "profile", label: "Profile", title: "Profile", description: "Manage your IIP profile and personal preferences.", icon: User },
  { key: "governance", label: "Governance & Policies", title: "Governance & Policies", description: "Review approved workspace policy and audit evidence.", icon: Scale },
];

type CardState = { tone: PillTone; label: string; detail: string | null; connected: boolean; checks?: CatalogueCheck[] };

// The Integration Center's own read of each integration: one of four states, each earned by a check on the server.
type CatalogueCheck = { name: string; passed: boolean; note: string; blocking: boolean };
type CatalogueCard = { key: string; state: "Connected" | "NeedsAttention" | "NotConnected" | "NotConfigured"; stateLabel: string; summary: string; checks: CatalogueCheck[] };

function cardStateFromCatalogue(card: CatalogueCard): CardState {
  const tone: PillTone = card.state === "Connected" ? "success" : card.state === "NeedsAttention" ? "warning" : "neutral";
  return { tone, label: card.stateLabel, detail: card.summary, connected: card.state === "Connected" || card.state === "NeedsAttention", checks: card.checks };
}

type PanelKey = "whatsapp" | "templates" | "connections" | "mailbox";

type Integration = {
  key: string;
  name: string;
  description: string;
  icon: LucideIcon;
  /** The settings panel that sets it up, shown inside this window. Absent for a source that cannot be connected yet. */
  panel?: PanelKey;
  /** A file service each person connects their own account to, from this card. */
  fileProvider?: FileProviderKey;
  note?: string;
};

const PANELS: Record<PanelKey, { title: string; Component: ComponentType }> = {
  whatsapp: { title: "WhatsApp Business", Component: WhatsAppPanel },
  templates: { title: "WhatsApp templates", Component: TemplatesPanel },
  connections: { title: "SMS, email and translation", Component: ConnectionsPanel },
  mailbox: { title: "Claims mailbox", Component: EmailInboxPanel },
};

const CATALOG: Integration[] = [
  { key: "whatsapp", name: "WhatsApp Business", description: "Customers write to Kato and staff reply, on the insurer's own WhatsApp number.", icon: MessageCircle, panel: "whatsapp" },
  { key: "sms", name: "SMS", description: "Text messages to customers through the insurer's SMS gateway.", icon: Smartphone, panel: "connections" },
  { key: "email", name: "Email", description: "Send customer email from the insurer's own address.", icon: Mail, panel: "connections" },
  { key: "teams", name: "Microsoft Teams", description: "Alerts about claims waiting for approval post to a Teams channel.", icon: Users },
  { key: "mailbox", name: "Claims mailbox", description: "Collect claims mail from a shared mailbox and turn it into claims.", icon: Mail, panel: "mailbox" },
  { key: "translation", name: "Local-language translation", description: "Lets Kato talk with customers in Luganda and Kiswahili.", icon: Languages, panel: "connections" },
  { key: "gdrive", name: "Google Drive", description: "Pick a customer list from your own Google Drive. IIP can only open the files you choose.", icon: HardDrive, fileProvider: "google" },
  { key: "onedrive", name: "OneDrive", description: "Pick a customer list from your own OneDrive. It is your account, so IIP sees only what you can open.", icon: HardDrive, fileProvider: "microsoft" },
  { key: "sharepoint", name: "SharePoint", description: "Pick files from a SharePoint library, and save exports there.", icon: FolderOpen, note: "Not connected. A Microsoft app registration has not been set up for this workspace yet. Upload a CSV file in the meantime." },
];

type ConnectionDto = { connected?: boolean; providerKey?: string | null; lastValidatedAt?: string | null; lastValidationStatus?: string | null; businessDisplayName?: string | null };

async function readJson(path: string, role: string): Promise<{ status: number; body: unknown }> {
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    return { status: response.status, body: response.ok ? await response.json() : null };
  } catch {
    return { status: 0, body: null };
  }
}

function stateFrom(result: { status: number; body: unknown }, label: string): CardState {
  if (result.status === 403) return { tone: "neutral", label: "No permission", detail: `Your role cannot see the ${label} connection.`, connected: false };
  if (result.status === 0 || result.status >= 500) return { tone: "warning", label: "Could not check", detail: "The status could not be read just now.", connected: false };
  const dto = (result.body ?? {}) as ConnectionDto;
  if (!dto.connected) return { tone: "neutral", label: "Not connected", detail: null, connected: false };
  const validated = dto.lastValidatedAt ? `Checked ${formatDateTime(dto.lastValidatedAt)}` : null;
  const who = dto.businessDisplayName ?? (dto.providerKey ? `Provider: ${dto.providerKey}` : null);
  if (dto.lastValidationStatus && dto.lastValidationStatus !== "Success") {
    return { tone: "warning", label: "Needs attention", detail: [who, "The last check failed."].filter(Boolean).join(". "), connected: true };
  }
  return { tone: "success", label: "Connected", detail: [who, validated].filter(Boolean).join(". ") || null, connected: true };
}

export function SettingsModal({
  open,
  section,
  onSectionChange,
  onClose,
}: {
  open: boolean;
  section: SettingsSection;
  onSectionChange: (section: SettingsSection) => void;
  onClose: () => void;
}) {
  const reduceMotion = useReducedMotion();
  const pathname = usePathname();
  const meta = SECTIONS.find((item) => item.key === section) ?? SECTIONS[0];

  // A link inside a panel that opens a full screen (a product's editor) takes the person there, so the
  // window must not stay open over it.
  const [openedAt, setOpenedAt] = useState(pathname);
  useEffect(() => {
    if (open) setOpenedAt(pathname);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);
  useEffect(() => {
    if (open && pathname !== openedAt) onClose();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname]);

  return (
    <Modal open={open} title="Settings" onClose={onClose} width={1180} bare>
      <div className="flex h-[min(820px,calc(100dvh-1.5rem))] flex-col sm:flex-row">
        <div className="hidden w-[276px] shrink-0 flex-col border-r border-dls-border-subtle bg-dls-surface sm:flex">
          <div className="flex items-center gap-2 border-b border-gray-100 px-5 py-4">
            <Settings className="h-4 w-4 text-dls-text-secondary" aria-hidden="true" />
            <span className="text-sm font-semibold text-dls-text-primary">Settings</span>
          </div>
          <nav aria-label="Settings sections" className="flex-1 space-y-0.5 overflow-y-auto px-3 py-3">
            {SECTIONS.map((item) => {
              const Icon = item.icon;
              const active = section === item.key;
              return (
                <button
                  key={item.key}
                  type="button"
                  onClick={() => onSectionChange(item.key)}
                  aria-current={active ? "page" : undefined}
                  className={cn(
                    "flex min-h-10 w-full items-center gap-2.5 rounded-dls-control px-3 py-2 text-left text-[13px] font-medium transition-colors dls-focus-outline",
                    active ? "bg-dls-primary-soft text-dls-primary" : "text-dls-text-secondary hover:bg-dls-bg-page hover:text-dls-text-primary",
                  )}
                >
                  <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
                  {item.label}
                </button>
              );
            })}
          </nav>
        </div>

        <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
          <div className="flex shrink-0 items-start justify-between gap-3 border-b border-gray-100 px-4 py-3 sm:px-7 sm:py-5">
            <div className="min-w-0">
              <h2 className="m-0 truncate text-lg font-semibold tracking-tight text-dls-text-primary sm:text-xl">{meta.title}</h2>
              <p className="m-0 mt-1 hidden text-sm text-dls-text-secondary sm:block">{meta.description}</p>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="dls-focus-outline flex h-9 w-9 shrink-0 items-center justify-center rounded-dls-control text-dls-text-secondary transition-colors hover:bg-dls-bg-page hover:text-dls-text-primary"
              aria-label="Close settings"
              title="Close settings (Esc)"
            >
              <X className="h-5 w-5" aria-hidden="true" />
            </button>
          </div>

          <div className="shrink-0 border-b border-dls-border-subtle bg-dls-surface px-3 py-2 sm:hidden">
            <label className="block text-xs font-semibold text-dls-text-secondary" htmlFor="settings-section-select">Settings section</label>
            <select id="settings-section-select" value={section} onChange={(event) => onSectionChange(event.target.value as SettingsSection)} className="mt-1 min-h-10 w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 text-sm text-dls-text-primary dls-focus-outline">
              {SECTIONS.map((item) => <option key={item.key} value={item.key}>{item.label}</option>)}
            </select>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4 pb-[max(1.25rem,env(safe-area-inset-bottom))] sm:px-7 sm:py-5">
            <AnimatePresence mode="wait">
              <motion.div
                key={section}
                initial={{ opacity: 0, y: reduceMotion ? 0 : 4 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0 }}
                transition={{ duration: reduceMotion ? 0 : 0.15 }}
              >
                <Suspense fallback={<LoadingSkeleton rows={4} />}>
                  {section === "integrations" ? <IntegrationsSection /> : null}
                  {section === "agents" ? <AgentsSection /> : null}
                  {section === "dataHub" ? <DataHubSection /> : null}
                  {section === "security" ? <SecuritySection /> : null}
                  {section === "profile" ? <Embedded><ProfileSettings /></Embedded> : null}
                  {section === "notifications" ? <Embedded><NotificationRoutingSettings /></Embedded> : null}
                  {section === "usage" ? <Embedded><CreditMeter /></Embedded> : null}
                  {section === "billing" ? <Embedded><BillingPanel /></Embedded> : null}
                  {section === "seats" ? <Embedded><UsersPanel /></Embedded> : null}
                  {section === "organisation" ? <OrganisationSection /> : null}
                  {section === "governance" ? <GovernanceSection /> : null}
                </Suspense>
              </motion.div>
            </AnimatePresence>
          </div>
        </div>
      </div>
    </Modal>
  );
}

/** Screens built as full pages are shown inside the window; their own page titles are scaled down to fit. */
function Embedded({ children }: { children: React.ReactNode }) {
  return <div className="[&_h1]:text-[17px] [&_h1]:leading-snug">{children}</div>;
}

function Tabbed({ tabs }: { tabs: { key: string; label: string; Component: ComponentType }[] }) {
  const [active, setActive] = useState(tabs[0].key);
  const current = tabs.find((tab) => tab.key === active) ?? tabs[0];
  return (
    <div>
      <div role="tablist" aria-label="Section views" className="mb-5 inline-flex rounded-full border border-gray-200 bg-white p-1">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            role="tab"
            aria-selected={active === tab.key}
            onClick={() => setActive(tab.key)}
            className={cn(
              "rounded-full px-4 py-1.5 text-[13px] font-semibold transition-colors",
              active === tab.key ? "bg-blue-50 text-blue-700" : "text-gray-600 hover:text-gray-900",
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <Embedded><current.Component /></Embedded>
    </div>
  );
}

function AgentsSection() {
  return (
    <Embedded>
      <AgentsPanel />
    </Embedded>
  );
}

function DataHubSection() {
  return (
    <div className="space-y-5">
      <section className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 sm:p-5">
        <h3 className="m-0 text-[15px] font-semibold text-dls-text-primary">Insurer knowledge and sources</h3>
        <p className="m-0 mt-1 text-[13px] leading-5 text-dls-text-secondary">Review policy wordings, product documents and extracted proposals in the knowledge library. Source material remains separate from approved product and agent rules.</p>
        <Link href="/app/knowledge" className="mt-3 inline-flex min-h-10 items-center rounded-dls-control border border-dls-border-input px-3 text-[13px] font-semibold text-dls-text-primary hover:bg-dls-bg-page dls-focus-outline">Open knowledge library</Link>
      </section>
      <Tabbed tabs={[
        { key: "products", label: "Products and sources", Component: ProductsPanel },
        { key: "wordings", label: "Import wordings", Component: PolicyImportPanel },
      ]} />
    </div>
  );
}

function OrganisationSection() {
  const { runtimeContext } = useRuntimeContext();
  const active = runtimeContext?.availableWorkspaces.find((item) => item.workspaceId === runtimeContext.workspaceId);
  return (
    <div className="space-y-4">
      <section className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 sm:p-5">
        <h3 className="m-0 text-[15px] font-semibold text-dls-text-primary">Current workspace</h3>
        <dl className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div><dt className="text-xs font-semibold text-dls-text-secondary">Organisation</dt><dd className="m-0 mt-1 text-sm text-dls-text-primary">{active?.name ?? "Not configured"}</dd></div>
          <div><dt className="text-xs font-semibold text-dls-text-secondary">Workspace</dt><dd className="m-0 mt-1 break-all text-sm text-dls-text-primary">{runtimeContext?.workspaceId ?? "Not configured"}</dd></div>
        </dl>
      </section>
      <Embedded><OnboardingPanel /></Embedded>
    </div>
  );
}

function GovernanceSection() {
  return (
    <div className="space-y-5">
      <section className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 sm:p-5">
        <h3 className="m-0 text-[15px] font-semibold text-dls-text-primary">Human decision boundaries</h3>
        <p className="m-0 mt-1 text-[13px] leading-5 text-dls-text-secondary">IIP can prepare evidence and recommendations. A person remains responsible for claim, settlement, underwriting, fraud and complaint decisions.</p>
      </section>
      <Tabbed tabs={[
        { key: "policy", label: "Workspace policy", Component: TenantPolicyPanel },
        { key: "agents", label: "Agent rules", Component: AgentsPanel },
      ]} />
    </div>
  );
}

function IntegrationsSection() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [view, setView] = useState<"catalog" | "yours">("catalog");
  const [query, setQuery] = useState("");
  const [states, setStates] = useState<Record<string, CardState> | null>(null);
  const [catalogue, setCatalogue] = useState<Record<string, CatalogueCard> | "error" | "forbidden" | null>(null);
  const [panel, setPanel] = useState<PanelKey | null>(null);
  const [fileStates, setFileStates] = useState<Record<FileProviderKey, FileConnectionStatus | "error"> | null>(null);
  const [fileProblem, setFileProblem] = useState<string | null>(null);
  const [busyProvider, setBusyProvider] = useState<FileProviderKey | null>(null);

  const loadFiles = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) {
      setFileStates(null);
      return;
    }
    try {
      const list = await getFileConnectionStatus(role);
      const byKey = Object.fromEntries(list.map((item) => [item.provider, item])) as Record<FileProviderKey, FileConnectionStatus>;
      setFileStates({ google: byKey.google ?? "error", microsoft: byKey.microsoft ?? "error" });
    } catch {
      setFileStates({ google: "error", microsoft: "error" });
    }
  }, [role]);

  const connectFiles = async (provider: FileProviderKey) => {
    setBusyProvider(provider);
    setFileProblem(null);
    try {
      await startFileConnection(provider, window.location.pathname, role);
    } catch (reason) {
      setFileProblem(reason instanceof FileConnectionError ? reason.message : "The connection could not be started.");
      setBusyProvider(null);
    }
  };

  const disconnectFiles = async (provider: FileProviderKey) => {
    setBusyProvider(provider);
    setFileProblem(null);
    try {
      await disconnectFileProvider(provider, role);
      await loadFiles();
    } catch (reason) {
      setFileProblem(reason instanceof FileConnectionError ? reason.message : "The account could not be disconnected.");
    } finally {
      setBusyProvider(null);
    }
  };

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) {
      setStates({});
      return;
    }
    const [whatsapp, sms, email, translation, summary] = await Promise.all([
      readJson("/api/v1/integrations/whatsapp/configuration", role),
      readJson("/api/v1/integrations/connections/sms", role),
      readJson("/api/v1/integrations/connections/email", role),
      readJson("/api/v1/integrations/connections/translation", role),
      readJson("/api/v1/integrations/catalogue", role),
    ]);
    if (summary.status === 403) setCatalogue("forbidden");
    else if (summary.status === 200 && summary.body && Array.isArray((summary.body as { cards?: unknown }).cards)) {
      setCatalogue(Object.fromEntries(((summary.body as { cards: CatalogueCard[] }).cards).map((card) => [card.key, card])));
    } else setCatalogue("error");
    setStates({
      whatsapp: stateFrom(whatsapp, "WhatsApp"),
      sms: stateFrom(sms, "SMS"),
      email: stateFrom(email, "email"),
      translation: stateFrom(translation, "translation"),
    });
  }, [role]);

  useEffect(() => { if (panel === null) { void load(); void loadFiles(); } }, [load, loadFiles, panel]);

  const stateOf = (item: Integration): CardState => {
    if (item.key === "mailbox") return { tone: "neutral", label: "Open to check", detail: null, connected: false };
    if (item.fileProvider) {
      const status = fileStates?.[item.fileProvider];
      if (status === undefined) return { tone: "neutral", label: "Checking", detail: null, connected: false };
      if (status === "error") return { tone: "warning", label: "Could not check", detail: "The status could not be read just now.", connected: false };
      if (!status.configured) return { tone: "neutral", label: "Not connected", detail: `${status.reason ?? "Sign-in has not been set up for IIP yet."} Upload a CSV or Excel file in the meantime.`, connected: false };
      if (!status.connected) return { tone: "neutral", label: "Not connected", detail: "Connect your own account to pick files from it.", connected: false };
      return { tone: "success", label: "Connected", detail: status.accountEmail ? `Your account: ${status.accountEmail}` : "Your account is connected.", connected: true };
    }
    // The server's own read of the integration comes first: its state is earned by checks, not by a saved setting.
    const earned = typeof catalogue === "object" && catalogue !== null ? catalogue[item.key] : undefined;
    if (earned) return cardStateFromCatalogue(earned);
    if (item.key === "teams") {
      return catalogue === null
        ? { tone: "neutral", label: "Checking", detail: null, connected: false }
        : { tone: "warning", label: "Could not check", detail: "The status could not be read just now.", connected: false };
    }
    return item.panel
      ? states?.[item.key] ?? { tone: "neutral", label: "Checking", detail: null, connected: false }
      : { tone: "neutral", label: "Not connected", detail: item.note ?? null, connected: false };
  };

  const shown = useMemo(() => {
    const q = query.trim().toLowerCase();
    return CATALOG.filter((item) => {
      const fileStatus = item.fileProvider ? fileStates?.[item.fileProvider] : undefined;
      const earned = typeof catalogue === "object" && catalogue !== null ? catalogue[item.key] : undefined;
      const isConnected = item.fileProvider
        ? typeof fileStatus === "object" && fileStatus.connected
        : earned ? earned.state === "Connected" || earned.state === "NeedsAttention" : (states?.[item.key]?.connected ?? false);
      if (view === "yours" && !isConnected) return false;
      return !q || `${item.name} ${item.description}`.toLowerCase().includes(q);
    });
  }, [query, view, states, fileStates, catalogue]);

  if (panel) {
    const { title, Component } = PANELS[panel];
    return (
      <div>
        <button
          type="button"
          onClick={() => setPanel(null)}
          className="mb-4 inline-flex items-center gap-1.5 rounded-lg px-2 py-1 text-[13px] font-semibold text-blue-700 transition-colors hover:bg-blue-50"
        >
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden="true" />
          All integrations
        </button>
        <h3 className="m-0 mb-3 text-[16px] font-bold text-slate-900">{title}</h3>
        <Embedded><Component /></Embedded>
      </div>
    );
  }

  return (
    <div>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="inline-flex shrink-0 rounded-full border border-gray-200 bg-white p-1" role="tablist" aria-label="Integration views">
          {([["catalog", "Catalog"], ["yours", "Yours"]] as const).map(([key, label]) => (
            <button
              key={key}
              type="button"
              role="tab"
              aria-selected={view === key}
              onClick={() => setView(key)}
              className={cn(
                "rounded-full px-4 py-1.5 text-[13px] font-semibold transition-colors",
                view === key ? "bg-blue-50 text-blue-700" : "text-gray-600 hover:text-gray-900",
              )}
            >
              {label}
            </button>
          ))}
        </div>
        <label className="block flex-1">
          <span className="sr-only">Search integrations</span>
          <Input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search integrations" leadingIcon={<Search aria-hidden="true" />} />
        </label>
      </div>

      {fileProblem ? <p role="alert" className="m-0 mt-3 rounded-lg border border-red-100 bg-red-50 px-3 py-2 text-[12.5px] text-red-700">{fileProblem}</p> : null}

      <div className="mt-5">
        {states === null ? (
          <LoadingSkeleton rows={4} />
        ) : shown.length === 0 ? (
          <div className="rounded-xl border border-gray-200 bg-white p-6 text-[13.5px] text-slate-500">
            {view === "yours" && query.length === 0
              ? "Nothing is connected yet. Open the Catalog to see what can be."
              : "No integration matches your search."}
          </div>
        ) : (
          <ul className="m-0 grid grid-cols-1 gap-3 p-0 lg:grid-cols-2">
            {shown.map((item, index) => {
              const state = stateOf(item);
              const Icon = item.icon;
              return (
                <motion.li
                  key={item.key}
                  className="list-none rounded-xl border border-gray-200 bg-white p-4 transition-colors hover:border-blue-200"
                  initial={{ opacity: 0, y: 4 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.2, delay: Math.min(index, 8) * 0.03 }}
                >
                  <div className="flex items-start gap-3">
                    <IntegrationBrandIcon
                      id={item.key}
                      className="!h-10 !w-10"
                      fallback={
                        <span className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-blue-50 text-[#1769FF]">
                          <Icon className="h-5 w-5" aria-hidden="true" />
                        </span>
                      }
                    />
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <h4 className="m-0 text-[14.5px] font-bold text-slate-900">{item.name}</h4>
                        <StatusPill tone={state.tone} label={state.label} confirmed={state.connected && state.tone === "success"} />
                      </div>
                      <p className="m-0 mt-1 text-[13px] leading-5 text-slate-500">{item.description}</p>
                      {state.detail ? <p className="m-0 mt-2 text-[12.5px] leading-5 text-slate-500">{state.detail}</p> : null}
                      {state.checks && state.checks.length > 0 ? (
                        <ul className="m-0 mt-2 space-y-1 p-0" aria-label={`${item.name} checks`}>
                          {state.checks.map((check) => (
                            <li key={check.name} className="flex list-none items-start gap-2 text-[12px] leading-5 text-slate-500">
                              <span className={cn("mt-[7px] h-1.5 w-1.5 shrink-0 rounded-full", check.passed ? "bg-emerald-500" : check.blocking ? "bg-amber-500" : "bg-slate-300")} aria-hidden="true" />
                              <span><span className="font-medium text-slate-600">{check.name}.</span> {check.note}</span>
                            </li>
                          ))}
                        </ul>
                      ) : null}
                      {item.fileProvider && typeof fileStates?.[item.fileProvider] === "object" && (fileStates[item.fileProvider] as FileConnectionStatus).configured ? (
                        <button
                          type="button"
                          disabled={busyProvider !== null}
                          onClick={() => void (state.connected ? disconnectFiles(item.fileProvider!) : connectFiles(item.fileProvider!))}
                          className="mt-3 inline-flex min-h-9 items-center rounded-lg border border-gray-200 bg-white px-3 text-[13px] font-semibold text-slate-700 transition-colors hover:bg-slate-50 disabled:opacity-50"
                        >
                          {state.connected ? "Disconnect" : "Connect"}
                        </button>
                      ) : null}
                      {item.panel ? (
                        <button
                          type="button"
                          onClick={() => setPanel(item.panel!)}
                          className="mt-3 inline-flex min-h-9 items-center rounded-lg border border-gray-200 bg-white px-3 text-[13px] font-semibold text-slate-700 transition-colors hover:bg-slate-50"
                        >
                          {state.connected ? "Manage" : "Set up"}
                        </button>
                      ) : null}
                      {item.key === "whatsapp" ? (
                        <button type="button" onClick={() => setPanel("templates")} className="ml-2 mt-3 inline-flex min-h-9 items-center rounded-lg px-3 text-[13px] font-semibold text-[#1769FF] transition-colors hover:bg-blue-50 dls-focus-outline">
                          Templates
                        </button>
                      ) : null}
                      {(item.key === "email" || item.key === "sms") && state.connected ? <TestSend channel={item.key} role={role} name={item.name} /> : null}
                    </div>
                  </div>
                </motion.li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}

