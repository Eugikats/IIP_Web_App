"use client";
import Image from "next/image";
import { useEffect, useRef, useState } from "react";
import { Link, useLocation } from "@/components/iip/NextRouterCompat";
import {
  BookOpen, ChevronDown, Compass, LifeBuoy, LogOut, Menu, MessageSquarePlus, Plug, Search, Settings, ShieldCheck, X,
} from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { cn } from "@/lib/utils";
import { useUI } from "@/context/UIStateContext";
import { runtimeInitials, useRuntimeContext } from "@/lib/runtimeContext";
import { canAccess, canAccessWithPermissions } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { signOut } from "@/lib/entraAuth";
import { signOutMfa } from "@/lib/security/mfa";
import { Avatar } from "@/components/ui/Avatar";
import { openFirstRunOrientation } from "./FirstRunOrientation";
import RecentChats from "./RecentChats";
import { MORE_TOOLS, RAIL_GROUPS, isActive } from "./Sidebar";
import type { SettingsSection } from "@/components/settings/SettingsModal";

// On a phone (below the md breakpoint) the 80px rail is replaced by what Claude and ChatGPT use: a slim top bar with a
// menu button, the screen's name and a new-chat button, and a drawer that slides in from the left holding everything the
// rail holds (destinations, More tools, recent chats, Manage, the profile menu). The destinations are Sidebar's own
// lists, so the rail and the drawer cannot disagree. The rail stays mounted but hidden because it owns the Settings
// dialog; the drawer opens that dialog through the same `iip:open-settings` event.

const ROW = "flex min-h-11 w-full items-center gap-3 rounded-dls-control px-3 text-left text-[14px] text-dls-text-primary hover:bg-dls-bg-page dls-focus-outline";
const LABEL = "px-3 pb-1 pt-3 text-[10.5px] font-bold uppercase tracking-[0.08em] text-dls-text-muted";

function openSettings(section: SettingsSection) {
  window.dispatchEvent(new CustomEvent("iip:open-settings", { detail: { section } }));
}

export default function MobileNav() {
  const { roleDef } = useUI();
  const { runtimeContext, loading, parityMode } = useRuntimeContext();
  const loc = useLocation();
  const usePreviewRole = canUseLocalTestDoubles || parityMode;
  const permissions = runtimeContext?.permissions ?? [];
  const can = (path: string) => (usePreviewRole ? canAccess(roleDef.key, path) : canAccessWithPermissions(permissions, path));

  const [open, setOpen] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [signingOut, setSigningOut] = useState(false);
  const drawerRef = useRef<HTMLDivElement>(null);
  const menuRef = useRef<HTMLButtonElement>(null);

  const primary = RAIL_GROUPS.flatMap((group) => group.items).filter((item) => can(item.to));
  const allSections = MORE_TOOLS
    .map((section) => ({ ...section, items: section.items.filter((item) => can(item.to)) }))
    .filter((section) => section.items.length > 0);
  // The organisation screens (overview, reports, conversations) are shown in the drawer itself, not behind More tools.
  const organisation = allSections.find((section) => section.title === "Organisation");
  const sections = allSections.filter((section) => section !== organisation);
  const everything = [...primary, ...(organisation?.items ?? []), ...sections.flatMap((section) => section.items)];
  const current = everything.find((item) => isActive(loc.pathname, item.to));
  const moreActive = sections.some((section) => section.items.some((item) => isActive(loc.pathname, item.to)));

  const displayName = runtimeContext?.displayName ?? (loading ? "Resolving user" : "User unavailable");
  const roleLabel = runtimeContext?.roleLabel ?? (loading ? "Authority resolving" : "Authority unavailable");
  const workspaceName =
    runtimeContext?.availableWorkspaces.find((workspace) => workspace.workspaceId === runtimeContext.workspaceId)?.name ??
    runtimeContext?.workspaceId ?? "";
  const userId = runtimeContext?.userId ?? "unknown-user";

  // Moving to another screen closes the drawer.
  useEffect(() => { setOpen(false); }, [loc.pathname]);

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") { setOpen(false); menuRef.current?.focus(); }
    };
    window.addEventListener("keydown", onKey);
    drawerRef.current?.focus();
    return () => window.removeEventListener("keydown", onKey);
  }, [open]);

  function submitSearch() {
    const text = query.trim();
    if (text.length < 2) return;
    setOpen(false);
    window.location.assign(`/app/search?q=${encodeURIComponent(text)}`);
  }

  async function handleSignOut() {
    setSigningOut(true);
    try {
      await signOutMfa().catch(() => undefined);
      await signOut();
    } finally {
      setSigningOut(false);
    }
  }

  const manage: { key: string; label: string; icon: typeof Plug; section: SettingsSection; show: boolean }[] = [
    { key: "integrations", label: "Integrations", icon: Plug, section: "integrations", show: can("/app/integrations") },
    { key: "dataHub", label: "Data Hub", icon: BookOpen, section: "dataHub", show: can("/app/settings") },
    { key: "settings", label: "Settings", icon: Settings, section: "profile", show: can("/app/settings") },
  ];

  return (
    <>
      <header
        data-testid="mobile-bar"
        className="flex h-12 shrink-0 items-center gap-1 border-b border-dls-border-subtle bg-dls-surface px-1.5 md:hidden"
      >
        <button
          ref={menuRef}
          type="button"
          aria-label="Open menu"
          aria-haspopup="dialog"
          aria-expanded={open}
          onClick={() => setOpen(true)}
          className="dls-focus-outline grid h-11 w-11 shrink-0 place-items-center rounded-dls-control text-dls-text-primary hover:bg-dls-bg-page"
        >
          <Menu className="h-[22px] w-[22px]" strokeWidth={1.8} aria-hidden="true" />
        </button>
        <div className="flex min-w-0 flex-1 items-center justify-center gap-2">
          <Image src={iipLogo.url} alt="" width={22} height={22} className="h-[22px] w-[22px] shrink-0 object-contain" priority />
          <span className="truncate text-[15px] font-semibold text-dls-text-primary">{current?.label ?? "IIP"}</span>
        </div>
        <Link
          to="/app"
          aria-label="New chat"
          className="dls-focus-outline grid h-11 w-11 shrink-0 place-items-center rounded-dls-control text-dls-primary hover:bg-dls-bg-page"
        >
          <MessageSquarePlus className="h-[22px] w-[22px]" strokeWidth={1.8} aria-hidden="true" />
        </Link>
      </header>

      {open && (
        <div className="fixed inset-0 z-50 md:hidden" data-testid="mobile-drawer">
          <button type="button" aria-label="Close menu" onClick={() => setOpen(false)} className="absolute inset-0 bg-dls-text-primary/30" tabIndex={-1} />
          <div
            ref={drawerRef}
            role="dialog"
            aria-modal="true"
            aria-label="Menu"
            tabIndex={-1}
            className="absolute inset-y-0 left-0 flex w-[86vw] max-w-[340px] flex-col border-r border-dls-border-subtle bg-dls-surface outline-none"
          >
            <div className="flex h-12 shrink-0 items-center justify-between border-b border-dls-border-subtle pl-4 pr-1.5">
              <span className="flex min-w-0 items-center gap-2">
                <Image src={iipLogo.url} alt="" width={24} height={24} className="h-6 w-6 shrink-0 object-contain" />
                <span className="truncate text-[14px] font-bold text-dls-text-primary">{workspaceName || "Insurance Intelligence Platform"}</span>
              </span>
              <button type="button" aria-label="Close menu" onClick={() => setOpen(false)} className="dls-focus-outline grid h-11 w-11 shrink-0 place-items-center rounded-dls-control text-dls-text-secondary hover:bg-dls-bg-page">
                <X className="h-5 w-5" aria-hidden="true" />
              </button>
            </div>

            <div className="min-h-0 flex-1 overflow-y-auto px-2 pb-3 pt-2">
              <Link to="/app" onClick={() => setOpen(false)} className={cn(ROW, "border border-dls-border-subtle font-semibold")}>
                <MessageSquarePlus className="h-[18px] w-[18px] shrink-0 text-dls-primary" aria-hidden="true" /> New chat
              </Link>

              {can("/app/search") && (
                <form className="px-1 pt-2" onSubmit={(event) => { event.preventDefault(); submitSearch(); }} role="search">
                  <label className="relative block">
                    <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-dls-text-muted" aria-hidden="true" />
                    <input
                      value={query}
                      onChange={(event) => setQuery(event.target.value)}
                      minLength={2}
                      maxLength={100}
                      enterKeyHint="search"
                      aria-label="Search query"
                      placeholder="Search claims, customers"
                      className="h-11 w-full rounded-dls-control border border-dls-border-input bg-dls-surface pl-9 pr-3 text-[16px] text-dls-text-primary placeholder:text-dls-text-muted focus:border-dls-primary focus:outline-none focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                    />
                  </label>
                </form>
              )}

              <p className={LABEL}>Work</p>
              {primary.map((item) => {
                const active = isActive(loc.pathname, item.to);
                return (
                  <Link
                    key={item.to}
                    to={item.to}
                    onClick={() => setOpen(false)}
                    aria-current={active ? "page" : undefined}
                    className={cn(ROW, active && "bg-dls-primary-soft font-semibold text-dls-primary")}
                  >
                    <item.icon className={cn("h-[18px] w-[18px] shrink-0", active ? "text-dls-primary" : "text-dls-text-muted")} aria-hidden="true" />
                    <span className="truncate">{item.label}</span>
                  </Link>
                );
              })}

              {organisation && (
                <>
                  <p className={LABEL}>Organisation</p>
                  {organisation.items.map((item) => {
                    const active = isActive(loc.pathname, item.to, item.to === "/app/platform");
                    return (
                      <Link
                        key={item.to}
                        to={item.to}
                        onClick={() => setOpen(false)}
                        aria-current={active ? "page" : undefined}
                        className={cn(ROW, active && "bg-dls-primary-soft font-semibold text-dls-primary")}
                      >
                        <item.icon className={cn("h-[18px] w-[18px] shrink-0", active ? "text-dls-primary" : "text-dls-text-muted")} aria-hidden="true" />
                        <span className="truncate">{item.label}</span>
                      </Link>
                    );
                  })}
                </>
              )}

              {sections.length > 0 && (
                <>
                  <button
                    type="button"
                    aria-expanded={moreOpen || moreActive}
                    onClick={() => setMoreOpen((value) => !value)}
                    className={cn(ROW, "justify-between", moreActive && "text-dls-primary")}
                  >
                    <span>More tools</span>
                    <ChevronDown className={cn("h-4 w-4 text-dls-text-muted transition-transform", (moreOpen || moreActive) && "rotate-180")} aria-hidden="true" />
                  </button>
                  {(moreOpen || moreActive) && sections.map((section) => (
                    <div key={section.title}>
                      <p className={LABEL}>{section.title}</p>
                      {section.items.map((item) => {
                        const active = isActive(loc.pathname, item.to);
                        return (
                          <Link
                            key={item.to}
                            to={item.to}
                            onClick={() => setOpen(false)}
                            aria-current={active ? "page" : undefined}
                            className={cn(ROW, active && "bg-dls-primary-soft font-semibold text-dls-primary")}
                          >
                            <item.icon className={cn("h-[18px] w-[18px] shrink-0", active ? "text-dls-primary" : "text-dls-text-muted")} aria-hidden="true" />
                            <span className="truncate">{item.label}</span>
                          </Link>
                        );
                      })}
                    </div>
                  ))}
                </>
              )}

              <div className="mt-3">
                <RecentChats expanded onNavigate={() => setOpen(false)} />
              </div>

              <div className="mt-3 border-t border-dls-border-subtle pt-1">
                <p className={LABEL}>Manage</p>
                {manage.filter((row) => row.show).map((row) => (
                  <button key={row.key} type="button" className={ROW} onClick={() => { setOpen(false); openSettings(row.section); }}>
                    <row.icon className="h-[18px] w-[18px] shrink-0 text-dls-text-muted" aria-hidden="true" />
                    {row.label}
                  </button>
                ))}
                {can("/app/support") && (
                  <Link to="/app/support" onClick={() => setOpen(false)} className={ROW}>
                    <LifeBuoy className="h-[18px] w-[18px] shrink-0 text-dls-text-muted" aria-hidden="true" /> Support
                  </Link>
                )}
                {can("/app/admin") && (
                  <Link to="/app/admin" onClick={() => setOpen(false)} className={ROW}>
                    <ShieldCheck className="h-[18px] w-[18px] shrink-0 text-dls-text-muted" aria-hidden="true" /> Admin console
                  </Link>
                )}
                <button
                  type="button"
                  className={ROW}
                  onClick={() => {
                    openFirstRunOrientation();
                    setOpen(false);
                    window.location.assign("/app");
                  }}
                >
                  <Compass className="h-[18px] w-[18px] shrink-0 text-dls-text-muted" aria-hidden="true" /> Show me around
                </button>
              </div>
            </div>

            <div className="flex shrink-0 items-center gap-3 border-t border-dls-border-subtle px-3 pb-3 pt-2.5">
              <Avatar id={userId} initials={runtimeInitials(displayName)} presence />
              <div className="min-w-0 flex-1">
                <p className="truncate text-[13px] font-bold text-dls-text-primary">{displayName}</p>
                <p className="truncate text-[11px] text-dls-text-muted">{roleLabel}</p>
              </div>
              <button
                type="button"
                aria-label={signingOut ? "Signing out" : "Sign out"}
                title="Sign out"
                onClick={handleSignOut}
                disabled={signingOut || canUseLocalTestDoubles}
                className="dls-focus-outline grid h-11 w-11 shrink-0 place-items-center rounded-dls-control text-dls-text-secondary hover:bg-dls-bg-page disabled:opacity-50"
              >
                <LogOut className="h-[18px] w-[18px]" aria-hidden="true" />
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
