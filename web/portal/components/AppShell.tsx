"use client";

import { ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ShieldCheck } from "lucide-react";
import { RightCopilotPanel } from "./RightCopilotPanel";
import { MobileNav } from "./MobileNav";
import { RoleProvider } from "./RoleContext";
import { CommandCenterQueryProvider } from "@/hooks/useCommandCenterQuery";
import { RouteAccessGuard } from "./RouteAccessGuard";
import { SidebarNav } from "./SidebarNav";
import { TopBar } from "./TopBar";
import { ProviderStatusStrip } from "./ProviderStatusStrip";

export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const isCustomerPortal = pathname.startsWith("/customer-portal");

  if (isCustomerPortal) {
    return (
      <RoleProvider>
        <div className="min-h-screen ambient-bg font-sans text-ink">
          <header className="border-b border-line glass">
            <div className="mx-auto flex min-h-16 max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-3">
              <Link href="/customer-portal" className="font-semibold text-ink">IIP — Insurance Intelligence Platform</Link>
              <span className="inline-flex items-center gap-2 rounded-full border border-line bg-mist px-3 py-1 text-xs font-semibold text-muted">
                <ShieldCheck className="h-3.5 w-3.5 text-violet" aria-hidden="true" />
                Customer-safe view
              </span>
            </div>
          </header>
          <main className="mx-auto w-full max-w-6xl px-4 py-6">
            {children}
          </main>
        </div>
      </RoleProvider>
    );
  }

  return (
    <RoleProvider>
      {/* Chat surface (children on "/") and RightCopilotPanel are siblings —
          share Command Center query state through this provider so the rail can
          reflect the latest AI turn. */}
      <CommandCenterQueryProvider>
        <div className="flex h-screen w-full overflow-hidden ambient-bg font-sans text-ink">
          <SidebarNav />

          <div className="relative flex min-w-0 flex-1 flex-col h-screen overflow-hidden">
            <TopBar />
            <ProviderStatusStrip />

            <main className="flex-1 overflow-y-auto w-full custom-scrollbar">
              <div className="relative z-10 mx-auto w-full max-w-[1600px] px-3 pb-28 pt-6 sm:px-4 lg:px-6 lg:pb-10">
                <RouteAccessGuard>{children}</RouteAccessGuard>
              </div>
            </main>
          </div>

          <RightCopilotPanel />
          <MobileNav />
        </div>
      </CommandCenterQueryProvider>
    </RoleProvider>
  );
}
