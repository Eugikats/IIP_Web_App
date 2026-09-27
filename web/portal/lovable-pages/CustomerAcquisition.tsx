"use client";
import { useState } from "react";
import { SectionHeader, GlassCard, StatusBadge } from "@/components/iip/Primitives";
import { Avatar } from "@/components/ui/Avatar";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import { Building2, ChevronDown, Plus, Loader2, Info, MessageCircle } from "lucide-react";
import { toast } from "sonner";
import { useSalesLeads, type SalesLeadDetail } from "@/lib/lovable-parity/data/salesLeads";

export default function CustomerAcquisition() {
  const { leads, loading, error, creating, createLead, getLeadDetail } = useSalesLeads();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ customerName: "", leadSource: "Manual capture", customerNeed: "", productFit: "", assignedSalesOfficer: "", contactInfo: "" });
  const [expandedLeadId, setExpandedLeadId] = useState<string | null>(null);
  const [detailByLeadId, setDetailByLeadId] = useState<Record<string, SalesLeadDetail>>({});
  const [detailLoading, setDetailLoading] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<Record<string, string>>({});

  async function toggleLead(leadId: string) {
    if (expandedLeadId === leadId) {
      setExpandedLeadId(null);
      return;
    }
    setExpandedLeadId(leadId);
    if (detailByLeadId[leadId]) return;
    setDetailLoading(leadId);
    const result = await getLeadDetail(leadId);
    setDetailLoading(null);
    if (result.ok) {
      setDetailByLeadId((prev) => ({ ...prev, [leadId]: result.detail }));
    } else {
      setDetailError((prev) => ({ ...prev, [leadId]: result.message }));
    }
  }

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
      <SectionHeader eyebrow="Customer Acquisition" title="Leads Inbox" sub="A lead entered by hand here, or created automatically when the WhatsApp assistant detects buying interest in a conversation. Open a lead to see the conversation it came from." />

      <div className="mb-4 flex items-center justify-between">
        <div className="inline-flex items-center gap-1.5 text-[12px] text-dls-text-secondary">
          <Info className="h-3.5 w-3.5" aria-hidden="true" /> A lead created from WhatsApp shows its source below; open it to see the conversation and what it still needs.
        </div>
        <Button variant="primary" onClick={() => setOpen((v) => !v)} icon={<Plus aria-hidden="true" />}>
          New lead
        </Button>
      </div>

      {open && (
        <GlassCard className="mb-6">
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Customer name *" value={form.customerName} onChange={(v) => setForm({ ...form, customerName: v })} />
            <Field label="Lead source" value={form.leadSource} onChange={(v) => setForm({ ...form, leadSource: v })} />
            <Field label="What does the customer need? *" value={form.customerNeed} onChange={(v) => setForm({ ...form, customerNeed: v })} className="md:col-span-2" />
            <Field label="Product fit" value={form.productFit} onChange={(v) => setForm({ ...form, productFit: v })} />
            <Field label="Assign to officer" value={form.assignedSalesOfficer} onChange={(v) => setForm({ ...form, assignedSalesOfficer: v })} />
            <Field label="Contact info" value={form.contactInfo} onChange={(v) => setForm({ ...form, contactInfo: v })} className="md:col-span-2" />
          </div>
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setOpen(false)}>Cancel</Button>
            <Button
              variant="primary"
              onClick={submit}
              disabled={creating || !form.customerName.trim() || !form.customerNeed.trim()}
              icon={creating ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}
            >
              Capture lead
            </Button>
          </div>
        </GlassCard>
      )}

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Leads unavailable" message={error} /></GlassCard>
      ) : leads.length === 0 ? (
        <GlassCard><EmptyStateSurface message="Capture a lead with “New lead”. Connected channel and import sources appear only after configuration." /></GlassCard>
      ) : (
        <div className="space-y-3">
          {leads.map((l) => {
            const isWhatsApp = l.leadSource.toLowerCase().startsWith("whatsapp");
            const isExpanded = expandedLeadId === l.leadId;
            const detail = detailByLeadId[l.leadId];
            return (
              <GlassCard key={l.leadId}>
                <button
                  type="button"
                  onClick={() => void toggleLead(l.leadId)}
                  className="dls-focus-outline flex w-full items-start gap-4 text-left"
                  aria-expanded={isExpanded}
                >
                  <Avatar id={l.leadId} initials={l.customerName.split(" ").map((x) => x[0]).slice(0, 2).join("")} size={40} />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <div className="text-[14px] font-bold text-dls-text-primary">{l.customerName}</div>
                    </div>
                    <div className="mt-0.5 inline-flex items-center gap-1.5 text-[12.5px] text-dls-text-secondary">
                      {isWhatsApp ? <MessageCircle className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" /> : <Building2 className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" />}
                      {l.leadSource} · {l.productFit}
                    </div>
                    <div className="mt-3 flex flex-wrap items-center gap-2">
                      <StatusBadge status={l.quoteStatus} />
                      {l.assignedSalesOfficer ? <span className="text-[11.5px] text-dls-text-muted">Assigned · {l.assignedSalesOfficer}</span> : <span className="text-[11.5px] text-dls-text-muted">Unassigned</span>}
                      {l.createdAt ? <span className="text-[11px] text-dls-text-muted">{new Date(l.createdAt).toLocaleDateString()}</span> : null}
                    </div>
                  </div>
                  <ChevronDown className={`h-4 w-4 shrink-0 text-dls-text-muted transition-transform ${isExpanded ? "rotate-180" : ""}`} aria-hidden="true" />
                </button>

                {isExpanded && (
                  <div className="mt-4 border-t border-dls-border-subtle pt-4">
                    {detailLoading === l.leadId ? (
                      <LoadingSkeleton rows={2} />
                    ) : detailError[l.leadId] ? (
                      <p className="text-[12.5px] text-dls-danger-text">{detailError[l.leadId]}</p>
                    ) : detail ? (
                      <div className="space-y-3 text-[12.5px]">
                        <div>
                          <div className="text-[10px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">What the customer needs</div>
                          <p className="mt-1 text-dls-text-primary">{detail.customerNeed || "Not recorded."}</p>
                        </div>
                        <div>
                          <div className="text-[10px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">
                            {isWhatsApp ? "Conversation summary" : "Notes"}
                          </div>
                          <p className="mt-1 whitespace-pre-wrap text-dls-text-secondary">{detail.conversationSummary || "Not recorded."}</p>
                        </div>
                        {detail.requiredInformation.length > 0 && (
                          <div>
                            <div className="text-[10px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Still needed</div>
                            <ul className="mt-1 list-disc space-y-0.5 pl-4 text-dls-text-secondary">
                              {detail.requiredInformation.map((item) => <li key={item}>{item}</li>)}
                            </ul>
                          </div>
                        )}
                        {detail.contactInfo && (
                          <div>
                            <div className="text-[10px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Contact</div>
                            <p className="mt-1 text-dls-text-primary">{detail.contactInfo}</p>
                          </div>
                        )}
                      </div>
                    ) : null}
                  </div>
                )}
              </GlassCard>
            );
          })}
        </div>
      )}
    </>
  );
}

function Field({ label, value, onChange, className }: { label: string; value: string; onChange: (v: string) => void; className?: string }) {
  return (
    <label className={"block " + (className ?? "")}>
      <span className="text-[11.5px] font-semibold text-dls-text-secondary">{label}</span>
      <div className="mt-1"><Input value={value} onChange={(e) => onChange(e.target.value)} /></div>
    </label>
  );
}
