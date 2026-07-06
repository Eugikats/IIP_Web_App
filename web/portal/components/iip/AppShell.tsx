"use client";
import { useLocation, Navigate } from "@/components/iip/NextRouterCompat";
import type { ReactNode } from "react";
import Sidebar from "./Sidebar";
import TopBar from "./TopBar";
import StatusBar from "./StatusBar";
import CopilotPanel from "./CopilotPanel";
import EnvironmentBanner from "./EnvironmentBanner";
import { UIStateProvider, useUI } from "@/context/UIStateContext";
import { canAccess } from "@/lib/roles";
import { PanelLeftOpen, PanelRightOpen } from "lucide-react";

function ShellInner({ children }: { children: ReactNode }) {
  const { sidebarOpen, copilotOpen, toggleSidebar, toggleCopilot, roleDef } = useUI();
  const loc = useLocation();

  if (!canAccess(roleDef.key, loc.pathname)) {
    return <Navigate to={roleDef.landing} replace />;
  }

  return (
    <div className="h-screen w-full ambient-bg flex flex-col overflow-hidden">
      <EnvironmentBanner />
      <div className="flex-1 flex overflow-hidden relative">
        <div
          className={
            "transition-all duration-300 ease-out overflow-hidden shrink-0 " +
            (sidebarOpen ? "w-[264px]" : "w-0")
          }
        >
          <div className="w-[264px] h-full">
            <Sidebar />
          </div>
        </div>

        <div className="flex-1 flex flex-col overflow-hidden min-w-0 relative">
          <TopBar />
          {!sidebarOpen && (
            <button
              onClick={toggleSidebar}
              aria-label="Open sidebar"
              className="absolute left-2 top-20 z-10 h-8 w-8 grid place-items-center rounded-xl border border-cloud bg-white/80 backdrop-blur text-slate2 hover-lift"
            >
              <PanelLeftOpen className="h-4 w-4" />
            </button>
          )}
          {!copilotOpen && (
            <button
              onClick={toggleCopilot}
              aria-label="Open Copilot"
              className="absolute right-2 top-20 z-10 h-8 w-8 grid place-items-center rounded-xl border border-cloud bg-white/80 backdrop-blur text-slate2 hover-lift"
            >
              <PanelRightOpen className="h-4 w-4" />
            </button>
          )}
          <main className="flex-1 overflow-y-auto">
            <div className="max-w-[1400px] mx-auto px-6 lg:px-10 py-8 animate-fade-in">
              {children}
            </div>
          </main>
        </div>

        <div
          className={
            "hidden xl:block transition-all duration-300 ease-out overflow-hidden shrink-0 " +
            (copilotOpen ? "w-[340px]" : "w-0")
          }
        >
          <div className="w-[340px] h-full">
            <CopilotPanel />
          </div>
        </div>
      </div>
      <StatusBar />
    </div>
  );
}

export default function AppShell({ children }: { children: ReactNode }) {
  return (
    <UIStateProvider>
      <ShellInner>{children}</ShellInner>
    </UIStateProvider>
  );
}
