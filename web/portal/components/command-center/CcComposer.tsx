"use client";

import { ArrowUp, AudioLines, Mic, Paperclip, Sparkles, X } from "lucide-react";
import AgentOrb from "@/components/iip/AgentOrb";
import type { VoiceNoteState } from "@/lib/voiceNote";

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
  voice,
  onTalk,
  talkDisabledReason,
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
  /** A voice note: record, read back into the box, send. Left out when voice is not offered. */
  voice?: { state: VoiceNoteState; seconds: number; level: number; onStart: () => void; onStop: () => void; onCancel: () => void; disabledReason?: string };
  /** Opens the live voice conversation. Left out when it is not offered. */
  onTalk?: () => void;
  /** Why Talk cannot be used right now, shown as its tooltip. */
  talkDisabledReason?: string;
}) {
  const recording = voice?.state === "recording";
  const transcribing = voice?.state === "transcribing";
  const ready = (canSend ?? value.trim().length > 0) && !disabled && !recording && !transcribing;
  const clock = voice ? `${Math.floor(voice.seconds / 60)}:${String(voice.seconds % 60).padStart(2, "0")}` : "";
  return (
    <div className="cip-cc-composer pointer-events-auto mx-auto w-full max-w-3xl">
      <div className="flex flex-wrap items-center gap-x-1 gap-y-0 rounded-[26px] border border-slate-200/90 bg-white px-2 pb-1.5 pt-1 shadow-[0_12px_40px_rgba(15,23,42,0.12)] sm:flex-nowrap sm:gap-2 sm:rounded-full sm:py-1.5 sm:pl-1.5 sm:pr-1.5 transition-shadow focus-within:border-[#1769FF]/40 focus-within:shadow-[0_12px_40px_rgba(23,105,255,0.16)]">
        {onAttach ? (
          <button
            type="button"
            onClick={onAttach}
            disabled={attachDisabled || disabled}
            title={attachTitle}
            aria-label={attachLabel}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-[#1769FF] hover:bg-[#1769FF]/10 disabled:cursor-not-allowed disabled:opacity-40 max-sm:h-11 max-sm:w-11"
          >
            <Paperclip className="h-5 w-5" aria-hidden />
          </button>
        ) : (
          <Sparkles className="ml-2 h-5 w-5 shrink-0 text-[#1769FF]" aria-hidden />
        )}
        <span aria-hidden="true" className="min-w-0 flex-1 sm:hidden" />
        {recording && voice ? (
          <div className="flex min-w-0 flex-1 items-center gap-3 py-1 max-sm:order-first max-sm:basis-full max-sm:px-2 max-sm:py-3" role="status" aria-live="polite" data-testid="voice-recording">
            <AgentOrb state="listening" size={32} level={voice.level} />
            <span className="text-[14px] text-[#0B1640]">Recording <span className="tabular-nums">{clock}</span></span>
            <span className="hidden truncate text-xs text-slate-400 sm:inline">Tap Done when you have finished.</span>
          </div>
        ) : null}
        <input
          hidden={recording}
          type="text"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              if (ready) onSend();
            }
          }}
          placeholder={transcribing ? "Reading your voice note" : placeholder}
          disabled={disabled || transcribing}
          maxLength={2000}
          className="dls-field-in-focus-container min-w-0 flex-1 truncate border-0 bg-transparent py-2.5 text-[14px] text-[#0B1640] outline-none placeholder:truncate placeholder:text-slate-400 disabled:opacity-60 max-sm:order-first max-sm:basis-full max-sm:px-2 max-sm:pb-2 max-sm:pt-3 max-sm:text-[16px]"
          aria-label={ariaLabel}
        />
        {recording && voice ? (
          <>
            <button type="button" onClick={voice.onCancel} aria-label="Cancel the recording" className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-slate-500 hover:bg-slate-100 max-sm:h-11 max-sm:w-11">
              <X className="h-4 w-4" aria-hidden />
            </button>
            <button type="button" onClick={voice.onStop} className="h-10 shrink-0 rounded-full bg-[#1769FF] px-4 max-sm:h-11 max-sm:px-5 text-[13px] font-semibold text-white hover:bg-[#1258d6]">
              Done
            </button>
          </>
        ) : (
          <>
            {voice && !transcribing ? (
              <button
                type="button"
                onClick={voice.onStart}
                disabled={disabled || !!voice.disabledReason}
                aria-label="Record a voice note"
                title={voice.disabledReason ?? "Record a voice note"}
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-slate-500 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-40 max-sm:h-11 max-sm:w-11"
              >
                <Mic className="h-[18px] w-[18px]" aria-hidden />
              </button>
            ) : null}
            {onTalk && !transcribing ? (
              <button
                type="button"
                onClick={onTalk}
                disabled={disabled || !!talkDisabledReason}
                aria-label="Talk to IIP"
                title={talkDisabledReason ?? "Talk to IIP"}
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-[#1769FF] hover:bg-[#1769FF]/10 disabled:cursor-not-allowed disabled:opacity-40 max-sm:h-11 max-sm:w-11"
              >
                <AudioLines className="h-[18px] w-[18px]" aria-hidden />
              </button>
            ) : null}
            <button
              type="button"
              onClick={onSend}
              disabled={!ready}
              className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full transition-all max-sm:h-11 max-sm:w-11 disabled:cursor-not-allowed disabled:opacity-40 ${
                ready ? "bg-[#1769FF] text-white shadow-[0_6px_16px_rgba(23,105,255,0.35)] hover:bg-[#1258d6]" : "bg-slate-100 text-slate-400"
              }`}
              aria-label="Send message"
            >
              <ArrowUp className="h-4 w-4" strokeWidth={2.5} />
            </button>
          </>
        )}
      </div>
    </div>
  );
}
