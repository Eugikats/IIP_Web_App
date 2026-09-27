"use client";

import { useCallback, useEffect, useState } from "react";
import { Loader2, RefreshCw } from "lucide-react";
import { Badge, GlassCard } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { getTranslationStatus, type TranslationStatus } from "@/lib/agentBehaviour";

const STATUS_BADGE = {
  Healthy: { tone: "success", label: "Working" },
  Failing: { tone: "warning", label: "Cannot be reached" },
  NotConfigured: { tone: "neutral", label: "Not configured" },
} as const;

/**
 * Whether the customer assistant can really answer in Luganda and Kiswahili right now. It shows what
 * the platform can reach, not what was configured, so an engine that is down never looks set up.
 * The wording comes from the API in plain words; this only lays it out.
 */
export default function TranslationStatusCard({ role }: { role: string }) {
  const [status, setStatus] = useState<TranslationStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    const result = await getTranslationStatus(role);
    if (result.ok) {
      setStatus(result.value);
      setError(null);
    } else {
      setError(result.error);
    }
    setLoading(false);
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  if (loading && !status) return <GlassCard><LoadingSkeleton rows={3} /></GlassCard>;
  if (error && !status) {
    return <GlassCard><ErrorStateSurface title="Language status could not be loaded" message={error} onRetry={() => void load()} /></GlassCard>;
  }
  if (!status) return null;

  const badge = STATUS_BADGE[status.status as keyof typeof STATUS_BADGE] ?? STATUS_BADGE.NotConfigured;

  return (
    <GlassCard>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="text-[14px] font-bold text-dls-text-primary">Languages</div>
          <p className="mt-1 max-w-2xl text-[12.5px] leading-5 text-dls-text-secondary">{status.summary}</p>
        </div>
        <Button variant="secondary" icon={loading ? <Loader2 className="animate-spin" /> : <RefreshCw />} disabled={loading} onClick={() => void load()}>
          Check again
        </Button>
      </div>

      <dl className="mt-4 grid gap-3 sm:grid-cols-3">
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
          <dt className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Translation engine</dt>
          <dd className="mt-1.5 flex flex-wrap items-center gap-2 text-[12.5px] font-semibold text-dls-text-primary">
            {status.engine}
            <Badge tone={badge.tone}>{badge.label}</Badge>
          </dd>
        </div>
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
          <dt className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Customers can write in</dt>
          <dd className="mt-1.5 flex flex-wrap items-center gap-1.5 text-[12.5px] font-semibold text-dls-text-primary">
            <Badge tone="neutral">English</Badge>
            {status.languages.map((language) => <Badge key={language.code} tone="info">{language.name}</Badge>)}
          </dd>
        </div>
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
          <dt className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Replies in their language</dt>
          <dd className="mt-1.5 text-[12.5px] font-semibold text-dls-text-primary">
            {status.allowUnreviewedTranslations ? "On, marked as machine translated" : "Off, replies go out in English"}
          </dd>
        </div>
      </dl>

      <p className="mt-3 text-[11.5px] leading-5 text-dls-text-muted">
        A message that cannot be translated is never guessed at: it goes to a person with the customer&apos;s own words.
        Policy numbers, links and claim references are always kept exactly as written. Which languages are on, and whether
        translated replies are allowed, is set by your platform administrator.
      </p>
    </GlassCard>
  );
}
