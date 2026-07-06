"use client";
import { useState } from "react";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Building2, Inbox, Plus, Loader2, Info } from "lucide-react";
import { toast } from "sonner";
import { useSalesLeads } from "@/lib/lovable-parity/data/salesLeads";

export default function CustomerAcquisition() {
  const { leads, loading, error, creating, createLead } = useSalesLeads();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ customerName: "", leadSource: "Manual capture", customerNeed: "", productFit: "", assignedSalesOfficer: "", contactInfo: "" });

  const submit = async () => {
    const res = await createLead(form);
    if (res.ok) {
      toast.success("Lead captured");
      setOpen(false);
      setForm({ customerName: "", leadSource: "Manual capture", customerNeed: "", productFit: "", assignedSalesOfficer: "", contactInfo: "" });
    } else {
      toast.error(res.message ?? "Could not create lead");
    }
  };

  return (
    <>
      <SectionHeader eyebrow="Customer Acquisition" title="Leads Inbox" sub="Manually captured leads today; channel and integration-fabric ingestion feed the same queue once connected. AI qualifies and drafts customer-safe product education for human review." />

      <div className="flex items-center justify-between mb-4">
        <div className="text-[12px] text-stone inline-flex items-center gap-1.5">
          <Info className="h-3.5 w-3.5" /> Channel ingestion (WhatsApp/email/SMS) &amp; fabric import are connected in a later gate — they write to this same list.
        </div>
        <button onClick={() => setOpen((v) => !v)} className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2 shadow-l2 hover-lift">
          <Plus className="h-4 w-4" /> New lead
        </button>
      </div>

      {open && (
        <GlassCard className="mb-6">
          <div className="grid md:grid-cols-2 gap-3">
            <Field label="Customer name *" value={form.customerName} onChange={(v) => setForm({ ...form, customerName: v })} />
            <Field label="Lead source" value={form.leadSource} onChange={(v) => setForm({ ...form, leadSource: v })} />
            <Field label="What does the customer need? *" value={form.customerNeed} onChange={(v) => setForm({ ...form, customerNeed: v })} className="md:col-span-2" />
            <Field label="Product fit" value={form.productFit} onChange={(v) => setForm({ ...form, productFit: v })} />
            <Field label="Assign to officer" value={form.assignedSalesOfficer} onChange={(v) => setForm({ ...form, assignedSalesOfficer: v })} />
            <Field label="Contact info" value={form.contactInfo} onChange={(v) => setForm({ ...form, contactInfo: v })} className="md:col-span-2" />
          </div>
          <div className="mt-4 flex justify-end gap-2">
            <button onClick={() => setOpen(false)} className="text-[12.5px] rounded-xl border border-cloud bg-white px-3 py-1.5 text-navy">Cancel</button>
            <button onClick={submit} disabled={creating || !form.customerName.trim() || !form.customerNeed.trim()} className="inline-flex items-center gap-2 text-[12.5px] rounded-xl gradient-primary text-white px-4 py-1.5 disabled:opacity-50">
              {creating ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null} Capture lead
            </button>
          </div>
        </GlassCard>
      )}

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading leads…</div></GlassCard>
      ) : leads.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <Inbox className="h-6 w-6 text-stone" />
            <div className="text-navy font-medium">{error ? "Leads unavailable" : "No leads yet"}</div>
            <div className="text-[12.5px] text-stone max-w-md">{error ? error : "Capture a lead with “New lead”, or connect a channel/import source in a later gate. Nothing is fabricated."}</div>
          </div>
        </GlassCard>
      ) : (
        <div className="space-y-3">
          {leads.map((l) => (
            <GlassCard key={l.leadId} className="hover-lift">
              <div className="flex items-start gap-4">
                <div className="h-11 w-11 rounded-2xl bg-gradient-secondary grid place-items-center text-white text-[13px] font-semibold">{l.customerName.split(" ").map((x) => x[0]).slice(0, 2).join("")}</div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <div className="text-[14px] font-semibold text-navy">{l.customerName}</div>
                    <div className="text-[11.5px] text-stone">· {l.leadId}</div>
                  </div>
                  <div className="text-[12.5px] text-slate2 mt-0.5 inline-flex items-center gap-1.5">
                    <Building2 className="h-3.5 w-3.5 text-stone" /> {l.leadSource} · {l.productFit}
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2 items-center">
                    <Badge tone={l.quoteStatus === "New" ? "info" : "violet"}>{l.quoteStatus}</Badge>
                    {l.assignedSalesOfficer ? <span className="text-[11.5px] text-stone">Assigned · {l.assignedSalesOfficer}</span> : <span className="text-[11.5px] text-stone">Unassigned</span>}
                    {l.createdAt ? <span className="text-[11px] text-stone">{new Date(l.createdAt).toLocaleDateString()}</span> : null}
                  </div>
                </div>
              </div>
            </GlassCard>
          ))}
        </div>
      )}
    </>
  );
}

function Field({ label, value, onChange, className }: { label: string; value: string; onChange: (v: string) => void; className?: string }) {
  return (
    <label className={"block " + (className ?? "")}>
      <span className="text-[11.5px] text-stone">{label}</span>
      <input value={value} onChange={(e) => onChange(e.target.value)} className="mt-1 w-full rounded-xl border border-cloud bg-white/80 px-3 py-2 text-[13px] text-navy outline-none focus:border-info" />
    </label>
  );
}
