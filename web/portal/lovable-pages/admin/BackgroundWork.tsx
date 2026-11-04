"use client";

import { useCallback, useEffect, useState } from "react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { EmptyStateSurface, ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { loadBackgroundWork, retryBackgroundJob, type BackgroundJob, type BackgroundWork } from "@/lib/backgroundWork";

const TONE: Record<BackgroundJob["status"], PillTone> = { Pending: "warning", Running: "info", Succeeded: "success", DeadLettered: "danger" };
const LABEL: Record<BackgroundJob["status"], string> = { Pending: "Waiting", Running: "Running", Succeeded: "Done", DeadLettered: "Gave up" };

function when(value: string | null) {
  return value ? new Date(value).toLocaleString() : "";
}

/**
 * What the platform is doing in the background for this workspace (build-loop item 17): documents
 * being read and claims being reviewed, with what is waiting, running, done and stuck. A job that
 * has given up shows why, in plain words, and can be sent back. Refreshes itself.
 */
export default function BackgroundWorkPage() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [work, setWork] = useState<BackgroundWork | null>(null);
  const [status, setStatus] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    const result = await loadBackgroundWork(role, status || undefined);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setError(null);
    setWork(result.value);
  }, [role, status]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => void load(), 5000);
    return () => window.clearInterval(timer);
  }, [load]);

  async function sendBack(job: BackgroundJob) {
    setBusy(job.jobId);
    setNotice(null);
    const result = await retryBackgroundJob(role, job.jobId);
    setBusy(null);
    if (!result.ok) {
      setNotice(result.error);
      return;
    }
    setNotice(`${job.what} was sent back to the queue.`);
    await load();
  }

  return (
    <>
      <SectionHeader eyebrow="Admin" title="Background work" sub="Documents being read and claims being reviewed behind the scenes. Work that fails is tried again by itself; what still cannot be done is listed here with the reason." />
      {!work && !error && <GlassCard><LoadingSkeleton rows={4} /></GlassCard>}
      {error && <GlassCard><ErrorStateSurface title="The background work could not be loaded" message={error} onRetry={load} /></GlassCard>}
      {work && (
        <div className="space-y-4" data-testid="background-work">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {([["Waiting", work.pending], ["Running", work.running], ["Done", work.succeeded], ["Gave up", work.deadLettered]] as const).map(([label, count]) => (
              <GlassCard key={label}>
                <div className="text-[11px] font-semibold uppercase tracking-[0.08em] text-dls-text-muted">{label}</div>
                <div className="mt-1 text-[22px] font-bold text-dls-text-primary" data-testid={`count-${label.toLowerCase().replace(" ", "-")}`}>{count}</div>
              </GlassCard>
            ))}
          </div>

          <GlassCard>
            <div className="mb-3 flex flex-wrap items-center gap-3">
              <label htmlFor="job-status" className="text-[12.5px] font-semibold text-dls-text-primary">Show</label>
              <Select id="job-status" value={status} onChange={(event) => setStatus(event.target.value)}>
                <option value="">Everything</option>
                <option value="Pending">Waiting</option>
                <option value="Running">Running</option>
                <option value="DeadLettered">Gave up</option>
                <option value="Succeeded">Done</option>
              </Select>
              <Button variant="ghost" onClick={() => void load()}>Refresh</Button>
            </div>
            {notice && <p role="status" className="mb-3 text-[12.5px] text-dls-text-secondary">{notice}</p>}
            {work.jobs.length === 0 ? (
              <EmptyStateSurface title="Nothing here" message={status ? "No work in this state." : "No background work has been needed yet."} />
            ) : (
              <ul className="divide-y divide-dls-border-subtle">
                {work.jobs.map((job) => (
                  <li key={job.jobId} className="py-3" data-testid="background-job">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div className="min-w-0">
                        <div className="text-[13px] font-semibold text-dls-text-primary">{job.what}{job.claimReference ? `, claim ${job.claimReference}` : ""}</div>
                        <div className="text-[11.5px] text-dls-text-muted">
                          Started {when(job.createdAt)} · attempt {Math.max(job.attempts, 0)} of {job.maxAttempts}
                          {job.nextAttemptAt ? ` · next try ${when(job.nextAttemptAt)}` : ""}
                          {job.completedAt ? ` · finished ${when(job.completedAt)}` : ""}
                        </div>
                        {job.lastError && job.status !== "Succeeded" && <div className="mt-1 text-[12px] text-dls-text-secondary">{job.lastError}</div>}
                      </div>
                      <div className="flex items-center gap-2">
                        <StatusPill tone={TONE[job.status]} label={LABEL[job.status]} />
                        {job.status === "DeadLettered" && (
                          <Button variant="secondary" disabled={busy === job.jobId} onClick={() => void sendBack(job)} aria-label={`Send back: ${job.what}`}>Send back</Button>
                        )}
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </GlassCard>
        </div>
      )}
    </>
  );
}
