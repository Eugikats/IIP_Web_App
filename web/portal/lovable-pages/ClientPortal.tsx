"use client";
import { ShieldCheck, MessageSquare, FileText, Plus } from "lucide-react";
import { Badge } from "@/components/iip/Primitives";
import { ChannelBadge } from "@/components/iip/ChatPrimitives";

export default function ClientPortal() {
  return (
    <div className="max-w-3xl mx-auto animate-fade-in">
      <div className="mb-6">
        <div className="text-[11px] uppercase tracking-[0.14em] text-violet2 mb-2">Customer portal</div>
        <h1 className="text-[26px] font-semibold text-navy">Welcome, Aisha.</h1>
        <p className="text-[13.5px] text-stone mt-1">
          A restricted, customer-safe view. Internal operations, reserves, staff notes, and risk indicators are never shown here.
        </p>
      </div>

      <div className="grid md:grid-cols-3 gap-3 mb-6">
        <button className="glass-subtle p-4 text-left hover-lift">
          <Plus className="h-4 w-4 text-info mb-2" />
          <div className="text-[13.5px] font-medium text-navy">Submit a new claim</div>
          <div className="text-[11.5px] text-stone mt-1">Guided intake, secure upload.</div>
        </button>
        <button className="glass-subtle p-4 text-left hover-lift">
          <MessageSquare className="h-4 w-4 text-info mb-2" />
          <div className="text-[13.5px] font-medium text-navy">Message your team</div>
          <div className="text-[11.5px] text-stone mt-1">WhatsApp, email or web chat.</div>
        </button>
        <button className="glass-subtle p-4 text-left hover-lift">
          <FileText className="h-4 w-4 text-info mb-2" />
          <div className="text-[13.5px] font-medium text-navy">My documents</div>
          <div className="text-[11.5px] text-stone mt-1">Policy schedule and receipts.</div>
        </button>
      </div>

      <div className="glass p-5">
        <div className="flex items-center justify-between mb-3">
          <div className="text-[13.5px] font-semibold text-navy">Your active claims</div>
          <ShieldCheck className="h-3.5 w-3.5 text-info" />
        </div>
        <div className="divide-y divide-cloud/70">
          {[
            { id: "CLM-1048", incident: "Rear-end collision, Kampala Rd", status: "In Review", channel: "WhatsApp", updated: "12 min ago" },
          ].map((c) => (
            <div key={c.id} className="py-3 flex items-center gap-4">
              <div className="flex-1 min-w-0">
                <div className="text-[13.5px] text-navy">{c.id} · {c.incident}</div>
                <div className="text-[11.5px] text-stone">Updated {c.updated}</div>
              </div>
              <ChannelBadge channel={c.channel} />
              <Badge tone="info">{c.status}</Badge>
            </div>
          ))}
        </div>
        <p className="text-[11.5px] text-stone mt-4">
          Final decisions on your claim are made by a human reviewer at Global Insurance Co.
        </p>
      </div>
    </div>
  );
}
