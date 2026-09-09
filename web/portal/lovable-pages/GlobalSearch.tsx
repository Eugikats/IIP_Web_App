"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { ArrowRight, FileText, ListChecks, Search, ShieldCheck } from "lucide-react";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { loadGlobalSearch, type GlobalSearchResult, type GlobalSearchResultType } from "@/lib/lovable-parity/data/globalSearch";
import { useRuntimeContext } from "@/lib/runtimeContext";

const TYPE_ORDER: GlobalSearchResultType[] = ["Case", "Document", "WorkflowTask", "Approval"];

const TYPE_LABELS: Record<GlobalSearchResultType, string> = {
  Case: "Cases",
  Document: "Documents",
  WorkflowTask: "Workflow tasks",
  Approval: "Approvals",
};

function ResultIcon({ type }: { type: GlobalSearchResultType }) {
  const Icon = type === "Case" ? ShieldCheck : type === "Document" ? FileText : ListChecks;
  return <Icon className="h-4 w-4" aria-hidden="true" />;
}

function formatWhen(value: string) {
  const timestamp = Date.parse(value);
  return Number.isNaN(timestamp)
    ? "Updated time unavailable"
    : `Updated ${new Date(timestamp).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}`;
}

export default function GlobalSearch({ initialQuery }: { initialQuery: string }) {
  const router = useRouter();
  const { roleDef } = useUI();
  const { runtimeContext } = useRuntimeContext();
  const [draft, setDraft] = useState(initialQuery);
  const [response, setResponse] = useState<Awaited<ReturnType<typeof loadGlobalSearch>> | null>(null);
  const [loading, setLoading] = useState(initialQuery.trim().length >= 2);
  const [error, setError] = useState<string | null>(null);
  const query = initialQuery.trim();

  useEffect(() => {
    setDraft(initialQuery);
  }, [initialQuery]);

  useEffect(() => {
    if (query.length < 2) {
      setResponse(null);
      setLoading(false);
      setError(null);
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setError(null);
    void loadGlobalSearch(query, roleDef.key, runtimeContext?.userId ?? "", controller.signal)
      .then(setResponse)
      .catch((reason: unknown) => {
        if (reason instanceof DOMException && reason.name === "AbortError") return;
        setResponse(null);
        setError(reason instanceof Error ? reason.message : "Search is temporarily unavailable.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });

    return () => controller.abort();
  }, [query, roleDef.key, runtimeContext?.userId]);

  const groups = useMemo(() => {
    const results = response?.results ?? [];
    return TYPE_ORDER.map((type) => ({ type, items: results.filter((item) => item.type === type) }))
      .filter((group) => group.items.length > 0);
  }, [response]);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextQuery = draft.trim();
    if (nextQuery.length < 2) return;
    router.push(`/app/search?q=${encodeURIComponent(nextQuery)}`);
  }

  return (
    <>
      <SectionHeader
        eyebrow="Find work"
        title="Search IIP"
        sub="Results are limited to records and work you are currently authorized to view in this workspace."
      />

      <GlassCard className="mb-5">
        <form onSubmit={submit} role="search" className="flex flex-col gap-3 sm:flex-row">
          <div className="min-w-0 flex-1">
            <Input
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              minLength={2}
              maxLength={100}
              placeholder="Reference, customer, vehicle, document, task…"
              leadingIcon={<Search aria-hidden="true" />}
              aria-label="Search cases, documents, workflow tasks, and approvals"
            />
          </div>
          <Button type="submit" variant="primary" disabled={draft.trim().length < 2}>
            Search
          </Button>
        </form>
        <p className="mt-2 text-[11.5px] text-dls-text-muted">Enter at least two characters. Restricted records are not disclosed.</p>
      </GlassCard>

      <div aria-live="polite" aria-atomic="true" className="sr-only">
        {loading ? "Searching" : error ? error : response ? `${response.returnedCount} authorized results returned` : "Enter a search query"}
      </div>

      {query.length < 2 ? (
        <GlassCard><EmptyStateSurface message="Enter at least two characters to search." /></GlassCard>
      ) : loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Search unavailable" message={error} /></GlassCard>
      ) : response && response.results.length === 0 ? (
        <GlassCard><EmptyStateSurface title="No authorized results" message="Check the reference or try a broader term. Restricted records are not disclosed." /></GlassCard>
      ) : (
        <div className="space-y-5">
          <div className="flex flex-wrap items-center justify-between gap-2 text-[12px] text-dls-text-muted">
            <span>{response?.returnedCount ?? 0} authorized result{response?.returnedCount === 1 ? "" : "s"}</span>
            {response?.hasMore ? <span>More matches exist—refine the query to narrow results.</span> : null}
          </div>
          {groups.map((group) => (
            <section key={group.type} aria-labelledby={`search-group-${group.type}`}>
              <h2 id={`search-group-${group.type}`} className="mb-2 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">
                {TYPE_LABELS[group.type]} · {group.items.length}
              </h2>
              <GlassCard className="!p-0 overflow-hidden">
                <ul>
                  {group.items.map((item: GlobalSearchResult) => (
                    <li key={`${item.type}-${item.id}`} className="border-t border-dls-border-subtle first:border-t-0">
                      <Link href={item.href} className="dls-focus-outline flex items-center gap-3 p-4 transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page">
                        <span className="grid h-9 w-9 shrink-0 place-items-center rounded-dls-chip bg-dls-info-tint text-dls-info-text"><ResultIcon type={item.type} /></span>
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-[13px] font-semibold text-dls-text-primary">{item.title}</span>
                          <span className="mt-0.5 block truncate text-[12px] text-dls-text-secondary">{item.subtitle}</span>
                          <span className="mt-1 block text-[11px] text-dls-text-muted">{formatWhen(item.updatedAt)}</span>
                        </span>
                        <Badge tone="neutral" className="hidden sm:inline-flex">Matched: {item.matchReason}</Badge>
                        <ArrowRight className="h-4 w-4 shrink-0 text-dls-text-muted" aria-hidden="true" />
                      </Link>
                    </li>
                  ))}
                </ul>
              </GlassCard>
            </section>
          ))}
        </div>
      )}
    </>
  );
}
