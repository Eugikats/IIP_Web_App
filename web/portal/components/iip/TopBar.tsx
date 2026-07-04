import { Bell, Search, HelpCircle, PanelLeftClose, PanelRightClose } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import RoleSwitcher from "./RoleSwitcher";

export default function TopBar() {
  const { sidebarOpen, copilotOpen, toggleSidebar, toggleCopilot, roleDef } = useUI();
  return (
    <header className="h-16 shrink-0 border-b border-cloud/70 bg-white/60 backdrop-blur-xl px-4 flex items-center gap-3">
      <div className="flex items-center gap-1">
        {sidebarOpen && !roleDef.customerSafeOnly && (
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

      <div className="flex-1 max-w-2xl mx-auto">
        <div className="flex items-center gap-2 rounded-2xl bg-white/80 border border-cloud px-3.5 py-2 text-[13px] text-stone shadow-l1">
          <Search className="h-4 w-4" />
          <input
            placeholder="Ask IIP about claims, documents, customers, policies, or workflows…"
            className="flex-1 bg-transparent outline-none placeholder:text-stone text-navy"
          />
          <kbd className="text-[10px] rounded bg-cloud/80 px-1.5 py-0.5 text-slate2">⌘K</kbd>
        </div>
      </div>

      <div className="flex items-center gap-1.5">
        <button className="h-9 w-9 grid place-items-center rounded-xl hover:bg-white/70 text-stone hover:text-navy transition"><HelpCircle className="h-4 w-4" /></button>
        <button className="relative h-9 w-9 grid place-items-center rounded-xl hover:bg-white/70 text-stone hover:text-navy transition">
          <Bell className="h-4 w-4" />
          <span className="absolute top-1.5 right-1.5 h-1.5 w-1.5 rounded-full bg-error" />
        </button>
        {copilotOpen && !roleDef.customerSafeOnly && (
          <button
            onClick={toggleCopilot}
            aria-label="Collapse Copilot"
            className="hidden xl:grid h-9 w-9 place-items-center rounded-xl hover:bg-white/70 text-stone hover:text-navy transition"
          >
            <PanelRightClose className="h-4 w-4" />
          </button>
        )}
        <div className="ml-2 flex items-center gap-2.5 pl-3 border-l border-cloud">
          <div className="text-right leading-tight hidden sm:block">
            <div className="text-[12.5px] font-medium text-navy">{roleDef.name}</div>
            <div className="text-[10.5px] text-stone">{roleDef.title}</div>
          </div>
          <div className="h-8 w-8 rounded-full bg-gradient-secondary grid place-items-center text-white text-[11px] font-semibold">{roleDef.initials}</div>
        </div>
      </div>
    </header>
  );
}
