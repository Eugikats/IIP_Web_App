"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { AnimatePresence, motion, useReducedMotion } from "framer-motion";
import {
  Bot, CreditCard, FolderOpen, HardDrive, Languages, Mail, MessageCircle, Package, Plug, Search, Smartphone, Users, type LucideIcon,
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

// Settings as one place: integrations first (what the platform supports, what is connected and what is
// is not), then the existing administration screens grouped under it. Every status here is read from
// the API; a card is "Connected" only when the provider validated the saved connection. File sources
// without a connection screen say "Not connected" and why, so nobody expects them to work.

type Section = "integrations" | "agents" | "team" | "products" | "billing";

const SECTIONS: { key: Section; label: string; icon: LucideIcon }[] = [
  { key: "integrations", label: "Integrations", icon: Plug },
  { key: "agents", label: "Agent rules", icon: Bot },
  { key: "team", label: "Team and access", icon: Users },
  { key: "products", label: "Products", icon: Package },
  { key: "billing", label: "Usage and billing", icon: CreditCard },
];

type CardState = { tone: PillTone; label: string; detail: string | null; connected: boolean };

type Integration = {
  key: string;
  name: string;
  description: string;
  icon: LucideIcon;
  /** Where to set it up. Absent for a file source that cannot be connected from here yet. */
  setupHref?: string;
  note?: string;
};

const CATALOG: Integration[] = [
  { key: "whatsapp", name: "WhatsApp Business", description: "Customers write to Kato and staff reply, on the insurer's own WhatsApp number.", icon: MessageCircle, setupHref: "/app/admin/whatsapp" },
  { key: "sms", name: "SMS", description: "Text messages to customers through the insurer's SMS gateway.", icon: Smartphone, setupHref: "/app/admin/connections" },
  { key: "email", name: "Email", description: "Send customer email and collect claims mail from a shared mailbox.", icon: Mail, setupHref: "/app/admin/connections" },
  { key: "sharepoint", name: "SharePoint", description: "Pick a customer list from a SharePoint library.", icon: FolderOpen, note: "Needs a Microsoft app registration set up by your administrator. Upload a CSV or Excel file in the meantime." },
  { key: "onedrive", name: "OneDrive", description: "Pick a customer list from OneDrive.", icon: HardDrive, note: "Needs a Microsoft app registration set up by your administrator. Upload a CSV or Excel file in the meantime." },
  { key: "gdrive", name: "Google Drive", description: "Pick a customer list from Google Drive.", icon: HardDrive, note: "Needs a Google app registration set up by your administrator. Upload a CSV or Excel file in the meantime." },
  { key: "translation", name: "Local-language translation", description: "Lets Kato talk with customers in Luganda and Kiswahili.", icon: Languages, setupHref: "/app/admin/connections" },
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

export function SettingsModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [section, setSection] = useState<Section>("integrations");
  const reduceMotion = useReducedMotion();

  return (
    <Modal open={open} title="Settings" onClose={onClose} width={1040}>
      <div className="flex max-h-[calc(100vh-9rem)] min-h-[28rem] flex-col md:flex-row">
        <nav aria-label="Settings sections" className="flex shrink-0 gap-1 overflow-x-auto border-b border-dls-border-subtle bg-dls-surface p-3 md:w-60 md:flex-col md:overflow-visible md:border-b-0 md:border-r">
          {SECTIONS.map((item) => {
            const Icon = item.icon;
            const active = section === item.key;
            return (
              <button
                key={item.key}
                type="button"
                onClick={() => setSection(item.key)}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "dls-focus-outline inline-flex shrink-0 items-center gap-2.5 rounded-dls-control px-3 py-2.5 text-left text-[14px] font-semibold transition-colors duration-dls-fast",
                  active ? "bg-dls-primary-soft-alt text-dls-primary-accent" : "text-dls-text-secondary hover:bg-dls-bg-page hover:text-dls-text-primary",
                )}
              >
                <Icon className="h-4 w-4" aria-hidden="true" />
                {item.label}
              </button>
            );
          })}
        </nav>
        <div className="min-w-0 flex-1 overflow-y-auto p-5 sm:p-6">
          <AnimatePresence mode="wait">
            <motion.div
              key={section}
              initial={{ opacity: 0, y: reduceMotion ? 0 : 4 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0 }}
              transition={{ duration: reduceMotion ? 0 : 0.15 }}
            >
              {section === "integrations" ? <IntegrationsSection onNavigate={onClose} /> : null}
              {section === "agents" ? (
                <LinkSection
                  onNavigate={onClose}
                  title="Agent rules"
                  intro="What each agent may do, how Kato speaks, and the kill switch for every agent."
                  links={[
                    { href: "/app/admin/agents", label: "Agents", detail: "Switch an agent on or off, read its run history, and open its behaviour settings (tone, scope and voice)." },
                  ]}
                />
              ) : null}
              {section === "team" ? (
                <LinkSection
                  onNavigate={onClose}
                  title="Team and access"
                  intro="Who can sign in and what they can see and do."
                  links={[
                    { href: "/app/admin/users", label: "Users and roles", detail: "Invite people and set their role." },
                    { href: "/app/admin/tenant-policy", label: "Workspace policy", detail: "Rules that apply to everyone in this workspace." },
                  ]}
                />
              ) : null}
              {section === "products" ? (
                <LinkSection
                  onNavigate={onClose}
                  title="Products"
                  intro="The covers sold, their documents and claim stages."
                  links={[
                    { href: "/app/admin/products", label: "Products", detail: "Edit a product, its claim stages and required documents." },
                    { href: "/app/admin/policy-import", label: "Import policies", detail: "Upload a file of existing policies." },
                  ]}
                />
              ) : null}
              {section === "billing" ? (
                <LinkSection
                  onNavigate={onClose}
                  title="Usage and billing"
                  intro="How much the platform has been used and what it costs."
                  links={[{ href: "/app/admin/billing", label: "Billing", detail: "Usage against the plan, and invoices." }]}
                />
              ) : null}
            </motion.div>
          </AnimatePresence>
        </div>
      </div>
    </Modal>
  );
}

function LinkSection({ title, intro, links, onNavigate }: { title: string; intro: string; links: { href: string; label: string; detail: string }[]; onNavigate: () => void }) {
  return (
    <div>
      <h3 className="m-0 text-[18px] font-bold text-dls-text-primary">{title}</h3>
      <p className="m-0 mt-1 text-[13.5px] text-dls-text-secondary">{intro}</p>
      <ul className="m-0 mt-5 space-y-3 p-0">
        {links.map((link) => (
          <li key={link.href} className="list-none">
            <Link href={link.href} onClick={onNavigate} className="dls-focus-outline block rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 hover:border-dls-primary">
              <div className="text-[14.5px] font-bold text-dls-text-primary">{link.label}</div>
              <div className="mt-0.5 text-[13px] text-dls-text-secondary">{link.detail}</div>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}

function IntegrationsSection({ onNavigate }: { onNavigate: () => void }) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [view, setView] = useState<"catalog" | "yours">("catalog");
  const [query, setQuery] = useState("");
  const [states, setStates] = useState<Record<string, CardState> | null>(null);

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

  useEffect(() => { void load(); }, [load]);

  const stateOf = (item: Integration): CardState =>
    item.setupHref
      ? states?.[item.key] ?? { tone: "neutral", label: "Checking", detail: null, connected: false }
      : { tone: "neutral", label: "Not connected", detail: item.note ?? null, connected: false };

  const shown = useMemo(() => {
    const q = query.trim().toLowerCase();
    return CATALOG.filter((item) => {
      if (view === "yours" && !(states?.[item.key]?.connected ?? false)) return false;
      return !q || `${item.name} ${item.description}`.toLowerCase().includes(q);
    });
  }, [query, view, states]);

  return (
    <div>
      <h3 className="m-0 text-[18px] font-bold text-dls-text-primary">Integrations</h3>
      <p className="m-0 mt-1 text-[13.5px] text-dls-text-secondary">What the platform can connect to. Status is read from your workspace; nothing is shown as connected unless the provider confirmed it.</p>

      <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="inline-flex shrink-0 rounded-dls-pill border border-dls-border-input bg-dls-surface p-1" role="tablist" aria-label="Integration views">
          {([["catalog", "Catalog"], ["yours", "Yours"]] as const).map(([key, label]) => (
            <button
              key={key}
              type="button"
              role="tab"
              aria-selected={view === key}
              onClick={() => setView(key)}
              className={cn(
                "dls-focus-outline rounded-dls-pill px-4 py-1.5 text-[13px] font-semibold transition-colors duration-dls-fast",
                view === key ? "bg-dls-primary-soft-alt text-dls-primary-accent" : "text-dls-text-secondary hover:text-dls-text-primary",
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

      <div className="mt-5">
        {states === null ? (
          <LoadingSkeleton rows={4} />
        ) : shown.length === 0 ? (
          <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6 text-[13.5px] text-dls-text-secondary">
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
                  className="list-none rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4"
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  transition={{ duration: 0.15, delay: Math.min(index, 8) * 0.03 }}
                >
                  <div className="flex items-start gap-3">
                    <span className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-dls-control bg-dls-primary-soft text-dls-primary-accent">
                      <Icon className="h-5 w-5" aria-hidden="true" />
                    </span>
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <h4 className="m-0 text-[14.5px] font-bold text-dls-text-primary">{item.name}</h4>
                        <StatusPill tone={state.tone} label={state.label} confirmed={state.connected && state.tone === "success"} />
                      </div>
                      <p className="m-0 mt-1 text-[13px] leading-5 text-dls-text-secondary">{item.description}</p>
                      {state.detail ? <p className="m-0 mt-2 text-[12.5px] leading-5 text-dls-text-muted">{state.detail}</p> : null}
                      {item.setupHref ? (
                        <Link
                          href={item.setupHref}
                          onClick={onNavigate}
                          className="dls-focus-outline mt-3 inline-flex min-h-9 items-center rounded-dls-control border border-dls-border-input bg-dls-surface px-3 text-[13px] font-semibold text-dls-text-primary hover:bg-dls-bg-page"
                        >
                          {state.connected ? "Manage" : "Set up"}
                        </Link>
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
