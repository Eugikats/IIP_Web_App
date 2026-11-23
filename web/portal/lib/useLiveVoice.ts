"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { OrbState } from "@/components/iip/AgentOrb";
import { cueEnd, cueStart, unlockAudio } from "@/lib/liveCues";
import { downsampleTo16k, encodeWav, transcribeVoiceNote } from "@/lib/voiceNote";
import { fetchSpokenReply } from "@/lib/voice/voiceApi";
import type { CommandCenterQueryResult } from "@/lib/lovable-parity/data/commandCenterQuery";

/**
 * A spoken conversation with the agent, a turn at a time: the person speaks, the agent writes the words
 * down, works out the answer with the same tools and permissions as typing, and says it aloud. The person
 * can speak over it to interrupt. The answer is read, shown and prepared; the person decides, exactly as
 * in the written conversation, and nothing here acts on its own.
 *
 * The turn-based engine below needs no extra service. A realtime voice engine can replace it behind the
 * same hook without the screen changing.
 */

export type LiveTurn = { id: number; who: "you" | "iip"; text: string };

export type LiveLanguage = "en" | "lg" | "sw";

const LOCKED_REPLY = "The phone is locked, so I have not looked anything up or read anything out. Unlock it and ask again.";

const LANGUAGE_NAME: Record<LiveLanguage, string> = { en: "English", lg: "Luganda", sw: "Swahili" };

type Ask = (question: string) => Promise<CommandCenterQueryResult>;

const FRAME = 2048;
const PREROLL_MS = 350;
const END_SILENCE_MS = 900;
const MIN_SPEECH_MS = 400;
const MAX_SPEECH_MS = 30_000;
const MAX_SPOKEN_CHARS = 420;

/** The words of an answer worth reading aloud: its first written block, without markup. */
export function spokenText(blocks: Array<{ type: string; markdown?: unknown }>): string | null {
  const block = blocks.find((candidate) => candidate.type === "text" && typeof candidate.markdown === "string");
  if (!block || typeof block.markdown !== "string") return null;
  const plain = block.markdown.replace(/[*_`#>]+/g, "").replace(/\[([^\]]+)\]\([^)]*\)/g, "$1").replace(/\s+/g, " ").trim();
  if (!plain) return null;
  if (plain.length <= MAX_SPOKEN_CHARS) return plain;
  const cut = plain.slice(0, MAX_SPOKEN_CHARS);
  const stop = Math.max(cut.lastIndexOf(". "), cut.lastIndexOf("? "), cut.lastIndexOf("! "));
  return stop > 80 ? cut.slice(0, stop + 1) : cut;
}

export function useLiveVoice({
  role,
  ask,
  onExchange,
  language = "en",
  locked = false,
}: {
  role: string;
  ask: Ask;
  /** The language of the conversation. English is spoken by the device; Luganda and Swahili are translated and spoken by the server. */
  language?: LiveLanguage;
  /** While the phone is locked nothing is looked up and nothing is read out. */
  locked?: boolean;
  /** Called once a question and its answer are complete, so the written conversation can show them too. */
  onExchange: (question: string, answer: Extract<CommandCenterQueryResult, { ok: true }>["answer"]) => void;
}) {
  const [open, setOpen] = useState(false);
  const [state, setStateValue] = useState<OrbState>("idle");
  const [level, setLevel] = useState(0);
  const [turns, setTurns] = useState<LiveTurn[]>([]);
  const [notice, setNotice] = useState<string | null>(null);
  const [muted, setMuted] = useState(false);
  const [localStep, setLocalStep] = useState<string | null>(null);
  const languageRef = useRef<LiveLanguage>(language);
  const lockedRef = useRef(locked);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const stateRef = useRef<OrbState>("idle");
  const mutedRef = useRef(false);
  const askRef = useRef(ask);
  const exchangeRef = useRef(onExchange);
  const turnId = useRef(1);
  const engine = useRef<{
    context: AudioContext;
    stream: MediaStream;
    processor: ScriptProcessorNode;
    source: MediaStreamAudioSourceNode;
  } | null>(null);
  const vad = useRef({ inSpeech: false, voicedRun: 0, bargeRun: 0, silenceMs: 0, floor: 0.01, preroll: [] as Float32Array[], utterance: [] as Float32Array[] });

  useEffect(() => { askRef.current = ask; exchangeRef.current = onExchange; languageRef.current = language; lockedRef.current = locked; });

  const setState = useCallback((next: OrbState) => {
    stateRef.current = next;
    setStateValue(next);
  }, []);

  const addTurn = useCallback((who: LiveTurn["who"], text: string) => {
    setTurns((current) => [...current, { id: turnId.current++, who, text }]);
  }, []);

  const stopEngine = useCallback(() => {
    const running = engine.current;
    engine.current = null;
    if (running) {
      running.processor.onaudioprocess = null;
      running.processor.disconnect();
      running.source.disconnect();
      running.stream.getTracks().forEach((track) => track.stop());
      void running.context.close();
    }
    window.speechSynthesis?.cancel();
    audioRef.current?.pause();
    audioRef.current = null;
    setLocalStep(null);
    setLevel(0);
  }, []);

  useEffect(() => () => stopEngine(), [stopEngine]);

  const listen = useCallback(() => {
    const v = vad.current;
    v.inSpeech = false;
    v.voicedRun = 0;
    v.bargeRun = 0;
    v.silenceMs = 0;
    v.utterance = [];
    setState(mutedRef.current ? "muted" : "listening");
  }, [setState]);

  /** A reply already turned into Luganda or Swahili speech by the server. */
  const playAudio = useCallback((audio: string, mimeType: string) => {
    const element = new Audio(`data:${mimeType};base64,${audio}`);
    audioRef.current = element;
    const done = () => { if (audioRef.current === element) audioRef.current = null; if (stateRef.current === "speaking" || stateRef.current === "processing") listen(); };
    element.onended = done;
    element.onerror = done;
    setState("speaking");
    element.play().catch(done);
  }, [listen, setState]);

  const speak = useCallback((text: string | null) => {
    const synth = typeof window !== "undefined" ? window.speechSynthesis : undefined;
    if (!text || !synth) {
      if (text && !synth) setNotice("Spoken replies are not available on this device. The answer is shown instead.");
      listen();
      return;
    }
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.onstart = () => { if (stateRef.current === "processing") setState("speaking"); };
    const done = () => { if (stateRef.current === "speaking" || stateRef.current === "processing") listen(); };
    utterance.onend = done;
    utterance.onerror = done;
    synth.cancel();
    setState("speaking");
    synth.speak(utterance);
  }, [listen, setState]);

  const handleUtterance = useCallback(async (samples: Float32Array, sampleRate: number) => {
    setState("processing");
    setNotice(null);
    const wav = encodeWav(downsampleTo16k(samples, sampleRate), 16_000);

    let words: string | null = null;
    for (let attempt = 0; attempt < 3 && words === null; attempt += 1) {
      const heard = await transcribeVoiceNote(role, wav, languageRef.current === "en" ? undefined : languageRef.current);
      if (heard.ok) {
        words = heard.text;
      } else if (heard.kind === "network" && attempt < 2) {
        setState("reconnecting");
        await new Promise((resolve) => setTimeout(resolve, 1500));
        if (!engine.current) return;
        setState("processing");
      } else {
        setNotice(heard.message);
        listen();
        return;
      }
    }
    if (!engine.current || words === null) return;

    addTurn("you", words);
    if (lockedRef.current) {
      // Nothing is looked up while the phone is locked: the question is not sent, so no figure is fetched to be read out.
      addTurn("iip", LOCKED_REPLY);
      listen();
      return;
    }
    const result = await askRef.current(words);
    if (!engine.current) return;
    if (!result.ok) {
      setNotice(result.message);
      listen();
      return;
    }
    const reply = spokenText(result.answer.blocks);
    exchangeRef.current(words, result.answer);
    const chosen = languageRef.current;
    if (chosen !== "en" && reply) {
      // The answer is written in English. It is translated before it is spoken, so a Luganda voice never reads English words.
      setLocalStep(`Preparing the reply in ${LANGUAGE_NAME[chosen]}`);
      const spoken = await fetchSpokenReply(role, reply, chosen);
      setLocalStep(null);
      if (!engine.current) return;
      if (spoken?.translated) {
        addTurn("iip", spoken.text);
        if (spoken.audio && spoken.mimeType) {
          playAudio(spoken.audio, spoken.mimeType);
        } else {
          setNotice(`No ${LANGUAGE_NAME[chosen]} voice is available, so the reply is shown instead.`);
          listen();
        }
      } else {
        addTurn("iip", reply);
        setNotice(`I could not turn that reply into ${LANGUAGE_NAME[chosen]}, so it is shown in English and not spoken.`);
        listen();
      }
      return;
    }
    addTurn("iip", reply ?? "I have put the answer on your screen.");
    speak(reply);
  }, [addTurn, listen, playAudio, role, setState, speak]);

  const onFrame = useCallback((input: Float32Array, sampleRate: number) => {
    const v = vad.current;
    const frame = new Float32Array(input);
    let sum = 0;
    for (let i = 0; i < frame.length; i += 1) sum += frame[i] * frame[i];
    const rms = Math.sqrt(sum / frame.length);
    const frameMs = (frame.length / sampleRate) * 1000;
    const current = stateRef.current;

    if (mutedRef.current || current === "processing" || current === "reconnecting") {
      setLevel(0);
      return;
    }

    const voiced = rms > Math.max(0.015, v.floor * 3);
    setLevel(current === "listening" ? Math.min(1, rms * 5) : 0);

    const keepPreroll = () => {
      v.preroll.push(frame);
      let total = v.preroll.length * frameMs;
      while (total > PREROLL_MS && v.preroll.length > 1) {
        v.preroll.shift();
        total -= frameMs;
      }
    };

    if (current === "speaking") {
      // Speaking over the agent interrupts it. Echo cancellation keeps its own voice from counting.
      v.bargeRun = voiced ? v.bargeRun + 1 : 0;
      keepPreroll();
      if (v.bargeRun >= 3) {
        window.speechSynthesis?.cancel();
        audioRef.current?.pause();
        audioRef.current = null;
        setState("interrupted");
        v.inSpeech = true;
        v.silenceMs = 0;
        v.utterance = [...v.preroll];
        window.setTimeout(() => { if (stateRef.current === "interrupted") setState("listening"); }, 420);
      }
      return;
    }

    if (!v.inSpeech) {
      keepPreroll();
      if (voiced) {
        v.voicedRun += 1;
        if (v.voicedRun >= 2) {
          v.inSpeech = true;
          v.silenceMs = 0;
          v.utterance = [...v.preroll];
        }
      } else {
        v.voicedRun = 0;
        v.floor = v.floor * 0.95 + rms * 0.05;
      }
      return;
    }

    v.utterance.push(frame);
    v.silenceMs = voiced ? 0 : v.silenceMs + frameMs;
    const spokenMs = v.utterance.length * frameMs;
    if (v.silenceMs >= END_SILENCE_MS || spokenMs >= MAX_SPEECH_MS) {
      const collected = v.utterance;
      v.inSpeech = false;
      v.utterance = [];
      v.preroll = [];
      if (spokenMs - v.silenceMs < MIN_SPEECH_MS) {
        listen();
        return;
      }
      const all = new Float32Array(collected.reduce((total, part) => total + part.length, 0));
      let offset = 0;
      for (const part of collected) {
        all.set(part, offset);
        offset += part.length;
      }
      void handleUtterance(all, sampleRate);
    }
  }, [handleUtterance, listen, setState]);

  const start = useCallback(async () => {
    if (engine.current) return;
    unlockAudio();
    setNotice(null);
    setTurns([]);
    mutedRef.current = false;
    setMuted(false);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } });
      const context = new AudioContext();
      const source = context.createMediaStreamSource(stream);
      const processor = context.createScriptProcessor(FRAME, 1, 1);
      processor.onaudioprocess = (event) => onFrame(event.inputBuffer.getChannelData(0), context.sampleRate);
      source.connect(processor);
      processor.connect(context.destination);
      engine.current = { context, stream, processor, source };
      vad.current = { inSpeech: false, voicedRun: 0, bargeRun: 0, silenceMs: 0, floor: 0.01, preroll: [], utterance: [] };
      setOpen(true);
      setState("listening");
      cueStart();
    } catch (failure) {
      const denied = failure instanceof DOMException && (failure.name === "NotAllowedError" || failure.name === "SecurityError");
      setNotice(denied ? "The microphone is blocked. Allow it for this site, then try again." : "No microphone was found.");
      setOpen(false);
    }
  }, [onFrame, setState]);

  const end = useCallback(() => {
    const wasOpen = engine.current !== null;
    stopEngine();
    setOpen(false);
    setState("idle");
    if (wasOpen) cueEnd();
  }, [setState, stopEngine]);

  const toggleMute = useCallback(() => {
    const next = !mutedRef.current;
    mutedRef.current = next;
    setMuted(next);
    engine.current?.stream.getAudioTracks().forEach((track) => { track.enabled = !next; });
    if (next) {
      window.speechSynthesis?.cancel();
      audioRef.current?.pause();
      audioRef.current = null;
      setState("muted");
    } else if (stateRef.current === "muted") {
      listen();
    }
  }, [listen, setState]);

  return { open, state, level, turns, notice, muted, step: localStep, start, end, toggleMute };
}
