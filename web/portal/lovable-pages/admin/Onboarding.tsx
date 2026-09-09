"use client";
import { useId, useState } from "react";
import { toast } from "sonner";
import { Building2, Plus, Loader2, ShieldCheck, ExternalLink, Info } from "lucide-react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Field as UIField } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
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

      <div className="mb-4 flex items-center justify-between">
        <div className="inline-flex items-center gap-1.5 text-[12px] text-dls-text-secondary">
          <Info className="h-3.5 w-3.5" aria-hidden="true" /> Admin consent uses the insurer&apos;s own Entra tenant (never &ldquo;common&rdquo;). The consent link is Not configured until Microsoft Entra is set up.
        </div>
        <Button variant="primary" onClick={() => setOpen((v) => !v)} icon={<Plus aria-hidden="true" />}>
          New onboarding
        </Button>
      </div>

      {open && (
        <GlassCard className="mb-6">
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Insurer legal / customer name *" value={form.customerName} onChange={(v) => setForm({ ...form, customerName: v })} />
            <Field label="Initial admin contact email *" value={form.adminContactEmail} onChange={(v) => setForm({ ...form, adminContactEmail: v })} />
            <Field label="Expected Entra tenant id or verified domain" value={form.expectedEntraTenantOrDomain} onChange={(v) => setForm({ ...form, expectedEntraTenantOrDomain: v })} className="md:col-span-2" />
            <Field label="Target workspaces (comma-separated)" value={form.targetWorkspaces} onChange={(v) => setForm({ ...form, targetWorkspaces: v })} />
            <Field label="Azure subscription id(s) for Managed App (comma-separated)" value={form.azureSubscriptionIds} onChange={(v) => setForm({ ...form, azureSubscriptionIds: v })} />
          </div>
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setOpen(false)}>Cancel</Button>
            <Button
              variant="primary"
              onClick={submit}
              disabled={creating || !form.customerName.trim() || !form.adminContactEmail.trim()}
              icon={creating ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}
            >
              Create pending tenant
            </Button>
          </div>
        </GlassCard>
      )}

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Onboarding unavailable" message={error} /></GlassCard>
      ) : records.length === 0 ? (
        <GlassCard><EmptyStateSurface title="No onboarding sessions yet" message="Create a pending insurer tenant with “New onboarding” to begin the admin-consent flow." /></GlassCard>
      ) : (
        <div className="space-y-3">
          {records.map((r) => (
            <GlassCard key={r.onboardingId}>
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
                <div className="grid h-11 w-11 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><Building2 className="h-5 w-5" aria-hidden="true" /></div>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="text-[14px] font-bold text-dls-text-primary">{r.customerName}</div>
                    <div className="break-all text-[11.5px] text-dls-text-muted">· {r.onboardingId}</div>
                    <Badge tone={r.status === "Consented" || r.status === "Active" ? "success" : r.status === "Blocked" ? "error" : "warning"}>{r.status}</Badge>
                  </div>
                  <div className="mt-0.5 break-words text-[12.5px] text-dls-text-secondary">{r.adminContactEmail} · {r.expectedEntraTenantOrDomain || "tenant TBD"}</div>
                  <div className="mt-1 break-words text-[11.5px] text-dls-text-muted">Internal tenant: <span className="break-all font-mono">{r.internalTenantId}</span> · Workspaces: {r.targetWorkspaces.join(", ") || "—"}{r.externalEntraTenantId ? <> · External Entra: <span className="break-all font-mono">{r.externalEntraTenantId}</span></> : null}</div>
                  <div className="mt-1 break-words text-[11.5px] text-dls-text-muted">Azure subscriptions: <span className="break-all font-mono">{r.azureSubscriptionIds.join(", ") || "not provided"}</span></div>
                  {consent[r.onboardingId] && (
                    <div className="mt-2 text-[12px]">
                      {consent[r.onboardingId].configured && consent[r.onboardingId].url ? (
                        <a href={consent[r.onboardingId].url!} target="_blank" rel="noreferrer" className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-control font-semibold text-dls-primary hover:underline"><ExternalLink className="h-3.5 w-3.5" aria-hidden="true" /> Admin consent link</a>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-dls-text-muted"><ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" /> {consent[r.onboardingId].message}</span>
                      )}
                    </div>
                  )}
                </div>
                {r.status === "Pending" && (
                  <Button variant="secondary" onClick={() => loadConsent(r.onboardingId)} icon={<ShieldCheck aria-hidden="true" />} className="w-full shrink-0 sm:w-auto">
                    Get consent link
                  </Button>
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
  const id = useId();
  return (
    <div className={className}>
      <UIField label={label} htmlFor={id}>
        <Input id={id} value={value} onChange={(e) => onChange(e.target.value)} />
      </UIField>
    </div>
  );
}
