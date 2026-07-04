"use client";
import { Link, useNavigate } from "@/components/iip/NextRouterCompat";
import { useState } from "react";
import iipLogo from "@/assets/iip-logo";

export default function Onboarding() {
  const nav = useNavigate();
  const [step, setStep] = useState(0);
  const steps = [
    { title: "Tenant details", desc: "Register your insurer, region, and default currency." },
    { title: "Identity", desc: "Connect Entra ID for staff SSO and directory sync." },
    { title: "Product lines", desc: "Enable Motor MVP now — add other lines later." },
    { title: "Channels", desc: "Choose which customer channels to activate." },
    { title: "Review & finish", desc: "Confirm setup and enter your workspace." },
  ];
  return (
    <div className="min-h-screen ambient-bg flex items-center justify-center p-6">
      <div className="w-full max-w-2xl glass-elevated p-8">
        <div className="flex items-center gap-2 mb-6">
          <div className="h-9 w-9 rounded-xl bg-white grid place-items-center shadow-l2 ring-1 ring-cloud/70 overflow-hidden">
            <img src={iipLogo.url} alt="IIP" className="h-7 w-7 object-contain" />
          </div>
          <div>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Tenant onboarding</div>
            <div className="text-[16px] font-semibold text-navy">Set up your Insurance Intelligence Platform</div>
          </div>
        </div>
        <div className="flex items-center gap-1.5 mb-6">
          {steps.map((s, i) => (
            <div key={s.title} className={`flex-1 h-1 rounded-full ${i <= step ? "gradient-primary" : "bg-cloud"}`} />
          ))}
        </div>
        <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Step {step + 1} of {steps.length}</div>
        <div className="text-[20px] font-semibold text-navy">{steps[step].title}</div>
        <p className="text-[13px] text-stone mt-1">{steps[step].desc}</p>

        <div className="mt-6 grid gap-3">
          <input className="rounded-xl border border-cloud bg-white/80 px-3 py-2.5 text-[13px] placeholder:text-stone" placeholder="Sample field for this step…" />
          <input className="rounded-xl border border-cloud bg-white/80 px-3 py-2.5 text-[13px] placeholder:text-stone" placeholder="Another sample field…" />
        </div>

        <div className="mt-8 flex items-center justify-between">
          <Link to="/" className="text-[12.5px] text-stone hover:text-navy">Cancel</Link>
          <div className="flex items-center gap-2">
            {step > 0 && <button onClick={() => setStep(step - 1)} className="text-[13px] rounded-2xl border border-cloud bg-white px-4 py-2 text-navy">Back</button>}
            {step < steps.length - 1
              ? <button onClick={() => setStep(step + 1)} className="text-[13px] rounded-2xl gradient-primary text-white px-4 py-2 shadow-l2">Continue</button>
              : <button onClick={() => nav("/workspaces")} className="text-[13px] rounded-2xl gradient-primary text-white px-4 py-2 shadow-l2">Finish setup</button>}
          </div>
        </div>
      </div>
    </div>
  );
}
