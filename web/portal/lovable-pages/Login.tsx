"use client";
import { Link } from "@/components/iip/NextRouterCompat";
import { ShieldCheck, ChevronRight, Lock } from "lucide-react";
import iipLogo from "@/assets/iip-logo";

export default function Login() {
  return (
    <div className="min-h-screen ambient-bg relative flex items-center justify-center px-6 py-12">
      <div className="absolute top-6 left-6 flex items-center gap-2.5">
        <div className="h-9 w-9 rounded-xl bg-white grid place-items-center shadow-l2 ring-1 ring-cloud/70 overflow-hidden">
          <img src={iipLogo.url} alt="IIP logo" className="h-7 w-7 object-contain" />
        </div>
        <div className="leading-tight">
          <div className="text-[13px] font-semibold tracking-tight text-navy">Insurance Intelligence Platform</div>
          <div className="text-[10.5px] uppercase tracking-[0.14em] text-stone">IIP · Azure-native</div>
        </div>
      </div>

      <div className="w-full max-w-md glass-elevated p-8 animate-fade-in">
        <div className="mb-6">
          <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Sign in</div>
          <h1 className="text-[26px] leading-[32px] font-semibold text-navy mt-1">Enter your workspace</h1>
          <p className="text-[13.5px] text-stone mt-2">Human-reviewed insurance intelligence for regulated operations.</p>
        </div>

        <div className="space-y-4">
          <div>
            <label className="text-[11.5px] font-medium text-slate2">Tenant</label>
            <div className="mt-1 rounded-2xl border border-cloud bg-white/80 px-4 py-3 text-[13.5px] text-navy flex items-center justify-between">
              Global Insurance Co. <ChevronRight className="h-4 w-4 text-stone" />
            </div>
          </div>
          <div>
            <label className="text-[11.5px] font-medium text-slate2">Workspace</label>
            <div className="mt-1 rounded-2xl border border-cloud bg-white/80 px-4 py-3 text-[13.5px] text-navy flex items-center justify-between">
              Motor Ops · Production <ChevronRight className="h-4 w-4 text-stone" />
            </div>
          </div>

          <Link
            to="/app"
            className="mt-2 w-full inline-flex items-center justify-center gap-2 rounded-2xl gradient-primary text-white text-[13.5px] font-medium py-3.5 shadow-l3 hover-lift focus-ring"
          >
            <svg width="16" height="16" viewBox="0 0 21 21"><rect x="1" y="1" width="9" height="9" fill="#F35325"/><rect x="11" y="1" width="9" height="9" fill="#81BC06"/><rect x="1" y="11" width="9" height="9" fill="#05A6F0"/><rect x="11" y="11" width="9" height="9" fill="#FFBA08"/></svg>
            Continue with Microsoft
          </Link>

          <div className="flex items-center gap-2 pt-2 text-[11.5px] text-stone">
            <ShieldCheck className="h-3.5 w-3.5 text-success" />
            SSO enforced · Conditional Access · AAD tenant <span className="text-navy">global-ins.onmicrosoft.com</span>
          </div>
        </div>
      </div>

      <div className="absolute bottom-6 left-0 right-0 flex items-center justify-center text-[11.5px] text-stone gap-2">
        <Lock className="h-3 w-3" /> All actions are auditable · Regulator-ready logs · <a className="text-info hover:underline" href="#">Support</a>
      </div>
    </div>
  );
}
