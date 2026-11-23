"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import LiveVoiceSheet from "@/components/iip/LiveVoiceSheet";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, canUseLocalTestDoubles } from "@/lib/config";
import { liveThread } from "@/lib/liveThread";
import { cueEnd, cueStart, unlockAudio } from "@/lib/liveCues";
import { useCommandCenterQuery, type CommandCenterBlock } from "@/lib/lovable-parity/data/commandCenterQuery";
import { nativeCall, onNativeCallAction } from "@/lib/nativeCall";
import { nativeDevice, onNativeTalk } from "@/lib/nativeDevice";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { useLiveVoice, type LiveLanguage, type LiveTurn } from "@/lib/useLiveVoice";
import { RealtimeVoiceEngine } from "@/lib/voice/realtimeEngine";
import type { EngineHandlers, MicrophoneLike, VoiceOrbState } from "@/lib/voice/types";
import { createVoiceApi, fetchVoiceStatus, registerDevice, type VoiceStatus } from "@/lib/voice/voiceApi";

/**
 * The live conversation, kept in the app shell so it carries on while the person moves between screens: the sheet
 * and its minimised bar are drawn here, and what was said is added to the written conversation through the shared
 * thread, whichever screen the person is on when the call ends.
 *
 * Two ways of talking sit behind one screen. The realtime call (WebRTC to the voice service) is used for English when the
 * environment has it and the browser can do it. A turn-by-turn conversation (record, transcribe, ask, speak) is used
 * otherwise, and for Luganda and Swahili. The person never has to know which.
 */

export type LiveVoiceValue = {
  /** The person may use live voice at all. */
  offered: boolean;
  /** Why Talk cannot be pressed right now, in words for the person. Undefined when it can. */
  unavailableReason?: string;
  active: boolean;
  start: () => void;
};

const INERT: LiveVoiceValue = { offered: false, active: false, start: () => undefined };
const LiveVoiceContext = createContext<LiveVoiceValue>(INERT);

export function useLiveVoiceContext(): LiveVoiceValue {
  return useContext(LiveVoiceContext);
}

const LANGUAGE_KEY = "iip.live.language";
const CONSENT_KEY = "iip.live.consent";
/** How long a wake phrase may stay unconfirmed before it is taken as heard. */
const WAKE_WAIT_MILLISECONDS = 6_000;

function readStored(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStored(key: string, value: string) {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    /* storage can be blocked; the choice then lasts for this visit only */
  }
}

/** Only screens inside IIP can be offered: a link the agent produced is never an address elsewhere. */
function isOwnScreen(href: string) {
  return href.startsWith("/") && !href.startsWith("//") && !href.includes("\\");
}

let nextLine = 1;

export default function LiveVoiceProvider({ children }: { children: ReactNode }) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const { runtimeContext, parityMode } = useRuntimeContext();
  const permissions = runtimeContext?.permissions ?? [];
  const offered = canUseLocalTestDoubles || parityMode || permissions.includes("ViewCommandCenter");
  const { ask, steps } = useCommandCenterQuery();

  const [status, setStatus] = useState<VoiceStatus | null | undefined>(undefined);
  const [language, setLanguage] = useState<LiveLanguage>("en");
  const [locked, setLocked] = useState(false);
  const [consentOpen, setConsentOpen] = useState(false);
  const [mode, setMode] = useState<"realtime" | "turns" | null>(null);

  // The realtime call's own view of itself.
  const [rtOpen, setRtOpen] = useState(false);
  const [rtState, setRtState] = useState<VoiceOrbState>("idle");
  const [rtLevel, setRtLevel] = useState(0);
  const [rtLines, setRtLines] = useState<LiveTurn[]>([]);
  const [rtCaption, setRtCaption] = useState<string | null>(null);
  const [rtNotice, setRtNotice] = useState<string | null>(null);
  const [rtStep, setRtStep] = useState<string | null>(null);
  const [rtMuted, setRtMuted] = useState(false);
  const [rtVisuals, setRtVisuals] = useState<CommandCenterBlock[]>([]);
  const [rtOffers, setRtOffers] = useState<Array<{ label: string; href: string }>>([]);
  const [rtFailed, setRtFailed] = useState(false);
  const engine = useRef<RealtimeVoiceEngine | null>(null);
  const restartAfterEnd = useRef(false);

  useEffect(() => {
    const stored = readStored(LANGUAGE_KEY);
    if (stored === "lg" || stored === "sw") setLanguage(stored);
  }, []);

  useEffect(() => {
    if (!offered) return;
    let cancelled = false;
    void fetchVoiceStatus(role).then((result) => { if (!cancelled) setStatus(result); });
    return () => { cancelled = true; };
  }, [offered, role]);

  // The phone tells the page when it is locked (the Android app) so no figure is read out to whoever holds it.
  useEffect(() => {
    const native = (window as unknown as { IipNative?: { isLocked?: () => boolean } }).IipNative;
    try {
      if (native?.isLocked?.()) setLocked(true);
    } catch {
      /* an older app without the lock bridge: the phone is treated as unlocked */
    }
    const onLock = (event: Event) => setLocked(Boolean((event as CustomEvent<{ locked?: boolean }>).detail?.locked));
    window.addEventListener("iip:device-lock", onLock);
    return () => window.removeEventListener("iip:device-lock", onLock);
  }, []);

  // ---- turn by turn ---------------------------------------------------------------------------

  const turns = useLiveVoice({
    role,
    language,
    locked,
    ask: (question) => ask(question, liveThread.conversationId, crypto.randomUUID()),
    onExchange: (question, answer) => {
      liveThread.setConversationId(answer.conversationId);
      liveThread.publish({ kind: "exchange", question, answer });
      if (answer.conversationId) window.dispatchEvent(new CustomEvent("iip:conversations-changed"));
    },
  });

  // ---- realtime -------------------------------------------------------------------------------

  const handlers = useMemo<EngineHandlers>(() => ({
    state: setRtState,
    level: setRtLevel,
    caption: (who, text, final) => { if (who === "iip") setRtCaption(final ? null : text); },
    line: (who, text) => {
      setRtLines((current) => [...current, { id: nextLine++, who, text }]);
      liveThread.publish({ kind: "said", who, text });
    },
    notice: setRtNotice,
    step: setRtStep,
    blocks: (blocks) => {
      const shown = blocks as CommandCenterBlock[];
      setRtVisuals(shown);
      liveThread.publish({ kind: "shown", blocks: shown });
    },
    open: (label, href) => {
      if (!isOwnScreen(href)) return;
      setRtOffers((current) => (current.some((offer) => offer.href === href) ? current : [...current.slice(-3), { label, href }]));
    },
    conversation: (id) => liveThread.setConversationId(id),
    ended: (reason) => {
      setRtCaption(null);
      setRtLevel(0);
      if (reason === "ended") {
        setRtOpen(false);
        cueEnd();
      } else {
        setRtFailed(true);
      }
    },
  }), []);

  const beginRealtime = useCallback(async () => {
    setConsentOpen(false);
    setMode("realtime");
    setRtOpen(true);
    setRtFailed(false);
    setRtLines([]);
    setRtVisuals([]);
    setRtOffers([]);
    setRtCaption(null);
    setRtNotice(null);
    setRtMuted(false);
    unlockAudio();
    const api = createVoiceApi(role);
    const instance = new RealtimeVoiceEngine(
      {
        api,
        createPeer: () => new RTCPeerConnection({ iceServers: [] }) as never,
        getMicrophone: () => navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } }) as Promise<MicrophoneLike>,
        playRemote: (stream) => {
          const element = document.createElement("audio");
          element.autoplay = true;
          element.setAttribute("playsinline", "");
          element.srcObject = stream as MediaStream;
          document.body.appendChild(element);
          void element.play().catch(() => undefined);
          return () => { element.srcObject = null; element.remove(); };
        },
        meter: (stream, onLevel) => {
          const context = new AudioContext();
          const analyser = context.createAnalyser();
          analyser.fftSize = 512;
          context.createMediaStreamSource(stream as MediaStream).connect(analyser);
          const data = new Uint8Array(analyser.fftSize);
          let frame = 0;
          const tick = () => {
            analyser.getByteTimeDomainData(data);
            let sum = 0;
            for (let i = 0; i < data.length; i += 1) { const v = (data[i] - 128) / 128; sum += v * v; }
            onLevel(Math.min(1, Math.sqrt(sum / data.length) * 4));
            frame = requestAnimationFrame(tick);
          };
          frame = requestAnimationFrame(tick);
          return () => { cancelAnimationFrame(frame); void context.close(); };
        },
        now: () => performance.now(),
        timeZoneOffsetMinutes: -new Date().getTimezoneOffset(),
      },
      handlers,
    );
    engine.current = instance;
    cueStart();
    await instance.start(liveThread.conversationId);
    if (locked) void instance.setLocked(true);
  }, [handlers, locked, role]);

  useEffect(() => { void engine.current?.setLocked(locked); }, [locked]);
  useEffect(() => () => { void engine.current?.end(); }, []);

  // ---- the one screen -------------------------------------------------------------------------

  const realtimePossible = Boolean(status?.available) && typeof RTCPeerConnection !== "undefined";
  const turnPossible = Boolean(status?.turnBasedAvailable);
  const otherLanguages = turnPossible;

  const startCall = useCallback(() => {
    unlockAudio();
    const wantsOther = language !== "en";
    if (wantsOther && !turnPossible) {
      // Luganda and Swahili need the speech provider; say so and carry on in English if that can be done.
      setLanguage("en");
    }
    const useRealtime = realtimePossible && (language === "en" || !turnPossible);
    if (useRealtime) {
      if (readStored(CONSENT_KEY) === "yes") {
        void beginRealtime();
      } else {
        setMode("realtime");
        setRtOpen(true);
        setRtFailed(false);
        setConsentOpen(true);
      }
      return;
    }
    if (turnPossible) {
      setMode("turns");
      void turns.start();
    }
  }, [beginRealtime, language, realtimePossible, turnPossible, turns]);

  const endCall = useCallback(() => {
    setConsentOpen(false);
    if (mode === "realtime") {
      const running = engine.current;
      if (running?.isActive) {
        void running.end();
      } else {
        setRtOpen(false);
      }
      setRtFailed(false);
    } else {
      turns.end();
    }
  }, [mode, turns]);

  const muteCall = useCallback(() => {
    if (mode === "realtime") {
      const next = !rtMuted;
      setRtMuted(next);
      engine.current?.setMuted(next);
    } else {
      turns.toggleMute();
    }
  }, [mode, rtMuted, turns]);

  const chooseLanguage = useCallback((next: LiveLanguage) => {
    if (next === language) return;
    setLanguage(next);
    writeStored(LANGUAGE_KEY, next);
    // A call in the other way of talking is ended and started again in the new language.
    if (mode === "realtime" && engine.current?.isActive) {
      restartAfterEnd.current = true;
      void engine.current.end();
    } else if (mode === "turns" && turns.open) {
      restartAfterEnd.current = true;
      turns.end();
    }
  }, [language, mode, turns]);

  const acceptConsent = useCallback(() => {
    writeStored(CONSENT_KEY, "yes");
    void beginRealtime();
  }, [beginRealtime]);

  const realtime = mode === "realtime";
  const open = realtime ? rtOpen : turns.open;

  useEffect(() => {
    if (!open && restartAfterEnd.current) {
      restartAfterEnd.current = false;
      startCall();
    }
  }, [open, startCall]);
  const callMuted = realtime ? rtMuted : turns.muted;

  // The Android app keeps a call going with the phone in a pocket: tell it when a call is open and whether it is muted,
  // and act on Mute and Hang up pressed in its notification.
  useEffect(() => {
    if (open) nativeCall.started();
    else nativeCall.ended();
  }, [open]);
  useEffect(() => { if (open) nativeCall.muted(callMuted); }, [open, callMuted]);
  const callActions = useRef({ mute: muteCall, hangUp: endCall });
  callActions.current = { mute: muteCall, hangUp: endCall };
  useEffect(
    () => onNativeCallAction({ mute: () => callActions.current.mute(), hangUp: () => callActions.current.hangUp() }),
    [],
  );
  const lastStep = steps.length > 0 ? steps[steps.length - 1].label : null;

  // The Android app hands the page a narrow credential so it can check for nudges while the app is closed and listen for
  // "Hey Kato". Asked for once, when the phone holds none; a credential the server no longer knows is cleared by the app,
  // which brings the page back here to ask again.
  useEffect(() => {
    if (!offered || !nativeDevice.available() || nativeDevice.hasCredential()) return;
    let cancelled = false;
    void registerDevice(role).then((token) => {
      if (cancelled || !token) return;
      nativeDevice.saveCredential(token, apiBaseUrl || window.location.origin);
    });
    return () => { cancelled = true; };
  }, [offered, role]);

  // A conversation the phone asked for: the wake phrase, or Talk now on a nudge. It starts as soon as voice is known to
  // be ready, and a wake the server decides was not "Hey Kato" ends it again quietly.
  const [talkAsked, setTalkAsked] = useState(false);
  useEffect(() => {
    if (nativeDevice.takeTalk()) setTalkAsked(true);
    return onNativeTalk(() => { if (nativeDevice.takeTalk()) setTalkAsked(true); });
  }, []);
  const wakeWatch = useRef<number | null>(null);
  useEffect(() => {
    if (!talkAsked || !offered || status === undefined) return;
    setTalkAsked(false);
    if (open || (!realtimePossible && !turnPossible)) return;
    startCall();
    if (nativeDevice.wakeVerdict() === "yes") return;
    const began = Date.now();
    if (wakeWatch.current !== null) window.clearInterval(wakeWatch.current);
    wakeWatch.current = window.setInterval(() => {
      const verdict = nativeDevice.wakeVerdict();
      // Still undecided after a few seconds counts as yes: a slow network must not cut off someone who did say it.
      if (verdict === "pending" && Date.now() - began < WAKE_WAIT_MILLISECONDS) return;
      if (wakeWatch.current !== null) window.clearInterval(wakeWatch.current);
      wakeWatch.current = null;
      if (verdict === "no") callActions.current.hangUp();
    }, 250);
  }, [offered, open, realtimePossible, startCall, status, talkAsked, turnPossible]);
  useEffect(() => () => { if (wakeWatch.current !== null) window.clearInterval(wakeWatch.current); }, []);

  const unavailableReason = !offered
    ? undefined
    : status === undefined
      ? "Checking whether voice is set up."
      : !realtimePossible && !turnPossible
        ? "Voice is not set up for this workspace. An administrator can connect a speech provider in Settings."
        : undefined;

  const value = useMemo<LiveVoiceValue>(
    () => ({ offered, unavailableReason, active: open, start: startCall }),
    [offered, open, startCall, unavailableReason],
  );

  const visibleOffers = rtOffers;

  return (
    <LiveVoiceContext.Provider value={value}>
      {children}
      {open ? (
        <LiveVoiceSheet
          state={realtime ? rtState : (turns.state as VoiceOrbState)}
          level={realtime ? rtLevel : turns.level}
          turns={realtime ? rtLines : turns.turns}
          caption={realtime ? rtCaption : null}
          notice={realtime ? rtNotice : turns.notice}
          muted={realtime ? rtMuted : turns.muted}
          step={realtime ? rtStep : (turns.step ?? lastStep)}
          visuals={realtime ? rtVisuals : []}
          offers={realtime ? visibleOffers : []}
          language={language}
          otherLanguages={otherLanguages}
          locked={locked}
          consent={consentOpen ? { region: status?.processedIn ?? "Europe", onAccept: acceptConsent } : null}
          canRetry={realtime && rtFailed}
          onLanguage={chooseLanguage}
          onRetry={() => { setRtFailed(false); void beginRealtime(); }}
          onMute={muteCall}
          onEnd={endCall}
        />
      ) : null}
    </LiveVoiceContext.Provider>
  );
}
