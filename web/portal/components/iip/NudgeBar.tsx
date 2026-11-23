"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "@/components/iip/NextRouterCompat";
import { useUI } from "@/context/UIStateContext";
import { useLiveVoiceContext } from "@/components/iip/LiveVoiceProvider";
import { fetchNudge, type Nudge } from "@/lib/voice/voiceApi";

/**
 * One quiet line when something needs the person, taken from the same brief they see on opening the app. The server
 * decides whether to say anything (a few a day, in working hours, never the same thing twice in three days), so this
 * only asks now and then while the app is open and in front. It can be heard as well as read. It is never shown over a
 * live conversation, and on a locked phone it says nothing about names or figures.
 */

const FIRST_LOOK_MILLISECONDS = 6_000;
const LOOK_EVERY_MILLISECONDS = 20 * 60 * 1000;

function phoneIsLocked(): boolean {
  try {
    return Boolean((window as unknown as { IipNative?: { isLocked?: () => boolean } }).IipNative?.isLocked?.());
  } catch {
    return false;
  }
}

export default function NudgeBar() {
  const { roleDef } = useUI();
  const { offered, active } = useLiveVoiceContext();
  const [nudge, setNudge] = useState<Nudge | null>(null);
  const [speaking, setSpeaking] = useState(false);
  const busy = useRef(false);
  const canSpeak = typeof window !== "undefined" && "speechSynthesis" in window;

  const look = useCallback(async () => {
    if (busy.current || document.visibilityState !== "visible") return;
    busy.current = true;
    try {
      const next = await fetchNudge(roleDef.key, phoneIsLocked());
      if (next) setNudge(next);
    } finally {
      busy.current = false;
    }
  }, [roleDef.key]);

  useEffect(() => {
    if (!offered || active) return;
    const first = window.setTimeout(() => void look(), FIRST_LOOK_MILLISECONDS);
    const repeat = window.setInterval(() => void look(), LOOK_EVERY_MILLISECONDS);
    return () => { window.clearTimeout(first); window.clearInterval(repeat); };
  }, [active, look, offered]);

  useEffect(() => () => { if (canSpeak) window.speechSynthesis.cancel(); }, [canSpeak]);

  const dismiss = () => {
    if (canSpeak) window.speechSynthesis.cancel();
    setSpeaking(false);
    setNudge(null);
  };

  const hear = () => {
    if (!nudge || !canSpeak) return;
    window.speechSynthesis.cancel();
    const speech = new SpeechSynthesisUtterance(nudge.text);
    speech.onend = () => setSpeaking(false);
    speech.onerror = () => setSpeaking(false);
    setSpeaking(true);
    window.speechSynthesis.speak(speech);
  };

  if (!nudge || active) return null;

  return (
    <aside
      role="status"
      aria-live="polite"
      data-testid="nudge"
      className="fixed left-4 right-4 top-16 z-40 rounded-lg border border-dls-border-subtle bg-dls-surface p-3 sm:left-auto sm:w-[360px]"
    >
      <p className="m-0 text-[13px] font-semibold text-dls-text-primary">Needs you</p>
      <p className="m-0 mt-1 text-[13px] leading-relaxed text-dls-text-secondary" data-testid="nudge-text">{nudge.text}</p>
      <div className="mt-3 flex flex-wrap gap-2">
        {nudge.href ? (
          <Link to={nudge.href} onClick={dismiss} data-testid="nudge-open" className="flex h-11 items-center rounded-full bg-[#1769FF] px-4 text-[13px] font-semibold text-white hover:bg-[#1258d6] dls-focus-outline">Open</Link>
        ) : null}
        {canSpeak ? (
          <button type="button" onClick={hear} disabled={speaking} className="h-11 rounded-full border border-dls-border-input px-4 text-[13px] font-medium text-dls-text-secondary hover:bg-dls-bg-page disabled:opacity-60 dls-focus-outline">
            {speaking ? "Reading" : "Hear it"}
          </button>
        ) : null}
        <button type="button" onClick={dismiss} className="h-11 rounded-full px-4 text-[13px] font-medium text-dls-text-secondary hover:bg-dls-bg-page dls-focus-outline">Dismiss</button>
      </div>
    </aside>
  );
}
