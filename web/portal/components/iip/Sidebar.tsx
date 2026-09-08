"use client";
import Image from "next/image";
import { useEffect, useRef, useState } from "react";
import { NavLink, useLocation, Link } from "@/components/iip/NextRouterCompat";
import iipLogo from "@/assets/iip-logo";
import {
  MessageSquare, ListChecks, FileSearch, Inbox, FileText, GitBranch, LineChart,
  Settings, Search, LogOut, ShieldCheck,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useUI } from "@/context/UIStateContext";
import { runtimeInitials, useRuntimeContext } from "@/lib/runtimeContext";
import { canAccess, canAccessWithPermissions } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { signOut } from "@/lib/entraAuth";
import { Avatar } from "@/components/ui/Avatar";
import { Popover, PopoverGroupLabel } from "@/components/ui/Popover";
import RoleSwitcher from "./RoleSwitcher";

// Navigation model §5.2 — exhaustive. Adding an eighth requires removing one and amending
// docs/03-design-architecture/iip_ux_cognitive_load_and_navigation_model.md first.
const RAIL_DESTINATIONS = [
  { to: "/app", label: "Assistant", icon: MessageSquare },
  { to: "/app/work-items", label: "My work", icon: ListChecks },
  { to: "/app/claims", label: "Claims", icon: FileSearch },
  { to: "/app/channels", label: "Inbox", icon: Inbox },
  { to: "/app/documents", label: "Documents", icon: FileText },
  { to: "/app/flows", label: "Automation", icon: GitBranch },
  { to: "/app/executive", label: "Insights", icon: LineChart },
] as const;

function RailButton({
  to,
  label,
  icon: Icon,
  onNavigate,
}: {
  to: string;
  label: string;
  icon: typeof MessageSquare;
  onNavigate?: () => void;
}) {
  const loc = useLocation();
  const active = to === "/app" ? loc.pathname === "/app" : loc.pathname === to || loc.pathname.startsWith(to + "/");
  return (
    <NavLink
      to={to}
      end={to === "/app"}
      onClick={onNavigate}
      title={label}
      aria-label={label}
      className={cn(
        "group relative flex h-10 w-10 items-center justify-center rounded-dls-control transition-colors duration-dls-fast ease-dls",
        "dls-focus-outline",
        active ? "bg-dls-primary-soft" : "hover:bg-dls-bg-page",
      )}
    >
      <Icon
        className={cn("h-[19px] w-[19px]", active ? "text-dls-primary" : "text-dls-text-secondary")}
        strokeWidth={1.8}
        aria-hidden="true"
      />
    </NavLink>
  );
}

// Navigation model §5.2, §5.4: Settings, Search (command palette), and the user avatar, pinned to
// the bottom of the rail below the flexible gap.
export default function Sidebar({ modal = false, onNavigate }: { modal?: boolean; onNavigate?: () => void }) {
  const { roleDef } = useUI();
  const { runtimeContext, loading, parityMode } = useRuntimeContext();
  const usePreviewRole = canUseLocalTestDoubles || parityMode;
  const permissions = runtimeContext?.permissions ?? [];

  const destinations = RAIL_DESTINATIONS.filter((item) =>
    usePreviewRole ? canAccess(roleDef.key, item.to) : canAccessWithPermissions(permissions, item.to),
  );
  const settingsAvailable = usePreviewRole ? canAccess(roleDef.key, "/app/settings") : canAccessWithPermissions(permissions, "/app/settings");
  const adminAvailable = usePreviewRole ? canAccess(roleDef.key, "/app/admin") : canAccessWithPermissions(permissions, "/app/admin");
  const searchAvailable = usePreviewRole
    ? canAccess(roleDef.key, "/app/search")
    : canAccessWithPermissions(permissions, "/app/search");

  const activeWorkspace = runtimeContext?.availableWorkspaces.find(
    (workspace) => workspace.workspaceId === runtimeContext.workspaceId,
  );
  const workspaceName = activeWorkspace?.name ?? runtimeContext?.workspaceId ?? (loading ? "Resolving workspace" : "Workspace not configured");
  const displayName = runtimeContext?.displayName ?? (loading ? "Resolving user" : "User unavailable");
  const roleLabel = runtimeContext?.roleLabel ?? (loading ? "Authority resolving" : "Authority unavailable");
  const userInitials = runtimeInitials(displayName);
  const userId = runtimeContext?.userId ?? "unknown-user";

  const [searchOpen, setSearchOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [signingOut, setSigningOut] = useState(false);
  const searchTriggerRef = useRef<HTMLButtonElement>(null);
  const profileTriggerRef = useRef<HTMLButtonElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);

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
      await signOut();
    } finally {
      setSigningOut(false);
    }
  }

  return (
    <aside
      aria-label="Primary navigation"
      className="flex h-full w-16 flex-col items-center border-r border-dls-border-subtle bg-dls-surface py-3.5"
    >
      <Link to="/app" aria-label="Insurance Intelligence Platform" title="Insurance Intelligence Platform" className="mb-[18px] block h-8 w-8 shrink-0 overflow-hidden rounded-dls-control">
        <Image src={iipLogo.url} alt="" width={32} height={32} className="h-8 w-8 object-contain" priority />
      </Link>

      <div
        role="img"
        aria-label={`Workspace: ${workspaceName}`}
        title={workspaceName}
        className="mb-4 flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] bg-dls-primary-soft text-[13px] font-bold text-dls-primary"
      >
        {workspaceName.slice(0, 2).toUpperCase()}
      </div>

      <nav className="flex flex-1 flex-col items-center gap-1 overflow-y-auto">
        {destinations.map((item) => (
          <RailButton key={item.to} to={item.to} label={item.label} icon={item.icon} onNavigate={onNavigate} />
        ))}
      </nav>

      <div className="flex shrink-0 flex-col items-center gap-1 pt-2">
        {canUseLocalTestDoubles && (
          <div className="mb-1">
            <RoleSwitcher />
          </div>
        )}

        {settingsAvailable && (
          <NavLink
            to="/app/settings"
            onClick={onNavigate}
            title="Settings"
            aria-label="Settings"
            className="flex h-10 w-10 items-center justify-center rounded-dls-control text-dls-text-secondary transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page dls-focus-outline"
          >
            <Settings className="h-[19px] w-[19px]" strokeWidth={1.8} aria-hidden="true" />
          </NavLink>
        )}

        {searchAvailable && (
          <>
            <button
              ref={searchTriggerRef}
              type="button"
              title="Search (Ctrl+K)"
              aria-label="Search"
              onClick={() => setSearchOpen(true)}
              className="flex h-10 w-10 items-center justify-center rounded-dls-control text-dls-text-secondary transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page dls-focus-outline"
            >
              <Search className="h-[19px] w-[19px]" strokeWidth={1.8} aria-hidden="true" />
            </button>
            <Popover open={searchOpen} onClose={() => setSearchOpen(false)} anchorRef={searchTriggerRef} label="Search IIP">
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
          </>
        )}

        <button
          ref={profileTriggerRef}
          type="button"
          aria-haspopup="menu"
          aria-expanded={profileOpen}
          aria-label={`${displayName}. Signed in as ${roleLabel}. Open profile menu`}
          onClick={() => setProfileOpen((value) => !value)}
          className="mt-1.5 rounded-full dls-focus-outline"
        >
          <Avatar id={userId} initials={userInitials} presence />
        </button>
        <Popover open={profileOpen} onClose={() => setProfileOpen(false)} anchorRef={profileTriggerRef} label="Profile menu">
          <div className="border-b border-dls-border-subtle px-3 py-2.5">
            <p className="truncate text-[13px] font-bold text-dls-text-primary">{displayName}</p>
            <p className="truncate text-[11px] text-dls-text-muted">{roleLabel}</p>
            <p className="mt-0.5 truncate text-[10.5px] text-dls-text-muted">Workspace &middot; {workspaceName}</p>
          </div>
          <div className="py-1" role="menu" aria-label="Profile menu">
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
            <button
              type="button"
              role="menuitem"
              onClick={handleSignOut}
              disabled={signingOut || canUseLocalTestDoubles}
              title={canUseLocalTestDoubles ? "Local development identity — no session to sign out of." : undefined}
              className="flex w-full items-center gap-2.5 rounded-dls-control px-3 py-2 text-[13px] text-dls-text-primary hover:bg-dls-bg-page disabled:opacity-50"
            >
              <LogOut className="h-4 w-4 text-dls-text-muted" aria-hidden="true" /> {signingOut ? "Signing out…" : "Sign out"}
            </button>
          </div>
        </Popover>
      </div>
    </aside>
  );
}
