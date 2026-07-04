"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { LineChart, Line, ResponsiveContainer, XAxis, YAxis, Tooltip, AreaChart, Area, BarChart, Bar, CartesianGrid } from "recharts";
import { trendData } from "@/lib/lovable-parity/mock";
import { Download, Sparkles } from "lucide-react";
import { toast } from "sonner";

export default function Executive() {
  return (
    <>
      <SectionHeader
        eyebrow="Executive Intelligence"
        title="Operations command view"
        sub="MVP metrics: claims volume, SLA breach rate, approval turnaround, document completeness. Financial and loss metrics remain placeholders pending data validation."
        action={<button onClick={() => toast.success("CSV export prepared")} className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] px-4 py-2 shadow-l2 hover-lift"><Download className="h-4 w-4" /> Export CSV</button>}
      />

      <div className="grid md:grid-cols-4 gap-3 mb-6">
        {[
          { l: "Avg TAT (days)", v: "4.1", d: "-0.5" },
          { l: "Doc completeness", v: "92%", d: "+3%" },
          { l: "AI assist volume", v: "1,284", d: "+128" },
          { l: "SLA at risk", v: "6", d: "-2" },
        ].map(k => (
          <div key={k.l} className="glass-subtle p-4">
            <div className="text-[11.5px] text-stone">{k.l}</div>
            <div className="mt-1 flex items-end justify-between">
              <div className="text-[26px] leading-none font-semibold text-navy">{k.v}</div>
              <div className="text-[11.5px] text-success">{k.d}</div>
            </div>
          </div>
        ))}
      </div>

      <div className="glass-elevated p-5 mb-6">
        <div className="flex items-center gap-2 mb-2">
          <div className="h-8 w-8 rounded-xl gradient-primary grid place-items-center shadow-l2"><Sparkles className="h-4 w-4 text-white" /></div>
          <div>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone">AI executive brief · prepared for human review</div>
            <div className="text-[14px] font-semibold text-navy">Motor Operations · this week</div>
          </div>
        </div>
        <p className="text-[13.5px] leading-[21px] text-slate2">
          Claims turnaround improved to <b>4.1 days</b> (−0.5 vs prior period). SLA-at-risk claims dropped by 2. Document completeness across intake channels rose to 92% (+3%), driven mostly by WhatsApp intake with prompted uploads. Approval turnaround averaged 3.4h across 9 pending items. No regulator-facing incidents this week. Advisory fraud signals are trending flat; three claims remain in supervisor review.
        </p>
      </div>

      <div className="grid lg:grid-cols-3 gap-4 mb-4">
        <GlassCard className="lg:col-span-2">
          <div className="text-[14px] font-semibold text-navy mb-1">Claims turnaround trend</div>
          <div className="text-[11.5px] text-stone mb-4">Average days to resolution · last 6 months (tenant timezone)</div>
          <div className="h-64">
            <ResponsiveContainer>
              <AreaChart data={trendData}>
                <defs>
                  <linearGradient id="g1" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#2563FF" stopOpacity={0.35} />
                    <stop offset="100%" stopColor="#7C4DFF" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid stroke="#EEF2F7" vertical={false} />
                <XAxis dataKey="m" tick={{ fill: "#6B7280", fontSize: 11 }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fill: "#6B7280", fontSize: 11 }} axisLine={false} tickLine={false} />
                <Tooltip contentStyle={{ borderRadius: 12, border: "1px solid #E5E7EB" }} />
                <Area type="monotone" dataKey="tat" stroke="#2563FF" strokeWidth={2} fill="url(#g1)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </GlassCard>

        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-1">SLA breach rate</div>
          <div className="text-[11.5px] text-stone mb-4">Claims trending toward breach</div>
          <div className="h-64">
            <ResponsiveContainer>
              <BarChart data={trendData}>
                <CartesianGrid stroke="#EEF2F7" vertical={false} />
                <XAxis dataKey="m" tick={{ fill: "#6B7280", fontSize: 11 }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fill: "#6B7280", fontSize: 11 }} axisLine={false} tickLine={false} />
                <Tooltip contentStyle={{ borderRadius: 12, border: "1px solid #E5E7EB" }} />
                <Bar dataKey="sla" fill="#7C4DFF" radius={[8,8,0,0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </GlassCard>
      </div>

      <div className="grid lg:grid-cols-3 gap-4">
        <GlassCard className="lg:col-span-2">
          <div className="text-[14px] font-semibold text-navy mb-1">AI questions answered</div>
          <div className="text-[11.5px] text-stone mb-4">Monthly Copilot volume across modules</div>
          <div className="h-56">
            <ResponsiveContainer>
              <LineChart data={trendData}>
                <CartesianGrid stroke="#EEF2F7" vertical={false} />
                <XAxis dataKey="m" tick={{ fill: "#6B7280", fontSize: 11 }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fill: "#6B7280", fontSize: 11 }} axisLine={false} tickLine={false} />
                <Tooltip contentStyle={{ borderRadius: 12, border: "1px solid #E5E7EB" }} />
                <Line type="monotone" dataKey="ai" stroke="#2563FF" strokeWidth={2.5} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </GlassCard>

        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-3">Operational bottlenecks</div>
          <div className="space-y-2.5 text-[13px]">
            {[
              { t: "Document follow-up cycles", v: "Avg 1.8", tone: "warning" },
              { t: "Supervisor approvals", v: "Avg 3.4h", tone: "info" },
              { t: "Broker response wait", v: "Avg 12h", tone: "warning" },
              { t: "Customer photo upload", v: "Avg 6h", tone: "success" },
            ].map(x => (
              <div key={x.t} className="flex items-center justify-between">
                <span className="text-slate2">{x.t}</span>
                <Badge tone={x.tone as any}>{x.v}</Badge>
              </div>
            ))}
          </div>
        </GlassCard>
      </div>
      <div className="mt-3 text-[11px] text-stone">Reports use tenant-local timezone (Africa/Kampala) with UTC storage.</div>
    </>
  );
}
