"use client";

import { useState } from "react";
import { ClipboardCheck, ListChecks } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { SectionHeader } from "@/components/iip/Primitives";
import AgentWorkItems from "@/lovable-pages/AgentWorkItems";
import Workflows from "@/lovable-pages/Workflows";

type ReviewView = "prepared" | "approvals";

export default function ReviewWorkspace({ initialView = "prepared" }: { initialView?: ReviewView }) {
  const [view, setView] = useState<ReviewView>(initialView);

  return (
    <>
      <SectionHeader
        eyebrow="Operations · Review & Approvals"
        title="Human review workspace"
        sub="One accountable queue for AI-prepared artifacts and consequential workflow approvals, with the boundary between them made explicit."
      />
      <div className="mb-4"><GuardrailBanner text={view === "prepared"
        ? "Prepared work may be accepted, corrected, or escalated here. Acceptance does not execute a regulated insurance decision."
        : "Approval actions advance a governed workflow only. Settlement, denial, payment, binding, pricing, fraud conclusions, and external sends remain explicit named-human actions."} /></div>

      <div className="mb-5 inline-flex rounded-2xl border border-cloud bg-white/70 p-1 shadow-l1">
        <button type="button" onClick={() => setView("prepared")} className={`inline-flex items-center gap-2 rounded-xl px-4 py-2 text-[12px] font-medium focus-ring ${view === "prepared" ? "bg-navy text-white shadow-l1" : "text-slate2 hover:bg-white"}`}>
          <ListChecks className="h-4 w-4" /> Prepared work
        </button>
        <button type="button" onClick={() => setView("approvals")} className={`inline-flex items-center gap-2 rounded-xl px-4 py-2 text-[12px] font-medium focus-ring ${view === "approvals" ? "bg-navy text-white shadow-l1" : "text-slate2 hover:bg-white"}`}>
          <ClipboardCheck className="h-4 w-4" /> Consequential approvals
        </button>
      </div>

      {view === "prepared" ? <AgentWorkItems embedded /> : <Workflows embedded />}
    </>
  );
}
