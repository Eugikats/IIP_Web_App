"use client";

import { useState } from "react";
import { ClipboardCheck, Layers3, ListChecks } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { SectionHeader } from "@/components/iip/Primitives";
import AgentWorkItems from "@/lovable-pages/AgentWorkItems";
import Workflows from "@/lovable-pages/Workflows";
import UniversalMyWork from "@/lovable-pages/UniversalMyWork";

type ReviewView = "all" | "prepared" | "approvals";

export default function ReviewWorkspace({ initialView = "all" }: { initialView?: ReviewView }) {
  const [view, setView] = useState<ReviewView>(initialView);

  return (
    <>
      <SectionHeader
        eyebrow="Operations · Review & Approvals"
        title="Human review workspace"
        sub="One permission-filtered view of owned operational outcomes, while every source keeps its own lifecycle and authority."
      />
      <div className="mb-4"><GuardrailBanner text={view === "all"
        ? "My Work is a current projection, not a new source of truth. Open the source workspace before acting; permissions and state are checked again there."
        : view === "prepared"
        ? "Prepared work may be accepted, corrected, or escalated here. Acceptance does not execute a regulated insurance decision."
        : "Approval actions advance a governed workflow only. Settlement, denial, payment, binding, pricing, fraud conclusions, and external sends remain explicit named-human actions."} /></div>

      <div className="mb-5 inline-flex rounded-2xl border border-line bg-white/70 p-1 shadow-l1">
        <button type="button" onClick={() => setView("all")} className={`inline-flex items-center gap-2 rounded-xl px-4 py-2 text-[12px] font-medium focus-ring ${view === "all" ? "bg-ink text-white shadow-l1" : "text-ink-soft hover:bg-white"}`}>
          <Layers3 className="h-4 w-4" /> My Work
        </button>
        <button type="button" onClick={() => setView("prepared")} className={`inline-flex items-center gap-2 rounded-xl px-4 py-2 text-[12px] font-medium focus-ring ${view === "prepared" ? "bg-ink text-white shadow-l1" : "text-ink-soft hover:bg-white"}`}>
          <ListChecks className="h-4 w-4" /> Prepared work
        </button>
        <button type="button" onClick={() => setView("approvals")} className={`inline-flex items-center gap-2 rounded-xl px-4 py-2 text-[12px] font-medium focus-ring ${view === "approvals" ? "bg-ink text-white shadow-l1" : "text-ink-soft hover:bg-white"}`}>
          <ClipboardCheck className="h-4 w-4" /> Consequential approvals
        </button>
      </div>

      {view === "all" ? <UniversalMyWork onOpenSpecialist={setView} /> : view === "prepared" ? <AgentWorkItems embedded /> : <Workflows embedded />}
    </>
  );
}
