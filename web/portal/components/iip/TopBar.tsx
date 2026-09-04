import { FormEvent, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Search, PanelLeftClose, PanelRightClose } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import RoleSwitcher from "./RoleSwitcher";
import ProfileMenu from "./ProfileMenu";
import NotificationBell from "./NotificationBell";
import { runtimeInitials, useRuntimeContext } from "@/lib/runtimeContext";
import { canUseLocalTestDoubles } from "@/lib/config";
import { canUseGlobalSearchPreview, searchPreviewGuidance } from "@/lib/roles";

export default function TopBar() {
  const router = useRouter();
  const searchInput = useRef<HTMLInputElement>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const { sidebarOpen, copilotOpen, toggleSidebar, toggleCopilot, roleDef } = useUI();
  const { runtimeContext, loading, parityMode } = useRuntimeContext();
  const activeWorkspace = runtimeContext?.availableWorkspaces.find(
    (workspace) => workspace.workspaceId === runtimeContext.workspaceId,
  );
  const workspaceName = activeWorkspace?.name ?? runtimeContext?.workspaceId ?? (loading ? "Resolving workspace" : "Workspace unavailable");
  const displayName = runtimeContext?.displayName ?? (loading ? "Resolving user" : "User unavailable");
  const roleLabel = runtimeContext?.roleLabel ?? (loading ? "Authority resolving" : "Authority unavailable");
  const userInitials = runtimeInitials(displayName);
  const searchAvailable = parityMode
    ? false
    : canUseLocalTestDoubles
      ? canUseGlobalSearchPreview(roleDef.key)
      : (runtimeContext?.permissions ?? []).some((permission) => permission === "ViewAssignedClaims" || permission === "ViewAllClaims");
  const searchGuidance = parityMode
    ? searchPreviewGuidance(roleDef.key, true)
    : canUseLocalTestDoubles
      ? searchPreviewGuidance(roleDef.key, false)
      : `Your signed-in role (${roleLabel}) does not include global search. Ask a workspace administrator if your work requires it.`;

  useEffect(() => {
    function focusSearch(event: KeyboardEvent) {
      if (searchAvailable && (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        searchInput.current?.focus();
      }
    }
    window.addEventListener("keydown", focusSearch);
    return () => window.removeEventListener("keydown", focusSearch);
  }, [searchAvailable]);

  function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = searchQuery.trim();
    if (!searchAvailable || query.length < 2) return;
    router.push(`/app/search?q=${encodeURIComponent(query)}`);
  }

  return (
    <header className="min-h-16 shrink-0 border-b border-line/70 bg-white/60 px-2 backdrop-blur-xl sm:px-4 flex items-center gap-1.5 sm:gap-3">
      <div className="flex items-center gap-1">
        {sidebarOpen && (
          <button
            onClick={toggleSidebar}
            aria-label="Collapse sidebar from header"
            className="h-11 w-11 grid place-items-center rounded-xl hover:bg-white/70 text-muted hover:text-ink focus-ring"
          >
            <PanelLeftClose className="h-4 w-4" />
          </button>
        )}
        <RoleSwitcher />
      </div>

      <div className="min-w-0 flex-1 leading-tight sm:hidden">
        <span className="block text-[9px] uppercase tracking-[0.12em] text-muted">Workspace</span>
        <span className="block truncate text-[11px] font-medium text-ink">{workspaceName}</span>
      </div>

      <div className="hidden lg:flex min-w-0 max-w-[240px] flex-col leading-tight rounded-xl border border-line bg-white/70 px-3 py-1.5">
        <span className="text-[10.5px] uppercase tracking-[0.14em] text-muted">Workspace</span>
        <span className="truncate text-[12.5px] font-medium text-ink">{workspaceName}</span>
      </div>

      <form role="search" onSubmit={submitSearch} className="hidden flex-1 min-w-0 max-w-2xl mx-auto sm:block">
        <label className="flex items-center gap-2 rounded-2xl bg-white/80 border border-line px-3.5 py-2 text-[13px] text-muted shadow-l1 focus-within:ring-2 focus-within:ring-info/30">
          <Search className="h-4 w-4" />
          <input
            ref={searchInput}
            aria-label="Search IIP"
            aria-describedby={!searchAvailable ? "search-access-guidance" : undefined}
            value={searchQuery}
            onChange={(event) => setSearchQuery(event.target.value)}
            disabled={!searchAvailable}
            minLength={2}
            maxLength={100}
            placeholder={searchAvailable ? "Search IIP" : `Search unavailable for ${roleDef.label}`}
            className="flex-1 min-w-0 bg-transparent outline-none placeholder:text-muted text-ink"
          />
          <span id="search-access-guidance" title={!searchAvailable ? searchGuidance : undefined} className="hidden sm:inline text-[10px] rounded bg-line/80 px-1.5 py-0.5 text-ink-soft">{searchAvailable ? "Ctrl K" : "No access"}</span>
        </label>
      </form>

      <div className="flex items-center gap-1.5">
        <button type="button" aria-label={searchAvailable ? "Open search" : "Search unavailable"} title={searchAvailable ? "Open search" : searchGuidance} disabled={!searchAvailable} onClick={() => router.push("/app/search")} className="grid h-11 w-11 place-items-center rounded-xl text-muted hover:bg-white/70 hover:text-ink disabled:opacity-50 sm:hidden focus-ring"><Search className="h-4 w-4" /></button>
        {/* Help removed — no handler existed and there is no help-content system to link to.
            /app/support was an orphaned redirect stub to /app/integrations, not real help
            content; not wired to here for the same reason. Reinstate only once real help
            content or a support flow exists. */}
        <NotificationBell />
        {copilotOpen && (
          <button
            onClick={toggleCopilot}
            aria-label="Collapse Copilot"
            className="hidden xl:grid h-11 w-11 place-items-center rounded-xl hover:bg-white/70 text-muted hover:text-ink transition-colors focus-ring"
          >
            <PanelRightClose className="h-4 w-4" />
          </button>
        )}
        <ProfileMenu
          displayName={displayName}
          roleLabel={roleLabel}
          workspaceName={workspaceName}
          initials={userInitials}
        />
      </div>
    </header>
  );
}
