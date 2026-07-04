"use client";
import { Link } from "@/components/iip/NextRouterCompat";
import iipLogo from "@/assets/iip-logo";
import { Building2, ChevronRight } from "lucide-react";
import { Badge } from "@/components/iip/Primitives";

const workspaces = [
  { id: "GIC-PROD", name: "Global Insurance Co.", env: "PROD", role: "Admin", users: 42 },
  { id: "GIC-UAT", name: "Global Insurance Co. · UAT", env: "UAT", role: "Admin", users: 12 },
  { id: "GIC-SBX", name: "Global Insurance Co. · Sandbox", env: "SBX", role: "Admin", users: 4 },
];

export default function Workspaces() {
  return (
    <div className="min-h-screen ambient-bg flex items-center justify-center p-6">
      <div className="w-full max-w-2xl glass-elevated p-8">
        <div className="flex items-center gap-2 mb-6">
          <div className="h-9 w-9 rounded-xl bg-white grid place-items-center shadow-l2 ring-1 ring-cloud/70 overflow-hidden">
            <img src={iipLogo.url} alt="IIP" className="h-7 w-7 object-contain" />
          </div>
          <div>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Workspace</div>
            <div className="text-[16px] font-semibold text-navy">Choose a workspace</div>
          </div>
        </div>
        <div className="space-y-2">
          {workspaces.map((w) => (
            <Link key={w.id} to="/app" className="flex items-center gap-3 rounded-2xl border border-cloud bg-white/70 p-4 hover-lift">
              <div className="h-10 w-10 rounded-xl bg-[hsl(var(--info)/0.08)] text-info grid place-items-center"><Building2 className="h-5 w-5" /></div>
              <div className="flex-1">
                <div className="text-[14px] font-semibold text-navy">{w.name}</div>
                <div className="text-[11.5px] text-stone">{w.id} · {w.users} users · role: {w.role}</div>
              </div>
              <Badge tone={w.env === "PROD" ? "success" : w.env === "UAT" ? "info" : "neutral"}>{w.env}</Badge>
              <ChevronRight className="h-4 w-4 text-stone" />
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
