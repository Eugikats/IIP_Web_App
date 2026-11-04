"use client";
import Image from "next/image";
import { useEffect, useRef, useState } from "react";
import { NavLink, useLocation, Link } from "@/components/iip/NextRouterCompat";
import iipLogo from "@/assets/iip-logo";
import {
  MessageSquare, ListChecks, FileSearch, Inbox, GraduationCap, FileText, GitBranch, LineChart,
  Users, Building2, Mail, BookOpen, ShieldAlert, Gavel, ScrollText,
  Search, LogOut, ShieldCheck, Plug, LifeBuoy, Settings,
  ChevronsLeft, ChevronsRight, Compass, Home, RefreshCw, MonitorDot,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useUI } from "@/context/UIStateContext";
import { runtimeInitials, useRuntimeContext } from "@/lib/runtimeContext";
import { canAccess, canAccessWithPermissions } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { signOut } from "@/lib/entraAuth";
import { signOutMfa } from "@/lib/security/mfa";
import { Avatar } from "@/components/ui/Avatar";
import { Popover, PopoverGroupLabel } from "@/components/ui/Popover";
import RoleSwitcher from "./RoleSwitcher";
import { openFirstRunOrientation } from "./FirstRunOrientation";
import { SettingsModal, type SettingsSection } from "@/components/settings/SettingsModal";
import RecentChats from "./RecentChats";

const EXPANDED_STORAGE_KEY = "iip.sidebar.expanded";
const HOVER_STORAGE_KEY = "iip.sidebar.expandOnHover";
// How long the pointer may be away before a peeking sidebar closes, so brushing past its edge does
// not make it flicker.
const PEEK_CLOSE_DELAY_MS = 180;

// Grouped rail destinations (navigation model §5.2, amended, see
// docs/03-design-architecture/iip_ux_cognitive_load_and_navigation_model.md §5.2 for why: the
// document's original plan nested these as tabs within a seven-item rail; that nesting was never
// built, so these routes existed and worked but had no link pointing at them anywhere in the app.
// An expandable, grouped rail reaches all of them without abandoning a compact default.
const RAIL_GROUPS: { label: string; items: { to: string; label: string; icon: typeof MessageSquare }[] }[] = [
  {
    label: "Work",
    items: [
      { to: "/app", label: "Brief", icon: Home },
      { to: "/app/claims", label: "Claims", icon: FileSearch },
      { to: "/app/acquisition", label: "Acquisition", icon: Building2 },
      { to: "/app/channels", label: "Inbox", icon: Inbox },
      { to: "/academy", label: "IIP Academy", icon: GraduationCap },
    ],
  },
  {
    label: "Data",
    items: [{ to: "/app/knowledge", label: "Data Hub", icon: BookOpen }],
  },
];

// Everything else is one step away in the profile menu, so the rail stays short (as in CIP) and no
// screen is unreachable.
const MORE_DESTINATIONS: { to: string; label: string; icon: typeof MessageSquare }[] = [
  { to: "/app/assistant", label: "Assistant", icon: MessageSquare },
  { to: "/app/brief", label: "My day", icon: Home },
  { to: "/app/work-items", label: "My work", icon: ListChecks },
  { to: "/app/customers", label: "Customers", icon: Users },
  { to: "/app/communications", label: "Communications", icon: Mail },
  { to: "/app/retention", label: "Retention", icon: RefreshCw },
  { to: "/app/documents", label: "Documents", icon: FileText },
  { to: "/app/policy", label: "Policy", icon: ScrollText },
  { to: "/app/fraud", label: "Fraud", icon: ShieldAlert },
  { to: "/app/underwriting", label: "Underwriting", icon: Gavel },
  { to: "/app/flows", label: "Automation", icon: GitBranch },
  { to: "/app/executive", label: "Insights", icon: LineChart },
  { to: "/app/audit", label: "Audit", icon: ShieldCheck },
];

function isActive(pathname: string, to: string) {
  return to === "/app" ? pathname === "/app" : pathname === to || pathname.startsWith(to + "/");
}

function ModalRailButton({ label, icon: Icon, expanded, onClick }: { label: string; icon: typeof MessageSquare; expanded: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={expanded ? undefined : label}
      aria-label={label}
      aria-haspopup="dialog"
      className={cn(
        "group relative flex h-10 shrink-0 items-center gap-2.5 rounded-dls-control transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page dls-focus-outline",
        expanded ? "w-full px-2.5" : "w-10 justify-center",
      )}
    >
      <Icon className="h-[19px] w-[19px] shrink-0 text-dls-text-secondary" strokeWidth={1.8} aria-hidden="true" />
      {expanded && <span className="animate-label-in truncate text-[13px] font-semibold text-dls-text-secondary">{label}</span>}
    </button>
  );
}

function RailButton({
  to, label, icon: Icon, expanded, onNavigate,
}: {
  to: string; label: string; icon: typeof MessageSquare; expanded: boolean; onNavigate?: () => void;
}) {
  const loc = useLocation();
  const active = isActive(loc.pathname, to);
  return (
    <NavLink
      to={to}
      end={to === "/app"}
      onClick={onNavigate}
      title={expanded ? undefined : label}
      aria-label={label}
      // Anchor for the first-run orientation (iip_first_run_orientation_spec.md). Keyed on the
      // route rather than the label, so renaming a destination cannot silently break the tour.
      data-tour={to}
      className={cn(
        "group relative flex h-10 shrink-0 items-center gap-2.5 rounded-dls-control transition-colors duration-dls-fast ease-dls",
        "dls-focus-outline",
        expanded ? "w-full px-2.5" : "w-10 justify-center",
        active ? "bg-dls-primary-soft" : "hover:bg-dls-bg-page",
      )}
    >
      <Icon
        className={cn("h-[19px] w-[19px] shrink-0", active ? "text-dls-primary" : "text-dls-text-secondary")}
        strokeWidth={1.8}
        aria-hidden="true"
      />
      {expanded && (
        <span className={cn("animate-label-in truncate text-[13px] font-semibold", active ? "text-dls-primary" : "text-dls-text-secondary")}>
          {label}
        </span>
      )}
    </NavLink>
  );
}

// Navigation model §5.2, §5.4, §10.4 (readiness blueprint): only the avatar is pinned at the
// bottom now, Settings, Search, Integrations, Support, and Admin console all live in the profile
// menu behind it. The previous three-control pinned cluster (Settings icon, Search icon, avatar)
// left too little room for the scrollable destination list on short viewports, and a saved-but-
// hard-to-notice overflow read as "some icons cannot be reached."
export default function Sidebar({ modal = false, onNavigate }: { modal?: boolean; onNavigate?: () => void }) {
  const { roleDef } = useUI();
  const { runtimeContext, loading, parityMode } = useRuntimeContext();
  const usePreviewRole = canUseLocalTestDoubles || parityMode;
  const permissions = runtimeContext?.permissions ?? [];
  const loc = useLocation();

  const can = (path: string) => (usePreviewRole ? canAccess(roleDef.key, path) : canAccessWithPermissions(permissions, path));

  const groups = RAIL_GROUPS
    .map((group) => ({ ...group, items: group.items.filter((item) => can(item.to)) }))
    .filter((group) => group.items.length > 0);

  const settingsAvailable = can("/app/settings");
  const adminAvailable = can("/app/admin");
  const integrationsAvailable = can("/app/integrations");
  const supportAvailable = can("/app/support");
  const searchAvailable = can("/app/search");

  const activeWorkspace = runtimeContext?.availableWorkspaces.find(
    (workspace) => workspace.workspaceId === runtimeContext.workspaceId,
  );
  const workspaceName = activeWorkspace?.name ?? runtimeContext?.workspaceId ?? (loading ? "Resolving workspace" : "Workspace not configured");
  const displayName = runtimeContext?.displayName ?? (loading ? "Resolving user" : "User unavailable");
  const roleLabel = runtimeContext?.roleLabel ?? (loading ? "Authority resolving" : "Authority unavailable");
  const userInitials = runtimeInitials(displayName);
  const userId = runtimeContext?.userId ?? "unknown-user";

  // `pinned` is the saved choice (expanded or the 64px rail). `peek` is a temporary opening of the
  // rail while the pointer is over it, so the page does not shift. `expanded` is what is drawn.
  const [pinned, setExpanded] = useState(false);
  const [peek, setPeek] = useState(false);
  const [expandOnHover, setExpandOnHover] = useState(true);
  const peekTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const asideRef = useRef<HTMLElement>(null);
  const [expandedLoaded, setExpandedLoaded] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [settingsSection, setSettingsSection] = useState<SettingsSection>("integrations");
  const openSettings = (section: SettingsSection) => { setSettingsSection(section); setSettingsOpen(true); };

  // Other parts of the app (the set-up prompt for a second step) ask for a Settings section to be opened.
  useEffect(() => {
    const onOpen = (event: Event) => {
      const section = (event as CustomEvent<{ section?: SettingsSection }>).detail?.section;
      if (section) { setSettingsSection(section); setSettingsOpen(true); }
    };
    window.addEventListener("iip:open-settings", onOpen);
    return () => window.removeEventListener("iip:open-settings", onOpen);
  }, []);
  const expanded = pinned || peek;
  const [searchQuery, setSearchQuery] = useState("");
  const [signingOut, setSigningOut] = useState(false);
  const [scrollClipped, setScrollClipped] = useState<{ top: boolean; bottom: boolean }>({ top: false, bottom: false });
  const [supportsHover, setSupportsHover] = useState(false);
  const profileTriggerRef = useRef<HTMLButtonElement>(null);
  const searchAnchorRef = useRef<HTMLButtonElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const navRef = useRef<HTMLElement>(null);

  // The approved shell starts with the compact 64px rail at every viewport width. Users can
  // expand it when they need visible destination labels; the saved choice is restored on return.
  useEffect(() => {
    try {
      const stored = window.localStorage.getItem(EXPANDED_STORAGE_KEY);
      // Narrow screens start with the 64px rail so the page keeps its width; a saved choice always wins.
      if (stored !== null) setExpanded(stored === "true");
      else if (window.matchMedia("(max-width: 1023px)").matches) setExpanded(false);
      setExpandOnHover(window.localStorage.getItem(HOVER_STORAGE_KEY) !== "false");
    } catch {
      // Private browsing / blocked storage, keep the default.
    }
    setExpandedLoaded(true);
  }, []);

  useEffect(() => {
    const media = window.matchMedia("(hover: hover) and (pointer: fine)");
    const update = () => setSupportsHover(media.matches);
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);

  function toggleExpanded() {
    setExpanded((current) => {
      const next = !current;
      try {
        window.localStorage.setItem(EXPANDED_STORAGE_KEY, String(next));
      } catch {
        // Not persisted this session, still applied to the live view.
      }
      return next;
    });
  }

  function toggleExpandOnHover() {
    setExpandOnHover((current) => {
      const next = !current;
      try {
        window.localStorage.setItem(HOVER_STORAGE_KEY, String(next));
      } catch {
        // Not persisted this session, still applied to the live view.
      }
      if (!next) setPeek(false);
      return next;
    });
  }

  function openPeek() {
    if (peekTimer.current) clearTimeout(peekTimer.current);
    if (supportsHover && expandOnHover && !pinned) setPeek(true);
  }

  function closePeekSoon() {
    if (peekTimer.current) clearTimeout(peekTimer.current);
    peekTimer.current = setTimeout(() => setPeek(false), PEEK_CLOSE_DELAY_MS);
  }

  // A menu, the search box or the settings dialog opened from the sidebar sits outside it, so the
  // pointer leaving must not close the sidebar underneath it. A click anywhere else closes at once.
  const holdingOpen = profileOpen || searchOpen || settingsOpen;
  useEffect(() => {
    if (!peek) return;
    function onPointerDown(event: PointerEvent) {
      if (holdingOpen) return;
      if (asideRef.current && !asideRef.current.contains(event.target as Node)) setPeek(false);
    }
    document.addEventListener("pointerdown", onPointerDown);
    return () => document.removeEventListener("pointerdown", onPointerDown);
  }, [peek, holdingOpen]);

  useEffect(() => () => {
    if (peekTimer.current) clearTimeout(peekTimer.current);
  }, []);

  // Clipping-fade affordance (blueprint §10.4): only shown when the destination list is actually
  // taller than its scroll container, so a short list never shows a fade with nothing to reveal.
  useEffect(() => {
    const el = navRef.current;
    if (!el) return;
    function update() {
      if (!el) return;
      setScrollClipped({
        top: el.scrollTop > 2,
        bottom: el.scrollTop + el.clientHeight < el.scrollHeight - 2,
      });
    }
    update();
    el.addEventListener("scroll", update);
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => {
      el.removeEventListener("scroll", update);
      observer.disconnect();
    };
  }, [groups.length, expanded]);

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (searchAvailable && (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setSearchOpen(true);
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [searchAvailable]);

  function submitSearch() {
    const query = searchQuery.trim();
    if (query.length < 2) return;
    setSearchOpen(false);
    setSearchQuery("");
    onNavigate?.();
    window.location.assign(`/app/search?q=${encodeURIComponent(query)}`);
  }

  async function handleSignOut() {
    setSigningOut(true);
    try {
      // Ends the verified state on the server first, so the next sign-in on any device asks again.
      await signOutMfa().catch(() => undefined);
      await signOut();
    } finally {
      setSigningOut(false);
    }
  }

  return (
    // The wrapper keeps the page's share of the width at the pinned size; the sidebar itself is
    // positioned over the page, so a peek slides over the content instead of pushing it.
    <div className={cn("relative h-full shrink-0 transition-[width] duration-dls-panel ease-dls", pinned ? "w-60" : "w-16", !expandedLoaded && "invisible")}>
    <aside
      ref={asideRef}
      aria-label="Primary navigation"
      onPointerEnter={(event) => { if (event.pointerType === "mouse") openPeek(); }}
      onPointerLeave={(event) => { if (event.pointerType === "mouse" && !holdingOpen) closePeekSoon(); }}
      // Keyboard focus opens the rail; a tap does not, or the icons would move under a finger mid-tap.
      onFocusCapture={(event) => { if ((event.target as HTMLElement).matches(":focus-visible")) openPeek(); }}
      onBlurCapture={(event) => {
        if (!holdingOpen && !event.currentTarget.contains(event.relatedTarget as Node | null)) closePeekSoon();
      }}
      className={cn(
        "absolute inset-y-0 left-0 z-30 flex flex-col border-r border-dls-border-subtle bg-dls-surface py-3.5 transition-[width,box-shadow] duration-dls-panel ease-dls",
        expanded ? "w-60 items-stretch px-3" : "w-16 items-center",
        peek && !pinned && "shadow-dls-popover",
      )}
    >
      <div className={cn("mb-[18px] flex shrink-0 items-center", expanded ? "justify-between px-0.5" : "justify-center")}>
        <Link to="/app" aria-label="Insurance Intelligence Platform" title="Insurance Intelligence Platform" className="block h-8 w-8 shrink-0 overflow-hidden rounded-dls-control">
          <Image src={iipLogo.url} alt="" width={32} height={32} className="h-8 w-8 object-contain" priority />
        </Link>
        {expanded && (
          <button
            type="button"
            onClick={toggleExpanded}
            title="Collapse navigation"
            aria-label="Collapse navigation"
            className="dls-focus-outline flex h-7 w-7 items-center justify-center rounded-dls-control text-dls-text-muted hover:bg-dls-bg-page hover:text-dls-text-secondary"
          >
            <ChevronsLeft className="h-4 w-4" aria-hidden="true" />
          </button>
        )}
      </div>

      {!expanded && (
        <button
          type="button"
          onClick={toggleExpanded}
          title="Expand navigation"
          aria-label="Expand navigation"
          className="dls-focus-outline mb-2.5 flex h-7 w-7 shrink-0 items-center justify-center self-center rounded-dls-control text-dls-text-muted hover:bg-dls-bg-page hover:text-dls-text-secondary"
        >
          <ChevronsRight className="h-4 w-4" aria-hidden="true" />
        </button>
      )}

      <div
        role="img"
        aria-label={`Workspace: ${workspaceName}`}
        title={workspaceName}
        className={cn(
          "mb-4 flex h-10 shrink-0 items-center rounded-[10px] bg-dls-primary-soft text-[13px] font-bold text-dls-primary",
          expanded ? "gap-2.5 px-2.5" : "w-10 justify-center",
        )}
      >
        <span className={cn(!expanded && "sr-only", expanded && "grid h-6 w-6 shrink-0 place-items-center rounded-dls-chip border border-dls-border-subtle bg-dls-surface text-[11px]")}>
          {workspaceName.slice(0, 2).toUpperCase()}
        </span>
        {expanded && <span className="animate-label-in truncate text-dls-text-primary">{workspaceName}</span>}
      </div>

      <div className="relative flex min-h-0 flex-1 flex-col">
        {scrollClipped.top && (
          <div className="pointer-events-none absolute inset-x-0 top-0 z-10 h-4 bg-gradient-to-b from-dls-surface to-transparent" aria-hidden="true" />
        )}
        <nav
          ref={navRef}
          className={cn("flex min-h-0 flex-1 flex-col overflow-y-auto", expanded ? "items-stretch gap-4" : "items-center gap-1")}
        >
          {groups.map((group) => (
            <div key={group.label} className={cn("flex flex-col", expanded ? "gap-0.5" : "contents")}>
              {expanded && (
                <div className="animate-label-in px-2.5 pb-0.5 text-[10.5px] font-bold uppercase tracking-[0.08em] text-dls-text-muted">
                  {group.label}
                </div>
              )}
              {group.items.map((item) => (
                <RailButton key={item.to} to={item.to} label={item.label} icon={item.icon} expanded={expanded} onNavigate={onNavigate} />
              ))}
              {!expanded && <div className="my-1 h-px w-6 bg-dls-border-subtle" aria-hidden="true" />}
            </div>
          ))}
          {integrationsAvailable || settingsAvailable ? (
            <div className={cn("flex flex-col", expanded ? "gap-0.5" : "contents")}>
              {expanded && (
                <div className="animate-label-in px-2.5 pb-0.5 text-[10.5px] font-bold uppercase tracking-[0.08em] text-dls-text-muted">
                  Manage
                </div>
              )}
              {integrationsAvailable && (
                <ModalRailButton label="Integrations" icon={Plug} expanded={expanded} onClick={() => openSettings("integrations")} />
              )}
              {settingsAvailable && (
                <ModalRailButton label="Settings" icon={Settings} expanded={expanded} onClick={() => openSettings("team")} />
              )}
            </div>
          ) : null}
          {/* Inside the same scrolling column as the destinations, so a long chat list or a short window scrolls
              instead of squeezing the destinations down to a sliver. */}
          <RecentChats expanded={expanded} onNavigate={onNavigate} />
        </nav>
        {scrollClipped.bottom && (
          <div className="pointer-events-none absolute inset-x-0 bottom-0 z-10 h-4 bg-gradient-to-t from-dls-surface to-transparent" aria-hidden="true" />
        )}
      </div>

      <div className={cn("flex shrink-0 flex-col pt-2", expanded ? "items-stretch gap-1" : "items-center gap-1")}>
        {canUseLocalTestDoubles && (
          <div className={cn("mb-1", expanded && "px-0.5")}>
            <RoleSwitcher />
          </div>
        )}

        <button
          ref={searchAnchorRef}
          type="button"
          onClick={() => setSearchOpen(true)}
          className="hidden"
          aria-hidden="true"
          tabIndex={-1}
        />

        <button
          ref={profileTriggerRef}
          type="button"
          data-tour="profile"
          aria-haspopup="menu"
          aria-expanded={profileOpen}
          aria-label={`${displayName}. Signed in as ${roleLabel}. Open profile menu`}
          onClick={() => setProfileOpen((value) => !value)}
          className={cn(
            "dls-focus-outline flex items-center gap-2.5 rounded-dls-control hover:bg-dls-bg-page",
            expanded ? "w-full px-2 py-1.5" : "rounded-full",
          )}
        >
          <Avatar id={userId} initials={userInitials} presence />
          {expanded && (
            <span className="min-w-0 flex-1 text-left">
              <span className="block truncate text-[12.5px] font-bold text-dls-text-primary">{displayName}</span>
              <span className="block truncate text-[11px] text-dls-text-muted">{roleLabel}</span>
            </span>
          )}
        </button>
        <Popover open={profileOpen} onClose={() => setProfileOpen(false)} anchorRef={profileTriggerRef} label="Profile menu">
          <div className="border-b border-dls-border-subtle px-3 py-2.5">
            <p className="truncate text-[13px] font-bold text-dls-text-primary">{displayName}</p>
            <p className="truncate text-[11px] text-dls-text-muted">Signed in as {roleLabel}</p>
            <p className="mt-0.5 truncate text-[10.5px] text-dls-text-muted">Workspace &middot; {workspaceName}</p>
          </div>
          <div className="py-1" role="menu" aria-label="Profile menu">
            {searchAvailable && (
              <button
                type="button"
                role="menuitem"
                onClick={() => { setProfileOpen(false); setSearchOpen(true); }}
                className="flex w-full items-center justify-between rounded-dls-control px-3 py-2 text-[13px] text-dls-text-primary hover:bg-dls-bg-page"
              >
                <span className="flex items-center gap-2.5"><Search className="h-4 w-4 text-dls-text-muted" aria-hidden="true" /> Search</span>
                <span className="text-[10.5px] text-dls-text-muted">Ctrl+K</span>
              </button>
            )}
            {settingsAvailable && (
              <button type="button" onClick={() => { setProfileOpen(false); openSettings("team"); }} role="menuitem" className="flex w-full items-center gap-2.5 rounded-dls-control px-3 py-2 text-[13px] text-dls-text-primary hover:bg-dls-bg-page">
                <Settings className="h-4 w-4 text-dls-text-muted" aria-hidden="true" /> Settings
              </button>
            )}
            <button
              type="button"
              role="menuitemcheckbox"
              aria-checked={expandOnHover}
              onClick={toggleExpandOnHover}
              className="flex w-full items-center justify-between gap-2.5 rounded-dls-control px-3 py-2 text-[13px] text-dls-text-primary hover:bg-dls-bg-page"
            >
              <span className="flex items-center gap-2.5"><ChevronsRight className="h-4 w-4 text-dls-text-muted" aria-hidden="true" /> Open sidebar on hover</span>
              <span className={cn("inline-flex h-4 w-7 shrink-0 items-center rounded-full border p-0.5 transition-colors duration-dls-fast", expandOnHover ? "border-dls-primary bg-dls-primary" : "border-dls-border-input bg-dls-surface")} aria-hidden="true">
                <span className={cn("h-2.5 w-2.5 rounded-full transition-transform duration-dls-fast", expandOnHover ? "translate-x-3 bg-dls-surface" : "translate-x-0 bg-dls-text-faint")} />
              </span>
            </button>
            <div className="my-1 border-t border-dls-border-subtle" role="separator" />
            <div className="px-3 pb-1 pt-1 text-[10.5px] font-bold uppercase tracking-[0.08em] text-dls-text-muted">More screens</div>
            <div className="grid max-h-56 grid-cols-2 gap-x-1 overflow-y-auto px-1">
              {MORE_DESTINATIONS.filter((item) => can(item.to)).map((item) => (
                <Link key={item.to} to={item.to} onClick={() => setProfileOpen(false)} role="menuitem" className="flex items-center gap-2 rounded-dls-control px-2 py-1.5 text-[12.5px] text-dls-text-primary hover:bg-dls-bg-page">
                  <item.icon className="h-3.5 w-3.5 shrink-0 text-dls-text-muted" aria-hidden="true" /> <span className="truncate">{item.label}</span>
                </Link>
              ))}
            </div>
            <div className="my-1 border-t border-dls-border-subtle" role="separator" />
            {supportAvailable && (
              <Link to="/app/support" onClick={() => setProfileOpen(false)} role="menuitem" className="flex items-center gap-2.5 rounded-dls-control px-3 py-2 text-[13px] text-dls-text-primary hover:bg-dls-bg-page">
                <LifeBuoy className="h-4 w-4 text-dls-text-muted" aria-hidden="true" /> Support
              </Link>
            )}
            {adminAvailable && (
              <Link
                to="/app/admin"
                onClick={() => setProfileOpen(false)}
                role="menuitem"
                className="flex items-center gap-2.5 rounded-dls-control px-3 py-2 text-[13px] text-dls-text-primary hover:bg-dls-bg-page"
              >
                <ShieldCheck className="h-4 w-4 text-dls-text-muted" aria-hidden="true" /> Admin console
              </Link>
            )}
            {/* The only way the first-run orientation ever opens (outcome J: off by default,
                available from here). Reloading is how it picks the request back up -- it
                evaluates once on mount by design, so it can never re-trigger itself mid-session. */}
            <button
              type="button"
              role="menuitem"
              onClick={() => {
                openFirstRunOrientation();
                setProfileOpen(false);
                window.location.assign("/app");
              }}
              className="flex w-full items-center gap-2.5 rounded-dls-control px-3 py-2 text-[13px] text-dls-text-primary hover:bg-dls-bg-page"
            >
              <Compass className="h-4 w-4 text-dls-text-muted" aria-hidden="true" /> Show me around
            </button>
            <button
              type="button"
              role="menuitem"
              onClick={handleSignOut}
              disabled={signingOut || canUseLocalTestDoubles}
              title={canUseLocalTestDoubles ? "Local development identity, no session to sign out of." : undefined}
              className="flex w-full items-center gap-2.5 rounded-dls-control px-3 py-2 text-[13px] text-dls-text-primary hover:bg-dls-bg-page disabled:opacity-50"
            >
              <LogOut className="h-4 w-4 text-dls-text-muted" aria-hidden="true" /> {signingOut ? "Signing out…" : "Sign out"}
            </button>
          </div>
        </Popover>

        {searchAvailable && (
          <Popover open={searchOpen} onClose={() => setSearchOpen(false)} anchorRef={searchAnchorRef} label="Search IIP">
            <PopoverGroupLabel>Search IIP</PopoverGroupLabel>
            <div className="px-2 pb-2">
              <input
                ref={searchInputRef}
                autoFocus
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") submitSearch();
                }}
                minLength={2}
                maxLength={100}
                placeholder="Search claims, customers, documents"
                aria-label="Search query"
                className="w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-sm text-dls-text-primary placeholder:text-dls-text-muted focus:border-dls-primary focus:outline-none focus:ring-[3px] focus:ring-dls-primary/[0.12]"
              />
            </div>
          </Popover>
        )}
      </div>
      <SettingsModal open={settingsOpen} section={settingsSection} onSectionChange={setSettingsSection} onClose={() => setSettingsOpen(false)} />
    </aside>
    </div>
  );
}
