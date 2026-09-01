"use client";
import { useLocation, Navigate } from "@/components/iip/NextRouterCompat";
import type { ReactNode } from "react";
import Link from "next/link";
import Sidebar from "./Sidebar";
import TopBar from "./TopBar";
import StatusBar from "./StatusBar";
import CopilotPanel from "./CopilotPanel";
import EnvironmentBanner from "./EnvironmentBanner";
import { UIStateProvider, useUI } from "@/context/UIStateContext";
import { canAccess, canAccessWithPermissions, firstAccessibleRoute, previewLandingRoute } from "@/lib/roles";
import { RuntimeContextProvider, useRuntimeContext } from "@/lib/runtimeContext";
import { canUseLocalTestDoubles } from "@/lib/config";
import { Loader2, PanelLeftOpen, PanelRightOpen, ShieldAlert } from "lucide-react";

function ShellInner({ children }: { children: ReactNode }) {
  const { sidebarOpen, copilotOpen, setSidebarOpen, toggleSidebar, toggleCopilot, roleDef } = useUI();
  const { runtimeContext, loading, error, parityMode, refresh } = useRuntimeContext();
  const loc = useLocation();

  if (!canUseLocalTestDoubles && !parityMode && loading) {
    return (
      <main className="grid min-h-screen place-items-center bg-white px-6 text-navy">
        <div className="flex items-center gap-3 text-sm text-slate2">
          <Loader2 className="h-5 w-5 animate-spin text-primary" />
          Verifying your access
        </div>
      </main>
    );
  }

  if (!canUseLocalTestDoubles && !parityMode && !runtimeContext) {
    return (
      <main className="grid min-h-screen place-items-center bg-white px-6 text-navy">
        <div className="w-full max-w-md border-y border-cloud py-8 text-center">
          <ShieldAlert className="mx-auto h-7 w-7 text-warning" />
          <h1 className="mt-4 text-xl font-semibold">Workspace access unavailable</h1>
          <p className="mt-2 text-sm leading-6 text-stone">
            {error
              ? "Your signed-in account could not be resolved to an IIP workspace."
              : "Sign in with an authorized organization account to continue."}
          </p>
          <div className="mt-6 flex items-center justify-center gap-3">
            <button type="button" onClick={() => void refresh()} className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-white focus-ring">
              Retry access check
            </button>
            <Link href="/" className="rounded-md border border-cloud px-4 py-2 text-sm font-medium text-slate2 focus-ring">
              Return to sign in
            </Link>
          </div>
        </div>
      </main>
    );
  }

  const permitted = canUseLocalTestDoubles || parityMode
    ? canAccess(roleDef.key, loc.pathname)
    : canAccessWithPermissions(runtimeContext?.permissions ?? [], loc.pathname);
  if (!permitted) {
    const fallback = canUseLocalTestDoubles || parityMode
      ? previewLandingRoute(roleDef.key)
      : firstAccessibleRoute(runtimeContext?.permissions ?? []) ?? "/";
    return <Navigate to={fallback} replace />;
  }

  return (
    <div className="h-screen w-full ambient-bg flex flex-col overflow-hidden">
      <EnvironmentBanner />
      <div className="flex-1 flex overflow-hidden relative">
        {sidebarOpen && (
          <button
            type="button"
            aria-label="Close sidebar overlay"
            onClick={() => setSidebarOpen(false)}
            className="absolute inset-y-0 left-[min(264px,calc(100vw-48px))] right-0 z-30 bg-navy/20 backdrop-blur-[1px] md:hidden"
          />
        )}
        <div
          className={
            "absolute inset-y-0 left-0 z-40 w-[264px] max-w-[calc(100vw-48px)] overflow-hidden shadow-l4 transition-[width,transform] duration-300 ease-out md:relative md:inset-auto md:z-auto md:max-w-none md:shrink-0 md:shadow-none " +
            (sidebarOpen ? "translate-x-0 md:w-[264px]" : "-translate-x-full md:w-0 md:translate-x-0")
          }
        >
          <div className="h-full w-[264px] max-w-full">
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
            <div className="max-w-[1400px] mx-auto px-4 sm:px-6 lg:px-10 py-6 sm:py-8 animate-fade-in">
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
      <RuntimeContextProvider>
        <ShellInner>{children}</ShellInner>
      </RuntimeContextProvider>
    </UIStateProvider>
  );
}
