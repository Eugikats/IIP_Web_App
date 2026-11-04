"use client";

import { useState } from "react";
import { Sparkles, ArrowRight } from "lucide-react";
import { useLocation, useNavigate } from "@/components/iip/NextRouterCompat";

/// Navigation model outcome F: chat is a bar available on every screen, not the landing screen
/// itself. Submitting a question here hands off to the full Assistant conversation at
/// /app/assistant (AIConversations.tsx picks the question up from ?q= and sends it once).
export default function AssistantBar() {
  const location = useLocation();
  const navigate = useNavigate();
  const [value, setValue] = useState("");

  // The Command Center has its own question box, so a second bar under it would only repeat it.
  if (location.pathname === "/app/assistant" || location.pathname.startsWith("/app/conversations/")) {
    return null;
  }

  function submit() {
    const question = value.trim();
    if (!question) return;
    setValue("");
    navigate(`/app/assistant?q=${encodeURIComponent(question)}`);
  }

  return (
    <section aria-label="Assistant prompt" className="shrink-0 border-t border-dls-border-subtle bg-dls-surface px-4 py-2">
      <div className="mx-auto flex max-w-[780px] items-center gap-2 rounded-dls-control border border-transparent px-2 py-1 focus-within:border-dls-primary focus-within:ring-[3px] focus-within:ring-dls-primary/[0.12]">
        <Sparkles className="h-4 w-4 shrink-0 text-dls-primary" aria-hidden="true" />
        <input
          value={value}
          onChange={(event) => setValue(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") submit();
          }}
          placeholder="Ask about your work…"
          aria-label="Your question"
          className="min-w-0 flex-1 bg-transparent text-[13px] text-dls-text-primary outline-none placeholder:text-dls-text-muted"
        />
        <button
          type="button"
          onClick={submit}
          disabled={!value.trim()}
          aria-label="Ask"
          className="dls-focus-outline flex h-8 w-8 shrink-0 items-center justify-center rounded-dls-control text-dls-text-secondary hover:bg-dls-bg-page disabled:opacity-40"
        >
          <ArrowRight className="h-4 w-4" aria-hidden="true" />
        </button>
      </div>
    </section>
  );
}
