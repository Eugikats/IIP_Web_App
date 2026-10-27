"use client";

import { useEffect, useState } from "react";
import { SideDrawer } from "@/components/ui/SideDrawer";
import { ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { getTimeline, type PersonTimeline } from "@/lib/demoCustomers";
import { shortWhen } from "./ReportParts";

const KIND: Record<string, string> = {
  consent: "Agreement",
  event: "At the bank",
  decision: "A rule decided",
  message: "Sent",
  reply: "Their answer",
};

/** Why this person was, or was not, messaged: the agreement they gave, what happened at the bank, each rule's decision with
 *  every check it made, the words that went out and what they said back, oldest first. */
export default function PersonTimelineDrawer({
  by,
  onClose,
}: {
  by: { eventId?: string; decisionId?: string } | null;
  onClose: () => void;
}) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [timeline, setTimeline] = useState<PersonTimeline | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [openChecks, setOpenChecks] = useState<number | null>(null);

  useEffect(() => {
    setTimeline(null);
    setError(null);
    setOpenChecks(null);
    if (!by) return;
    let cancelled = false;
    void getTimeline(role, by).then((result) => {
      if (cancelled) return;
      if (result.ok) setTimeline(result.value);
      else setError(result.error);
    });
    return () => {
      cancelled = true;
    };
  }, [by, role]);

  return (
    <SideDrawer
      open={by !== null}
      title={timeline?.customerName ? `Why ${timeline.customerName} was messaged` : "Why this person"}
      description={timeline ? timeline.phone : undefined}
      onClose={onClose}
    >
      {!timeline && !error ? <LoadingSkeleton rows={4} /> : null}
      {error ? <ErrorStateSurface title="The story could not be loaded" message={error} /> : null}
      {timeline ? (
        <div className="flex flex-col gap-4">
          <p className="m-0 text-[13px] text-slate-600">
            {timeline.consentSummary ? `Agreement to be contacted: ${timeline.consentSummary}` : "No agreement to be contacted is on record for this person."}
          </p>
          {timeline.entries.length === 0 ? (
            <p className="m-0 text-[13px] text-slate-500">Nothing has happened for this person yet.</p>
          ) : (
            <ol className="m-0 list-none space-y-3 p-0" aria-label="What happened, oldest first">
              {timeline.entries.map((entry, index) => (
                <li key={`${entry.at}-${index}`} className="rounded-xl border border-slate-200 px-3.5 py-3">
                  <p className="m-0 text-[11px] font-semibold uppercase tracking-wide text-slate-400">{KIND[entry.kind] ?? entry.kind} · {shortWhen(entry.at)}</p>
                  <p className="m-0 mt-1 text-[13.5px] font-semibold text-[#0B1640]">{entry.title}</p>
                  <p className="m-0 mt-0.5 text-[13px] leading-relaxed text-slate-600">{entry.detail}</p>
                  {entry.gates && entry.gates.length > 0 ? (
                    <>
                      <button
                        type="button"
                        className="mt-1.5 text-[12px] font-semibold text-[#1769FF] underline"
                        aria-expanded={openChecks === index}
                        onClick={() => setOpenChecks(openChecks === index ? null : index)}
                      >
                        {openChecks === index ? "Hide the checks" : "Show every check"}
                      </button>
                      {openChecks === index ? (
                        <ul className="m-0 mt-1.5 list-none space-y-1 p-0">
                          {entry.gates.map((gate) => (
                            <li key={gate.name} className="text-[12.5px] text-slate-600">
                              <span className="font-semibold text-[#0B1640]">{gate.name}: {gate.passed ? "passed" : "stopped here"}.</span> {gate.detail}
                            </li>
                          ))}
                        </ul>
                      ) : null}
                    </>
                  ) : null}
                </li>
              ))}
            </ol>
          )}
        </div>
      ) : null}
    </SideDrawer>
  );
}
