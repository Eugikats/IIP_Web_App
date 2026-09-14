"use client";

import { FormEvent, useState } from "react";
import { ChevronDown, Info, Send, Sparkles } from "lucide-react";

export type ChatContextItem = { label: string; value: string };

/**
 * Pinned chat composer used by AI-chat surfaces only (not global chrome).
 * Render it as the last child of a flex-column container whose transcript
 * area is `flex-1 overflow-y-auto`, so the conversation scrolls while this
 * bar stays fixed at the bottom, ChatGPT-style.
 *
 * The optional `context` list is surfaced in a collapsible "context window"
 * so the operator can see exactly what grounds the assistant (transparency).
 */
export function PromptBar({
  value,
  onChange,
  onSubmit,
  busy = false,
  placeholder = "Ask IIP AI…",
  context = [],
  disclaimer = "IIP AI can make mistakes. Verify important information.",
}: {
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  busy?: boolean;
  placeholder?: string;
  context?: ChatContextItem[];
  disclaimer?: string;
}) {
  const [showContext, setShowContext] = useState(false);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (value.trim() && !busy) onSubmit();
  }

  return (
    <div className="sticky bottom-0 z-10 border-t border-line bg-surface/80 px-4 py-3 backdrop-blur-sm shadow-[0_-4px_16px_rgba(0,0,0,0.02)]">
      {context.length > 0 && (
        <>
          <button
            type="button"
            onClick={() => setShowContext((s) => !s)}
            className="mb-2 inline-flex items-center gap-1.5 text-[11px] font-semibold text-muted transition-colors hover:text-ink"
            aria-expanded={showContext}
          >
            <Info className="h-3.5 w-3.5" />
            Context window
            <ChevronDown className={`h-3 w-3 transition-transform ${showContext ? "rotate-180" : ""}`} />
          </button>
          {showContext && (
            <div className="mb-2 flex flex-wrap gap-x-4 gap-y-1 rounded-2xl border border-line bg-mist/60 px-3 py-2">
              {context.map((item) => (
                <span key={item.label} className="text-[11px] text-muted">
                  <span className="font-semibold text-ink">{item.label}:</span> {item.value}
                </span>
              ))}
            </div>
          )}
        </>
      )}

      <form
        onSubmit={handleSubmit}
        className="flex items-center gap-2 rounded-2xl border border-line bg-shell px-3 py-1.5 transition-colors focus-within:border-accent"
      >
        <Sparkles className="h-4 w-4 flex-shrink-0 text-violet" />
        <input
          className="min-w-0 flex-1 bg-transparent py-2 text-sm text-ink outline-none placeholder:text-muted/60"
          placeholder={placeholder}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={busy}
        />
        <button
          type="submit"
          disabled={!value.trim() || busy}
          className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-xl bg-brand-gradient text-white transition-all hover:-translate-y-0.5 hover:shadow-md disabled:translate-y-0 disabled:cursor-not-allowed disabled:opacity-50 disabled:shadow-none"
          title="Send"
        >
          <Send className="h-4 w-4" />
        </button>
      </form>

      <p className="mt-2 text-center text-[11px] text-muted">{disclaimer}</p>
    </div>
  );
}
