"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

/**
 * A voice note from a person using the portal or the IIP app: record, turn into words, show the words.
 * The words go into the message box for the person to read and send, they are never sent on their own,
 * and the recording is not kept anywhere: it is read, transcribed on the server and dropped.
 */

export const MAX_VOICE_NOTE_SECONDS = 60;
const TARGET_RATE = 16_000;

export type VoiceNoteState = "idle" | "recording" | "transcribing";

/** 16-bit mono WAV. Every speech engine reads it, whatever the browser recorded in. */
export function encodeWav(samples: Float32Array, sampleRate: number): Blob {
  const buffer = new ArrayBuffer(44 + samples.length * 2);
  const view = new DataView(buffer);
  const write = (offset: number, text: string) => {
    for (let i = 0; i < text.length; i += 1) view.setUint8(offset + i, text.charCodeAt(i));
  };
  write(0, "RIFF");
  view.setUint32(4, 36 + samples.length * 2, true);
  write(8, "WAVE");
  write(12, "fmt ");
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true); // PCM
  view.setUint16(22, 1, true); // mono
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * 2, true);
  view.setUint16(32, 2, true);
  view.setUint16(34, 16, true);
  write(36, "data");
  view.setUint32(40, samples.length * 2, true);
  for (let i = 0; i < samples.length; i += 1) {
    const clamped = Math.max(-1, Math.min(1, samples[i]));
    view.setInt16(44 + i * 2, clamped < 0 ? clamped * 0x8000 : clamped * 0x7fff, true);
  }
  return new Blob([buffer], { type: "audio/wav" });
}

/** Averages 48 kHz (or 44.1 kHz) microphone samples down to the 16 kHz speech engines expect. */
export function downsampleTo16k(samples: Float32Array, fromRate: number): Float32Array {
  if (fromRate <= TARGET_RATE) return samples;
  const ratio = fromRate / TARGET_RATE;
  const length = Math.floor(samples.length / ratio);
  const out = new Float32Array(length);
  for (let i = 0; i < length; i += 1) {
    const start = Math.floor(i * ratio);
    const end = Math.min(samples.length, Math.floor((i + 1) * ratio));
    let sum = 0;
    for (let j = start; j < end; j += 1) sum += samples[j];
    out[i] = sum / Math.max(1, end - start);
  }
  return out;
}

async function toWav(recording: Blob): Promise<Blob> {
  const context = new AudioContext();
  try {
    const decoded = await context.decodeAudioData(await recording.arrayBuffer());
    const frames = Math.max(1, Math.ceil(decoded.duration * TARGET_RATE));
    const offline = new OfflineAudioContext(1, frames, TARGET_RATE);
    const source = offline.createBufferSource();
    source.buffer = decoded;
    source.connect(offline.destination);
    source.start();
    const rendered = await offline.startRendering();
    return encodeWav(rendered.getChannelData(0), TARGET_RATE);
  } finally {
    void context.close();
  }
}

/** "network" means the request never got an answer, so the same recording is safe to send again. */
export type TranscribeResult = { ok: true; text: string } | { ok: false; message: string; kind: "network" | "refused" };

export async function transcribeVoiceNote(role: string, wav: Blob, language?: string): Promise<TranscribeResult> {
  const form = new FormData();
  form.append("audio", wav, "voice-note.wav");
  if (language) form.append("language", language);
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/command-center/voice-note`, { method: "POST", headers: scopedHeaders(role, ""), body: form });
    if (response.ok) {
      const body = (await response.json()) as { text?: string };
      return body.text ? { ok: true, text: body.text } : { ok: false, kind: "refused", message: "I could not make out any words in that recording. Try again, or type it." };
    }
    if (response.status === 403) return { ok: false, kind: "refused", message: "You do not have permission to use voice notes." };
    const failure = (await response.json().catch(() => null)) as { message?: string } | null;
    return { ok: false, kind: "refused", message: failure?.message ?? "The voice note could not be read just now. Try again, or type it." };
  } catch {
    return { ok: false, kind: "network", message: "The voice note could not be sent. Check your connection and try again." };
  }
}

export function useVoiceNote(role: string, onText: (text: string) => void) {
  const [state, setState] = useState<VoiceNoteState>("idle");
  const [seconds, setSeconds] = useState(0);
  const [level, setLevel] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const recorder = useRef<MediaRecorder | null>(null);
  const stream = useRef<MediaStream | null>(null);
  const chunks = useRef<Blob[]>([]);
  const cancelled = useRef(false);
  const meter = useRef<{ stop: () => void } | null>(null);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);

  const release = useCallback(() => {
    meter.current?.stop();
    meter.current = null;
    if (timer.current) clearInterval(timer.current);
    timer.current = null;
    stream.current?.getTracks().forEach((track) => track.stop());
    stream.current = null;
    setLevel(0);
  }, []);

  useEffect(() => () => { cancelled.current = true; recorder.current?.state === "recording" && recorder.current.stop(); release(); }, [release]);

  // Known only in the browser, so it is set after the first render to keep the server and client markup the same.
  const [supported, setSupported] = useState(false);
  useEffect(() => {
    setSupported(!!navigator.mediaDevices?.getUserMedia && typeof MediaRecorder !== "undefined");
  }, []);

  const start = useCallback(async () => {
    if (state !== "idle") return;
    setError(null);
    if (!supported) {
      setError("This browser cannot record. Type your message instead.");
      return;
    }
    try {
      const media = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } });
      stream.current = media;
      chunks.current = [];
      cancelled.current = false;
      const instance = new MediaRecorder(media);
      recorder.current = instance;
      instance.ondataavailable = (event) => { if (event.data.size > 0) chunks.current.push(event.data); };
      instance.onstop = async () => {
        const wasCancelled = cancelled.current;
        const type = instance.mimeType || "audio/webm";
        release();
        if (wasCancelled) {
          setState("idle");
          return;
        }
        setState("transcribing");
        try {
          const wav = await toWav(new Blob(chunks.current, { type }));
          const result = await transcribeVoiceNote(role, wav);
          if (result.ok) onText(result.text);
          else setError(result.message);
        } catch {
          setError("That recording could not be read. Try again, or type it.");
        }
        setState("idle");
      };

      // A level for the orb: the loudness of the last moment, 0 to 1.
      const context = new AudioContext();
      const analyser = context.createAnalyser();
      analyser.fftSize = 512;
      context.createMediaStreamSource(media).connect(analyser);
      const data = new Uint8Array(analyser.fftSize);
      let frame = 0;
      const tick = () => {
        analyser.getByteTimeDomainData(data);
        let sum = 0;
        for (let i = 0; i < data.length; i += 1) {
          const v = (data[i] - 128) / 128;
          sum += v * v;
        }
        setLevel(Math.min(1, Math.sqrt(sum / data.length) * 4));
        frame = requestAnimationFrame(tick);
      };
      frame = requestAnimationFrame(tick);
      meter.current = { stop: () => { cancelAnimationFrame(frame); void context.close(); } };

      instance.start();
      setSeconds(0);
      setState("recording");
      const startedAt = Date.now();
      timer.current = setInterval(() => {
        const elapsed = Math.floor((Date.now() - startedAt) / 1000);
        setSeconds(elapsed);
        if (elapsed >= MAX_VOICE_NOTE_SECONDS && instance.state === "recording") instance.stop();
      }, 250);
    } catch (failure) {
      release();
      const denied = failure instanceof DOMException && (failure.name === "NotAllowedError" || failure.name === "SecurityError");
      setError(denied ? "The microphone is blocked. Allow it for this site, then try again." : "No microphone was found.");
      setState("idle");
    }
  }, [state, supported, role, onText, release]);

  const stop = useCallback(() => {
    if (recorder.current?.state === "recording") recorder.current.stop();
  }, []);

  const cancel = useCallback(() => {
    cancelled.current = true;
    if (recorder.current?.state === "recording") recorder.current.stop();
    else setState("idle");
  }, []);

  return { state, seconds, level, error, supported, start, stop, cancel, clearError: () => setError(null) };
}

/** Whether this workspace can turn speech into words. null while it is being asked. */
export function useVoiceAvailable(enabled: boolean, role: string): boolean | null {
  const [available, setAvailable] = useState<boolean | null>(null);
  useEffect(() => {
    if (!enabled) return;
    let cancelled = false;
    (async () => {
      try {
        const response = await fetch(`${apiBaseUrl}/api/v1/command-center/voice-note/status`, { headers: scopedHeaders(role, "") });
        const body = response.ok ? ((await response.json()) as { available?: boolean }) : null;
        if (!cancelled) setAvailable(Boolean(body?.available));
      } catch {
        if (!cancelled) setAvailable(false);
      }
    })();
    return () => { cancelled = true; };
  }, [enabled, role]);
  return available;
}
