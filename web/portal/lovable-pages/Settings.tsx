"use client";
import { SectionHeader, GlassCard } from "@/components/iip/Primitives";
import { toast } from "sonner";

function Toggle({ label, desc, on = true }: { label: string; desc: string; on?: boolean }) {
  return (
    <div className="flex items-center justify-between py-3 border-b border-cloud last:border-0">
      <div>
        <div className="text-[13px] text-navy">{label}</div>
        <div className="text-[11.5px] text-stone">{desc}</div>
      </div>
      <button onClick={() => toast("Preference updated")} className={`relative h-5 w-9 rounded-full transition ${on ? "gradient-primary" : "bg-cloud"}`}>
        <span className={`absolute top-0.5 h-4 w-4 rounded-full bg-white shadow transition-all ${on ? "left-4" : "left-0.5"}`} />
      </button>
    </div>
  );
}

export default function Settings() {
  return (
    <>
      <SectionHeader eyebrow="Settings" title="Preferences" sub="Personal, notification, security, and AI assistance preferences for your workspace." />

      <div className="grid lg:grid-cols-2 gap-4">
        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-2">Profile</div>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Name" value="Timothy Okello" />
            <Field label="Role" value="Senior Claims Officer" />
            <Field label="Email" value="t.okello@global-ins.co" />
            <Field label="Region" value="Central" />
          </div>
        </GlassCard>

        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-2">Notifications</div>
          <Toggle label="SLA breach alerts" desc="Notify when a claim enters at-risk state" />
          <Toggle label="Escalations assigned to me" desc="Push + email" />
          <Toggle label="Copilot recommendations" desc="Daily morning digest" on={false} />
        </GlassCard>

        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-2">AI assistance</div>
          <Toggle label="Show confidence badges" desc="Always display AI confidence on extracted fields" />
          <Toggle label="Auto-draft customer updates" desc="Requires review before send" />
          <Toggle label="Suggest reassignments" desc="Based on workload analysis" on={false} />
        </GlassCard>

        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-2">Security</div>
          <Toggle label="Sign out on idle" desc="After 15 minutes" />
          <Toggle label="Require re-authentication for exports" desc="Regulator-ready audit export" />
        </GlassCard>
      </div>
    </>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <label className="text-[11.5px] font-medium text-slate2">{label}</label>
      <input defaultValue={value} className="mt-1 w-full rounded-2xl border border-cloud bg-white/80 px-4 py-2.5 text-[13.5px] text-navy outline-none focus:ring-2 focus:ring-info/40" />
    </div>
  );
}
