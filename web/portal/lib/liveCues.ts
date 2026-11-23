"use client";

/**
 * The small sounds and taps that tell a person a live conversation has started or ended. They are cues,
 * not decoration: they confirm the microphone is open without the person having to look. A person can
 * switch them off, and the choice is remembered on this device only.
 */

const KEY = "iip.live.cues";

export function cuesEnabled(): boolean {
  try {
    return window.localStorage.getItem(KEY) !== "off";
  } catch {
    return true;
  }
}

export function setCuesEnabled(on: boolean) {
  try {
    window.localStorage.setItem(KEY, on ? "on" : "off");
  } catch {
    /* storage can be blocked; the cue setting then lasts for this visit only */
  }
}

let context: AudioContext | null = null;

/** Call from a tap. Browsers keep audio locked until a person has touched the page. */
export function unlockAudio() {
  try {
    context ??= new AudioContext();
    if (context.state === "suspended") void context.resume();
  } catch {
    /* no audio available: cues are simply skipped */
  }
}

type Direction = "up" | "down";

// Two sine tones a fifth apart, gliding together: up when the conversation opens, down when it ends.
function chirp(direction: Direction) {
  if (!context) return;
  const now = context.currentTime;
  const length = direction === "up" ? 0.18 : 0.15;
  const peak = direction === "up" ? 0.12 : 0.07;
  const glide: Array<[number, number]> = direction === "up" ? [[440, 880], [880, 1760]] : [[700, 400], [1400, 800]];
  for (const [from, to] of glide) {
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.type = "sine";
    oscillator.frequency.setValueAtTime(from, now);
    oscillator.frequency.exponentialRampToValueAtTime(to, now + length);
    gain.gain.setValueAtTime(0.0001, now);
    gain.gain.exponentialRampToValueAtTime(peak / (from === glide[0][0] ? 1 : 2), now + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + length);
    oscillator.connect(gain).connect(context.destination);
    oscillator.start(now);
    oscillator.stop(now + length + 0.02);
  }
}

type NativeBridge = { haptic?: (milliseconds: number) => void };

function tap(milliseconds: number) {
  try {
    const native = (window as unknown as { IipNative?: NativeBridge }).IipNative;
    if (native?.haptic) native.haptic(milliseconds);
    else navigator.vibrate?.(milliseconds);
  } catch {
    /* a device without vibration simply does not tap */
  }
}

export function cueStart() {
  if (!cuesEnabled()) return;
  chirp("up");
  tap(18);
}

export function cueEnd() {
  if (!cuesEnabled()) return;
  chirp("down");
  tap(10);
}
