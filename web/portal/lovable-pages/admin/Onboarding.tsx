"use client";
import { useState } from "react";
import { toast } from "sonner";
import { Building2, Plus, Loader2, ShieldCheck, ExternalLink, Info } from "lucide-react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { useOnboarding, type ConsentUrlView } from "@/lib/lovable-parity/data/onboarding";

export default function Onboarding() {
  const { records, loading, error, creating, create, getConsentUrl } = useOnboarding();
  const [open, setOpen] = useState(false);
  const [consent, setConsent] = useState<Record<string, ConsentUrlView>>({});
  const [form, setForm] = useState({ customerName: "", expectedEntraTenantOrDomain: "", adminContactEmail: "", targetWorkspaces: "workspace-claims", azureSubscriptionIds: "" });

  const submit = async () => {
    const res = await create({
      customerName: form.customerName,
      expectedEntraTenantOrDomain: form.expectedEntraTenantOrDomain,
      adminContactEmail: form.adminContactEmail,
      targetWorkspaces: split(form.targetWorkspaces),
      azureSubscriptionIds: split(form.azureSubscriptionIds),
    });
    if (res.ok) {
      toast.success("Pending tenant created");
      setOpen(false);
      setForm({ customerName: "", expectedEntraTenantOrDomain: "", adminContactEmail: "", targetWorkspaces: "workspace-claims", azureSubscriptionIds: "" });
    } else {
      toast.error(res.message ?? "Could not create onboarding");
    }
  };

  const loadConsent = async (id: string) => {
    const c = await getConsentUrl(id);
    setConsent((prev) => ({ ...prev, [id]: c }));
    if (!c.configured) toast(c.message);
  };

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Tenant Onboarding"
        title="Insurer onboarding & admin consent"
        sub="Create a pending insurer tenant, then send its administrator through Microsoft Entra tenant-wide admin consent. IIP stores the returned tenant only as a lookup key — runtime access comes from IIP's own membership store."
      />

      <div className="flex items-center justify-between mb-4">
        <div className="text-[12px] text-stone inline-flex items-center gap-1.5">
          <Info className="h-3.5 w-3.5" /> Admin consent uses the insurer's own Entra tenant (never “common”). The consent link is Not configured until Microsoft Entra is set up.
        </div>
        <button onClick={() => setOpen((v) => !v)} className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2 shadow-l2 hover-lift">
          <Plus className="h-4 w-4" /> New onboarding
        </button>
      </div>

      {open && (
        <GlassCard className="mb-6">
          <div className="grid md:grid-cols-2 gap-3">
            <Field label="Insurer legal / customer name *" value={form.customerName} onChange={(v) => setForm({ ...form, customerName: v })} />
            <Field label="Initial admin contact email *" value={form.adminContactEmail} onChange={(v) => setForm({ ...form, adminContactEmail: v })} />
            <Field label="Expected Entra tenant id or verified domain" value={form.expectedEntraTenantOrDomain} onChange={(v) => setForm({ ...form, expectedEntraTenantOrDomain: v })} className="md:col-span-2" />
            <Field label="Target workspaces (comma-separated)" value={form.targetWorkspaces} onChange={(v) => setForm({ ...form, targetWorkspaces: v })} />
            <Field label="Azure subscription id(s) for Managed App (comma-separated)" value={form.azureSubscriptionIds} onChange={(v) => setForm({ ...form, azureSubscriptionIds: v })} />
          </div>
          <div className="mt-4 flex justify-end gap-2">
            <button onClick={() => setOpen(false)} className="text-[12.5px] rounded-xl border border-cloud bg-white px-3 py-1.5 text-navy">Cancel</button>
            <button onClick={submit} disabled={creating || !form.customerName.trim() || !form.adminContactEmail.trim()} className="inline-flex items-center gap-2 text-[12.5px] rounded-xl gradient-primary text-white px-4 py-1.5 disabled:opacity-50">
              {creating ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null} Create pending tenant
            </button>
          </div>
        </GlassCard>
      )}

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading onboarding records…</div></GlassCard>
      ) : records.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <Building2 className="h-6 w-6 text-stone" />
            <div className="text-navy font-medium">{error ? "Onboarding unavailable" : "No onboarding sessions yet"}</div>
            <div className="text-[12.5px] text-stone max-w-md">{error ? error : "Create a pending insurer tenant with “New onboarding” to begin the admin-consent flow."}</div>
          </div>
        </GlassCard>
      ) : (
        <div className="space-y-3">
          {records.map((r) => (
            <GlassCard key={r.onboardingId} className="hover-lift">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
                <div className="h-11 w-11 rounded-2xl bg-gradient-secondary grid place-items-center text-white"><Building2 className="h-5 w-5" /></div>
                <div className="flex-1 min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="text-[14px] font-semibold text-navy">{r.customerName}</div>
                    <div className="break-all text-[11.5px] text-stone">· {r.onboardingId}</div>
                    <Badge tone={r.status === "Consented" || r.status === "Active" ? "success" : r.status === "Blocked" ? "error" : "warning"}>{r.status}</Badge>
                  </div>
                  <div className="break-words text-[12.5px] text-slate2 mt-0.5">{r.adminContactEmail} · {r.expectedEntraTenantOrDomain || "tenant TBD"}</div>
                  <div className="break-words text-[11.5px] text-stone mt-1">Internal tenant: <span className="font-mono break-all">{r.internalTenantId}</span> · Workspaces: {r.targetWorkspaces.join(", ") || "—"}{r.externalEntraTenantId ? <> · External Entra: <span className="font-mono break-all">{r.externalEntraTenantId}</span></> : null}</div>
                  <div className="break-words text-[11.5px] text-stone mt-1">Azure subscriptions: <span className="font-mono break-all">{r.azureSubscriptionIds.join(", ") || "not provided"}</span></div>
                  {consent[r.onboardingId] && (
                    <div className="mt-2 text-[12px]">
                      {consent[r.onboardingId].configured && consent[r.onboardingId].url ? (
                        <a href={consent[r.onboardingId].url!} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-info hover:underline"><ExternalLink className="h-3.5 w-3.5" /> Admin consent link</a>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-stone"><ShieldCheck className="h-3.5 w-3.5" /> {consent[r.onboardingId].message}</span>
                      )}
                    </div>
                  )}
                </div>
                {r.status === "Pending" && (
                  <button onClick={() => loadConsent(r.onboardingId)} className="inline-flex w-full shrink-0 items-center justify-center gap-1.5 rounded-xl border border-cloud bg-white px-3 py-1.5 text-[12.5px] text-navy sm:w-auto"><ShieldCheck className="h-3.5 w-3.5" /> Get consent link</button>
                )}
              </div>
            </GlassCard>
          ))}
        </div>
      )}
    </>
  );
}

function split(v: string): string[] {
  return v.split(",").map((x) => x.trim()).filter(Boolean);
}

function Field({ label, value, onChange, className }: { label: string; value: string; onChange: (v: string) => void; className?: string }) {
  return (
    <label className={"block " + (className ?? "")}>
      <span className="text-[11.5px] text-stone">{label}</span>
      <input value={value} onChange={(e) => onChange(e.target.value)} className="mt-1 w-full rounded-xl border border-cloud bg-white/80 px-3 py-2 text-[13px] text-navy outline-none focus:border-info" />
    </label>
  );
}
