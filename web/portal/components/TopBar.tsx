"use client";

import { authMode, canUseLocalTestDoubles, runtimeProfile } from "@/lib/config";
import { roleLabels, roles } from "@/lib/roleUiConfig";
import { useRole } from "./RoleContext";
import { Bell, ChevronDown, HelpCircle, Search, ShieldCheck, Sparkles } from "lucide-react";

export function TopBar() {
  const { role, setRole, tenantId, workspaceId } = useRole();
  const workspaceLabel = workspaceId || "Workspace not configured";
  const tenantLabel = tenantId || "Tenant not configured";

  return (
    <header className="sticky top-0 z-20 h-16 border-b border-line bg-surface/70 backdrop-blur-xl">
      <div className="flex h-full items-center gap-3 px-4 lg:px-6">
        {/* Left — workspace identity + active agent */}
        <div className="flex min-w-0 items-center divide-x divide-line">
          <div className="min-w-0 pr-4">
            <p className="truncate text-[11px] font-medium leading-tight text-muted-soft">Workspace</p>
            <p className="truncate text-sm font-semibold leading-tight text-ink">{workspaceLabel}</p>
          </div>
          <div className="min-w-0 px-4">
            <p className="truncate text-[11px] font-medium leading-tight text-muted-soft">Active context</p>
            <p className="truncate font-mono text-[11px] text-muted">
              {tenantLabel}
            </p>
          </div>
          <span className="hidden items-center gap-1.5 rounded-full border border-ai-border bg-ai-soft px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-ai xl:inline-flex">
            <Sparkles className="h-3 w-3" />
            AI assistance
          </span>
        </div>

        {/* Center — global search */}
        <div className="hidden flex-1 justify-center md:flex">
          <form
            onSubmit={(e) => e.preventDefault()}
            className="relative w-full max-w-md"
            title="Global search is not configured yet"
          >
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-soft" />
            <input
              className="w-full rounded-xl border border-line bg-shell py-2 pl-9 pr-3 text-sm text-ink outline-none transition-colors placeholder:text-muted-soft focus:border-ai/40 focus:ring-4 focus:ring-ai/10 disabled:cursor-not-allowed"
              placeholder="Search not configured"
              disabled
            />
          </form>
        </div>

        {/* Right — controls */}
        <div className="ml-auto flex flex-wrap items-center gap-2">
          <span className="hidden items-center gap-1.5 rounded-full border border-line bg-surface-soft px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-muted lg:inline-flex">
            <ShieldCheck className="h-3 w-3 text-ai" />
            {runtimeProfile} / {authMode}
          </span>

          <button className="flex h-9 w-9 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-soft hover:text-ink" title="Help" aria-label="Help">
            <HelpCircle className="h-4 w-4" />
          </button>

          <button className="relative flex h-9 w-9 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-soft hover:text-ink" title="Notifications" aria-label="Notifications">
            <Bell className="h-4 w-4" />
            <span className="absolute right-1.5 top-1.5 h-2 w-2 rounded-full border border-surface bg-danger" />
          </button>

          {canUseLocalTestDoubles ? (
            <label className="flex cursor-pointer items-center gap-2 rounded-xl border border-line bg-surface px-2.5 py-1.5 text-xs font-semibold text-muted transition-colors hover:border-ai/40">
              <span className="text-muted-soft">Role</span>
              <select
                className="cursor-pointer bg-transparent text-xs font-bold text-ink outline-none"
                value={role}
                onChange={(event) => setRole(event.target.value as typeof role)}
              >
                {roles.map((item) => (
                  <option key={item} value={item} className="bg-surface font-medium text-ink">
                    {roleLabels[item]}
                  </option>
                ))}
              </select>
              <ChevronDown className="h-3 w-3 opacity-50" />
            </label>
          ) : null}
        </div>
      </div>
    </header>
  );
}
