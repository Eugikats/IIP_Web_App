"use client";

import { Link } from "@/components/iip/NextRouterCompat";
import type { BriefState } from "@/lib/agentBrief";

const DOT = { danger: "bg-dls-danger-dot", warning: "bg-dls-warning-dot", info: "bg-dls-info-dot" } as const;
const WORD = { danger: "Overdue", warning: "Needs you", info: "For your information" } as const;

/**
 * What the agent found when the workspace opened. Loading, empty and error each say so in words; a
 * status is always a dot plus a word, never colour alone.
 */
export default function AgentBrief({ brief }: { brief: BriefState }) {
  return (
    <section aria-label="Your brief" data-testid="agent-brief" className="mx-auto mt-6 w-full max-w-xl rounded-xl border border-slate-200/90 bg-white px-4 py-3 text-left">
      <h2 className="m-0 text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-400">Your brief</h2>
      {brief.status === "loading" ? (
        <p role="status" className="m-0 mt-2 text-[13px] text-slate-500">Reading your workspace</p>
      ) : brief.status === "error" ? (
        <p role="status" className="m-0 mt-2 text-[13px] text-slate-500">The brief could not be read just now. You can still ask me anything below.</p>
      ) : brief.items.length === 0 ? (
        <p role="status" className="m-0 mt-2 text-[13px] text-slate-600">Nothing needs you right now. New claims, approvals and deadlines will show here.</p>
      ) : (
        <ul className="m-0 mt-2 list-none space-y-1.5 p-0">
          {brief.items.slice(0, 4).map((item) => (
            <li key={item.key}>
              <Link to={item.href} className="group flex items-start gap-2.5 rounded-md py-0.5 text-[13px] text-dls-text-primary dls-focus-outline">
                <i className={`mt-[7px] inline-block h-1.5 w-1.5 shrink-0 rounded-full ${DOT[item.tone]}`} aria-hidden="true" />
                <span className="min-w-0 flex-1">
                  <span className="sr-only">{WORD[item.tone]}: </span>
                  {item.sentence}
                </span>
                <span className="shrink-0 text-xs text-slate-400 group-hover:text-dls-primary">{WORD[item.tone]}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
