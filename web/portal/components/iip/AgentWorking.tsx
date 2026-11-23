"use client";

import { Check } from "lucide-react";
import AgentOrb from "@/components/iip/AgentOrb";

/**
 * What the agent is doing while it works: the orb at work, then each step it took, the latest still in
 * progress. Every line is a step the server reported from work it really ran, so a person can see the
 * answer being made and trust that nothing is staged for effect.
 */
export default function AgentWorking({ steps }: { steps: string[] }) {
  const current = steps.length > 0 ? steps[steps.length - 1] : "Starting";
  const done = steps.slice(0, -1);
  return (
    <div
      role="status"
      aria-live="polite"
      data-testid="agent-working"
      className="w-fit max-w-full rounded-2xl rounded-tl-md border border-slate-100 bg-white px-4 py-3"
    >
      <div className="flex items-center gap-2.5">
        <AgentOrb state="processing" size={28} />
        <span className="text-[13px] font-medium text-dls-text-primary">Working on it</span>
      </div>
      <ol className="m-0 mt-2.5 list-none space-y-1.5 p-0 text-[13px]">
        {done.map((label, index) => (
          <li key={`${index}-${label}`} data-testid="agent-step" data-state="done" className="flex items-center gap-2 text-dls-text-secondary">
            <Check className="h-3.5 w-3.5 shrink-0 text-dls-success-text" aria-hidden="true" />
            <span>{label}</span>
          </li>
        ))}
        <li data-testid="agent-step" data-state="active" className="flex items-center gap-2 text-dls-text-primary">
          <i className="inline-block h-1.5 w-1.5 shrink-0 rounded-full bg-dls-primary" aria-hidden="true" />
          <span>{current}</span>
        </li>
      </ol>
    </div>
  );
}
