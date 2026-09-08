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

      <div className="mb-5 inline-flex rounded-dls-control border border-dls-border-subtle bg-dls-surface p-1">
        {(
          [
            { key: "all" as const, label: "My Work", icon: Layers3 },
            { key: "prepared" as const, label: "Prepared work", icon: ListChecks },
            { key: "approvals" as const, label: "Consequential approvals", icon: ClipboardCheck },
          ]
        ).map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => setView(tab.key)}
            className={
              "dls-focus-outline inline-flex items-center gap-2 rounded-dls-control px-4 py-2 text-[12px] font-bold transition-colors duration-dls-fast ease-dls " +
              (view === tab.key ? "bg-dls-primary text-dls-text-inverse" : "text-dls-text-secondary hover:bg-dls-bg-page")
            }
          >
            <tab.icon className="h-4 w-4" aria-hidden="true" /> {tab.label}
          </button>
        ))}
      </div>

      {view === "all" ? <UniversalMyWork onOpenSpecialist={setView} /> : view === "prepared" ? <AgentWorkItems embedded /> : <Workflows embedded />}
    </>
  );
}
