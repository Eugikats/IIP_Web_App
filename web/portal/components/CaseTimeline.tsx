import { EmptyState } from "./States";

export type TimelineEvent = {
  eventId: string;
  eventType: string;
  actor: string;
  timestamp: string;
  summary: string;
  beforeStatus: string | null;
  afterStatus: string | null;
};

export function CaseTimeline({ events }: { events: TimelineEvent[] }) {
  if (events.length === 0) {
    return <EmptyState title="No timeline yet" description="Case events will appear after workflow activity is recorded." />;
  }

  return (
    <ol className="space-y-3">
      {events.map((event) => (
        <li key={event.eventId} className="rounded-xl border border-line bg-mist p-4 transition hover:border-accent/40">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div>
              <p className="text-sm font-semibold text-ink">{event.summary}</p>
              <p className="mt-2 text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{event.eventType} / {event.actor}</p>
            </div>
            <time className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{new Date(event.timestamp).toLocaleString()}</time>
          </div>
          {(event.beforeStatus || event.afterStatus) && (
            <p className="mt-3 text-xs text-muted font-mono">
              {event.beforeStatus ?? "none"} &rarr; {event.afterStatus ?? "none"}
            </p>
          )}
        </li>
      ))}
    </ol>
  );
}
