"use client";
import { useLocation, Navigate } from "@/components/iip/NextRouterCompat";
import { type ReactNode } from "react";
import Link from "next/link";
import Sidebar from "./Sidebar";
import FirstRunOrientation from "./FirstRunOrientation";
import StatusBar from "./StatusBar";
import CopilotPanel from "./CopilotPanel";
import EnvironmentBanner from "./EnvironmentBanner";
import NotificationBell from "./NotificationBell";
import { UIStateProvider, useUI } from "@/context/UIStateContext";
import { canAccess, canAccessWithPermissions, firstAccessibleRoute, previewLandingRoute } from "@/lib/roles";
import { RuntimeContextProvider, useRuntimeContext } from "@/lib/runtimeContext";
import { canUseLocalTestDoubles } from "@/lib/config";
import { Loader2, PanelRightOpen, PanelRightClose, ShieldAlert } from "lucide-react";

// Design language v2 §3: 64px rail (Sidebar) + an optional contextual panel + a left-aligned
// content column. There is no fixed top bar — a page's own breadcrumb/h1/primary action live at
// the top of its content column. This slot exists for screens with genuine sub-navigation or
// history (navigation model §5.1); no screen populates it yet, so it renders nothing today.
function ContextualPanel({ children }: { children?: ReactNode }) {
  if (!children) return null;
  return (
    <div className="hidden w-64 shrink-0 border-r border-dls-border-subtle bg-dls-surface md:block">
      {children}
    </div>
  );
}

function ShellInner({ children }: { children: ReactNode }) {
  const { copilotOpen, toggleCopilot, roleDef } = useUI();
  const { runtimeContext, loading, error, parityMode, refresh } = useRuntimeContext();
  const loc = useLocation();

  if (!canUseLocalTestDoubles && !parityMode && loading) {
    return (
      <main className="grid min-h-screen place-items-center bg-dls-surface px-6 text-dls-text-primary">
        <div className="flex items-center gap-3 text-sm text-dls-text-secondary">
          <Loader2 className="h-5 w-5 animate-spin text-dls-primary" />
          Verifying your access
        </div>
      </main>
    );
  }

  if (!canUseLocalTestDoubles && !parityMode && !runtimeContext) {
    return (
      <main className="grid min-h-screen place-items-center bg-dls-surface px-6 text-dls-text-primary">
        <div className="w-full max-w-md border-y border-dls-border-subtle py-8 text-center">
          <ShieldAlert className="mx-auto h-7 w-7 text-dls-warning-text" />
          <h1 className="mt-4 text-xl font-bold">Workspace access unavailable</h1>
          <p className="mt-2 text-sm leading-6 text-dls-text-secondary">
            {error
              ? "Your signed-in account could not be resolved to an IIP workspace."
              : "Sign in with an authorized organization account to continue."}
          </p>
          <div className="mt-6 flex items-center justify-center gap-3">
            <button
              type="button"
              onClick={() => void refresh()}
              className="rounded-dls-control bg-dls-primary px-4 py-2 text-sm font-bold text-dls-text-inverse shadow-dls-primary dls-focus-outline"
            >
              Retry access check
            </button>
            <Link
              href="/"
              className="rounded-dls-control border border-dls-border-input px-4 py-2 text-sm font-semibold text-dls-text-primary dls-focus-outline"
            >
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
    <div className="flex h-[100dvh] w-full flex-col overflow-hidden bg-dls-bg-page">
      <EnvironmentBanner />
      <div className="relative flex flex-1 overflow-hidden">
        {/* The rail is always present at every viewport width (navigation model §5.1) — it never
            collapses into an off-canvas drawer, so there is no mobile open/close state to manage. */}
        <Sidebar />
        <ContextualPanel />

        {/* First-run orientation (docs/03-design-architecture/iip_first_run_orientation_spec.md).
            Mounted here rather than on a page so it anchors to the rail, which lives at this level.
            `ready` gates it on permissions having resolved -- a step must never be shown or dropped
            on the basis of access that was still unknown. */}
        <FirstRunOrientation userId={runtimeContext?.userId ?? null} ready={!loading} pathname={loc.pathname} />

        <div className="relative flex min-w-0 flex-1 flex-col overflow-hidden">
          <div className="absolute right-4 top-4 z-10 flex items-center gap-1.5">
            <NotificationBell />
            {copilotOpen ? (
              <button
                type="button"
                onClick={toggleCopilot}
                aria-label="Collapse evidence panel"
                title="Collapse evidence panel"
                className="hidden h-10 w-10 items-center justify-center rounded-dls-control text-dls-text-secondary hover:bg-dls-bg-page dls-focus-outline xl:flex"
              >
                <PanelRightClose className="h-4 w-4" aria-hidden="true" />
              </button>
            ) : (
              <button
                type="button"
                onClick={toggleCopilot}
                aria-label="Open evidence panel"
                title="Open evidence panel"
                className="hidden h-10 w-10 items-center justify-center rounded-dls-control text-dls-text-secondary hover:bg-dls-bg-page dls-focus-outline xl:flex"
              >
                <PanelRightOpen className="h-4 w-4" aria-hidden="true" />
              </button>
            )}
          </div>

          <main className="flex-1 overflow-y-auto">
            <div className="mx-auto max-w-[1040px] px-8 pb-[60px] pt-7">{children}</div>
          </main>
        </div>

        <div
          className={
            "hidden shrink-0 overflow-hidden transition-[width] duration-dls-base ease-dls xl:block " +
            (copilotOpen ? "w-[340px]" : "w-0")
          }
        >
          <div className="h-full w-[340px] border-l border-dls-border-subtle">
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
