"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { hasPermission, navigationGroups, type NavigationItem } from "@/lib/roleUiConfig";
import { useRole } from "./RoleContext";
import { navigationIcons } from "./navigationIcons";

export function MobileNav() {
  const pathname = usePathname();
  const { permissions } = useRole();
  const items = navigationGroups
    .flatMap((group) => group.items)
    .filter((item) => item.mobile !== false && hasPermission(permissions, item.permission));

  if (items.length === 0) {
    return null;
  }

  return (
    <nav className="fixed inset-x-0 bottom-0 z-30 border-t border-line bg-surface/95 px-2 pb-[calc(env(safe-area-inset-bottom)+0.5rem)] pt-2 shadow-panel backdrop-blur-md lg:hidden" aria-label="Primary mobile navigation">
      <div className="flex gap-1 overflow-x-auto custom-scrollbar">
        {items.map((item) => {
          const active = isActive(item, pathname);
          const Icon = navigationIcons[item.icon];

          return (
            <Link
              key={item.href}
              href={item.href}
              title={item.label}
              aria-label={item.label}
              aria-current={active ? "page" : undefined}
              className={`flex min-h-[58px] min-w-[74px] flex-col items-center justify-center gap-1 rounded-xl border px-2 text-center text-[10px] font-semibold leading-tight transition-colors ${
                active
                  ? "border-violet/25 bg-surface-ai text-violet"
                  : "border-transparent text-muted hover:bg-surface-soft hover:text-ink"
              }`}
            >
              <Icon className="h-4 w-4 flex-shrink-0" />
              <span className="line-clamp-2">{shortLabel(item.label)}</span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}

function isActive(item: NavigationItem, pathname: string) {
  return item.exact ? pathname === item.href : pathname === item.href || pathname.startsWith(`${item.href}/`);
}

function shortLabel(label: string) {
  return label
    .replace("Claims Intelligence", "Claims")
    .replace("Document Checker", "Docs")
    .replace("Workflow & Approvals", "Approvals")
    .replace("Integrations & Channels", "Integrations")
    .replace("Reports & Audit", "Reports")
    .replace("Admin Console", "Admin")
    .replace("Support & Health", "Health");
}
