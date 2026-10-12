"use client";

import Link from "next/link";
import { Suspense, useCallback, useEffect, useMemo, useState, type ComponentType } from "react";
import { usePathname } from "next/navigation";
import { AnimatePresence, motion, useReducedMotion } from "framer-motion";
import {
  ArrowLeft, Bot, CreditCard, FolderOpen, GraduationCap, HardDrive, Languages, Mail, MessageCircle, Package, Plug, Search, Settings, Smartphone, User, Users, X, type LucideIcon,
} from "lucide-react";
import { Modal } from "@/components/ui/Modal";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { Input } from "@/components/ui/Input";
import { LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { formatDateTime } from "@/lib/format";
import { cn } from "@/lib/utils";
import { disconnectFileProvider, FileConnectionError, getFileConnectionStatus, startFileConnection, type FileConnectionStatus, type FileProviderKey } from "@/lib/files/fileConnections";
import { COURSES } from "@/lib/academy/courses";
import { formatPrice, useAcademyPricing } from "@/lib/academy/pricing";
import CreditMeter from "@/components/billing/CreditMeter";
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

// Settings as a window over whatever screen the person is on, laid out as in CIP: a list of sections on
// the left, the section's title and description on top, and the content beneath. Every settings screen
// lives here, so nothing opens as a full page. Integrations come first: what the platform supports, what
// is connected and what is not. A card is "Connected" only when the provider validated the saved
// connection; file sources that cannot be connected yet say "Not connected" and why.

export type SettingsSection = "profile" | "integrations" | "agents" | "team" | "products" | "academy" | "billing";

const SECTIONS: { key: SettingsSection; label: string; title: string; description: string; icon: LucideIcon }[] = [
  { key: "integrations", label: "Integrations", title: "Integrations", description: "What the platform can connect to, and what is connected.", icon: Plug },
  { key: "agents", label: "Agent rules", title: "Agent rules", description: "What each agent may do, how Kato speaks, and the switch that turns each agent off.", icon: Bot },
  { key: "team", label: "Team and access", title: "Team and access", description: "Who can sign in, what they can see and do, and the rules everyone in this workspace follows.", icon: Users },
  { key: "products", label: "Products", title: "Products", description: "The covers sold, their documents and claim stages, and policy imports.", icon: Package },
  { key: "academy", label: "IIP Academy", title: "IIP Academy prices", description: "What each course costs a learner.", icon: GraduationCap },
  { key: "billing", label: "Usage and billing", title: "Usage and billing", description: "How much the platform has been used, the plan, and AI credits.", icon: CreditCard },
  { key: "profile", label: "Profile", title: "Profile", description: "Who you are signed in as, and the workspace you are working in.", icon: User },
];

type CardState = { tone: PillTone; label: string; detail: string | null; connected: boolean };

type PanelKey = "whatsapp" | "connections" | "mailbox";

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
  connections: { title: "SMS, email and translation", Component: ConnectionsPanel },
  mailbox: { title: "Claims mailbox", Component: EmailInboxPanel },
};

const CATALOG: Integration[] = [
  { key: "whatsapp", name: "WhatsApp Business", description: "Customers write to Kato and staff reply, on the insurer's own WhatsApp number.", icon: MessageCircle, panel: "whatsapp" },
  { key: "sms", name: "SMS", description: "Text messages to customers through the insurer's SMS gateway.", icon: Smartphone, panel: "connections" },
  { key: "email", name: "Email", description: "Send customer email from the insurer's own address.", icon: Mail, panel: "connections" },
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
    <Modal open={open} title="Settings" onClose={onClose} width={960} bare>
      <div className="flex h-[min(720px,calc(100dvh-3rem))] flex-col sm:flex-row">
        <div className="hidden w-52 shrink-0 flex-col border-r border-gray-100 bg-gray-50 sm:flex">
          <div className="flex items-center gap-2 border-b border-gray-100 px-5 py-4">
            <Settings className="h-4 w-4 text-gray-500" aria-hidden="true" />
            <span className="text-sm font-semibold text-gray-900">Settings</span>
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
                    "flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-sm font-medium transition-colors",
                    active ? "bg-blue-50 text-blue-700" : "text-gray-600 hover:bg-white hover:text-gray-900",
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
              <h2 className="m-0 truncate text-lg font-semibold tracking-tight text-slate-900 sm:text-xl">{meta.title}</h2>
              <p className="m-0 mt-1 hidden text-sm text-slate-500 sm:block">{meta.description}</p>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-500 transition-colors hover:bg-slate-200 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
              aria-label="Close settings"
              title="Close settings (Esc)"
            >
              <X className="h-5 w-5" aria-hidden="true" />
            </button>
          </div>

          <div className="flex shrink-0 gap-1 overflow-x-auto border-b border-gray-100 bg-gray-50 px-3 py-2 sm:hidden">
            {SECTIONS.map((item) => (
              <button
                key={item.key}
                type="button"
                onClick={() => onSectionChange(item.key)}
                aria-current={section === item.key ? "page" : undefined}
                className={cn(
                  "shrink-0 rounded-full px-3 py-1.5 text-[12px] font-semibold transition-colors",
                  section === item.key ? "bg-blue-600 text-white" : "bg-white text-gray-600 ring-1 ring-gray-200",
                )}
              >
                {item.label}
              </button>
            ))}
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
                  {section === "team" ? (
                    <Tabbed
                      tabs={[
                        { key: "users", label: "Users and roles", Component: UsersPanel },
                        { key: "policy", label: "Workspace policy", Component: TenantPolicyPanel },
                      ]}
                    />
                  ) : null}
                  {section === "products" ? (
                    <Tabbed
                      tabs={[
                        { key: "products", label: "Products", Component: ProductsPanel },
                        { key: "import", label: "Import policies", Component: PolicyImportPanel },
                      ]}
                    />
                  ) : null}
                  {section === "academy" ? <AcademyPricingSection /> : null}
                  {section === "billing" ? <BillingSection /> : null}
                  {section === "profile" ? <Embedded><ProfileSettings /></Embedded> : null}
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

function BillingSection() {
  return (
    <div>
      <CreditMeter />
      <Embedded>
        <BillingPanel />
      </Embedded>
    </div>
  );
}

function IntegrationsSection() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [view, setView] = useState<"catalog" | "yours">("catalog");
  const [query, setQuery] = useState("");
  const [states, setStates] = useState<Record<string, CardState> | null>(null);
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
    const [whatsapp, sms, email, translation] = await Promise.all([
      readJson("/api/v1/integrations/whatsapp/configuration", role),
      readJson("/api/v1/integrations/connections/sms", role),
      readJson("/api/v1/integrations/connections/email", role),
      readJson("/api/v1/integrations/connections/translation", role),
    ]);
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
    return item.panel
      ? states?.[item.key] ?? { tone: "neutral", label: "Checking", detail: null, connected: false }
      : { tone: "neutral", label: "Not connected", detail: item.note ?? null, connected: false };
  };

  const shown = useMemo(() => {
    const q = query.trim().toLowerCase();
    return CATALOG.filter((item) => {
      const fileStatus = item.fileProvider ? fileStates?.[item.fileProvider] : undefined;
      const isConnected = item.fileProvider ? typeof fileStatus === "object" && fileStatus.connected : (states?.[item.key]?.connected ?? false);
      if (view === "yours" && !isConnected) return false;
      return !q || `${item.name} ${item.description}`.toLowerCase().includes(q);
    });
  }, [query, view, states, fileStates]);

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
                    <span className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-blue-50 text-[#1769FF]">
                      <Icon className="h-5 w-5" aria-hidden="true" />
                    </span>
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <h4 className="m-0 text-[14.5px] font-bold text-slate-900">{item.name}</h4>
                        <StatusPill tone={state.tone} label={state.label} confirmed={state.connected && state.tone === "success"} />
                      </div>
                      <p className="m-0 mt-1 text-[13px] leading-5 text-slate-500">{item.description}</p>
                      {state.detail ? <p className="m-0 mt-2 text-[12.5px] leading-5 text-slate-400">{state.detail}</p> : null}
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

function AcademyPricingSection() {
  const { prices, currency, loading, error, save } = useAcademyPricing();
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  useEffect(() => {
    if (prices) setDraft(Object.fromEntries(Object.entries(prices).map(([id, amount]) => [id, String(amount)])));
  }, [prices]);

  const invalid = COURSES.some((course) => {
    const value = draft[course.id];
    return value === undefined || value.trim() === "" || Number.isNaN(Number(value)) || Number(value) < 0 || Number(value) > 10000;
  });
  const changed = !!prices && COURSES.some((course) => Number(draft[course.id]) !== prices[course.id]);

  const onSave = async () => {
    setSaving(true);
    setMessage(null);
    const result = await save(Object.fromEntries(COURSES.map((course) => [course.id, Math.round(Number(draft[course.id]) * 100) / 100])));
    setSaving(false);
    setMessage(result.ok ? { tone: "ok", text: "Prices saved." } : { tone: "bad", text: result.message ?? "The prices could not be saved." });
  };

  return (
    <div>
      <h3 className="m-0 text-[16px] font-bold text-dls-text-primary">IIP Academy prices</h3>
      <p className="m-0 mt-1 max-w-2xl text-[13px] text-dls-text-secondary">
        What each course costs a learner, in Ugandan shillings. Learners see the price on the course page. Every course starts at {formatPrice(500_000)}. Payment is not connected yet, so nobody is charged.
      </p>
      {loading && !prices ? <div className="mt-4"><LoadingSkeleton rows={4} /></div> : null}
      {error ? <p role="alert" className="m-0 mt-4 text-[13px] text-red-700">{error}</p> : null}
      {prices ? (
        <div className="mt-4 space-y-2.5">
          {COURSES.map((course) => (
            <label key={course.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-dls-border-subtle bg-dls-surface px-4 py-3">
              <span className="min-w-0">
                <span className="block text-[14px] font-semibold text-dls-text-primary">{course.title}</span>
                <span className="block text-[12px] text-dls-text-muted">Course {course.number}, {course.duration}</span>
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span className="text-[13px] text-dls-text-secondary" aria-hidden>{currency}</span>
                <Input
                  inputMode="decimal"
                  value={draft[course.id] ?? ""}
                  onChange={(event) => { setDraft({ ...draft, [course.id]: event.target.value }); setMessage(null); }}
                  aria-label={`Price of ${course.title} in ${currency}`}
                  className="w-24 text-right"
                />
              </span>
            </label>
          ))}
          <div className="flex flex-wrap items-center gap-3 pt-1">
            <button
              type="button"
              disabled={saving || invalid || !changed}
              onClick={() => void onSave()}
              className="inline-flex h-9 items-center rounded-lg bg-dls-primary-accent px-4 text-[13px] font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50"
            >
              {saving ? "Saving" : "Save prices"}
            </button>
            {invalid ? <span className="text-[12.5px] text-amber-700">Enter a price from 0 to 100,000,000. Use 0 for a free course.</span> : null}
            {message ? <span role="status" className={cn("text-[12.5px]", message.tone === "ok" ? "text-emerald-700" : "text-red-700")}>{message.text}</span> : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}
