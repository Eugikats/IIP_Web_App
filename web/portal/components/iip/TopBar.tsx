import { Bell, Search, HelpCircle, PanelLeftClose, PanelRightClose } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import RoleSwitcher from "./RoleSwitcher";
import { runtimeInitials, useRuntimeContext } from "@/lib/runtimeContext";

export default function TopBar() {
  const { sidebarOpen, copilotOpen, toggleSidebar, toggleCopilot, roleDef } = useUI();
  const { runtimeContext, loading } = useRuntimeContext();
  const activeWorkspace = runtimeContext?.availableWorkspaces.find(
    (workspace) => workspace.workspaceId === runtimeContext.workspaceId,
  );
  const workspaceName = activeWorkspace?.name ?? runtimeContext?.workspaceId ?? (loading ? "Resolving workspace" : "Workspace unavailable");
  const displayName = runtimeContext?.displayName ?? (loading ? "Resolving user" : "User unavailable");
  const roleLabel = runtimeContext?.roleLabel ?? roleDef.title;
  const userInitials = runtimeInitials(displayName);

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

      <div className="flex-1 min-w-0 max-w-2xl mx-auto">
        <div className="flex items-center gap-2 rounded-2xl bg-white/80 border border-cloud px-3.5 py-2 text-[13px] text-stone shadow-l1">
          <Search className="h-4 w-4" />
          <input
            placeholder="Ask IIP about claims, documents, customers, policies, or workflows…"
            className="flex-1 min-w-0 bg-transparent outline-none placeholder:text-stone text-navy"
          />
          <kbd className="hidden sm:inline text-[10px] rounded bg-cloud/80 px-1.5 py-0.5 text-slate2">⌘K</kbd>
        </div>
      </div>

      <div className="flex items-center gap-1.5">
        <button className="hidden sm:grid h-9 w-9 place-items-center rounded-xl hover:bg-white/70 text-stone hover:text-navy transition"><HelpCircle className="h-4 w-4" /></button>
        <button className="relative hidden sm:grid h-9 w-9 place-items-center rounded-xl hover:bg-white/70 text-stone hover:text-navy transition">
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
            <div className="text-[10.5px] text-stone">{roleLabel}</div>
          </div>
          <div className="h-8 w-8 rounded-full bg-gradient-secondary grid place-items-center text-white text-[11px] font-semibold">{userInitials}</div>
        </div>
      </div>
    </header>
  );
}
