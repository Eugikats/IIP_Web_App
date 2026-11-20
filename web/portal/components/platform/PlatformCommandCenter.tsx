"use client";

/**
 * Organisation overview (/app/platform). The greeting and the questions follow the workspace Command Center; the cards
 * are the Intelligence Inbox notifications and the strip shows the intents. Questions go to the Assistant, which reads
 * the same governed data the rest of IIP reads.
 */

import { useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { ArrowUp } from "lucide-react";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { greeting as greetingFor } from "@/lib/platform/format";
import type { PlatformInsights, PlatformNotifications } from "@/lib/platform/types";
import { usePlatformQuery } from "@/lib/platform/use-platform";
import IntelligenceCards from "./IntelligenceCards";
import IntentStrip from "./IntentStrip";
import { PlatformErrorState, SectionDivider, inputClass, primaryButton } from "./ui";

export default function PlatformCommandCenter() {
  const router = useRouter();
  const { runtimeContext } = useRuntimeContext();
  const firstName = runtimeContext?.displayName?.trim().split(/\s+/)[0] ?? "";
  const [greeting, setGreeting] = useState("Good day");
  const [query, setQuery] = useState("");
  const insights = usePlatformQuery<PlatformInsights>("/insights");
  const notifications = usePlatformQuery<PlatformNotifications>("/notifications");

  useEffect(() => setGreeting(greetingFor()), []);

  const ask = (prompt: string) => {
    const text = prompt.trim();
    if (!text) return;
    router.push(`/app/assistant?q=${encodeURIComponent(text)}`);
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    ask(query);
  };

  const cards = insights.data?.cards ?? [];

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col pb-10 pt-6 sm:pt-12">
      <header className="text-center">
        <h1 className="text-balance text-[1.85rem] font-semibold leading-tight tracking-tight text-dls-text-primary sm:text-[2.35rem]">
          {greeting}
          {firstName ? (
            <>
              , <span className="text-dls-primary">{firstName}</span>
            </>
          ) : null}
        </h1>
        <p className="mx-auto mt-3 max-w-xl text-[15px] leading-relaxed text-dls-text-secondary sm:text-base">
          Your whole organisation in one place: every workspace, the automations watching your data, and what needs a person today.
        </p>
      </header>

      <form onSubmit={submit} className="mt-8 flex w-full items-center gap-2" role="search" aria-label="Ask the Assistant">
        <input
          className={inputClass}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Ask about any workspace, automation or signal"
          aria-label="Ask the Assistant"
        />
        <button type="submit" className={primaryButton} disabled={!query.trim()} aria-label="Send to the Assistant">
          <ArrowUp className="h-4 w-4" aria-hidden="true" />
        </button>
      </form>

      <section className="mt-10 w-full" aria-label="Intelligence from your organisation">
        <SectionDivider>Intelligence from your organisation</SectionDivider>
        {insights.error && cards.length === 0 ? (
          <PlatformErrorState title="Insights are not available right now" description={insights.error} />
        ) : (
          <IntelligenceCards cards={cards} loading={insights.loading} onAsk={ask} limit={9} />
        )}
        {!insights.loading && !insights.error && cards.length === 0 ? (
          <p className="text-center text-[13px] text-dls-text-muted">Insights appear as your workspaces gather data and your automations run.</p>
        ) : null}
      </section>

      {notifications.data && notifications.data.intents.length > 0 ? (
        <div className="mt-8">
          <IntentStrip intents={notifications.data.intents} />
        </div>
      ) : null}
    </div>
  );
}
