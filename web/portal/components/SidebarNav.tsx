"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { useRole } from "./RoleContext";
import { hasPermission, navigationGroups, type NavigationItem } from "@/lib/roleUiConfig";
import { PanelLeftClose, PanelLeftOpen } from "lucide-react";
import { navigationIcons } from "./navigationIcons";

export function SidebarNav() {
  const pathname = usePathname();
  const { displayName, roleLabel, userId, permissions } = useRole();
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    if (window.localStorage.getItem("iip-sidebar-collapsed") === "1") setCollapsed(true);
  }, []);

  function toggle() {
    setCollapsed((c) => {
      const next = !c;
      window.localStorage.setItem("iip-sidebar-collapsed", next ? "1" : "0");
      return next;
    });
  }

  const initials =
    (displayName || userId)
      .split(/[-_.\s]+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0]?.toUpperCase() ?? "")
      .join("") || "II";

  function isActive(item: NavigationItem) {
    return item.exact ? pathname === item.href : pathname === item.href || pathname.startsWith(`${item.href}/`);
  }

  return (
    <aside
      aria-label="Primary sidebar"
      className={`hidden h-screen flex-shrink-0 flex-col border-r border-line bg-surface/70 backdrop-blur-xl transition-all duration-300 ease-in-out lg:flex ${
        collapsed ? "w-[72px]" : "w-[280px]"
      }`}
    >
      {/* Header / logo */}
      <div className={`flex items-center border-b border-line px-4 py-4 ${collapsed ? "justify-center" : "justify-between"}`}>
        <div className="flex items-center gap-3 overflow-hidden">
          <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center overflow-hidden rounded-xl border border-line bg-surface">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/iip-logo.png" alt="IIP logo" className="h-7 w-7 object-contain" />
          </div>
          {!collapsed && (
            <div className="min-w-0">
              <p className="text-sm font-bold leading-tight text-ink">IIP Operations</p>
              <p className="text-[10px] font-semibold uppercase tracking-wider text-muted-soft">Insurance Intelligence Platform</p>
            </div>
          )}
        </div>
        {!collapsed && (
          <button onClick={toggle} title="Collapse sidebar" aria-label="Collapse sidebar" className="flex h-8 w-8 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-soft hover:text-ink">
            <PanelLeftClose className="h-4 w-4" />
          </button>
        )}
      </div>

      {collapsed && (
        <button onClick={toggle} title="Expand sidebar" aria-label="Expand sidebar" className="mx-auto mt-3 flex h-10 w-10 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-soft hover:text-ink">
          <PanelLeftOpen className="h-4 w-4" />
        </button>
      )}

      {/* Navigation */}
      <nav className="min-h-0 flex-1 overflow-y-auto px-3 pb-3 pt-3 custom-scrollbar">
        {navigationGroups.map((group) => {
          const items = group.items.filter((item) => hasPermission(permissions, item.permission));
          if (items.length === 0) return null;
          return (
            <div key={group.title} className="mb-5">
              {!collapsed && (
                <p className="mb-1.5 px-3 text-[11px] font-semibold uppercase tracking-[0.14em] text-muted-soft">{group.title}</p>
              )}
              <div className="space-y-1">
                {items.map((item) => {
                  const active = isActive(item);
                  const Icon = navigationIcons[item.icon];
                  if (collapsed) {
                    return (
                      <Link
                        key={item.href}
                        href={item.href}
                        title={item.label}
                        aria-current={active ? "page" : undefined}
                        className={`mx-auto flex h-11 w-11 items-center justify-center rounded-xl transition-colors ${
                          active ? "border border-primary-border bg-primary-soft text-primary" : "text-muted hover:bg-surface-soft hover:text-ink"
                        }`}
                      >
                        <Icon className="h-[18px] w-[18px]" />
                      </Link>
                    );
                  }
                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      aria-current={active ? "page" : undefined}
                      className={`flex min-h-11 items-center gap-3 rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors ${
                        active
                          ? "border-primary-border bg-primary-soft text-primary"
                          : "border-transparent text-muted hover:bg-surface-soft hover:text-ink"
                      }`}
                    >
                      <Icon className={`h-[17px] w-[17px] flex-shrink-0 ${active ? "text-primary" : ""}`} />
                      <span className="truncate">{item.label}</span>
                    </Link>
                  );
                })}
              </div>
            </div>
          );
        })}
      </nav>

      {/* Footer / user */}
      <div className="border-t border-line p-3">
        <div className={`flex items-center gap-3 rounded-xl p-2 transition-colors hover:bg-surface-soft ${collapsed ? "justify-center" : ""}`}>
          <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full bg-brand-gradient text-xs font-bold text-white">
            {initials}
          </div>
          {!collapsed && (
            <div className="min-w-0 flex-1 overflow-hidden">
              <p className="truncate text-sm font-semibold text-ink">{displayName || userId || "User not configured"}</p>
              <p className="truncate text-xs text-muted">{roleLabel}</p>
            </div>
          )}
        </div>
      </div>
    </aside>
  );
}
