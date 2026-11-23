"use client";

/**
 * The agent's orb and the word that goes with it. Every state is written out as well as drawn, so the
 * motion is never the only signal. Styles live in app/globals.css (.iip-orb) with the rest of the
 * design tokens; the orb gradient is the one gradient the design language allows here.
 */

export type OrbState = "idle" | "listening" | "speaking" | "processing" | "interrupted" | "muted" | "reconnecting";

export const ORB_LABEL: Record<OrbState, string> = {
  idle: "Ready",
  listening: "Listening",
  speaking: "Speaking",
  processing: "Working on it",
  interrupted: "Go ahead",
  muted: "Muted",
  reconnecting: "Reconnecting",
};

export default function AgentOrb({
  state,
  size = 40,
  level = 0,
  className = "",
}: {
  state: OrbState;
  size?: number;
  /** 0 to 1, how loud the person is right now. Only read while listening. */
  level?: number;
  className?: string;
}) {
  return (
    <span
      className={`iip-orb ${className}`}
      data-state={state}
      data-testid="agent-orb"
      style={{ ["--orb-size" as string]: `${size}px`, ["--orb-level" as string]: Math.max(0, Math.min(1, level)) }}
      aria-hidden="true"
    >
      <i className="iip-orb__ring" />
      <i className="iip-orb__ring" />
      <i className="iip-orb__ring" />
      <i className="iip-orb__core" />
      <i className="iip-orb__slash" />
    </span>
  );
}
