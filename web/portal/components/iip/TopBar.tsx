import { FormEvent, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Bell, Search, HelpCircle, PanelLeftClose, PanelRightClose } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import RoleSwitcher from "./RoleSwitcher";
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
    <header className="h-16 shrink-0 border-b border-cloud/70 bg-white/60 backdrop-blur-xl px-2 sm:px-4 flex items-center gap-2 sm:gap-3">
      <div className="flex items-center gap-1">
        {sidebarOpen && (
          <button
            onClick={toggleSidebar}
            aria-label="Collapse sidebar"
            className="h-9 w-9 grid place-items-center rounded-xl hover:bg-white/70 text-stone hover:text-navy"
          >
            <PanelLeftClose className="h-4 w-4" />
          </button>
        )}
        <RoleSwitcher />
      </div>

      <div className="hidden lg:flex min-w-0 max-w-[240px] flex-col leading-tight rounded-xl border border-cloud bg-white/70 px-3 py-1.5">
        <span className="text-[10.5px] uppercase tracking-[0.14em] text-stone">Workspace</span>
        <span className="truncate text-[12.5px] font-medium text-navy">{workspaceName}</span>
      </div>

      <form role="search" onSubmit={submitSearch} className="flex-1 min-w-0 max-w-2xl mx-auto">
        <label className="flex items-center gap-2 rounded-2xl bg-white/80 border border-cloud px-3.5 py-2 text-[13px] text-stone shadow-l1 focus-within:ring-2 focus-within:ring-info/30">
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
            className="flex-1 min-w-0 bg-transparent outline-none placeholder:text-stone text-navy"
          />
          <span id="search-access-guidance" title={!searchAvailable ? searchGuidance : undefined} className="hidden sm:inline text-[10px] rounded bg-cloud/80 px-1.5 py-0.5 text-slate2">{searchAvailable ? "Ctrl K" : "No access"}</span>
        </label>
      </form>

      <div className="flex items-center gap-1.5">
        <button aria-label="Help" className="hidden sm:grid h-9 w-9 place-items-center rounded-xl hover:bg-white/70 text-stone hover:text-navy transition"><HelpCircle className="h-4 w-4" /></button>
        <button aria-label="Notifications" className="relative hidden sm:grid h-9 w-9 place-items-center rounded-xl hover:bg-white/70 text-stone hover:text-navy transition">
          <Bell className="h-4 w-4" />
          <span className="absolute top-1.5 right-1.5 h-1.5 w-1.5 rounded-full bg-error" />
        </button>
        {copilotOpen && (
          <button
            onClick={toggleCopilot}
            aria-label="Collapse Copilot"
            className="hidden xl:grid h-9 w-9 place-items-center rounded-xl hover:bg-white/70 text-stone hover:text-navy transition"
          >
            <PanelRightClose className="h-4 w-4" />
          </button>
        )}
        <div className="flex items-center gap-2.5 sm:ml-2 sm:pl-3 sm:border-l sm:border-cloud">
          <div className="text-right leading-tight hidden sm:block">
            <div className="text-[12.5px] font-medium text-navy">{displayName}</div>
            <div className="text-[10.5px] text-stone">Signed in as {roleLabel}</div>
          </div>
          <div className="h-8 w-8 rounded-full bg-gradient-secondary grid place-items-center text-white text-[11px] font-semibold">{userInitials}</div>
        </div>
      </div>
    </header>
  );
}
