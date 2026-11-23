"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { ChevronDown, ChevronUp, Lock, Mic, MicOff, PhoneOff, Volume2, VolumeX } from "lucide-react";
import AgentOrb, { ORB_LABEL, type OrbState } from "@/components/iip/AgentOrb";
import { CommandCenterBlocks } from "@/components/iip/CommandCenterAnswer";
import { Link } from "@/components/iip/NextRouterCompat";
import { cuesEnabled, setCuesEnabled } from "@/lib/liveCues";
import type { CommandCenterBlock } from "@/lib/lovable-parity/data/commandCenterQuery";
import type { LiveLanguage, LiveTurn } from "@/lib/useLiveVoice";

/**
 * The live conversation with the agent. The orb is the whole screen: it shows, and the word beneath says,
 * whether the agent is listening, working, speaking or has stopped to let the person speak. Swipe down
 * (or press Transcript) to read the conversation, swipe up to return to the orb. The conversation can be
 * minimised to a small bar and carries on while the person looks at the screen behind it.
 */
// Drawn on the page itself, not inside the content column, so the rail and header cannot sit above it.
function OnPage({ children }: { children: ReactNode }) {
  return createPortal(children, document.body);
}

const LANGUAGES: Array<{ code: LiveLanguage; label: string }> = [
  { code: "en", label: "English" },
  { code: "lg", label: "Luganda" },
  { code: "sw", label: "Swahili" },
];

export type LiveSheetView = {
  state: OrbState;
  level: number;
  turns: LiveTurn[];
  /** The agent's words as they are being said, paced to the voice. Null when nothing is being said. */
  caption: string | null;
  notice: string | null;
  muted: boolean;
  /** What the agent is doing right now, from the live step stream. */
  step: string | null;
  /** Charts, tables and drafts the agent put on screen during the call. */
  visuals: CommandCenterBlock[];
  /** Screens the agent offers to open. The person taps; nothing opens on its own. */
  offers: Array<{ label: string; href: string }>;
  language: LiveLanguage;
  /** Whether Luganda and Swahili can be chosen (needs the speech provider connected for this workspace). */
  otherLanguages: boolean;
  locked: boolean;
  /** Before the first realtime call: where the voice is processed, to be accepted. */
  consent: { region: string; onAccept: () => void } | null;
  /** The call ended with a problem and can be tried again. */
  canRetry: boolean;
  onLanguage: (language: LiveLanguage) => void;
  onRetry: () => void;
  onMute: () => void;
  onEnd: () => void;
};

export default function LiveVoiceSheet(view: LiveSheetView) {
  const { state, level, turns, caption, notice, muted, step, visuals, offers, language, otherLanguages, locked, consent, canRetry, onLanguage, onRetry, onMute, onEnd } = view;
  const [minimised, setMinimised] = useState(false);
  const [showTranscript, setShowTranscript] = useState(false);
  const [cues, setCues] = useState(true);
  const root = useRef<HTMLDivElement>(null);
  const touchStart = useRef<number | null>(null);

  useEffect(() => setCues(cuesEnabled()), []);

  useEffect(() => {
    if (minimised) return;
    const previous = document.activeElement as HTMLElement | null;
    root.current?.focus();
    return () => previous?.focus?.();
  }, [minimised]);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onEnd();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onEnd]);

  const last = turns[turns.length - 1];
  const word = ORB_LABEL[state];
  const MuteIcon = muted ? MicOff : Mic;
  const shownText = caption ?? last?.text ?? null;
  const shownWho = caption !== null ? "iip" : last?.who;

  if (minimised) {
    return (
      <OnPage>
      <div
        role="region"
        aria-label="Live conversation, minimised"
        data-testid="live-minimised"
        className="fixed bottom-4 right-4 z-50 flex items-center gap-2 rounded-full border border-dls-border-subtle bg-dls-surface py-1.5 pl-2 pr-1.5"
      >
        <button type="button" onClick={() => setMinimised(false)} className="flex items-center gap-2 rounded-full px-1 py-0.5 text-left dls-focus-outline" aria-label="Open the live conversation">
          <AgentOrb state={state} size={36} level={level} />
          <span className="pr-1 text-[13px] font-medium text-dls-text-primary" role="status">{word}</span>
        </button>
        <button type="button" onClick={onMute} aria-label={muted ? "Unmute" : "Mute"} aria-pressed={muted} className="flex h-10 w-10 items-center justify-center rounded-full text-dls-text-secondary hover:bg-dls-bg-page dls-focus-outline">
          <MuteIcon className="h-[18px] w-[18px]" aria-hidden="true" />
        </button>
        <button type="button" onClick={onEnd} aria-label="End the conversation" className="flex h-10 w-10 items-center justify-center rounded-full text-dls-danger-text hover:bg-dls-danger-tint dls-focus-outline">
          <PhoneOff className="h-[18px] w-[18px]" aria-hidden="true" />
        </button>
      </div>
      </OnPage>
    );
  }

  return (
    <OnPage>
    <div
      ref={root}
      role="dialog"
      aria-modal="true"
      aria-label="Live conversation with IIP"
      tabIndex={-1}
      data-testid="live-sheet"
      data-state={state}
      className="fixed inset-0 z-50 flex flex-col bg-dls-surface outline-none"
      onTouchStart={(event) => { touchStart.current = event.touches[0]?.clientY ?? null; }}
      onTouchEnd={(event) => {
        const from = touchStart.current;
        touchStart.current = null;
        const to = event.changedTouches[0]?.clientY;
        if (from === null || to === undefined) return;
        if (to - from > 40) setShowTranscript(true);
        else if (from - to > 40) setShowTranscript(false);
      }}
    >
      <header className="flex items-center justify-between px-4 pt-[max(0.75rem,env(safe-area-inset-top))]">
        <button type="button" onClick={() => setMinimised(true)} aria-label="Minimise the conversation" className="flex h-10 w-10 items-center justify-center rounded-full text-dls-text-secondary hover:bg-dls-bg-page dls-focus-outline">
          <ChevronDown className="h-5 w-5" aria-hidden="true" />
        </button>
        <span className="text-[13px] font-medium text-dls-text-secondary">Live with IIP</span>
        <button
          type="button"
          onClick={() => { const next = !cues; setCues(next); setCuesEnabled(next); }}
          aria-label={cues ? "Turn start and end sounds off" : "Turn start and end sounds on"}
          aria-pressed={!cues}
          className="flex h-10 w-10 items-center justify-center rounded-full text-dls-text-secondary hover:bg-dls-bg-page dls-focus-outline"
        >
          {cues ? <Volume2 className="h-[18px] w-[18px]" aria-hidden="true" /> : <VolumeX className="h-[18px] w-[18px]" aria-hidden="true" />}
        </button>
      </header>

      {locked ? (
        <p role="status" data-testid="live-locked" className="mx-4 mt-2 flex items-center justify-center gap-2 rounded-lg border border-dls-border-subtle bg-dls-bg-page px-3 py-2 text-[13px] text-dls-text-secondary">
          <Lock className="h-4 w-4 shrink-0" aria-hidden="true" />
          The phone is locked, so I will not read out any figures.
        </p>
      ) : null}

      <main className="flex min-h-0 flex-1 flex-col items-center overflow-y-auto px-6 py-4 text-center">
        <div className="my-auto flex w-full flex-col items-center">
          <AgentOrb state={state} size={176} level={level} className="shrink-0" />
          <p role="status" aria-live="polite" data-testid="live-state" className="m-0 mt-6 text-[20px] font-semibold text-dls-text-primary">{word}</p>
          <p className="m-0 mt-1.5 min-h-[20px] text-[13px] text-dls-text-secondary" data-testid="live-step">
            {step && (state === "processing" || state === "reconnecting" || state === "speaking")
              ? step
              : state === "muted" ? "The microphone is off. Tap the microphone to speak." : state === "reconnecting" ? "The connection dropped. Trying again." : ""}
          </p>

          {consent ? (
            <div data-testid="live-consent" className="mt-5 w-full max-w-sm rounded-lg border border-dls-border-subtle bg-dls-bg-page p-4 text-left">
              <p className="m-0 text-[14px] font-semibold text-dls-text-primary">Before your first call</p>
              <p className="m-0 mt-1.5 text-[13px] leading-relaxed text-dls-text-secondary">
                Your voice is processed by Microsoft&apos;s speech service in {consent.region}, outside Africa. Nothing you say is recorded or kept as audio. The words are saved in your conversation, the same as typing.
              </p>
              <div className="mt-3 flex gap-2">
                <button type="button" onClick={consent.onAccept} className="h-11 flex-1 rounded-full bg-[#1769FF] px-4 text-[13px] font-semibold text-white hover:bg-[#1258d6] dls-focus-outline">Continue</button>
                <button type="button" onClick={onEnd} className="h-11 flex-1 rounded-full border border-dls-border-input px-4 text-[13px] font-medium text-dls-text-secondary hover:bg-dls-surface dls-focus-outline">Not now</button>
              </div>
            </div>
          ) : null}

          {notice ? <p role="alert" className="m-0 mt-3 max-w-sm text-[13px] text-dls-danger-text">{notice}</p> : null}
          {canRetry ? (
            <button type="button" onClick={onRetry} className="mt-3 h-11 rounded-full border border-dls-border-input px-5 text-[13px] font-medium text-dls-text-primary hover:bg-dls-bg-page dls-focus-outline">Try again</button>
          ) : null}

          {shownText && !showTranscript ? (
            <p className="m-0 mt-6 line-clamp-4 max-w-md text-[15px] leading-relaxed text-dls-text-primary" data-testid="live-caption">
              <span className="sr-only">{shownWho === "you" ? "You said: " : "IIP said: "}</span>{shownText}
            </p>
          ) : null}
          {!shownText && !notice && !consent ? <p className="m-0 mt-6 max-w-sm text-[13px] text-dls-text-muted">Ask about claims, customers or acquisition. I will read out what I find, and you decide what happens next.</p> : null}

          {visuals.length > 0 && !showTranscript ? (
            <section aria-label="Shown during the conversation" data-testid="live-visuals" className="mt-5 w-full max-w-xl overflow-x-auto rounded-lg border border-dls-border-subtle bg-dls-surface p-3 text-left">
              <CommandCenterBlocks blocks={visuals} />
            </section>
          ) : null}

          {offers.length > 0 && !showTranscript ? (
            <ul aria-label="Open" className="m-0 mt-3 flex list-none flex-wrap justify-center gap-2 p-0">
              {offers.map((offer) => (
                <li key={offer.href}>
                  <Link
                    to={offer.href}
                    onClick={() => setMinimised(true)}
                    data-testid="live-offer"
                    className="inline-flex h-11 items-center rounded-full border border-dls-border-input px-4 text-[13px] font-medium text-dls-text-primary hover:bg-dls-bg-page dls-focus-outline"
                  >
                    Open {offer.label}
                  </Link>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      </main>

      {showTranscript ? (
        <section aria-label="Transcript" data-testid="live-transcript" className="max-h-[48dvh] overflow-y-auto border-t border-dls-border-subtle px-5 py-4">
          {turns.length === 0 ? (
            <p className="m-0 text-[13px] text-dls-text-muted">Nothing has been said yet.</p>
          ) : (
            <ol className="m-0 list-none space-y-3 p-0">
              {turns.map((turn) => (
                <li key={turn.id} className="text-[14px]">
                  <span className="block text-[11px] font-semibold uppercase tracking-[0.12em] text-dls-text-muted">{turn.who === "you" ? "You" : "IIP"}</span>
                  <span className="text-dls-text-primary">{turn.text}</span>
                </li>
              ))}
            </ol>
          )}
        </section>
      ) : null}

      <div role="group" aria-label="Language" data-testid="live-language" className="flex flex-wrap items-center justify-center gap-1.5 px-4 pt-2">
        {LANGUAGES.map((item) => {
          const unavailable = item.code !== "en" && !otherLanguages;
          return (
            <button
              key={item.code}
              type="button"
              aria-pressed={language === item.code}
              disabled={unavailable}
              title={unavailable ? "Luganda and Swahili need the speech provider connected for this workspace." : undefined}
              onClick={() => onLanguage(item.code)}
              className="h-10 rounded-full border border-dls-border-input px-3.5 text-[13px] font-medium text-dls-text-secondary hover:bg-dls-bg-page disabled:cursor-not-allowed disabled:opacity-40 aria-pressed:border-[#1769FF] aria-pressed:text-dls-text-primary dls-focus-outline"
            >
              {item.label}
            </button>
          );
        })}
      </div>

      <footer className="flex items-center justify-center gap-4 px-4 pb-[max(1.25rem,env(safe-area-inset-bottom))] pt-3">
        <button
          type="button"
          onClick={() => setShowTranscript((open) => !open)}
          aria-expanded={showTranscript}
          className="inline-flex h-11 items-center gap-1.5 rounded-full border border-dls-border-input px-4 text-[13px] font-medium text-dls-text-secondary hover:bg-dls-bg-page dls-focus-outline"
        >
          {showTranscript ? <ChevronUp className="h-4 w-4" aria-hidden="true" /> : <ChevronDown className="h-4 w-4" aria-hidden="true" />}
          Transcript
        </button>
        <button
          type="button"
          onClick={onMute}
          aria-pressed={muted}
          aria-label={muted ? "Unmute" : "Mute"}
          className="flex h-14 w-14 items-center justify-center rounded-full border border-dls-border-input text-dls-text-primary hover:bg-dls-bg-page dls-focus-outline"
        >
          <MuteIcon className="h-6 w-6" aria-hidden="true" />
        </button>
        <button
          type="button"
          onClick={onEnd}
          aria-label="End the conversation"
          className="flex h-14 w-14 items-center justify-center rounded-full bg-dls-danger-tint text-dls-danger-text hover:opacity-90 dls-focus-outline"
        >
          <PhoneOff className="h-6 w-6" aria-hidden="true" />
        </button>
      </footer>
    </div>
    </OnPage>
  );
}
