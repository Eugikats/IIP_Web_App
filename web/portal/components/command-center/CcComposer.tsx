"use client";

import { ArrowUp, Paperclip, Sparkles } from "lucide-react";

/**
 * The round "Ask a question" box, as in CIP's Command Center.
 *
 * With `onAttach` the leading icon becomes a paperclip button (the CIP composer); without it the
 * box keeps the decorative sparkle, so the Assistant page is unchanged.
 */
export default function CcComposer({
  value,
  onChange,
  onSend,
  disabled,
  placeholder = "Ask a question...",
  ariaLabel = "Ask the Command Center",
  onAttach,
  attachDisabled,
  attachLabel = "Attach a file",
  attachTitle,
  canSend,
}: {
  value: string;
  onChange: (value: string) => void;
  onSend: () => void;
  disabled?: boolean;
  placeholder?: string;
  ariaLabel?: string;
  onAttach?: () => void;
  attachDisabled?: boolean;
  attachLabel?: string;
  attachTitle?: string;
  /** Overrides the default "there is text to send" rule, for example when a file is attached. */
  canSend?: boolean;
}) {
  const ready = (canSend ?? value.trim().length > 0) && !disabled;
  return (
    <div className="cip-cc-composer pointer-events-auto mx-auto w-full max-w-3xl">
      <div className="flex items-center gap-2 rounded-full border border-slate-200/90 bg-white py-1.5 pl-1.5 pr-1.5 shadow-[0_12px_40px_rgba(15,23,42,0.12)] transition-shadow focus-within:border-[#1769FF]/40 focus-within:shadow-[0_12px_40px_rgba(23,105,255,0.16)]">
        {onAttach ? (
          <button
            type="button"
            onClick={onAttach}
            disabled={attachDisabled || disabled}
            title={attachTitle}
            aria-label={attachLabel}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-[#1769FF] hover:bg-[#1769FF]/10 disabled:cursor-not-allowed disabled:opacity-40"
          >
            <Paperclip className="h-5 w-5" aria-hidden />
          </button>
        ) : (
          <Sparkles className="ml-2 h-5 w-5 shrink-0 text-[#1769FF]" aria-hidden />
        )}
        <input
          type="text"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              if (ready) onSend();
            }
          }}
          placeholder={placeholder}
          disabled={disabled}
          maxLength={2000}
          className="min-w-0 flex-1 truncate border-0 bg-transparent py-2.5 text-[14px] text-[#0B1640] outline-none placeholder:truncate placeholder:text-slate-400 disabled:opacity-60"
          aria-label={ariaLabel}
        />
        <button
          type="button"
          onClick={onSend}
          disabled={!ready}
          className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full transition-all disabled:cursor-not-allowed disabled:opacity-40 ${
            ready ? "bg-[#1769FF] text-white shadow-[0_6px_16px_rgba(23,105,255,0.35)] hover:bg-[#1258d6]" : "bg-slate-100 text-slate-400"
          }`}
          aria-label="Send message"
        >
          <ArrowUp className="h-4 w-4" strokeWidth={2.5} />
        </button>
      </div>
    </div>
  );
}
