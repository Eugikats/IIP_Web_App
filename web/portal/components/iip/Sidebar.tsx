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

const NAV_GROUPS = [
  { label: "Command", items: [
    { to: "/app", label: "Command Center", icon: MessagesSquare },
    { to: "/app/claims", label: "Claims Intelligence", icon: FileSearch },
    { to: "/app/channels", label: "Channels Inbox", icon: Inbox },
    { to: "/app/customers", label: "Customer 360", icon: UserSquare2 },
  ]},
  { label: "Operations", items: [
    { to: "/app/work-items", label: "Review & Approvals", icon: ListChecks },
    { to: "/app/documents", label: "Documents", icon: FileText },
    { to: "/app/communications", label: "Communications", icon: MessageSquareText },
    { to: "/app/acquisition", label: "Customer Acquisition", icon: Users },
  ]},
  { label: "Intelligence", items: [
    { to: "/app/policy", label: "Policy Intelligence", icon: ShieldCheck },
    { to: "/app/fraud", label: "Risk & Fraud Signals", icon: AlertTriangle },
    { to: "/app/underwriting", label: "Underwriting", icon: ClipboardList },
    { to: "/app/knowledge", label: "Knowledge Hub", icon: BookOpen },
    { to: "/app/executive", label: "Operations Intelligence", icon: LineChart },
  ]},
  { label: "Automation", items: [
    { to: "/app/flows", label: "Automation Studio", icon: Bot },
  ]},
  { label: "Governance", items: [
    { to: "/app/integrations", label: "Integrations & Health", icon: Plug },
    { to: "/app/audit", label: "Audit & Compliance", icon: ScrollText },
  ]},
];
const ALL_ADMIN = [
  { to: "/app/admin", label: "Admin Console", icon: Cog },
  { to: "/app/admin/agents", label: "AI & Agent Management", icon: Bot },
  { to: "/app/admin/tenant-policy", label: "Tenant Agent Policy", icon: Gavel },
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
  const { runtimeContext, loading } = useRuntimeContext();
  const groups = NAV_GROUPS
    .map((group) => ({ ...group, items: group.items.filter((item) => roleDef.modules.includes(item.to)) }))
    .filter((group) => group.items.length > 0);
  const admin = ALL_ADMIN.filter((m) => roleDef.admin.includes(m.to));
  const activeWorkspace = runtimeContext?.availableWorkspaces.find(
    (workspace) => workspace.workspaceId === runtimeContext.workspaceId,
  );
  const workspaceName = activeWorkspace?.name ?? runtimeContext?.workspaceId ?? (loading ? "Resolving workspace" : "Workspace not configured");
  const workspaceKind = activeWorkspace?.kind ?? runtimeContext?.source ?? "Not configured";
  const environmentLabel = runtimeContext?.environment.name ?? (loading ? "Checking" : "Unverified");
  const displayName = runtimeContext?.displayName ?? (loading ? "Resolving user" : "User unavailable");
  const roleLabel = runtimeContext?.roleLabel ?? roleDef.title;
  const userInitials = runtimeInitials(displayName);

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

      <div className="px-4">
        <div className="flex items-center gap-2 rounded-2xl bg-white/70 border border-cloud px-3 py-2 text-[12.5px] text-stone">
          <Search className="h-3.5 w-3.5" />
          <span className="flex-1">Search everything</span>
          <kbd className="text-[10px] rounded bg-cloud/80 px-1.5 py-0.5 text-slate2">⌘K</kbd>
        </div>
      </div>

      <div className="px-4 mt-3">
        <NavLink to="/app" className="w-full inline-flex items-center justify-center gap-2 rounded-2xl gradient-primary text-white text-[13px] font-medium py-2.5 shadow-l2 hover-lift focus-ring">
          <Plus className="h-4 w-4" /> New Conversation
        </NavLink>
      </div>

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
            <div className="text-[11px] text-stone truncate">{roleLabel}</div>
          </div>
          <div className="flex items-center gap-1 text-[11px] text-success"><Circle className="h-2 w-2 fill-current" /> Auth</div>
        </div>
      </div>
    </aside>
  );
}
