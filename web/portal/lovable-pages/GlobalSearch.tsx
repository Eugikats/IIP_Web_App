"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { ArrowRight, FileText, ListChecks, Loader2, Search, ShieldCheck } from "lucide-react";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
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
          <label className="flex min-w-0 flex-1 items-center gap-2 rounded-xl border border-cloud bg-white px-3 py-2.5 focus-within:ring-2 focus-within:ring-info/30">
            <Search className="h-4 w-4 text-stone" aria-hidden="true" />
            <span className="sr-only">Search cases, documents, workflow tasks, and approvals</span>
            <input
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              minLength={2}
              maxLength={100}
              placeholder="Reference, customer, vehicle, document, task…"
              className="min-w-0 flex-1 bg-transparent text-[13px] text-navy outline-none placeholder:text-stone"
            />
          </label>
          <button
            type="submit"
            disabled={draft.trim().length < 2}
            className="rounded-xl bg-primary px-5 py-2.5 text-[13px] font-medium text-white shadow-l1 disabled:cursor-not-allowed disabled:opacity-50 focus-ring"
          >
            Search
          </button>
        </form>
        <p className="mt-2 text-[11.5px] text-stone">Enter at least two characters. Restricted records are not disclosed.</p>
      </GlassCard>

      <div aria-live="polite" aria-atomic="true" className="sr-only">
        {loading ? "Searching" : error ? error : response ? `${response.returnedCount} authorized results returned` : "Enter a search query"}
      </div>

      {query.length < 2 ? (
        <GlassCard><div className="py-10 text-center text-sm text-stone">Enter at least two characters to search.</div></GlassCard>
      ) : loading ? (
        <GlassCard><div className="flex items-center justify-center gap-2 py-10 text-sm text-stone"><Loader2 className="h-4 w-4 animate-spin" />Searching authorized records…</div></GlassCard>
      ) : error ? (
        <GlassCard>
          <div className="py-10 text-center">
            <div className="font-medium text-navy">Search unavailable</div>
            <p className="mt-1 text-sm text-stone">{error}</p>
          </div>
        </GlassCard>
      ) : response && response.results.length === 0 ? (
        <GlassCard>
          <div className="py-10 text-center">
            <Search className="mx-auto h-6 w-6 text-stone" aria-hidden="true" />
            <div className="mt-3 font-medium text-navy">No authorized results</div>
            <p className="mt-1 text-sm text-stone">Check the reference or try a broader term. Restricted records are not disclosed.</p>
          </div>
        </GlassCard>
      ) : (
        <div className="space-y-5">
          <div className="flex flex-wrap items-center justify-between gap-2 text-[12px] text-stone">
            <span>{response?.returnedCount ?? 0} authorized result{response?.returnedCount === 1 ? "" : "s"}</span>
            {response?.hasMore ? <span>More matches exist—refine the query to narrow results.</span> : null}
          </div>
          {groups.map((group) => (
            <section key={group.type} aria-labelledby={`search-group-${group.type}`}>
              <h2 id={`search-group-${group.type}`} className="mb-2 text-[11px] uppercase tracking-[0.14em] text-stone">
                {TYPE_LABELS[group.type]} · {group.items.length}
              </h2>
              <GlassCard className="!p-0 overflow-hidden">
                <ul>
                  {group.items.map((item: GlobalSearchResult) => (
                    <li key={`${item.type}-${item.id}`} className="border-t border-cloud/70 first:border-t-0">
                      <Link href={item.href} className="group flex items-center gap-3 p-4 hover:bg-white/60 focus-ring">
                        <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-info/10 text-info"><ResultIcon type={item.type} /></span>
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-[13px] font-medium text-navy">{item.title}</span>
                          <span className="mt-0.5 block truncate text-[12px] text-stone">{item.subtitle}</span>
                          <span className="mt-1 block text-[10.5px] text-stone">{formatWhen(item.updatedAt)}</span>
                        </span>
                        <Badge tone="neutral" className="hidden sm:inline-flex">Matched: {item.matchReason}</Badge>
                        <ArrowRight className="h-4 w-4 shrink-0 text-stone transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
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
