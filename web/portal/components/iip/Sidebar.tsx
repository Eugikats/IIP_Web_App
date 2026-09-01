import { NavLink, useLocation } from "@/components/iip/NextRouterCompat";
import Image from "next/image";
import iipLogo from "@/assets/iip-logo";
import {
  MessagesSquare, FileSearch, Users, ShieldCheck, AlertTriangle, ClipboardList,
  BookOpen, LineChart, Plug, ScrollText, Cog, Plus, Search,
  Circle, PanelLeftClose, Inbox, UserSquare2, MessageSquareText, FileText, Bot, CreditCard, Gavel, Building2,
  ListChecks,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useUI } from "@/context/UIStateContext";
import { runtimeInitials, useRuntimeContext } from "@/lib/runtimeContext";
import { canAccess, canAccessWithPermissions, canUseGlobalSearchPreview } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { useEffect, useState } from "react";
import { listConversations, type ConversationSummary } from "@/lib/lovable-parity/data/commandCenterConversations";

const NAV_GROUPS = [
  { label: "Work", items: [
    { to: "/app", label: "Command Center", icon: MessagesSquare },
    { to: "/app/work-items", label: "My Work", icon: ListChecks },
    { to: "/app/claims", label: "Claims", icon: FileSearch },
    { to: "/app/channels", label: "Channels", icon: Inbox },
  ]},
  { label: "Records", items: [
    { to: "/app/customers", label: "Customers", icon: UserSquare2 },
    { to: "/app/documents", label: "Documents", icon: FileText },
    { to: "/app/communications", label: "Communications", icon: MessageSquareText },
    { to: "/app/knowledge", label: "Knowledge", icon: BookOpen },
  ]},
  { label: "Specialist work", items: [
    { to: "/app/acquisition", label: "Acquisition", icon: Users },
    { to: "/app/policy", label: "Policy Guidance", icon: ShieldCheck },
    { to: "/app/fraud", label: "Risk Signals", icon: AlertTriangle },
    { to: "/app/underwriting", label: "Underwriting", icon: ClipboardList },
  ]},
  { label: "Automation", items: [
    { to: "/app/flows", label: "Flows", icon: Bot },
  ]},
  { label: "Oversight", items: [
    { to: "/app/executive", label: "Operations Intelligence", icon: LineChart },
    { to: "/app/audit", label: "Audit & Compliance", icon: ScrollText },
  ]},
  { label: "Platform", items: [
    { to: "/app/integrations", label: "Integrations & Health", icon: Plug },
  ]},
];
const ALL_ADMIN = [
  { to: "/app/admin", label: "Admin Console", icon: Cog },
  { to: "/app/admin/agents", label: "Agents", icon: Bot },
  { to: "/app/admin/tenant-policy", label: "Agent Policy", icon: Gavel },
  { to: "/app/admin/onboarding", label: "Tenant Onboarding", icon: Building2 },
  { to: "/app/admin/billing", label: "Billing & Usage", icon: CreditCard },
];

function Item({ to, label, icon: Icon }: { to: string; label: string; icon: any }) {
  const loc = useLocation();
  const active = to === "/app" ? loc.pathname === "/app" : loc.pathname === to || loc.pathname.startsWith(to + "/");
  return (
    <NavLink
      to={to}
      end={to === "/app"}
      className={cn(
        "group relative flex items-center gap-3 rounded-2xl px-3 py-2 text-[13px] transition-all focus-ring",
        active ? "text-navy font-medium" : "text-slate2/80 hover:text-navy hover:bg-white/60"
      )}
      style={active ? {
        background: "linear-gradient(135deg, rgba(37,99,255,0.10), rgba(124,77,255,0.12))",
        border: "1px solid rgba(124,77,255,0.18)",
      } : undefined}
    >
      {active && <span className="absolute left-0 top-1/2 -translate-y-1/2 h-5 w-[3px] rounded-full bg-gradient-primary" />}
      <Icon className={cn("h-[16px] w-[16px]", active ? "text-info" : "text-stone")} strokeWidth={active ? 2.2 : 1.7} />
      <span className="truncate">{label}</span>
    </NavLink>
  );
}

export default function Sidebar() {
  const { roleDef, toggleSidebar } = useUI();
  const { runtimeContext, loading, parityMode } = useRuntimeContext();
  const usePreviewRole = canUseLocalTestDoubles || parityMode;
  const searchAvailable = parityMode
    ? false
    : usePreviewRole
      ? canUseGlobalSearchPreview(roleDef.key)
      : (runtimeContext?.permissions ?? []).some((permission) => permission === "ViewAssignedClaims" || permission === "ViewAllClaims");
  const groups = NAV_GROUPS
    .map((group) => ({
      ...group,
      items: group.items.filter((item) =>
        usePreviewRole
          ? canAccess(roleDef.key, item.to)
          : canAccessWithPermissions(runtimeContext?.permissions ?? [], item.to)
      ),
    }))
    .filter((group) => group.items.length > 0);
  const admin = ALL_ADMIN.filter((item) =>
    usePreviewRole
      ? canAccess(roleDef.key, item.to)
      : canAccessWithPermissions(runtimeContext?.permissions ?? [], item.to)
  );
  const activeWorkspace = runtimeContext?.availableWorkspaces.find(
    (workspace) => workspace.workspaceId === runtimeContext.workspaceId,
  );
  const workspaceName = activeWorkspace?.name ?? runtimeContext?.workspaceId ?? (loading ? "Resolving workspace" : "Workspace not configured");
  const workspaceKind = activeWorkspace?.kind ?? runtimeContext?.source ?? "Not configured";
  const environmentLabel = runtimeContext?.environment.name ?? (loading ? "Checking" : "Unverified");
  const displayName = runtimeContext?.displayName ?? (loading ? "Resolving user" : "User unavailable");
  const roleLabel = runtimeContext?.roleLabel ?? (loading ? "Authority resolving" : "Authority unavailable");
  const userInitials = runtimeInitials(displayName);
  const commandCenterAvailable = usePreviewRole
    ? canAccess(roleDef.key, "/app")
    : (runtimeContext?.permissions ?? []).includes("ViewCommandCenter");
  const [recentConversations, setRecentConversations] = useState<ConversationSummary[]>([]);

  useEffect(() => {
    if (!commandCenterAvailable || !runtimeContext || parityMode) {
      setRecentConversations([]);
      return;
    }
    const controller = new AbortController();
    void listConversations(roleDef.key, runtimeContext.userId, false, 5, controller.signal)
      .then((response) => setRecentConversations(Array.isArray(response.items) ? response.items : []))
      .catch(() => setRecentConversations([]));
    return () => controller.abort();
  }, [commandCenterAvailable, parityMode, roleDef.key, runtimeContext]);

  return (
    <aside className="h-full flex flex-col border-r border-cloud/70 bg-white/60 backdrop-blur-xl">
      <div className="px-5 pt-5 pb-4 flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="relative h-9 w-9 rounded-xl bg-white grid place-items-center shadow-l2 ring-1 ring-cloud/70 overflow-hidden">
            <Image src={iipLogo.url} alt="IIP logo" width={28} height={28} className="h-7 w-7 object-contain" priority />
            <span className="absolute -bottom-0.5 -right-0.5 h-2 w-2 rounded-full bg-success ring-2 ring-white" />
          </div>
          <div className="leading-tight">
            <div className="text-[13px] font-semibold tracking-tight text-navy">Insurance Intelligence Platform</div>
            <div className="text-[10.5px] uppercase tracking-[0.14em] text-stone">Platform · IIP</div>
          </div>
        </div>
        <button onClick={toggleSidebar} aria-label="Collapse sidebar" className="h-7 w-7 grid place-items-center rounded-lg text-stone hover:text-navy hover:bg-white/70">
          <PanelLeftClose className="h-4 w-4" />
        </button>
      </div>

      <div className="mx-4 mb-3 rounded-2xl border border-cloud/80 bg-white/70 p-3">
        <div className="text-[10.5px] uppercase tracking-[0.14em] text-stone">Workspace</div>
        <div className="mt-1 flex items-center justify-between">
          <div className="min-w-0">
            <div className="text-[13px] font-medium text-navy truncate">{workspaceName}</div>
            <div className="text-[11px] text-stone truncate">{workspaceKind}</div>
          </div>
          <span className="text-[10px] font-medium rounded-full px-2 py-0.5 text-info ring-1 ring-[hsl(var(--info)/0.2)] bg-[hsl(var(--info)/0.08)]">
            {environmentLabel}
          </span>
        </div>
      </div>

      {searchAvailable && <div className="px-4">
        <NavLink to="/app/search" className="flex items-center gap-2 rounded-2xl bg-white/70 border border-cloud px-3 py-2 text-[12.5px] text-stone hover:text-navy hover:bg-white focus-ring">
          <Search className="h-3.5 w-3.5" />
          <span className="flex-1">Search IIP</span>
          <span className="text-[10px] rounded bg-cloud/80 px-1.5 py-0.5 text-slate2">Ctrl K</span>
        </NavLink>
      </div>}

      <div className="px-4 mt-3">
        <NavLink to="/app" className="w-full inline-flex items-center justify-center gap-2 rounded-2xl gradient-primary text-white text-[13px] font-medium py-2.5 shadow-l2 hover-lift focus-ring">
          <Plus className="h-4 w-4" /> New Conversation
        </NavLink>
      </div>

      {commandCenterAvailable && (
        <div className="px-4 pt-4">
          <div className="flex items-center justify-between px-1 text-[10.5px] uppercase tracking-[0.14em] text-stone">
            <span>Recent conversations</span>
            <NavLink to="/app/conversations" className="normal-case tracking-normal text-info hover:underline focus-ring">All</NavLink>
          </div>
          {recentConversations.length > 0 ? (
            <div className="mt-2 space-y-1">
              {recentConversations.map((conversation) => (
                <NavLink key={conversation.conversationId} to={`/app/conversations/${conversation.conversationId}`} className="block truncate rounded-lg px-2 py-1.5 text-[11.5px] text-slate2 hover:bg-white/70 hover:text-navy focus-ring">
                  {conversation.title}
                </NavLink>
              ))}
            </div>
          ) : (
            <div className="mt-2 px-2 text-[11px] text-stone">No recent conversations</div>
          )}
        </div>
      )}

      <nav className="flex-1 overflow-y-auto px-3 pt-5 pb-4">
        {groups.map((group, index) => (
          <div key={group.label} className={index === 0 ? "" : "mt-5"}>
            <div className="px-2 text-[10.5px] uppercase tracking-[0.14em] text-stone mb-2">{group.label}</div>
            <div className="space-y-0.5">{group.items.map((item) => <Item key={item.to} {...item} />)}</div>
          </div>
        ))}
        {admin.length > 0 && (
          <>
            <div className="px-2 mt-6 text-[10.5px] uppercase tracking-[0.14em] text-stone mb-2">Platform administration</div>
            <div className="space-y-0.5">
              {admin.map(m => <Item key={m.to} {...m} />)}
            </div>
          </>
        )}
      </nav>

      <div className="border-t border-cloud/80 p-3 mx-2 mb-2 rounded-2xl">
        <div className="flex items-center gap-3">
          <div className="h-9 w-9 rounded-full bg-gradient-secondary grid place-items-center text-white text-[12px] font-semibold">{userInitials}</div>
          <div className="min-w-0 flex-1">
            <div className="text-[13px] font-medium text-navy truncate">{displayName}</div>
            <div className="text-[11px] text-stone truncate">Signed in as {roleLabel}</div>
          </div>
          <div className="flex items-center gap-1 text-[11px] text-success"><Circle className="h-2 w-2 fill-current" /> Auth</div>
        </div>
      </div>
    </aside>
  );
}
